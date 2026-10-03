using System.Numerics;
using Aetherphone.Apps.Games.Coil;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CoilBoardTests
{
    private const float Frame = 1f / 60f;
    private const float Radius = CoilBoard.MarbleRadius;
    private const float Diameter = CoilBoard.Diameter;

    public static TheoryData<int> EveryStage()
    {
        var data = new TheoryData<int>();
        for (var stage = 1; stage <= CoilShapes.Count; stage++)
        {
            data.Add(stage);
        }

        return data;
    }

    [Fact]
    public void ThereAreAtLeastTwelveStages()
    {
        Assert.True(CoilShapes.Count >= 12);
    }

    [Theory]
    [MemberData(nameof(EveryStage))]
    public void EveryStageTrackIsFiniteMonotonicAndFitsTheField(int stage)
    {
        var board = new CoilBoard(7);
        board.StartAtStage(stage);
        var track = board.Track;
        Assert.True(float.IsFinite(track.Length));
        Assert.True(track.Length > Diameter * 40f);
        var arcs = track.RawArc;
        Assert.Equal(0f, arcs[0]);
        for (var index = 1; index < arcs.Length; index++)
        {
            Assert.True(float.IsFinite(arcs[index]));
            Assert.True(arcs[index] > arcs[index - 1], $"arc stalls at raw sample {index}");
        }

        var samples = track.Samples;
        for (var index = 0; index < samples.Length; index++)
        {
            var point = samples[index];
            Assert.InRange(point.X, Radius, CoilBoard.FieldWidth - Radius);
            Assert.InRange(point.Y, Radius, CoilBoard.FieldHeight - Radius);
        }

        Assert.InRange(board.Launcher.X, 0f, CoilBoard.FieldWidth);
        Assert.InRange(board.Launcher.Y, 0f, CoilBoard.FieldHeight);
    }

    [Theory]
    [MemberData(nameof(EveryStage))]
    public void EveryStageKeepsItsGroovesApartAndClearsTheLauncher(int stage)
    {
        var board = new CoilBoard(7);
        board.StartAtStage(stage);
        var track = board.Track;
        var samples = track.Samples;
        var skip = (int)MathF.Ceiling(Diameter * 3f / track.SampleSpacing);
        for (var first = 0; first < samples.Length; first++)
        {
            Assert.True(Vector2.Distance(samples[first], board.Launcher) >= Radius * 4f,
                $"track crowds the launcher at sample {first}");
            for (var second = first + skip; second < samples.Length; second++)
            {
                Assert.True(Vector2.Distance(samples[first], samples[second]) >= Diameter * 2.1f,
                    $"grooves overlap between samples {first} and {second}");
            }
        }
    }

    [Fact]
    public void ArcLengthLookupWalksTheTrackAtUnitSpeed()
    {
        var board = new CoilBoard(7);
        var track = board.Track;
        var step = track.Length / 50f;
        for (var index = 0; index < 50; index++)
        {
            var distance = Vector2.Distance(track.PositionAt(index * step), track.PositionAt((index + 1) * step));
            Assert.InRange(distance, step * 0.8f, step * 1.0001f);
        }
    }

    [Fact]
    public void InsertionPlacesTheMarbleOnTheSideItHit()
    {
        var board = new CoilBoard(3);
        board.LoadChain(new byte[] { 0, 1, 2, 3 }, 1.5f);
        var ahead = board.Insert(1, true, 3, Vector2.Zero);
        Assert.Equal(2, ahead);
        Assert.Equal(new byte[] { 0, 1, 3, 2, 3 }, Colours(board));
        var behind = board.Insert(1, false, 2, Vector2.Zero);
        Assert.Equal(1, behind);
        Assert.Equal(new byte[] { 0, 2, 1, 3, 2, 3 }, Colours(board));
        var marbles = board.Marbles;
        for (var index = 1; index < marbles.Length; index++)
        {
            Assert.Equal(Diameter, marbles[index].Arc - marbles[index - 1].Arc, 4);
        }
    }

    [Fact]
    public void AFiredMarbleJoinsTheChainNextToWhatItHits()
    {
        var board = new CoilBoard(11);
        board.LoadChain(new byte[] { 0, 1, 2, 3, 0, 1, 2, 3 }, board.Track.Length * 0.45f);
        var fired = board.LoadedColour;
        var target = board.Positions[4];
        Assert.True(board.Fire(target - board.Launcher));
        var landed = false;
        for (var frame = 0; frame < 120 && !landed; frame++)
        {
            board.BeginFrame();
            board.Tick(Frame);
            landed = board.LandedThisFrame;
        }

        Assert.True(landed);
        Assert.Equal(9, board.MarbleCount);
        var arriving = 0;
        var marbles = board.Marbles;
        for (var index = 0; index < marbles.Length; index++)
        {
            if (marbles[index].Arrive > 0f)
            {
                arriving++;
                Assert.Equal(fired, marbles[index].Colour);
            }
        }

        Assert.Equal(1, arriving);
    }

    [Fact]
    public void ThreeOfAColourClear()
    {
        var board = new CoilBoard(5);
        board.LoadChain(new byte[] { 2, 0, 0, 1, 3 }, 1.5f);
        board.BeginFrame();
        board.LandAt(2, true, 0);
        Assert.Equal(new byte[] { 2, 1, 3 }, Colours(board));
        Assert.Equal(3 * CoilBoard.PointsPerMarble, board.Score);
        Assert.Equal(1, board.Clears.Length);
        Assert.Equal(3, board.Bursts.Length);
    }

    [Fact]
    public void TwoOfAColourStay()
    {
        var board = new CoilBoard(5);
        board.LoadChain(new byte[] { 2, 0, 1, 3 }, 1.5f);
        board.LandAt(1, true, 0);
        Assert.Equal(new byte[] { 2, 0, 0, 1, 3 }, Colours(board));
        Assert.Equal(0, board.Score);
    }

    [Fact]
    public void AMatchingGapPullsTheFrontBackIntoAChainReaction()
    {
        var board = new CoilBoard(5);
        board.LoadChain(new byte[] { 3, 1, 1, 0, 0, 1, 2 }, 1.5f);
        board.LandAt(4, true, 0);
        Assert.Equal(30, board.Score);
        Assert.Equal(new byte[] { 3, 1, 1, 1, 2 }, Colours(board));
        var deepest = 0;
        for (var frame = 0; frame < 120 && board.MarbleCount > 2; frame++)
        {
            board.BeginFrame();
            board.Tick(Frame);
            var clears = board.Clears;
            for (var index = 0; index < clears.Length; index++)
            {
                deepest = Math.Max(deepest, clears[index].Multiplier);
            }
        }

        Assert.Equal(new byte[] { 3, 2 }, Colours(board));
        Assert.Equal(2, deepest);
        Assert.Equal(30 + 3 * CoilBoard.PointsPerMarble * 2, board.Score);
    }

    [Fact]
    public void ANonMatchingGapLeavesTheFrontSegmentWaiting()
    {
        var board = new CoilBoard(5);
        board.LoadChain(new byte[] { 3, 2, 0, 0, 1, 2 }, 1.5f);
        board.LandAt(3, true, 0);
        var front = board.Marbles[2].Arc;
        for (var frame = 0; frame < 30; frame++)
        {
            board.BeginFrame();
            board.Tick(Frame);
        }

        Assert.Equal(front, board.Marbles[2].Arc, 5);
        Assert.True(board.Marbles[1].Arc > 1.5f - 4f * Diameter);
    }

    [Fact]
    public void ReachingTheDrainCostsALifeAndRestartsTheStage()
    {
        var board = new CoilBoard(9);
        board.StartAtStage(4);
        board.LoadChain(new byte[] { 0, 1, 2 }, board.Track.Length - 0.0005f);
        var drained = false;
        var lifeLost = false;
        var restarted = false;
        for (var frame = 0; frame < 600 && !restarted; frame++)
        {
            board.BeginFrame();
            board.Tick(Frame);
            drained |= board.DrainStartedThisFrame;
            lifeLost |= board.LifeLostThisFrame;
            restarted = board.StageStartedThisFrame;
        }

        Assert.True(drained);
        Assert.True(lifeLost);
        Assert.True(restarted);
        Assert.Equal(CoilBoard.StartLives - 1, board.Lives);
        Assert.Equal(4, board.Stage);
        Assert.Equal(CoilState.Playing, board.State);
    }

    [Fact]
    public void LosingEveryLifeEndsTheGame()
    {
        var board = new CoilBoard(9);
        board.Begin();
        var over = false;
        for (var life = 0; life < CoilBoard.StartLives; life++)
        {
            board.LoadChain(new byte[] { 0, 1 }, board.Track.Length - 0.0005f);
            for (var frame = 0; frame < 600; frame++)
            {
                board.BeginFrame();
                board.Tick(Frame);
                over |= board.GameOverThisFrame;
                if (board.LifeLostThisFrame)
                {
                    break;
                }
            }
        }

        Assert.True(over);
        Assert.Equal(0, board.Lives);
        Assert.Equal(CoilState.GameOver, board.State);
    }

    [Fact]
    public void AmmoIsOnlyDrawnFromColoursOnTheTrack()
    {
        var board = new CoilBoard(21);
        board.LoadChain(new byte[] { 1, 4, 4, 1, 1, 4 }, 1.5f);
        for (var roll = 0; roll < 200; roll++)
        {
            board.RefreshAmmo();
            Assert.True(board.LoadedColour is 1 or 4);
            Assert.True(board.NextColour is 1 or 4);
        }
    }

    [Fact]
    public void ClearingTheLastOfAColourRerollsTheAmmo()
    {
        var board = new CoilBoard(21);
        board.LoadChain(new byte[] { 2, 3, 3, 1, 1 }, 1.5f);
        board.SetAmmo(3, 3);
        board.LandAt(2, true, 3);
        Assert.True(board.LoadedColour is 1 or 2);
        Assert.True(board.NextColour is 1 or 2);
    }

    [Fact]
    public void ClearingAPowerMarbleTriggersIt()
    {
        var board = new CoilBoard(5);
        board.LoadChain(new byte[] { 3, 0, 0, 1 }, 1.5f);
        board.SetMarble(2, board.Marbles[2].Arc, CoilPower.Freeze);
        board.BeginFrame();
        board.LandAt(2, true, 0);
        Assert.Equal(1, board.Triggers.Length);
        Assert.Equal(CoilBoard.FreezeSeconds, board.FreezeLeft);
        var rear = board.Marbles[0].Arc;
        for (var frame = 0; frame < 60; frame++)
        {
            board.BeginFrame();
            board.Tick(Frame);
        }

        Assert.Equal(rear, board.Marbles[0].Arc, 5);
    }

    [Fact]
    public void AnArmedPrismClearsEveryMarbleOfTheColourItHits()
    {
        var board = new CoilBoard(5);
        board.LoadChain(new byte[] { 3, 0, 1, 3, 2, 3 }, 1.5f);
        board.LandAt(3, true, 0, CoilPower.Prism);
        Assert.Equal(new byte[] { 0, 1, 2 }, Colours(board));
    }

    [Fact]
    public void EmptyingTheTrackAfterTheQuotaClearsTheStage()
    {
        var board = new CoilBoard(5);
        board.LoadChain(new byte[] { 2, 2 }, 1.5f);
        board.LandAt(1, true, 2);
        var cleared = false;
        for (var frame = 0; frame < 4; frame++)
        {
            board.BeginFrame();
            board.Tick(Frame);
            cleared |= board.StageClearedThisFrame;
        }

        Assert.True(cleared);
        Assert.Equal(CoilState.StageClear, board.State);
        Assert.True(board.ClearBonus >= CoilBoard.StageClearBase);
        for (var frame = 0; frame < 240; frame++)
        {
            board.BeginFrame();
            board.Tick(Frame);
        }

        Assert.Equal(2, board.Stage);
        Assert.Equal(CoilState.Playing, board.State);
    }

    [Fact]
    public void StagesRampColoursQuotaAndSpeedThenLoopHarder()
    {
        Assert.Equal(4, CoilBoard.ColoursFor(1));
        Assert.Equal(5, CoilBoard.ColoursFor(4));
        Assert.Equal(6, CoilBoard.ColoursFor(12));
        Assert.Equal(6, CoilBoard.ColoursFor(13));
        Assert.True(CoilBoard.QuotaFor(12) > CoilBoard.QuotaFor(1));
        Assert.True(CoilBoard.QuotaFor(13) > CoilBoard.QuotaFor(1));
        Assert.True(CoilBoard.CrawlSpeedFor(12) > CoilBoard.CrawlSpeedFor(1));
        Assert.True(CoilBoard.CrawlSpeedFor(13) > CoilBoard.CrawlSpeedFor(1));
        Assert.Equal(CoilBoard.ShapeFor(1), CoilBoard.ShapeFor(1 + CoilShapes.Count));
    }

    [Fact]
    public void TheSameSeedPlaysTheSameGame()
    {
        var first = Play(1234);
        var second = Play(1234);
        Assert.Equal(first.Score, second.Score);
        Assert.Equal(first.MarbleCount, second.MarbleCount);
        Assert.Equal(Colours(first), Colours(second));
        Assert.Equal(first.LoadedColour, second.LoadedColour);
        Assert.Equal(first.NextColour, second.NextColour);
        Assert.NotEqual(Colours(first), Colours(Play(99)));
    }

    private static CoilBoard Play(int seed)
    {
        var board = new CoilBoard(seed);
        board.Begin();
        for (var frame = 0; frame < 60 * 40; frame++)
        {
            board.BeginFrame();
            if (frame % 20 == 0)
            {
                var angle = frame * 0.37f;
                board.Fire(new Vector2(MathF.Cos(angle), MathF.Sin(angle)));
            }

            if (frame % 90 == 0)
            {
                board.Swap();
            }

            board.Tick(Frame);
        }

        return board;
    }

    private static byte[] Colours(CoilBoard board)
    {
        var marbles = board.Marbles;
        var colours = new byte[marbles.Length];
        for (var index = 0; index < marbles.Length; index++)
        {
            colours[index] = marbles[index].Colour;
        }

        return colours;
    }
}
