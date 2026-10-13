using Aetherphone.Core;
using Aetherphone.Core.Clock;
using Aetherphone.Core.Game;
using Aetherphone.Core.Theme;

namespace Aetherphone.Windows.Components;

internal readonly record struct NightView(float Reveal, float Lift, Vector2 Sway, float Nightness, bool HalloweenNight)
{
    private const float MoonSway = 3f;
    private const float StarSway = 1.5f;
    private const float GroundSway = 0.75f;

    public float Dimming => 0.35f + 0.65f * Nightness;

    public Vector2 MoonShift => Sway * MoonSway * UiScale.Current;

    public Vector2 StarShift => Sway * StarSway * UiScale.Current;

    public float GroundShift => Sway.X * GroundSway * UiScale.Current;

    public static NightView Now(Rect frame, float reveal)
    {
        SceneDrift.Sample(frame, out var lift, out var sway);
        var dayFraction = EorzeaTime.CurrentSeconds() % EorzeaClock.SecondsPerDay / (float)EorzeaClock.SecondsPerDay;
        var nightness = 0.5f + 0.5f * MathF.Cos(dayFraction * MathF.Tau);
        return new NightView(reveal, lift, sway, nightness, SeasonalTheme.IsHalloweenNight(DateTime.Today));
    }

    public float StarSweep(float starX) => Math.Clamp(Reveal * 1.6f - starX * 0.6f, 0f, 1f);
}
