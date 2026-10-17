using Aetherphone.Core.Home;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Graphics.Environment;
using FFXIVClientStructs.FFXIV.Client.System.Framework;

namespace Aetherphone.Core.Game;

internal sealed class WeatherControl : IDisposable
{
    private const float TransitionSeconds = 0.5f;
    private const long EorzeaSecondsPerDay = 86400;
    private const long ReleaseHoldMilliseconds = 1_000;
    private const byte NoWeather = 0;
    private const int NoTime = -1;
    private readonly WeatherService weather;
    private readonly IFramework framework;
    private readonly IClientState clientState;
    private readonly ICondition condition;
    private readonly AppGate installed;
    private byte weatherOverride = NoWeather;
    private int timeOverride = NoTime;
    private long releaseUntilTick;

    public WeatherControl(WeatherService weather, IFramework framework, IClientState clientState, ICondition condition,
        AppGate installed)
    {
        this.weather = weather;
        this.framework = framework;
        this.clientState = clientState;
        this.condition = condition;
        this.installed = installed;
        framework.Update += OnUpdate;
        clientState.TerritoryChanged += OnTerritoryChanged;
    }

    public bool CanControl => clientState.IsLoggedIn && !condition[ConditionFlag.InCombat] &&
                              !condition[ConditionFlag.BetweenAreas] && !condition[ConditionFlag.BetweenAreas51];

    public bool HasWeatherOverride => weatherOverride != NoWeather;
    public bool HasTimeOverride => timeOverride != NoTime;
    public bool HasOverride => HasWeatherOverride || HasTimeOverride;
    public byte WeatherOverride => weatherOverride;
    public int TimeOverrideMinutes => timeOverride;

    private bool ReleasingTime => releaseUntilTick != 0;

    public void SetWeather(byte id)
    {
        if (id != NoWeather && CanControl)
        {
            weatherOverride = id;
        }
    }

    public void SetTime(int minuteOfDay)
    {
        if (CanControl)
        {
            releaseUntilTick = 0;
            timeOverride = Math.Clamp(minuteOfDay, 0, EorzeaTime.MinutesPerDay - 1);
        }
    }

    public unsafe void ClearWeather()
    {
        if (weatherOverride == NoWeather)
        {
            return;
        }

        weatherOverride = NoWeather;
        var natural = weather.NaturalNow();
        var environment = EnvManager.Instance();
        if (environment != null && natural != NoWeather)
        {
            environment->ActiveWeather = natural;
            environment->TransitionTime = TransitionSeconds;
        }
    }

    public void ClearTime()
    {
        if (timeOverride == NoTime)
        {
            return;
        }

        timeOverride = NoTime;
        releaseUntilTick = Environment.TickCount64 + ReleaseHoldMilliseconds;
    }

    public void ClearAll()
    {
        ClearWeather();
        ClearTime();
    }

    private unsafe void DropTime()
    {
        timeOverride = NoTime;
        releaseUntilTick = 0;
        var instance = Framework.Instance();
        if (instance != null)
        {
            instance->ClientTime.IsEorzeaTimeOverridden = false;
        }
    }

    private void DropAll()
    {
        ClearWeather();
        DropTime();
    }

    private unsafe void OnUpdate(IFramework _)
    {
        if (weatherOverride == NoWeather && timeOverride == NoTime && !ReleasingTime)
        {
            return;
        }

        if (!installed.Open || !CanControl)
        {
            DropAll();
            return;
        }

        if (weatherOverride != NoWeather)
        {
            var environment = EnvManager.Instance();
            if (environment != null && environment->ActiveWeather != weatherOverride)
            {
                environment->ActiveWeather = weatherOverride;
                environment->TransitionTime = TransitionSeconds;
            }
        }

        if (timeOverride != NoTime)
        {
            var instance = Framework.Instance();
            if (instance != null)
            {
                var days = instance->ClientTime.EorzeaTime / EorzeaSecondsPerDay;
                instance->ClientTime.IsEorzeaTimeOverridden = true;
                instance->ClientTime.EorzeaTimeOverride = days * EorzeaSecondsPerDay + timeOverride * 60L;
            }

            return;
        }

        if (ReleasingTime)
        {
            ReleaseTime();
        }
    }

    private unsafe void ReleaseTime()
    {
        var instance = Framework.Instance();
        if (instance == null || Environment.TickCount64 >= releaseUntilTick)
        {
            DropTime();
            return;
        }

        instance->ClientTime.IsEorzeaTimeOverridden = true;
        instance->ClientTime.EorzeaTimeOverride = instance->ClientTime.EorzeaTime;
    }

    private void OnTerritoryChanged(uint _)
    {
        weatherOverride = NoWeather;
        DropTime();
    }

    public void Dispose()
    {
        framework.Update -= OnUpdate;
        clientState.TerritoryChanged -= OnTerritoryChanged;
        DropAll();
    }
}
