namespace Aetherphone.Core.GameChat;

internal enum ChatDensity : byte
{
    Log,
    Bubbles,
}

internal enum AlertPolicy : byte
{
    All,
    Mentions,
    Off,
}

internal enum HistoryPolicy : byte
{
    Off,
    Session,
    Days30,
    Forever,
}

internal sealed class ChatTab
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public List<string> Channels { get; set; } = new();

    public List<string> MutedChannels { get; set; } = new();

    public string SendChannel { get; set; } = string.Empty;

    public int Tint { get; set; }

    public ChatDensity Density { get; set; }

    public AlertPolicy Alerts { get; set; }

    public bool Pinned { get; set; }

    public float TextScale { get; set; }

    public bool? Timestamps { get; set; }

    public bool Includes(string channelKey) => Channels.Contains(channelKey);

    public bool IsMuted(string channelKey) => MutedChannels.Contains(channelKey);

    public bool AlertsEnabledFor(string channelKey) => Alerts != AlertPolicy.Off && !IsMuted(channelKey);
}
