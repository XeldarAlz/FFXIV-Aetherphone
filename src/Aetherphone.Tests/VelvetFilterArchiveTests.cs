using Aetherphone.Apps.Velvet;
using Aetherphone.Core.Social;
using Xunit;

namespace Aetherphone.Tests;

public sealed class VelvetFilterArchiveTests
{
    private const string AccountId = "acct-123";

    [Fact]
    public void SaveThenLoadRoundTripsEveryField()
    {
        var root = TempRoot();
        try
        {
            var archive = new VelvetFilterArchive(root);
            var filters = new StoredVelvetFilters
            {
                DiscoverInclude = new VelvetFilterPreferences
                {
                    Intent = 0b0001,
                    Gender = 0b0010,
                    Sexuality = 0b0100,
                    Relationship = 0b1000,
                    Region = "NA",
                    Roles = new List<string> { "dom" },
                    Tags = new List<string> { "rp" },
                },
                DiscoverExclude = new VelvetFilterPreferences { Intent = 0b0010 },
                FeedInclude = new VelvetFilterPreferences { Gender = 0b0001 },
                FeedExclude = new VelvetFilterPreferences { Relationship = 0b0100 },
                Mutes = new VelvetFilterPreferences
                {
                    Gender = 0b1000,
                    Kinks = new List<string> { "impact" },
                    Limits = new List<string> { "no marks" },
                },
            };

            Assert.True(archive.Save(AccountId, filters));

            var reopened = new VelvetFilterArchive(root);
            var loaded = reopened.Load(AccountId);

            Assert.Equal(0b0001, loaded.DiscoverInclude.Intent);
            Assert.Equal(0b0010, loaded.DiscoverInclude.Gender);
            Assert.Equal(0b0100, loaded.DiscoverInclude.Sexuality);
            Assert.Equal(0b1000, loaded.DiscoverInclude.Relationship);
            Assert.Equal("NA", loaded.DiscoverInclude.Region);
            Assert.Equal(new List<string> { "dom" }, loaded.DiscoverInclude.Roles);
            Assert.Equal(new List<string> { "rp" }, loaded.DiscoverInclude.Tags);

            Assert.Equal(0b0010, loaded.DiscoverExclude.Intent);
            Assert.Equal(0b0001, loaded.FeedInclude.Gender);
            Assert.Equal(0b0100, loaded.FeedExclude.Relationship);

            Assert.Equal(0b1000, loaded.Mutes.Gender);
            Assert.Equal(new List<string> { "impact" }, loaded.Mutes.Kinks);
            Assert.Equal(new List<string> { "no marks" }, loaded.Mutes.Limits);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public void LoadForUnknownAccountReturnsEmptyDefaults()
    {
        var root = TempRoot();
        try
        {
            var archive = new VelvetFilterArchive(root);
            var loaded = archive.Load("never-saved");

            Assert.Equal(0, loaded.DiscoverInclude.Intent);
            Assert.Empty(loaded.DiscoverInclude.Roles);
            Assert.Empty(loaded.Mutes.Kinks);
            Assert.Empty(loaded.Mutes.Limits);
        }
        finally
        {
            root.Delete(true);
        }
    }

    private static DirectoryInfo TempRoot() =>
        new(Path.Combine(Path.GetTempPath(), "aetherphone-tests-" + Guid.NewGuid().ToString("N")));
}
