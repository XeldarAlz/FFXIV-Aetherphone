using Aetherphone.Core.Hunts;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HuntMobTextCatalogTests
{
    private static FileInfo DescriptionsSource() =>
        new(Path.Combine(AppContext.BaseDirectory, "Hunts", "HuntMobDescriptions.json"));

    private static FileInfo TipsSource() =>
        new(Path.Combine(AppContext.BaseDirectory, "Hunts", "HuntMobTips.json"));

    [Fact]
    public void ResolvesTextForALocaleTheFileActuallyCarries()
    {
        var catalog = new HuntMobTextCatalog(DescriptionsSource());

        var text = catalog.TextFor("forneus", "de");

        Assert.False(string.IsNullOrWhiteSpace(text));
    }

    [Fact]
    public void FallsBackToEnglishForALocaleTheFileDoesNotCarry()
    {
        var catalog = new HuntMobTextCatalog(DescriptionsSource());

        var english = catalog.TextFor("forneus", "en");
        var spanish = catalog.TextFor("forneus", "es");

        Assert.Equal(english, spanish);
    }

    [Fact]
    public void HasNativeTextIsFalseOnlyForTheEnglishFallback()
    {
        var catalog = new HuntMobTextCatalog(DescriptionsSource());

        Assert.True(catalog.HasNativeText("forneus", "de"));
        Assert.False(catalog.HasNativeText("forneus", "es"));
    }

    [Fact]
    public void ReturnsNullForAMobIdTheFileDoesNotHave()
    {
        var catalog = new HuntMobTextCatalog(DescriptionsSource());

        var text = catalog.TextFor("not_a_real_mob_id", "en");

        Assert.Null(text);
    }

    [Fact]
    public void MissingSourceFileDoesNotThrow()
    {
        var missing = new FileInfo(Path.Combine(AppContext.BaseDirectory, "Hunts", "DoesNotExist.json"));
        var catalog = new HuntMobTextCatalog(missing);

        var text = catalog.TextFor("forneus", "en");

        Assert.Null(text);
    }

    [Fact]
    public void ResolvesTipsFromTheirOwnFile()
    {
        var catalog = new HuntMobTextCatalog(TipsSource());

        var text = catalog.TextFor("ker", "de");

        Assert.False(string.IsNullOrWhiteSpace(text));
        Assert.Null(catalog.TextFor("forneus", "en"));
    }
}
