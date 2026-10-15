using Aetherphone.Apps.Activity;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ActivityLayoutTests
{
    private const float BaseFontUnits = 16f;
    private const float LineSpacingUnits = 4f;
    private const float FootnoteScale = 0.80f;
    private const float HeadlineScale = 1.00f;
    private const float Title3Scale = 1.20f;
    private const float Title2Scale = 1.32f;
    private const float GlyphTextGap = 8f;
    private const float StreakTileDesign = 100f;
    private const float TrendTileDesign = 88f;
    private const float HeroDesign = 172f;
    private const float StreakGlyph = 30f;
    private const float TrendArrow = 28f;
    private const int RingRows = 3;

    public static TheoryData<float, float> ZoomsAndScales => new()
    {
        { 0.7f, 1f },
        { 1f, 1f },
        { 1.5f, 1f },
        { 0.7f, 1.4f },
        { 1f, 0.8f },
        { 1.5f, 1.25f },
    };

    private static float LineHeight(float styleScale, float zoom, float scale) =>
        (BaseFontUnits * styleScale * zoom + LineSpacingUnits) * scale;

    [Theory]
    [MemberData(nameof(ZoomsAndScales))]
    public void AStreakTileKeepsItsGlyphGapLabelAndValueInsideThePadding(float zoom, float scale)
    {
        var value = LineHeight(Title3Scale, zoom, scale);
        for (var lines = 1; lines <= ActivityArt.MaxTileLabelLines; lines++)
        {
            var label = LineHeight(FootnoteScale, zoom, scale) * lines;
            var height = ActivityArt.TileHeight(StreakTileDesign, StreakGlyph, label, value, scale);
            var pad = ActivityArt.CardPad * scale;
            var labelTop = height - pad - value - label;

            Assert.True(height >= StreakTileDesign * scale - 0.01f);
            Assert.True(labelTop >= pad + StreakGlyph * scale + GlyphTextGap * scale - 0.01f);
        }
    }

    [Theory]
    [MemberData(nameof(ZoomsAndScales))]
    public void ATrendTileKeepsItsArrowGapLabelAndValueInsideThePadding(float zoom, float scale)
    {
        var value = LineHeight(HeadlineScale, zoom, scale);
        for (var lines = 1; lines <= ActivityArt.MaxTileLabelLines; lines++)
        {
            var label = LineHeight(FootnoteScale, zoom, scale) * lines;
            var height = ActivityArt.TileHeight(TrendTileDesign, TrendArrow, label, value, scale);
            var pad = ActivityArt.CardPad * scale;
            var labelTop = height - pad - value - label;

            Assert.True(height >= TrendTileDesign * scale - 0.01f);
            Assert.True(labelTop >= pad + TrendArrow * scale + GlyphTextGap * scale - 0.01f);
        }
    }

    [Theory]
    [MemberData(nameof(ZoomsAndScales))]
    public void TheHeroHoldsThreeLabelAndValueRowsInsideThePadding(float zoom, float scale)
    {
        var row = LineHeight(FootnoteScale, zoom, scale) + LineHeight(Title2Scale, zoom, scale);

        var height = ActivityArt.HeroCardHeight(HeroDesign, row, scale);

        Assert.True(height >= HeroDesign * scale - 0.01f);
        Assert.True(height - ActivityArt.CardPad * 2f * scale >= row * RingRows - 0.01f);
    }

    [Fact]
    public void TheDesignHeightsHoldUntilTheTextOutgrowsThem()
    {
        var smallRow = LineHeight(FootnoteScale, 0.7f, 1f) + LineHeight(Title2Scale, 0.7f, 1f);
        var largeRow = LineHeight(FootnoteScale, 1.5f, 1f) + LineHeight(Title2Scale, 1.5f, 1f);

        Assert.Equal(HeroDesign, ActivityArt.HeroCardHeight(HeroDesign, smallRow, 1f), 3);
        Assert.Equal(ActivityArt.CardPad * 2f + largeRow * RingRows,
            ActivityArt.HeroCardHeight(HeroDesign, largeRow, 1f), 3);
        Assert.Equal(110f, ActivityArt.TileHeight(StreakTileDesign, StreakGlyph, LineHeight(FootnoteScale, 1f, 1f),
            LineHeight(Title3Scale, 1f, 1f), 1f), 3);
        Assert.Equal(TrendTileDesign, ActivityArt.TileHeight(TrendTileDesign, TrendArrow, 10f, 10f, 1f), 3);
    }

    [Fact]
    public void ATileLabelReservesAtLeastOneAndAtMostTwoLines()
    {
        Assert.Equal(1, ActivityArt.CapTileLabelLines(0));
        Assert.Equal(1, ActivityArt.CapTileLabelLines(1));
        Assert.Equal(2, ActivityArt.CapTileLabelLines(2));
        Assert.Equal(2, ActivityArt.CapTileLabelLines(5));
    }
}
