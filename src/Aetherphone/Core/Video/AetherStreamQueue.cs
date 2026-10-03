using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Video;

internal sealed class VideoQueueEntry
{
    private string source;
    private TimeSpan? duration;
    private string? subtitle;

    internal VideoQueueEntry(string url, string title, string source, TimeSpan? duration, string? thumbnailUrl)
    {
        Url = url;
        Title = title;
        this.source = source;
        this.duration = duration;
        ThumbnailUrl = thumbnailUrl;
    }

    internal Guid Id { get; } = Guid.NewGuid();
    internal string Url { get; }
    internal string Title { get; set; }

    internal string Source
    {
        get => source;
        set
        {
            source = value;
            subtitle = null;
        }
    }

    internal TimeSpan? Duration
    {
        get => duration;
        set
        {
            duration = value;
            subtitle = null;
        }
    }

    internal string Subtitle => subtitle ??= MediaInput.Subtitle(source, duration?.TotalSeconds);

    internal string? ThumbnailUrl { get; set; }
    internal bool EnrichRequested { get; set; }
    internal LocalMediaIdentity? LocalMedia { get; set; }
    internal bool FingerprintRequested { get; set; }

    internal VideoQueueRecord ToRecord() => new()
    {
        Url = Url,
        Title = Title,
        Source = Source,
        DurationSeconds = Duration?.TotalSeconds,
        ThumbnailUrl = ThumbnailUrl,
    };
}

internal enum QueueAddMode : byte
{
    PlayNow,
    PlayNext,
    AddToQueue,
}

internal enum PlaylistImportState : byte
{
    Idle,
    Loading,
    Failed,
}

internal readonly record struct PlaylistImportResult(int Stamp, string Title, int Added, bool Truncated, bool Failed);

internal sealed class AetherStreamQueue : IDisposable
{
    private const int MaxConsecutiveFailures = 3;
    private const long PositionNoteMilliseconds = 5000;
    private const long PositionSaveMilliseconds = 30000;

    private readonly VideoPlayer video;
    private readonly VideoUrlResolver metadata;
    private readonly VideoLibrary library;
    private readonly Configuration configuration;
    private readonly List<VideoQueueEntry> entries = [];
    private readonly CancellationTokenSource lifetime = new();

    private int consecutiveFailures;
    private bool suspended;
    private long positionNotedAtTicks;
    private long positionSavedAtTicks;
    private int importStamp;

    internal AetherStreamQueue(VideoPlayer video, VideoUrlResolver metadata, VideoLibrary library,
        Configuration configuration)
    {
        this.video = video;
        this.metadata = metadata;
        this.library = library;
        this.configuration = configuration;
        video.Finished += OnPlaybackFinished;
        video.Failed += OnPlaybackFailed;

        var persisted = configuration.VideoQueue;
        for (var recordIndex = 0; recordIndex < persisted.Count; recordIndex++)
        {
            var entry = FromRecord(persisted[recordIndex]);
            entries.Add(entry);
            FingerprintIfLocalFile(entry);
        }
    }

    internal IReadOnlyList<VideoQueueEntry> Entries => entries;
    internal VideoQueueEntry? Current { get; private set; }
    internal bool IsSuspended => suspended;
    internal PlaylistImportState ImportState { get; private set; }
    internal PlaylistImportResult LastImport { get; private set; }
    internal VideoPlaylist? LastImportedPlaylist { get; private set; }

    internal event Action? Changed;

    internal static VideoQueueEntry FromRecord(VideoQueueRecord record) =>
        new(record.Url, record.Title, record.Source,
            record.DurationSeconds is { } seconds ? TimeSpan.FromSeconds(seconds) : null, record.ThumbnailUrl)
        {
            EnrichRequested = record.DurationSeconds is not null,
        };

    internal VideoQueueEntry CreateDisplayEntry(string url)
    {
        if (LocalMediaToken.TryParse(url, out var identity))
        {
            return new VideoQueueEntry(url, Path.GetFileNameWithoutExtension(identity.FileName),
                Loc.T(L.AetherStream.LocalFileSource), null, null)
            {
                LocalMedia = identity,
                FingerprintRequested = true,
            };
        }

        if (MediaInput.LooksLikeLocalPath(url))
        {
            return new VideoQueueEntry(url, Path.GetFileNameWithoutExtension(url),
                Loc.T(L.AetherStream.LocalFileSource), null, null);
        }

        var entry = new VideoQueueEntry(url, TitleFromUrl(url), string.Empty, null, null);
        EnrichIfYouTube(entry);
        return entry;
    }

    private static string TitleFromUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed))
        {
            return url;
        }

        var name = Path.GetFileName(parsed.LocalPath);
        return name.Length > 0 && Path.HasExtension(name) ? name : url;
    }

    internal void Suspend()
    {
        if (suspended)
        {
            return;
        }

        suspended = true;
        if (Current is not null)
        {
            NoteCurrentPosition();
            entries.Insert(0, Current);
            Current = null;
            Persist();
        }

        video.Stop();
    }

    internal void Resume() => suspended = false;

    internal void Add(VideoQueueEntry entry) => Insert(entry, QueueAddMode.AddToQueue);

    internal void PlayNow(VideoQueueEntry entry, double startSeconds = 0d)
    {
        entries.Remove(entry);
        if (entries.Count > 0 && string.Equals(entries[0].Url, entry.Url, StringComparison.Ordinal))
        {
            entries.RemoveAt(0);
        }

        entries.Insert(0, entry);
        Prepare(entry);
        Advance(startSeconds);
    }

    internal void Insert(VideoQueueEntry entry, QueueAddMode mode)
    {
        if (mode == QueueAddMode.PlayNow)
        {
            PlayNow(entry);
            return;
        }

        entries.Remove(entry);
        if (mode == QueueAddMode.PlayNext)
        {
            entries.Insert(0, entry);
        }
        else
        {
            entries.Add(entry);
        }

        Prepare(entry);
        Persist();
    }

    internal void InsertMany(IReadOnlyList<VideoQueueEntry> batch, QueueAddMode mode)
    {
        if (batch.Count == 0)
        {
            return;
        }

        if (mode == QueueAddMode.AddToQueue)
        {
            for (var index = 0; index < batch.Count; index++)
            {
                entries.Add(batch[index]);
                Prepare(batch[index]);
            }

            Persist();
            return;
        }

        for (var index = batch.Count - 1; index >= 0; index--)
        {
            entries.Insert(0, batch[index]);
            Prepare(batch[index]);
        }

        if (mode == QueueAddMode.PlayNow)
        {
            Advance();
            return;
        }

        Persist();
    }

    internal void InsertRecords(IReadOnlyList<VideoQueueRecord> records, QueueAddMode mode)
    {
        var batch = new VideoQueueEntry[records.Count];
        for (var index = 0; index < records.Count; index++)
        {
            batch[index] = FromRecord(records[index]);
        }

        InsertMany(batch, mode);
    }

    internal bool ImportPlaylist(string url, QueueAddMode mode, bool remember)
    {
        if (ImportState == PlaylistImportState.Loading)
        {
            return false;
        }

        ImportState = PlaylistImportState.Loading;
        _ = ImportPlaylistAsync(url, mode, remember);
        return true;
    }

    private async Task ImportPlaylistAsync(string url, QueueAddMode mode, bool remember)
    {
        var playlist = await metadata.ResolvePlaylistAsync(url, lifetime.Token).ConfigureAwait(false);
        if (lifetime.IsCancellationRequested)
        {
            return;
        }

        await Plugin.Framework.RunOnFrameworkThread(() =>
        {
            importStamp++;
            if (playlist is null)
            {
                ImportState = PlaylistImportState.Failed;
                LastImport = new PlaylistImportResult(importStamp, string.Empty, 0, false, true);
                Changed?.Invoke();
                return;
            }

            var batch = new VideoQueueEntry[playlist.Videos.Length];
            var records = new List<VideoQueueRecord>(batch.Length);
            for (var index = 0; index < batch.Length; index++)
            {
                var item = playlist.Videos[index];
                batch[index] = new VideoQueueEntry(item.Url, item.Metadata.Title, item.Metadata.Source,
                    item.Metadata.Duration, item.Metadata.ThumbnailUrl)
                {
                    EnrichRequested = true,
                };
                records.Add(batch[index].ToRecord());
            }

            if (remember)
            {
                library.SavePlaylist(playlist.Title, playlist.SourceUrl, records);
            }

            ImportState = PlaylistImportState.Idle;
            LastImportedPlaylist = playlist;
            LastImport = new PlaylistImportResult(importStamp, playlist.Title, batch.Length, playlist.Truncated,
                false);
            InsertMany(batch, mode);
        }).ConfigureAwait(false);
    }

    internal void Remove(VideoQueueEntry entry)
    {
        entries.Remove(entry);
        Persist();
    }

    internal void Clear()
    {
        NoteCurrentPosition();
        entries.Clear();
        Current = null;
        video.Stop();
        Persist();
    }

    internal void ClearUpcoming()
    {
        entries.Clear();
        Persist();
    }

    internal void StopPlayback()
    {
        if (Current is { } stopped)
        {
            NoteCurrentPosition();
            entries.Insert(0, stopped);
            Current = null;
            Persist();
        }

        video.Stop();
        Changed?.Invoke();
    }

    internal void Reorder(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= entries.Count || toIndex < 0 || toIndex >= entries.Count
            || fromIndex == toIndex)
        {
            return;
        }

        var item = entries[fromIndex];
        entries.RemoveAt(fromIndex);
        entries.Insert(toIndex, item);
        Persist();
    }

    internal void Shuffle()
    {
        for (var index = entries.Count - 1; index > 0; index--)
        {
            var swapIndex = Random.Shared.Next(index + 1);
            (entries[index], entries[swapIndex]) = (entries[swapIndex], entries[index]);
        }

        Persist();
    }

    internal bool HasNext => entries.Count > 0;

    internal void Advance() => Advance(0d);

    internal void AdvanceFrom(double startSeconds) => Advance(startSeconds);

    private void Advance(double startSeconds)
    {
        if (suspended)
        {
            return;
        }

        NoteCurrentPosition();
        while (entries.Count > 0)
        {
            var next = entries[0];
            entries.RemoveAt(0);
            if (PlayableUrl(next) is not { } playable)
            {
                continue;
            }

            Current = next;
            EnrichIfYouTube(next);
            library.NotePlayed(next);
            video.Play(playable, startSeconds);
            Persist();
            return;
        }

        Current = null;
        video.Stop();
        Persist();
    }

    internal void Restart()
    {
        if (Current is null)
        {
            return;
        }

        video.Seek(0d);
    }

    internal void AdoptParty(VideoQueueEntry? playing, IReadOnlyList<VideoQueueEntry> upcoming)
    {
        suspended = false;
        consecutiveFailures = 0;
        Current = playing;
        for (var index = upcoming.Count - 1; index >= 0; index--)
        {
            entries.Insert(0, upcoming[index]);
            Prepare(upcoming[index]);
        }

        Persist();
    }

    internal VideoQueueEntry? HandOver(int sharedCount)
    {
        var playing = Current;
        Current = null;
        entries.RemoveRange(0, Math.Min(Math.Max(sharedCount, 0), entries.Count));
        suspended = true;
        Persist();
        return playing;
    }

    internal void OnFrameworkUpdate()
    {
        var now = Environment.TickCount64;
        if (now - positionNotedAtTicks < PositionNoteMilliseconds)
        {
            return;
        }

        positionNotedAtTicks = now;
        if (video.State != VideoPlaybackState.Playing || !NoteCurrentPosition())
        {
            return;
        }

        if (now - positionSavedAtTicks >= PositionSaveMilliseconds)
        {
            positionSavedAtTicks = now;
            configuration.Save();
        }
    }

    private bool NoteCurrentPosition()
    {
        if (Current is not { } current
            || video.State is not (VideoPlaybackState.Playing or VideoPlaybackState.Paused))
        {
            return false;
        }

        var progress = video.Progress;
        library.NotePosition(current.Url, progress.Position, progress.Duration);
        return true;
    }

    private string? PlayableUrl(VideoQueueEntry entry)
    {
        if (!LocalMediaToken.TryParse(entry.Url, out var identity))
        {
            return entry.Url;
        }

        return LocalMediaFiles.TryResolve(configuration, identity, out var path) ? path : null;
    }

    private void Prepare(VideoQueueEntry entry)
    {
        EnrichIfYouTube(entry);
        FingerprintIfLocalFile(entry);
    }

    private void OnPlaybackFinished()
    {
        consecutiveFailures = 0;
        if (suspended)
        {
            return;
        }

        if (Current is { } finished)
        {
            library.NoteFinished(finished.Url);
        }

        Advance();
    }

    private void OnPlaybackFailed(string? reason)
    {
        if (suspended)
        {
            return;
        }

        consecutiveFailures++;
        if (consecutiveFailures >= MaxConsecutiveFailures || entries.Count == 0)
        {
            Changed?.Invoke();
            return;
        }

        Advance();
    }

    internal void Replay(double positionSeconds)
    {
        if (suspended || Current is not { } current || PlayableUrl(current) is not { } playable)
        {
            return;
        }

        consecutiveFailures = 0;
        video.Play(playable, positionSeconds);
        Changed?.Invoke();
    }

    private void Persist()
    {
        var records = new List<VideoQueueRecord>(entries.Count);
        for (var entryIndex = 0; entryIndex < entries.Count; entryIndex++)
        {
            records.Add(entries[entryIndex].ToRecord());
        }

        configuration.VideoQueue = records;
        configuration.Save();
        Changed?.Invoke();
    }

    private void FingerprintIfLocalFile(VideoQueueEntry entry)
    {
        if (entry.LocalMedia is not null || entry.FingerprintRequested || !MediaInput.LooksLikeLocalPath(entry.Url))
        {
            return;
        }

        entry.FingerprintRequested = true;
        _ = FingerprintAsync(entry);
    }

    private async Task FingerprintAsync(VideoQueueEntry entry)
    {
        var identity = await Task.Run(() => LocalMediaToken.TryCompute(entry.Url)).ConfigureAwait(false);
        if (identity is null)
        {
            return;
        }

        await Plugin.Framework.RunOnFrameworkThread(() =>
        {
            entry.LocalMedia = identity;
            Changed?.Invoke();
        }).ConfigureAwait(false);
    }

    private void EnrichIfYouTube(VideoQueueEntry entry)
    {
        if (entry.EnrichRequested || MediaInput.LooksLikeLocalPath(entry.Url) || !VideoUrlResolver.IsYouTubeUrl(entry.Url))
        {
            return;
        }

        entry.EnrichRequested = true;
        _ = EnrichAsync(entry);
    }

    private async Task EnrichAsync(VideoQueueEntry entry)
    {
        var resolved = await metadata.ResolveMetadataAsync(entry.Url, lifetime.Token).ConfigureAwait(false);
        if (resolved is null)
        {
            entry.EnrichRequested = false;
            return;
        }

        await Plugin.Framework.RunOnFrameworkThread(() =>
        {
            entry.Title = resolved.Title;
            entry.Source = resolved.Source;
            entry.Duration = resolved.Duration;
            entry.ThumbnailUrl = resolved.ThumbnailUrl;
            library.NoteDetails(entry);
            Persist();
        }).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (NoteCurrentPosition())
        {
            configuration.Save();
        }

        lifetime.Cancel();
        video.Finished -= OnPlaybackFinished;
        video.Failed -= OnPlaybackFailed;
        lifetime.Dispose();
    }
}
