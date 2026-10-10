using Aetherphone.Core;
using Aetherphone.Core.Clock;
using Aetherphone.Core.Game;
using Aetherphone.Core.Theme;

namespace Aetherphone.Windows.Components;

internal readonly record struct NightView(float Reveal, float Lift, float Nightness, bool HalloweenNight)
{
    private const float LiftPerScroll = 0.12f;
    private const float MaxLift = 44f;

    public float Dimming => 0.35f + 0.65f * Nightness;

    public static NightView Now(float reveal)
    {
        var lift = MathF.Min(AppSurface.OuterScrollY * LiftPerScroll, MaxLift * UiScale.Current);
        var dayFraction = EorzeaTime.CurrentSeconds() % EorzeaClock.SecondsPerDay / (float)EorzeaClock.SecondsPerDay;
        var nightness = 0.5f + 0.5f * MathF.Cos(dayFraction * MathF.Tau);
        return new NightView(reveal, lift, nightness, SeasonalTheme.IsHalloweenNight(DateTime.Today));
    }

    public float StarSweep(float starX) => Math.Clamp(Reveal * 1.6f - starX * 0.6f, 0f, 1f);
}
