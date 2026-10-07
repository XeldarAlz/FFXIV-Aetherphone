using System.Globalization;
using System.Numerics;
using System.Text;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Lander;
using Xunit;

namespace Aetherphone.Tests;

public sealed class LanderBoardTests
{
    private const float Frame = 1f / 60f;

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = Fly(31, out var firstTrace);
        var second = Fly(31, out var secondTrace);
        Fly(32, out var otherTrace);

        Assert.Equal(firstTrace, secondTrace);
        Assert.Equal(first.Score, second.Score);
        Assert.True(first.Landings > 0);
        Assert.NotEqual(firstTrace, otherTrace);
    }

    [Fact]
    public void TheSameSeedBuildsTheSameGround()
    {
        var first = new LanderTerrain();
        var second = new LanderTerrain();
        var other = new LanderTerrain();
        var firstRandom = GameRandom.FromSeed(5);
        var secondRandom = GameRandom.FromSeed(5);
        var otherRandom = GameRandom.FromSeed(6);

        first.Generate(ref firstRandom, 3);
        second.Generate(ref secondRandom, 3);
        other.Generate(ref otherRandom, 3);

        Assert.Equal(first.Points.ToArray(), second.Points.ToArray());
        Assert.Equal(first.Pads.ToArray(), second.Pads.ToArray());
        Assert.NotEqual(first.Points.ToArray(), other.Points.ToArray());
    }

    [Fact]
    public void PadsAreFlatDistinctAndWideEnoughForTheLegs()
    {
        for (ulong seed = 1; seed <= 20; seed++)
        {
            for (var level = 1; level <= 8; level++)
            {
                var terrain = new LanderTerrain();
                var random = GameRandom.FromSeed(seed);
                terrain.Generate(ref random, level);
                var pads = terrain.Pads;

                Assert.Equal(LanderTerrain.PadCountFor(level), pads.Length);
                var seen = 0;
                for (var index = 0; index < pads.Length; index++)
                {
                    var pad = pads[index];
                    Assert.InRange(pad.Multiplier, 2, 5);
                    Assert.Equal(0, seen & (1 << pad.Multiplier));
                    seen |= 1 << pad.Multiplier;
                    Assert.True(pad.Width > LanderBoard.FootSpread * 2f + 0.15f);
                    for (var sample = 0; sample <= 10; sample++)
                    {
                        var probe = pad.Left + 0.01f + (pad.Width - 0.02f) * sample / 10f;
                        Assert.Equal(pad.Y, terrain.GroundY(probe), 3);
                    }

                    if (index > 0)
                    {
                        Assert.True(pad.Left > pads[index - 1].Right);
                    }
                }

                Assert.NotEqual(0, seen & (1 << 5));
            }
        }
    }

    [Fact]
    public void RidgesRiseBetweenThePads()
    {
        var rugged = 0;
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var terrain = new LanderTerrain();
            var random = GameRandom.FromSeed(seed);
            terrain.Generate(ref random, 1);
            var pads = terrain.Pads;
            for (var index = 1; index < pads.Length; index++)
            {
                var peak = terrain.PeakBetween(pads[index - 1].Right + 0.01f, pads[index].Left - 0.01f);
                if (peak < MathF.Min(pads[index - 1].Y, pads[index].Y) - 2.5f)
                {
                    rugged++;
                }
            }
        }

        Assert.True(rugged >= 40);
    }

    [Fact]
    public void ASoftTouchdownOnAPadLandsAndScoresItsMultiplier()
    {
        var board = Fresh(7);
        var pad = WidestPad(board);
        var spot = board.Terrain.Pads[pad];

        PlaceAbove(board, pad, new Vector2(0f, 1f), 0f);
        board.Step(Frame, 0, false);

        Assert.True(board.LandedThisStep);
        Assert.Equal(LanderState.Landed, board.State);
        Assert.Equal(1, board.Landings);
        Assert.Equal(board.LastLandingPoints, board.Score);
        Assert.InRange(board.LastLandingPoints, LanderBoard.PointsFor(1.05f, board.FuelCapacity, spot.Multiplier, false),
            LanderBoard.PointsFor(1f, board.FuelCapacity, spot.Multiplier, false));
    }

    [Theory]
    [InlineData(1f, 0f, 0f, (int)LanderCrash.None)]
    [InlineData(1.7f, 0.9f, 0.15f, (int)LanderCrash.None)]
    [InlineData(2.4f, 0f, 0f, (int)LanderCrash.TooFast)]
    [InlineData(1f, 1.5f, 0f, (int)LanderCrash.TooFast)]
    [InlineData(1f, 0f, 0.3f, (int)LanderCrash.TooSteep)]
    [InlineData(1f, 0f, -0.3f, (int)LanderCrash.TooSteep)]
    public void LandingLimitsDecideBetweenATouchdownAndACrash(float descent, float drift, float tilt,
        int expectedCrash)
    {
        var expected = (LanderCrash)expectedCrash;
        var board = Fresh(8);
        var pad = WidestPad(board);

        PlaceAbove(board, pad, new Vector2(drift, descent), tilt);
        board.Step(Frame, 0, false);

        Assert.Equal(expected, board.CrashThisStep);
        Assert.Equal(expected == LanderCrash.None ? LanderState.Landed : LanderState.Crashed, board.State);
    }

    [Fact]
    public void TouchingDownOffThePadCrashes()
    {
        var board = Fresh(9);
        var pads = board.Terrain.Pads;
        var gap = (pads[0].Right + pads[1].Left) * 0.5f;
        var ground = board.Terrain.GroundY(gap);
        board.Place(new Vector2(gap, ground - LanderBoard.FootDrop - 1.5f), new Vector2(0f, 1f), 0f);

        var crashed = RunUntil(board, 0, false, () => board.State != LanderState.Flying, 3f);

        Assert.True(crashed);
        Assert.Equal(LanderState.Crashed, board.State);
        Assert.Equal(LanderBoard.StartLives - 1, board.Lives);
    }

    [Fact]
    public void FuelBurnsOnlyWhileTheEngineFires()
    {
        var board = Fresh(10);
        board.Place(new Vector2(24f, 2f), Vector2.Zero, 0f);
        var full = board.Fuel;

        Hold(board, 0, false, 1f);
        Assert.Equal(full, board.Fuel);

        board.Place(new Vector2(24f, 2f), Vector2.Zero, 0f);
        Hold(board, 0, true, 1f);
        Assert.InRange(full - board.Fuel, 0.97f, 1.03f);
    }

    [Fact]
    public void ThrustPushesAgainstGravityAlongTheNose()
    {
        var board = Fresh(11);
        board.Place(new Vector2(24f, 0f), Vector2.Zero, 0f);
        Hold(board, 0, true, 1f);
        Assert.InRange(board.Velocity.Y, LanderBoard.Gravity - LanderBoard.Thrust - 0.05f,
            LanderBoard.Gravity - LanderBoard.Thrust + 0.05f);
        Assert.InRange(board.Velocity.X, -0.01f, 0.01f);

        board.Place(new Vector2(24f, 0f), Vector2.Zero, 0.5f);
        Hold(board, 0, true, 0.5f);
        Assert.True(board.Velocity.X > 0.5f);
    }

    [Fact]
    public void AnEmptyTankCannotFireTheEngine()
    {
        var board = Fresh(12);
        board.Place(new Vector2(24f, 0f), Vector2.Zero, 0f);
        board.SetFuel(0f);

        Hold(board, 0, true, 1f);

        Assert.False(board.Thrusting);
        Assert.InRange(board.Velocity.Y, LanderBoard.Gravity - 0.05f, LanderBoard.Gravity + 0.05f);
    }

    [Fact]
    public void TheEngineRunsDryAndWarnsOnTheWay()
    {
        var board = Fresh(13);
        board.Place(new Vector2(24f, 2f), Vector2.Zero, 0f);
        board.SetFuel(board.FuelCapacity * 0.25f);
        var warned = false;
        var empty = false;
        for (var frame = 0; frame < 60 * 8; frame++)
        {
            board.Place(new Vector2(24f, 2f), Vector2.Zero, 0f);
            board.Step(Frame, 0, true);
            warned |= board.FuelLowThisStep;
            empty |= board.FuelEmptyThisStep;
        }

        Assert.True(warned);
        Assert.True(empty);
        Assert.Equal(0f, board.Fuel);
    }

    [Fact]
    public void NarrowPadsPayMore()
    {
        Assert.Equal(LanderBoard.PointsFor(1f, 5f, 2, false) * 5, LanderBoard.PointsFor(1f, 5f, 5, false) * 2);
        Assert.Equal(LanderBoard.PointsFor(0.4f, 5f, 3, false) * 3 / 2, LanderBoard.PointsFor(0.4f, 5f, 3, true));
        Assert.True(LanderBoard.PointsFor(0.5f, 5f, 3, false) > LanderBoard.PointsFor(1.5f, 5f, 3, false));
        Assert.True(LanderBoard.PointsFor(1f, 9f, 3, false) > LanderBoard.PointsFor(1f, 2f, 3, false));

        var doubled = Fresh(14);
        var fived = Fresh(14);
        PlaceAbove(doubled, PadWith(doubled, 2), new Vector2(0f, 1f), 0f);
        PlaceAbove(fived, PadWith(fived, 5), new Vector2(0f, 1f), 0f);
        doubled.Step(Frame, 0, false);
        fived.Step(Frame, 0, false);

        Assert.Equal(LanderState.Landed, doubled.State);
        Assert.Equal(LanderState.Landed, fived.State);
        Assert.Equal(doubled.LastLandingPoints * 5, fived.LastLandingPoints * 2);
    }

    [Fact]
    public void ALandingAdvancesToAHarderLevelWithLessFuel()
    {
        var board = Fresh(15);
        var version = board.Terrain.Version;
        var capacity = board.FuelCapacity;
        PlaceAbove(board, WidestPad(board), new Vector2(0f, 0.8f), 0f);

        RunUntil(board, 0, false, () => board.Level == 2, LanderBoard.LandedSeconds + 0.5f);

        Assert.Equal(2, board.Level);
        Assert.Equal(LanderState.Flying, board.State);
        Assert.Equal(version + 1, board.Terrain.Version);
        Assert.True(board.FuelCapacity < capacity);
        Assert.Equal(board.FuelCapacity, board.Fuel);
        Assert.Equal(LanderBoard.StartLives, board.Lives);
    }

    [Fact]
    public void ACrashCostsALanderAndRetriesTheSameGround()
    {
        var board = Fresh(16);
        var version = board.Terrain.Version;
        var spawn = board.Position;
        PlaceAbove(board, WidestPad(board), new Vector2(0f, 3f), 0f);
        board.Step(Frame, 0, false);
        Assert.Equal(LanderCrash.TooFast, board.CrashThisStep);

        RunUntil(board, 0, false, () => board.State == LanderState.Flying, LanderBoard.CrashSeconds + 0.5f);

        Assert.Equal(LanderBoard.StartLives - 1, board.Lives);
        Assert.Equal(version, board.Terrain.Version);
        Assert.Equal(board.FuelCapacity, board.Fuel);
        Assert.InRange(board.Position.X, spawn.X - 0.1f, spawn.X + 0.1f);
        Assert.Equal(1, board.Level);
    }

    [Fact]
    public void TheLastCrashEndsTheRun()
    {
        var board = Fresh(17);
        for (var life = 0; life < LanderBoard.StartLives; life++)
        {
            PlaceAbove(board, WidestPad(board), new Vector2(0f, 3f), 0f);
            board.Step(Frame, 0, false);
            RunUntil(board, 0, false, () => board.State != LanderState.Crashed, LanderBoard.CrashSeconds + 0.5f);
        }

        Assert.Equal(0, board.Lives);
        Assert.Equal(LanderState.Over, board.State);
    }

    [Fact]
    public void LevelsTightenFuelPadsAndDrift()
    {
        Assert.True(LanderBoard.FuelFor(1) > LanderBoard.FuelFor(5));
        Assert.Equal(LanderBoard.MinFuel, LanderBoard.FuelFor(40));
        Assert.True(LanderTerrain.PadWidth(5, 1) > LanderTerrain.PadWidth(5, 6));
        Assert.True(LanderTerrain.PadWidth(2, 1) > LanderTerrain.PadWidth(5, 1));
        Assert.True(LanderBoard.StartDrift(6) > LanderBoard.StartDrift(1));
        Assert.True(LanderTerrain.RoughnessFor(6) > LanderTerrain.RoughnessFor(1));
    }

    [Fact]
    public void TheAutopilotLandsTheFirstLevel()
    {
        for (ulong seed = 1; seed <= 8; seed++)
        {
            var board = Fresh(seed);
            var pilot = LanderPilot.Create();
            for (var frame = 0; frame < 60 * 60 && board.Landings == 0; frame++)
            {
                pilot.Decide(board, out var rotate, out var thrust);
                board.Step(Frame, rotate, thrust);
            }

            Assert.Equal(1, board.Landings);
            Assert.Equal(LanderBoard.StartLives, board.Lives);
        }
    }

    private static LanderBoard Fresh(ulong seed)
    {
        var board = new LanderBoard();
        board.Reset(GameRandom.FromSeed(seed));
        return board;
    }

    private static int WidestPad(LanderBoard board)
    {
        var pads = board.Terrain.Pads;
        var best = 0;
        for (var index = 1; index < pads.Length; index++)
        {
            if (pads[index].Width > pads[best].Width)
            {
                best = index;
            }
        }

        return best;
    }

    private static int PadWith(LanderBoard board, int multiplier)
    {
        var pads = board.Terrain.Pads;
        for (var index = 0; index < pads.Length; index++)
        {
            if (pads[index].Multiplier == multiplier)
            {
                return index;
            }
        }

        return -1;
    }

    private static void PlaceAbove(LanderBoard board, int pad, Vector2 velocity, float angle)
    {
        var spot = board.Terrain.Pads[pad];
        board.Place(new Vector2(spot.Center, 0f), velocity, angle);
        var left = board.ToWorld(new Vector2(-LanderBoard.FootSpread, LanderBoard.FootDrop));
        var right = board.ToWorld(new Vector2(LanderBoard.FootSpread, LanderBoard.FootDrop));
        var lowest = MathF.Max(left.Y, right.Y);
        board.Place(new Vector2(spot.Center, spot.Y - lowest - 0.005f), velocity, angle);
    }

    private static void Hold(LanderBoard board, int rotate, bool thrust, float seconds)
    {
        var frames = (int)MathF.Round(seconds / Frame);
        for (var frame = 0; frame < frames; frame++)
        {
            board.Step(Frame, rotate, thrust);
        }
    }

    private static bool RunUntil(LanderBoard board, int rotate, bool thrust, Func<bool> done, float seconds)
    {
        var frames = (int)MathF.Ceiling(seconds / Frame);
        for (var frame = 0; frame < frames; frame++)
        {
            board.Step(Frame, rotate, thrust);
            if (done())
            {
                return true;
            }
        }

        return done();
    }

    private static LanderBoard Fly(ulong seed, out string trace)
    {
        var board = Fresh(seed);
        var pilot = LanderPilot.Create();
        var builder = new StringBuilder();
        for (var frame = 0; frame < 60 * 60; frame++)
        {
            pilot.Decide(board, out var rotate, out var thrust);
            board.Step(Frame, rotate, thrust);
            if (frame % 30 != 0)
            {
                continue;
            }

            builder.Append(board.Position.X.ToString("0.000", CultureInfo.InvariantCulture)).Append(',')
                .Append(board.Position.Y.ToString("0.000", CultureInfo.InvariantCulture)).Append(',')
                .Append(board.Fuel.ToString("0.000", CultureInfo.InvariantCulture)).Append(',')
                .Append(board.Score).Append(';');
        }

        trace = builder.ToString();
        return board;
    }
}
