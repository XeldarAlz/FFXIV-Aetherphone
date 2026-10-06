using System.Text;
using Aetherphone.Apps.Games.Updraft;
using Xunit;

namespace Aetherphone.Tests;

public sealed class UpdraftBoardTests
{
    private const float Frame = 1f / 60f;

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = Play(4321, out var firstTrace);
        var second = Play(4321, out var secondTrace);
        Play(8765, out var otherTrace);

        Assert.Equal(firstTrace, secondTrace);
        Assert.Equal(first.Score, second.Score);
        Assert.Equal(first.MaxHeight, second.MaxHeight);
        Assert.Equal(first.Crystals, second.Crystals);
        Assert.Equal(first.GameOver, second.GameOver);
        Assert.NotEqual(firstTrace, otherTrace);
    }

    private static UpdraftBoard Play(int seed, out string trace)
    {
        var board = new UpdraftBoard();
        board.StartGame(seed, 0f);
        board.Launch();
        var builder = new StringBuilder();
        for (var frame = 0; frame < 60 * 20 && !board.GameOver; frame++)
        {
            var axis = MathF.Sin(frame * 0.07f) > 0.3f ? 1f : MathF.Sin(frame * 0.07f) < -0.3f ? -1f : 0f;
            board.Tick(Frame, UpdraftInput.Keys(axis));
            var events = board.Events;
            for (var index = 0; index < events.Length; index++)
            {
                builder.Append((int)events[index].Kind).Append(':').Append(events[index].Value).Append(' ');
            }
        }

        builder.Append('|').Append(board.HeightMetres).Append('|').Append(board.ActiveCloudCount);
        trace = builder.ToString();
        return board;
    }

    private readonly struct PathCloud
    {
        public readonly float X;
        public readonly float Y;
        public readonly float HalfWidth;
        public readonly UpdraftCloudKind Kind;

        public PathCloud(in UpdraftCloud cloud)
        {
            X = cloud.X;
            Y = cloud.Y;
            HalfWidth = cloud.HalfWidth;
            Kind = cloud.Kind;
        }
    }

    private static List<UpdraftEventKind> Run(UpdraftBoard board, float seconds, UpdraftInput input)
    {
        var seen = new List<UpdraftEventKind>();
        var frames = (int)MathF.Round(seconds / Frame);
        for (var frame = 0; frame < frames; frame++)
        {
            board.Tick(Frame, input);
            var events = board.Events;
            for (var index = 0; index < events.Length; index++)
            {
                seen.Add(events[index].Kind);
            }

            if (board.GameOver)
            {
                break;
            }
        }

        return seen;
    }

    private static int Count(List<UpdraftEventKind> events, UpdraftEventKind kind)
    {
        var count = 0;
        for (var index = 0; index < events.Count; index++)
        {
            if (events[index] == kind)
            {
                count++;
            }
        }

        return count;
    }

    private static float ApexAbove(UpdraftCloudKind kind)
    {
        var board = new UpdraftBoard();
        board.StartEmpty();
        board.AddCloud(kind, 4.5f, 0f, 1f);
        board.PlaceBird(4.5f, 0.6f, 0f, 0f);
        var apex = 0f;
        for (var frame = 0; frame < 240; frame++)
        {
            board.Tick(Frame, UpdraftInput.Keys(0f));
            apex = MathF.Max(apex, board.BirdY);
        }

        return apex;
    }

    private static List<PathCloud> CollectClimb(int seed, float height, List<PathCloud>? storms = null)
    {
        var board = new UpdraftBoard();
        board.StartGame(seed, 0f);
        var path = new List<PathCloud>();
        var fresh = new List<PathCloud>();
        var lastPathY = float.NegativeInfinity;
        var lastStormY = float.NegativeInfinity;
        for (var camera = 0f; camera < height; camera += 1f)
        {
            board.AdvanceCamera(camera);
            fresh.Clear();
            var clouds = board.Clouds;
            var highestStorm = lastStormY;
            for (var index = 0; index < clouds.Length; index++)
            {
                ref readonly var cloud = ref clouds[index];
                if (!cloud.Active)
                {
                    continue;
                }

                if (cloud.OnPath && cloud.Y > lastPathY)
                {
                    fresh.Add(new PathCloud(cloud));
                }

                if (storms is not null && cloud.Kind == UpdraftCloudKind.Storm && cloud.Y > lastStormY)
                {
                    storms.Add(new PathCloud(cloud));
                    highestStorm = MathF.Max(highestStorm, cloud.Y);
                }
            }

            lastStormY = highestStorm;
            fresh.Sort((first, second) => first.Y.CompareTo(second.Y));
            for (var index = 0; index < fresh.Count; index++)
            {
                path.Add(fresh[index]);
                lastPathY = fresh[index].Y;
            }
        }

        return path;
    }

    [Fact]
    public void TheSameSeedAndInputsReplayTheSameClimb()
    {
        var first = new UpdraftBoard();
        var second = new UpdraftBoard();
        first.StartGame(1234, 40f);
        second.StartGame(1234, 40f);
        first.Launch();
        second.Launch();
        for (var frame = 0; frame < 900; frame++)
        {
            var axis = MathF.Sin(frame * 0.05f);
            first.Tick(Frame, UpdraftInput.Keys(axis));
            second.Tick(Frame, UpdraftInput.Keys(axis));
        }

        Assert.Equal(first.BirdX, second.BirdX);
        Assert.Equal(first.BirdY, second.BirdY);
        Assert.Equal(first.Score, second.Score);
        Assert.Equal(first.GameOver, second.GameOver);
        var firstClouds = first.Clouds;
        var secondClouds = second.Clouds;
        for (var index = 0; index < firstClouds.Length; index++)
        {
            Assert.Equal(firstClouds[index].Active, secondClouds[index].Active);
            Assert.Equal(firstClouds[index].X, secondClouds[index].X);
            Assert.Equal(firstClouds[index].Y, secondClouds[index].Y);
            Assert.Equal(firstClouds[index].Kind, secondClouds[index].Kind);
        }
    }

    [Fact]
    public void DifferentSeedsGenerateDifferentSkies()
    {
        var first = CollectClimb(1, 60f);
        var second = CollectClimb(2, 60f);
        var differs = false;
        for (var index = 1; index < Math.Min(first.Count, second.Count); index++)
        {
            differs |= first[index].X != second[index].X;
        }

        Assert.True(differs);
    }

    [Fact]
    public void AGapAtTheLargestStepCanStillBeCrossedFromAnywhereOnTheField()
    {
        Assert.True(UpdraftBoard.MaxPathGap < UpdraftBoard.Apex(UpdraftBoard.PlainBounce));
        Assert.True(UpdraftBoard.HorizontalReach(UpdraftBoard.MaxPathGap) + UpdraftBoard.MinHalfWidth >=
            UpdraftBoard.FieldWidth * 0.5f);
    }

    [Fact]
    public void EveryGeneratedStepIsReachableWithAPlainBounce()
    {
        var apex = UpdraftBoard.Apex(UpdraftBoard.PlainBounce);
        for (var seed = 1; seed <= 40; seed++)
        {
            var path = CollectClimb(seed, 2400f);
            Assert.True(path.Count > 900);
            Assert.True(path[^1].Y > 2400f);
            for (var index = 1; index < path.Count; index++)
            {
                var rise = path[index].Y - path[index - 1].Y;
                var across = MathF.Abs(UpdraftBoard.WrapDelta(path[index].X - path[index - 1].X));
                Assert.InRange(rise, 0.5f, UpdraftBoard.MaxPathGap);
                Assert.True(rise < apex);
                Assert.True(across <= UpdraftBoard.HorizontalReach(rise) + path[index].HalfWidth,
                    $"seed {seed} step {index}: {across} across a {rise} rise");
            }
        }
    }

    [Fact]
    public void StormsNeverSitOnTheRouteBetweenTwoPathClouds()
    {
        var stormsSeen = 0;
        for (var seed = 1; seed <= 30; seed++)
        {
            var storms = new List<PathCloud>();
            var path = CollectClimb(seed, 1600f, storms);
            stormsSeen += storms.Count;
            for (var stormIndex = 0; stormIndex < storms.Count; stormIndex++)
            {
                var storm = storms[stormIndex];
                var floor = storm.Y - UpdraftBoard.StormBottom - UpdraftBoard.BirdRadius;
                var ceiling = storm.Y + UpdraftBoard.StormTop + UpdraftBoard.BirdRadius;
                for (var index = 1; index < path.Count; index++)
                {
                    var from = path[index - 1];
                    var to = path[index];
                    if (UpdraftBoard.ArcTop(from.Y, from.Kind) < floor || from.Y + UpdraftBoard.BirdRadius > ceiling)
                    {
                        continue;
                    }

                    Assert.NotEqual(UpdraftCloudKind.Drifting, from.Kind);
                    Assert.NotEqual(UpdraftCloudKind.Drifting, to.Kind);
                    Assert.True(UpdraftBoard.ArcDistance(storm.X, from.X, to.X) > storm.HalfWidth + UpdraftBoard.BirdRadius);
                }
            }
        }

        Assert.True(stormsSeen > 50);
    }

    [Fact]
    public void ThePoolsNeverOverflowOnALongClimb()
    {
        for (var seed = 1; seed <= 20; seed++)
        {
            var board = new UpdraftBoard();
            board.StartGame(seed, 0f);
            for (var camera = 0f; camera < 6000f; camera += 0.5f)
            {
                board.AdvanceCamera(camera);
                Assert.InRange(board.ActiveCloudCount, 1, UpdraftBoard.CloudCapacity);
            }

            Assert.Equal(0, board.OverflowCount);
        }
    }

    [Fact]
    public void AFragileCloudBreaksAfterOneBounce()
    {
        var board = new UpdraftBoard();
        board.StartEmpty();
        var cloud = board.AddCloud(UpdraftCloudKind.Fragile, 4.5f, 0f, 1f);
        board.PlaceBird(4.5f, 1f, 0f, 0f);
        var events = Run(board, 4f, UpdraftInput.Keys(0f));
        Assert.Equal(1, Count(events, UpdraftEventKind.Bounce));
        Assert.Equal(1, Count(events, UpdraftEventKind.Break));
        Assert.False(board.Clouds[cloud].Active);
        Assert.True(board.GameOver);
    }

    [Fact]
    public void APlainCloudKeepsBouncing()
    {
        var board = new UpdraftBoard();
        board.StartEmpty();
        board.AddCloud(UpdraftCloudKind.Plain, 4.5f, 0f, 1f);
        board.PlaceBird(4.5f, 1f, 0f, 0f);
        var events = Run(board, 4f, UpdraftInput.Keys(0f));
        Assert.True(Count(events, UpdraftEventKind.Bounce) >= 3);
        Assert.False(board.GameOver);
    }

    [Fact]
    public void GoldenCloudsLaunchFarHigherThanPlainOnesAndSpringsSitBetween()
    {
        var plain = ApexAbove(UpdraftCloudKind.Plain);
        var spring = ApexAbove(UpdraftCloudKind.Spring);
        var golden = ApexAbove(UpdraftCloudKind.Golden);
        Assert.InRange(plain, 3.4f, 4f);
        Assert.True(spring > plain * 1.6f);
        Assert.True(golden > plain * 3f);
        Assert.True(golden > spring);
    }

    [Fact]
    public void AStormWithoutAShieldZapsAndTheBirdDropsThrough()
    {
        var board = new UpdraftBoard();
        board.StartEmpty();
        board.AddCloud(UpdraftCloudKind.Storm, 4.5f, 0f, 1f);
        board.PlaceBird(4.5f, 1.5f, 0f, 0f);
        var events = Run(board, 0.5f, UpdraftInput.Keys(0f));
        Assert.Equal(1, Count(events, UpdraftEventKind.Zap));
        Assert.Equal(0, Count(events, UpdraftEventKind.Bounce));
        Assert.True(board.VelocityY < 0f);
        Assert.True(board.BirdY < 0f);
    }

    [Fact]
    public void AShieldAbsorbsOneStormAndBouncesTheBird()
    {
        var board = new UpdraftBoard();
        board.StartEmpty();
        board.AddCloud(UpdraftCloudKind.Storm, 4.5f, 0f, 1f);
        board.AddPickup(UpdraftPickupKind.Shield, 4.5f, 1.7f);
        board.PlaceBird(4.5f, 2f, 0f, 0f);
        var events = new List<UpdraftEventKind>();
        var sawShield = false;
        var peakAfterBlock = 0f;
        var blocked = false;
        for (var frame = 0; frame < 60; frame++)
        {
            board.Tick(Frame, UpdraftInput.Keys(0f));
            var frameEvents = board.Events;
            for (var index = 0; index < frameEvents.Length; index++)
            {
                events.Add(frameEvents[index].Kind);
                blocked |= frameEvents[index].Kind == UpdraftEventKind.ShieldBlock;
            }

            sawShield |= board.HasShield;
            if (blocked)
            {
                peakAfterBlock = MathF.Max(peakAfterBlock, board.BirdY);
            }
        }

        Assert.True(sawShield);
        Assert.Equal(1, Count(events, UpdraftEventKind.Shield));
        Assert.Equal(1, Count(events, UpdraftEventKind.ShieldBlock));
        Assert.Equal(0, Count(events, UpdraftEventKind.Zap));
        Assert.False(board.HasShield);
        Assert.True(peakAfterBlock > 3f);
    }

    [Theory]
    [InlineData(9.5f, 0.5f)]
    [InlineData(-0.5f, 8.5f)]
    [InlineData(18.25f, 0.25f)]
    [InlineData(4f, 4f)]
    public void PositionsWrapAroundTheField(float x, float expected)
    {
        Assert.Equal(expected, UpdraftBoard.Wrap(x), 3);
    }

    [Theory]
    [InlineData(8f, -1f)]
    [InlineData(-8f, 1f)]
    [InlineData(3f, 3f)]
    public void WrappedDistancesTakeTheShortWayRound(float delta, float expected)
    {
        Assert.Equal(expected, UpdraftBoard.WrapDelta(delta), 3);
    }

    [Fact]
    public void SteeringOffTheRightEdgeComesBackOnTheLeftAndCloudsCatchAcrossTheSeam()
    {
        var board = new UpdraftBoard();
        board.StartEmpty();
        board.PlaceBird(8.8f, 5f, 0f, 0f);
        Run(board, 0.25f, UpdraftInput.Keys(1f));
        Assert.InRange(board.BirdX, 0f, 2f);

        var seam = new UpdraftBoard();
        seam.StartEmpty();
        seam.AddCloud(UpdraftCloudKind.Plain, 0.2f, 0f, 1f);
        seam.PlaceBird(8.9f, 1f, 0f, 0f);
        var events = Run(seam, 0.4f, UpdraftInput.Keys(0f));
        Assert.Equal(1, Count(events, UpdraftEventKind.Bounce));
    }

    [Fact]
    public void FallingBelowTheCameraEndsTheRun()
    {
        var board = new UpdraftBoard();
        board.StartEmpty();
        board.PlaceBird(4.5f, 2f, 0f, 0f);
        var events = Run(board, 3f, UpdraftInput.Keys(0f));
        Assert.True(board.GameOver);
        Assert.Equal(1, Count(events, UpdraftEventKind.Fell));
        Assert.True(board.BirdY < board.CameraBottom);
        Assert.Empty(Run(board, 1f, UpdraftInput.Keys(0f)));
    }

    [Fact]
    public void TheCameraOnlyEverRises()
    {
        var board = new UpdraftBoard();
        board.StartGame(77, 0f);
        board.Launch();
        var previous = board.CameraBottom;
        for (var frame = 0; frame < 600 && !board.GameOver; frame++)
        {
            board.Tick(Frame, UpdraftInput.Keys(0f));
            Assert.True(board.CameraBottom >= previous);
            previous = board.CameraBottom;
        }
    }

    [Fact]
    public void CrystalsCollectedInQuickSuccessionClimbTheChain()
    {
        var board = new UpdraftBoard();
        board.StartEmpty();
        board.AddPickup(UpdraftPickupKind.Crystal, 4.5f, 1f);
        board.AddPickup(UpdraftPickupKind.Crystal, 4.5f, 1.7f);
        board.AddPickup(UpdraftPickupKind.Crystal, 4.5f, 2.4f);
        board.PlaceBird(4.5f, 0.3f, 0f, UpdraftBoard.PlainBounce);
        Run(board, 0.3f, UpdraftInput.Keys(0f));
        Assert.Equal(3, board.Crystals);
        Assert.Equal(UpdraftBoard.CrystalPoints * 6, board.CrystalScore);
        Assert.Equal(board.HeightMetres + board.CrystalScore, board.Score);
    }

    [Fact]
    public void AFeatherLiftsTheBirdForItsDuration()
    {
        var board = new UpdraftBoard();
        board.StartEmpty();
        board.AddPickup(UpdraftPickupKind.Feather, 4.5f, 1f);
        board.PlaceBird(4.5f, 1f, 0f, 0f);
        Run(board, 3f, UpdraftInput.Keys(0f));
        Assert.True(board.Gliding);
        Assert.True(board.BirdY > 15f);
        Assert.True(board.VelocityY > 0f);
    }

    [Fact]
    public void MilestonesFireEveryHundredMetres()
    {
        var board = new UpdraftBoard();
        board.StartEmpty();
        board.PlaceBird(4.5f, 49f, 0f, 10f);
        var milestones = new List<int>();
        for (var frame = 0; frame < 30; frame++)
        {
            board.Tick(Frame, UpdraftInput.Keys(0f));
            var events = board.Events;
            for (var index = 0; index < events.Length; index++)
            {
                if (events[index].Kind == UpdraftEventKind.Milestone)
                {
                    milestones.Add(events[index].Value);
                }
            }
        }

        Assert.Equal(new[] { 100 }, milestones);
    }

    [Fact]
    public void ClimbingPastThePreviousBestFiresOnce()
    {
        var board = new UpdraftBoard();
        board.StartGame(5, 3f);
        board.Launch();
        var events = Run(board, 0.6f, UpdraftInput.Keys(0f));
        Assert.Equal(1, Count(events, UpdraftEventKind.PassedBest));
        Assert.True(board.PassedBest);
    }

    [Fact]
    public void LaunchReportsItsBounceOnTheFirstTick()
    {
        var board = new UpdraftBoard();
        board.StartGame(11, 0f);
        board.Launch();
        board.Tick(Frame, UpdraftInput.Keys(0f));
        var events = board.Events;
        Assert.True(events.Length > 0);
        Assert.Equal(UpdraftEventKind.Bounce, events[0].Kind);
        board.Tick(Frame, UpdraftInput.Keys(0f));
        Assert.Equal(0, board.Events.Length);
    }
}
