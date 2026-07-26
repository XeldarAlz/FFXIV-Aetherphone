using Aetherphone.Core.Lodestone;
using Xunit;

namespace Aetherphone.Tests;

public sealed class LodestoneMatchTests
{
    [Fact]
    public void ReturnsTheExactNameMatch()
    {
        var candidates = new[]
        {
            new LodestoneCandidate("Sakura Aiko", "60312034"),
            new LodestoneCandidate("Aiko Sakura", "2955171"),
        };

        Assert.Equal("60312034", LodestoneMatch.ExactId(candidates, "Sakura Aiko"));
    }

    [Fact]
    public void FindsTheExactMatchBehindOtherResults()
    {
        var candidates = new[]
        {
            new LodestoneCandidate("Aiko Sakura", "2955171"),
            new LodestoneCandidate("Sakura Aiko", "60312034"),
        };

        Assert.Equal("60312034", LodestoneMatch.ExactId(candidates, "Sakura Aiko"));
    }

    [Fact]
    public void NeverAdoptsAReversedName()
    {
        var candidates = new[] { new LodestoneCandidate("Aiko Sakura", "2955171") };

        Assert.Null(LodestoneMatch.ExactId(candidates, "Sakura Aiko"));
    }

    [Fact]
    public void NeverAdoptsAPartialNameMatch()
    {
        var candidates = new[]
        {
            new LodestoneCandidate("Lemoncake Sweetheart", "44672887"),
            new LodestoneCandidate("Purple Sweetheart", "43739616"),
        };

        Assert.Null(LodestoneMatch.ExactId(candidates, "Sweetheart"));
    }

    [Fact]
    public void ReturnsNullWhenNothingMatches()
    {
        Assert.Null(LodestoneMatch.ExactId(Array.Empty<LodestoneCandidate>(), "Sakura Aiko"));
    }

    [Fact]
    public void IgnoresCase()
    {
        var candidates = new[] { new LodestoneCandidate("Sakura Aiko", "60312034") };

        Assert.Equal("60312034", LodestoneMatch.ExactId(candidates, "sakura aiko"));
    }

    [Fact]
    public void SkipsMatchesWithNoId()
    {
        var candidates = new[]
        {
            new LodestoneCandidate("Sakura Aiko", string.Empty),
            new LodestoneCandidate("Sakura Aiko", "60312034"),
        };

        Assert.Equal("60312034", LodestoneMatch.ExactId(candidates, "Sakura Aiko"));
    }
}
