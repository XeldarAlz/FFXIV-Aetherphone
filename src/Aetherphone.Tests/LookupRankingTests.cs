using Aetherphone.Core.Lodestone;
using Xunit;

namespace Aetherphone.Tests;

public sealed class LookupRankingTests
{
    [Fact]
    public void PutsTheExactMatchFirst()
    {
        var matches = new[]
        {
            new CharacterMatch("1", "Aiko Sakura", string.Empty),
            new CharacterMatch("2", "Sakura Aikon", string.Empty),
            new CharacterMatch("3", "Sakura Aiko", string.Empty),
        };

        LookupRanking.SortByRank(matches, "Sakura Aiko");

        Assert.Equal("3", matches[0].Id);
        Assert.Equal("2", matches[1].Id);
        Assert.Equal("1", matches[2].Id);
    }

    [Fact]
    public void KeepsLodestoneOrderWithinARank()
    {
        var matches = new[]
        {
            new CharacterMatch("1", "Purple Sweetheart", string.Empty),
            new CharacterMatch("2", "Lemoncake Sweetheart", string.Empty),
            new CharacterMatch("3", "Sweetheart Pie", string.Empty),
        };

        LookupRanking.SortByRank(matches, "Sweetheart");

        Assert.Equal("3", matches[0].Id);
        Assert.Equal("1", matches[1].Id);
        Assert.Equal("2", matches[2].Id);
    }

    [Fact]
    public void IgnoresCase()
    {
        Assert.Equal(0, LookupRanking.Rank("Sakura Aiko", "sakura aiko"));
    }

    [Fact]
    public void RanksAReversedNameBelowAPrefixMatch()
    {
        Assert.True(LookupRanking.Rank("Aiko Sakura", "Sakura Aiko") > LookupRanking.Rank("Sakura Aikon", "Sakura Aiko"));
    }

    [Fact]
    public void LeavesAnEmptyListAlone()
    {
        var matches = Array.Empty<FreeCompanyMatch>();

        LookupRanking.SortByRank(matches, "Sweetheart");

        Assert.Empty(matches);
    }
}
