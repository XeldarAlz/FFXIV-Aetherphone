using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Home;
using Aetherphone.Core.Shell.Home;
using Aetherphone.Core.Theme;
using Xunit;

namespace Aetherphone.Tests;

public sealed class PhoneScalingTests
{
    private const float SnapTolerance = 2.5f;
    private const int Columns = 4;
    private const int Rows = 6;

    public static TheoryData<float, float> Cases()
    {
        var widths = new[] { PhoneSizeCatalog.MinimumWidth, 280f, 337f, 400f, 500f, 673f, PhoneSizeCatalog.MaximumWidth };
        var globalScales = new[] { 1f, 1.25f };
        var data = new TheoryData<float, float>();
        for (var widthIndex = 0; widthIndex < widths.Length; widthIndex++)
        {
            for (var scaleIndex = 0; scaleIndex < globalScales.Length; scaleIndex++)
            {
                data.Add(widths[widthIndex], globalScales[scaleIndex]);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ChassisScalesLinearlyWithPhoneWidth(float width, float globalScale)
    {
        var reference = ChassisFor(PhoneSizeCatalog.DesignWidth, globalScale);
        var scaled = ChassisFor(width, globalScale);
        var ratio = PhoneSizeCatalog.ZoomFor(width);
        var tolerance = SnapTolerance * MathF.Max(ratio, 1f);
        Assert.Equal(reference.Screen.Width * ratio, scaled.Screen.Width, tolerance);
        Assert.Equal(reference.Screen.Height * ratio, scaled.Screen.Height, tolerance);
        Assert.Equal(reference.ScreenRadius * ratio, scaled.ScreenRadius, tolerance);
        Assert.Equal(BezelWidth(reference) * ratio, BezelWidth(scaled), tolerance);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void HomeGridScalesLinearlyWithPhoneWidth(float width, float globalScale)
    {
        var reference = HomeFor(PhoneSizeCatalog.DesignWidth, globalScale);
        var scaled = HomeFor(width, globalScale);
        var ratio = PhoneSizeCatalog.ZoomFor(width);
        var tolerance = SnapTolerance * MathF.Max(ratio, 1f);
        Assert.Equal(reference.CellWidth * ratio, scaled.CellWidth, tolerance);
        Assert.Equal(reference.CellHeight * ratio, scaled.CellHeight, tolerance);
        Assert.Equal(reference.IconSize * ratio, scaled.IconSize, tolerance);
        Assert.Equal(reference.DockBar.Height * ratio, scaled.DockBar.Height, tolerance);
        Assert.Equal(reference.Grid.Height * ratio, scaled.Grid.Height, tolerance);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ScreenKeepsTheDesignAspect(float width, float globalScale)
    {
        var chassis = ChassisFor(width, globalScale);
        var reference = ChassisFor(PhoneSizeCatalog.DesignWidth, globalScale);
        var expected = reference.Screen.Width / reference.Screen.Height;
        Assert.Equal(expected, chassis.Screen.Width / chassis.Screen.Height, 0.01f);
    }

    [Fact]
    public void MigrationMapsLegacyPresetsToTheirWidths()
    {
        Assert.Equal(280f, MathF.Round(0.778f * PhoneSizeCatalog.DesignWidth));
        Assert.Equal(360f, MathF.Round(1.0f * PhoneSizeCatalog.DesignWidth));
        Assert.Equal(450f, MathF.Round(1.25f * PhoneSizeCatalog.DesignWidth));
        Assert.Equal(500f, MathF.Round(1.389f * PhoneSizeCatalog.DesignWidth));
    }

    [Fact]
    public void SnapPullsNearMissesOntoPresets()
    {
        Assert.Equal(450f, PhoneSizeCatalog.Snap(452f));
        Assert.Equal(360f, PhoneSizeCatalog.Snap(357f));
        Assert.Equal(423f, PhoneSizeCatalog.Snap(423f));
    }

    [Fact]
    public void TextZoomSnapKeepsTheLegacySteps()
    {
        Assert.Equal(1.0f, TextZoomCatalog.Snap(1.005f), 0.0001f);
        Assert.Equal(1.15f, TextZoomCatalog.Snap(1.16f), 0.0001f);
        Assert.Equal(1.3f, TextZoomCatalog.Snap(1.29f), 0.0001f);
        Assert.Equal(1.5f, TextZoomCatalog.Snap(1.49f), 0.0001f);
        Assert.Equal(1.22f, TextZoomCatalog.Snap(1.221f), 0.0001f);
    }

    [Fact]
    public void FreshInstallDefaultsMatchTheShippedSizes()
    {
        var fresh = new Aetherphone.Configuration();
        var width = PhoneSizeCatalog.WidthForScale(fresh.PhoneScale);
        Assert.Equal(PhoneSizeCatalog.DefaultWidth, width, 0.5f);
        Assert.Equal(1.35f, PhoneSizeCatalog.ZoomFor(width), 0.005f);
        Assert.Equal(1.0f, fresh.TextZoom, 0.0001f);
    }

    [Fact]
    public void TextZoomStaysInsideTheBakedRange()
    {
        Assert.Equal(TextZoomCatalog.MinimumZoom, TextZoomCatalog.Clamp(0.4f));
        Assert.Equal(TextZoomCatalog.MaximumZoom, TextZoomCatalog.Clamp(9f));
    }

    private static float BezelWidth(in ChassisGeometry chassis) => chassis.Screen.Min.X - chassis.Body.Min.X;

    private static ChassisGeometry ChassisFor(float width, float globalScale)
    {
        var theme = PhoneTheme.Dark(new Vector4(0.55f, 0.45f, 0.95f, 1f),
            PhoneCase.Color("Titanium", new Vector4(0.145f, 0.145f, 0.170f, 1f)),
            ChassisMetrics.For(PhoneCaseKind.Color, PhoneSizeCatalog.DesignWidth), "DuskLight", "DuskDark");
        var size = PhoneSizeCatalog.SizeFor(width) * globalScale;
        var window = new Rect(Vector2.Zero, size);
        return ChassisGeometry.Device(window, theme, globalScale * PhoneSizeCatalog.ZoomFor(width));
    }

    private static HomeMetrics HomeFor(float width, float globalScale)
    {
        var chassis = ChassisFor(width, globalScale);
        return HomeMetrics.Compute(chassis.Screen, Columns, Rows, globalScale * PhoneSizeCatalog.ZoomFor(width),
            HomeMotion.Rest);
    }
}
