using System.Numerics;
using Aetherphone.Apps.Casino.Strip;
using Aetherphone.Core;
using Xunit;

namespace Aetherphone.Tests;

public sealed class FloorPosterTests
{
    private const float PhoneContent = 328f;
    private const float GridGap = 8f;
    private const int CardColumns = 3;
    private const int QuickColumns = 4;
    private const float HeadlineHeight = 22f;
    private const float SubheadlineHeight = 19.4f;
    private const float FootnoteHeight = 18f;

    [Fact]
    public void ThreeColumnsGiveAboutAHundredUnitsAndFourGiveAboutSeventyFive()
    {
        Assert.Equal(104f, StripGrid.CellWidth(PhoneContent, CardColumns, GridGap), 3);
        Assert.Equal(76f, StripGrid.CellWidth(PhoneContent, QuickColumns, GridGap), 3);
        Assert.Equal(0f, StripGrid.CellWidth(PhoneContent, 0, GridGap));
        Assert.Equal(0f, StripGrid.CellWidth(10f, QuickColumns, GridGap));
    }

    [Fact]
    public void GridsWrapIntoRowsAndAddTheirGaps()
    {
        Assert.Equal(0, StripGrid.Rows(0, 3));
        Assert.Equal(1, StripGrid.Rows(3, 3));
        Assert.Equal(2, StripGrid.Rows(4, 3));
        Assert.Equal(2, StripGrid.Rows(8, 4));
        Assert.Equal(0, StripGrid.Rows(5, 0));
        Assert.Equal(0f, StripGrid.Height(0, 4, 122f, 8f));
        Assert.Equal(252f, StripGrid.Height(8, 4, 122f, 8f));
        Assert.Equal(new Vector2(94f, 150f), StripGrid.CellOrigin(new Vector2(10f, 20f), 5, 4, 76f, 122f, 8f));
        Assert.Equal(new Vector2(10f, 20f), StripGrid.CellOrigin(new Vector2(10f, 20f), 0, 3, 104f, 176f, 8f));
    }

    [Theory]
    [InlineData(0.75f)]
    [InlineData(1f)]
    [InlineData(1.5f)]
    public void GridPostersStayCloseToTheDesignedSize(float scale)
    {
        var block = PosterTile.TextBlock(PosterTile.Grid, scale, HeadlineHeight * scale, FootnoteHeight * scale);
        Assert.InRange(PosterTile.TileHeight(PosterTile.Grid, block, scale) / scale, 160f, 195f);
        var quick = PosterTile.TextBlock(PosterTile.Quick, scale, SubheadlineHeight * scale, FootnoteHeight * scale);
        Assert.InRange(PosterTile.TileHeight(PosterTile.Quick, quick, scale) / scale, 110f, 135f);
    }

    [Fact]
    public void ACompactPosterTradesADetailLineForASecondTitleLine()
    {
        var grid = PosterTile.Grid;
        var room = PosterTile.TextBlock(grid, 1f, HeadlineHeight, FootnoteHeight) - grid.TextTop - grid.TextBottom;
        Assert.True(HeadlineHeight + FootnoteHeight * 2f <= room + 0.01f);
        Assert.True(HeadlineHeight * 2f + FootnoteHeight <= room + 0.01f);
        var quick = PosterTile.Quick;
        var quickRoom = PosterTile.TextBlock(quick, 1f, SubheadlineHeight, FootnoteHeight) - quick.TextBottom;
        Assert.True(SubheadlineHeight * 2f <= quickRoom + 0.01f);
        Assert.Equal(PosterTile.TitleLines + PosterTile.DetailLines,
            PosterTile.Rail.TitleSlots + PosterTile.Rail.DetailSlots);
    }

    [Theory]
    [InlineData(0.75f)]
    [InlineData(1f)]
    [InlineData(1.5f)]
    public void GridSignsClearTheCornerCapsulesAndTheGlyphSitsBetweenSignAndText(float scale)
    {
        AssertPosterFits(PosterTile.Grid, CardColumns, HeadlineHeight, scale);
        AssertPosterFits(PosterTile.GridBare, CardColumns, HeadlineHeight, scale);
        AssertPosterFits(PosterTile.Quick, QuickColumns, SubheadlineHeight, scale);
        Assert.Equal(PosterTile.GridBare.SignTop * scale, PosterTile.SignBandTop(PosterTile.GridBare, scale), 3);
    }

    [Fact]
    public void TheRailLayoutKeepsTheShelfPosterMetrics()
    {
        Assert.Equal(PosterTile.Pad * 0.6f + PosterTile.CrowdHeight + PosterTile.CapsuleGap, PosterTile.SignBandTop(1f),
            3);
        Assert.Equal(PosterTile.Pad * 2f + HeadlineHeight * 2f + FootnoteHeight * 2f,
            PosterTile.TextBlock(1f, HeadlineHeight, FootnoteHeight), 3);
        Assert.False(PosterTile.Rail.Centered);
        Assert.True(PosterTile.Quick.Centered);
    }

    [Fact]
    public void ATileLabelNeverSplitsAWordInHalf()
    {
        Assert.True(TileLabel.WholeWords("Golden Bird Deluxe", new[] { "Golden Bird", "Deluxe" }));
        Assert.False(TileLabel.WholeWords("Ruhmeshalle", new[] { "Ruhmesh", "alle" }));
        Assert.True(TileLabel.WholeWords("Больше-меньше", new[] { "Больше-", "меньше" }));
        Assert.True(TileLabel.WholeWords("名人堂", new[] { "名人", "堂" }));
        Assert.True(TileLabel.WholeWords("Plinko", new[] { "Plinko" }));
        Assert.False(TileLabel.Keeps("A B C", new[] { "A", "B", "C" }, 2));
        Assert.True(TileLabel.Keeps("A B", new[] { "A", "B" }, 2));
    }

    private static void AssertPosterFits(PosterLayout layout, int columns, float titleHeight, float scale)
    {
        var width = StripGrid.CellWidth(PhoneContent * scale, columns, GridGap * scale);
        var block = PosterTile.TextBlock(layout, scale, titleHeight * scale, FootnoteHeight * scale);
        var tile = new Rect(Vector2.Zero, new Vector2(width, PosterTile.TileHeight(layout, block, scale)));
        var signTop = PosterTile.SignBandTop(layout, scale);
        var signBottom = signTop + layout.SignHeight * scale;
        if (layout.CapsuleBand)
        {
            Assert.True(signTop >= (layout.CapsuleInset + layout.CapsuleHeight) * scale);
        }

        var art = PosterTile.ArtCenter(tile, layout, block, scale);
        var glyphHalf = width * layout.GlyphFraction * 0.9f;
        Assert.True(art.Y - glyphHalf >= signBottom - 0.01f);
        Assert.True(art.Y + glyphHalf <= tile.Max.Y - block + 0.01f);
        Assert.True(PosterTile.ArtBelowSign(tile, layout, block, scale).Height > 0f);
    }
}
