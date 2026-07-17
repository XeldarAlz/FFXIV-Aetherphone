using System.Numerics;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Analytics;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Playback;
using Aetherphone.Core.Telephony.Contracts;

namespace Aetherphone.Core.Telephony;

internal sealed class CallHub : IDisposable
{
    private const float RingIntervalSeconds = 3f;
    private const float IncomingTimeoutSeconds = 60f;
    private const float DialingTimeoutSeconds = 60f;
    private const long ReconnectGraceMs = 20_000;
    private static readonly Vector4 Accent = new(0.20f, 0.78f, 0.35f, 1f);
    private readonly Configuration configuration;
    private readonly AethernetSession session;
    private readonly NotificationService notifications;
    private readonly SoundService sound;
    private readonly CallSignalRouter router;
    private readonly CallAudioController audio;
    private readonly CallLogStore log;
    private readonly object gate = new();
    private CallState state = CallState.Idle;
    private Guid callId;
    private ParticipantInfo[] roster = Array.Empty<ParticipantInfo>();
    private ParticipantInfo? incomingFrom;
    private CallContact? dialingTo;
    private float ringTimer;
    private float stateTimer;
    private long connectionLostTicks;
    private volatile bool callScreenRequested;

    public CallHub(Configuration configuration, AethernetSession session, NotificationService notifications,
        SoundService sound, PlaybackHub playback, RealtimeSignalBus signals)
    {
        this.configuration = configuration;
        this.session = session;
        this.notifications = notifications;
        this.sound = sound;
        router = new CallSignalRouter(session, signals);
        audio = new CallAudioController(configuration, playback, router.Connection);
        log = new CallLogStore(configuration);
    }

    public event Action? IncomingCallPresented;
    public bool Enabled => configuration.CallsEnabled;
    public bool SignedIn => session.IsSignedIn;
    public bool Connected => router.Connected;
    public string LocalUserId => session.CurrentUser?.Id ?? string.Empty;
    public CallLogEntry[] CallLog => log.Entries;
    public int UnseenMissed => log.UnseenMissed;

    public void MarkLogSeen() => log.MarkSeen();

    public void Start()
    {
        router.IncomingReceived += HandleIncoming;
        router.RosterReceived += HandleRoster;
        router.DeclinedReceived += HandleDeclined;
        router.UnavailableReceived += HandleUnavailable;
        router.EndedReceived += HandleEnded;
        router.HandledElsewhereReceived += HandleHandledElsewhere;
        router.ConnectedChanged += OnConnectedChanged;
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
            var seconds = state == CallState.Active ? audio.ElapsedSecondsLocked : 0;
            return new CallView(state, audio.MutedLocked, audio.VolumeLocked, audio.MicLevelLocked, seconds, roster,
                incomingFrom, router.Connected && connectionLostTicks == 0, localId, BuildPeerLabel(localId), others);
        }
    }

    public float LevelOf(ParticipantInfo participant)
    {
        CallSession? current;
        lock (gate)
        {
            current = audio.SessionLocked;
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

        log.Add(target, CallDirection.Outgoing);
        AepLog.Info($"[calls] start-sent call={id:D} to={target.UserId} realtime_connected={router.Connected}");
        router.Send(new CallControl
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

        log.Add(target, CallDirection.Outgoing);
        router.Send(new CallControl { Type = SignalType.Invite, CallId = id, InviteeIds = new[] { target.UserId }, });
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
        router.Send(new CallControl { Type = SignalType.Accept, CallId = id });
        if (from is not null)
        {
            log.Add(new CallContact(from.UserId, from.Name, from.World, from.DisplayName), CallDirection.Incoming);
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

        router.Send(new CallControl { Type = SignalType.Decline, CallId = id });
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

        router.Send(new CallControl { Type = SignalType.Leave, CallId = id });
        EndCall(notify: false, reason: null);
    }

    public void ToggleMute()
    {
        lock (gate)
        {
            audio.ToggleMuteLocked();
        }
    }

    public void SetVolume(float value)
    {
        lock (gate)
        {
            audio.SetVolumeLocked(value);
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

    private void HandleIncoming(Guid id, CallControl message)
    {
        if (message.From is null)
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
            router.Send(new CallControl { Type = SignalType.Decline, CallId = message.CallId, Reason = "busy" });
            LogMissed(message.From, notify: true);
            return;
        }

        sound.StartCallRing();
        notifications.Notify(new PhoneNotification("message", message.From.DisplayName, Loc.T(L.Phone.IncomingCallBody), DateTime.Now,
            Accent));
        IncomingCallPresented?.Invoke();
        Plugin.Analytics.Track(AnalyticsEvents.Call("received"));
    }

    private void HandleRoster(Guid id, CallControl message)
    {
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
                connected = audio.EnsureStartedLocked(callId, localSlot);
                audio.SyncRemotesLocked(participants, localId);
                state = CallState.Active;
            }
            else if (audio.HasSessionLocked)
            {
                sessionToDispose = ClearLocked();
            }
        }

        sessionToDispose?.Dispose();
        if (connected)
        {
            Plugin.Analytics.Track(AnalyticsEvents.Call("connected"));
        }
    }

    private void HandleDeclined(Guid id, CallControl message)
    {
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

    private void HandleUnavailable(Guid id, CallControl message)
    {
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

    private void HandleEnded(Guid id, CallControl message)
    {
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

    private void HandleHandledElsewhere(Guid id, CallControl message)
    {
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

        router.Send(new CallControl { Type = SignalType.Rejoin, CallId = id });
        if (resendAccept)
        {
            router.Send(new CallControl { Type = SignalType.Accept, CallId = id });
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
            wasConnected = audio.StartTicksLocked != 0;
            durationMs = wasConnected ? Environment.TickCount64 - audio.StartTicksLocked : 0;
            toDispose = ClearLocked();
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

    private CallSession? ClearLocked()
    {
        state = CallState.Idle;
        callId = default;
        roster = Array.Empty<ParticipantInfo>();
        incomingFrom = null;
        dialingTo = null;
        connectionLostTicks = 0;
        ringTimer = 0f;
        stateTimer = 0f;
        return audio.TakeLocked();
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

    private void LogMissed(ParticipantInfo? from, bool notify)
    {
        if (from is null)
        {
            return;
        }

        log.Add(new CallContact(from.UserId, from.Name, from.World, from.DisplayName), CallDirection.Missed);
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
            router.Start();
        }
        else
        {
            EndCall(notify: false, reason: null);
            router.Stop();
        }
    }

    public void Dispose()
    {
        router.IncomingReceived -= HandleIncoming;
        router.RosterReceived -= HandleRoster;
        router.DeclinedReceived -= HandleDeclined;
        router.UnavailableReceived -= HandleUnavailable;
        router.EndedReceived -= HandleEnded;
        router.HandledElsewhereReceived -= HandleHandledElsewhere;
        router.ConnectedChanged -= OnConnectedChanged;
        session.Changed -= Reconcile;
        CallSession? toDispose;
        lock (gate)
        {
            toDispose = audio.TakeLocked();
        }

        toDispose?.Dispose();
        router.Dispose();
    }
}
