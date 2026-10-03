using Aetherphone.Core.Songs;
using Xunit;

namespace Aetherphone.Tests;

public sealed class MusicCatalogTests : IDisposable
{
    private const string Key = "new.releases";
    private readonly DirectoryInfo root;
    private long now = 1_000_000;
    private int searches;
    private int mixes;
    private Func<string, Task<Song[]>> searchResult;

    public MusicCatalogTests()
    {
        root = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "aetherphone-catalog-" + Guid.NewGuid().ToString("N")));
        searchResult = query => Task.FromResult(Songs(query, 3));
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
    public async Task A_fresh_shelf_is_fetched_once_within_its_time_to_live()
    {
        using var catalog = Create(null);
        var request = CatalogRequest.Query("lofi");

        Assert.True(catalog.Ensure(Key, request));
        await catalog.Pending(Key);
        Assert.False(catalog.Ensure(Key, request));

        Assert.Equal(1, searches);
        Assert.Equal(3, catalog.Songs(Key).Length);
        Assert.Equal(CatalogState.Ready, catalog.State(Key));
    }

    [Fact]
    public async Task A_stale_shelf_keeps_serving_its_songs_while_it_revalidates()
    {
        using var catalog = Create(null);
        var request = CatalogRequest.Query("lofi");
        catalog.Ensure(Key, request);
        await catalog.Pending(Key);
        var first = catalog.Songs(Key);
        var gate = new TaskCompletionSource<Song[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        searchResult = _ => gate.Task;
        now += MusicCatalog.ShelfTtlSeconds + 1;

        Assert.True(catalog.Ensure(Key, request));
        Assert.Same(first, catalog.Songs(Key));
        Assert.Equal(CatalogState.Ready, catalog.State(Key));

        gate.SetResult(Songs("fresh", 5));
        await catalog.Pending(Key);
        Assert.Equal(5, catalog.Songs(Key).Length);
    }

    [Fact]
    public async Task A_failed_fetch_backs_off_before_trying_again()
    {
        searchResult = _ => Task.FromResult(Array.Empty<Song>());
        using var catalog = Create(null);
        var request = CatalogRequest.Query("nothing");
        catalog.Ensure(Key, request);
        await catalog.Pending(Key);

        Assert.Equal(CatalogState.Failed, catalog.State(Key));
        Assert.False(catalog.Ensure(Key, request));
        now += MusicCatalog.RetryBackoffSeconds + 1;
        Assert.True(catalog.Ensure(Key, request));
        await catalog.Pending(Key);
        Assert.Equal(2, searches);
    }

    [Fact]
    public async Task A_new_argument_refetches_even_inside_the_time_to_live()
    {
        using var catalog = Create(null);
        catalog.Ensure(Key, CatalogRequest.Query("lofi"));
        await catalog.Pending(Key);

        Assert.True(catalog.Ensure(Key, CatalogRequest.Query("jazz")));
        await catalog.Pending(Key);

        Assert.Equal(2, searches);
        Assert.StartsWith("jazz", catalog.Songs(Key)[0].Title, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_failed_fetch_for_a_new_argument_drops_the_old_songs_and_retries_after_backoff()
    {
        using var catalog = Create(null);
        catalog.Ensure(Key, CatalogRequest.Query("lofi"));
        await catalog.Pending(Key);
        searchResult = _ => Task.FromResult(Array.Empty<Song>());

        Assert.True(catalog.Ensure(Key, CatalogRequest.Query("jazz")));
        await catalog.Pending(Key);

        Assert.Empty(catalog.Songs(Key));
        Assert.Equal(CatalogState.Failed, catalog.State(Key));
        searchResult = query => Task.FromResult(Songs(query, 3));
        now += MusicCatalog.RetryBackoffSeconds + 1;
        Assert.True(catalog.Ensure(Key, CatalogRequest.Query("jazz")));
        await catalog.Pending(Key);
        Assert.StartsWith("jazz", catalog.Songs(Key)[0].Title, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Persisted_shelves_load_from_disk_without_a_network_call()
    {
        var path = Path.Combine(root.FullName, "catalog.json");
        root.Create();
        using (var first = Create(path))
        {
            await first.Ready;
            first.Ensure(Key, CatalogRequest.Query("lofi"));
            await first.Pending(Key);
            first.Flush();
        }

        searches = 0;
        using var second = Create(path);
        await second.Ready;

        Assert.Equal(3, second.Songs(Key).Length);
        Assert.False(second.Ensure(Key, CatalogRequest.Query("lofi")));
        Assert.Equal(0, searches);
    }

    [Fact]
    public async Task Imported_playlists_stay_in_memory_only()
    {
        var path = Path.Combine(root.FullName, "catalog.json");
        root.Create();
        const string playlistKey = "playlist.PL123";
        using (var first = Create(path))
        {
            await first.Ready;
            first.Ensure(playlistKey, CatalogRequest.Playlist("https://www.youtube.com/playlist?list=PL123"));
            await first.Pending(playlistKey);
            Assert.Equal("Road trip", first.Title(playlistKey));
            first.Flush();
        }

        using var second = Create(path);
        await second.Ready;
        Assert.Empty(second.Songs(playlistKey));
    }

    [Fact]
    public async Task A_discovery_mix_skips_songs_the_listener_already_knows()
    {
        using var catalog = Create(null);
        var known = new HashSet<string>(StringComparer.Ordinal) { "seedA-0", "seedB-1" };
        var request = CatalogRequest.Discovery("day:seedA,seedB", ["seedA", "seedB"], known);

        catalog.Ensure("mix.discovery", request);
        await catalog.Pending("mix.discovery");

        var songs = catalog.Songs("mix.discovery");
        Assert.Equal(2, mixes);
        Assert.DoesNotContain(songs, song => known.Contains(song.VideoId));
        Assert.Equal("seedB-0", songs[0].VideoId);
        Assert.Equal("seedA-1", songs[1].VideoId);
    }

    private MusicCatalog Create(string? path)
    {
        return new MusicCatalog(path,
            (query, _) =>
            {
                Interlocked.Increment(ref searches);
                return searchResult(query);
            },
            (videoId, max, _) =>
            {
                Interlocked.Increment(ref mixes);
                return Songs(videoId, 4);
            },
            (url, max, _) => new CatalogList("Road trip", Songs("track", 2)),
            () => now);
    }

    private static Song[] Songs(string prefix, int count)
    {
        var songs = new Song[count];
        for (var index = 0; index < count; index++)
        {
            songs[index] = new Song(prefix + "-" + index, prefix + " song " + index, "Artist", string.Empty, 200,
                "channel");
        }

        return songs;
    }
}
