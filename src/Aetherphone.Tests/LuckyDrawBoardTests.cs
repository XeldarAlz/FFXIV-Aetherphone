using System.Text;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.LuckyDraw;
using Xunit;

namespace Aetherphone.Tests;

public sealed class LuckyDrawBoardTests
{
    private const int StepGuard = 200;
    private const int MatchGuard = 40000;

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = PlayMatch(1234, 4, out var firstTrace);
        var second = PlayMatch(1234, 4, out var secondTrace);
        PlayMatch(99, 4, out var otherTrace);

        Assert.Equal(firstTrace, secondTrace);
        Assert.Equal(first.Winner, second.Winner);
        Assert.Equal(first.Round, second.Round);
        Assert.Equal(LuckyPhase.MatchOver, first.Phase);
        Assert.True(first.Total(first.Winner) >= LuckyDrawBoard.WinTarget);
        Assert.NotEqual(firstTrace, otherTrace);
    }

    [Fact]
    public void TheDeckHoldsExactlySeventyNineNumberCardsPlusTheActionsAndModifiers()
    {
        Span<byte> deck = stackalloc byte[LuckyCards.DeckSize];
        var count = LuckyCards.Fill(deck);
        var faces = new int[LuckyCards.FaceCount];
        for (var index = 0; index < count; index++)
        {
            faces[deck[index]]++;
        }

        var numbers = 0;
        for (var number = 0; number <= LuckyCards.MaxNumber; number++)
        {
            Assert.Equal(Math.Max(1, number), faces[number]);
            numbers += faces[number];
        }

        Assert.Equal(LuckyCards.NumberCards, numbers);
        Assert.Equal(LuckyCards.ActionCopies, faces[LuckyCards.Freeze]);
        Assert.Equal(LuckyCards.ActionCopies, faces[LuckyCards.FlipThree]);
        Assert.Equal(LuckyCards.ActionCopies, faces[LuckyCards.SecondChance]);
        for (var face = LuckyCards.PlusTwo; face <= LuckyCards.Times; face++)
        {
            Assert.Equal(1, faces[face]);
        }

        Assert.Equal(LuckyCards.DeckSize, count);
        var board = new LuckyDrawBoard();
        board.NewMatch(3, GameRandom.FromSeed(5));
        Assert.Equal(LuckyCards.DeckSize, board.DeckCount);
        for (var face = 0; face < LuckyCards.FaceCount; face++)
        {
            Assert.Equal(LuckyCards.CopiesOf(face), board.CountRemaining(face));
        }
    }

    [Fact]
    public void DrawingANumberYouAlreadyHoldBusts()
    {
        var board = Rigged(2, 5, 9, 5);
        Settle(board);
        Assert.Equal(LuckyPhase.Turn, board.Phase);
        Assert.Equal(0, board.TurnSeat);

        var drew = board.Hit();
        Assert.Equal(LuckyEventKind.Drew, drew.Kind);
        Assert.Equal(5, drew.Face);
        var bust = board.Advance();

        Assert.Equal(LuckyEventKind.Bust, bust.Kind);
        Assert.Equal(0, bust.Seat);
        Assert.Equal(1, bust.Slot);
        Assert.Equal(0, bust.OtherSlot);
        Assert.Equal(LuckySeatState.Busted, board.State(0));
        Assert.Equal(0, board.HandScore(0));
        Settle(board);
        Assert.Equal(1, board.TurnSeat);
        Assert.Equal(2, board.SweepBusted(0));
        Assert.Equal(0, board.RowCount(0));
    }

    [Fact]
    public void ASecondChanceIsSpentToSaveADuplicate()
    {
        var board = Rigged(2, LuckyCards.SecondChance, 3, 8, 8);
        Settle(board);
        Assert.True(board.HasSecondChance(0));
        board.Hit();
        Assert.Equal(LuckyEventKind.Kept, board.Advance().Kind);
        Settle(board);
        Assert.Equal(1, board.TurnSeat);
        board.Stay();
        Settle(board);
        Assert.Equal(0, board.TurnSeat);

        board.Hit();
        var saved = board.Advance();

        Assert.Equal(LuckyEventKind.Saved, saved.Kind);
        Assert.Equal(LuckySeatState.Active, board.State(0));
        Assert.False(board.HasSecondChance(0));
        Assert.Equal(1, board.RowCount(0));
        Assert.Equal(8, board.RowFace(0, 0));
        var discard = board.Discard;
        Assert.Equal(LuckyCards.SecondChance, discard[^1]);
        Assert.Equal(8, discard[^2]);
    }

    [Fact]
    public void SevenUniqueNumbersEndTheRoundWithTheBonus()
    {
        var board = Rigged(2, 1, 12, 2, 3, 4, 5, 6, 7);
        Settle(board);
        board.Hit();
        Settle(board);
        Assert.Equal(1, board.TurnSeat);
        board.Stay();
        Settle(board);
        for (var draw = 0; draw < 4; draw++)
        {
            board.Hit();
            Assert.Equal(LuckyEventKind.Kept, board.Advance().Kind);
            Settle(board);
        }

        board.Hit();
        var seven = board.Advance();

        Assert.Equal(LuckyEventKind.Seven, seven.Kind);
        Assert.Equal(0, board.SevenSeat);
        Assert.Equal(LuckyPhase.RoundOver, board.Phase);
        Assert.Equal(1 + 2 + 3 + 4 + 5 + 6 + 7 + LuckyDrawBoard.SevenBonus, board.LastRoundScore(0));
        Assert.Equal(board.LastRoundScore(0), board.Total(0));
        Assert.Equal(12, board.Total(1));
        Assert.Equal(1, board.Sevens(0));
    }

    [Fact]
    public void TheRoundScoreDoublesTheNumbersBeforeAddingTheModifiers()
    {
        var board = Rigged(2, 5, 1, LuckyCards.Times, 7, LuckyCards.PlusFour);
        Settle(board);
        board.Hit();
        Settle(board);
        board.Stay();
        Settle(board);
        board.Hit();
        Settle(board);
        board.Hit();
        Settle(board);

        Assert.Equal((5 + 7) * 2 + 4, board.HandScore(0));
        board.Stay();
        Settle(board);
        Assert.Equal(LuckyPhase.RoundOver, board.Phase);
        Assert.Equal(28, board.LastRoundScore(0));
        Assert.Equal(1, board.LastRoundScore(1));
    }

    [Fact]
    public void FlipThreeDrawsAllThreeCardsBeforeResolvingTheActionsItTurnedUp()
    {
        var board = Rigged(3, LuckyCards.FlipThree, 2, LuckyCards.Freeze, 4, 9);
        Assert.Equal(LuckyEventKind.Drew, board.Advance().Kind);
        var choose = board.Advance();
        Assert.Equal(LuckyEventKind.ChooseTarget, choose.Kind);
        Assert.Equal(LuckyPhase.Target, board.Phase);
        Assert.Equal(0, board.Actor);

        var flip = board.Target(1);
        Assert.Equal(LuckyEventKind.FlipThree, flip.Kind);
        Assert.Equal(1, flip.Target);
        Assert.Equal(1, board.ForcedSeat);
        AssertDraw(board, 1, 2, LuckyEventKind.Kept);
        AssertDraw(board, 1, LuckyCards.Freeze, LuckyEventKind.ActionDeferred);
        Assert.Equal(LuckyPhase.Auto, board.Phase);
        AssertDraw(board, 1, 4, LuckyEventKind.Kept);

        var deferred = board.Advance();
        Assert.Equal(LuckyEventKind.ChooseTarget, deferred.Kind);
        Assert.Equal(1, board.Actor);
        Assert.Equal(LuckyCards.Freeze, board.PendingFace);
        var frozen = board.Target(2);
        Assert.Equal(LuckyEventKind.Frozen, frozen.Kind);
        Assert.Equal(LuckySeatState.Frozen, board.State(2));

        Settle(board);
        Assert.Equal(LuckyPhase.Turn, board.Phase);
        Assert.Equal(3, board.RowCount(1));
        Assert.Equal(9, board.RowFace(1, 2));
        Assert.Equal(0, board.RowCount(2));
    }

    [Fact]
    public void FlipThreeStopsAtABust()
    {
        var board = Rigged(2, LuckyCards.FlipThree, 6, 6);
        board.Advance();
        board.Advance();
        board.Target(1);
        AssertDraw(board, 1, 6, LuckyEventKind.Kept);
        AssertDraw(board, 1, 6, LuckyEventKind.Bust);

        Settle(board);
        Assert.Equal(LuckyCards.DeckSize - 3, board.DeckCount);
        Assert.Equal(LuckySeatState.Busted, board.State(1));
        Assert.Equal(LuckyPhase.Turn, board.Phase);
        Assert.Equal(0, board.TurnSeat);
    }

    [Fact]
    public void FreezeBanksTheTargetWithTheCardsTheyHold()
    {
        var board = Rigged(2, 9, 4, LuckyCards.Freeze);
        Settle(board);
        board.Hit();
        Assert.Equal(LuckyEventKind.ChooseTarget, board.Advance().Kind);
        Assert.True(board.IsValidTarget(0));
        Assert.True(board.IsValidTarget(1));

        var frozen = board.Target(1);
        Assert.Equal(LuckyEventKind.Frozen, frozen.Kind);
        Assert.Equal(LuckySeatState.Frozen, board.State(1));
        Assert.Equal(1, board.RowCount(0));
        Settle(board);
        Assert.Equal(0, board.TurnSeat);
        board.Stay();
        Settle(board);
        Assert.Equal(LuckyPhase.RoundOver, board.Phase);
        Assert.Equal(4, board.Total(1));
        Assert.Equal(9, board.Total(0));
    }

    [Fact]
    public void ASecondSecondChanceIsPassedToAnotherPlayer()
    {
        var board = Rigged(3, LuckyCards.SecondChance, 5, 6, LuckyCards.SecondChance);
        Settle(board);
        board.Hit();
        var choose = board.Advance();
        Assert.Equal(LuckyEventKind.ChooseTarget, choose.Kind);
        Assert.False(board.IsValidTarget(0));
        Assert.True(board.IsValidTarget(1));

        var given = board.Target(2);
        Assert.Equal(LuckyEventKind.SecondChanceGiven, given.Kind);
        Assert.True(board.HasSecondChance(2));
        Assert.True(board.HasSecondChance(0));
        Assert.Equal(1, board.RowCount(0));
        Assert.Equal(2, board.RowCount(2));
    }

    [Fact]
    public void TheFirstToTwoHundredWinsAndATieAtTheTopPlaysOn()
    {
        var winning = Rigged(2, 9, 2);
        winning.SetTotal(0, 195);
        Settle(winning);
        winning.Stay();
        Settle(winning);
        winning.Stay();
        var end = Settle(winning);
        Assert.Equal(LuckyEventKind.MatchOver, end.Kind);
        Assert.Equal(LuckyPhase.MatchOver, winning.Phase);
        Assert.Equal(0, winning.Winner);

        var tied = Rigged(2, 8, 5);
        tied.SetTotal(0, 195);
        tied.SetTotal(1, 198);
        Settle(tied);
        tied.Stay();
        Settle(tied);
        tied.Stay();
        Settle(tied);
        Assert.Equal(LuckyPhase.RoundOver, tied.Phase);
        Assert.Equal(-1, tied.Winner);
        tied.StartRound();
        Assert.Equal(2, tied.Round);
        Assert.Equal(LuckyPhase.Auto, tied.Phase);
    }

    [Fact]
    public void BustChanceCountsTheDuplicatesStillInTheDeck()
    {
        var board = Rigged(2, 12, 1);
        Settle(board);

        var expected = (12 - 1) / (float)board.DeckCount;
        Assert.Equal(expected, board.BustChance(0), 4);
        Assert.Equal(0f, board.BustChance(1), 4);
    }

    [Fact]
    public void BotsHitAnEmptyHandAndStayWhenTheRiskIsHigh()
    {
        var board = Rigged(2, 12, 1, 11, 10, 9, 8);
        Assert.True(LuckyDrawBot.WantsHit(board, 0, LuckyDrawBot.Careful));
        Settle(board);
        board.Hit();
        Settle(board);
        board.Stay();
        Settle(board);
        for (var draw = 0; draw < 3; draw++)
        {
            board.Hit();
            Settle(board);
        }

        Assert.Equal(12 + 11 + 10 + 9 + 8, board.HandScore(0));
        Assert.True(board.BustChance(0) > 0.4f);
        Assert.False(LuckyDrawBot.WantsHit(board, 0, LuckyDrawBot.Careful));
        Assert.False(LuckyDrawBot.WantsHit(board, 0, LuckyDrawBot.Bold));
    }

    private static LuckyDrawBoard Rigged(int seats, params byte[] cards)
    {
        var board = new LuckyDrawBoard();
        board.NewMatch(seats, GameRandom.FromSeed(7));
        Assert.True(board.PutOnTop(cards));
        return board;
    }

    private static LuckyEvent Settle(LuckyDrawBoard board)
    {
        var last = default(LuckyEvent);
        for (var step = 0; step < StepGuard && board.Phase == LuckyPhase.Auto; step++)
        {
            last = board.Advance();
        }

        return last;
    }

    private static void AssertDraw(LuckyDrawBoard board, int seat, int face, LuckyEventKind outcome)
    {
        var drew = board.Advance();
        Assert.Equal(LuckyEventKind.Drew, drew.Kind);
        Assert.Equal(seat, drew.Seat);
        Assert.Equal(face, drew.Face);
        Assert.Equal(outcome, board.Advance().Kind);
    }

    private static LuckyDrawBoard PlayMatch(ulong seed, int seats, out string trace)
    {
        var board = new LuckyDrawBoard();
        board.NewMatch(seats, GameRandom.FromSeed(seed));
        var builder = new StringBuilder();
        for (var step = 0; step < MatchGuard && board.Phase != LuckyPhase.MatchOver; step++)
        {
            LuckyEvent update;
            switch (board.Phase)
            {
                case LuckyPhase.Turn:
                {
                    var seat = board.TurnSeat;
                    update = LuckyDrawBot.WantsHit(board, seat, LuckyDrawBot.AppetiteFor(seat)) ? board.Hit() : board.Stay();
                    break;
                }
                case LuckyPhase.Target:
                    update = board.Target(LuckyDrawBot.ChooseTarget(board, board.Actor,
                        LuckyDrawBot.AppetiteFor(board.Actor)));
                    break;
                case LuckyPhase.RoundOver:
                    board.StartRound();
                    builder.Append('|');
                    continue;
                default:
                    update = board.Advance();
                    break;
            }

            builder.Append((int)update.Kind).Append(':').Append(update.Seat).Append(':').Append(update.Face)
                .Append(update.Reshuffled ? "R" : string.Empty).Append(';');
        }

        trace = builder.ToString();
        return board;
    }
}
