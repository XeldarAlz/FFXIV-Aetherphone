using System.Numerics;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Playback;
using Aetherphone.Core.Telephony.Audio;
using Aetherphone.Core.Telephony.Contracts;

namespace Aetherphone.Core.Telephony;

internal sealed class CallHub : IDisposable
{
    private const float RingIntervalSeconds = 3f;
    private const float IncomingTimeoutSeconds = 60f;
    private const float DialingTimeoutSeconds = 60f;
    private const int MaxRecents = 8;

    private static readonly Vector4 Accent = new(0.20f, 0.78f, 0.35f, 1f);

    private readonly Configuration configuration;
    private readonly AethernetSession session;
    private readonly NotificationService notifications;
    private readonly IRingtone ringtone;
    private readonly PlaybackHub playback;
    private readonly RealtimeConnection connection;

    private readonly object gate = new();
    private readonly HashSet<int> remoteSlots = new();

    private CallState state = CallState.Idle;
    private Guid callId;
    private ParticipantInfo[] roster = Array.Empty<ParticipantInfo>();
    private ParticipantInfo? incomingFrom;
    private CallContact? dialingTo;
    private CallSession? activeSession;
    private bool muted;
    private float volume = 0.85f;
    private long callStartTicks;
    private float ringTimer;
    private float stateTimer;
    private CallContact[] recents = Array.Empty<CallContact>();

    public CallHub(Configuration configuration, AethernetSession session, NotificationService notifications, IRingtone ringtone, PlaybackHub playback)
    {
        this.configuration = configuration;
        this.session = session;
        this.notifications = notifications;
        this.ringtone = ringtone;
        this.playback = playback;
        connection = new RealtimeConnection(session);
    }

    public event Action? IncomingCallPresented;

    public bool Enabled => configuration.CallsEnabled;

    public bool SignedIn => session.IsSignedIn;

    public bool Connected => connection.Connected;

    public string LocalUserId => session.CurrentUser?.Id ?? string.Empty;

    public CallContact[] Recents => recents;

    public void Start()
    {
        connection.ControlReceived += OnControl;
        connection.ConnectedChanged += OnConnectedChanged;
        session.Changed += Reconcile;
        Reconcile();
    }

    public void SetEnabled(bool value)
    {
        configuration.CallsEnabled = value;
        configuration.Save();
        Reconcile();
    }

    public CallView Snapshot()
    {
        lock (gate)
        {
            var localId = LocalUserId;
            var others = CountOthers(localId);
            var seconds = state == CallState.Active && callStartTicks != 0
                ? (int)((Environment.TickCount64 - callStartTicks) / 1000)
                : 0;

            return new CallView(
                state,
                muted,
                volume,
                activeSession?.MicLevel ?? 0f,
                seconds,
                roster,
                incomingFrom,
                connection.Connected,
                localId,
                BuildPeerLabel(localId),
                others);
        }
    }

    public float LevelOf(ParticipantInfo participant)
    {
        CallSession? current;
        lock (gate)
        {
            current = activeSession;
        }

        return current?.LevelOf(participant.Slot) ?? 0f;
    }

    public void StartCall(CallContact target)
    {
        if (!configuration.CallsEnabled || !session.IsSignedIn)
        {
            return;
        }

        Guid id;
        lock (gate)
        {
            if (state != CallState.Idle)
            {
                return;
            }

            id = Guid.NewGuid();
            callId = id;
            state = CallState.Dialing;
            dialingTo = target;
            incomingFrom = null;
            roster = Array.Empty<ParticipantInfo>();
            stateTimer = 0f;
        }

        AddRecent(target);
        Send(new CallControl
        {
            Type = SignalType.Start,
            CallId = id.ToString("D"),
            InviteeIds = new[] { target.UserId },
        });
    }

    public void AddParticipant(CallContact target)
    {
        string? id;
        lock (gate)
        {
            if (state is not CallState.Active and not CallState.Dialing)
            {
                return;
            }

            id = callId.ToString("D");
        }

        AddRecent(target);
        Send(new CallControl
        {
            Type = SignalType.Invite,
            CallId = id,
            InviteeIds = new[] { target.UserId },
        });
    }

    public void Accept()
    {
        string? id;
        lock (gate)
        {
            if (state != CallState.Ringing)
            {
                return;
            }

            state = CallState.Connecting;
            id = callId.ToString("D");
        }

        Send(new CallControl { Type = SignalType.Accept, CallId = id });
    }

    public void Decline()
    {
        string? id;
        lock (gate)
        {
            if (state != CallState.Ringing)
            {
                return;
            }

            id = callId.ToString("D");
        }

        Send(new CallControl { Type = SignalType.Decline, CallId = id });
        EndCall(notify: false, reason: null);
    }

    public void Hangup()
    {
        string? id;
        lock (gate)
        {
            if (state == CallState.Idle)
            {
                return;
            }

            id = callId.ToString("D");
        }

        Send(new CallControl { Type = SignalType.Leave, CallId = id });
        EndCall(notify: false, reason: null);
    }

    public void ToggleMute()
    {
        lock (gate)
        {
            muted = !muted;
            if (activeSession is not null)
            {
                activeSession.Muted = muted;
            }
        }
    }

    public void SetVolume(float value)
    {
        lock (gate)
        {
            volume = Math.Clamp(value, 0f, 1f);
            if (activeSession is not null)
            {
                activeSession.Volume = volume;
            }
        }
    }

    public void Advance(float deltaSeconds)
    {
        var ring = false;
        var declineTimeout = false;
        var dialTimeout = false;

        lock (gate)
        {
            if (state == CallState.Ringing)
            {
                stateTimer += deltaSeconds;
                ringTimer += deltaSeconds;
                if (ringTimer >= RingIntervalSeconds)
                {
                    ringTimer = 0f;
                    ring = true;
                }

                if (stateTimer >= IncomingTimeoutSeconds)
                {
                    declineTimeout = true;
                }
            }
            else if (state == CallState.Dialing)
            {
                stateTimer += deltaSeconds;
                if (stateTimer >= DialingTimeoutSeconds)
                {
                    dialTimeout = true;
                }
            }
        }

        if (ring)
        {
            ringtone.Play();
        }

        if (declineTimeout)
        {
            Decline();
        }
        else if (dialTimeout)
        {
            Hangup();
            notifications.Notify(new PhoneNotification("phone", "No answer", "The call was not answered", DateTime.Now, Accent));
        }
    }

    private void OnControl(CallControl message)
    {
        switch (message.Type)
        {
            case SignalType.Incoming:
                HandleIncoming(message);
                break;
            case SignalType.Roster:
                HandleRoster(message);
                break;
            case SignalType.Declined:
                HandleDeclined(message);
                break;
            case SignalType.Unavailable:
                HandleUnavailable(message);
                break;
            case SignalType.Ended:
                HandleEnded(message);
                break;
        }
    }

    private void HandleIncoming(CallControl message)
    {
        if (!Guid.TryParse(message.CallId, out var id) || message.From is null)
        {
            return;
        }

        var busy = false;
        lock (gate)
        {
            if (state != CallState.Idle)
            {
                busy = true;
            }
            else
            {
                callId = id;
                state = CallState.Ringing;
                incomingFrom = message.From;
                roster = message.Participants ?? new[] { message.From };
                stateTimer = 0f;
                ringTimer = RingIntervalSeconds;
            }
        }

        if (busy)
        {
            Send(new CallControl { Type = SignalType.Decline, CallId = message.CallId, Reason = "busy" });
            return;
        }

        ringtone.Play();
        notifications.Notify(new PhoneNotification("phone", message.From.DisplayName, "Incoming call", DateTime.Now, Accent));
        IncomingCallPresented?.Invoke();
    }

    private void HandleRoster(CallControl message)
    {
        if (!Guid.TryParse(message.CallId, out var id))
        {
            return;
        }

        var participants = message.Participants ?? Array.Empty<ParticipantInfo>();
        CallSession? sessionToDispose = null;
        var localId = LocalUserId;

        lock (gate)
        {
            if (id != callId)
            {
                return;
            }

            roster = participants;

            var localSlot = -1;
            var activeOthers = 0;
            for (var index = 0; index < participants.Length; index++)
            {
                var participant = participants[index];
                if (participant.UserId == localId)
                {
                    localSlot = participant.Slot;
                }
                else if (participant.State == ParticipantState.Active)
                {
                    activeOthers++;
                }
            }

            if (activeOthers > 0)
            {
                if (activeSession is null)
                {
                    StartSessionLocked(localSlot);
                }
                else if (localSlot >= 0)
                {
                    activeSession.SetLocalSlot(localSlot);
                }

                SyncRemotesLocked(participants, localId);
                state = CallState.Active;
            }
            else if (activeSession is not null)
            {
                sessionToDispose = activeSession;
                ClearLocked();
            }
        }

        sessionToDispose?.Dispose();
    }

    private void HandleDeclined(CallControl message)
    {
        if (!Guid.TryParse(message.CallId, out var id))
        {
            return;
        }

        var ended = false;
        var localId = LocalUserId;
        lock (gate)
        {
            if (id != callId || state != CallState.Dialing)
            {
                return;
            }

            var pending = 0;
            for (var index = 0; index < roster.Length; index++)
            {
                var participant = roster[index];
                if (participant.UserId != localId && participant.State != ParticipantState.Left)
                {
                    pending++;
                }
            }

            if (pending == 0)
            {
                ended = true;
            }
        }

        if (ended)
        {
            EndCall(notify: true, reason: "Call declined");
        }
    }

    private void HandleUnavailable(CallControl message)
    {
        if (!Guid.TryParse(message.CallId, out var id))
        {
            return;
        }

        var matched = false;
        lock (gate)
        {
            matched = id == callId && state == CallState.Dialing;
        }

        if (matched)
        {
            EndCall(notify: true, reason: "Unavailable");
        }
    }

    private void HandleEnded(CallControl message)
    {
        if (!Guid.TryParse(message.CallId, out var id))
        {
            return;
        }

        var matched = false;
        lock (gate)
        {
            matched = id == callId && state != CallState.Idle;
        }

        if (matched)
        {
            EndCall(notify: false, reason: null);
        }
    }

    private void OnConnectedChanged(bool isConnected)
    {
        if (isConnected)
        {
            return;
        }

        var inCall = false;
        lock (gate)
        {
            inCall = state != CallState.Idle;
        }

        if (inCall)
        {
            EndCall(notify: true, reason: "Connection lost");
        }
    }

    private void StartSessionLocked(int localSlot)
    {
        var input = AudioDevices.ResolveInput(configuration.CallInputDevice);
        var output = AudioDevices.ResolveOutput(configuration.CallOutputDevice);
        var created = new CallSession(callId, connection, input, output, volume)
        {
            Muted = muted,
        };

        if (localSlot >= 0)
        {
            created.SetLocalSlot(localSlot);
        }

        activeSession = created;
        remoteSlots.Clear();
        callStartTicks = Environment.TickCount64;
        playback.Stop();
    }

    private void SyncRemotesLocked(ParticipantInfo[] participants, string localId)
    {
        if (activeSession is null)
        {
            return;
        }

        Span<int> present = participants.Length <= 16 ? stackalloc int[participants.Length] : new int[participants.Length];
        var presentCount = 0;
        for (var index = 0; index < participants.Length; index++)
        {
            var participant = participants[index];
            if (participant.UserId == localId || participant.State != ParticipantState.Active)
            {
                continue;
            }

            present[presentCount++] = participant.Slot;
            if (remoteSlots.Add(participant.Slot))
            {
                activeSession.AddRemote(participant.Slot);
            }
        }

        if (remoteSlots.Count == presentCount)
        {
            return;
        }

        var stale = new List<int>();
        foreach (var slot in remoteSlots)
        {
            var found = false;
            for (var index = 0; index < presentCount; index++)
            {
                if (present[index] == slot)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                stale.Add(slot);
            }
        }

        for (var index = 0; index < stale.Count; index++)
        {
            activeSession.RemoveRemote(stale[index]);
            remoteSlots.Remove(stale[index]);
        }
    }

    private void EndCall(bool notify, string? reason)
    {
        CallSession? toDispose;
        lock (gate)
        {
            if (state == CallState.Idle)
            {
                return;
            }

            toDispose = activeSession;
            ClearLocked();
        }

        toDispose?.Dispose();

        if (notify && reason is not null)
        {
            notifications.Notify(new PhoneNotification("phone", reason, "Call ended", DateTime.Now, Accent));
        }
    }

    private void ClearLocked()
    {
        state = CallState.Idle;
        callId = default;
        roster = Array.Empty<ParticipantInfo>();
        incomingFrom = null;
        dialingTo = null;
        activeSession = null;
        remoteSlots.Clear();
        muted = false;
        callStartTicks = 0;
        ringTimer = 0f;
        stateTimer = 0f;
    }

    private int CountOthers(string localId)
    {
        var count = 0;
        for (var index = 0; index < roster.Length; index++)
        {
            if (roster[index].UserId != localId)
            {
                count++;
            }
        }

        return count;
    }

    private string BuildPeerLabel(string localId)
    {
        switch (state)
        {
            case CallState.Dialing:
                return dialingTo?.DisplayName ?? "Calling…";
            case CallState.Ringing:
                return incomingFrom?.DisplayName ?? "Incoming call";
            case CallState.Connecting:
                return "Connecting…";
            case CallState.Active:
                ParticipantInfo? only = null;
                var others = 0;
                for (var index = 0; index < roster.Length; index++)
                {
                    if (roster[index].UserId != localId)
                    {
                        others++;
                        only = roster[index];
                    }
                }

                return others == 1 && only is not null ? only.DisplayName : "Group call";
            default:
                return string.Empty;
        }
    }

    private void AddRecent(CallContact contact)
    {
        lock (gate)
        {
            var list = new List<CallContact>(recents.Length + 1) { contact };
            for (var index = 0; index < recents.Length && list.Count < MaxRecents; index++)
            {
                if (recents[index].UserId != contact.UserId)
                {
                    list.Add(recents[index]);
                }
            }

            recents = list.ToArray();
        }
    }

    private void Reconcile()
    {
        if (configuration.CallsEnabled && session.IsSignedIn)
        {
            connection.Start();
        }
        else
        {
            EndCall(notify: false, reason: null);
            connection.Stop();
        }
    }

    private void Send(CallControl control)
    {
        _ = connection.SendControlAsync(control);
    }

    public void Dispose()
    {
        connection.ControlReceived -= OnControl;
        connection.ConnectedChanged -= OnConnectedChanged;
        session.Changed -= Reconcile;

        CallSession? toDispose;
        lock (gate)
        {
            toDispose = activeSession;
            activeSession = null;
        }

        toDispose?.Dispose();
        connection.Dispose();
    }
}
