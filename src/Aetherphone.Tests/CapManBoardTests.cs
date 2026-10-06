using System.Numerics;
using Aetherphone.Apps.Games.CapMan;
using Aetherphone.Apps.Games.Framework;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CapManBoardTests
{
    private const float Step = 1f / 60f;
    private const ulong Seed = 2024;

    [Fact]
    public void TheLayoutHoldsOneHundredAndTwentySevenCollectables()
    {
        var board = new CapManBoard();
        board.StartGame(GameRandom.FromSeed(Seed));
        Assert.Equal(127, board.DotsLeft);
        Assert.Equal(1, board.Level);
        Assert.Equal(new Vector2(7f, 13f), board.PlayerPosition);
    }

    [Fact]
    public void ThePenDoorOpensOnlyForGhostsLeavingOrReturning()
    {
        var board = new CapManBoard();
        board.StartGame(GameRandom.FromSeed(Seed));
        Assert.False(board.Walkable(7, 8, false));
        Assert.False(board.Walkable(7, 8, true));
        Assert.True(board.Walkable(7, 8, true, true));
    }

    [Fact]
    public void TheTunnelWrapsOnRowSevenOnly()
    {
        var board = new CapManBoard();
        board.StartGame(GameRandom.FromSeed(Seed));
        Assert.True(board.Walkable(-1, 7, false));
        Assert.True(board.Walkable(CapManBoard.Columns, 7, false));
        Assert.False(board.Walkable(-1, 3, false));
    }

    [Fact]
    public void AQueuedTurnMovesThePlayerAndEatsDotsAfterTheReadyBeat()
    {
        var board = new CapManBoard();
        board.StartGame(GameRandom.FromSeed(Seed));
        Assert.True(board.Ready);
        Advance(board, CapManBoard.ReadySeconds + 0.05f);
        Assert.False(board.Ready);
        board.Turn(CapManBoard.Left);
        Advance(board, 0.5f);
        Assert.True(board.PlayerPosition.X < 7f);
        Assert.True(board.Score >= CapManBoard.DotPoints);
        Assert.True(board.DotsLeft < 127);
    }

    [Theory]
    [InlineData(1, 4.8f)]
    [InlineData(7, 6.0f)]
    [InlineData(8, 6.076f)]
    public void GhostSpeedRampsAndCapsBelowThePlayer(int level, float expected)
    {
        Assert.Equal(expected, CapManBoard.NormalGhostSpeed(level), 3);
    }

    [Theory]
    [InlineData(1, 6.5f)]
    [InlineData(10, 2f)]
    [InlineData(20, 2f)]
    public void FrightDurationShrinksToAFloor(int level, float expected)
    {
        Assert.Equal(expected, CapManBoard.FrightDuration(level), 3);
    }

    [Fact]
    public void GhostChainDoublesToSixteenHundred()
    {
        Assert.Equal(200, CapManBoard.ChainPoints(1));
        Assert.Equal(400, CapManBoard.ChainPoints(2));
        Assert.Equal(800, CapManBoard.ChainPoints(3));
        Assert.Equal(1600, CapManBoard.ChainPoints(4));
    }

    [Theory]
    [InlineData(69, false)]
    [InlineData(70, true)]
    [InlineData(71, false)]
    [InlineData(127, false)]
    [InlineData(170, true)]
    [InlineData(220, false)]
    [InlineData(270, true)]
    public void TheFruitSpawnsAtTheSeventiethCollectableAndEveryHundredAfter(int collected, bool expected)
    {
        Assert.Equal(expected, CapManBoard.FruitSpawnsAt(collected));
    }

    [Fact]
    public void TheFirstFruitPaysOneHundredAndLaterOnesThreeHundred()
    {
        Assert.Equal(100, CapManBoard.FruitPoints(0));
        Assert.Equal(300, CapManBoard.FruitPoints(1));
        Assert.Equal(300, CapManBoard.FruitPoints(4));
    }

    [Fact]
    public void TheFruitAppearsAtTheStartTileAfterSeventyCollectablesAndFadesAfterNineSeconds()
    {
        var board = new CapManBoard();
        board.StartGame(GameRandom.FromSeed(Seed));
        var elapsed = 0f;
        while (!board.FruitSpawnedThisFrame && !board.GameOver && elapsed < 240f)
        {
            Autopilot.Steer(board, false);
            board.Tick(Step);
            elapsed += Step;
        }

        Assert.True(board.FruitSpawnedThisFrame);
        Assert.Equal(CapManBoard.FirstFruitCollectables, board.Collected);
        Assert.True(board.FruitActive);
        Assert.Equal(new Vector2(7f, 13f), board.FruitPosition);
        Assert.InRange(board.FruitRemaining, CapManBoard.FruitSeconds - Step, CapManBoard.FruitSeconds);
        var livesBefore = board.Lives;
        var fade = 0f;
        while (board.FruitActive && fade < CapManBoard.FruitSeconds + 1f)
        {
            Autopilot.Steer(board, true);
            board.Tick(Step);
            fade += Step;
        }

        Assert.False(board.FruitActive);
        Assert.Equal(0, board.FruitEaten);
        Assert.Equal(livesBefore, board.Lives);
        Assert.InRange(fade, CapManBoard.FruitSeconds - 0.1f, CapManBoard.FruitSeconds + 0.1f);
    }

    [Fact]
    public void EatingTheFruitPaysItsPointsOnce()
    {
        var board = new CapManBoard();
        board.StartGame(GameRandom.FromSeed(Seed));
        var elapsed = 0f;
        while (!board.FruitSpawnedThisFrame && !board.GameOver && elapsed < 240f)
        {
            Autopilot.Steer(board, false);
            board.Tick(Step);
            elapsed += Step;
        }

        Assert.True(board.FruitActive);
        var scoreBefore = board.Score;
        var chase = 0f;
        while (!board.FruitEatenThisFrame && board.FruitActive && chase < CapManBoard.FruitSeconds)
        {
            Autopilot.SteerTo(board, board.FruitPosition);
            board.Tick(Step);
            chase += Step;
        }

        Assert.True(board.FruitEatenThisFrame);
        Assert.Equal(1, board.FruitEaten);
        Assert.Equal(CapManBoard.FirstFruitPoints, board.LastFruitPoints);
        Assert.True(board.Score - scoreBefore >= CapManBoard.FirstFruitPoints);
        Assert.False(board.FruitActive);
    }

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = new CapManBoard();
        var second = new CapManBoard();
        first.StartGame(GameRandom.FromSeed(Seed));
        second.StartGame(GameRandom.FromSeed(Seed));
        var elapsed = 0f;
        while (elapsed < 90f && !first.GameOver)
        {
            Autopilot.Steer(first, false);
            Autopilot.Steer(second, false);
            first.Tick(Step);
            second.Tick(Step);
            elapsed += Step;
            Assert.Equal(first.Score, second.Score);
            Assert.Equal(first.Lives, second.Lives);
            Assert.Equal(first.Level, second.Level);
            Assert.Equal(first.DotsLeft, second.DotsLeft);
            Assert.Equal(first.PlayerPosition, second.PlayerPosition);
            Assert.Equal(first.PlayerDirection, second.PlayerDirection);
            Assert.Equal(first.FruitActive, second.FruitActive);
            Assert.Equal(first.FruitRemaining, second.FruitRemaining);
            Assert.Equal(first.Fruit, second.Fruit);
            for (var index = 0; index < CapManBoard.GhostCount; index++)
            {
                Assert.Equal(first.GetGhost(index).Position, second.GetGhost(index).Position);
                Assert.Equal(first.GetGhost(index).State, second.GetGhost(index).State);
            }
        }

        Assert.True(first.Score > 0);
        Assert.Equal(first.GhostsEaten, second.GhostsEaten);
        Assert.Equal(first.Collected, second.Collected);
    }

    private static void Advance(CapManBoard board, float seconds)
    {
        var elapsed = 0f;
        while (elapsed < seconds)
        {
            board.Tick(Step);
            elapsed += Step;
        }
    }

    private static class Autopilot
    {
        private const int CellCount = CapManBoard.Columns * CapManBoard.Rows;
        private static readonly Vector2[] Directions = { CapManBoard.Up, CapManBoard.Left, CapManBoard.Down, CapManBoard.Right };

        public static void Steer(CapManBoard board, bool avoidFruit)
        {
            if (board.Frozen || board.GameOver)
            {
                return;
            }

            var start = StartCell(board);
            var direction = FirstStep(board, start, avoidFruit, true, -1);
            if (direction == Vector2.Zero)
            {
                direction = FirstStep(board, start, avoidFruit, false, -1);
            }

            Turn(board, direction);
        }

        public static void SteerTo(CapManBoard board, Vector2 tile)
        {
            if (board.Frozen || board.GameOver)
            {
                return;
            }

            var target = (int)tile.Y * CapManBoard.Columns + (int)tile.X;
            Turn(board, FirstStep(board, StartCell(board), false, false, target));
        }

        private static void Turn(CapManBoard board, Vector2 direction)
        {
            if (direction == Vector2.Zero || direction == board.PlayerDirection)
            {
                return;
            }

            board.Turn(direction);
        }

        private static int StartCell(CapManBoard board)
        {
            var x = Wrap((int)MathF.Round(board.PlayerPosition.X));
            var y = (int)MathF.Round(board.PlayerPosition.Y);
            return y * CapManBoard.Columns + x;
        }

        private static Vector2 FirstStep(CapManBoard board, int start, bool avoidFruit, bool avoidGhosts, int target)
        {
            Span<int> parent = stackalloc int[CellCount];
            Span<int> queue = stackalloc int[CellCount];
            parent.Fill(-1);
            parent[start] = start;
            var head = 0;
            var tail = 0;
            queue[tail++] = start;
            var fruitX = (int)board.FruitPosition.X;
            var fruitY = (int)board.FruitPosition.Y;
            while (head < tail)
            {
                var cell = queue[head++];
                var x = cell % CapManBoard.Columns;
                var y = cell / CapManBoard.Columns;
                var found = target >= 0 ? cell == target : cell != start && IsCollectable(board.Tile(x, y));
                if (found)
                {
                    return StepToward(parent, start, cell);
                }

                for (var index = 0; index < Directions.Length; index++)
                {
                    var nextX = Wrap(x + (int)Directions[index].X);
                    var nextY = y + (int)Directions[index].Y;
                    if (!board.Walkable(nextX, nextY, false))
                    {
                        continue;
                    }

                    var next = nextY * CapManBoard.Columns + nextX;
                    if (parent[next] >= 0)
                    {
                        continue;
                    }

                    if (avoidFruit && nextX == fruitX && nextY == fruitY)
                    {
                        continue;
                    }

                    if (avoidGhosts && NearGhost(board, nextX, nextY))
                    {
                        continue;
                    }

                    parent[next] = cell;
                    queue[tail++] = next;
                }
            }

            return Vector2.Zero;
        }

        private static Vector2 StepToward(ReadOnlySpan<int> parent, int start, int target)
        {
            var cell = target;
            while (parent[cell] != start)
            {
                cell = parent[cell];
            }

            var deltaX = cell % CapManBoard.Columns - start % CapManBoard.Columns;
            var deltaY = cell / CapManBoard.Columns - start / CapManBoard.Columns;
            if (deltaX > 1)
            {
                deltaX = -1;
            }
            else if (deltaX < -1)
            {
                deltaX = 1;
            }

            return new Vector2(deltaX, deltaY);
        }

        private static bool NearGhost(CapManBoard board, int x, int y)
        {
            for (var index = 0; index < CapManBoard.GhostCount; index++)
            {
                var ghost = board.GetGhost(index);
                if (ghost.State is not (GhostState.Normal or GhostState.Leaving))
                {
                    continue;
                }

                var ghostX = (int)MathF.Round(ghost.Position.X);
                var ghostY = (int)MathF.Round(ghost.Position.Y);
                if (Math.Abs(ghostX - x) + Math.Abs(ghostY - y) <= 1)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsCollectable(char tile) => tile is CapManBoard.Dot or CapManBoard.Pellet;

        private static int Wrap(int x) => ((x % CapManBoard.Columns) + CapManBoard.Columns) % CapManBoard.Columns;
    }
}
