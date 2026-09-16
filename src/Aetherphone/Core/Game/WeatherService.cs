using Aetherphone.Core.Localization;
using Dalamud.Game;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Graphics.Environment;
using Lumina.Excel.Sheets;

namespace Aetherphone.Core.Game;

internal readonly record struct WeatherEntry(byte Id, string Name, string EnglishKey);

internal readonly record struct WeatherWindow(WeatherEntry Weather, int MinutesFromNow, bool IsCurrent, int StartBell);

internal readonly record struct WeatherZoneEntry(uint TerritoryId, string ZoneName);

internal readonly record struct WeatherRegionGroup(string Region, IReadOnlyList<WeatherZoneEntry> Zones);

internal interface IWeatherChance
{
    int Chance { get; }
}

internal sealed class WeatherService
{
    public const long RealSecondsPerWindow = 1400;
    private const long RealSecondsPerEorzeaHour = 175;
    private const long RealSecondsPerEorzeaDay = 4200;
    private const string UnknownRegionPlaceholder = "???";
    private readonly IDataManager data;
    private readonly IClientState clientState;
    private readonly Dictionary<byte, WeatherEntry> entries = new();
    private readonly Dictionary<uint, ZoneWeatherTable> tablesByWeatherRate = new();
    private readonly Dictionary<uint, string> zoneNames = new();
    private readonly Dictionary<uint, string> regionNames = new();
    private List<WeatherRegionGroup>? regionGroups;
    private string regionGroupsLocale = string.Empty;
    private string namesLocale = string.Empty;

    private readonly record struct WeatherChance(byte Id, int Chance) : IWeatherChance;

    private sealed class ZoneWeatherTable
    {
        public readonly List<WeatherChance> Chances = new();
        public readonly List<WeatherEntry> Weathers = new();
    }

    private static readonly ZoneWeatherTable EmptyZoneTable = new();

    public WeatherService(IDataManager data, IClientState clientState)
    {
        this.data = data;
        this.clientState = clientState;
    }

    public uint CurrentTerritoryId => clientState.TerritoryType;

    public string CurrentZone() => ZoneName(clientState.TerritoryType);

    public string ZoneName(uint territoryId)
    {
        EnsureNamesCurrentLocale();
        if (zoneNames.TryGetValue(territoryId, out var cached))
        {
            return cached;
        }

        var name = string.Empty;
        if (territoryId != 0 &&
            data.GetExcelSheet<TerritoryType>(GameSheetLanguage.Current()).TryGetRow(territoryId, out var territory) &&
            territory.PlaceName.IsValid)
        {
            name = territory.PlaceName.Value.Name.ExtractText();
        }

        zoneNames[territoryId] = name;
        return name;
    }

    public string RegionName(uint territoryId)
    {
        EnsureNamesCurrentLocale();
        if (regionNames.TryGetValue(territoryId, out var cached))
        {
            return cached;
        }

        var region = string.Empty;
        if (territoryId != 0 &&
            data.GetExcelSheet<TerritoryType>(GameSheetLanguage.Current()).TryGetRow(territoryId, out var territory) &&
            territory.PlaceNameRegion.IsValid)
        {
            var regionName = territory.PlaceNameRegion.Value.Name.ExtractText();
            region = regionName == UnknownRegionPlaceholder ? string.Empty : regionName;
        }

        regionNames[territoryId] = region;
        return region;
    }

    private void EnsureNamesCurrentLocale()
    {
        if (namesLocale == Loc.Current.Code)
        {
            return;
        }

        zoneNames.Clear();
        regionNames.Clear();
        namesLocale = Loc.Current.Code;
    }

    public IReadOnlyList<WeatherRegionGroup> ZonesByRegion()
    {
        if (regionGroups != null && regionGroupsLocale == Loc.Current.Code)
        {
            return regionGroups;
        }

        var groups = new Dictionary<string, Dictionary<string, WeatherZoneEntry>>();
        foreach (var territory in data.GetExcelSheet<TerritoryType>(GameSheetLanguage.Current()))
        {
            if (GetZoneTable(territory.RowId).Weathers.Count <= 1)
            {
                continue;
            }

            var zoneName = ZoneName(territory.RowId);
            if (zoneName.Length == 0)
            {
                continue;
            }

            var region = RegionName(territory.RowId);
            if (region.Length == 0)
            {
                region = FieldOperations.IsFieldOperationZone(territory.TerritoryIntendedUse.RowId)
                    ? Loc.T(L.Skywatcher.FieldOpsRegion)
                    : Loc.T(L.Skywatcher.MiscellaneousRegion);
            }

            if (!groups.TryGetValue(region, out var zones))
            {
                zones = new Dictionary<string, WeatherZoneEntry>();
                groups[region] = zones;
            }

            if (!zones.TryGetValue(zoneName, out var existing) || territory.RowId < existing.TerritoryId)
            {
                zones[zoneName] = new WeatherZoneEntry(territory.RowId, zoneName);
            }
        }

        var regionKeys = new List<string>(groups.Keys);

        var built = new List<WeatherRegionGroup>(regionKeys.Count);
        for (var index = 0; index < regionKeys.Count; index++)
        {
            var zones = new List<WeatherZoneEntry>(groups[regionKeys[index]].Values);
            zones.Sort(CompareByTerritoryId);
            built.Add(new WeatherRegionGroup(regionKeys[index], zones));
        }

        regionGroups = built;
        regionGroupsLocale = Loc.Current.Code;
        return built;
    }

    public IReadOnlyList<WeatherEntry> ZoneWeathers() => ZoneWeathers(clientState.TerritoryType);

    public IReadOnlyList<WeatherEntry> ZoneWeathers(uint territoryId) => GetZoneTable(territoryId).Weathers;

    public unsafe WeatherEntry? LiveRenderedWeather()
    {
        var environment = EnvManager.Instance();
        if (environment == null || environment->ActiveWeather == 0)
        {
            return null;
        }

        return Entry(environment->ActiveWeather);
    }

    public byte NaturalNow() => NaturalNow(clientState.TerritoryType);

    public byte NaturalNow(uint territoryId)
    {
        var table = GetZoneTable(territoryId);
        if (table.Chances.Count == 0)
        {
            return 0;
        }

        return Resolve(table, ForecastTarget(CurrentWindowStartUnix()));
    }

    public static long CurrentWindowStartUnix()
    {
        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return nowUnix - nowUnix % RealSecondsPerWindow;
    }

    public void Forecast(List<WeatherWindow> into, int count) => Forecast(clientState.TerritoryType, into, count);

    public void Forecast(uint territoryId, List<WeatherWindow> into, int count)
    {
        into.Clear();
        var table = GetZoneTable(territoryId);
        if (table.Chances.Count == 0)
        {
            return;
        }

        var live = territoryId == clientState.TerritoryType ? LiveRenderedWeather() : null;
        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var startUnix = nowUnix - nowUnix % RealSecondsPerWindow;
        for (var index = 0; index < count; index++)
        {
            var timestamp = startUnix + index * RealSecondsPerWindow;
            var entry = index == 0 && live.HasValue ? live.Value : Entry(Resolve(table, ForecastTarget(timestamp)));
            var minutes = (int)((timestamp - nowUnix) / 60);
            var windowBell = (int)(timestamp / RealSecondsPerEorzeaHour % 24);
            into.Add(new WeatherWindow(entry, minutes, index == 0, windowBell));
        }
    }

    public WeatherEntry Entry(byte id)
    {
        if (entries.TryGetValue(id, out var cached))
        {
            return cached;
        }

        var name = string.Empty;
        if (data.GetExcelSheet<Weather>().TryGetRow(id, out var row))
        {
            name = row.Name.ExtractText();
        }

        var key = name;
        if (data.GetExcelSheet<Weather>(ClientLanguage.English).TryGetRow(id, out var englishRow))
        {
            key = englishRow.Name.ExtractText();
        }

        var entry = new WeatherEntry(id, name, key);
        entries[id] = entry;
        return entry;
    }

    private ZoneWeatherTable GetZoneTable(uint territoryId)
    {
        if (territoryId == 0 || !data.GetExcelSheet<TerritoryType>().TryGetRow(territoryId, out var territory))
        {
            return EmptyZoneTable;
        }

        var weatherRateRowId = territory.WeatherRate.RowId;
        if (tablesByWeatherRate.TryGetValue(weatherRateRowId, out var cached))
        {
            return cached;
        }

        var table = BuildZoneTable(weatherRateRowId);
        tablesByWeatherRate[weatherRateRowId] = table;
        return table;
    }

    private ZoneWeatherTable BuildZoneTable(uint weatherRateRowId)
    {
        var table = new ZoneWeatherTable();
        if (!data.GetExcelSheet<WeatherRate>().TryGetRow(weatherRateRowId, out var rate))
        {
            return table;
        }

        var rates = rate.Rate;
        var weathers = rate.Weather;
        for (var index = 0; index < rates.Count; index++)
        {
            var id = (byte)weathers[index].RowId;
            var chance = rates[index];
            if (id == 0 || chance <= 0)
            {
                continue;
            }

            table.Chances.Add(new WeatherChance(id, chance));
            if (!ContainsWeather(table.Weathers, id))
            {
                table.Weathers.Add(Entry(id));
            }
        }

        return table;
    }

    private static bool ContainsWeather(List<WeatherEntry> weathers, byte id)
    {
        for (var index = 0; index < weathers.Count; index++)
        {
            if (weathers[index].Id == id)
            {
                return true;
            }
        }

        return false;
    }

    private static int CompareByTerritoryId(WeatherZoneEntry left, WeatherZoneEntry right) =>
        left.TerritoryId.CompareTo(right.TerritoryId);

    private static byte Resolve(ZoneWeatherTable table, uint target)
    {
        var index = ResolveChanceIndex(table.Chances, target);
        return index >= 0 ? table.Chances[index].Id : (byte)0;
    }

    public static int ResolveChanceIndex<TChance>(IReadOnlyList<TChance> chances, uint target)
        where TChance : IWeatherChance
    {
        var cumulative = 0;
        for (var index = 0; index < chances.Count; index++)
        {
            cumulative += chances[index].Chance;
            if (target < cumulative)
            {
                return index;
            }
        }

        return chances.Count > 0 ? chances.Count - 1 : -1;
    }

    internal static uint ForecastTarget(long unixSeconds)
    {
        var eorzeaHour = unixSeconds / RealSecondsPerEorzeaHour;
        var increment = (uint)((eorzeaHour + 8 - eorzeaHour % 8) % 24);
        var totalDays = (uint)(unixSeconds / RealSecondsPerEorzeaDay);
        var calcBase = totalDays * 100u + increment;
        var step1 = (calcBase << 11) ^ calcBase;
        var step2 = (step1 >> 8) ^ step1;
        return step2 % 100u;
    }
}
