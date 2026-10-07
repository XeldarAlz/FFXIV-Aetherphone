using System.Numerics;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Pegfall;
using Xunit;

namespace Aetherphone.Tests;

public sealed class PegfallBoardTests
{
    private const float Frame = 1f / 60f;
    private const float MinStaticGap = 0.6f;
    private const float MinRingGap = 0.55f;
    private static readonly Vector2 StraightDown = Vector2.UnitY;

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = Play(77);
        var second = Play(77);
        var other = Play(78);

        Assert.Equal(first.Score, second.Score);
        Assert.Equal(first.PegsHit, second.PegsHit);
        Assert.Equal(first.BallsLeft, second.BallsLeft);
        Assert.Equal(first.OrangeLeft, second.OrangeLeft);
        Assert.Equal(first.Phase, second.Phase);
        for (var peg = 0; peg < first.PegCount; peg++)
        {
            Assert.Equal(first.KindOf(peg), second.KindOf(peg));
            Assert.Equal(first.StateOf(peg), second.StateOf(peg));
        }

        Assert.True(first.PegsHit > 0);
        var differs = false;
        for (var peg = 0; peg < first.PegCount; peg++)
        {
            differs |= first.KindOf(peg) != other.KindOf(peg);
        }

        Assert.True(differs);
    }

    [Fact]
    public void EveryLevelParsesInsideTheFieldWithoutOverlaps()
    {
        for (var level = 1; level <= PegfallLevels.Count; level++)
        {
            var layout = PegfallLevels.Get(level);
            Assert.InRange(layout.Count, 40, PegfallBoard.MaxPegs);
            Assert.True(layout.BucketSpeed > 0f);
            for (var peg = 0; peg < layout.Count; peg++)
            {
                var spot = layout.Pegs[peg];
                Assert.InRange(spot.Position.X, 0.45f, PegfallBoard.Width - 0.45f);
                Assert.InRange(spot.Position.Y, 2.4f, PegfallBoard.BucketTop - 0.8f);
                if (spot.Rotates)
                {
                    continue;
                }

                for (var other = peg + 1; other < layout.Count; other++)
                {
                    if (layout.Pegs[other].Rotates)
                    {
                        continue;
                    }

                    Assert.True(Vector2.Distance(spot.Position, layout.Pegs[other].Position) >= MinStaticGap,
                        $"level {level} pegs {peg} and {other} overlap");
                }

                for (var ring = 0; ring < layout.Rings.Length; ring++)
                {
                    var spec = layout.Rings[ring];
                    var gap = MathF.Abs(Vector2.Distance(spot.Position, spec.Center) - spec.Radius);
                    Assert.True(gap >= MinRingGap, $"level {level} peg {peg} sits on ring {ring}");
                }
            }
        }
    }

    [Fact]
    public void LoadingPaintsOrangeAndPowerPegsFromTheSeed()
    {
        var board = new PegfallBoard();
        board.Load(PegfallLevels.Get(1), GameRandom.FromSeed(5));

        var oranges = 0;
        var greens = 0;
        var multiball = 0;
        var magnet = 0;
        for (var peg = 0; peg < board.PegCount; peg++)
        {
            switch (board.KindOf(peg))
            {
                case PegKind.Orange:
                    oranges++;
                    break;
                case PegKind.Green:
                    greens++;
                    multiball += board.PowerOf(peg) == PegPower.Multiball ? 1 : 0;
                    magnet += board.PowerOf(peg) == PegPower.Magnet ? 1 : 0;
                    break;
            }
        }

        Assert.Equal(PegfallBoard.MaxOranges, oranges);
        Assert.Equal(oranges, board.OrangeTotal);
        Assert.Equal(PegfallBoard.GreenPegs, greens);
        Assert.Equal(1, multiball);
        Assert.Equal(1, magnet);
        Assert.Equal(PegfallBoard.StartingBalls, board.BallsLeft);
    }

    [Fact]
    public void AHitLightsThePegScoresItAndClearsItAfterTheShot()
    {
        var board = new PegfallBoard();
        board.Load(PegfallLevels.Parse("p 5.25 6;p 0.6 12;b 0"), GameRandom.FromSeed(3));
        var target = 0;
        Assert.True(board.Fire(StraightDown));

        for (var frame = 0; frame < 240 && board.PegsHit == 0; frame++)
        {
            board.Step(Frame);
        }

        Assert.Equal(1, board.PegsHit);
        Assert.Equal(PegState.Lit, board.StateOf(target));
        var expected = board.KindOf(target) == PegKind.Orange ? PegfallBoard.OrangePoints : PegfallBoard.BluePoints;
        Assert.Equal(expected, board.Score);
        Assert.Equal(PegState.Idle, board.StateOf(1));

        for (var frame = 0; frame < 900 && board.StateOf(target) != PegState.Cleared; frame++)
        {
            board.Step(Frame);
        }

        Assert.Equal(PegState.Cleared, board.StateOf(target));
    }

    [Fact]
    public void TheBucketCatchesTheBallAndReturnsIt()
    {
        var board = new PegfallBoard();
        board.Load(PegfallLevels.Parse("p 0.6 12;b 0"), GameRandom.FromSeed(9));
        Assert.Equal(PegfallBoard.Width * 0.5f, board.BucketX, 3);
        Assert.True(board.Fire(StraightDown));
        Assert.Equal(PegfallBoard.StartingBalls - 1, board.BallsLeft);

        RunUntilAiming(board);

        Assert.Equal(1, board.Catches);
        Assert.Equal(PegfallBoard.StartingBalls, board.BallsLeft);
        Assert.Equal(PegfallPhase.Aiming, board.Phase);
        var caught = false;
        for (var index = 0; index < board.EventCount; index++)
        {
            caught |= board.Event(index).Kind == PegfallEventKind.FreeBall;
        }

        Assert.True(caught);
    }

    [Fact]
    public void AMissedBallDrainsAndCostsTheBall()
    {
        var board = new PegfallBoard();
        board.Load(PegfallLevels.Parse("p 0.6 12;b 0"), GameRandom.FromSeed(9));
        Assert.True(board.Fire(new Vector2(0.45f, 1f)));

        RunUntilAiming(board);

        Assert.Equal(0, board.Catches);
        Assert.Equal(PegfallBoard.StartingBalls - 1, board.BallsLeft);
    }

    [Fact]
    public void ClearingTheLastOrangeStartsFeverAndWinsTheLevel()
    {
        var board = new PegfallBoard();
        board.Load(PegfallLevels.Parse("p 5.3 6;b 0"), GameRandom.FromSeed(11));
        Assert.Equal(1, board.OrangeTotal);
        Assert.True(board.Fire(StraightDown));

        var feverSeen = false;
        for (var frame = 0; frame < 1800 && board.Phase != PegfallPhase.Over; frame++)
        {
            board.Step(Frame);
            feverSeen |= board.Fever;
        }

        Assert.True(feverSeen);
        Assert.Equal(PegfallPhase.Over, board.Phase);
        Assert.True(board.Won);
        Assert.Equal(0, board.OrangeLeft);
        Assert.InRange(board.FeverSlot, 0, PegfallBoard.FeverSlotCount - 1);
        var expected = PegfallBoard.OrangePoints + PegfallBoard.FeverBonus(board.FeverSlot) +
                       (PegfallBoard.StartingBalls - 1) * PegfallBoard.UnusedBallPoints;
        Assert.Equal(expected, board.Score);
        Assert.True(board.Stars >= 1);
    }

    [Fact]
    public void RunningOutOfBallsWithOrangesLeftLosesTheLevel()
    {
        var board = new PegfallBoard();
        board.Load(PegfallLevels.Parse("p 0.6 12;b 0"), GameRandom.FromSeed(2));
        for (var shot = 0; shot < PegfallBoard.StartingBalls; shot++)
        {
            Assert.True(board.Fire(new Vector2(0.5f, 1f)));
            for (var frame = 0; frame < 900 && board.Phase is PegfallPhase.Flying or PegfallPhase.Clearing; frame++)
            {
                board.Step(Frame);
            }
        }

        Assert.Equal(PegfallPhase.Over, board.Phase);
        Assert.False(board.Won);
        Assert.Equal(0, board.Stars);
        Assert.False(board.Fire(StraightDown));
    }

    [Fact]
    public void StarsFollowTheScoreThresholds()
    {
        var layout = PegfallLevels.Get(3);
        Assert.True(layout.TwoStarScore > 0);
        Assert.True(layout.ThreeStarScore > layout.TwoStarScore);
    }

    [Fact]
    public void ThePreviewStopsAtTheFirstPegAndReflects()
    {
        var board = new PegfallBoard();
        board.Load(PegfallLevels.Parse("p 5.1 6;b 0"), GameRandom.FromSeed(4));
        var path = new Vector2[120];

        var count = board.PreviewPath(StraightDown, path, out var bounced, out var start, out var velocity);

        Assert.True(bounced);
        Assert.True(count < path.Length);
        Assert.InRange(start.Y, 5.4f, 6f);
        Assert.True(velocity.Y < 0f);
        Assert.True(velocity.X < 0f);
    }

    [Fact]
    public void AimingUpwardsIsClampedBelowTheLauncher()
    {
        var aim = PegfallBoard.ClampAim(new Vector2(1f, -1f));

        Assert.True(aim.Y >= PegfallBoard.MinAimY - 0.0001f);
        Assert.True(aim.X > 0f);
        Assert.Equal(1f, aim.Length(), 4);
    }

    private static PegfallBoard Play(ulong seed)
    {
        var board = new PegfallBoard();
        board.Load(PegfallLevels.Get(1), GameRandom.FromSeed(seed));
        var aims = new[] { new Vector2(0.3f, 1f), new Vector2(-0.6f, 1f), new Vector2(1f, 0.4f) };
        for (var shot = 0; shot < aims.Length; shot++)
        {
            board.Fire(aims[shot]);
            for (var frame = 0; frame < 600 && board.Phase != PegfallPhase.Aiming && board.Phase != PegfallPhase.Over;
                 frame++)
            {
                board.Step(Frame);
                board.ClearEvents();
            }
        }

        return board;
    }

    private static void RunUntilAiming(PegfallBoard board)
    {
        for (var frame = 0; frame < 900 && board.Phase != PegfallPhase.Aiming && board.Phase != PegfallPhase.Over;
             frame++)
        {
            board.Step(Frame);
        }
    }
}
