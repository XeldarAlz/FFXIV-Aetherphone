using System.Text;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Tetris;
using Xunit;

namespace Aetherphone.Tests;

public sealed class TetrisModernTests
{
    private const int ReplayPieces = 40;
    private const float LockoutSeconds = 0.2f;

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = Play(1234, out var firstTrace);
        var second = Play(1234, out var secondTrace);
        Play(99, out var otherTrace);

        Assert.Equal(firstTrace, secondTrace);
        Assert.Equal(first.Score, second.Score);
        Assert.Equal(first.Lines, second.Lines);
        Assert.Equal(first.NextPieceKind, second.NextPieceKind);
        Assert.NotEqual(firstTrace, otherTrace);
    }

    [Fact]
    public void ClearingReportsTheClearedRowsAndTracksTheBestCombo()
    {
        var board = Seeded(TetrisRuleset.Classic, 5);
        FillRowExceptOColumns(board, 18);
        FillRowExceptOColumns(board, 19);
        Assert.True(board.PlaceActive(TetrisPieceKind.O, 3, 17, 0));
        board.HardDrop();
        Assert.Equal(2, board.ClearedLinesThisFrame);
        Assert.Equal((1 << 18) | (1 << 19), board.ClearedRowsMask);
        Assert.Equal(1, board.BestCombo);

        FillRowExceptOColumns(board, 19);
        Assert.True(board.PlaceActive(TetrisPieceKind.O, 3, 17, 0));
        board.HardDrop();
        Assert.Equal(1 << 19, board.ClearedRowsMask);
        Assert.Equal(1, board.LastCombo);
        Assert.Equal(2, board.BestCombo);

        Assert.True(board.PlaceActive(TetrisPieceKind.O, 0, 17, 0));
        board.HardDrop();
        Assert.Equal(0, board.ClearedRowsMask);
        Assert.Equal(-1, board.LastCombo);
        Assert.Equal(2, board.BestCombo);
    }

    [Fact]
    public void ModernUsesSrsIStatesAndSharesEverySpawnShape()
    {
        var classic = TetrisBoard.GetCells(TetrisPieceKind.I, 2, TetrisRuleset.Classic);
        var modern = TetrisBoard.GetCells(TetrisPieceKind.I, 2, TetrisRuleset.Modern);
        Assert.Equal(1, classic[0].Y);
        Assert.Equal(2, modern[0].Y);
        Assert.Equal(1, TetrisBoard.GetCells(TetrisPieceKind.I, 3, TetrisRuleset.Modern)[0].X);
        for (var kind = 0; kind < 7; kind++)
        {
            Assert.Equal(TetrisBoard.GetCells((TetrisPieceKind)kind, 0, TetrisRuleset.Classic),
                TetrisBoard.GetCells((TetrisPieceKind)kind, 0, TetrisRuleset.Modern));
        }
    }

    [Fact]
    public void SrsKicksAFloorRotationUpAndSideways()
    {
        var modern = Seeded(TetrisRuleset.Modern, 1);
        Assert.True(modern.PlaceActive(TetrisPieceKind.T, 4, 17, 0));
        Assert.True(modern.Rotate(1));
        Assert.Equal(1, modern.ActiveRotation);
        Assert.Equal(3, modern.ActiveX);
        Assert.Equal(16, modern.ActiveY);

        var classic = Seeded(TetrisRuleset.Classic, 1);
        Assert.True(classic.PlaceActive(TetrisPieceKind.T, 4, 17, 0));
        Assert.True(classic.Rotate(1));
        Assert.Equal(4, classic.ActiveX);
        Assert.Equal(16, classic.ActiveY);
    }

    [Fact]
    public void ATSpinDoubleIsDetectedAndPaidInModern()
    {
        var board = Seeded(TetrisRuleset.Modern, 2);
        for (var column = 0; column < TetrisBoard.Columns; column++)
        {
            if (column != 4)
            {
                board.Paint(column, 19, 1);
            }

            if (column < 3 || column > 5)
            {
                board.Paint(column, 18, 1);
            }
        }

        board.Paint(5, 17, 1);
        Assert.True(board.PlaceActive(TetrisPieceKind.T, 3, 16, 1));
        Assert.True(board.Rotate(1));
        Assert.Equal(2, board.ActiveRotation);
        board.HardDrop();
        Assert.True(board.LockedThisFrame);
        Assert.Equal(TetrisSpin.Full, board.LastSpin);
        Assert.Equal(2, board.ClearedLinesThisFrame);
        Assert.Equal(1200, board.LastLockScore);
    }

    [Theory]
    [InlineData(1, 1, 0, false, 100)]
    [InlineData(4, 1, 0, false, 800)]
    [InlineData(4, 1, 0, true, 1200)]
    [InlineData(2, 1, 2, false, 1200)]
    [InlineData(2, 1, 2, true, 1800)]
    [InlineData(1, 1, 1, false, 200)]
    [InlineData(3, 2, 0, false, 1000)]
    public void ModernClearsPayTheGuidelineTable(int lines, int level, int spin, bool backToBack, int expected)
    {
        Assert.Equal(expected, TetrisScoringSystem.ModernClearScore(lines, level, (TetrisSpin)spin, backToBack));
    }

    [Fact]
    public void BackToBackMultipliesTheBaseButNotTheCombo()
    {
        var scoring = new TetrisScoringSystem();
        scoring.Reset();
        Assert.Equal(800, scoring.CommitPiece(4, 1, TetrisSpin.None, TetrisRuleset.Modern));
        Assert.Equal(1200 + 50, scoring.CommitPiece(4, 1, TetrisSpin.None, TetrisRuleset.Modern));
        Assert.True(scoring.LastBackToBack);
        Assert.Equal(1, scoring.LastCombo);
        Assert.Equal(400, scoring.CommitPiece(0, 1, TetrisSpin.Full, TetrisRuleset.Modern));
        Assert.Equal(-1, scoring.LastCombo);
    }

    [Fact]
    public void AGroundedModernPieceLocksAfterHalfASecondNotAtOnce()
    {
        var board = Seeded(TetrisRuleset.Modern, 3);
        while (board.SoftDrop())
        {
        }

        Assert.True(board.HasActivePiece);
        var lockedEarly = false;
        for (var step = 0; step < 4; step++)
        {
            board.Update(0.1f);
            lockedEarly |= board.LockedThisFrame;
        }

        Assert.False(lockedEarly);
        var locked = false;
        for (var step = 0; step < 3; step++)
        {
            board.Update(0.1f);
            locked |= board.LockedThisFrame;
        }

        Assert.True(locked);
    }

    [Fact]
    public void ClassicLocksTheMomentSoftDropMeetsTheStack()
    {
        var board = Seeded(TetrisRuleset.Classic, 4);
        while (board.SoftDrop())
        {
        }

        Assert.True(board.LockedThisFrame);
    }

    [Fact]
    public void HardDropIsIgnoredDuringTheModernLockout()
    {
        var board = Seeded(TetrisRuleset.Modern, 6);
        board.HardDrop();
        Assert.True(board.LockedThisFrame);
        var spawnY = board.ActiveY;
        board.HardDrop();
        Assert.Equal(spawnY, board.ActiveY);
        board.Update(LockoutSeconds);
        board.HardDrop();
        Assert.True(board.LockedThisFrame);
    }

    private static TetrisBoard Seeded(TetrisRuleset ruleset, int seed)
    {
        var board = new TetrisBoard();
        board.Reset(ruleset, GameRandom.FromSeed((ulong)seed));
        return board;
    }

    private static void FillRowExceptOColumns(TetrisBoard board, int row)
    {
        for (var column = 0; column < TetrisBoard.Columns; column++)
        {
            if (column != 4 && column != 5)
            {
                board.Paint(column, row, 1);
            }
        }
    }

    private static TetrisBoard Play(int seed, out string trace)
    {
        var board = Seeded(TetrisRuleset.Modern, seed);
        var builder = new StringBuilder();
        for (var piece = 0; piece < ReplayPieces && !board.GameOver; piece++)
        {
            builder.Append((int)board.ActiveKind);
            for (var rotation = 0; rotation < piece % 4; rotation++)
            {
                board.Rotate(1);
            }

            var shift = piece % 7 - 3;
            for (var step = 0; step < Math.Abs(shift); step++)
            {
                board.Move(Math.Sign(shift));
            }

            board.Update(LockoutSeconds);
            board.HardDrop();
            builder.Append(':').Append(board.Score).Append(' ');
        }

        trace = builder.ToString();
        return board;
    }
}
