using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Interface;

namespace Aetherphone.Core.Dailies;

internal static class DailyCatalog
{
    public static readonly DailyItem[] Items =
    {
        new("daily.roulettes", L.Dailies.DutyRoulettes, FontAwesomeIcon.Dungeon, Accent.Blue, DailyCadence.Daily, DailyTracking.Manual, 1),
        new("daily.beastTribe", L.Dailies.BeastTribe, FontAwesomeIcon.Paw, Accent.Mint, DailyCadence.Daily, DailyTracking.BeastTribeAllowances, 12),
        new("daily.miniCactpot", L.Dailies.MiniCactpot, FontAwesomeIcon.Dice, Accent.Amber, DailyCadence.Daily, DailyTracking.Manual, 3),
        new("daily.gcSupply", L.Dailies.GrandCompanySupply, FontAwesomeIcon.ShieldAlt, Accent.Rose, DailyCadence.Daily, DailyTracking.Manual, 1),
        new("daily.domanEnclave", L.Dailies.DomanEnclave, FontAwesomeIcon.Coins, Accent.AmberSoft, DailyCadence.Daily, DailyTracking.DomanEnclave, 1),
        new("daily.levequests", L.Dailies.Levequests, FontAwesomeIcon.Scroll, Accent.BlueSoft, DailyCadence.Daily, DailyTracking.Levequests, 100),
        new("weekly.wondrousTails", L.Dailies.WondrousTails, FontAwesomeIcon.BookOpen, Accent.Violet, DailyCadence.Weekly, DailyTracking.WondrousTails, 9),
        new("weekly.jumboCactpot", L.Dailies.JumboCactpot, FontAwesomeIcon.TicketAlt, Accent.Amber, DailyCadence.Weekly, DailyTracking.Manual, 3),
        new("weekly.customDeliveries", L.Dailies.CustomDeliveries, FontAwesomeIcon.Truck, Accent.Mint, DailyCadence.Weekly, DailyTracking.CustomDeliveries, 12),
        new("weekly.fashionReport", L.Dailies.FashionReport, FontAwesomeIcon.Tshirt, Accent.Pink, DailyCadence.Weekly, DailyTracking.Manual, 1),
        new("weekly.challengeLog", L.Dailies.ChallengeLog, FontAwesomeIcon.ClipboardList, Accent.Blue, DailyCadence.Weekly, DailyTracking.Manual, 1),
        new("weekly.raidLockout", L.Dailies.RaidLockout, FontAwesomeIcon.Skull, Accent.Rose, DailyCadence.Weekly, DailyTracking.Manual, 1),
        new("weekly.huntBills", L.Dailies.HuntBills, FontAwesomeIcon.Crosshairs, Accent.AmberSoft, DailyCadence.Weekly, DailyTracking.Manual, 1),
    };
}
