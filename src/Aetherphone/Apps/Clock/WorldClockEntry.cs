namespace Aetherphone.Apps.Clock;

[Serializable]
internal sealed class WorldClockEntry
{
    public string TimeZoneId { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
}
