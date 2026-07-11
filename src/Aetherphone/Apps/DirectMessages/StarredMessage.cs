namespace Aetherphone.Apps.DirectMessages;

internal sealed class StarredMessage
{
    public string ConversationId { get; set; } = string.Empty;

    public string MessageId { get; set; } = string.Empty;

    public string ConversationTitle { get; set; } = string.Empty;

    public string SenderName { get; set; } = string.Empty;

    public string Preview { get; set; } = string.Empty;

    public int Kind { get; set; }

    public long CreatedAtUnix { get; set; }

    public long StarredAtUnix { get; set; }
}
