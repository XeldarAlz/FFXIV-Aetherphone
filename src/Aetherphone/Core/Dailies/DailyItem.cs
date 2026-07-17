using Aetherphone.Core.Localization;
using Dalamud.Interface;

namespace Aetherphone.Core.Dailies;

internal enum DailyTracking
{
    Manual,
    BeastTribeAllowances,
    CustomDeliveries,
    WondrousTails,
    DomanEnclave,
    Levequests,
    DutyRoulettes,
    HuntBills,
}

internal readonly record struct DailyItem(
    string Id,
    LocString Label,
    FontAwesomeIcon Icon,
    Vector4 Accent,
    DailyCadence Cadence,
    DailyTracking Tracking,
    int Goal);
