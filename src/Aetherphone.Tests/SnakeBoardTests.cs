using System.Text;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Snake;
using Xunit;

namespace Aetherphone.Tests;

public sealed class SnakeBoardTests
{
    private const float Frame = 1f / 60f;

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = Play(1234, out var firstTrace);
        var second = Play(1234, out var secondTrace);
        Play(99, out var otherTrace);

        Assert.Equal(firstTrace, secondTrace);
        Assert.Equal(first.Score, second.Score);
        Assert.Equal(first.Length, second.Length);
        Assert.Equal(first.HeadCell, second.HeadCell);
        Assert.Equal(first.FruitCell, second.FruitCell);
        Assert.Equal(first.State, second.State);
        Assert.NotEqual(firstTrace, otherTrace);
    }

    [Fact]
    public void TheSnakeStartsHeadingUpAndGrowsAfterAnApple()
    {
        var board = Fresh(1, false);
        var startLength = board.Length;
        board.PlaceFruit(Ahead(board, 2));

        StepCells(board, 2);

        Assert.Equal(SnakeBoard.ApplePoints, board.Score);
        Assert.Equal(FruitKind.Apple, board.AteThisStep);
        Assert.Equal(startLength, board.Length);
        StepCells(board, 1);
        Assert.Equal(startLength + 1, board.Length);
        Assert.Equal(1, board.Eaten);
    }

    [Fact]
    public void AGoldFruitScoresThreeAndExpiresWhenIgnored()
    {
        var board = Fresh(2, true);
        board.PlaceSpecial(Ahead(board, 2), FruitKind.Gold);
        StepCells(board, 2);
        Assert.Equal(SnakeBoard.GoldPoints, board.Score);
        Assert.Equal(FruitKind.None, board.SpecialKind);

        var side = SnakeBoard.CellIndex(0, SnakeBoard.CellY(board.HeadCell));
        board.PlaceSpecial(side, FruitKind.Gold);
        for (var frame = 0; frame < 60 * 7; frame++)
        {
            board.Step(Frame);
        }

        Assert.Equal(FruitKind.None, board.SpecialKind);
        Assert.Equal(SnakeBoard.GoldPoints, board.Score);
    }

    [Fact]
    public void ABombShrinksTheSnakeButNeverBelowTheMinimum()
    {
        var board = Fresh(3, false);
        board.PlaceSpecial(Ahead(board, 1), FruitKind.Bomb);

        StepCells(board, 1);

        Assert.Equal(FruitKind.Bomb, board.AteThisStep);
        Assert.Equal(SnakeBoard.MinLength, board.Length);
        Assert.Equal(0, board.Score);
    }

    [Fact]
    public void TheWallEndsTheRunInClassicAndWrapsInWrapMode()
    {
        var classic = Fresh(4, false);
        var rowsAbove = SnakeBoard.CellY(classic.HeadCell);
        StepCells(classic, rowsAbove + 1);
        Assert.Equal(SnakeState.Dying, classic.State);
        Assert.True(classic.DiedThisStep);
        for (var frame = 0; frame < 60; frame++)
        {
            classic.Step(Frame);
        }

        Assert.Equal(SnakeState.Over, classic.State);

        var wrap = Fresh(4, true);
        StepCells(wrap, rowsAbove + 1);
        Assert.Equal(SnakeState.Playing, wrap.State);
        Assert.Equal(SnakeBoard.Rows - 1, SnakeBoard.CellY(wrap.HeadCell));
    }

    [Fact]
    public void ReversingIntoTheBodyIsIgnoredAndTurnsQueueInOrder()
    {
        var board = Fresh(5, true);
        board.Steer(SnakeDirection.Down);
        StepCells(board, 1);
        Assert.Equal(SnakeDirection.Up, board.Direction);

        board.Steer(SnakeDirection.Right);
        board.Steer(SnakeDirection.Down);
        StepCells(board, 1);
        Assert.Equal(SnakeDirection.Right, board.Direction);
        StepCells(board, 1);
        Assert.Equal(SnakeDirection.Down, board.Direction);
    }

    [Fact]
    public void RunningIntoTheBodyEndsTheRun()
    {
        var board = Fresh(6, true);
        board.PlaceFruit(Ahead(board, 1));
        StepCells(board, 2);
        board.Steer(SnakeDirection.Right);
        StepCells(board, 1);
        board.Steer(SnakeDirection.Down);
        StepCells(board, 1);
        board.Steer(SnakeDirection.Left);
        StepCells(board, 1);

        Assert.Equal(SnakeState.Dying, board.State);
    }

    [Fact]
    public void EveryFourthFruitRaisesTheLevelAndTheSpeed()
    {
        var board = Fresh(7, true);
        for (var fruit = 0; fruit < SnakeBoard.FruitPerLevel; fruit++)
        {
            board.PlaceFruit(Ahead(board, 1));
            StepCells(board, 1);
        }

        Assert.Equal(2, board.Level);
        Assert.True(board.LevelledThisStep);
        Assert.True(board.StepSeconds < SnakeBoard.BaseStepSeconds);
    }

    private static SnakeBoard Seeded(int seed, bool wrap)
    {
        var board = new SnakeBoard();
        board.Reset(GameRandom.FromSeed((ulong)seed), wrap);
        return board;
    }

    private static SnakeBoard Fresh(int seed, bool wrap)
    {
        var board = Seeded(seed, wrap);
        board.PlaceFruit(SnakeBoard.CellIndex(0, 0));
        board.PlaceSpecial(-1, FruitKind.None);
        return board;
    }

    private static int Ahead(SnakeBoard board, int cells)
    {
        var heading = board.Heading;
        var x = SnakeBoard.CellX(board.HeadCell) + (int)heading.X * cells;
        var y = SnakeBoard.CellY(board.HeadCell) + (int)heading.Y * cells;
        return SnakeBoard.CellIndex((x + SnakeBoard.Columns) % SnakeBoard.Columns, (y + SnakeBoard.Rows) % SnakeBoard.Rows);
    }

    private static void StepCells(SnakeBoard board, int cells)
    {
        for (var cell = 0; cell < cells; cell++)
        {
            board.Step(board.StepSeconds + 0.0001f);
        }
    }

    private static SnakeBoard Play(int seed, out string trace)
    {
        var board = Seeded(seed, true);
        var builder = new StringBuilder();
        for (var frame = 0; frame < 60 * 40 && board.State == SnakeState.Playing; frame++)
        {
            if (frame % 36 == 0)
            {
                board.Steer((frame / 36 % 4) switch
                {
                    0 => SnakeDirection.Right,
                    1 => SnakeDirection.Up,
                    2 => SnakeDirection.Left,
                    _ => SnakeDirection.Up,
                });
            }

            board.Step(Frame);
            if (board.AteThisStep != FruitKind.None)
            {
                builder.Append(board.HeadCell).Append(':').Append((int)board.AteThisStep).Append(' ');
            }
        }

        builder.Append('|').Append(board.FruitCell).Append('|').Append(board.SpecialCell);
        trace = builder.ToString();
        return board;
    }
}
