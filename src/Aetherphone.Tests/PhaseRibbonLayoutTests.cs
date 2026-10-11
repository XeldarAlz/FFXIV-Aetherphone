using System.Numerics;
using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Xunit;

namespace Aetherphone.Tests;

public sealed class PhaseRibbonLayoutTests
{
    private const float PhoneWidth = 360f;
    private const float PhoneHeight = 640f;

    public static TheoryData<float> Scales => new() { 0.75f, 0.9f, 1f, 1.25f, 1.5f };

    [Theory]
    [MemberData(nameof(Scales))]
    public void TheRibbonIsOneCompactLineUnderTheChromeBand(float scale)
    {
        var stage = Stage(scale);
        var layout = PhaseRibbonLayout.Compute(stage.Ribbon, 14f * scale, true, 9f * scale, true, scale);

        Assert.True(stage.Ribbon.Height <= PhaseRibbonLayout.MaximumHeight * scale + 0.01f);
        Assert.True(layout.Capsule.Height <= PhaseRibbonLayout.MaximumHeight * scale + 0.01f);
        Assert.True(stage.Ribbon.Min.Y >= stage.Band.Max.Y - 0.01f);
        Assert.True(stage.CapsuleCenter.Y + CasinoStageLayout.CapsuleHeight * scale * 0.5f <= layout.Capsule.Min.Y);
        Assert.True(Inside(layout.Capsule, stage.Ribbon));
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void RingLabelAndCrowdReadLeftToRightWithoutOverlap(float scale)
    {
        var stage = Stage(scale);
        var layout = PhaseRibbonLayout.Compute(stage.Ribbon, 26f * scale, true, 22f * scale, true, scale);

        Assert.True(layout.HasRing && layout.HasCrowd);
        Assert.True(layout.RingCenter.X - layout.RingRadiusPixels >= layout.Capsule.Min.X - 0.01f);
        Assert.True(layout.RingCenter.Y - layout.RingRadiusPixels >= layout.Capsule.Min.Y - 0.01f);
        Assert.True(layout.RingCenter.Y + layout.RingRadiusPixels <= layout.Capsule.Max.Y + 0.01f);
        Assert.True(layout.Label.Min.X >= layout.RingCenter.X + layout.RingRadiusPixels);
        Assert.True(layout.Label.Max.X <= layout.Crowd.Min.X);
        Assert.True(layout.Crowd.Max.X <= layout.Capsule.Max.X);
        Assert.True(layout.Label.Width > 120f * scale);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void TheCountdownTextAlwaysFitsInsideItsRing(float scale)
    {
        var stage = Stage(scale);
        var digits = 26f * scale;
        var layout = PhaseRibbonLayout.Compute(stage.Ribbon, digits, true, 0f, false, scale);

        Assert.True(layout.RingRadiusPixels * 2f >= digits);
        Assert.True(layout.RingRadiusPixels >= PhaseRibbonLayout.RingRadius * scale - 0.01f);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void WithoutATimerOrCrowdTheLabelTakesTheWholeCapsule(float scale)
    {
        var stage = Stage(scale);
        var layout = PhaseRibbonLayout.Compute(stage.Ribbon, 0f, false, 0f, false, scale);

        Assert.False(layout.HasRing);
        Assert.False(layout.HasCrowd);
        Assert.True(Inside(layout.Label, layout.Capsule));
        Assert.True(layout.Label.Width > layout.Capsule.Width * 0.8f);
    }

    private static CasinoStageLayout Stage(float scale)
    {
        var full = new Rect(Vector2.Zero, new Vector2(PhoneWidth * scale, PhoneHeight * scale));
        return CasinoStageLayout.Compute(full, true, false, CasinoStageLayout.DeckHeight, scale);
    }

    private static bool Inside(Rect inner, Rect outer) =>
        inner.Min.X >= outer.Min.X - 0.01f && inner.Min.Y >= outer.Min.Y - 0.01f
        && inner.Max.X <= outer.Max.X + 0.01f && inner.Max.Y <= outer.Max.Y + 0.01f;
}
