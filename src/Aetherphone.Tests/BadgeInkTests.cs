using System.Numerics;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class BadgeInkTests
{
    private const float Readable = 4.5f;

    private static readonly Vector4[] DarkSurfaces =
    {
        new(9f / 255f, 41f / 255f, 81f / 255f, 1f),
        new(78f / 255f, 20f / 255f, 27f / 255f, 1f),
        new(41f / 255f, 11f / 255f, 33f / 255f, 1f),
    };

    private static readonly Vector4[] LightSurfaces =
    {
        new(0.949f, 0.949f, 0.969f, 1f),
        new(0.97f, 0.97f, 0.98f, 1f),
        Vector4.One,
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EmptyPocketsStaysNeutralAndReadable(bool light)
    {
        var badge = Badge("empty-pockets", "none", "0x3D3D3D", "0x040404", "0x454545");
        var ink = RoleInk.For(badge.Colors[0], light);

        Assert.Equal(ink.X, ink.Y);
        Assert.Equal(ink.Y, ink.Z);
        Assert.Equal(NameEffectKind.None, NameEffects.For(badge, light).Kind);
        AssertReadable(ink, light);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SuperchargedKeepsEightReadableStripes(bool light)
    {
        var badge = Badge("supercharged", "stripes", "0xFFDB73", "0xFF9C40", "0xDB4059", "0x7A3885",
            "0x4A2C6B", "0x9B3A6E", "0xE8664F", "0xFFB84D");
        var effect = NameEffects.For(badge, light);

        Assert.Equal(NameEffectKind.Stripes, effect.Kind);
        Assert.Equal(8, effect.Ramp.Count);
        for (var stopIndex = 0; stopIndex < effect.Ramp.Count; stopIndex++)
        {
            AssertReadable(effect.Ramp.Stop(stopIndex), light);
        }

        var purple = effect.Ramp.Stop(4);
        Assert.True(purple.Z > purple.X && purple.X > purple.Y);
        var gold = effect.Ramp.Stop(0);
        Assert.True(gold.X > gold.Y && gold.Y > gold.Z);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WasteOfMoneyKeepsItsRedAndGreenFirePalette(bool light)
    {
        var badge = Badge("waste-of-money", "witchfire", "0xE40303", "0x35961E");
        var effect = NameEffects.For(badge, light);

        Assert.Equal(NameEffectKind.Witchfire, effect.Kind);
        Assert.Equal(2, effect.Ramp.Count);
        var red = effect.Ramp.Stop(0);
        var green = effect.Ramp.Stop(1);
        Assert.True(red.X > red.Y && red.X > red.Z);
        Assert.True(green.Y > green.X && green.Y > green.Z);
        AssertReadable(red, light);
        AssertReadable(green, light);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CustomInksStayReadableIncludingBlackAndSaturatedChannels(bool light)
    {
        for (var red = 0; red <= 4; red++)
        {
            for (var green = 0; green <= 4; green++)
            {
                for (var blue = 0; blue <= 4; blue++)
                {
                    var ink = RoleInk.For(new Vector4(red / 4f, green / 4f, blue / 4f, 1f), light);
                    AssertReadable(ink, light);
                }
            }
        }
    }

    [Fact]
    public void ReadableColorsKeepTheirExistingInkAndOpacity()
    {
        var gold = new Vector4(1f, 219f / 255f, 115f / 255f, 0.65f);
        var charcoal = new Vector4(61f / 255f, 61f / 255f, 61f / 255f, 0.45f);
        Assert.Equal(gold, RoleInk.For(gold, false));
        Assert.Equal(charcoal, RoleInk.For(charcoal, true));
        Assert.Equal(charcoal.W, RoleInk.For(charcoal, false).W);
        Assert.Equal(gold.W, RoleInk.For(gold, true).W);
    }

    private static BadgeStyle Badge(string id, string effect, params string[] colors) =>
        BadgeStyle.From(new BadgeDescriptorDto(id, id, "0xF059", string.Empty, string.Empty, colors, effect));

    private static void AssertReadable(Vector4 ink, bool light)
    {
        var surfaces = light ? LightSurfaces : DarkSurfaces;
        foreach (var surface in surfaces)
        {
            var contrast = Palette.ContrastRatio(ink, surface);
            Assert.True(contrast >= Readable, $"Badge ink renders at {contrast:F2}:1 on {surface}.");
        }
    }
}
