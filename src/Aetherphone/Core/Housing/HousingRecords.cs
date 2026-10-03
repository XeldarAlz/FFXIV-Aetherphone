using Newtonsoft.Json;
using System.Globalization;

namespace Aetherphone.Core.Housing;

[Serializable]
internal sealed class HousingWatchRecord
{
    public uint WorldId { get; set; }
    public uint DistrictId { get; set; }
    public int Ward { get; set; }
    public int Plot { get; set; }
    public string WorldName { get; set; } = string.Empty;
    public byte Size { get; set; }
    public byte Phase { get; set; }
    public byte Eligibility { get; set; }
    public long Price { get; set; }
    public int? Entries { get; set; }
    public long PhaseEndUnix { get; set; }
    public long LastSeenUnix { get; set; }
    public long FirstSeenUnix { get; set; }
    public long AddedUnix { get; set; }
    public bool StillReported { get; set; } = true;
    public long LastReportedUnix { get; set; }

    [JsonIgnore] public HousingPlotKey Key => new(WorldId, DistrictId, Ward, Plot);
}

[Serializable]
internal sealed class HousingReminderRecord
{
    public uint WorldId { get; set; }
    public uint DistrictId { get; set; }
    public int Ward { get; set; }
    public int Plot { get; set; }
    public string WorldName { get; set; } = string.Empty;
    public byte Phase { get; set; }
    public long PhaseEndUnix { get; set; }
    public int OffsetMinutes { get; set; } = HousingDefaults.ReminderMinutes;
    public bool Notified { get; set; }
    public long CreatedUnix { get; set; }
    public int? Entries { get; set; }
    public long LastSeenUnix { get; set; }

    [JsonIgnore] public HousingPlotKey Key => new(WorldId, DistrictId, Ward, Plot);

    [JsonIgnore]
    public string DedupeKey => string.Concat("housing:",
        WorldId.ToString(CultureInfo.InvariantCulture), ":",
        DistrictId.ToString(CultureInfo.InvariantCulture), ":",
        Ward.ToString(CultureInfo.InvariantCulture), ":",
        Plot.ToString(CultureInfo.InvariantCulture), ":",
        Phase.ToString(CultureInfo.InvariantCulture), ":",
        PhaseEndUnix.ToString(CultureInfo.InvariantCulture), ":",
        OffsetMinutes.ToString(CultureInfo.InvariantCulture));

    [JsonIgnore]
    public DateTime? FireAtUtc
    {
        get
        {
            if (PhaseEndUnix <= 0L)
            {
                return null;
            }

            return DateTimeOffset.FromUnixTimeSeconds(PhaseEndUnix).UtcDateTime.AddMinutes(-OffsetMinutes);
        }
    }

    [JsonIgnore]
    public DateTime? PhaseEndUtc =>
        PhaseEndUnix <= 0L ? null : DateTimeOffset.FromUnixTimeSeconds(PhaseEndUnix).UtcDateTime;
}

internal static class HousingDefaults
{
    public const int ReminderMinutes = 30;
    public const int RefreshMinutes = 20;
    public const int MinRefreshMinutes = 15;
    public const int MaxRefreshMinutes = 60;
    public const int RefreshStepMinutes = 5;
    public const int IdleRefreshMinutes = 60;
    public const int ExpiryRefreshMinutes = 5;
    public const int ManualRefreshSeconds = 15;
    public const uint DefaultWorldId = 0u;
    public const int DefaultWard = 1;

    public static readonly int[] ReminderChoices = { 5, 15, 30, 60 };
}
