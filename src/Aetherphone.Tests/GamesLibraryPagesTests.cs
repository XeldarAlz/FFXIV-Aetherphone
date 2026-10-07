using Aetherphone.Apps.Games;
using Xunit;

namespace Aetherphone.Tests;

public sealed class GamesLibraryPagesTests
{
    [Theory]
    [InlineData(0, 4, 0)]
    [InlineData(1, 4, 1)]
    [InlineData(4, 4, 1)]
    [InlineData(5, 4, 2)]
    [InlineData(67, 4, 17)]
    [InlineData(3, 0, 0)]
    public void TheRowCountRoundsUpToWholeRows(int itemCount, int columns, int expected)
    {
        Assert.Equal(expected, VisibleRows.Count(itemCount, columns));
    }

    [Fact]
    public void AGridInsideTheClipDrawsEveryRow()
    {
        var rows = VisibleRows.Between(5, 100f, 120f, 0f, 1000f);

        Assert.Equal(0, rows.First);
        Assert.Equal(5, rows.End);
    }

    [Fact]
    public void ScrollingSkipsTheRowsAboveAndBelowTheClip()
    {
        var rows = VisibleRows.Between(17, 0f, 120f, 500f, 900f);

        Assert.Equal(4, rows.First);
        Assert.Equal(8, rows.End);
    }

    [Fact]
    public void ARowCutByTheClipEdgeStillDraws()
    {
        var rows = VisibleRows.Between(10, 0f, 100f, 250f, 301f);

        Assert.Equal(2, rows.First);
        Assert.Equal(4, rows.End);
    }

    [Theory]
    [InlineData(2000f, 3000f)]
    [InlineData(-500f, -100f)]
    public void AGridOutsideTheClipDrawsNothing(float clipTop, float clipBottom)
    {
        var rows = VisibleRows.Between(10, 0f, 100f, clipTop, clipBottom);

        Assert.Equal(rows.First, rows.End);
    }

    [Fact]
    public void AnEmptyGridOrAFlatPitchDrawsNothing()
    {
        Assert.Equal(0, VisibleRows.Between(0, 0f, 100f, 0f, 500f).End);
        Assert.Equal(0, VisibleRows.Between(10, 0f, 0f, 0f, 500f).End);
    }
}
