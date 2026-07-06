using System.Globalization;
using Dalamud.Game;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace Aetherphone.Core.Game;

internal sealed class GameData
{
    private readonly IDataManager data;
    private readonly IObjectTable objectTable;
    private uint[]? collectableMountIds;
    private uint[]? collectableMinionIds;
    private Dictionary<string, string>? worldRegionCodes;

    public GameData(IDataManager data, IObjectTable objectTable)
    {
        this.data = data;
        this.objectTable = objectTable;
    }

    public IPlayerCharacter? LocalPlayer => objectTable.LocalPlayer;
    public uint LocalHomeWorldId => objectTable.LocalPlayer?.HomeWorld.RowId ?? 0u;
    public uint LocalCurrentWorldId => objectTable.LocalPlayer?.CurrentWorld.RowId ?? 0u;

    public bool IsLocalPlayer(string name, string world)
    {
        var local = objectTable.LocalPlayer;
        if (local is null || name.Length == 0)
        {
            return false;
        }

        if (!string.Equals(name, local.Name.TextValue, StringComparison.Ordinal))
        {
            return false;
        }

        if (world.Length == 0)
        {
            return true;
        }

        return string.Equals(world, WorldName(local.HomeWorld.RowId), StringComparison.Ordinal) ||
               string.Equals(world, WorldName(local.CurrentWorld.RowId), StringComparison.Ordinal);
    }

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
        if (worldId != 0 && data.GetExcelSheet<World>().TryGetRow(worldId, out var world) &&
            world.DataCenter.RowId != 0)
        {
            return world.DataCenter.Value.Name.ExtractText();
        }

        return string.Empty;
    }

    public bool IsDataCenterName(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        var sheet = data.GetExcelSheet<WorldDCGroupType>();
        foreach (var group in sheet)
        {
            if (group.RowId == 0)
            {
                continue;
            }

            if (string.Equals(group.Name.ExtractText(), value, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public string RegionName(uint worldId)
    {
        if (worldId != 0 && data.GetExcelSheet<World>().TryGetRow(worldId, out var world) &&
            world.DataCenter.RowId != 0)
        {
            return RegionNameFromId(world.DataCenter.Value.Region.RowId);
        }

        return string.Empty;
    }

    private static string RegionNameFromId(uint region) =>
        region switch
        {
            1 => "Japan",
            2 => "North-America",
            3 => "Europe",
            4 => "Oceania",
            _ => string.Empty,
        };

    public string LocalRegionCode() => RegionCodeFromId(RegionId());

    public string RegionCodeForWorld(string worldName)
    {
        if (string.IsNullOrEmpty(worldName))
        {
            return string.Empty;
        }

        var map = worldRegionCodes ??= BuildWorldRegionCodes();
        return map.TryGetValue(worldName, out var code) ? code : string.Empty;
    }

    private Dictionary<string, string> BuildWorldRegionCodes()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var world in data.GetExcelSheet<World>())
        {
            if (world.RowId == 0 || world.DataCenter.RowId == 0)
            {
                continue;
            }

            var code = RegionCodeFromId(world.DataCenter.Value.Region.RowId);
            if (code.Length == 0)
            {
                continue;
            }

            var name = world.Name.ExtractText();
            if (name.Length == 0)
            {
                continue;
            }

            map[name] = code;
        }

        return map;
    }

    private static string RegionCodeFromId(uint region) =>
        region switch
        {
            1 => "JP",
            2 => "NA",
            3 => "EU",
            4 => "OCE",
            _ => string.Empty,
        };

    public string LodestoneLocale() =>
        RegionId() switch
        {
            1 => "jp",
            3 => EuropeanLocale(),
            _ => "na",
        };

    private uint RegionId()
    {
        var worldId = LocalCurrentWorldId;
        if (worldId == 0)
        {
            worldId = LocalHomeWorldId;
        }

        if (worldId != 0 && data.GetExcelSheet<World>().TryGetRow(worldId, out var world) &&
            world.DataCenter.RowId != 0)
        {
            return world.DataCenter.Value.Region.RowId;
        }

        return 0;
    }

    private string EuropeanLocale() =>
        data.Language switch
        {
            ClientLanguage.French => "fr",
            ClientLanguage.German => "de",
            _ => "eu",
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

    public uint[] CollectableMountIds()
    {
        if (collectableMountIds is not null)
        {
            return collectableMountIds;
        }

        var ids = new List<uint>(512);
        foreach (var row in data.GetExcelSheet<Mount>())
        {
            if (row.RowId == 0 || row.Order < 0 || row.Singular.ExtractText().Length == 0)
            {
                continue;
            }

            ids.Add(row.RowId);
        }

        collectableMountIds = ids.ToArray();
        return collectableMountIds;
    }

    public uint[] CollectableMinionIds()
    {
        if (collectableMinionIds is not null)
        {
            return collectableMinionIds;
        }

        var ids = new List<uint>(768);
        foreach (var row in data.GetExcelSheet<Companion>())
        {
            if (row.RowId == 0 || row.Order == 0 || row.Singular.ExtractText().Length == 0)
            {
                continue;
            }

            ids.Add(row.RowId);
        }

        collectableMinionIds = ids.ToArray();
        return collectableMinionIds;
    }
}
