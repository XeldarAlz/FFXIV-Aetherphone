using System.Numerics;
using Aetherphone.Core.Honorific;
using Xunit;

namespace Aetherphone.Tests;

public sealed class NameplateGradientsTests
{
    private const int TitleLength = 12;
    private static readonly Vector3 Red = new(1f, 0f, 0f);
    private static readonly Vector3 Blue = new(0f, 0f, 1f);

    [Fact]
    public void EveryHonorificSetDecodes()
    {
        Assert.Equal(16, NameplateGradients.All.Length);
        for (var index = 0; index < NameplateGradients.All.Length; index++)
        {
            Assert.True(NameplateGradients.All[index].Count >= 2);
            Assert.True(NameplateGradients.IsValid(index));
        }

        Assert.True(NameplateGradients.IsValid(NameplateGradients.TwoColors));
        Assert.False(NameplateGradients.IsValid(NameplateGradients.All.Length));
        Assert.False(NameplateGradients.IsValid(-2));
    }

    [Fact]
    public void PrideRainbowStartsOnHonorificsRed()
    {
        var first = NameplateGradients.All[0].Step(0);

        Assert.Equal(new Vector3(228 / 255f, 3 / 255f, 3 / 255f), first);
    }

    [Fact]
    public void PulseLightsEveryGlyphTheSame()
    {
        var look = Look(0, NameplateGradientMotion.Pulse);

        Assert.True(NameplateGradients.TryGlyphGlow(look, 0, TitleLength, 7, out var first));
        Assert.True(NameplateGradients.TryGlyphGlow(look, 5, TitleLength, 7, out var later));
        Assert.Equal(first, later);
    }

    [Fact]
    public void WaveMovesOneStepPerGlyphAndTick()
    {
        var look = Look(0, NameplateGradientMotion.Wave);

        Assert.True(NameplateGradients.TryGlyphGlow(look, 3, TitleLength, 10, out var glyphThree));
        Assert.True(NameplateGradients.TryGlyphGlow(look, 4, TitleLength, 9, out var glyphFourEarlier));
        Assert.Equal(NameplateGradients.All[0].Step(13), glyphThree);
        Assert.Equal(glyphThree, glyphFourEarlier);
    }

    [Fact]
    public void StaticSpreadsTheSetAcrossTheTitle()
    {
        var look = Look(0, NameplateGradientMotion.Static);
        var count = NameplateGradients.All[0].Count;

        Assert.True(NameplateGradients.TryGlyphGlow(look, 0, TitleLength, 999, out var start));
        Assert.True(NameplateGradients.TryGlyphGlow(look, TitleLength / 2, TitleLength, 999, out var middle));
        Assert.Equal(NameplateGradients.All[0].Step(0), start);
        Assert.Equal(NameplateGradients.All[0].Step(count / 2), middle);
    }

    [Fact]
    public void TwoColorsHoldTheFirstAtTheEndsAndTheSecondInTheMiddle()
    {
        Assert.Equal(Red, NameplateGradients.Step(NameplateGradients.TwoColors, Red, Blue, 0));
        Assert.Equal(Red, NameplateGradients.Step(NameplateGradients.TwoColors, Red, Blue, 63));
        Assert.Equal(Blue, NameplateGradients.Step(NameplateGradients.TwoColors, Red, Blue, 32));
    }

    [Fact]
    public void TwoColorsNeedBothColorsToGlow()
    {
        var look = new TitleLook(Vector3.One, Red, null, NameplateGradients.TwoColors, 1);

        Assert.False(NameplateGradients.TryGlyphGlow(look, 0, TitleLength, 0, out _));
    }

    [Fact]
    public void SolidLooksHaveNoGlyphGlow()
    {
        var look = TitleLook.Solid(Vector3.One, Red);

        Assert.False(NameplateGradients.TryGlyphGlow(look, 0, TitleLength, 0, out _));
    }

    [Fact]
    public void BlendWrapsAroundTheSet()
    {
        var count = NameplateGradients.StepCount(0);

        var wrapped = NameplateGradients.Blend(0, Red, Blue, count);
        var before = NameplateGradients.Blend(0, Red, Blue, -1f);

        Assert.Equal(NameplateGradients.All[0].Step(0), wrapped);
        Assert.Equal(NameplateGradients.All[0].Step(count - 1), before);
    }

    private static TitleLook Look(int gradient, NameplateGradientMotion motion) =>
        new(Vector3.One, Red, null, gradient, (int)motion);
}
