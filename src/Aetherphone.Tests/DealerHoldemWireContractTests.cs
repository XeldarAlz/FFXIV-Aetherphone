using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aetherphone.Apps.Casino;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class DealerHoldemWireContractTests
{
    [Fact]
    public void TheRoutesKindAndFeatureMatchTheWireDoc()
    {
        Assert.Equal("/casino/dealerholdem/start", CasinoClient.DealerHoldemStartPath);
        Assert.Equal("/casino/dealerholdem/decide", CasinoClient.DealerHoldemDecidePath);
        Assert.Equal("/casino/dealerholdem/open", CasinoClient.DealerHoldemOpenPath);
        Assert.Equal("casino.dealerholdem", DealerHoldemRules.Kind);
        Assert.Equal(DealerHoldemRules.Kind, CasinoWire.Kind(CasinoGames.DealerHoldem));
        Assert.Equal("dealer.holdem", DealerHoldemRules.Feature);
        Assert.Equal(DealerHoldemRules.Feature, CasinoGameGate.FlagFor(CasinoGames.DealerHoldem));
    }

    [Fact]
    public void TheRequestsSerializeTheBackendShape()
    {
        Assert.Equal("{\"sittingId\":\"s1\",\"clientRoundId\":\"r-123\",\"ante\":1000,\"trips\":0}",
            JsonSerializer.Serialize(new CasinoDealerHoldemStartRequest("s1", "r-123", 1000, 0),
                AethernetJsonContext.Default.CasinoDealerHoldemStartRequest));
        Assert.Equal("{\"roundId\":\"r-123\",\"step\":0,\"action\":\"bet\",\"multiple\":4}",
            JsonSerializer.Serialize(new CasinoDealerHoldemDecideRequest("r-123", 0, "bet", 4),
                AethernetJsonContext.Default.CasinoDealerHoldemDecideRequest));
    }

    [Fact]
    public void TheFlopExampleReadsEveryWireField()
    {
        const string json = "{ \"granted\": true, \"reason\": \"\", \"roundId\": \"r-123\", \"ante\": 1000, "
            + "\"blind\": 1000, \"trips\": 1000, \"play\": 0, \"playMultiple\": 0, \"stake\": 3000, \"phase\": 1, "
            + "\"step\": 1, \"actions\": [\"check\", \"bet\"], \"multiples\": [2], \"playerCards\": [0, 13], "
            + "\"board\": [26, 5, 44], \"dealerCards\": [], \"playerHand\": 1101824, \"dealerHand\": -1, "
            + "\"playerBest\": [], \"dealerBest\": [], \"dealerQualifies\": false, \"folded\": false, "
            + "\"outcome\": 0, \"antePayout\": 0, \"blindPayout\": 0, \"playPayout\": 0, \"tripsPayout\": 0, "
            + "\"payout\": 0, \"capped\": false, \"nextSeedHash\": \"ab12\", \"stack\": 1016250, \"ceiling\": 0 }";
        var round = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoDealerHoldemDto)!;
        Assert.True(round.Granted);
        Assert.Equal("r-123", round.RoundId);
        Assert.Equal(1000, round.Ante);
        Assert.Equal(1000, round.Blind);
        Assert.Equal(1000, round.Trips);
        Assert.Equal(3000, round.Stake);
        Assert.Equal(DealerHoldemRules.PhaseFlop, round.Phase);
        Assert.Equal(1, round.Step);
        Assert.Equal(new[] { "check", "bet" }, round.Actions);
        Assert.Equal(new[] { 2 }, round.Multiples);
        Assert.Equal(new[] { 0, 13 }, round.PlayerCards);
        Assert.Equal(DealerHoldemRules.BoardShown(round.Phase), round.Board!.Length);
        Assert.Empty(round.DealerCards!);
        Assert.Equal(HoldemHands.Pair, HoldemHands.CategoryOf(round.PlayerHand));
        Assert.Equal(-1, round.DealerHand);
        Assert.Equal(DealerHoldemRules.OutcomePending, round.Outcome);
        Assert.Equal("ab12", round.NextSeedHash);
        Assert.Equal(1016250, round.Stack);
        Assert.False(round.Capped);
    }

    [Fact]
    public void ASettledHandReadsThePerBetPayouts()
    {
        const string json = "{\"granted\":true,\"roundId\":\"r9\",\"ante\":100,\"blind\":100,\"trips\":100,"
            + "\"play\":400,\"playMultiple\":4,\"stake\":700,\"phase\":3,\"dealerCards\":[1,2],"
            + "\"dealerHand\":1048576,\"playerBest\":[0,13,26,5,44],\"dealerBest\":[1,2,26,5,44],"
            + "\"dealerQualifies\":true,\"outcome\":1,\"antePayout\":200,\"blindPayout\":100,\"playPayout\":800,"
            + "\"tripsPayout\":400,\"payout\":1500,\"capped\":true}";
        var round = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoDealerHoldemDto)!;
        Assert.Equal(DealerHoldemRules.PhaseSettled, round.Phase);
        Assert.True(round.DealerQualifies);
        Assert.Equal(DealerHoldemRules.OutcomeWin, round.Outcome);
        Assert.Equal(200, round.AntePayout);
        Assert.Equal(100, round.BlindPayout);
        Assert.Equal(800, round.PlayPayout);
        Assert.Equal(400, round.TripsPayout);
        Assert.Equal(1500, round.Payout);
        Assert.True(round.Capped);
        Assert.Equal(5, round.PlayerBest!.Length);
        Assert.Equal(-1, new CasinoDealerHoldemDto().PlayerHand);
    }

    [Fact]
    public void TheOpenReadCarriesARoundOrNull()
    {
        var empty = JsonSerializer.Deserialize("{\"round\":null}",
            AethernetJsonContext.Default.CasinoDealerHoldemOpenDto)!;
        Assert.Null(empty.Round);
        var live = JsonSerializer.Deserialize("{\"round\":{\"granted\":true,\"roundId\":\"r1\",\"phase\":2}}",
            AethernetJsonContext.Default.CasinoDealerHoldemOpenDto)!;
        Assert.Equal("r1", live.Round!.RoundId);
        Assert.Equal(DealerHoldemRules.PhaseRiver, live.Round.Phase);
    }

    [Fact]
    public void EveryRefusalTheRoutesSendHasAClientString()
    {
        string[] reasons =
        {
            CasinoReasons.StakeRange, CasinoReasons.Ladder, CasinoReasons.Ceiling, CasinoReasons.Insufficient,
            CasinoReasons.Cooldown, CasinoReasons.LossLimit, CasinoReasons.StakesPaused, CasinoReasons.Draining,
            CasinoReasons.Frozen, CasinoReasons.Expired, CasinoReasons.InvalidMove, CasinoReasons.RoundOpen,
        };
        for (var index = 0; index < reasons.Length; index++)
        {
            Assert.True(CasinoReasons.TryMessage(reasons[index], out _), reasons[index]);
        }
    }

    [Fact]
    public void TheVerifierReplaysTheHoldemShuffleForThisKind()
    {
        var seed = SHA256.HashData(Encoding.UTF8.GetBytes("dealer-holdem-seed"));
        const string roundId = "r-verify";
        var stream = new CasinoVerifier.DrawStream(seed, roundId);
        var log = new StringBuilder();
        for (var top = 51; top >= 1; top--)
        {
            if (log.Length > 0)
            {
                log.Append(';');
            }

            log.Append("shuffle:").Append(stream.NextBelow((uint)(top + 1)));
        }

        var seedHex = Convert.ToHexStringLower(seed);
        var commit = Convert.ToHexStringLower(SHA256.HashData(seed));
        Assert.Equal(CasinoRoundVerdict.Match,
            CasinoVerifier.Verify(DealerHoldemRules.Kind, seedHex, commit, roundId, log.ToString()));
        var tampered = log.ToString().Replace("shuffle:", "shuffle:9", StringComparison.Ordinal);
        Assert.Equal(CasinoRoundVerdict.Mismatch,
            CasinoVerifier.Verify(DealerHoldemRules.Kind, seedHex, commit, roundId, tampered));
    }
}
