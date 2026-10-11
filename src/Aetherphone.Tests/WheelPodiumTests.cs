using Aetherphone.Apps.Casino.Cabinets;
using Aetherphone.Core.Aethernet.Contracts;
using Xunit;

namespace Aetherphone.Tests;

public sealed class WheelPodiumTests
{
    [Fact]
    public void PodiumsPrintEachSpotsReturnFromTheRules()
    {
        Assert.Equal(960, WheelCabinet.ReturnTenths(null, 0));
        Assert.Equal(960, WheelCabinet.ReturnTenths(null, 1));
        Assert.Equal(960, WheelCabinet.ReturnTenths(null, 2));
        Assert.Equal(960, WheelCabinet.ReturnTenths(null, 3));
        Assert.Equal(960, WheelCabinet.ReturnTenths(null, 4));
    }

    [Fact]
    public void ARoomThatShipsItsReturnWins()
    {
        var board = new CasinoWheelRoomStateDto(Spots: new[] { new CasinoWheelSpotDto(Spot: 4, ReturnBasisPoints: 9600) });
        Assert.Equal(960, WheelCabinet.ReturnTenths(board, 4));
        Assert.Equal(960, WheelCabinet.ReturnTenths(board, 0));
    }

    [Fact]
    public void OneReturnIsSaidOnceWhenEverySpotAgrees()
    {
        WheelCabinet.ReturnRange(null, out var low, out var high);
        Assert.Equal(960, low);
        Assert.Equal(960, high);
    }

    [Fact]
    public void SpotsThatDisagreeReadAsARange()
    {
        var board = new CasinoWheelRoomStateDto(Spots: new[] { new CasinoWheelSpotDto(Spot: 4, ReturnBasisPoints: 9200) });
        WheelCabinet.ReturnRange(board, out var low, out var high);
        Assert.Equal(920, low);
        Assert.Equal(960, high);
    }

    [Fact]
    public void ACardShowsTheTableOnlyWhenSomeoneElseIsOnIt()
    {
        Assert.False(WheelSpotCard.ShowsTable(0, 0));
        Assert.False(WheelSpotCard.ShowsTable(1, 500));
        Assert.True(WheelSpotCard.ShowsTable(2, 500));
        Assert.True(WheelSpotCard.ShowsTable(1, 0));
    }

    [Fact]
    public void YourOwnStakeCountsYouInBeforeTheRoomCatchesUp()
    {
        var view = new WheelSpotView(3, 1_000, 0, 0, true, false, false, false, true);
        Assert.Equal(1, view.Bettors);
        Assert.Equal(1_000, view.Total);
        Assert.False(view.ShowsTable);
        Assert.False(view.Ghost);
    }

    [Fact]
    public void TheSelectedEmptySpotShowsWhereTheBetWillGo()
    {
        Assert.True(new WheelSpotView(1, 0, 0, 0, true, false, false, false, true).Ghost);
        Assert.False(new WheelSpotView(1, 0, 0, 0, true, false, false, false, false).Ghost);
        Assert.False(new WheelSpotView(1, 0, 0, 0, false, false, false, false, true).Ghost);
    }

    [Fact]
    public void ThePickedCardStandsOutOnlyWhileBetsAreOpen()
    {
        Assert.True(new WheelSpotView(2, 0, 0, 0, true, false, false, false, true).Marked);
        Assert.False(new WheelSpotView(2, 500, 1, 500, true, false, false, false, false).Marked);
        Assert.False(new WheelSpotView(2, 500, 1, 500, true, false, false, true, false).Marked);
        Assert.True(new WheelSpotView(2, 0, 0, 0, false, false, true, true, false).Marked);
    }

    [Fact]
    public void OnlyYourLosingSpotsDimWhenTheWheelLands()
    {
        Assert.True(new WheelSpotView(0, 500, 1, 500, false, false, false, true, false).Losing);
        Assert.False(new WheelSpotView(0, 500, 1, 500, false, false, true, true, false).Losing);
        Assert.False(new WheelSpotView(0, 0, 3, 9_000, false, false, false, true, false).Losing);
        Assert.False(new WheelSpotView(0, 500, 1, 500, false, false, false, false, false).Losing);
    }
}
