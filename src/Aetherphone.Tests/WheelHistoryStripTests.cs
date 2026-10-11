using System.Numerics;
using Aetherphone.Apps.Casino.Cabinets;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Xunit;

namespace Aetherphone.Tests;

public sealed class WheelHistoryStripTests
{
    private const int Full = 12;
    private const string TopMultiplier = "23";

    [Theory]
    [InlineData(0.75f)]
    [InlineData(1f)]
    [InlineData(1.5f)]
    public void TheStripIsTheTopRowOfSafe(float scale)
    {
        var stage = WheelTextMeasure.Stage(scale, WheelCabinet.DeckHeight);
        var strip = WheelHistoryLayout.StripOf(stage.Safe, scale);
        Assert.True(WheelTextMeasure.Inside(strip, stage.Safe));
        Assert.Equal(stage.Safe.Min.Y, strip.Min.Y);
        Assert.Equal(WheelHistoryLayout.Height * scale, strip.Height, 3);
    }

    [Theory]
    [InlineData(0.75f)]
    [InlineData(1f)]
    [InlineData(1.5f)]
    public void EveryLanguageKeepsItsCaptionWholeAndShowsAtLeastFiveSpins(float scale)
    {
        var stage = WheelTextMeasure.Stage(scale, WheelCabinet.DeckHeight);
        var strip = WheelHistoryLayout.StripOf(stage.Safe, scale);
        var captions = WheelTextMeasure.Translations(L.Casino.WheelLastSpins);
        var labels = WheelTextMeasure.Translations(L.Casino.WheelMultiplier);
        for (var language = 0; language < captions.Length; language++)
        {
            var caption = WheelTextMeasure.Width(captions[language], WheelHistoryStrip.CaptionStyle, scale);
            var label = string.Format(labels[language], TopMultiplier);
            var layout = WheelHistoryLayout.Compute(strip, caption,
                WheelTextMeasure.Width(label, WheelHistoryStrip.ChipStyle, scale),
                WheelTextMeasure.Width(label, WheelHistoryStrip.LatestStyle, scale), Full, scale);
            Assert.True(layout.Caption.Width >= caption - 0.01f, captions[language]);
            Assert.True(layout.Visible >= 5, captions[language]);
            AssertWellFormed(layout, scale);
        }
    }

    [Theory]
    [InlineData(160f)]
    [InlineData(240f)]
    [InlineData(336f)]
    [InlineData(420f)]
    [InlineData(600f)]
    public void TheStripFitsEveryWidthWithoutOverlap(float width)
    {
        var strip = new Rect(new Vector2(12f, 104f), new Vector2(12f + width, 104f + WheelHistoryLayout.Height));
        var caption = WheelTextMeasure.Widest(L.Casino.WheelLastSpins, WheelHistoryStrip.CaptionStyle, 1f);
        var layout = Compute(strip, caption, 1f, Full);
        AssertWellFormed(layout, 1f);
        Assert.True(layout.Caption.Width <= width * WheelHistoryLayout.CaptionShare + 0.01f);
    }

    [Fact]
    public void TheNewestSpinLeadsLargerAndOlderSpinsFade()
    {
        var strip = new Rect(new Vector2(12f, 104f), new Vector2(348f, 104f + WheelHistoryLayout.Height));
        var layout = Compute(strip, 60f, 1f, Full);
        var latest = layout.Chip(0);
        var next = layout.Chip(1);
        Assert.True(latest.Width > next.Width);
        Assert.True(latest.Height > next.Height);
        Assert.True(latest.Max.X + WheelHistoryLayout.LatestGap - 0.01f <= next.Min.X);
        Assert.Equal(1f, layout.Fade(0));
        for (var index = 1; index < layout.Visible; index++)
        {
            Assert.True(layout.Fade(index) <= layout.Fade(index - 1));
            Assert.True(layout.Fade(index) >= WheelHistoryLayout.OldestFade - 0.0001f);
        }

        Assert.Equal(WheelHistoryLayout.OldestFade, layout.Fade(layout.Visible - 1), 4);
    }

    [Fact]
    public void AShortHistoryShowsEverySpinItHas()
    {
        var strip = new Rect(new Vector2(12f, 104f), new Vector2(348f, 104f + WheelHistoryLayout.Height));
        Assert.Equal(3, Compute(strip, 60f, 1f, 3).Visible);
        Assert.Equal(0, Compute(strip, 60f, 1f, 0).Visible);
    }

    [Fact]
    public void AStripTooNarrowForTheNewestSpinShowsNoChips()
    {
        var strip = new Rect(new Vector2(12f, 104f), new Vector2(72f, 104f + WheelHistoryLayout.Height));
        var layout = Compute(strip, 60f, 1f, Full);
        Assert.Equal(0, layout.Visible);
    }

    private static WheelHistoryLayout Compute(Rect strip, float caption, float scale, int count)
    {
        var chip = WheelTextMeasure.Widest(L.Casino.WheelMultiplier, TopMultiplier, WheelHistoryStrip.ChipStyle,
            scale);
        var latest = WheelTextMeasure.Widest(L.Casino.WheelMultiplier, TopMultiplier, WheelHistoryStrip.LatestStyle,
            scale);
        return WheelHistoryLayout.Compute(strip, caption, chip, latest, count, scale);
    }

    private static void AssertWellFormed(in WheelHistoryLayout layout, float scale)
    {
        Assert.True(WheelTextMeasure.Inside(layout.Caption, layout.Strip));
        for (var index = 0; index < layout.Visible; index++)
        {
            var chip = layout.Chip(index);
            Assert.True(WheelTextMeasure.Inside(chip, layout.Strip));
            Assert.True(chip.Min.X >= layout.Caption.Max.X + WheelHistoryLayout.CaptionGap * scale - 0.01f);
            if (index > 0)
            {
                Assert.True(WheelTextMeasure.Disjoint(chip, layout.Chip(index - 1)));
                Assert.True(chip.Min.X > layout.Chip(index - 1).Min.X);
            }
        }
    }
}
