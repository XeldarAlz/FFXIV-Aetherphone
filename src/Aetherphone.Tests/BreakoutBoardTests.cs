using System.Numerics;
using System.Text;
using Aetherphone.Apps.Games.Breakout;
using Aetherphone.Apps.Games.Framework;
using Xunit;

namespace Aetherphone.Tests;

public sealed class BreakoutBoardTests
{
    private const float Step = 1f / 120f;
    private const float AutoPlaySeconds = 45f;
    private const float PatienceSeconds = 10f;

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = Play(77, out var firstTrace, out _);
        var second = Play(77, out var secondTrace, out _);
        Play(78, out var otherTrace, out _);

        Assert.Equal(firstTrace, secondTrace);
        Assert.Equal(first.Score, second.Score);
        Assert.Equal(first.Level, second.Level);
        Assert.Equal(first.Lives, second.Lives);
        Assert.Equal(first.BricksBroken, second.BricksBroken);
        Assert.True(first.BricksBroken > 0);
        Assert.NotEqual(firstTrace, otherTrace);
    }

    [Fact]
    public void AnArmouredBrickTakesTwoHits()
    {
        var board = Prepared(1);
        board.PlaceBrick(3, 2, BrickKind.Armoured);
        var target = BreakoutBoard.BrickCenter(3, 2);
        board.PlaceBall(new Vector2(target.X, target.Y + 0.4f), new Vector2(0f, -BreakoutBoard.BaseSpeed));
        board.SetPaddle(target.X);

        RunUntil(board, static current => current.DentCount > 0);

        Assert.True(board.BrickAlive(3, 2));
        Assert.Equal(1, board.BrickHits(3, 2));
        Assert.Equal(0, board.Score);
        Assert.Equal(0, board.BricksBroken);

        RunUntil(board, static current => current.BreakCount > 0);

        Assert.False(board.BrickAlive(3, 2));
        Assert.Equal(BrickKind.Armoured, board.BreakKind(0));
        Assert.Equal(BreakoutBoard.ArmouredPoints, board.Score);
        Assert.Equal(1, board.BricksBroken);
    }

    [Fact]
    public void AnExplosiveBrickClearsItsNeighbours()
    {
        var board = Prepared(2);
        for (var row = 2; row <= 4; row++)
        {
            for (var column = 2; column <= 4; column++)
            {
                if (column == 3 && row == 4)
                {
                    continue;
                }

                board.PlaceBrick(column, row, BrickKind.Normal);
            }
        }

        board.PlaceBrick(3, 3, BrickKind.Explosive);
        board.PlaceBrick(0, 0, BrickKind.Normal);
        var target = BreakoutBoard.BrickCenter(3, 3);
        board.PlaceBall(new Vector2(target.X, target.Y + 0.4f), new Vector2(0f, -BreakoutBoard.BaseSpeed));
        board.SetPaddle(target.X);

        RunUntil(board, static current => current.BreakCount > 0);

        Assert.Equal(8, board.BreakCount);
        Assert.Equal(1, board.ExplosionCount);
        Assert.Equal(target, board.ExplosionPosition(0));
        for (var row = 2; row <= 4; row++)
        {
            for (var column = 2; column <= 4; column++)
            {
                Assert.False(board.BrickAlive(column, row));
            }
        }

        Assert.True(board.BrickAlive(0, 0));
        Assert.Equal(1, board.AliveBricks);
        Assert.False(board.LevelCleared);
        Assert.Equal(8, board.BricksBroken);
    }

    [Fact]
    public void AChainOfExplosivesDetonatesTogether()
    {
        var board = Prepared(3);
        board.PlaceBrick(3, 4, BrickKind.Explosive);
        board.PlaceBrick(4, 3, BrickKind.Explosive);
        board.PlaceBrick(5, 2, BrickKind.Armoured);
        board.PlaceBrick(0, 0, BrickKind.Normal);
        var target = BreakoutBoard.BrickCenter(3, 4);
        board.PlaceBall(new Vector2(target.X, target.Y + 0.4f), new Vector2(0f, -BreakoutBoard.BaseSpeed));
        board.SetPaddle(target.X);

        RunUntil(board, static current => current.BreakCount > 0);

        Assert.Equal(2, board.ExplosionCount);
        Assert.Equal(3, board.BreakCount);
        Assert.False(board.BrickAlive(5, 2));
        Assert.True(board.BrickAlive(0, 0));
    }

    [Fact]
    public void PowerUpDropsFollowTheSeed()
    {
        Play(300, out _, out var firstDrops);
        Play(300, out _, out var secondDrops);
        Play(301, out _, out var otherDrops);

        Assert.NotEmpty(firstDrops);
        Assert.Equal(firstDrops, secondDrops);
        Assert.NotEqual(firstDrops, otherDrops);
    }

    [Fact]
    public void TheLastBrickHoldsTheFieldBeforeTheNextLevelBuilds()
    {
        var board = Prepared(4);
        board.PlaceBrick(3, 2, BrickKind.Normal);
        var target = BreakoutBoard.BrickCenter(3, 2);
        board.PlaceBall(new Vector2(target.X, target.Y + 0.4f), new Vector2(0f, -BreakoutBoard.BaseSpeed));
        board.SetPaddle(target.X);

        RunUntil(board, static current => current.LevelCleared);

        Assert.True(board.Holding);
        Assert.Equal(1, board.Level);
        Assert.Equal(0, board.AliveBricks);

        RunUntil(board, static current => current.LevelStarted);

        Assert.False(board.Holding);
        Assert.Equal(2, board.Level);
        Assert.True(board.Attached);
        Assert.True(board.AliveBricks > 0);
        Assert.Equal(BreakoutBoard.StartingLives, board.Lives);
    }

    private static BreakoutBoard Prepared(int seed)
    {
        var board = new BreakoutBoard();
        board.StartGame(GameRandom.FromSeed((ulong)seed));
        board.ClearBricks();
        return board;
    }

    private static void RunUntil(BreakoutBoard board, Func<BreakoutBoard, bool> condition)
    {
        var elapsed = 0f;
        while (!condition(board))
        {
            board.Update(Step);
            elapsed += Step;
            Assert.True(elapsed < PatienceSeconds);
        }
    }

    private static BreakoutBoard Play(int seed, out string trace, out string drops)
    {
        var board = new BreakoutBoard();
        board.StartGame(GameRandom.FromSeed((ulong)seed));
        var builder = new StringBuilder();
        var dropBuilder = new StringBuilder();
        for (var row = 0; row < board.Rows; row++)
        {
            for (var column = 0; column < BreakoutBoard.Columns; column++)
            {
                builder.Append((int)board.BrickKindAt(column, row));
            }
        }

        builder.Append('|');
        var frames = (int)(AutoPlaySeconds / Step);
        for (var frame = 0; frame < frames && !board.GameOver; frame++)
        {
            if (board.Attached)
            {
                board.Launch();
            }

            board.SetPaddle(LowestBallX(board) + 0.03f * MathF.Sin(frame * 0.05f));
            board.Update(Step);
            for (var index = 0; index < board.BreakCount; index++)
            {
                var position = board.BreakPosition(index);
                builder.Append((int)(position.X * 1000f)).Append(':').Append((int)(position.Y * 1000f)).Append(' ');
            }

            for (var index = 0; index < board.SpawnCount; index++)
            {
                dropBuilder.Append((int)board.SpawnedKind(index)).Append('@').Append(frame).Append(' ');
            }
        }

        builder.Append('|').Append(board.Score).Append('|').Append(board.Level).Append('|').Append(board.Lives);
        trace = builder.ToString();
        drops = dropBuilder.ToString();
        return board;
    }

    private static float LowestBallX(BreakoutBoard board)
    {
        var lowest = board.GetBall(0);
        for (var index = 1; index < board.BallCount; index++)
        {
            var ball = board.GetBall(index);
            if (ball.Position.Y > lowest.Position.Y)
            {
                lowest = ball;
            }
        }

        return lowest.Position.X;
    }
}
