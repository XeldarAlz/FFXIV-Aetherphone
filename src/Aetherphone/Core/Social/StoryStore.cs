using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Media;
using Aetherphone.Core.Wallpapers;

namespace Aetherphone.Core.Social;

internal sealed class StoryStore : IDisposable
{
    public const int StoryWidth = 1080;
    public const int StoryHeight = 1920;

    private readonly AethernetSession session;
    private readonly GramClient client;
    private readonly MediaClient media;
    private readonly RealtimeSignalBus signals;
    private readonly StoreWork work;
    private readonly object seenLock = new();
    private readonly HashSet<string> seenStoryIds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> seenAuthorsThrough = new(StringComparer.Ordinal);
    private volatile StoryRingDto[] rings = Array.Empty<StoryRingDto>();
    private volatile StoryDto[] openStories = Array.Empty<StoryDto>();
    private volatile string? openAuthorId;
    private volatile StoryViewerDto[] viewers = Array.Empty<StoryViewerDto>();
    private volatile string? viewersStoryId;
    private volatile int viewersTotal;
    private volatile string? viewersCursor;
    private volatile bool viewersLoadingMore;
    private volatile bool trayLoading;
    private volatile bool groupLoading;
    private volatile bool viewersLoading;
    private volatile bool posting;
    private string? lastAccountId;

    public StoryStore(AethernetSession session, GramClient client, MediaClient media, RealtimeSignalBus signals,
        string logTag)
    {
        this.session = session;
        this.client = client;
        this.media = media;
        this.signals = signals;
        work = new StoreWork(logTag);
        session.Changed += OnSessionChanged;
        signals.ContentRemoved += OnContentRemoved;
    }

    private void OnContentRemoved(ContentRemovalSignal removal)
    {
        if (!string.Equals(removal.Kind, ContentRemovalKinds.Story, StringComparison.Ordinal))
        {
            return;
        }

        RemoveOpenStory(removal.ContentId);
        RefreshTray();
    }

    private void OnSessionChanged()
    {
        var accountId = session.CurrentUser?.Id;
        if (string.Equals(accountId, lastAccountId, StringComparison.Ordinal))
        {
            return;
        }

        lastAccountId = accountId;
        rings = Array.Empty<StoryRingDto>();
        CloseAuthor();
        ClearViewers();
        lock (seenLock)
        {
            seenStoryIds.Clear();
            seenAuthorsThrough.Clear();
        }
    }

    public bool IsSignedIn => session.IsSignedIn;
    public StoryRingDto[] Rings => rings;
    public StoryDto[] OpenStories => openStories;
    public string? OpenAuthorId => openAuthorId;
    public bool GroupLoading => groupLoading;
    public bool Posting => posting;
    public StoryViewerDto[] Viewers => viewers;
    public int ViewersTotal => viewersTotal;
    public bool ViewersLoading => viewersLoading;
    public bool ViewersLoadingMore => viewersLoadingMore;
    public bool HasMoreViewers => viewersCursor is not null;

    public void LoadViewers(string storyId)
    {
        if (viewersStoryId == storyId)
        {
            return;
        }

        viewersStoryId = storyId;
        viewers = Array.Empty<StoryViewerDto>();
        viewersTotal = 0;
        viewersCursor = null;
        viewersLoading = true;
        work.Run("story viewers", async token =>
        {
            var page = await client.StoryViewersAsync(storyId, null, token).ConfigureAwait(false);
            if (page is not null && viewersStoryId == storyId)
            {
                viewers = page.Items;
                viewersTotal = page.Total;
                viewersCursor = page.NextCursor;
            }
        }, () => viewersLoading = false);
    }

    public void LoadMoreViewers()
    {
        var storyId = viewersStoryId;
        var cursor = viewersCursor;
        if (storyId is null || cursor is null || viewersLoadingMore || viewersLoading)
        {
            return;
        }

        viewersLoadingMore = true;
        work.Run("story viewers more", async token =>
        {
            var page = await client.StoryViewersAsync(storyId, cursor, token).ConfigureAwait(false);
            if (page is null || viewersStoryId != storyId)
            {
                return;
            }

            viewers = AppendViewers(viewers, page.Items);
            viewersTotal = page.Total;
            viewersCursor = page.NextCursor;
        }, () => viewersLoadingMore = false);
    }

    private static StoryViewerDto[] AppendViewers(StoryViewerDto[] source, StoryViewerDto[] incoming)
    {
        if (incoming.Length == 0)
        {
            return source;
        }

        var existing = new HashSet<string>(source.Length, StringComparer.Ordinal);
        for (var index = 0; index < source.Length; index++)
        {
            existing.Add(source[index].UserId);
        }

        var fresh = new List<StoryViewerDto>(incoming.Length);
        for (var index = 0; index < incoming.Length; index++)
        {
            if (!existing.Contains(incoming[index].UserId))
            {
                fresh.Add(incoming[index]);
            }
        }

        if (fresh.Count == 0)
        {
            return source;
        }

        var result = new StoryViewerDto[source.Length + fresh.Count];
        Array.Copy(source, result, source.Length);
        fresh.CopyTo(result, source.Length);
        return result;
    }

    public void ClearViewers()
    {
        viewersStoryId = null;
        viewers = Array.Empty<StoryViewerDto>();
        viewersTotal = 0;
        viewersCursor = null;
    }

    public bool HasOwnRing
    {
        get
        {
            var snapshot = rings;
            for (var index = 0; index < snapshot.Length; index++)
            {
                if (snapshot[index].IsMe)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public bool TryRing(string authorId, out StoryRingDto ring)
    {
        var snapshot = rings;
        for (var index = 0; index < snapshot.Length; index++)
        {
            if (snapshot[index].AuthorId == authorId)
            {
                ring = snapshot[index];
                return true;
            }
        }

        ring = null!;
        return false;
    }

    public void RefreshTray()
    {
        if (!session.IsSignedIn || trayLoading)
        {
            return;
        }

        trayLoading = true;
        work.Run("story tray", async token =>
        {
            var tray = await client.StoryTrayAsync(token).ConfigureAwait(false);
            if (tray is not null)
            {
                rings = ApplyLocalSeen(tray.Rings);
            }
        }, () => trayLoading = false);
    }

    public void OpenAuthor(string authorId)
    {
        openAuthorId = authorId;
        openStories = Array.Empty<StoryDto>();
        groupLoading = true;
        work.Run("story group", async token =>
        {
            var group = await client.UserStoriesAsync(authorId, token).ConfigureAwait(false);
            if (group is not null && openAuthorId == authorId)
            {
                openStories = group.Items;
            }
        }, () => groupLoading = false);
    }

    public void CloseAuthor()
    {
        openAuthorId = null;
        openStories = Array.Empty<StoryDto>();
    }

    public void MarkSeen(StoryDto story)
    {
        lock (seenLock)
        {
            if (!seenStoryIds.Add(story.Id))
            {
                return;
            }
        }

        work.Run("story seen", token => client.MarkStoryViewedAsync(story.Id, token));
        HoldAuthorSeen(story.AuthorId);
    }

    public void CreateStory(string sourcePath, WallpaperCrop crop, string caption, Action<bool> onComplete)
    {
        if (posting)
        {
            return;
        }

        posting = true;
        work.Run("create story", async token =>
        {
            var baked = ImageProcessor.BakeCroppedJpeg(sourcePath, crop, StoryWidth, StoryHeight);
            var upload = await media.UploadUrlAsync("image/jpeg", "story", token).ConfigureAwait(false);
            if (upload is null)
            {
                return false;
            }

            var uploaded = await media.UploadImageAsync(upload.UploadUrl, baked.Bytes, "image/jpeg", token)
                .ConfigureAwait(false);
            if (!uploaded)
            {
                return false;
            }

            var created = await client.CreateStoryAsync(caption.Trim(), upload.Key, baked.Width, baked.Height, token)
                .ConfigureAwait(false);
            if (created is null)
            {
                return false;
            }

            RefreshTray();
            return true;
        }, onComplete, () => posting = false);
    }

    public void DeleteStory(string storyId, Action<bool> onComplete)
    {
        work.Run("delete story", async token =>
        {
            var deleted = await client.DeleteStoryAsync(storyId, token).ConfigureAwait(false);
            if (!deleted)
            {
                return false;
            }

            RemoveOpenStory(storyId);
            RefreshTray();
            return true;
        }, onComplete);
    }

    private void RemoveOpenStory(string storyId)
    {
        var snapshot = openStories;
        var kept = new List<StoryDto>(snapshot.Length);
        for (var index = 0; index < snapshot.Length; index++)
        {
            if (snapshot[index].Id != storyId)
            {
                kept.Add(snapshot[index]);
            }
        }

        openStories = kept.ToArray();
    }

    private void HoldAuthorSeen(string authorId)
    {
        var snapshot = openStories;
        if (openAuthorId != authorId || snapshot.Length == 0)
        {
            return;
        }

        var latest = 0L;
        lock (seenLock)
        {
            for (var index = 0; index < snapshot.Length; index++)
            {
                var story = snapshot[index];
                if (!story.Seen && !seenStoryIds.Contains(story.Id))
                {
                    return;
                }

                if (story.CreatedAtUnix > latest)
                {
                    latest = story.CreatedAtUnix;
                }
            }

            seenAuthorsThrough[authorId] = latest;
        }

        rings = ApplyLocalSeen(rings);
    }

    private StoryRingDto[] ApplyLocalSeen(StoryRingDto[] source)
    {
        if (source.Length == 0)
        {
            return source;
        }

        var result = new StoryRingDto[source.Length];
        lock (seenLock)
        {
            for (var index = 0; index < source.Length; index++)
            {
                var ring = source[index];
                var held = ring.HasUnseen
                    && seenAuthorsThrough.TryGetValue(ring.AuthorId, out var seenThrough)
                    && ring.LatestAtUnix <= seenThrough;
                result[index] = held ? ring with { HasUnseen = false } : ring;
            }
        }

        return result;
    }

    public void Dispose()
    {
        session.Changed -= OnSessionChanged;
        signals.ContentRemoved -= OnContentRemoved;
        work.Dispose();
    }
}
