using System.Numerics;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Invaders;
using Xunit;

namespace Aetherphone.Tests;

public sealed class InvadersBoardTests
{
    private const float Step = 1f / 120f;
    private const float BulletFlightToSaucer =
        (InvadersBoard.PlayerY - InvadersBoard.PlayerHeight - InvadersBoard.SaucerY) / InvadersBoard.BulletSpeed;

    [Fact]
    public void TheFormationStartsAtTheTopLeftAndDropsWithEachWaveUpToACap()
    {
        var board = new InvadersBoard();
        board.StartGame(GameRandom.FromSeed(1));
        Assert.Equal(new Vector2(6f, 14f), board.InvaderPosition(0, 0));
        Assert.Equal(InvadersBoard.InvaderCount, board.AliveCount);
        Assert.Equal(1, board.Wave);
        board.FormationBounds(out var minX, out var maxX);
        Assert.Equal(6f, minX);
        Assert.Equal(6f + 6 * InvadersBoard.ColumnPitch + InvadersBoard.InvaderWidth, maxX);
    }

    [Theory]
    [InlineData(1, 35, 0.59f)]
    [InlineData(1, 1, 0.055f)]
    [InlineData(19, 35, 0.055f)]
    public void TheStepIntervalShrinksWithTheRankAndFloors(int wave, int alive, float expected)
    {
        Assert.Equal(expected, InvadersBoard.StepInterval(wave, alive), 3);
    }

    [Fact]
    public void APerfectWavePaysEightHundredAndThirty()
    {
        Assert.Equal(830, InvadersBoard.PerfectWavePoints());
    }

    [Fact]
    public void OnlyOneBulletMayBeInTheAir()
    {
        var board = new InvadersBoard();
        board.StartGame(GameRandom.FromSeed(1));
        Assert.True(board.Fire());
        Assert.False(board.Fire());
        Assert.Equal(1, board.ShotsFired);
    }

    [Fact]
    public void ABulletUnderABunkerChipsExactlyOneCellAndStopsThere()
    {
        var board = new InvadersBoard();
        board.StartGame(GameRandom.FromSeed(1));
        var target = InvadersBoard.ShieldX[1];
        var direction = target < board.PlayerX ? -1f : 1f;
        while (MathF.Abs(board.PlayerX - target) > 0.2f)
        {
            board.Move(direction, Step);
        }

        Assert.True(board.Fire());
        var elapsed = 0f;
        while (board.HasBullet && elapsed < 1f)
        {
            board.Update(Step);
            elapsed += Step;
        }

        Assert.False(board.HasBullet);
        Assert.Equal(InvadersBoard.ShieldCellCount - 1, CountShieldCells(board));
        Assert.Equal(InvadersBoard.InvaderCount, board.AliveCount);
        Assert.Equal(0, board.Score);
        Assert.Equal(0, board.ShotsHit);
    }

    [Fact]
    public void ShieldIndexIsUniquePerCell()
    {
        var seen = new bool[InvadersBoard.ShieldCellCount];
        for (var shield = 0; shield < InvadersBoard.ShieldCount; shield++)
        {
            for (var column = 0; column < InvadersBoard.ShieldColumns; column++)
            {
                for (var row = 0; row < InvadersBoard.ShieldRows; row++)
                {
                    var index = InvadersBoard.ShieldIndex(shield, column, row);
                    Assert.False(seen[index]);
                    seen[index] = true;
                }
            }
        }
    }

    [Fact]
    public void TheMysterySaucerCrossesTheTopAfterItsDelay()
    {
        var board = new InvadersBoard();
        board.StartGame(GameRandom.FromSeed(4));
        var elapsed = 0f;
        while (!board.SaucerActive && elapsed < 40f && !board.GameOver)
        {
            Dodge(board);
            board.Update(Step);
            elapsed += Step;
        }

        Assert.True(board.SaucerActive);
        Assert.InRange(elapsed, InvadersBoard.SaucerMinInterval, InvadersBoard.SaucerMaxInterval + 1f);
        var direction = board.SaucerDirection;
        Assert.True(direction > 0 ? board.SaucerX < 0f : board.SaucerX > InvadersBoard.Width);
        var crossing = 0f;
        var lastX = board.SaucerX;
        while (board.SaucerActive && crossing < 10f && !board.GameOver)
        {
            Dodge(board);
            board.Update(Step);
            crossing += Step;
            if (board.SaucerActive)
            {
                Assert.True(direction > 0 ? board.SaucerX >= lastX : board.SaucerX <= lastX);
                lastX = board.SaucerX;
            }
        }

        Assert.False(board.SaucerActive);
        Assert.True(direction > 0 ? lastX > InvadersBoard.Width : lastX < 0f);
        var expectedCrossing = (InvadersBoard.Width + InvadersBoard.SaucerHalfWidth * 4f) / InvadersBoard.SaucerSpeed;
        Assert.InRange(crossing, expectedCrossing - 0.2f, expectedCrossing + 0.2f);
    }

    [Fact]
    public void ShootingTheSaucerFromTheClearLeftLanePaysThreeHundred()
    {
        var kills = 0;
        var passes = 0;
        for (var seed = 1UL; seed <= 24UL; seed++)
        {
            var board = new InvadersBoard();
            board.StartGame(GameRandom.FromSeed(seed));
            var elapsed = 0f;
            var sawSaucer = false;
            while (elapsed < 60f && !board.GameOver)
            {
                board.Move(-1f, Step);
                if (board.SaucerActive && !board.HasBullet)
                {
                    sawSaucer = true;
                    var predicted = board.SaucerX + board.SaucerDirection * InvadersBoard.SaucerSpeed * BulletFlightToSaucer;
                    if (MathF.Abs(predicted - board.PlayerX) <= InvadersBoard.SaucerHalfWidth - 0.5f)
                    {
                        board.Fire();
                    }
                }

                var scoreBefore = board.Score;
                var saucersBefore = board.SaucersHit;
                board.Update(Step);
                elapsed += Step;
                if (!board.SaucerKilledThisFrame)
                {
                    continue;
                }

                kills++;
                Assert.Equal(0, board.KillCount);
                Assert.Equal(scoreBefore + InvadersBoard.SaucerPoints, board.Score);
                Assert.Equal(saucersBefore + 1, board.SaucersHit);
                Assert.False(board.SaucerActive);
                Assert.False(board.HasBullet);
                Assert.Equal(InvadersBoard.SaucerY, board.SaucerKillPosition.Y);
                break;
            }

            if (sawSaucer)
            {
                passes++;
            }
        }

        Assert.True(passes >= 12);
        Assert.True(kills >= passes / 2);
    }

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = new InvadersBoard();
        var second = new InvadersBoard();
        first.StartGame(GameRandom.FromSeed(77));
        second.StartGame(GameRandom.FromSeed(77));
        var elapsed = 0f;
        var nextShot = 0.3f;
        while (elapsed < 90f && !first.GameOver)
        {
            var direction = MathF.Sin(elapsed * 0.7f) > 0f ? 1f : -1f;
            first.Move(direction, Step);
            second.Move(direction, Step);
            if (elapsed >= nextShot)
            {
                Assert.Equal(first.Fire(), second.Fire());
                nextShot += 0.3f;
            }

            first.Update(Step);
            second.Update(Step);
            elapsed += Step;
            Assert.Equal(first.Score, second.Score);
            Assert.Equal(first.Wave, second.Wave);
            Assert.Equal(first.AliveCount, second.AliveCount);
            Assert.Equal(first.Lives, second.Lives);
            Assert.Equal(first.PlayerX, second.PlayerX);
            Assert.Equal(first.BombCount, second.BombCount);
            Assert.Equal(first.SaucerActive, second.SaucerActive);
            Assert.Equal(first.SaucerX, second.SaucerX);
            Assert.Equal(first.FormationY, second.FormationY);
        }

        Assert.True(first.Score > 0);
        Assert.Equal(first.ShotsFired, second.ShotsFired);
        Assert.Equal(first.ShotsHit, second.ShotsHit);
    }

    private static void Dodge(InvadersBoard board)
    {
        for (var index = 0; index < board.BombCount; index++)
        {
            var bomb = board.GetBomb(index);
            if (bomb.Y < 80f || MathF.Abs(bomb.X - board.PlayerX) > InvadersBoard.PlayerWidth)
            {
                continue;
            }

            board.Move(bomb.X < board.PlayerX ? 1f : -1f, Step);
            return;
        }
    }

    private static int CountShieldCells(InvadersBoard board)
    {
        var count = 0;
        for (var shield = 0; shield < InvadersBoard.ShieldCount; shield++)
        {
            for (var column = 0; column < InvadersBoard.ShieldColumns; column++)
            {
                for (var row = 0; row < InvadersBoard.ShieldRows; row++)
                {
                    if (board.ShieldCellAlive(shield, column, row))
                    {
                        count++;
                    }
                }
            }
        }

        return count;
    }
}
