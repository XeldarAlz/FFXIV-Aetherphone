using System.Numerics;
using System.Text;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Thrust;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ThrustBoardTests
{
    private const float Frame = 1f / 60f;
    private const float Tick = 1f / 120f;

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = Drive(1234, 2400, out var firstTrace);
        var second = Drive(1234, 2400, out var secondTrace);
        Drive(99, 2400, out var otherTrace);

        Assert.Equal(firstTrace, secondTrace);
        Assert.Equal(first.Score, second.Score);
        Assert.Equal(first.X, second.X);
        Assert.Equal(first.Y, second.Y);
        Assert.Equal(first.State, second.State);
        Assert.NotEqual(firstTrace, otherTrace);
    }

    [Fact]
    public void TheGeneratorAlwaysLeavesAPassableGap()
    {
        var zappers = new ThrustZapper[ThrustGenerator.MaxZappers];
        var coins = new Vector2[ThrustGenerator.MaxCoins];
        var inflate = ThrustGenerator.ZapperRadius + ThrustGenerator.PlayerRadius;
        for (var seed = 1UL; seed <= 30UL; seed++)
        {
            var random = GameRandom.FromSeed(seed);
            var pathY = ThrustBoard.Height * 0.5f;
            for (var ordinal = 0; ordinal < 260; ordinal++)
            {
                var startX = ThrustBoard.RunwayLength + ordinal * ThrustGenerator.ChunkWidth;
                var count = ThrustGenerator.Generate(ref random, ordinal, startX, ref pathY, zappers, coins, out _, out _);
                for (var x = startX; x <= startX + ThrustGenerator.ChunkWidth; x += ThrustGenerator.ColumnStep)
                {
                    var gap = LongestFreeRun(zappers, count, x, inflate);
                    Assert.True(gap >= ThrustGenerator.MinGap - 0.11f,
                        $"seed {seed} chunk {ordinal} column {x:F2} leaves only {gap:F2}");
                }
            }
        }
    }

    [Fact]
    public void ObstaclesStayInsideTheirChunkWithClearRunways()
    {
        var zappers = new ThrustZapper[ThrustGenerator.MaxZappers];
        var coins = new Vector2[ThrustGenerator.MaxCoins];
        var random = GameRandom.FromSeed(77);
        var pathY = ThrustBoard.Height * 0.5f;
        var sawRotating = false;
        var sawGate = false;
        for (var ordinal = 0; ordinal < 400; ordinal++)
        {
            var startX = ThrustBoard.RunwayLength + ordinal * ThrustGenerator.ChunkWidth;
            var count = ThrustGenerator.Generate(ref random, ordinal, startX, ref pathY, zappers, coins, out var coinCount,
                out var pattern);
            if (ordinal == 0)
            {
                Assert.Equal(0, count);
            }

            sawRotating |= pattern == ThrustPattern.Spinner;
            sawGate |= pattern == ThrustPattern.Gate;
            for (var index = 0; index < count; index++)
            {
                Assert.True(zappers[index].Left >= startX + 1f, $"chunk {ordinal} starts too early");
                Assert.True(zappers[index].Right <= startX + ThrustGenerator.ChunkWidth - 1f, $"chunk {ordinal} ends too late");
            }

            for (var index = 0; index < coinCount; index++)
            {
                Assert.InRange(coins[index].Y, 0.5f, ThrustBoard.Height - 0.5f);
                for (var zapper = 0; zapper < count; zapper++)
                {
                    Assert.True(ThrustGenerator.SegmentDistance(coins[index], zappers[zapper], 0f) >
                                ThrustGenerator.ZapperRadius + ThrustGenerator.CoinRadius || zappers[zapper].Rotating);
                }
            }
        }

        Assert.True(sawRotating);
        Assert.True(sawGate);
    }

    [Fact]
    public void HoldingRisesWithAccelerationAndReleasingFalls()
    {
        var board = Fresh(3);
        var previous = board.VelocityY;
        for (var step = 0; step < 12; step++)
        {
            board.Step(Tick, true);
            Assert.True(board.VelocityY < previous);
            previous = board.VelocityY;
        }

        Assert.True(board.Y < ThrustBoard.Height - ThrustBoard.PlayerRadius);
        for (var step = 0; step < 60; step++)
        {
            board.Step(Tick, true);
        }

        Assert.Equal(-ThrustBoard.MaxRise, board.VelocityY, 3);
        previous = board.VelocityY;
        for (var step = 0; step < 12; step++)
        {
            board.Step(Tick, false);
            Assert.True(board.VelocityY > previous);
            previous = board.VelocityY;
        }
    }

    [Fact]
    public void TheCeilingAndFloorHoldThePilotInside()
    {
        var board = Fresh(4);
        for (var step = 0; step < 150; step++)
        {
            board.Step(Tick, true);
            Assert.True(board.Y >= ThrustBoard.PlayerRadius - 0.0001f);
        }

        Assert.Equal(ThrustBoard.PlayerRadius, board.Y, 3);
        var landed = false;
        for (var step = 0; step < 150 && !landed; step++)
        {
            board.Step(Tick, false);
            landed = board.LandedThisStep;
        }

        Assert.True(landed);
        Assert.True(board.Grounded);
        Assert.Equal(ThrustBoard.Height - ThrustBoard.PlayerRadius, board.Y, 3);
    }

    [Fact]
    public void AMissileWarnsAndLocksBeforeItFlies()
    {
        var board = Fresh(5);
        board.Step(Tick, false);
        board.QueueMissile();
        Assert.Equal(1, board.MissileCount);
        Assert.Equal(MissilePhase.Warning, board.MissileAt(0).Phase);

        var elapsed = 0f;
        var lockedY = float.NaN;
        while (board.MissileAt(0).Phase != MissilePhase.Flying)
        {
            var holding = elapsed < ThrustBoard.WarningSeconds * 0.5f;
            board.Step(Tick, holding);
            elapsed += Tick;
            if (board.MissileLockedThisStep)
            {
                lockedY = board.MissileAt(0).Y;
                Assert.Equal(ThrustBoard.WarningSeconds, elapsed, 1);
            }

            if (board.MissileAt(0).Phase == MissilePhase.Locked)
            {
                Assert.Equal(lockedY, board.MissileAt(0).Y);
            }

            Assert.True(elapsed < 3f);
        }

        Assert.True(board.MissileLaunchedThisStep);
        Assert.InRange(elapsed, ThrustBoard.WarningSeconds + ThrustBoard.LockSeconds - Tick * 2f,
            ThrustBoard.WarningSeconds + ThrustBoard.LockSeconds + Tick * 2f);
        Assert.Equal(lockedY, board.MissileAt(0).Y);
        var launchX = board.MissileAt(0).X;
        board.Step(Tick, false);
        Assert.True(board.MissileAt(0).X < launchX);
    }

    [Fact]
    public void TheWarningTracksThePilotUntilItLocks()
    {
        var board = Fresh(6);
        board.QueueMissile();
        var start = board.MissileAt(0).Y;
        for (var step = 0; step < 90; step++)
        {
            board.Step(Tick, true);
        }

        Assert.Equal(MissilePhase.Warning, board.MissileAt(0).Phase);
        Assert.True(board.MissileAt(0).Y < start - 1f);
        Assert.True(MathF.Abs(board.MissileAt(0).Y - board.Y) < 2.5f);
    }

    [Fact]
    public void TheChocoboAbsorbsOneHit()
    {
        var board = Fresh(7);
        board.Step(Tick, false);
        board.Mount();
        Assert.True(board.Mounted);

        Assert.True(board.TakeHit(ThrustDeath.Zapped));
        Assert.Equal(ThrustState.Running, board.State);
        Assert.False(board.Mounted);
        Assert.True(board.Invulnerable);
        Assert.True(board.DismountedThisStep);

        Assert.False(board.TakeHit(ThrustDeath.Zapped));
        Assert.Equal(ThrustState.Running, board.State);

        for (var step = 0; step < (int)(ThrustBoard.InvulnerableSeconds / Tick) + 2; step++)
        {
            board.Step(Tick, true);
        }

        Assert.False(board.Invulnerable);
        Assert.True(board.TakeHit(ThrustDeath.Blasted));
        Assert.Equal(ThrustState.Dying, board.State);
        Assert.Equal(ThrustDeath.Blasted, board.Death);
    }

    [Fact]
    public void TheChocoboJumpsTwiceButNotThreeTimes()
    {
        var board = Fresh(8);
        board.Step(Tick, false);
        board.Mount();
        board.Tap();
        board.Step(Tick, false);
        Assert.True(board.JumpedThisStep);
        Assert.False(board.Grounded);
        for (var step = 0; step < 20; step++)
        {
            board.Step(Tick, false);
        }

        board.Tap();
        board.Step(Tick, false);
        Assert.True(board.JumpedThisStep);
        board.Tap();
        board.Step(Tick, false);
        Assert.False(board.JumpedThisStep);
    }

    [Fact]
    public void CoinsAlongTheFlightPathBuildAChain()
    {
        var board = Fresh(9);
        var collected = 0;
        for (var frame = 0; frame < 60 * 30 && board.State == ThrustState.Running; frame++)
        {
            var hold = ThrustAutopilot.Hold(board, out var tap);
            if (tap)
            {
                board.Tap();
            }

            board.Step(Frame, hold);
            collected += board.CoinsThisStep;
        }

        Assert.True(collected > 10);
        Assert.Equal(collected, board.Coins);
        Assert.True(board.BestChain >= 4);
        Assert.True(board.Score >= board.Metres + collected * ThrustBoard.CoinPoints);
    }

    [Fact]
    public void ADroppedPilotEventuallyHitsSomething()
    {
        var board = Fresh(10);
        for (var frame = 0; frame < 60 * 400 && board.State != ThrustState.Over; frame++)
        {
            board.Step(Frame, false);
        }

        Assert.Equal(ThrustState.Over, board.State);
        Assert.NotEqual(ThrustDeath.None, board.Death);
    }

    [Fact]
    public void TheAutopilotFliesFarThroughTheGeneratedCourse()
    {
        var total = 0f;
        for (var seed = 1UL; seed <= 12UL; seed++)
        {
            var board = Fresh(seed);
            for (var frame = 0; frame < 60 * 400 && board.State == ThrustState.Running && board.Distance < 1500f; frame++)
            {
                var hold = ThrustAutopilot.Hold(board, out var tap);
                if (tap)
                {
                    board.Tap();
                }

                board.Step(Frame, hold);
            }

            Assert.True(board.Distance >= ThrustBoard.MissileStart, $"seed {seed} died ({board.Death}) at {board.Metres} m");
            total += board.Distance;
        }

        Assert.True(total / 12f >= 800f, $"the autopilot averaged {total / 12f:F0} m");
    }

    [Fact]
    public void SpeedRampsWithDistanceTowardTheCap()
    {
        Assert.Equal(ThrustBoard.BaseSpeed, ThrustBoard.RunSpeed(0f));
        var previous = 0f;
        for (var distance = 0f; distance < 5000f; distance += 250f)
        {
            var speed = ThrustBoard.RunSpeed(distance);
            Assert.True(speed > previous);
            Assert.True(speed < ThrustBoard.MaxSpeed);
            previous = speed;
        }
    }

    private static ThrustBoard Fresh(ulong seed)
    {
        var board = new ThrustBoard();
        board.Reset(GameRandom.FromSeed(seed));
        return board;
    }

    private static ThrustBoard Drive(ulong seed, int frames, out string trace)
    {
        var board = Fresh(seed);
        var builder = new StringBuilder();
        for (var frame = 0; frame < frames && board.State != ThrustState.Over; frame++)
        {
            var hold = ThrustAutopilot.Hold(board, out var tap);
            if (tap)
            {
                board.Tap();
            }

            board.Step(Frame, hold);
            if (frame % 30 == 0)
            {
                builder.Append(board.Score).Append(':').Append(board.Y.ToString("F3")).Append(':')
                    .Append(board.ZapperCount).Append(':').Append(board.MissileCount).Append(';');
            }
        }

        trace = builder.ToString();
        return board;
    }

    private static float LongestFreeRun(ThrustZapper[] zappers, int count, float x, float inflate)
    {
        const float step = 0.05f;
        var top = ThrustGenerator.PlayerRadius;
        var bottom = ThrustBoard.Height - ThrustGenerator.PlayerRadius;
        var best = 0f;
        var run = 0f;
        for (var y = top; y <= bottom; y += step)
        {
            var blocked = false;
            for (var index = 0; index < count && !blocked; index++)
            {
                var zapper = zappers[index];
                if (zapper.Rotating)
                {
                    blocked = Vector2.Distance(new Vector2(x, y), zapper.Center) <= zapper.HalfLength + inflate;
                    continue;
                }

                var direction = new Vector2(MathF.Cos(zapper.Angle), MathF.Sin(zapper.Angle));
                var start = zapper.Center - direction * zapper.HalfLength;
                var along = Math.Clamp(Vector2.Dot(new Vector2(x, y) - start, direction), 0f, zapper.HalfLength * 2f);
                blocked = Vector2.Distance(new Vector2(x, y), start + direction * along) <= inflate;
            }

            run = blocked ? 0f : run + step;
            best = MathF.Max(best, run);
        }

        return best;
    }
}
