namespace Aetherphone.Core.Video;

// Title/Source/Duration/ThumbnailUrl start out as just the raw URL and get filled in
// asynchronously - see AetherStreamQueue's enrichment step. Mutable (not a record) precisely so
// that fill-in can update the same instance already sitting in the queue/UI rather than needing
// every caller to track down and replace it after a reorder. Reference equality (the class
// default) is also the correct behavior for List.Remove here, unlike a record's value equality,
// which could match the wrong entry if two queued items happen to share every field.
internal sealed class VideoQueueEntry
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Url { get; }
    public string Title { get; set; }
    public string Source { get; set; }
    public TimeSpan? Duration { get; set; }
    public string? ThumbnailUrl { get; set; }

    public VideoQueueEntry(string url, string title, string source, TimeSpan? duration, string? thumbnailUrl)
    {
        Url = url;
        Title = title;
        Source = source;
        Duration = duration;
        ThumbnailUrl = thumbnailUrl;
    }
}

// Aetherphone's own queue - VideoPlayer has no knowledge this exists. One item plays at a time;
// the next is pushed when VideoPlayer.IsIdle() reports the current one has naturally ended.
// Stage 0 established that end-of-playback is only observable by polling mpv's idle-active
// property (docs/video-pipeline.md in the AlphaChannel repo, §"when is state published" for the
// equivalent finding on the reference implementation) - there's no event to subscribe to, so
// this polls, but on a tick-throttled interval, never every frame.
internal sealed class AetherStreamQueue
{
    private const int PollEveryTicks = 30; // roughly twice a second at 60fps, not per-frame

    private readonly VideoPlayer video;
    private readonly VideoUrlResolver metadataResolver = new();
    private readonly List<VideoQueueEntry> entries = new();
    private int tickCounter;
    private bool wasIdle = true;
    private bool autoAdvanceArmed;

    // Restores the upcoming list (not whatever was actively Current - that doesn't survive a
    // reload anyway, since the mpv session behind it is gone) from Configuration, so a relog or
    // plugin reload mid watch-party doesn't wipe out what was queued up.
    public AetherStreamQueue(VideoPlayer video)
    {
        this.video = video;
        var persisted = Plugin.Cfg.VideoQueue;
        for (var recordIndex = 0; recordIndex < persisted.Count; recordIndex++)
        {
            var record = persisted[recordIndex];
            entries.Add(new VideoQueueEntry(record.Url, record.Title, record.Source,
                record.DurationSeconds is { } seconds ? TimeSpan.FromSeconds(seconds) : null, record.ThumbnailUrl));
        }
    }

    public IReadOnlyList<VideoQueueEntry> Entries => entries;
    public VideoQueueEntry? Current { get; private set; }

    // Builds a display-only entry for a URL, enriched the same way a real queue entry would be,
    // without touching entries or Current - used by WatchAlongSession to show what a viewer is
    // watching. Current specifically must stay untouched here: OnFrameworkUpdate's Viewing branch
    // treats any non-null Current as "the user queued something locally, leave the stream", so
    // populating it from the sync path itself would immediately self-trigger a Leave().
    public VideoQueueEntry CreateDisplayEntry(string url)
    {
        var entry = new VideoQueueEntry(url, url, string.Empty, null, null);
        EnrichIfYouTube(entry);
        return entry;
    }

    public void Add(VideoQueueEntry entry)
    {
        entries.Add(entry);
        EnrichIfYouTube(entry);
        Persist();
    }

    public void PlayNow(VideoQueueEntry entry)
    {
        entries.Remove(entry);
        entries.Insert(0, entry);
        EnrichIfYouTube(entry);
        Advance(); // Persists on our behalf - see Advance's own Persist call.
    }

    public void PlayNext(VideoQueueEntry entry)
    {
        entries.Remove(entry);
        var insertAt = Current is null ? 0 : Math.Min(1, entries.Count);
        entries.Insert(insertAt, entry);
        EnrichIfYouTube(entry);
        Persist();
    }

    public void Remove(VideoQueueEntry entry)
    {
        entries.Remove(entry);
        Persist();
    }

    public void Clear()
    {
        entries.Clear();
        Current = null;
        video.Stop();
        Persist();
    }

    public void Reorder(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= entries.Count || toIndex < 0 || toIndex >= entries.Count ||
            fromIndex == toIndex)
        {
            return;
        }

        var item = entries[fromIndex];
        entries.RemoveAt(fromIndex);
        entries.Insert(toIndex, item);
        Persist();
    }

    private void Persist()
    {
        var records = new List<VideoQueueRecord>(entries.Count);
        for (var entryIndex = 0; entryIndex < entries.Count; entryIndex++)
        {
            var entry = entries[entryIndex];
            records.Add(new VideoQueueRecord
            {
                Url = entry.Url,
                Title = entry.Title,
                Source = entry.Source,
                DurationSeconds = entry.Duration?.TotalSeconds,
                ThumbnailUrl = entry.ThumbnailUrl,
            });
        }

        Plugin.Cfg.VideoQueue = records;
        Plugin.Cfg.Save();
    }

    // Plays the next queued entry now, whether or not something was already playing - used both
    // for the user's own "skip" action and for auto-advance on natural end.
    public void Advance()
    {
        if (entries.Count == 0)
        {
            // Deliberately does NOT call video.Stop() - that deactivates the in-world screen
            // entirely (VideoEngine._isActive = false), which reads as the screen just
            // vanishing mid watch-party. Leaving it alone keeps the screen up, frozen on the
            // last frame (mpv's own keep-open=yes), as a "waiting for the next video" state.
            // Clear() is the explicit-close path for when the user actually wants it gone.
            Current = null;
            Persist();
            return;
        }

        Current = entries[0];
        entries.RemoveAt(0);
        autoAdvanceArmed = false;
        EnrichIfYouTube(Current);
        video.Play(Current.Url);
        Persist();
    }

    private void EnrichIfYouTube(VideoQueueEntry entry)
    {
        if (!VideoUrlResolver.IsYouTubeUrl(entry.Url))
        {
            return;
        }

        _ = EnrichAsync(entry);
    }

    private async Task EnrichAsync(VideoQueueEntry entry)
    {
        var metadata = await metadataResolver.ResolveMetadataAsync(entry.Url, CancellationToken.None)
            .ConfigureAwait(false);
        if (metadata is null)
        {
            return;
        }

        entry.Title = metadata.Title;
        entry.Source = metadata.Source;
        entry.Duration = metadata.Duration;
        entry.ThumbnailUrl = metadata.ThumbnailUrl;
        Persist(); // Keep the saved copy in sync so a reload doesn't fall back to a bare URL title.
    }

    public void OnFrameworkUpdate()
    {
        tickCounter++;
        if (tickCounter < PollEveryTicks)
        {
            return;
        }

        tickCounter = 0;

        if (video.State == VideoPlaybackState.Playing)
        {
            autoAdvanceArmed = true;
        }

        var idle = video.IsIdle();
        if (idle && !wasIdle && autoAdvanceArmed && Current is not null)
        {
            autoAdvanceArmed = false;
            Advance();
        }

        wasIdle = idle;
    }
}
