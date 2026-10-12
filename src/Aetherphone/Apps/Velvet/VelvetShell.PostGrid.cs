using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private enum PostGridSource
    {
        Archive,
        Liked,
    }

    private const float PostGridTopGap = 6f;
    private const float PostGridBottomPad = 40f;

    private void OpenLiked()
    {
        store.RefreshLiked();
        router.Push(VelvetView.Liked);
    }

    private void DrawArchive(Rect area) =>
        DrawPostGridScreen(area, PostGridSource.Archive, L.Social.ArchiveTitle, L.Social.ArchiveEmpty,
            L.Social.ArchiveEmptyHint);

    private void DrawLiked(Rect area) =>
        DrawPostGridScreen(area, PostGridSource.Liked, L.Social.LikedTitle, L.Social.LikedEmpty,
            L.Social.LikedEmptyHint);

    private void DrawPostGridScreen(Rect area, PostGridSource source, LocString title, LocString emptyTitle,
        LocString emptyHint)
    {
        var scale = UiScale.Current;
        if (VHeader.Push(area, Loc.T(title)))
        {
            router.Pop();
            return;
        }

        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + VHeader.Height * scale), area.Max);
        var posts = GridPosts(source);
        using (AppSurface.BeginEdgeToEdge(body))
        {
            if (posts.Length == 0)
            {
                if (GridLoaded(source))
                {
                    DrawEmpty(body, Loc.T(emptyTitle), Loc.T(emptyHint));
                }
                else
                {
                    DrawEmpty(body, Loc.T(L.Common.Loading), string.Empty);
                }

                return;
            }

            var width = ScrollLayout.StableContentWidth();
            Gap(PostGridTopGap);
            DrawPostTiles(posts, width);
            var loadingMore = GridLoadingMore(source);
            if (loadingMore)
            {
                InfiniteScroll.DrawLoadingRow(body.Center.X, VelvetTheme.MutedInk);
            }

            Gap(PostGridBottomPad);
            if (GridHasMore(source) && !loadingMore && InfiniteScroll.ReachedBottom())
            {
                LoadMoreGrid(source);
            }
        }
    }

    private VelvetPostDto[] GridPosts(PostGridSource source) =>
        source == PostGridSource.Liked ? store.LikedPosts : store.ArchivedPosts;

    private bool GridLoaded(PostGridSource source) =>
        source == PostGridSource.Liked ? store.LikedLoaded : store.ArchivedLoaded;

    private bool GridLoadingMore(PostGridSource source) =>
        source == PostGridSource.Liked ? store.LikedLoadingMore : store.ArchivedLoadingMore;

    private bool GridHasMore(PostGridSource source) =>
        source == PostGridSource.Liked ? store.HasMoreLiked : store.HasMoreArchived;

    private void LoadMoreGrid(PostGridSource source)
    {
        if (source == PostGridSource.Liked)
        {
            store.LoadMoreLiked();
            return;
        }

        store.LoadMoreArchived();
    }

    private void DrawPostTiles(VelvetPostDto[] posts, float width)
    {
        var scale = UiScale.Current;
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
            if (!ImGui.IsRectVisible(min, max))
            {
                continue;
            }

            var veiled = SensitiveReveals.ShouldVeil(post.Sensitive, post.Id, configuration.ShowSensitiveContent);
            DrawMedia(drawList, min, max, post.MediaUrl, 0f, veiled: veiled);
            if (UiInteract.Click(min, max))
            {
                OpenPostDetail(post.Id);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rows * cell + (rows - 1) * cellGap));
    }
}
