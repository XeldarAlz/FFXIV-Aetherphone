using System.Collections.Concurrent;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Playback;
using Aetherphone.Core.Songs;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Telephony.Contracts;
using Aetherphone.Core.Video;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Jam;

internal sealed partial class JamSession : IDisposable
{
    public const string InviteGroupPrefix = "music:jam:";

    private const long ResponseTimeoutMilliseconds = 10_000;
    private const long ReconnectGraceMilliseconds = 20_000;
    private const int MaxPendingOperations = 64;
    private const long ReactionCooldownMilliseconds = 220;
    private const long ControlCooldownMilliseconds = 220;

    private readonly JamSignalRouter signals;
    private readonly PlaybackHub hub;
    private readonly AethernetSession session;
    private readonly NotificationService? notifications;
    private readonly IFramework? framework;
    private readonly ConcurrentQueue<CallControl> inbound = new();
    private readonly List<CallControl> pendingOperations = new();
    private readonly JamOperationPacer pacer = new();
    private readonly JamInviteQueue invites = new();
    private readonly JamInviteGate inviteGate = new();
    private readonly JamHostAuthority hostAuthority;
    private readonly JamGuestAuthority guestAuthority;
    private readonly JamPendingMoves pendingMoves = new();
    private readonly Func<JamLocation> locate;

    private JamMember[] members = Array.Empty<JamMember>();
    private JamQueueItem[] serverQueue = Array.Empty<JamQueueItem>();
    private JamQueueItem[] queue = Array.Empty<JamQueueItem>();
    private JamJoinRequest[] joinRequests = Array.Empty<JamJoinRequest>();
    private string jamId = string.Empty;
    private string hostId = string.Empty;
    private string boundUserId = string.Empty;
    private int serverQueueVersion;
    private int dropCount;
    private int seenDropCount;
    private long awaitingSinceTicks;
    private long disconnectedSinceTicks;
    private long lastTickTicks;
    private long reactedAtTicks;
    private long controlledAtTicks;
    private long lastAddSentAtTicks;
    private volatile bool stopRequested;

    public JamSession(CallSignalRouter calls, PlaybackHub hub, AethernetSession session,
        NotificationService? notifications, IFramework? framework, Func<JamLocation>? locate = null)
    {
        signals = new JamSignalRouter(calls);
        this.locate = locate ?? LocateLocalPlayer;
        this.hub = hub;
        this.session = session;
        this.notifications = notifications;
        this.framework = framework;
        hostAuthority = new JamHostAuthority(this);
        guestAuthority = new JamGuestAuthority(this);
        signals.Received += OnReceived;
        signals.ConnectedChanged += OnConnectedChanged;
        hub.TrackStarted += OnTrackStarted;
        if (framework is not null)
        {
            framework.Update += OnFrameworkUpdate;
        }
    }

    public JamMode Mode { get; private set; } = JamMode.Idle;
    public bool IsHost => Mode == JamMode.Hosting;
    public bool InJam => Mode is JamMode.Hosting or JamMode.Listening;
    public bool IsBusy => Mode != JamMode.Idle;
    public string Code { get; private set; } = string.Empty;
    public string DisplayCode { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public int GuestPermissions { get; private set; } = JamPermission.All;
    public bool ApprovalRequired { get; private set; }
    public bool Stale { get; private set; }
    public bool LocalHold => localHold;
    public bool Reconnecting => disconnectedSinceTicks != 0 || (awaitingSinceTicks != 0 && InJam);
    public JamDeclineReason LastDecline { get; private set; }
    public int DeclineVersion { get; private set; }
    public JamRefusal LastRefusal { get; private set; }
    public int RefusalVersion { get; private set; }
    public ReadOnlySpan<JamMember> Members => members;
    public int MembersVersion { get; private set; }
    public ReadOnlySpan<JamQueueItem> Queue => queue;
    public int QueueVersion { get; private set; }
    public ReadOnlySpan<JamJoinRequest> JoinRequests => joinRequests;
    public int JoinRequestsVersion { get; private set; }
    public JamReactions Reactions { get; } = new();

    public int MyPermissions
    {
        get
        {
            if (Mode == JamMode.Hosting)
            {
                return JamPermission.All;
            }

            var me = MyUserId;
            for (var index = 0; index < members.Length; index++)
            {
                if (string.Equals(members[index].UserId, me, StringComparison.Ordinal))
                {
                    return members[index].Permissions;
                }
            }

            return GuestPermissions;
        }
    }

    public bool CanControlPlayback => JamPermission.Allows(MyPermissions, JamPermission.ControlPlayback);
    public bool CanAddToQueue => JamPermission.Allows(MyPermissions, JamPermission.AddToQueue);

    private string MyUserId => session.CurrentUser?.Id ?? string.Empty;

    public bool Start(string? title)
    {
        if (Mode == JamMode.Hosting)
        {
            return true;
        }

        if (!CanReachServer())
        {
            return false;
        }

        if (Mode != JamMode.Idle)
        {
            Leave();
        }

        Title = JamWire.NormalizeTitle(title);
        seedPending = hub.SongActive && hub.Queue.QueuedCount > 0;
        boundUserId = MyUserId;
        Mode = JamMode.Starting;
        awaitingSinceTicks = Environment.TickCount64;
        signals.Start(Title.Length > 0 ? Title : null);
        return true;
    }

    public bool Join(string input)
    {
        var code = PartyCode.Normalize(input);
        if (code.Length == 0)
        {
            Decline(JamDeclineReason.BadCode);
            return false;
        }

        if (InJam && string.Equals(code, Code, StringComparison.Ordinal))
        {
            return true;
        }

        if (!CanReachServer())
        {
            return false;
        }

        if (Mode != JamMode.Idle)
        {
            Leave();
        }

        SetCode(code);
        boundUserId = MyUserId;
        Mode = JamMode.Joining;
        awaitingSinceTicks = Environment.TickCount64;
        signals.Join(code);
        return true;
    }

    public void Leave()
    {
        if (Mode == JamMode.Idle)
        {
            return;
        }

        signals.Leave();
        ExitLocal(JamDeclineReason.None);
    }

    public void End()
    {
        if (Mode != JamMode.Hosting)
        {
            Leave();
            return;
        }

        signals.End();
        ExitLocal(JamDeclineReason.None);
    }

    public void Kick(string userId)
    {
        if (Mode == JamMode.Hosting && userId.Length > 0)
        {
            signals.Kick(userId);
        }
    }

    public void Transfer(string userId)
    {
        if (Mode == JamMode.Hosting && userId.Length > 0)
        {
            signals.Transfer(userId);
        }
    }

    public void Approve(string userId)
    {
        DecideJoin(userId, true);
    }

    public void Deny(string userId)
    {
        DecideJoin(userId, false);
    }

    public void SetGuestPermissions(int permissions)
    {
        if (Mode == JamMode.Hosting)
        {
            signals.Settings(permissions & JamPermission.All, null, null);
        }
    }

    public void SetApprovalRequired(bool required)
    {
        if (Mode == JamMode.Hosting)
        {
            signals.Settings(null, required, null);
        }
    }

    public void Rename(string title)
    {
        if (Mode == JamMode.Hosting)
        {
            signals.Settings(null, null, JamWire.NormalizeTitle(title));
        }
    }

    public void Invite(string userId)
    {
        if (InJam && userId.Length > 0)
        {
            invites.Enqueue(userId);
        }
    }

    public void React(int kind)
    {
        var now = Environment.TickCount64;
        if (!InJam || kind < 0 || kind >= JamReactions.MaxKinds || now - reactedAtTicks < ReactionCooldownMilliseconds)
        {
            return;
        }

        reactedAtTicks = now;
        signals.React(kind);
        var me = MyUserId;
        Reactions.Add(new JamReaction(kind, me, NameOf(me), now));
    }

    public void QueueSong(in Song song, bool playNext)
    {
        if (playNext)
        {
            hub.PlayNext(song);
            return;
        }

        hub.PlayLast(song);
    }

    public void RemoveFromQueue(int entryId) => hub.RemoveQueued(entryId);

    public void MoveInQueue(int entryId, int toIndex) => hub.MoveQueued(entryId, toIndex);

    public void PlayQueuedNow(int entryId) => hub.JumpTo(entryId);

    public void ClearRefusal()
    {
        LastRefusal = JamRefusal.None;
    }

    public void Tick()
    {
        var now = Environment.TickCount64;
        var deltaSeconds = lastTickTicks == 0 ? 0f : Math.Clamp((now - lastTickTicks) / 1000f, 0f, 0.5f);
        lastTickTicks = now;

        if (Mode != JamMode.Idle && !string.Equals(MyUserId, boundUserId, StringComparison.Ordinal))
        {
            ExitLocal(JamDeclineReason.None);
        }

        while (inbound.TryDequeue(out var message))
        {
            Dispatch(message);
        }

        TrackConnection(now);
        TickTimeouts(now);
        if (stopRequested)
        {
            stopRequested = false;
            ApplyStopRequest();
        }

        if (Mode == JamMode.Hosting)
        {
            TickHost(deltaSeconds, now);
        }
        else if (Mode == JamMode.Listening)
        {
            TickGuest(deltaSeconds);
        }

        if (pendingMoves.Count > 0 && pendingMoves.HasExpired(now))
        {
            ProjectQueue(now);
        }

        DrainOperations(now);
        TickNearby(now);
    }

    public void Dispose()
    {
        signals.Received -= OnReceived;
        signals.ConnectedChanged -= OnConnectedChanged;
        hub.TrackStarted -= OnTrackStarted;
        if (framework is not null)
        {
            framework.Update -= OnFrameworkUpdate;
        }

        ReleaseAuthority();
        signals.Dispose();
    }

    private void OnFrameworkUpdate(IFramework _) => Tick();

    private void OnReceived(CallControl message) => inbound.Enqueue(message);

    private void OnConnectedChanged(bool connected)
    {
        if (!connected)
        {
            Interlocked.Increment(ref dropCount);
        }
    }

    private void OnTrackStarted()
    {
        if (Mode == JamMode.Hosting)
        {
            publishRequested = true;
        }
    }

    private bool CanReachServer()
    {
        if (signals.Connected && session.CurrentUser is not null)
        {
            return true;
        }

        Decline(JamDeclineReason.Offline);
        return false;
    }

    private void Dispatch(CallControl message)
    {
        if (message.Type == SignalType.JamInvited)
        {
            OnInvited(message);
            return;
        }

        if (message.Type == SignalType.JamNearbyRoster)
        {
            OnNearbyRoster(message);
            return;
        }

        if (Mode == JamMode.Idle)
        {
            if (message.Type == SignalType.JamJoined)
            {
                signals.Leave();
            }

            return;
        }

        if (message.Type != SignalType.JamJoined && message.JamId is { Length: > 0 } messageJamId
            && jamId.Length > 0 && !string.Equals(messageJamId, jamId, StringComparison.Ordinal))
        {
            return;
        }

        switch (message.Type)
        {
            case SignalType.JamJoined:
                ApplySnapshot(message);
                return;
            case SignalType.JamHostChanged:
                SetJoinRequests(Array.Empty<JamJoinRequest>());
                ApplySnapshot(message);
                return;
            case SignalType.JamJoinPending:
                OnJoinPending(message);
                return;
            case SignalType.JamDeclined:
                OnDeclined(message);
                return;
            case SignalType.JamJoinRequest:
                OnJoinRequest(message);
                return;
            case SignalType.JamJoinCancelled:
                RemoveJoinRequest(message.UserId);
                return;
            case SignalType.JamRoster:
                OnRoster(message);
                return;
            case SignalType.JamState:
                OnState(message);
                return;
            case SignalType.JamQueue:
                OnQueue(message);
                return;
            case SignalType.JamControlRequest:
                OnControlRequest(message);
                return;
            case SignalType.JamReaction:
                OnReaction(message);
                return;
            case SignalType.JamMessage:
                OnChatMessage(message);
                return;
            case SignalType.JamMessageDeleted:
                OnChatDeleted(message);
                return;
            case SignalType.JamRefused:
                OnRefused(message);
                return;
            case SignalType.JamKicked:
                ExitLocal(JamDeclineReason.Kicked);
                return;
            case SignalType.JamEnded:
                ExitLocal(JamDeclineReason.Ended);
                return;
        }
    }

    private void ApplySnapshot(CallControl message)
    {
        awaitingSinceTicks = 0;
        disconnectedSinceTicks = 0;
        var incomingJamId = message.JamId ?? string.Empty;
        var sameJam = jamId.Length > 0 && string.Equals(jamId, incomingJamId, StringComparison.Ordinal);
        jamId = incomingJamId;
        hostId = message.HostId ?? string.Empty;
        if (message.Code is { Length: > 0 } code)
        {
            SetCode(PartyCode.Normalize(code) is { Length: > 0 } normalized ? normalized : code);
        }

        Title = message.Title ?? string.Empty;
        GuestPermissions = message.GuestPermissions ?? GuestPermissions;
        ApprovalRequired = message.ApprovalRequired ?? false;
        Stale = message.Stale ?? false;
        ApplyDiscoverable(message.Discoverable ?? false);
        SetMembers(message.JamMembers);
        ApplyQueue(message, snapshot: true);
        LoadChat(message, sameJam);

        if (string.Equals(hostId, MyUserId, StringComparison.Ordinal))
        {
            BecomeHost();
            return;
        }

        BecomeGuest();
        AnchorRemoteState(message, absorbClock: false);
        ApplyRemoteState();
    }

    private void OnJoinPending(CallControl message)
    {
        awaitingSinceTicks = 0;
        disconnectedSinceTicks = 0;
        jamId = message.JamId ?? jamId;
        if (InJam)
        {
            ReleaseAuthority();
            ResetHostState();
            ResetGuestState();
            SetMembers(null);
            SetJoinRequests(Array.Empty<JamJoinRequest>());
            Mode = JamMode.Pending;
            return;
        }

        if (Mode is JamMode.Joining or JamMode.Pending)
        {
            Mode = JamMode.Pending;
        }
    }

    private void OnDeclined(CallControl message)
    {
        var reason = JamDeclineCodes.Parse(message.Reason);
        if (InJam && reason == JamDeclineReason.BadCode)
        {
            reason = JamDeclineReason.Ended;
        }

        ExitLocal(reason);
    }

    private void OnRoster(CallControl message)
    {
        if (!InJam)
        {
            return;
        }

        Title = message.Title ?? string.Empty;
        GuestPermissions = message.GuestPermissions ?? GuestPermissions;
        ApprovalRequired = message.ApprovalRequired ?? ApprovalRequired;
        ApplyDiscoverable(message.Discoverable);
        SetMembers(message.JamMembers);
    }

    private void OnQueue(CallControl message)
    {
        if (!InJam)
        {
            return;
        }

        ApplyQueue(message, snapshot: false);
        OnHostQueue();
    }

    private void ApplyQueue(CallControl message, bool snapshot)
    {
        var incoming = message.QueueVersion ?? 0;
        if (!JamQueueVersion.ShouldApply(serverQueueVersion, incoming, snapshot))
        {
            return;
        }

        serverQueue = JamWire.ToQueue(message.Entries);
        serverQueueVersion = incoming;
        ProjectQueue(Environment.TickCount64);
    }

    private void ProjectQueue(long now)
    {
        queue = pendingMoves.Project(serverQueue, now);
        QueueVersion++;
    }

    private void OnState(CallControl message)
    {
        Stale = message.Stale ?? false;
        if (Mode == JamMode.Hosting)
        {
            OnHostStateEcho(message);
            return;
        }

        if (Mode != JamMode.Listening)
        {
            return;
        }

        AnchorRemoteState(message, absorbClock: !Stale);
        ApplyRemoteState();
    }

    private void OnJoinRequest(CallControl message)
    {
        if (Mode != JamMode.Hosting || JamWire.ToJoinRequest(message) is not { } request)
        {
            return;
        }

        for (var index = 0; index < joinRequests.Length; index++)
        {
            if (string.Equals(joinRequests[index].UserId, request.UserId, StringComparison.Ordinal))
            {
                return;
            }
        }

        var grown = new JamJoinRequest[joinRequests.Length + 1];
        Array.Copy(joinRequests, grown, joinRequests.Length);
        grown[^1] = request;
        SetJoinRequests(grown);
    }

    private void RemoveJoinRequest(string? userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        var index = -1;
        for (var candidate = 0; candidate < joinRequests.Length; candidate++)
        {
            if (string.Equals(joinRequests[candidate].UserId, userId, StringComparison.Ordinal))
            {
                index = candidate;
                break;
            }
        }

        if (index < 0)
        {
            return;
        }

        var shrunk = new JamJoinRequest[joinRequests.Length - 1];
        Array.Copy(joinRequests, 0, shrunk, 0, index);
        Array.Copy(joinRequests, index + 1, shrunk, index, joinRequests.Length - index - 1);
        SetJoinRequests(shrunk);
    }

    private void DecideJoin(string userId, bool approve)
    {
        if (Mode != JamMode.Hosting || userId.Length == 0)
        {
            return;
        }

        if (approve)
        {
            signals.Approve(userId);
        }
        else
        {
            signals.Deny(userId);
        }

        RemoveJoinRequest(userId);
    }

    private void OnReaction(CallControl message)
    {
        if (message.Reaction is not { } kind || message.UserId is not { Length: > 0 } userId)
        {
            return;
        }

        Reactions.Add(new JamReaction(kind, userId, NameOf(userId), Environment.TickCount64));
    }

    private void OnInvited(CallControl message)
    {
        var code = PartyCode.Normalize(message.Code);
        if (code.Length == 0 || notifications is null
            || (Mode != JamMode.Idle && string.Equals(code, Code, StringComparison.Ordinal))
            || !inviteGate.Admit(code, Environment.TickCount64))
        {
            return;
        }

        notifications.Notify(JamInviteNotification.Build(code, JamWire.PublicName(message.From), message.Title,
            message.From?.UserId));
    }

    private void TrackConnection(long now)
    {
        var drops = Volatile.Read(ref dropCount);
        if (drops != seenDropCount)
        {
            seenDropCount = drops;
            if (Mode != JamMode.Idle && disconnectedSinceTicks == 0)
            {
                disconnectedSinceTicks = now;
            }
        }

        if (disconnectedSinceTicks == 0 || !signals.Connected)
        {
            return;
        }

        if (now - disconnectedSinceTicks > ReconnectGraceMilliseconds)
        {
            ExitLocal(JamDeclineReason.ConnectionLost);
            return;
        }

        disconnectedSinceTicks = 0;
        awaitingSinceTicks = now;
        pacer.Reset();
        chatPacer.Reset();
        nearbyCadence.Reset();
        if (Mode == JamMode.Starting || Code.Length == 0)
        {
            signals.Start(Title.Length > 0 ? Title : null);
            return;
        }

        signals.Join(Code);
    }

    private void TickTimeouts(long now)
    {
        if (disconnectedSinceTicks != 0 && now - disconnectedSinceTicks > ReconnectGraceMilliseconds)
        {
            ExitLocal(JamDeclineReason.ConnectionLost);
            return;
        }

        if (awaitingSinceTicks != 0 && now - awaitingSinceTicks > ResponseTimeoutMilliseconds)
        {
            signals.Leave();
            ExitLocal(JamDeclineReason.NoResponse);
        }
    }

    private void ApplyStopRequest()
    {
        if (hub.RadioActive)
        {
            hub.Radio.Stop();
        }

        if (Mode == JamMode.Hosting)
        {
            if (hub.SongActive && !hub.IsPaused)
            {
                hub.ApplyTogglePlayPause();
            }

            publishRequested = true;
            return;
        }

        if (Mode != JamMode.Listening)
        {
            return;
        }

        localHold = true;
        ReleaseRate();
        if (hub.SongActive && !hub.IsPaused)
        {
            hub.ApplyTogglePlayPause();
        }
    }

    private void Enqueue(CallControl operation, bool urgent = false)
    {
        if (pendingOperations.Count >= MaxPendingOperations)
        {
            return;
        }

        if (urgent)
        {
            pendingOperations.Insert(0, operation);
            return;
        }

        pendingOperations.Add(operation);
    }

    private void EnqueueAdd(in Song song, bool playNext)
    {
        if (JamWire.ToTrack(song) is not { } track)
        {
            return;
        }

        Enqueue(new CallControl
        {
            Type = SignalType.JamQueueAdd,
            Track = track,
            Mode = playNext ? JamQueueMode.Next : JamQueueMode.End,
        });
    }

    private void EnqueueRemove(int entryId)
    {
        Enqueue(new CallControl { Type = SignalType.JamQueueRemove, EntryId = entryId });
    }

    private void EnqueueMove(int entryId, int toIndex)
    {
        var target = Math.Max(0, toIndex);
        Enqueue(new CallControl { Type = SignalType.JamQueueMove, EntryId = entryId, ToIndex = target });
        var now = Environment.TickCount64;
        pendingMoves.Add(entryId, target, now);
        ProjectQueue(now);
    }

    private void DrainOperations(long now)
    {
        if (!InJam || !signals.Connected || disconnectedSinceTicks != 0)
        {
            return;
        }

        while (pendingOperations.Count > 0 && pacer.TryAcquire(now))
        {
            var operation = pendingOperations[0];
            signals.Send(operation);
            pendingOperations.RemoveAt(0);
            if (string.Equals(operation.Type, SignalType.JamQueueAdd, StringComparison.Ordinal))
            {
                lastAddSentAtTicks = now;
            }
        }

        if (invites.TryDequeue(now, out var invitee))
        {
            signals.Invite(invitee);
        }
    }

    private bool TrySendControl(string action, double? positionSeconds)
    {
        var now = Environment.TickCount64;
        if (now - controlledAtTicks < ControlCooldownMilliseconds)
        {
            return false;
        }

        controlledAtTicks = now;
        signals.Control(action, positionSeconds);
        return true;
    }

    private void Refuse(JamRefusal refusal)
    {
        LastRefusal = refusal;
        RefusalVersion++;
    }

    private void Decline(JamDeclineReason reason)
    {
        LastDecline = reason;
        DeclineVersion++;
    }

    private void ExitLocal(JamDeclineReason reason)
    {
        ReleaseAuthority();
        ResetHostState();
        ResetGuestState();
        pendingOperations.Clear();
        invites.Clear();
        pacer.Reset();
        pendingMoves.Clear();
        ResetChat();
        nearbyCadence.Reset();
        Discoverable = false;
        lastAddSentAtTicks = 0;
        Mode = JamMode.Idle;
        jamId = string.Empty;
        hostId = string.Empty;
        boundUserId = string.Empty;
        awaitingSinceTicks = 0;
        disconnectedSinceTicks = 0;
        Stale = false;
        Title = string.Empty;
        SetCode(string.Empty);
        SetMembers(null);
        serverQueue = Array.Empty<JamQueueItem>();
        queue = Array.Empty<JamQueueItem>();
        serverQueueVersion = 0;
        QueueVersion++;
        SetJoinRequests(Array.Empty<JamJoinRequest>());
        Reactions.Clear();
        if (reason != JamDeclineReason.None)
        {
            Decline(reason);
        }
    }

    private void ReleaseAuthority()
    {
        if (ReferenceEquals(hub.Authority, hostAuthority) || ReferenceEquals(hub.Authority, guestAuthority))
        {
            hub.Authority = null;
            hub.SetRate(1f);
        }
    }

    private void SetCode(string code)
    {
        Code = code;
        DisplayCode = code.Length == 0 ? string.Empty : PartyCode.Display(code);
    }

    private void SetMembers(JamMember[]? incoming)
    {
        members = incoming ?? Array.Empty<JamMember>();
        MembersVersion++;
    }

    private void SetJoinRequests(JamJoinRequest[] requests)
    {
        joinRequests = requests;
        JoinRequestsVersion++;
    }

    private string NameOf(string userId)
    {
        for (var index = 0; index < members.Length; index++)
        {
            var member = members[index];
            if (!string.Equals(member.UserId, userId, StringComparison.Ordinal))
            {
                continue;
            }

            if (member.DisplayName.Length > 0)
            {
                return member.DisplayName;
            }

            return member.Handle.Length > 0 ? string.Concat("@", member.Handle) : string.Empty;
        }

        return string.Empty;
    }

    private bool AddInFlight(long now)
    {
        var queued = false;
        for (var index = 0; index < pendingOperations.Count; index++)
        {
            if (string.Equals(pendingOperations[index].Type, SignalType.JamQueueAdd, StringComparison.Ordinal))
            {
                queued = true;
                break;
            }
        }

        return JamHostRecovery.AddInFlight(queued, lastAddSentAtTicks, now);
    }

    private bool OwnsEntry(int entryId)
    {
        var index = JamWire.IndexOfEntry(queue, entryId);
        return index >= 0 && string.Equals(queue[index].AddedByUserId, MyUserId, StringComparison.Ordinal);
    }

    private static Song SongOf(in PlaybackIntent intent)
    {
        if (intent.Kind != PlaybackIntentKind.PlaySongs)
        {
            return intent.Song;
        }

        return intent.Songs.Length == 0 ? default : intent.Songs[Math.Clamp(intent.Index, 0, intent.Songs.Length - 1)];
    }

    private sealed class JamHostAuthority : IPlaybackAuthority
    {
        private readonly JamSession owner;

        public JamHostAuthority(JamSession owner) => this.owner = owner;

        public bool TryHandle(in PlaybackIntent intent) => owner.HandleHostIntent(intent);
    }

    private sealed class JamGuestAuthority : IPlaybackAuthority
    {
        private readonly JamSession owner;

        public JamGuestAuthority(JamSession owner) => this.owner = owner;

        public bool TryHandle(in PlaybackIntent intent) => owner.HandleGuestIntent(intent);
    }
}
