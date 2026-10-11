using System.Text.Json;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoHostingWireContractTests
{
    [Fact]
    public void TheHostPanelRoutesMatchTheServer()
    {
        Assert.Equal("/casino/tables/table-a1/rename", CasinoClient.TablePath("table-a1", CasinoClient.RenameLeaf));
        Assert.Equal("/casino/tables/table-a1/codealers",
            CasinoClient.TablePath("table-a1", CasinoClient.CoDealersLeaf));
        Assert.Equal("/casino/tables/table-a1/pause", CasinoClient.TablePath("table-a1", CasinoClient.PauseLeaf));
        Assert.Equal("/casino/tables/table-a1/deal", CasinoClient.TablePath("table-a1", CasinoClient.DealLeaf));
        Assert.Equal("/casino/tables/table-a1/tournament",
            CasinoClient.TablePath("table-a1", CasinoClient.TournamentLeaf));
        Assert.Equal("/casino/tables/table-a1/tournament/stop",
            CasinoClient.TablePath("table-a1", CasinoClient.TournamentStopLeaf));
        Assert.Equal("/casino/tables/table-a1/ledger", CasinoClient.TablePath("table-a1", CasinoClient.LedgerLeaf));
        Assert.Equal("/casino/tables/table-a1/close", CasinoClient.TablePath("table-a1", CasinoClient.CloseLeaf));
        Assert.Equal("/casino/ledger/e1/confirm", CasinoClient.LedgerEntryPath("e1", "confirm"));
        Assert.Equal("/casino/ledger/e1/dispute", CasinoClient.LedgerEntryPath("e1", "dispute"));
        Assert.Equal("/casino/ledger", CasinoClient.LedgerPath);
        Assert.Equal("/casino/blackjack/rebuy", CasinoClient.BlackjackRebuyPath);
        Assert.Equal("/casino/venue/venue-x/act", CasinoClient.VenueActPath("venue-x"));
        Assert.Equal("/casino/rooms/table-a1/verify/12", CasinoClient.RoomVerifyPath("table-a1", 12));
        Assert.Equal("/casino/tables/nearby?world=73&territory=339&ward=12", CasinoClient.NearbyPath(73, 339, 12));
    }

    [Fact]
    public void TheCreateBodyCarriesTheWholeConfigInCamelCase()
    {
        var config = new CasinoTableConfigDto(Name: "Rose Room", Seats: 4, MaxBet: 500000, Currency: CasinoCurrencies.Gil,
            Bank: 20000000, MaxPayout: 5000000, DealerMode: CasinoDealerModes.Host, CoDealers: new[] { "u2" },
            AutoDeal: false, Listing: CasinoListings.Open,
            HouseRules: new CasinoBlackjackRuleSheetDto(BlackjackPays: CasinoRuleSheet.PaysTwoToOne, Decks: 2));
        var body = JsonSerializer.Serialize(new CasinoTableCreateRequest("a1b2", 0, config),
            AethernetJsonContext.Default.CasinoTableCreateRequest);

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.Equal("a1b2", root.GetProperty("clientTableId").GetString());
        var sent = root.GetProperty("config");
        Assert.Equal("casino.blackjack", sent.GetProperty("gameKind").GetString());
        Assert.Equal("Rose Room", sent.GetProperty("name").GetString());
        Assert.Equal(4, sent.GetProperty("seats").GetInt32());
        Assert.Equal(2, sent.GetProperty("currency").GetInt32());
        Assert.Equal(20000000, sent.GetProperty("bank").GetInt64());
        Assert.Equal(5000000, sent.GetProperty("maxPayout").GetInt64());
        Assert.Equal(1, sent.GetProperty("dealerMode").GetInt32());
        Assert.False(sent.GetProperty("autoDeal").GetBoolean());
        Assert.Equal("u2", sent.GetProperty("coDealers")[0].GetString());
        Assert.Equal(2, sent.GetProperty("listing").GetInt32());
        Assert.Equal(1, sent.GetProperty("houseRules").GetProperty("blackjackPays").GetInt32());
        Assert.Equal(2, sent.GetProperty("houseRules").GetProperty("decks").GetInt32());
        Assert.True(sent.GetProperty("houseRules").GetProperty("dealerPeek").GetBoolean());
        Assert.Equal(3, sent.GetProperty("timeBankUses").GetInt32());
        Assert.Equal(20, sent.GetProperty("turnSeconds").GetInt32());
        Assert.True(sent.GetProperty("spectators").GetBoolean());
    }

    [Fact]
    public void AHostedCardReadsItsCurrencyConfigAndReputation()
    {
        const string json = """
        {
          "tables": [
            {
              "tableId": "table-a1b2", "gameKind": "casino.blackjack", "kind": 2, "stakeTier": 0,
              "ownerUserId": "u-host", "ownerName": "Rhan", "minBet": 1, "maxBet": 500000, "minBuyIn": 0,
              "maxBuyIn": 0, "maxSeats": 4, "seatedCount": 2, "occupancy": 5, "admitted": true, "reason": "",
              "inviteToken": "[aep.casino.v1:table-a1b2]", "name": "Rose Room", "listing": 2, "practice": false,
              "paused": false,
              "config": { "gameKind": "casino.blackjack", "name": "Rose Room", "seats": 4, "currency": 2,
                          "bank": 20000000, "maxPayout": 5000000, "maxBet": 500000, "minBet": 1,
                          "houseRules": { "blackjackPays": 0, "dealerHitsSoft17": true, "decks": 6, "splits": 2,
                                          "doubles": 1, "fiveCardCharlie": false, "dealerPeek": true } },
              "currency": 2,
              "reputation": { "gilTablesHosted": 3, "payoutsConfirmed": 12, "disputesOpen": 1, "frozen": false }
            }
          ],
          "serverNowUnixMs": 0
        }
        """;

        var directory = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoTableListDto);
        var card = Assert.Single(directory!.Tables!);

        Assert.Equal("Rose Room", card.Name);
        Assert.Equal(CasinoListings.Open, card.Listing);
        Assert.Equal(CasinoCurrencies.Gil, CasinoCurrencies.Of(card));
        Assert.Equal(20000000, card.Config!.Bank);
        Assert.True(card.Config.HouseRules!.DealerHitsSoft17);
        Assert.Equal(CasinoRuleSheet.DoublesNineToEleven, card.Config.HouseRules.Doubles);
        Assert.Equal(12, card.Reputation!.PayoutsConfirmed);
        Assert.Equal(1, card.Reputation.DisputesOpen);
    }

    [Fact]
    public void APracticeFlagWithoutACurrencyStillReadsAsPractice()
    {
        var card = new CasinoTableRowDto(Practice: true);
        var house = new CasinoTableRowDto();

        Assert.Equal(CasinoCurrencies.Practice, CasinoCurrencies.Of(card));
        Assert.Equal(CasinoCurrencies.Chips, CasinoCurrencies.Of(house));
    }

    [Fact]
    public void TheBlackjackRoomCarriesTheHostingTail()
    {
        const string json = """
        {
          "handId": "h1", "handIndex": 3, "phase": 0, "seats": [ { "seatIndex": 0, "userId": "u1", "displayName": "Mira",
          "chips": 90000, "state": 1, "connected": true, "hands": [], "timeBankLeft": 2 } ],
          "minBet": 500, "maxBet": 10000, "practice": true, "paused": true, "dealerMode": 1, "dealerUserId": "u-host",
          "dealerName": "Rhan", "coDealers": ["u2"], "autoDeal": false, "faceUp": true, "practiceRebuy": true,
          "practiceStack": 100000,
          "rules": { "blackjackPays": 2, "dealerHitsSoft17": true, "decks": 1, "splits": 0, "doubles": 2,
                     "fiveCardCharlie": true, "dealerPeek": false },
          "tournament": { "live": true, "hands": 20, "handsPlayed": 3, "stack": 100000, "winnerUserId": "",
                          "winnerName": "", "standings": [ { "userId": "u1", "displayName": "Mira", "chips": 120000,
                          "eliminated": false, "place": 1 } ] },
          "name": "Mira's table", "turnSeconds": 30, "currency": 1, "bank": 0, "bankHeadroom": 0, "maxPayout": 0
        }
        """;

        var board = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoBlackjackRoomStateDto);

        Assert.NotNull(board);
        Assert.Equal(CasinoCurrencies.Practice, CasinoCurrencies.Of(board!));
        Assert.True(board.Paused);
        Assert.Equal(CasinoDealerModes.Host, board.DealerMode);
        Assert.Equal("Rhan", board.DealerName);
        Assert.False(board.AutoDeal);
        Assert.Equal(2, board.Seats![0].TimeBankLeft);
        Assert.Equal(CasinoRuleSheet.PaysEven, board.Rules!.BlackjackPays);
        Assert.True(board.Rules.FiveCardCharlie);
        Assert.Equal(1, board.Tournament!.Standings![0].Place);
        Assert.Equal(30, board.TurnSeconds);
    }

    [Fact]
    public void TheGilLedgerReadsBothSidesOfEveryEntry()
    {
        const string json = """
        {
          "tableId": "table-a1b2", "owner": true, "practice": false,
          "rows": [ { "userId": "u1", "displayName": "Mira", "seatIndex": 0, "buyIns": 500000, "stack": 650000,
                      "net": 150000, "hands": 12, "seated": true } ],
          "serverNowUnixMs": 0, "currency": 2,
          "entries": [ { "entryId": "e1", "tableId": "table-a1b2", "kind": "buyin", "amount": 500000,
                         "payerUserId": "u1", "payerName": "Mira", "payeeUserId": "u-host", "payeeName": "Rhan",
                         "proposedBy": "player", "source": "manual", "payerConfirmed": true, "payeeConfirmed": false,
                         "settled": false, "disputed": false, "disputeResolved": false, "createdAtUnixMs": 1,
                         "settledAtUnixMs": 0 } ]
        }
        """;

        var ledger = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoTableLedgerDto);

        Assert.NotNull(ledger);
        Assert.Equal(CasinoCurrencies.Gil, ledger!.Currency);
        var entry = Assert.Single(ledger.Entries!);
        Assert.Equal(CasinoLedgerKinds.BuyIn, entry.Kind);
        Assert.True(entry.PayerConfirmed);
        Assert.False(entry.PayeeConfirmed);
        Assert.Equal(150000, ledger.Rows![0].Net);

        var propose = JsonSerializer.Serialize(new CasinoLedgerProposeRequest("x1", CasinoLedgerKinds.Payout, "u1",
            250000), AethernetJsonContext.Default.CasinoLedgerProposeRequest);
        Assert.Equal(
            "{\"clientEntryId\":\"x1\",\"kind\":\"payout\",\"counterpartyUserId\":\"u1\",\"amount\":250000,\"source\":\"manual\"}",
            propose);
    }

    [Fact]
    public void TheHostPanelBodiesAreTheShapesTheServerReads()
    {
        Assert.Equal("{\"name\":\"Rose\"}",
            JsonSerializer.Serialize(new CasinoTableRenameRequest("Rose"),
                AethernetJsonContext.Default.CasinoTableRenameRequest));
        Assert.Equal("{\"paused\":true}",
            JsonSerializer.Serialize(new CasinoTablePauseRequest(true),
                AethernetJsonContext.Default.CasinoTablePauseRequest));
        Assert.Equal("{\"userIds\":[\"u2\",\"u3\"]}",
            JsonSerializer.Serialize(new CasinoTableCoDealersRequest(new[] { "u2", "u3" }),
                AethernetJsonContext.Default.CasinoTableCoDealersRequest));
        Assert.Equal("{\"hands\":20,\"stack\":0}",
            JsonSerializer.Serialize(new CasinoTableTournamentRequest(),
                AethernetJsonContext.Default.CasinoTableTournamentRequest));
        Assert.Equal("{\"roomId\":\"table-a1\"}",
            JsonSerializer.Serialize(new CasinoBlackjackRebuyRequest("table-a1"),
                AethernetJsonContext.Default.CasinoBlackjackRebuyRequest));
    }

    [Fact]
    public void VenueRoomsReadTheirGameStates()
    {
        const string dice = """
        { "name": "Mira's dice", "sides": 1000, "highestWins": true, "roundSeconds": 60, "lastSeq": 12,
          "nextCommit": "ab", "rolls": [ { "seq": 12, "userId": "u1", "displayName": "Mira", "bound": 1000,
          "value": 777, "atUnixMs": 0, "seed": "cd" } ], "round": { "index": 1, "openedSeq": 10, "endsAtUnixMs": 0,
          "winnerUserId": "", "winnerName": "", "winningValue": 0, "closed": false, "rollers": ["u1"] },
          "lastRound": null, "currency": 1 }
        """;
        var table = JsonSerializer.Deserialize(dice, AethernetJsonContext.Default.CasinoDiceTableStateDto);
        Assert.Equal(777, table!.Rolls![0].Value);
        Assert.Equal("u1", table.Round!.Rollers![0]);

        const string raffle = """
        { "name": "", "lastSeq": 30, "nextCommit": "", "raffle": null,
          "last": { "seq": 2, "title": "Bar tab", "ticketsPerPerson": 3, "winners": 1, "endsAtUnixMs": 0, "tickets": 14,
          "entrants": [ { "userId": "u1", "displayName": "Mira", "tickets": 3 } ], "drawn": true, "drawSeq": 30,
          "seed": "ef", "winnersDrawn": [ { "userId": "u1", "displayName": "Mira", "tickets": 3 } ] }, "currency": 2 }
        """;
        var room = JsonSerializer.Deserialize(raffle, AethernetJsonContext.Default.CasinoRaffleStateDto);
        Assert.True(room!.Last!.Drawn);
        Assert.Equal("Mira", room.Last.WinnersDrawn![0].DisplayName);

        const string deathroll = """
        { "name": "", "startAt": 1000, "stake": 10000, "practiceStack": 100000, "lastSeq": 7, "nextCommit": "",
          "duel": { "seq": 3, "challengerUserId": "u1", "challengerName": "Mira", "opponentUserId": "u2",
          "opponentName": "Rhan", "stake": 10000, "phase": 1, "turnUserId": "u1", "current": 412,
          "turnEndsAtUnixMs": 0, "rolls": [], "loserUserId": "" }, "recent": [],
          "stacks": [ { "userId": "u1", "displayName": "Mira", "chips": 110000 } ], "currency": 1 }
        """;
        var duel = JsonSerializer.Deserialize(deathroll, AethernetJsonContext.Default.CasinoDeathrollStateDto);
        Assert.Equal(412, duel!.Duel!.Current);
        Assert.Equal(110000, duel.Stacks![0].Chips);

        var act = JsonSerializer.Deserialize(
            "{\"granted\":true,\"reason\":\"\",\"roomId\":\"venue-x\",\"seq\":42,\"value\":777,\"seed\":\"aa\",\"nextCommit\":\"bb\"}",
            AethernetJsonContext.Default.CasinoVenueActDto);
        Assert.Equal(42, act!.Seq);
    }

    [Fact]
    public void SnapshotsAndRoomRowsCarryThePracticeFlag()
    {
        var snapshot = JsonSerializer.Deserialize("{\"roomId\":\"t\",\"practice\":true}",
            AethernetJsonContext.Default.CasinoRoomSnapshotDto);
        var row = JsonSerializer.Deserialize("{\"roomId\":\"t\",\"practice\":true}",
            AethernetJsonContext.Default.CasinoRoomListItemDto);

        Assert.True(snapshot!.Practice);
        Assert.True(row!.Practice);
    }
}
