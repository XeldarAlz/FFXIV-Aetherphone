using Aetherphone.Core.Songs;
using Xunit;

namespace Aetherphone.Tests;

public sealed class DownloadStoreFileTests : IDisposable
{
    private const string VideoId = "dQw4w9WgXcQ";

    private readonly string root = Path.Combine(Path.GetTempPath(), "aep-downloads-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void FileNamesFollowTheContainer()
    {
        Assert.Equal(VideoId + ".opus", DownloadStore.FileName(VideoId, true));
        Assert.Equal(VideoId + ".m4a", DownloadStore.FileName(VideoId, false));
    }

    [Fact]
    public void AtomicWriteLeavesOnlyTheFinalFile()
    {
        var path = DownloadStore.WriteAtomic(root, VideoId, [1, 2, 3], true);

        Assert.Equal(Path.Combine(root, VideoId + ".opus"), path);
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(path));
        Assert.Single(Directory.GetFiles(root));
        Assert.Equal(path, DownloadStore.FindFile(root, VideoId));
        Assert.Equal(3, DownloadStore.MeasureFolder(root));
    }

    [Fact]
    public void RewritingInTheOtherFormatReplacesTheOldFile()
    {
        DownloadStore.WriteAtomic(root, VideoId, [1, 2, 3], true);

        var path = DownloadStore.WriteAtomic(root, VideoId, [9, 9], false);

        Assert.Equal(Path.Combine(root, VideoId + ".m4a"), path);
        Assert.Single(Directory.GetFiles(root));
        Assert.Equal(path, DownloadStore.FindFile(root, VideoId));
    }

    [Fact]
    public void DeleteRemovesEveryVariant()
    {
        DownloadStore.WriteAtomic(root, VideoId, [1], true);
        File.WriteAllBytes(Path.Combine(root, VideoId + ".m4a.tmp"), [2]);

        DownloadStore.DeleteFiles(root, VideoId);

        Assert.Empty(Directory.GetFiles(root));
        Assert.Null(DownloadStore.FindFile(root, VideoId));
        Assert.Equal(0, DownloadStore.MeasureFolder(root));
    }

    [Fact]
    public void PathTraversalIdsAreRefused()
    {
        Assert.Throws<ArgumentException>(() => DownloadStore.WriteAtomic(root, "..\\..\\evil", [1], true));
        Assert.Null(DownloadStore.FindFile(root, "../escape00"));
    }
}
