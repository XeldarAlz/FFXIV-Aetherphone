using Aetherphone.Apps.Casino;
using Aetherphone.Apps.Casino.Strip;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoLiveBoardTests
{
    private static readonly HashSet<string> NoFriends = new();

    private static readonly CasinoFeatureSet EveryFeature = CasinoFeatureSet.From(new[]
    {
        CasinoFeatures.Race, CasinoFeatures.Holdem, CasinoFeatures.Venue, CasinoFeatures.Machines,
    });

    [Fact]
    public void HouseBlackjackTablesListAsOneRowBesideEveryOtherTable()
    {
        var rows = new LiveRow[LiveBoard.Capacity];
        var house = new[]
        {
            Table("pit", CasinoTableKinds.House, seated: 1),
            Table("vault", CasinoTableKinds.House, seated: 3),
        };
        var listed = new[]
        {
            Table("pit", CasinoTableKinds.House, seated: 1),
            Table("vault", CasinoTableKinds.House, seated: 3),
            Table("mine", CasinoTableKinds.Private),
        };
        var holdem = new[] { Table("holdem-1", CasinoTableKinds.House, gameKind: HoldemRules.Kind) };
        var count = LiveBoard.Collect(Rooms(), house, listed, holdem, EveryFeature, rows);
        var shown = rows.Take(count).ToArray();
        Assert.Equal(6, count);
        Assert.Equal(LiveRowKind.Room, shown[0].Kind);
        Assert.Equal(CasinoGames.Wheel, shown[0].GameId);
        var houseBlackjack = Assert.Single(shown, row => row.Table is { } table && HouseBlackjack.Is(table));
        Assert.Equal("vault", houseBlackjack.RoomId);
        Assert.Equal(CasinoGames.Holdem, shown.Single(row => row.RoomId == "holdem-1").GameId);
        Assert.Contains(shown, row => row.RoomId == "mine");
    }

    [Fact]
    public void AnOldServerHidesRoomsAndTablesItDoesNotRun()
    {
        var rows = new LiveRow[LiveBoard.Capacity];
        var holdem = new[] { Table("holdem-1", CasinoTableKinds.House, gameKind: HoldemRules.Kind) };
        var count = LiveBoard.Collect(Rooms(), Array.Empty<CasinoTableRowDto>(), Array.Empty<CasinoTableRowDto>(),
            holdem, CasinoFeatureSet.Empty, rows);
        Assert.Equal(2, count);
        Assert.DoesNotContain(rows.Take(count), row => row.GameId == CasinoGames.Race);
    }

    [Fact]
    public void FiltersKeepWhatTheyNameAndNothingElse()
    {
        var full = Table("full", CasinoTableKinds.House, seated: 6, seats: 6);
        var open = Table("open", CasinoTableKinds.House, seated: 2, seats: 6);
        var practice = Table("practice", CasinoTableKinds.Private, currency: CasinoCurrencies.Practice);
        var gil = Table("gil", CasinoTableKinds.Private, currency: CasinoCurrencies.Gil, owner: "Mira Moon");
        var highStakes = Table("high", CasinoTableKinds.Private, minBet: CasinoTableFilters.HighStakeFloor);
        var friends = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "mira moon" };
        Assert.False(LiveBoard.Matches(LiveFilter.OpenSeats, Row(full), NoFriends));
        Assert.True(LiveBoard.Matches(LiveFilter.OpenSeats, Row(open), NoFriends));
        Assert.True(LiveBoard.Matches(LiveFilter.Practice, Row(practice), NoFriends));
        Assert.False(LiveBoard.Matches(LiveFilter.Practice, Row(open), NoFriends));
        Assert.True(LiveBoard.Matches(LiveFilter.Gil, Row(gil), NoFriends));
        Assert.True(LiveBoard.Matches(LiveFilter.Friends, Row(gil), friends));
        Assert.False(LiveBoard.Matches(LiveFilter.Friends, Row(gil), NoFriends));
        Assert.True(LiveBoard.Matches(LiveFilter.HighRoller, Row(highStakes), NoFriends));
        Assert.False(LiveBoard.Matches(LiveFilter.HighRoller, Row(open), NoFriends));
        Assert.True(LiveBoard.Matches(LiveFilter.All, Row(full), NoFriends));
    }

    [Fact]
    public void RoomsShowUnderAllAndOpenSeatsAndPracticeOnlyWhenPractice()
    {
        var room = new LiveRow(LiveRowKind.Room, CasinoGames.Wheel, "wheel-floor",
            new CasinoRoomListItemDto("wheel-floor", CasinoWire.WheelKind), null);
        Assert.True(LiveBoard.Matches(LiveFilter.All, room, NoFriends));
        Assert.True(LiveBoard.Matches(LiveFilter.OpenSeats, room, NoFriends));
        Assert.False(LiveBoard.Matches(LiveFilter.Practice, room, NoFriends));
        Assert.False(LiveBoard.Matches(LiveFilter.HighRoller, room, NoFriends));
    }

    [Fact]
    public void FilteringCopiesOnlyTheMatches()
    {
        var rows = new[]
        {
            Row(Table("a", CasinoTableKinds.House, seated: 6, seats: 6)),
            Row(Table("b", CasinoTableKinds.House, seated: 1, seats: 6)),
        };
        var into = new LiveRow[4];
        Assert.Equal(1, LiveBoard.Filter(rows, rows.Length, LiveFilter.OpenSeats, NoFriends, into));
        Assert.Equal("b", into[0].RoomId);
    }

    [Fact]
    public void AListedBookNeverJoinsTheLiveBoard()
    {
        var rows = new LiveRow[LiveBoard.Capacity];
        var book = Table("book-1", CasinoTableKinds.Private, seated: 0, seats: 0, currency: CasinoCurrencies.Gil,
            owner: "Ciri", gameKind: CasinoWire.BookKind);
        var listed = new[] { book, Table("mine", CasinoTableKinds.Private) };
        var features = CasinoFeatureSet.From(new[] { CasinoFeatures.Venue, CasinoFeatures.VenueBook });
        var count = LiveBoard.Collect(Array.Empty<CasinoRoomListItemDto>(), Array.Empty<CasinoTableRowDto>(), listed,
            Array.Empty<CasinoTableRowDto>(), features, rows);

        Assert.Equal(1, count);
        Assert.Equal("mine", rows[0].RoomId);
        Assert.Equal(CasinoGames.Book, LiveBoard.TableGameOf(CasinoWire.BookKind));
    }

    private static CasinoRoomListItemDto[] Rooms() =>
        new[]
        {
            new CasinoRoomListItemDto("wheel-floor", CasinoWire.WheelKind),
            new CasinoRoomListItemDto("bingo-hall", CasinoWire.BingoKind),
            new CasinoRoomListItemDto("race-track", CasinoWire.RaceKind),
            new CasinoRoomListItemDto("other", "casino.unknown"),
        };

    private static LiveRow Row(CasinoTableRowDto table) =>
        new(LiveRowKind.Table, CasinoGames.Blackjack, table.TableId, null, table);

    private static CasinoTableRowDto Table(string id, int kind, long minBet = 2_500, int seated = 1, int seats = 6,
        int currency = CasinoCurrencies.Chips, string owner = "", string gameKind = CasinoWire.BlackjackKind) =>
        new(id, gameKind, kind, OwnerName: owner, MinBet: minBet, MaxBet: minBet * 10, MaxSeats: seats,
            SeatedCount: seated, Occupancy: seated, Currency: currency, Practice: currency == CasinoCurrencies.Practice);
}
