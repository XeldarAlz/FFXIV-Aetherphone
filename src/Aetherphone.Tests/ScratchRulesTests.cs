using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ScratchRulesTests
{
    [Fact]
    public void ConstantsMatchTheBackendEngine()
    {
        Assert.Equal(4, ScratchRules.TierCount);
        Assert.Equal(9, ScratchRules.CellCount);
        Assert.Equal(3, ScratchRules.GridSide);
        Assert.Equal(4, ScratchRules.PrizeSymbolCount);
        Assert.Equal(7, ScratchRules.SymbolCount);
        Assert.Equal(1_000_000, ScratchRules.TableScale);
        Assert.Equal(3, ScratchRules.MatchesToWin);
        Assert.Equal(new long[] { 5_000, 10_000, 25_000, 50_000 }, ScratchRules.Prices);
    }

    [Fact]
    public void PrizeTablesMatchTheBackendLiterals()
    {
        var expected = new (long Chips, int CountPerMillion)[][]
        {
            new[] { (10_000L, 285_000), (25_000L, 50_000), (50_000L, 7_500), (100_000L, 1_400) },
            new[] { (20_000L, 285_000), (50_000L, 50_000), (100_000L, 7_500), (200_000L, 1_400) },
            new[] { (50_000L, 285_000), (125_000L, 51_000), (250_000L, 7_600), (500_000L, 1_450) },
            new[] { (100_000L, 286_000), (250_000L, 52_000), (500_000L, 7_800), (1_000_000L, 1_500) },
        };
        for (var tier = 0; tier < ScratchRules.TierCount; tier++)
        {
            Assert.Equal(expected[tier].Length, ScratchRules.PrizeTables[tier].Length);
            for (var prizeIndex = 0; prizeIndex < expected[tier].Length; prizeIndex++)
            {
                Assert.Equal(expected[tier][prizeIndex].Chips, ScratchRules.PrizeTables[tier][prizeIndex].Chips);
                Assert.Equal(expected[tier][prizeIndex].CountPerMillion,
                    ScratchRules.PrizeTables[tier][prizeIndex].CountPerMillion);
            }
        }
    }

    [Fact]
    public void WinCountsSumTheTierTables()
    {
        Assert.Equal(343_900, ScratchRules.WinCountPerMillion(0));
        Assert.Equal(343_900, ScratchRules.WinCountPerMillion(1));
        Assert.Equal(345_050, ScratchRules.WinCountPerMillion(2));
        Assert.Equal(347_300, ScratchRules.WinCountPerMillion(3));
    }

    [Fact]
    public void TheInfoSheetPrintsEachTiersReturn()
    {
        Assert.Equal(923, ScratchRules.ReturnTenths(0));
        Assert.Equal(923, ScratchRules.ReturnTenths(1));
        Assert.Equal(930, ScratchRules.ReturnTenths(2));
        Assert.Equal(940, ScratchRules.ReturnTenths(3));
        Assert.Equal(0, ScratchRules.ReturnTenths(9));
    }

    [Fact]
    public void TierForPriceRoundTripsAndRejectsUnknownPrices()
    {
        Assert.Equal(0, ScratchRules.TierForPrice(5_000));
        Assert.Equal(1, ScratchRules.TierForPrice(10_000));
        Assert.Equal(2, ScratchRules.TierForPrice(25_000));
        Assert.Equal(3, ScratchRules.TierForPrice(50_000));
        Assert.Equal(-1, ScratchRules.TierForPrice(7_500));
        Assert.True(ScratchRules.IsValidTier(0));
        Assert.True(ScratchRules.IsValidTier(3));
        Assert.False(ScratchRules.IsValidTier(-1));
        Assert.False(ScratchRules.IsValidTier(4));
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
