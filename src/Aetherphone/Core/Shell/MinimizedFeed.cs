using Aetherphone.Core.Activity;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Coins;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Timers;
using Aetherphone.Core.Wallet;

namespace Aetherphone.Core.Shell;

internal sealed class MinimizedFeed
{
    private const float GilIntervalSeconds = 1f;
    private const float VentureIntervalSeconds = 5f;
    private const int ForecastWindows = 1;
    private const uint GilItemId = 1;

    private readonly WeatherService weather;
    private readonly CoinStore coins;
    private readonly AethernetSession session;
    private readonly ActivityTracker activity;
    private readonly GameData gameData;
    private readonly List<WeatherWindow> forecast = new();
    private readonly GameTimers timers;
    private float clock;
    private WeatherPulse weatherPulse;
    private float gilDue;
    private float ventureDue;
    private long gilValue = -1;
    private long coinValue = -1;
    private string gilText = string.Empty;
    private string coinText = string.Empty;
    private uint gilIconId;
    private bool gilIconResolved;

    public MinimizedFeed(WeatherService weather, CoinStore coins, AethernetSession session, ActivityTracker activity,
        GameData gameData, GameTimers timers)
    {
        this.timers = timers;
        this.weather = weather;
        this.coins = coins;
        this.session = session;
        this.activity = activity;
        this.gameData = gameData;
    }

    public string WeatherName { get; private set; } = string.Empty;

    public string WeatherKey { get; private set; } = string.Empty;

    public string Zone { get; private set; } = string.Empty;

    public bool HasWeather => WeatherName.Length > 0;

    public bool RetainersKnown { get; private set; }

    public int VenturesReady { get; private set; }

    public bool HasRunningVenture { get; private set; }

    public DateTime NextVentureUtc { get; private set; }

    public ActivityDay? Today => activity.IsTracking ? activity.Today : null;

    public void Update(float delta)
    {
        clock += delta;
    }

    public void EnsureWeather()
    {
        if (!weatherPulse.Due(weather.CurrentTerritory, weather.LiveWeatherId()))
        {
            return;
        }

        Zone = weather.CurrentZone();
        weather.Forecast(forecast, ForecastWindows);
        if (forecast.Count == 0)
        {
            WeatherName = string.Empty;
            WeatherKey = string.Empty;
            return;
        }

        WeatherName = forecast[0].Weather.Name;
        WeatherKey = forecast[0].Weather.EnglishKey;
    }

    public uint GilIconId()
    {
        if (gilIconResolved)
        {
            return gilIconId;
        }

        gilIconResolved = gameData.TryGetItem(GilItemId, out _, out var iconId, out _);
        gilIconId = gilIconResolved ? iconId : 0u;
        return gilIconId;
    }

    public string GilText()
    {
        if (clock < gilDue)
        {
            return gilText;
        }

        gilDue = clock + GilIntervalSeconds;
        var amount = WalletReader.CurrentGil();
        if (amount != gilValue)
        {
            gilValue = amount;
            gilText = NumberText.Compact(amount);
        }

        return gilText;
    }

    public string CoinText()
    {
        var amount = coins.Wallet?.Balance ?? session.CurrentUser?.Coins ?? 0;
        if (amount != coinValue)
        {
            coinValue = amount;
            coinText = NumberText.Compact(amount);
        }

        return coinText;
    }

    public void EnsureVentures()
    {
        if (clock < ventureDue)
        {
            return;
        }

        ventureDue = clock + VentureIntervalSeconds;
        var character = TimerLedger.Find(timers.Characters, timers.CurrentContentId);
        RetainersKnown = character is { Retainers.Count: > 0 };
        VenturesReady = 0;
        HasRunningVenture = false;
        NextVentureUtc = DateTime.MaxValue;
        if (character is null || !RetainersKnown)
        {
            return;
        }

        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var nextUnix = long.MaxValue;
        var retainers = character.Retainers;
        for (var index = 0; index < retainers.Count; index++)
        {
            var completeUnix = retainers[index].CompleteUnix;
            if (completeUnix <= 0)
            {
                continue;
            }

            if (completeUnix <= nowUnix)
            {
                VenturesReady++;
                continue;
            }

            HasRunningVenture = true;
            nextUnix = Math.Min(nextUnix, completeUnix);
        }

        if (HasRunningVenture)
        {
            NextVentureUtc = DateTimeOffset.FromUnixTimeSeconds(nextUnix).UtcDateTime;
        }
    }
}
