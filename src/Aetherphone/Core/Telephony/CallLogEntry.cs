namespace Aetherphone.Core.Telephony;

internal enum CallDirection : byte
{
    Outgoing,
    Incoming,
    Missed,
}

internal sealed class CallLogEntry
{
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string World { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public CallDirection Direction { get; set; }
    public long TimestampUnix { get; set; }
    public int Count { get; set; } = 1;
}
