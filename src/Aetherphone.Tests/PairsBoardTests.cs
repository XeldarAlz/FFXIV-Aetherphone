using System.Text;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Pairs;
using Xunit;

namespace Aetherphone.Tests;

public sealed class PairsBoardTests
{
    private const float Frame = 1f / 60f;

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = Play(1234, out var firstTrace);
        var second = Play(1234, out var secondTrace);
        Play(99, out var otherTrace);

        Assert.Equal(firstTrace, secondTrace);
        Assert.Equal(first.Attempts, second.Attempts);
        Assert.Equal(first.BestStreak, second.BestStreak);
        Assert.True(first.Over);
        Assert.NotEqual(firstTrace, otherTrace);
    }

    [Fact]
    public void EveryCardHasExactlyOneTwin()
    {
        var board = Fresh(5);
        var counts = new int[PairsBoard.PairCount];
        for (var index = 0; index < PairsBoard.CardCount; index++)
        {
            counts[board.Symbol(index)]++;
        }

        for (var symbol = 0; symbol < PairsBoard.PairCount; symbol++)
        {
            Assert.Equal(2, counts[symbol]);
        }
    }

    [Fact]
    public void AMatchingPairStaysUpAndLandsInTheTray()
    {
        var board = Fresh(5);
        var first = 0;
        var twin = TwinOf(board, first);

        Assert.True(board.Reveal(first));
        Assert.Equal(PairsPhase.Selecting, board.Phase);
        Assert.True(board.Reveal(twin));
        Assert.Equal(PairsPhase.Revealing, board.Phase);
        Assert.Equal(1, board.Attempts);

        Run(board, PairsBoard.RevealSeconds);
        Assert.Equal(PairsPhase.Matched, board.Phase);
        Assert.Equal(1, board.Streak);

        Run(board, PairsBoard.MatchSeconds);
        Assert.Equal(PairsPhase.Selecting, board.Phase);
        Assert.Equal(CardState.Matched, board.State(first));
        Assert.Equal(CardState.Matched, board.State(twin));
        Assert.Equal(0, board.TraySlot(board.Symbol(first)));
        Assert.Equal(1, board.MatchedPairs);
        Assert.False(board.CanReveal(first));
    }

    [Fact]
    public void AMismatchHidesBothCardsAndResetsTheStreak()
    {
        var board = Fresh(5);
        var first = 0;
        var other = NotTwinOf(board, first);

        board.Reveal(first);
        board.Reveal(other);
        Run(board, PairsBoard.RevealSeconds);

        Assert.Equal(PairsPhase.Mismatched, board.Phase);
        Assert.Equal(0, board.Streak);
        Assert.False(board.CanReveal(TwinOf(board, first)));

        Run(board, PairsBoard.MismatchSeconds);

        Assert.Equal(PairsPhase.Selecting, board.Phase);
        Assert.Equal(CardState.FaceDown, board.State(first));
        Assert.Equal(CardState.FaceDown, board.State(other));
        Assert.Equal(1, board.Attempts);
        Assert.True(board.CanReveal(first));
    }

    [Fact]
    public void TheSameCardCannotBeRevealedTwiceAndTheClockStopsOnTheWin()
    {
        var board = Fresh(3);
        Assert.True(board.Reveal(4));
        Assert.False(board.Reveal(4));
        Assert.True(board.CanReveal(5));

        var finished = Play(3, out _);
        var elapsed = finished.Elapsed;
        finished.Step(1f);

        Assert.True(finished.Over);
        Assert.Equal(elapsed, finished.Elapsed);
        Assert.Equal(PairsBoard.PairCount, finished.MatchedPairs);
    }

    private static PairsBoard Fresh(int seed)
    {
        var board = new PairsBoard();
        board.Reset(GameRandom.FromSeed((ulong)seed));
        return board;
    }

    private static void Run(PairsBoard board, float seconds)
    {
        var frames = (int)MathF.Ceiling(seconds / Frame) + 1;
        for (var frame = 0; frame < frames; frame++)
        {
            board.Step(Frame);
        }
    }

    private static int TwinOf(PairsBoard board, int index)
    {
        for (var other = 0; other < PairsBoard.CardCount; other++)
        {
            if (other != index && board.Symbol(other) == board.Symbol(index))
            {
                return other;
            }
        }

        return -1;
    }

    private static int NotTwinOf(PairsBoard board, int index)
    {
        for (var other = 0; other < PairsBoard.CardCount; other++)
        {
            if (other != index && board.Symbol(other) != board.Symbol(index))
            {
                return other;
            }
        }

        return -1;
    }

    private static PairsBoard Play(int seed, out string trace)
    {
        var board = Fresh(seed);
        var builder = new StringBuilder();
        for (var index = 0; index < PairsBoard.CardCount; index++)
        {
            builder.Append(board.Symbol(index));
        }

        var guard = 0;
        while (!board.Over && guard++ < 6000)
        {
            board.Step(Frame);
            if (board.Phase != PairsPhase.Selecting)
            {
                continue;
            }

            var pick = board.FirstCard < 0 ? FirstFaceDown(board) : SecondPick(board);
            if (pick < 0)
            {
                break;
            }

            board.Reveal(pick);
            builder.Append(':').Append(pick);
        }

        builder.Append('|').Append(board.Attempts).Append('|').Append(board.BestStreak);
        trace = builder.ToString();
        return board;
    }

    private static int SecondPick(PairsBoard board)
    {
        var first = board.FirstCard;
        if (board.Attempts % 3 != 1)
        {
            return TwinOf(board, first);
        }

        for (var other = 0; other < PairsBoard.CardCount; other++)
        {
            if (board.CanReveal(other) && board.Symbol(other) != board.Symbol(first))
            {
                return other;
            }
        }

        return TwinOf(board, first);
    }

    private static int FirstFaceDown(PairsBoard board)
    {
        for (var index = 0; index < PairsBoard.CardCount; index++)
        {
            if (board.CanReveal(index))
            {
                return index;
            }
        }

        return -1;
    }
}
