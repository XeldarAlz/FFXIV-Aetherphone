using System.Numerics;
using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoHostFlowLayoutTests
{
    private const float PhoneWidth = 330f;
    private const float HeadlineUnits = 22f;
    private const float FootnoteUnits = 18f;
    private const float TitleUnits = 30f;

    public static TheoryData<float> Scales => new() { 0.75f, 0.9f, 1f, 1.25f, 1.5f };

    [Theory]
    [MemberData(nameof(Scales))]
    public void GameTilesAreLargeTouchTargetsThatNeverTouch(float scale)
    {
        var width = PhoneWidth * scale;
        var tileWidth = HostFlowLayout.TileWidth(width, scale);
        var tileHeight = HostFlowLayout.TileHeight(scale, HeadlineUnits * scale, FootnoteUnits * scale * 3f);
        Assert.True(tileWidth >= HostFlowLayout.Touch * scale);
        Assert.True(tileHeight >= HostFlowLayout.TileArt * scale + HeadlineUnits * scale);
        for (var index = 1; index < HostGames.All.Length; index++)
        {
            var previous = HostFlowLayout.Tile(0f, 0f, index - 1, tileWidth, tileHeight, scale);
            var tile = HostFlowLayout.Tile(0f, 0f, index, tileWidth, tileHeight, scale);
            Assert.True(tile.Min.X >= previous.Max.X);
        }

        var visible = (width + HostFlowLayout.TileGap * scale) / (tileWidth + HostFlowLayout.TileGap * scale);
        Assert.InRange(visible, 1.8f, 2.6f);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void ChoiceCardsFitTheirTextBesideTheIconAndCheck(float scale)
    {
        var width = PhoneWidth * scale;
        var lineBlock = FootnoteUnits * scale * 2f;
        var height = HostFlowLayout.ChoiceHeight(scale, HeadlineUnits * scale, lineBlock);
        var card = new Rect(Vector2.Zero, new Vector2(width, height));
        var icon = HostFlowLayout.ChoiceIconRect(card, scale);
        var textLeft = HostFlowLayout.ChoiceTextLeft(card.Min.X, scale);
        var textRight = textLeft + HostFlowLayout.ChoiceTextWidth(width, scale);
        var check = HostFlowLayout.ChoiceCheck(card, scale);

        Assert.True(height >= HostFlowLayout.Touch * scale);
        Assert.True(height >= HeadlineUnits * scale + lineBlock);
        Assert.True(icon.Min.Y >= card.Min.Y && icon.Max.Y <= card.Max.Y);
        Assert.True(textLeft > icon.Max.X);
        Assert.True(textRight < check.X - HostFlowLayout.CheckSize * scale * 0.5f);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void SeatTargetsAreTouchSizedAndNeverOverlap(float scale)
    {
        CheckSeats(PhoneWidth * scale - 28f * scale, 6, scale);
        CheckSeats(PhoneWidth * scale - 28f * scale, 9, scale);
        CheckSeats(260f, 9, scale);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void JoinCardsSplitTheRowWithoutOverlap(float scale)
    {
        var width = PhoneWidth * scale;
        var height = HostFlowLayout.JoinHeight(scale, FootnoteUnits * scale * 2f);
        Assert.True(height >= HostFlowLayout.Touch * scale);
        for (var index = 1; index < 3; index++)
        {
            var previous = HostFlowLayout.JoinColumn(0f, 0f, width, index - 1, 3, height, scale);
            var column = HostFlowLayout.JoinColumn(0f, 0f, width, index, 3, height, scale);
            Assert.True(column.Min.X > previous.Max.X);
            Assert.True(column.Width >= HostFlowLayout.Touch * scale);
        }

        Assert.Equal(width, HostFlowLayout.JoinColumn(0f, 0f, width, 2, 3, height, scale).Max.X, 2);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void TheLadderRowSitsBelowItsValueAndHoldsAThumb(float scale)
    {
        var label = FootnoteUnits * scale;
        var value = TitleUnits * scale;
        var row = HostFlowLayout.LadderRow(0f, 0f, PhoneWidth * scale, scale, label, value);
        Assert.True(row.Min.Y >= label + value);
        Assert.True(row.Height >= HostFlowLayout.Touch * scale);
        Assert.True(row.Height >= LadderSlider.ThumbRadius * 2f * scale);
        Assert.Equal(HostFlowLayout.LadderHeight(scale, label, value), row.Max.Y, 2);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void TheStickyFooterKeepsTheSummaryAboveTheButton(float scale)
    {
        var summary = FootnoteUnits * scale * 2f;
        var height = HostFlowLayout.FooterHeight(scale, summary);
        var footer = new Rect(new Vector2(0f, 500f), new Vector2(PhoneWidth * scale, 500f + height));
        var button = HostFlowLayout.FooterButton(footer, scale);
        Assert.True(button.Height >= HostFlowLayout.Touch * scale - 0.01f);
        Assert.True(button.Min.Y >= footer.Min.Y + HostFlowLayout.FooterPad * scale + summary - 0.01f);
        Assert.True(button.Max.Y <= footer.Max.Y);
    }

    [Fact]
    public void TheLadderMapsEveryRungToItsOwnPosition()
    {
        var span = new LadderSpan(3, 9);
        for (var index = span.First; index <= span.Last; index++)
        {
            var x = LadderSlider.XOf(index, span, 10f, 300f);
            Assert.Equal(index, LadderSlider.IndexAt(x, span, 10f, 300f));
        }

        Assert.Equal(span.First, LadderSlider.IndexAt(-50f, span, 10f, 300f));
        Assert.Equal(span.Last, LadderSlider.IndexAt(900f, span, 10f, 300f));
    }

    private static void CheckSeats(float width, int seats, float scale)
    {
        var touch = HostFlowLayout.Touch * scale;
        var block = HostFlowLayout.SeatBlockHeight(width, seats, scale);
        for (var index = 0; index < seats; index++)
        {
            var target = HostFlowLayout.SeatTarget(0f, 0f, width, index, seats, scale);
            Assert.True(target.Width >= touch - 0.01f);
            Assert.True(target.Max.X <= width + 0.01f);
            Assert.True(target.Max.Y <= block + 0.01f);
            for (var other = 0; other < index; other++)
            {
                var earlier = HostFlowLayout.SeatTarget(0f, 0f, width, other, seats, scale);
                var apart = target.Min.X >= earlier.Max.X - 0.01f || target.Min.Y >= earlier.Max.Y - 0.01f;
                Assert.True(apart, $"seat {index} overlaps seat {other} at {scale}");
            }
        }
    }
}
