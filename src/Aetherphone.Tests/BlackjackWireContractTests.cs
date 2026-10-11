using System.Text.Json;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class BlackjackWireContractTests
{
    [Fact]
    public void TheRoutesStayWhereTheServerServesThem()
    {
        Assert.Equal("/casino/blackjack/bet", CasinoClient.BlackjackBetPath);
        Assert.Equal("/casino/blackjack/wager", CasinoClient.BlackjackWagerPath);
        Assert.Equal("/casino/blackjack/act", CasinoClient.BlackjackActionPath);
        Assert.Equal("/casino/rooms/table-a1/verify/12", CasinoClient.RoomVerifyPath("table-a1", 12));
    }

    [Fact]
    public void TheBetCarriesBothSideBets()
    {
        var request = new CasinoBlackjackBetRequest("blackjack-pit", "round", "action", 2_500, 500, 100);
        var json = JsonSerializer.Serialize(request, AethernetJsonContext.Default.CasinoBlackjackBetRequest);

        Assert.Contains("\"amount\":2500", json, StringComparison.Ordinal);
        Assert.Contains("\"perfectPairs\":500", json, StringComparison.Ordinal);
        Assert.Contains("\"twentyOnePlusThree\":100", json, StringComparison.Ordinal);
    }

    [Fact]
    public void TheRoomStateReadsInsuranceSideBetsAndSurrender()
    {
        const string json = """
        {
          "handId": "hand-9",
          "handIndex": 9,
          "phase": 1,
          "dealerCards": [0, -1],
          "seats": [
            {
              "seatIndex": 2,
              "userId": "u1",
              "displayName": "Mira",
              "chips": 40000,
              "state": 1,
              "connected": true,
              "committed": 3600,
              "hands": [ { "cards": [9, 22], "bet": 2500, "total": 20, "outcome": 6, "delta": -1250, "splitAces": false, "surrendered": true } ],
              "sideBets": { "perfectPairs": 500, "twentyOnePlusThree": 100, "perfectPairsKind": 2, "twentyOnePlusThreeKind": 0, "perfectPairsWin": 6500, "twentyOnePlusThreeWin": 0 },
              "insurance": 1250,
              "insuranceDecided": true,
              "insuranceWin": 3750
            }
          ],
          "minBet": 500,
          "maxBet": 10000,
          "maxWin": 0,
          "insuranceOpen": true,
          "sideBetMin": 100,
          "insurance": true,
          "surrender": true
        }
        """;

        var board = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoBlackjackRoomStateDto)!;
        var seat = board.Seats![0];

        Assert.True(board.InsuranceOpen);
        Assert.Equal(100, board.SideBetMin);
        Assert.True(board.Insurance);
        Assert.True(board.Surrender);
        Assert.Equal(1_250, seat.Insurance);
        Assert.True(seat.InsuranceDecided);
        Assert.Equal(3_750, seat.InsuranceWin);
        Assert.Equal(500, seat.SideBets!.PerfectPairs);
        Assert.Equal(BlackjackSideBets.PairColoured, seat.SideBets.PerfectPairsKind);
        Assert.Equal(6_500, seat.SideBets.PerfectPairsWin);
        Assert.True(seat.Hands![0].Surrendered);
        Assert.Equal(BlackjackOutcomes.Surrender, seat.Hands[0].Outcome);
    }

    [Fact]
    public void AnOldServerStillReadsWithEveryNewFieldDefaulted()
    {
        const string json = """{ "handId": "h", "seats": [ { "seatIndex": 0, "hands": [ { "bet": 500 } ] } ] }""";

        var board = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoBlackjackRoomStateDto)!;

        Assert.False(board.InsuranceOpen);
        Assert.Equal(0, board.SideBetMin);
        Assert.Null(board.Seats![0].SideBets);
        Assert.False(board.Seats[0].Hands![0].Surrendered);
    }

    [Fact]
    public void AHouseRuleShoeVerifiesAtItsOwnSize()
    {
        Assert.Equal(52, CasinoVerifier.ShoeCardsOf(ShuffleLog(51)));
        Assert.Equal(312, CasinoVerifier.ShoeCardsOf(ShuffleLog(311)));
        Assert.Equal(416, CasinoVerifier.ShoeCardsOf(ShuffleLog(415)));
        Assert.Equal(BlackjackRules.ShoeCards, CasinoVerifier.ShoeCardsOf(ShuffleLog(17)));
    }

    [Fact]
    public void TableHandsKeepTheNewestTwenty()
    {
        var held = Array.Empty<CasinoTableHand>();
        for (var index = 0; index < CasinoHistoryStore.TableHandLimit + 5; index++)
        {
            held = CasinoHistoryStore.Prepend(held, new CasinoTableHand(
                CasinoHistoryStore.TableHandKey("table-a1", index), "table-a1", index, "Rose Room", 1, 0, index));
        }

        Assert.Equal(CasinoHistoryStore.TableHandLimit, held.Length);
        Assert.Equal(CasinoHistoryStore.TableHandLimit + 4, held[0].HandIndex);
        Assert.Equal("table-a1#24", held[0].Key);
    }

    private static string ShuffleLog(int draws)
    {
        var builder = new System.Text.StringBuilder();
        for (var index = 0; index < draws; index++)
        {
            if (index > 0)
            {
                builder.Append(';');
            }

            builder.Append("shuffle:0");
        }

        return builder.ToString();
    }
}
