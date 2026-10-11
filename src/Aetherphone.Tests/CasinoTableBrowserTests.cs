using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
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
        Assert.True(BlackjackTable.AsksToJoin(CasinoReasons.InviteOnly));
        Assert.True(BlackjackTable.AsksToJoin(CasinoReasons.NotMember));
        Assert.True(BlackjackTable.AsksToJoin(CasinoReasons.Denied));
        Assert.False(BlackjackTable.AsksToJoin(CasinoReasons.BannedFromTable));
        Assert.False(BlackjackTable.AsksToJoin(CasinoReasons.Ended));
        Assert.False(BlackjackTable.AsksToJoin(CasinoReasons.Full));
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
