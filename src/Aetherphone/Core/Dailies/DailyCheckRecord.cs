namespace Aetherphone.Core.Dailies;

[Serializable]
internal sealed class DailyCheckRecord
{
    public string ItemId { get; set; } = string.Empty;

    public long PeriodResetUnix { get; set; }
}
