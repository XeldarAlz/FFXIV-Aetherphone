using Aetherphone.Core;
using Aetherphone.Core.Hunts;
using Xunit;

namespace Aetherphone.Tests;

public sealed class SettingsSnapshotStoreTests
{
    [Fact]
    public void LoadReturnsNullWhenNothingWasEverSaved()
    {
        var configuration = new Configuration();
        var store = new SettingsSnapshotStore<HuntsFilterSnapshot>(configuration,
            static config => config.HuntsFilterSettings, static (config, snapshot) => config.HuntsFilterSettings = snapshot);

        Assert.Null(store.Load());
    }

    [Fact]
    public void LoadReturnsWhateverReadReportsWithoutTouchingConfigurationDirectly()
    {
        var configuration = new Configuration();
        configuration.HuntsFilterSettings = new HuntsFilterSnapshot { RankS = true, Worlds = { "adamantoise" } };

        var store = new SettingsSnapshotStore<HuntsFilterSnapshot>(configuration,
            static config => config.HuntsFilterSettings, static (config, snapshot) => config.HuntsFilterSettings = snapshot);

        var loaded = store.Load();
        Assert.NotNull(loaded);
        Assert.True(loaded!.RankS);
        Assert.Equal(new[] { "adamantoise" }, loaded.Worlds);
    }
}
