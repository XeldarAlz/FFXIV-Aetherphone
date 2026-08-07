using Aetherphone.Core.Localization;
using Dalamud.Interface;

namespace Aetherphone.Core.YellowPages;

internal static class AdArchetypes
{
    public const int Place = 0;
    public const int Service = 1;
    public const int Call = 2;
}

internal static class AdPriceModes
{
    public const int Ask = 0;
    public const int Fixed = 1;
    public const int From = 2;
}

internal static class AdIntents
{
    public const int Go = 0;
    public const int Hire = 1;
    public const int Join = 2;

    public static readonly int[] All = { Go, Hire, Join };

    public static LocString Label(int intent) =>
        intent switch
        {
            Hire => L.YellowPages.IntentHire,
            Join => L.YellowPages.IntentJoin,
            _ => L.YellowPages.IntentGo,
        };
}

internal static class AdCategories
{
    public const int VenueNight = 0;
    public const int EventShow = 1;
    public const int Casino = 2;
    public const int Crafting = 3;
    public const int Gathering = 4;
    public const int Glamour = 5;
    public const int Portraits = 6;
    public const int Performance = 7;
    public const int Coaching = 8;
    public const int OddJobs = 9;
    public const int FreeCompany = 10;
    public const int RaidStatic = 11;
    public const int VenueStaff = 12;
    public const int Community = 13;
    public const int HousingTour = 14;
    public const int Mods = 15;
    public const int HousingDesign = 16;
    public const int Weddings = 17;
    public const int Writing = 18;
    public const int Count = 19;

    public static readonly int[] GoCategories = { VenueNight, EventShow, Casino, HousingTour };

    public static readonly int[] HireCategories =
    {
        Crafting, Gathering, Glamour, Portraits, Performance, Coaching, HousingDesign, Weddings, Writing, Mods,
        OddJobs,
    };

    public static readonly int[] JoinCategories = { FreeCompany, RaidStatic, VenueStaff, Community };

    public static int[] ForIntent(int intent) =>
        intent switch
        {
            AdIntents.Hire => HireCategories,
            AdIntents.Join => JoinCategories,
            _ => GoCategories,
        };

    public static int IntentFor(int category) =>
        ArchetypeFor(category) switch
        {
            AdArchetypes.Service => AdIntents.Hire,
            AdArchetypes.Call => AdIntents.Join,
            _ => AdIntents.Go,
        };

    public static int ArchetypeFor(int category) =>
        category switch
        {
            VenueNight or EventShow or Casino or HousingTour => AdArchetypes.Place,
            FreeCompany or RaidStatic or VenueStaff or Community => AdArchetypes.Call,
            _ => AdArchetypes.Service,
        };

    public static bool IsLinkOnly(int category) => category == Mods;

    public static int MaskFor(int[] categories)
    {
        var mask = 0;
        for (var index = 0; index < categories.Length; index++)
        {
            mask |= 1 << categories[index];
        }

        return mask;
    }

    public static FontAwesomeIcon Icon(int category) =>
        category switch
        {
            VenueNight => FontAwesomeIcon.Cocktail,
            EventShow => FontAwesomeIcon.TheaterMasks,
            Casino => FontAwesomeIcon.Dice,
            HousingTour => FontAwesomeIcon.DoorOpen,
            Mods => FontAwesomeIcon.PuzzlePiece,
            HousingDesign => FontAwesomeIcon.Couch,
            Weddings => FontAwesomeIcon.Ring,
            Writing => FontAwesomeIcon.PenNib,
            Crafting => FontAwesomeIcon.Hammer,
            Gathering => FontAwesomeIcon.Leaf,
            Glamour => FontAwesomeIcon.Tshirt,
            Portraits => FontAwesomeIcon.Camera,
            Performance => FontAwesomeIcon.Music,
            Coaching => FontAwesomeIcon.GraduationCap,
            OddJobs => FontAwesomeIcon.Briefcase,
            FreeCompany => FontAwesomeIcon.Flag,
            RaidStatic => FontAwesomeIcon.ShieldAlt,
            VenueStaff => FontAwesomeIcon.UserTie,
            Community => FontAwesomeIcon.Users,
            _ => FontAwesomeIcon.Bullhorn,
        };

    public static LocString Label(int category) =>
        category switch
        {
            VenueNight => L.YellowPages.CategoryVenueNight,
            EventShow => L.YellowPages.CategoryEventShow,
            Casino => L.YellowPages.CategoryCasino,
            HousingTour => L.YellowPages.CategoryHousingTour,
            Mods => L.YellowPages.CategoryMods,
            HousingDesign => L.YellowPages.CategoryHousingDesign,
            Weddings => L.YellowPages.CategoryWeddings,
            Writing => L.YellowPages.CategoryWriting,
            Crafting => L.YellowPages.CategoryCrafting,
            Gathering => L.YellowPages.CategoryGathering,
            Glamour => L.YellowPages.CategoryGlamour,
            Portraits => L.YellowPages.CategoryPortraits,
            Performance => L.YellowPages.CategoryPerformance,
            Coaching => L.YellowPages.CategoryCoaching,
            OddJobs => L.YellowPages.CategoryOddJobs,
            FreeCompany => L.YellowPages.CategoryFreeCompany,
            RaidStatic => L.YellowPages.CategoryRaidStatic,
            VenueStaff => L.YellowPages.CategoryVenueStaff,
            _ => L.YellowPages.CategoryCommunity,
        };
}

internal static class AdScopes
{
    public const int Region = 0;
    public const int DataCenter = 1;
    public const int Everywhere = 2;
}
