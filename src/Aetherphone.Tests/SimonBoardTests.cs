using System.Text;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Simon;
using Xunit;

namespace Aetherphone.Tests;

public sealed class SimonBoardTests
{
    private const float Frame = 1f / 60f;
    private const int RoundsPlayed = 12;

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = Play(1234, out var firstTrace);
        var second = Play(1234, out var secondTrace);
        Play(99, out var otherTrace);

        Assert.Equal(firstTrace, secondTrace);
        Assert.Equal(first.Length, second.Length);
        Assert.Equal(RoundsPlayed, first.Score);
        Assert.NotEqual(firstTrace, otherTrace);
    }

    [Fact]
    public void TheShowLightsEveryPadInOrderBeforeOpeningInput()
    {
        var board = Fresh(5);
        DriveToInput(board);
        DriveToInput(board, replay: true);
        DriveToInput(board, replay: true);
        Assert.Equal(3, board.Length);

        var shown = new StringBuilder();
        var guard = 0;
        while (board.Phase != SimonPhase.Input && guard++ < 1000)
        {
            board.Step(Frame);
            if (board.PadLitThisStep)
            {
                shown.Append(board.LitPad);
            }
        }

        var expected = new StringBuilder();
        for (var index = 0; index < board.Length; index++)
        {
            expected.Append(board.PadAt(index));
        }

        Assert.Equal(expected.ToString(), shown.ToString());
        Assert.True(board.InputOpenedThisStep);
    }

    [Fact]
    public void ACorrectRoundScoresAndGrowsTheSequence()
    {
        var board = Fresh(5);
        DriveToInput(board);
        Assert.Equal(1, board.Length);
        Assert.Equal(0, board.Score);

        Assert.Equal(SimonPress.RoundComplete, board.Press(board.PadAt(0)));

        Assert.Equal(1, board.Score);
        Assert.Equal(2, board.Length);
        Assert.Equal(SimonPhase.Waiting, board.Phase);
        DriveToInput(board);
        Assert.Equal(SimonPress.Correct, board.Press(board.PadAt(0)));
        Assert.Equal(SimonPress.RoundComplete, board.Press(board.PadAt(1)));
        Assert.Equal(2, board.Score);
    }

    [Fact]
    public void AWrongPadFailsTheRunAndKeepsTheScore()
    {
        var board = Fresh(5);
        DriveToInput(board);
        board.Press(board.PadAt(0));
        DriveToInput(board);
        var wrong = (board.PadAt(0) + 1) % SimonBoard.PadCount;

        Assert.Equal(SimonPress.Wrong, board.Press(wrong));

        Assert.True(board.Over);
        Assert.Equal(1, board.Score);
        Assert.Equal(SimonPress.Ignored, board.Press(board.PadAt(0)));
        board.Step(Frame);
        Assert.Equal(SimonPhase.Failed, board.Phase);
    }

    [Fact]
    public void PressesAreIgnoredWhileTheSequenceIsShown()
    {
        var board = Fresh(5);

        Assert.Equal(SimonPhase.Waiting, board.Phase);
        Assert.Equal(SimonPress.Ignored, board.Press(board.PadAt(0)));
        board.Step(SimonBoard.StartDelaySeconds + Frame);
        Assert.Equal(SimonPhase.Showing, board.Phase);
        Assert.Equal(SimonPress.Ignored, board.Press(board.PadAt(0)));
        Assert.False(board.Over);
    }

    [Fact]
    public void TheShowSpeedsUpEveryFiveRounds()
    {
        var board = Fresh(5);
        Assert.Equal(SimonBoard.BaseOnSeconds, board.OnSeconds);
        for (var round = 0; round < SimonBoard.RampEvery - 1; round++)
        {
            DriveToInput(board, replay: true);
        }

        Assert.Equal(SimonBoard.RampEvery, board.Length);
        Assert.Equal(SimonBoard.BaseOnSeconds, board.OnSeconds);

        DriveToInput(board, replay: true);

        Assert.Equal(SimonBoard.RampEvery + 1, board.Length);
        Assert.True(board.OnSeconds < SimonBoard.BaseOnSeconds);
        Assert.True(board.GapSeconds < SimonBoard.BaseGapSeconds);
    }

    private static SimonBoard Fresh(int seed)
    {
        var board = new SimonBoard();
        board.Reset(GameRandom.FromSeed((ulong)seed));
        return board;
    }

    private static void DriveToInput(SimonBoard board, bool replay = false)
    {
        var guard = 0;
        while (board.Phase != SimonPhase.Input && guard++ < 10000)
        {
            board.Step(Frame);
        }

        if (!replay)
        {
            return;
        }

        for (var index = 0; index < board.Length; index++)
        {
            board.Press(board.PadAt(index));
        }
    }

    private static SimonBoard Play(int seed, out string trace)
    {
        var board = Fresh(seed);
        var builder = new StringBuilder();
        for (var round = 0; round < RoundsPlayed; round++)
        {
            DriveToInput(board);
            for (var index = 0; index < board.Length; index++)
            {
                var pad = board.PadAt(index);
                builder.Append(pad);
                board.Press(pad);
            }

            builder.Append(':');
        }

        trace = builder.ToString();
        return board;
    }
}
