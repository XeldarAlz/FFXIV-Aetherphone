using System.Text.Json;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoGameWireContractTests
{
    [Fact]
    public void GameRoutesMatchTheBackendEndpoints()
    {
        Assert.Equal("/casino/slots/spin", CasinoClient.SpinSlotsPath);
        Assert.Equal("/casino/scratch/buy", CasinoClient.BuyScratchPath);
        Assert.Equal("/casino/barkeep/start", CasinoClient.StartBarkeepPath);
        Assert.Equal("/casino/barkeep/finish", CasinoClient.FinishBarkeepPath);
        Assert.Equal("/casino/rounds/round1/verify", CasinoClient.VerifyRoundPath("round1"));
        Assert.Equal("casino.slots", CasinoWire.SlotsKind);
    }

    [Fact]
    public void SpinRequestSerializesTheBackendShape()
    {
        var request = new CasinoSlotsSpinRequest("sit1", "round1", 2);
        var json = JsonSerializer.Serialize(request, AethernetJsonContext.Default.CasinoSlotsSpinRequest);
        Assert.Equal("{\"sittingId\":\"sit1\",\"clientRoundId\":\"round1\",\"stake\":2,"
            + "\"machineId\":\"slots.bird\",\"mode\":\"base\"}", json);
    }

    [Fact]
    public void GrantedSpinPayloadDeserializesWithBaseSpinAndFreeSpins()
    {
        const string json = "{\"granted\":true,\"reason\":\"\",\"roundId\":\"r1\",\"stake\":2,"
            + "\"baseSpin\":{\"grid\":[4,0,5,2,6,3,7,1,4,9,5,2,6,0,7],"
            + "\"lineWins\":[{\"line\":1,\"symbol\":4,\"count\":3,\"pay\":2}],"
            + "\"scatterCount\":3,\"scatterPay\":2,\"win\":4,\"spinsAdded\":8},"
            + "\"freeSpins\":[{\"grid\":[0,0,0,1,1,1,2,2,2,3,3,3,4,4,4],\"lineWins\":[],"
            + "\"scatterCount\":0,\"scatterPay\":0,\"win\":0,\"spinsAdded\":0}],"
            + "\"totalWin\":4,\"capApplied\":false,\"nextSeedHash\":\"next\",\"stack\":138}";
        var spin = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoSlotsSpinDto);
        Assert.NotNull(spin);
        Assert.True(spin.Granted);
        Assert.Equal("r1", spin.RoundId);
        Assert.Equal(2, spin.Stake);
        Assert.NotNull(spin.BaseSpin);
        Assert.NotNull(spin.BaseSpin.Grid);
        Assert.Equal(15, spin.BaseSpin.Grid.Length);
        Assert.Equal(9, spin.BaseSpin.Grid[9]);
        Assert.NotNull(spin.BaseSpin.LineWins);
        var lineWin = Assert.Single(spin.BaseSpin.LineWins);
        Assert.Equal(1, lineWin.Line);
        Assert.Equal(4, lineWin.Symbol);
        Assert.Equal(3, lineWin.Count);
        Assert.Equal(2, lineWin.Pay);
        Assert.Equal(3, spin.BaseSpin.ScatterCount);
        Assert.Equal(2, spin.BaseSpin.ScatterPay);
        Assert.Equal(4, spin.BaseSpin.Win);
        Assert.Equal(8, spin.BaseSpin.SpinsAdded);
        Assert.NotNull(spin.FreeSpins);
        Assert.Single(spin.FreeSpins);
        Assert.Equal(4, spin.TotalWin);
        Assert.False(spin.CapApplied);
        Assert.Equal("next", spin.NextSeedHash);
        Assert.Equal(138, spin.Stack);
    }

    [Fact]
    public void AFiveScatterSpinCarriesTheJackpotItPaid()
    {
        const string json = "{\"granted\":true,\"reason\":\"\",\"roundId\":\"r1\",\"stake\":500,"
            + "\"baseSpin\":{\"grid\":[9,0,5,2,9,3,7,9,4,1,9,2,6,0,9],\"lineWins\":[],"
            + "\"scatterCount\":5,\"scatterPay\":25000,\"win\":25000,\"spinsAdded\":12},"
            + "\"freeSpins\":[],\"totalWin\":25000,\"capApplied\":false,"
            + "\"nextSeedHash\":\"next\",\"stack\":868000,\"jackpot\":843000}";
        var spin = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoSlotsSpinDto);
        Assert.NotNull(spin);
        Assert.Equal(843000L, spin.Jackpot);
        Assert.NotNull(spin.BaseSpin);
        Assert.Equal(5, spin.BaseSpin.ScatterCount);
    }

    [Fact]
    public void AnOrdinarySpinReportsNoJackpot()
    {
        const string json = "{\"granted\":true,\"reason\":\"\",\"roundId\":\"r1\",\"stake\":500,"
            + "\"baseSpin\":{\"grid\":[4,0,5,2,6,3,7,1,4,9,5,2,6,0,7],\"lineWins\":[],"
            + "\"scatterCount\":0,\"scatterPay\":0,\"win\":0,\"spinsAdded\":0},"
            + "\"freeSpins\":[],\"totalWin\":0,\"capApplied\":false,"
            + "\"nextSeedHash\":\"next\",\"stack\":137500}";
        var spin = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoSlotsSpinDto);
        Assert.NotNull(spin);
        Assert.Equal(0L, spin.Jackpot);
    }

    [Fact]
    public void RefusedSpinPayloadDeserializesWithTheReason()
    {
        const string json = "{\"granted\":false,\"reason\":\"cooldown\",\"roundId\":\"r1\",\"stake\":5,"
            + "\"baseSpin\":null,\"freeSpins\":[],\"totalWin\":0,\"capApplied\":false,"
            + "\"nextSeedHash\":\"\",\"stack\":40}";
        var spin = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoSlotsSpinDto);
        Assert.NotNull(spin);
        Assert.False(spin.Granted);
        Assert.Equal(CasinoReasons.Cooldown, spin.Reason);
        Assert.Null(spin.BaseSpin);
        Assert.Equal(40, spin.Stack);
    }

    [Fact]
    public void ScratchRequestSerializesTheBackendShape()
    {
        var request = new CasinoScratchBuyRequest("sit1", "round2", 1);
        var json = JsonSerializer.Serialize(request, AethernetJsonContext.Default.CasinoScratchBuyRequest);
        Assert.Equal("{\"sittingId\":\"sit1\",\"clientRoundId\":\"round2\",\"tier\":1}", json);
    }

    [Fact]
    public void ScratchCardPayloadDeserializes()
    {
        const string json = "{\"granted\":true,\"reason\":\"\",\"roundId\":\"r2\",\"tier\":1,"
            + "\"cells\":[0,1,2,0,3,5,0,6,4],\"prize\":50,\"nextSeedHash\":\"next\",\"stack\":90}";
        var card = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoScratchCardDto);
        Assert.NotNull(card);
        Assert.True(card.Granted);
        Assert.Equal(1, card.Tier);
        Assert.NotNull(card.Cells);
        Assert.Equal(9, card.Cells.Length);
        Assert.Equal(50, card.Prize);
        Assert.Equal(90, card.Stack);
    }

    [Fact]
    public void BarkeepStartRequestSerializesTheBackendShape()
    {
        var request = new CasinoBarkeepStartRequest("sit1", "round3");
        var json = JsonSerializer.Serialize(request, AethernetJsonContext.Default.CasinoBarkeepStartRequest);
        Assert.Equal("{\"sittingId\":\"sit1\",\"clientRoundId\":\"round3\"}", json);
    }

    [Fact]
    public void BarkeepStartPayloadDeserializesWithThePatronScript()
    {
        const string json = "{\"granted\":true,\"reason\":\"\",\"roundId\":\"r3\","
            + "\"patrons\":[{\"arrivalSecond\":5,\"stepKinds\":[0,2,3]},{\"arrivalSecond\":22,\"stepKinds\":[1,1]}],"
            + "\"maxScore\":500,\"startedAtUnix\":1754784000,\"expiresAtUnix\":1754784300,"
            + "\"nextSeedHash\":\"next\",\"stack\":80}";
        var start = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoBarkeepStartDto);
        Assert.NotNull(start);
        Assert.True(start.Granted);
        Assert.NotNull(start.Patrons);
        Assert.Equal(2, start.Patrons.Length);
        Assert.Equal(5, start.Patrons[0].ArrivalSecond);
        Assert.NotNull(start.Patrons[0].StepKinds);
        Assert.Equal(new[] { 0, 2, 3 }, start.Patrons[0].StepKinds);
        Assert.Equal(500, start.MaxScore);
        Assert.Equal(1754784000, start.StartedAtUnix);
        Assert.Equal(1754784300, start.ExpiresAtUnix);
        Assert.Equal(80, start.Stack);
    }

    [Fact]
    public void BarkeepFinishRequestSerializesTheOrderGrades()
    {
        var request = new CasinoBarkeepFinishRequest("r3", new[]
        {
            new CasinoBarkeepOrderRequest(new[] { 100, 70, 40 }),
            new CasinoBarkeepOrderRequest(new[] { 0, 100 }),
        });
        var json = JsonSerializer.Serialize(request, AethernetJsonContext.Default.CasinoBarkeepFinishRequest);
        Assert.Equal("{\"roundId\":\"r3\",\"orders\":[{\"stepGrades\":[100,70,40]},{\"stepGrades\":[0,100]}]}",
            json);
    }

    [Fact]
    public void BarkeepFinishPayloadDeserializesGrantAndCap()
    {
        const string json = "{\"granted\":true,\"reason\":\"cap_reached\",\"roundId\":\"r3\","
            + "\"score\":940,\"payout\":40,\"netWinToday\":100,\"stack\":120}";
        var finish = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoBarkeepFinishDto);
        Assert.NotNull(finish);
        Assert.True(finish.Granted);
        Assert.Equal(CasinoReasons.CapReached, finish.Reason);
        Assert.Equal(940, finish.Score);
        Assert.Equal(40, finish.Payout);
        Assert.Equal(100, finish.NetWinToday);
        Assert.Equal(120, finish.Stack);
    }

    [Fact]
    public void VerifyPayloadDeserializesTheFairnessRecipeFields()
    {
        const string json = "{\"granted\":true,\"reason\":\"\",\"roundId\":\"r1\",\"gameKind\":\"casino.slots\","
            + "\"state\":2,\"stake\":2,\"payout\":4,\"seedCommitHash\":\"commit\",\"seedRevealed\":\"seed\","
            + "\"nextSeedHash\":\"next\",\"drawLog\":\"s0r0:12;s0r1:3\",\"streamBinding\":\"room-7#4\"}";
        var verify = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoRoundVerifyDto);
        Assert.NotNull(verify);
        Assert.True(verify.Granted);
        Assert.Equal("casino.slots", verify.GameKind);
        Assert.Equal(2, verify.State);
        Assert.Equal(2, verify.Stake);
        Assert.Equal(4, verify.Payout);
        Assert.Equal("commit", verify.SeedCommitHash);
        Assert.Equal("seed", verify.SeedRevealed);
        Assert.Equal("next", verify.NextSeedHash);
        Assert.Equal("s0r0:12;s0r1:3", verify.DrawLog);
        Assert.Equal("room-7#4", verify.StreamBinding);
    }

    [Fact]
    public void OpenRoundVerifyPayloadKeepsTheSeedHidden()
    {
        const string json = "{\"granted\":false,\"reason\":\"round_open\",\"roundId\":\"r4\","
            + "\"gameKind\":\"casino.bartender\",\"state\":0,\"stake\":20,\"payout\":0,"
            + "\"seedCommitHash\":\"commit\",\"seedRevealed\":\"\",\"nextSeedHash\":\"next\",\"drawLog\":\"\"}";
        var verify = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoRoundVerifyDto);
        Assert.NotNull(verify);
        Assert.False(verify.Granted);
        Assert.Equal(CasinoReasons.RoundOpen, verify.Reason);
        Assert.Equal("commit", verify.SeedCommitHash);
        Assert.Equal(string.Empty, verify.SeedRevealed);
        Assert.Equal(string.Empty, verify.StreamBinding);
    }
}
