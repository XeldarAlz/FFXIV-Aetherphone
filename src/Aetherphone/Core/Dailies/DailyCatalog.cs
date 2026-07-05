using Aetherphone.Core.Localization;
using Aetherphone.Windows;
using Dalamud.Interface;

namespace Aetherphone.Core.Dailies;

internal static class DailyCatalog
{
    public static readonly DailyItem[] Items =
    {
        new("daily.roulettes", L.Dailies.DutyRoulettes, FontAwesomeIcon.Dungeon, Styling.AccentBlue, DailyCadence.Daily, DailyTracking.Manual, 1),
        new("daily.beastTribe", L.Dailies.BeastTribe, FontAwesomeIcon.Paw, Styling.AccentMint, DailyCadence.Daily, DailyTracking.BeastTribeAllowances, 12),
        new("daily.miniCactpot", L.Dailies.MiniCactpot, FontAwesomeIcon.Dice, Styling.AccentAmber, DailyCadence.Daily, DailyTracking.Manual, 3),
        new("daily.gcSupply", L.Dailies.GrandCompanySupply, FontAwesomeIcon.ShieldAlt, Styling.AccentRose, DailyCadence.Daily, DailyTracking.Manual, 1),
        new("daily.domanEnclave", L.Dailies.DomanEnclave, FontAwesomeIcon.Coins, Styling.AccentAmberSoft, DailyCadence.Daily, DailyTracking.DomanEnclave, 1),
        new("daily.levequests", L.Dailies.Levequests, FontAwesomeIcon.Scroll, Styling.AccentBlueSoft, DailyCadence.Daily, DailyTracking.Levequests, 100),
        new("weekly.wondrousTails", L.Dailies.WondrousTails, FontAwesomeIcon.BookOpen, Styling.AccentViolet, DailyCadence.Weekly, DailyTracking.WondrousTails, 9),
        new("weekly.jumboCactpot", L.Dailies.JumboCactpot, FontAwesomeIcon.TicketAlt, Styling.AccentAmber, DailyCadence.Weekly, DailyTracking.Manual, 3),
        new("weekly.customDeliveries", L.Dailies.CustomDeliveries, FontAwesomeIcon.Truck, Styling.AccentMint, DailyCadence.Weekly, DailyTracking.CustomDeliveries, 12),
        new("weekly.fashionReport", L.Dailies.FashionReport, FontAwesomeIcon.Tshirt, Styling.AccentPink, DailyCadence.Weekly, DailyTracking.Manual, 1),
        new("weekly.challengeLog", L.Dailies.ChallengeLog, FontAwesomeIcon.ClipboardList, Styling.AccentBlue, DailyCadence.Weekly, DailyTracking.Manual, 1),
        new("weekly.raidLockout", L.Dailies.RaidLockout, FontAwesomeIcon.Skull, Styling.AccentRose, DailyCadence.Weekly, DailyTracking.Manual, 1),
        new("weekly.huntBills", L.Dailies.HuntBills, FontAwesomeIcon.Crosshairs, Styling.AccentAmberSoft, DailyCadence.Weekly, DailyTracking.Manual, 1),
    };
}
