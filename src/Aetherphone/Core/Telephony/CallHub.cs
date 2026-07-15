using System.Numerics;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Analytics;
using Aetherphone.Core.Localization;
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
    private const long ReconnectGraceMs = 20_000;
    private const int MaxCallLog = 50;
    private static readonly Vector4 Accent = new(0.20f, 0.78f, 0.35f, 1f);
    private readonly Configuration configuration;
    private readonly AethernetSession session;
    private readonly NotificationService notifications;
    private readonly SoundService sound;
    private readonly PlaybackHub playback;
    private readonly RealtimeSignalBus signals;
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
    private long connectionLostTicks;
    private volatile bool callScreenRequested;
    private CallLogEntry[] callLog = Array.Empty<CallLogEntry>();

    public CallHub(Configuration configuration, AethernetSession session, NotificationService notifications,
        SoundService sound, PlaybackHub playback, RealtimeSignalBus signals)
    {
        this.configuration = configuration;
        this.session = session;
        this.notifications = notifications;
        this.sound = sound;
        this.playback = playback;
        this.signals = signals;
        connection = new RealtimeConnection(session);
        callLog = configuration.CallLog.ToArray();
    }

    public event Action? IncomingCallPresented;
    public bool Enabled => configuration.CallsEnabled;
    public bool SignedIn => session.IsSignedIn;
    public bool Connected => connection.Connected;
    public string LocalUserId => session.CurrentUser?.Id ?? string.Empty;
    public CallLogEntry[] CallLog => callLog;

    public int UnseenMissed
    {
        get
        {
            var seen = configuration.CallLogSeenUnix;
            var log = callLog;
            var total = 0;
            for (var index = 0; index < log.Length; index++)
            {
                if (log[index].Direction == CallDirection.Missed && log[index].TimestampUnix > seen)
                {
                    total += log[index].Count;
                }
            }

            return total;
        }
    }

    public void MarkLogSeen()
    {
        if (UnseenMissed == 0)
        {
            return;
        }

        configuration.CallLogSeenUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        configuration.Save();
    }

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
        Plugin.Analytics.Track(AnalyticsEvents.SettingChanged("calls_enabled", value ? "1" : "0"));
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
            return new CallView(state, muted, volume, activeSession?.MicLevel ?? 0f, seconds, roster, incomingFrom,
                connection.Connected && connectionLostTicks == 0, localId, BuildPeerLabel(localId), others);
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

    public void RequestCallScreen()
    {
        callScreenRequested = true;
    }

    public bool ConsumeCallScreenRequest()
    {
        if (!callScreenRequested)
        {
            return false;
        }

        callScreenRequested = false;
        return true;
    }

    public void StartCall(CallContact target)
    {
        if (!configuration.CallsEnabled || !session.IsSignedIn)
        {
            AepLog.Info($"[calls] start-refused calls_enabled={configuration.CallsEnabled} signed_in={session.IsSignedIn}");
            return;
        }

        var switching = false;
        lock (gate)
        {
            if (state == CallState.Ringing)
            {
                AepLog.Info("[calls] start-refused an incoming call is still ringing");
                return;
            }

            if (state != CallState.Idle)
            {
                if (InvolvesLocked(target.UserId))
                {
                    AepLog.Info($"[calls] start-refused already engaged with {target.UserId} in state {state}");
                    return;
                }

                switching = true;
            }
        }

        if (switching)
        {
            Hangup();
        }

        Guid id;
        lock (gate)
        {
            if (state != CallState.Idle)
            {
                AepLog.Info($"[calls] start-refused state {state} never returned to idle");
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

        AddLog(target, CallDirection.Outgoing);
        AepLog.Info($"[calls] start-sent call={id:D} to={target.UserId} realtime_connected={connection.Connected}");
        Send(new CallControl
        {
            Type = SignalType.Start, CallId = id.ToString("D"), InviteeIds = new[] { target.UserId },
        });
        Plugin.Analytics.Track(AnalyticsEvents.Call("placed"));
    }

    private bool InvolvesLocked(string userId)
    {
        if (dialingTo is not null && dialingTo.UserId == userId)
        {
            return true;
        }

        for (var index = 0; index < roster.Length; index++)
        {
            if (roster[index].UserId == userId)
            {
                return true;
            }
        }

        return false;
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

        AddLog(target, CallDirection.Outgoing);
        Send(new CallControl { Type = SignalType.Invite, CallId = id, InviteeIds = new[] { target.UserId }, });
    }

    public void Accept()
    {
        string? id;
        ParticipantInfo? from;
        lock (gate)
        {
            if (state != CallState.Ringing)
            {
                return;
            }

            state = CallState.Connecting;
            id = callId.ToString("D");
            from = incomingFrom;
        }

        sound.StopCallRing();
        Send(new CallControl { Type = SignalType.Accept, CallId = id });
        if (from is not null)
        {
            AddLog(new CallContact(from.UserId, from.Name, from.World, from.DisplayName), CallDirection.Incoming);
        }
    }

    public void Decline() => DeclineInternal(timedOut: false);

    private void DeclineInternal(bool timedOut)
    {
        string? id;
        ParticipantInfo? from;
        lock (gate)
        {
            if (state != CallState.Ringing)
            {
                return;
            }

            id = callId.ToString("D");
            from = incomingFrom;
        }

        Send(new CallControl { Type = SignalType.Decline, CallId = id });
        EndCall(notify: false, reason: null);
        LogMissed(from, notify: timedOut);
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
        var reconnectTimeout = false;
        lock (gate)
        {
            if (state != CallState.Idle && connectionLostTicks != 0
                && Environment.TickCount64 - connectionLostTicks >= ReconnectGraceMs)
            {
                reconnectTimeout = true;
            }

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

        if (reconnectTimeout)
        {
            EndCall(notify: true, reason: Loc.T(L.Phone.ConnectionLost));
            return;
        }

        if (ring)
        {
            sound.PulseCallRing();
        }

        if (declineTimeout)
        {
            DeclineInternal(timedOut: true);
        }
        else if (dialTimeout)
        {
            Hangup();
            notifications.Notify(new PhoneNotification("message", Loc.T(L.Phone.NoAnswerTitle), Loc.T(L.Phone.NoAnswerBody), DateTime.Now,
                Accent));
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
            case SignalType.Handled:
                HandleHandledElsewhere(message);
                break;
            case SignalType.ChatPing:
                signals.PublishChat();
                break;
            case SignalType.VelvetPing:
                signals.PublishVelvet();
                break;
            case SignalType.SocialPing:
                signals.PublishSocial();
                break;
            default:
                AepLog.Warning($"[calls] unhandled-signal type={message.Type} call={message.CallId} reason={message.Reason}");
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
            LogMissed(message.From, notify: true);
            return;
        }

        sound.StartCallRing();
        notifications.Notify(new PhoneNotification("message", message.From.DisplayName, Loc.T(L.Phone.IncomingCallBody), DateTime.Now,
            Accent));
        IncomingCallPresented?.Invoke();
        Plugin.Analytics.Track(AnalyticsEvents.Call("received"));
    }

    private void HandleRoster(CallControl message)
    {
        if (!Guid.TryParse(message.CallId, out var id))
        {
            return;
        }

        var participants = message.Participants ?? Array.Empty<ParticipantInfo>();
        CallSession? sessionToDispose = null;
        var connected = false;
        var localId = LocalUserId;
        lock (gate)
        {
            if (id != callId)
            {
                return;
            }

            connectionLostTicks = 0;
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
                    connected = true;
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
        if (connected)
        {
            Plugin.Analytics.Track(AnalyticsEvents.Call("connected"));
        }
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
            EndCall(notify: true, reason: Loc.T(L.Phone.CallDeclined));
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
            EndCall(notify: true, reason: Loc.T(L.Phone.Unavailable));
        }
    }

    private void HandleEnded(CallControl message)
    {
        if (!Guid.TryParse(message.CallId, out var id))
        {
            return;
        }

        var matched = false;
        ParticipantInfo? missedFrom = null;
        lock (gate)
        {
            matched = id == callId && state != CallState.Idle;
            if (matched && state == CallState.Ringing)
            {
                missedFrom = incomingFrom;
            }
        }

        if (matched)
        {
            EndCall(notify: false, reason: null);
            LogMissed(missedFrom, notify: true);
        }
    }

    private void HandleHandledElsewhere(CallControl message)
    {
        if (!Guid.TryParse(message.CallId, out var id))
        {
            return;
        }

        bool matched;
        lock (gate)
        {
            matched = id == callId && state == CallState.Ringing;
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
            OnReconnected();
            return;
        }

        var abandonRinging = false;
        ParticipantInfo? missedFrom = null;
        lock (gate)
        {
            if (state == CallState.Idle)
            {
                return;
            }

            if (state == CallState.Ringing)
            {
                abandonRinging = true;
                missedFrom = incomingFrom;
            }
            else if (connectionLostTicks == 0)
            {
                connectionLostTicks = Environment.TickCount64;
            }
        }

        if (abandonRinging)
        {
            EndCall(notify: false, reason: null);
            LogMissed(missedFrom, notify: false);
        }
    }

    private void OnReconnected()
    {
        string? id = null;
        var resendAccept = false;
        lock (gate)
        {
            if (state is CallState.Dialing or CallState.Connecting or CallState.Active)
            {
                id = callId.ToString("D");
                resendAccept = state == CallState.Connecting;
            }
            else
            {
                connectionLostTicks = 0;
            }
        }

        if (id is null)
        {
            return;
        }

        Send(new CallControl { Type = SignalType.Rejoin, CallId = id });
        if (resendAccept)
        {
            Send(new CallControl { Type = SignalType.Accept, CallId = id });
        }
    }

    private void StartSessionLocked(int localSlot)
    {
        var input = AudioDevices.ResolveInput(configuration.CallInputDevice);
        var output = AudioDevices.ResolveOutput(configuration.CallOutputDevice);
        var created = new CallSession(callId, connection, input, output, volume) { Muted = muted, };
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

        Span<int> present = participants.Length <= 16
            ? stackalloc int[participants.Length]
            : new int[participants.Length];
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
        bool wasConnected;
        double durationMs;
        CallState priorState;
        lock (gate)
        {
            if (state == CallState.Idle)
            {
                return;
            }

            priorState = state;
            wasConnected = callStartTicks != 0;
            durationMs = wasConnected ? Environment.TickCount64 - callStartTicks : 0;
            toDispose = activeSession;
            ClearLocked();
        }

        AepLog.Info($"[calls] ended from={priorState} reason={reason ?? "server hung up"} connected={wasConnected} duration_ms={durationMs:F0}");

        sound.StopCallRing();
        toDispose?.Dispose();
        Plugin.Analytics.Track(AnalyticsEvents.CallEnded(durationMs, wasConnected));
        if (notify && reason is not null)
        {
            notifications.Notify(new PhoneNotification("message", reason, Loc.T(L.Phone.CallEnded), DateTime.Now, Accent));
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
        connectionLostTicks = 0;
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
                return dialingTo?.DisplayName ?? Loc.T(L.Phone.StatusCalling);
            case CallState.Ringing:
                return incomingFrom?.DisplayName ?? Loc.T(L.Phone.IncomingCallBody);
            case CallState.Connecting:
                return Loc.T(L.Phone.StatusConnecting);
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

                return others == 1 && only is not null ? only.DisplayName : Loc.T(L.Phone.GroupCall);
            default:
                return string.Empty;
        }
    }

    private void AddLog(CallContact contact, CallDirection direction)
    {
        lock (gate)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var current = callLog;
            if (current.Length > 0 && current[0].UserId == contact.UserId && current[0].Direction == direction)
            {
                var merged = (CallLogEntry[])current.Clone();
                merged[0] = new CallLogEntry
                {
                    UserId = contact.UserId,
                    Name = contact.Name,
                    World = contact.World,
                    DisplayName = contact.DisplayName,
                    Direction = direction,
                    TimestampUnix = now,
                    Count = current[0].Count + 1,
                };
                callLog = merged;
            }
            else
            {
                var length = Math.Min(current.Length + 1, MaxCallLog);
                var next = new CallLogEntry[length];
                next[0] = new CallLogEntry
                {
                    UserId = contact.UserId,
                    Name = contact.Name,
                    World = contact.World,
                    DisplayName = contact.DisplayName,
                    Direction = direction,
                    TimestampUnix = now,
                };
                for (var index = 1; index < length; index++)
                {
                    next[index] = current[index - 1];
                }

                callLog = next;
            }

            configuration.CallLog.Clear();
            configuration.CallLog.AddRange(callLog);
        }

        configuration.Save();
    }

    private void LogMissed(ParticipantInfo? from, bool notify)
    {
        if (from is null)
        {
            return;
        }

        AddLog(new CallContact(from.UserId, from.Name, from.World, from.DisplayName), CallDirection.Missed);
        if (notify)
        {
            notifications.Notify(new PhoneNotification("message", from.DisplayName, Loc.T(L.Phone.MissedCallBody),
                DateTime.Now, Accent));
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
