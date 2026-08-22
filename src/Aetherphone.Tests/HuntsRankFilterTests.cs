using Aetherphone.Core.Hunts;
using Newtonsoft.Json;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HuntsRankFilterTests
{
    private static HuntWindowDto Window(string mobId, string worldId = "zalera") =>
        new() { MobId = mobId, WorldId = worldId, ZoneInstance = 0 };

    private static HuntMobDefinition Mob(string rank) => new() { Id = "test", Rank = rank };

    [Fact]
    public void FilterStateEnablesSSRankByDefault()
    {
        var filter = new HuntsFilterState();

        Assert.True(filter.RankSS);
        Assert.True(filter.Matches(Window("ker"), Mob("SS"), HuntWindowStatus.Open));
    }

    [Fact]
    public void FilterStateTogglesSSRankIndependentlyFromSRank()
    {
        var filter = new HuntsFilterState { RankSS = false };

        Assert.False(filter.Matches(Window("ker"), Mob("SS"), HuntWindowStatus.Open));
        Assert.True(filter.Matches(Window("gunitt"), Mob("S"), HuntWindowStatus.Open));
    }

    [Fact]
    public void FilterSnapshotDeserializedWithoutRankSSDefaultsToEnabled()
    {
        var snapshot = JsonConvert.DeserializeObject<HuntsFilterSnapshot>("{}");

        Assert.NotNull(snapshot);
        Assert.True(snapshot!.RankSS);
    }

    [Fact]
    public void NotificationSettingsEnableSSRankByDefault()
    {
        var settings = new HuntsNotificationSettings();

        Assert.True(settings.RankSS);
        Assert.True(settings.IsEnabledFor(Mob("SS"), "zalera"));
    }

    [Fact]
    public void NotificationSettingsToggleSSRankIndependentlyFromSRank()
    {
        var settings = new HuntsNotificationSettings { RankSS = false };

        Assert.False(settings.IsEnabledFor(Mob("SS"), "zalera"));
        Assert.True(settings.IsEnabledFor(Mob("S"), "zalera"));
    }

    [Fact]
    public void NotificationSnapshotDeserializedWithoutRankSSDefaultsToEnabled()
    {
        var snapshot = JsonConvert.DeserializeObject<HuntsNotificationSnapshot>("{}");

        Assert.NotNull(snapshot);
        Assert.True(snapshot!.RankSS);
    }
}
