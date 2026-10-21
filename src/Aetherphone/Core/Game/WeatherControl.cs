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
    private int heldTime = NoTime;
    private string? timeHoldOwner;
    private bool timeApplied;
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
    public bool HasTimeHold => timeHoldOwner is not null;
    public string? TimeHoldOwner => timeHoldOwner;
    public byte WeatherOverride => weatherOverride;
    public int TimeOverrideMinutes => timeOverride;
    private int EffectiveTime => timeOverride != NoTime ? timeOverride : heldTime;

    public void SetWeather(byte id)
    {
        if (id != NoWeather && CanControl)
        {
            OverrideWeather(id);
        }
    }

    private void OverrideWeather(byte id)
    {
        weatherOverride = id;
        weather.LiveOverridden = id != NoWeather;
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

        OverrideWeather(NoWeather);
        var natural = weather.NaturalNow();
        if (!GameMemory.Attached)
        {
            if (GameMemory.Simulated is { } simulated && natural != NoWeather)
            {
                simulated.WeatherId = natural;
            }

            return;
        }

        var environment = EnvManager.Instance();
        if (environment != null && natural != NoWeather)
        {
            environment->ActiveWeather = natural;
            environment->TransitionTime = TransitionSeconds;
        }
    }

    public void HoldTime(string owner, int minuteOfDay)
    {
        timeHoldOwner = owner;
        heldTime = Math.Clamp(minuteOfDay, 0, EorzeaTime.MinutesPerDay - 1);
    }

    public void ReleaseTime(string owner)
    {
        if (!string.Equals(timeHoldOwner, owner, StringComparison.Ordinal))
        {
            return;
        }

        timeHoldOwner = null;
        heldTime = NoTime;
        if (timeOverride == NoTime)
        {
            ReleaseAppliedTime();
        }
    }

    public void ClearTime()
    {
        timeOverride = NoTime;
        if (heldTime == NoTime)
        {
            ReleaseAppliedTime();
        }
    }

    private unsafe void ReleaseAppliedTime()
    {
        if (!timeApplied)
        {
            return;
        }

        timeApplied = false;
        if (!GameMemory.Attached)
        {
            if (GameMemory.Simulated is { } simulated)
            {
                simulated.EorzeaMinuteOverride = NoTime;
            }

            return;
        }

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
        if (GameMemory.Attached && !strayOffsetSwept)
        {
            SweepStrayOffset();
        }

        if (weatherOverride == NoWeather && timeOverride == NoTime && heldTime == NoTime)
        {
            return;
        }

        if (!CanControl)
        {
            ClearWeather();
            timeOverride = NoTime;
            ReleaseAppliedTime();
            return;
        }

        if (!installed.Open)
        {
            ClearAll();
            if (heldTime == NoTime)
            {
                return;
            }
        }

        var minute = EffectiveTime;
        if (!GameMemory.Attached)
        {
            ApplySimulated(minute);
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

        if (minute == NoTime)
        {
            return;
        }

        var instance = Framework.Instance();
        if (instance != null)
        {
            var days = instance->ClientTime.EorzeaTime / EorzeaSecondsPerDay;
            instance->ClientTime.IsEorzeaTimeOverridden = true;
            instance->ClientTime.EorzeaTimeOverride = days * EorzeaSecondsPerDay + minute * 60L;
            timeApplied = true;
        }
    }

    private void ApplySimulated(int minute)
    {
        if (GameMemory.Simulated is not { } simulated)
        {
            return;
        }

        if (weatherOverride != NoWeather)
        {
            simulated.WeatherId = weatherOverride;
        }

        simulated.EorzeaMinuteOverride = minute;
        timeApplied = minute != NoTime;
    }

    private void OnTerritoryChanged(uint _)
    {
        OverrideWeather(NoWeather);
        ClearTime();
    }

    public void Dispose()
    {
        framework.Update -= OnUpdate;
        clientState.TerritoryChanged -= OnTerritoryChanged;
        timeHoldOwner = null;
        heldTime = NoTime;
        ClearAll();
    }
}
