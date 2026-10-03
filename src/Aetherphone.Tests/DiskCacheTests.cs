using System.Security.Cryptography;
using System.Text;
using Aetherphone.Core.Net;
using Xunit;

namespace Aetherphone.Tests;

public sealed class DiskCacheTests : IDisposable
{
    private static readonly TimeSpan AnyAge = TimeSpan.FromDays(1);
    private readonly DirectoryInfo root;

    public DiskCacheTests()
    {
        root = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "aetherphone-diskcache-" + Guid.NewGuid().ToString("N")));
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
    public void A_protected_entry_reads_back_unchanged()
    {
        var cache = new DiskCache(root, 1024 * 1024, protect: true);
        var payload = Encoding.UTF8.GetBytes("a private photo");

        cache.Set("https://example.test/a.png", payload);

        Assert.Equal(payload, cache.Get("https://example.test/a.png", AnyAge));
    }

    [Fact]
    public void A_protected_entry_does_not_hold_the_readable_bytes_on_disk()
    {
        var cache = new DiskCache(root, 1024 * 1024, protect: true);
        var payload = Encoding.UTF8.GetBytes("readable pixels that must not leak");

        cache.Set("https://example.test/a.png", payload);

        var stored = File.ReadAllBytes(SingleEntry().FullName);
        Assert.Equal("AEC1"u8.ToArray(), stored[..4]);
        Assert.Equal(-1, stored.AsSpan().IndexOf(payload));
    }

    [Fact]
    public void A_legacy_readable_file_is_a_miss_and_is_deleted()
    {
        var cache = new DiskCache(root, 1024 * 1024, protect: true);
        const string key = "https://example.test/legacy.png";
        cache.Set(key, Encoding.UTF8.GetBytes("placeholder"));
        var entry = SingleEntry();
        File.WriteAllBytes(entry.FullName, Encoding.UTF8.GetBytes("legacy plaintext image"));

        Assert.Null(cache.Get(key, AnyAge));
        Assert.False(File.Exists(entry.FullName));
    }

    [Fact]
    public void A_tampered_protected_file_is_a_miss_and_is_deleted()
    {
        var cache = new DiskCache(root, 1024 * 1024, protect: true);
        const string key = "https://example.test/tampered.png";
        cache.Set(key, Encoding.UTF8.GetBytes("payload"));
        var entry = SingleEntry();
        var stored = File.ReadAllBytes(entry.FullName);
        stored[^1] ^= 0xFF;
        File.WriteAllBytes(entry.FullName, stored);

        Assert.Null(cache.Get(key, AnyAge));
        Assert.False(File.Exists(entry.FullName));
    }

    [Fact]
    public void Turning_on_protection_purges_the_old_files_once_and_leaves_a_marker()
    {
        root.Create();
        var legacy = Path.Combine(root.FullName, "0123abcd");
        File.WriteAllBytes(legacy, Encoding.UTF8.GetBytes("legacy plaintext image"));

        var cache = new DiskCache(root, 1024 * 1024, protect: true);

        Assert.False(File.Exists(legacy));
        Assert.True(File.Exists(Path.Combine(root.FullName, DiskCache.ProtectedMarkerName)));

        cache.Set("https://example.test/kept.png", Encoding.UTF8.GetBytes("kept"));
        var reopened = new DiskCache(root, 1024 * 1024, protect: true);
        Assert.Equal(Encoding.UTF8.GetBytes("kept"), reopened.Get("https://example.test/kept.png", AnyAge));
    }

    [Fact]
    public void An_unprotected_cache_never_purges()
    {
        root.Create();
        var existing = Path.Combine(root.FullName, "0123abcd");
        File.WriteAllBytes(existing, Encoding.UTF8.GetBytes("public data"));

        _ = new DiskCache(root, 1024 * 1024);

        Assert.True(File.Exists(existing));
        Assert.False(File.Exists(Path.Combine(root.FullName, DiskCache.ProtectedMarkerName)));
    }

    [Fact]
    public void Clear_removes_every_entry_but_keeps_the_marker()
    {
        var cache = new DiskCache(root, 1024 * 1024, protect: true);
        cache.Set("https://example.test/a.png", RandomNumberGenerator.GetBytes(2048));
        cache.Set("https://example.test/b.png", RandomNumberGenerator.GetBytes(2048));
        Assert.True(cache.SizeBytes() > 0);

        cache.Clear();

        Assert.Equal(0, cache.SizeBytes());
        Assert.Null(cache.Get("https://example.test/a.png", AnyAge));
        Assert.True(File.Exists(Path.Combine(root.FullName, DiskCache.ProtectedMarkerName)));
    }

    [Fact]
    public void Size_counts_the_bytes_of_every_entry()
    {
        var cache = new DiskCache(root, 1024 * 1024);
        cache.Set("one", new byte[300]);
        cache.Set("two", new byte[700]);

        Assert.Equal(1000, cache.SizeBytes());
    }

    [Fact]
    public void The_budget_evicts_the_oldest_entries_and_spares_the_marker()
    {
        var cache = new DiskCache(root, 6 * 1024, protect: true);
        cache.Set("oldest", RandomNumberGenerator.GetBytes(2048));
        BackdateEntries(TimeSpan.FromMinutes(10));
        cache.Set("middle", RandomNumberGenerator.GetBytes(2048));
        cache.Set("newest", RandomNumberGenerator.GetBytes(2048));
        cache.Set("latest", RandomNumberGenerator.GetBytes(2048));

        Assert.Null(cache.Get("oldest", AnyAge));
        Assert.NotNull(cache.Get("latest", AnyAge));
        Assert.True(cache.SizeBytes() <= 6 * 1024);
        Assert.True(File.Exists(Path.Combine(root.FullName, DiskCache.ProtectedMarkerName)));
    }

    private void BackdateEntries(TimeSpan age)
    {
        var files = root.GetFiles();
        for (var index = 0; index < files.Length; index++)
        {
            files[index].LastWriteTimeUtc = DateTime.UtcNow - age;
        }
    }

    private FileInfo SingleEntry()
    {
        var files = root.GetFiles();
        FileInfo? entry = null;
        for (var index = 0; index < files.Length; index++)
        {
            if (files[index].Name == DiskCache.ProtectedMarkerName)
            {
                continue;
            }

            Assert.Null(entry);
            entry = files[index];
        }

        Assert.NotNull(entry);
        return entry;
    }
}
