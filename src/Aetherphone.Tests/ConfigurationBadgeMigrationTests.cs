using Newtonsoft.Json;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ConfigurationBadgeMigrationTests
{
    [Fact]
    public void LegacyBadgeFlagsDeserializeFromTheirPreRenameJsonKeys()
    {
        const string json = """{"ShowWalletBadge":false,"ShowDailiesBadge":false,"ShowActivityBadge":true}""";

        var configuration = JsonConvert.DeserializeObject<Configuration>(json);

        Assert.NotNull(configuration);
        Assert.False(configuration!.LegacyShowWalletBadge);
        Assert.False(configuration.LegacyShowDailiesBadge);
        Assert.True(configuration.LegacyShowActivityBadge);
    }

    [Fact]
    public void LegacyBadgeFlagsDefaultToTrueWhenAbsentFromSavedJson()
    {
        const string json = "{}";

        var configuration = JsonConvert.DeserializeObject<Configuration>(json);

        Assert.NotNull(configuration);
        Assert.True(configuration!.LegacyShowWalletBadge);
        Assert.True(configuration.LegacyShowDailiesBadge);
        Assert.True(configuration.LegacyShowActivityBadge);
    }

    [Fact]
    public void MigrationCarriesEachDisabledLegacyFlagToTheRealAppId()
    {
        const string json =
            """{"ShowWalletBadge":false,"ShowDailiesBadge":false,"ShowActivityBadge":false}""";
        var configuration = JsonConvert.DeserializeObject<Configuration>(json)!;

        var migrated = configuration.ApplyBadgeSettingsMigration();

        Assert.True(migrated);
        Assert.False(configuration.IsAppBadgeEnabled("wallet"));
        Assert.False(configuration.IsAppBadgeEnabled("dailies"));
        Assert.False(configuration.IsAppBadgeEnabled("character"));
        Assert.True(configuration.BadgeSettingsMigrated);
    }

    [Fact]
    public void MigrationLeavesBadgeSettingsEmptyWhenEveryLegacyFlagWasStillOn()
    {
        const string json = "{}";
        var configuration = JsonConvert.DeserializeObject<Configuration>(json)!;

        var migrated = configuration.ApplyBadgeSettingsMigration();

        Assert.True(migrated);
        Assert.Empty(configuration.BadgeSettings);
        Assert.True(configuration.IsAppBadgeEnabled("wallet"));
        Assert.True(configuration.IsAppBadgeEnabled("dailies"));
        Assert.True(configuration.IsAppBadgeEnabled("character"));
    }

    [Fact]
    public void MigrationDoesNothingOnceAlreadyMarkedMigrated()
    {
        const string json =
            """{"ShowWalletBadge":false,"BadgeSettingsMigrated":true}""";
        var configuration = JsonConvert.DeserializeObject<Configuration>(json)!;

        var migrated = configuration.ApplyBadgeSettingsMigration();

        Assert.False(migrated);
        Assert.True(configuration.IsAppBadgeEnabled("wallet"));
    }
}
