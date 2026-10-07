using System.Globalization;
using System.Numerics;
using System.Text;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Framework.Physics;
using Aetherphone.Apps.Games.Pinball;
using Xunit;

namespace Aetherphone.Tests;

public sealed class PinballBoardTests
{
    private const float Frame = 1f / 60f;
    private const float Tick = PhysicsWorld.StepSeconds;
    private const float AngleTolerance = 0.04f;
    private static readonly Vector2 FieldCenter = new(2.7f, 6.8f);

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = Play(1234);
        var second = Play(1234);
        var other = Play(99);

        Assert.Equal(first, second);
        Assert.NotEqual(first, other);
    }

    [Fact]
    public void FlipperHingesReachTheirLimitsAndFallBack()
    {
        var board = Fresh(1);

        board.SetFlippers(true, true);
        Run(board, 0.3f);

        Assert.InRange(board.HingeAngle(PinballTable.LeftFlipper), -PinballTable.FlipperSwing - AngleTolerance,
            -PinballTable.FlipperSwing + AngleTolerance);
        Assert.InRange(board.HingeAngle(PinballTable.RightFlipper), PinballTable.FlipperSwing - AngleTolerance,
            PinballTable.FlipperSwing + AngleTolerance);

        board.SetFlippers(false, false);
        Run(board, 0.4f);

        Assert.InRange(board.HingeAngle(PinballTable.LeftFlipper), -AngleTolerance, AngleTolerance);
        Assert.InRange(board.HingeAngle(PinballTable.RightFlipper), -AngleTolerance, AngleTolerance);
    }

    [Fact]
    public void BallDoesNotTunnelTheFlipperAtMaxSpeed()
    {
        var board = Fresh(2);
        var pivot = PinballTable.LeftPivot;
        var angle = PinballTable.RestAngle(PinballTable.LeftFlipper);
        var along = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        var above = new Vector2(along.Y, -along.X);
        var start = pivot + along * 0.75f + above * 0.7f;
        Place(board, start, new Vector2(0f, 24f));
        board.SetFlippers(true, false);
        var body = board.FlipperBody(PinballTable.LeftFlipper);
        var world = board.World;
        var halfLength = PinballTable.FlipperLength * 0.5f;

        for (var step = 0; step < 40; step++)
        {
            board.BeginFrame();
            board.Update(Tick);
            var local = world.LocalPoint(body, board.BallPositionAt(0));
            if (MathF.Abs(local.X) < halfLength)
            {
                Assert.True(local.Y < 0f, "the ball crossed under the flipper");
            }
        }

        Assert.True(board.BallVelocityAt(0).Y < -5f);
    }

    [Fact]
    public void DroppedBanksStayDownUntilBothFallThenLightTheJackpotAndReset()
    {
        var board = Fresh(3);
        board.BeginFrame();

        Assert.True(board.HitTarget(0));
        Assert.False(board.HitTarget(0));
        board.HitTarget(1);
        board.HitTarget(2);

        Assert.True(board.BankDown(0));
        Assert.False(board.JackpotLit);
        Run(board, 1f);
        Assert.True(board.TargetDown(0));

        board.BeginFrame();
        board.HitTarget(3);
        board.HitTarget(4);
        board.HitTarget(5);

        Assert.True(board.JackpotLit);
        Assert.True(HasEvent(board, PinballEventKind.JackpotLit));
        Assert.Equal(1, CountEvents(board, PinballEventKind.BankDown));
        Assert.Equal(PinballBoard.TargetPoints * 6 + PinballBoard.BankPoints * 2, board.Score);

        Run(board, 1f);

        for (var target = 0; target < PinballTable.TargetCount; target++)
        {
            Assert.False(board.TargetDown(target));
        }

        Assert.True(board.JackpotLit);
    }

    [Fact]
    public void RampCollectsTheLitJackpotOnceAndRaisesItsValue()
    {
        var board = Fresh(4);
        DropBothBanks(board);
        var before = board.Score;
        board.BeginFrame();

        board.CompleteRamp(PinballTable.RampMade);

        Assert.True(HasEvent(board, PinballEventKind.Jackpot));
        Assert.Equal(before + PinballBoard.RampPoints + PinballBoard.JackpotBase, board.Score);
        Assert.False(board.JackpotLit);
        Assert.Equal(1, board.Jackpots);
        Assert.Equal(PinballBoard.JackpotBase + PinballBoard.JackpotStep, board.JackpotValue);

        board.BeginFrame();
        board.CompleteRamp(PinballTable.RampMade);

        Assert.False(HasEvent(board, PinballEventKind.Jackpot));
        Assert.Equal(2, board.Ramps);
    }

    [Fact]
    public void RaisingTheJackpotWhileLitAddsToItsValue()
    {
        var board = Fresh(5);
        DropBothBanks(board);
        Run(board, 1f);
        board.BeginFrame();

        DropBothBanks(board);

        Assert.True(HasEvent(board, PinballEventKind.JackpotRaised));
        Assert.Equal(PinballBoard.JackpotBase + PinballBoard.JackpotStep, board.JackpotValue);
    }

    [Fact]
    public void ABallThrownAtATargetFaceDropsIt()
    {
        var board = Fresh(6);
        var target = PinballTable.Targets[1];

        Place(board, target + new Vector2(0.6f, 0f), new Vector2(-7f, -1f));
        Run(board, 0.4f);

        Assert.True(board.TargetDown(1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ABallThrownAtASlingFaceIsKickedBackOut(int sling)
    {
        var board = Fresh(12);
        var face = PinballTable.Sling(sling);
        var normal = PinballTable.SlingFaceNormal(sling);
        var middle = (face[0] + face[2]) * 0.5f;

        Assert.True(normal.X * (sling == 0 ? 1f : -1f) > 0f);
        Assert.True(normal.Y < 0f);
        Place(board, middle + normal * 0.4f, -normal * 4f);
        Run(board, 0.12f);

        Assert.True(board.SlingFlash(sling) > 0f);
        Assert.True(Vector2.Dot(board.BallVelocityAt(0), normal) > 4f);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public void ADroppedTargetSlotNeverTrapsTheBall(int target)
    {
        var board = Fresh(13);
        board.HitTarget(target);
        var position = PinballTable.Targets[target];
        var facing = PinballTable.TargetFacing[target];

        Place(board, position + facing * 0.3f, -facing * 3f);
        Run(board, 1.5f);

        Assert.True(Vector2.Distance(board.BallPositionAt(0), position) > 0.8f);
    }

    [Fact]
    public void ThirdLockStartsAThreeBallMultiball()
    {
        var board = Fresh(7);
        board.Autopilot = true;
        Place(board, FieldCenter, Vector2.Zero);

        board.BeginFrame();
        board.CaptureBall(0);
        Assert.Equal(1, board.Locks);
        Assert.True(HasEvent(board, PinballEventKind.Locked));
        Assert.Equal(BallState.Held, board.BallStateAt(0));

        Run(board, PinballBoard.SaucerHoldSeconds + 0.05f);
        Assert.Equal(BallState.Rolling, board.BallStateAt(0));

        board.CaptureBall(0);
        Run(board, PinballBoard.SaucerHoldSeconds + 0.05f);
        board.BeginFrame();
        board.CaptureBall(0);

        Assert.True(board.MultiballActive);
        Assert.True(HasEvent(board, PinballEventKind.Multiball));
        Assert.Equal(PinballBoard.MultiballBalls - 1, board.PendingLaunches);

        var most = 0;
        for (var frame = 0; frame < 180; frame++)
        {
            board.BeginFrame();
            board.Update(Frame);
            most = Math.Max(most, board.LiveBalls);
        }

        Assert.Equal(PinballBoard.MultiballBalls, most);
    }

    [Fact]
    public void MultiballEndsAndResetsLocksWhenOneBallIsLeft()
    {
        var board = Fresh(8);
        board.Autopilot = true;
        Place(board, FieldCenter, Vector2.Zero);
        for (var lockIndex = 0; lockIndex < PinballBoard.LocksForMultiball; lockIndex++)
        {
            board.CaptureBall(0);
            Run(board, PinballBoard.SaucerHoldSeconds + 0.05f);
        }

        Assert.True(board.MultiballActive);
        var elapsed = 0f;
        while ((board.BallSaveLeft > 0f || board.PendingLaunches > 0) && elapsed < 30f)
        {
            board.BeginFrame();
            board.Update(Frame);
            elapsed += Frame;
        }

        Assert.True(board.MultiballActive);
        Assert.True(board.LiveBalls >= 2);
        while (board.LiveBalls > 1)
        {
            DrainFirst(board);
        }

        Assert.False(board.MultiballActive);
        Assert.Equal(0, board.Locks);
        Assert.Equal(PinballPhase.Playing, board.Phase);
    }

    [Fact]
    public void BallSaveReturnsADrainInTheFirstEightSecondsOnly()
    {
        var board = Fresh(9);
        Place(board, FieldCenter, Vector2.Zero);
        Assert.Equal(PinballBoard.BallSaveSeconds, board.BallSaveLeft, 3);

        board.BeginFrame();
        board.DrainBall(0);

        Assert.True(HasEvent(board, PinballEventKind.BallSaved));
        Assert.Equal(1, board.PendingLaunches);
        Assert.Equal(1, board.BallNumber);
        Assert.Equal(PinballPhase.Playing, board.Phase);

        Run(board, 1f);
        Assert.Equal(1, board.LiveBalls);
        Assert.Equal(1, board.BallNumber);

        board.Autopilot = true;
        var elapsed = 0f;
        while ((board.BallSaveLeft > 0f || board.PendingLaunches > 0 || RollingSlot(board) < 0) && elapsed < 30f)
        {
            board.BeginFrame();
            board.Update(Frame);
            elapsed += Frame;
        }

        board.BeginFrame();
        DrainFirst(board);

        Assert.False(HasEvent(board, PinballEventKind.BallSaved));
        Assert.True(HasEvent(board, PinballEventKind.Bonus));
        Assert.Equal(PinballPhase.BallEnd, board.Phase);

        Run(board, PinballBoard.BallEndSeconds + 0.1f);

        Assert.Equal(2, board.BallNumber);
        Assert.True(board.BallWaiting);
    }

    [Fact]
    public void TwoWarningsThenATiltKillsFlippersScoringAndBonus()
    {
        var board = Fresh(10);
        Place(board, FieldCenter, Vector2.Zero);
        board.BeginFrame();

        for (var nudge = 0; nudge < 3; nudge++)
        {
            board.Nudge();
        }

        Assert.Equal(1, board.TiltWarnings);
        Assert.False(board.Tilted);
        board.Nudge();
        board.Nudge();
        Assert.Equal(2, board.TiltWarnings);
        board.Nudge();
        board.Nudge();

        Assert.True(board.Tilted);
        Assert.Equal(2, CountEvents(board, PinballEventKind.TiltWarning));
        Assert.True(HasEvent(board, PinballEventKind.Tilt));
        Assert.False(board.Nudge());

        var score = board.Score;
        board.HitTarget(0);
        board.RollTopLane(1);
        board.CompleteRamp(PinballTable.RampMade);
        Assert.Equal(score, board.Score);

        board.SetFlippers(true, true);
        Run(board, 0.3f);
        Assert.InRange(board.HingeAngle(PinballTable.LeftFlipper), -AngleTolerance, AngleTolerance);

        board.BeginFrame();
        DrainFirst(board);
        Assert.False(HasEvent(board, PinballEventKind.BallSaved));
        Assert.Equal(0, BonusAward(board));

        Run(board, PinballBoard.BallEndSeconds + 0.1f);
        Assert.False(board.Tilted);
        Assert.Equal(0, board.TiltWarnings);
    }

    [Fact]
    public void SpacedNudgesNeverWarn()
    {
        var board = Fresh(11);
        board.Autopilot = true;
        Place(board, FieldCenter, Vector2.Zero);

        for (var nudge = 0; nudge < 10; nudge++)
        {
            board.Nudge();
            Run(board, 2f);
        }

        Assert.Equal(0, board.TiltWarnings);
        Assert.False(board.Tilted);
    }

    [Fact]
    public void SkillShotPaysTheLitLaneOnlyAsTheFirstSwitchAfterAPlunge()
    {
        var hit = Plunged(12);
        Assert.True(hit.SkillArmed);
        var score = hit.Score;
        hit.BeginFrame();

        hit.RollTopLane(hit.SkillLane);

        Assert.True(HasEvent(hit, PinballEventKind.SkillShot));
        Assert.Equal(score + PinballBoard.SkillShotPoints + PinballBoard.TopLanePoints, hit.Score);
        Assert.False(hit.SkillArmed);

        var miss = Plunged(12);
        miss.BeginFrame();
        miss.RollTopLane((miss.SkillLane + 1) % PinballTable.TopLaneCount);
        miss.RollTopLane(miss.SkillLane);
        Assert.False(HasEvent(miss, PinballEventKind.SkillShot));

        var spoiled = Plunged(12);
        spoiled.BeginFrame();
        spoiled.HitTarget(0);
        spoiled.RollTopLane(spoiled.SkillLane);
        Assert.False(HasEvent(spoiled, PinballEventKind.SkillShot));
    }

    [Fact]
    public void FlippersMoveTheSkillLaneWhileTheBallWaits()
    {
        var board = Fresh(13);
        Run(board, 0.2f);
        var lane = board.SkillLane;

        board.SetFlippers(false, true);
        board.SetFlippers(false, false);
        Assert.Equal((lane + 1) % PinballTable.TopLaneCount, board.SkillLane);

        board.SetFlippers(true, false);
        board.SetFlippers(true, false);
        board.SetFlippers(false, false);
        Assert.Equal(lane, board.SkillLane);
    }

    [Fact]
    public void FullPlungeLeavesTheLaneAndArmsTheBallSave()
    {
        var board = Fresh(14);
        Run(board, 0.3f);
        Assert.True(board.BallWaiting);

        board.SetPlungerPull(1f);
        Assert.True(board.ReleasePlunger());
        Run(board, 1.2f);

        Assert.Equal(BallState.Rolling, board.BallStateAt(0));
        Assert.True(board.BallSaveLeft > 0f);
    }

    [Fact]
    public void AllThreeTopLanesRaiseTheBonusMultiplier()
    {
        var board = Fresh(15);
        Place(board, FieldCenter, Vector2.Zero);
        board.BeginFrame();

        for (var lane = 0; lane < PinballTable.TopLaneCount; lane++)
        {
            board.RollTopLane(lane);
        }

        Assert.Equal(2, board.Multiplier);
        Assert.Equal(0, board.LanesLit);
        Assert.True(HasEvent(board, PinballEventKind.LanesComplete));
    }

    [Fact]
    public void ScoreMilestoneAwardsOneExtraBall()
    {
        var board = Fresh(16);
        board.BeginFrame();
        var ramps = PinballBoard.ExtraBallScore / PinballBoard.RampPoints;

        for (var ramp = 0; ramp < ramps * 2; ramp++)
        {
            board.CompleteRamp(PinballTable.RampMade);
        }

        Assert.Equal(1, CountEvents(board, PinballEventKind.ExtraBall));
        Assert.Equal(1, board.ExtraBalls);
        Assert.Equal(PinballBoard.BallsPerGame + 1, board.BallsLeft);

        board.DrainBall(0);
        Assert.Equal(PinballPhase.BallEnd, board.Phase);
        Run(board, PinballBoard.BallEndSeconds + 0.1f);

        Assert.Equal(1, board.BallNumber);
        Assert.Equal(0, board.ExtraBalls);
    }

    [Fact]
    public void ARampShotRisesOverThePlayfieldAndReturnsToTheRightInlane()
    {
        var board = Fresh(17);
        Place(board, new Vector2(4.6f, 6.2f), new Vector2(0f, -16f));
        var rose = false;
        var made = false;

        for (var frame = 0; frame < 150; frame++)
        {
            board.BeginFrame();
            board.Update(Frame);
            rose |= board.BallLayerAt(0) == BallLayer.Ramp;
            made |= HasEvent(board, PinballEventKind.RampMade);
        }

        Assert.True(rose);
        Assert.True(made);
        Assert.Equal(BallLayer.Playfield, board.BallLayerAt(0));
    }

    [Fact]
    public void BallsStayOnTheTableForThreeMinutesOfPlay()
    {
        var board = new PinballBoard();
        board.Reset(GameRandom.FromSeed(18), true);

        for (var frame = 0; frame < 60 * 180; frame++)
        {
            board.BeginFrame();
            board.Update(Frame);
            for (var slot = 0; slot < PinballBoard.MaxBalls; slot++)
            {
                if (board.BallStateAt(slot) == BallState.None)
                {
                    continue;
                }

                var position = board.BallPositionAt(slot);
                Assert.InRange(position.X, 0f, PinballTable.Width);
                Assert.InRange(position.Y, 0f, PinballTable.CabinetBottom);
            }
        }

        Assert.True(board.Score > 0);
    }

    private static PinballBoard Fresh(ulong seed)
    {
        var board = new PinballBoard();
        board.Reset(GameRandom.FromSeed(seed), false);
        return board;
    }

    private static PinballBoard Plunged(ulong seed)
    {
        var board = Fresh(seed);
        Run(board, 0.3f);
        board.SetPlungerPull(1f);
        board.ReleasePlunger();
        while (board.BallStateAt(0) == BallState.Lane)
        {
            board.BeginFrame();
            board.Update(Tick);
        }

        return board;
    }

    private static void Place(PinballBoard board, Vector2 position, Vector2 velocity)
    {
        var body = board.BallBodyAt(0);
        board.World.SetTransform(body, position, 0f);
        board.World.SetVelocity(body, velocity);
        board.BeginFrame();
        board.Update(Tick);
    }

    private static void DropBothBanks(PinballBoard board)
    {
        for (var target = 0; target < PinballTable.TargetCount; target++)
        {
            board.HitTarget(target);
        }
    }

    private static void DrainFirst(PinballBoard board)
    {
        for (var slot = 0; slot < PinballBoard.MaxBalls; slot++)
        {
            if (board.BallStateAt(slot) != BallState.None)
            {
                board.DrainBall(slot);
                return;
            }
        }
    }

    private static int RollingSlot(PinballBoard board)
    {
        for (var slot = 0; slot < PinballBoard.MaxBalls; slot++)
        {
            if (board.BallStateAt(slot) == BallState.Rolling)
            {
                return slot;
            }
        }

        return -1;
    }

    private static int BonusAward(PinballBoard board)
    {
        for (var index = 0; index < board.EventCount; index++)
        {
            if (board.Event(index).Kind == PinballEventKind.Bonus)
            {
                return board.Event(index).Value;
            }
        }

        return -1;
    }

    private static bool HasEvent(PinballBoard board, PinballEventKind kind) => CountEvents(board, kind) > 0;

    private static int CountEvents(PinballBoard board, PinballEventKind kind)
    {
        var count = 0;
        for (var index = 0; index < board.EventCount; index++)
        {
            if (board.Event(index).Kind == kind)
            {
                count++;
            }
        }

        return count;
    }

    private static void Run(PinballBoard board, float seconds)
    {
        var steps = (int)MathF.Round(seconds / Frame);
        for (var step = 0; step < steps; step++)
        {
            board.BeginFrame();
            board.Update(Frame);
        }
    }

    private static string Play(ulong seed)
    {
        var board = Fresh(seed);
        board.Autopilot = true;
        var trace = new StringBuilder();
        for (var frame = 0; frame < 60 * 45; frame++)
        {
            board.BeginFrame();
            if (board.BallWaiting)
            {
                board.SetPlungerPull(0.8f);
                board.ReleasePlunger();
            }

            if (frame % 240 == 120)
            {
                board.Nudge();
            }

            board.Update(Frame);
            if (frame % 30 != 0)
            {
                continue;
            }

            trace.Append(board.Score).Append(' ').Append(board.BallNumber).Append(' ').Append(board.SkillLane);
            for (var slot = 0; slot < PinballBoard.MaxBalls; slot++)
            {
                var position = board.BallPositionAt(slot);
                trace.Append(' ').Append(position.X.ToString("0.0000", CultureInfo.InvariantCulture)).Append(',')
                    .Append(position.Y.ToString("0.0000", CultureInfo.InvariantCulture));
            }

            trace.Append('\n');
        }

        return trace.ToString();
    }
}
