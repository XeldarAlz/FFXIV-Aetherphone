using System.Numerics;
using Aetherphone.Apps.Games.Fling;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Framework.Physics;
using Xunit;

namespace Aetherphone.Tests;

public sealed class FlingBoardTests
{
    private const float Frame = 1f / 60f;
    private const string FarTarget = "nnn|Hw 30 0 2 1.5;G 30 0";

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = Play(21);
        var second = Play(21);

        Assert.Equal(first.Score, second.Score);
        Assert.Equal(first.Destroyed, second.Destroyed);
        Assert.Equal(first.GoblinsLeft, second.GoblinsLeft);
        Assert.Equal(first.Phase, second.Phase);
        for (var piece = 0; piece < first.PieceCount; piece++)
        {
            Assert.Equal(first.PieceAlive(piece), second.PieceAlive(piece));
            Assert.Equal(first.PieceSeed(piece), second.PieceSeed(piece));
            if (first.PieceAlive(piece))
            {
                Assert.Equal(first.PiecePosition(piece), second.PiecePosition(piece));
                Assert.Equal(first.PieceAngle(piece), second.PieceAngle(piece));
            }
        }

        Assert.True(first.Shots > 0);
    }

    [Fact]
    public void EveryLevelParsesWithBirdsGoblinsAndOrderedStars()
    {
        for (var number = 1; number <= FlingLevels.Count; number++)
        {
            var level = FlingLevels.Get(number);
            Assert.InRange(level.Birds.Length, 1, 5);
            Assert.True(level.GoblinCount > 0, $"level {number} has no goblin");
            Assert.InRange(level.Pieces.Length, 1, FlingBoard.MaxPieces);
            Assert.True(level.ThreeStarScore > level.TwoStarScore);
            Assert.True(level.TwoStarScore >= level.GoblinCount * FlingLevels.GoblinPoints);
            for (var piece = 0; piece < level.Pieces.Length; piece++)
            {
                Assert.True(level.Pieces[piece].Center.X > 12f, $"level {number} piece {piece} crowds the sling");
                Assert.True(level.Pieces[piece].Center.Y < 0f, $"level {number} piece {piece} is under the ground");
            }
        }
    }

    [Fact]
    public void EveryLevelStandsStillUntilTheFirstShot()
    {
        var board = new FlingBoard();
        for (var number = 1; number <= FlingLevels.Count; number++)
        {
            var level = FlingLevels.Get(number);
            board.Load(level, GameRandom.FromSeed(7));
            for (var frame = 0; frame < 240; frame++)
            {
                board.Step(Frame);
            }

            Assert.Equal(level.GoblinCount, board.GoblinsLeft);
            Assert.Equal(0, board.Destroyed);
            for (var piece = 0; piece < board.PieceCount; piece++)
            {
                Assert.True(board.PieceAlive(piece), $"level {number} lost piece {piece}");
                var drift = Vector2.Distance(level.Pieces[piece].Center, board.PiecePosition(piece));
                Assert.True(drift < 0.2f, $"level {number} piece {piece} drifted {drift}");
                Assert.True(MathF.Abs(board.PieceAngle(piece)) < 0.08f, $"level {number} piece {piece} tipped");
            }
        }
    }

    [Fact]
    public void ImpactsBreakAboveTheThresholdAndWearDownBelowIt()
    {
        var strength = FlingBoard.StrengthOf(FlingPieceKind.Block, FlingMaterial.Wood);

        Assert.True(FlingBoard.ResolveImpact(0f, strength, strength, out _));
        Assert.False(FlingBoard.ResolveImpact(0f, strength * 0.9f, strength, out var worn));
        Assert.Equal(0.9f, worn, 4);
        Assert.True(FlingBoard.ResolveImpact(worn, strength * 0.2f + strength * FlingBoard.DamageFloor, strength, out _));
        Assert.False(FlingBoard.ResolveImpact(0.5f, strength * FlingBoard.DamageFloor * 0.9f, strength, out var untouched));
        Assert.Equal(0.5f, untouched);
        Assert.True(FlingBoard.StrengthOf(FlingPieceKind.Block, FlingMaterial.Glass) < strength);
        Assert.True(FlingBoard.StrengthOf(FlingPieceKind.Block, FlingMaterial.Stone) > strength);
        Assert.True(float.IsPositiveInfinity(FlingBoard.StrengthOf(FlingPieceKind.Block, FlingMaterial.Rock)));
    }

    [Fact]
    public void AFastBirdShattersGlassAndScoresIt()
    {
        var board = Loaded("nnn|R 2.6 0 1.4 1.4;Bg 2.6 1.4 1.4;G 30 0");
        const int glass = 1;

        Assert.True(board.Launch(new Vector2(-FlingBoard.MaxPull, 0f)));
        for (var frame = 0; frame < 120 && board.PieceAlive(glass); frame++)
        {
            board.Step(Frame);
        }

        Assert.False(board.PieceAlive(glass));
        Assert.Equal(1, board.Destroyed);
        Assert.Equal(FlingLevels.PointsFor(FlingMaterial.Glass), board.Score);
    }

    [Fact]
    public void AGentleShotOnlyCracksStone()
    {
        var board = Loaded("nnn|R 2.6 0 1.4 1.4;Bs 2.6 1.4 1.4;G 30 0");
        const int stone = 1;

        Assert.True(board.Launch(new Vector2(-0.8f, 0.2f)));
        for (var frame = 0; frame < 120; frame++)
        {
            board.Step(Frame);
        }

        Assert.True(board.PieceAlive(stone));
        Assert.Equal(0, board.Destroyed);
        Assert.True(board.PieceDamage(stone) > 0f);
        Assert.True(board.PieceDamage(stone) < 1f);
    }

    [Fact]
    public void ThePreviewMatchesTheFirstHalfSecondOfFlight()
    {
        var board = Loaded(FarTarget);
        var pull = new Vector2(-1.5f, 0.9f);
        var path = new Vector2[FlingBoard.PreviewPoints];
        board.PreviewPath(pull, path);

        Assert.True(board.Launch(pull));
        for (var point = 0; point < path.Length; point++)
        {
            for (var tick = 0; tick < FlingBoard.PreviewStepsPerPoint; tick++)
            {
                board.Step(PhysicsWorld.StepSeconds);
            }

            var flown = board.BirdPosition(0);
            Assert.True(Vector2.Distance(path[point], flown) < 0.0005f, $"point {point} off by {path[point] - flown}");
        }

        Assert.InRange(PhysicsWorld.StepSeconds * FlingBoard.PreviewStepsPerPoint * path.Length, 0.49f, 0.51f);
    }

    [Fact]
    public void PoppingEveryGoblinWinsAndBanksTheSpareBirds()
    {
        var board = Loaded("nnn|R 2.6 0 1.4 1.4;G 2.6 1.4");

        Assert.True(board.Launch(new Vector2(-FlingBoard.MaxPull, 0f)));
        RunUntilSettled(board);

        Assert.Equal(FlingPhase.Over, board.Phase);
        Assert.True(board.Won);
        Assert.Equal(0, board.GoblinsLeft);
        Assert.Equal(1, board.Popped);
        Assert.Equal(2 * FlingLevels.SpareBirdPoints, board.BirdBonus);
        Assert.Equal(FlingLevels.GoblinPoints + board.BirdBonus, board.Score);
        Assert.Equal(3, board.Stars);
    }

    [Fact]
    public void MissingWithTheLastBirdLosesTheLevel()
    {
        var board = Loaded("n|Hw 30 0 2 1.5;G 30 0");

        Assert.True(board.Launch(new Vector2(-1.2f, -0.6f)));
        RunUntilSettled(board);

        Assert.Equal(FlingPhase.Over, board.Phase);
        Assert.False(board.Won);
        Assert.Equal(0, board.Stars);
        Assert.False(board.Launch(new Vector2(-1.5f, 0.5f)));
    }

    [Fact]
    public void AMissedShotHandsTheNextBirdToTheSling()
    {
        var board = Loaded(FarTarget);

        Assert.True(board.Launch(new Vector2(-1.2f, -0.6f)));
        Assert.Equal(2, board.BirdsLeft);
        RunUntilSettled(board);

        Assert.Equal(FlingPhase.Aiming, board.Phase);
        Assert.Equal(0, board.FlyingCount);
        Assert.True(board.CanAim);
    }

    [Fact]
    public void TheSplitterSplitsOnceIntoThree()
    {
        var board = Loaded("s|Hw 30 0 2 1.5;G 30 0");

        Assert.False(board.Ability());
        Assert.True(board.Launch(new Vector2(-1.6f, 0.8f)));
        board.Step(Frame * 6f);
        Assert.True(board.AbilityReady);
        Assert.True(board.Ability());

        Assert.Equal(3, board.FlyingCount);
        Assert.False(board.Ability());
        var speed = board.BirdVelocity(0).Length();
        for (var bird = 1; bird < board.FlyingCount; bird++)
        {
            Assert.Equal(speed, board.BirdVelocity(bird).Length(), 2);
        }
    }

    [Fact]
    public void ATinyPullDoesNotLaunch()
    {
        var board = Loaded(FarTarget);

        Assert.False(board.Launch(new Vector2(-0.1f, 0.05f)));
        Assert.Equal(FlingPhase.Aiming, board.Phase);
        Assert.Equal(3, board.BirdsLeft);
        var clamped = FlingBoard.ClampPull(new Vector2(-5f, 0f));
        Assert.Equal(FlingBoard.MaxPull, clamped.Length(), 4);
    }

    private static FlingBoard Loaded(string source)
    {
        var board = new FlingBoard();
        board.Load(FlingLevels.Parse(source), GameRandom.FromSeed(3));
        for (var frame = 0; frame < 75; frame++)
        {
            board.Step(Frame);
        }

        return board;
    }

    private static FlingBoard Play(ulong seed)
    {
        var board = new FlingBoard();
        board.Load(FlingLevels.Get(5), GameRandom.FromSeed(seed));
        for (var frame = 0; frame < 75; frame++)
        {
            board.Step(Frame);
        }

        board.Launch(new Vector2(-1.7f, 0.55f));
        for (var frame = 0; frame < 360; frame++)
        {
            board.Step(Frame);
            board.ClearEvents();
        }

        return board;
    }

    private static void RunUntilSettled(FlingBoard board)
    {
        for (var frame = 0; frame < 900 && board.Phase == FlingPhase.Flying; frame++)
        {
            board.Step(Frame);
            board.ClearEvents();
        }
    }
}
