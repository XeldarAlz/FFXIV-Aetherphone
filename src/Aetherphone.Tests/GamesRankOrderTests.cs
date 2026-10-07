using Aetherphone.Apps.Games;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Xunit;

namespace Aetherphone.Tests;

public sealed class GamesRankOrderTests
{
    [Fact]
    public void RanksSortBestFirstAndKeepTiesInArrivalOrder()
    {
        var values = new[] { 12, 3, 48, 3, 1 };
        var order = new int[values.Length];

        GamesApp.SortByRank(order, values, values.Length);

        Assert.Equal(new[] { 4, 1, 3, 0, 2 }, order);
    }

    [Fact]
    public void SortingOnlyTouchesTheCountedSlots()
    {
        var values = new[] { 9, 2, 1 };
        var order = new[] { -1, -1, 7 };

        GamesApp.SortByRank(order, values, 2);

        Assert.Equal(new[] { 1, 0, 7 }, order);
    }

    [Fact]
    public void AnEmptyListLeavesTheOrderAlone()
    {
        var order = new[] { 5 };

        GamesApp.SortByRank(order, new[] { 1 }, 0);

        Assert.Equal(new[] { 5 }, order);
    }

    [Fact]
    public void ARankCardNamesItsBoardAfterTheGamesOwnMode()
    {
        var spec = new GameSpec("siege", new LocString("test.siege", "Garden Siege"), GameGenre.Strategy,
            kind: ScoreKind.Level,
            modes: new[] { new LocString("test.campaign", "Campaign"), new LocString("test.endless", "Endless") },
            modeStatIds: new[] { "siege", "siege.endless" });

        Assert.Equal("Endless", GamesApp.RankModeName(spec, "siege.endless"));
        Assert.Equal(string.Empty, GamesApp.RankModeName(spec, "siege"));
    }

    [Fact]
    public void ARankCardForASpecWithoutModesFallsBackToTheCatalogName()
    {
        var spec = new GameSpec("sudoku", new LocString("test.sudoku", "Sudoku"), GameGenre.Brain,
            kind: ScoreKind.Time);

        Assert.Equal("Hard", GamesApp.RankModeName(spec, "sudoku.hard"));
    }
}
