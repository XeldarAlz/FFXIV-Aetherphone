using Aetherphone.Core.Lodestone;
using Xunit;

namespace Aetherphone.Tests;

public sealed class LookupKeysTests
{
    [Fact]
    public void UsesTheLodestoneIdWhenOneIsKnown()
    {
        Assert.Equal("60312034", LookupKeys.CharacterDetail("60312034", "Sakura Aiko", "Phoenix"));
    }

    [Fact]
    public void IgnoresTheNameAndWorldOnceAnIdIsKnown()
    {
        var fromSearch = LookupKeys.CharacterDetail("60312034", "Sakura Aiko", "Phoenix");
        var fromRoster = LookupKeys.CharacterDetail("60312034", string.Empty, string.Empty);

        Assert.Equal(fromSearch, fromRoster);
    }

    [Fact]
    public void KeysAnIdLessLookupByNameAndWorld()
    {
        Assert.Equal("Sakura Aiko@Phoenix", LookupKeys.CharacterDetail(string.Empty, "Sakura Aiko", "Phoenix"));
    }

    [Fact]
    public void NeverCollapsesTwoFriendsOntoOneEntry()
    {
        var first = LookupKeys.CharacterDetail(string.Empty, "Sakura Aiko", "Phoenix");
        var second = LookupKeys.CharacterDetail(string.Empty, "Purple Sweetheart", "Phoenix");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void KeepsTheSameNameApartAcrossWorlds()
    {
        var phoenix = LookupKeys.CharacterDetail(string.Empty, "Sakura Aiko", "Phoenix");
        var odin = LookupKeys.CharacterDetail(string.Empty, "Sakura Aiko", "Odin");

        Assert.NotEqual(phoenix, odin);
    }

    [Fact]
    public void TrimsEveryPart()
    {
        Assert.Equal("60312034", LookupKeys.CharacterDetail(" 60312034 ", "Sakura Aiko", "Phoenix"));
        Assert.Equal("Sakura Aiko@Phoenix", LookupKeys.CharacterDetail("  ", " Sakura Aiko ", " Phoenix "));
    }
}
