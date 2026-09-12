using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Photos;

internal sealed partial class PhotosApp
{
    private enum SelectionScope : byte
    {
        Library,
        Album,
        Trash,
    }

    private enum TileHit : byte
    {
        None,
        Open,
        Menu,
    }

    private const float SelectToolbarIconSize = 26f;
    private const float SelectToolbarHitRadius = 20f;
    private const float SelectCancelPadX = 14f;
    private const float SelectCancelHeight = 30f;

    private void BeginSelect(SelectionScope scope)
    {
        selecting = true;
        selectionScope = scope;
        selection.Clear();
        selectionOrder.Clear();
    }

    private void EndSelect()
    {
        selecting = false;
        selection.Clear();
        selectionOrder.Clear();
    }

    private void ToggleSelected(string path)
    {
        if (selection.Remove(path))
        {
            selectionOrder.Remove(path);
            return;
        }

        selection.Add(path);
        selectionOrder.Add(path);
    }

    private string[] SelectionArray() => selectionOrder.ToArray();

    private bool AllSelectedFavorites()
    {
        if (selectionOrder.Count == 0)
        {
            return false;
        }

        for (var index = 0; index < selectionOrder.Count; index++)
        {
            if (!favorites.Contains(selectionOrder[index]))
            {
                return false;
            }
        }

        return true;
    }

    private void FavoriteSelection()
    {
        var remove = AllSelectedFavorites();
        for (var index = 0; index < selectionOrder.Count; index++)
        {
            if (remove)
            {
                favorites.Remove(selectionOrder[index]);
            }
            else
            {
                favorites.Add(selectionOrder[index]);
            }
        }

        BuildFavorites();
        SaveFavorites();
    }

    private bool DrawSelectHeaderIcon(Rect area, int slot)
    {
        var scale = UiScale.Current;
        return SocialChrome.DrawHeaderIcon(ImGui.GetWindowDrawList(), SocialChrome.HeaderSlot(area, slot),
            SocialChrome.HeaderIconRadius * scale, PhoneIcons.CircleCheck, HeaderIconSize, Loc.T(L.Photos.Select),
            Ink, Ink.TitleInk);
    }

    private void DrawSelectHeader(Rect area)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        var cancelLabel = Loc.T(L.Common.Cancel);
        var pillWidth = Typography.Measure(cancelLabel, DonePillStyle).X + SelectCancelPadX * 2f * scale;
        var pillHeight = SelectCancelHeight * scale;
        var pillRight = area.Max.X - CellPadX * scale;
        var pill = new Rect(new Vector2(pillRight - pillWidth, rowCenterY - pillHeight * 0.5f),
            new Vector2(pillRight, rowCenterY + pillHeight * 0.5f));
        var title = selection.Count > 0
            ? Loc.Plural(L.Photos.Selected, selection.Count)
            : Loc.T(L.Photos.SelectPhotos);
        var titleLeft = area.Min.X + CellPadX * scale;
        var fitted = Typography.FitText(title, MathF.Max(1f, pill.Min.X - Metrics.Space.Sm * scale - titleLeft),
            ScreenTitleStyle);
        Typography.Draw(drawList, new Vector2(titleLeft, rowCenterY - Typography.LineHeight(ScreenTitleStyle) * 0.5f),
            fitted, Ink.TitleInk, ScreenTitleStyle);
        if (SocialPill.Flat(drawList, pill, cancelLabel, Ink.ButtonFill, Ink.ButtonHover, default, Ink.TitleInk,
                DonePillStyle, pillHeight * 0.5f))
        {
            EndSelect();
        }
    }

    private Rect ToolbarRect(Rect area) =>
        new(new Vector2(area.Min.X, area.Max.Y - BottomTabBar.Height * UiScale.Current), area.Max);

    private void DrawSelectToolbarIfActive(Rect area)
    {
        if (selecting)
        {
            DrawSelectToolbar(ToolbarRect(area));
        }
    }

    private void DrawSelectToolbar(Rect bar)
    {
        var drawList = ImGui.GetWindowDrawList();
        SocialChrome.PaintBarBackdrop(ui, drawList, bar, frameScreen);
        FeedCell.Hairline(drawList, bar.Min.X, bar.Max.X, bar.Min.Y + 1f, Ink.Hairline);
        var enabled = selection.Count > 0;
        if (selectionScope == SelectionScope.Trash)
        {
            if (ToolbarAction(drawList, ToolbarSlot(bar, 0, 2), PhoneIcons.ArrowBackUp, Loc.T(L.Photos.Recover),
                    Ink.TitleInk, enabled))
            {
                RecoverPhotos(SelectionArray());
            }

            if (ToolbarAction(drawList, ToolbarSlot(bar, 1, 2), PhoneIcons.Trash, Loc.T(L.Photos.DeletePermanently),
                    Ink.Danger, enabled))
            {
                AskDeleteForever(SelectionArray());
            }

            return;
        }

        var allFavorites = AllSelectedFavorites();
        if (ToolbarAction(drawList, ToolbarSlot(bar, 0, 3), allFavorites ? PhoneIcons.HeartFilled : PhoneIcons.Heart,
                Loc.T(allFavorites ? L.Photos.Unfavorite : L.Photos.Favorite), Ink.TitleInk, enabled))
        {
            FavoriteSelection();
        }

        if (ToolbarAction(drawList, ToolbarSlot(bar, 1, 3), PhoneIcons.SquareRoundedPlus, Loc.T(L.Photos.AddToAlbum),
                Ink.TitleInk, enabled))
        {
            OpenAddToAlbum(SelectionArray());
        }

        if (ToolbarAction(drawList, ToolbarSlot(bar, 2, 3), PhoneIcons.Trash, Loc.T(L.Photos.Delete), Ink.Danger,
                enabled))
        {
            AskDeletePhotos(SelectionArray());
        }
    }

    private static Vector2 ToolbarSlot(Rect bar, int index, int count) =>
        new(bar.Min.X + bar.Width / count * (index + 0.5f), bar.Center.Y);

    private bool ToolbarAction(ImDrawListPtr drawList, Vector2 center, string glyph, string tooltip, Vector4 ink,
        bool enabled)
    {
        var scale = UiScale.Current;
        if (!enabled)
        {
            PhoneIcon.Draw(drawList, center, glyph, Ink.FaintInk, SelectToolbarIconSize * scale);
            return false;
        }

        return SocialChrome.DrawHeaderIcon(drawList, center, SelectToolbarHitRadius * scale, glyph,
            SelectToolbarIconSize, tooltip, Ink, ink, side: HoverLabelSide.Above);
    }

    private void OpenTrashViewer(int index)
    {
        viewerPaths = trashPaths;
        viewerIndex = Math.Clamp(index, 0, trashPaths.Length - 1);
        viewerInTrash = true;
        zoomView.Reset();
        router.Push(PhotoView.Viewer());
    }

    private TileHit DrawTile(ImDrawListPtr drawList, Vector2 min, Vector2 max, string path, bool withMenu, float scale)
    {
        var hovered = UiInteract.Hover(min, max);
        PhotosChrome.Thumbnail(drawList, GetThumbnail(path), min, max, hovered && !selecting, Ink.ThumbFill,
            configuration.PhotosAspectGrid);
        if (favorites.Contains(path))
        {
            PhotosChrome.FavoriteBadge(drawList, min, max, Ink, scale);
        }

        if (selecting)
        {
            PhotosChrome.SelectionMark(drawList, min, max, selection.Contains(path), Ink, scale);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(min, max, hovered))
            {
                ToggleSelected(path);
            }

            return TileHit.None;
        }

        var overBadge = false;
        if (withMenu && hovered)
        {
            var badgeOffset = (PhotosChrome.BadgeRadius + TileBadgeInset) * scale;
            var badgeCenter = new Vector2(max.X - badgeOffset, min.Y + badgeOffset);
            var extent = new Vector2(PhotosChrome.BadgeRadius * scale, PhotosChrome.BadgeRadius * scale);
            overBadge = UiInteract.Hover(badgeCenter - extent, badgeCenter + extent);
            if (PhotosChrome.CoverBadge(drawList, badgeCenter, Loc.T(L.Photos.RemoveFromAlbum), Ink, scale))
            {
                return TileHit.Menu;
            }

            if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            {
                return TileHit.Menu;
            }
        }

        if (hovered && !overBadge)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(min, max, hovered && !overBadge) ? TileHit.Open : TileHit.None;
    }
}
