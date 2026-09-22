using System.Collections.Concurrent;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Maps;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Telephony.Contracts;

namespace Aetherphone.Core.Video;

internal sealed record WatchAlongParticipant(string UserId, string DisplayName, string? AvatarUrl, bool IsHost);

internal sealed record NearbyStream(string HostId, string DisplayName, string Handle, string? AvatarUrl);

internal sealed record PendingJoinRequest(string UserId, string DisplayName, string? AvatarUrl);

internal sealed record QueueSuggestion(string SuggestionId, string UserId, string DisplayName, string Url);

internal sealed record HostQueueItem(string Url, string Title);

internal sealed record ViewerFailure(string UserId, string DisplayName, string? Reason);

internal readonly record struct PendingAlert(LocString Title, LocString Body);

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
    private const int MaxSharedQueueEntries = 32;
    private const int MaxLocalFileMapEntries = 64;

    private const float ScreenPositionDriftTolerance = 0.1f;
    private const float ScreenYawDriftTolerance = 0.02f;
    private const float ScreenScaleDriftTolerance = 0.02f;

    private readonly AethernetSession session;
    private readonly Configuration configuration;
    private readonly ConfirmService confirm;
    private readonly VideoPlayer video;
    private readonly AetherStreamQueue queue;
    private readonly StreamSignalRouter stream;
    private readonly ScreenController screen;
    private readonly ServerClock serverClock = new();
    private readonly PlaybackSyncController sync = new();

    private int tickCounter;
    private float heartbeatTimer;
    private string? lastPublishedUrl;
    private double lastPublishedPosition;
    private DateTime lastPublishedAt = DateTime.UtcNow;
    private bool lastPublishedPaused;
    private Vector3? lastPublishedScreenPosition;
    private float lastPublishedScreenYaw;
    private float lastPublishedScreenScale;
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
    private CallControl? lastStateMessage;

    private CallControl? pendingJoinSync;
    private CallControl? pendingStateSync;
    private volatile bool pendingViewerStop;

    private readonly ConcurrentQueue<PendingAlert> pendingAlerts = new();

    private bool awaitingHostAck;
    private bool partyOpen;

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
    }

    internal WatchAlongMode Mode { get; private set; } = WatchAlongMode.None;
    internal bool IsHosting => Mode == WatchAlongMode.Hosting;
    internal bool IsViewing => Mode == WatchAlongMode.Viewing;
    internal bool IsPartyOpen => partyOpen;

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

    internal void Join(string hostId)
    {
        if (Mode == WatchAlongMode.Hosting)
        {
            StopHostingLocal();
        }

        queue.Suspend();
        stream.Join(hostId);
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
        if (Mode == WatchAlongMode.None && !awaitingHostAck && !IsAwaitingApproval && !partyOpen)
        {
            return;
        }

        stream.Leave();
        if (Mode == WatchAlongMode.Viewing)
        {
            sync.Reset();
            video.Stop();
            ClearViewingState();
        }

        queue.Resume();
        Mode = WatchAlongMode.None;
        awaitingHostAck = false;
        IsAwaitingApproval = false;
        partyOpen = false;
        Roster = [];
        PendingRequests = [];
        PendingQueueSuggestions = [];
        ViewerFailures = [];
        HostQueue = [];
        Interlocked.Exchange(ref pendingJoinSync, null);
        Interlocked.Exchange(ref pendingStateSync, null);
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

    internal void SuggestQueueItem(string url) => stream.SuggestQueueItem(url, Guid.NewGuid().ToString());

    internal void ApproveQueueSuggestion(string suggestionId)
    {
        var suggestion = FindQueueSuggestion(suggestionId);
        if (suggestion is null)
        {
            return;
        }

        queue.Add(queue.CreateDisplayEntry(suggestion.Url));
        stream.ApproveQueueSuggestion(suggestionId);
        RemoveQueueSuggestion(suggestionId);
        RequestPublish();
    }

    internal void DenyQueueSuggestion(string suggestionId)
    {
        stream.DenyQueueSuggestion(suggestionId);
        RemoveQueueSuggestion(suggestionId);
    }

    internal void KickParticipant(string userId) => stream.Kick(userId);

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

    internal void DenyRequest(string userId)
    {
        stream.Deny(userId);
        RemovePendingRequest(userId);
    }

    internal void OnFrameworkUpdate(float deltaSeconds)
    {
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
            video.Stop();
        }

        while (pendingAlerts.TryDequeue(out var alert))
        {
            confirm.Alert(Loc.T(alert.Title), Loc.T(alert.Body), Loc.T(L.Phone.OutcomeDismiss));
        }

        if (Mode == WatchAlongMode.Viewing)
        {
            StepViewerSync(deltaSeconds);
            return;
        }

        if (IsAwaitingApproval)
        {
            return;
        }

        if (!partyOpen && queue.Current is null && !video.HasMedia)
        {
            if (Mode == WatchAlongMode.Hosting || awaitingHostAck)
            {
                Leave();
            }

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

    private void PublishHostStateIfNeeded()
    {
        var progress = video.Progress;
        var position = (double)progress.Position;
        var paused = progress.Paused;
        var url = queue.Current is { } current ? ShareableUrl(current) : string.Empty;

        var screenPosition = screen.Engine.IsActive ? screen.Engine.ScreenPosition : (Vector3?)null;
        var screenYaw = screen.Engine.ScreenYaw;
        var screenScale = screen.Engine.ScreenScale;
        var screenChanged = screenPosition is { } currentScreenPosition
            && (lastPublishedScreenPosition is not { } lastScreenPosition
                || Vector3.Distance(currentScreenPosition, lastScreenPosition) > ScreenPositionDriftTolerance
                || MathF.Abs(screenYaw - lastPublishedScreenYaw) > ScreenYawDriftTolerance
                || MathF.Abs(screenScale - lastPublishedScreenScale) > ScreenScaleDriftTolerance);

        var approvalRequired = configuration.VideoStreamApprovalRequired;
        var sharedQueue = BuildSharedQueue();
        var expected = lastPublishedPaused
            ? lastPublishedPosition
            : lastPublishedPosition + (DateTime.UtcNow - lastPublishedAt).TotalSeconds;
        var jumped = url == lastPublishedUrl && Math.Abs(position - expected) > PositionJumpTolerance;

        var changed = url != lastPublishedUrl || paused != lastPublishedPaused || jumped || screenChanged
            || approvalRequired != lastPublishedApprovalRequired || sharedQueue.Length != lastPublishedQueueCount
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
        lastPublishedScreenPosition = screenPosition;
        lastPublishedScreenYaw = screenYaw;
        lastPublishedScreenScale = screenScale;
        lastPublishedApprovalRequired = approvalRequired;
        lastPublishedQueueCount = sharedQueue.Length;

        if (Mode != WatchAlongMode.Hosting)
        {
            awaitingHostAck = true;
        }

        stream.PublishState(url, position, paused, Plugin.ClientState.TerritoryType, LocationShare.CurrentWorldId(),
            approvalRequired, configuration.VideoStreamDiscoverable, sharedQueue, screenPosition,
            screenPosition is not null ? screenYaw : null,
            screenPosition is not null ? screenScale : null);
    }

    private StreamQueueEntry[] BuildSharedQueue()
    {
        var entries = queue.Entries;
        if (entries.Count == 0)
        {
            return [];
        }

        var count = Math.Min(entries.Count, MaxSharedQueueEntries);
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
        Roster = [];
        PendingRequests = [];
        PendingQueueSuggestions = [];
        ViewerFailures = [];
        lastPublishedUrl = null;
        lastPublishedQueueCount = -1;
    }

    private void OnJoined(CallControl message)
    {
        AbsorbServerClock(message);
        Mode = WatchAlongMode.Viewing;
        IsAwaitingApproval = false;
        Roster = ToParticipants(message.Participants);
        HostQueue = ToHostQueue(message.UpcomingQueue);
        lastStateMessage = message;

        if (message.Url is { Length: > 0 })
        {
            Interlocked.Exchange(ref pendingJoinSync, message);
        }
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

    private void ApplyJoinSync(CallControl message) => StartViewing(message.Url!, message);

    private void StartViewing(string url, CallControl message)
    {
        sync.Reset();
        if (LocalMediaToken.TryParse(url, out var identity))
        {
            rejectedRemoteUrl = null;
            viewingUrl = url;
            ViewingEntry = queue.CreateDisplayEntry(url);
            ApplyRemoteScreenTransform(message);

            if (TryResolveLocalMedia(identity, out var localPath))
            {
                ClearLocalMediaPrompt();
                viewingPlaybackUrl = localPath;
                var pausedLocal = message.Paused ?? false;
                video.Play(localPath, ProjectRemotePosition(message), !pausedLocal);
                return;
            }

            viewingPlaybackUrl = null;
            PendingLocalMedia = identity;
            LocalMediaMismatch = false;
            mismatchCandidatePath = null;
            video.Stop();
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
        ApplyRemoteScreenTransform(message);

        var paused = message.Paused ?? false;
        video.Play(url, ProjectRemotePosition(message), !paused);
    }

    private void ClearLocalMediaPrompt()
    {
        PendingLocalMedia = null;
        LocalMediaMismatch = false;
        mismatchCandidatePath = null;
    }

    private void OnDeclined(CallControl message)
    {
        Mode = WatchAlongMode.None;
        IsAwaitingApproval = false;
        queue.Resume();

        if (message.Reason == "denied")
        {
            QueueAlert(L.AetherStream.JoinDeniedTitle, L.AetherStream.JoinDeniedBody);
            return;
        }

        QueueAlert(L.AetherStream.StreamUnavailableTitle, L.AetherStream.StreamUnavailableBody);
    }

    private void OnRoster(CallControl message)
    {
        Roster = ToParticipants(message.Participants);

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
        if (Mode != WatchAlongMode.Hosting || message.From is not { } from)
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

        var suggestion = new QueueSuggestion(suggestionId, from.UserId, from.DisplayName, url);
        updated.Add(suggestion);
        PendingQueueSuggestions = updated;
        if (!replaced)
        {
            QueueSuggestionArrived?.Invoke(suggestion);
        }
    }

    private void OnQueueSuggestionResult(CallControl message)
    {
        if (message.Reason == "accepted")
        {
            QueueAlert(L.AetherStream.QueueSuggestionAcceptedTitle, L.AetherStream.QueueSuggestionAcceptedBody);
            return;
        }

        QueueAlert(L.AetherStream.QueueSuggestionDeniedTitle, L.AetherStream.QueueSuggestionDeniedBody);
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
            return;
        }

        if (Mode != WatchAlongMode.Viewing)
        {
            return;
        }

        AbsorbServerClock(message);
        lastStateMessage = message;
        Interlocked.Exchange(ref pendingStateSync, message);
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
                StoreLocalMediaPath(expected, path, picked.SizeBytes);
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

        StoreLocalMediaPath(expected, path, mismatchCandidateSizeBytes);
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

    private void StoreLocalMediaPath(LocalMediaIdentity identity, string path, long sizeBytes)
    {
        var records = configuration.VideoLocalFileMap;
        for (var index = records.Count - 1; index >= 0; index--)
        {
            if (records[index].Key == identity.MapKey)
            {
                records.RemoveAt(index);
            }
        }

        records.Add(new VideoLocalFileMapRecord { Key = identity.MapKey, Path = path, SizeBytes = sizeBytes });
        while (records.Count > MaxLocalFileMapEntries)
        {
            records.RemoveAt(0);
        }

        configuration.Save();
    }

    private bool TryResolveLocalMedia(LocalMediaIdentity identity, out string path)
    {
        path = string.Empty;
        var records = configuration.VideoLocalFileMap;
        for (var index = 0; index < records.Count; index++)
        {
            var record = records[index];
            if (record.Key != identity.MapKey)
            {
                continue;
            }

            try
            {
                var file = new FileInfo(record.Path);
                if (file.Exists && file.Length == record.SizeBytes)
                {
                    path = record.Path;
                    return true;
                }
            }
            catch (Exception exception)
            {
                AepLog.Warning($"[WatchAlong] could not stat a mapped local file: {exception.Message}");
            }

            records.RemoveAt(index);
            configuration.Save();
            return false;
        }

        return false;
    }

    private void ApplyRemoteScreenTransform(CallControl message)
    {
        if (message is { ScreenX: { } x, ScreenY: { } y, ScreenZ: { } z })
        {
            screen.Engine.ApplyRemoteScreenTransform(new Vector3(x, y, z), message.ScreenYaw ?? 0f,
                message.ScreenScale ?? 1f);
        }
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
            result[index] = new NearbyStream(entry.HostId, entry.DisplayName, entry.Handle, entry.AvatarUrl);
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

    private void OnEnded(CallControl message)
    {
        if (Mode == WatchAlongMode.Viewing)
        {
            ClearViewingState();
            pendingViewerStop = true;
        }

        queue.Resume();
        Mode = WatchAlongMode.None;
        IsAwaitingApproval = false;
        partyOpen = false;
        Roster = [];
        PendingRequests = [];
        PendingQueueSuggestions = [];
        ViewerFailures = [];
        HostQueue = [];
        Interlocked.Exchange(ref pendingJoinSync, null);
        Interlocked.Exchange(ref pendingStateSync, null);
    }

    private void OnKicked(CallControl message)
    {
        if (Mode == WatchAlongMode.Viewing)
        {
            ClearViewingState();
            pendingViewerStop = true;
        }

        queue.Resume();
        Mode = WatchAlongMode.None;
        IsAwaitingApproval = false;
        Roster = [];
        HostQueue = [];
        Interlocked.Exchange(ref pendingJoinSync, null);
        Interlocked.Exchange(ref pendingStateSync, null);

        QueueAlert(L.AetherStream.KickedTitle, L.AetherStream.KickedBody);
    }

    private void QueueAlert(LocString title, LocString body) => pendingAlerts.Enqueue(new PendingAlert(title, body));

    private static WatchAlongParticipant[] ToParticipants(ParticipantInfo[]? participants)
    {
        if (participants is null || participants.Length == 0)
        {
            return [];
        }

        var result = new WatchAlongParticipant[participants.Length];
        for (var index = 0; index < participants.Length; index++)
        {
            var participant = participants[index];
            result[index] = new WatchAlongParticipant(participant.UserId, participant.DisplayName,
                participant.AvatarUrl, IsHost: participant.Slot == 0);
        }

        return result;
    }

    public void Dispose()
    {
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
    }
}
