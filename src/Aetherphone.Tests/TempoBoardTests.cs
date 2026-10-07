using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Tempo;
using Xunit;

namespace Aetherphone.Tests;

public sealed class TempoBoardTests
{
    private const string Flat = "22222222222222222222222222222222";
    private const string None = "................................";

    private static readonly int[][] RecordedJumpFrames =
    {
        new[] { 236, 356, 506, 596, 676, 1223, 1286, 1537, 1706, 1996, 2086, 2456, 2576 },
        new[] { 203, 266, 562, 712, 836, 1031, 1096, 1181, 1361, 1466, 1706, 1996, 2096, 2176, 2423, 2486 },
        new[] { 194, 279, 575, 674, 1083, 1535, 1846, 1974, 2029, 2100, 2255, 2344 },
    };

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = Drive(TempoLevels.Get(4), 1234, 77);
        var second = Drive(TempoLevels.Get(4), 1234, 77);
        var other = Drive(TempoLevels.Get(4), 99, 77);

        Assert.Equal(first.Runner.X, second.Runner.X);
        Assert.Equal(first.Runner.Y, second.Runner.Y);
        Assert.Equal(first.Runner.Tick, second.Runner.Tick);
        Assert.Equal(first.Attempts, second.Attempts);
        Assert.Equal(first.Deaths, second.Deaths);
        Assert.Equal(first.ShatterSeed, second.ShatterSeed);
        Assert.True(first.Deaths > 0);
        Assert.Equal(first.Runner.X, other.Runner.X);
        Assert.NotEqual(first.ShatterSeed, other.ShatterSeed);
    }

    [Fact]
    public void EveryLevelParses()
    {
        Assert.Equal(20, TempoLevels.Count);
        for (var number = 1; number <= TempoLevels.Count; number++)
        {
            var level = TempoLevels.Get(number);
            Assert.False(level.Malformed, $"level {number} is malformed");
            Assert.Equal(TempoLevel.CoinCount, level.CoinTotal);
            Assert.Equal(0, level.Length % 32);
            Assert.True(level.Seconds is > 15f and < 40f, $"level {number} lasts {level.Seconds:F1} s");
            Assert.True(level.Length / level.Speed / TempoPhysics.TickSeconds < TempoAutopilot.DefaultMaxTicks);
            for (var column = 0; column < 12; column++)
            {
                Assert.Equal(TempoItem.None, level.Item(column));
                Assert.NotEqual(TempoLevel.Pit, level.Ground(column));
            }

            for (var column = 0; column < level.Length; column++)
            {
                var item = level.Item(column);
                if (item is TempoItem.Spike or TempoItem.Pad)
                {
                    Assert.NotEqual(TempoLevel.Pit, level.Ground(column));
                }

                if (item is TempoItem.CeilingSpike or TempoItem.CeilingPad or TempoItem.GravityUp)
                {
                    Assert.NotEqual(TempoLevel.Open, level.Ceiling(column));
                }
            }
        }
    }

    [Fact]
    public void EveryLevelCanBeClearedWithAllThreeCoins()
    {
        var pilot = new TempoAutopilot(TempoAutopilot.DefaultMaxTicks, true);
        var plan = new TempoPlan(256);
        for (var number = 1; number <= TempoLevels.Count; number++)
        {
            var level = TempoLevels.Get(number);
            Assert.True(pilot.Plan(level, plan), $"level {number} cannot be cleared");
            Assert.Equal(TempoLevel.CoinCount, pilot.PlannedCoins);
            var board = Replay(level, plan);
            Assert.True(board.Complete, $"level {number} replay did not finish");
            Assert.Equal(1, board.Attempts);
            Assert.Equal(TempoLevel.CoinCount, board.Runner.CoinsCollected);
        }
    }

    [Fact]
    public void AutopilotClearsTheFirstThreeLevelsWithTheRecordedJumps()
    {
        var pilot = new TempoAutopilot();
        var plan = new TempoPlan(256);
        for (var number = 1; number <= RecordedJumpFrames.Length; number++)
        {
            var level = TempoLevels.Get(number);
            Assert.True(pilot.Plan(level, plan));
            var frames = RecordedJumpFrames[number - 1];
            Assert.Equal(frames.Length, plan.Count);
            for (var index = 0; index < frames.Length; index++)
            {
                Assert.True(plan.IsPress(index));
                Assert.Equal(frames[index], plan.Start(index));
                Assert.Equal(frames[index] + 1, plan.End(index));
            }

            var board = new TempoBoard();
            board.Load(level, GameRandom.FromSeed(5), false);
            for (var tick = 0; tick < TempoAutopilot.DefaultMaxTicks && !board.Complete; tick++)
            {
                var next = board.Runner.Tick + 1;
                var pressed = Array.IndexOf(frames, next) >= 0;
                board.Tick(pressed, pressed);
                Assert.False(board.Dead, $"level {number} died at tick {next}");
            }

            Assert.True(board.Complete);
            Assert.Equal(frames.Length, board.Runner.Jumps);
        }
    }

    [Fact]
    public void AJumpRisesAboutTwoTilesAndTurnsAQuarter()
    {
        var level = new TempoLevel(120, 8f, Flat, None, None, None, Flat, None, None, None);
        var runner = TempoPhysics.Start(level);
        var ground = runner.Y;
        var signals = TempoPhysics.Tick(ref runner, level, true, true);
        Assert.True((signals & TempoSignal.Jumped) != 0);
        var peak = runner.Y;
        var ticks = 1;
        while (!runner.Grounded && ticks < 200)
        {
            TempoPhysics.Tick(ref runner, level, false, false);
            peak = MathF.Max(peak, runner.Y);
            ticks++;
        }

        var expectedTicks = 2f * TempoPhysics.JumpVelocity / TempoPhysics.Gravity / TempoPhysics.TickSeconds;
        Assert.InRange(peak - ground, 2.1f, 2.3f);
        Assert.InRange(ticks, (int)expectedTicks - 1, (int)expectedTicks + 2);
        Assert.Equal(ground, runner.Y);
        for (var settle = 0; settle < 20; settle++)
        {
            TempoPhysics.Tick(ref runner, level, false, false);
        }

        Assert.InRange(runner.Rotation, MathF.PI * 0.5f - 0.02f, MathF.PI * 0.5f + 0.02f);
    }

    [Fact]
    public void SpikesWallsAndPitsKillButJumpsClearThem()
    {
        var spike = new TempoLevel(120, 8f, Flat, None, "....................^...........", None);
        Assert.False(Survives(spike, -1));
        Assert.True(Survives(spike, 17.6f));

        var wall = new TempoLevel(120, 8f, "22222222222222222222333333333333", None, None, None);
        Assert.False(Survives(wall, -1));
        Assert.True(Survives(wall, 18f));

        var pit = new TempoLevel(120, 8f, "22222222222222222222___222222222", None, None, None);
        Assert.False(Survives(pit, -1));
        Assert.True(Survives(pit, 19f));
    }

    [Fact]
    public void PadsLaunchHigherThanJumpsAndHoldingCarriesFurther()
    {
        var level = new TempoLevel(120, 8f, Flat, None, "..........p.....................", None, Flat, None, None, None);
        var free = Flight(level, false, out var freePeak);
        var held = Flight(level, true, out var heldPeak);

        Assert.True(freePeak > 3.3f);
        Assert.True(heldPeak > freePeak + 1.5f);
        Assert.True(held > free + 20);
    }

    [Fact]
    public void GravityPortalsFlipTheCubeOntoTheCeiling()
    {
        var level = new TempoLevel(120, 8f, Flat, "88888888888888888888888888888888", "..........u.....................", None,
            Flat, "88888888888888888888888888888888", "..........d.....................", None);
        var runner = TempoPhysics.Start(level);
        var landedOnCeiling = false;
        var backOnGround = false;
        while (runner.Alive && !runner.Finished)
        {
            TempoPhysics.Tick(ref runner, level, false, false);
            if (runner.Grounded && runner.Gravity < 0)
            {
                landedOnCeiling = true;
                Assert.Equal(8f - TempoPhysics.Half, runner.Y, 3);
            }

            if (landedOnCeiling && runner.Grounded && runner.Gravity > 0)
            {
                backOnGround = true;
            }
        }

        Assert.True(runner.Finished);
        Assert.True(landedOnCeiling);
        Assert.True(backOnGround);
    }

    [Fact]
    public void CoinsAreCollectedOnlyWhenTouched()
    {
        var level = new TempoLevel(120, 8f, Flat, None, None, "..........2.........4.........2.", Flat, None, None, None);
        var runner = TempoPhysics.Start(level);
        while (runner.Alive && !runner.Finished)
        {
            TempoPhysics.Tick(ref runner, level, false, false);
        }

        Assert.Equal(2, runner.CoinsCollected);
        Assert.Equal(5, runner.Coins);
    }

    [Fact]
    public void PracticeRespawnsAtTheLastCheckpointAndCountsAttempts()
    {
        var level = new TempoLevel(120, 8f, Flat, None, None, None, Flat, None, "........^.......................", None);
        var board = new TempoBoard();
        board.Load(level, GameRandom.FromSeed(3), true);
        for (var tick = 0; tick < 240; tick++)
        {
            board.Tick(false, false);
        }

        Assert.True(board.PlaceCheckpoint());
        var checkpoint = board.Checkpoint(0);
        while (!board.Dead)
        {
            board.Tick(false, false);
        }

        for (var tick = 0; tick < TempoBoard.RespawnTicks; tick++)
        {
            board.Tick(false, false);
        }

        Assert.False(board.Dead);
        Assert.Equal(2, board.Attempts);
        Assert.Equal(checkpoint.X, board.Runner.X);
        Assert.True(board.RemoveCheckpoint());
        Assert.False(board.RemoveCheckpoint());

        var ranked = new TempoBoard();
        ranked.Load(level, GameRandom.FromSeed(3), false);
        Assert.False(ranked.PlaceCheckpoint());
    }

    [Fact]
    public void StarsRewardTheClearTheFirstTryAndTheCoins()
    {
        Assert.Equal(3, TempoApp.Stars(1, 3));
        Assert.Equal(2, TempoApp.Stars(1, 2));
        Assert.Equal(2, TempoApp.Stars(4, 3));
        Assert.Equal(1, TempoApp.Stars(4, 0));
    }

    private static TempoBoard Drive(TempoLevel level, ulong seed, ulong inputSeed)
    {
        var board = new TempoBoard();
        board.Load(level, GameRandom.FromSeed(seed), false);
        var input = GameRandom.FromSeed(inputSeed);
        for (var tick = 0; tick < 3000 && !board.Complete; tick++)
        {
            var press = input.Chance(0.02f);
            board.Tick(press, press);
        }

        return board;
    }

    private static TempoBoard Replay(TempoLevel level, TempoPlan plan)
    {
        var board = new TempoBoard();
        board.Load(level, GameRandom.FromSeed(1), false);
        for (var tick = 0; tick < TempoAutopilot.DefaultMaxTicks && !board.Complete && !board.Dead; tick++)
        {
            var next = board.Runner.Tick + 1;
            board.Tick(plan.Held(next), plan.Pressed(next));
        }

        return board;
    }

    private static bool Survives(TempoLevel level, float jumpAt)
    {
        var runner = TempoPhysics.Start(level);
        var jumped = false;
        while (runner.Alive && !runner.Finished)
        {
            var press = !jumped && jumpAt > 0f && runner.X >= jumpAt;
            jumped |= press;
            TempoPhysics.Tick(ref runner, level, press, press);
        }

        return runner.Finished;
    }

    private static int Flight(TempoLevel level, bool hold, out float peak)
    {
        var runner = TempoPhysics.Start(level);
        var ground = runner.Y;
        peak = 0f;
        var launched = -1;
        while (runner.Alive && !runner.Finished)
        {
            var rising = runner.PadFlight && runner.VelocityY > 0f;
            var signals = TempoPhysics.Tick(ref runner, level, hold && rising, false);
            if ((signals & TempoSignal.Pad) != 0)
            {
                launched = runner.Tick;
            }

            if (launched >= 0)
            {
                peak = MathF.Max(peak, runner.Y - ground);
            }

            if (launched >= 0 && runner.Grounded)
            {
                return runner.Tick - launched;
            }
        }

        return 0;
    }
}
