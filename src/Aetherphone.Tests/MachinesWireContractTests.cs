using System.Text.Json;
using Aetherphone.Apps.Casino;
using Aetherphone.Apps.Casino.Machines;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Xunit;

namespace Aetherphone.Tests;

public sealed class MachinesWireContractTests
{
    [Fact]
    public void RoutesMatchTheBackendEndpoints()
    {
        Assert.Equal("/casino/slots/spin", CasinoClient.SpinSlotsPath);
        Assert.Equal("/casino/slots/gamble", CasinoClient.GambleSlotsPath);
        Assert.Equal("/casino/slots/meters?machineId=slots.moogle&bet=1000",
            CasinoClient.SlotsMetersQuery(SlotsRules.MoogleId, 1_000));
        Assert.Equal("casino.slots.gamble", CasinoWire.SlotsGambleKind);
        Assert.Equal("machines", CasinoFeatures.Machines);
    }

    [Fact]
    public void SpinRequestCarriesTheMachineAndMode()
    {
        var request = new CasinoSlotsSpinRequest("sit1", "round1", 1_000, SlotsRules.CascadeId, SlotsRules.AnteMode);
        var json = JsonSerializer.Serialize(request, AethernetJsonContext.Default.CasinoSlotsSpinRequest);
        Assert.Equal("{\"sittingId\":\"sit1\",\"clientRoundId\":\"round1\",\"stake\":1000,"
            + "\"machineId\":\"slots.cascade\",\"mode\":\"ante\"}", json);
    }

    [Fact]
    public void GambleRequestSerializesTheBackendShape()
    {
        var request = new CasinoSlotsGambleRequest("sit1", "round2", "round1", SlotsRules.GambleBlack);
        var json = JsonSerializer.Serialize(request, AethernetJsonContext.Default.CasinoSlotsGambleRequest);
        Assert.Equal("{\"sittingId\":\"sit1\",\"clientRoundId\":\"round2\",\"parentRoundId\":\"round1\",\"pick\":1}",
            json);
    }

    [Fact]
    public void StepRoundDeserializesEveryNewField()
    {
        const string json = "{\"granted\":true,\"reason\":\"\",\"roundId\":\"r1\",\"stake\":1250,\"baseSpin\":null,"
            + "\"freeSpins\":[],\"totalWin\":4200,\"capApplied\":false,\"nextSeedHash\":\"n\",\"stack\":151000,"
            + "\"jackpot\":0,\"ceiling\":0,\"machineId\":\"slots.cascade\",\"bet\":1000,\"mode\":\"ante\","
            + "\"steps\":[{\"kind\":\"tumble\",\"grid\":[1,2],\"wins\":[{\"line\":-2,\"symbol\":4,\"count\":9,"
            + "\"cells\":[1,2],\"pay\":500}],\"pay\":1000,\"multiplier\":2,\"spinsAdded\":0,\"spinsLeft\":0,"
            + "\"expander\":-1,\"coins\":[{\"cell\":7,\"kind\":10,\"multiple\":5,\"value\":0}],\"running\":1500}],"
            + "\"bonusTriggered\":true,\"freeSpinsPlayed\":12,\"expander\":-1,\"featureMultiplier\":37,"
            + "\"meters\":[{\"tier\":\"mini\",\"value\":23150,\"multipleHundredths\":2315,\"resetHundredths\":2000,"
            + "\"ceilingHundredths\":3000}]}";
        var spin = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoSlotsSpinDto)!;
        Assert.Equal(SlotsRules.CascadeId, spin.MachineId);
        Assert.Equal(1_000, spin.Bet);
        Assert.Equal(SlotsRules.AnteMode, spin.Mode);
        Assert.True(spin.BonusTriggered);
        Assert.Equal(12, spin.FreeSpinsPlayed);
        Assert.Equal(37, spin.FeatureMultiplier);
        var step = Assert.Single(spin.Steps!);
        Assert.Equal(SlotsRules.StepTumble, step.Kind);
        Assert.Equal(2, step.Multiplier);
        Assert.Equal(1_500, step.Running);
        var win = Assert.Single(step.Wins!);
        Assert.Equal(SlotsRules.LineCluster, win.Line);
        Assert.Equal(new[] { 1, 2 }, win.Cells);
        var coin = Assert.Single(step.Coins!);
        Assert.Equal(SlotsRules.CoinOrb, coin.Kind);
        Assert.Equal(5, coin.Multiple);
        var meter = Assert.Single(spin.Meters!);
        Assert.Equal(SlotsRules.MeterMini, meter.Tier);
        Assert.Equal(3_000, meter.CeilingHundredths);
    }

    [Fact]
    public void AnOldServerSpinDefaultsToGoldenBird()
    {
        const string json = "{\"granted\":false,\"reason\":\"cooldown\",\"roundId\":\"r1\",\"stake\":5}";
        var spin = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoSlotsSpinDto)!;
        Assert.Equal(SlotsRules.BirdId, spin.MachineId);
        Assert.Equal(SlotsRules.BaseMode, spin.Mode);
        Assert.Equal(-1, spin.Expander);
        Assert.Null(spin.Steps);
    }

    [Fact]
    public void GambleAndMetersAnswersDeserialize()
    {
        const string gambleJson = "{\"granted\":true,\"reason\":\"\",\"roundId\":\"g1\",\"parentRoundId\":\"r1\","
            + "\"step\":1,\"stake\":2000,\"card\":1,\"won\":true,\"payout\":4000,\"canContinue\":true,"
            + "\"nextSeedHash\":\"n\",\"stack\":153000}";
        var gamble = JsonSerializer.Deserialize(gambleJson, AethernetJsonContext.Default.CasinoSlotsGambleDto)!;
        Assert.Equal("r1", gamble.ParentRoundId);
        Assert.Equal(SlotsRules.GambleBlack, gamble.Card);
        Assert.True(gamble.Won);
        Assert.True(gamble.CanContinue);
        Assert.Equal(4_000, gamble.Payout);
        const string metersJson = "{\"machineId\":\"slots.moogle\",\"bet\":1000,\"meters\":[{\"tier\":\"mini\","
            + "\"value\":23150,\"multipleHundredths\":2315,\"resetHundredths\":2000,\"ceilingHundredths\":3000},"
            + "{\"tier\":\"minor\",\"value\":61800,\"multipleHundredths\":6180,\"resetHundredths\":5000,"
            + "\"ceilingHundredths\":10000}]}";
        var meters = JsonSerializer.Deserialize(metersJson, AethernetJsonContext.Default.CasinoSlotsMetersDto)!;
        Assert.Equal(2, meters.Meters!.Length);
        Assert.Equal(61_800, meters.Meters[1].Value);
    }

    [Fact]
    public void TheUnknownMachineRefusalIsCovered()
    {
        Assert.Contains("machine_unknown", CasinoReasons.All);
        Assert.Equal(L.Machines.ReasonMachineUnknown.Key, CasinoReasons.MessageFor("machine_unknown").Key);
    }

    [Fact]
    public void ThreeMachinesAreRegisteredOnTheFloor()
    {
        Assert.Equal(new[] { "slots.bird", "slots.cascade", "slots.moogle" }, MachineCabinet.MachineIds);
        Assert.True(MachineCabinet.Owns(CasinoGames.Slots));
        Assert.Equal(SlotsRules.BirdId, MachineCabinet.MachineFor(CasinoGames.Slots));
        Assert.False(MachineCabinet.Owns(CasinoGames.SlotsGamble));
        for (var index = 0; index < MachineCabinet.MachineIds.Length; index++)
        {
            var id = MachineCabinet.MachineIds[index];
            Assert.True(MachineCabinet.Owns(id));
            Assert.NotEmpty(CasinoRules.StepsOf(id));
            Assert.True(CasinoRules.FactsOf(id, 0, out _, out _));
            Assert.Equal(MachineCabinet.TitleOf(id).Key, CasinoRules.TitleOf(id).Key);
        }
    }
}
