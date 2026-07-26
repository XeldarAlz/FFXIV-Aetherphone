using Lumina.Excel.Sheets;

namespace Aetherphone.Core.Muster;

internal readonly record struct MusterDataCenter(int Id, string Name, int RegionBit);

internal static class MusterDataCenters
{
    private static MusterDataCenter[]? catalog;

    public static MusterDataCenter[] All => catalog ??= Build();

    public static string Name(int dataCenterId)
    {
        var all = All;
        for (var index = 0; index < all.Length; index++)
        {
            if (all[index].Id == dataCenterId)
            {
                return all[index].Name;
            }
        }

        return string.Empty;
    }

    public static int RegionBitFor(int dataCenterId)
    {
        var all = All;
        for (var index = 0; index < all.Length; index++)
        {
            if (all[index].Id == dataCenterId)
            {
                return all[index].RegionBit;
            }
        }

        return 0;
    }

    public static int CountIn(int regionBit)
    {
        var all = All;
        var count = 0;
        for (var index = 0; index < all.Length; index++)
        {
            if (all[index].RegionBit == regionBit)
            {
                count++;
            }
        }

        return count;
    }

    private static MusterDataCenter[] Build()
    {
        var populated = new HashSet<uint>();
        foreach (var world in Plugin.DataManager.GetExcelSheet<World>())
        {
            if (world.DataCenter.RowId == 0 || world.Name.ExtractText().Length == 0)
            {
                continue;
            }

            populated.Add(world.DataCenter.RowId);
        }

        var found = new List<MusterDataCenter>(populated.Count);
        foreach (var group in Plugin.DataManager.GetExcelSheet<WorldDCGroupType>())
        {
            if (group.RowId == 0 || !populated.Contains(group.RowId))
            {
                continue;
            }

            var name = group.Name.ExtractText();
            var regionBit = MusterCategories.RegionBitForRegionId(group.Region.RowId);
            if (name.Length == 0 || regionBit == 0)
            {
                continue;
            }

            found.Add(new MusterDataCenter((int)group.RowId, name, regionBit));
        }

        found.Sort(CompareOrder);
        return found.ToArray();
    }

    private static int CompareOrder(MusterDataCenter left, MusterDataCenter right)
    {
        var byRegion = RegionOrder(left.RegionBit).CompareTo(RegionOrder(right.RegionBit));
        return byRegion != 0 ? byRegion : string.CompareOrdinal(left.Name, right.Name);
    }

    private static int RegionOrder(int regionBit) => Array.IndexOf(MusterCategories.AllRegions, regionBit);
}
