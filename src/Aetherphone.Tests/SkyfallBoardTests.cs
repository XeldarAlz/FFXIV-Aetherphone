using System.Numerics;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Skyfall;
using Xunit;

namespace Aetherphone.Tests;

public sealed class SkyfallBoardTests
{
    private const float Step = 1f / 120f;
    private const float ShotSpeed = 110f;

    [Fact]
    public void ABlastGrowsHoldsAndShrinksInUnderASecond()
    {
        var board = new SkyfallBoard();
        board.StartGame(GameRandom.FromSeed(1));
        Assert.True(board.Fire(new Vector2(SkyfallBoard.BatteryX, 60f)));
        var elapsed = 0f;
        while (board.BlastCount == 0)
        {
            board.Update(Step);
            elapsed += Step;
            Assert.True(elapsed < 1f);
        }

        var peak = 0f;
        var lifetime = 0f;
        while (board.BlastCount > 0)
        {
            peak = MathF.Max(peak, board.GetBlast(0).Radius);
            board.Update(Step);
            lifetime += Step;
        }

        Assert.Equal(SkyfallBoard.BlastMaxRadius, peak, 2);
        Assert.InRange(lifetime, 0.8f, 0.95f);
    }

    [Fact]
    public void FireRefusesTargetsBelowTheBarrelAndCountsAmmo()
    {
        var board = new SkyfallBoard();
        board.StartGame(GameRandom.FromSeed(1));
        Assert.False(board.Fire(new Vector2(20f, SkyfallBoard.GroundY)));
        Assert.Equal(SkyfallBoard.AmmoPerWave, board.Ammo);
        Assert.Equal(0, board.ShotsFired);
        Assert.True(board.Fire(new Vector2(20f, 50f)));
        Assert.Equal(SkyfallBoard.AmmoPerWave - 1, board.Ammo);
        Assert.Equal(1, board.ShotsFired);
    }

    [Fact]
    public void ASingleImpactCanNeverTakeTwoCities()
    {
        for (var first = 0; first < SkyfallBoard.CityCount; first++)
        {
            for (var second = first + 1; second < SkyfallBoard.CityCount; second++)
            {
                var spacing = MathF.Abs(SkyfallBoard.CityX[first] - SkyfallBoard.CityX[second]);
                Assert.True(spacing > SkyfallBoard.CityHalfWidth * 2f);
            }
        }
    }

    [Theory]
    [InlineData(1, 9)]
    [InlineData(5, 17)]
    [InlineData(10, 26)]
    [InlineData(30, 26)]
    public void MeteorsPerWaveRampAndCap(int wave, int expected)
    {
        Assert.Equal(expected, SkyfallBoard.MeteorsForWave(wave));
    }

    [Fact]
    public void WaveBonusPaysCitiesAndSpareAmmo()
    {
        Assert.Equal(740, SkyfallBoard.WaveBonus(6, 28));
        Assert.Equal(0, SkyfallBoard.WaveBonus(0, 0));
        Assert.Equal(315, SkyfallBoard.WaveBonus(3, 3));
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    [InlineData(4, true)]
    public void AShieldPickupFallsOnEverySecondWave(int wave, bool expected)
    {
        Assert.Equal(expected, SkyfallBoard.WaveCarriesShield(wave));
    }

    [Fact]
    public void AnUnopposedWaveEndsTheRunOnlyWhenEveryCityFalls()
    {
        var board = new SkyfallBoard();
        board.StartGame(GameRandom.FromSeed(3));
        var elapsed = 0f;
        while (!board.GameOver && elapsed < 600f)
        {
            board.Update(Step);
            elapsed += Step;
        }

        Assert.True(board.GameOver);
        Assert.Equal(0, board.CitiesLeft);
        Assert.Equal(0, board.ShotsHit);
    }

    [Fact]
    public void AShotThatDestroysAMeteorCountsAsAHit()
    {
        var board = new SkyfallBoard();
        board.StartGame(GameRandom.FromSeed(5));
        var elapsed = 0f;
        while (board.MeteorCount == 0 && elapsed < 5f)
        {
            board.Update(Step);
            elapsed += Step;
        }

        Assert.True(board.MeteorCount > 0);
        AutoDefend(board);
        Assert.Equal(1, board.ShotsFired);
        while (board.DestroyedCount == 0 && elapsed < 10f)
        {
            board.Update(Step);
            elapsed += Step;
        }

        Assert.True(board.DestroyedCount > 0);
        Assert.Equal(1, board.ShotsHit);
        Assert.Equal(board.DestroyedCount, board.MeteorsDestroyed);
    }

    [Fact]
    public void TheLastMeteorOfAWaveRaisesItsOwnEvent()
    {
        var board = new SkyfallBoard();
        board.StartGame(GameRandom.FromSeed(8));
        var elapsed = 0f;
        var seen = false;
        while (!board.WaveClearedThisFrame && elapsed < 120f && !board.GameOver)
        {
            AutoDefend(board);
            board.Update(Step);
            elapsed += Step;
            if (!board.LastMeteorDestroyedThisFrame)
            {
                continue;
            }

            seen = true;
            Assert.Equal(0, board.MeteorCount);
            Assert.True(board.DestroyedCount > 0);
        }

        Assert.True(board.WaveClearedThisFrame);
        Assert.True(seen);
    }

    [Fact]
    public void AShieldPickupCaughtByABlastAbsorbsTheNextCityHit()
    {
        var board = new SkyfallBoard();
        board.StartGame(GameRandom.FromSeed(11));
        var elapsed = 0f;
        while (!board.ShieldCollectedThisFrame && elapsed < 300f && !board.GameOver)
        {
            AutoDefend(board);
            board.Update(Step);
            elapsed += Step;
        }

        Assert.True(board.ShieldCollectedThisFrame);
        Assert.Equal(1, board.ShieldCharges);
        Assert.False(board.ShieldFalling);
        Assert.True(SkyfallBoard.WaveCarriesShield(board.Wave));
        var citiesBefore = board.CitiesLeft;
        while (board.ShieldAbsorbedCityThisFrame < 0 && board.CityLostThisFrame < 0 && elapsed < 600f)
        {
            board.Update(Step);
            elapsed += Step;
        }

        Assert.True(board.ShieldAbsorbedCityThisFrame >= 0);
        Assert.Equal(-1, board.CityLostThisFrame);
        Assert.Equal(citiesBefore, board.CitiesLeft);
        Assert.Equal(0, board.ShieldCharges);
    }

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = new SkyfallBoard();
        var second = new SkyfallBoard();
        first.StartGame(GameRandom.FromSeed(2024));
        second.StartGame(GameRandom.FromSeed(2024));
        var elapsed = 0f;
        var shot = 0;
        var nextShot = 0.4f;
        while (elapsed < 90f && !first.GameOver)
        {
            if (elapsed >= nextShot)
            {
                var target = new Vector2(shot * 13 % 100, 30f + shot * 7 % 60);
                Assert.Equal(first.Fire(target), second.Fire(target));
                shot++;
                nextShot += 0.4f;
            }

            first.Update(Step);
            second.Update(Step);
            elapsed += Step;
            Assert.Equal(first.Score, second.Score);
            Assert.Equal(first.Wave, second.Wave);
            Assert.Equal(first.MeteorCount, second.MeteorCount);
            Assert.Equal(first.BlastCount, second.BlastCount);
            Assert.Equal(first.CitiesLeft, second.CitiesLeft);
            Assert.Equal(first.Ammo, second.Ammo);
            Assert.Equal(first.ShieldFalling, second.ShieldFalling);
            Assert.Equal(first.ShieldPosition, second.ShieldPosition);
            for (var index = 0; index < first.MeteorCount; index++)
            {
                Assert.Equal(first.GetMeteor(index).Position, second.GetMeteor(index).Position);
            }
        }

        Assert.True(first.Score > 0);
        Assert.Equal(first.ShotsHit, second.ShotsHit);
        Assert.Equal(first.MeteorsDestroyed, second.MeteorsDestroyed);
    }

    private static void AutoDefend(SkyfallBoard board)
    {
        if (board.InterceptorCount > 0 || board.Ammo <= 0 || board.InWaveBreak)
        {
            return;
        }

        var barrel = new Vector2(SkyfallBoard.BatteryX, SkyfallBoard.BarrelY);
        if (board.ShieldFalling)
        {
            var travel = Vector2.Distance(barrel, board.ShieldPosition) / ShotSpeed;
            var lead = board.ShieldPosition + new Vector2(0f, SkyfallBoard.ShieldFallSpeed * travel);
            board.Fire(lead);
            return;
        }

        if (board.MeteorCount == 0)
        {
            return;
        }

        var lowest = 0;
        for (var index = 1; index < board.MeteorCount; index++)
        {
            if (board.GetMeteor(index).Position.Y > board.GetMeteor(lowest).Position.Y)
            {
                lowest = index;
            }
        }

        var meteor = board.GetMeteor(lowest);
        var flight = Vector2.Distance(barrel, meteor.Position) / ShotSpeed;
        var target = meteor.Position + meteor.Direction * meteor.Speed * flight;
        if (target.Y > SkyfallBoard.BarrelY - 4f)
        {
            target.Y = SkyfallBoard.BarrelY - 4f;
        }

        board.Fire(target);
    }
}
