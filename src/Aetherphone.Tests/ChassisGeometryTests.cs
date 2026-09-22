using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ChassisGeometryTests
{
    private const float Tolerance = 1e-4f;

    private static readonly float[] Widths =
    {
        PhoneSizeCatalog.MinimumWidth, 280f, 320f, 337f, 360f, 400f, 450f, 500f, 673f, PhoneSizeCatalog.MaximumWidth,
    };

    private static readonly float[] GlobalScales = { 1f, 1.111f, 1.25f, 1.4f };

    public static TheoryData<float, float, float> Devices()
    {
        var data = new TheoryData<float, float, float>();
        for (var widthIndex = 0; widthIndex < Widths.Length; widthIndex++)
        {
            var size = PhoneSizeCatalog.SizeFor(Widths[widthIndex]);
            for (var scaleIndex = 0; scaleIndex < GlobalScales.Length; scaleIndex++)
            {
                data.Add(size.X, size.Y, GlobalScales[scaleIndex]);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Devices))]
    public void BandInsetsMatchRadiusDifferences(float width, float height, float scale)
    {
        var chassis = Device(width, height, scale);
        var metal = chassis.Glass.Min.X - chassis.Body.Min.X;
        var glass = chassis.Screen.Min.X - chassis.Glass.Min.X;
        Assert.Equal(chassis.BodyRadius - metal, chassis.GlassRadius, Tolerance);
        Assert.Equal(chassis.GlassRadius - glass, chassis.ScreenRadius, Tolerance);
    }

    [Theory]
    [MemberData(nameof(Devices))]
    public void CornerCentresCoincideAcrossBands(float width, float height, float scale)
    {
        var chassis = Device(width, height, scale);
        Assert.Equal(chassis.Body.Min.X + chassis.BodyRadius, chassis.Glass.Min.X + chassis.GlassRadius, Tolerance);
        Assert.Equal(chassis.Body.Min.Y + chassis.BodyRadius, chassis.Glass.Min.Y + chassis.GlassRadius, Tolerance);
        Assert.Equal(chassis.Glass.Min.X + chassis.GlassRadius, chassis.Screen.Min.X + chassis.ScreenRadius, Tolerance);
        Assert.Equal(chassis.Glass.Min.Y + chassis.GlassRadius, chassis.Screen.Min.Y + chassis.ScreenRadius, Tolerance);
    }

    [Theory]
    [MemberData(nameof(Devices))]
    public void BandsAreOrderedAndPositive(float width, float height, float scale)
    {
        var chassis = Device(width, height, scale);
        Assert.True(chassis.Glass.Min.X > chassis.Body.Min.X);
        Assert.True(chassis.Screen.Min.X > chassis.Glass.Min.X);
        Assert.True(chassis.Screen.Width > 0f);
        Assert.True(chassis.Screen.Height > 0f);
        Assert.True(chassis.ScreenRadius > 0f);
    }

    [Theory]
    [MemberData(nameof(Devices))]
    public void BodyRectSnapsToWholePixels(float width, float height, float scale)
    {
        var chassis = Device(width, height, scale);
        Assert.Equal(MathF.Round(chassis.Body.Min.X), chassis.Body.Min.X);
        Assert.Equal(MathF.Round(chassis.Body.Min.Y), chassis.Body.Min.Y);
        Assert.Equal(MathF.Round(chassis.Body.Max.X), chassis.Body.Max.X);
        Assert.Equal(MathF.Round(chassis.Body.Max.Y), chassis.Body.Max.Y);
    }

    [Fact]
    public void SnappingIsIdempotent()
    {
        var first = Device(360f, 780f, 1.111f);
        var second = ChassisGeometry.Device(Grow(first.Body, ThemeFor(360f).RailWidth * 1.111f), ThemeFor(360f),
            1.111f);
        Assert.Equal(first.Body, second.Body);
        Assert.Equal(first.Screen, second.Screen);
        Assert.Equal(first.ScreenRadius, second.ScreenRadius, Tolerance);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MorphEndpointsMatchDeviceAndPuck(bool art)
    {
        var kind = art ? PhoneCaseKind.Art : PhoneCaseKind.Color;
        var theme = ThemeFor(360f, kind);
        var body = new Rect(new Vector2(10f, 20f), new Vector2(10f + 360f - 2f * theme.RailWidth, 800f));
        var puckBody = new Rect(new Vector2(10f, 20f), new Vector2(92f, 176f));
        var atStart = ChassisGeometry.Morph(body, theme, 1f, 0f);
        var device = ChassisGeometry.Device(Grow(body, theme.RailWidth), theme, 1f);
        Assert.Equal(device.BodyRadius, atStart.BodyRadius, Tolerance);
        Assert.Equal(device.ScreenRadius, atStart.ScreenRadius, Tolerance);
        Assert.Equal(device.Screen, atStart.Screen);

        var atEnd = ChassisGeometry.Morph(puckBody, theme, 1f, 1f);
        var puck = ChassisGeometry.Puck(puckBody, kind);
        Assert.Equal(puck.BodyRadius, atEnd.BodyRadius, Tolerance);
        Assert.Equal(puck.ScreenRadius, atEnd.ScreenRadius, Tolerance);
        Assert.Equal(puck.Screen, atEnd.Screen);
    }

    [Theory]
    [InlineData(0.25f)]
    [InlineData(0.5f)]
    [InlineData(0.75f)]
    public void ArtMorphKeepsTheCaseTemplateAtEveryStep(float eased)
    {
        var theme = ThemeFor(360f, PhoneCaseKind.Art);
        var body = new Rect(new Vector2(10f, 20f), new Vector2(210f, 420f));
        var morph = ChassisGeometry.Morph(body, theme, 1f, eased);
        var template = ChassisGeometry.Puck(body, PhoneCaseKind.Art);
        Assert.Equal(template.BodyRadius, morph.BodyRadius, Tolerance);
        Assert.Equal(template.Glass, morph.Glass);
        Assert.Equal(template.Screen, morph.Screen);
    }

    [Theory]
    [InlineData(82f, 156f)]
    [InlineData(82f, 260f)]
    [InlineData(148f, 148f)]
    [InlineData(205f, 390f)]
    public void ArtPuckWearsTheCaseTemplateBands(float width, float height)
    {
        var body = new Rect(new Vector2(0f, 0f), new Vector2(width, height));
        var puck = ChassisGeometry.Puck(body, PhoneCaseKind.Art);
        var template = ChassisMetrics.ForBody(PhoneCaseKind.Art, width);
        Assert.Equal(MathF.Round(template.MetalWidth), puck.Glass.Min.X - puck.Body.Min.X, Tolerance);
        Assert.Equal(MathF.Round(template.MetalWidth), puck.Glass.Min.Y - puck.Body.Min.Y, Tolerance);
        Assert.Equal(MathF.Round(template.GlassWidth), puck.Screen.Min.X - puck.Glass.Min.X, Tolerance);
        Assert.Equal(template.DeviceRounding, puck.BodyRadius, Tolerance);
    }

    [Theory]
    [InlineData(1f, false)]
    [InlineData(1.25f, false)]
    [InlineData(1.5f, false)]
    [InlineData(2f, false)]
    [InlineData(1f, true)]
    [InlineData(1.25f, true)]
    [InlineData(1.5f, true)]
    [InlineData(2f, true)]
    public void PuckBandMatchesPuckGeometry(float scale, bool art)
    {
        var kind = art ? PhoneCaseKind.Art : PhoneCaseKind.Color;
        var body = new Rect(new Vector2(0f, 0f), new Vector2(82f * scale, 156f * scale));
        var puck = ChassisGeometry.Puck(body, kind);
        var band = ChassisGeometry.PuckBand(body.Width, kind);
        Assert.Equal(puck.Body.Width - band, puck.Screen.Width, Tolerance);
        Assert.Equal(puck.Body.Height - band, puck.Screen.Height, Tolerance);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DegenerateBodyClampsWithoutNegatives(bool art)
    {
        var kind = art ? PhoneCaseKind.Art : PhoneCaseKind.Color;
        var chassis = ChassisGeometry.Puck(new Rect(new Vector2(0f, 0f), new Vector2(6f, 6f)), kind);
        Assert.True(chassis.BodyRadius >= 0f);
        Assert.True(chassis.GlassRadius >= 0f);
        Assert.True(chassis.ScreenRadius >= 0f);
        Assert.True(chassis.Screen.Max.X >= chassis.Screen.Min.X);
        Assert.True(chassis.Screen.Max.Y >= chassis.Screen.Min.Y);
    }

    [Fact]
    public void MediumPhoneReproducesLegacyChassisNumbers()
    {
        var chassis = ChassisMetrics.For(PhoneCaseKind.Color, 360f);
        Assert.Equal(9f, chassis.MetalWidth + chassis.GlassWidth, 0.05f);
        Assert.Equal(46f, chassis.DeviceRounding, 0.05f);
        Assert.Equal(7f, chassis.RailWidth, 0.05f);
    }

    [Fact]
    public void ArtCasesShareScreenRoundingButThickenTheBezel()
    {
        var color = ChassisMetrics.For(PhoneCaseKind.Color, 360f);
        var art = ChassisMetrics.For(PhoneCaseKind.Art, 360f);
        var colorScreen = color.DeviceRounding - color.MetalWidth - color.GlassWidth;
        var artScreen = art.DeviceRounding - art.MetalWidth - art.GlassWidth;
        Assert.Equal(colorScreen, artScreen, Tolerance);
        Assert.True(art.MetalWidth > color.MetalWidth * 2f);
        Assert.Equal(color.RailWidth, art.RailWidth, Tolerance);
    }

    [Fact]
    public void BodyMetricsRoundTripThroughRailInset()
    {
        var window = ChassisMetrics.For(PhoneCaseKind.Art, 360f);
        var bodyWidth = 360f - 2f * window.RailWidth;
        var body = ChassisMetrics.ForBody(PhoneCaseKind.Art, bodyWidth);
        Assert.Equal(window.MetalWidth, body.MetalWidth, Tolerance);
        Assert.Equal(window.DeviceRounding, body.DeviceRounding, Tolerance);
    }

    private static PhoneTheme ThemeFor(float deviceWidth) => ThemeFor(deviceWidth, PhoneCaseKind.Color);

    private static PhoneTheme ThemeFor(float deviceWidth, PhoneCaseKind kind)
    {
        var tint = new Vector4(0.145f, 0.145f, 0.170f, 1f);
        var phoneCase = kind == PhoneCaseKind.Art
            ? PhoneCase.Art("Silkie", PhoneCaseCategory.ArtistSeries, tint, "Silkie")
            : PhoneCase.Color("Titanium", tint);
        return PhoneTheme.Dark(new Vector4(0.55f, 0.45f, 0.95f, 1f), phoneCase,
            ChassisMetrics.For(kind, deviceWidth), "DuskLight", "DuskDark");
    }

    private static ChassisGeometry Device(float width, float height, float scale)
    {
        var theme = ThemeFor(width);
        var window = new Rect(new Vector2(13.4f, 27.9f),
            new Vector2(13.4f + width * scale, 27.9f + height * scale));
        return ChassisGeometry.Device(window, theme, scale);
    }

    private static Rect Grow(Rect body, float rail) =>
        new(new Vector2(body.Min.X - rail, body.Min.Y), new Vector2(body.Max.X + rail, body.Max.Y));
}
