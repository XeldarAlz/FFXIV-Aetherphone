using Lumina.Excel;
using Lumina.Excel.Sheets;
using Newtonsoft.Json.Linq;

namespace Aetherphone.Harness.Scenarios;

internal sealed class SheetLookup
{
    public const int NoEquipCategory = -1;
    public const int RingCategory = 11;
    public const int EquipCategoryCount = 13;

    private readonly ExcelModule excel;
    private readonly List<string> warnings;
    private Dictionary<string, uint>? itemsByName;
    private Dictionary<string, uint>? worldsByName;
    private Dictionary<string, uint>? jobsByAbbreviation;
    private Dictionary<string, byte>? weathersByName;

    public SheetLookup(ExcelModule excel, List<string> warnings)
    {
        this.excel = excel;
        this.warnings = warnings;
    }

    public ExcelModule Excel => excel;

    public uint Item(JToken? token)
    {
        if (token is null)
        {
            return 0;
        }

        if (token.Type == JTokenType.Integer)
        {
            return token.Value<uint>();
        }

        var name = token.Value<string>() ?? string.Empty;
        itemsByName ??= BuildItems();
        if (itemsByName.TryGetValue(name, out var itemId))
        {
            return itemId;
        }

        warnings.Add($"unknown item '{name}'");
        return 0;
    }

    public uint World(string name)
    {
        worldsByName ??= BuildWorlds();
        if (worldsByName.TryGetValue(name, out var worldId))
        {
            return worldId;
        }

        warnings.Add($"unknown world '{name}'");
        return 0;
    }

    public uint ClassJob(string abbreviation)
    {
        jobsByAbbreviation ??= BuildJobs();
        if (jobsByAbbreviation.TryGetValue(abbreviation, out var classJobId))
        {
            return classJobId;
        }

        warnings.Add($"unknown job '{abbreviation}'");
        return 0;
    }

    public sbyte ExpArrayIndex(uint classJobId) =>
        excel.GetSheet<ClassJob>().TryGetRow(classJobId, out var job) ? job.ExpArrayIndex : (sbyte)-1;

    public int ExpArrayLength()
    {
        var length = 0;
        foreach (var job in excel.GetSheet<ClassJob>())
        {
            length = Math.Max(length, job.ExpArrayIndex + 1);
        }

        return length;
    }

    public byte Weather(JToken? token)
    {
        if (token is null)
        {
            return 0;
        }

        if (token.Type == JTokenType.Integer)
        {
            return token.Value<byte>();
        }

        var name = token.Value<string>() ?? string.Empty;
        weathersByName ??= BuildWeathers();
        if (weathersByName.TryGetValue(name, out var weatherId))
        {
            return weatherId;
        }

        warnings.Add($"unknown weather '{name}'");
        return 0;
    }

    public uint TerritoryMap(uint territoryId) =>
        excel.GetSheet<TerritoryType>().TryGetRow(territoryId, out var territory) ? territory.Map.RowId : 0;

    public int EquipCategory(uint itemId)
    {
        if (!excel.GetSheet<Item>().TryGetRow(itemId, out var item) ||
            !excel.GetSheet<EquipSlotCategory>().TryGetRow(item.EquipSlotCategory.RowId, out var slots))
        {
            return NoEquipCategory;
        }

        Span<sbyte> flags =
        [
            slots.MainHand, slots.OffHand, slots.Head, slots.Body, slots.Gloves, slots.Waist, slots.Legs, slots.Feet,
            slots.Ears, slots.Neck, slots.Wrists, (sbyte)Math.Max(slots.FingerL, slots.FingerR), slots.SoulCrystal,
        ];
        for (var index = 0; index < flags.Length; index++)
        {
            if (flags[index] > 0)
            {
                return index;
            }
        }

        return NoEquipCategory;
    }

    private Dictionary<string, uint> BuildItems()
    {
        var lookup = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in excel.GetSheet<Item>())
        {
            var name = item.Name.ExtractText();
            if (name.Length > 0)
            {
                lookup.TryAdd(name, item.RowId);
            }
        }

        return lookup;
    }

    private Dictionary<string, uint> BuildWorlds()
    {
        var lookup = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        foreach (var world in excel.GetSheet<World>())
        {
            var name = world.Name.ExtractText();
            if (name.Length > 0 && world.IsPublic)
            {
                lookup.TryAdd(name, world.RowId);
            }
        }

        return lookup;
    }

    private Dictionary<string, uint> BuildJobs()
    {
        var lookup = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        foreach (var job in excel.GetSheet<ClassJob>())
        {
            var abbreviation = job.Abbreviation.ExtractText();
            if (abbreviation.Length > 0 && job.RowId != 0)
            {
                lookup.TryAdd(abbreviation, job.RowId);
            }
        }

        return lookup;
    }

    private Dictionary<string, byte> BuildWeathers()
    {
        var lookup = new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        foreach (var weather in excel.GetSheet<Weather>())
        {
            var name = weather.Name.ExtractText();
            if (name.Length > 0 && weather.RowId <= byte.MaxValue)
            {
                lookup.TryAdd(name, (byte)weather.RowId);
            }
        }

        return lookup;
    }
}
