using System.Text.Json;
using Aetherphone.Apps.Casino;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoPartialCashOutTests
{
    [Fact]
    public void PartialCashOutHasItsOwnRouteBesideBuy()
    {
        Assert.Equal("/casino/chips/cashout", CasinoClient.CashOutChipsPath);
        Assert.Equal("/casino/chips/buy", CasinoClient.BuyChipsPath);
    }

    [Fact]
    public void TheRequestSerializesCoinsAndTheActionId()
    {
        var json = JsonSerializer.Serialize(new CasinoCashOutChipsRequest(310, "c0ffee02"),
            AethernetJsonContext.Default.CasinoCashOutChipsRequest);

        Assert.Equal("{\"coins\":310,\"clientActionId\":\"c0ffee02\"}", json);
    }

    [Fact]
    public void APartialAnswerCarriesTheBankrollAfter()
    {
        const string json = """
        { "granted": true, "reason": "", "coins": 310, "chips": 310000, "stack": 930500, "balance": 12790,
          "sitting": { "id": "s1", "state": 1, "stack": 930500, "rateChipsPerCoin": 1000 } }
        """;

        var result = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoCashOutChipsDto);

        Assert.NotNull(result);
        Assert.True(result!.Granted);
        Assert.Equal(310, result.Coins);
        Assert.Equal(310_000, result.Chips);
        Assert.Equal(930_500, result.Stack);
        Assert.Equal(12_790, result.Balance);
        Assert.Equal("s1", result.Sitting!.Id);
    }

    [Fact]
    public void AWholeAnswerClosesTheBankroll()
    {
        const string json = """
        { "granted": true, "reason": "", "coins": 1240, "chips": 1240500, "stack": 0, "balance": 13720,
          "sitting": null }
        """;

        var result = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoCashOutChipsDto);

        Assert.True(result!.Granted);
        Assert.Null(result.Sitting);
        Assert.Equal(0, result.Stack);
    }

    [Fact]
    public void ARefusalReusesTheCoinReasons()
    {
        const string json = """{ "granted": false, "reason": "cooldown", "balance": 12480 }""";

        var result = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoCashOutChipsDto);

        Assert.False(result!.Granted);
        Assert.Equal(CasinoReasons.Cooldown, result.Reason);
        Assert.Equal(Core.Localization.L.Casino.ReasonCooldown.Key, CasinoReasons.MessageFor(result.Reason).Key);
    }

    [Fact]
    public void AnOutOfRangeCashOutSpeaksAboutChipsNotTheTable()
    {
        Assert.Equal(Core.Localization.Loc.T(Core.Localization.L.CashierDesk.ReasonAmountRange),
            CashierDrawer.CashOutMessage(CasinoReasons.BuyInRange, 0));
        Assert.Equal(CasinoReasons.Text(CasinoReasons.Cooldown, 0),
            CashierDrawer.CashOutMessage(CasinoReasons.Cooldown, 0));
        Assert.Equal(CasinoReasons.Text(CasinoReasons.Expired, 0),
            CashierDrawer.CashOutMessage(CasinoReasons.Expired, 0));
    }

    [Fact]
    public void TheAsBuiltSittingShapeDeserializes()
    {
        const string json = """
        { "granted": true, "reason": "", "coins": 310, "chips": 310000, "stack": 930500, "balance": 12790,
          "sitting": { "id": "s1", "tableId": "", "gameKind": "bankroll", "state": 1, "stack": 930500,
                       "chipsIn": 2000000, "chipsOut": 310000, "rateChipsPerCoin": 1000 } }
        """;

        var result = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoCashOutChipsDto);

        Assert.Equal(930_500, result!.Sitting!.Stack);
        Assert.Equal(1_000, result.Sitting.RateChipsPerCoin);
    }

    [Fact]
    public void APartialCashOutLeavesTheRestOfTheBankrollOpen()
    {
        var state = new CasinoStateDto(Balance: 12_480,
            Sitting: new CasinoSittingDto(Id: "s1", State: 1, Stack: 1_240_500));
        var result = new CasinoCashOutChipsDto(true, string.Empty, 310, 310_000, 930_500, 12_790,
            new CasinoSittingDto(Id: "s1", State: 1, Stack: 930_500));

        var next = CasinoStore.CashOutChipsAbsorbedInto(state, result);

        Assert.Equal(12_790, next.Balance);
        Assert.Equal("s1", next.Sitting!.Id);
        Assert.Equal(930_500, next.Sitting.Stack);
    }

    [Fact]
    public void AnAnswerWithoutTheSittingStillLandsTheNewStack()
    {
        var state = new CasinoStateDto(Sitting: new CasinoSittingDto(Id: "s1", State: 1, Stack: 1_240_500));
        var result = new CasinoCashOutChipsDto(true, string.Empty, 310, 310_000, 930_500, 12_790);

        var next = CasinoStore.CashOutChipsAbsorbedInto(state, result);

        Assert.Equal("s1", next.Sitting!.Id);
        Assert.Equal(930_500, next.Sitting.Stack);
    }

    [Fact]
    public void CashingOutEverythingClosesTheBankroll()
    {
        var state = new CasinoStateDto(Sitting: new CasinoSittingDto(Id: "s1", State: 1, Stack: 1_240_500));
        var result = new CasinoCashOutChipsDto(true, string.Empty, 1_240, 1_240_500, 0, 13_720);

        var next = CasinoStore.CashOutChipsAbsorbedInto(state, result);

        Assert.Null(next.Sitting);
        Assert.Equal(13_720, next.Balance);
    }

    [Fact]
    public void ARefusedCashOutChangesNothing()
    {
        var state = new CasinoStateDto(Sitting: new CasinoSittingDto(Id: "s1", Stack: 1_240_500));

        Assert.Same(state,
            CasinoStore.CashOutChipsAbsorbedInto(state, new CasinoCashOutChipsDto(false, CasinoReasons.Frozen)));
    }

    [Fact]
    public void ARetryAfterALostAnswerReusesTheActionId()
    {
        var intent = new CashOutIntent("c0ffee02", 310, "s1", 1_240_500, 10_000);

        Assert.True(CasinoStore.KeepsCashOutIntent(intent, 310, "s1", 1_240_500, 15_000));
        Assert.False(CasinoStore.KeepsCashOutIntent(intent, 311, "s1", 1_240_500, 15_000));
        Assert.False(CasinoStore.KeepsCashOutIntent(intent, 310, "s1", 930_500, 15_000));
        Assert.False(CasinoStore.KeepsCashOutIntent(intent, 310, "s2", 1_240_500, 15_000));
        Assert.False(CasinoStore.KeepsCashOutIntent(CashOutIntent.None, 310, string.Empty, 0, 15_000));
    }

    [Fact]
    public void AKeptActionIdExpiresAfterAMinute()
    {
        var intent = new CashOutIntent("c0ffee02", 310, "s1", 1_240_500, 10_000);

        Assert.True(CasinoStore.KeepsCashOutIntent(intent, 310, "s1", 1_240_500,
            10_000 + CasinoStore.CashOutIntentLifetimeMilliseconds - 1));
        Assert.False(CasinoStore.KeepsCashOutIntent(intent, 310, "s1", 1_240_500,
            10_000 + CasinoStore.CashOutIntentLifetimeMilliseconds));
    }

    [Fact]
    public void ARefreshThatShowsADifferentBankrollDropsTheKeptId()
    {
        var intent = new CashOutIntent("c0ffee02", 310, "s1", 1_240_500, 10_000);

        Assert.False(CasinoStore.CashOutIntentStale(intent, new CasinoSittingDto(Id: "s1", Stack: 1_240_500)));
        Assert.True(CasinoStore.CashOutIntentStale(intent, new CasinoSittingDto(Id: "s1", Stack: 930_500)));
        Assert.True(CasinoStore.CashOutIntentStale(intent, new CasinoSittingDto(Id: "s2", Stack: 1_240_500)));
        Assert.True(CasinoStore.CashOutIntentStale(intent, null));
        Assert.False(CasinoStore.CashOutIntentStale(CashOutIntent.None, null));
    }

    [Fact]
    public void AReplayThatMovedNothingIsNotCelebrated()
    {
        var replay = new CasinoCashOutChipsDto(true, string.Empty, 310, 310_000, 1_240_500, 12_480,
            new CasinoSittingDto(Id: "s1", Stack: 1_240_500));
        var moved = replay with { Stack = 930_500, Sitting = new CasinoSittingDto(Id: "s1", Stack: 930_500) };
        var closed = replay with { Stack = 0, Sitting = null };

        Assert.False(CasinoStore.CashOutMoved(replay, 1_240_500));
        Assert.True(CasinoStore.CashOutMoved(moved, 1_240_500));
        Assert.True(CasinoStore.CashOutMoved(closed, 1_240_500));
        Assert.False(CasinoStore.CashOutMoved(new CasinoCashOutChipsDto(false, CasinoReasons.Cooldown), 1_240_500));
    }

    [Fact]
    public void TheFeatureFlagGatesTheQuarterTiles()
    {
        var tiles = new long[CashierAmounts.TileCount];
        var on = CasinoFeatureSet.From(new[] { CasinoFeatures.EconomyV3, "cashier.partial" });
        var off = CasinoFeatureSet.From(new[] { CasinoFeatures.EconomyV3 });

        Assert.Equal("cashier.partial", CasinoFeatures.PartialCashOut);
        Assert.Contains(CasinoFeatures.PartialCashOut, CasinoFeatureSet.Known);
        Assert.True(on.Has(CasinoFeatures.PartialCashOut));
        Assert.False(off.Has(CasinoFeatures.PartialCashOut));
        Assert.False(CasinoFeatureSet.Empty.Has(CasinoFeatures.PartialCashOut));
        Assert.Equal(4, CashierAmounts.CashOutTiles(1_240, on.Has(CasinoFeatures.PartialCashOut), tiles));
        Assert.Equal(1, CashierAmounts.CashOutTiles(1_240, off.Has(CasinoFeatures.PartialCashOut), tiles));
    }
}
