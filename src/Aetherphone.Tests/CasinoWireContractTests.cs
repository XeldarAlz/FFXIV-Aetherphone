using System.Text.Json;
using Aetherphone.Apps.Casino;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoWireContractTests
{
    [Fact]
    public void RoutesMatchTheBackendCasinoEndpoints()
    {
        Assert.Equal("/casino", CasinoClient.StatePath);
        Assert.Equal("/casino/chips/buy", CasinoClient.BuyChipsPath);
        Assert.Equal("/casino/sittings/close", CasinoClient.CloseSittingPath);
        Assert.Equal("/casino/limits", CasinoClient.LimitsPath);
    }

    [Fact]
    public void GameKindsMatchTheBackendWhitelist()
    {
        Assert.Equal("casino.slots", CasinoWire.Kind(CasinoGames.Slots));
        Assert.Equal("casino.scratch", CasinoWire.Kind(CasinoGames.Scratch));
        Assert.Equal("casino.bartender", CasinoWire.Kind(CasinoGames.Barkeep));
        Assert.Equal("casino.blackjack", CasinoWire.Kind(CasinoGames.Blackjack));
        Assert.Equal("casino.bingo", CasinoWire.Kind(CasinoGames.Bingo));
        Assert.Equal("casino.wheel", CasinoWire.Kind(CasinoGames.Wheel));
    }

    [Fact]
    public void CloseSittingRequestSerializesTheBackendShape()
    {
        var request = new CasinoCloseSittingRequest("sit1");
        var json = JsonSerializer.Serialize(request, AethernetJsonContext.Default.CasinoCloseSittingRequest);
        Assert.Equal("{\"sittingId\":\"sit1\"}", json);
    }

    [Fact]
    public void LimitRequestSerializesBothAValueAndAClear()
    {
        var lowered = JsonSerializer.Serialize(new CasinoLimitRequest(50),
            AethernetJsonContext.Default.CasinoLimitRequest);
        Assert.Equal("{\"selfLossLimit\":50}", lowered);

        var cleared = JsonSerializer.Serialize(new CasinoLimitRequest(null),
            AethernetJsonContext.Default.CasinoLimitRequest);
        Assert.Equal("{\"selfLossLimit\":null}", cleared);
    }

    [Fact]
    public void BackendStatePayloadDeserializesWithANestedSitting()
    {
        const string json = "{\"stakesPaused\":false,\"draining\":true,"
            + "\"sitting\":{\"id\":\"sit1\",\"tableId\":\"solo:casino.slots\",\"gameKind\":\"casino.slots\","
            + "\"state\":1,\"stack\":140,\"chipsIn\":200,\"chipsOut\":0},"
            + "\"minBuyIn\":100,\"maxBuyIn\":2000,\"dailyBuyInCap\":5000,"
            + "\"lossLimit\":500,\"lossHeadroom\":440,\"selfLossLimit\":null,"
            + "\"pendingRaiseLimit\":null,\"pendingRaiseAtUnix\":null,"
            + "\"netLossToday\":60,\"atRisk\":0,\"buyInToday\":200,\"balance\":1300}";
        var state = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoStateDto);
        Assert.NotNull(state);
        Assert.False(state.StakesPaused);
        Assert.True(state.Draining);
        Assert.NotNull(state.Sitting);
        Assert.Equal("sit1", state.Sitting.Id);
        Assert.Equal("casino.slots", state.Sitting.GameKind);
        Assert.Equal(140, state.Sitting.Stack);
        Assert.Equal(200, state.Sitting.ChipsIn);
        Assert.Equal(100, state.MinBuyIn);
        Assert.Equal(2000, state.MaxBuyIn);
        Assert.Equal(5000, state.DailyBuyInCap);
        Assert.Equal(500, state.LossLimit);
        Assert.Equal(440, state.LossHeadroom);
        Assert.Null(state.SelfLossLimit);
        Assert.Null(state.PendingRaiseLimit);
        Assert.Null(state.PendingRaiseAtUnix);
        Assert.Equal(60, state.NetLossToday);
        Assert.Equal(200, state.BuyInToday);
        Assert.Equal(1300, state.Balance);
    }

    [Fact]
    public void BackendStatePayloadCarriesTheProgressiveJackpot()
    {
        const string json = "{\"stakesPaused\":false,\"draining\":false,\"sitting\":null,"
            + "\"minBuyIn\":2000,\"maxBuyIn\":200000,\"dailyBuyInCap\":500000,"
            + "\"lossLimit\":50000,\"lossHeadroom\":50000,\"selfLossLimit\":null,"
            + "\"pendingRaiseLimit\":null,\"pendingRaiseAtUnix\":null,"
            + "\"netLossToday\":0,\"atRisk\":0,\"buyInToday\":0,\"balance\":1300,"
            + "\"tableSitting\":null,\"jackpot\":843000}";
        var state = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoStateDto);
        Assert.NotNull(state);
        Assert.Equal(843000L, state.Jackpot);
    }

    [Fact]
    public void AStatePayloadWithoutAJackpotStillLoads()
    {
        const string json = "{\"stakesPaused\":false,\"draining\":false,\"sitting\":null,"
            + "\"minBuyIn\":2000,\"maxBuyIn\":200000,\"balance\":1300}";
        var state = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoStateDto);
        Assert.NotNull(state);
        Assert.Equal(0L, state.Jackpot);
    }

    [Fact]
    public void HouseRoomIdsMatchTheRoomsTheBackendSeeds()
    {
        Assert.Equal("wheel-floor", CasinoRoomIds.WheelFloor);
        Assert.Equal("bingo-hall", CasinoRoomIds.BingoHall);
        Assert.Equal("blackjack-pit", CasinoRoomIds.BlackjackPit);
        Assert.Equal("blackjack-parlour", CasinoRoomIds.BlackjackParlour);
        Assert.Equal("blackjack-salon", CasinoRoomIds.BlackjackSalon);
        Assert.Equal("blackjack-vault", CasinoRoomIds.BlackjackVault);
        Assert.Equal(4, CasinoRoomIds.BlackjackHouse.Length);
    }

    [Fact]
    public void TheRoomCadenceMatchesTheScheduleTheServerRuns()
    {
        Assert.Equal(25, CasinoRoomCadence.WheelWindow(CasinoRoomPhases.Open));
        Assert.Equal(5, CasinoRoomCadence.WheelWindow(CasinoRoomPhases.Locked));
        Assert.Equal(10, CasinoRoomCadence.WheelWindow(CasinoRoomPhases.Result));
        Assert.Equal(45, CasinoRoomCadence.BingoWindow(CasinoRoomPhases.Open));
        Assert.Equal(110, CasinoRoomCadence.BingoWindow(CasinoRoomPhases.Locked));
        Assert.Equal(10, CasinoRoomCadence.BingoWindow(CasinoRoomPhases.Result));
    }

    [Fact]
    public void BackendStatePayloadDeserializesWithoutASitting()
    {
        const string json = "{\"stakesPaused\":false,\"draining\":false,\"sitting\":null,"
            + "\"minBuyIn\":100,\"maxBuyIn\":2000,\"dailyBuyInCap\":5000,"
            + "\"lossLimit\":500,\"lossHeadroom\":500,\"selfLossLimit\":50,"
            + "\"pendingRaiseLimit\":200,\"pendingRaiseAtUnix\":1754784000,"
            + "\"netLossToday\":0,\"atRisk\":0,\"buyInToday\":0,\"balance\":800}";
        var state = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoStateDto);
        Assert.NotNull(state);
        Assert.Null(state.Sitting);
        Assert.Equal(50L, state.SelfLossLimit);
        Assert.Equal(200L, state.PendingRaiseLimit);
        Assert.Equal(1754784000L, state.PendingRaiseAtUnix);
    }

    [Fact]
    public void BackendSittingResultDeserializesGrantAndDenial()
    {
        const string grantJson = "{\"granted\":true,\"reason\":\"\","
            + "\"sitting\":{\"id\":\"sit1\",\"tableId\":\"solo:casino.scratch\",\"gameKind\":\"casino.scratch\","
            + "\"state\":1,\"stack\":100,\"chipsIn\":100,\"chipsOut\":0},\"balance\":900}";
        var grant = JsonSerializer.Deserialize(grantJson, AethernetJsonContext.Default.CasinoSittingResultDto);
        Assert.NotNull(grant);
        Assert.True(grant.Granted);
        Assert.Equal(string.Empty, grant.Reason);
        Assert.NotNull(grant.Sitting);
        Assert.Equal(100, grant.Sitting.Stack);
        Assert.Equal(900, grant.Balance);

        const string denialJson = "{\"granted\":false,\"reason\":\"loss_limit\",\"sitting\":null,\"balance\":0}";
        var denial = JsonSerializer.Deserialize(denialJson, AethernetJsonContext.Default.CasinoSittingResultDto);
        Assert.NotNull(denial);
        Assert.False(denial.Granted);
        Assert.Equal(CasinoReasons.LossLimit, denial.Reason);
        Assert.Null(denial.Sitting);
    }

    [Fact]
    public void BackendLimitsPayloadDeserializes()
    {
        const string json = "{\"lossLimit\":50,\"selfLossLimit\":50,"
            + "\"pendingRaiseLimit\":null,\"pendingRaiseAtUnix\":null}";
        var limits = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoLimitsDto);
        Assert.NotNull(limits);
        Assert.Equal(50, limits.LossLimit);
        Assert.Equal(50L, limits.SelfLossLimit);
        Assert.Null(limits.PendingRaiseLimit);
        Assert.Null(limits.PendingRaiseAtUnix);
    }

    [Fact]
    public void MergeLimitsKeepsTheSittingAndFloorStateIntact()
    {
        var sitting = new CasinoSittingDto("sit1", "solo:casino.slots", "casino.slots", 1, 140, 200, 0);
        var state = new CasinoStateDto(
            StakesPaused: true,
            Draining: true,
            Sitting: sitting,
            MinBuyIn: 100,
            MaxBuyIn: 2000,
            DailyBuyInCap: 5000,
            LossLimit: 500,
            LossHeadroom: 440,
            SelfLossLimit: null,
            PendingRaiseLimit: null,
            PendingRaiseAtUnix: null,
            NetLossToday: 60,
            AtRisk: 0,
            BuyInToday: 200,
            Balance: 1300);
        var limits = new CasinoLimitsDto(50, 50, null, null);

        var merged = CasinoStore.MergeLimits(state, limits);

        Assert.Same(sitting, merged.Sitting);
        Assert.True(merged.StakesPaused);
        Assert.True(merged.Draining);
        Assert.Equal(100, merged.MinBuyIn);
        Assert.Equal(2000, merged.MaxBuyIn);
        Assert.Equal(60, merged.NetLossToday);
        Assert.Equal(50, merged.LossLimit);
        Assert.Equal(50L, merged.SelfLossLimit);
        Assert.Equal(0, merged.LossHeadroom);
    }

    [Fact]
    public void MergeLimitsRecomputesTheHeadroomFromTonightsMeter()
    {
        var state = new CasinoStateDto(
            LossLimit: 500,
            LossHeadroom: 500,
            NetLossToday: 60,
            AtRisk: 40);
        var limits = new CasinoLimitsDto(500, null, 500, 1754784000);

        var merged = CasinoStore.MergeLimits(state, limits);

        Assert.Equal(400, merged.LossHeadroom);
        Assert.Null(merged.SelfLossLimit);
        Assert.Equal(500L, merged.PendingRaiseLimit);
        Assert.Equal(1754784000L, merged.PendingRaiseAtUnix);
    }
}
