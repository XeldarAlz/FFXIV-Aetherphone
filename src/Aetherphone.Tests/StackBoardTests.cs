using System.Globalization;
using System.Text;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Stack;
using Xunit;

namespace Aetherphone.Tests;

public sealed class StackBoardTests
{
    private const float Frame = 1f / 60f;
    private const float Tolerance = 0.001f;

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = Play(1234, out var firstTrace);
        var second = Play(1234, out var secondTrace);
        Play(99, out var otherTrace);

        Assert.Equal(firstTrace, secondTrace);
        Assert.Equal(first.Score, second.Score);
        Assert.Equal(first.Level, second.Level);
        Assert.Equal(first.State, second.State);
        Assert.Equal(first.ColorOffset, second.ColorOffset);
        Assert.NotEqual(firstTrace, otherTrace);
    }

    [Fact]
    public void APerfectDropKeepsTheWidthAndExtendsTheStreak()
    {
        var board = Seeded(1);

        Assert.Equal(StackDrop.Perfect, board.DropAt(0.5f));

        Assert.Equal(2, board.Level);
        Assert.Equal(2, board.Score);
        Assert.Equal(1, board.Perfects);
        Assert.Equal(1, board.Combo.Count);
        Assert.Equal(StackBoard.StartWidth, board.MovingWidth, Tolerance);
        Assert.Equal(0, board.SliceCount);
    }

    [Fact]
    public void AnOffsetDropSlicesTheOverhangIntoATumblingSlab()
    {
        var board = Seeded(2);

        Assert.Equal(StackDrop.Placed, board.DropAt(0.6f));

        Assert.Equal(1, board.Score);
        Assert.Equal(0, board.Combo.Count);
        Assert.Equal(StackBoard.StartWidth - 0.1f, board.MovingWidth, Tolerance);
        Assert.Equal(1, board.SliceCount);
        var slice = board.Slice(0);
        Assert.Equal(0.1f, slice.Width, Tolerance);
        Assert.Equal(0.84f, board.LastSliceCenterX, Tolerance);
        for (var frame = 0; frame < 60; frame++)
        {
            board.Step(Frame);
        }

        Assert.True(board.Slice(0).Level < slice.Level);
        Assert.NotEqual(0f, board.Slice(0).Rotation);
        for (var frame = 0; frame < (int)(StackBoard.SliceLife / Frame) + 2; frame++)
        {
            board.Step(Frame);
        }

        Assert.Equal(0, board.SliceCount);
    }

    [Fact]
    public void FourPerfectsInARowRebuildTheWidth()
    {
        var board = Seeded(3);
        board.DropAt(0.6f);
        var narrowed = board.MovingWidth;
        for (var perfect = 0; perfect < StackBoard.PerfectsPerReward - 1; perfect++)
        {
            Assert.Equal(StackDrop.Perfect, board.DropAt(board.Block(board.Level - 1).CenterX));
            Assert.False(board.WidenedThisDrop);
        }

        Assert.Equal(narrowed, board.MovingWidth, Tolerance);
        Assert.Equal(StackDrop.Perfect, board.DropAt(board.Block(board.Level - 1).CenterX));
        Assert.True(board.WidenedThisDrop);
        Assert.Equal(narrowed + StackBoard.RewardWidth, board.MovingWidth, Tolerance);
        Assert.Equal(StackBoard.PerfectsPerReward, board.BestCombo);
    }

    [Fact]
    public void MissingTheTowerEndsTheRun()
    {
        var board = Seeded(4);
        Assert.Equal(StackDrop.Placed, board.DropAt(0.6f));
        Assert.Equal(StackDrop.Placed, board.DropAt(0.9f));
        Assert.Equal(StackDrop.Placed, board.DropAt(1f));

        Assert.Equal(StackDrop.Missed, board.DropAt(1f));

        Assert.Equal(StackState.Over, board.State);
        Assert.Equal(3, board.Score);
        Assert.Equal(0, board.Combo.Count);
        Assert.Equal(StackDrop.None, board.DropAt(0.5f));
    }

    [Fact]
    public void TheMovingBlockStaysBetweenTheWalls()
    {
        var board = Seeded(6);
        var half = board.MovingWidth * 0.5f;
        var reachedLeft = false;
        var reachedRight = false;
        for (var frame = 0; frame < 60 * 5; frame++)
        {
            board.Step(Frame);
            Assert.InRange(board.MovingCenterX, half - Tolerance, 1f - half + Tolerance);
            reachedLeft |= board.MovingCenterX <= half + Tolerance;
            reachedRight |= board.MovingCenterX >= 1f - half - Tolerance;
        }

        Assert.True(reachedLeft);
        Assert.True(reachedRight);
    }

    private static StackBoard Seeded(int seed)
    {
        var board = new StackBoard();
        board.Reset(GameRandom.FromSeed((ulong)seed));
        return board;
    }

    private static StackBoard Play(int seed, out string trace)
    {
        var board = Seeded(seed);
        var builder = new StringBuilder();
        builder.Append(board.ColorOffset).Append('#');
        for (var frame = 0; frame < 60 * 30 && board.State == StackState.Playing; frame++)
        {
            board.Step(Frame);
            if (frame % 50 == 0)
            {
                builder.Append((int)board.Drop());
                for (var slice = 0; slice < board.SliceCount; slice++)
                {
                    builder.Append(':').Append(board.Slice(slice).Spin.ToString("F3", CultureInfo.InvariantCulture));
                }
            }

            if (frame % 60 != 0)
            {
                continue;
            }

            builder.Append('|').Append(board.Level).Append('/').Append(board.Score).Append('/')
                .Append(board.MovingCenterX.ToString("F3", CultureInfo.InvariantCulture));
        }

        trace = builder.ToString();
        return board;
    }
}
