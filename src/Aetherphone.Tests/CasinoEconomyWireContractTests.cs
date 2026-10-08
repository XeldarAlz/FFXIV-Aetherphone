using System.Text.Json;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoEconomyWireContractTests
{
    [Fact]
    public void TheFloorStateCarriesTheWholeEconomyBlock()
    {
        const string json = """
        {
          "stakesPaused": false,
          "draining": false,
          "sitting": { "id": "s1", "tableId": "", "gameKind": "casino.floor", "state": 1, "stack": 150000,
                       "chipsIn": 100000, "chipsOut": 0, "rateChipsPerCoin": 1000 },
          "minBuyIn": 20000,
          "maxBuyIn": 5000000,
          "dailyBuyInCap": 100000000,
          "lossLimit": 0,
          "lossHeadroom": 0,
          "netLossToday": 0,
          "atRisk": 0,
          "buyInToday": 0,
          "balance": 420,
          "features": ["economy.v3", "bonus", "levels", "club", "hosting.v2", "venue", "gil.tables"],
          "rateChipsPerCoin": 1000,
          "progress": { "level": 23, "xp": 41234, "levelStartXp": 39000, "nextLevelXp": 46100,
                        "lifetimeWagered": 12345000, "lifetimeWon": 11800000,
                        "bestMultiplierTenths": 412, "title": "shark" },
          "ceiling": { "maxBet": 250000, "levelCap": 400000, "balanceCap": 7500,
                       "balance": 150000, "reason": "level", "nextLevelCap": 470000 },
          "ladder": [100, 250, 500],
          "levelCapAnchors": [10000, 50000, 250000, 1000000, 5000000, 25000000, 100000000, 500000000,
                              2000000000, 10000000000, 50000000000],
          "cashier": { "dailyNetCashOutCoins": 500, "cashOutCoinsToday": 120, "buyInCoinsToday": 20,
                       "allowanceCoins": 400, "queuedChips": 0 },
          "bonuses": [ { "kind": "timed", "ready": false, "amount": 6250, "nextAtUnix": 1791500000,
                         "streakDay": 0, "eligible": true } ],
          "club": { "tier": 2, "tierKey": "gold", "points": 61000, "tierFloor": 50000,
                    "nextTierPoints": 250000, "multiplierPercent": 150, "rebateBasisPoints": 750 }
        }
        """;

        var state = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoStateDto);

        Assert.NotNull(state);
        Assert.Equal(1000, state!.RateChipsPerCoin);
        Assert.Equal(1000, state.Sitting!.RateChipsPerCoin);
        Assert.Contains("gil.tables", state.Features!);
        Assert.Equal(new long[] { 100, 250, 500 }, state.Ladder);
        Assert.Equal(400, state.Cashier!.AllowanceCoins);
        Assert.Equal(500, state.Cashier.DailyNetCashOutCoins);
        var bonus = Assert.Single(state.Bonuses!);
        Assert.Equal(CasinoBonusKinds.Timed, bonus.Kind);
        Assert.Equal(6250, bonus.Amount);
        Assert.True(bonus.Eligible);
        Assert.Equal("gold", state.Club!.TierKey);
        Assert.Equal(150, state.Club.MultiplierPercent);
        Assert.Equal(750, state.Club.RebateBasisPoints);
        Assert.Equal(23, state.Progress!.Level);
        Assert.Equal(250000, state.Ceiling!.MaxBet);
    }

    [Fact]
    public void AnOldFloorReadsAsZerosAndEmptyLists()
    {
        var state = JsonSerializer.Deserialize("{\"balance\":12}", AethernetJsonContext.Default.CasinoStateDto);

        Assert.NotNull(state);
        Assert.Null(state!.Features);
        Assert.Null(state.Cashier);
        Assert.Null(state.Bonuses);
        Assert.Null(state.Club);
        Assert.Equal(0, state.RateChipsPerCoin);
    }

    [Fact]
    public void TheCashOutAnswerSaysWhatConvertedAndWhatWaits()
    {
        const string json = """
        { "granted": true, "reason": "", "balance": 920, "convertedCoins": 400, "queuedChips": 600000,
          "sitting": { "id": "s1", "state": 1, "stack": 600000, "rateChipsPerCoin": 1000 } }
        """;

        var result = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoSittingResultDto);

        Assert.NotNull(result);
        Assert.True(result!.Granted);
        Assert.Equal(400, result.ConvertedCoins);
        Assert.Equal(600000, result.QueuedChips);
        Assert.Equal(1, result.Sitting!.State);
    }

    [Fact]
    public void TheBonusClaimHasItsOwnRouteAndShape()
    {
        Assert.Equal("/casino/bonus/timed/claim", CasinoClient.BonusClaimPath(CasinoBonusKinds.Timed));
        Assert.Equal("/casino/bonus/levelup/claim", CasinoClient.BonusClaimPath(CasinoBonusKinds.LevelUp));

        var body = JsonSerializer.Serialize(new CasinoBonusClaimRequest("abc"),
            AethernetJsonContext.Default.CasinoBonusClaimRequest);
        Assert.Equal("{\"clientActionId\":\"abc\"}", body);

        const string json = """
        { "granted": true, "reason": "", "kind": "streak", "amount": 6000, "stack": 156000,
          "sitting": { "id": "s1", "stack": 156000 }, "nextAtUnix": 1791500000, "streakDay": 3, "level": 0 }
        """;
        var claim = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoBonusClaimDto);
        Assert.NotNull(claim);
        Assert.Equal(3, claim!.StreakDay);
        Assert.Equal(156000, claim.Sitting!.Stack);
    }

    [Fact]
    public void EveryCeilingRefusalCarriesTheCap()
    {
        const string refused = "{\"granted\":false,\"reason\":\"ceiling\",\"ceiling\":25000}";
        Assert.Equal(25000, JsonSerializer.Deserialize(refused, AethernetJsonContext.Default.CasinoSlotsSpinDto)!.Ceiling);
        Assert.Equal(25000,
            JsonSerializer.Deserialize(refused, AethernetJsonContext.Default.CasinoScratchCardDto)!.Ceiling);
        Assert.Equal(25000, JsonSerializer.Deserialize(refused, AethernetJsonContext.Default.CasinoWheelBetDto)!.Ceiling);
        Assert.Equal(25000,
            JsonSerializer.Deserialize(refused, AethernetJsonContext.Default.CasinoBingoCardsDto)!.Ceiling);
    }

    [Fact]
    public void ARefusedCeilingLowersTheMaxBetAtOnce()
    {
        var state = new CasinoStateDto(Ceiling: new CasinoCeilingDto(MaxBet: 250000, LevelCap: 250000));

        var next = CasinoStore.CeilingAbsorbedInto(state, 25000);

        Assert.NotNull(next);
        Assert.Equal(25000, next!.Ceiling!.MaxBet);
        Assert.Equal(25000, CasinoLadder.CeilingFor(next).MaxBet);
        Assert.Null(CasinoStore.CeilingAbsorbedInto(state, 0));
        Assert.Null(CasinoStore.CeilingAbsorbedInto(next, 25000));
    }

    [Fact]
    public void TheRateAndTheCashierFiguresFollowEconomyV3()
    {
        Assert.Equal(1000, CasinoChipLots.ChipPerCoin);
        Assert.Equal(10_000, CasinoChipLots.JackpotSeedCoins);
        Assert.Equal(100_000, CasinoChipLots.JackpotCapCoins);
        Assert.Equal(20_000, CasinoHostingRules.ChipMinBuyIn);
        Assert.Equal(5_000_000, CasinoHostingRules.ChipMaxBuyIn);
        Assert.Equal(new long[] { 250, 1_000, 5_000, 25_000, 100_000 }, ScratchRules.Prices);
        Assert.Equal(50_000, WheelRules.MaxStakePerSpot);
        Assert.Equal(200_000, WheelRules.MaxStakePerRound);
        Assert.Equal(2_000, BarkeepRules.EntryChips);
        Assert.Equal(new long[] { 500, 5_000, 50_000, 500_000 }, BlackjackRules.HouseTierMinBets);
        Assert.Equal(new long[] { 10_000, 100_000, 1_000_000, 10_000_000 }, BlackjackRules.HouseTierMaxBets);
        Assert.Equal(500, BlackjackRules.MinBet);
    }
}
