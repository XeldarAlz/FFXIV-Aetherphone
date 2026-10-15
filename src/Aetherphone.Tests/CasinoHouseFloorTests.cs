using Aetherphone.Apps.Casino;
using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoHouseFloorTests
{
    private const string Me = "u-me";

    [Fact]
    public void TheHouseLeadIsTheBusiestBlackjackTableWithASeatLeft()
    {
        var full = Table("a", CasinoTableKinds.House, seated: 6);
        var quiet = Table("b", CasinoTableKinds.House, seated: 1);
        var busy = Table("c", CasinoTableKinds.House, seated: 4);
        var hosted = Table("d", CasinoTableKinds.Private, seated: 5);
        var floor = HouseBlackjack.Of(new[] { full, quiet, busy, hosted });
        Assert.Same(busy, floor.Lead);
        Assert.Equal(11, floor.Seated);
        Assert.True(floor.Open);

        var packed = HouseBlackjack.Of(new[] { full });
        Assert.Same(full, packed.Lead);
        Assert.False(packed.Open);

        Assert.Null(HouseBlackjack.Of(new[] { hosted }).Lead);
    }

    [Fact]
    public void TheHouseRangeRunsFromTheTableFloorToThePlayersOwnCeiling()
    {
        var floor = new HouseBlackjackFloor(Table("a", CasinoTableKinds.House), 0, true, 500, 10_000_000);
        Assert.Equal(500, floor.MinBet);
        Assert.Equal(250_000, floor.TopBetFor(250_000));
        Assert.Equal(500, floor.TopBetFor(100));
        Assert.Equal(10_000_000, floor.TopBetFor(50_000_000));
        Assert.Equal(10_000_000, floor.TopBetFor(0));
        Assert.Equal(BlackjackRules.MinBet, new HouseBlackjackFloor(null, 0, false, 0, 0).MinBet);
    }

    [Fact]
    public void TheHouseRangeSpansTheLowestFeltToTheHighest()
    {
        var pit = Table("pit", CasinoTableKinds.House) with { MinBet = 500, MaxBet = 10_000 };
        var salon = Table("salon", CasinoTableKinds.House) with { MinBet = 50_000, MaxBet = 1_000_000 };
        var vault = Table("vault", CasinoTableKinds.House) with { MinBet = 500_000, MaxBet = 10_000_000 };
        var hosted = Table("mine", CasinoTableKinds.Private) with { MinBet = 100, MaxBet = 50_000_000 };
        var floor = HouseBlackjack.Of(new[] { salon, vault, hosted, pit });
        Assert.Equal(500, floor.MinBet);
        Assert.Equal(100_000, floor.TopBetFor(100_000));
        Assert.Equal(10_000_000, floor.TopBetFor(0));
        Assert.True(HouseBlackjack.Reaches(100_000, salon.MinBet));
        Assert.False(HouseBlackjack.Reaches(100_000, vault.MinBet));
        Assert.True(HouseBlackjack.Reaches(0, vault.MinBet));
    }

    [Fact]
    public void TheBrowserFloorShowsOneHouseBlackjackRowThenHoldemThenTheRest()
    {
        var pit = Table("pit", CasinoTableKinds.House, seated: 2);
        var salon = Table("salon", CasinoTableKinds.House);
        var hosted = Table("mine", CasinoTableKinds.Private);
        var holdemHouse = Table("holdem-low", CasinoTableKinds.House, gameKind: HoldemRules.Kind);
        var holdemHosted = Table("holdem-mine", CasinoTableKinds.Private, gameKind: HoldemRules.Kind);
        var rows = CasinoTableFilters.Floor(new[] { pit, salon, hosted, holdemHosted },
            new[] { holdemHouse, holdemHosted }, pit);
        Assert.Equal(new[] { "pit", "holdem-low", "mine", "holdem-mine" }, rows.Select(row => row.TableId));
        Assert.Equal(new[] { "holdem-low", "mine" },
            CasinoTableFilters.Floor(new[] { salon, hosted }, new[] { holdemHouse }, null).Select(row => row.TableId));
    }

    [Fact]
    public void AHouseHoldemTableIsNamedByItsBlindsAndShowsItsBuyIn()
    {
        var row = new CasinoTableRowDto("holdem-low", HoldemRules.Kind, CasinoTableKinds.House, MinBet: 50,
            MaxBet: 100, MinBuyIn: 2_000, MaxBuyIn: 10_000, MaxSeats: 6);
        var view = TableRowViews.ViewOf(row, Me);
        Assert.Equal(Loc.T(L.Holdem.TableName, NumberText.Compact(50), NumberText.Compact(100)), view.Name);
        Assert.Equal(Loc.T(L.Holdem.BuyInValue, NumberText.Compact(2_000), NumberText.Compact(10_000)), view.Stakes);
        Assert.Equal(CasinoGames.Holdem, view.GameId);
    }

    [Fact]
    public void AHostedHoldemTableKeepsItsNameAndShowsItsBlinds()
    {
        var row = new CasinoTableRowDto("holdem-ada", HoldemRules.Kind, CasinoTableKinds.Private,
            OwnerName: "Ada Moon", MinBet: 500, MaxBet: 1_000, Name: "Ada's table");
        var view = TableRowViews.ViewOf(row, Me);
        Assert.Equal("Ada's table", view.Name);
        Assert.Equal(Loc.T(L.Holdem.BlindsValue, NumberText.Compact(500), NumberText.Compact(1_000)), view.Stakes);
    }

    [Fact]
    public void TheHouseBlackjackRowQuotesThePlayersRangeAndTheWholeFloorsHeadcount()
    {
        var lead = Table("blackjack-pit", CasinoTableKinds.House, seated: 2);
        var view = TableRowViews.HouseView(new HouseBlackjackFloor(lead, 5, true, 500, 10_000_000), 250_000);
        Assert.Equal(Loc.T(L.Casino.GameBlackjack), view.Name);
        Assert.Equal(Loc.T(L.Casino.TableStakes, NumberText.Compact(500), NumberText.Compact(250_000)), view.Stakes);
        Assert.Equal(5, view.Seated);
        Assert.Equal(CasinoGames.Blackjack, view.GameId);
        Assert.False(view.Full);
        Assert.True(TableRowViews.HouseView(new HouseBlackjackFloor(lead, 24, false, 500, 10_000_000), 250_000)
            .Full);
    }

    private static CasinoTableRowDto Table(string id, int kind, int seated = 0,
        string gameKind = CasinoWire.BlackjackKind) =>
        new(id, gameKind, kind, MinBet: 500, MaxBet: 10_000_000, MaxSeats: 6, SeatedCount: seated,
            Occupancy: seated);
}
