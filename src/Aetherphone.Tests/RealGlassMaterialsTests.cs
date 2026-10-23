using System.Numerics;
using Aetherphone.Core.Shell;
using Aetherphone.Windows.Components;
using Glasswork.Glass;
using Xunit;

namespace Aetherphone.Tests;

public sealed class RealGlassMaterialsTests
{
    private const float NeutralAmount = 0.5f;
    private const float Radius = 12f;
    private const float Tolerance = 1e-4f;
    private static readonly Vector2 Bar = new(320f, 56f);
    private static readonly Vector4 Accent = new(0.2f, 0.5f, 0.9f, 0.7f);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ToneSelectsTheAppearance(bool light)
    {
        var tone = light ? GlassTone.Light : GlassTone.Dark;
        var material = Build(RealGlassKind.Liquid, tone, NeutralAmount);

        Assert.Equal(light, material.LightAppearance);
        Assert.Equal(GlassMaterial.ForAppearance(light).Lift, material.Lift);
        Assert.Equal(GlassMaterial.ForAppearance(light).Specular, material.Specular);
    }

    [Fact]
    public void LiquidAtTheNeutralAmountKeepsThePreset()
    {
        var preset = GlassMaterial.ForAppearance(false);
        var material = Build(RealGlassKind.Liquid, GlassTone.Dark, NeutralAmount);

        Assert.Equal(preset.Bezel, material.Bezel);
        Assert.Equal(preset.Depth, material.Depth);
        Assert.Equal(preset.Frost, material.Frost);
        Assert.Equal(preset.BlurLevels, material.BlurLevels);
        Assert.Equal(0f, material.TintAmount);
    }

    [Fact]
    public void FrostedIsFullyFrostedWithTheDeepestBlur()
    {
        var material = Build(RealGlassKind.Frosted, GlassTone.Dark, NeutralAmount);

        Assert.Equal(RealGlassMaterials.FrostedFrost, material.Frost);
        Assert.Equal(RealGlassMaterials.FrostedBlurLevels, material.BlurLevels);
        Assert.False(material.LightAppearance);
    }

    [Fact]
    public void AccentWashesThePresetWithTheOpaqueAccent()
    {
        var material = RealGlassMaterials.Build(RealGlassKind.Accent, GlassTone.Dark, Accent, 1f, NeutralAmount,
            Radius, Bar);

        Assert.Equal(Accent.X, material.Tint.R);
        Assert.Equal(Accent.Y, material.Tint.G);
        Assert.Equal(Accent.Z, material.Tint.B);
        Assert.Equal(1f, material.Tint.A);
        Assert.Equal(RealGlassMaterials.AccentTintAmount, material.TintAmount);
    }

    [Fact]
    public void AccentOnALightToneTakesTheLightPreset()
    {
        var material = RealGlassMaterials.Build(RealGlassKind.Accent, GlassTone.Light, Accent, 1f, NeutralAmount,
            Radius, Bar);

        Assert.True(material.LightAppearance);
        Assert.Equal(RealGlassMaterials.AccentTintAmount, material.TintAmount);
    }

    [Theory]
    [InlineData(0.4f, 0.4f)]
    [InlineData(3f, 1f)]
    [InlineData(-1f, 0f)]
    public void OpacityPassesThroughClamped(float opacity, float expected)
    {
        var material = RealGlassMaterials.Build(RealGlassKind.Liquid, GlassTone.Dark, default, opacity, NeutralAmount,
            Radius, Bar);

        Assert.Equal(expected, material.Opacity);
    }

    [Fact]
    public void AmountZeroClearsTheGlass()
    {
        var material = Build(RealGlassKind.Liquid, GlassTone.Dark, 0f);

        Assert.InRange(material.Frost, 0f, 0.05f + Tolerance);
        Assert.Equal(1, material.BlurLevels);
        Assert.Equal(0f, material.TintAmount);
    }

    [Fact]
    public void AmountOneFrostsAndTintsTheGlass()
    {
        var material = Build(RealGlassKind.Liquid, GlassTone.Dark, 1f);

        Assert.Equal(1f, material.Frost);
        Assert.Equal(4, material.BlurLevels);
        Assert.True(material.TintAmount > 0f);
    }

    [Fact]
    public void AmountOneLiftsALightMaterialInsteadOfTintingIt()
    {
        var material = Build(RealGlassKind.Liquid, GlassTone.Light, 1f);

        Assert.True(material.Lift > GlassMaterial.RegularLight.Lift);
        Assert.Equal(0f, material.TintAmount);
    }

    [Theory]
    [InlineData(12f, 320f, 56f, 4.2f)]
    [InlineData(28f, 320f, 56f, 2f)]
    [InlineData(40f, 320f, 56f, 2f)]
    [InlineData(22f, 44f, 44f, 2f)]
    [InlineData(0f, 44f, 44f, 4.2f)]
    public void CornersStayContinuousUntilTheRadiusReachesHalfTheShortSide(float radius, float width, float height,
        float exponent)
    {
        Assert.Equal(exponent, RealGlassMaterials.CornerExponent(radius, new Vector2(width, height)));
    }

    [Fact]
    public void BuildAppliesTheCornerExponentOfTheShape()
    {
        var squircle = RealGlassMaterials.Build(RealGlassKind.Liquid, GlassTone.Dark, default, 1f, NeutralAmount,
            Radius, Bar);
        var capsule = RealGlassMaterials.Build(RealGlassKind.Liquid, GlassTone.Dark, default, 1f, NeutralAmount,
            Bar.Y * 0.5f, Bar);

        Assert.Equal(Glasswork.CornerCurve.ContinuousExponent, squircle.CornerExponent);
        Assert.Equal(Glasswork.CornerCurve.CircularExponent, capsule.CornerExponent);
    }

    [Fact]
    public void LengthsStayUnscaled()
    {
        var material = Build(RealGlassKind.Liquid, GlassTone.Light, NeutralAmount);

        Assert.Equal(GlassMaterial.RegularLight.Bezel, material.Bezel);
        Assert.Equal(GlassMaterial.RegularLight.ShadowRadius, material.ShadowRadius);
        Assert.Equal(GlassMaterial.RegularLight.EdgeLineWidth, material.EdgeLineWidth);
    }

    private static GlassMaterial Build(RealGlassKind kind, GlassTone tone, float amount) =>
        RealGlassMaterials.Build(kind, tone, default, 1f, amount, Radius, Bar);
}
