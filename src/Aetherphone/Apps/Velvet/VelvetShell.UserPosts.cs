using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private const float GridEmptyTop = 24f;
    private const float GridEmptyPad = 48f;
    private const float GridEmptyBottom = 80f;
    private const float GridTeaserGap = 14f;
    private const float GridTeaserBottom = 34f;
    private const float GridUnlockBottom = 30f;
    private const float GridBadgeInset = 8f;
    private const float UserPostsBottomPad = 40f;

    private readonly FeedVirtualizer userPostsVirtualizer = new(400f);
    private string userPostsStartId = string.Empty;
    private bool userPostsJumpPending;

    private void OpenUserPosts(string userId, string postId)
    {
        userPostsStartId = postId;
        userPostsJumpPending = true;
        router.Push(VelvetView.UserPosts(userId));
    }

    private void DrawPostGrid(VelvetProfileDto user, bool isMe, bool connected, float width)
    {
        var scale = UiScale.Current;
        store.EnsureUserPosts(user.UserId);
        var loaded = store.UserPostsUserId == user.UserId && store.UserPostsLoaded;
        var posts = loaded ? store.UserPosts : Array.Empty<VelvetPostDto>();
        var totalCount = loaded ? store.UserPostsTotal : 0;
        if (posts.Length == 0)
        {
            if (!loaded && !store.UserPostsFailed)
            {
                DrawGridEmpty(width, Loc.T(L.Common.Loading));
            }
            else if (!isMe && !connected)
            {
                DrawLockedPosts(DisplayNameOf(user.DisplayName, user.Handle), width, totalCount);
            }
            else
            {
                DrawGridEmpty(width, isMe ? Loc.T(L.Velvet.NoPostsMine) : Loc.T(L.Velvet.NoPostsShared));
            }

            return;
        }

        var cellGap = ProfileGridGap * scale;
        var cell = (width - cellGap * (ProfileColumns - 1)) / ProfileColumns;
        var rows = (posts.Length + ProfileColumns - 1) / ProfileColumns;
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        for (var index = 0; index < posts.Length; index++)
        {
            var post = posts[index];
            var row = index / ProfileColumns;
            var column = index % ProfileColumns;
            var min = new Vector2(origin.X + column * (cell + cellGap), origin.Y + row * (cell + cellGap));
            var max = new Vector2(min.X + cell, min.Y + cell);
            var veiled = SensitiveReveals.ShouldVeil(post.Sensitive, post.Id, configuration.ShowSensitiveContent);
            DrawMedia(drawList, min, max, post.MediaUrl, 0f, veiled: veiled);
            if (!veiled && PostMedia.Photos(post.MediaUrls, post.MediaUrl).Length > 1)
            {
                MultiPhotoBadge.Draw(drawList,
                    new Vector2(max.X - GridBadgeInset * scale, min.Y + GridBadgeInset * scale), scale);
            }

            if (UiInteract.Click(min, max))
            {
                OpenUserPosts(user.UserId, post.Id);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rows * cell + (rows - 1) * cellGap));
        if (store.UserPostsLoadingMore)
        {
            InfiniteScroll.DrawLoadingRow(origin.X + width * 0.5f, VelvetTheme.MutedInk);
        }
        else if (store.HasMoreUserPosts && InfiniteScroll.ReachedBottom())
        {
            store.LoadMoreUserPosts();
        }

        if (isMe || connected || totalCount <= posts.Length)
        {
            Gap(ProfileGridGap);
            return;
        }

        Gap(GridTeaserGap);
        Typography.DrawWrappedCentered(new Vector2(origin.X + width * 0.5f, ImGui.GetCursorScreenPos().Y),
            Loc.Plural(L.Velvet.ConnectToUnlockPosts, totalCount - posts.Length), VelvetTheme.RoseInk,
            TextStyles.Callout, width - GridEmptyPad * scale);
        Gap(GridUnlockBottom);
    }

    private void DrawLockedPosts(string name, float width, int totalCount)
    {
        var scale = UiScale.Current;
        var cellGap = ProfileGridGap * scale;
        var cell = (width - cellGap * (ProfileColumns - 1)) / ProfileColumns;
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        for (var column = 0; column < ProfileColumns; column++)
        {
            var min = new Vector2(origin.X + column * (cell + cellGap), origin.Y);
            var max = new Vector2(min.X + cell, min.Y + cell);
            VMediaTile.Conceal(drawList, min, max, 0f, string.Empty, 0f);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cell));
        Gap(GridTeaserGap);
        var teaser = totalCount > 0
            ? Loc.Plural(L.Velvet.ConnectToUnlockPosts, totalCount)
            : Loc.T(L.Velvet.ConnectToSeePosts, name);
        Typography.DrawWrappedCentered(new Vector2(origin.X + width * 0.5f, ImGui.GetCursorScreenPos().Y), teaser,
            VelvetTheme.RoseInk, TextStyles.Callout, width - GridEmptyPad * scale);
        Gap(GridTeaserBottom);
    }

    private static void DrawGridEmpty(float width, string text)
    {
        var scale = UiScale.Current;
        var origin = ImGui.GetCursorScreenPos();
        Typography.DrawWrappedCentered(new Vector2(origin.X + width * 0.5f, origin.Y + GridEmptyTop * scale), text,
            VelvetTheme.MutedInk, TextStyles.Subheadline, width - GridEmptyPad * scale);
        Gap(GridEmptyBottom);
    }

    private void DrawUserPosts(Rect area, string userId)
    {
        var scale = UiScale.Current;
        if (VHeader.Push(area, Loc.T(L.Velvet.Posts)))
        {
            router.Pop();
            return;
        }

        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + VHeader.Height * scale), area.Max);
        store.EnsureUserPosts(userId);
        SyncFeedRelations();
        var posts = store.UserPostsUserId == userId ? store.UserPosts : Array.Empty<VelvetPostDto>();
        using (var surface = AppSurface.BeginEdgeToEdge(body))
        {
            if (posts.Length == 0)
            {
                var settled = store.UserPostsUserId == userId && (store.UserPostsLoaded || store.UserPostsFailed);
                DrawEmpty(body, settled ? Loc.T(L.Velvet.NoPostsShared) : Loc.T(L.Common.Loading), string.Empty);
                return;
            }

            var width = ScrollLayout.StableContentWidth();
            var contentTop = ImGui.GetCursorPosY();
            var jumpY = -1f;
            userPostsVirtualizer.BeginFrame();
            for (var index = 0; index < posts.Length; index++)
            {
                var post = posts[index];
                if (userPostsJumpPending && string.Equals(post.Id, userPostsStartId, StringComparison.Ordinal))
                {
                    jumpY = ImGui.GetCursorPosY() - contentTop;
                }

                if (userPostsVirtualizer.Skip(post.Id))
                {
                    continue;
                }

                DrawPostCard(post, width);
                userPostsVirtualizer.Record(post.Id);
            }

            if (store.UserPostsLoadingMore)
            {
                InfiniteScroll.DrawLoadingRow(body.Center.X, VelvetTheme.MutedInk);
            }

            Gap(UserPostsBottomPad);
            if (store.HasMoreUserPosts && !store.UserPostsLoadingMore && InfiniteScroll.ReachedBottom())
            {
                store.LoadMoreUserPosts();
            }

            if (!userPostsJumpPending)
            {
                return;
            }

            userPostsJumpPending = false;
            if (jumpY >= 0f)
            {
                surface.JumpTo(jumpY);
            }
        }
    }
}
