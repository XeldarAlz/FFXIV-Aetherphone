using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Solitaire;
using Xunit;

namespace Aetherphone.Tests;

public sealed class SolitaireBoardTests
{
    private const ulong Seed = 0xC0FFEEUL;
    private const int SeedSweep = 64;

    private static SolitaireBoard Deal(ulong seed, bool vegas = false)
    {
        var board = new SolitaireBoard();
        board.Deal(GameRandom.FromSeed(seed), vegas);
        return board;
    }

    private static void AssertSameLayout(SolitaireBoard first, SolitaireBoard second)
    {
        Assert.Equal(first.StockCount, second.StockCount);
        for (var fromTop = 0; fromTop < first.StockCount; fromTop++)
        {
            Assert.Equal(first.StockPeek(fromTop), second.StockPeek(fromTop));
        }

        for (var pile = 0; pile < SolitaireBoard.TableauPiles; pile++)
        {
            Assert.Equal(first.TableauCount(pile), second.TableauCount(pile));
            Assert.Equal(first.TableauFaceDownCount(pile), second.TableauFaceDownCount(pile));
            for (var index = 0; index < first.TableauCount(pile); index++)
            {
                Assert.Equal(first.TableauCardAt(pile, index), second.TableauCardAt(pile, index));
            }
        }
    }

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = Deal(Seed);
        var second = Deal(Seed);

        AssertSameLayout(first, second);
    }

    [Fact]
    public void DifferentSeedsDealDifferentBoards()
    {
        var first = Deal(1);
        var second = Deal(2);
        var same = 0;
        for (var fromTop = 0; fromTop < first.StockCount; fromTop++)
        {
            if (first.StockPeek(fromTop) == second.StockPeek(fromTop))
            {
                same++;
            }
        }

        Assert.True(same < SolitaireBoard.StockSize / 2);
    }

    [Fact]
    public void EverySeededDealIsALegalFiftyTwoCardLayout()
    {
        for (var seed = 1; seed <= SeedSweep; seed++)
        {
            var board = Deal((ulong)seed);
            var seen = new bool[SolitaireBoard.DeckSize];
            var dealt = 0;
            for (var pile = 0; pile < SolitaireBoard.TableauPiles; pile++)
            {
                Assert.Equal(pile + 1, board.TableauCount(pile));
                Assert.Equal(pile, board.TableauFaceDownCount(pile));
                Assert.True(board.IsTableauFaceUp(pile, pile));
                for (var index = 0; index < board.TableauCount(pile); index++)
                {
                    var card = board.TableauCardAt(pile, index);
                    Assert.InRange(card, 0, SolitaireBoard.DeckSize - 1);
                    Assert.False(seen[card], $"seed {seed} dealt card {card} twice");
                    seen[card] = true;
                    dealt++;
                }
            }

            Assert.Equal(SolitaireBoard.StockSize, board.StockCount);
            Assert.Equal(0, board.WasteCount);
            for (var fromTop = 0; fromTop < board.StockCount; fromTop++)
            {
                var card = board.StockPeek(fromTop);
                Assert.False(seen[card], $"seed {seed} dealt card {card} twice");
                seen[card] = true;
                dealt++;
            }

            Assert.Equal(SolitaireBoard.DeckSize, dealt);
            Assert.Equal(0, board.Moves);
            Assert.False(board.IsWon);
            Assert.True(board.HasAnyMove());
        }
    }

    [Fact]
    public void ClassicRecyclesTheWasteWithoutLimit()
    {
        var board = Deal(Seed);
        for (var pass = 0; pass < 3; pass++)
        {
            for (var draw = 0; draw < SolitaireBoard.StockSize; draw++)
            {
                Assert.True(board.DrawStock());
            }

            Assert.Equal(0, board.StockCount);
            Assert.True(board.CanRecycle);
            Assert.True(board.DrawStock());
            Assert.Equal(SolitaireBoard.StockSize, board.StockCount);
        }

        Assert.Equal(0, board.Score);
    }

    [Fact]
    public void VegasAllowsTwoRecyclesThenTheStockStaysSpent()
    {
        var board = Deal(Seed, true);
        Assert.Equal(SolitaireBoard.VegasRecycles, board.RecyclesLeft);
        for (var recycle = 0; recycle < SolitaireBoard.VegasRecycles; recycle++)
        {
            for (var draw = 0; draw < SolitaireBoard.StockSize; draw++)
            {
                Assert.True(board.DrawStock());
            }

            Assert.True(board.CanRecycle);
            Assert.True(board.DrawStock());
            Assert.Equal(SolitaireBoard.VegasRecycles - recycle - 1, board.RecyclesLeft);
        }

        for (var draw = 0; draw < SolitaireBoard.StockSize; draw++)
        {
            Assert.True(board.DrawStock());
        }

        Assert.False(board.CanRecycle);
        Assert.False(board.DrawStock());
        Assert.Equal(0, board.StockCount);
    }

    [Fact]
    public void VegasPaysFiveACardOnTopOfTheDeckCost()
    {
        for (var seed = 1; seed <= SeedSweep; seed++)
        {
            var board = Deal((ulong)seed, true);
            Assert.Equal(-SolitaireBoard.VegasDeckCost, board.Score);
            for (var pile = 0; pile < SolitaireBoard.TableauPiles; pile++)
            {
                if (!board.SendTableauToFoundation(pile))
                {
                    continue;
                }

                Assert.Equal(SolitaireBoard.VegasCardValue - SolitaireBoard.VegasDeckCost, board.Score);
                Assert.Equal(1, board.FoundationTotal);
                return;
            }
        }

        Assert.Fail("no seed in the sweep dealt a playable ace on top of a column");
    }

    [Fact]
    public void AutoCompleteWaitsForEveryCardToBeFaceUp()
    {
        var board = Deal(Seed);

        Assert.False(board.AllFaceUp);
        Assert.False(board.IsAutoCompletable);
        Assert.Equal(SolitaireAutoMove.Draw, board.PlanAutoStep(out _));
    }
}
