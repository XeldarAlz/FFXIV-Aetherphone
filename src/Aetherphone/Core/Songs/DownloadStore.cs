using Aetherphone.Core.Net;

namespace Aetherphone.Core.Songs;

internal enum DownloadState : byte
{
    None,
    Queued,
    Downloading,
    Done,
    Failed,
}

internal sealed class DownloadStore : IDisposable
{
    public const string OpusExtension = ".opus";
    public const string AacExtension = ".m4a";
    private const string TempSuffix = ".tmp";

    private readonly DirectoryInfo root;
    private readonly DiskCache cache;
    private readonly SongLinkResolver resolver;
    private readonly LibraryStore library;
    private readonly Action<Action> post;
    private readonly object gate = new();
    private readonly Queue<Song> queue = new();
    private readonly Dictionary<string, DownloadState> states = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource lifetime = new();
    private CancellationTokenSource? current;
    private string currentId = string.Empty;
    private bool working;
    private bool reconciled;
    private long totalBytes = -1;
    private int failures;

    public DownloadStore(DirectoryInfo root, DiskCache cache, SongLinkResolver resolver, LibraryStore library,
        Action<Action> post)
    {
        this.root = root;
        this.cache = cache;
        this.resolver = resolver;
        this.library = library;
        this.post = post;
    }

    public int Version { get; private set; }

    public int Failures => Volatile.Read(ref failures);

    public int PendingCount
    {
        get
        {
            lock (gate)
            {
                return queue.Count + (working ? 1 : 0);
            }
        }
    }

    public long TotalBytes
    {
        get
        {
            lock (gate)
            {
                if (totalBytes < 0)
                {
                    totalBytes = MeasureFolder(root.FullName);
                }

                return totalBytes;
            }
        }
    }

    public DownloadState StateOf(string videoId)
    {
        if (string.IsNullOrEmpty(videoId))
        {
            return DownloadState.None;
        }

        lock (gate)
        {
            if (states.TryGetValue(videoId, out var state))
            {
                return state;
            }
        }

        return library.IsDownloaded(videoId) ? DownloadState.Done : DownloadState.None;
    }

    public void Download(in Song song)
    {
        if (song.IsEmpty || !PlaylistImporter.IsVideoId(song.VideoId))
        {
            return;
        }

        lock (gate)
        {
            if (states.TryGetValue(song.VideoId, out var state) &&
                state is DownloadState.Queued or DownloadState.Downloading)
            {
                return;
            }

            states[song.VideoId] = DownloadState.Queued;
            queue.Enqueue(song);
            Version++;
            if (working)
            {
                return;
            }

            working = true;
        }

        _ = Task.Run(Drain);
    }

    public void Remove(in Song song)
    {
        if (song.IsEmpty)
        {
            return;
        }

        var videoId = song.VideoId;
        lock (gate)
        {
            states.Remove(videoId);
            if (string.Equals(currentId, videoId, StringComparison.Ordinal))
            {
                current?.Cancel();
            }

            RemoveQueued(videoId);
            DeleteFiles(root.FullName, videoId);
            totalBytes = -1;
            Version++;
        }

        library.SetDownloaded(song, false);
    }

    public void Toggle(in Song song)
    {
        var state = StateOf(song.VideoId);
        if (state is DownloadState.None or DownloadState.Failed)
        {
            Download(song);
            return;
        }

        Remove(song);
    }

    public void EnsureReconciled()
    {
        lock (gate)
        {
            if (reconciled)
            {
                return;
            }

            reconciled = true;
        }

        var ids = library.DownloadedIds();
        for (var index = 0; index < ids.Length; index++)
        {
            if (FindFile(root.FullName, ids[index]) is not null || library.FindSong(ids[index]) is not { } record)
            {
                continue;
            }

            Download(record.ToSong());
        }
    }

    public SongResolvedAudio? TryRead(string videoId)
    {
        if (!PlaylistImporter.IsVideoId(videoId))
        {
            return null;
        }

        try
        {
            var opus = Path.Combine(root.FullName, FileName(videoId, true));
            if (File.Exists(opus))
            {
                return new SongResolvedAudio(File.ReadAllBytes(opus), true);
            }

            var aac = Path.Combine(root.FullName, FileName(videoId, false));
            return File.Exists(aac) ? new SongResolvedAudio(File.ReadAllBytes(aac), false) : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            AepLog.Warning(exception, "Downloaded song could not be read");
            return null;
        }
    }

    public void Dispose()
    {
        lifetime.Cancel();
        lock (gate)
        {
            queue.Clear();
            current?.Cancel();
        }

        lifetime.Dispose();
    }

    internal static string FileName(string videoId, bool isOpus) =>
        videoId + (isOpus ? OpusExtension : AacExtension);

    internal static string? FindFile(string directory, string videoId)
    {
        if (!PlaylistImporter.IsVideoId(videoId))
        {
            return null;
        }

        var opus = Path.Combine(directory, FileName(videoId, true));
        if (File.Exists(opus))
        {
            return opus;
        }

        var aac = Path.Combine(directory, FileName(videoId, false));
        return File.Exists(aac) ? aac : null;
    }

    internal static string WriteAtomic(string directory, string videoId, byte[] bytes, bool isOpus)
    {
        if (!PlaylistImporter.IsVideoId(videoId))
        {
            throw new ArgumentException("Not a video id", nameof(videoId));
        }

        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, FileName(videoId, isOpus));
        var temp = target + TempSuffix;
        File.WriteAllBytes(temp, bytes);
        File.Move(temp, target, true);
        var other = Path.Combine(directory, FileName(videoId, !isOpus));
        if (File.Exists(other))
        {
            File.Delete(other);
        }

        return target;
    }

    internal static void DeleteFiles(string directory, string videoId)
    {
        if (!PlaylistImporter.IsVideoId(videoId))
        {
            return;
        }

        TryDelete(Path.Combine(directory, FileName(videoId, true)));
        TryDelete(Path.Combine(directory, FileName(videoId, false)));
        TryDelete(Path.Combine(directory, FileName(videoId, true) + TempSuffix));
        TryDelete(Path.Combine(directory, FileName(videoId, false) + TempSuffix));
    }

    internal static long MeasureFolder(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return 0;
        }

        var total = 0L;
        try
        {
            var files = new DirectoryInfo(directory).GetFiles();
            for (var index = 0; index < files.Length; index++)
            {
                if (files[index].Extension is OpusExtension or AacExtension)
                {
                    total += files[index].Length;
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            AepLog.Debug(exception, "Download folder could not be measured");
        }

        return total;
    }

    private void Drain()
    {
        while (!lifetime.IsCancellationRequested)
        {
            Song song;
            CancellationToken token;
            lock (gate)
            {
                if (queue.Count == 0)
                {
                    working = false;
                    currentId = string.Empty;
                    return;
                }

                song = queue.Dequeue();
                currentId = song.VideoId;
                states[song.VideoId] = DownloadState.Downloading;
                current?.Dispose();
                current = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                token = current.Token;
                Version++;
            }

            var succeeded = TryDownload(song.VideoId, token);
            Finish(song, succeeded, token);
        }
    }

    private bool TryDownload(string videoId, CancellationToken token)
    {
        try
        {
            if (FindFile(root.FullName, videoId) is not null)
            {
                return true;
            }

            var audio = FromCache(videoId) ?? resolver.Fetch(videoId, token) ?? FromCache(videoId);
            if (audio is not { Bytes.Length: > 0 } fetched || token.IsCancellationRequested)
            {
                return false;
            }

            WriteAtomic(root.FullName, videoId, fetched.Bytes, fetched.IsOpus);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "Song download failed");
            return false;
        }
    }

    private SongResolvedAudio? FromCache(string videoId)
    {
        var opus = cache.Get(SongPlayer.OpusCacheKey(videoId), TimeSpan.MaxValue);
        if (opus is { Length: > 0 })
        {
            return new SongResolvedAudio(opus, true);
        }

        var other = cache.Get(videoId, TimeSpan.MaxValue);
        return other is { Length: > 0 } ? new SongResolvedAudio(other, false) : null;
    }

    private void Finish(in Song song, bool succeeded, CancellationToken token)
    {
        var videoId = song.VideoId;
        var finished = song;
        lock (gate)
        {
            currentId = string.Empty;
            totalBytes = -1;
            Version++;
            if (!states.ContainsKey(videoId))
            {
                DeleteFiles(root.FullName, videoId);
                return;
            }

            if (token.IsCancellationRequested)
            {
                return;
            }

            states[videoId] = succeeded ? DownloadState.Done : DownloadState.Failed;
        }

        if (!succeeded)
        {
            Interlocked.Increment(ref failures);
            return;
        }

        post(() => library.SetDownloaded(finished, true));
    }

    private void RemoveQueued(string videoId)
    {
        var count = queue.Count;
        for (var index = 0; index < count; index++)
        {
            var song = queue.Dequeue();
            if (!string.Equals(song.VideoId, videoId, StringComparison.Ordinal))
            {
                queue.Enqueue(song);
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            AepLog.Debug(exception, $"Download cleanup skipped {path}");
        }
    }
}
