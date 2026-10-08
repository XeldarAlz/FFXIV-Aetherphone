using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class OriginalsRulesTests
{
    [Theory]
    [InlineData(3, 3, 147)]
    [InlineData(5, 5, 339)]
    [InlineData(10, 5, 1751)]
    [InlineData(1, 24, 2475)]
    [InlineData(24, 1, 2475)]
    [InlineData(12, 13, 514829700)]
    [InlineData(13, 12, 514829700)]
    public void MinesMultipliersMatchTheBackendPins(int mines, int picks, long hundredths)
    {
        Assert.Equal(hundredths, OriginalsRules.MinesHundredths(mines, picks));
    }

    [Fact]
    public void MinesPaysNothingBeforeTheFirstPickAndPastTheLastSafeTile()
    {
        Assert.Equal(0, OriginalsRules.MinesHundredths(3, 0));
        Assert.Equal(0, OriginalsRules.MinesHundredths(3, 23));
        Assert.Equal(0, OriginalsRules.MinesHundredths(0, 1));
        Assert.Equal(0, OriginalsRules.MinesHundredths(25, 1));
        Assert.Equal(1470, OriginalsRules.MinesPayout(1000, 3, 3));
    }

    [Fact]
    public void MinesNeverReturnsMoreThanNinetyNinePercent()
    {
        for (var mines = OriginalsRules.MinMines; mines <= OriginalsRules.MaxMines; mines++)
        {
            for (var picks = 1; picks <= OriginalsRules.SafeTiles(mines); picks++)
            {
                var survival = (double)OriginalsRules.Binomial(25 - mines, picks) / OriginalsRules.Binomial(25, picks);
                var expected = survival * OriginalsRules.MinesHundredths(mines, picks) / 100d;
                Assert.InRange(expected, 0.9828, 0.99 + 1e-9);
            }
        }
    }

    [Fact]
    public void MinesChanceOfTheNextSafePickShrinksAsTheBoardEmpties()
    {
        Assert.Equal(8800, OriginalsRules.MinesNextChanceBasisPoints(3, 0));
        Assert.Equal(0, OriginalsRules.MinesNextChanceBasisPoints(3, 22));
    }

    [Theory]
    [InlineData(4950, 20000)]
    [InlineData(5000, 19800)]
    [InlineData(9800, 10102)]
    [InlineData(1, 99000000)]
    public void DiceMultiplierIsNinetyNineOverTheChance(int chance, long tenThousandths)
    {
        Assert.Equal(tenThousandths, OriginalsRules.DiceMultiplierTenThousandths(chance));
    }

    [Fact]
    public void DiceTargetsAndChancesMirrorTheWire()
    {
        Assert.Equal(4950, OriginalsRules.DiceChance(5050, true));
        Assert.Equal(5050, OriginalsRules.DiceChance(5050, false));
        Assert.Equal(5050, OriginalsRules.DiceTargetFor(4950, true));
        Assert.True(OriginalsRules.IsDiceTarget(200, true));
        Assert.False(OriginalsRules.IsDiceTarget(199, true));
        Assert.True(OriginalsRules.IsDiceTarget(9800, false));
        Assert.False(OriginalsRules.IsDiceTarget(9801, false));
        Assert.False(OriginalsRules.IsDiceTarget(0, false));
        Assert.True(OriginalsRules.DiceWins(7312, 5050, true));
        Assert.False(OriginalsRules.DiceWins(5050, 5050, true));
        Assert.False(OriginalsRules.DiceWins(5050, 5050, false));
        Assert.Equal(2000, OriginalsRules.DicePayout(1000, 4950));
        Assert.Equal(5000, OriginalsRules.DiceChanceForMultiplier(198));
        Assert.Equal(OriginalsRules.DiceMaxChance, OriginalsRules.DiceChanceForMultiplier(100));
        Assert.Equal(OriginalsRules.DiceMinChance, OriginalsRules.DiceChanceForMultiplier(990000));
    }

    [Fact]
    public void LimboResultIsClampedToTheWireRange()
    {
        Assert.Equal(OriginalsRules.LimboMaxTarget, OriginalsRules.LimboResult(0));
        Assert.Equal(OriginalsRules.LimboMinResult, OriginalsRules.LimboResult(OriginalsRules.LimboBound - 1));
        Assert.Equal(106, OriginalsRules.LimboResult(15608879));
        Assert.Equal(4950, OriginalsRules.LimboChanceBasisPoints(200));
        Assert.Equal(2000, OriginalsRules.LimboPayout(1000, 200));
        Assert.True(OriginalsRules.LimboWins(200, 200));
        Assert.False(OriginalsRules.LimboWins(199, 200));
        Assert.False(OriginalsRules.IsLimboTarget(100));
        Assert.True(OriginalsRules.IsLimboTarget(101));
        Assert.True(OriginalsRules.IsLimboTarget(100_000_000));
    }

    [Fact]
    public void EveryKenoTableReturnsInsideThePublishedBand()
    {
        for (var risk = 0; risk < OriginalsRules.KenoRiskCount; risk++)
        {
            for (var picks = 1; picks <= OriginalsRules.KenoMaxPicks; picks++)
            {
                Assert.InRange(OriginalsRules.KenoReturn(risk, picks), 0.9865, 0.9907);
            }
        }
    }

    [Fact]
    public void KenoFullFiveHitsPayTheTableTops()
    {
        Assert.Equal(3600, OriginalsRules.KenoPayHundredths(OriginalsRules.KenoClassic, 5, 5));
        Assert.Equal(30000, OriginalsRules.KenoPayHundredths(OriginalsRules.KenoLow, 5, 5));
        Assert.Equal(39000, OriginalsRules.KenoPayHundredths(OriginalsRules.KenoMedium, 5, 5));
        Assert.Equal(45000, OriginalsRules.KenoPayHundredths(OriginalsRules.KenoHigh, 5, 5));
        Assert.Equal(310, OriginalsRules.KenoPayHundredths(OriginalsRules.KenoClassic, 3, 2));
        Assert.Equal(0, OriginalsRules.KenoPayHundredths(4, 3, 2));
        Assert.Equal(0, OriginalsRules.KenoPayHundredths(OriginalsRules.KenoClassic, 3, 4));
    }

    [Fact]
    public void KenoPicksMustBeDistinctTilesOnTheBoard()
    {
        Assert.True(OriginalsRules.AreKenoPicks(new[] { 3, 9, 17 }));
        Assert.False(OriginalsRules.AreKenoPicks(new[] { 3, 3 }));
        Assert.False(OriginalsRules.AreKenoPicks(new[] { 40 }));
        Assert.False(OriginalsRules.AreKenoPicks(Array.Empty<int>()));
        Assert.False(OriginalsRules.AreKenoPicks(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }));
        Assert.Equal(2, OriginalsRules.KenoHits(new[] { 3, 9, 17 }, new[] { 17, 2, 30, 9, 11, 0, 25, 39, 14, 6 }));
    }

    [Fact]
    public void HiLoCardsDecodeAceLowWithTheWireSuitOrder()
    {
        Assert.Equal(1, OriginalsRules.HiLoRank(0));
        Assert.Equal(0, OriginalsRules.HiLoSuit(0));
        Assert.Equal(13, OriginalsRules.HiLoRank(51));
        Assert.Equal(3, OriginalsRules.HiLoSuit(51));
        Assert.Equal(6, OriginalsRules.HiLoRank(21));
        Assert.Equal(1, OriginalsRules.HiLoSuit(21));
    }

    [Fact]
    public void HiLoEndCardsOfferOnlyTheWireVariants()
    {
        Assert.False(OriginalsRules.IsLegalCall(HiLoCall.Higher, 1));
        Assert.False(OriginalsRules.IsLegalCall(HiLoCall.Lower, 1));
        Assert.False(OriginalsRules.IsLegalCall(HiLoCall.Below, 1));
        Assert.True(OriginalsRules.IsLegalCall(HiLoCall.Above, 1));
        Assert.True(OriginalsRules.IsLegalCall(HiLoCall.Same, 1));
        Assert.False(OriginalsRules.IsLegalCall(HiLoCall.Higher, 13));
        Assert.False(OriginalsRules.IsLegalCall(HiLoCall.Above, 13));
        Assert.True(OriginalsRules.IsLegalCall(HiLoCall.Below, 13));
        Assert.True(OriginalsRules.IsLegalCall(HiLoCall.Same, 13));
        Assert.True(OriginalsRules.IsLegalCall(HiLoCall.Higher, 7));
        Assert.True(OriginalsRules.IsLegalCall(HiLoCall.Lower, 7));
        Assert.False(OriginalsRules.IsLegalCall(HiLoCall.Skip, 7));
    }

    [Fact]
    public void HiLoCallsPayNinetyNinePercentOfFairOdds()
    {
        Assert.Equal(10725, OriginalsRules.CallMultiplierTenThousandths(HiLoCall.Higher, 2));
        Assert.Equal(128700, OriginalsRules.CallMultiplierTenThousandths(HiLoCall.Same, 7));
        Assert.Equal(9230, OriginalsRules.CallChanceBasisPoints(HiLoCall.Higher, 2));
        for (var rank = 1; rank <= 13; rank++)
        {
            for (var call = HiLoCall.Higher; call <= HiLoCall.Same; call++)
            {
                if (!OriginalsRules.IsLegalCall(call, rank))
                {
                    continue;
                }

                var chance = OriginalsRules.WinningRanks(call, rank) / 13d;
                Assert.Equal(0.99, chance * OriginalsRules.CallFactor(call, rank), 9);
            }
        }
    }

    [Fact]
    public void HiLoCallsRoundTripTheirWireNames()
    {
        for (var call = HiLoCall.Higher; call <= HiLoCall.Skip; call++)
        {
            Assert.True(OriginalsRules.TryParseCall(OriginalsRules.CallWire(call), out var parsed));
            Assert.Equal(call, parsed);
        }

        Assert.False(OriginalsRules.TryParseCall("sideways", out _));
    }

    [Fact]
    public void TopMultiplesMatchTheRegistry()
    {
        Assert.Equal(5_200_000, OriginalsRules.TopMultiple(CasinoWire.MinesKind));
        Assert.Equal(9_900, OriginalsRules.TopMultiple(CasinoWire.DiceKind));
        Assert.Equal(1_000_000, OriginalsRules.TopMultiple(CasinoWire.LimboKind));
        Assert.Equal(1_000, OriginalsRules.TopMultiple(CasinoWire.KenoKind));
        Assert.Equal(1_000_000, OriginalsRules.TopMultiple(CasinoWire.HiLoKind));
        Assert.Equal(100, OriginalsRules.MinBet);
        Assert.Equal(990, OriginalsRules.ReturnTenths);
    }
}
