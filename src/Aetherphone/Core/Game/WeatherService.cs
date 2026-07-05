using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace Aetherphone.Core.Game;

internal readonly record struct WeatherWindow(string Weather, int MinutesFromNow, bool IsCurrent, int StartBell);

internal sealed class WeatherService
{
    private const long RealSecondsPerEorzeaHour = 175;
    private const long RealSecondsPerWindow = 1400;
    private const long RealSecondsPerEorzeaDay = 4200;
    private readonly IDataManager data;
    private readonly IClientState clientState;

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

    public void Forecast(List<WeatherWindow> into, int count)
    {
        into.Clear();
        var territoryId = clientState.TerritoryType;
        if (territoryId == 0 || !data.GetExcelSheet<TerritoryType>().TryGetRow(territoryId, out var territory))
        {
            return;
        }

        if (!data.GetExcelSheet<WeatherRate>().TryGetRow(territory.WeatherRate.RowId, out var rate))
        {
            return;
        }

        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var startBell = nowUnix / RealSecondsPerEorzeaHour;
        startBell -= startBell % 8;
        var startUnix = startBell * RealSecondsPerEorzeaHour;
        for (var index = 0; index < count; index++)
        {
            var timestamp = startUnix + index * RealSecondsPerWindow;
            var name = ResolveWeather(rate, ForecastTarget(timestamp));
            var minutes = (int)((timestamp - nowUnix) / 60);
            var windowBell = (int)(timestamp / RealSecondsPerEorzeaHour % 24);
            into.Add(new WeatherWindow(name, minutes, index == 0, windowBell));
        }
    }

    private string ResolveWeather(WeatherRate rate, uint target)
    {
        var cumulative = 0;
        var rates = rate.Rate;
        var weathers = rate.Weather;
        for (var index = 0; index < rates.Count; index++)
        {
            cumulative += rates[index];
            if (target < cumulative)
            {
                if (data.GetExcelSheet<Weather>().TryGetRow(weathers[index].RowId, out var weather))
                {
                    return weather.Name.ExtractText();
                }

                return string.Empty;
            }
        }

        return string.Empty;
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
