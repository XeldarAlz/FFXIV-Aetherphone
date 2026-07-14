using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Media;
using Aetherphone.Core.Wallpapers;

namespace Aetherphone.Core.Social;

/// <summary>
/// Owns the story tray and the currently opened author's stories.
/// Marking a story seen is fire and forget, so the ring state is also tracked locally: an author is
/// held as seen only up to the story timestamp that was actually watched, which lets a newly posted
/// story light the ring again before the next tray fetch lands.
/// </summary>
internal sealed class StoryStore : IDisposable
{
    public const int StoryWidth = 1080;
    public const int StoryHeight = 1920;

    private readonly AethernetSession session;
    private readonly AethernetClient client;
    private readonly StoreWork work;
    private readonly object seenLock = new();
    private readonly HashSet<string> seenStoryIds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> seenAuthorsThrough = new(StringComparer.Ordinal);
    private volatile StoryRingDto[] rings = Array.Empty<StoryRingDto>();
    private volatile StoryDto[] openStories = Array.Empty<StoryDto>();
    private volatile string? openAuthorId;
    private volatile bool trayLoading;
    private volatile bool groupLoading;
    private volatile bool posting;

    public StoryStore(AethernetSession session, AethernetClient client, string logTag)
    {
        this.session = session;
        this.client = client;
        work = new StoreWork(logTag);
    }

    public bool IsSignedIn => session.IsSignedIn;
    public StoryRingDto[] Rings => rings;
    public StoryDto[] OpenStories => openStories;
    public string? OpenAuthorId => openAuthorId;
    public bool GroupLoading => groupLoading;
    public bool Posting => posting;

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
            var upload = await client.UploadUrlAsync("image/jpeg", "story", token).ConfigureAwait(false);
            if (upload is null)
            {
                return false;
            }

            var uploaded = await client.UploadImageAsync(upload.UploadUrl, baked.Bytes, "image/jpeg", token)
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
        work.Dispose();
    }
}
