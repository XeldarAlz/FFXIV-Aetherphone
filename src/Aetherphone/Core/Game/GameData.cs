using Dalamud.Game;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using System.Globalization;
using ActionSheet = Lumina.Excel.Sheets.Action;
using EmoteSheet = Lumina.Excel.Sheets.Emote;

namespace Aetherphone.Core.Game;

internal sealed class GameData
{
    private const uint FramedJobIconBaseId = 62100;
    public const int ChineseSimplifiedClientLanguage = 4;
    private const uint ChinaRegionId = 5;

    private readonly IDataManager data;
    private readonly IObjectTable objectTable;
    private readonly IFramework framework;
    private uint[]? collectableMountIds;
    private uint[]? collectableMinionIds;
    private uint[]? triviaActionIds;
    private uint[]? triviaEmoteIds;
    private byte[]? dailyBonusRouletteRowIds;
    private byte[]? weeklyHuntBillIndices;
    private Dictionary<uint, uint[]>? classJobIdsByCategory;
    private Dictionary<string, string>? worldRegionCodes;
    private bool? chineseGameClient;

    public GameData(IDataManager data, IObjectTable objectTable, IFramework framework)
    {
        this.data = data;
        this.objectTable = objectTable;
        this.framework = framework;
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

    public bool TryGetClassJobDivision(uint rowId, out byte jobType, out byte role, out byte uiPriority,
        out uint classJobCategoryId)
    {
        jobType = 0;
        role = 0;
        uiPriority = 0;
        classJobCategoryId = 0;
        if (rowId != 0 && data.GetExcelSheet<ClassJob>().TryGetRow(rowId, out var job))
        {
            jobType = job.JobType;
            role = job.Role;
            uiPriority = job.UIPriority;
            classJobCategoryId = job.ClassJobCategory.RowId;
            return true;
        }

        return false;
    }

    public uint[] ClassJobIdsInCategory(uint classJobCategoryId)
    {
        classJobIdsByCategory ??= new Dictionary<uint, uint[]>();
        if (classJobIdsByCategory.TryGetValue(classJobCategoryId, out var cached))
        {
            return cached;
        }

        var rowIds = new List<uint>(16);
        foreach (var job in data.GetExcelSheet<ClassJob>())
        {
            if (job.RowId != 0 && job.ClassJobCategory.RowId == classJobCategoryId)
            {
                rowIds.Add(job.RowId);
            }
        }

        cached = rowIds.Count > 0 ? rowIds.ToArray() : Array.Empty<uint>();
        classJobIdsByCategory[classJobCategoryId] = cached;
        return cached;
    }

    public static uint JobIconId(uint classJobId) => classJobId == 0 ? 0u : FramedJobIconBaseId + classJobId;

    public int JobExpArrayIndex(uint rowId)
    {
        if (rowId != 0 && data.GetExcelSheet<ClassJob>().TryGetRow(rowId, out var job))
        {
            return job.ExpArrayIndex;
        }

        return -1;
    }

    public long ExpToNextLevel(int level)
    {
        if (level > 0 && data.GetExcelSheet<ParamGrow>().TryGetRow((uint)level, out var row))
        {
            return row.ExpToNext;
        }

        return 0;
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
            5 => "中国",
            _ => string.Empty,
        };

    public IReadOnlyList<(uint WorldId, string Name, uint DataCenterId, string DataCenterName)> ChinaWorlds()
    {
        var results = new List<(uint WorldId, string Name, uint DataCenterId, string DataCenterName)>();

        if (!IsChineseGameClient())
        {
            return results;
        }

        var worlds = data.GetExcelSheet<World>();
        foreach (var world in worlds)
        {
            if (world.RowId is > 1000 and < 2000 &&
                world.DataCenter.RowId != 0 &&
                world.Region == 2 &&
                world.DataCenter.Value.Region.RowId == ChinaRegionId &&
                world.UserType == 101 &&
                world.RowId != 1200)
            {
                var dataCenter = world.DataCenter.Value;
                var name = world.Name.ExtractText();
                if (name.Length == 0)
                {
                    continue;
                }

                results.Add((world.RowId, name, dataCenter.RowId, dataCenter.Name.ExtractText()));
            }
        }

        return results;
    }

    public string LocalRegionCode() => RegionCodeFromId(RegionId());

    public bool IsChineseGameClient()
    {
        if (chineseGameClient is { } known)
        {
            return known;
        }

        if ((int)data.Language == ChineseSimplifiedClientLanguage)
        {
            chineseGameClient = true;
            return true;
        }

        if (!framework.IsInFrameworkUpdateThread)
        {
            return false;
        }

        var regionId = RegionId();
        if (regionId == 0)
        {
            return false;
        }

        chineseGameClient = regionId == ChinaRegionId;
        return chineseGameClient.Value;
    }

    public string RegionCodeForWorld(string? worldName)
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
            5 => "CN",
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

    public uint[] TriviaActionIds()
    {
        if (triviaActionIds is not null)
        {
            return triviaActionIds;
        }

        var ids = new List<uint>(1024);
        var seenNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in data.GetExcelSheet<ActionSheet>())
        {
            if (row.RowId == 0 || !row.IsPlayerAction || row.IsPvP || row.ClassJobLevel == 0 || row.Icon == 0)
            {
                continue;
            }

            var name = row.Name.ExtractText();
            if (name.Length == 0 || !seenNames.Add(name))
            {
                continue;
            }

            ids.Add(row.RowId);
        }

        triviaActionIds = ids.ToArray();
        return triviaActionIds;
    }

    public uint[] TriviaEmoteIds()
    {
        if (triviaEmoteIds is not null)
        {
            return triviaEmoteIds;
        }

        var ids = new List<uint>(256);
        foreach (var row in data.GetExcelSheet<EmoteSheet>())
        {
            if (row.RowId == 0 || row.Icon == 0 || row.TextCommand.RowId == 0 ||
                row.Name.ExtractText().Length == 0)
            {
                continue;
            }

            ids.Add(row.RowId);
        }

        triviaEmoteIds = ids.ToArray();
        return triviaEmoteIds;
    }

    public NamedIcon ActionEntry(uint rowId)
    {
        if (!data.GetExcelSheet<ActionSheet>().TryGetRow(rowId, out var row))
        {
            return default;
        }

        return new NamedIcon(TitleCase(row.Name.ExtractText()), row.Icon);
    }

    public NamedIcon EmoteEntry(uint rowId)
    {
        if (!data.GetExcelSheet<EmoteSheet>().TryGetRow(rowId, out var row))
        {
            return default;
        }

        return new NamedIcon(TitleCase(row.Name.ExtractText()), row.Icon);
    }

    public NamedIcon MountEntry(uint rowId)
    {
        if (!data.GetExcelSheet<Mount>().TryGetRow(rowId, out var row))
        {
            return default;
        }

        return new NamedIcon(TitleCase(row.Singular.ExtractText()), row.Icon);
    }

    public NamedIcon MinionEntry(uint rowId)
    {
        if (!data.GetExcelSheet<Companion>().TryGetRow(rowId, out var row))
        {
            return default;
        }

        return new NamedIcon(TitleCase(row.Singular.ExtractText()), row.Icon);
    }

    private static string TitleCase(string text)
    {
        if (text.Length == 0)
        {
            return text;
        }

        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text);
    }

    public byte[] DailyBonusRouletteRowIds()
    {
        if (dailyBonusRouletteRowIds is not null)
        {
            return dailyBonusRouletteRowIds;
        }

        var rowIds = new List<byte>(16);
        foreach (var row in data.GetExcelSheet<ContentRoulette>())
        {
            if (!row.IsInDutyFinder || row.IsGoldSaucer || row.CompletionArrayIndex < 0)
            {
                continue;
            }

            if (row.Name.ExtractText().Length == 0)
            {
                continue;
            }

            rowIds.Add((byte)row.RowId);
        }

        dailyBonusRouletteRowIds = rowIds.Count > 0 ? rowIds.ToArray() : Array.Empty<byte>();
        return dailyBonusRouletteRowIds;
    }

    public byte[] WeeklyHuntBillIndices()
    {
        if (weeklyHuntBillIndices is not null)
        {
            return weeklyHuntBillIndices;
        }

        const byte weeklyOrderType = 2;
        var indices = new List<byte>(8);
        foreach (var row in data.GetExcelSheet<MobHuntOrderType>())
        {
            if (row.Type == weeklyOrderType)
            {
                indices.Add((byte)row.RowId);
            }
        }

        weeklyHuntBillIndices = indices.Count > 0 ? indices.ToArray() : Array.Empty<byte>();
        return weeklyHuntBillIndices;
    }

    public ExcelSheet<MobHuntOrderType> HuntOrderTypeSheet() => data.GetExcelSheet<MobHuntOrderType>();

    public SubrowExcelSheet<MobHuntOrder> HuntOrderSheet() => data.GetSubrowExcelSheet<MobHuntOrder>();
}
