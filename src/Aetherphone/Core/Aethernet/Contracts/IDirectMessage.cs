namespace Aetherphone.Core.Aethernet.Contracts;

internal interface IDirectMessage : IIdentified
{
    string SenderId { get; }

    string Body { get; }

    int Kind { get; }

    long CreatedAtUnix { get; }

    int MediaWidth { get; }

    int MediaHeight { get; }

    long? ReadAtUnix { get; }

    int EncVersion { get; }

    string? ReplyToId { get; }

    string? ReplySenderId { get; }

    string? ReplyBody { get; }

    int ReplyKind { get; }

    bool Deleted { get; }

    int DurationSecs { get; }

    ReactionSummaryDto[]? Reactions { get; }

    long? EditedAtUnix { get; }
}
