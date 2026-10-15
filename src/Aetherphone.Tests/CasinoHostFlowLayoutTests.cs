using System.Numerics;
using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoHostFlowLayoutTests
{
    private const float PhoneWidth = 328f;
    private const float HeadlineUnits = 22f;
    private const float FootnoteUnits = 18f;
    private const float TitleUnits = 30f;

    public static TheoryData<float> Scales => new() { 0.75f, 0.9f, 1f, 1.25f, 1.5f };

    [Theory]
    [MemberData(nameof(Scales))]
    public void GridCellsSplitTheRowWithoutOverlap(float scale)
    {
        var width = PhoneWidth * scale;
        var gap = HostFlowLayout.SwitcherGap * scale;
        var height = HostFlowLayout.SwitcherHeight * scale;
        var count = HostGames.All.Length;
        for (var index = 1; index < count; index++)
        {
            var previous = HostFlowLayout.GridCell(0f, 0f, width, index - 1, HostFlowLayout.Columns, height, gap);
            var cell = HostFlowLayout.GridCell(0f, 0f, width, index, HostFlowLayout.Columns, height, gap);
            var apart = cell.Min.X >= previous.Max.X + gap - 0.01f || cell.Min.Y >= previous.Max.Y + gap - 0.01f;
            Assert.True(apart, $"cell {index} touches cell {index - 1} at {scale}");
            Assert.True(cell.Max.X <= width + 0.01f);
        }

        var last = HostFlowLayout.GridCell(0f, 0f, width, count - 1, HostFlowLayout.Columns, height, gap);
        Assert.Equal(width, last.Max.X, 2);
        Assert.Equal(last.Max.Y, HostFlowLayout.GridHeight(count, HostFlowLayout.Columns, height, gap), 2);
        Assert.Equal(height, HostFlowLayout.GridHeight(2, HostFlowLayout.Columns, height, gap), 2);
        Assert.Equal(0f, HostFlowLayout.GridHeight(0, HostFlowLayout.Columns, height, gap));
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void ChoiceAndJoinTilesHoldTheirIconAboveAWrappedLabel(float scale)
    {
        var twoLines = FootnoteUnits * scale * 2f;
        var choice = HostFlowLayout.ChoiceTileHeight(scale, twoLines);
        var join = HostFlowLayout.JoinTileHeight(scale, twoLines);
        Assert.True(choice >= HostFlowLayout.ChoiceHeight * scale);
        Assert.True(choice >= (HostFlowLayout.ChoiceIcon + HostFlowLayout.ChoiceInnerGap) * scale + twoLines);
        Assert.True(join >= HostFlowLayout.Touch * scale);
        Assert.True(join >= (HostFlowLayout.JoinIcon + HostFlowLayout.JoinInnerGap) * scale + twoLines);

        var width = HostFlowLayout.ColumnWidth(PhoneWidth * scale, HostFlowLayout.Columns,
            HostFlowLayout.ChoiceGap * scale);
        var tile = new Rect(Vector2.Zero, new Vector2(width, choice));
        var check = HostFlowLayout.CheckCenter(tile, scale);
        var half = HostFlowLayout.CheckSize * 0.5f * scale;
        Assert.True(check.X + half <= tile.Max.X && check.Y - half >= tile.Min.Y);
        Assert.True(width >= HostFlowLayout.Touch * scale);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void SeatDiscsFitOneRowAndTheirTargetsNeverOverlap(float scale)
    {
        var inner = (PhoneWidth - HostFlowLayout.CardPad * 2f) * scale;
        CheckSeats(inner, CasinoHostingRules.MaxSeats, scale);
        CheckSeats(inner, 9, scale);
        CheckSeats(240f * scale, 9, scale);
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
        var hint = FootnoteUnits * scale;
        Assert.True(HostFlowLayout.LadderBlockHeight(scale, label, value, hint) >= row.Max.Y + hint);
        Assert.Equal(row.Max.Y, HostFlowLayout.LadderBlockHeight(scale, label, value, 0f), 2);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void TheRulesRowKeepsItsTextBetweenTheIconAndTheChevron(float scale)
    {
        var width = PhoneWidth * scale;
        var text = HostFlowLayout.RulesTextWidth(width, scale);
        var used = (HostFlowLayout.CardPad * 2f + HostFlowLayout.RulesIcon + HostFlowLayout.HeaderGap
            + HostFlowLayout.Chevron) * scale + text;
        Assert.True(used <= width + 0.01f);
        Assert.True(HostFlowLayout.RulesRowHeight(scale, HeadlineUnits * scale, 0f)
                    >= (HostFlowLayout.RulesIcon + HostFlowLayout.RulesPad * 2f) * scale - 0.01f);
        Assert.True(HostFlowLayout.HeaderHeight(scale, TitleUnits * scale, FootnoteUnits * scale)
                    >= (HostFlowLayout.HeaderTile + HostFlowLayout.HeaderPad * 2f) * scale - 0.01f);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void RuleRowsLeaveRoomForTheirControls(float scale)
    {
        var inner = (PhoneWidth - HostFlowLayout.CardPad * 2f) * scale;
        var text = HostFlowLayout.ToggleTextWidth(inner, scale);
        Assert.True(text + Metrics.Size.ToggleWidth * scale <= inner);
        Assert.True(HostFlowLayout.ToggleRowHeight(scale, FootnoteUnits * scale) >= HostFlowLayout.Touch * scale);
        var plain = HostFlowLayout.SegmentRowHeight(scale, HeadlineUnits * scale, 0f);
        var hinted = HostFlowLayout.SegmentRowHeight(scale, HeadlineUnits * scale, FootnoteUnits * scale);
        Assert.True(plain >= (HostFlowLayout.SegmentTrack + HostFlowLayout.RowPad * 2f) * scale + HeadlineUnits * scale);
        Assert.True(hinted > plain + FootnoteUnits * scale);
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
        var disc = HostFlowLayout.SeatDiscSize(width, seats, scale);
        Assert.True(disc <= HostFlowLayout.SeatDisc * scale + 0.01f);
        for (var index = 0; index < seats; index++)
        {
            var slot = HostFlowLayout.SeatSlot(0f, 0f, width, index, seats, scale);
            var center = HostFlowLayout.SeatCenter(0f, 0f, width, index, seats, scale);
            Assert.True(slot.Min.X >= -0.01f && slot.Max.X <= width + 0.01f, $"seat {index} leaves the row");
            Assert.True(slot.Height >= HostFlowLayout.Touch * scale - 0.01f);
            Assert.True(center.X - disc * 0.5f >= -0.01f && center.X + disc * 0.5f <= width + 0.01f);
            Assert.True(center.X >= slot.Min.X && center.X <= slot.Max.X);
            if (index == 0)
            {
                continue;
            }

            var previous = HostFlowLayout.SeatSlot(0f, 0f, width, index - 1, seats, scale);
            Assert.True(slot.Min.X >= previous.Max.X - 0.01f, $"seat {index} overlaps seat {index - 1} at {scale}");
        }
    }
}
