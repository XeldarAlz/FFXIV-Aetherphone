using Aetherphone.Core.Localization;
using Dalamud.Interface;
using Lumina.Excel.Sheets;

namespace Aetherphone.Core.Muster;

internal static class MusterCategories
{
    public const int Social = 0;
    public const int Roleplay = 1;
    public const int Pve = 2;
    public const int Pvp = 3;
    public const int HuntTrain = 4;
    public const int TreasureHunt = 5;
    public const int DeepDungeon = 6;
    public const int Fishing = 7;
    public const int GoldSaucer = 8;
    public const int Gpose = 9;
    public const int Fates = 10;
    public const int Other = 11;

    public static readonly int[] All =
    {
        Social, Roleplay, Pve, Pvp, HuntTrain, TreasureHunt, DeepDungeon, Fishing, GoldSaucer, Gpose, Fates, Other,
    };

    public const int RegionNorthAmerica = 1;
    public const int RegionEurope = 2;
    public const int RegionJapan = 4;
    public const int RegionOceania = 8;

    public static readonly int[] AllRegions = { RegionNorthAmerica, RegionEurope, RegionJapan, RegionOceania };

    public static FontAwesomeIcon Icon(int category) =>
        category switch
        {
            Roleplay => FontAwesomeIcon.TheaterMasks,
            Pve => FontAwesomeIcon.FistRaised,
            Pvp => FontAwesomeIcon.Crosshairs,
            HuntTrain => FontAwesomeIcon.Paw,
            TreasureHunt => FontAwesomeIcon.Gem,
            DeepDungeon => FontAwesomeIcon.Dungeon,
            Fishing => FontAwesomeIcon.Fish,
            GoldSaucer => FontAwesomeIcon.Dice,
            Gpose => FontAwesomeIcon.Camera,
            Fates => FontAwesomeIcon.Meteor,
            Other => FontAwesomeIcon.MapMarkerAlt,
            _ => FontAwesomeIcon.Users,
        };

    public static LocString Label(int category) =>
        category switch
        {
            Roleplay => L.Muster.CategoryRoleplay,
            Pve => L.Muster.CategoryPve,
            Pvp => L.Muster.CategoryPvp,
            HuntTrain => L.Muster.CategoryHuntTrain,
            TreasureHunt => L.Muster.CategoryTreasureHunt,
            DeepDungeon => L.Muster.CategoryDeepDungeon,
            Fishing => L.Muster.CategoryFishing,
            GoldSaucer => L.Muster.CategoryGoldSaucer,
            Gpose => L.Muster.CategoryGpose,
            Fates => L.Muster.CategoryFates,
            Other => L.Muster.CategoryOther,
            _ => L.Muster.CategorySocial,
        };

    public static LocString RegionLabel(int regionBit) =>
        regionBit switch
        {
            RegionEurope => L.Muster.RegionEu,
            RegionJapan => L.Muster.RegionJp,
            RegionOceania => L.Muster.RegionOce,
            _ => L.Muster.RegionNa,
        };

    public static int RegionBitForWorld(uint worldId)
    {
        if (worldId == 0 || !Plugin.DataManager.GetExcelSheet<World>().TryGetRow(worldId, out var world)
            || world.DataCenter.RowId == 0)
        {
            return 0;
        }

        return world.DataCenter.Value.Region.RowId switch
        {
            1 => RegionJapan,
            2 => RegionNorthAmerica,
            3 => RegionEurope,
            4 => RegionOceania,
            _ => 0,
        };
    }
}
