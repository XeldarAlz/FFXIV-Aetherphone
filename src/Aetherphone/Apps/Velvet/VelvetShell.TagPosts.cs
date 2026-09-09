using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private const float TagPostsBottomPad = 40f;

    private readonly FeedVirtualizer tagPostsVirtualizer = new(400f);
    private string tagTitleToken = string.Empty;
    private string tagTitle = string.Empty;

    private void OpenTagPosts(string token) => router.Push(VelvetView.TagPosts(token));

    private string TagTitle(string token)
    {
        if (!string.Equals(tagTitleToken, token, StringComparison.Ordinal))
        {
            tagTitleToken = token;
            tagTitle = "#" + VelvetTokenLabels.Of(token);
        }

        return tagTitle;
    }

    private void DrawTagPosts(Rect area, string token)
    {
        var scale = UiScale.Current;
        if (VHeader.Push(area, TagTitle(token)))
        {
            router.Pop();
            return;
        }

        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + VHeader.Height * scale), area.Max);
        store.EnsureTagPosts(token, MutesFilter());
        var showing = string.Equals(store.TagToken, token, StringComparison.Ordinal);
        var posts = showing ? store.TagPosts : Array.Empty<VelvetPostDto>();
        using (AppSurface.BeginEdgeToEdge(body))
        {
            if (posts.Length == 0)
            {
                DrawEmpty(body, showing && store.TagPostsLoaded
                    ? Loc.T(L.Social.HashtagEmpty)
                    : Loc.T(L.Common.Loading), string.Empty);
                return;
            }

            var width = ScrollLayout.StableContentWidth();
            Gap(6f);
            tagPostsVirtualizer.BeginFrame(store.TagPostsSource);
            for (var index = 0; index < posts.Length; index++)
            {
                if (tagPostsVirtualizer.Skip(posts[index].Id))
                {
                    continue;
                }

                DrawPostCard(posts[index], width);
                tagPostsVirtualizer.Record(posts[index].Id);
            }

            if (store.TagPostsLoadingMore)
            {
                InfiniteScroll.DrawLoadingRow(body.Center.X, VelvetTheme.MutedInk);
            }

            Gap(TagPostsBottomPad);
            if (store.HasMoreTagPosts && !store.TagPostsLoadingMore && InfiniteScroll.ReachedBottom())
            {
                store.LoadMoreTagPosts();
            }
        }
    }
}
