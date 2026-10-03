using Aetherphone.Apps.Games.GemSwap;
using Xunit;

namespace Aetherphone.Tests;

public sealed class GemSwapBoardTests
{
    private const int FreeColor = 4;
    private const int OtherFreeColor = 5;

    [Fact]
    public void TheSameSeedDealsTheSameBoard()
    {
        var first = new GemSwapBoard(7);
        var second = new GemSwapBoard(7);
        first.Reset();
        second.Reset();
        for (var index = 0; index < GemSwapBoard.CellCount; index++)
        {
            Assert.Equal(first.Color(index), second.Color(index));
        }

        Assert.False(first.HasAnyMatch());
        Assert.True(first.HasPossibleMoves());
    }

    [Fact]
    public void FourInARowLeavesALineGemOnTheMovedCell()
    {
        var board = PatternBoard();
        board.SetCell(Cell(0, 3), FreeColor, GemSpecial.None);
        board.SetCell(Cell(1, 3), FreeColor, GemSpecial.None);
        board.SetCell(Cell(3, 3), FreeColor, GemSpecial.None);
        board.SetCell(Cell(2, 4), FreeColor, GemSpecial.None);
        board.Swap(Cell(2, 4), Cell(2, 3));
        var cleared = board.ResolveMatches(1);
        Assert.Equal(3, cleared);
        Assert.Equal(GemSpecial.LineHorizontal, board.Special(Cell(2, 3)));
        Assert.Equal(FreeColor, board.Color(Cell(2, 3)));
        Assert.False(board.Matched(Cell(2, 3)));
        Assert.True(board.Matched(Cell(0, 3)));
        Assert.True(board.Matched(Cell(3, 3)));
    }

    [Fact]
    public void ALineGemThatFormsANewLineStillFiresBeforeItIsReplaced()
    {
        var board = PatternBoard();
        board.SetCell(Cell(0, 3), FreeColor, GemSpecial.None);
        board.SetCell(Cell(1, 3), FreeColor, GemSpecial.None);
        board.SetCell(Cell(3, 3), FreeColor, GemSpecial.None);
        board.SetCell(Cell(2, 4), FreeColor, GemSpecial.LineVertical);
        board.Swap(Cell(2, 4), Cell(2, 3));
        var cleared = board.ResolveMatches(1);
        Assert.Equal(3 + GemSwapBoard.Rows - 1, cleared);
        Assert.Equal(1, board.ActivatedCount);
        Assert.Equal(GemSpecial.LineVertical, board.ActivatedKind(0));
        Assert.Equal(GemSpecial.LineHorizontal, board.Special(Cell(2, 3)));
        Assert.False(board.Matched(Cell(2, 3)));
        Assert.True(board.Matched(Cell(2, 0)));
        Assert.True(board.Matched(Cell(2, 6)));
    }

    [Fact]
    public void LAndTShapesLeaveABurstGemOnTheCorner()
    {
        var lShape = PatternBoard();
        SetRun(lShape, 1, 1, 3, true, FreeColor);
        SetRun(lShape, 1, 1, 3, false, FreeColor);
        Assert.Equal(4, lShape.ResolveMatches(1));
        Assert.Equal(GemSpecial.Burst, lShape.Special(Cell(1, 1)));
        Assert.Equal(1, lShape.LastSpecialsCreated);

        var tShape = PatternBoard();
        SetRun(tShape, 2, 2, 3, true, FreeColor);
        SetRun(tShape, 3, 2, 3, false, FreeColor);
        Assert.Equal(4, tShape.ResolveMatches(1));
        Assert.Equal(GemSpecial.Burst, tShape.Special(Cell(3, 2)));
        Assert.Equal(1, tShape.LastSpecialsCreated);
    }

    [Fact]
    public void FiveInARowLeavesAPrism()
    {
        var board = PatternBoard();
        SetRun(board, 1, 5, 5, true, FreeColor);
        Assert.Equal(4, board.ResolveMatches(1));
        Assert.Equal(GemSpecial.Prism, board.Special(Cell(3, 5)));
        Assert.Equal(GemSwapBoard.PrismColor, board.Color(Cell(3, 5)));
        Assert.Equal(1, board.LastSpecialsCreated);
    }

    [Fact]
    public void AMatchedLineGemClearsItsWholeColumn()
    {
        var board = PatternBoard();
        board.SetCell(Cell(3, 2), OtherFreeColor, GemSpecial.None);
        board.SetCell(Cell(4, 2), OtherFreeColor, GemSpecial.LineVertical);
        board.SetCell(Cell(5, 2), OtherFreeColor, GemSpecial.None);
        var cleared = board.ResolveMatches(1);
        Assert.Equal(3 + GemSwapBoard.Rows - 1, cleared);
        for (var row = 0; row < GemSwapBoard.Rows; row++)
        {
            Assert.True(board.Matched(Cell(4, row)));
        }

        Assert.Equal(1, board.ActivatedCount);
        Assert.Equal(GemSpecial.LineVertical, board.ActivatedKind(0));
    }

    [Fact]
    public void APrismSwappedWithAGemClearsEveryGemOfThatColour()
    {
        var board = PatternBoard();
        board.SetCell(Cell(0, 0), 0, GemSpecial.Prism);
        var targetColor = board.Color(Cell(1, 0));
        var expected = 1;
        for (var index = 0; index < GemSwapBoard.CellCount; index++)
        {
            if (board.Color(index) == targetColor)
            {
                expected++;
            }
        }

        board.Swap(Cell(0, 0), Cell(1, 0));
        Assert.True(board.IsComboSwap(Cell(0, 0), Cell(1, 0)));
        var cleared = board.ResolveCombo(Cell(1, 0), Cell(0, 0), 1);
        Assert.Equal(expected, cleared);
        Assert.Equal(GemCombo.PrismColor, board.LastCombo);
        for (var index = 0; index < GemSwapBoard.CellCount; index++)
        {
            var shouldClear = board.Color(index) == targetColor || index == Cell(1, 0);
            Assert.Equal(shouldClear, board.Matched(index));
        }
    }

    [Fact]
    public void TwoLineGemsSwappedTogetherClearACross()
    {
        var board = PatternBoard();
        board.SetCell(Cell(3, 3), 0, GemSpecial.LineHorizontal);
        board.SetCell(Cell(3, 4), 1, GemSpecial.LineVertical);
        board.Swap(Cell(3, 4), Cell(3, 3));
        var cleared = board.ResolveCombo(Cell(3, 3), Cell(3, 4), 1);
        Assert.Equal(GemCombo.Cross, board.LastCombo);
        Assert.Equal(GemSwapBoard.Columns + GemSwapBoard.Rows - 1, cleared);
    }

    [Fact]
    public void TwoPrismsClearTheWholeBoard()
    {
        var board = PatternBoard();
        board.SetCell(Cell(3, 3), 0, GemSpecial.Prism);
        board.SetCell(Cell(4, 3), 0, GemSpecial.Prism);
        Assert.Equal(GemSwapBoard.CellCount, board.ResolveCombo(Cell(3, 3), Cell(4, 3), 1));
        Assert.Equal(GemCombo.PrismBoard, board.LastCombo);
    }

    [Fact]
    public void PrismsNeverMatchEachOtherByColour()
    {
        var board = PatternBoard();
        board.SetCell(Cell(0, 6), 0, GemSpecial.Prism);
        board.SetCell(Cell(1, 6), 0, GemSpecial.Prism);
        board.SetCell(Cell(2, 6), 0, GemSpecial.Prism);
        Assert.False(board.HasAnyMatch());
        Assert.Equal(0, board.ResolveMatches(1));
    }

    [Fact]
    public void ClassicScoringForASimpleThreeMatchIsUnchanged()
    {
        var board = PatternBoard();
        SetRun(board, 0, 0, 3, true, FreeColor);
        Assert.Equal(3, board.ResolveMatches(1));
        Assert.Equal(36, board.Score);
        Assert.Equal(0, board.LastSpecialsCreated);
        Assert.Equal(3, board.ClearedOfColor(FreeColor));
    }

    [Fact]
    public void FireBlastsAFourByFourPatch()
    {
        var board = PatternBoard();
        var cleared = GemSwapPowers.Fire(board, 1, out var origin);
        Assert.Equal(GemSwapPowers.FireSize * GemSwapPowers.FireSize, cleared);
        Assert.True(board.Matched(origin));
    }

    [Fact]
    public void AFullTimeBarAddsSecondsAndRaisesTheRequirement()
    {
        var blitz = new GemSwapBlitz();
        var bonuses = 0;
        var calls = 0;
        while (bonuses == 0)
        {
            calls++;
            if (blitz.AddClear(3, 1))
            {
                bonuses++;
            }

            Assert.True(calls < 100);
        }

        Assert.Equal((int)(GemSwapBlitz.FirstRequirement / 3f), calls);
        Assert.Equal(GemSwapBlitz.StartSeconds + GemSwapBlitz.BonusSeconds, blitz.TimeLeft);
        Assert.Equal(0f, blitz.BarFill);
        Assert.Equal(GemSwapBlitz.FirstRequirement + GemSwapBlitz.RequirementStep, blitz.BarRequirement);
        Assert.True(GemSwapBlitz.FillFor(5, 2) > GemSwapBlitz.FillFor(3, 1) * 2f);
    }

    [Fact]
    public void FrostStopsTheClockForItsDuration()
    {
        var blitz = new GemSwapBlitz();
        Assert.False(blitz.TryFire((int)GemPower.Frost));
        Assert.True(blitz.AddCharge((int)GemPower.Frost, (int)GemSwapBlitz.ChargeNeeded));
        Assert.True(blitz.TryFire((int)GemPower.Frost));
        Assert.True(blitz.Frozen);
        blitz.Tick(3f);
        blitz.Tick(GemSwapBlitz.FrostDuration - 3f);
        Assert.Equal(GemSwapBlitz.StartSeconds, blitz.TimeLeft);
        Assert.False(blitz.Frozen);
        blitz.Tick(1f);
        Assert.Equal(GemSwapBlitz.StartSeconds - 1f, blitz.TimeLeft);
        Assert.Equal(0f, blitz.Charge((int)GemPower.Frost));
    }

    [Fact]
    public void TheClockReachingZeroEndsTheRound()
    {
        var blitz = new GemSwapBlitz();
        Assert.False(blitz.Tick(GemSwapBlitz.StartSeconds - 0.5f));
        Assert.False(blitz.Ended);
        Assert.True(blitz.Tick(1f));
        Assert.True(blitz.Ended);
        Assert.Equal(0f, blitz.TimeLeft);
        Assert.False(blitz.Tick(1f));
        Assert.False(blitz.AddClear(30, 3));
        Assert.Equal(0f, blitz.TimeLeft);
    }

    private static GemSwapBoard PatternBoard()
    {
        var board = new GemSwapBoard(1);
        for (var row = 0; row < GemSwapBoard.Rows; row++)
        {
            for (var column = 0; column < GemSwapBoard.Columns; column++)
            {
                board.SetCell(Cell(column, row), (column + row * 2) % 4, GemSpecial.None);
            }
        }

        Assert.False(board.HasAnyMatch());
        return board;
    }

    private static void SetRun(GemSwapBoard board, int column, int row, int length, bool horizontal, int color)
    {
        for (var offset = 0; offset < length; offset++)
        {
            var cell = horizontal ? Cell(column + offset, row) : Cell(column, row + offset);
            board.SetCell(cell, color, GemSpecial.None);
        }
    }

    private static int Cell(int column, int row) => row * GemSwapBoard.Columns + column;
}
