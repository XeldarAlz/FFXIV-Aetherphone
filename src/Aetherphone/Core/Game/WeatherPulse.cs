namespace Aetherphone.Core.Game;

internal struct WeatherPulse
{
    private const long SecondsPerMinute = 60;
    private long minute;
    private long window;
    private uint territory;
    private byte live;
    private bool primed;

    public bool Due(uint watchedTerritory, byte liveWeather)
    {
        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var nowMinute = nowUnix / SecondsPerMinute;
        var nowWindow = WeatherService.WindowStart(nowUnix);
        if (primed && nowMinute == minute && nowWindow == window && watchedTerritory == territory &&
            liveWeather == live)
        {
            return false;
        }

        primed = true;
        minute = nowMinute;
        window = nowWindow;
        territory = watchedTerritory;
        live = liveWeather;
        return true;
    }
}
