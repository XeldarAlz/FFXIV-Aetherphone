namespace Aetherphone.Core.Message;

internal sealed class MessagePopoutState
{
    public string ConversationId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public float X { get; set; }

    public float Y { get; set; }

    public float Width { get; set; }

    public float Height { get; set; }

    public bool Collapsed { get; set; }
}
