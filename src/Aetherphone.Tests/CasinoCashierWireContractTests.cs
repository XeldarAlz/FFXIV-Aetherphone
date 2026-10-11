using System.Text.Json;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoCashierWireContractTests
{
    [Fact]
    public void BuyingChipsIsOneCallToItsOwnRoute()
    {
        Assert.Equal("/casino/chips/buy", CasinoClient.BuyChipsPath);
        Assert.Equal("/casino/sittings/close", CasinoClient.CloseSittingPath);
    }

    [Fact]
    public void TheBuyRequestSerializesCoinsAndTheActionId()
    {
        var json = JsonSerializer.Serialize(new CasinoBuyChipsRequest(250, "c0ffee01"),
            AethernetJsonContext.Default.CasinoBuyChipsRequest);

        Assert.Equal("{\"coins\":250,\"clientActionId\":\"c0ffee01\"}", json);
    }

    [Fact]
    public void TheBuyAnswerCarriesTheStackTheWalletAndTheCeiling()
    {
        const string json = """
        { "granted": true, "reason": "", "coins": 250, "chips": 250000, "stack": 1250000, "balance": 1016001,
          "sitting": { "id": "s1", "state": 1, "stack": 1250000, "rateChipsPerCoin": 1000 },
          "ceiling": { "maxBet": 50000, "levelCap": 50000, "balanceCap": 62500, "balance": 1250000,
                       "reason": "balance", "nextLevelCap": 58740, "maxWinCap": 50000000 } }
        """;

        var result = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoBuyChipsDto);

        Assert.NotNull(result);
        Assert.True(result!.Granted);
        Assert.Equal(250, result.Coins);
        Assert.Equal(250_000, result.Chips);
        Assert.Equal(1_250_000, result.Stack);
        Assert.Equal(1_016_001, result.Balance);
        Assert.Equal("s1", result.Sitting!.Id);
        Assert.Equal(50_000_000, result.Ceiling!.MaxWinCap);
    }

    [Fact]
    public void AShortWalletAnswersInsufficient()
    {
        const string json = """{ "granted": false, "reason": "insufficient", "coins": 0, "chips": 0, "balance": 12 }""";

        var result = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoBuyChipsDto);

        Assert.False(result!.Granted);
        Assert.Equal(CasinoReasons.Insufficient, result.Reason);
        Assert.Equal(12, result.Balance);
        Assert.Null(result.Sitting);
        Assert.Equal(Core.Localization.L.Casino.ReasonInsufficient.Key,
            CasinoReasons.MessageFor(result.Reason).Key);
    }

    [Fact]
    public void TheFloorStateCarriesTheMaxWinAndItsCeilingBound()
    {
        const string json = """
        { "minBuyIn": 1000, "maxBuyIn": 0, "dailyBuyInCap": 0, "rateChipsPerCoin": 1000,
          "maxWinPerBet": 50000000,
          "ceiling": { "maxBet": 50000000, "levelCap": 100000000, "balanceCap": 7500, "balance": 150000,
                       "reason": "max_win", "nextLevelCap": 115000000, "maxWinCap": 50000000 },
          "cashier": null, "features": ["economy.v3", "cashier.v2"] }
        """;

        var state = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoStateDto);

        Assert.NotNull(state);
        Assert.Equal(1_000, state!.MinBuyIn);
        Assert.Equal(50_000_000, state.MaxWinPerBet);
        Assert.Equal(50_000_000, state.Ceiling!.MaxWinCap);
        var ceiling = CasinoLadder.CeilingFor(state);
        Assert.Equal(CeilingReason.MaxWin, ceiling.Reason);
        Assert.Equal(50_000_000, ceiling.MaxBet);
        Assert.Equal(50_000_000, ceiling.MaxWinCap);
    }

    [Fact]
    public void AnOldFloorFallsBackToTheDefaultMaxWin()
    {
        var state = JsonSerializer.Deserialize("{\"balance\":12}", AethernetJsonContext.Default.CasinoStateDto);

        Assert.Equal(0, state!.MaxWinPerBet);
        Assert.Equal(CasinoLadder.DefaultMaxWinPerBet, CasinoLadder.MaxWinOf(state));
        Assert.Equal(50_000_000, CasinoLadder.DefaultMaxWinPerBet);
    }

    [Fact]
    public void TheRetiredCapFieldsAreGoneFromTheClient()
    {
        Assert.Null(typeof(CasinoStateDto).GetProperty("Cashier"));
        Assert.Null(typeof(CasinoStateDto).GetProperty("MaxBuyIn"));
        Assert.Null(typeof(CasinoStateDto).GetProperty("DailyBuyInCap"));
        Assert.Null(typeof(CasinoSittingResultDto).GetProperty("QueuedChips"));
        Assert.Null(typeof(CasinoCashier).GetField("DailyNetCashOutCoinsFallback"));
        Assert.DoesNotContain("daily_buyin", CasinoReasons.All);
    }

    [Fact]
    public void ALegacyCashierBlockIsIgnored()
    {
        const string json = """
        { "sitting": { "id": "s1", "state": 1, "stack": 1000000 },
          "cashier": { "dailyNetCashOutCoins": 500, "allowanceCoins": 0, "queuedChips": 600000 } }
        """;

        var state = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoStateDto);

        Assert.Equal(1_000_000, state!.Sitting!.Stack);
    }

    [Fact]
    public void SoloRoundsCarryTheirCappedFlag()
    {
        Assert.True(Read("{\"totalWin\":50001000,\"capped\":true}",
            AethernetJsonContext.Default.CasinoSlotsSpinDto).Capped);
        Assert.True(Read("{\"payout\":20000,\"capped\":true}",
            AethernetJsonContext.Default.CasinoSlotsGambleDto).Capped);
        Assert.True(Read("{\"payout\":50000100,\"capped\":true}",
            AethernetJsonContext.Default.CasinoPlinkoDropDto).Capped);
        Assert.True(Read("{\"payout\":1,\"capped\":true}", AethernetJsonContext.Default.CasinoMinesDto).Capped);
        Assert.True(Read("{\"payout\":1,\"capped\":true}", AethernetJsonContext.Default.CasinoDiceDto).Capped);
        Assert.True(Read("{\"payout\":1,\"capped\":true}", AethernetJsonContext.Default.CasinoLimboDto).Capped);
        Assert.True(Read("{\"payout\":1,\"capped\":true}", AethernetJsonContext.Default.CasinoKenoDto).Capped);
        Assert.True(Read("{\"payout\":1,\"capped\":true}", AethernetJsonContext.Default.CasinoHiLoDto).Capped);
        Assert.True(Read("{\"prize\":1,\"capped\":true}", AethernetJsonContext.Default.CasinoScratchCardDto).Capped);
        Assert.True(Read("{\"payout\":1,\"capped\":true}",
            AethernetJsonContext.Default.CasinoBarkeepFinishDto).Capped);
        Assert.True(Read("{\"payout\":1,\"capped\":true}",
            AethernetJsonContext.Default.CasinoDealerHoldemDto).Capped);
        Assert.False(Read("{}", AethernetJsonContext.Default.CasinoSlotsSpinDto).Capped);
        Assert.False(Read("{}", AethernetJsonContext.Default.CasinoMinesDto).Capped);
    }

    [Fact]
    public void RoomAndTableRoundsCarryTheirCappedFlag()
    {
        var wheel = Read("{\"roomId\":\"wheel\",\"payout\":50100000,\"capped\":true}",
            AethernetJsonContext.Default.CasinoWheelBetsDto);
        Assert.Equal(50_100_000, wheel.Payout);
        Assert.True(wheel.Capped);
        Assert.True(Read("{\"payout\":1,\"capped\":true}", AethernetJsonContext.Default.CasinoBingoCardsDto).Capped);
        var race = Read("{\"myPayout\":5,\"capped\":true,\"previousCapped\":true}",
            AethernetJsonContext.Default.CasinoRaceBetsDto);
        Assert.True(race.Capped);
        Assert.True(race.PreviousCapped);
        Assert.True(Read("{\"seatIndex\":2,\"capped\":true}",
            AethernetJsonContext.Default.CasinoBlackjackSeatDto).Capped);
        Assert.False(Read("{}", AethernetJsonContext.Default.CasinoRaceBetsDto).PreviousCapped);
    }

    private static T Read<T>(string json, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> info) =>
        JsonSerializer.Deserialize(json, info)!;
}
