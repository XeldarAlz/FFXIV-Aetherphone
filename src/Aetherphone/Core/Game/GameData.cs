using System.Globalization;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace Aetherphone.Core.Game;

internal sealed class GameData
{
    private readonly IDataManager data;
    private readonly IObjectTable objectTable;

    public GameData(IDataManager data, IObjectTable objectTable)
    {
        this.data = data;
        this.objectTable = objectTable;
    }

    public IPlayerCharacter? LocalPlayer => objectTable.LocalPlayer;

    public uint LocalHomeWorldId => objectTable.LocalPlayer?.HomeWorld.RowId ?? 0u;

    public uint LocalCurrentWorldId => objectTable.LocalPlayer?.CurrentWorld.RowId ?? 0u;

    public string WorldName(uint rowId)
    {
        if (rowId != 0 && data.GetExcelSheet<World>().TryGetRow(rowId, out var world))
        {
            return world.Name.ExtractText();
        }

        return string.Empty;
    }

    public string JobAbbreviation(uint rowId)
    {
        if (rowId != 0 && data.GetExcelSheet<ClassJob>().TryGetRow(rowId, out var job))
        {
            return job.Abbreviation.ExtractText();
        }

        return string.Empty;
    }

    public string JobName(uint rowId)
    {
        if (rowId != 0 && data.GetExcelSheet<ClassJob>().TryGetRow(rowId, out var job))
        {
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(job.Name.ExtractText());
        }

        return string.Empty;
    }

    public string TerritoryName(uint rowId)
    {
        if (rowId != 0 && data.GetExcelSheet<TerritoryType>().TryGetRow(rowId, out var territory))
        {
            return territory.PlaceName.Value.Name.ExtractText();
        }

        return string.Empty;
    }

    public string DataCenterName(uint worldId)
    {
        if (worldId != 0 && data.GetExcelSheet<World>().TryGetRow(worldId, out var world) && world.DataCenter.RowId != 0)
        {
            return world.DataCenter.Value.Name.ExtractText();
        }

        return string.Empty;
    }

    public string RegionName(uint worldId)
    {
        if (worldId != 0 && data.GetExcelSheet<World>().TryGetRow(worldId, out var world) && world.DataCenter.RowId != 0)
        {
            return RegionNameFromId(world.DataCenter.Value.Region.RowId);
        }

        return string.Empty;
    }

    private static string RegionNameFromId(uint region) => region switch
    {
        1 => "Japan",
        2 => "North-America",
        3 => "Europe",
        4 => "Oceania",
        _ => string.Empty,
    };

    public string RaceName(uint raceId, bool female)
    {
        if (raceId != 0 && data.GetExcelSheet<Race>().TryGetRow(raceId, out var race))
        {
            return (female ? race.Feminine : race.Masculine).ExtractText();
        }

        return string.Empty;
    }

    public string ClanName(uint tribeId, bool female)
    {
        if (tribeId != 0 && data.GetExcelSheet<Tribe>().TryGetRow(tribeId, out var tribe))
        {
            return (female ? tribe.Feminine : tribe.Masculine).ExtractText();
        }

        return string.Empty;
    }

    public string GuardianDeityName(uint rowId)
    {
        if (rowId != 0 && data.GetExcelSheet<GuardianDeity>().TryGetRow(rowId, out var deity))
        {
            return deity.Name.ExtractText();
        }

        return string.Empty;
    }

    public string CityStateName(uint townId)
    {
        if (townId != 0 && data.GetExcelSheet<Town>().TryGetRow(townId, out var town))
        {
            return town.Name.ExtractText();
        }

        return string.Empty;
    }

    public string GrandCompanyName(uint rowId)
    {
        if (rowId != 0 && data.GetExcelSheet<GrandCompany>().TryGetRow(rowId, out var company))
        {
            return company.Name.ExtractText();
        }

        return string.Empty;
    }

    public bool TryGetItem(uint itemId, out string name, out uint iconId, out int itemLevel)
    {
        name = string.Empty;
        iconId = 0;
        itemLevel = 0;
        if (itemId == 0 || !data.GetExcelSheet<Item>().TryGetRow(itemId, out var item))
        {
            return false;
        }

        name = item.Name.ExtractText();
        iconId = item.Icon;
        itemLevel = (int)item.LevelItem.RowId;
        return true;
    }

    public void CollectTomestoneItemIds(List<uint> into)
    {
        const uint poeticsItemId = 28;

        into.Clear();
        var highest = 0u;
        var second = 0u;
        foreach (var row in data.GetExcelSheet<TomestonesItem>())
        {
            var itemId = row.Item.RowId;
            if (itemId == 0 || itemId == poeticsItemId)
            {
                continue;
            }

            if (itemId > highest)
            {
                second = highest;
                highest = itemId;
            }
            else if (itemId > second)
            {
                second = itemId;
            }
        }

        if (highest != 0)
        {
            into.Add(highest);
        }

        if (second != 0)
        {
            into.Add(second);
        }

        into.Add(poeticsItemId);
    }
}
