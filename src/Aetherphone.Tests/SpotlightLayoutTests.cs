using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Shell.Spotlight;
using Xunit;

namespace Aetherphone.Tests;

public sealed class SpotlightLayoutTests
{
    private const int Precision = 3;

    private static SpotlightResult Result(SpotlightKind kind) =>
        new(kind, "title", string.Empty, string.Empty, 0, Guid.Empty, 0, 0);

    private static SpotlightResult[] TwoSections() =>
        new[] { Result(SpotlightKind.App), Result(SpotlightKind.App), Result(SpotlightKind.Contact) };

    [Fact]
    public void Measure_Adds_One_Header_Per_Section()
    {
        var layout = new SpotlightLayout(2f);

        var expected = 2f * SpotlightLayout.SectionHeaderUnits * 2f + 3f * SpotlightLayout.RowHeightUnits * 2f;
        Assert.Equal(expected, layout.Measure(TwoSections()), Precision);
    }

    [Fact]
    public void Measure_Is_Zero_For_No_Results()
    {
        var layout = new SpotlightLayout(1f);

        Assert.Equal(0f, layout.Measure(Array.Empty<SpotlightResult>()));
    }

    [Theory]
    [InlineData(0, 48f)]
    [InlineData(1, 160f)]
    [InlineData(2, 320f)]
    public void RowTop_Counts_Headers_And_Rows_Before_The_Row(int index, float expected)
    {
        var layout = new SpotlightLayout(2f);

        Assert.Equal(expected, layout.RowTop(TwoSections(), index), Precision);
    }

    [Theory]
    [InlineData(100f, 156f, 200f, 300f, 100f)]
    [InlineData(500f, 556f, 0f, 300f, 256f)]
    [InlineData(100f, 156f, 50f, 300f, 50f)]
    public void ScrollToReveal_Keeps_The_Row_Inside_The_View(float rowTop, float rowBottom, float scrollY,
        float viewHeight, float expected)
    {
        Assert.Equal(expected, SpotlightLayout.ScrollToReveal(rowTop, rowBottom, scrollY, viewHeight), Precision);
    }

    [Fact]
    public void FieldRect_Travels_From_The_Pill_To_Its_Rest()
    {
        var origin = new Rect(new Vector2(0f, 500f), new Vector2(96f, 526f));
        var rest = new Rect(new Vector2(16f, 40f), new Vector2(344f, 84f));

        Assert.Equal(origin, SpotlightLayout.FieldRect(origin, rest, 0f));
        Assert.Equal(rest, SpotlightLayout.FieldRect(origin, rest, 1f));
        var midway = SpotlightLayout.FieldRect(origin, rest, 0.5f);
        Assert.Equal(8f, midway.Min.X, Precision);
        Assert.Equal(270f, midway.Min.Y, Precision);
        Assert.Equal(220f, midway.Max.X, Precision);
        Assert.Equal(305f, midway.Max.Y, Precision);
    }

    [Fact]
    public void FieldRadius_Lerps_From_Half_The_Pill_Height()
    {
        var origin = new Rect(new Vector2(0f, 500f), new Vector2(96f, 526f));

        Assert.Equal(13f, SpotlightLayout.FieldRadius(origin, 22f, 0f), Precision);
        Assert.Equal(17.5f, SpotlightLayout.FieldRadius(origin, 22f, 0.5f), Precision);
        Assert.Equal(22f, SpotlightLayout.FieldRadius(origin, 22f, 1f), Precision);
    }

    [Fact]
    public void RestRect_Sits_At_The_Content_Inset()
    {
        var content = new Rect(new Vector2(16f, 40f), new Vector2(344f, 700f));

        var rest = SpotlightLayout.RestRect(content, 1f);

        Assert.Equal(content.Min, rest.Min);
        Assert.Equal(344f, rest.Max.X);
        Assert.Equal(40f + SpotlightLayout.FieldHeightUnits, rest.Max.Y);
    }

    [Fact]
    public void TileRect_Is_A_Square_At_The_Row_Inset()
    {
        var layout = new SpotlightLayout(1f);
        var row = new Rect(new Vector2(0f, 0f), new Vector2(300f, 56f));

        var tile = layout.TileRect(row);

        Assert.Equal(10f, tile.Min.X);
        Assert.Equal(10f, tile.Min.Y);
        Assert.Equal(46f, tile.Max.X);
        Assert.Equal(46f, tile.Max.Y);
        Assert.Equal(tile.Width, tile.Height);
        Assert.Equal(58f, layout.TextLeft(row));
    }

    [Fact]
    public void PillRect_Hugs_Its_Label_And_The_Right_Edge()
    {
        var layout = new SpotlightLayout(1f);
        var row = new Rect(new Vector2(0f, 0f), new Vector2(300f, 56f));

        var pill = layout.PillRect(row, 290f, 40f);

        Assert.Equal(226f, pill.Min.X);
        Assert.Equal(290f, pill.Max.X);
        Assert.Equal(14f, pill.Min.Y);
        Assert.Equal(42f, pill.Max.Y);
        Assert.Equal(SpotlightLayout.PillHeightUnits, pill.Height);
    }

    [Fact]
    public void Recents_Row_Splits_Into_Six_Cells()
    {
        var layout = new SpotlightLayout(1f);

        Assert.Equal(50f, layout.RecentCellWidth(300f));
        Assert.Equal(36f, layout.RecentTileSize(300f));
        Assert.Equal(98f, layout.RecentsPanelHeight(300f));
    }

    [Fact]
    public void Recent_Tile_Caps_At_Its_Rest_Size_On_Wide_Panels()
    {
        var layout = new SpotlightLayout(1f);

        Assert.Equal(SpotlightLayout.RecentTileUnits, layout.RecentTileSize(600f));
    }
}
