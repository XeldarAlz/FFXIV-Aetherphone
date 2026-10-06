using Aetherphone.Apps.Games.Sudoku;
using Xunit;

namespace Aetherphone.Tests;

public sealed class SudokuBoardTests
{
    private const ulong Seed = 0x5EED5EEDUL;

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void EveryGeneratedPuzzleHasExactlyOneSolution(int difficulty)
    {
        var board = new SudokuBoard();
        for (var attempt = 0; attempt < 3; attempt++)
        {
            board.Reset((SudokuDifficulty)difficulty, Seed + (ulong)attempt);
            var grid = new byte[SudokuBoard.CellCount];
            for (var cell = 0; cell < SudokuBoard.CellCount; cell++)
            {
                grid[cell] = board.Given(cell);
            }

            Assert.Equal(1, CountSolutions(grid, 2));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void GivensAlwaysAgreeWithTheStoredAnswer(int difficulty)
    {
        var board = new SudokuBoard();
        board.Reset((SudokuDifficulty)difficulty, Seed);

        for (var cell = 0; cell < SudokuBoard.CellCount; cell++)
        {
            if (board.IsGiven(cell))
            {
                Assert.Equal(board.Answer(cell), board.Given(cell));
            }
        }
    }

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = new SudokuBoard();
        var second = new SudokuBoard();
        first.Reset(SudokuDifficulty.Hard, 0xC0FFEEUL);
        second.Reset(SudokuDifficulty.Hard, 0xC0FFEEUL);

        for (var cell = 0; cell < SudokuBoard.CellCount; cell++)
        {
            Assert.Equal(first.Given(cell), second.Given(cell));
            Assert.Equal(first.Answer(cell), second.Answer(cell));
        }

        Assert.Equal(first.PickHintCell(-1), second.PickHintCell(-1));
    }

    [Fact]
    public void DifferentSeedsProduceDifferentPuzzles()
    {
        var first = new SudokuBoard();
        var second = new SudokuBoard();
        first.Reset(SudokuDifficulty.Easy, 1UL);
        second.Reset(SudokuDifficulty.Easy, 2UL);

        var same = true;
        for (var cell = 0; cell < SudokuBoard.CellCount && same; cell++)
        {
            same = first.Answer(cell) == second.Answer(cell);
        }

        Assert.False(same);
    }

    [Fact]
    public void FillingEveryEmptyCellWithTheAnswerSolvesTheBoard()
    {
        var board = new SudokuBoard();
        board.Reset(SudokuDifficulty.Medium, Seed);

        for (var cell = 0; cell < SudokuBoard.CellCount; cell++)
        {
            if (!board.IsGiven(cell))
            {
                board.SetEntry(cell, board.Answer(cell));
            }
        }

        Assert.True(board.Solved);
        Assert.Equal(0, board.RemainingCells);
    }

    [Fact]
    public void AWrongDigitIsFlaggedAndDoesNotSolveTheBoard()
    {
        var board = new SudokuBoard();
        board.Reset(SudokuDifficulty.Easy, Seed);
        var target = FirstEmptyCell(board);
        var wrong = (byte)(board.Answer(target) % 9 + 1);

        Assert.True(board.SetEntry(target, wrong));
        Assert.True(board.IsWrong(target));
        Assert.False(board.Solved);
    }

    [Fact]
    public void UndoRestoresThePreviousEntry()
    {
        var board = new SudokuBoard();
        board.Reset(SudokuDifficulty.Easy, Seed);
        var target = FirstEmptyCell(board);
        board.SetEntry(target, board.Answer(target));

        Assert.True(board.CanUndo);
        Assert.True(board.Undo());
        Assert.Equal(0, board.Entry(target));
        Assert.False(board.CanUndo);
    }

    [Fact]
    public void PlacingADigitClearsThatNoteFromItsPeersAndUndoBringsItBack()
    {
        var board = new SudokuBoard();
        board.Reset(SudokuDifficulty.Hard, Seed);
        var target = FirstEmptyCell(board);
        var peer = FirstEmptyPeer(board, target);
        var digit = board.Answer(target);
        board.ToggleNote(peer, digit);

        Assert.True((board.Notes(peer) & (1 << digit)) != 0);

        board.SetEntry(target, digit);

        Assert.Equal(0, board.Notes(peer) & (1 << digit));

        board.Undo();

        Assert.True((board.Notes(peer) & (1 << digit)) != 0);
    }

    [Fact]
    public void DuplicateDigitsInAUnitAreReportedAsConflicts()
    {
        var board = new SudokuBoard();
        board.Reset(SudokuDifficulty.Easy, Seed);
        var source = FirstGivenCell(board);
        var peer = FirstEmptyPeer(board, source);

        board.SetEntry(peer, board.Given(source));

        Assert.True(board.IsConflicting(peer));
        Assert.True(board.IsConflicting(source));
    }

    [Fact]
    public void RevealFillsTheCellWithTheAnswer()
    {
        var board = new SudokuBoard();
        board.Reset(SudokuDifficulty.Medium, Seed);
        var target = board.PickHintCell(-1);

        Assert.True(target >= 0);
        Assert.True(board.Reveal(target));
        Assert.Equal(board.Answer(target), board.Entry(target));
        Assert.True(board.IsRevealed(target));
    }

    private static int FirstEmptyCell(SudokuBoard board)
    {
        for (var cell = 0; cell < SudokuBoard.CellCount; cell++)
        {
            if (!board.IsGiven(cell))
            {
                return cell;
            }
        }

        return -1;
    }

    private static int FirstGivenCell(SudokuBoard board)
    {
        for (var cell = 0; cell < SudokuBoard.CellCount; cell++)
        {
            if (board.IsGiven(cell))
            {
                return cell;
            }
        }

        return -1;
    }

    private static int FirstEmptyPeer(SudokuBoard board, int cell)
    {
        for (var peer = 0; peer < SudokuBoard.CellCount; peer++)
        {
            if (peer != cell && !board.IsGiven(peer) && SudokuBoard.ArePeers(peer, cell))
            {
                return peer;
            }
        }

        return -1;
    }

    private static int CountSolutions(byte[] grid, int limit)
    {
        var target = -1;
        for (var cell = 0; cell < SudokuBoard.CellCount; cell++)
        {
            if (grid[cell] == 0)
            {
                target = cell;
                break;
            }
        }

        if (target < 0)
        {
            return 1;
        }

        var found = 0;
        for (byte digit = 1; digit <= SudokuBoard.Size; digit++)
        {
            if (!IsPlaceable(grid, target, digit))
            {
                continue;
            }

            grid[target] = digit;
            found += CountSolutions(grid, limit - found);
            grid[target] = 0;
            if (found >= limit)
            {
                return found;
            }
        }

        return found;
    }

    private static bool IsPlaceable(byte[] grid, int cell, byte digit)
    {
        var row = SudokuBoard.RowOf(cell);
        var column = SudokuBoard.ColumnOf(cell);
        var box = SudokuBoard.BoxOf(cell);
        for (var peer = 0; peer < SudokuBoard.CellCount; peer++)
        {
            if (grid[peer] != digit)
            {
                continue;
            }

            if (SudokuBoard.RowOf(peer) == row || SudokuBoard.ColumnOf(peer) == column ||
                SudokuBoard.BoxOf(peer) == box)
            {
                return false;
            }
        }

        return true;
    }
}
