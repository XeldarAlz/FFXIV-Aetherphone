using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class AppIconCacheOverrideTests : IDisposable
{
    private const string AppId = "loveisland";
    private readonly DirectoryInfo root;
    private readonly string bundled;
    private readonly string overrideDirectory;

    public AppIconCacheOverrideTests()
    {
        root = new DirectoryInfo(Path.Combine(Path.GetTempPath(),
            "aetherphone-icon-override-" + Guid.NewGuid().ToString("N")));
        bundled = Path.Combine(root.FullName, "Icons");
        overrideDirectory = Path.Combine(root.FullName, "campaign-icons");
        WritePair(bundled, AppId, true, true);
    }

    public void Dispose()
    {
        root.Refresh();
        if (root.Exists)
        {
            root.Delete(true);
        }
    }

    [Fact]
    public void An_override_directory_with_both_files_wins_over_the_bundle()
    {
        WritePair(overrideDirectory, AppId, true, true);
        var source = new IconPairSource(bundled);

        Assert.True(source.TryUseOverride(AppId, overrideDirectory));

        Assert.True(source.HasOverride(AppId));
        Assert.Equal(overrideDirectory, source.DirectoryFor(AppId));
        Assert.Equal(Path.Combine(overrideDirectory, AppId + IconPairSource.FinishedSuffix),
            IconPairSource.FinishedPath(source.DirectoryFor(AppId), AppId));
        Assert.Equal(Path.Combine(overrideDirectory, AppId + IconPairSource.ForegroundSuffix),
            IconPairSource.ForegroundPath(source.DirectoryFor(AppId), AppId));
    }

    [Fact]
    public void Clearing_the_override_falls_back_to_the_bundled_pair()
    {
        WritePair(overrideDirectory, AppId, true, true);
        var source = new IconPairSource(bundled);
        source.TryUseOverride(AppId, overrideDirectory);

        Assert.True(source.ClearOverride(AppId));

        Assert.False(source.HasOverride(AppId));
        Assert.Equal(bundled, source.DirectoryFor(AppId));
        Assert.True(IconPairSource.PairExists(source.DirectoryFor(AppId), AppId));
        Assert.False(source.ClearOverride(AppId));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void A_directory_missing_part_of_the_pair_is_refused(bool finished, bool foreground)
    {
        WritePair(overrideDirectory, AppId, finished, foreground);
        var source = new IconPairSource(bundled);

        Assert.False(source.TryUseOverride(AppId, overrideDirectory));

        Assert.False(source.HasOverride(AppId));
        Assert.Equal(bundled, source.DirectoryFor(AppId));
    }

    [Fact]
    public void An_override_only_affects_its_own_app()
    {
        WritePair(overrideDirectory, AppId, true, true);
        var source = new IconPairSource(bundled);
        source.TryUseOverride(AppId, overrideDirectory);

        Assert.Equal(bundled, source.DirectoryFor("announcements"));
    }

    private static void WritePair(string directory, string appId, bool finished, bool foreground)
    {
        Directory.CreateDirectory(directory);
        if (finished)
        {
            File.WriteAllBytes(IconPairSource.FinishedPath(directory, appId), new byte[] { 1 });
        }

        if (foreground)
        {
            File.WriteAllBytes(IconPairSource.ForegroundPath(directory, appId), new byte[] { 1 });
        }
    }
}
