using System.Text;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Nonogram;
using Xunit;

namespace Aetherphone.Tests;

public sealed class NonogramBoardTests
{
    private static readonly int[] Sizes = { NonogramBoard.MinSize, 8, NonogramBoard.MaxSize };

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = Seeded(NonogramBoard.MaxSize, 1234);
        var second = Seeded(NonogramBoard.MaxSize, 1234);
        var other = Seeded(NonogramBoard.MaxSize, 99);

        Assert.Equal(Picture(first), Picture(second));
        Assert.Equal(Clues(first), Clues(second));
        Assert.Equal(first.FilledTarget, second.FilledTarget);
        Assert.NotEqual(Picture(first), Picture(other));
    }

    [Fact]
    public void GeneratedPuzzleIsConsistentWithItsOwnClues()
    {
        for (var sizeIndex = 0; sizeIndex < Sizes.Length; sizeIndex++)
        {
            for (var seed = 1; seed <= 8; seed++)
            {
                var board = Seeded(Sizes[sizeIndex], seed);
                Assert.Equal(Clues(board), CluesFromSolution(board));
                Assert.True(board.FilledTarget > board.CellCount / 4);

                for (var index = 0; index < board.CellCount; index++)
                {
                    if (board.SolutionAt(index))
                    {
                        Assert.Equal(MarkResult.Changed, board.SetMark(index, CellMark.Filled));
                    }
                }

                Assert.True(board.Solved);
                Assert.Equal(0, board.Mistakes);
                Assert.Equal(board.FilledTarget, board.FilledCount);
            }
        }
    }

    [Fact]
    public void EveryGeneratedPuzzleIsSolvableByLineLogicAlone()
    {
        for (var sizeIndex = 0; sizeIndex < Sizes.Length; sizeIndex++)
        {
            for (var seed = 1; seed <= 12; seed++)
            {
                Assert.True(Seeded(Sizes[sizeIndex], seed).LineSolvable);
            }
        }
    }

    [Fact]
    public void AWrongFillCountsAMistakeAndCrossesTheCell()
    {
        var board = Seeded(8, 3);
        var wrong = FirstCell(board, false, CellMark.Empty);
        var right = FirstCell(board, true, CellMark.Empty);

        Assert.Equal(MarkResult.Mistake, board.SetMark(wrong, CellMark.Filled));
        Assert.Equal(CellMark.Marked, board.MarkAt(wrong));
        Assert.Equal(1, board.Mistakes);
        Assert.True(board.Started);

        Assert.Equal(MarkResult.Changed, board.SetMark(right, CellMark.Marked));
        Assert.Equal(1, board.Mistakes);
        Assert.Equal(MarkResult.None, board.SetMark(right, CellMark.Marked));
        Assert.Equal(0, board.ChangedCount);
    }

    [Fact]
    public void ASatisfiedRowCrossesItsRemainingCells()
    {
        var board = Seeded(NonogramBoard.MaxSize, 11);
        var row = FirstMixedRow(board);

        Assert.False(board.RowSatisfied(row));
        var lastChanged = 0;
        for (var column = 0; column < board.Size; column++)
        {
            var index = row * board.Size + column;
            if (board.SolutionAt(index))
            {
                board.SetMark(index, CellMark.Filled);
                lastChanged = board.ChangedCount;
            }
        }

        Assert.True(board.RowSatisfied(row));
        Assert.True(lastChanged > 1);
        for (var column = 0; column < board.Size; column++)
        {
            var index = row * board.Size + column;
            Assert.Equal(board.SolutionAt(index) ? CellMark.Filled : CellMark.Marked, board.MarkAt(index));
        }
    }

    [Fact]
    public void ErasingAFillReopensTheRowButKeepsItsCrosses()
    {
        var board = Seeded(NonogramBoard.MaxSize, 11);
        var row = FirstMixedRow(board);
        var filledIndex = -1;
        for (var column = 0; column < board.Size; column++)
        {
            var index = row * board.Size + column;
            if (board.SolutionAt(index))
            {
                board.SetMark(index, CellMark.Filled);
                filledIndex = index;
            }
        }

        Assert.Equal(MarkResult.Changed, board.SetMark(filledIndex, CellMark.Empty));
        Assert.False(board.RowSatisfied(row));
        Assert.False(board.Solved);
        for (var column = 0; column < board.Size; column++)
        {
            var index = row * board.Size + column;
            if (!board.SolutionAt(index))
            {
                Assert.Equal(CellMark.Marked, board.MarkAt(index));
            }
        }
    }

    private static NonogramBoard Seeded(int size, int seed)
    {
        var board = new NonogramBoard();
        board.Reset(size, GameRandom.FromSeed((ulong)seed));
        return board;
    }

    private static string Picture(NonogramBoard board)
    {
        var builder = new StringBuilder();
        for (var index = 0; index < board.CellCount; index++)
        {
            builder.Append(board.SolutionAt(index) ? '#' : '.');
        }

        return builder.ToString();
    }

    private static string Clues(NonogramBoard board)
    {
        var builder = new StringBuilder();
        for (var row = 0; row < board.Size; row++)
        {
            for (var slot = 0; slot < board.RowClueCount(row); slot++)
            {
                builder.Append(board.RowClue(row, slot)).Append(',');
            }

            builder.Append('|');
        }

        builder.Append('/');
        for (var column = 0; column < board.Size; column++)
        {
            for (var slot = 0; slot < board.ColumnClueCount(column); slot++)
            {
                builder.Append(board.ColumnClue(column, slot)).Append(',');
            }

            builder.Append('|');
        }

        return builder.ToString();
    }

    private static string CluesFromSolution(NonogramBoard board)
    {
        var builder = new StringBuilder();
        for (var row = 0; row < board.Size; row++)
        {
            AppendRuns(builder, board, row, true);
        }

        builder.Append('/');
        for (var column = 0; column < board.Size; column++)
        {
            AppendRuns(builder, board, column, false);
        }

        return builder.ToString();
    }

    private static void AppendRuns(StringBuilder builder, NonogramBoard board, int line, bool isRow)
    {
        var run = 0;
        var any = false;
        for (var cell = 0; cell < board.Size; cell++)
        {
            var index = isRow ? line * board.Size + cell : cell * board.Size + line;
            if (board.SolutionAt(index))
            {
                run++;
                continue;
            }

            if (run > 0)
            {
                builder.Append(run).Append(',');
                any = true;
                run = 0;
            }
        }

        if (run > 0)
        {
            builder.Append(run).Append(',');
            any = true;
        }

        if (!any)
        {
            builder.Append("0,");
        }

        builder.Append('|');
    }

    private static int FirstCell(NonogramBoard board, bool inSolution, CellMark mark)
    {
        for (var index = 0; index < board.CellCount; index++)
        {
            if (board.SolutionAt(index) == inSolution && board.MarkAt(index) == mark)
            {
                return index;
            }
        }

        throw new InvalidOperationException("The board has no cell in the requested state.");
    }

    private static int FirstMixedRow(NonogramBoard board)
    {
        for (var row = 0; row < board.Size; row++)
        {
            var filled = 0;
            for (var column = 0; column < board.Size; column++)
            {
                if (board.SolutionAt(row * board.Size + column))
                {
                    filled++;
                }
            }

            if (filled > 0 && filled < board.Size)
            {
                return row;
            }
        }

        throw new InvalidOperationException("The board has no row with both filled and empty cells.");
    }
}
