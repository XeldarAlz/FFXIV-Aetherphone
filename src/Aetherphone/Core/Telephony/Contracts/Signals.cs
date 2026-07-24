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
    public const string ChatPing = "chat.ping";
    public const string VelvetPing = "velvet.ping";
    public const string GramPing = "gram.ping";
    public const string SocialPing = "social.ping";
    public const string Error = "error";
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
}
