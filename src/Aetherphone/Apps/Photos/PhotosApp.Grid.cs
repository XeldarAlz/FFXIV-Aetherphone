using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Apps.Photos;

internal sealed partial class PhotosApp
{
    private const long CoverBudgetBytes = 32L * 1024 * 1024;
    private const int CoverMaxDimension = 512;
    private const float TileBadgeInset = 6f;
    private const float PickerRingInset = 2f;
    private const float PickerRingStroke = 2.5f;
    private const float PickerBadgeRadius = 11f;
    private const float PickerBadgeRing = 1.5f;
    private const float PickerBadgeInset = 6f;
    private const float PickerCheckGlyph = 20f;
    private const float PickerVeil = 0.45f;
    private const float PickerSelectVeil = 0.22f;
    private const int OrderLabelCount = 100;
    private static readonly Vector4 Black = new(0f, 0f, 0f, 1f);
    private static readonly string[] OrderLabels = new string[OrderLabelCount];

    private enum GridMode : byte
    {
        Library,
        Album,
        Plain,
        Picker,
        Trash,
    }

    private enum TileHit : byte
    {
        None,
        Open,
        Menu,
    }

    private readonly TextureLedger covers = new(CoverBudgetBytes);

    private float DrawFlatGrid(ImDrawListPtr drawList, Vector2 origin, float width, string[] paths, GridMode mode,
        int albumKey, float scale)
    {
        var columns = Columns;
        var gap = GridGap * scale;
        var cell = (width - gap * (columns - 1)) / columns;
        var stride = cell + gap;
        var visible = VisibleWindow(cell);
        var rows = (paths.Length + columns - 1) / columns;
        var firstRow = Math.Max(0, (int)MathF.Floor((visible.X - origin.Y) / stride));
        var lastRow = Math.Min(rows - 1, (int)MathF.Ceiling((visible.Y - origin.Y) / stride));
        for (var row = firstRow; row <= lastRow; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var index = row * columns + column;
                if (index >= paths.Length)
                {
                    break;
                }

                var min = new Vector2(origin.X + column * stride, origin.Y + row * stride);
                var max = new Vector2(min.X + cell, min.Y + cell);
                DrawGridTile(drawList, min, max, paths, index, mode, albumKey, scale);
            }
        }

        return rows * stride + GridBottomPad * scale;
    }

    private void DrawGridTile(ImDrawListPtr drawList, Vector2 min, Vector2 max, string[] paths, int index,
        GridMode mode, int albumKey, float scale)
    {
        var path = paths[index];
        if (mode == GridMode.Picker)
        {
            DrawPickerTile(drawList, min, max, path, scale);
            return;
        }

        var hit = DrawTile(drawList, min, max, path, mode == GridMode.Album, scale);
        if (mode == GridMode.Trash)
        {
            PhotosChrome.TileCaption(drawList, min, max, DaysLeftLabel(DaysLeft(path)), scale);
        }

        switch (hit)
        {
            case TileHit.Menu:
                OpenPhotoSheet(albumKey, path);
                break;
            case TileHit.Open:
                ShowViewer(paths, index, mode == GridMode.Trash, true);
                break;
        }
    }

    private TileHit DrawTile(ImDrawListPtr drawList, Vector2 min, Vector2 max, string path, bool withMenu, float scale)
    {
        var hovered = UiInteract.Hover(min, max);
        PhotosChrome.Thumbnail(drawList, GetThumbnail(path), min, max, hovered && !selecting,
            PhotosChrome.Placeholder(ui), configuration.PhotosAspectGrid);
        if (favorites.Contains(path))
        {
            PhotosChrome.FavoriteBadge(drawList, min, max, scale);
        }

        if (selecting)
        {
            PhotosChrome.SelectionMark(drawList, min, max, selection.Contains(path), ui.Accent, scale);
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
            if (PhotosChrome.CoverBadge(drawList, badgeCenter, Loc.T(L.Photos.RemoveFromAlbum), scale))
            {
                return TileHit.Menu;
            }

            if (ImGui.IsMouseReleased(ImGuiMouseButton.Right))
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

    private void DrawPickerTile(ImDrawListPtr drawList, Vector2 min, Vector2 max, string path, float scale)
    {
        var alreadyInAlbum = pickerMembership.Contains(path);
        var order = 0;
        var isSelected = !alreadyInAlbum && pickerSelectionOrder.TryGetValue(path, out order);
        var hovered = !alreadyInAlbum && UiInteract.Hover(min, max);
        PhotosChrome.Thumbnail(drawList, GetThumbnail(path), min, max, hovered, PhotosChrome.Placeholder(ui));
        var badgeOffset = (PickerBadgeRadius + PickerBadgeInset) * scale;
        var badgeCenter = new Vector2(max.X - badgeOffset, max.Y - badgeOffset);
        if (alreadyInAlbum)
        {
            drawList.AddRectFilled(min, max, ImGui.GetColorU32(Palette.WithAlpha(Black, PickerVeil)));
            PhoneIcon.Draw(drawList, badgeCenter, PhoneIcons.CircleCheckFilled, WhiteMuted, PickerCheckGlyph * scale);
            return;
        }

        if (isSelected)
        {
            drawList.AddRectFilled(min, max, ImGui.GetColorU32(Palette.WithAlpha(White, PickerSelectVeil)));
            var inset = new Vector2(PickerRingInset * scale, PickerRingInset * scale);
            drawList.AddRect(min + inset, max - inset, ImGui.GetColorU32(ui.Accent), 0f, ImDrawFlags.None,
                PickerRingStroke * scale);
            var badgeRadius = PickerBadgeRadius * scale;
            drawList.AddCircleFilled(badgeCenter, badgeRadius + PickerBadgeRing * scale, ImGui.GetColorU32(White), 24);
            drawList.AddCircleFilled(badgeCenter, badgeRadius, ImGui.GetColorU32(ui.Accent), 24);
            Typography.DrawCentered(drawList, badgeCenter, OrderLabel(order), White, TextStyles.FootnoteEmphasized);
        }

        if (!hovered)
        {
            return;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (!UiInteract.Click(min, max, hovered))
        {
            return;
        }

        if (isSelected)
        {
            RemoveFromPickerSelection(path);
        }
        else
        {
            AddToPickerSelection(path);
        }
    }

    private static string OrderLabel(int order)
    {
        if (order < 1 || order >= OrderLabelCount)
        {
            return order.ToString(Loc.Culture);
        }

        return OrderLabels[order] ??= order.ToString(Loc.Culture);
    }

    private IDalamudTextureWrap? GetCover(string path)
    {
        if (covers.Get(path) is { } wrap)
        {
            return wrap;
        }

        if (failed.ContainsKey(path) || !loading.TryAdd((path, CoverLoad), 0))
        {
            return null;
        }

        QueueLoad(path, CoverLoad);
        return null;
    }

    private async Task LoadCoverAsync(string path)
    {
        try
        {
            var token = cancellation.Token;
            using var slot = await decodeSlots.EnterAsync(token).ConfigureAwait(false);
            var bytes = await File.ReadAllBytesAsync(path, token).ConfigureAwait(false);
            var wrap = await ImageProcessor.DecodeToTextureAsync(Plugin.TextureProvider, bytes, "cover:" + path,
                ImageProcessor.MaxLocalDecodePixels, CoverMaxDimension, token).ConfigureAwait(false);
            if (!covers.TryAdd(path, wrap))
            {
                wrap.Dispose();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            failed.TryAdd(path, 0);
            AepLog.Warning(exception, $"[Photos] cover failed for {Path.GetFileName(path)}");
        }
        finally
        {
            loading.TryRemove((path, CoverLoad), out _);
        }
    }
}
