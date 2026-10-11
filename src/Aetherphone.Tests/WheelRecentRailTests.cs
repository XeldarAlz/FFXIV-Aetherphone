using Aetherphone.Apps.Casino.Cabinets;
using Xunit;

namespace Aetherphone.Tests;

public sealed class WheelRecentRailTests
{
    [Fact]
    public void AJoinerSeesTheServersRecentResults()
    {
        var rail = new WheelRecentRail();
        rail.Sync(new[] { 4, 0, 1 });
        Assert.Equal(new[] { 4, 0, 1 }, Read(rail));
    }

    [Fact]
    public void TheLandedSpotLeadsTheRailUntilTheServerCarriesIt()
    {
        var rail = new WheelRecentRail();
        rail.Sync(new[] { 0, 1 });
        rail.Land(3);
        Assert.True(rail.HasLanded);
        Assert.Equal(new[] { 3, 0, 1 }, Read(rail));

        rail.Sync(new[] { 0, 1 });
        Assert.Equal(new[] { 3, 0, 1 }, Read(rail));

        rail.Sync(new[] { 3, 0, 1 });
        Assert.False(rail.HasLanded);
        Assert.Equal(new[] { 3, 0, 1 }, Read(rail));
    }

    [Fact]
    public void TheRailKeepsTwelveWhenTheServerOnlySendsTen()
    {
        var rail = new WheelRecentRail();
        var server = new[] { 0, 1, 0, 2, 0, 1, 0, 3, 0, 1 };
        rail.Sync(server);
        var spins = new[] { 2, 4, 1 };
        for (var spin = 0; spin < spins.Length; spin++)
        {
            var next = new int[10];
            next[0] = spins[spin];
            Array.Copy(server, 0, next, 1, 9);
            server = next;
            rail.Sync(server);
        }

        Assert.Equal(WheelRecentRail.Capacity, rail.Count);
        Assert.Equal(new[] { 1, 4, 2, 0, 1, 0, 2, 0, 1, 0, 3, 0 }, Read(rail));
    }

    [Fact]
    public void AnUnrelatedListReplacesTheRail()
    {
        var rail = new WheelRecentRail();
        rail.Sync(new[] { 0, 0, 0 });
        rail.Sync(new[] { 4, 3, 2, 1 });
        Assert.Equal(new[] { 4, 3, 2, 1 }, Read(rail));
    }

    [Fact]
    public void OutOfRangeSpotsNeverReachTheRail()
    {
        var rail = new WheelRecentRail();
        rail.Sync(new[] { 0, 9, -1, 2 });
        rail.Land(7);
        Assert.False(rail.HasLanded);
        Assert.Equal(new[] { 0, 2 }, Read(rail));
    }

    [Fact]
    public void ResetEmptiesTheRail()
    {
        var rail = new WheelRecentRail();
        rail.Sync(new[] { 1, 2 });
        rail.Land(4);
        rail.Reset();
        Assert.Equal(0, rail.Count);
    }

    private static int[] Read(WheelRecentRail rail)
    {
        var spots = new int[rail.Count];
        for (var index = 0; index < spots.Length; index++)
        {
            spots[index] = rail.SpotAt(index);
        }

        return spots;
    }
}
