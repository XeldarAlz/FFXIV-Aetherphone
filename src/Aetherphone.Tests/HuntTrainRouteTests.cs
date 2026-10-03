using Aetherphone.Core.Hunts;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HuntTrainRouteTests
{
    private static HuntMobDefinition Mob(string id, string rank, string expansion, string zone) => new()
    {
        Id = id,
        Rank = rank,
        ExpansionId = expansion,
        ZoneIds = new[] { zone },
    };

    private static HuntWindowDto Window(string mobId, string worldId, int instance = 0) => new()
    {
        Num = 1,
        MobId = mobId,
        WorldId = worldId,
        ZoneInstance = instance,
    };

    private static Dictionary<string, HuntMobDefinition> Catalog() => new()
    {
        ["first_a"] = Mob("first_a", "A", "dawntrail", "zone_one"),
        ["second_a"] = Mob("second_a", "A", "dawntrail", "zone_one"),
        ["third_a"] = Mob("third_a", "A", "dawntrail", "zone_two"),
        ["some_s"] = Mob("some_s", "S", "dawntrail", "zone_one"),
        ["old_a"] = Mob("old_a", "A", "endwalker", "zone_old"),
    };

    [Fact]
    public void KeepsOnlyARanksOfTheExpansionOnTheWorld()
    {
        var windows = new[]
        {
            Window("some_s", "cerberus"),
            Window("old_a", "cerberus"),
            Window("third_a", "louisoix"),
            Window("first_a", "cerberus"),
        };
        var stops = new List<HuntTrainStop>();

        HuntTrainRoute.Build(Catalog(), windows, "Cerberus", "dawntrail", stops);

        Assert.Single(stops);
        Assert.Equal("first_a", stops[0].MobId);
        Assert.Equal(3, stops[0].WindowIndex);
    }

    [Fact]
    public void OrdersStopsByZoneThenInstanceThenCatalog()
    {
        var windows = new[]
        {
            Window("third_a", "cerberus"),
            Window("second_a", "cerberus", 2),
            Window("first_a", "cerberus", 2),
            Window("second_a", "cerberus", 1),
            Window("first_a", "cerberus", 1),
        };
        var stops = new List<HuntTrainStop>();

        HuntTrainRoute.Build(Catalog(), windows, "cerberus", "dawntrail", stops);

        Assert.Equal(new[] { "first_a", "second_a", "first_a", "second_a", "third_a" },
            stops.ConvertAll(stop => stop.MobId).ToArray());
        Assert.Equal(new[] { 1, 1, 2, 2, 0 }, stops.ConvertAll(stop => stop.ZoneInstance).ToArray());
        Assert.True(HuntTrainRoute.StartsLeg(stops, 0));
        Assert.False(HuntTrainRoute.StartsLeg(stops, 1));
        Assert.True(HuntTrainRoute.StartsLeg(stops, 2));
        Assert.True(HuntTrainRoute.StartsLeg(stops, 4));
    }

    [Fact]
    public void EmptyWorldBuildsNothing()
    {
        var stops = new List<HuntTrainStop> { new("zone", 0, "mob", 0) };

        HuntTrainRoute.Build(Catalog(), new[] { Window("first_a", "cerberus") }, string.Empty, "dawntrail", stops);

        Assert.Empty(stops);
    }
}
