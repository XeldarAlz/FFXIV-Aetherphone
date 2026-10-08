using System.Text.Json;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class OriginalsWireContractTests
{
    [Fact]
    public void TheRoutesMatchTheWireDoc()
    {
        Assert.Equal("/casino/mines/start", CasinoClient.MinesStartPath);
        Assert.Equal("/casino/mines/reveal", CasinoClient.MinesRevealPath);
        Assert.Equal("/casino/mines/cashout", CasinoClient.MinesCashOutPath);
        Assert.Equal("/casino/dice/roll", CasinoClient.DiceRollPath);
        Assert.Equal("/casino/limbo/play", CasinoClient.LimboPlayPath);
        Assert.Equal("/casino/keno/draw", CasinoClient.KenoDrawPath);
        Assert.Equal("/casino/hilo/start", CasinoClient.HiLoStartPath);
        Assert.Equal("/casino/hilo/guess", CasinoClient.HiLoGuessPath);
        Assert.Equal("/casino/hilo/skip", CasinoClient.HiLoSkipPath);
        Assert.Equal("/casino/hilo/cashout", CasinoClient.HiLoCashOutPath);
        Assert.Equal("/casino/originals/open", CasinoClient.OriginalsOpenPath);
    }

    [Fact]
    public void TheGameKindsMatchTheRegistry()
    {
        Assert.Equal("casino.mines", CasinoWire.MinesKind);
        Assert.Equal("casino.dice", CasinoWire.DiceKind);
        Assert.Equal("casino.limbo", CasinoWire.LimboKind);
        Assert.Equal("casino.keno", CasinoWire.KenoKind);
        Assert.Equal("casino.hilo", CasinoWire.HiLoKind);
        Assert.True(CasinoVerifier.IsOriginalsKind(CasinoWire.KenoKind));
        Assert.False(CasinoVerifier.IsOriginalsKind(CasinoWire.SlotsKind));
    }

    [Fact]
    public void TheRequestsSerializeTheBackendShapes()
    {
        Assert.Equal("{\"sittingId\":\"s1\",\"clientRoundId\":\"c1\",\"stake\":1000,\"mines\":3}",
            JsonSerializer.Serialize(new CasinoMinesStartRequest("s1", "c1", 1000, 3),
                AethernetJsonContext.Default.CasinoMinesStartRequest));
        Assert.Equal("{\"roundId\":\"r1\",\"tile\":12}",
            JsonSerializer.Serialize(new CasinoMinesRevealRequest("r1", 12),
                AethernetJsonContext.Default.CasinoMinesRevealRequest));
        Assert.Equal("{\"roundId\":\"r1\"}",
            JsonSerializer.Serialize(new CasinoOriginalsRoundRequest("r1"),
                AethernetJsonContext.Default.CasinoOriginalsRoundRequest));
        Assert.Equal("{\"sittingId\":\"s1\",\"clientRoundId\":\"c1\",\"stake\":1000,\"target\":5050,\"over\":true}",
            JsonSerializer.Serialize(new CasinoDiceRollRequest("s1", "c1", 1000, 5050, true),
                AethernetJsonContext.Default.CasinoDiceRollRequest));
        Assert.Equal("{\"sittingId\":\"s1\",\"clientRoundId\":\"c1\",\"stake\":1000,\"target\":200}",
            JsonSerializer.Serialize(new CasinoLimboPlayRequest("s1", "c1", 1000, 200),
                AethernetJsonContext.Default.CasinoLimboPlayRequest));
        Assert.Equal("{\"sittingId\":\"s1\",\"clientRoundId\":\"c1\",\"stake\":1000,\"risk\":2,\"picks\":[3,9,17]}",
            JsonSerializer.Serialize(new CasinoKenoDrawRequest("s1", "c1", 1000, 2, new[] { 3, 9, 17 }),
                AethernetJsonContext.Default.CasinoKenoDrawRequest));
        Assert.Equal("{\"sittingId\":\"s1\",\"clientRoundId\":\"c1\",\"stake\":1000}",
            JsonSerializer.Serialize(new CasinoHiLoStartRequest("s1", "c1", 1000),
                AethernetJsonContext.Default.CasinoHiLoStartRequest));
        Assert.Equal("{\"roundId\":\"r5\",\"step\":2,\"call\":\"higher\"}",
            JsonSerializer.Serialize(new CasinoHiLoGuessRequest("r5", 2, "higher"),
                AethernetJsonContext.Default.CasinoHiLoGuessRequest));
        Assert.Equal("{\"roundId\":\"r5\",\"step\":2}",
            JsonSerializer.Serialize(new CasinoHiLoSkipRequest("r5", 2),
                AethernetJsonContext.Default.CasinoHiLoSkipRequest));
    }

    [Fact]
    public void AMinesRoundReadsEveryWireField()
    {
        const string json = "{\"granted\":true,\"reason\":\"\",\"roundId\":\"r1\",\"stake\":1000,\"mines\":3,"
            + "\"phase\":0,\"revealed\":[12,7],\"mineTiles\":[],\"multiplierTenThousandths\":12500,"
            + "\"nextMultiplierTenThousandths\":14700,\"payout\":0,\"nextSeedHash\":\"hex\",\"stack\":199000,"
            + "\"ceiling\":0}";
        var round = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoMinesDto)!;
        Assert.True(round.Granted);
        Assert.Equal("r1", round.RoundId);
        Assert.Equal(3, round.Mines);
        Assert.Equal(OriginalsRules.PhaseLive, round.Phase);
        Assert.Equal(new[] { 12, 7 }, round.Revealed);
        Assert.Empty(round.MineTiles!);
        Assert.Equal(12500, round.MultiplierTenThousandths);
        Assert.Equal(14700, round.NextMultiplierTenThousandths);
        Assert.Equal(199000, round.Stack);
    }

    [Fact]
    public void TheInstantGamesReadEveryWireField()
    {
        var dice = JsonSerializer.Deserialize("{\"granted\":true,\"reason\":\"\",\"roundId\":\"r2\",\"stake\":1000,"
            + "\"target\":5050,\"over\":true,\"chance\":4950,\"roll\":7312,\"won\":true,"
            + "\"multiplierTenThousandths\":20000,\"payout\":2000,\"nextSeedHash\":\"hex\",\"stack\":201000,"
            + "\"ceiling\":0}", AethernetJsonContext.Default.CasinoDiceDto)!;
        Assert.Equal(7312, dice.Roll);
        Assert.Equal(4950, dice.Chance);
        Assert.True(dice.Over);
        Assert.Equal(2000, dice.Payout);

        var limbo = JsonSerializer.Deserialize("{\"granted\":true,\"reason\":\"\",\"roundId\":\"r3\",\"stake\":1000,"
            + "\"target\":200,\"result\":347,\"won\":true,\"payout\":2000,\"nextSeedHash\":\"hex\",\"stack\":201000,"
            + "\"ceiling\":0}", AethernetJsonContext.Default.CasinoLimboDto)!;
        Assert.Equal(347, limbo.Result);
        Assert.True(limbo.Won);

        var keno = JsonSerializer.Deserialize("{\"granted\":true,\"reason\":\"\",\"roundId\":\"r4\",\"stake\":1000,"
            + "\"risk\":0,\"picks\":[3,9,17],\"drawn\":[17,2,30,9,11,0,25,39,14,6],\"hits\":2,"
            + "\"multiplierTenThousandths\":31000,\"payout\":3100,\"nextSeedHash\":\"hex\",\"stack\":202100,"
            + "\"ceiling\":0}", AethernetJsonContext.Default.CasinoKenoDto)!;
        Assert.Equal(10, keno.Drawn!.Length);
        Assert.Equal(2, keno.Hits);
        Assert.Equal(3100, keno.Payout);
    }

    [Fact]
    public void AHiLoRoundAndTheOpenRoundsRead()
    {
        const string hiLo = "{\"granted\":true,\"reason\":\"\",\"roundId\":\"r5\",\"stake\":1000,\"phase\":0,"
            + "\"step\":2,\"cards\":[20,33,5],\"moves\":[\"higher\",\"skip\"],\"card\":5,"
            + "\"multiplierTenThousandths\":17160,\"calls\":[{\"call\":\"higher\",\"chanceBasisPoints\":9231,"
            + "\"multiplierTenThousandths\":10725}],\"payout\":0,\"nextSeedHash\":\"hex\",\"stack\":199000,"
            + "\"ceiling\":0}";
        var round = JsonSerializer.Deserialize(hiLo, AethernetJsonContext.Default.CasinoHiLoDto)!;
        Assert.Equal(2, round.Step);
        Assert.Equal(new[] { "higher", "skip" }, round.Moves);
        Assert.Equal("higher", round.Calls![0].Call);
        Assert.Equal(10725, round.Calls[0].MultiplierTenThousandths);

        var open = JsonSerializer.Deserialize("{\"mines\":null,\"hiLo\":" + hiLo + "}",
            AethernetJsonContext.Default.CasinoOriginalsOpenDto)!;
        Assert.Null(open.Mines);
        Assert.Equal("r5", open.HiLo!.RoundId);
    }

    [Fact]
    public void ARefusalCarriesTheCeilingAndAReasonTheCabinetCanSpeak()
    {
        var refused = JsonSerializer.Deserialize("{\"granted\":false,\"reason\":\"ceiling\",\"ceiling\":25000}",
            AethernetJsonContext.Default.CasinoDiceDto)!;
        Assert.False(refused.Granted);
        Assert.Equal(25000, refused.Ceiling);
        Assert.True(CasinoReasons.TryMessage(refused.Reason, out _));
        Assert.True(CasinoReasons.TryMessage(CasinoReasons.InvalidMove, out _));
    }

    [Fact]
    public void ResendingAFailedStakeReusesItsRoundIdForTheSameGameOnly()
    {
        var held = new PendingStake(OriginalsGame.Dice, "s1", "c1", 1000, 5050, true, null);
        Assert.True(CasinoOriginalsStore.ResendsPending(held, OriginalsGame.Dice, "s1"));
        Assert.False(CasinoOriginalsStore.ResendsPending(held, OriginalsGame.Limbo, "s1"));
        Assert.False(CasinoOriginalsStore.ResendsPending(held, OriginalsGame.Dice, "s2"));
        Assert.False(CasinoOriginalsStore.ResendsPending(null, OriginalsGame.Dice, "s1"));
    }
}
