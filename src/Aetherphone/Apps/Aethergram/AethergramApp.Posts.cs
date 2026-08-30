using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Aethergram;

internal sealed partial class AethergramApp
{
    private const float PostsEmptyOffset = 60f;

    private readonly FeedVirtualizer postsVirtualizer = new(400f);
    private bool postsJumpPending;

    private void OpenPosts(string postId, PostSource source)
    {
        postsJumpPending = true;
        router.Push(AethergramRoute.Posts(postId, source));
    }

    private void DrawPosts(Rect area, string startPostId, PostSource source)
    {
        var scale = UiScale.Current;
        var title = Loc.T(source == PostSource.Tagged ? L.PhotoTag.TaggedTab : L.PhotoTag.PostsTab);
        DrawScreenHeader(area, title, 0, true, false, PostsSubtitle(source));
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        var posts = PostsOf(source);
        using (var surface = AppSurface.BeginEdgeToEdge(body))
        {
            if (posts.Length == 0)
            {
                var message = PostsLoading(source) ? Loc.T(L.Common.Loading) : Loc.T(L.Aethergram.Empty);
                Typography.DrawCentered(new Vector2(body.Center.X, body.Min.Y + PostsEmptyOffset * scale), message,
                    Ink.MutedInk);
                return;
            }

            var contentTop = ImGui.GetCursorPosY();
            var jumpY = -1f;
            postsVirtualizer.BeginFrame();
            for (var index = 0; index < posts.Length; index++)
            {
                var post = posts[index];
                if (HiddenByMediaPreference(post))
                {
                    continue;
                }

                if (postsJumpPending && string.Equals(post.Id, startPostId, StringComparison.Ordinal))
                {
                    jumpY = ImGui.GetCursorPosY() - contentTop;
                }

                var revision = post.CommentCount > 0 ? 1 : 0;
                if (postsVirtualizer.Skip(post.Id, revision))
                {
                    continue;
                }

                DrawGramCard(post);
                postsVirtualizer.Record(post.Id, revision);
            }

            if (PostsLoadingMore(source))
            {
                InfiniteScroll.DrawLoadingRow(body.Center.X, Ink.MutedInk);
            }

            ImGui.Dummy(new Vector2(0f, 16f * scale));
            if (InfiniteScroll.ReachedBottom() && PostsHasMore(source) && !PostsLoadingMore(source))
            {
                LoadMorePosts(source);
            }

            if (!postsJumpPending)
            {
                return;
            }

            postsJumpPending = false;
            if (jumpY >= 0f)
            {
                surface.JumpTo(jumpY);
            }
        }
    }

    private string PostsSubtitle(PostSource source)
    {
        switch (source)
        {
            case PostSource.Profile:
            case PostSource.Tagged:
                if (store.ProfileUser is not { } user)
                {
                    return string.Empty;
                }

                return user.Handle.Length > 0 ? user.Handle : user.DisplayName;
            case PostSource.Saved:
                return Loc.T(L.Aethergram.SavedTitle);
            case PostSource.Hashtag:
                return store.HashtagTag is { } tag ? HashtagTitle(tag) : string.Empty;
            default:
                return string.Empty;
        }
    }

    private PostDto[] PostsOf(PostSource source) => source switch
    {
        PostSource.Tagged => store.TaggedPosts,
        PostSource.Saved => store.SavedPosts,
        PostSource.Hashtag => store.HashtagPosts,
        PostSource.Explore => store.Feed(SocialFeedScope.ForYou),
        _ => store.ProfilePosts,
    };

    private bool PostsLoading(PostSource source) => source switch
    {
        PostSource.Tagged => store.TaggedLoading,
        PostSource.Saved => store.SavedLoading,
        PostSource.Hashtag => store.HashtagLoading,
        PostSource.Explore => store.IsLoading(SocialFeedScope.ForYou),
        _ => store.ProfileLoading,
    };

    private bool PostsLoadingMore(PostSource source) => source switch
    {
        PostSource.Tagged => store.TaggedLoadingMore,
        PostSource.Saved => store.SavedLoadingMore,
        PostSource.Hashtag => store.HashtagLoadingMore,
        PostSource.Explore => store.LoadingMore(SocialFeedScope.ForYou),
        _ => store.ProfileLoadingMore,
    };

    private bool PostsHasMore(PostSource source) => source switch
    {
        PostSource.Tagged => store.HasMoreTagged,
        PostSource.Saved => store.HasMoreSaved,
        PostSource.Hashtag => store.HasMoreHashtagPosts,
        PostSource.Explore => store.HasMoreFeed(SocialFeedScope.ForYou),
        _ => store.HasMoreProfilePosts,
    };

    private void LoadMorePosts(PostSource source)
    {
        switch (source)
        {
            case PostSource.Tagged:
                store.LoadMoreTaggedPosts();
                break;
            case PostSource.Saved:
                store.LoadMoreSaved();
                break;
            case PostSource.Hashtag:
                store.LoadMoreHashtagPosts();
                break;
            case PostSource.Explore:
                store.LoadMoreFeed(SocialFeedScope.ForYou);
                break;
            default:
                store.LoadMoreProfilePosts();
                break;
        }
    }
}
