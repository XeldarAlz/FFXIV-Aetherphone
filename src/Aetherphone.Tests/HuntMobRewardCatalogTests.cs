using Aetherphone.Core.Hunts;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HuntMobRewardCatalogTests
{
    private static FileInfo Source() => new(Path.Combine(AppContext.BaseDirectory, "Hunts", "HuntMobRewards.json"));

    [Fact]
    public void ResolvesRewardsForAMobTheFileCarries()
    {
        var catalog = new HuntMobRewardCatalog(Source());

        var rewards = catalog.RewardsFor("behemoth");

        Assert.NotEmpty(rewards);
        Assert.Contains(rewards, entry => entry.ItemId == "behemoth_horn");
    }

    [Fact]
    public void MobsSharingAGroupedRewardSetResolveTheSameEntries()
    {
        var catalog = new HuntMobRewardCatalog(Source());

        var first = catalog.RewardsFor("aegeiros");
        var second = catalog.RewardsFor("arch_eta");

        Assert.NotEmpty(first);
        Assert.Equal(first.Select(entry => entry.ItemId), second.Select(entry => entry.ItemId));
    }

    [Fact]
    public void ReturnsEmptyForAMobWithNoRewards()
    {
        var catalog = new HuntMobRewardCatalog(Source());

        var rewards = catalog.RewardsFor("not_a_real_mob_id");

        Assert.Empty(rewards);
    }

    [Fact]
    public void ResolvesItemNameForALocaleTheFileCarries()
    {
        var catalog = new HuntMobRewardCatalog(Source());

        var name = catalog.ItemNameFor("allied_seal", "de");

        Assert.False(string.IsNullOrWhiteSpace(name));
    }

    [Fact]
    public void FallsBackToEnglishItemNameForALocaleTheFileDoesNotCarry()
    {
        var catalog = new HuntMobRewardCatalog(Source());

        var english = catalog.ItemNameFor("allied_seal", "en");
        var spanish = catalog.ItemNameFor("allied_seal", "es");

        Assert.Equal(english, spanish);
    }

    [Fact]
    public void MissingSourceFileDoesNotThrow()
    {
        var missing = new FileInfo(Path.Combine(AppContext.BaseDirectory, "Hunts", "DoesNotExist.json"));
        var catalog = new HuntMobRewardCatalog(missing);

        Assert.Empty(catalog.RewardsFor("behemoth"));
        Assert.Null(catalog.ItemNameFor("allied_seal", "en"));
    }
}
