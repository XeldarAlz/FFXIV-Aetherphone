using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Telephony.Contracts;

namespace Aetherphone.Core.Video;

// Shape mirrors Telephony's ParticipantInfo - kept distinct so the video layer never needs to
// know about Telephony's contracts directly. IsHost is decided from the server's own slot 0
// convention (see StreamSignalRouter), not fabricated locally.
internal sealed record WatchAlongParticipant(string UserId, string Name, string World, string DisplayName,
    string? AvatarUrl, bool IsHost);

// Shape mirrors Telephony's NearbyStreamInfo, same reasoning as WatchAlongParticipant above.
internal sealed record NearbyStream(string HostId, string Name, string World, string DisplayName);

// A viewer awaiting the host's approve/deny decision (see SignalType.StreamJoinRequest's doc
// comment). Name/World always arrive empty here - the server has never carried real name/world in
// the From it sends on stream.joinRequest, only DisplayName (same as call.incoming) - keep them on
// the record for shape-parity with WatchAlongParticipant, but render DisplayName only.
internal sealed record PendingJoinRequest(string UserId, string Name, string World, string DisplayName);

// A viewer's proposed queue URL awaiting the host's approve/deny decision (see
// SignalType.StreamQueueSuggest's doc comment). Same empty-Name/World caveat as PendingJoinRequest
// above applies to stream.queueSuggestion's From too.
internal sealed record QueueSuggestion(string SuggestionId, string UserId, string Name, string World,
    string DisplayName, string Url);

internal enum WatchAlongMode : byte
{
    None,
    Hosting,
    Viewing,
}

// Real cross-player watch-along, built on StreamSignalRouter's stream.* signals (server-side as
// of the dev-branch deploy - see the spec that shipped this file). A connection is host XOR
// viewer, never both, mirroring the server's own "joining ends your own stream" policy - Mode
// tracks that locally so the UI and the framework tick agree on which role is active.
internal sealed class WatchAlongSession : IDisposable
{
    private const int CheckEveryTicks = 30; // ~2x/sec at 60fps, matches AetherStreamQueue's own throttle
    private const float HeartbeatSeconds = 8f;
    private const double PositionDriftToleranceSeconds = 3.0;

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

    private int tickCounter;
    private float heartbeatTimer;
    private string? lastPublishedUrl;
    private double lastPublishedPosition;
    private bool lastPublishedPaused;
    private Vector3? lastPublishedScreenPosition;
    private float lastPublishedScreenYaw;
    private float lastPublishedScreenScale;
    private bool lastPublishedApprovalRequired;
    private string? viewingUrl;
    // Last stream.state actually applied while Viewing - lets ResyncNow() force a re-apply
    // (re-seek, re-pause, re-place the screen) without waiting on the next heartbeat. Reference
    // assignment/read of an immutable record is safe to share between OnState's background thread
    // and the main-thread button tap without extra locking; a one-message-stale read is harmless
    // here, unlike pendingJoinSync/pendingStateSync which need exact single-consume handoff.
    private CallControl? lastStateMessage;

    // stream.joined/stream.state arrive on RealtimeConnection's background receive loop
    // (Task.Run in RealtimeConnection.cs), never the game's main/Framework thread - but
    // video.Play/Seek/Pause and the screen's D3D-backed painter both require the main thread
    // (Dalamud's ObjectTable access and D3D11 resource creation both assert this and throw
    // "not on main thread" otherwise). OnJoined/OnState only stash the message here; the actual
    // engine calls happen in OnFrameworkUpdate, which does run on the main thread.
    private CallControl? pendingJoinSync;
    private CallControl? pendingStateSync;
    // Same reasoning as pendingJoinSync/pendingStateSync above - OnEnded also touches video.Stop()
    // (which tears down the D3D-backed screen painter) directly from the background receive
    // thread. A plain volatile bool is enough here (no payload to hand off, and a worst-case
    // one-frame-late Stop is harmless) rather than the CallControl handoff the other two need.
    private volatile bool pendingViewerStop;

    // Per the protocol spec: "the host's FIRST stream.state creates the room ('go live') ... wait
    // for that echo before treating yourself as hosting." Set right before the first PublishState
    // call of a session, cleared the moment that echo comes back through OnState. Mode only
    // becomes Hosting once the server has actually confirmed it, never optimistically.
    private bool awaitingHostAck;

    // Whether the host has explicitly opened a party independent of having anything queued -
    // "function like a party": friends should be able to join and hang out in the room before
    // anyone's picked a video, not only once queue.Current is non-null. Cleared by Leave() and by
    // StopHostingLocal() (switching to viewing someone else's stream also ends this one).
    private bool partyOpen;

    public WatchAlongSession(AethernetSession session, Configuration configuration, ConfirmService confirm,
        VideoPlayer video, AetherStreamQueue queue, StreamSignalRouter stream, ScreenController screen)
    {
        this.session = session;
        this.configuration = configuration;
        this.confirm = confirm;
        this.video = video;
        this.queue = queue;
        this.stream = stream;
        this.screen = screen;
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
    }

    public WatchAlongMode Mode { get; private set; } = WatchAlongMode.None;
    public bool IsHosting => Mode == WatchAlongMode.Hosting;
    public bool IsViewing => Mode == WatchAlongMode.Viewing;
    public IReadOnlyList<WatchAlongParticipant> Roster { get; private set; } = Array.Empty<WatchAlongParticipant>();
    public IReadOnlyList<NearbyStream> Nearby { get; private set; } = Array.Empty<NearbyStream>();

    // What a viewer is actually watching, for display only (Player tab's Now Playing card) - kept
    // separate from queue.Current on purpose, see AetherStreamQueue.CreateDisplayEntry's doc
    // comment on why writing this into the queue itself would be unsafe.
    public VideoQueueEntry? ViewingEntry { get; private set; }

    // Host-approval extension state (see PendingJoinRequest's doc comment).
    public bool IsAwaitingApproval { get; private set; }
    public IReadOnlyList<PendingJoinRequest> PendingRequests { get; private set; } = Array.Empty<PendingJoinRequest>();

    // Shared-queue extension state (see QueueSuggestion's doc comment).
    public IReadOnlyList<QueueSuggestion> PendingQueueSuggestions { get; private set; } =
        Array.Empty<QueueSuggestion>();

    // Only meaningful while sharing is on and signed in - showing a roster from a room the local
    // player has no visibility into, or while there's no identity to attribute it to, would just
    // be misleading chrome. Mirrors the same VideoShareWatchPresence gate documented on the
    // Settings toggle that introduced it.
    public IReadOnlyList<WatchAlongParticipant> Watching()
    {
        if (!configuration.VideoShareWatchPresence || !session.IsSignedIn)
        {
            return Array.Empty<WatchAlongParticipant>();
        }

        return Roster;
    }

    // Zone-scoped discovery - unlike Join, this needs no hostId and no mutual-contact relationship;
    // the server answers with whoever is currently hosting (see OnFrameworkUpdate's PublishState
    // call below) in the caller's own current territory. Response lands in Nearby via OnNearby.
    public void RequestNearbyStreams() => stream.RequestNearby(Plugin.ClientState.TerritoryType);

    public void Join(string hostId)
    {
        if (Mode == WatchAlongMode.Hosting)
        {
            // The server would end our own room anyway once the join lands - reflect that
            // locally right away instead of waiting for the stream.ended round-trip.
            StopHostingLocal();
        }

        stream.Join(hostId);
    }

    // Host only - opens a room independent of having anything queued yet, so friends can join and
    // wait together before a video is picked ("function like a party"). If currently watching
    // someone else, that gets left first, same as Join() does for the reverse direction.
    public void OpenParty()
    {
        if (Mode == WatchAlongMode.Viewing)
        {
            Leave();
        }

        partyOpen = true;
    }

    // Viewer only - forces a re-apply of the last known stream.state (re-seek, re-pause, re-place
    // the screen) instead of waiting on the next heartbeat. A manual safety valve for whenever
    // drift/lag makes things look off; harmless no-op if nothing's been received yet.
    public void ResyncNow()
    {
        if (Mode == WatchAlongMode.Viewing && lastStateMessage is { } message)
        {
            ApplyStateSync(message);
        }
    }

    public void Leave()
    {
        if (Mode == WatchAlongMode.None && !awaitingHostAck && !IsAwaitingApproval && !partyOpen)
        {
            return;
        }

        stream.Leave();
        if (Mode == WatchAlongMode.Viewing)
        {
            video.Stop();
            viewingUrl = null;
            ViewingEntry = null;
            lastStateMessage = null;
        }

        Mode = WatchAlongMode.None;
        awaitingHostAck = false;
        IsAwaitingApproval = false;
        partyOpen = false;
        Roster = Array.Empty<WatchAlongParticipant>();
        PendingRequests = Array.Empty<PendingJoinRequest>();
        PendingQueueSuggestions = Array.Empty<QueueSuggestion>();
        // Drop anything queued for the next OnFrameworkUpdate - applying a sync for a session that
        // just ended would resurrect video.Play/screen state right after Stop() just cleared it.
        Interlocked.Exchange(ref pendingJoinSync, null);
        Interlocked.Exchange(ref pendingStateSync, null);
    }

    // Host only - decide a pending join request.
    public void ApproveRequest(string userId)
    {
        stream.Approve(userId);
        RemovePendingRequest(userId);
    }

    // Viewer only - propose a URL for the host's queue.
    public void SuggestQueueItem(string url)
    {
        stream.SuggestQueueItem(url, Guid.NewGuid().ToString());
    }

    // Host only - approving adds it straight to the host's own local queue (there is no
    // server-tracked shared queue - once it's in the host's queue, the existing stream.state
    // sync already carries whatever the host plays to every viewer, no extra plumbing needed).
    public void ApproveQueueSuggestion(string suggestionId)
    {
        var suggestion = FindQueueSuggestion(suggestionId);
        if (suggestion is null)
        {
            return;
        }

        queue.Add(new VideoQueueEntry(suggestion.Url, suggestion.Url, string.Empty, null, null));
        stream.ApproveQueueSuggestion(suggestionId);
        RemoveQueueSuggestion(suggestionId);
    }

    public void DenyQueueSuggestion(string suggestionId)
    {
        stream.DenyQueueSuggestion(suggestionId);
        RemoveQueueSuggestion(suggestionId);
    }

    // Host only - force-remove a participant. No local roster change here - the authoritative
    // stream.roster broadcast that follows already updates Roster the same way any other
    // departure does.
    public void KickParticipant(string userId)
    {
        stream.Kick(userId);
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

    // A single viewer can have more than one suggestion outstanding (unlike join requests, which
    // are one-per-user) - see OnLeft, which is the only caller.
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

    public void DenyRequest(string userId)
    {
        stream.Deny(userId);
        RemovePendingRequest(userId);
    }

    public void OnFrameworkUpdate(float deltaSeconds)
    {
        // Must run before the Mode == Viewing branch below, which returns early - otherwise a
        // pending sync queued the same frame Mode flips to Viewing would never get applied.
        if (Interlocked.Exchange(ref pendingJoinSync, null) is { } joinMessage)
        {
            ApplyJoinSync(joinMessage);
        }

        if (Interlocked.Exchange(ref pendingStateSync, null) is { } stateMessage)
        {
            ApplyStateSync(stateMessage);
        }

        if (pendingViewerStop)
        {
            pendingViewerStop = false;
            video.Stop();
        }

        if (Mode == WatchAlongMode.Viewing)
        {
            // The user's own queue only ever gets an entry through explicit local action (URL
            // entry, local file, queue advance) - that always wins over a joined stream's
            // mirrored playback, so treat it as the user choosing to leave.
            if (queue.Current is not null)
            {
                Leave();
            }

            return;
        }

        if (IsAwaitingApproval)
        {
            // Same "local action wins" rule as the Viewing branch above, while we're parked
            // waiting on a host decision that (today) will never arrive.
            if (queue.Current is not null)
            {
                Leave();
            }

            return;
        }

        if (!partyOpen && queue.Current is null)
        {
            // awaitingHostAck also needs a Leave() here, not just Mode == Hosting - the server
            // creates the room off our very first stream.state, before the echo (and thus our
            // local Mode flip) ever arrives, so a room can already exist server-side even while
            // we're still technically "None" from our own point of view.
            //
            // Deliberately does NOT check VideoShareWatchPresence here (unlike Watching() above) -
            // that toggle's own label/hint ("Off keeps your name out of the watching list on this
            // screen") only promises to hide presence from the local display, not to stop hosting.
            // Gating publish on it too meant flipping a privacy toggle mid-stream silently ended
            // the room out from under any connected viewers with no warning.
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

        var (position, _, paused) = video.GetProgress();
        // Empty, not null - a party can be open with nothing queued yet (see partyOpen), and the
        // envelope's own url field is a plain string, not nullable.
        var url = queue.Current?.Url ?? string.Empty;

        // Only meaningful to publish while the host's own screen is actually up - an idle engine's
        // ScreenPosition is just whatever it was last left at, not a real placement.
        Vector3? screenPosition = screen.Engine.IsActive ? screen.Engine.ScreenPosition : null;
        var screenYaw = screen.Engine.ScreenYaw;
        var screenScale = screen.Engine.ScreenScale;
        var screenChanged = screenPosition is { } currentScreenPosition
            && (lastPublishedScreenPosition is not { } lastScreenPosition
                || Vector3.Distance(currentScreenPosition, lastScreenPosition) > ScreenPositionDriftTolerance
                || MathF.Abs(screenYaw - lastPublishedScreenYaw) > ScreenYawDriftTolerance
                || MathF.Abs(screenScale - lastPublishedScreenScale) > ScreenScaleDriftTolerance);

        var approvalRequired = configuration.VideoStreamApprovalRequired;
        var changed = url != lastPublishedUrl || paused != lastPublishedPaused
            || Math.Abs(position - lastPublishedPosition) > PositionDriftToleranceSeconds || screenChanged
            || approvalRequired != lastPublishedApprovalRequired;
        var heartbeatDue = heartbeatTimer >= HeartbeatSeconds;
        if (!changed && !heartbeatDue)
        {
            return;
        }

        heartbeatTimer = 0f;
        lastPublishedUrl = url;
        lastPublishedPosition = position;
        lastPublishedPaused = paused;
        lastPublishedScreenPosition = screenPosition;
        lastPublishedScreenYaw = screenYaw;
        lastPublishedScreenScale = screenScale;
        lastPublishedApprovalRequired = approvalRequired;

        // Only the very first publish of a session needs to wait for the ack - once we're already
        // confirmed Hosting, later updates are just that, updates, not another "go live" moment.
        if (Mode != WatchAlongMode.Hosting)
        {
            awaitingHostAck = true;
        }

        // Temporary diagnostic for the screen-transform sync path - remove once confirmed working
        // end to end. IsActive being false here (screenPosition null) is the single most likely
        // reason a viewer's screen stays parked at its own spawn fallback instead of following.
        AepLog.Info(screenPosition is { } sp
            ? $"[WatchAlong] host publish screen active=true pos={sp} yaw={screenYaw:F2} scale={screenScale:F2}"
            : "[WatchAlong] host publish screen active=false (screen.Engine.IsActive is false - no coords sent)");

        stream.PublishState(url, position, paused, Plugin.ClientState.TerritoryType, approvalRequired,
            screenPosition, screenPosition is not null ? screenYaw : null,
            screenPosition is not null ? screenScale : null);
    }

    private void StopHostingLocal()
    {
        Mode = WatchAlongMode.None;
        awaitingHostAck = false;
        partyOpen = false;
        Roster = Array.Empty<WatchAlongParticipant>();
        PendingRequests = Array.Empty<PendingJoinRequest>();
        PendingQueueSuggestions = Array.Empty<QueueSuggestion>();
        lastPublishedUrl = null;
    }

    private void OnJoined(CallControl message)
    {
        Mode = WatchAlongMode.Viewing;
        IsAwaitingApproval = false;
        Roster = ToParticipants(message.Participants);

        if (message.Url is { Length: > 0 })
        {
            // Actual playback deferred to OnFrameworkUpdate - see pendingJoinSync's doc comment.
            Interlocked.Exchange(ref pendingJoinSync, message);
        }
    }

    // Runs on the main thread via OnFrameworkUpdate. Only ever called with a message that already
    // passed OnJoined's Url-present check.
    private void ApplyJoinSync(CallControl message)
    {
        var url = message.Url!;
        viewingUrl = url;
        ViewingEntry = queue.CreateDisplayEntry(url);
        video.Play(url); // Spawns the screen in front of the local player first (a new session) -
        ApplyRemoteScreenTransform(message); // then immediately re-place it at the host's spot, if sent.
        if (message.PositionSeconds is { } position)
        {
            video.Seek((float)position);
        }

        video.Pause(message.Paused ?? false);
    }

    private void OnDeclined(CallControl message)
    {
        Mode = WatchAlongMode.None;
        IsAwaitingApproval = false;

        if (message.Reason == "denied")
        {
            // Unlike "full"/"unavailable" this reason is unambiguous (the host looked at the
            // request and said no), so it earns its own message instead of the deliberately
            // generic one below. Deny is not sticky server-side - a denied user may ask again,
            // throttled only by the 2s join cooldown.
            confirm.Alert(Loc.T(L.AetherStream.JoinDeniedTitle), Loc.T(L.AetherStream.JoinDeniedBody),
                Loc.T(L.Phone.OutcomeDismiss));
            return;
        }

        // Deliberately generic - a decline must never reveal whether it was a block, a full room,
        // or the host simply isn't live, per the server's own policy note.
        confirm.Alert(Loc.T(L.AetherStream.StreamUnavailableTitle), Loc.T(L.AetherStream.StreamUnavailableBody),
            Loc.T(L.Phone.OutcomeDismiss));
    }

    private void OnRoster(CallControl message)
    {
        Roster = ToParticipants(message.Participants);

        // Temporary diagnostic for the "host doesn't see who joined" report - covers both halves
        // at once: whether the server ever sent anyone besides us in the roster, and whether the
        // Watching() display gate (VideoShareWatchPresence) would hide it locally even if so.
        var names = Roster.Count == 0 ? "(none)" : string.Join(", ", Roster.Select(p => $"{p.DisplayName}{(p.IsHost ? " [host]" : string.Empty)}"));
        AepLog.Info($"[WatchAlong] roster update: {Roster.Count} participant(s): {names} " +
            $"(VideoShareWatchPresence={configuration.VideoShareWatchPresence}, signedIn={session.IsSignedIn})");

        // Defensive tidy-up: if someone we still thought was pending shows up in the authoritative
        // roster (approved through some path other than our own ApproveRequest call), drop them
        // from PendingRequests too rather than leaving a stale approve/deny row for someone
        // already in.
        if (PendingRequests.Count > 0)
        {
            foreach (var participant in Roster)
            {
                RemovePendingRequest(participant.UserId);
            }
        }
    }

    // Host only: the server telling us a pending requester (or queue suggester) dropped or
    // withdrew before we decided on them - stream.roster (handled above) already reflects the
    // room's actual membership, but says nothing about PendingRequests/PendingQueueSuggestions,
    // which would otherwise leave a stale approve/deny row that no-ops. The server forgets a
    // suggestion the same way on the suggester's departure (see SignalType.StreamQueueSuggest's
    // doc comment), so clearing it here just mirrors what the server already did.
    private void OnLeft(CallControl message)
    {
        if (Mode != WatchAlongMode.Hosting || message.UserId is not { } userId)
        {
            return;
        }

        RemovePendingRequest(userId);
        RemoveQueueSuggestionsByUser(userId);
    }

    // Host only: the server telling us someone wants to join while ApprovalRequired is on. A
    // stray one arriving after we've already stopped (a race with our own stream.leave) is
    // ignored rather than resurrecting a request list for a stream that no longer exists.
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

        updated.Add(new PendingJoinRequest(from.UserId, from.Name, from.World, from.DisplayName));
        PendingRequests = updated;
    }

    // Viewer only: the server acknowledging our stream.join is waiting on the host's decision,
    // instead of the immediate stream.joined/stream.declined verdict Open mode gives when
    // approval isn't required. Resolved by whichever of those two eventually follows (or
    // stream.ended if the host stops streaming first).
    private void OnJoinPending(CallControl message)
    {
        if (Mode != WatchAlongMode.None)
        {
            return;
        }

        IsAwaitingApproval = true;
    }

    // Host only: a viewer proposed a URL for the queue. Same "only while actually hosting"
    // reasoning as OnJoinRequested.
    private void OnQueueSuggested(CallControl message)
    {
        if (Mode != WatchAlongMode.Hosting || message.From is not { } from
            || message.SuggestionId is not { Length: > 0 } suggestionId || message.Url is not { Length: > 0 } url)
        {
            return;
        }

        var updated = new List<QueueSuggestion>(PendingQueueSuggestions.Count + 1);
        foreach (var existing in PendingQueueSuggestions)
        {
            if (existing.SuggestionId != suggestionId)
            {
                updated.Add(existing);
            }
        }

        updated.Add(new QueueSuggestion(suggestionId, from.UserId, from.Name, from.World, from.DisplayName, url));
        PendingQueueSuggestions = updated;
    }

    // Viewer only: the host's verdict on a suggestion this client made. Reason reuses the same
    // "accepted"/"denied" convention as stream.declined's Reason field rather than adding a
    // dedicated bool.
    private void OnQueueSuggestionResult(CallControl message)
    {
        if (message.Reason == "accepted")
        {
            confirm.Alert(Loc.T(L.AetherStream.QueueSuggestionAcceptedTitle),
                Loc.T(L.AetherStream.QueueSuggestionAcceptedBody), Loc.T(L.Phone.OutcomeDismiss));
            return;
        }

        confirm.Alert(Loc.T(L.AetherStream.QueueSuggestionDeniedTitle),
            Loc.T(L.AetherStream.QueueSuggestionDeniedBody), Loc.T(L.Phone.OutcomeDismiss));
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
            // The live-ack for our own first publish - this is what actually makes us "hosting"
            // per the protocol spec ("wait for that echo before treating yourself as hosting"),
            // not the optimistic PublishState call itself.
            awaitingHostAck = false;
            Mode = WatchAlongMode.Hosting;
            return;
        }

        if (Mode == WatchAlongMode.Hosting)
        {
            // A later echo of our own update - nothing else to do; publishing already reflects this.
            return;
        }

        if (Mode != WatchAlongMode.Viewing)
        {
            return;
        }

        // Actual apply deferred to OnFrameworkUpdate - see pendingStateSync's doc comment.
        lastStateMessage = message; // Cached for ResyncNow(), independent of the pending handoff.
        Interlocked.Exchange(ref pendingStateSync, message);
    }

    // Runs on the main thread via OnFrameworkUpdate. Only ever called once OnState has already
    // confirmed Mode == Viewing (or, via ResyncNow(), a re-apply of the last message under the
    // same guarantee).
    private void ApplyStateSync(CallControl message)
    {
        if (message.Url is { Length: > 0 } url && url != viewingUrl)
        {
            viewingUrl = url;
            ViewingEntry = queue.CreateDisplayEntry(url);
            video.Play(url);
        }

        if (message.PositionSeconds is { } position)
        {
            var (localPosition, _, _) = video.GetProgress();
            if (Math.Abs(localPosition - position) > PositionDriftToleranceSeconds)
            {
                video.Seek((float)position);
            }
        }

        if (message.Paused is { } paused)
        {
            video.Pause(paused);
        }

        // Live re-placement while already watching - lets a host who nudges their screen mid-stream
        // (Casting tab sliders, a Recenter tap, a preset) carry viewers along with them.
        ApplyRemoteScreenTransform(message);
    }

    // Shared by OnJoined/OnState - a no-op if the host didn't send a screen transform (their own
    // screen isn't active), matching CallControl's ScreenX/Y/Z/Yaw/Scale doc comment.
    private void ApplyRemoteScreenTransform(CallControl message)
    {
        if (message is { ScreenX: { } x, ScreenY: { } y, ScreenZ: { } z })
        {
            // Temporary diagnostic - see the matching host publish log. Pairs the two log lines up
            // so we can tell whether coords are being sent-but-not-applied vs. never sent at all.
            AepLog.Info($"[WatchAlong] viewer apply screen pos=({x:F2},{y:F2},{z:F2}) " +
                $"yaw={message.ScreenYaw:F2} scale={message.ScreenScale:F2} ownScreenActive={screen.Engine.IsActive}");
            screen.Engine.ApplyRemoteScreenTransform(new Vector3(x, y, z), message.ScreenYaw ?? 0f,
                message.ScreenScale ?? 1f);
        }
        else
        {
            AepLog.Info("[WatchAlong] viewer received stream update with no screen transform - " +
                "host's own screen not active at publish time.");
        }
    }

    private void OnNearby(CallControl message)
    {
        var streams = message.NearbyStreams;
        if (streams is null || streams.Length == 0)
        {
            Nearby = Array.Empty<NearbyStream>();
            return;
        }

        var result = new NearbyStream[streams.Length];
        for (var index = 0; index < streams.Length; index++)
        {
            var entry = streams[index];
            result[index] = new NearbyStream(entry.HostId, entry.Name, entry.World, entry.DisplayName);
        }

        Nearby = result;
    }

    private void OnEnded(CallControl message)
    {
        if (Mode == WatchAlongMode.Viewing)
        {
            viewingUrl = null;
            ViewingEntry = null;
            lastStateMessage = null;
            pendingViewerStop = true;
        }

        Mode = WatchAlongMode.None;
        IsAwaitingApproval = false;
        partyOpen = false;
        Roster = Array.Empty<WatchAlongParticipant>();
        PendingRequests = Array.Empty<PendingJoinRequest>();
        PendingQueueSuggestions = Array.Empty<QueueSuggestion>();
        Interlocked.Exchange(ref pendingJoinSync, null);
        Interlocked.Exchange(ref pendingStateSync, null);
    }

    // Viewer only: the host removed us specifically - distinct from OnEnded ("room is gone for
    // everyone"), so it gets its own message instead of the generic stream-unavailable one.
    private void OnKicked(CallControl message)
    {
        if (Mode == WatchAlongMode.Viewing)
        {
            viewingUrl = null;
            ViewingEntry = null;
            lastStateMessage = null;
            pendingViewerStop = true;
        }

        Mode = WatchAlongMode.None;
        IsAwaitingApproval = false;
        Roster = Array.Empty<WatchAlongParticipant>();
        Interlocked.Exchange(ref pendingJoinSync, null);
        Interlocked.Exchange(ref pendingStateSync, null);

        confirm.Alert(Loc.T(L.AetherStream.KickedTitle), Loc.T(L.AetherStream.KickedBody),
            Loc.T(L.Phone.OutcomeDismiss));
    }

    private static WatchAlongParticipant[] ToParticipants(ParticipantInfo[]? participants)
    {
        if (participants is null || participants.Length == 0)
        {
            return Array.Empty<WatchAlongParticipant>();
        }

        var result = new WatchAlongParticipant[participants.Length];
        for (var index = 0; index < participants.Length; index++)
        {
            var participant = participants[index];
            result[index] = new WatchAlongParticipant(participant.UserId, participant.Name, participant.World,
                participant.DisplayName, null, IsHost: participant.Slot == 0);
        }

        return result;
    }

    public void Dispose()
    {
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
    }
}
