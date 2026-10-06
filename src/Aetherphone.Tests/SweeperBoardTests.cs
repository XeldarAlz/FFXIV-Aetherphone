using System.Text;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Sweeper;
using Xunit;

namespace Aetherphone.Tests;

public sealed class SweeperBoardTests
{
    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = Opened(Difficulty.Hard, 1234, 84);
        var second = Opened(Difficulty.Hard, 1234, 84);
        var other = Opened(Difficulty.Hard, 99, 84);

        Assert.Equal(Layout(first), Layout(second));
        Assert.Equal(first.WaveCellCount, second.WaveCellCount);
        Assert.Equal(first.WaveMaxDistance, second.WaveMaxDistance);
        Assert.NotEqual(Layout(first), Layout(other));
    }

    [Fact]
    public void TheFirstRevealAlwaysOpensARegion()
    {
        for (var seed = 1; seed <= 40; seed++)
        {
            for (var difficulty = Difficulty.Easy; difficulty <= Difficulty.Hard; difficulty++)
            {
                var board = Seeded(difficulty, seed);
                var click = seed % 3 == 0 ? 0 : seed % 3 == 1 ? board.CellCount - 1 : board.CellCount / 2;
                board.Reveal(click);

                Assert.Equal(SweeperState.Playing, board.State);
                Assert.True(board.Started);
                Assert.Equal(0, board.Adjacent(click));
                Assert.Equal(board.MineCount, CountMines(board));
                var column = click % board.Columns;
                var row = click / board.Columns;
                for (var rowOffset = -1; rowOffset <= 1; rowOffset++)
                {
                    for (var columnOffset = -1; columnOffset <= 1; columnOffset++)
                    {
                        var neighborColumn = column + columnOffset;
                        var neighborRow = row + rowOffset;
                        if (neighborColumn < 0 || neighborColumn >= board.Columns || neighborRow < 0 ||
                            neighborRow >= board.Rows)
                        {
                            continue;
                        }

                        var neighbor = neighborRow * board.Columns + neighborColumn;
                        Assert.False(board.IsMine(neighbor));
                        Assert.True(board.IsRevealed(neighbor));
                    }
                }
            }
        }
    }

    [Fact]
    public void ARevealCascadeRipplesOutwardFromTheClick()
    {
        var board = Opened(Difficulty.Medium, 7, 60);

        Assert.Equal(1, board.WaveId);
        Assert.Equal(0, board.RevealDistance(60));
        Assert.True(board.WaveMaxDistance > 0);
        var revealedCells = 0;
        for (var index = 0; index < board.CellCount; index++)
        {
            if (!board.IsRevealed(index))
            {
                continue;
            }

            revealedCells++;
            Assert.Equal(board.WaveId, board.RevealWave(index));
            var distance = board.RevealDistance(index);
            if (distance == 0)
            {
                continue;
            }

            Assert.True(HasRevealedNeighbourAtDistance(board, index, distance - 1));
        }

        Assert.Equal(revealedCells, board.WaveCellCount);
    }

    [Fact]
    public void AChordNeedsEveryFlagInPlaceBeforeItReveals()
    {
        var board = OpenedWithChordCandidate(out var number);
        var revealedBefore = CountRevealed(board);

        Assert.False(board.Chord(number));
        Assert.Equal(revealedBefore, CountRevealed(board));

        FlagMineNeighbours(board, number);
        var waveBefore = board.WaveId;

        Assert.True(board.Chord(number));
        Assert.Equal(SweeperState.Playing, board.State);
        Assert.Equal(waveBefore + 1, board.WaveId);
        Assert.True(CountRevealed(board) > revealedBefore);
        ForEachNeighbour(board, number, neighbour => Assert.True(board.IsMine(neighbour) || board.IsRevealed(neighbour)));
        Assert.False(board.Chord(number));
    }

    [Fact]
    public void AChordOverAWrongFlagDetonates()
    {
        var board = OpenedWithChordCandidate(out var number);
        var wrongFlag = -1;
        ForEachNeighbour(board, number, neighbour =>
        {
            if (wrongFlag < 0 && !board.IsRevealed(neighbour) && !board.IsMine(neighbour))
            {
                wrongFlag = neighbour;
            }
        });
        board.ToggleFlag(wrongFlag);
        var flags = 1;
        ForEachNeighbour(board, number, neighbour =>
        {
            if (flags < board.Adjacent(number) && board.IsMine(neighbour))
            {
                board.ToggleFlag(neighbour);
                flags++;
            }
        });

        Assert.Equal(board.Adjacent(number), flags);
        Assert.True(board.Chord(number));
        Assert.Equal(SweeperState.Lost, board.State);
        Assert.True(board.ClickedBomb >= 0);
        Assert.True(board.IsRevealed(board.ClickedBomb));
    }

    [Fact]
    public void FlagsBlockRevealsAndCountAgainstTheMines()
    {
        var board = Seeded(Difficulty.Easy, 3);
        board.ToggleFlag(12);

        Assert.Equal(board.MineCount - 1, board.MinesRemaining);
        Assert.False(board.Reveal(12));
        Assert.False(board.Started);

        board.ToggleFlag(12);
        Assert.Equal(board.MineCount, board.MinesRemaining);
        Assert.True(board.Reveal(12));
    }

    private static SweeperBoard Seeded(Difficulty difficulty, int seed)
    {
        var board = new SweeperBoard();
        board.Reset(difficulty, GameRandom.FromSeed((ulong)seed));
        return board;
    }

    private static SweeperBoard Opened(Difficulty difficulty, int seed, int click)
    {
        var board = Seeded(difficulty, seed);
        board.Reveal(click);
        return board;
    }

    private static string Layout(SweeperBoard board)
    {
        var builder = new StringBuilder();
        for (var index = 0; index < board.CellCount; index++)
        {
            builder.Append(board.IsMine(index) ? '*' : board.IsRevealed(index) ? (char)('0' + board.Adjacent(index)) : '.');
        }

        return builder.ToString();
    }

    private static int CountMines(SweeperBoard board)
    {
        var count = 0;
        for (var index = 0; index < board.CellCount; index++)
        {
            if (board.IsMine(index))
            {
                count++;
            }
        }

        return count;
    }

    private static int CountRevealed(SweeperBoard board)
    {
        var count = 0;
        for (var index = 0; index < board.CellCount; index++)
        {
            if (board.IsRevealed(index))
            {
                count++;
            }
        }

        return count;
    }

    private static void ForEachNeighbour(SweeperBoard board, int index, Action<int> visit)
    {
        var column = index % board.Columns;
        var row = index / board.Columns;
        for (var rowOffset = -1; rowOffset <= 1; rowOffset++)
        {
            for (var columnOffset = -1; columnOffset <= 1; columnOffset++)
            {
                if (rowOffset == 0 && columnOffset == 0)
                {
                    continue;
                }

                var neighbourColumn = column + columnOffset;
                var neighbourRow = row + rowOffset;
                if (neighbourColumn < 0 || neighbourColumn >= board.Columns || neighbourRow < 0 ||
                    neighbourRow >= board.Rows)
                {
                    continue;
                }

                visit(neighbourRow * board.Columns + neighbourColumn);
            }
        }
    }

    private static bool HasRevealedNeighbourAtDistance(SweeperBoard board, int index, int distance)
    {
        var found = false;
        ForEachNeighbour(board, index, neighbour =>
        {
            if (board.IsRevealed(neighbour) && board.RevealDistance(neighbour) == distance)
            {
                found = true;
            }
        });
        return found;
    }

    private static SweeperBoard OpenedWithChordCandidate(out int number)
    {
        for (var seed = 5; seed < 60; seed++)
        {
            var board = Opened(Difficulty.Easy, seed, 40);
            number = FindNumberedCellWithCoveredSafeNeighbours(board);
            if (number >= 0)
            {
                return board;
            }
        }

        throw new InvalidOperationException("No seed produced a numbered cell with covered safe neighbours.");
    }

    private static int FindNumberedCellWithCoveredSafeNeighbours(SweeperBoard board)
    {
        for (var index = 0; index < board.CellCount; index++)
        {
            if (!board.IsRevealed(index) || board.Adjacent(index) == 0)
            {
                continue;
            }

            var coveredSafe = 0;
            ForEachNeighbour(board, index, neighbour =>
            {
                if (!board.IsRevealed(neighbour) && !board.IsMine(neighbour))
                {
                    coveredSafe++;
                }
            });
            if (coveredSafe > 0)
            {
                return index;
            }
        }

        return -1;
    }

    private static void FlagMineNeighbours(SweeperBoard board, int index)
    {
        ForEachNeighbour(board, index, neighbour =>
        {
            if (board.IsMine(neighbour) && !board.IsFlagged(neighbour))
            {
                board.ToggleFlag(neighbour);
            }
        });
    }
}
