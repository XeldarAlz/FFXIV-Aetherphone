using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Apps.LoveIsland;

internal sealed class GalleryLane : IDisposable
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(60);

    private readonly GramClient client;
    private readonly AethernetSession session;
    private readonly StoreWork work = new("loveisland.gallery");
    private volatile PostDto[] posts = Array.Empty<PostDto>();
    private volatile string? cursor;
    private volatile bool loading;
    private volatile bool loadingMore;
    private volatile bool loadedOnce;
    private volatile bool failed;
    private string tag = string.Empty;
    private string? tagSource;
    private DateTime fetchedUtc = DateTime.MinValue;

    public GalleryLane(GramClient client, AethernetSession session)
    {
        this.client = client;
        this.session = session;
    }

    public PostDto[] Posts => posts;

    public bool Loading => loading;

    public bool LoadingMore => loadingMore;

    public bool LoadedOnce => loadedOnce;

    public bool Failed => failed;

    public bool HasMore => cursor is not null;

    public void EnsureFresh(string hashtag)
    {
        if (!ReferenceEquals(hashtag, tagSource))
        {
            tagSource = hashtag;
            var normalized = hashtag.ToLowerInvariant();
            if (!string.Equals(normalized, tag, StringComparison.Ordinal))
            {
                tag = normalized;
                posts = Array.Empty<PostDto>();
                cursor = null;
                loadedOnce = false;
                fetchedUtc = DateTime.MinValue;
            }
        }

        if (DateTime.UtcNow - fetchedUtc < StaleAfter)
        {
            return;
        }

        Refresh();
    }

    public void Refresh()
    {
        if (!session.IsSignedIn || loading || tag.Length == 0)
        {
            return;
        }

        loading = true;
        fetchedUtc = DateTime.UtcNow;
        var requested = tag;
        work.Run("gallery refresh", async token =>
        {
            var page = await client.TagPostsAsync(requested, null, token).ConfigureAwait(false);
            if (page is null)
            {
                failed = true;
                return;
            }

            failed = false;
            posts = page.Items ?? Array.Empty<PostDto>();
            cursor = page.NextCursor;
            loadedOnce = true;
        }, () => loading = false);
    }

    public void LoadMore()
    {
        var next = cursor;
        if (!session.IsSignedIn || next is null || loading || loadingMore)
        {
            return;
        }

        loadingMore = true;
        var requested = tag;
        work.Run("gallery more", async token =>
        {
            var page = await client.TagPostsAsync(requested, next, token).ConfigureAwait(false);
            if (page is null)
            {
                return;
            }

            posts = IdentifiedMerge.AppendNew(posts, page.Items ?? Array.Empty<PostDto>());
            cursor = page.NextCursor;
        }, () => loadingMore = false);
    }

    public void Dispose()
    {
        work.Dispose();
    }
}
