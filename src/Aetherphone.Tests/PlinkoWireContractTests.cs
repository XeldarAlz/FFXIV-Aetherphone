using System.Text.Json;
using Aetherphone.Apps.Casino;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class PlinkoWireContractTests
{
    [Fact]
    public void TheRouteKindAndFeatureMatchTheWireDoc()
    {
        Assert.Equal("/casino/plinko/drop", CasinoClient.PlinkoDropPath);
        Assert.Equal("casino.plinko", CasinoWire.PlinkoKind);
        Assert.Equal(CasinoWire.PlinkoKind, CasinoWire.Kind(CasinoGames.Plinko));
        Assert.Equal("plinko", PlinkoRules.Feature);
    }

    [Fact]
    public void TheRequestSerializesTheBackendShape()
    {
        Assert.Equal("{\"sittingId\":\"s1\",\"clientRoundId\":\"c1\",\"rows\":16,\"risk\":1,\"stake\":1000}",
            JsonSerializer.Serialize(new CasinoPlinkoDropRequest("s1", "c1", 16, 1, 1000),
                AethernetJsonContext.Default.CasinoPlinkoDropRequest));
    }

    [Fact]
    public void ADropReadsEveryWireField()
    {
        const string json = "{\"granted\":true,\"reason\":\"\",\"roundId\":\"r1\",\"rows\":16,\"risk\":1,"
            + "\"stake\":1000,\"path\":[1,0,0,1,1,0,1,0,1,1,0,0,1,0,1,1],\"slot\":9,\"multiplierTenths\":5,"
            + "\"payout\":500,\"nextSeedHash\":\"hex\",\"stack\":150500,\"ceiling\":0}";
        var drop = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoPlinkoDropDto)!;
        Assert.True(drop.Granted);
        Assert.Equal("r1", drop.RoundId);
        Assert.Equal(16, drop.Rows);
        Assert.Equal(1, drop.Risk);
        Assert.Equal(1000, drop.Stake);
        Assert.Equal(16, drop.Path!.Length);
        Assert.Equal(9, drop.Slot);
        Assert.Equal(5, drop.MultiplierTenths);
        Assert.Equal(500, drop.Payout);
        Assert.Equal("hex", drop.NextSeedHash);
        Assert.Equal(150500, drop.Stack);
        Assert.Equal(0, drop.Ceiling);
        Assert.True(PlinkoRules.IsPath(drop.Path, drop.Rows, drop.Slot));
        Assert.Equal(PlinkoRules.MultiplierTenths(16, 1, 9), drop.MultiplierTenths);
    }

    [Fact]
    public void ACeilingRefusalCarriesTheCap()
    {
        const string json = "{\"granted\":false,\"reason\":\"ceiling\",\"roundId\":\"r2\",\"rows\":8,\"risk\":2,"
            + "\"stake\":900000,\"path\":[],\"slot\":0,\"multiplierTenths\":0,\"payout\":0,\"nextSeedHash\":\"\","
            + "\"stack\":5000,\"ceiling\":250000}";
        var drop = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoPlinkoDropDto)!;
        Assert.False(drop.Granted);
        Assert.Equal(CasinoReasons.Ceiling, drop.Reason);
        Assert.Equal(250000, drop.Ceiling);
        Assert.True(CasinoReasons.TryMessage(drop.Reason, out _));
    }

    [Fact]
    public void EveryRefusalTheRouteSendsHasAClientString()
    {
        string[] reasons =
        {
            CasinoReasons.StakeRange, CasinoReasons.Ladder, CasinoReasons.Ceiling, CasinoReasons.Cooldown,
            CasinoReasons.LossLimit, CasinoReasons.Insufficient, CasinoReasons.Expired, CasinoReasons.StakesPaused,
            CasinoReasons.Draining, CasinoReasons.Frozen,
        };
        for (var index = 0; index < reasons.Length; index++)
        {
            Assert.True(CasinoReasons.TryMessage(reasons[index], out _), reasons[index]);
        }
    }

    [Fact]
    public void AnEmptyBodyDefaultsEveryField()
    {
        var drop = JsonSerializer.Deserialize("{}", AethernetJsonContext.Default.CasinoPlinkoDropDto)!;
        Assert.False(drop.Granted);
        Assert.Equal(string.Empty, drop.RoundId);
        Assert.Null(drop.Path);
        Assert.Equal(0, drop.Stack);
    }
}
