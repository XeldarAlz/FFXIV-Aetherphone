using Aetherphone.Apps.Casino;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoCashierTests
{
    [Fact]
    public void TheRateComesFromTheServerAndFallsBackToTheSitting()
    {
        Assert.Equal(1000, CasinoCashier.Rate(null));
        Assert.Equal(1000, CasinoCashier.Rate(new CasinoStateDto(RateChipsPerCoin: 1000)));
        Assert.Equal(100, CasinoCashier.Rate(new CasinoStateDto(Sitting: new CasinoSittingDto(RateChipsPerCoin: 100))));
    }

    [Fact]
    public void ACashOutConvertsTheWholeStackAndClosesTheSitting()
    {
        var state = new CasinoStateDto(Sitting: new CasinoSittingDto(Id: "s1", State: 1, Stack: 5_111_825));
        var result = new CasinoSittingResultDto(true, string.Empty, new CasinoSittingDto(Id: "s1", State: 3), 6_111,
            5_111);

        var next = CasinoStore.CashOutAbsorbedInto(state, result);

        Assert.Null(next.Sitting);
    }

    [Fact]
    public void ARefusedCashOutChangesNothing()
    {
        var state = new CasinoStateDto(Sitting: new CasinoSittingDto(Id: "s1", Stack: 150_000));

        Assert.Same(state, CasinoStore.CashOutAbsorbedInto(state, new CasinoSittingResultDto(false, "frozen")));
    }

    [Fact]
    public void AClaimedBonusLandsInTheBankrollAndRestartsItsTimer()
    {
        var state = new CasinoStateDto(Sitting: new CasinoSittingDto(Id: "s1", Stack: 100_000),
            Bonuses: new[]
            {
                new CasinoBonusDto(CasinoBonusKinds.Welcome, false, 50_000, 0, 0, false),
                new CasinoBonusDto(CasinoBonusKinds.Timed, true, 5_000, 0, 0, true),
            });
        var claim = new CasinoBonusClaimDto(true, string.Empty, CasinoBonusKinds.Timed, 5_000, 105_000,
            new CasinoSittingDto(Id: "s1", Stack: 105_000), 1_791_500_000);

        var next = CasinoStore.BonusAbsorbedInto(state, claim);

        Assert.Equal(105_000, next.Sitting!.Stack);
        Assert.False(next.Bonuses![1].Ready);
        Assert.Equal(1_791_500_000, next.Bonuses[1].NextAtUnix);
        Assert.Equal(CasinoBonusKinds.Welcome, next.Bonuses[0].Kind);
    }

    [Fact]
    public void AFirstGrantOpensTheBankrollItCameWith()
    {
        var state = new CasinoStateDto();
        var claim = new CasinoBonusClaimDto(true, string.Empty, CasinoBonusKinds.Welcome, 50_000, 50_000,
            new CasinoSittingDto(Id: "s9", State: 1, Stack: 50_000));

        var next = CasinoStore.BonusAbsorbedInto(state, claim);

        Assert.Equal("s9", next.Sitting!.Id);
        Assert.Same(state, CasinoStore.BonusAbsorbedInto(state, claim with { Granted = false }));
    }

    [Fact]
    public void FaucetsAndClubTiersMirrorTheBackendPolicy()
    {
        Assert.Equal(new[] { "welcome", "timed", "reload", "streak", "levelup", "broke", "rebate" },
            CasinoBonusKinds.All);
        Assert.Equal(new long[] { 2_000, 4_000, 6_000, 8_000, 10_000, 15_000, 25_000 }, CasinoFaucets.StreakChips);
        Assert.Equal(6_000, CasinoFaucets.StreakGrant(2, 150));
        Assert.Equal(25_000, CasinoFaucets.StreakGrant(9, 100));
        Assert.Equal(23_000, CasinoFaucets.LevelUpGrant(23));
        Assert.Equal(50_000, CasinoFaucets.LevelUpGrant(80));
        Assert.Equal(new long[] { 0, 10_000, 50_000, 250_000, 1_000_000, 5_000_000, 25_000_000 },
            CasinoClubTiers.Floors);
        Assert.Equal(new[] { 100, 125, 150, 200, 250, 300, 300 }, CasinoClubTiers.MultiplierPercents);
        Assert.Equal(new[] { 0, 500, 750, 750, 1000, 1000, 1000 }, CasinoClubTiers.RebateBasisPoints);
        Assert.Equal(0.22f,
            CasinoClubTiers.Progress(new CasinoClubDto(2, "gold", 94_000, 50_000, 250_000, 150, 750)), 3);
        Assert.Equal(1f, CasinoClubTiers.Progress(new CasinoClubDto(6, "obsidian", 30_000_000, 25_000_000, 0)));
    }

    [Fact]
    public void BuyingChipsTakesAnyWholeCoinsUpToTheWallet()
    {
        var bounds = BuyInBounds.Of(new CasinoStateDto(RateChipsPerCoin: 1000), 1_016_251);

        Assert.True(bounds.Allows(1));
        Assert.True(bounds.Allows(5_001));
        Assert.True(bounds.Allows(1_016_251));
        Assert.False(bounds.Allows(0));
        Assert.False(bounds.Allows(1_016_252));
        Assert.Equal(137_000, bounds.ChipsFor(137));

        var broke = BuyInBounds.Of(new CasinoStateDto(RateChipsPerCoin: 1000), 0);
        Assert.False(broke.Allows(1));
    }

    [Fact]
    public void TheTimedBonusCountsDownToItsNextClaim()
    {
        var ready = new CasinoBonusDto(CasinoBonusKinds.Timed, true, 5_000, 0, 0, true);
        var waiting = new CasinoBonusDto(CasinoBonusKinds.Timed, false, 5_000, 1_000_600, 0, true);

        Assert.Equal(600, CashierBonusShelf.SecondsUntil(waiting, 1_000_000));
        Assert.Equal(0, CashierBonusShelf.SecondsUntil(ready, 1_000_000));
    }

    [Fact]
    public void FeatureFlagsGateTheNewFloor()
    {
        var state = new CasinoStateDto(Features: new[] { CasinoFeatures.EconomyV3, CasinoFeatures.Bonus });

        Assert.True(CasinoFeatures.Has(state, CasinoFeatures.Bonus));
        Assert.False(CasinoFeatures.Has(state, CasinoFeatures.GilTables));
        Assert.False(CasinoFeatures.Has(null, CasinoFeatures.Bonus));
    }
}
