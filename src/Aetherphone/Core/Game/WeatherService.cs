using Dalamud.Game;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Graphics.Environment;
using Lumina.Excel.Sheets;

namespace Aetherphone.Core.Game;

internal readonly record struct WeatherEntry(byte Id, string Name, string EnglishKey);

internal readonly record struct WeatherWindow(WeatherEntry Weather, int MinutesFromNow, bool IsCurrent, int StartBell);

internal sealed class WeatherService
{
    private const long RealSecondsPerEorzeaHour = 175;
    private const long RealSecondsPerWindow = 1400;
    private const long RealSecondsPerEorzeaDay = 4200;
    private readonly IDataManager data;
    private readonly IClientState clientState;
    private readonly Dictionary<byte, WeatherEntry> entries = new();
    private readonly List<WeatherEntry> zoneWeathers = new();
    private readonly List<WeatherChance> chances = new();
    private uint cachedTerritory = uint.MaxValue;

    private readonly record struct WeatherChance(byte Id, int Cumulative);

    public WeatherService(IDataManager data, IClientState clientState)
    {
        this.data = data;
        this.clientState = clientState;
    }

    public string CurrentZone()
    {
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

    public unsafe WeatherEntry? LiveRenderedWeather()
    {
        var environment = EnvManager.Instance();
        if (environment == null || environment->ActiveWeather == 0)
        {
            return null;
        }

        return Entry(environment->ActiveWeather);
    }

    public byte NaturalNow()
    {
        if (!RefreshZone())
        {
            return 0;
        }

        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return Resolve(ForecastTarget(nowUnix - nowUnix % RealSecondsPerWindow));
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
        var startUnix = nowUnix - nowUnix % RealSecondsPerWindow;
        for (var index = 0; index < count; index++)
        {
            var timestamp = startUnix + index * RealSecondsPerWindow;
            var entry = index == 0 && live.HasValue ? live.Value : Entry(Resolve(ForecastTarget(timestamp)));
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
        chances.Clear();
        zoneWeathers.Clear();
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

    private static uint ForecastTarget(long unixSeconds)
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
