using System.Globalization;
using System.Numerics;
using System.Text;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Slice;
using Xunit;

namespace Aetherphone.Tests;

public sealed class SliceBoardTests
{
    private const float Frame = 1f / 60f;
    private const float Tolerance = 0.001f;
    private const float Row = 8f;

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = Play(1234, out var firstTrace);
        var second = Play(1234, out var secondTrace);
        Play(99, out var otherTrace);

        Assert.Equal(firstTrace, secondTrace);
        Assert.Equal(first.Score, second.Score);
        Assert.Equal(first.Sliced, second.Sliced);
        Assert.Equal(first.Lives, second.Lives);
        Assert.Equal(first.State, second.State);
        Assert.NotEqual(firstTrace, otherTrace);
    }

    [Fact]
    public void ASegmentCutsACircleItCrossesAndMissesOneBesideIt()
    {
        var center = new Vector2(4f, 4f);

        Assert.True(Geometry2D.SegmentCircle(new Vector2(2f, 4f), new Vector2(6f, 4f), center, 0.5f));
        Assert.True(Geometry2D.SegmentCircle(new Vector2(2f, 4.45f), new Vector2(6f, 4.45f), center, 0.5f));
        Assert.True(Geometry2D.SegmentCircle(new Vector2(3.8f, 4f), new Vector2(4.1f, 4f), center, 0.5f));
        Assert.False(Geometry2D.SegmentCircle(new Vector2(2f, 4.6f), new Vector2(6f, 4.6f), center, 0.5f));
        Assert.False(Geometry2D.SegmentCircle(new Vector2(1f, 4f), new Vector2(3.4f, 4f), center, 0.5f));
        Assert.True(Geometry2D.SegmentCircle(new Vector2(3f, 3f), new Vector2(5f, 5f), center, 0.1f));
    }

    [Fact]
    public void ThreeInOneSwipeScoreACombo()
    {
        var board = Seeded(1, SliceMode.Classic);
        SpawnRow(board, 3);

        Swipe(board, 0.4f, 8.6f);

        Assert.Equal(3, board.HitCount);
        Assert.Equal(3, board.Sliced);
        Assert.Equal(0, board.ObjectCount);
        Assert.Equal(6, board.HalfCount);
        board.MoveBlade(new Vector2(8.6f, Row), false, Frame);
        Assert.Equal(3, board.ComboThisFrame);
        Assert.Equal(3, board.ComboBonusThisFrame);
        Assert.Equal(1, board.Combos);
        Assert.Equal(3, board.BestSwipe);
        Assert.Equal(6, board.Score);
    }

    [Fact]
    public void TwoInOneSwipeAreNoCombo()
    {
        var board = Seeded(2, SliceMode.Classic);
        SpawnRow(board, 2);

        Swipe(board, 0.4f, 8.6f);
        board.MoveBlade(new Vector2(8.6f, Row), false, Frame);

        Assert.Equal(2, board.Sliced);
        Assert.Equal(0, board.ComboThisFrame);
        Assert.Equal(0, board.Combos);
        Assert.Equal(2, board.BestSwipe);
        Assert.Equal(2, board.Score);
    }

    [Fact]
    public void APauseInTheSwipeSplitsItIntoTwoStrokes()
    {
        var board = Seeded(3, SliceMode.Classic);
        SpawnRow(board, 4);
        board.MoveBlade(new Vector2(0.4f, Row), true, Frame);
        board.MoveBlade(new Vector2(4.2f, Row), true, Frame);
        Assert.Equal(2, board.Sliced);

        var rested = 0f;
        while (rested < SliceBoard.StrokeBreakSeconds + Frame)
        {
            board.MoveBlade(new Vector2(4.2f, Row), true, Frame);
            rested += Frame;
        }

        board.MoveBlade(new Vector2(8.6f, Row), true, Frame);
        board.MoveBlade(new Vector2(8.6f, Row), false, Frame);

        Assert.Equal(4, board.Sliced);
        Assert.Equal(0, board.Combos);
        Assert.Equal(2, board.BestSwipe);
    }

    [Fact]
    public void ASlowDragDoesNotCut()
    {
        var board = Seeded(4, SliceMode.Classic);
        board.Spawn(SliceKind.Crystal, 0, new Vector2(4.5f, Row), Vector2.Zero);
        board.MoveBlade(new Vector2(3.6f, Row), true, Frame);

        for (var step = 0; step < 30; step++)
        {
            board.MoveBlade(new Vector2(3.6f + step * 0.06f, Row), true, Frame);
        }

        Assert.Equal(0, board.Sliced);
        Assert.Equal(1, board.ObjectCount);
        Assert.False(board.BladeSharp);
    }

    [Fact]
    public void RapidSlicesClimbTheComboMultiplier()
    {
        var board = Seeded(5, SliceMode.Classic);
        SpawnRow(board, 4);

        Swipe(board, 0.4f, 8.6f);

        Assert.Equal(4, board.Combo.Count);
        Assert.Equal(2, board.Combo.Multiplier);
        Assert.Equal(1 + 1 + 1 + 2, board.Score);
    }

    [Fact]
    public void ABombEndsAClassicRun()
    {
        var board = Seeded(6, SliceMode.Classic);
        board.Spawn(SliceKind.Bomb, 0, new Vector2(4.5f, Row), Vector2.Zero);

        Swipe(board, 2f, 7f);

        Assert.True(board.BombThisFrame);
        Assert.Equal(SliceState.Over, board.State);
        Assert.Equal(SliceBoard.StartLives, board.Lives);
        Assert.Equal(1, board.BombsHit);
        Assert.Equal(0, board.HalfCount);
    }

    [Fact]
    public void ABombCostsTenSecondsInArcadeAndBreaksTheCombo()
    {
        var board = Seeded(7, SliceMode.Arcade);
        board.Spawn(SliceKind.Crystal, 0, new Vector2(2f, Row), Vector2.Zero);
        board.Spawn(SliceKind.Bomb, 0, new Vector2(6f, Row), Vector2.Zero);

        Swipe(board, 0.5f, 8.5f);

        Assert.True(board.BombThisFrame);
        Assert.Equal(SliceState.Playing, board.State);
        Assert.Equal(SliceBoard.ArcadeSeconds - SliceBoard.BombPenaltySeconds, board.TimeLeft, Tolerance);
        Assert.Equal(0, board.Combo.Count);
        Assert.Equal(1, board.Sliced);
    }

    [Fact]
    public void ThreeMissesCostALifeAndThreeLivesEndTheRun()
    {
        var board = Seeded(8, SliceMode.Classic);

        for (var miss = 1; miss <= SliceBoard.MissesPerLife; miss++)
        {
            DropMiss(board, SliceKind.Egg);
        }

        Assert.Equal(SliceBoard.StartLives - 1, board.Lives);
        Assert.Equal(0, board.Misses);
        Assert.True(board.LifeLostThisFrame);
        for (var miss = 0; miss < SliceBoard.MissesPerLife * (SliceBoard.StartLives - 1) - 1; miss++)
        {
            DropMiss(board, SliceKind.Crystal);
        }

        Assert.Equal(SliceState.Playing, board.State);
        Assert.Equal(1, board.Lives);
        DropMiss(board, SliceKind.Moogle);
        Assert.Equal(0, board.Lives);
        Assert.Equal(SliceState.Over, board.State);
    }

    [Fact]
    public void FallingBombsAndPickupsAreNotMisses()
    {
        var board = Seeded(9, SliceMode.Classic);

        DropMiss(board, SliceKind.Bomb);
        DropMiss(board, SliceKind.Freeze);

        Assert.Equal(0, board.Misses);
        Assert.Equal(0, board.MissCount);
        Assert.Equal(SliceBoard.StartLives, board.Lives);
    }

    [Fact]
    public void ArcadeMissesNeverCostLivesAndTheClockEndsTheRun()
    {
        var board = Seeded(10, SliceMode.Arcade);
        DropMiss(board, SliceKind.Crystal);
        Assert.Equal(SliceBoard.StartLives, board.Lives);
        Assert.Equal(1, board.MissCount);

        var sawTimeUp = false;
        for (var step = 0; step < (int)((SliceBoard.ArcadeSeconds + 1f) / Frame); step++)
        {
            board.BeginFrame();
            board.Step(Frame);
            sawTimeUp |= board.TimeUpThisFrame;
        }

        Assert.True(sawTimeUp);
        Assert.Equal(SliceState.Over, board.State);
        Assert.Equal(0f, board.TimeLeft);
    }

    [Fact]
    public void TheThrowScheduleIsSeededPeaksInsideTheWorldAndOpensWithoutBombs()
    {
        var first = Schedule(4321, out var firstApexes);
        var second = Schedule(4321, out _);
        var other = Schedule(77, out _);

        Assert.Equal(first, second);
        Assert.NotEqual(first, other);
        Assert.True(firstApexes.Count > 8);
        for (var index = 0; index < firstApexes.Count; index++)
        {
            var apex = firstApexes[index];
            Assert.InRange(apex.Y, SliceBoard.ApexMin - 0.05f, SliceBoard.ApexMax + 0.05f);
            Assert.InRange(apex.X, 0f, SliceBoard.WorldWidth);
        }

        var board = Seeded(4321, SliceMode.Classic);
        for (var step = 0; step < (int)(4.5f / Frame); step++)
        {
            board.Step(Frame);
            for (var index = 0; index < board.ObjectCount; index++)
            {
                Assert.NotEqual(SliceKind.Bomb, board.Object(index).Kind);
            }
        }
    }

    [Fact]
    public void FreezeSlowsTheWorldAndHoldsTheClock()
    {
        var board = Seeded(11, SliceMode.Arcade);
        board.Spawn(SliceKind.Freeze, 0, new Vector2(4.5f, Row), Vector2.Zero);
        Swipe(board, 2f, 7f);
        Assert.True(board.PickupActivated);
        Assert.Equal(SliceKind.Freeze, board.PickupThisFrame);
        var drifter = board.Spawn(SliceKind.Crystal, 0, new Vector2(1f, 4f), new Vector2(3f, 0f));

        board.Step(0.1f);

        Assert.Equal(SliceBoard.ArcadeSeconds, board.TimeLeft, Tolerance);
        Assert.Equal(1f + 3f * 0.1f * SliceBoard.FreezeScale, board.Object(drifter).Position.X, Tolerance);
        Step(board, SliceBoard.FreezeSeconds);
        Assert.Equal(0f, board.FreezeLeft);
        Step(board, 1f);
        Assert.True(board.TimeLeft < SliceBoard.ArcadeSeconds - 0.9f);
    }

    [Fact]
    public void DoublePointsDoubleEverySlice()
    {
        var board = Seeded(12, SliceMode.Arcade);
        board.Spawn(SliceKind.Double, 0, new Vector2(4.5f, Row), Vector2.Zero);
        Swipe(board, 2f, 7f);
        board.MoveBlade(new Vector2(7f, Row), false, Frame);
        Assert.True(board.DoubleLeft > 0f);
        var before = board.Score;

        board.Spawn(SliceKind.Crystal, 1, new Vector2(4.5f, 5f), Vector2.Zero);
        Swipe(board, 2f, 7f, 5f);

        Assert.Equal(2, board.Hit(0).Points);
        Assert.Equal(before + 2 * board.Combo.Multiplier, board.Score);
    }

    [Fact]
    public void FrenzyThrowsFromBothSides()
    {
        var board = Seeded(13, SliceMode.Arcade);
        board.Spawn(SliceKind.Frenzy, 0, new Vector2(4.5f, Row), Vector2.Zero);
        Swipe(board, 2f, 7f);
        Assert.True(board.FrenzyLeft > 0f);

        var fromLeft = 0;
        var fromRight = 0;
        for (var step = 0; step < 20; step++)
        {
            board.Step(Frame);
            for (var index = 0; index < board.ObjectCount; index++)
            {
                var item = board.Object(index);
                if (item.Position.X < 0f && item.Velocity.X > 0f)
                {
                    fromLeft++;
                }
                else if (item.Position.X > SliceBoard.WorldWidth && item.Velocity.X < 0f)
                {
                    fromRight++;
                }
            }
        }

        Assert.True(fromLeft > 0);
        Assert.True(fromRight > 0);
    }

    [Fact]
    public void ASlicedObjectSplitsIntoHalvesThatFlyApartAndFall()
    {
        var board = Seeded(14, SliceMode.Classic);
        board.Spawn(SliceKind.Moogle, 0, new Vector2(4.5f, Row), Vector2.Zero);

        Swipe(board, 2f, 7f);

        Assert.Equal(2, board.HalfCount);
        var firstStart = board.Half(0).Position;
        var secondStart = board.Half(1).Position;
        var gapBefore = Vector2.Distance(firstStart, secondStart);
        Step(board, 0.5f);
        Assert.True(Vector2.Distance(board.Half(0).Position, board.Half(1).Position) > gapBefore + 0.5f);
        Step(board, 1f);
        Assert.True(board.Half(0).Position.Y > Row);
        Step(board, SliceBoard.HalfLife);
        Assert.Equal(0, board.HalfCount);
    }

    private static SliceBoard Seeded(ulong seed, SliceMode mode)
    {
        var board = new SliceBoard();
        board.Reset(GameRandom.FromSeed(seed), mode);
        return board;
    }

    private static void SpawnRow(SliceBoard board, int count)
    {
        for (var index = 0; index < count; index++)
        {
            var x = 1.2f + index * (6.6f / Math.Max(1, count - 1));
            board.Spawn(SliceKind.Crystal, (byte)index, new Vector2(x, Row), Vector2.Zero);
        }
    }

    private static void Swipe(SliceBoard board, float fromX, float toX, float y = Row)
    {
        board.BeginFrame();
        board.MoveBlade(new Vector2(fromX, y), true, Frame);
        board.MoveBlade(new Vector2(toX, y), true, Frame);
    }

    private static void DropMiss(SliceBoard board, SliceKind kind)
    {
        board.BeginFrame();
        board.Spawn(kind, 0, new Vector2(4.5f, SliceBoard.MissY + 0.1f), new Vector2(0f, 4f));
        board.Step(Frame);
    }

    private static void Step(SliceBoard board, float seconds)
    {
        for (var elapsed = 0f; elapsed < seconds; elapsed += Frame)
        {
            board.BeginFrame();
            board.Step(Frame);
        }
    }

    private static string Schedule(ulong seed, out List<Vector2> apexes)
    {
        apexes = new List<Vector2>();
        var board = Seeded(seed, SliceMode.Classic);
        var trace = new StringBuilder();
        for (var step = 0; step < (int)(20f / Frame); step++)
        {
            board.Step(Frame);
            for (var index = 0; index < board.ObjectCount; index++)
            {
                var item = board.Object(index);
                if (item.Position.Y < SliceBoard.SpawnY - 0.6f || item.Velocity.Y >= 0f)
                {
                    continue;
                }

                var rise = item.Velocity.Y * item.Velocity.Y / (2f * SliceBoard.Gravity);
                var flight = -item.Velocity.Y / SliceBoard.Gravity;
                if (item.Position.Y > SliceBoard.SpawnY - 0.4f)
                {
                    apexes.Add(new Vector2(item.Position.X + item.Velocity.X * flight, item.Position.Y - rise));
                }

                trace.Append(step).Append(':').Append(item.Kind).Append('@')
                    .Append(item.Position.X.ToString("F3", CultureInfo.InvariantCulture)).Append(';');
            }
        }

        return trace.ToString();
    }

    private static SliceBoard Play(ulong seed, out string trace)
    {
        var board = Seeded(seed, SliceMode.Classic);
        var builder = new StringBuilder();
        var time = 0f;
        for (var step = 0; step < (int)(40f / Frame) && board.State == SliceState.Playing; step++)
        {
            time += Frame;
            board.BeginFrame();
            var blade = new Vector2(4.5f + 3.6f * MathF.Sin(time * 2.1f), 6f + 2.4f * MathF.Sin(time * 3.3f));
            board.MoveBlade(blade, step % 90 < 70, Frame);
            board.Step(Frame);
            if (step % 15 != 0)
            {
                continue;
            }

            builder.Append(board.Score).Append(',').Append(board.ObjectCount).Append(',').Append(board.HalfCount)
                .Append(',').Append(board.Lives).Append(',').Append(board.Misses).Append(';');
        }

        trace = builder.ToString();
        return board;
    }
}
