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

        var crest = RoleInk.Highlight(badge.Colors[0], light);
        if (badge.Effect == NameEffectKind.Wave)
        {
            return new TextEffect(badge.Effect, crest, Phase(badge.Effect), RampFrom(badge.Colors, light));
        }

        return new TextEffect(badge.Effect, crest, Phase(badge.Effect));
    }

    private static WaveRamp RampFrom(Vector4[] colors, bool light)
    {
        if (colors.Length == 1)
        {
            var fill = RoleInk.For(colors[0], light);
            var crest = RoleInk.Highlight(colors[0], light);
            return new WaveRamp(fill, crest, fill, crest);
        }

        return new WaveRamp(
            RoleInk.For(colors[0], light),
            RoleInk.For(colors[1 % colors.Length], light),
            RoleInk.For(colors[2 % colors.Length], light),
            RoleInk.For(colors[3 % colors.Length], light));
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
            _ => 0f,
        };
    }
}
