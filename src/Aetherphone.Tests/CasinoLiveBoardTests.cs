using Aetherphone.Apps.Casino;
using Aetherphone.Apps.Casino.Strip;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoLiveBoardTests
{
    private const string Me = "u-me";

    private static readonly CasinoFeatureSet EveryFeature = CasinoFeatureSet.From(new[]
    {
        CasinoFeatures.Race, CasinoFeatures.Holdem, CasinoFeatures.Venue, CasinoFeatures.VenueBook,
        CasinoFeatures.Machines,
    });

    [Fact]
    public void PlayerTablesLeaveTheHouseTablesToTheirOwnSection()
    {
        var listed = new[]
        {
            Table("pit", CasinoTableKinds.House),
            Table("vault", CasinoTableKinds.House),
            Table("hosted", CasinoTableKinds.Private),
        };
        var holdem = new[]
        {
            Table(HoldemRules.LowRoom, CasinoTableKinds.House, gameKind: HoldemRules.Kind),
            Table("holdem-kind-house", CasinoTableKinds.House, gameKind: HoldemRules.Kind),
            Table("friday", CasinoTableKinds.Private, gameKind: HoldemRules.Kind),
        };

        var rows = Collect(listed, holdem, Array.Empty<CasinoTableRowDto>(), EveryFeature, string.Empty);

        Assert.Equal(new[] { "hosted", "friday" }, Ids(rows));
        Assert.Equal(CasinoGames.Holdem, rows[1].GameId);
    }

    [Fact]
    public void BooksAndVenueRoomsJoinThePlayerTables()
    {
        var listed = new[]
        {
            Table("book-1", CasinoTableKinds.Private, seated: 0, seats: 0, currency: CasinoCurrencies.Gil,
                gameKind: CasinoWire.BookKind),
            Table("raffle-1", CasinoTableKinds.Private, seated: 0, seats: 0, currency: CasinoCurrencies.Gil,
                gameKind: VenueKinds.Raffle),
        };

        var rows = Collect(listed, Array.Empty<CasinoTableRowDto>(), Array.Empty<CasinoTableRowDto>(), EveryFeature,
            string.Empty);

        Assert.Equal(new[] { "book-1", "raffle-1" }, Ids(rows));
        Assert.Equal(CasinoGames.Book, rows[0].GameId);
        Assert.Equal(CasinoGames.Raffle, rows[1].GameId);
        Assert.All(rows, row => Assert.True(row.Open));
    }

    [Fact]
    public void AnOldServerHidesTablesItDoesNotRun()
    {
        var listed = new[]
        {
            Table("book-1", CasinoTableKinds.Private, currency: CasinoCurrencies.Gil, gameKind: CasinoWire.BookKind),
            Table("dice-1", CasinoTableKinds.Private, gameKind: VenueKinds.DiceTable),
            Table("blackjack-1", CasinoTableKinds.Private),
        };
        var holdem = new[] { Table("friday", CasinoTableKinds.Private, gameKind: HoldemRules.Kind) };

        var rows = Collect(listed, holdem, Array.Empty<CasinoTableRowDto>(), CasinoFeatureSet.Empty, string.Empty);

        Assert.Equal(new[] { "blackjack-1" }, Ids(rows));
    }

    [Fact]
    public void YourOwnAndYourPinnedTableStayOutOfTheList()
    {
        var listed = new[]
        {
            Table("mine", CasinoTableKinds.Private, ownerId: Me),
            Table("seated", CasinoTableKinds.Private),
            Table("other", CasinoTableKinds.Private),
        };

        var rows = Collect(listed, Array.Empty<CasinoTableRowDto>(), Array.Empty<CasinoTableRowDto>(), EveryFeature,
            "seated");

        Assert.Equal(new[] { "other" }, Ids(rows));
    }

    [Fact]
    public void TablesAtYourWardSortFirstThenOpenSeats()
    {
        var full = Table("full", CasinoTableKinds.Private, seated: 6, seats: 6);
        var open = Table("open", CasinoTableKinds.Private, seated: 2, seats: 6);
        var fullHere = Table("full-here", CasinoTableKinds.Private, seated: 6, seats: 6);
        var openHere = Table("open-here", CasinoTableKinds.Private, seated: 1, seats: 6);
        var paused = Table("paused", CasinoTableKinds.Private, seated: 1, seats: 6) with { Paused = true };
        var listed = new[] { full, paused, open, fullHere, openHere };
        var nearby = new[] { fullHere, openHere };

        var rows = Collect(listed, Array.Empty<CasinoTableRowDto>(), nearby, EveryFeature, string.Empty);

        Assert.Equal(new[] { "open-here", "full-here", "open", "full", "paused" }, Ids(rows));
        Assert.True(rows[0].Here);
        Assert.True(rows[1].Here);
        Assert.False(rows[2].Here);
    }

    [Fact]
    public void ANearbyTableMissingFromTheDirectoryStillShowsOnce()
    {
        var pinned = Table("pinned", CasinoTableKinds.Private, gameKind: VenueKinds.Deathroll, seated: 0, seats: 0);
        var listed = new[] { Table("listed", CasinoTableKinds.Private), pinned };
        var nearby = new[] { pinned, Table("unlisted", CasinoTableKinds.Private) };

        var rows = Collect(listed, Array.Empty<CasinoTableRowDto>(), nearby, EveryFeature, string.Empty);

        Assert.Equal(new[] { "pinned", "unlisted", "listed" }, Ids(rows));
        Assert.Same(pinned, rows[0].Row);
    }

    [Fact]
    public void TheCurrencyFiltersKeepWhatTheyNameAndNothingElse()
    {
        var chips = Table("chips", CasinoTableKinds.Private);
        var gil = Table("gil", CasinoTableKinds.Private, currency: CasinoCurrencies.Gil);
        var practice = Table("practice", CasinoTableKinds.Private, currency: CasinoCurrencies.Practice);
        var configuredGil = Table("configured", CasinoTableKinds.Private) with
        {
            Config = new CasinoTableConfigDto(Currency: CasinoCurrencies.Gil),
        };

        Assert.True(LiveBoard.Matches(LiveFilter.Chips, chips));
        Assert.False(LiveBoard.Matches(LiveFilter.Chips, gil));
        Assert.True(LiveBoard.Matches(LiveFilter.Gil, gil));
        Assert.True(LiveBoard.Matches(LiveFilter.Gil, configuredGil));
        Assert.False(LiveBoard.Matches(LiveFilter.Gil, practice));
        Assert.True(LiveBoard.Matches(LiveFilter.FreePlay, practice));
        Assert.False(LiveBoard.Matches(LiveFilter.FreePlay, chips));
        Assert.True(LiveBoard.Matches(LiveFilter.All, practice));
    }

    [Fact]
    public void FilteringKeepsTheSortedOrderAsIndexes()
    {
        var rows = new[]
        {
            new LiveTable(Table("a", CasinoTableKinds.Private, currency: CasinoCurrencies.Gil), CasinoGames.Blackjack,
                true, true),
            new LiveTable(Table("b", CasinoTableKinds.Private), CasinoGames.Blackjack, false, true),
            new LiveTable(Table("c", CasinoTableKinds.Private, currency: CasinoCurrencies.Gil), CasinoGames.Blackjack,
                false, false),
        };
        var into = new int[4];

        Assert.Equal(2, LiveBoard.Filter(rows, rows.Length, LiveFilter.Gil, into));
        Assert.Equal(0, into[0]);
        Assert.Equal(2, into[1]);
        Assert.Equal(3, LiveBoard.Filter(rows, rows.Length, LiveFilter.All, into));
    }

    [Fact]
    public void TheFilterRailShowsOnlyForMoreThanThreeTables()
    {
        Assert.False(LiveBoard.ShowsRail(0));
        Assert.False(LiveBoard.ShowsRail(LiveBoard.RailThreshold));
        Assert.True(LiveBoard.ShowsRail(LiveBoard.RailThreshold + 1));
        Assert.Equal(new[] { LiveFilter.All, LiveFilter.Chips, LiveFilter.Gil, LiveFilter.FreePlay },
            LiveBoard.Filters);
    }

    [Fact]
    public void TheCollectorStopsAtCapacity()
    {
        var listed = new CasinoTableRowDto[LiveBoard.Capacity + 5];
        for (var index = 0; index < listed.Length; index++)
        {
            listed[index] = Table("t" + index, CasinoTableKinds.Private);
        }

        var rows = new LiveTable[LiveBoard.Capacity];
        var count = LiveBoard.Collect(listed, Array.Empty<CasinoTableRowDto>(), Array.Empty<CasinoTableRowDto>(),
            EveryFeature, string.Empty, Me, rows);

        Assert.Equal(LiveBoard.Capacity, count);
    }

    [Fact]
    public void HouseHoldemSeatsAreSummedOverTheHouseRoomsOnly()
    {
        var rows = new[]
        {
            Table(HoldemRules.LowRoom, CasinoTableKinds.House, seated: 3, gameKind: HoldemRules.Kind),
            Table(HoldemRules.HighRoom, CasinoTableKinds.House, seated: 2, gameKind: HoldemRules.Kind),
            Table("friday", CasinoTableKinds.Private, seated: 4, gameKind: HoldemRules.Kind),
        };

        Assert.Equal(5, LiveBoard.HouseHoldemSeated(rows));
    }

    private static LiveTable[] Collect(CasinoTableRowDto[] listed, CasinoTableRowDto[] holdem,
        CasinoTableRowDto[] nearby, CasinoFeatureSet features, string pinned)
    {
        var rows = new LiveTable[LiveBoard.Capacity];
        var count = LiveBoard.Collect(listed, holdem, nearby, features, pinned, Me, rows);
        return rows.Take(count).ToArray();
    }

    private static string[] Ids(LiveTable[] rows) => rows.Select(row => row.Row.TableId).ToArray();

    private static CasinoTableRowDto Table(string id, int kind, long minBet = 2_500, int seated = 1, int seats = 6,
        int currency = CasinoCurrencies.Chips, string owner = "Mira Moon", string ownerId = "u-mira",
        string gameKind = CasinoWire.BlackjackKind) =>
        new(id, gameKind, kind, OwnerUserId: ownerId, OwnerName: owner, MinBet: minBet, MaxBet: minBet * 10,
            MaxSeats: seats, SeatedCount: seated, Occupancy: seated, Currency: currency,
            Practice: currency == CasinoCurrencies.Practice);
}
