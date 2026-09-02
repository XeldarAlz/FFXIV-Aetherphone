using Aetherphone.Apps.Velvet;
using Aetherphone.Core.Social;
using Xunit;

namespace Aetherphone.Tests;

public sealed class VelvetFilterSelectionTests
{
    [Fact]
    public void MergeIntoUnionsALegacySelectionIntoEveryTarget()
    {
        var legacy = new VelvetFilterSelection();
        legacy.LoadFrom(new VelvetFilterPreferences
        {
            Intent = 0b0001,
            Gender = 0b0010,
            Kinks = new List<string> { "rope" },
            Limits = new List<string> { "no marks" },
        });

        var discoverExclude = new VelvetFilterPreferences { Intent = 0b0100, Kinks = new List<string> { "impact" } };
        var feedExclude = new VelvetFilterPreferences();
        var mutes = new VelvetFilterPreferences();

        legacy.MergeInto(discoverExclude);
        legacy.MergeInto(feedExclude);
        legacy.MergeInto(mutes);

        Assert.Equal(0b0101, discoverExclude.Intent);
        Assert.Equal(0b0010, discoverExclude.Gender);
        Assert.Contains("rope", discoverExclude.Kinks);
        Assert.Contains("impact", discoverExclude.Kinks);
        Assert.Contains("no marks", discoverExclude.Limits);

        Assert.Equal(0b0001, feedExclude.Intent);
        Assert.Equal(0b0010, feedExclude.Gender);
        Assert.Contains("rope", feedExclude.Kinks);
        Assert.Contains("no marks", feedExclude.Limits);

        Assert.Equal(0b0001, mutes.Intent);
        Assert.Equal(0b0010, mutes.Gender);
        Assert.Contains("rope", mutes.Kinks);
        Assert.Contains("no marks", mutes.Limits);
    }

    [Fact]
    public void MergeIntoDoesNotDuplicateTokensOnRepeatedMerge()
    {
        var legacy = new VelvetFilterSelection();
        legacy.LoadFrom(new VelvetFilterPreferences { Kinks = new List<string> { "rope" } });

        var target = new VelvetFilterPreferences();
        legacy.MergeInto(target);
        legacy.MergeInto(target);

        Assert.Single(target.Kinks);
    }
}
