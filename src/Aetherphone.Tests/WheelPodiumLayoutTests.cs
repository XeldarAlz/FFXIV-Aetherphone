using Aetherphone.Apps.Casino.Cabinets;
using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class WheelPodiumLayoutTests
{
    private const string LargestCrowd = "120";
    private const string LargestTotal = "12.5M";
    private const string CommonTotal = "1.2M";

    [Theory]
    [InlineData(0.75f)]
    [InlineData(1f)]
    [InlineData(1.5f)]
    public void CardRowsStackTopDownInsideTheCard(float scale)
    {
        var board = Board(scale);
        for (var spot = 0; spot < WheelRules.SpotCount; spot++)
        {
            var card = board.Card(spot);
            var rows = WheelPodiumLayout.Compute(card, scale);
            var ordered = new[] { rows.Header, rows.Stake, rows.Crowd, rows.Total };
            for (var index = 0; index < ordered.Length; index++)
            {
                Assert.True(ordered[index].Height > 0f);
                Assert.True(WheelTextMeasure.Inside(ordered[index], card));
                if (index > 0)
                {
                    Assert.True(ordered[index].Min.Y >= ordered[index - 1].Max.Y - 0.01f);
                }
            }

            Assert.True(rows.DividerY > rows.Stake.Max.Y && rows.DividerY < rows.Crowd.Min.Y);
            Assert.True(rows.Total.Max.Y <= card.Max.Y - WheelPodiumLayout.PadBottom * scale + 0.01f);
        }
    }

    [Theory]
    [InlineData(0.75f)]
    [InlineData(1f)]
    [InlineData(1.5f)]
    public void EveryCardLineFitsWholeInEveryLanguage(float scale)
    {
        var rows = WheelPodiumLayout.Compute(Board(scale).Card(0), scale);
        var multipliers = WheelTextMeasure.Translations(L.Casino.WheelMultiplier);
        for (var language = 0; language < multipliers.Length; language++)
        {
            for (var spot = 0; spot < WheelRules.SpotCount; spot++)
            {
                var label = string.Format(multipliers[language], WheelRules.Multipliers[spot]);
                Assert.True(WheelTextMeasure.Width(label, WheelSpotCard.HeaderStyle, scale) <= rows.Header.Width,
                    label);
            }
        }

        var stake = WheelSpotCard.DiscRadius * 2f * scale + WheelSpotCard.GlyphGap * scale
            + WheelTextMeasure.Width(NumberText.CompactText(WheelRules.MaxStakePerSpot), WheelSpotCard.StakeStyle, scale);
        Assert.True(stake <= rows.Stake.Width);

        var crowd = WheelSpotCard.GlyphBox * scale + WheelSpotCard.GlyphGap * scale
            + WheelTextMeasure.Width(LargestCrowd, WheelSpotCard.TableStyle, scale);
        Assert.True(crowd <= rows.Crowd.Width);

        var line = WheelTextMeasure.EmPixels * WheelSpotCard.TableStyle.Scale * scale;
        var common = CurrencyGlyph.Reserve(line) + WheelTextMeasure.Width(CommonTotal, WheelSpotCard.TableStyle, scale);
        Assert.True(common <= rows.Total.Width);
        Assert.True(WheelTextMeasure.Width(LargestTotal, WheelSpotCard.TableStyle, scale) <= rows.Total.Width);
    }

    [Theory]
    [InlineData(0.75f)]
    [InlineData(1f)]
    [InlineData(1.5f)]
    public void RowsAreTallEnoughForTheirText(float scale)
    {
        var rows = WheelPodiumLayout.Compute(Board(scale).Card(0), scale);
        Assert.True(Line(WheelSpotCard.HeaderStyle, scale) <= rows.Header.Height);
        Assert.True(Line(WheelSpotCard.StakeStyle, scale) <= rows.Stake.Height);
        Assert.True(WheelSpotCard.DiscRadius * 2f * scale <= rows.Stake.Height);
        Assert.True(Line(WheelSpotCard.TableStyle, scale) <= rows.Crowd.Height);
        Assert.True(Line(WheelSpotCard.TableStyle, scale) <= rows.Total.Height);
    }

    private static float Line(in TextStyle style, float scale) =>
        (WheelTextMeasure.EmPixels * style.Scale + WheelTextMeasure.LineSpacing) * scale;

    private static WheelBoardLayout Board(float scale) =>
        WheelBoardLayout.Compute(WheelTextMeasure.Stage(scale, WheelCabinet.DeckHeight).Safe, scale);
}
