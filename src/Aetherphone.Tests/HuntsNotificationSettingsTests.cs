using Aetherphone.Core.Hunts;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HuntsNotificationSettingsTests
{
    private static HuntMobDefinition MobWith(string rank, string expansionId) =>
        new() { Id = "test_mob", Rank = rank, ExpansionId = expansionId };

    [Fact]
    public void DefaultsNotifyForSAndFateOnlyAndEveryWorld()
    {
        var settings = new HuntsNotificationSettings();

        Assert.True(settings.RankF);
        Assert.False(settings.RankA);
        Assert.False(settings.RankB);
        Assert.True(settings.RankS);
        Assert.True(settings.IsWorldEnabled("adamantoise"));

        Assert.True(settings.IsEnabledFor(MobWith("S", "dawntrail"), "adamantoise"));
        Assert.True(settings.IsEnabledFor(MobWith("SS", "dawntrail"), "adamantoise"));
        Assert.True(settings.IsEnabledFor(MobWith("F", "dawntrail"), "adamantoise"));
        Assert.False(settings.IsEnabledFor(MobWith("A", "dawntrail"), "adamantoise"));
        Assert.False(settings.IsEnabledFor(MobWith("B", "dawntrail"), "adamantoise"));
    }

    [Fact]
    public void ResetToDefaultUndoesEveryToggle()
    {
        var settings = new HuntsNotificationSettings();
        settings.RankS = false;
        settings.RankA = true;
        settings.ToggleExpansion(0);
        settings.ToggleWorld("goblin");

        settings.ResetToDefault();

        Assert.True(settings.RankF);
        Assert.False(settings.RankA);
        Assert.False(settings.RankB);
        Assert.True(settings.RankS);
        Assert.True(settings.IsExpansionActive(0));
        Assert.True(settings.IsWorldEnabled("goblin"));
    }

    [Fact]
    public void ToggleWorldMutesOnlyThatWorld()
    {
        var settings = new HuntsNotificationSettings();
        settings.ToggleWorld("goblin");

        Assert.False(settings.IsWorldEnabled("goblin"));
        Assert.True(settings.IsWorldEnabled("adamantoise"));
        Assert.False(settings.IsEnabledFor(MobWith("S", "dawntrail"), "goblin"));
        Assert.True(settings.IsEnabledFor(MobWith("S", "dawntrail"), "adamantoise"));
    }

    [Fact]
    public void ToggleExpansionMutesOnlyThatExpansion()
    {
        var settings = new HuntsNotificationSettings();
        var dawntrailIndex = Array.IndexOf(HuntExpansions.Ids, "dawntrail");
        settings.ToggleExpansion(dawntrailIndex);

        Assert.False(settings.IsExpansionActive(dawntrailIndex));
        Assert.False(settings.IsEnabledFor(MobWith("S", "dawntrail"), "adamantoise"));
        Assert.True(settings.IsEnabledFor(MobWith("S", "endwalker"), "adamantoise"));
    }

    [Fact]
    public void ApplySnapshotRestoresEveryFieldToSnapshotCaptured()
    {
        var original = new HuntsNotificationSettings();
        original.RankS = false;
        original.RankF = false;
        original.ToggleExpansion(0);
        original.ToggleWorld("goblin");

        var restored = new HuntsNotificationSettings();
        restored.ApplySnapshot(original.ToSnapshot());

        Assert.Equal(original.RankF, restored.RankF);
        Assert.Equal(original.RankB, restored.RankB);
        Assert.Equal(original.RankA, restored.RankA);
        Assert.Equal(original.RankS, restored.RankS);
        Assert.False(restored.IsExpansionActive(0));
        Assert.False(restored.IsWorldEnabled("goblin"));
        Assert.True(restored.IsWorldEnabled("adamantoise"));
    }

    [Fact]
    public void ApplySnapshotToleratesAShorterExpansionListThanTheLiveOneHas()
    {
        var restored = new HuntsNotificationSettings();
        var snapshot = new HuntsNotificationSnapshot { ExpansionActive = { false } };

        restored.ApplySnapshot(snapshot);

        Assert.False(restored.IsExpansionActive(0));
        for (var index = 1; index < HuntExpansions.Ids.Length; index++)
        {
            Assert.True(restored.IsExpansionActive(index));
        }
    }

    [Fact]
    public void IsEnabledForFailsOpenWhenTheMobIsNotInTheCatalog() =>
        Assert.True(new HuntsNotificationSettings().IsEnabledFor(null, "adamantoise"));

    [Fact]
    public void DefaultMobOverrideFallsThroughToTheGlobalRules()
    {
        var settings = new HuntsNotificationSettings();

        Assert.Equal(HuntMobNotificationMode.Default, settings.MobOverrideModeFor("test_mob"));
        Assert.False(settings.IsEnabledFor(MobWith("A", "dawntrail"), "adamantoise"));
    }

    [Fact]
    public void EnabledMobOverrideNotifiesRegardlessOfRankOrWorld()
    {
        var settings = new HuntsNotificationSettings();
        settings.ToggleWorld("goblin");
        settings.SetMobOverride("test_mob", HuntMobNotificationMode.Enabled, null);

        Assert.True(settings.IsEnabledFor(MobWith("A", "dawntrail"), "goblin"));
    }

    [Fact]
    public void DisabledMobOverrideSilencesRegardlessOfRankOrWorld()
    {
        var settings = new HuntsNotificationSettings();
        settings.SetMobOverride("test_mob", HuntMobNotificationMode.Disabled, null);

        Assert.False(settings.IsEnabledFor(MobWith("S", "dawntrail"), "adamantoise"));
    }

    [Fact]
    public void EnabledOnWorldMobOverrideOnlyNotifiesForItsOwnWorld()
    {
        var settings = new HuntsNotificationSettings();
        settings.SetMobOverride("test_mob", HuntMobNotificationMode.EnabledOnWorld, "goblin");

        Assert.True(settings.IsEnabledFor(MobWith("A", "dawntrail"), "goblin"));
        Assert.False(settings.IsEnabledFor(MobWith("A", "dawntrail"), "adamantoise"));
        Assert.Equal("goblin", settings.MobOverrideWorldFor("test_mob"));
    }

    [Fact]
    public void SettingTheOverrideBackToDefaultClearsIt()
    {
        var settings = new HuntsNotificationSettings();
        settings.SetMobOverride("test_mob", HuntMobNotificationMode.Disabled, null);

        settings.SetMobOverride("test_mob", HuntMobNotificationMode.Default, null);

        Assert.Equal(HuntMobNotificationMode.Default, settings.MobOverrideModeFor("test_mob"));
        Assert.True(settings.IsEnabledFor(MobWith("S", "dawntrail"), "adamantoise"));
    }

    [Fact]
    public void ApplySnapshotRestoresMobOverridesIncludingTheirWorld()
    {
        var original = new HuntsNotificationSettings();
        original.SetMobOverride("scheduled_mob", HuntMobNotificationMode.EnabledOnWorld, "goblin");
        original.SetMobOverride("muted_mob", HuntMobNotificationMode.Disabled, null);

        var restored = new HuntsNotificationSettings();
        restored.ApplySnapshot(original.ToSnapshot());

        Assert.Equal(HuntMobNotificationMode.EnabledOnWorld, restored.MobOverrideModeFor("scheduled_mob"));
        Assert.Equal("goblin", restored.MobOverrideWorldFor("scheduled_mob"));
        Assert.Equal(HuntMobNotificationMode.Disabled, restored.MobOverrideModeFor("muted_mob"));
    }

    [Fact]
    public void CollectMobOverridesOmitsDefaultAndReflectsCount()
    {
        var settings = new HuntsNotificationSettings();
        settings.SetMobOverride("scheduled_mob", HuntMobNotificationMode.EnabledOnWorld, "goblin");
        settings.SetMobOverride("muted_mob", HuntMobNotificationMode.Disabled, null);
        settings.SetMobOverride("untouched_mob", HuntMobNotificationMode.Default, null);

        Assert.Equal(2, settings.MobOverrideCount);

        var entries = new List<HuntMobOverrideEntry>();
        settings.CollectMobOverrides(entries);

        Assert.Equal(2, entries.Count);
        Assert.DoesNotContain(entries, entry => entry.MobId == "untouched_mob");
        Assert.Contains(entries, entry => entry.MobId == "scheduled_mob" &&
            entry.Mode == HuntMobNotificationMode.EnabledOnWorld && entry.WorldId == "goblin");
        Assert.Contains(entries, entry => entry.MobId == "muted_mob" &&
            entry.Mode == HuntMobNotificationMode.Disabled);
    }

    [Fact]
    public void CollectMobOverridesReusesTheGivenListAcrossCalls()
    {
        var settings = new HuntsNotificationSettings();
        settings.SetMobOverride("first_mob", HuntMobNotificationMode.Enabled, null);
        var entries = new List<HuntMobOverrideEntry>();

        settings.CollectMobOverrides(entries);
        Assert.Single(entries);

        settings.SetMobOverride("first_mob", HuntMobNotificationMode.Default, null);
        settings.CollectMobOverrides(entries);

        Assert.Empty(entries);
    }

    [Fact]
    public void SetMobOverrideRaisesChangedExactlyOnce()
    {
        var settings = new HuntsNotificationSettings();
        var raised = 0;
        settings.Changed += () => raised++;

        settings.SetMobOverride("test_mob", HuntMobNotificationMode.Enabled, null);

        Assert.Equal(1, raised);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void EveryMutatorRaisesChangedExactlyOnce(int mutatorIndex)
    {
        var settings = new HuntsNotificationSettings();
        var raised = 0;
        settings.Changed += () => raised++;

        switch (mutatorIndex)
        {
            case 0:
                settings.RankF = false;
                break;
            case 1:
                settings.RankA = true;
                break;
            case 2:
                settings.ToggleExpansion(0);
                break;
            case 3:
                settings.ToggleWorld("goblin");
                break;
            case 4:
                settings.ApplySnapshot(new HuntsNotificationSnapshot());
                break;
            case 5:
                settings.ResetToDefault();
                break;
        }

        Assert.Equal(1, raised);
    }
}
