namespace Aetherphone.Core.Telephony.Contracts;

internal static class SignalType
{
    public const string Hello = "hello";
    public const string Start = "call.start";
    public const string Invite = "call.invite";
    public const string Accept = "call.accept";
    public const string Decline = "call.decline";
    public const string Cancel = "call.cancel";
    public const string Leave = "call.leave";
    public const string Rejoin = "call.rejoin";
    public const string Mute = "call.mute";
    public const string Incoming = "call.incoming";
    public const string Ringing = "call.ringing";
    public const string Roster = "call.roster";
    public const string Accepted = "call.accepted";
    public const string Declined = "call.declined";
    public const string Left = "call.left";
    public const string Ended = "call.ended";
    public const string Handled = "call.handled";
    public const string Unavailable = "call.unavailable";
    public const string ContentRemoved = "content.removed";
    public const string ChatPing = "chat.ping";
    public const string VelvetPing = "velvet.ping";
    public const string GramPing = "gram.ping";
    public const string SocialPing = "social.ping";
    public const string MusterPing = "muster.ping";
    public const string AnnouncePing = "announce.ping";
    public const string Error = "error";

    // AetherStream watch-along - rides the same wss /rt socket and CallControl envelope as the
    // call.* signals above (see StreamSignalRouter), not a second connection.
    public const string StreamState = "stream.state";
    public const string StreamJoin = "stream.join";
    public const string StreamLeave = "stream.leave";
    public const string StreamJoined = "stream.joined";
    public const string StreamDeclined = "stream.declined";
    public const string StreamRoster = "stream.roster";
    public const string StreamLeft = "stream.left";
    public const string StreamEnded = "stream.ended";

    // Host-approval extension (approve/deny each join instead of the automatic contact-check
    // accept/decline that runs when ApprovalRequired is unset/false) - live on the dev backend as
    // of commit 95f3acd. Approval layers on top of the mutual-contact + block gate, it does not
    // replace it; a non-contact still can't reach the host. Deny is not sticky - a denied user may
    // ask again, throttled only by the 2s join cooldown. See WatchAlongSession.PendingRequests/
    // IsAwaitingApproval and CallControl.ApprovalRequired below.
    public const string StreamJoinRequest = "stream.joinRequest";
    public const string StreamApprove = "stream.approve";
    public const string StreamDeny = "stream.deny";
    public const string StreamJoinPending = "stream.joinPending";

    // Shared-queue extension, live on the dev backend as of commit 95f3acd. A viewer proposes a
    // URL; the server relays it to the host only (stream.queueSuggestion); the host approves (adds
    // it to their own local queue - see WatchAlongSession.ApproveQueueSuggestion, there is no
    // server-tracked shared queue, playback sync of whatever the host eventually plays already
    // works via stream.state) or denies it; either way the server tells the original suggester the
    // outcome (stream.queueSuggestionResult, Reason "accepted"/"denied", same reused-Reason
    // pattern as stream.declined). The server has no queue of its own either - it only remembers
    // suggestionId -> who asked, long enough to route the verdict back, and forgets it on the
    // verdict or when that viewer leaves (see WatchAlongSession.OnLeft).
    public const string StreamQueueSuggest = "stream.queueSuggest";
    public const string StreamQueueSuggestion = "stream.queueSuggestion";
    public const string StreamQueueApprove = "stream.queueApprove";
    public const string StreamQueueDeny = "stream.queueDeny";
    public const string StreamQueueSuggestionResult = "stream.queueSuggestionResult";

    // Host-kick extension, live on the dev backend as of commit 95f3acd. Host targets a specific
    // participant by UserId; the server force-removes them from the room (a stream.roster
    // broadcast to everyone else covers reflecting that removal) and tells the removed viewer
    // specifically via stream.kicked, distinct from stream.ended ("room is gone for everyone")
    // since a kick only affects the one person. A kicked user can't rejoin that room while it
    // lives - the retry comes back as stream.declined{reason:"denied"}.
    public const string StreamKick = "stream.kick";
    public const string StreamKicked = "stream.kicked";

    // Zone-scoped discovery, deliberately separate from StreamJoin's mutual-contact/block-gated
    // path - a client asks "what's live in my current territory" and the server answers with
    // whoever is currently hosting in that same TerritoryId, no contact relationship required.
    public const string StreamNearby = "stream.nearby";
    public const string StreamNearbyRoster = "stream.nearby.roster";
}

internal static class ParticipantState
{
    public const string Ringing = "ringing";
    public const string Active = "active";
    public const string Left = "left";
}

internal sealed record ParticipantInfo(
    string UserId,
    string Name,
    string World,
    string DisplayName,
    int Slot,
    string State,
    bool Muted);

// One entry in a stream.nearby.roster response - a currently-hosting user in the same territory
// as the requester. Deliberately just enough to show and join with, not a full ParticipantInfo
// (there's no call slot/mute state for someone you haven't joined yet).
internal sealed record NearbyStreamInfo(string HostId, string Name, string World, string DisplayName);

internal sealed record CallControl
{
    public string Type { get; init; } = string.Empty;
    public string? CallId { get; init; }
    public string[]? InviteeIds { get; init; }
    public ParticipantInfo? From { get; init; }
    public ParticipantInfo[]? Participants { get; init; }
    public string? UserId { get; init; }
    public bool? Muted { get; init; }
    public string? Reason { get; init; }

    // stream.* fields - nulls are omitted on the wire and unknown fields are ignored server-side,
    // so adding these here is backward compatible with the existing call.* parsing above.
    public string? HostId { get; init; }
    public string? Url { get; init; }
    public double? PositionSeconds { get; init; }
    public bool? Paused { get; init; }

    // Part of the host-approval extension (see StreamJoinRequest et al. above) - sent by the host
    // alongside every stream.state so the server knows whether to auto-decide joins (false/omitted)
    // or hold them for stream.approve/stream.deny (true).
    public bool? ApprovalRequired { get; init; }

    // Part of the shared-queue extension (see StreamQueueSuggest et al. above) - client-generated
    // (Guid.NewGuid().ToString() on the suggesting viewer's side) since a single viewer can have
    // more than one suggestion pending at once, unlike join requests which are naturally
    // one-per-user. Max 64 chars; a viewer cannot reuse another viewer's suggestionId.
    public string? SuggestionId { get; init; }

    // The host's AetherStream world-anchored screen transform (VideoEngine.ScreenPosition/
    // ScreenYaw/ScreenScale) - optional, omitted entirely if the host's screen isn't active. Each
    // viewer's own client applies this to their own local ScreenPainter (see
    // VideoEngine.ApplyRemoteScreenTransform) - there is no shared/networked 3D object, every
    // Aetherphone client just draws its own copy at the same coordinates.
    public float? ScreenX { get; init; }
    public float? ScreenY { get; init; }
    public float? ScreenZ { get; init; }
    public float? ScreenYaw { get; init; }
    public float? ScreenScale { get; init; }

    // Zone-scoped discovery (stream.nearby / stream.nearby.roster). TerritoryId does double duty:
    // sent by a host alongside stream.state so the server knows which zone their stream is "in",
    // and sent by a would-be viewer on stream.nearby to ask "what's live in this zone". The server
    // is the only thing that ever compares the two - the client just reports its own current one.
    public uint? TerritoryId { get; init; }
    public NearbyStreamInfo[]? NearbyStreams { get; init; }

    // content.removed fields - moderation broadcast when a post/comment/etc is taken down while
    // someone is viewing it.
    public string? App { get; init; }
    public string? ContentKind { get; init; }
    public string? ContentId { get; init; }
    public string? ParentId { get; init; }
}
