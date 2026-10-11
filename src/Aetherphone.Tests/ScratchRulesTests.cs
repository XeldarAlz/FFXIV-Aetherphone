using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ScratchRulesTests
{
    [Fact]
    public void ConstantsMatchTheBackendEngine()
    {
        Assert.Equal(5, ScratchRules.TierCount);
        Assert.Equal(9, ScratchRules.CellCount);
        Assert.Equal(3, ScratchRules.GridSide);
        Assert.Equal(4, ScratchRules.PrizeSymbolCount);
        Assert.Equal(7, ScratchRules.SymbolCount);
        Assert.Equal(1_000_000, ScratchRules.TableScale);
        Assert.Equal(3, ScratchRules.MatchesToWin);
        Assert.Equal(9_500, ScratchRules.ReturnBasisPoints);
        Assert.Equal(new long[] { 250, 1_000, 5_000, 25_000, 100_000 }, ScratchRules.Prices);
        Assert.Equal(new[] { 2, 5, 10, 20 }, ScratchRules.PrizeMultiples);
        Assert.Equal(new[] { 285_000, 52_000, 8_000, 2_000 }, ScratchRules.PrizeCountsPerMillion);
    }

    [Fact]
    public void EveryTierPaysTheSameMultiplesOfItsOwnPrice()
    {
        for (var tier = 0; tier < ScratchRules.TierCount; tier++)
        {
            var table = ScratchRules.PrizeTables[tier];
            Assert.Equal(ScratchRules.PrizeSymbolCount, table.Length);
            for (var prizeIndex = 0; prizeIndex < table.Length; prizeIndex++)
            {
                Assert.Equal(ScratchRules.Prices[tier] * ScratchRules.PrizeMultiples[prizeIndex],
                    table[prizeIndex].Chips);
                Assert.Equal(ScratchRules.PrizeCountsPerMillion[prizeIndex], table[prizeIndex].CountPerMillion);
            }
        }

        Assert.Equal(2_000_000, ScratchRules.PrizeTables[4][3].Chips);
        Assert.Equal(500, ScratchRules.PrizeTables[0][0].Chips);
    }

    [Fact]
    public void EveryTierReturnsNinetyFivePercentExactly()
    {
        for (var tier = 0; tier < ScratchRules.TierCount; tier++)
        {
            var returned = 0L;
            var table = ScratchRules.PrizeTables[tier];
            for (var prizeIndex = 0; prizeIndex < table.Length; prizeIndex++)
            {
                returned += table[prizeIndex].Chips * table[prizeIndex].CountPerMillion;
            }

            Assert.Equal(ScratchRules.Prices[tier] * ScratchRules.TableScale / 10_000 * ScratchRules.ReturnBasisPoints,
                returned);
            Assert.Equal(950, ScratchRules.ReturnTenths(tier));
            Assert.Equal(347_000, ScratchRules.WinCountPerMillion(tier));
        }

        Assert.Equal(0, ScratchRules.ReturnTenths(9));
    }

    [Fact]
    public void TierForPriceRoundTripsAndRejectsUnknownPrices()
    {
        Assert.Equal(0, ScratchRules.TierForPrice(250));
        Assert.Equal(1, ScratchRules.TierForPrice(1_000));
        Assert.Equal(2, ScratchRules.TierForPrice(5_000));
        Assert.Equal(3, ScratchRules.TierForPrice(25_000));
        Assert.Equal(4, ScratchRules.TierForPrice(100_000));
        Assert.Equal(-1, ScratchRules.TierForPrice(500));
        Assert.True(ScratchRules.IsValidTier(0));
        Assert.True(ScratchRules.IsValidTier(4));
        Assert.False(ScratchRules.IsValidTier(-1));
        Assert.False(ScratchRules.IsValidTier(5));
    }

    [Fact]
    public void EveryPriceSitsOnTheGlobalLadder()
    {
        for (var tier = 0; tier < ScratchRules.TierCount; tier++)
        {
            Assert.Equal(ScratchRules.Prices[tier], CasinoLadder.FloorToRung(ScratchRules.Prices[tier]));
        }
    }

    [Fact]
    public void TheWinStampReadsThePrizeOverThePrice()
    {
        Assert.Equal(20, ScratchRules.MultipleOf(4, 2_000_000));
        Assert.Equal(2, ScratchRules.MultipleOf(0, 500));
        Assert.Equal(0, ScratchRules.MultipleOf(0, 0));
        Assert.Equal(0, ScratchRules.MultipleOf(7, 500));
    }

    [Fact]
    public void WinningSymbolFindsTheTripleAndOnlyTheTriple()
    {
        Assert.Equal(2, ScratchRules.WinningSymbol(new[] { 2, 0, 0, 1, 1, 3, 3, 2, 2 }));
        Assert.Equal(-1, ScratchRules.WinningSymbol(new[] { 0, 0, 1, 1, 2, 2, 3, 3, 4 }));
    }

    [Fact]
    public void CellValidationRejectsMalformedGrids()
    {
        Assert.True(ScratchRules.AreValidCells(new[] { 0, 1, 2, 3, 4, 5, 6, 0, 1 }));
        Assert.False(ScratchRules.AreValidCells(new[] { 0, 1, 2 }));
        Assert.False(ScratchRules.AreValidCells(new[] { 0, 1, 2, 3, 4, 5, 7, 0, 1 }));
        Assert.False(ScratchRules.AreValidCells(new[] { 0, 1, 2, 3, 4, 5, -1, 0, 1 }));
    }
}
