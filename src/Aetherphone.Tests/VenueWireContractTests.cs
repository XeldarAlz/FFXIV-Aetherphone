using System.Text.Json;
using Aetherphone.Apps.Casino.Venue;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Venues;
using Xunit;

namespace Aetherphone.Tests;

public sealed class VenueWireContractTests
{
    [Fact]
    public void TheVenueRoutesMatchTheWire()
    {
        Assert.Equal("/casino/venue/venue-x/act", CasinoClient.VenueActPath("venue-x"));
        Assert.Equal("/casino/rooms/venue-x/verify/12", CasinoClient.RoomVerifyPath("venue-x", 12));
        Assert.Equal("/casino/tables/nearby?world=74&territory=339&ward=5", CasinoClient.NearbyPath(74, 339, 5));
        Assert.Equal("/casino/tables/table-a1/tournament", CasinoClient.TablePath("table-a1", CasinoClient.TournamentLeaf));
        Assert.Equal("casino_table", CasinoVenueStore.ReportTarget);
    }

    [Fact]
    public void AnActSendsEveryFieldInCamelCase()
    {
        var json = JsonSerializer.Serialize(new CasinoVenueActRequest("x1", VenueActions.RaffleOpen, 3, "Bar tab", 1, 600,
            string.Empty, 500, 1_000_000), AethernetJsonContext.Default.CasinoVenueActRequest);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal("x1", root.GetProperty("clientActionId").GetString());
        Assert.Equal("raffle.open", root.GetProperty("action").GetString());
        Assert.Equal(3, root.GetProperty("count").GetInt32());
        Assert.Equal(600, root.GetProperty("durationSeconds").GetInt32());
        Assert.Equal(500, root.GetProperty("ticketPrice").GetInt64());
        Assert.Equal(1_000_000, root.GetProperty("prize").GetInt64());
        Assert.True(root.TryGetProperty("opponentUserId", out _));
    }

    [Fact]
    public void ATradeProposalCarriesTheTradeSource()
    {
        var json = JsonSerializer.Serialize(new CasinoLedgerProposeRequest("c1", CasinoLedgerKinds.BuyIn, "u-host",
            500_000, CasinoLedgerKinds.Trade), AethernetJsonContext.Default.CasinoLedgerProposeRequest);
        Assert.Contains("\"source\":\"trade\"", json, StringComparison.Ordinal);
        Assert.Contains("\"kind\":\"buyin\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ALocationFromTheHousingPositionIsDroppedOutsideAWard()
    {
        Assert.Null(new CasinoHousingPosition(74, 339, 0, 0, 0, 0).ToLocation());
        var location = new CasinoHousingPosition(74, 339, 5, 12, 0, 0).ToLocation();
        Assert.NotNull(location);
        Assert.Equal(12, location!.Plot);
        Assert.True(new CasinoHousingPosition(74, 339, 5, 3, 0, 0).SameWard(location));
        Assert.False(new CasinoHousingPosition(74, 339, 6, 12, 0, 0).SameWard(location));
    }

    [Fact]
    public void EachVenueRoomParsesItsGameState()
    {
        Assert.Equal(VenueRoomKind.Dice, ViewOf(VenueKinds.DiceTable,
            "{\"name\":\"Mira's dice\",\"sides\":20,\"highestWins\":true,\"lastSeq\":3,\"rolls\":[{\"seq\":3,\"userId\":\"u1\",\"displayName\":\"Mira\",\"bound\":20,\"value\":17}],\"currency\":2}").Kind);
        var dice = ViewOf(VenueKinds.DiceTable, "{\"sides\":20,\"currency\":2,\"rolls\":[{\"seq\":3,\"value\":17}]}");
        Assert.True(dice.Gil);
        Assert.Equal(17, dice.Dice!.Rolls![0].Value);
        var duel = ViewOf(VenueKinds.Deathroll,
            "{\"startAt\":1000,\"duel\":{\"seq\":3,\"phase\":1,\"turnUserId\":\"u1\",\"current\":412},\"currency\":1}");
        Assert.Equal(412, duel.Deathroll!.Duel!.Current);
        var raffle = ViewOf(VenueKinds.Raffle,
            "{\"raffle\":{\"seq\":2,\"title\":\"Bar tab\",\"ticketsPerPerson\":3,\"entrants\":[{\"userId\":\"u1\",\"tickets\":2}]}}");
        Assert.Equal("Bar tab", raffle.Raffle!.Raffle!.Title);
        Assert.Equal(CasinoCurrencies.Practice, raffle.Currency);
    }

    [Fact]
    public void ABlackjackRoomIsNotAVenueView()
    {
        var snapshot = new CasinoRoomSnapshotDto(RoomId: "t", GameKind: CasinoWire.BlackjackKind);
        Assert.Null(VenueRoomView.From(CasinoRoomSession.Build("t", 1, 1, snapshot)));
    }

    [Fact]
    public void ListedTablesIndexByTheVenueAddress()
    {
        var located = new CasinoTableRowDto(TableId: "t1", Listing: CasinoListings.Open, MaxSeats: 6, SeatedCount: 4,
            Config: new CasinoTableConfigDto(Location: new CasinoTableLocationDto(74, 340, 5, 12)));
        var hidden = located with { TableId = "t2", Listing = CasinoListings.Private };
        var index = CasinoVenueStore.IndexByAddress(new[] { hidden, located }, _ => "Gilgamesh");
        var address = VenueAddress.Of("Gilgamesh", "The Lavender Beds", 5, 12);
        Assert.True(index.TryGetValue(address, out var row));
        Assert.Equal("t1", row!.TableId);
        Assert.Single(index);
    }

    [Fact]
    public void TheVenueFilterShowsRoomsOnTheRail()
    {
        var room = new CasinoTableRowDto(GameKind: VenueKinds.Raffle);
        Assert.True(CasinoTableFilters.Shown(room));
        Assert.True(CasinoTableFilters.Matches(CasinoTableFilter.Rooms, room, string.Empty));
        Assert.False(CasinoTableFilters.Matches(CasinoTableFilter.Rooms,
            new CasinoTableRowDto(GameKind: CasinoWire.BlackjackKind), string.Empty));
    }

    private static VenueRoomView ViewOf(string kind, string gameState)
    {
        var snapshot = new CasinoRoomSnapshotDto(RoomId: "venue-x", GameKind: kind, GameState: gameState);
        var view = VenueRoomView.From(CasinoRoomSession.Build("venue-x", 1, 1, snapshot));
        Assert.NotNull(view);
        Assert.True(view!.Ready);
        return view;
    }
}

public sealed class VenueBroadcastTests
{
    [Fact]
    public void ABlackjackSnapshotProjectsSeatedPlayersOnly()
    {
        var board = new CasinoBlackjackRoomStateDto(HandId: "h1", ActiveSeat: 2, DealerCards: new[] { 12 }, DealerTotal: 10,
            Seats: new[]
            {
                new CasinoBlackjackSeatDto(0, "u1", "Mira", 120_000,
                    Hands: new[] { new CasinoBlackjackHandDto(new[] { 1, 14 }, 5_000, 13) }),
                new CasinoBlackjackSeatDto(1),
                new CasinoBlackjackSeatDto(2, "u2", "Rhan", 80_000),
            });
        var table = BroadcastProjection.FromBlackjack(board);
        Assert.Equal(2, table.Seats.Length);
        Assert.Equal(5_000, table.Seats[0].Bet);
        Assert.Equal(13, table.Seats[0].Total);
        Assert.True(table.Seats[1].Acting);
        Assert.Equal(10, table.DealerTotal);
        Assert.False(table.Waiting);
    }

    [Fact]
    public void AHoldemSnapshotProjectsBoardPotAndHiddenCards()
    {
        const string state = "{\"handId\":\"h\",\"phase\":2,\"cursorSeat\":4,\"potTotal\":2200,\"board\":[12,25,38]," +
            "\"seats\":[{\"seatIndex\":0,\"displayName\":\"Mira\",\"stack\":9400,\"bet\":200,\"state\":1,\"cards\":[-1,-1]}," +
            "{\"seatIndex\":2,\"state\":0},{\"seatIndex\":4,\"displayName\":\"Rhan\",\"stack\":5000,\"state\":2,\"cards\":[]}]}";
        var table = BroadcastProjection.FromHoldem(state);
        Assert.Equal(BroadcastGame.Holdem, table.Game);
        Assert.Equal(new[] { 12, 25, 38 }, table.Board);
        Assert.Equal(2_200, table.Pot);
        Assert.Equal(2, table.Seats.Length);
        Assert.Equal(new[] { BroadcastProjection.HiddenCard, BroadcastProjection.HiddenCard }, table.Seats[0].Cards);
        Assert.True(table.Seats[1].Acting);
        Assert.True(table.Seats[1].Out);
    }

    [Fact]
    public void AnUnreadableHoldemStateIsEmpty()
    {
        Assert.Same(BroadcastTable.Empty, BroadcastProjection.FromHoldem("{not json"));
    }
}

public sealed class TournamentPlaybackTests
{
    private static CasinoBlackjackTournamentDto Tournament(bool live, int played, string winner, params CasinoBlackjackStandingDto[] standings) =>
        new(live, 20, played, 100_000, winner, winner.Length > 0 ? "Mira" : string.Empty, standings);

    [Fact]
    public void JoiningMidTournamentDoesNotReplayOldEliminations()
    {
        var playback = new TournamentPlayback();
        playback.Update(Tournament(true, 5, string.Empty, new CasinoBlackjackStandingDto("u2", "Rhan", 0, true, 2)), 0.016f,
            false);
        Assert.Equal(TournamentEvent.None, playback.TakeEvent());
    }

    [Fact]
    public void ANewEliminationAndTheWinnerAreAnnounced()
    {
        var playback = new TournamentPlayback();
        var alive = new CasinoBlackjackStandingDto("u2", "Rhan", 10_000, false, 2);
        playback.Update(Tournament(true, 5, string.Empty, alive), 0.016f, false);
        playback.Update(Tournament(true, 6, string.Empty, alive with { Chips = 0, Eliminated = true }), 0.016f, false);
        Assert.Equal(TournamentEvent.Eliminated, playback.TakeEvent());
        Assert.Equal("Rhan", playback.NoticeName);
        playback.Update(Tournament(false, 6, "u1"), 0.016f, false);
        Assert.Equal(TournamentEvent.Won, playback.TakeEvent());
        Assert.Equal("u1", playback.WinnerUserId);
    }

    [Fact]
    public void TournamentHandsClampToTheServerBand()
    {
        Assert.Equal(10, TournamentDoorCard.HandsFrom("3"));
        Assert.Equal(50, TournamentDoorCard.HandsFrom("99"));
        Assert.Equal(20, TournamentDoorCard.HandsFrom(string.Empty));
    }
}
