using System.Numerics;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class NameEffectSyncTests
{
    private static readonly string[] ServerEffectKeys =
    {
        "none", "gradient", "breath", "ripple", "flow", "glint", "sweep", "wave",
        "ember", "frost", "aurora", "prism", "glitch", "starfall", "eclipse", "heartbeat",
        "pulse", "glow",
        "spectrum", "candy", "stripes", "horizon", "chrome", "blaze",
        "bounce", "shiver", "wobble", "pop", "flipboard", "typewriter",
        "outline", "shadow", "longshadow", "emboss", "chromatic", "neon", "underline", "scan", "comet",
        "sakura", "snowfall", "fireflies", "hearts", "glitter", "bubbles", "confetti", "storm",
    };

    private static readonly string[] SellableEffectKeys =
    {
        "gradient", "flow", "wave", "ember", "frost", "aurora", "prism", "glitch",
        "starfall", "eclipse", "heartbeat", "pulse", "glow",
        "spectrum", "candy", "stripes", "horizon", "chrome", "blaze",
        "bounce", "shiver", "wobble", "pop", "flipboard", "typewriter",
        "outline", "shadow", "longshadow", "emboss", "chromatic", "neon", "underline", "scan", "comet",
        "sakura", "snowfall", "fireflies", "hearts", "glitter", "bubbles", "confetti", "storm",
    };

    private static readonly string[] SeededEffectKeys =
    {
        "glitch", "starfall", "bounce", "shiver", "wobble", "pop", "flipboard", "typewriter",
        "neon", "scan", "comet", "storm",
    };

    private static readonly string[] RoleSignatureKeys =
    {
        "breath", "ripple", "glint", "sweep",
    };

    private static BadgeStyle Badge(string effect, params string[] colors) =>
        BadgeStyle.From(new BadgeDescriptorDto("shop-flair-" + effect, effect, "0xF06D", string.Empty, string.Empty,
            colors, effect));

    [Fact]
    public void EveryEffectKeyTheServerCanSendResolvesToItsOwnKind()
    {
        var seen = new Dictionary<NameEffectKind, string>();
        for (var index = 0; index < ServerEffectKeys.Length; index++)
        {
            var key = ServerEffectKeys[index];
            var kind = Badge(key, "0xFF8A3D").Effect;
            if (key != "none")
            {
                Assert.True(kind != NameEffectKind.None, key + " fell through to None");
            }

            Assert.False(seen.ContainsKey(kind), key + " collides with " + seen.GetValueOrDefault(kind));
            seen[kind] = key;
        }

        Assert.Equal(ServerEffectKeys.Length, seen.Count);
    }

    [Fact]
    public void EveryKindTheClientKnowsHasAServerKey()
    {
        foreach (var kind in Enum.GetValues<NameEffectKind>())
        {
            var matched = false;
            for (var index = 0; index < ServerEffectKeys.Length; index++)
            {
                if (Badge(ServerEffectKeys[index], "0xFF8A3D").Effect == kind)
                {
                    matched = true;
                    break;
                }
            }

            Assert.True(matched, kind + " has no server key, so it can never reach the client");
        }
    }

    [Fact]
    public void EverySellableEffectAnimates()
    {
        for (var index = 0; index < SellableEffectKeys.Length; index++)
        {
            var key = SellableEffectKeys[index];
            var effect = NameEffects.For(Badge(key, "0xFF8A3D", "0x0000EF"), false);
            Assert.True(effect.Kind != NameEffectKind.None, key + " produced no effect");
            Assert.True(effect.Crest.W > 0f, key + " has a transparent crest");
        }
    }

    [Fact]
    public void TheSecondColourDrivesTheAccentSoBothColoursShowUp()
    {
        var twoTone = NameEffects.For(Badge("flow", "0xFF8A3D", "0x0000EF"), false);
        var accentOnly = RoleInk.Highlight(new Vector4(0f, 0f, 0xEF / 255f, 1f), false);
        Assert.Equal(accentOnly.X, twoTone.Crest.X, 3);
        Assert.Equal(accentOnly.Y, twoTone.Crest.Y, 3);
        Assert.Equal(accentOnly.Z, twoTone.Crest.Z, 3);
    }

    [Fact]
    public void ASingleColourBadgeStillGetsAReadableCrest()
    {
        var single = NameEffects.For(Badge("ember", "0xFF8A3D"), false);
        var expected = RoleInk.Highlight(new Vector4(1f, 0x8A / 255f, 0x3D / 255f, 1f), false);
        Assert.Equal(expected.X, single.Crest.X, 3);
        Assert.Equal(expected.Y, single.Crest.Y, 3);
        Assert.Equal(expected.Z, single.Crest.Z, 3);
    }

    [Fact]
    public void RampEffectsCarryAllFourStops()
    {
        var ramped = NameEffects.For(Badge("aurora", "0xFF0000", "0x00FF00", "0x0000FF", "0xFFFF00"), false);
        Assert.NotEqual(ramped.Ramp.Start, ramped.Ramp.Quarter);
        Assert.NotEqual(ramped.Ramp.Quarter, ramped.Ramp.Half);
        Assert.NotEqual(ramped.Ramp.Half, ramped.Ramp.ThreeQuarter);
    }

    [Fact]
    public void RoleSignatureEffectsAreNeverSold()
    {
        for (var signatureIndex = 0; signatureIndex < RoleSignatureKeys.Length; signatureIndex++)
        {
            for (var sellableIndex = 0; sellableIndex < SellableEffectKeys.Length; sellableIndex++)
            {
                Assert.True(RoleSignatureKeys[signatureIndex] != SellableEffectKeys[sellableIndex],
                    RoleSignatureKeys[signatureIndex] + " is a role signature but the shop sells it");
            }
        }
    }

    [Fact]
    public void EveryRoleEffectIsEitherASignatureOrColourDifferentiated()
    {
        foreach (var role in Enum.GetValues<RoleKind>())
        {
            var roleEffect = NameEffects.KindFor(role);
            if (roleEffect == NameEffectKind.None)
            {
                continue;
            }

            var signature = false;
            for (var index = 0; index < RoleSignatureKeys.Length; index++)
            {
                if (Badge(RoleSignatureKeys[index], "0xFF8A3D").Effect == roleEffect)
                {
                    signature = true;
                    break;
                }
            }

            Assert.True(signature
                    || roleEffect == NameEffectKind.Wave
                    || roleEffect == NameEffectKind.Gradient,
                role + " wears " + roleEffect + " which is neither a signature nor ramp-differentiated");
        }
    }

    [Fact]
    public void AnEightColourBadgeCarriesAllEightStops()
    {
        var ramped = NameEffects.For(Badge("wave",
            "0xE40303", "0xFC7B00", "0xFFE500", "0x35961E",
            "0x006691", "0x024BFD", "0x3E39BC", "0x732982"), false);
        Assert.Equal(8, ramped.Ramp.Count);
        Assert.NotEqual(ramped.Ramp.Stop(0), ramped.Ramp.Stop(7));
        Assert.Equal(ramped.Ramp.Stop(0), ramped.Ramp.Sample(0f));
        Assert.Equal(ramped.Ramp.Sample(0f), ramped.Ramp.Sample(1f));
    }

    [Fact]
    public void AStaticGradientSpansFirstToLastColourWithoutWrapping()
    {
        var ramped = NameEffects.For(Badge("gradient", "0xFF0000", "0x00FF00", "0x0000FF"), false);
        Assert.Equal(3, ramped.Ramp.Count);
        Assert.Equal(ramped.Ramp.Stop(0), ramped.Ramp.SampleAcross(0f));
        Assert.Equal(ramped.Ramp.Stop(2), ramped.Ramp.SampleAcross(1f));
    }

    [Fact]
    public void ATwoColourWaveKeepsItsDoubledCadence()
    {
        var ramped = NameEffects.For(Badge("wave", "0x000000", "0xFFD700"), false);
        Assert.Equal(4, ramped.Ramp.Count);
        Assert.Equal(ramped.Ramp.Start, ramped.Ramp.Half);
        Assert.Equal(ramped.Ramp.Quarter, ramped.Ramp.ThreeQuarter);
    }

    [Fact]
    public void PulseCyclesTheWholeNameThroughTheRamp()
    {
        var ramped = NameEffects.For(Badge("pulse", "0x5BCEFA", "0xF5AAB9", "0xFFFCFD"), false);
        Assert.Equal(NameEffectKind.Pulse, ramped.Kind);
        Assert.Equal(3, ramped.Ramp.Count);
    }

    [Fact]
    public void GlowStaysStill()
    {
        var glow = NameEffects.For(Badge("glow", "0xFFFFFF", "0x00B8FF"), false);
        Assert.Equal(NameEffectKind.Glow, glow.Kind);
        Assert.Equal(0f, glow.Phase);
        Assert.True(glow.Crest.W > 0f);
    }

    [Fact]
    public void GlitchAndStarfallStaggerAcrossBadgesSoAFeedDoesNotFireInLockstep()
    {
        var first = BadgeStyle.From(new BadgeDescriptorDto("shop-flair-a", "A", "0xF06D", string.Empty, string.Empty,
            new[] { "0xFF8A3D" }, "glitch"));
        var second = BadgeStyle.From(new BadgeDescriptorDto("shop-flair-b", "B", "0xF06D", string.Empty, string.Empty,
            new[] { "0xFF8A3D" }, "glitch"));
        Assert.NotEqual(NameEffects.For(first, false).Phase, NameEffects.For(second, false).Phase);
    }

    [Fact]
    public void EverySeededEffectStaggersAcrossBadges()
    {
        for (var index = 0; index < SeededEffectKeys.Length; index++)
        {
            var key = SeededEffectKeys[index];
            var first = BadgeStyle.From(new BadgeDescriptorDto("shop-flair-a", "A", "0xF06D", string.Empty,
                string.Empty, new[] { "0xFF8A3D" }, key));
            var second = BadgeStyle.From(new BadgeDescriptorDto("shop-flair-b", "B", "0xF06D", string.Empty,
                string.Empty, new[] { "0xFF8A3D" }, key));
            Assert.NotEqual(NameEffects.For(first, false).Phase, NameEffects.For(second, false).Phase);
        }
    }

    [Fact]
    public void EveryBadgeEffectCarriesTheSameSeedForTheSameBadge()
    {
        for (var index = 1; index < ServerEffectKeys.Length; index++)
        {
            var key = ServerEffectKeys[index];
            var badge = BadgeStyle.From(new BadgeDescriptorDto("shop-flair-seeded", key, "0xF06D", string.Empty,
                string.Empty, new[] { "0xFF8A3D", "0x0000EF" }, key));
            var effect = NameEffects.For(badge, false);
            Assert.True(effect.Seed >= 0f && effect.Seed < 1f, key + " seed out of range");
            Assert.Equal(NameEffects.For(badge, true).Seed, effect.Seed);
        }
    }

    [Fact]
    public void SpectrumIgnoresTheBadgeColoursAndCarriesAFullRainbow()
    {
        var rainbow = NameEffects.For(Badge("spectrum", "0xFF8A3D"), false);
        Assert.Equal(8, rainbow.Ramp.Count);
        Assert.NotEqual(rainbow.Ramp.Stop(0), rainbow.Ramp.Stop(4));
    }

    [Fact]
    public void HorizonWithTwoColoursSpansTopToBottomWithoutRepeating()
    {
        var horizon = NameEffects.For(Badge("horizon", "0xFF0000", "0x0000FF"), false);
        Assert.Equal(2, horizon.Ramp.Count);
        Assert.Equal(horizon.Ramp.Stop(0), horizon.Ramp.SampleAcross(0f));
        Assert.Equal(horizon.Ramp.Stop(1), horizon.Ramp.SampleAcross(1f));
    }

    [Fact]
    public void HorizonWithOneColourLeavesTheRampToTheCrest()
    {
        var horizon = NameEffects.For(Badge("horizon", "0xFF0000"), false);
        Assert.Equal(0, horizon.Ramp.Count);
        Assert.True(horizon.Crest.W > 0f);
    }

    [Fact]
    public void CandyAndConfettiAlwaysCarryARampToStepThrough()
    {
        Assert.Equal(4, NameEffects.For(Badge("candy", "0xFF8A3D"), false).Ramp.Count);
        Assert.Equal(4, NameEffects.For(Badge("confetti", "0xFF8A3D", "0x0000EF"), false).Ramp.Count);
        Assert.Equal(3, NameEffects.For(Badge("stripes", "0xFF0000", "0x00FF00", "0x0000FF"), false).Ramp.Count);
    }

    [Fact]
    public void StaticDecorationsStayStill()
    {
        string[] still = { "horizon", "outline", "shadow", "longshadow", "emboss" };
        for (var index = 0; index < still.Length; index++)
        {
            var effect = NameEffects.For(Badge(still[index], "0xFFFFFF", "0x00B8FF"), false);
            Assert.Equal(0f, effect.Phase);
        }
    }
}
