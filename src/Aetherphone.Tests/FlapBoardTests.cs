using System.Text;
using Aetherphone.Apps.Games.Flap;
using Aetherphone.Apps.Games.Framework;
using Xunit;

namespace Aetherphone.Tests;

public sealed class FlapBoardTests
{
    private const float Frame = 1f / 60f;

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = Play(1234, out var firstTrace);
        var second = Play(1234, out var secondTrace);
        Play(99, out var otherTrace);

        Assert.Equal(firstTrace, secondTrace);
        Assert.Equal(first.Score, second.Score);
        Assert.Equal(first.BirdX, second.BirdX);
        Assert.Equal(first.BirdY, second.BirdY);
        Assert.Equal(first.State, second.State);
        Assert.NotEqual(firstTrace, otherTrace);
    }

    [Fact]
    public void PipesAreSpacedEvenlyWithGapsInsideTheWorld()
    {
        var board = Seeded(7);

        Assert.True(board.PipeCount >= 3);
        Assert.Equal(FlapBoard.FirstPipeDistance, board.PipeAt(0).X, 3);
        for (var index = 0; index < board.PipeCount; index++)
        {
            ref readonly var pipe = ref board.PipeAt(index);
            if (index > 0)
            {
                Assert.Equal(FlapBoard.PipeSpacing, pipe.X - board.PipeAt(index - 1).X, 3);
            }

            Assert.InRange(pipe.GapHalf, FlapBoard.GapHalfMin, FlapBoard.GapHalfStart);
            Assert.True(pipe.GapCenter - pipe.GapHalf >= FlapBoard.EdgeMargin - 0.001f);
            Assert.True(pipe.GapCenter + pipe.GapHalf <= FlapBoard.WorldHeight - FlapBoard.EdgeMargin + 0.001f);
        }
    }

    [Fact]
    public void TheBirdWaitsUntilTheFirstFlap()
    {
        var board = Seeded(3);
        var restingY = board.BirdY;
        for (var frame = 0; frame < 120; frame++)
        {
            board.Step(Frame);
        }

        Assert.Equal(FlapState.Ready, board.State);
        Assert.Equal(restingY, board.BirdY);
        Assert.Equal(0f, board.BirdX);

        board.Flap();

        Assert.Equal(FlapState.Playing, board.State);
        Assert.Equal(1, board.Flaps);
    }

    [Fact]
    public void FallingToTheGroundEndsTheRunAtOnce()
    {
        var board = Seeded(5);
        board.Flap();
        var died = false;
        for (var frame = 0; frame < 600 && board.State != FlapState.Over; frame++)
        {
            board.Step(Frame);
            died |= board.DiedThisStep;
        }

        Assert.True(died);
        Assert.Equal(FlapState.Over, board.State);
        Assert.Equal(FlapDeath.Ground, board.Death);
        Assert.Equal(FlapBoard.WorldHeight - FlapBoard.BirdRadius, board.BirdY, 3);
        Assert.Equal(0, board.Score);
    }

    [Fact]
    public void HuggingTheCeilingHitsTheFirstPipeAndTheBirdTumblesToTheGround()
    {
        var board = Seeded(11);
        var died = false;
        for (var frame = 0; frame < 600 && board.State is FlapState.Ready or FlapState.Playing; frame++)
        {
            board.Flap();
            board.Step(Frame);
            died |= board.DiedThisStep;
        }

        Assert.True(died);
        Assert.Equal(FlapDeath.Pipe, board.Death);
        Assert.Equal(FlapState.Dying, board.State);
        Assert.InRange(board.BirdX, FlapBoard.FirstPipeDistance - FlapBoard.BirdRadius - 0.2f,
            FlapBoard.FirstPipeDistance + FlapBoard.PipeWidth + FlapBoard.BirdRadius);
        var progressX = board.BirdX;
        for (var frame = 0; frame < 240 && board.State != FlapState.Over; frame++)
        {
            board.Step(Frame);
        }

        Assert.Equal(FlapState.Over, board.State);
        Assert.Equal(progressX, board.BirdX);
        Assert.Equal(FlapBoard.WorldHeight - FlapBoard.BirdRadius, board.BirdY, 3);
    }

    [Fact]
    public void EveryPipePassedScoresExactlyOnce()
    {
        var board = Seeded(21);
        board.Flap();
        var scoreEvents = 0;
        for (var frame = 0; frame < 60 * 30 && board.State == FlapState.Playing; frame++)
        {
            Autopilot(board);
            board.Step(Frame);
            if (board.ScoredThisStep)
            {
                scoreEvents++;
            }
        }

        var passed = (int)MathF.Floor((board.BirdX - FlapBoard.FirstPipeDistance - FlapBoard.PipeWidth) / FlapBoard.PipeSpacing) + 1;
        Assert.True(board.Score > 0);
        Assert.Equal(Math.Max(0, passed), board.Score);
        Assert.Equal(board.Score, scoreEvents);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(9, 0)]
    [InlineData(10, 1)]
    [InlineData(24, 1)]
    [InlineData(25, 2)]
    [InlineData(49, 2)]
    [InlineData(50, 3)]
    [InlineData(120, 3)]
    public void MedalTiersLandAtTenTwentyFiveAndFiftyPipes(int pipes, int expected)
    {
        Assert.Equal(expected, FlapBoard.MedalTier(pipes));
    }

    [Fact]
    public void TheGapNarrowsWithEveryPipeDownToTheMinimum()
    {
        var board = Seeded(2);
        board.Flap();
        var narrowest = float.MaxValue;
        var widest = 0f;
        for (var frame = 0; frame < 60 * 40 && board.State == FlapState.Playing; frame++)
        {
            Autopilot(board);
            board.Step(Frame);
            for (var index = 0; index < board.PipeCount; index++)
            {
                narrowest = MathF.Min(narrowest, board.PipeAt(index).GapHalf);
                widest = MathF.Max(widest, board.PipeAt(index).GapHalf);
            }
        }

        Assert.Equal(FlapBoard.GapHalfStart, widest, 3);
        Assert.True(narrowest < FlapBoard.GapHalfStart);
        Assert.True(narrowest >= FlapBoard.GapHalfMin);
    }

    private static FlapBoard Seeded(int seed)
    {
        var board = new FlapBoard();
        board.Reset(GameRandom.FromSeed((ulong)seed));
        return board;
    }

    private static void Autopilot(FlapBoard board)
    {
        if (!board.TryNextGap(out var gapCenter))
        {
            return;
        }

        if (board.BirdY > gapCenter + 0.9f && board.BirdVelocity > -2f)
        {
            board.Flap();
        }
    }

    private static FlapBoard Play(int seed, out string trace)
    {
        var board = Seeded(seed);
        var builder = new StringBuilder();
        board.Flap();
        for (var frame = 0; frame < 60 * 30 && board.State == FlapState.Playing; frame++)
        {
            if (frame % 23 == 0)
            {
                board.Flap();
            }

            board.Step(Frame);
            if (board.ScoredThisStep)
            {
                builder.Append(board.Score).Append('@').Append(board.BirdX.ToString("F3")).Append(' ');
            }
        }

        for (var index = 0; index < board.PipeCount; index++)
        {
            builder.Append('|').Append(board.PipeAt(index).GapCenter.ToString("F3"));
        }

        trace = builder.ToString();
        return board;
    }
}
