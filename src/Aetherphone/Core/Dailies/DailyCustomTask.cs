namespace Aetherphone.Core.Dailies;

[Serializable]
internal sealed class DailyCustomTask
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DailyCadence Cadence { get; set; }
}
