using System.Numerics;
using Aetherphone.Apps.Casino;
using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoLightsTests
{
    private static readonly Rect Square = new(new Vector2(10f, 20f), new Vector2(110f, 120f));

    [Fact]
    public void SharpCornersMakeThePerimeterTheFourSides()
    {
        Assert.Equal(400f, CasinoLights.Perimeter(Square, 0f), 3);
    }

    [Fact]
    public void RoundCornersTradeTheirLegsForQuarterArcs()
    {
        var expected = (100f - 20f) * 4f + MathF.PI * 2f * 10f;
        Assert.Equal(expected, CasinoLights.Perimeter(Square, 10f), 3);
    }

    [Fact]
    public void TheWalkStartsTopLeftAndRunsClockwise()
    {
        AssertNear(new Vector2(10f, 20f), CasinoLights.PointOnPerimeter(Square, 0f, 0f));
        AssertNear(new Vector2(110f, 20f), CasinoLights.PointOnPerimeter(Square, 0f, 100f));
        AssertNear(new Vector2(110f, 120f), CasinoLights.PointOnPerimeter(Square, 0f, 200f));
        AssertNear(new Vector2(10f, 120f), CasinoLights.PointOnPerimeter(Square, 0f, 300f));
        AssertNear(new Vector2(10f, 70f), CasinoLights.PointOnPerimeter(Square, 0f, 350f));
    }

    [Fact]
    public void TheWalkWrapsPastOneLap()
    {
        AssertNear(CasinoLights.PointOnPerimeter(Square, 12f, 37f),
            CasinoLights.PointOnPerimeter(Square, 12f, 37f + CasinoLights.Perimeter(Square, 12f)));
    }

    [Fact]
    public void BulbsSitOnTheStandardPitch()
    {
        Assert.Equal(28, CasinoLights.BulbCount(Square, 0f, CasinoLights.BulbPitch));
        Assert.Equal(0, CasinoLights.BulbCount(Square, 0f, 0f));
    }

    [Fact]
    public void TheChaseDarkensEveryThirdBulbAndStepsSixTimesASecond()
    {
        var dark = 0;
        for (var bulb = 0; bulb < 30; bulb++)
        {
            if (!CasinoLights.BulbLit(bulb, 0f))
            {
                dark++;
            }
        }

        Assert.Equal(10, dark);
        Assert.False(CasinoLights.BulbLit(0, 0f));
        Assert.True(CasinoLights.BulbLit(0, 1.5f / CasinoLights.ChaseBulbsPerSecond));
        Assert.False(CasinoLights.BulbLit(0, 3.5f / CasinoLights.ChaseBulbsPerSecond));
    }

    [Fact]
    public void EverySignIsSpelledWithDrawnGlyphs()
    {
        foreach (var sign in Enum.GetValues<CasinoSign>())
        {
            var text = CasinoSigns.Text(sign);
            Assert.False(string.IsNullOrEmpty(text));
            for (var index = 0; index < text.Length; index++)
            {
                Assert.True(CasinoSigns.HasGlyph(text[index]), $"{sign} misses '{text[index]}'");
            }

            Assert.True(CasinoSigns.Measure(sign, 12f) > 0f);
        }
    }

    [Fact]
    public void SignsShrinkToFitTheirBox()
    {
        var height = CasinoSigns.HeightToFit(CasinoSign.FreeSpin, 60f, 40f);
        Assert.True(CasinoSigns.Measure(CasinoSign.FreeSpin, height) <= 60.01f);
        Assert.Equal(40f, CasinoSigns.HeightToFit(CasinoSign.Bar, 1000f, 40f));
    }

    private static void AssertNear(Vector2 expected, Vector2 actual)
    {
        Assert.Equal(expected.X, actual.X, 2);
        Assert.Equal(expected.Y, actual.Y, 2);
    }
}
