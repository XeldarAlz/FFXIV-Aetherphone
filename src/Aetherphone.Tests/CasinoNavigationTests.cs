using Aetherphone.Apps.Casino;
using Aetherphone.Core.Apps;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoNavigationTests
{
    private static CasinoNavigation Fresh() => new(new ViewRouter<CasinoRoute>(CasinoRoute.Floor));

    [Fact]
    public void AGameFromTheFloorBacksOutToTheFloor()
    {
        var routes = Fresh();
        Assert.True(routes.Push(new CasinoRoute(CasinoScreen.Cabinet, CasinoGames.Plinko)));
        Assert.True(routes.Back());
        Assert.True(routes.OnRoot);
        Assert.Equal(CasinoTab.Floor, routes.Tab);
    }

    [Fact]
    public void ATableIsTwoTapsAndBacksThroughThePit()
    {
        var routes = Fresh();
        routes.Push(new CasinoRoute(CasinoScreen.Pit, CasinoGames.Blackjack));
        routes.Push(new CasinoRoute(CasinoScreen.Table, CasinoGames.Blackjack, string.Empty, "house-pit-1"));
        Assert.Equal(3, routes.Depth);
        routes.Back();
        Assert.Equal(CasinoScreen.Pit, routes.Current.Screen);
        routes.Back();
        Assert.True(routes.OnRoot);
    }

    [Fact]
    public void AWidgetLaunchLandsOnTheFloorUnderTheGame()
    {
        var routes = Fresh();
        routes.Select(CasinoTab.Cashier);
        routes.Push(new CasinoRoute(CasinoScreen.Limits));
        routes.Launch(new CasinoRoute(CasinoScreen.DailySpin));
        Assert.Equal(CasinoTab.Floor, routes.Tab);
        Assert.Equal(2, routes.Depth);
        routes.Back();
        Assert.True(routes.OnRoot);
        Assert.Equal(CasinoTab.Floor, routes.Tab);
    }

    [Fact]
    public void ADeepLinkToTheFloorLeavesNothingStacked()
    {
        var routes = Fresh();
        routes.Push(new CasinoRoute(CasinoScreen.History));
        routes.Launch(CasinoRoute.Floor);
        Assert.True(routes.OnRoot);
    }

    [Fact]
    public void PushingTheSameScreenTwiceStacksOnce()
    {
        var routes = Fresh();
        var table = new CasinoRoute(CasinoScreen.Table, CasinoGames.Blackjack, string.Empty, "t1");
        Assert.True(routes.Push(table));
        Assert.False(routes.Push(table));
        Assert.True(routes.Push(table with { TableId = "t2" }));
        Assert.Equal(3, routes.Depth);
    }

    [Fact]
    public void SwitchingTabsDropsThePushedPages()
    {
        var routes = Fresh();
        routes.Push(new CasinoRoute(CasinoScreen.Cabinet, CasinoGames.Wheel));
        routes.Select(CasinoTab.Live);
        Assert.True(routes.OnRoot);
        Assert.Equal(CasinoTab.Live, routes.Tab);
    }

    [Fact]
    public void ClosingATableFromTheDoorLeavesEveryPageOfThatTable()
    {
        var routes = Fresh();
        routes.Select(CasinoTab.Tables);
        routes.Push(new CasinoRoute(CasinoScreen.TableDoor, CasinoGames.Blackjack, string.Empty, "mine"));
        routes.Push(new CasinoRoute(CasinoScreen.TableLedger, CasinoGames.Blackjack, string.Empty, "mine"));
        while (routes.Holds("mine"))
        {
            routes.Back();
        }

        Assert.True(routes.OnRoot);
        Assert.Equal(CasinoTab.Tables, routes.Tab);
    }

    [Fact]
    public void AnotherTablesPagesAreNotLeftWhenOneCloses()
    {
        var routes = Fresh();
        routes.Push(new CasinoRoute(CasinoScreen.Pit, CasinoGames.Blackjack));
        routes.Push(new CasinoRoute(CasinoScreen.Table, CasinoGames.Blackjack, string.Empty, "other"));
        Assert.False(routes.Holds("mine"));
        Assert.False(routes.Holds(string.Empty));
        Assert.True(routes.Holds("other"));
    }
}
