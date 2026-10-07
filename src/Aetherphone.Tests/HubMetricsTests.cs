using Aetherphone.Apps.Games.Hub;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HubMetricsTests
{
    [Theory]
    [InlineData(328f, 4)]
    [InlineData(300f, 3)]
    [InlineData(440f, 5)]
    [InlineData(60f, 1)]
    public void TheLibraryGridFitsAsManyMinimumTilesAsTheWidthAllows(float width, int expected)
    {
        Assert.Equal(expected, HubMetrics.GridColumns(width, HubMetrics.GridMinTile, HubMetrics.GridGapX));
    }

    [Fact]
    public void TheStandardPhoneGridHasFourTilesOfSeventyOneAndAHalf()
    {
        var columns = HubMetrics.GridColumns(328f, HubMetrics.GridMinTile, HubMetrics.GridGapX);

        Assert.Equal(71.5f, HubMetrics.GridTile(328f, columns, HubMetrics.GridGapX), 3);
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    public void TheGridIsTheSameAtEveryScale(float scale)
    {
        var columns = HubMetrics.GridColumns(328f * scale, HubMetrics.GridMinTile * scale,
            HubMetrics.GridGapX * scale);
        var tile = HubMetrics.GridTile(328f * scale, columns, HubMetrics.GridGapX * scale);

        Assert.Equal(4, columns);
        Assert.Equal(71.5f * scale, tile, 3);
        Assert.Equal(328f * scale, columns * tile + (columns - 1) * HubMetrics.GridGapX * scale, 2);
    }

    [Fact]
    public void ASingleColumnTakesTheWholeWidth()
    {
        Assert.Equal(60f, HubMetrics.GridTile(60f, 1, HubMetrics.GridGapX));
    }
}
