using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Squadron;
using Xunit;

namespace Aetherphone.Tests;

public sealed class SquadronBoardTests
{
    private const float Step = 1f / 60f;

    [Theory]
    [InlineData(1, 20)]
    [InlineData(2, 22)]
    [InlineData(4, 26)]
    [InlineData(9, 26)]
    public void FormationSizeGrowsByTwoDronesAStageAndCaps(int stage, int expected)
    {
        Assert.Equal(expected, SquadronBoard.FormationSize(stage));
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(3, true)]
    [InlineData(6, true)]
    [InlineData(7, false)]
    public void EveryThirdStageIsAChallenge(int stage, bool expected)
    {
        Assert.Equal(expected, SquadronBoard.StageIsChallenge(stage));
    }

    [Fact]
    public void DiveTimingRampsToItsFloors()
    {
        Assert.Equal(2.6f, SquadronBoard.DiveInterval(1), 3);
        Assert.Equal(0.85f, SquadronBoard.DiveInterval(20), 3);
        Assert.Equal(2.5f, SquadronBoard.DiveSeconds(1), 3);
        Assert.Equal(1.7f, SquadronBoard.DiveSeconds(30), 3);
        Assert.Equal(0.1f, SquadronBoard.PairChance(1), 3);
        Assert.Equal(0.65f, SquadronBoard.PairChance(20), 3);
    }

    [Fact]
    public void PointsDoubleForAirborneShips()
    {
        Assert.Equal(50, SquadronBoard.PointsFor(ShipKind.Drone, true));
        Assert.Equal(100, SquadronBoard.PointsFor(ShipKind.Drone, false));
        Assert.Equal(80, SquadronBoard.PointsFor(ShipKind.Raptor, true));
        Assert.Equal(160, SquadronBoard.PointsFor(ShipKind.Raptor, false));
        Assert.Equal(150, SquadronBoard.PointsFor(ShipKind.Warden, true));
        Assert.Equal(300, SquadronBoard.PointsFor(ShipKind.Warden, false));
    }

    [Fact]
    public void StageOneFliesTwentyShipsInFourWavesOfFive()
    {
        var board = new SquadronBoard();
        board.StartGame(GameRandom.FromSeed(1));
        Assert.Equal(20, board.ShipCount);
        Assert.False(board.IsChallenge);
        var firstWaveStart = float.MaxValue;
        var lastWaveStart = float.MinValue;
        for (var index = 0; index < board.ShipCount; index++)
        {
            var ship = board.GetShip(index);
            firstWaveStart = MathF.Min(firstWaveStart, ship.FlyAt);
            lastWaveStart = MathF.Max(lastWaveStart, ship.FlyAt);
        }

        Assert.Equal(0.8f, firstWaveStart, 3);
        Assert.Equal(0.8f + 3 * 1.5f + 4 * 0.16f, lastWaveStart, 3);
    }

    [Fact]
    public void TheFirstWaveParksOnItsBreathingSlotBeforeAnyDive()
    {
        var board = new SquadronBoard();
        board.StartGame(GameRandom.FromSeed(1));
        var elapsed = 0f;
        while (elapsed < 3.45f)
        {
            board.Tick(Step);
            elapsed += Step;
        }

        for (var index = 0; index < 5; index++)
        {
            var ship = board.GetShip(index);
            Assert.Equal(ShipState.Parked, ship.State);
            Assert.Equal(board.SlotPosition(in ship), ship.Position);
        }

        for (var index = 5; index < board.ShipCount; index++)
        {
            var state = board.GetShip(index).State;
            Assert.True(state is ShipState.Waiting or ShipState.FlyIn or ShipState.Parked);
        }

        Assert.Equal(3, board.Lives);
    }

    [Fact]
    public void SingleFighterHoldsTwoBulletsAndDualHoldsThree()
    {
        var board = new SquadronBoard();
        board.StartGame(GameRandom.FromSeed(1));
        Assert.True(board.Fire());
        Assert.True(board.Fire());
        Assert.False(board.Fire());
        Assert.Equal(2, board.BulletCount);
        Assert.Equal(2, board.ShotsFired);
    }

    [Fact]
    public void TheBeamExtendsHoldsAndRetracts()
    {
        var board = new SquadronBoard();
        var ship = new Ship { State = ShipState.Beam, BeamTime = 0.25f };
        Assert.Equal(0.5f, board.BeamExtent(in ship), 3);
        ship.BeamTime = 1.5f;
        Assert.Equal(1f, board.BeamExtent(in ship), 3);
        ship.BeamTime = 2.35f;
        Assert.Equal(0.5f, board.BeamExtent(in ship), 3);
        ship.State = ShipState.Diving;
        Assert.Equal(0f, board.BeamExtent(in ship), 3);
    }

    [Fact]
    public void AKillCountsAsAHitAndPaysItsKindsRate()
    {
        var board = new SquadronBoard();
        board.StartGame(GameRandom.FromSeed(1));
        var elapsed = 0f;
        while (elapsed < 3.45f)
        {
            board.Tick(Step);
            elapsed += Step;
        }

        var target = board.GetShip(0);
        var direction = target.Position.X < board.PlayerX ? -1f : 1f;
        while (MathF.Abs(board.PlayerX - target.Position.X) > 0.3f)
        {
            board.Move(direction, Step);
        }

        Assert.True(board.Fire());
        while (board.KillCount == 0 && elapsed < 6f)
        {
            board.Tick(Step);
            elapsed += Step;
        }

        Assert.Equal(1, board.KillCount);
        Assert.Equal(1, board.ShotsHit);
        Assert.Equal(1, board.ShotsFired);
        var kind = board.KillKind(0);
        var points = board.KillPoints(0);
        Assert.True(points == SquadronBoard.PointsFor(kind, true) || points == SquadronBoard.PointsFor(kind, false));
        Assert.Equal(points, board.Score);
    }

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = new SquadronBoard();
        var second = new SquadronBoard();
        first.StartGame(GameRandom.FromSeed(4242));
        second.StartGame(GameRandom.FromSeed(4242));
        var elapsed = 0f;
        var nextShot = 0.25f;
        while (elapsed < 120f && !first.GameOver)
        {
            var direction = MathF.Sin(elapsed * 0.9f) > 0f ? 1f : -1f;
            first.Move(direction, Step);
            second.Move(direction, Step);
            if (elapsed >= nextShot)
            {
                Assert.Equal(first.Fire(), second.Fire());
                nextShot += 0.25f;
            }

            first.Tick(Step);
            second.Tick(Step);
            elapsed += Step;
            Assert.Equal(first.Score, second.Score);
            Assert.Equal(first.Stage, second.Stage);
            Assert.Equal(first.Lives, second.Lives);
            Assert.Equal(first.PlayerX, second.PlayerX);
            Assert.Equal(first.ShipCount, second.ShipCount);
            Assert.Equal(first.BulletCount, second.BulletCount);
            Assert.Equal(first.ShotCount, second.ShotCount);
            Assert.Equal(first.Dual, second.Dual);
            Assert.Equal(first.CaptureActive, second.CaptureActive);
            for (var index = 0; index < first.ShipCount; index++)
            {
                Assert.Equal(first.GetShip(index).State, second.GetShip(index).State);
                Assert.Equal(first.GetShip(index).Position, second.GetShip(index).Position);
            }
        }

        Assert.True(first.Score > 0);
        Assert.Equal(first.ShotsFired, second.ShotsFired);
        Assert.Equal(first.ShotsHit, second.ShotsHit);
        Assert.Equal(first.Rescues, second.Rescues);
    }
}
