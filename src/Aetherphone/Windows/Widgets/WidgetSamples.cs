using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Interface;

namespace Aetherphone.Windows.Widgets;

internal static class WidgetSamples
{
    public const long CoinBalance = 12450;
    public const long CoinEarnedToday = 180;
    public const long CoinDailyCap = 300;
    public const long Gil = 1284500;

    public static readonly string[] Names = { "Aerin Vale", "Koto Mizu", "Rhea Solace", "Bram Ashford" };
    public static readonly string[] Worlds = { "Ultros", "Cactuar", "Balmung", "Twintania" };
    public static readonly LocString[] Events = { L.Widgets.SampleEvent, L.Widgets.SampleEventLater };

    public static readonly LocString[] Reminders =
    {
        L.WidgetsUtility.SampleReminderSupplies, L.WidgetsUtility.SampleReminderTinctures,
        L.WidgetsUtility.SampleReminderCactpot,
    };

    public static readonly LocString[] Shortcuts =
    {
        L.WidgetsUtility.SampleShortcutRaid, L.WidgetsUtility.SampleShortcutHome,
        L.WidgetsUtility.SampleShortcutGlamour, L.WidgetsUtility.SampleShortcutPartyFinder,
        L.WidgetsUtility.SampleShortcutRetainers, L.WidgetsUtility.SampleShortcutSaucer,
        L.WidgetsUtility.SampleShortcutHunt, L.WidgetsUtility.SampleShortcutCrafting,
    };

    public static readonly FontAwesomeIcon[] ShortcutGlyphs =
    {
        FontAwesomeIcon.Bolt, FontAwesomeIcon.Home, FontAwesomeIcon.Tshirt, FontAwesomeIcon.Users,
        FontAwesomeIcon.Briefcase, FontAwesomeIcon.Dice, FontAwesomeIcon.Crosshairs, FontAwesomeIcon.Hammer,
    };

    public static readonly Vector4[] ShortcutTints =
    {
        AccentRing.Indigo, AccentRing.Green, AccentRing.Rose, AccentRing.Azure, AccentRing.Orange,
        AccentRing.Violet, AccentRing.Red, AccentRing.Teal,
    };

    public static readonly LocString[] Musters = { L.WidgetsUtility.SampleMusterMaps, L.WidgetsUtility.SampleMusterTour };

    public static readonly string[] Venues = { "The Velvet Lantern", "Moonfire Lounge", "Crystal Tavern", "Starlight Bistro", "Mist Garden Club" };

    public static readonly string[] MarketItems = { "Grade 2 Gemdraught of Strength", "Ra'Kaznar Ingot", "Claro Walnut Lumber", "Moqueca" };

    public static readonly long[] MarketPrices = { 1850, 42000, 3900, 760 };

    public static readonly LocString[] Headlines =
    {
        L.WidgetsUtility.SampleHeadlinePatch, L.WidgetsUtility.SampleHeadlineMaintenance,
        L.WidgetsUtility.SampleHeadlineEvent, L.WidgetsUtility.SampleHeadlineFestival,
    };

    public static float Fraction(int index) => index switch
    {
        0 => 0.72f,
        1 => 0.45f,
        _ => 0.88f,
    };

    public const long TomestoneWeekly = 450;
    public const long TomestoneWeeklyCap = 900;
    public const uint JobClassJobId = 21;
    public const int JobLevel = 100;
    public const int JobItemLevel = 745;
    public const float JobExperience = 0.62f;
    public const int HousingPhaseHours = 53;
    public const int HousingOpenPlots = 14;

    public static readonly uint[] GearsetJobIds = { 19, 24, 25 };
    public static readonly uint[] AetheryteIds = { 8, 2, 9, 111 };
    public static readonly string[] HuntMarks = { "Neyoozoteel", "Kirlirger the Abhorrent", "Ihnuxokiy" };
    public static readonly int[] HuntMinutesAgo = { 4, 17, 31 };
    public static readonly int[] HousingWards = { 5, 12, 21 };
    public static readonly int[] HousingPlots = { 12, 3, 27 };

    public static bool Checked(int index) => index % 3 == 0;

    public static long CurrencyAmount(int index) => 120 + (index * 937 + 311) % 1880;
}
