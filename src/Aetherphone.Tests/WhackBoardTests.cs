using System.Text;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Whack;
using Xunit;

namespace Aetherphone.Tests;

public sealed class WhackBoardTests
{
    private const float Frame = 1f / 60f;
    private const float RaiseSeconds = 0.3f;

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = Play(1234, out var firstTrace);
        var second = Play(1234, out var secondTrace);
        Play(99, out var otherTrace);

        Assert.Equal(firstTrace, secondTrace);
        Assert.Equal(first.Score, second.Score);
        Assert.Equal(first.MolesWhacked, second.MolesWhacked);
        Assert.Equal(first.BestCombo, second.BestCombo);
        Assert.True(first.Over);
        Assert.NotEqual(firstTrace, otherTrace);
    }

    [Fact]
    public void ABombCostsPointsAndTimeAndResetsTheCombo()
    {
        var board = Raised(1);
        board.Place(0, Occupant.Mole);
        board.Step(RaiseSeconds);
        Assert.Equal(WhackResult.Mole, board.Whack(0));
        Assert.Equal(WhackBoard.MolePoints, board.Score);
        Assert.Equal(1, board.Combo.Count);

        board.Place(4, Occupant.Bomb);
        board.Step(RaiseSeconds);
        Assert.Equal(WhackResult.Bomb, board.Whack(4));

        Assert.Equal(0, board.Score);
        Assert.Equal(0, board.Combo.Count);
        Assert.True(board.TimeLeft < WhackBoard.RoundSeconds - RaiseSeconds * 2f - WhackBoard.BombTimePenalty + 0.001f);
    }

    [Fact]
    public void ABombChainKnocksNeighbouringMolesAndBombsAndCountsTheMolesAsHits()
    {
        var board = Raised(7);
        board.Place(4, Occupant.Bomb);
        board.Place(1, Occupant.Mole);
        board.Place(3, Occupant.Mole);
        board.Place(5, Occupant.Bomb);
        board.Place(2, Occupant.Mole);
        board.Place(8, Occupant.Mole);
        board.Step(RaiseSeconds);

        Assert.Equal(WhackResult.Bomb, board.Whack(4));

        Assert.Equal((1 << 1) | (1 << 3) | (1 << 5) | (1 << 2) | (1 << 8), board.ChainMask);
        Assert.True(board.WhackedAt(1));
        Assert.True(board.WhackedAt(3));
        Assert.True(board.WhackedAt(5));
        Assert.True(board.WhackedAt(2));
        Assert.True(board.WhackedAt(8));
        Assert.False(board.WhackedAt(7));
        Assert.Equal(4, board.MolesWhacked);
        Assert.Equal(4, board.Combo.Count);
        var fourthHit = WhackBoard.MolePoints * ComboMeter.MultiplierFor(4);
        Assert.Equal(3 * WhackBoard.MolePoints + fourthHit, board.Score);
    }

    [Fact]
    public void EightHitsInARowStartAFrenzyThatDoublesPoints()
    {
        var board = Raised(3);
        for (var hole = 0; hole < WhackBoard.HoleCount; hole++)
        {
            board.Place(hole, Occupant.Mole);
        }

        board.Step(RaiseSeconds);
        for (var hole = 0; hole < WhackBoard.FrenzyCombo - 1; hole++)
        {
            Assert.Equal(WhackResult.Mole, board.Whack(hole));
            Assert.False(board.Frenzy);
        }

        Assert.Equal(WhackResult.Mole, board.Whack(WhackBoard.FrenzyCombo - 1));
        Assert.True(board.FrenzyStarted);
        Assert.True(board.Frenzy);
        var scoreBefore = board.Score;

        Assert.Equal(WhackResult.Mole, board.Whack(WhackBoard.FrenzyCombo));

        var expected = WhackBoard.MolePoints * ComboMeter.MultiplierFor(WhackBoard.FrenzyCombo + 1) * 2;
        Assert.Equal(expected, board.GainAt(WhackBoard.FrenzyCombo));
        Assert.Equal(scoreBefore + expected, board.Score);
    }

    [Fact]
    public void AFrenzyFillsEveryEmptyHoleWithMoles()
    {
        var board = Raised(5);
        for (var hole = 0; hole < WhackBoard.FrenzyCombo; hole++)
        {
            board.Place(hole, Occupant.Mole);
        }

        board.Step(RaiseSeconds);
        for (var hole = 0; hole < WhackBoard.FrenzyCombo; hole++)
        {
            board.Whack(hole);
        }

        Assert.True(board.Frenzy);
        Assert.Equal(Occupant.None, board.KindAt(WhackBoard.HoleCount - 1));

        board.Step(Frame);

        Assert.Equal(Occupant.Mole, board.KindAt(WhackBoard.HoleCount - 1));
    }

    [Fact]
    public void TheRoundEndsWhenTheClockRunsOut()
    {
        var board = Raised(11);
        for (var frame = 0; frame < 60 * 61; frame++)
        {
            board.Step(Frame);
        }

        Assert.True(board.Over);
        Assert.Equal(0f, board.TimeLeft);
        Assert.Equal(WhackResult.None, board.Whack(0));
    }

    private static WhackBoard Raised(int seed)
    {
        var board = new WhackBoard();
        board.Reset(GameRandom.FromSeed((ulong)seed));
        return board;
    }

    private static WhackBoard Play(int seed, out string trace)
    {
        var board = Raised(seed);
        var builder = new StringBuilder();
        for (var frame = 0; frame < 60 * 61; frame++)
        {
            board.Step(Frame);
            if (frame % 9 == 0)
            {
                WhackFirstMole(board);
            }

            if (frame % 60 != 0)
            {
                continue;
            }

            for (var hole = 0; hole < WhackBoard.HoleCount; hole++)
            {
                builder.Append((int)board.KindAt(hole));
            }

            builder.Append(':').Append(board.Score).Append(' ');
        }

        trace = builder.ToString();
        return board;
    }

    private static void WhackFirstMole(WhackBoard board)
    {
        for (var hole = 0; hole < WhackBoard.HoleCount; hole++)
        {
            if (board.KindAt(hole) == Occupant.Mole && board.AliveAt(hole))
            {
                board.Whack(hole);
                return;
            }
        }
    }
}
