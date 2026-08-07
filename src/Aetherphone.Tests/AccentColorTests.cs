using System.Numerics;
using Aetherphone.Core.Theme;
using Xunit;

namespace Aetherphone.Tests;

public sealed class AccentColorTests
{
    private const float Tolerance = 1e-3f;

    public static TheoryData<string> Hexes()
    {
        var data = new TheoryData<string>();
        var samples = new[]
        {
            "000000", "FFFFFF", "FF0000", "00FF00", "0000FF", "3A7BD5", "F2B705", "7F7F7F", "1A1B1E", "C0FFEE",
        };

        for (var index = 0; index < samples.Length; index++)
        {
            data.Add(samples[index]);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Hexes))]
    public void HexSurvivesTheHsvRoundTrip(string digits)
    {
        Assert.True(HexColor.TryParse(digits, out var color));
        Assert.Equal(digits, HexColor.ToDigits(HsvColor.FromRgb(color).ToRgb()));
    }

    [Theory]
    [MemberData(nameof(Hexes))]
    public void CustomAccentResolvesToTheStoredColor(string digits)
    {
        var stored = "#" + digits;
        Assert.True(ThemeCatalog.IsCustomAccent(stored));
        var resolved = ThemeCatalog.ResolveAccent(stored);
        Assert.True(HexColor.TryParse(digits, out var expected));
        Assert.Equal(expected.X, resolved.X, Tolerance);
        Assert.Equal(expected.Y, resolved.Y, Tolerance);
        Assert.Equal(expected.Z, resolved.Z, Tolerance);
        Assert.Equal(1f, resolved.W, Tolerance);
    }

    [Fact]
    public void PresetNamesStayPresets()
    {
        for (var index = 0; index < ThemeCatalog.Accents.Count; index++)
        {
            var preset = ThemeCatalog.Accents[index];
            Assert.False(ThemeCatalog.IsCustomAccent(preset.Name));
            Assert.Equal(preset.Color, ThemeCatalog.ResolveAccent(preset.Name));
        }
    }

    [Fact]
    public void UnknownAccentFallsBackToTheFirstPreset()
    {
        Assert.Equal(ThemeCatalog.Accents[0].Color, ThemeCatalog.ResolveAccent("Chartreuse"));
        Assert.Equal(ThemeCatalog.Accents[0].Color, ThemeCatalog.ResolveAccent("#GGGGGG"));
    }

    [Fact]
    public void GrayShadesKeepTheirHue()
    {
        var gray = new Vector4(0.5f, 0.5f, 0.5f, 1f);
        var hsv = HsvColor.FromRgb(gray);
        Assert.Equal(0f, hsv.Saturation, Tolerance);
        Assert.Equal(0.5f, hsv.Value, Tolerance);
    }
}
