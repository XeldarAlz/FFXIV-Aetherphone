using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.WaterSort;
using Xunit;

namespace Aetherphone.Tests;

public sealed class WaterSortBoardTests
{
    private const ulong Seed = 0xC0FFEEUL;
    private const int SeedSweep = 4;
    private const int LevelSweep = 9;

    private static WaterSortBoard Build(int level, ulong seed)
    {
        var board = new WaterSortBoard();
        board.Reset(level, GameRandom.FromSeed(WaterSortBoard.SeedFor(seed, level)));
        return board;
    }

    private static int[] Snapshot(WaterSortBoard board)
    {
        var segments = new int[board.TubeCount * WaterSortBoard.Capacity];
        for (var tube = 0; tube < board.TubeCount; tube++)
        {
            for (var level = 0; level < WaterSortBoard.Capacity; level++)
            {
                segments[tube * WaterSortBoard.Capacity + level] = board.Segment(tube, level);
            }
        }

        return segments;
    }

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = Build(5, Seed);
        var second = Build(5, Seed);

        Assert.Equal(first.TubeCount, second.TubeCount);
        Assert.Equal(Snapshot(first), Snapshot(second));
    }

    [Fact]
    public void TheLevelIsMixedIntoTheSeedAndDifferentSeedsDiffer()
    {
        Assert.NotEqual(WaterSortBoard.SeedFor(Seed, 1), WaterSortBoard.SeedFor(Seed, 2));
        Assert.NotEqual(Snapshot(Build(5, 1)), Snapshot(Build(5, 2)));
    }

    [Fact]
    public void EveryGeneratedLevelIsSolvable()
    {
        for (var level = 1; level <= LevelSweep; level++)
        {
            for (var seed = 1; seed <= SeedSweep; seed++)
            {
                var board = Build(level, (ulong)seed);
                var colorCount = WaterSortBoard.ColorsForLevel(level);
                Assert.Equal(colorCount + 2, board.TubeCount);
                Assert.False(board.IsSolved(), $"level {level} seed {seed} started solved");
                Assert.True(board.IsSolvable(), $"level {level} seed {seed} generated an unsolvable board");

                var tally = new int[colorCount];
                for (var tube = 0; tube < board.TubeCount; tube++)
                {
                    for (var segment = 0; segment < board.Count(tube); segment++)
                    {
                        tally[board.Segment(tube, segment)]++;
                    }
                }

                for (var color = 0; color < colorCount; color++)
                {
                    Assert.Equal(WaterSortBoard.Capacity, tally[color]);
                }
            }
        }
    }

    [Fact]
    public void UndoRestoresThePreviousPour()
    {
        var board = Build(3, Seed);
        var before = Snapshot(board);
        var from = -1;
        var to = -1;
        for (var source = 0; source < board.TubeCount && from < 0; source++)
        {
            for (var target = 0; target < board.TubeCount; target++)
            {
                if (board.CanPour(source, target))
                {
                    from = source;
                    to = target;
                    break;
                }
            }
        }

        Assert.True(from >= 0, "no legal pour on a fresh board");
        Assert.Equal(TubeAction.Selected, board.ClickTube(from));
        Assert.Equal(TubeAction.Poured, board.ClickTube(to));
        Assert.Equal(1, board.Moves);
        Assert.True(board.CanUndo);
        Assert.NotEqual(before, Snapshot(board));

        Assert.True(board.Undo());

        Assert.Equal(before, Snapshot(board));
        Assert.Equal(0, board.Moves);
        Assert.False(board.CanUndo);
        Assert.False(board.Undo());
    }

    [Fact]
    public void APourMovesTheWholeRunOfTheTopColor()
    {
        var board = Build(3, Seed);
        var emptyTube = board.TubeCount - 1;
        Assert.Equal(0, board.Count(emptyTube));
        var source = 0;
        var topColor = board.TopColor(source);
        var run = 0;
        for (var segment = board.Count(source) - 1; segment >= 0 && board.Segment(source, segment) == topColor; segment--)
        {
            run++;
        }

        board.ClickTube(source);
        Assert.Equal(TubeAction.Poured, board.ClickTube(emptyTube));

        Assert.Equal(run, board.Count(emptyTube));
        Assert.Equal(run, board.LastPour.Count);
        Assert.Equal(topColor, board.LastPour.Color);
        Assert.True(board.IsTubeSorted(emptyTube) || run < WaterSortBoard.Capacity);
    }
}
