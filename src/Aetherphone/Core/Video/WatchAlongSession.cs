using System.Collections.Concurrent;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Maps;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Telephony.Contracts;
using Aetherphone.Windows.Components;

namespace Aetherphone.Core.Video;

internal sealed record WatchAlongParticipant(string UserId, string DisplayName, string? AvatarUrl, bool IsHost,
    int Flags)
{
    internal bool CanHost => (Flags & StreamPermission.CanHost) != 0;
}

internal sealed record NearbyStream(string HostId, string DisplayName, string Handle, string? AvatarUrl);

internal sealed record PendingJoinRequest(string UserId, string DisplayName, string? AvatarUrl);

internal sealed record QueueSuggestion(string SuggestionId, string UserId, string DisplayName, string Url);

internal sealed record HostQueueItem(string Url, string Title);

internal sealed record ViewerFailure(string UserId, string DisplayName, string? Reason);

internal readonly record struct PendingAlert(LocString Title, LocString Body);

internal readonly record struct PendingToast(LocString Text, string? Argument);

internal enum WatchAlongMode : byte
{
    None,
    Hosting,
    Viewing,
}

internal sealed class WatchAlongSession : IDisposable
{
    private const int CheckEveryTicks = 30;
    private const float HeartbeatSeconds = 8f;
    private const double PositionJumpTolerance = 2.0;
    private const double StaleStateSeconds = 30.0;
    private const long AutoReplayInitialDelayMilliseconds = 10 * 1000;
    private const long AutoReplayMaxDelayMilliseconds = 60 * 1000;
    private const long AutoReplayBotCheckDelayMilliseconds = 5 * 60 * 1000;
    private const long AutoReplayBotCheckMaxDelayMilliseconds = 15 * 60 * 1000;
    private const long ReactionCooldownMilliseconds = 250;
    private const long ControlCooldownMilliseconds = 250;
    private const long RejoinRetryMilliseconds = 3_000;
    private const int MaxRejoinAttempts = 8;
    private const int MaxSharedQueueEntries = 64;

    private const float ScreenPositionDriftTolerance = 0.01f;
    private const float ScreenAngleDriftTolerance = 0.005f;
    private const float ScreenScaleDriftTolerance = 0.005f;
    private const float HostScreenReach = 120f;

    private readonly AethernetSession session;
    private readonly Configuration configuration;
    private readonly ConfirmService confirm;
    private readonly VideoPlayer video;
    private readonly AetherStreamQueue queue;
    private readonly StreamSignalRouter stream;
    private readonly ScreenController screen;
    private readonly ServerClock serverClock = new();
    private readonly PlaybackSyncController sync = new();
    private readonly ConcurrentDictionary<string, int> grants = new(StringComparer.Ordinal);

    private int tickCounter;
    private float heartbeatTimer;
    private int dropCount;
    private int seenDropCount;
    private bool resumePending;
    private int rejoinAttempts;
    private long rejoinNextTicks;
    private volatile bool rejoining;
    private string? joinHostId;
    private string? joinCode;
    private string? lastPublishedUrl;
    private double lastPublishedPosition;
    private DateTime lastPublishedAt = DateTime.UtcNow;
    private bool lastPublishedPaused;
    private StreamScreenPose? lastPublishedScreen;
    private bool lastPublishedApprovalRequired;
    private int lastPublishedQueueCount = -1;
    private volatile bool publishRequested;

    private string? viewingUrl;
    private string? viewingPlaybackUrl;
    private string? rejectedRemoteUrl;
    private string? autoReplayUrl;
    private string? reportedFailureUrl;
    private string? mismatchCandidatePath;
    private long mismatchCandidateSizeBytes;
    private long autoReplayDelayMilliseconds;
    private long autoReplayNextAtTicks;
    private long idleSinceTicks;
    private long reactedAtTicks;
    private long controlledAtTicks;
    private CallControl? lastStateMessage;

    private CallControl? pendingJoinSync;
    private CallControl? pendingStateSync;
    private CallControl? pendingHostChange;
    private CallControl? pendingRejoinDecline;
    private volatile bool pendingViewerStop;

    private volatile int serverFeatures;
    private volatile int remoteGuestPermissions;
    private volatile StreamMember[]? remoteMembers;
    private volatile ParticipantInfo[]? remoteParticipants;
    private volatile string? roomCode;
    private StreamMember[]? publishedGrants;
    private volatile bool grantsChanged;
    private volatile bool promotionPending;
    private volatile bool serverSeen;
    private volatile bool joinRequested;
    private volatile bool policyAdopted;
    private volatile int publishedGuestPermissions;
    private volatile string? roomHostId;
    private PartyPolicy adoptedPolicy;

    private readonly ConcurrentQueue<PendingAlert> pendingAlerts = new();
    private readonly ConcurrentQueue<PendingToast> pendingToasts = new();
    private readonly ConcurrentQueue<CallControl> pendingControls = new();
    private readonly ConcurrentQueue<QueueSuggestion> trustedSuggestions = new();
    private readonly ConcurrentQueue<int> pendingReactions = new();

    private bool awaitingHostAck;
    private bool partyOpen;
    private string? accountToken;

    internal WatchAlongSession(AethernetSession session, Configuration configuration, ConfirmService confirm,
        VideoPlayer video, AetherStreamQueue queue, StreamSignalRouter stream, ScreenController screen)
    {
        this.session = session;
        this.configuration = configuration;
        this.confirm = confirm;
        this.video = video;
        this.queue = queue;
        this.stream = stream;
        this.screen = screen;
        accountToken = session.Token;
        session.Changed += OnSessionChanged;
        queue.Changed += RequestPublish;
        stream.Joined += OnJoined;
        stream.Declined += OnDeclined;
        stream.RosterReceived += OnRoster;
        stream.LeftReceived += OnLeft;
        stream.StateReceived += OnState;
        stream.Ended += OnEnded;
        stream.NearbyReceived += OnNearby;
        stream.JoinRequested += OnJoinRequested;
        stream.JoinPending += OnJoinPending;
        stream.QueueSuggested += OnQueueSuggested;
        stream.QueueSuggestionResult += OnQueueSuggestionResult;
        stream.Kicked += OnKicked;
        stream.ViewerFailed += OnViewerFailed;
        stream.HostChanged += OnHostChanged;
        stream.ControlRequested += OnControlRequested;
        stream.Reacted += OnReacted;
        stream.ConnectedChanged += OnConnectedChanged;
    }

    internal WatchAlongMode Mode { get; private set; } = WatchAlongMode.None;
    internal bool IsHosting => Mode == WatchAlongMode.Hosting;
    internal bool IsViewing => Mode == WatchAlongMode.Viewing;
    internal bool IsPartyOpen => partyOpen;
    internal bool HasCompany => Roster.Count > 1;
    internal bool InParty => IsViewing || partyOpen || (IsHosting && HasCompany);
    private bool InSession => Mode != WatchAlongMode.None || awaitingHostAck || partyOpen || IsJoining;

    internal IReadOnlyList<WatchAlongParticipant> Roster { get; private set; } = [];
    internal IReadOnlyList<NearbyStream> Nearby { get; private set; } = [];
    internal IReadOnlyList<HostQueueItem> HostQueue { get; private set; } = [];

    internal VideoQueueEntry? ViewingEntry { get; private set; }

    internal LocalMediaIdentity? PendingLocalMedia { get; private set; }
    internal bool LocalMediaMismatch { get; private set; }
    internal bool HasMismatchCandidate => mismatchCandidatePath is not null;
    internal bool IsLocatingLocalMedia { get; private set; }

    internal bool IsAwaitingApproval { get; private set; }
    internal IReadOnlyList<PendingJoinRequest> PendingRequests { get; private set; } = [];
    internal IReadOnlyList<QueueSuggestion> PendingQueueSuggestions { get; private set; } = [];
    internal IReadOnlyList<ViewerFailure> ViewerFailures { get; private set; } = [];

    internal PartyReactions Reactions { get; } = new();

    internal bool ServerSupportsParty => (serverFeatures & StreamFeature.Party) != 0;

    internal bool ServerLacksParty => serverSeen && !ServerSupportsParty;

    internal string RoomCode => roomCode ?? string.Empty;

    internal bool HostScreenOutOfReach { get; private set; }

    internal float IdleGraceSeconds => PartyIdle.RemainingSeconds(idleSinceTicks, Environment.TickCount64);

    internal PartyPolicy Policy => policyAdopted ? adoptedPolicy : PartyPolicy.From(configuration);

    internal int GuestPermissions => Policy.GuestPermissions;

    internal bool IsJoining => joinRequested || IsAwaitingApproval;

    internal int RoomGuestPermissions => IsViewing ? remoteGuestPermissions : GuestPermissions;

    internal int MyPermissions => session.CurrentUser is { } me
        ? PartyPermissions.Held(remoteGuestPermissions, remoteMembers, me.Id)
        : 0;

    internal bool CanAddDirectly =>
        IsViewing && PartyPermissions.Allows(MyPermissions, StreamPermission.AddToQueue);

    internal bool CanControlPlayback => IsViewing && ServerSupportsParty
        && PartyPermissions.Allows(MyPermissions, StreamPermission.ControlPlayback);

    internal float AutoReplayInSeconds => autoReplayUrl is null
        ? 0f
        : Math.Max(0f, (autoReplayNextAtTicks - Environment.TickCount64) / 1000f);

    internal event Action<QueueSuggestion>? QueueSuggestionArrived;

    internal IReadOnlyList<WatchAlongParticipant> Watching()
    {
        if (!configuration.VideoShareWatchPresence || !session.IsSignedIn)
        {
            return [];
        }

        return Roster;
    }

    internal void RequestNearbyStreams() =>
        stream.RequestNearby(Plugin.ClientState.TerritoryType, LocationShare.CurrentWorldId());

    private void RequestPublish() => publishRequested = true;

    internal void SetPolicy(PartyPolicy policy)
    {
        if (policyAdopted)
        {
            adoptedPolicy = policy;
        }
        else
        {
            policy.Store(configuration);
        }

        RequestPublish();
    }

    internal void Join(string hostId)
    {
        PrepareToJoin();
        joinHostId = hostId;
        joinCode = null;
        stream.Join(hostId);
    }

    internal bool JoinByCode(string input)
    {
        var code = PartyCode.Normalize(input);
        if (code.Length == 0)
        {
            return false;
        }

        PrepareToJoin();
        joinHostId = null;
        joinCode = code;
        stream.JoinByCode(code);
        return true;
    }

    private void PrepareToJoin()
    {
        if (Mode == WatchAlongMode.Hosting || awaitingHostAck)
        {
            stream.Leave(session.CurrentUser?.Id);
            StopHostingLocal();
        }

        joinRequested = true;
        queue.Suspend();
    }

    internal void OpenParty()
    {
        if (Mode == WatchAlongMode.Viewing)
        {
            Leave();
        }

        partyOpen = true;
        RequestPublish();
    }

    internal void ResyncNow()
    {
        if (Mode == WatchAlongMode.Viewing && lastStateMessage is { } message)
        {
            ApplyStateSync(message, force: true);
        }
    }

    internal void RetryNow()
    {
        autoReplayUrl = null;
        video.ResetRecoveryBudget();
        ResyncNow();
    }

    internal void DismissViewerFailures() => ViewerFailures = [];

    internal void Leave()
    {
        if (Mode == WatchAlongMode.None && !awaitingHostAck && !IsJoining && !partyOpen)
        {
            return;
        }

        stream.Leave(Mode == WatchAlongMode.Viewing || IsJoining ? roomHostId : session.CurrentUser?.Id);
        LeaveLocally();
    }

    private void OnSessionChanged()
    {
        var token = session.Token;
        if (string.Equals(token, accountToken, StringComparison.Ordinal))
        {
            return;
        }

        accountToken = token;
        if (Mode == WatchAlongMode.None && !awaitingHostAck && !IsJoining && !partyOpen)
        {
            return;
        }

        LeaveLocally();
    }

    private void LeaveLocally()
    {
        if (Mode == WatchAlongMode.Viewing)
        {
            sync.Reset();
            ClearViewingState();
            video.HoldScreen = false;
            video.Stop();
        }

        queue.Resume();
        ResetRoom();
    }

    private void ResetRoom()
    {
        Mode = WatchAlongMode.None;
        awaitingHostAck = false;
        IsAwaitingApproval = false;
        partyOpen = false;
        idleSinceTicks = 0;
        Roster = [];
        PendingRequests = [];
        PendingQueueSuggestions = [];
        ViewerFailures = [];
        HostQueue = [];
        HostScreenOutOfReach = false;
        remoteParticipants = null;
        remoteMembers = null;
        remoteGuestPermissions = 0;
        roomCode = null;
        roomHostId = null;
        joinRequested = false;
        promotionPending = false;
        policyAdopted = false;
        rejoining = false;
        rejoinNextTicks = 0;
        grants.Clear();
        publishedGrants = null;
        grantsChanged = false;
        lastPublishedUrl = null;
        lastPublishedQueueCount = -1;
        lastPublishedScreen = null;
        Reactions.Clear();
        video.HoldScreen = false;
        screen.Engine.ScreenCurve = configuration.VideoScreenCurve;
        Interlocked.Exchange(ref pendingJoinSync, null);
        Interlocked.Exchange(ref pendingStateSync, null);
        Interlocked.Exchange(ref pendingHostChange, null);
        Interlocked.Exchange(ref pendingRejoinDecline, null);
        while (pendingControls.TryDequeue(out _))
        {
        }

        while (trustedSuggestions.TryDequeue(out _))
        {
        }
    }

    private void ClearViewingState()
    {
        viewingUrl = null;
        viewingPlaybackUrl = null;
        ViewingEntry = null;
        lastStateMessage = null;
        autoReplayUrl = null;
        reportedFailureUrl = null;
        ClearLocalMediaPrompt();
    }

    internal void ApproveRequest(string userId)
    {
        stream.Approve(userId);
        RemovePendingRequest(userId);
    }

    internal void DenyRequest(string userId)
    {
        stream.Deny(userId);
        RemovePendingRequest(userId);
    }

    internal void SuggestQueueItem(string url) => stream.SuggestQueueItem(url, Guid.NewGuid().ToString());

    internal void ApproveQueueSuggestion(string suggestionId)
    {
        var suggestion = FindQueueSuggestion(suggestionId);
        if (suggestion is null)
        {
            return;
        }

        AcceptSuggestion(suggestion);
        RemoveQueueSuggestion(suggestionId);
    }

    private void AcceptSuggestion(QueueSuggestion suggestion)
    {
        if (VideoUrlResolver.IsPlaylistUrl(suggestion.Url) && !VideoUrlResolver.NamesOneVideo(suggestion.Url))
        {
            if (!queue.ImportPlaylist(suggestion.Url, QueueAddMode.AddToQueue, false))
            {
                stream.DenyQueueSuggestion(suggestion.SuggestionId);
                return;
            }
        }
        else
        {
            queue.Add(queue.CreateDisplayEntry(suggestion.Url));
        }

        stream.ApproveQueueSuggestion(suggestion.SuggestionId);
        RequestPublish();
    }

    internal void DenyQueueSuggestion(string suggestionId)
    {
        stream.DenyQueueSuggestion(suggestionId);
        RemoveQueueSuggestion(suggestionId);
    }

    internal void KickParticipant(string userId)
    {
        grants.TryRemove(userId, out _);
        grantsChanged = true;
        stream.Kick(userId);
    }

    internal int GrantsFor(string userId) => grants.GetValueOrDefault(userId);

    internal void SetGrant(string userId, int permission, bool granted)
    {
        var held = grants.GetValueOrDefault(userId);
        var updated = granted ? held | permission : held & ~permission;
        updated &= StreamPermission.GrantMask;
        if (updated == 0)
        {
            grants.TryRemove(userId, out _);
        }
        else
        {
            grants[userId] = updated;
        }

        grantsChanged = true;
        RequestPublish();
    }

    internal bool CanTransferTo(WatchAlongParticipant participant) =>
        IsHosting && ServerSupportsParty && !participant.IsHost && participant.CanHost;

    internal void TransferHost(string userId)
    {
        if (IsHosting && ServerSupportsParty)
        {
            stream.Transfer(userId);
        }
    }

    internal void ControlPause(bool paused) =>
        SendControl(paused ? StreamControlAction.Pause : StreamControlAction.Play, null);

    internal void ControlSeek(double seconds) => SendControl(StreamControlAction.Seek, Math.Max(0d, seconds));

    internal void ControlNext() => SendControl(StreamControlAction.Next, null);

    private void SendControl(string action, double? positionSeconds)
    {
        var now = Environment.TickCount64;
        if (!CanControlPlayback || now - controlledAtTicks < ControlCooldownMilliseconds)
        {
            return;
        }

        controlledAtTicks = now;
        stream.Control(action, positionSeconds);
    }

    internal void SendReaction(int kind)
    {
        var now = Environment.TickCount64;
        if (Mode == WatchAlongMode.None || !ServerSupportsParty || now - reactedAtTicks < ReactionCooldownMilliseconds)
        {
            return;
        }

        reactedAtTicks = now;
        Reactions.Add(kind, now);
        stream.React(kind);
    }

    private QueueSuggestion? FindQueueSuggestion(string suggestionId)
    {
        foreach (var suggestion in PendingQueueSuggestions)
        {
            if (suggestion.SuggestionId == suggestionId)
            {
                return suggestion;
            }
        }

        return null;
    }

    private void RemoveQueueSuggestion(string suggestionId)
    {
        if (PendingQueueSuggestions.Count == 0)
        {
            return;
        }

        var updated = new List<QueueSuggestion>(PendingQueueSuggestions.Count);
        foreach (var existing in PendingQueueSuggestions)
        {
            if (existing.SuggestionId != suggestionId)
            {
                updated.Add(existing);
            }
        }

        PendingQueueSuggestions = updated;
    }

    private void RemoveQueueSuggestionsByUser(string userId)
    {
        if (PendingQueueSuggestions.Count == 0)
        {
            return;
        }

        var updated = new List<QueueSuggestion>(PendingQueueSuggestions.Count);
        foreach (var existing in PendingQueueSuggestions)
        {
            if (existing.UserId != userId)
            {
                updated.Add(existing);
            }
        }

        PendingQueueSuggestions = updated;
    }

    internal void OnFrameworkUpdate(float deltaSeconds)
    {
        DrainSocketWork();
        TrackConnection(Environment.TickCount64);
        video.HoldScreen = InParty;

        if (Mode == WatchAlongMode.Viewing)
        {
            StepViewerSync(deltaSeconds);
            return;
        }

        if (IsAwaitingApproval)
        {
            return;
        }

        if (!HoldRoomOpen())
        {
            return;
        }

        heartbeatTimer += deltaSeconds;
        tickCounter++;
        if (tickCounter < CheckEveryTicks)
        {
            return;
        }

        tickCounter = 0;
        PublishHostStateIfNeeded();
    }

    private bool HoldRoomOpen()
    {
        var idle = queue.Current is null && !video.HasMedia;
        if (!idle || partyOpen)
        {
            idleSinceTicks = 0;
            return true;
        }

        if (Mode != WatchAlongMode.Hosting || !HasCompany)
        {
            if (Mode == WatchAlongMode.Hosting || awaitingHostAck)
            {
                Leave();
            }

            return false;
        }

        var now = Environment.TickCount64;
        if (idleSinceTicks == 0)
        {
            idleSinceTicks = now;
        }

        if (PartyIdle.Expired(idleSinceTicks, now))
        {
            Leave();
            return false;
        }

        return true;
    }

    private void OnConnectedChanged(bool connected)
    {
        if (!connected)
        {
            Interlocked.Increment(ref dropCount);
        }
    }

    private void TrackConnection(long now)
    {
        var drops = Volatile.Read(ref dropCount);
        if (drops != seenDropCount)
        {
            seenDropCount = drops;
            resumePending = InSession;
        }

        if (!stream.Connected)
        {
            return;
        }

        if (resumePending)
        {
            resumePending = false;
            ResumeAfterDrop();
        }

        if (rejoinNextTicks != 0 && now >= rejoinNextTicks)
        {
            rejoinNextTicks = 0;
            SendRejoin();
        }
    }

    private void ResumeAfterDrop()
    {
        if (Mode == WatchAlongMode.Hosting || awaitingHostAck || partyOpen)
        {
            publishRequested = true;
            tickCounter = CheckEveryTicks;
            return;
        }

        if (Mode == WatchAlongMode.Viewing)
        {
            rejoinAttempts = 0;
            SendRejoin();
            return;
        }

        if (!IsJoining)
        {
            return;
        }

        if (joinHostId is { } hostId)
        {
            stream.Join(hostId);
        }
        else if (joinCode is { } code)
        {
            stream.JoinByCode(code);
        }
    }

    private void SendRejoin()
    {
        if (Mode != WatchAlongMode.Viewing || roomHostId is not { } hostId)
        {
            rejoining = false;
            return;
        }

        rejoining = true;
        rejoinAttempts++;
        stream.Join(hostId);
    }

    private void ApplyRejoinDecline(CallControl message)
    {
        if (Mode != WatchAlongMode.Viewing)
        {
            rejoining = false;
            return;
        }

        if (message.Reason == StreamDeclineReason.Unavailable && rejoinAttempts < MaxRejoinAttempts)
        {
            rejoinNextTicks = Environment.TickCount64 + RejoinRetryMilliseconds;
            return;
        }

        rejoining = false;
        EndRoom();
    }

    private void DrainSocketWork()
    {
        if (Interlocked.Exchange(ref pendingHostChange, null) is { } hostChange)
        {
            ApplyHostChange(hostChange);
        }

        if (Interlocked.Exchange(ref pendingRejoinDecline, null) is { } rejoinDecline)
        {
            ApplyRejoinDecline(rejoinDecline);
        }

        if (Interlocked.Exchange(ref pendingJoinSync, null) is { } joinMessage)
        {
            ApplyJoinSync(joinMessage);
        }

        if (Interlocked.Exchange(ref pendingStateSync, null) is { } stateMessage)
        {
            ApplyStateSync(stateMessage, force: false);
        }

        if (pendingViewerStop)
        {
            pendingViewerStop = false;
            sync.Reset();
            video.HoldScreen = false;
            video.Stop();
            Reactions.Clear();
            screen.Engine.ScreenCurve = configuration.VideoScreenCurve;
        }

        while (pendingAlerts.TryDequeue(out var alert))
        {
            confirm.Alert(Loc.T(alert.Title), Loc.T(alert.Body), Loc.T(L.Phone.OutcomeDismiss));
        }

        while (pendingToasts.TryDequeue(out var toast))
        {
            ShellToast.Show(toast.Argument is null
                ? Loc.T(toast.Text)
                : string.Format(Loc.Culture, Loc.T(toast.Text), toast.Argument));
        }

        while (pendingControls.TryDequeue(out var control))
        {
            ApplyControlRequest(control);
        }

        while (trustedSuggestions.TryDequeue(out var suggestion))
        {
            if (Mode == WatchAlongMode.Hosting)
            {
                AcceptSuggestion(suggestion);
            }
        }

        var now = Environment.TickCount64;
        while (pendingReactions.TryDequeue(out var reaction))
        {
            Reactions.Add(reaction, now);
        }
    }

    private void StepViewerSync(float deltaSeconds)
    {
        ReportPlaybackFailureIfNeeded();
        TickAutoReplay();
        if (lastStateMessage is not { } message || viewingPlaybackUrl is null
            || video.State != VideoPlaybackState.Playing || (message.Paused ?? false)
            || message.PositionSeconds is not { } position || message.StateAtUnixMs is not { } stamp)
        {
            ReleaseSync();
            return;
        }

        var age = StateAgeSeconds(stamp);
        if (age > StaleStateSeconds)
        {
            ReleaseSync();
            return;
        }

        var progress = video.Progress;
        var target = Math.Max(0d, position + age);
        var decision = sync.Step(progress.Position, target, progress.Duration, progress.Seeking, deltaSeconds);
        if (decision.SpeedChanged)
        {
            video.SetSpeed(decision.Speed);
        }

        if (decision.Seek)
        {
            video.Seek(decision.SeekTarget);
        }
    }

    private void ReleaseSync()
    {
        if (sync.Release())
        {
            video.SetSpeed(1d);
        }
    }

    private void ReportPlaybackFailureIfNeeded()
    {
        if (video.State != VideoPlaybackState.Failed || viewingUrl is not { } url || reportedFailureUrl == url)
        {
            return;
        }

        reportedFailureUrl = url;
        stream.ReportPlaybackFailure(url, video.LastError);
    }

    private void TickAutoReplay()
    {
        if (autoReplayUrl is not null && video.State == VideoPlaybackState.Playing)
        {
            autoReplayUrl = null;
        }

        if (video.State != VideoPlaybackState.Failed || viewingPlaybackUrl is not { } failedUrl
            || lastStateMessage is not { } message)
        {
            return;
        }

        if (PlaybackFailureClassifier.RetryCannotHelp(video.FailureKind))
        {
            autoReplayUrl = null;
            return;
        }

        var now = Environment.TickCount64;
        var botCheck = video.FailureKind == PlaybackFailureKind.BotCheck;
        if (autoReplayUrl != failedUrl)
        {
            autoReplayUrl = failedUrl;
            autoReplayDelayMilliseconds = botCheck
                ? AutoReplayBotCheckDelayMilliseconds
                : AutoReplayInitialDelayMilliseconds;
            autoReplayNextAtTicks = now + autoReplayDelayMilliseconds;
            return;
        }

        if (botCheck && autoReplayDelayMilliseconds < AutoReplayBotCheckDelayMilliseconds)
        {
            autoReplayDelayMilliseconds = AutoReplayBotCheckDelayMilliseconds;
            autoReplayNextAtTicks = now + autoReplayDelayMilliseconds;
            return;
        }

        if (now < autoReplayNextAtTicks)
        {
            return;
        }

        var maxDelayMilliseconds = botCheck ? AutoReplayBotCheckMaxDelayMilliseconds : AutoReplayMaxDelayMilliseconds;
        autoReplayDelayMilliseconds = Math.Min(autoReplayDelayMilliseconds * 2, maxDelayMilliseconds);
        autoReplayNextAtTicks = now + autoReplayDelayMilliseconds;
        video.Play(failedUrl, ProjectRemotePosition(message), !(message.Paused ?? false));
    }

    private void AbsorbServerClock(CallControl message)
    {
        if (message.StateAtUnixMs is { } stamp)
        {
            serverClock.Absorb(stamp, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }
    }

    private double StateAgeSeconds(long stampUnixMs)
    {
        var serverNow = serverClock.ServerNowUnixMs(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        return Math.Max(0d, (serverNow - stampUnixMs) / 1000d);
    }

    private StreamScreenPose? CurrentScreenPose()
    {
        var engine = screen.Engine;
        if (!engine.IsActive)
        {
            return null;
        }

        return new StreamScreenPose(engine.ScreenPosition, engine.ScreenYaw, engine.ScreenPitch, engine.ScreenRoll,
            engine.ScreenScale, engine.ScreenCurve);
    }

    private static bool ScreenMoved(StreamScreenPose? current, StreamScreenPose? published)
    {
        if (current is not { } now)
        {
            return false;
        }

        if (published is not { } before)
        {
            return true;
        }

        return Vector3.Distance(now.Position, before.Position) > ScreenPositionDriftTolerance
            || MathF.Abs(now.Yaw - before.Yaw) > ScreenAngleDriftTolerance
            || MathF.Abs(now.Pitch - before.Pitch) > ScreenAngleDriftTolerance
            || MathF.Abs(now.Roll - before.Roll) > ScreenAngleDriftTolerance
            || MathF.Abs(now.Scale - before.Scale) > ScreenScaleDriftTolerance
            || MathF.Abs(now.Curve - before.Curve) > ScreenScaleDriftTolerance;
    }

    private void PublishHostStateIfNeeded()
    {
        var progress = video.Progress;
        var position = (double)progress.Position;
        var paused = progress.Paused;
        var url = queue.Current is { } current ? ShareableUrl(current) : string.Empty;

        var screenPose = CurrentScreenPose();
        var screenChanged = ScreenMoved(screenPose, lastPublishedScreen);

        var policy = Policy;
        var approvalRequired = policy.ApprovalRequired;
        var sharedCount = Math.Min(queue.Entries.Count, MaxSharedQueueEntries);
        var expected = lastPublishedPaused
            ? lastPublishedPosition
            : lastPublishedPosition + (DateTime.UtcNow - lastPublishedAt).TotalSeconds;
        var jumped = url == lastPublishedUrl && Math.Abs(position - expected) > PositionJumpTolerance;

        var changed = url != lastPublishedUrl || paused != lastPublishedPaused || jumped || screenChanged
            || approvalRequired != lastPublishedApprovalRequired || sharedCount != lastPublishedQueueCount
            || publishRequested;
        var heartbeatDue = heartbeatTimer >= HeartbeatSeconds;
        if (!changed && !heartbeatDue)
        {
            return;
        }

        heartbeatTimer = 0f;
        publishRequested = false;
        if (url != lastPublishedUrl)
        {
            ViewerFailures = [];
        }

        lastPublishedUrl = url;
        lastPublishedPosition = position;
        lastPublishedAt = DateTime.UtcNow;
        lastPublishedPaused = paused;
        lastPublishedScreen = screenPose;
        lastPublishedApprovalRequired = approvalRequired;
        lastPublishedQueueCount = sharedCount;

        if (Mode != WatchAlongMode.Hosting)
        {
            awaitingHostAck = true;
        }

        publishedGuestPermissions = policy.GuestPermissions;
        stream.PublishState(new StreamPublication(url, position, paused, Plugin.ClientState.TerritoryType,
            LocationShare.CurrentWorldId(), approvalRequired, policy.Discoverable, policy.CodeEnabled,
            policy.GuestPermissions, PublishedGrants(), BuildSharedQueue(sharedCount), screenPose));
    }

    private StreamMember[]? PublishedGrants()
    {
        if (!grantsChanged)
        {
            return publishedGrants;
        }

        grantsChanged = false;
        if (grants.IsEmpty)
        {
            publishedGrants = null;
            return null;
        }

        var members = new List<StreamMember>(grants.Count);
        foreach (var pair in grants)
        {
            members.Add(new StreamMember(pair.Key, pair.Value));
        }

        publishedGrants = members.ToArray();
        return publishedGrants;
    }

    private StreamQueueEntry[] BuildSharedQueue(int count)
    {
        if (count == 0)
        {
            return [];
        }

        var entries = queue.Entries;
        var shared = new StreamQueueEntry[count];
        for (var index = 0; index < count; index++)
        {
            var entry = entries[index];
            shared[index] = new StreamQueueEntry(ShareableUrl(entry), entry.Title);
        }

        return shared;
    }

    private static string ShareableUrl(VideoQueueEntry entry)
    {
        if (entry.LocalMedia is { } identity)
        {
            return identity.Token;
        }

        return IsPlayableRemoteUrl(entry.Url) || LocalMediaToken.IsToken(entry.Url) ? entry.Url : string.Empty;
    }

    private void StopHostingLocal()
    {
        Mode = WatchAlongMode.None;
        awaitingHostAck = false;
        partyOpen = false;
        idleSinceTicks = 0;
        Roster = [];
        PendingRequests = [];
        PendingQueueSuggestions = [];
        ViewerFailures = [];
        policyAdopted = false;
        grants.Clear();
        publishedGrants = null;
        lastPublishedUrl = null;
        lastPublishedQueueCount = -1;
        lastPublishedScreen = null;
    }

    private void AbsorbRoom(CallControl message)
    {
        serverSeen = true;
        if (message.Features is { } features)
        {
            serverFeatures = features;
        }

        roomCode = message.Code;
        remoteGuestPermissions = message.GuestPermissions ?? 0;
        remoteMembers = message.Members;
    }

    private void RebuildRoster()
    {
        var participants = remoteParticipants;
        if (participants is null || participants.Length == 0)
        {
            Roster = [];
            return;
        }

        var members = remoteMembers;
        var result = new WatchAlongParticipant[participants.Length];
        for (var index = 0; index < participants.Length; index++)
        {
            var participant = participants[index];
            result[index] = new WatchAlongParticipant(participant.UserId, participant.DisplayName,
                participant.AvatarUrl, participant.Slot == 0, FlagsOf(members, participant.UserId));
        }

        Roster = result;
    }

    private static int FlagsOf(StreamMember[]? members, string userId)
    {
        if (members is null)
        {
            return 0;
        }

        for (var index = 0; index < members.Length; index++)
        {
            if (string.Equals(members[index].UserId, userId, StringComparison.Ordinal))
            {
                return members[index].Flags;
            }
        }

        return 0;
    }

    private void OnJoined(CallControl message)
    {
        AbsorbServerClock(message);
        AbsorbRoom(message);
        Mode = WatchAlongMode.Viewing;
        IsAwaitingApproval = false;
        joinRequested = false;
        rejoining = false;
        roomHostId = message.HostId;
        remoteParticipants = message.Participants;
        RebuildRoster();
        HostQueue = ToHostQueue(message.UpcomingQueue);
        lastStateMessage = message;
        Interlocked.Exchange(ref pendingJoinSync, message);
    }

    private static bool IsPlayableRemoteUrl(string url) => VideoEngine.ValidateURL(url, out _);

    private void WarnOnceForRejectedUrl(string url)
    {
        if (rejectedRemoteUrl == url)
        {
            return;
        }

        rejectedRemoteUrl = url;
        AepLog.Warning($"[WatchAlong] ignoring a stream url that is not a remote http(s) address: {url}");
    }

    private void ApplyJoinSync(CallControl message)
    {
        if (Mode != WatchAlongMode.Viewing)
        {
            return;
        }

        if (viewingUrl is not null)
        {
            ApplyStateSync(message, force: false);
            return;
        }

        if (message.Url is { Length: > 0 } url)
        {
            StartViewing(url, message);
            return;
        }

        ApplyRemoteScreenTransform(message);
    }

    private void StartViewing(string url, CallControl message)
    {
        sync.Reset();
        if (LocalMediaToken.TryParse(url, out var identity))
        {
            rejectedRemoteUrl = null;
            viewingUrl = url;
            ViewingEntry = queue.CreateDisplayEntry(url);

            if (LocalMediaFiles.TryResolve(configuration, identity, out var localPath))
            {
                ClearLocalMediaPrompt();
                viewingPlaybackUrl = localPath;
                var pausedLocal = message.Paused ?? false;
                video.Play(localPath, ProjectRemotePosition(message), !pausedLocal);
                ApplyRemoteScreenTransform(message);
                return;
            }

            viewingPlaybackUrl = null;
            PendingLocalMedia = identity;
            LocalMediaMismatch = false;
            mismatchCandidatePath = null;
            video.Stop();
            ApplyRemoteScreenTransform(message);
            return;
        }

        if (!IsPlayableRemoteUrl(url))
        {
            WarnOnceForRejectedUrl(url);
            return;
        }

        rejectedRemoteUrl = null;
        ClearLocalMediaPrompt();
        viewingUrl = url;
        viewingPlaybackUrl = url;
        ViewingEntry = queue.CreateDisplayEntry(url);

        var paused = message.Paused ?? false;
        video.Play(url, ProjectRemotePosition(message), !paused);
        ApplyRemoteScreenTransform(message);
    }

    private void ClearLocalMediaPrompt()
    {
        PendingLocalMedia = null;
        LocalMediaMismatch = false;
        mismatchCandidatePath = null;
    }

    private void OnDeclined(CallControl message)
    {
        if (rejoining)
        {
            Interlocked.Exchange(ref pendingRejoinDecline, message);
            return;
        }

        Mode = WatchAlongMode.None;
        IsAwaitingApproval = false;
        joinRequested = false;
        queue.Resume();

        switch (message.Reason)
        {
            case StreamDeclineReason.Denied:
                QueueAlert(L.AetherStream.JoinDeniedTitle, L.AetherStream.JoinDeniedBody);
                return;
            case StreamDeclineReason.BadCode:
                QueueAlert(L.AetherStream.CodeNotFoundTitle, L.AetherStream.CodeNotFoundBody);
                return;
            case StreamDeclineReason.Full:
                QueueAlert(L.AetherStream.PartyFullTitle, L.AetherStream.PartyFullBody);
                return;
            default:
                QueueAlert(L.AetherStream.StreamUnavailableTitle, L.AetherStream.StreamUnavailableBody);
                return;
        }
    }

    private void OnRoster(CallControl message)
    {
        remoteParticipants = message.Participants;
        if (Mode == WatchAlongMode.Viewing)
        {
            remoteGuestPermissions = message.GuestPermissions ?? remoteGuestPermissions;
            remoteMembers = message.Members;
        }
        else
        {
            remoteMembers = message.Members;
        }

        RebuildRoster();

        if (Mode == WatchAlongMode.Hosting)
        {
            RequestPublish();
        }

        if (PendingRequests.Count > 0)
        {
            foreach (var participant in Roster)
            {
                RemovePendingRequest(participant.UserId);
            }
        }
    }

    private void OnLeft(CallControl message)
    {
        if (Mode != WatchAlongMode.Hosting || message.UserId is not { } userId)
        {
            return;
        }

        if (grants.TryRemove(userId, out _))
        {
            grantsChanged = true;
        }

        RemovePendingRequest(userId);
        RemoveQueueSuggestionsByUser(userId);
        RemoveViewerFailure(userId);
    }

    private void OnViewerFailed(CallControl message)
    {
        if (Mode != WatchAlongMode.Hosting || message.From is not { } from || message.Url is not { Length: > 0 } url
            || url != lastPublishedUrl)
        {
            return;
        }

        var updated = new List<ViewerFailure>(ViewerFailures.Count + 1);
        foreach (var existing in ViewerFailures)
        {
            if (existing.UserId != from.UserId)
            {
                updated.Add(existing);
            }
        }

        updated.Add(new ViewerFailure(from.UserId, from.DisplayName, message.Reason));
        ViewerFailures = updated;
    }

    private void RemoveViewerFailure(string userId)
    {
        if (ViewerFailures.Count == 0)
        {
            return;
        }

        var updated = new List<ViewerFailure>(ViewerFailures.Count);
        foreach (var existing in ViewerFailures)
        {
            if (existing.UserId != userId)
            {
                updated.Add(existing);
            }
        }

        ViewerFailures = updated;
    }

    private void OnJoinRequested(CallControl message)
    {
        if ((Mode != WatchAlongMode.Hosting && !promotionPending) || message.From is not { } from)
        {
            return;
        }

        var updated = new List<PendingJoinRequest>(PendingRequests.Count + 1);
        foreach (var existing in PendingRequests)
        {
            if (existing.UserId != from.UserId)
            {
                updated.Add(existing);
            }
        }

        updated.Add(new PendingJoinRequest(from.UserId, from.DisplayName, from.AvatarUrl));
        PendingRequests = updated;
    }

    private void OnJoinPending(CallControl message)
    {
        if (Mode != WatchAlongMode.None)
        {
            return;
        }

        IsAwaitingApproval = true;
        joinRequested = false;
    }

    private void OnQueueSuggested(CallControl message)
    {
        if (Mode != WatchAlongMode.Hosting || message.From is not { } from
            || message.SuggestionId is not { Length: > 0 } suggestionId || message.Url is not { Length: > 0 } url)
        {
            return;
        }

        if (!IsPlayableRemoteUrl(url))
        {
            AepLog.Warning($"[WatchAlong] dropping a queue suggestion that is not a remote http(s) address: {url}");
            return;
        }

        var suggestion = new QueueSuggestion(suggestionId, from.UserId, from.DisplayName, url);
        var held = publishedGuestPermissions | grants.GetValueOrDefault(from.UserId);
        if (PartyPermissions.Allows(held, StreamPermission.AddToQueue))
        {
            trustedSuggestions.Enqueue(suggestion);
            return;
        }

        var replaced = false;
        var updated = new List<QueueSuggestion>(PendingQueueSuggestions.Count + 1);
        foreach (var existing in PendingQueueSuggestions)
        {
            if (existing.SuggestionId != suggestionId)
            {
                updated.Add(existing);
            }
            else
            {
                replaced = true;
            }
        }

        updated.Add(suggestion);
        PendingQueueSuggestions = updated;
        if (!replaced)
        {
            QueueSuggestionArrived?.Invoke(suggestion);
        }
    }

    private void OnQueueSuggestionResult(CallControl message)
    {
        pendingToasts.Enqueue(new PendingToast(
            message.Reason == "accepted"
                ? L.AetherStream.QueueSuggestionAcceptedBody
                : L.AetherStream.QueueSuggestionDeniedBody, null));
    }

    private void RemovePendingRequest(string userId)
    {
        if (PendingRequests.Count == 0)
        {
            return;
        }

        var updated = new List<PendingJoinRequest>(PendingRequests.Count);
        foreach (var existing in PendingRequests)
        {
            if (existing.UserId != userId)
            {
                updated.Add(existing);
            }
        }

        PendingRequests = updated;
    }

    private void OnState(CallControl message)
    {
        if (awaitingHostAck)
        {
            awaitingHostAck = false;
            Mode = WatchAlongMode.Hosting;
            AbsorbHostEcho(message);
            return;
        }

        if (Mode == WatchAlongMode.Hosting)
        {
            AbsorbHostEcho(message);
            return;
        }

        if (Mode != WatchAlongMode.Viewing)
        {
            return;
        }

        AbsorbServerClock(message);
        AbsorbRoom(message);
        RebuildRoster();
        lastStateMessage = message;
        Interlocked.Exchange(ref pendingStateSync, message);
    }

    private void AbsorbHostEcho(CallControl message)
    {
        serverSeen = true;
        if (message.Features is { } features)
        {
            serverFeatures = features;
        }

        roomCode = message.Code;
        remoteMembers = message.Members;
        RebuildRoster();
    }

    private void OnHostChanged(CallControl message)
    {
        if (Mode == WatchAlongMode.None)
        {
            return;
        }

        if (message.HostId is { } hostId)
        {
            roomHostId = hostId;
            if (Mode == WatchAlongMode.Viewing && hostId == session.CurrentUser?.Id)
            {
                promotionPending = true;
            }
        }

        Interlocked.Exchange(ref pendingHostChange, message);
    }

    private void ApplyHostChange(CallControl message)
    {
        var myId = session.CurrentUser?.Id;
        var wasHosting = Mode == WatchAlongMode.Hosting;
        AbsorbServerClock(message);
        AbsorbRoom(message);
        remoteParticipants = message.Participants;
        RebuildRoster();

        if (myId is not null && message.HostId == myId && Mode == WatchAlongMode.Viewing)
        {
            BecomeHost(message);
            promotionPending = false;
            return;
        }

        promotionPending = false;
        if (myId is not null && message.UserId == myId && wasHosting)
        {
            BecomeViewer(message);
            return;
        }

        if (!wasHosting)
        {
            PendingRequests = [];
        }

        HostQueue = ToHostQueue(message.UpcomingQueue);
        lastStateMessage = message;
        if (HostName() is { Length: > 0 } hostName)
        {
            pendingToasts.Enqueue(new PendingToast(L.AetherStream.HostChangedToast, hostName));
        }
    }

    private void BecomeHost(CallControl message)
    {
        ReleaseSync();
        sync.Reset();
        Interlocked.Exchange(ref pendingJoinSync, null);
        Interlocked.Exchange(ref pendingStateSync, null);

        IReadOnlyList<HostQueueItem> inherited = ToHostQueue(message.UpcomingQueue);
        var upcoming = new List<VideoQueueEntry>(inherited.Count);
        for (var index = 0; index < inherited.Count; index++)
        {
            var item = inherited[index];
            if (item.Url.Length == 0)
            {
                continue;
            }

            var entry = queue.CreateDisplayEntry(item.Url);
            if (item.Title.Length > 0 && item.Title != item.Url)
            {
                entry.Title = item.Title;
            }

            upcoming.Add(entry);
        }

        var roomUrl = message.Url is { Length: > 0 } url ? url : null;
        var followed = roomUrl is not null && viewingUrl == roomUrl;
        var inheritedPlaying = roomUrl is null
            ? null
            : followed && ViewingEntry is not null ? ViewingEntry : queue.CreateDisplayEntry(roomUrl);
        var playing = followed && viewingPlaybackUrl is not null && video.HasMedia ? inheritedPlaying : null;
        var reloadInherited = playing is null && inheritedPlaying is not null;
        var resumeAt = ProjectRemotePosition(message);
        if (reloadInherited)
        {
            upcoming.Insert(0, inheritedPlaying!);
        }

        adoptedPolicy = new PartyPolicy(
            message.ApprovalRequired ?? configuration.VideoStreamApprovalRequired,
            message.Discoverable ?? configuration.VideoStreamDiscoverable,
            message.CodeEnabled ?? message.Code is { Length: > 0 },
            (message.GuestPermissions ?? 0) & PartyPolicy.GuestMask);
        policyAdopted = true;
        roomHostId = session.CurrentUser?.Id;

        ClearViewingState();
        HostQueue = [];
        HostScreenOutOfReach = false;
        Mode = WatchAlongMode.Hosting;
        awaitingHostAck = false;
        IsAwaitingApproval = false;
        idleSinceTicks = 0;
        grants.Clear();
        publishedGrants = null;
        grantsChanged = false;
        lastPublishedUrl = null;
        lastPublishedQueueCount = -1;
        lastPublishedScreen = null;
        heartbeatTimer = HeartbeatSeconds;
        tickCounter = CheckEveryTicks;
        queue.AdoptParty(playing, upcoming);
        if (reloadInherited)
        {
            queue.AdvanceFrom(resumeAt);
        }

        RequestPublish();
        pendingToasts.Enqueue(new PendingToast(L.AetherStream.YouAreHostToast, null));
    }

    private void BecomeViewer(CallControl message)
    {
        var sharedCount = Math.Max(lastPublishedQueueCount, 0);
        var playing = queue.HandOver(sharedCount);
        var playingUrl = video.HasMedia ? screen.Engine.GetCurrentUrl() : null;

        Mode = WatchAlongMode.Viewing;
        partyOpen = false;
        policyAdopted = false;
        awaitingHostAck = false;
        idleSinceTicks = 0;
        PendingRequests = [];
        PendingQueueSuggestions = [];
        ViewerFailures = [];
        grants.Clear();
        publishedGrants = null;
        grantsChanged = false;
        lastPublishedUrl = null;
        lastPublishedQueueCount = -1;
        lastPublishedScreen = null;
        sync.Reset();

        ViewingEntry = playingUrl is not null ? playing : null;
        viewingPlaybackUrl = playingUrl;
        viewingUrl = playingUrl is not null && message.Url is { Length: > 0 } url ? url : null;
        lastStateMessage = message;
        HostQueue = ToHostQueue(message.UpcomingQueue);
        if (HostName() is { Length: > 0 } hostName)
        {
            pendingToasts.Enqueue(new PendingToast(L.AetherStream.HostChangedToast, hostName));
        }
    }

    internal string? HostName()
    {
        var roster = Roster;
        for (var index = 0; index < roster.Count; index++)
        {
            if (roster[index].IsHost)
            {
                return roster[index].DisplayName;
            }
        }

        return null;
    }

    private void OnControlRequested(CallControl message)
    {
        if (Mode == WatchAlongMode.Hosting)
        {
            pendingControls.Enqueue(message);
        }
    }

    private void ApplyControlRequest(CallControl message)
    {
        if (Mode != WatchAlongMode.Hosting || message.From is not { } from || message.Action is not { } action)
        {
            return;
        }

        var held = GuestPermissions | grants.GetValueOrDefault(from.UserId);
        if (!PartyPermissions.Allows(held, StreamPermission.ControlPlayback))
        {
            return;
        }

        switch (action)
        {
            case StreamControlAction.Play:
                if (video.HasMedia)
                {
                    video.Pause(false);
                }

                break;
            case StreamControlAction.Pause:
                if (video.HasMedia)
                {
                    video.Pause(true);
                }

                break;
            case StreamControlAction.Seek:
                if (video.HasMedia && message.PositionSeconds is { } seconds && double.IsFinite(seconds))
                {
                    video.Seek(Math.Max(0d, seconds));
                }

                break;
            case StreamControlAction.Next:
                if (queue.HasNext)
                {
                    queue.Advance();
                }

                break;
            default:
                return;
        }

        RequestPublish();
    }

    private void OnReacted(CallControl message)
    {
        if (Mode != WatchAlongMode.None && message.Reaction is { } reaction)
        {
            pendingReactions.Enqueue(reaction);
        }
    }

    private double ProjectRemotePosition(CallControl message)
    {
        if (message.PositionSeconds is not { } position)
        {
            return 0d;
        }

        if ((message.Paused ?? false) || message.StateAtUnixMs is not { } stamp)
        {
            return Math.Max(0d, position);
        }

        return Math.Max(0d, position + Math.Min(StateAgeSeconds(stamp), StaleStateSeconds));
    }

    private void ApplyStateSync(CallControl message, bool force)
    {
        if (Mode != WatchAlongMode.Viewing)
        {
            return;
        }

        HostQueue = ToHostQueue(message.UpcomingQueue);

        if (message.Url is { Length: > 0 } url)
        {
            if (url != viewingUrl || force)
            {
                StartViewing(url, message);
                return;
            }
        }
        else if (viewingUrl is not null)
        {
            viewingUrl = null;
            viewingPlaybackUrl = null;
            ViewingEntry = null;
            ClearLocalMediaPrompt();
            video.Stop();
            ApplyRemoteScreenTransform(message);
            return;
        }

        if (viewingUrl is null)
        {
            ApplyRemoteScreenTransform(message);
            return;
        }

        if (message.Paused is { } paused && video.HasMedia)
        {
            video.Pause(paused);
        }

        ApplyRemoteScreenTransform(message);
    }

    internal void LocateLocalMedia(string path)
    {
        if (PendingLocalMedia is not { } expected || IsLocatingLocalMedia)
        {
            return;
        }

        IsLocatingLocalMedia = true;
        _ = MatchLocalMediaAsync(expected, path);
    }

    private async Task MatchLocalMediaAsync(LocalMediaIdentity expected, string path)
    {
        var picked = await Task.Run(() => LocalMediaToken.TryCompute(path)).ConfigureAwait(false);
        await Plugin.Framework.RunOnFrameworkThread(() =>
        {
            IsLocatingLocalMedia = false;
            if (PendingLocalMedia is not { } pending || pending.MapKey != expected.MapKey)
            {
                return;
            }

            if (picked is null)
            {
                LocalMediaMismatch = true;
                mismatchCandidatePath = null;
                return;
            }

            if (picked.Matches(expected))
            {
                LocalMediaFiles.Remember(configuration, expected, path, picked.SizeBytes);
                ReapplyAfterLocalResolve();
                return;
            }

            LocalMediaMismatch = true;
            mismatchCandidatePath = path;
            mismatchCandidateSizeBytes = picked.SizeBytes;
        }).ConfigureAwait(false);
    }

    internal void AcceptMismatchedLocalMedia()
    {
        if (PendingLocalMedia is not { } expected || mismatchCandidatePath is not { } path)
        {
            return;
        }

        LocalMediaFiles.Remember(configuration, expected, path, mismatchCandidateSizeBytes);
        ReapplyAfterLocalResolve();
    }

    private void ReapplyAfterLocalResolve()
    {
        ClearLocalMediaPrompt();
        if (Mode == WatchAlongMode.Viewing && lastStateMessage is { } message)
        {
            ApplyStateSync(message, force: true);
        }
    }

    private void ApplyRemoteScreenTransform(CallControl message)
    {
        if (message is not { ScreenX: { } x, ScreenY: { } y, ScreenZ: { } z })
        {
            return;
        }

        if (!configuration.VideoFollowHostScreen)
        {
            HostScreenOutOfReach = false;
            return;
        }

        var position = new Vector3(x, y, z);
        if (Plugin.ObjectTable.LocalPlayer is { } localPlayer
            && Vector3.Distance(localPlayer.Position, position) > HostScreenReach)
        {
            HostScreenOutOfReach = true;
            return;
        }

        HostScreenOutOfReach = false;
        var engine = screen.Engine;
        engine.ApplyRemoteScreenPose(
            new ScreenPose(position, message.ScreenYaw ?? 0f, message.ScreenPitch ?? engine.ScreenPitch,
                message.ScreenRoll ?? engine.ScreenRoll, message.ScreenScale ?? 1f), message.ScreenCurve);
    }

    private void OnNearby(CallControl message)
    {
        var streams = message.NearbyStreams;
        if (streams is null || streams.Length == 0)
        {
            Nearby = [];
            return;
        }

        var result = new NearbyStream[streams.Length];
        for (var index = 0; index < streams.Length; index++)
        {
            var entry = streams[index];
            result[index] = new NearbyStream(entry.HostId, entry.DisplayName,
                entry.Handle.Length > 0 ? "@" + entry.Handle : string.Empty, entry.AvatarUrl);
        }

        Nearby = result;
    }

    private static HostQueueItem[] ToHostQueue(StreamQueueEntry[]? entries)
    {
        if (entries is null || entries.Length == 0)
        {
            return [];
        }

        var items = new List<HostQueueItem>(entries.Length);
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            var url = entry.Url ?? string.Empty;
            var title = entry.Title ?? string.Empty;
            if (url.Length == 0 && title.Length == 0)
            {
                continue;
            }

            items.Add(new HostQueueItem(url, title.Length > 0 ? title : url));
        }

        return items.ToArray();
    }

    private void OnEnded(CallControl message) => EndRoom();

    private void EndRoom()
    {
        if (Mode == WatchAlongMode.Viewing)
        {
            ClearViewingState();
            pendingViewerStop = true;
            pendingToasts.Enqueue(new PendingToast(L.AetherStream.PartyEndedToast, null));
        }

        queue.Resume();
        ResetRoomFromSocket();
    }

    private void OnKicked(CallControl message)
    {
        if (Mode == WatchAlongMode.Viewing)
        {
            ClearViewingState();
            pendingViewerStop = true;
        }

        queue.Resume();
        ResetRoomFromSocket();
        QueueAlert(L.AetherStream.KickedTitle, L.AetherStream.KickedBody);
    }

    private void ResetRoomFromSocket()
    {
        Mode = WatchAlongMode.None;
        IsAwaitingApproval = false;
        partyOpen = false;
        idleSinceTicks = 0;
        Roster = [];
        PendingRequests = [];
        PendingQueueSuggestions = [];
        ViewerFailures = [];
        HostQueue = [];
        HostScreenOutOfReach = false;
        remoteParticipants = null;
        remoteMembers = null;
        remoteGuestPermissions = 0;
        roomCode = null;
        roomHostId = null;
        joinRequested = false;
        promotionPending = false;
        policyAdopted = false;
        rejoining = false;
        Interlocked.Exchange(ref pendingJoinSync, null);
        Interlocked.Exchange(ref pendingStateSync, null);
        Interlocked.Exchange(ref pendingHostChange, null);
    }

    private void QueueAlert(LocString title, LocString body) => pendingAlerts.Enqueue(new PendingAlert(title, body));

    public void Dispose()
    {
        session.Changed -= OnSessionChanged;
        queue.Changed -= RequestPublish;
        stream.Joined -= OnJoined;
        stream.Declined -= OnDeclined;
        stream.RosterReceived -= OnRoster;
        stream.LeftReceived -= OnLeft;
        stream.StateReceived -= OnState;
        stream.Ended -= OnEnded;
        stream.NearbyReceived -= OnNearby;
        stream.JoinRequested -= OnJoinRequested;
        stream.JoinPending -= OnJoinPending;
        stream.QueueSuggested -= OnQueueSuggested;
        stream.QueueSuggestionResult -= OnQueueSuggestionResult;
        stream.Kicked -= OnKicked;
        stream.ViewerFailed -= OnViewerFailed;
        stream.HostChanged -= OnHostChanged;
        stream.ControlRequested -= OnControlRequested;
        stream.Reacted -= OnReacted;
        stream.ConnectedChanged -= OnConnectedChanged;
    }
}
