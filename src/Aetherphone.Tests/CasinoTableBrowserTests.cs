using Aetherphone.Apps.Casino;
using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoTableBrowserTests
{
    private const string Me = "u-me";

    [Fact]
    public void TheAllFilterKeepsEveryTableGame()
    {
        Assert.True(CasinoTableFilters.Matches(CasinoTableFilter.All, Row(seatsTaken: 5, seatCount: 5), Me));
        Assert.True(CasinoTableFilters.Matches(CasinoTableFilter.All, Row(inviteOnly: true), Me));
        Assert.True(CasinoTableFilters.Matches(CasinoTableFilter.All, Row(gameKind: CasinoTableFilters.HoldemKind), Me));
        Assert.True(CasinoTableFilters.Matches(CasinoTableFilter.All, Row(gameKind: "casino.raffle"), Me));
        Assert.False(CasinoTableFilters.Matches(CasinoTableFilter.All, Row(gameKind: "casino.unknown"), Me));
    }

    [Fact]
    public void TheGameFiltersSplitBlackjackFromHoldem()
    {
        Assert.True(CasinoTableFilters.Matches(CasinoTableFilter.Blackjack, Row(), Me));
        Assert.False(CasinoTableFilters.Matches(CasinoTableFilter.Holdem, Row(), Me));
        Assert.True(CasinoTableFilters.Matches(CasinoTableFilter.Holdem, Row(gameKind: CasinoTableFilters.HoldemKind),
            Me));
    }

    [Fact]
    public void TheStakeBandsReadTheTableMinimumOnChipTablesOnly()
    {
        Assert.True(CasinoTableFilters.Matches(CasinoTableFilter.LowStakes, Row(minBet: 500), Me));
        Assert.True(CasinoTableFilters.Matches(CasinoTableFilter.LowStakes,
            Row(minBet: CasinoTableFilters.LowStakeCeiling), Me));
        Assert.False(CasinoTableFilters.Matches(CasinoTableFilter.LowStakes,
            Row(minBet: CasinoTableFilters.LowStakeCeiling + 1), Me));
        Assert.True(CasinoTableFilters.Matches(CasinoTableFilter.HighStakes,
            Row(minBet: CasinoTableFilters.HighStakeFloor), Me));
        Assert.False(CasinoTableFilters.Matches(CasinoTableFilter.HighStakes,
            Row(minBet: CasinoTableFilters.HighStakeFloor - 1), Me));
        Assert.False(CasinoTableFilters.Matches(CasinoTableFilter.LowStakes,
            Row(minBet: 500, currency: CasinoCurrencies.Practice), Me));
    }

    [Fact]
    public void ATableWithoutAMinimumIsNotALowStakesTable()
    {
        Assert.False(CasinoTableFilters.Matches(CasinoTableFilter.LowStakes, Row(minBet: 0), Me));
    }

    [Fact]
    public void PracticeAndGilFiltersReadTheCurrency()
    {
        Assert.True(CasinoTableFilters.Matches(CasinoTableFilter.Practice,
            Row(currency: CasinoCurrencies.Practice), Me));
        Assert.False(CasinoTableFilters.Matches(CasinoTableFilter.Practice, Row(), Me));
        Assert.True(CasinoTableFilters.Matches(CasinoTableFilter.Gil, Row(currency: CasinoCurrencies.Gil), Me));
        Assert.False(CasinoTableFilters.Matches(CasinoTableFilter.Gil, Row(currency: CasinoCurrencies.Practice), Me));
    }

    [Fact]
    public void MineIsATableIHost()
    {
        Assert.True(CasinoTableFilters.Matches(CasinoTableFilter.Mine, Row(owner: true), Me));
        Assert.False(CasinoTableFilters.Matches(CasinoTableFilter.Mine, Row(inviteOnly: true), Me));
        Assert.False(CasinoTableFilters.Matches(CasinoTableFilter.Mine, Row(owner: true), string.Empty));
    }

    [Fact]
    public void QuickSeatInheritsTheBandTheRailIsShowing()
    {
        Assert.Equal(CasinoStakeTiers.Low, CasinoStakeTiers.From(CasinoTableFilter.LowStakes));
        Assert.Equal(CasinoStakeTiers.High, CasinoStakeTiers.From(CasinoTableFilter.HighStakes));
        Assert.Equal(CasinoStakeTiers.Any, CasinoStakeTiers.From(CasinoTableFilter.All));
        Assert.Equal(CasinoStakeTiers.Any, CasinoStakeTiers.From(CasinoTableFilter.Practice));
        Assert.Equal(CasinoStakeTiers.Any, CasinoStakeTiers.From(CasinoTableFilter.Mine));
    }

    [Fact]
    public void EveryFilterOnTheRailIsListedOnceInTheStandardOrder()
    {
        Assert.Equal(new[]
        {
            CasinoTableFilter.All, CasinoTableFilter.Blackjack, CasinoTableFilter.Holdem, CasinoTableFilter.Rooms,
            CasinoTableFilter.LowStakes,
            CasinoTableFilter.HighStakes, CasinoTableFilter.Practice, CasinoTableFilter.Gil, CasinoTableFilter.Mine,
        }, CasinoTableFilters.All);
    }

    [Fact]
    public void OnlyTheBlackjackKindReachesTheLegacyDirectory()
    {
        var rows = new[] { Row(), Row(gameKind: CasinoTableFilters.HoldemKind), Row() };

        Assert.Equal(2, CasinoTableFilters.OfKind(rows, CasinoWire.BlackjackKind).Length);
        var blackjackOnly = new[] { rows[0], rows[2] };
        Assert.Same(blackjackOnly, CasinoTableFilters.OfKind(blackjackOnly, CasinoWire.BlackjackKind));
    }

    [Fact]
    public void AClosedTableDropsOutOfTheCachedList()
    {
        var rows = new[] { Row() with { TableId = "private-a" }, Row() with { TableId = "private-b" } };

        var kept = CasinoTableFilters.Without(rows, "private-a");

        Assert.Single(kept);
        Assert.Equal("private-b", kept[0].TableId);
        Assert.Same(rows, CasinoTableFilters.Without(rows, "private-c"));
        Assert.Same(rows, CasinoTableFilters.Without(rows, string.Empty));
    }

    [Fact]
    public void ACardCarriesItsCurrencyAndTheHostRecord()
    {
        var gil = Row(currency: CasinoCurrencies.Gil) with
        {
            Name = "Rose Room",
            MaxBet = 500_000,
            Config = new CasinoTableConfigDto(Currency: CasinoCurrencies.Gil, Bank: 20_000_000),
            Reputation = new CasinoHostReputationDto(3, 12, 1),
        };

        var view = TableBrowser.ViewOf(gil, Me);

        Assert.Equal("Rose Room", view.Name);
        Assert.Equal(CasinoCurrencies.Gil, view.Currency);
        Assert.Contains("20,000,000", view.Stakes, StringComparison.Ordinal);
        Assert.True(view.Reputation.Length > 0);
        Assert.True(view.ReputationWarns);

        var practice = TableBrowser.ViewOf(Row(currency: CasinoCurrencies.Practice), Me);
        Assert.Equal(CasinoCurrencies.Practice, practice.Currency);
        Assert.Equal(string.Empty, practice.Reputation);
    }

    [Fact]
    public void ABookRowNamesItsEntriesWhenTheLedgerIsKnownAndItsHostOtherwise()
    {
        var book = Row(seatsTaken: 0, seatCount: 0, owner: true, gameKind: CasinoWire.BookKind,
            currency: CasinoCurrencies.Gil) with
        {
            TableId = "table-book",
            Name = "Mist Lounge",
            Config = new CasinoTableConfigDto(GameKind: CasinoWire.BookKind, Currency: CasinoCurrencies.Gil,
                Bank: 5_000_000),
        };

        var counted = TableBrowser.ViewOf(book, Me, 12);
        Assert.True(counted.Book);
        Assert.Equal("Mist Lounge", counted.Name);
        Assert.Equal(Loc.T(L.Book.RowLine, "12"), counted.Stakes);
        Assert.Equal(Loc.T(L.Tables.HostedBy, "Emerald"), counted.Seats);
        Assert.False(counted.Full);
        Assert.True(counted.Mine);
        Assert.Equal(CasinoGames.Book, counted.GameId);
        Assert.Equal(CasinoCurrencies.Gil, counted.Currency);
        Assert.True(TableRow.Live(counted));

        var unknown = TableBrowser.ViewOf(book, Me);
        Assert.Equal(Loc.T(L.Tables.HostedBy, "Emerald"), unknown.Stakes);
        Assert.Equal(string.Empty, unknown.Seats);
        Assert.Equal(counted.Stakes, Apps.Casino.Venue.VenueContextCard.OwnLineOf(counted, 12));
        Assert.Equal(Loc.T(L.Book.Plays), Apps.Casino.Venue.VenueContextCard.OwnLineOf(unknown, -1));
        var table = TableBrowser.ViewOf(Row(), Me);
        Assert.Equal(table.Seats, Apps.Casino.Venue.VenueContextCard.OwnLineOf(table, -1));

        var ledger = new CasinoTableLedgerDto("table-book", true, Currency: CasinoCurrencies.Gil,
            Entries: new[] { new CasinoLedgerEntryDto("g1"), new CasinoLedgerEntryDto("g2") });
        Assert.Equal(2, TableBrowser.BookEntriesOf(book, ledger));
        Assert.Equal(-1, TableBrowser.BookEntriesOf(book, ledger with { TableId = "other" }));
        Assert.Equal(-1, TableBrowser.BookEntriesOf(book, null));
        Assert.Equal(-1, TableBrowser.BookEntriesOf(Row(), ledger with { TableId = "blackjack-pit" }));
        Assert.Equal(Loc.T(L.Book.Plays), TableBrowser.BookLine(-1, string.Empty));
    }

    [Fact]
    public void AnUnnamedHostedTableOrBookIsNamedForItsHostInTheReadersLanguage()
    {
        var table = Row(owner: true) with { Name = string.Empty };
        Assert.Equal(Loc.T(L.Casino.TableHostedBy, table.OwnerName), TableBrowser.NameOf(table));
        var book = table with { GameKind = CasinoWire.BookKind };
        Assert.Equal(Loc.T(L.Book.HostedBy, table.OwnerName), TableBrowser.NameOf(book));
        Assert.Equal("Mist Lounge", TableBrowser.NameOf(book with { Name = "Mist Lounge" }));
        Assert.Equal(TableBrowser.NameOf(book), TableBrowser.ViewOf(book, Me).Name);
    }

    [Fact]
    public void ABookIsListedUnderAllGilAndMineButNeverUnderRooms()
    {
        var book = Row(owner: true, gameKind: CasinoWire.BookKind, currency: CasinoCurrencies.Gil);
        Assert.True(CasinoTableFilters.Shown(book));
        Assert.True(CasinoTableFilters.Matches(CasinoTableFilter.All, book, Me));
        Assert.True(CasinoTableFilters.Matches(CasinoTableFilter.Gil, book, Me));
        Assert.True(CasinoTableFilters.Matches(CasinoTableFilter.Mine, book, Me));
        Assert.False(CasinoTableFilters.Matches(CasinoTableFilter.Rooms, book, Me));
        Assert.False(CasinoTableFilters.Matches(CasinoTableFilter.Blackjack, book, Me));
        Assert.False(CasinoTableFilters.Matches(CasinoTableFilter.Practice, book, Me));
    }

    [Fact]
    public void AnInviteTokenRoundTrips()
    {
        var token = CasinoShare.Compose("blackjack-pit");
        Assert.Equal("[aep.casino.v1:blackjack-pit]", token);
        Assert.True(CasinoShare.TryParse(token, out var tableId));
        Assert.Equal("blackjack-pit", tableId);
        Assert.True(CasinoShare.IsToken("  " + token + "  "));
    }

    [Fact]
    public void ATokenTheServerAlreadyWrappedIsNeverWrappedTwice()
    {
        var token = CasinoShare.Compose("[aep.casino.v1:table-442d]");
        Assert.Equal("[aep.casino.v1:table-442d]", token);
    }

    [Fact]
    public void ABareTableIdPastedWithoutItsWrapperStillNamesTheTable()
    {
        Assert.True(CasinoShare.TryParse("private-4f2a", out var tableId));
        Assert.Equal("private-4f2a", tableId);
    }

    [Fact]
    public void RubbishIsNotATable()
    {
        Assert.False(CasinoShare.TryParse(null, out _));
        Assert.False(CasinoShare.TryParse(string.Empty, out _));
        Assert.False(CasinoShare.TryParse("[aep.casino.v1:]", out _));
        Assert.False(CasinoShare.TryParse("[aep.muster.v1:abc]", out _));
        Assert.False(CasinoShare.TryParse("table id with spaces", out _));
        Assert.False(CasinoShare.TryParse("../../etc/passwd", out _));
        Assert.False(CasinoShare.TryParse(new string('a', CasinoShare.MaxIdLength + 1), out _));
    }

    [Fact]
    public void TheTableRoutesAreWhereTheBackendMustPutThem()
    {
        Assert.Equal("/casino/tables", CasinoClient.TablesPath);
        Assert.Equal("/casino/tables/quickseat", CasinoClient.QuickSeatPath);
        Assert.Equal("/casino/tables", CasinoClient.TablesPagePath(string.Empty));
        Assert.Equal("/casino/tables?game=casino.blackjack",
            CasinoClient.TablesPagePath(CasinoWire.BlackjackKind));
        Assert.Equal("/casino/tables/blackjack-pit/sit",
            CasinoClient.TablePath(CasinoRoomIds.BlackjackPit, "sit"));
        Assert.Equal("/casino/tables/blackjack-pit/claim",
            CasinoClient.TablePath(CasinoRoomIds.BlackjackPit, "claim"));
        Assert.Equal("/casino/tables/a%20b/door", CasinoClient.TablePath("a b", "door"));
    }

    [Fact]
    public void OnlyADoorRefusalOffersTheKnock()
    {
        Assert.True(CasinoKnocks.AsksToJoin(CasinoReasons.InviteOnly));
        Assert.True(CasinoKnocks.AsksToJoin(CasinoReasons.NotMember));
        Assert.True(CasinoKnocks.AsksToJoin(CasinoReasons.Denied));
        Assert.False(CasinoKnocks.AsksToJoin(CasinoReasons.BannedFromTable));
        Assert.False(CasinoKnocks.AsksToJoin(CasinoReasons.Ended));
        Assert.False(CasinoKnocks.AsksToJoin(CasinoReasons.Full));
    }

    [Fact]
    public void ARemovedPlayerIsToldTheyWereRemovedRatherThanThatTheTableClosed()
    {
        var closed = L.Casino.BlackjackClosedTitle;
        Assert.Equal(L.Tables.RemovedTitle.Key, TableDoorCard.TitleFor(CasinoReasons.Kicked, closed).Key);
        Assert.Equal(L.Tables.RemovedTitle.Key, TableDoorCard.TitleFor(CasinoReasons.BannedFromTable, closed).Key);
        Assert.Equal(L.Casino.BlackjackDoorTitle.Key, TableDoorCard.TitleFor(CasinoReasons.InviteOnly, closed).Key);
        Assert.Equal(closed.Key, TableDoorCard.TitleFor(CasinoReasons.Ended, closed).Key);
        Assert.True(CasinoReasons.TryMessage(CasinoReasons.Kicked, out _));
    }

    private static CasinoTableRowDto Row(long minBet = 500, int seatsTaken = 1, int seatCount = 5,
        bool inviteOnly = false, bool owner = false, string gameKind = CasinoWire.BlackjackKind,
        int currency = CasinoCurrencies.Chips)
    {
        return new CasinoTableRowDto(
            TableId: "blackjack-pit",
            GameKind: gameKind,
            Kind: inviteOnly || owner ? CasinoTableKinds.Private : CasinoTableKinds.House,
            OwnerUserId: owner ? Me : inviteOnly ? "u-other" : string.Empty,
            OwnerName: owner ? "Emerald" : string.Empty,
            MinBet: minBet,
            MaxBet: 5_000,
            MinBuyIn: 20_000,
            MaxBuyIn: 200_000,
            MaxSeats: seatCount,
            SeatedCount: seatsTaken,
            Admitted: true,
            Practice: currency == CasinoCurrencies.Practice,
            Currency: currency);
    }
}
