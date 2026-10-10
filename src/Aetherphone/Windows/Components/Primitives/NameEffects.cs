using Aetherphone.Core.Animation;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;

namespace Aetherphone.Windows.Components;

internal static class NameEffects
{
    private const double BreathPeriod = Pulse.Breath;
    private const double SweepPeriod = Pulse.Orbit;
    private const double GlintPeriod = 3000.0;
    private const double FlowPeriod = 4200.0;
    private const double RipplePeriod = 3600.0;
    private const double WavePeriod = 2400.0;
    private const double EmberPeriod = 2100.0;
    private const double FrostPeriod = 4600.0;
    private const double AuroraPeriod = 6500.0;
    private const double PrismPeriod = 1900.0;
    private const double GlitchPeriod = 2400.0;
    private const double StarfallPeriod = 2600.0;
    private const double EclipsePeriod = 3800.0;
    private const double HeartbeatPeriod = 2200.0;
    private const double PulsePeriod = 4800.0;
    private const double SpectrumPeriod = 3600.0;
    private const double CandyPeriod = 3200.0;
    private const double StripesPeriod = 2600.0;
    private const double ChromePeriod = 3600.0;
    private const double BlazePeriod = 2400.0;
    private const double BouncePeriod = 1600.0;
    private const double ShiverPeriod = 3000.0;
    private const double WobblePeriod = 2200.0;
    private const double PopPeriod = 2600.0;
    private const double FlipboardPeriod = 3800.0;
    private const double TypewriterPeriod = 6000.0;
    private const double ChromaticPeriod = 2600.0;
    private const double NeonPeriod = 4000.0;
    private const double UnderlinePeriod = 2800.0;
    private const double ScanPeriod = 2400.0;
    private const double CometPeriod = 2200.0;
    private const double MotePeriod = 5000.0;
    private const double StormPeriod = 1600.0;

    private const int RainbowStops = 8;
    private const float RainbowSaturation = 0.72f;

    public static TextEffect For(RoleKind role, bool light)
    {
        var kind = KindFor(role);
        if (kind == NameEffectKind.None)
        {
            return default;
        }

        if (kind == NameEffectKind.Wave)
        {
            return new TextEffect(kind, RoleInk.Highlight(role, light), Phase(kind), RoleInk.Ramp(role, light));
        }

        return new TextEffect(kind, RoleInk.Highlight(role, light), Phase(kind));
    }

    public static TextEffect For(BadgeStyle badge, bool light)
    {
        if (badge.Effect == NameEffectKind.None)
        {
            return default;
        }

        var crest = RoleInk.Highlight(badge.Colors[badge.Colors.Length > 1 ? 1 : 0], light);
        var seed = Seed(badge.Id);
        var phase = Phase(badge.Effect);
        if (Decorrelated(badge.Effect))
        {
            phase = Fraction(phase + seed);
        }

        if (badge.Effect == NameEffectKind.Spectrum)
        {
            return new TextEffect(badge.Effect, crest, phase, RainbowRamp(light), seed);
        }

        if (SamplesAcross(badge.Effect, badge.Colors.Length))
        {
            return new TextEffect(badge.Effect, crest, phase, RampAcross(badge.Colors, light), seed);
        }

        if (UsesRamp(badge.Effect, badge.Colors.Length))
        {
            return new TextEffect(badge.Effect, crest, phase, RampFrom(badge.Colors, light), seed);
        }

        return new TextEffect(badge.Effect, crest, phase, default, seed);
    }

    private static bool UsesRamp(NameEffectKind kind, int colorCount)
    {
        if (kind == NameEffectKind.Gradient || kind == NameEffectKind.Pulse)
        {
            return colorCount > 1;
        }

        return kind == NameEffectKind.Wave
            || kind == NameEffectKind.Aurora
            || kind == NameEffectKind.Prism
            || kind == NameEffectKind.Glitch
            || kind == NameEffectKind.Candy
            || kind == NameEffectKind.Stripes
            || kind == NameEffectKind.Confetti
            || kind == NameEffectKind.Sakura
            || kind == NameEffectKind.Hearts;
    }

    private static bool SamplesAcross(NameEffectKind kind, int colorCount)
    {
        return colorCount > 1 && (kind == NameEffectKind.Horizon || kind == NameEffectKind.Blaze);
    }

    private static bool Decorrelated(NameEffectKind kind)
    {
        return kind switch
        {
            NameEffectKind.Glitch => true,
            NameEffectKind.Starfall => true,
            NameEffectKind.Bounce => true,
            NameEffectKind.Shiver => true,
            NameEffectKind.Wobble => true,
            NameEffectKind.Pop => true,
            NameEffectKind.Flipboard => true,
            NameEffectKind.Typewriter => true,
            NameEffectKind.Neon => true,
            NameEffectKind.Scan => true,
            NameEffectKind.Comet => true,
            NameEffectKind.Storm => true,
            _ => false,
        };
    }

    private static float Seed(string badgeId)
    {
        var hash = 2166136261u;
        for (var index = 0; index < badgeId.Length; index++)
        {
            hash = (hash ^ badgeId[index]) * 16777619u;
        }

        return (hash % 1000u) / 1000f;
    }

    private static float Fraction(float value) => value - MathF.Floor(value);

    private static WaveRamp RampFrom(Vector4[] colors, bool light)
    {
        if (colors.Length == 1)
        {
            var fill = RoleInk.For(colors[0], light);
            var crest = RoleInk.Highlight(colors[0], light);
            return new WaveRamp(fill, crest, fill, crest);
        }

        if (colors.Length == 2)
        {
            var first = RoleInk.For(colors[0], light);
            var second = RoleInk.For(colors[1], light);
            return new WaveRamp(first, second, first, second);
        }

        return RampAcross(colors, light);
    }

    private static WaveRamp RampAcross(Vector4[] colors, bool light)
    {
        Span<Vector4> stops = stackalloc Vector4[WaveRamp.MaxStops];
        var count = Math.Min(colors.Length, WaveRamp.MaxStops);
        for (var stopIndex = 0; stopIndex < count; stopIndex++)
        {
            stops[stopIndex] = RoleInk.For(colors[stopIndex], light);
        }

        return new WaveRamp(stops[..count]);
    }

    private static WaveRamp RainbowRamp(bool light)
    {
        Span<Vector4> stops = stackalloc Vector4[RainbowStops];
        for (var stopIndex = 0; stopIndex < RainbowStops; stopIndex++)
        {
            stops[stopIndex] = RoleInk.For(Palette.FromHue(stopIndex / (float)RainbowStops, RainbowSaturation, 1f), light);
        }

        return new WaveRamp(stops);
    }

    public static NameEffectKind KindFor(RoleKind role)
    {
        return role switch
        {
            RoleKind.Management => NameEffectKind.Sweep,
            RoleKind.Patreon => NameEffectKind.Sweep,
            RoleKind.Moderator => NameEffectKind.Glint,
            RoleKind.Developer => NameEffectKind.Ripple,
            RoleKind.Support => NameEffectKind.Breath,
            RoleKind.Aide => NameEffectKind.Wave,
            RoleKind.Aurelia => NameEffectKind.Wave,
            RoleKind.Verified => NameEffectKind.Gradient,
            _ => NameEffectKind.None,
        };
    }

    private static float Phase(NameEffectKind kind)
    {
        return kind switch
        {
            NameEffectKind.Breath => Pulse.Phase(BreathPeriod),
            NameEffectKind.Sweep => Pulse.Phase(SweepPeriod),
            NameEffectKind.Glint => Pulse.Phase(GlintPeriod),
            NameEffectKind.Flow => Pulse.Phase(FlowPeriod),
            NameEffectKind.Ripple => Pulse.Phase(RipplePeriod),
            NameEffectKind.Wave => Pulse.Phase(WavePeriod),
            NameEffectKind.Ember => Pulse.Phase(EmberPeriod),
            NameEffectKind.Frost => Pulse.Phase(FrostPeriod),
            NameEffectKind.Aurora => Pulse.Phase(AuroraPeriod),
            NameEffectKind.Prism => Pulse.Phase(PrismPeriod),
            NameEffectKind.Glitch => Pulse.Phase(GlitchPeriod),
            NameEffectKind.Starfall => Pulse.Phase(StarfallPeriod),
            NameEffectKind.Eclipse => Pulse.Phase(EclipsePeriod),
            NameEffectKind.Heartbeat => Pulse.Phase(HeartbeatPeriod),
            NameEffectKind.Pulse => Pulse.Phase(PulsePeriod),
            NameEffectKind.Spectrum => Pulse.Phase(SpectrumPeriod),
            NameEffectKind.Candy => Pulse.Phase(CandyPeriod),
            NameEffectKind.Stripes => Pulse.Phase(StripesPeriod),
            NameEffectKind.Chrome => Pulse.Phase(ChromePeriod),
            NameEffectKind.Blaze => Pulse.Phase(BlazePeriod),
            NameEffectKind.Bounce => Pulse.Phase(BouncePeriod),
            NameEffectKind.Shiver => Pulse.Phase(ShiverPeriod),
            NameEffectKind.Wobble => Pulse.Phase(WobblePeriod),
            NameEffectKind.Pop => Pulse.Phase(PopPeriod),
            NameEffectKind.Flipboard => Pulse.Phase(FlipboardPeriod),
            NameEffectKind.Typewriter => Pulse.Phase(TypewriterPeriod),
            NameEffectKind.Chromatic => Pulse.Phase(ChromaticPeriod),
            NameEffectKind.Neon => Pulse.Phase(NeonPeriod),
            NameEffectKind.Underline => Pulse.Phase(UnderlinePeriod),
            NameEffectKind.Scan => Pulse.Phase(ScanPeriod),
            NameEffectKind.Comet => Pulse.Phase(CometPeriod),
            NameEffectKind.Sakura => Pulse.Phase(MotePeriod),
            NameEffectKind.Snowfall => Pulse.Phase(MotePeriod),
            NameEffectKind.Fireflies => Pulse.Phase(MotePeriod),
            NameEffectKind.Hearts => Pulse.Phase(MotePeriod),
            NameEffectKind.Glitter => Pulse.Phase(MotePeriod),
            NameEffectKind.Bubbles => Pulse.Phase(MotePeriod),
            NameEffectKind.Confetti => Pulse.Phase(MotePeriod),
            NameEffectKind.Storm => Pulse.Phase(StormPeriod),
            _ => 0f,
        };
    }
}
