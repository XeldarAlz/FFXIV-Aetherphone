using System.Numerics;
using System.Text;
using Aetherphone.Apps.Games.BubbleShooter;
using Aetherphone.Apps.Games.Framework;
using Xunit;

namespace Aetherphone.Tests;

public sealed class BubbleBoardTests
{
    private const float Step = 1f / 60f;
    private const int ShotsPerRun = 40;
    private const float AdjacencyTolerance = 1.05f;
    private const float PatienceSeconds = 10f;

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = Play(11, out var firstTrace);
        var second = Play(11, out var secondTrace);
        Play(12, out var otherTrace);

        Assert.Equal(firstTrace, secondTrace);
        Assert.Equal(first.Score, second.Score);
        Assert.Equal(first.ShotsFired, second.ShotsFired);
        Assert.Equal(first.RowsAdded, second.RowsAdded);
        Assert.Equal(first.TotalPopped, second.TotalPopped);
        Assert.True(first.ShotsFired > 0);
        Assert.NotEqual(firstTrace, otherTrace);
    }

    [Fact]
    public void ParityStaysShiftAwareAfterARowDescends()
    {
        var board = Seeded(21);
        Assert.Equal(BubbleBoard.Radius, board.CellCenter(0, 0).X, 5);
        Assert.Equal(BubbleBoard.Diameter, board.CellCenter(0, 1).X, 5);
        AssertNeighboursMatchGeometry(board);

        var interval = board.RowInterval;
        for (var shot = 0; shot < interval; shot++)
        {
            FireAndSettle(board, new Vector2(0f, -1f));
        }

        Assert.Equal(1, board.RowsAdded);
        Assert.False(board.GameOver);
        Assert.Equal(BubbleBoard.Diameter, board.CellCenter(0, 0).X, 5);
        Assert.Equal(BubbleBoard.Radius, board.CellCenter(0, 1).X, 5);
        AssertNeighboursMatchGeometry(board);
    }

    [Fact]
    public void TheRowLimitIsTheDeepestRowThatClearsTheLauncher()
    {
        var board = Seeded(3);
        var clearance = BubbleBoard.LauncherPosition.Y - BubbleBoard.Diameter * BubbleBoard.LauncherClearance;

        Assert.InRange(board.RowLimit, 6, BubbleBoard.RowCapacity);
        Assert.True(board.DangerY <= clearance + 0.0001f);
        Assert.True(board.DangerY + BubbleBoard.RowSpacing > clearance);
    }

    [Fact]
    public void AClusterOfThreePopsAndAMissResetsTheCombo()
    {
        var board = Seeded(5);
        board.ClearCells();
        board.SetCell(0, 0, 1);
        board.SetCell(3, 0, 0);
        board.SetCell(4, 0, 0);
        board.SetLauncher(0, BubbleKind.Normal);

        FireAndSettle(board, new Vector2(0f, -1f));

        Assert.Equal(3, board.PopCount);
        Assert.Equal(3, board.TotalPopped);
        Assert.Equal(1, board.Combo.Count);
        Assert.True(board.LastShotScore > 0);
        Assert.Equal(board.LastShotScore, board.Score);
        Assert.Equal(1, board.ColorAt(0, 0));
        Assert.Equal(-1, board.ColorAt(3, 0));
        Assert.Equal(-1, board.ColorAt(4, 0));

        board.SetLauncher(2, BubbleKind.Normal);
        FireAndSettle(board, new Vector2(0f, -1f));

        Assert.Equal(0, board.PopCount);
        Assert.Equal(0, board.Combo.Count);
        Assert.Equal(2, board.ShotsFired);
    }

    private static BubbleBoard Seeded(int seed)
    {
        var board = new BubbleBoard();
        board.Reset(GameRandom.FromSeed((ulong)seed));
        return board;
    }

    private static void FireAndSettle(BubbleBoard board, Vector2 direction)
    {
        board.Fire(direction);
        Assert.True(board.Flying);
        var elapsed = 0f;
        while (board.Flying)
        {
            board.Update(Step);
            elapsed += Step;
            Assert.True(elapsed < PatienceSeconds);
        }
    }

    private static void AssertNeighboursMatchGeometry(BubbleBoard board)
    {
        Span<int> neighbours = stackalloc int[BubbleBoard.NeighbourCapacity];
        var reach = BubbleBoard.Diameter * AdjacencyTolerance;
        for (var row = 0; row < board.RowLimit; row++)
        {
            for (var column = 0; column < BubbleBoard.Columns; column++)
            {
                var cell = row * BubbleBoard.Columns + column;
                var center = board.CellCenter(column, row);
                var count = board.Neighbors(cell, neighbours);
                var listed = neighbours[..count];
                var expected = 0;
                for (var otherRow = 0; otherRow < board.RowLimit; otherRow++)
                {
                    for (var otherColumn = 0; otherColumn < BubbleBoard.Columns; otherColumn++)
                    {
                        var other = otherRow * BubbleBoard.Columns + otherColumn;
                        if (other == cell)
                        {
                            continue;
                        }

                        if (Vector2.Distance(center, board.CellCenter(otherColumn, otherRow)) > reach)
                        {
                            continue;
                        }

                        expected++;
                        Assert.True(listed.IndexOf(other) >= 0);
                    }
                }

                Assert.Equal(expected, count);
            }
        }
    }

    private static BubbleBoard Play(int seed, out string trace)
    {
        var board = Seeded(seed);
        var builder = new StringBuilder();
        for (var shot = 0; shot < ShotsPerRun && !board.GameOver; shot++)
        {
            var direction = new Vector2(MathF.Sin(shot * 0.9f) * 0.85f, -1f);
            FireAndSettle(board, direction);
            builder.Append(board.PopCount).Append(':').Append(board.LastShotScore).Append(':')
                .Append(board.FallerCount).Append(' ');
        }

        builder.Append('|').Append(board.Score).Append('|').Append(board.RowsAdded).Append('|');
        for (var row = 0; row < board.RowLimit; row++)
        {
            for (var column = 0; column < BubbleBoard.Columns; column++)
            {
                builder.Append((char)('a' + board.ColorAt(column, row) + 1));
            }
        }

        trace = builder.ToString();
        return board;
    }
}
