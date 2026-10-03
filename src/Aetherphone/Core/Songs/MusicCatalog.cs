using Newtonsoft.Json;

namespace Aetherphone.Core.Songs;

internal enum CatalogSource : byte
{
    Query,
    Mix,
    Playlist,
    Discovery,
}

internal enum CatalogState : byte
{
    Missing,
    Loading,
    Ready,
    Failed,
}

internal readonly struct CatalogRequest
{
    public readonly CatalogSource Source;
    public readonly string Argument;
    public readonly string[] Seeds;
    public readonly HashSet<string>? Known;
    public readonly long TimeToLiveSeconds;
    public readonly bool Persist;

    private CatalogRequest(CatalogSource source, string argument, string[] seeds, HashSet<string>? known,
        long timeToLiveSeconds, bool persist)
    {
        Source = source;
        Argument = argument;
        Seeds = seeds;
        Known = known;
        TimeToLiveSeconds = timeToLiveSeconds;
        Persist = persist;
    }

    public bool IsEmpty => Argument is null || Argument.Length == 0;

    public static CatalogRequest Query(string query, long timeToLiveSeconds = MusicCatalog.ShelfTtlSeconds) =>
        new(CatalogSource.Query, query, Array.Empty<string>(), null, timeToLiveSeconds, true);

    public static CatalogRequest Mix(string videoId) =>
        new(CatalogSource.Mix, videoId, Array.Empty<string>(), null, MusicCatalog.MixTtlSeconds, true);

    public static CatalogRequest Playlist(string url) =>
        new(CatalogSource.Playlist, url, Array.Empty<string>(), null, MusicCatalog.ShelfTtlSeconds, false);

    public static CatalogRequest Discovery(string signature, string[] seeds, HashSet<string> known) =>
        new(CatalogSource.Discovery, signature, seeds, known, MusicCatalog.MixTtlSeconds, true);
}

internal readonly record struct CatalogList(string Title, Song[] Songs);

internal sealed class MusicCatalog : IDisposable
{
    public const long ShelfTtlSeconds = 6 * 60 * 60;
    public const long MixTtlSeconds = 24 * 60 * 60;
    public const long RetryBackoffSeconds = 5 * 60;
    public const int MixSize = 50;
    public const int SeedMixSize = 25;
    public const int DiscoverySize = 40;
    public const int PlaylistSize = 200;
    private const int MaxConcurrentFetches = 2;
    private const int SaveDelayMilliseconds = 2000;
    private const int FileVersion = 1;
    private const string FileName = "catalog.json";

    private readonly Dictionary<string, CatalogEntry> entries = new(StringComparer.Ordinal);
    private readonly object gate = new();
    private readonly object writeGate = new();
    private readonly Func<string, CancellationToken, Task<Song[]>> search;
    private readonly Func<string, int, CancellationToken, Song[]> mix;
    private readonly Func<string, int, CancellationToken, CatalogList> playlist;
    private readonly Func<long> clock;
    private readonly string? path;
    private readonly SemaphoreSlim fetchSlots = new(MaxConcurrentFetches);
    private readonly CancellationTokenSource lifetime = new();
    private readonly Timer? saveTimer;
    private volatile bool loaded;
    private bool dirty;
    private int version;

    public MusicCatalog(string? path, Func<string, CancellationToken, Task<Song[]>> search,
        Func<string, int, CancellationToken, Song[]> mix, Func<string, int, CancellationToken, CatalogList> playlist,
        Func<long>? clock = null)
    {
        this.path = path;
        this.search = search;
        this.mix = mix;
        this.playlist = playlist;
        this.clock = clock ?? DefaultClock;
        if (path is null)
        {
            loaded = true;
            Ready = Task.CompletedTask;
            return;
        }

        saveTimer = new Timer(_ => Flush(), null, Timeout.Infinite, Timeout.Infinite);
        Ready = Task.Run(LoadFromDisk);
    }

    public Task Ready { get; }

    public int Version => Volatile.Read(ref version);

    public static MusicCatalog Create(DirectoryInfo directory, SongSearchService songSearch,
        SongLinkResolver resolver)
    {
        if (!directory.Exists)
        {
            directory.Create();
        }

        return new MusicCatalog(Path.Combine(directory.FullName, FileName),
            (query, token) => songSearch.SearchAsync(query, SongSearchScope.Songs, token),
            (videoId, max, token) => ToSongs(resolver.FetchMix(videoId, max, token)),
            (url, max, token) => ToList(resolver.FetchPlaylist(url, max, token)));
    }

    public Song[] Songs(string key)
    {
        lock (gate)
        {
            return entries.TryGetValue(key, out var entry) ? entry.Songs : Array.Empty<Song>();
        }
    }

    public string Title(string key)
    {
        lock (gate)
        {
            return entries.TryGetValue(key, out var entry) ? entry.Title : string.Empty;
        }
    }

    public CatalogState State(string key)
    {
        lock (gate)
        {
            if (!entries.TryGetValue(key, out var entry))
            {
                return loaded ? CatalogState.Missing : CatalogState.Loading;
            }

            if (entry.Songs.Length > 0)
            {
                return CatalogState.Ready;
            }

            if (entry.Loading)
            {
                return CatalogState.Loading;
            }

            return entry.Failed ? CatalogState.Failed : CatalogState.Missing;
        }
    }

    public bool Ensure(string key, in CatalogRequest request) => Start(key, request, false);

    public bool Refresh(string key, in CatalogRequest request) => Start(key, request, true);

    internal Task Pending(string key)
    {
        lock (gate)
        {
            return entries.TryGetValue(key, out var entry) && entry.Pending is { } pending
                ? pending
                : Task.CompletedTask;
        }
    }

    public void Flush()
    {
        if (path is null)
        {
            return;
        }

        lock (writeGate)
        {
            string json;
            lock (gate)
            {
                if (!dirty)
                {
                    return;
                }

                dirty = false;
                json = JsonConvert.SerializeObject(Snapshot());
            }

            try
            {
                var temp = path + ".tmp";
                File.WriteAllText(temp, json);
                File.Move(temp, path, true);
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "Music catalog write failed");
                lock (gate)
                {
                    dirty = true;
                }
            }
        }
    }

    public void Dispose()
    {
        lifetime.Cancel();
        saveTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        Flush();
        saveTimer?.Dispose();
        lifetime.Dispose();
    }

    private bool Start(string key, in CatalogRequest request, bool force)
    {
        if (request.IsEmpty || !loaded || lifetime.IsCancellationRequested)
        {
            return false;
        }

        var now = clock();
        CatalogEntry entry;
        lock (gate)
        {
            if (!entries.TryGetValue(key, out var existing))
            {
                existing = new CatalogEntry();
                entries[key] = existing;
            }

            entry = existing;
            if (entry.Loading)
            {
                return false;
            }

            var matches = string.Equals(entry.Argument, request.Argument, StringComparison.Ordinal);
            if (!force && matches && entry.Songs.Length > 0 && now - entry.FetchedUnix < request.TimeToLiveSeconds)
            {
                return false;
            }

            if (!force && matches && now < entry.RetryAfterUnix)
            {
                return false;
            }

            entry.Loading = true;
        }

        var captured = request;
        var task = Task.Run(() => RunFetchAsync(key, captured));
        lock (gate)
        {
            if (entry.Loading)
            {
                entry.Pending = task;
            }
        }

        return true;
    }

    private async Task RunFetchAsync(string key, CatalogRequest request)
    {
        var result = new CatalogList(string.Empty, Array.Empty<Song>());
        var succeeded = false;
        try
        {
            var token = lifetime.Token;
            await fetchSlots.WaitAsync(token).ConfigureAwait(false);
            try
            {
                result = await FetchAsync(request, token).ConfigureAwait(false);
                succeeded = true;
            }
            finally
            {
                fetchSlots.Release();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"Music catalog fetch failed for {key}");
        }

        Complete(key, request, result, succeeded);
    }

    private async Task<CatalogList> FetchAsync(CatalogRequest request, CancellationToken token)
    {
        switch (request.Source)
        {
            case CatalogSource.Query:
                return new CatalogList(string.Empty,
                    await search(request.Argument, token).ConfigureAwait(false) ?? Array.Empty<Song>());
            case CatalogSource.Mix:
                return new CatalogList(string.Empty,
                    await Task.Run(() => mix(request.Argument, MixSize, token), token).ConfigureAwait(false));
            case CatalogSource.Playlist:
                return await Task.Run(() => playlist(request.Argument, PlaylistSize, token), token)
                    .ConfigureAwait(false);
            default:
                return new CatalogList(string.Empty, await FetchDiscoveryAsync(request, token).ConfigureAwait(false));
        }
    }

    private async Task<Song[]> FetchDiscoveryAsync(CatalogRequest request, CancellationToken token)
    {
        var seeds = request.Seeds;
        var mixes = new Song[seeds.Length][];
        for (var seedIndex = 0; seedIndex < seeds.Length; seedIndex++)
        {
            var seed = seeds[seedIndex];
            mixes[seedIndex] = await Task.Run(() => mix(seed, SeedMixSize, token), token).ConfigureAwait(false);
        }

        return MusicMixRules.Discovery(mixes, request.Known ?? new HashSet<string>(StringComparer.Ordinal),
            DiscoverySize);
    }

    private void Complete(string key, in CatalogRequest request, in CatalogList result, bool succeeded)
    {
        var now = clock();
        var songs = result.Songs ?? Array.Empty<Song>();
        var persist = false;
        lock (gate)
        {
            if (!entries.TryGetValue(key, out var entry))
            {
                return;
            }

            entry.Loading = false;
            entry.Pending = null;
            if (succeeded && songs.Length > 0)
            {
                entry.Songs = songs;
                entry.Title = result.Title ?? string.Empty;
                entry.Argument = request.Argument;
                entry.FetchedUnix = now;
                entry.RetryAfterUnix = 0;
                entry.Failed = false;
                entry.Persist = request.Persist;
                persist = request.Persist;
            }
            else
            {
                if (!string.Equals(entry.Argument, request.Argument, StringComparison.Ordinal))
                {
                    entry.Songs = Array.Empty<Song>();
                    entry.Title = string.Empty;
                }

                entry.Argument = request.Argument;
                entry.RetryAfterUnix = now + RetryBackoffSeconds;
                entry.Failed = true;
            }

            version++;
            if (persist)
            {
                dirty = true;
            }
        }

        if (persist && !lifetime.IsCancellationRequested)
        {
            saveTimer?.Change(SaveDelayMilliseconds, Timeout.Infinite);
        }
    }

    private CatalogFile Snapshot()
    {
        var file = new CatalogFile { Version = FileVersion };
        foreach (var pair in entries)
        {
            var entry = pair.Value;
            if (!entry.Persist || entry.Songs.Length == 0)
            {
                continue;
            }

            var records = new List<SongRecord>(entry.Songs.Length);
            for (var songIndex = 0; songIndex < entry.Songs.Length; songIndex++)
            {
                records.Add(SongRecord.From(entry.Songs[songIndex]));
            }

            file.Entries.Add(new CatalogFileEntry
            {
                Key = pair.Key,
                Title = entry.Title,
                Argument = entry.Argument,
                FetchedUnix = entry.FetchedUnix,
                Songs = records,
            });
        }

        return file;
    }

    private void LoadFromDisk()
    {
        try
        {
            if (path is null || !File.Exists(path))
            {
                return;
            }

            var file = JsonConvert.DeserializeObject<CatalogFile>(File.ReadAllText(path));
            if (file is null || file.Version != FileVersion)
            {
                return;
            }

            lock (gate)
            {
                for (var entryIndex = 0; entryIndex < file.Entries.Count; entryIndex++)
                {
                    var stored = file.Entries[entryIndex];
                    if (string.IsNullOrEmpty(stored.Key) || entries.ContainsKey(stored.Key))
                    {
                        continue;
                    }

                    var songs = new Song[stored.Songs.Count];
                    for (var songIndex = 0; songIndex < songs.Length; songIndex++)
                    {
                        songs[songIndex] = stored.Songs[songIndex].ToSong();
                    }

                    entries[stored.Key] = new CatalogEntry
                    {
                        Songs = songs,
                        Title = stored.Title ?? string.Empty,
                        Argument = stored.Argument ?? string.Empty,
                        FetchedUnix = stored.FetchedUnix,
                        Persist = true,
                    };
                }

                version++;
            }
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "Music catalog read failed");
        }
        finally
        {
            loaded = true;
        }
    }

    private static long DefaultClock() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private static Song[] ToSongs(SongSearchEntry[]? found)
    {
        if (found is null || found.Length == 0)
        {
            return Array.Empty<Song>();
        }

        var songs = new Song[found.Length];
        for (var index = 0; index < found.Length; index++)
        {
            var entry = found[index];
            songs[index] = new Song(entry.VideoId, entry.Title, entry.Author, entry.ThumbnailUrl,
                entry.DurationSeconds, entry.ChannelId);
        }

        return songs;
    }

    private static CatalogList ToList(SongPlaylistResult? found)
    {
        if (found is not { } result)
        {
            return new CatalogList(string.Empty, Array.Empty<Song>());
        }

        return new CatalogList(result.Title, ToSongs(result.Entries));
    }

    private sealed class CatalogEntry
    {
        public Song[] Songs = Array.Empty<Song>();
        public string Title = string.Empty;
        public string Argument = string.Empty;
        public long FetchedUnix;
        public long RetryAfterUnix;
        public bool Loading;
        public bool Failed;
        public bool Persist;
        public Task? Pending;
    }

    [Serializable]
    private sealed class CatalogFile
    {
        public int Version { get; set; }
        public List<CatalogFileEntry> Entries { get; set; } = new();
    }

    [Serializable]
    private sealed class CatalogFileEntry
    {
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Argument { get; set; } = string.Empty;
        public long FetchedUnix { get; set; }
        public List<SongRecord> Songs { get; set; } = new();
    }
}
