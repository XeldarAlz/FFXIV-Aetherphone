using Aetherphone.Core;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Photos;

internal sealed partial class PhotosApp
{
    private enum NameSheetMode : byte
    {
        Create,
        Rename,
    }

    private enum FlatGridMode : byte
    {
        Album,
        Picker,
        Plain,
        Trash,
    }

    private const int AlbumColumns = 2;
    private const int AlbumSheetItemCount = 3;
    private const int TrashSheetItemCount = 2;
    private const int MaxAlbumNameLength = 64;
    private const int OrderLabelCount = 100;
    private const float AlbumGap = 12f;
    private const float AlbumRounding = 14f;
    private const float AlbumTextBlock = 44f;
    private const float AlbumTitleGap = 8f;
    private const float AlbumCountGap = 19f;
    private const float AlbumTextInset = 2f;
    private const float AlbumBadgeInset = 8f;
    private const float TileBadgeInset = 6f;
    private const float MonthCardWidth = 128f;
    private const float RailGap = 10f;
    private const float SectionGap = 14f;
    private const float AlbumsTopPad = 4f;
    private const float AlbumsBottomPad = 24f;
    private const float CollectionGlyph = 22f;
    private const float CollectionGlyphGap = 12f;
    private const float CollectionChevron = 16f;
    private const float CollectionChevronGap = 6f;
    private const float FlatGridTopPad = 2f;
    private const float TrashHintPadY = 10f;
    private const float TrashHintMaxWidth = 300f;
    private const float PickerRingInset = 2f;
    private const float PickerRingStroke = 2.5f;
    private const float PickerBadgeRadius = 11f;
    private const float PickerBadgeRing = 1.5f;
    private const float PickerBadgeInset = 6f;
    private const float PickerCheckGlyph = 20f;
    private const float PickerVeil = 0.45f;
    private const float PickerSelectVeil = 0.22f;
    private const float DonePillHeight = 30f;
    private const float DonePillPadX = 14f;
    private const float SheetFieldHeight = 40f;
    private const float SheetFieldGap = 14f;
    private const float SheetPillHeight = 44f;
    private const float SheetHintGap = 10f;
    private const float SheetMinimumFraction = 0.28f;
    private const float SheetMaximumFraction = 0.6f;

    private static readonly TextStyle DonePillStyle = TextStyles.SubheadlineEmphasized;
    private static readonly Vector4 Black = new(0f, 0f, 0f, 1f);
    private static readonly string[] OrderLabels = new string[OrderLabelCount];

    private void DrawAlbumsTab(Rect body)
    {
        if (!configuration.PhotosMonthlyAlbumsAsked && entries.Length > 0)
        {
            AskMonthlyAlbums();
        }

        var scale = UiScale.Current;
        var albumsKey = ImGui.GetID("##photoAlbums");
        ImGui.SetCursorScreenPos(body.Min);
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, Vector2.Zero))
        using (var child = ImRaii.Child("##photoAlbums", body.Size, false,
                   DragScrollHost.ScrollFlags(ImGuiWindowFlags.NoBackground)))
        {
            if (!child)
            {
                return;
            }

            AppSurface.ResetScrollOnNewVisit();
            var surface = DragScrollHost.Begin(albumsKey);
            if (resetScroll)
            {
                surface.JumpToTop();
                resetScroll = false;
            }

            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var drawList = ImGui.GetWindowDrawList();
            var left = origin.X + CellPadX * scale;
            var right = origin.X + width - CellPadX * scale;
            var y = origin.Y + AlbumsTopPad * scale;
            PhotosChrome.SectionTitle(drawList, left, right, y, Loc.T(L.Photos.MyAlbums), Ink, scale);
            y += PhotosChrome.SectionTitleHeight * scale;
            y = DrawCustomAlbumsGrid(drawList, left, right, y, scale);
            if (configuration.PhotosMonthlyAlbums && albums.Count > 0)
            {
                y += SectionGap * scale;
                PhotosChrome.SectionTitle(drawList, left, right, y, Loc.T(L.Photos.Months), Ink, scale);
                y += PhotosChrome.SectionTitleHeight * scale;
                y = DrawMonthsRail(drawList, origin.X, width, y, scale);
            }

            y += SectionGap * scale;
            PhotosChrome.SectionTitle(drawList, left, right, y, Loc.T(L.Photos.Collections), Ink, scale);
            y += PhotosChrome.SectionTitleHeight * scale;
            y = DrawCollectionRows(drawList, left, right, y, scale);

            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, y - origin.Y + AlbumsBottomPad * scale));
        }
    }

    private float DrawCustomAlbumsGrid(ImDrawListPtr drawList, float left, float right, float top, float scale)
    {
        var gap = AlbumGap * scale;
        var tile = (right - left - gap) / AlbumColumns;
        var cardHeight = tile + AlbumTextBlock * scale;
        var total = customAlbums.Count + 1;
        for (var index = 0; index < total; index++)
        {
            var column = index % AlbumColumns;
            var rowIndex = index / AlbumColumns;
            var min = new Vector2(left + column * (tile + gap), top + rowIndex * (cardHeight + gap));
            var coverMax = new Vector2(min.X + tile, min.Y + tile);
            if (index == customAlbums.Count)
            {
                if (PhotosChrome.NewAlbumTile(drawList, min, coverMax, AlbumRounding * scale,
                        Loc.T(L.Photos.CreateAlbum), Ink, scale))
                {
                    OpenCreateAlbumSheet(null);
                }

                continue;
            }

            var rect = new Rect(min, new Vector2(coverMax.X, min.Y + cardHeight));
            DrawCustomAlbumCard(drawList, rect, customAlbums[index], tile, scale);
        }

        var rows = (total + AlbumColumns - 1) / AlbumColumns;
        return top + rows * cardHeight + (rows - 1) * gap;
    }

    private void DrawCustomAlbumCard(ImDrawListPtr drawList, Rect rect, CustomAlbum album, float tile, float scale)
    {
        var coverMax = new Vector2(rect.Min.X + tile, rect.Min.Y + tile);
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var coverPath = cachedCustomAlbumPaths.TryGetValue(album.Key, out var paths) && paths.Length > 0
            ? paths[0]
            : null;
        PhotosChrome.Cover(drawList, coverPath is null ? null : GetThumbnail(coverPath), rect.Min, coverMax,
            AlbumRounding * scale, Ink, scale, hovered);
        DrawAlbumCaption(drawList, rect, coverMax.Y, new MarqueeId("photos.album.", album.Key), album.Name, album.Count,
            hovered, scale);
        var overBadge = false;
        if (hovered || (albumSheet.IsOpen && albumSheetKey == album.Key))
        {
            var badgeOffset = (PhotosChrome.BadgeRadius + AlbumBadgeInset) * scale;
            var badgeCenter = new Vector2(coverMax.X - badgeOffset, rect.Min.Y + badgeOffset);
            var extent = new Vector2(PhotosChrome.BadgeRadius * scale, PhotosChrome.BadgeRadius * scale);
            overBadge = UiInteract.Hover(badgeCenter - extent, badgeCenter + extent);
            if (PhotosChrome.CoverBadge(drawList, badgeCenter, Loc.T(L.Photos.AlbumOptions), Ink, scale))
            {
                OpenAlbumSheet(album.Key);
                return;
            }
        }

        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            OpenAlbumSheet(album.Key);
            return;
        }

        if (hovered && !overBadge)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(rect.Min, rect.Max, hovered && !overBadge))
        {
            OpenAlbum(album.Key);
        }
    }

    private void DrawAlbumCaption(ImDrawListPtr drawList, Rect rect, float coverBottom, MarqueeId id, string title,
        int count, bool hovered, float scale)
    {
        var textTop = coverBottom + AlbumTitleGap * scale;
        var textLeft = rect.Min.X + AlbumTextInset * scale;
        Marquee.DrawLeft(id, title, textLeft, textTop, rect.Width - AlbumTextInset * 2f * scale,
            TextStyles.SubheadlineEmphasized, Ink.TitleInk, hovered);
        Typography.Draw(drawList, new Vector2(textLeft, textTop + AlbumCountGap * scale),
            Loc.Plural(L.Photos.Count, count), Ink.MutedInk, TextStyles.Footnote);
    }

    private float DrawMonthsRail(ImDrawListPtr drawList, float left, float width, float top, float scale)
    {
        var cardWidth = MonthCardWidth * scale;
        var cardHeight = cardWidth + AlbumTextBlock * scale;
        var gap = RailGap * scale;
        var inset = CellPadX * scale;
        var row = new Rect(new Vector2(left, top), new Vector2(left + width, top + cardHeight));
        var count = albums.Count;
        monthsRail.Begin(row, inset * 2f + count * cardWidth + (count - 1) * gap);
        var x = row.Min.X + inset - monthsRail.Offset;
        for (var index = 0; index < count; index++)
        {
            var min = new Vector2(x, top);
            x += cardWidth + gap;
            if (min.X + cardWidth < row.Min.X || min.X > row.Max.X)
            {
                continue;
            }

            var album = albums[index];
            var rect = new Rect(min, new Vector2(min.X + cardWidth, top + cardHeight));
            if (DrawSmartAlbumCard(drawList, rect, album.Title, album.Start, album.Count, cardWidth, scale))
            {
                OpenAlbum(album.Key);
            }
        }

        monthsRail.End();
        return top + cardHeight;
    }

    private bool DrawSmartAlbumCard(ImDrawListPtr drawList, Rect rect, string title, int coverStart, int coverCount,
        float tile, float scale)
    {
        var coverMax = new Vector2(rect.Min.X + tile, rect.Min.Y + tile);
        var hovered = monthsRail.Hover(rect.Min, rect.Max);
        var cover = coverCount > 0 ? GetThumbnail(entries[coverStart].Path) : null;
        PhotosChrome.Cover(drawList, cover, rect.Min, coverMax, AlbumRounding * scale, Ink, scale, hovered);
        DrawAlbumCaption(drawList, rect, coverMax.Y, new MarqueeId("photos.month.", title), title, coverCount, hovered,
            scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return monthsRail.Tapped(rect.Min, rect.Max, hovered);
    }

    private float DrawCollectionRows(ImDrawListPtr drawList, float left, float right, float top, float scale)
    {
        var rowHeight = Metrics.Size.Row * scale;
        var rounding = Metrics.Radius.Card * scale;
        var min = new Vector2(left, top);
        var max = new Vector2(right, top + rowHeight * 3f);
        ui.Card(drawList, min, max, rounding);
        var y = top;
        if (DrawCollectionRow(drawList, left, right, y, rowHeight, PhoneIcons.HeartFilled, Ink.LikeRed,
                Loc.T(L.Photos.Favorites), favoritePaths.Length, true, scale))
        {
            OpenAlbum(PhotoView.FavoritesKey);
        }

        y += rowHeight;
        if (DrawCollectionRow(drawList, left, right, y, rowHeight, PhoneIcons.Photo, Ink.AccentLink,
                Loc.T(L.Photos.Recents), entries.Length, true, scale))
        {
            OpenAlbum(PhotoView.RecentsKey);
        }

        y += rowHeight;
        if (DrawCollectionRow(drawList, left, right, y, rowHeight, PhoneIcons.Trash, Ink.MutedInk,
                Loc.T(L.Photos.RecentlyDeleted), trashPaths.Length, false, scale))
        {
            OpenAlbum(PhotoView.TrashKey);
        }

        return max.Y;
    }

    private bool DrawCollectionRow(ImDrawListPtr drawList, float left, float right, float top, float height,
        string glyph, Vector4 glyphInk, string label, int count, bool hairline, float scale)
    {
        var min = new Vector2(left, top);
        var max = new Vector2(right, top + height);
        var hovered = UiInteract.Hover(min, max);
        if (hovered)
        {
            UiInteract.HoverHighlight(drawList, min, max, Metrics.Radius.Card * scale);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var centerY = top + height * 0.5f;
        var padX = Metrics.Space.Lg * scale;
        var glyphSize = CollectionGlyph * scale;
        PhoneIcon.Draw(drawList, new Vector2(left + padX + glyphSize * 0.5f, centerY), glyph, glyphInk, glyphSize);
        var textLeft = left + padX + glyphSize + CollectionGlyphGap * scale;
        var chevronSize = CollectionChevron * scale;
        var chevronCenter = new Vector2(right - padX - chevronSize * 0.5f, centerY);
        PhoneIcon.Draw(drawList, chevronCenter, PhoneIcons.ChevronRight, Ink.MutedInk, chevronSize);
        var countLabel = Loc.Plural(L.Photos.Count, count);
        var countSize = Typography.Measure(countLabel, TextStyles.Footnote);
        var countLeft = chevronCenter.X - chevronSize * 0.5f - CollectionChevronGap * scale - countSize.X;
        Typography.Draw(drawList, new Vector2(countLeft, centerY - countSize.Y * 0.5f), countLabel, Ink.MutedInk,
            TextStyles.Footnote);
        var fitted = Typography.FitText(label, MathF.Max(1f, countLeft - Metrics.Space.Md * scale - textLeft),
            TextStyles.BodyEmphasized);
        var labelSize = Typography.Measure(fitted, TextStyles.BodyEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, centerY - labelSize.Y * 0.5f), fitted, Ink.TitleInk,
            TextStyles.BodyEmphasized);
        if (hairline)
        {
            FeedCell.Hairline(drawList, textLeft, right, max.Y, Ink.Hairline);
        }

        return UiInteract.Click(min, max, hovered);
    }

    private void DrawAlbum(Rect area, int key)
    {
        if (IsCustomKey(key))
        {
            DrawCustomAlbumView(area, key);
            return;
        }

        if (key == PhotoView.FavoritesKey)
        {
            DrawFavoritesView(area);
            return;
        }

        if (key == PhotoView.TrashKey)
        {
            DrawTrashView(area);
            return;
        }

        int start;
        int count;
        string title;
        if (key == PhotoView.RecentsKey)
        {
            start = 0;
            count = entries.Length;
            title = Loc.T(L.Photos.Recents);
        }
        else if (TryFindAlbum(key, out var album))
        {
            start = album.Start;
            count = album.Count;
            title = album.Title;
        }
        else
        {
            router.Pop(false);
            return;
        }

        var body = DrawAlbumHeader(area, title, count, 0, count > 0, SelectionScope.Album);
        if (count == 0)
        {
            DrawEmpty(body);
            return;
        }

        DrawPhotoGrid(body, start, count);
        DrawSelectToolbarIfActive(area);
    }

    private Rect DrawAlbumHeader(Rect area, string title, int count, int trailingSlots, bool canSelect,
        SelectionScope scope)
    {
        if (selecting)
        {
            DrawSelectHeader(area);
            var bodyAboveToolbar = BodyBelowHeader(area);
            return new Rect(bodyAboveToolbar.Min, new Vector2(area.Max.X, ToolbarRect(area).Min.Y));
        }

        var slots = trailingSlots + (canSelect ? 1 : 0);
        SocialChrome.DrawScreenHeader(area, title, Ink, back, ScreenTitleStyle, SocialChrome.HeaderReserve(slots),
            Loc.Plural(L.Photos.Count, count));
        if (canSelect && DrawSelectHeaderIcon(area, slots - 1))
        {
            BeginSelect(scope);
        }

        return BodyBelowHeader(area);
    }

    private bool DrawHeaderIconAt(Rect area, int slot, string glyph, string tooltip)
    {
        var scale = UiScale.Current;
        return SocialChrome.DrawHeaderIcon(ImGui.GetWindowDrawList(), SocialChrome.HeaderSlot(area, slot),
            SocialChrome.HeaderIconRadius * scale, glyph, HeaderIconSize, tooltip, Ink, Ink.TitleInk);
    }

    private void DrawCustomAlbumView(Rect area, int key)
    {
        if (!TryFindCustomAlbum(key, out var album))
        {
            router.Pop(false);
            return;
        }

        var body = DrawAlbumHeader(area, album.Name, album.Count, 2, album.Count > 0, SelectionScope.Album);
        if (!selecting)
        {
            if (DrawHeaderIconAt(area, 1, PhoneIcons.Plus, Loc.T(L.Photos.AddPhotos)))
            {
                OpenAlbumPicker(key);
                return;
            }

            if (DrawHeaderIconAt(area, 0, PhoneIcons.Dots, Loc.T(L.Photos.AlbumOptions)))
            {
                OpenAlbumSheet(key);
                return;
            }
        }

        if (!cachedCustomAlbumPaths.TryGetValue(key, out var paths) || paths.Length == 0)
        {
            if (EmptyState.Draw(body, ui, PhoneIcons.Photo, Loc.T(L.Photos.EmptyAlbum),
                    Loc.T(L.Photos.EmptyAlbumHint), Loc.T(L.Photos.AddPhotos)))
            {
                OpenAlbumPicker(key);
            }

            return;
        }

        DrawFlatGrid(body, paths, FlatGridMode.Album, key);
        DrawSelectToolbarIfActive(area);
    }

    private void DrawFavoritesView(Rect area)
    {
        var body = DrawAlbumHeader(area, Loc.T(L.Photos.Favorites), favoritePaths.Length, 0, favoritePaths.Length > 0,
            SelectionScope.Album);
        if (favoritePaths.Length == 0)
        {
            EmptyState.Draw(body, ui, PhoneIcons.Heart, Loc.T(L.Photos.NoFavorites), Loc.T(L.Photos.NoFavoritesHint));
            return;
        }

        DrawFlatGrid(body, favoritePaths, FlatGridMode.Plain, 0);
        DrawSelectToolbarIfActive(area);
    }

    private void DrawTrashView(Rect area)
    {
        var hasItems = trashPaths.Length > 0;
        var body = DrawAlbumHeader(area, Loc.T(L.Photos.RecentlyDeleted), trashPaths.Length, hasItems ? 1 : 0,
            hasItems, SelectionScope.Trash);
        if (!selecting && hasItems && DrawHeaderIconAt(area, 0, PhoneIcons.Dots, Loc.T(L.Photos.AlbumOptions)))
        {
            OpenTrashSheet();
            return;
        }

        if (!hasItems)
        {
            EmptyState.Draw(body, ui, PhoneIcons.Trash, Loc.T(L.Photos.TrashEmpty), Loc.T(L.Photos.TrashEmptyHint));
            return;
        }

        body = DrawTrashHint(body);
        DrawFlatGrid(body, trashPaths, FlatGridMode.Trash, 0);
        DrawSelectToolbarIfActive(area);
    }

    private Rect DrawTrashHint(Rect body)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var maxWidth = MathF.Min(body.Width - CellPadX * 2f * scale, TrashHintMaxWidth * scale);
        var text = Loc.T(L.Photos.RecentlyDeletedHint);
        var height = Typography.MeasureWrappedBlock(text, TextStyles.Footnote, maxWidth).Y;
        var pad = TrashHintPadY * scale;
        Typography.DrawWrappedCentered(drawList, new Vector2(body.Center.X, body.Min.Y + pad + height * 0.5f), text,
            Ink.MutedInk, TextStyles.Footnote, maxWidth);
        return new Rect(new Vector2(body.Min.X, body.Min.Y + pad * 2f + height), body.Max);
    }

    private void DrawAlbumPicker(Rect area, int key)
    {
        if (!TryFindCustomAlbum(key, out var album))
        {
            router.Pop(false);
            return;
        }

        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var doneLabel = Loc.T(L.Photos.Done);
        var pillWidth = Typography.Measure(doneLabel, DonePillStyle).X + DonePillPadX * 2f * scale;
        var pillHeight = DonePillHeight * scale;
        var selected = pickerSelection.Count;
        SocialChrome.DrawScreenHeader(area, Loc.T(L.Photos.AddPhotos), Ink, back, ScreenTitleStyle,
            pillWidth / scale + Metrics.Space.Sm, selected > 0 ? Loc.Plural(L.Photos.Selected, selected) : album.Name);
        var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        var pillRight = area.Max.X - CellPadX * scale;
        var pill = new Rect(new Vector2(pillRight - pillWidth, rowCenterY - pillHeight * 0.5f),
            new Vector2(pillRight, rowCenterY + pillHeight * 0.5f));
        if (SocialPill.Accent(drawList, pill, doneLabel, Ink, DonePillStyle, pillHeight * 0.5f, selected > 0))
        {
            AddPhotosToCustomAlbum(key, pickerSelection.ToArray());
            router.Pop();
            return;
        }

        var body = BodyBelowHeader(area);
        if (entries.Length == 0)
        {
            DrawEmpty(body);
            return;
        }

        EnsurePickerMembership(key);
        DrawFlatGrid(body, null, FlatGridMode.Picker, key);
    }

    private void DrawFlatGrid(Rect body, string[]? paths, FlatGridMode mode, int albumKey)
    {
        var scale = UiScale.Current;
        var childId = mode == FlatGridMode.Picker ? "##albumPicker" : "##albumGrid";
        var gridKey = ImGui.GetID(childId);
        ImGui.SetCursorScreenPos(body.Min);
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, Vector2.Zero))
        using (var child = ImRaii.Child(childId, body.Size, false,
                   DragScrollHost.ScrollFlags(ImGuiWindowFlags.NoBackground)))
        {
            if (!child)
            {
                return;
            }

            AppSurface.ResetScrollOnNewVisit();
            var surface = DragScrollHost.Begin(gridKey);
            if (resetScroll)
            {
                surface.JumpToTop();
                resetScroll = false;
            }

            var origin = ImGui.GetCursorScreenPos();
            var gap = GridGap * scale;
            var avail = ScrollLayout.StableContentWidth();
            var cell = (avail - gap * (Columns - 1)) / Columns;
            var drawList = ImGui.GetWindowDrawList();
            var scrollY = ImGui.GetScrollY();
            var viewHeight = ImGui.GetWindowSize().Y;
            var count = paths?.Length ?? entries.Length;
            var topPad = FlatGridTopPad * scale;
            for (var index = 0; index < count; index++)
            {
                var column = index % Columns;
                var rowIndex = index / Columns;
                var top = topPad + rowIndex * (cell + gap);
                if (top + cell < scrollY - cell || top > scrollY + viewHeight + cell)
                {
                    continue;
                }

                var min = new Vector2(origin.X + column * (cell + gap), origin.Y + top);
                var max = new Vector2(min.X + cell, min.Y + cell);
                if (mode == FlatGridMode.Picker)
                {
                    DrawPickerTile(drawList, min, max, entries[index].Path, scale);
                    continue;
                }

                var path = paths![index];
                var hit = DrawTile(drawList, min, max, path, mode == FlatGridMode.Album, scale);
                if (mode == FlatGridMode.Trash)
                {
                    PhotosChrome.TileCaption(drawList, min, max, DaysLeftLabel(DaysLeft(path)), Ink, scale);
                }

                switch (hit)
                {
                    case TileHit.Menu:
                        OpenPhotoSheet(albumKey, path);
                        break;
                    case TileHit.Open:
                        if (mode == FlatGridMode.Trash)
                        {
                            OpenTrashViewer(index);
                        }
                        else
                        {
                            OpenViewerFromPaths(paths, index);
                        }

                        break;
                }
            }

            var rows = (count + Columns - 1) / Columns;
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(avail, topPad + rows * (cell + gap) + GridBottomPad * scale));
        }
    }

    private void DrawPickerTile(ImDrawListPtr drawList, Vector2 min, Vector2 max, string path, float scale)
    {
        var alreadyInAlbum = pickerMembership.Contains(path);
        var order = 0;
        var isSelected = !alreadyInAlbum && pickerSelectionOrder.TryGetValue(path, out order);
        var hovered = !alreadyInAlbum && UiInteract.Hover(min, max);
        PhotosChrome.Thumbnail(drawList, GetThumbnail(path), min, max, hovered, Ink.ThumbFill);
        var badgeOffset = (PickerBadgeRadius + PickerBadgeInset) * scale;
        var badgeCenter = new Vector2(max.X - badgeOffset, min.Y + badgeOffset);
        if (alreadyInAlbum)
        {
            drawList.AddRectFilled(min, max, ImGui.GetColorU32(Palette.WithAlpha(Black, PickerVeil)));
            PhoneIcon.Draw(drawList, badgeCenter, PhoneIcons.CircleCheckFilled, WhiteMuted, PickerCheckGlyph * scale);
            return;
        }

        if (isSelected)
        {
            drawList.AddRectFilled(min, max, ImGui.GetColorU32(Palette.WithAlpha(Ink.Accent, PickerSelectVeil)));
            var inset = new Vector2(PickerRingInset * scale, PickerRingInset * scale);
            drawList.AddRect(min + inset, max - inset, ImGui.GetColorU32(Ink.Accent), 0f, ImDrawFlags.None,
                PickerRingStroke * scale);
            var badgeRadius = PickerBadgeRadius * scale;
            drawList.AddCircleFilled(badgeCenter, badgeRadius + PickerBadgeRing * scale, ImGui.GetColorU32(Ink.White),
                24);
            drawList.AddCircleFilled(badgeCenter, badgeRadius, ImGui.GetColorU32(Ink.Accent), 24);
            Typography.DrawCentered(drawList, badgeCenter, OrderLabel(order), Ink.White, TextStyles.FootnoteEmphasized);
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

    private void OpenAddToAlbum(string[] targets)
    {
        addTargets = targets;
        router.Push(PhotoView.AddToAlbum());
    }

    private void DrawAddToAlbumPage(Rect area)
    {
        SocialChrome.DrawScreenHeader(area, Loc.T(L.Photos.AddToAlbum), Ink, back, ScreenTitleStyle);
        if (addTargets.Length == 0)
        {
            router.Pop(false);
            return;
        }

        var body = BodyBelowHeader(area);
        using (AppSurface.Begin(body))
        {
            var available = 0;
            for (var index = 0; index < customAlbums.Count; index++)
            {
                if (!AlbumContainsAll(customAlbums[index], addTargets))
                {
                    available++;
                }
            }

            var card = GroupCard.Begin(frameTheme, available + 1);
            if (SettingsRow.Action(card.NextRow(), Loc.T(L.Photos.CreateAlbum), frameTheme.Accent, frameTheme))
            {
                card.End();
                OpenCreateAlbumSheet(addTargets);
                return;
            }

            for (var index = 0; index < customAlbums.Count; index++)
            {
                var album = customAlbums[index];
                if (AlbumContainsAll(album, addTargets))
                {
                    continue;
                }

                if (!SettingsRow.Disclosure(card.NextRow(), album.Name, Loc.Plural(L.Photos.Count, album.Count),
                        frameTheme))
                {
                    continue;
                }

                card.End();
                AddPhotosToCustomAlbum(album.Key, addTargets);
                EndSelect();
                router.Pop();
                return;
            }

            card.End();
            if (available == 0)
            {
                ui.HelpText(Loc.T(L.Photos.AlreadyInAllAlbums));
            }
        }
    }

    private void OpenAlbum(int key) => router.Push(PhotoView.Album(key));

    private void OpenAlbumPicker(int key)
    {
        pickerSelection.Clear();
        pickerSelectionOrder.Clear();
        router.Push(PhotoView.AlbumPicker(key));
    }

    private Rect BodyBelowHeader(Rect area) =>
        new(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * UiScale.Current), area.Max);

    private void OpenCreateAlbumSheet(string[]? photoPaths)
    {
        nameSheetMode = NameSheetMode.Create;
        nameSheetAlbumKey = 0;
        nameSheetPhotoPaths = photoPaths;
        nameDraft = string.Empty;
        nameSheetFocus = true;
        nameSheet.Open();
    }

    private void OpenRenameSheet(int key)
    {
        if (!TryFindCustomAlbum(key, out var album))
        {
            return;
        }

        nameSheetMode = NameSheetMode.Rename;
        nameSheetAlbumKey = key;
        nameSheetPhotoPaths = null;
        nameDraft = album.Name;
        nameSheetFocus = true;
        nameSheet.Open();
    }

    private void DrawNameSheet(Rect area)
    {
        var title = Loc.T(nameSheetMode == NameSheetMode.Rename ? L.Photos.Rename : L.Photos.CreateAlbum);
        nameSheet.Draw(area, frameTheme, title, NameSheetFraction(area), drawNameSheet);
    }

    private static float NameSheetFraction(Rect area)
    {
        var scale = UiScale.Current;
        var content = (SheetFieldHeight + SheetFieldGap + SheetPillHeight + SheetHintGap) * scale +
                      Typography.LineHeight(TextStyles.Footnote);
        var fraction = (content + SheetSurface.ChromeHeight()) / MathF.Max(1f, area.Height);
        return Math.Clamp(fraction, SheetMinimumFraction, SheetMaximumFraction);
    }

    private void DrawNameSheetContent(Rect content)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var field = new Rect(content.Min, new Vector2(content.Max.X, content.Min.Y + SheetFieldHeight * scale));
        if (nameSheetFocus)
        {
            nameSheetFocus = false;
            ImGui.SetKeyboardFocusHere();
        }

        var submitted = SubmitField.Draw(field, "##photos.albumName", Loc.T(L.Photos.AlbumName), ref nameDraft,
            frameTheme, MaxAlbumNameLength, FontAwesomeIcon.Images);
        var trimmed = nameDraft.Trim();
        var excludeKey = nameSheetMode == NameSheetMode.Rename ? nameSheetAlbumKey : 0;
        var duplicate = trimmed.Length > 0 && IsAlbumNameTaken(trimmed, excludeKey);
        var canCommit = trimmed.Length > 0 && !duplicate;
        var pillTop = field.Max.Y + SheetFieldGap * scale;
        var pill = new Rect(new Vector2(content.Min.X, pillTop),
            new Vector2(content.Max.X, pillTop + SheetPillHeight * scale));
        var label = Loc.T(nameSheetMode == NameSheetMode.Rename ? L.Photos.Rename : L.Photos.CreateAlbumButton);
        var pressed = SocialPill.Accent(drawList, pill, label, Ink, TextStyles.Headline, pill.Height * 0.5f, canCommit);
        if ((pressed || submitted) && canCommit)
        {
            CommitNameSheet(trimmed);
            return;
        }

        if (!duplicate)
        {
            return;
        }

        var hintCenterY = pill.Max.Y + SheetHintGap * scale + Typography.LineHeight(TextStyles.Footnote) * 0.5f;
        Typography.DrawCentered(drawList, new Vector2(content.Center.X, hintCenterY),
            Typography.FitText(Loc.T(L.Photos.AlbumExists), content.Width, TextStyles.Footnote), frameTheme.Danger,
            TextStyles.Footnote);
    }

    private void CommitNameSheet(string name)
    {
        nameSheet.Close();
        if (nameSheetMode == NameSheetMode.Rename)
        {
            RenameCustomAlbumInternal(nameSheetAlbumKey, name);
            return;
        }

        var key = CreateCustomAlbumInternal(name);
        if (key == 0)
        {
            return;
        }

        if (nameSheetPhotoPaths is { } paths)
        {
            nameSheetPhotoPaths = null;
            AddPhotosToCustomAlbum(key, paths);
            EndSelect();
            if (router.Current.Route == PhotoRoute.AddToAlbum)
            {
                router.Pop();
            }

            return;
        }

        OpenAlbum(key);
    }

    private bool IsAlbumNameTaken(string name, int excludeKey)
    {
        for (var index = 0; index < customAlbums.Count; index++)
        {
            var album = customAlbums[index];
            if (album.Key != excludeKey && string.Equals(album.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void OpenAlbumSheet(int key)
    {
        albumSheetKey = key;
        albumSheetItems[0] = new ActionSheet.Item(Loc.T(L.Photos.AddPhotos), IconGlyph.Of(FontAwesomeIcon.Plus));
        albumSheetItems[1] = new ActionSheet.Item(Loc.T(L.Photos.Rename), IconGlyph.Of(FontAwesomeIcon.Pen));
        albumSheetItems[2] = new ActionSheet.Item(Loc.T(L.Photos.DeleteAlbum), IconGlyph.Of(FontAwesomeIcon.Trash),
            Danger: true);
        albumSheet.Open();
    }

    private void DrawAlbumSheet(Rect screen)
    {
        if (!albumSheet.CapturesPointer)
        {
            return;
        }

        var title = TryFindCustomAlbum(albumSheetKey, out var album) ? album.Name : string.Empty;
        var picked = albumSheet.Draw(screen, ActionSheetStyle.From(ui), albumSheetItems, Loc.T(L.Common.Cancel), false,
            title);
        switch (picked)
        {
            case 0:
                OpenAlbumPicker(albumSheetKey);
                break;
            case 1:
                OpenRenameSheet(albumSheetKey);
                break;
            case 2:
                AskDeleteAlbum(albumSheetKey);
                break;
        }
    }

    private void OpenPhotoSheet(int albumKey, string path)
    {
        photoSheetAlbumKey = albumKey;
        photoSheetPath = path;
        photoSheetItems[0] = new ActionSheet.Item(Loc.T(L.Photos.RemoveFromAlbum), IconGlyph.Of(FontAwesomeIcon.Trash),
            Danger: true);
        photoSheet.Open();
    }

    private void DrawPhotoSheet(Rect screen)
    {
        if (!photoSheet.CapturesPointer)
        {
            return;
        }

        var picked = photoSheet.Draw(screen, ActionSheetStyle.From(ui), photoSheetItems, Loc.T(L.Common.Cancel), false);
        if (picked != 0)
        {
            return;
        }

        AskRemoveFromAlbum(photoSheetAlbumKey, photoSheetPath);
    }

    private void OpenTrashSheet()
    {
        trashSheetItems[0] = new ActionSheet.Item(Loc.T(L.Photos.RecoverAll), IconGlyph.Of(FontAwesomeIcon.Undo));
        trashSheetItems[1] = new ActionSheet.Item(Loc.T(L.Photos.DeleteAll), IconGlyph.Of(FontAwesomeIcon.Trash),
            Danger: true);
        trashSheet.Open();
    }

    private void DrawTrashSheet(Rect screen)
    {
        if (!trashSheet.CapturesPointer)
        {
            return;
        }

        var picked = trashSheet.Draw(screen, ActionSheetStyle.From(ui), trashSheetItems, Loc.T(L.Common.Cancel), false,
            Loc.T(L.Photos.RecentlyDeleted));
        switch (picked)
        {
            case 0:
                RecoverPhotos(trashPaths);
                break;
            case 1:
                AskDeleteForever(trashPaths);
                break;
        }
    }

    private void AskRemoveFromAlbum(int albumKey, string path)
    {
        if (!TryFindCustomAlbum(albumKey, out var album))
        {
            return;
        }

        confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Photos.RemoveFromAlbumConfirm, album.Name),
            ConfirmLabel = Loc.T(L.Photos.RemoveFromAlbum),
            CancelLabel = Loc.T(L.Common.Cancel),
            Sheet = true,
            Confirm = () => RemovePhotoFromCustomAlbum(albumKey, path),
        });
    }

    private void AskMonthlyAlbums()
    {
        configuration.PhotosMonthlyAlbumsAsked = true;
        configuration.Save();
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Photos.MonthlyAlbums),
            Message = Loc.T(L.Photos.MonthlyAlbumsPrompt),
            ConfirmLabel = Loc.T(L.Photos.MonthlyAlbumsOn),
            CancelLabel = Loc.T(L.Photos.MonthlyAlbumsNotNow),
            Danger = false,
            Sheet = true,
            Confirm = () => SetMonthlyAlbums(true),
        });
    }

    private void AskDeleteAlbum(int key)
    {
        if (!TryFindCustomAlbum(key, out var album))
        {
            return;
        }

        confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Photos.DeleteAlbumConfirm, album.Name) + "\n" + Loc.T(L.Photos.DeleteAlbumBody),
            ConfirmLabel = Loc.T(L.Photos.DeleteAlbum),
            CancelLabel = Loc.T(L.Common.Cancel),
            Sheet = true,
            Confirm = () => DeleteCustomAlbumInternal(key),
        });
    }

    private bool AlbumContains(CustomAlbum album, string path) =>
        customAlbumPhotos.TryGetValue(album.Name, out var photos) && ContainsOrdinalIgnoreCase(photos, path);

    private bool AlbumContainsAll(CustomAlbum album, string[] paths)
    {
        if (!customAlbumPhotos.TryGetValue(album.Name, out var photos))
        {
            return false;
        }

        for (var index = 0; index < paths.Length; index++)
        {
            if (!ContainsOrdinalIgnoreCase(photos, paths[index]))
            {
                return false;
            }
        }

        return paths.Length > 0;
    }

    private bool TryFindAlbum(int key, out MonthAlbum album)
    {
        for (var index = 0; index < albums.Count; index++)
        {
            if (albums[index].Key == key)
            {
                album = albums[index];
                return true;
            }
        }

        album = default;
        return false;
    }

    private static string Capitalize(string text) =>
        text.Length == 0 ? text : char.ToUpper(text[0], Loc.Culture) + text.Substring(1);

    private static bool ContainsOrdinalIgnoreCase(List<string> values, string value)
    {
        for (var index = 0; index < values.Count; index++)
        {
            if (string.Equals(values[index], value, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
