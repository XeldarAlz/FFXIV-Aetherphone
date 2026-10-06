using System.Numerics;
using System.Text;
using Aetherphone.Apps.Games.Swoop;
using Xunit;

namespace Aetherphone.Tests;

public sealed class SwoopBoardTests
{
    private const uint Seed = 20261003u;
    private const float FrameSeconds = 1f / 60f;

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = Play(Seed, out var firstTrace);
        var second = Play(Seed, out var secondTrace);
        Play(Seed + 1u, out var otherTrace);

        Assert.Equal(firstTrace, secondTrace);
        Assert.Equal(first.X, second.X);
        Assert.Equal(first.Y, second.Y);
        Assert.Equal(first.Score, second.Score);
        Assert.Equal(first.IslandsReached, second.IslandsReached);
        Assert.NotEqual(firstTrace, otherTrace);
    }

    private static SwoopBoard Play(uint seed, out string trace)
    {
        var board = new SwoopBoard();
        board.Start(seed);
        var builder = new StringBuilder();
        for (var frame = 0; frame < 60 * 45 && !board.GameOver; frame++)
        {
            board.Tick(FrameSeconds, board.Terrain.Slope(board.X) < 0.0);
            if (board.SmoothThisTick || board.ThudThisTick || board.LaunchedThisTick || board.PickupsThisTick > 0)
            {
                builder.Append(board.SmoothThisTick ? 'S' : board.ThudThisTick ? 'T' : board.LaunchedThisTick ? 'L' : 'P')
                    .Append(board.X.ToString("F2")).Append(' ');
            }
        }

        builder.Append('|').Append(board.Distance).Append('|').Append(board.CrystalCount);
        trace = builder.ToString();
        return board;
    }

    [Fact]
    public void TerrainSlopeIsContinuousAcrossIslandBoundaries()
    {
        var terrain = new SwoopTerrain();
        terrain.Generate(Seed);
        const double epsilon = 1e-4;
        for (var island = 0; island < 12; island++)
        {
            var boundary = terrain.IslandStart(island);
            var probes = new[]
            {
                boundary, boundary + SwoopTerrain.ShoreLength, terrain.IslandEnd(island) - SwoopTerrain.ShoreLength,
                boundary + SwoopTerrain.IslandLength(island) * 0.37,
            };
            for (var probeIndex = 0; probeIndex < probes.Length; probeIndex++)
            {
                var x = probes[probeIndex];
                var left = terrain.Slope(x - epsilon);
                var right = terrain.Slope(x + epsilon);
                Assert.True(Math.Abs(left - right) < 1e-3, $"slope jumps at {x}: {left} vs {right}");
                Assert.True(Math.Abs(terrain.Height(x - epsilon) - terrain.Height(x + epsilon)) < 1e-3);
            }
        }
    }

    [Fact]
    public void AnalyticSlopeMatchesFiniteDifferenceEverywhere()
    {
        var terrain = new SwoopTerrain();
        terrain.Generate(Seed);
        const double epsilon = 1e-4;
        var end = terrain.IslandStart(8);
        for (var x = -20.0; x < end; x += 3.7)
        {
            var finite = (terrain.Height(x + epsilon) - terrain.Height(x - epsilon)) / (2.0 * epsilon);
            Assert.True(Math.Abs(finite - terrain.Slope(x)) < 1e-3, $"slope mismatch at {x}");
        }
    }

    [Fact]
    public void RingBufferSamplesMatchTheTerrainAfterWrapping()
    {
        var board = new SwoopBoard();
        board.Start(Seed);
        for (var hop = 1; hop <= 6; hop++)
        {
            board.PlaceOnGround(hop * 173.0, 12f);
            board.Tick(FrameSeconds, false);
            for (var sample = board.FirstSample; sample < board.EndSample; sample++)
            {
                var expected = (float)board.Terrain.Height(SwoopBoard.SampleX(sample));
                Assert.Equal(expected, board.SampleHeight(sample), 3);
            }
        }
    }

    [Fact]
    public void TheSameSeedAndInputsReplayIdentically()
    {
        var first = new SwoopBoard();
        var second = new SwoopBoard();
        first.Start(Seed);
        second.Start(Seed);
        for (var frame = 0; frame < 1800; frame++)
        {
            var holding = frame % 90 < 40;
            first.Tick(FrameSeconds, holding);
            second.Tick(FrameSeconds, holding);
        }

        Assert.Equal(first.X, second.X);
        Assert.Equal(first.Y, second.Y);
        Assert.Equal(first.Speed, second.Speed);
        Assert.Equal(first.Score, second.Score);
        Assert.Equal(first.CrystalCount, second.CrystalCount);
    }

    [Fact]
    public void DivingDownASlopeBuildsMoreSpeedThanCoasting()
    {
        var probe = new SwoopBoard();
        probe.Start(Seed);
        FindDownslope(probe.Terrain, out var crest, out var valley);
        var diving = new SwoopBoard();
        var coasting = new SwoopBoard();
        diving.Start(Seed);
        coasting.Start(Seed);
        diving.PlaceOnGround(crest, 8f);
        coasting.PlaceOnGround(crest, 8f);
        RunUntil(diving, valley, true);
        RunUntil(coasting, valley, false);
        Assert.True(diving.Grounded);
        Assert.True(coasting.Grounded);
        Assert.True(diving.Speed > coasting.Speed + 1f, $"diving {diving.Speed} vs coasting {coasting.Speed}");
    }

    [Fact]
    public void ClassifierCallsParallelLandingsSmoothAndSteepOnesThuds()
    {
        Assert.Equal(SwoopLanding.Smooth, SwoopBoard.Classify(new Vector2(10f, -5f), -0.5f));
        Assert.Equal(SwoopLanding.Thud, SwoopBoard.Classify(new Vector2(10f, -10f), 0.4f));
        Assert.Equal(SwoopLanding.Thud, SwoopBoard.Classify(new Vector2(8f, -14f), -0.3f));
    }

    [Fact]
    public void AParallelLandingKeepsSpeedAndASteepLandingLosesIt()
    {
        var board = new SwoopBoard();
        board.Start(Seed);
        var x = FindSlope(board.Terrain, -0.6, -0.3);
        var slope = (float)board.Terrain.Slope(x);
        var parallel = Vector2.Normalize(new Vector2(1f, slope)) * 20f;
        board.PlaceInAir(x, 0.05f, parallel, 1f);
        LandOnce(board);
        Assert.True(board.SmoothThisTick);
        Assert.True(board.Speed >= 19f, $"smooth landing kept only {board.Speed}");

        board.PlaceInAir(x, 0.05f, new Vector2(8f, -14f), 1f);
        LandOnce(board);
        Assert.True(board.ThudThisTick);
        Assert.True(board.Speed < 10f, $"thud kept {board.Speed}");
    }

    [Fact]
    public void ThreeSmoothLandingsStartFeverAndAThudEndsIt()
    {
        var board = new SwoopBoard();
        board.Start(Seed);
        var x = FindSlope(board.Terrain, -0.6, -0.3);
        var slope = (float)board.Terrain.Slope(x);
        var parallel = Vector2.Normalize(new Vector2(1f, slope)) * 20f;
        for (var landing = 0; landing < SwoopBoard.FeverStreak; landing++)
        {
            Assert.False(board.Fever);
            board.PlaceInAir(x, 0.05f, parallel, 1f);
            LandOnce(board);
            Assert.True(board.SmoothThisTick);
        }

        Assert.True(board.Fever);
        Assert.Equal(2, board.Multiplier);
        board.PlaceInAir(x, 0.05f, new Vector2(8f, -14f), 1f);
        LandOnce(board);
        Assert.True(board.ThudThisTick);
        Assert.False(board.Fever);
        Assert.Equal(0, board.SmoothStreak);
    }

    [Fact]
    public void ReachingTheNextIslandExtendsTheDay()
    {
        var board = new SwoopBoard();
        board.Start(Seed);
        board.SetClock(30f);
        board.PlaceOnGround(board.Terrain.IslandStart(1) - 2.0, 20f);
        var reached = false;
        for (var frame = 0; frame < 60 && !reached; frame++)
        {
            board.Tick(FrameSeconds, false);
            reached = board.IslandReachedThisTick;
        }

        Assert.True(reached);
        Assert.Equal(1, board.IslandsReached);
        Assert.Equal(1, board.CurrentIsland);
        Assert.True(board.Clock > 30f + SwoopBoard.IslandBonusSeconds - 1.5f, $"clock {board.Clock}");
    }

    [Fact]
    public void TheRunEndsAfterNightfallOnceTheBirdStops()
    {
        var board = new SwoopBoard();
        board.Start(Seed);
        board.PlaceOnGround(200.0, 18f);
        board.SetClock(0.5f);
        var nightFell = false;
        var frames = 0;
        while (!board.GameOver && frames < 60 * 40)
        {
            board.Tick(FrameSeconds, true);
            nightFell |= board.NightFellThisTick;
            frames++;
        }

        Assert.True(nightFell);
        Assert.True(board.Night);
        Assert.True(board.GameOver);
        Assert.Equal(0f, board.Speed);
        Assert.True(board.EndedThisTick);
        var score = board.Score;
        board.Tick(1f, true);
        Assert.Equal(score, board.Score);
        Assert.False(board.EndedThisTick);
        Assert.False(board.ThudThisTick);
        Assert.False(board.SmoothThisTick);
        Assert.Equal(0, board.PickupsThisTick);
    }

    [Fact]
    public void ScoreCountsDistanceAndCrystals()
    {
        var board = new SwoopBoard();
        board.Start(Seed);
        for (var frame = 0; frame < 600; frame++)
        {
            board.Tick(FrameSeconds, false);
        }

        Assert.True(board.Distance > 50);
        Assert.True(board.Score >= board.Distance);
    }

    [Fact]
    public void SkilledTimingClearlyBeatsAlwaysHoldingAndNeverHolding()
    {
        var skilled = RunPolicy(Policy.Skilled, out var skilledIslands);
        var always = RunPolicy(Policy.AlwaysHold, out _);
        var never = RunPolicy(Policy.NeverHold, out _);
        Assert.True(skilled > always * 1.4, $"skilled {skilled} vs always {always}");
        Assert.True(skilled > never * 1.4, $"skilled {skilled} vs never {never}");
        Assert.True(skilledIslands >= 2.0, $"skilled reached {skilledIslands} islands on average");
    }

    [Fact]
    public void HoldingThroughACrestNeverLaunches()
    {
        var held = new SwoopBoard();
        var released = new SwoopBoard();
        held.Start(Seed);
        released.Start(Seed);
        FindDownslope(held.Terrain, out var crest, out _);
        held.PlaceOnGround(crest - 6.0, 30f);
        released.PlaceOnGround(crest - 6.0, 30f);
        var heldLaunched = false;
        var releasedLaunched = false;
        for (var frame = 0; frame < 40; frame++)
        {
            held.Tick(FrameSeconds, true);
            released.Tick(FrameSeconds, false);
            heldLaunched |= held.LaunchedThisTick;
            releasedLaunched |= released.LaunchedThisTick;
        }

        Assert.False(heldLaunched);
        Assert.True(releasedLaunched);
    }

    [Fact]
    public void HoldingIntoAHardUpturnThuds()
    {
        var board = new SwoopBoard();
        board.Start(Seed);
        FindDownslope(board.Terrain, out var crest, out var valley);
        board.PlaceOnGround((crest + valley) * 0.5, 30f);
        var thudded = false;
        for (var frame = 0; frame < 240 && !thudded; frame++)
        {
            board.Tick(FrameSeconds, true);
            thudded = board.ThudThisTick;
        }

        Assert.True(thudded);
        Assert.True(board.Grounded);
    }

    private enum Policy
    {
        Skilled,
        AlwaysHold,
        NeverHold,
    }

    private static double RunPolicy(Policy policy, out double averageIslands)
    {
        const int seedCount = 10;
        var totalDistance = 0.0;
        var totalIslands = 0.0;
        for (var seedIndex = 1; seedIndex <= seedCount; seedIndex++)
        {
            var board = new SwoopBoard();
            board.Start((uint)seedIndex * 7919u);
            var frames = 0;
            while (!board.GameOver && frames < 60 * 600)
            {
                var holding = policy switch
                {
                    Policy.AlwaysHold => true,
                    Policy.NeverHold => false,
                    _ => board.Terrain.Slope(board.X) < 0.0,
                };
                board.Tick(FrameSeconds, holding);
                frames++;
            }

            Assert.True(board.GameOver);
            totalDistance += board.Distance;
            totalIslands += board.IslandsReached;
        }

        averageIslands = totalIslands / seedCount;
        return totalDistance / seedCount;
    }

    private static void FindDownslope(SwoopTerrain terrain, out double crest, out double valley)
    {
        var start = terrain.IslandStart(0) + SwoopTerrain.ShoreLength + 20.0;
        var x = start;
        while (!(terrain.Slope(x) > 0.0 && terrain.Slope(x + 0.5) <= 0.0))
        {
            x += 0.5;
        }

        crest = x + 0.6;
        x = crest;
        while (!(terrain.Slope(x) < 0.0 && terrain.Slope(x + 0.5) >= 0.0))
        {
            x += 0.5;
        }

        valley = x;
    }

    private static double FindSlope(SwoopTerrain terrain, double minimum, double maximum)
    {
        var x = terrain.IslandStart(0) + SwoopTerrain.ShoreLength;
        while (true)
        {
            terrain.Evaluate(x, out _, out var slope, out var curvature);
            if (slope >= minimum && slope <= maximum && curvature > 0.02)
            {
                return x;
            }

            x += 0.25;
        }
    }

    private static void RunUntil(SwoopBoard board, double x, bool holding)
    {
        var frames = 0;
        while (board.X < x && frames < 600)
        {
            board.Tick(FrameSeconds, holding);
            frames++;
        }
    }

    private static void LandOnce(SwoopBoard board)
    {
        for (var frame = 0; frame < 30; frame++)
        {
            board.Tick(FrameSeconds, false);
            if (board.SmoothThisTick || board.ThudThisTick)
            {
                return;
            }
        }
    }
}
