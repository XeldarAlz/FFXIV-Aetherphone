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
    private const byte NoWeather = 0;
    private const int NoTime = -1;
    private readonly WeatherService weather;
    private readonly IFramework framework;
    private readonly IClientState clientState;
    private readonly ICondition condition;
    private readonly AppGate installed;
    private byte weatherOverride = NoWeather;
    private int timeOverride = NoTime;
    private bool strayOffsetSwept;

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

    public unsafe void ClearTime()
    {
        if (timeOverride == NoTime)
        {
            return;
        }

        timeOverride = NoTime;
        var instance = Framework.Instance();
        if (instance != null)
        {
            EorzeaTime.ReleaseOverride(ref instance->ClientTime.EorzeaTimeOverride,
                ref instance->ClientTime.IsEorzeaTimeOverridden);
        }
    }

    public void ClearAll()
    {
        ClearWeather();
        ClearTime();
    }

    private unsafe void SweepStrayOffset()
    {
        var instance = Framework.Instance();
        if (instance == null)
        {
            return;
        }

        strayOffsetSwept = true;
        if (EorzeaTime.HasStrayOverrideOffset(instance->ClientTime.EorzeaTimeOverride,
                instance->ClientTime.IsEorzeaTimeOverridden))
        {
            EorzeaTime.ReleaseOverride(ref instance->ClientTime.EorzeaTimeOverride,
                ref instance->ClientTime.IsEorzeaTimeOverridden);
        }
    }

    private unsafe void OnUpdate(IFramework _)
    {
        if (!strayOffsetSwept)
        {
            SweepStrayOffset();
        }

        if (weatherOverride == NoWeather && timeOverride == NoTime)
        {
            return;
        }

        if (!installed.Open || !CanControl)
        {
            ClearAll();
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

        if (timeOverride == NoTime)
        {
            return;
        }

        var instance = Framework.Instance();
        if (instance != null)
        {
            var days = instance->ClientTime.EorzeaTime / EorzeaSecondsPerDay;
            instance->ClientTime.IsEorzeaTimeOverridden = true;
            instance->ClientTime.EorzeaTimeOverride = days * EorzeaSecondsPerDay + timeOverride * 60L;
        }
    }

    private void OnTerritoryChanged(uint _)
    {
        weatherOverride = NoWeather;
        ClearTime();
    }

    public void Dispose()
    {
        framework.Update -= OnUpdate;
        clientState.TerritoryChanged -= OnTerritoryChanged;
        ClearAll();
    }
}
