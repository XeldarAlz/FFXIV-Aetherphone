using System.Globalization;
using System.Text;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Spiral;
using Xunit;

namespace Aetherphone.Tests;

public sealed class SpiralBoardTests
{
    private const float Frame = 1f / 60f;
    private const float TurnSpeed = 4f;
    private static readonly SpiralSegment[] AllGap = { SpiralSegment.Gap };
    private static readonly SpiralSegment[] AllSolid = { SpiralSegment.Solid };
    private static readonly SpiralSegment[] AllRed = { SpiralSegment.Red };

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = Play(1234, out var firstTrace);
        var second = Play(1234, out var secondTrace);
        Play(99, out var otherTrace);

        Assert.Equal(firstTrace, secondTrace);
        Assert.Equal(first.Score, second.Score);
        Assert.Equal(first.RingsPassed, second.RingsPassed);
        Assert.Equal(first.State, second.State);
        Assert.True(first.RingsPassed > 3);
        Assert.NotEqual(firstTrace, otherTrace);
    }

    [Fact]
    public void TheBoardDealsTheSameRingsAsTheGeneratorForItsSeed()
    {
        var board = Seeded(42);
        var random = GameRandom.FromSeed(42);
        Span<SpiralSegment> expected = stackalloc SpiralSegment[SpiralBoard.Segments];

        for (var ring = 0; ring < SpiralBoard.LookAhead; ring++)
        {
            SpiralBoard.Generate(ref random, ring, expected);
            for (var segment = 0; segment < SpiralBoard.Segments; segment++)
            {
                Assert.Equal(expected[segment], board.SegmentAt(ring, segment));
            }
        }
    }

    [Fact]
    public void EveryGeneratedRingHasAGapAndTheOpeningIsSafe()
    {
        Span<SpiralSegment> ring = stackalloc SpiralSegment[SpiralBoard.Segments];
        for (ulong seed = 1; seed <= 25; seed++)
        {
            var random = GameRandom.FromSeed(seed);
            for (var depth = 0; depth < 300; depth++)
            {
                SpiralBoard.Generate(ref random, depth, ring);
                var gaps = 0;
                var solids = 0;
                var reds = 0;
                for (var segment = 0; segment < SpiralBoard.Segments; segment++)
                {
                    switch (ring[segment])
                    {
                        case SpiralSegment.Gap:
                            gaps++;
                            break;
                        case SpiralSegment.Solid:
                            solids++;
                            break;
                        default:
                            reds++;
                            break;
                    }
                }

                Assert.True(gaps >= 1);
                Assert.True(solids >= 2);
                if (depth < SpiralBoard.SafeRings)
                {
                    Assert.Equal(0, reds);
                }

                if (depth == 0)
                {
                    Assert.Equal(SpiralSegment.Solid, ring[SpiralBoard.BallSegment]);
                }
            }
        }
    }

    [Fact]
    public void ASolidRingBouncesTheBallAtTheSameHeight()
    {
        var board = Seeded(1);
        board.SetRing(0, AllSolid);
        var bounces = 0;
        var highest = float.MaxValue;

        for (var step = 0; step < (int)(3f / Frame); step++)
        {
            board.BeginFrame();
            board.Step(Frame);
            bounces += board.Landing == SpiralLanding.Bounced ? 1 : 0;
            highest = MathF.Min(highest, board.BallY);
            Assert.True(board.BallY <= SpiralBoard.RingY(0) - SpiralBoard.BallRadius + 0.001f);
        }

        Assert.True(bounces >= 4);
        Assert.Equal(0, board.CurrentRing);
        Assert.Equal(0, board.Score);
        Assert.Equal(-SpiralBoard.BallRadius - SpiralBoard.BounceHeight, highest, 0.05f);
    }

    [Fact]
    public void FallingThroughAGapScoresARingAndLandingResetsTheStreak()
    {
        var board = Seeded(2);
        board.SetRing(0, AllGap);
        board.SetRing(1, AllSolid);

        RunUntil(board, () => board.Landing == SpiralLanding.Bounced);

        Assert.Equal(1, board.CurrentRing);
        Assert.Equal(1, board.RingsPassed);
        Assert.Equal(1, board.Score);
        Assert.Equal(1, board.LandingRing);
        Assert.Equal(0, board.Streak);
        Assert.Equal(1, board.BestStreak);
        Assert.False(board.Fireball);
    }

    [Fact]
    public void EachRingInOneDropScoresMoreAndThreeLightTheFireball()
    {
        var board = Seeded(3);
        board.SetRing(0, AllGap);
        board.SetRing(1, AllGap);
        board.SetRing(2, AllGap);
        board.SetRing(3, AllSolid);
        board.SetRing(4, AllSolid);
        var sawFireball = false;

        RunUntil(board, () =>
        {
            sawFireball |= board.FireballStarted;
            return board.Landing == SpiralLanding.Smashed;
        });

        Assert.True(sawFireball);
        Assert.Equal(1 + 2 + 3 + SpiralBoard.SmashPoints, board.Score);
        Assert.Equal(1, board.Smashes);
        Assert.Equal(3, board.LandingRing);
        Assert.Equal(4, board.CurrentRing);
        Assert.Equal(4, board.RingsPassed);
        Assert.False(board.Fireball);
        Assert.Equal(0, board.Streak);
        RunUntil(board, () => board.Landing == SpiralLanding.Bounced);
        Assert.Equal(4, board.LandingRing);
        Assert.Equal(SpiralState.Playing, board.State);
    }

    [Fact]
    public void AFireballSmashesThroughRed()
    {
        var board = Seeded(4);
        board.SetRing(0, AllGap);
        board.SetRing(1, AllGap);
        board.SetRing(2, AllGap);
        board.SetRing(3, AllRed);
        board.SetRing(4, AllSolid);

        RunUntil(board, () => board.Landing == SpiralLanding.Smashed);

        Assert.Equal(SpiralState.Playing, board.State);
        RunUntil(board, () => board.Landing != SpiralLanding.None);
        Assert.Equal(SpiralLanding.Bounced, board.Landing);
        Assert.Equal(4, board.LandingRing);
    }

    [Fact]
    public void LandingOnRedWithoutAFireballEndsTheRun()
    {
        var board = Seeded(5);
        board.SetRing(0, AllRed);

        RunUntil(board, () => board.Landing != SpiralLanding.None);

        Assert.Equal(SpiralLanding.Died, board.Landing);
        Assert.Equal(SpiralState.Over, board.State);
        var angle = board.Angle;
        board.Rotate(1f);
        Assert.Equal(angle, board.Angle);
    }

    [Fact]
    public void TurningTheTowerPicksTheSegmentUnderTheBall()
    {
        Span<SpiralSegment> layout = stackalloc SpiralSegment[SpiralBoard.Segments];
        layout.Fill(SpiralSegment.Solid);
        layout[0] = SpiralSegment.Gap;
        var still = Seeded(6);
        still.SetRing(0, layout);
        var turned = Seeded(6);
        turned.SetRing(0, layout);
        turned.SetRing(1, AllSolid);

        turned.Rotate(SpiralBoard.BallAngle - SpiralBoard.SegmentArc * 0.5f);

        Assert.Equal(SpiralBoard.BallSegment, still.SegmentIndexUnderBall);
        Assert.Equal(0, turned.SegmentIndexUnderBall);
        RunUntil(still, () => still.Landing != SpiralLanding.None);
        Assert.Equal(SpiralLanding.Bounced, still.Landing);
        Assert.Equal(0, still.CurrentRing);
        RunUntil(turned, () => turned.Landing != SpiralLanding.None);
        Assert.Equal(1, turned.LandingRing);
        Assert.Equal(1, turned.Score);
    }

    [Fact]
    public void TheLevelRisesEveryTwentyRingsAndScalesTheScore()
    {
        var board = Seeded(7);
        var sawLevelUp = false;

        for (var step = 0; step < 4000 && board.RingsPassed < SpiralBoard.LevelRings; step++)
        {
            board.SetRing(board.CurrentRing, AllGap);
            board.SetRing(board.CurrentRing + 1, AllGap);
            board.BeginFrame();
            board.Step(Frame);
            sawLevelUp |= board.LevelUp;
        }

        Assert.Equal(SpiralBoard.LevelRings, board.RingsPassed);
        Assert.True(sawLevelUp);
        Assert.Equal(2, board.Level);
        Assert.Equal(SpiralBoard.LevelRings * (SpiralBoard.LevelRings + 1) / 2, board.Score);
        Assert.True(board.Fireball);
    }

    private static SpiralBoard Seeded(ulong seed)
    {
        var board = new SpiralBoard();
        board.Reset(GameRandom.FromSeed(seed));
        return board;
    }

    private static void RunUntil(SpiralBoard board, Func<bool> done)
    {
        for (var step = 0; step < 600; step++)
        {
            board.BeginFrame();
            board.Step(Frame);
            if (done())
            {
                return;
            }
        }

        Assert.Fail("The condition was never reached.");
    }

    private static void Steer(SpiralBoard board)
    {
        var bestDelta = 0f;
        var bestDistance = float.MaxValue;
        for (var segment = 0; segment < SpiralBoard.Segments; segment++)
        {
            if (board.SegmentAt(board.CurrentRing, segment) != SpiralSegment.Gap)
            {
                continue;
            }

            var center = (segment + 0.5f) * SpiralBoard.SegmentArc + board.Angle;
            var delta = SpiralBoard.Wrap(SpiralBoard.BallAngle - center);
            if (delta > MathF.PI)
            {
                delta -= MathF.Tau;
            }

            if (MathF.Abs(delta) < bestDistance)
            {
                bestDistance = MathF.Abs(delta);
                bestDelta = delta;
            }
        }

        board.Rotate(Math.Clamp(bestDelta, -TurnSpeed * Frame, TurnSpeed * Frame));
    }

    private static SpiralBoard Play(ulong seed, out string trace)
    {
        var board = Seeded(seed);
        var builder = new StringBuilder();
        for (var step = 0; step < (int)(30f / Frame) && board.State == SpiralState.Playing; step++)
        {
            board.BeginFrame();
            Steer(board);
            board.Step(Frame);
            if (step % 10 != 0)
            {
                continue;
            }

            builder.Append(board.Score).Append(',').Append(board.CurrentRing).Append(',')
                .Append(board.BallY.ToString("F3", CultureInfo.InvariantCulture)).Append(',')
                .Append(board.Angle.ToString("F3", CultureInfo.InvariantCulture)).Append(';');
        }

        trace = builder.ToString();
        return board;
    }
}
