using Dalamud.Game;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Graphics.Environment;
using Lumina.Excel.Sheets;

namespace Aetherphone.Core.Game;

internal readonly record struct WeatherEntry(byte Id, string Name, string EnglishKey);

internal readonly record struct WeatherWindow(WeatherEntry Weather, int MinutesFromNow, bool IsCurrent, int StartBell);

internal readonly record struct WeatherZone(uint TerritoryId, string Name, string Region, uint RegionOrder,
    bool FieldOperation);

internal readonly record struct WeatherOdds(WeatherEntry Weather, int Percent);

internal sealed class WeatherService
{
    private const long RealSecondsPerEorzeaHour = 175;
    private const long RealSecondsPerWindow = 1400;
    private const long RealSecondsPerEorzeaDay = 4200;
    private const uint TownUse = 0;
    private const uint OverworldUse = 1;
    private const uint FieldOperationOrder = uint.MaxValue - 1;
    private const uint OtherOrder = uint.MaxValue;
    private const string UnnamedRegion = "???";
    private readonly IDataManager data;
    private readonly IClientState clientState;
    private readonly Dictionary<byte, WeatherEntry> entries = new();
    private readonly List<WeatherEntry> zoneWeathers = new();
    private readonly List<WeatherEntry> extraWeathers = new();
    private readonly List<byte> levelWeathers = new();
    private readonly List<WeatherChance> chances = new();
    private readonly Dictionary<uint, WeatherChance[]> territoryChances = new();
    private readonly Dictionary<uint, string> territoryNames = new();
    private uint cachedTerritory = uint.MaxValue;

    private readonly record struct WeatherChance(byte Id, int Cumulative);

    public WeatherService(IDataManager data, IClientState clientState)
    {
        this.data = data;
        this.clientState = clientState;
    }

    public string CurrentZone()
    {
        if (!GameSheets.Available)
        {
            return string.Empty;
        }

        var territoryId = clientState.TerritoryType;
        if (territoryId != 0 && data.GetExcelSheet<TerritoryType>().TryGetRow(territoryId, out var territory))
        {
            return territory.PlaceName.Value.Name.ExtractText();
        }

        return string.Empty;
    }

    public IReadOnlyList<WeatherEntry> ZoneWeathers()
    {
        RefreshZone();
        return zoneWeathers;
    }

    public IReadOnlyList<WeatherEntry> ExtraWeathers()
    {
        RefreshZone();
        return extraWeathers;
    }

    public unsafe byte LiveWeatherId()
    {
        if (!GameMemory.Attached)
        {
            return 0;
        }

        var environment = EnvManager.Instance();
        return environment == null ? (byte)0 : environment->ActiveWeather;
    }

    public WeatherEntry? LiveRenderedWeather()
    {
        var live = LiveWeatherId();
        return live == 0 ? null : Entry(live);
    }

    public static long WindowStart(long unixSeconds) => unixSeconds - unixSeconds % RealSecondsPerWindow;

    public byte NaturalNow()
    {
        if (!RefreshZone())
        {
            return 0;
        }

        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return Resolve(ForecastTarget(WindowStart(nowUnix)));
    }

    public void Forecast(List<WeatherWindow> into, int count)
    {
        into.Clear();
        if (!RefreshZone())
        {
            return;
        }

        var live = LiveRenderedWeather();
        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var startUnix = WindowStart(nowUnix);
        for (var index = 0; index < count; index++)
        {
            var timestamp = startUnix + index * RealSecondsPerWindow;
            var entry = index == 0 && live.HasValue ? live.Value : Entry(Resolve(ForecastTarget(timestamp)));
            var minutes = (int)((timestamp - nowUnix) / 60);
            var windowBell = (int)(timestamp / RealSecondsPerEorzeaHour % 24);
            into.Add(new WeatherWindow(entry, minutes, index == 0, windowBell));
        }
    }

    public uint CurrentTerritory => clientState.TerritoryType;

    public string ZoneName(uint territoryId)
    {
        if (territoryNames.TryGetValue(territoryId, out var cached))
        {
            return cached;
        }

        var name = string.Empty;
        if (territoryId != 0 && data.GetExcelSheet<TerritoryType>().TryGetRow(territoryId, out var territory))
        {
            name = territory.PlaceName.Value.Name.ExtractText();
        }

        territoryNames[territoryId] = name;
        return name;
    }

    public void Forecast(uint territoryId, List<WeatherWindow> into, int count)
    {
        into.Clear();
        var zoneChances = ChancesFor(territoryId);
        if (zoneChances.Length == 0)
        {
            return;
        }

        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var startUnix = WindowStart(nowUnix);
        for (var index = 0; index < count; index++)
        {
            var timestamp = startUnix + index * RealSecondsPerWindow;
            var entry = Entry(Resolve(zoneChances, ForecastTarget(timestamp)));
            var minutes = (int)((timestamp - nowUnix) / 60);
            var windowBell = (int)(timestamp / RealSecondsPerEorzeaHour % 24);
            into.Add(new WeatherWindow(entry, minutes, index == 0, windowBell));
        }
    }

    public void Odds(uint territoryId, List<WeatherOdds> into)
    {
        into.Clear();
        var zoneChances = ChancesFor(territoryId);
        var previous = 0;
        for (var index = 0; index < zoneChances.Length; index++)
        {
            var chance = zoneChances[index];
            var percent = chance.Cumulative - previous;
            previous = chance.Cumulative;
            var merged = false;
            for (var existing = 0; existing < into.Count; existing++)
            {
                if (into[existing].Weather.Id != chance.Id)
                {
                    continue;
                }

                into[existing] = into[existing] with { Percent = into[existing].Percent + percent };
                merged = true;
                break;
            }

            if (!merged)
            {
                into.Add(new WeatherOdds(Entry(chance.Id), percent));
            }
        }
    }

    public int MinutesUntil(uint territoryId, byte weatherId, int maxWindows)
    {
        var zoneChances = ChancesFor(territoryId);
        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var startUnix = WindowStart(nowUnix);
        for (var index = 0; index < maxWindows; index++)
        {
            var timestamp = startUnix + index * RealSecondsPerWindow;
            if (Resolve(zoneChances, ForecastTarget(timestamp)) == weatherId)
            {
                return index == 0 ? 0 : Math.Max(1, (int)((timestamp - nowUnix) / 60));
            }
        }

        return -1;
    }

    public void WeatherZones(List<WeatherZone> into)
    {
        into.Clear();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var regionOrders = new Dictionary<string, uint>(StringComparer.Ordinal);
        foreach (var territory in data.GetExcelSheet<TerritoryType>())
        {
            var use = territory.TerritoryIntendedUse.RowId;
            var fieldOperation = FieldOperations.IsFieldOperation(use);
            if (use != TownUse && use != OverworldUse && !fieldOperation || territory.WeatherRate.RowId == 0)
            {
                continue;
            }

            var name = territory.PlaceName.Value.Name.ExtractText();
            if (name.Length == 0 || !seen.Add(name) || ChancesFor(territory.RowId).Length == 0)
            {
                continue;
            }

            var region = fieldOperation ? string.Empty : RegionOf(territory);
            var order = fieldOperation ? FieldOperationOrder : OtherOrder;
            if (region.Length > 0 && !regionOrders.TryGetValue(region, out order))
            {
                order = territory.RowId;
                regionOrders[region] = order;
            }

            into.Add(new WeatherZone(territory.RowId, name, region, order, fieldOperation));
        }

        into.Sort(CompareZones);
    }

    private static string RegionOf(in TerritoryType territory)
    {
        if (!territory.PlaceNameRegion.IsValid)
        {
            return string.Empty;
        }

        var region = territory.PlaceNameRegion.Value.Name.ExtractText();
        return region == UnnamedRegion ? string.Empty : region;
    }

    private static int CompareZones(WeatherZone left, WeatherZone right)
    {
        var byRegion = left.RegionOrder.CompareTo(right.RegionOrder);
        return byRegion != 0 ? byRegion : string.Compare(left.Name, right.Name, StringComparison.CurrentCulture);
    }

    public WeatherEntry Entry(byte id)
    {
        if (!GameSheets.Available)
        {
            return new WeatherEntry(id, string.Empty, string.Empty);
        }

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

    private bool RefreshZone()
    {
        var territoryId = clientState.TerritoryType;
        if (territoryId != cachedTerritory)
        {
            cachedTerritory = territoryId;
            Rebuild(territoryId);
        }

        return chances.Count > 0;
    }

    private void Rebuild(uint territoryId)
    {
        if (!GameSheets.Available)
        {
            return;
        }

        chances.Clear();
        zoneWeathers.Clear();
        extraWeathers.Clear();
        if (territoryId == 0 || !data.GetExcelSheet<TerritoryType>().TryGetRow(territoryId, out var territory))
        {
            return;
        }

        if (!data.GetExcelSheet<WeatherRate>().TryGetRow(territory.WeatherRate.RowId, out var rate))
        {
            return;
        }

        var rates = rate.Rate;
        var weathers = rate.Weather;
        var cumulative = 0;
        for (var index = 0; index < rates.Count; index++)
        {
            var id = (byte)weathers[index].RowId;
            var chance = rates[index];
            if (id == 0 || chance <= 0)
            {
                continue;
            }

            cumulative += chance;
            chances.Add(new WeatherChance(id, cumulative));
            if (!KnownInZone(id))
            {
                zoneWeathers.Add(Entry(id));
            }
        }

        if (!territory.IsPvpZone)
        {
            LoadExtraWeathers(territory.Bg.ExtractText());
        }
    }

    private void LoadExtraWeathers(string background)
    {
        if (background.Length == 0)
        {
            return;
        }

        try
        {
            var level = data.GetFile($"bg/{background}.lvb");
            if (level is null)
            {
                return;
            }

            LevelWeatherTable.Read(level.Data, levelWeathers);
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[Skywatcher] could not read the zone's level weathers");
            return;
        }

        for (var index = 0; index < levelWeathers.Count; index++)
        {
            var id = levelWeathers[index];
            if (!HasIcon(id))
            {
                continue;
            }

            var entry = Entry(id);
            if (entry.Name.Length == 0 || entry.EnglishKey.Length == 0 || Listed(zoneWeathers, entry.Name) ||
                Listed(extraWeathers, entry.Name))
            {
                continue;
            }

            extraWeathers.Add(entry);
        }
    }

    private bool HasIcon(byte id) => data.GetExcelSheet<Weather>().TryGetRow(id, out var row) && row.Icon != 0;

    private static bool Listed(List<WeatherEntry> entries, string name)
    {
        for (var index = 0; index < entries.Count; index++)
        {
            if (string.Equals(entries[index].Name, name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private bool KnownInZone(byte id)
    {
        for (var index = 0; index < zoneWeathers.Count; index++)
        {
            if (zoneWeathers[index].Id == id)
            {
                return true;
            }
        }

        return false;
    }

    private WeatherChance[] ChancesFor(uint territoryId)
    {
        if (territoryChances.TryGetValue(territoryId, out var cached))
        {
            return cached;
        }

        var built = new List<WeatherChance>();
        if (territoryId != 0 && data.GetExcelSheet<TerritoryType>().TryGetRow(territoryId, out var territory) &&
            data.GetExcelSheet<WeatherRate>().TryGetRow(territory.WeatherRate.RowId, out var rate))
        {
            var cumulative = 0;
            for (var index = 0; index < rate.Rate.Count; index++)
            {
                var id = (byte)rate.Weather[index].RowId;
                var chance = rate.Rate[index];
                if (id == 0 || chance <= 0)
                {
                    continue;
                }

                cumulative += chance;
                built.Add(new WeatherChance(id, cumulative));
            }
        }

        var result = built.ToArray();
        territoryChances[territoryId] = result;
        return result;
    }

    private static byte Resolve(WeatherChance[] zoneChances, uint target)
    {
        for (var index = 0; index < zoneChances.Length; index++)
        {
            if (target < zoneChances[index].Cumulative)
            {
                return zoneChances[index].Id;
            }
        }

        return zoneChances.Length > 0 ? zoneChances[^1].Id : (byte)0;
    }

    private byte Resolve(uint target)
    {
        for (var index = 0; index < chances.Count; index++)
        {
            if (target < chances[index].Cumulative)
            {
                return chances[index].Id;
            }
        }

        return chances.Count > 0 ? chances[^1].Id : (byte)0;
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
