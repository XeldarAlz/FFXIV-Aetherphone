using System.Text;
using Aetherphone.Apps.Games.Blade;
using Aetherphone.Apps.Games.Framework;
using Xunit;

namespace Aetherphone.Tests;

public sealed class BladeBoardTests
{
    private const float Frame = 1f / 60f;
    private const float AngleTolerance = 0.02f;

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = Play(1234, out var firstTrace);
        var second = Play(1234, out var secondTrace);
        Play(99, out var otherTrace);

        Assert.Equal(firstTrace, secondTrace);
        Assert.Equal(first.Score, second.Score);
        Assert.Equal(first.Level, second.Level);
        Assert.Equal(first.Apples, second.Apples);
        Assert.Equal(first.State, second.State);
        Assert.NotEqual(firstTrace, otherTrace);
    }

    [Fact]
    public void AThrowSticksAtTheImpactAngleAndTurnsWithTheWheel()
    {
        var board = Seeded(1);

        Assert.Equal(BladeThrow.Stuck, ThrowAndResolve(board));

        Assert.Equal(1, board.StuckCount);
        Assert.Equal(1, board.Score - board.Apples * BladeBoard.ApplePoints);
        Assert.Equal(board.LevelBlades - 1, board.Remaining);
        Assert.Equal(0f, BladeBoard.Normalize(board.StuckAngle(0) - BladeBoard.ImpactAngle), AngleTolerance);
        var local = board.StuckAngle(0) - board.WheelAngle;
        Settle(board, 0.5f);
        Assert.Equal(local, board.StuckAngle(0) - board.WheelAngle, AngleTolerance);
    }

    [Fact]
    public void AThrowInsideTheCollisionArcOfAStuckBladeIsBlocked()
    {
        var board = Seeded(2);
        Assert.Equal(BladeThrow.Stuck, ThrowAndResolve(board));

        Assert.Equal(BladeThrow.Blocked, ThrowAndResolve(board));

        Assert.Equal(BladeState.Over, board.State);
        Assert.Equal(1, board.StuckCount);
        Assert.False(board.Throw());
    }

    [Fact]
    public void AThrowClearOfTheArcSticksBesideTheFirstBlade()
    {
        var board = Seeded(3);
        Assert.Equal(BladeThrow.Stuck, ThrowAndResolve(board));
        Settle(board, 0.5f);

        Assert.Equal(BladeThrow.Stuck, ThrowAndResolve(board));

        Assert.Equal(2, board.StuckCount);
        Assert.Equal(BladeState.Playing, board.State);
        Assert.True(MathF.Abs(BladeBoard.Normalize(board.StuckAngle(0) - board.StuckAngle(1))) >= BladeBoard.BladeArc);
    }

    [Fact]
    public void SlicingAnAppleAddsBonusPointsAndRemovesIt()
    {
        var board = Seeded(4);
        var resolveFrames = (int)MathF.Ceiling(BladeBoard.FlightSeconds / Frame);
        var wheelAtImpact = board.WheelAngle + board.Direction * 1.45f * resolveFrames * Frame;
        board.PlaceApple(BladeBoard.ImpactAngle - wheelAtImpact);
        var applesBefore = board.AppleCount;

        Assert.Equal(BladeThrow.Stuck, ThrowAndResolve(board));

        Assert.True(board.AppleHitThisStep);
        Assert.Equal(1, board.Apples);
        Assert.Equal(1 + BladeBoard.ApplePoints, board.Score);
        Assert.Equal(applesBefore - 1, board.AppleCount);
    }

    [Fact]
    public void ClearingEveryBladeAdvancesTheLevelAfterTheHold()
    {
        var board = Seeded(5);
        var result = BladeThrow.None;
        for (var blade = 0; blade < board.LevelBlades; blade++)
        {
            result = ThrowAndResolve(board);
            Settle(board, 0.6f);
        }

        Assert.Equal(BladeThrow.LevelCleared, result);
        Assert.Equal(BladeState.Cleared, board.State);
        Assert.Equal(1, board.Level);
        Settle(board, BladeBoard.ClearedSeconds);

        Assert.Equal(2, board.Level);
        Assert.Equal(BladeState.Playing, board.State);
        Assert.Equal(0, board.StuckCount);
    }

    [Fact]
    public void BossWheelsReverseDirectionAtHalfTheBlades()
    {
        var board = Seeded(7);
        board.StartLevel(BladeBoard.BossEvery);
        Assert.True(board.IsBoss);
        var direction = board.Direction;
        var reversed = false;
        for (var blade = 0; blade < board.LevelBlades && !reversed; blade++)
        {
            Assert.Equal(BladeThrow.Stuck, ThrowAndResolve(board));
            reversed = board.ReversedThisStep;
            if (!reversed)
            {
                Assert.Equal(direction, board.Direction);
            }

            Settle(board, 0.4f);
        }

        Assert.True(reversed);
        Assert.Equal(board.LevelBlades / 2, board.Remaining);
        Assert.Equal(-direction, board.Direction);
    }

    private static BladeBoard Seeded(int seed)
    {
        var board = new BladeBoard();
        board.Reset(GameRandom.FromSeed((ulong)seed));
        return board;
    }

    private static BladeThrow ThrowAndResolve(BladeBoard board)
    {
        Assert.True(board.Throw());
        for (var frame = 0; frame < 60; frame++)
        {
            var result = board.Step(Frame);
            if (result != BladeThrow.None)
            {
                return result;
            }
        }

        return BladeThrow.None;
    }

    private static void Settle(BladeBoard board, float seconds)
    {
        for (var elapsed = 0f; elapsed < seconds; elapsed += Frame)
        {
            board.Step(Frame);
        }
    }

    private static BladeBoard Play(int seed, out string trace)
    {
        var board = Seeded(seed);
        var builder = new StringBuilder();
        for (var frame = 0; frame < 60 * 90 && board.State != BladeState.Over; frame++)
        {
            if (frame % 30 == 0)
            {
                board.Throw();
            }

            var result = board.Step(Frame);
            if (result != BladeThrow.None)
            {
                builder.Append((int)result).Append(':').Append(board.Score).Append(':').Append(board.Level).Append(' ');
            }

            if (board.AppleHitThisStep)
            {
                builder.Append('a');
            }

            if (board.ReversedThisStep)
            {
                builder.Append('r');
            }
        }

        builder.Append('|').Append(board.StuckCount).Append('/').Append(board.AppleCount);
        trace = builder.ToString();
        return board;
    }
}
