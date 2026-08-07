using System.Diagnostics;
using Aetherphone.Core;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Photos;

internal sealed partial class PhotosApp
{
    private void DrawRoot(Rect area)
    {
        DrawNavBar(area, DisplayName, null);
        var scale = UiScale.Current;
        var pad = 14f * scale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var segBar = new Rect(new Vector2(area.Min.X + pad, top + 4f * scale),
            new Vector2(area.Max.X - pad, top + 4f * scale + SegmentHeight * scale));
        segmentLabels[0] = Loc.T(L.Photos.Library);
        segmentLabels[1] = Loc.T(L.Photos.Albums);
        var picked = SegmentStrip.Draw("photos.segment", segBar, segmentLabels, segment, ui.Palette);
        if (picked != segment)
        {
            segment = picked;
            configuration.PhotosSegment = picked;
            configuration.Save();
            resetScroll = true;
        }

        var body = new Rect(new Vector2(area.Min.X, segBar.Max.Y + 6f * scale), area.Max);
        if (segment == 0)
        {
            UiAnchors.Report("photos.grid", body);
            if (entries.Length == 0)
            {
                DrawEmpty(body);
                return;
            }

            DrawPhotoGrid(body, 0, entries.Length);
            DrawOpenFolder(body);
            return;
        }

        if (customAlbums.Count == 0 && entries.Length == 0)
        {
            DrawEmptyAlbums(body);
            DrawNewAlbumFab(body);
            return;
        }

        DrawAlbumsGrid(body);
        DrawNewAlbumFab(body);
    }

    private void DrawAlbum(Rect area, int key)
    {
        var scale = UiScale.Current;
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
            title = Capitalize(album.Month.ToString("MMMM yyyy", Loc.Culture));
        }
        else if (key < 0)
        {
            DrawCustomAlbumView(area, key);
            return;
        }
        else
        {
            router.Pop(false);
            return;
        }

        DrawNavBar(area, title, back);
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        if (count == 0)
        {
            DrawEmpty(body);
            return;
        }

        DrawPhotoGrid(body, start, count);
    }
    
    private void DrawCustomAlbumView(Rect area, int key)
    {
        var scale = UiScale.Current;
        if (!TryFindCustomAlbum(key, out var album))
        {
            router.Pop(false);
            return;
        }

        var addPhotosLabel = Loc.T(L.Photos.AddPhotos);
        var addPhotosWidth = AppSkin.HeaderActionWidth(addPhotosLabel);
        DrawNavBar(area, album.Name, back, addPhotosWidth + 12f * scale + 12f * scale);
        if (ui.HeaderAction(area, addPhotosLabel, entries.Length > 0))
        {
            OpenAlbumPicker(key);
        }

        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        if (!cachedCustomAlbumPaths.TryGetValue(key, out var paths) || paths.Length == 0)
        {
            Typography.DrawCentered(body.Center, Loc.T(L.Photos.EmptyAlbum), ui.MutedInk, TextStyles.Body);
            return;
        }

        DrawCustomAlbumGrid(body, paths, key);
    }

    private void CloseModifyAlbumPage()
    {
        newAlbumDraft = string.Empty;
        renameAlbumDraft = string.Empty;
        router.Pop();
    }

    private void DrawCreateAlbumPage(Rect area)
    {
        var scale = UiScale.Current;
        DrawNavBar(area, Loc.T(L.Photos.CreateAlbum), CloseModifyAlbumPage);
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        DrawCreateAlbumSheet(body, CloseModifyAlbumPage);
    }
    
    private void DrawRenameAlbumPage(Rect area, int albumKey)
    {
        var scale = UiScale.Current;
        DrawNavBar(area, Loc.T(L.Photos.Rename), CloseModifyAlbumPage);
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        DrawRenameAlbumSheet(body, albumKey, CloseModifyAlbumPage);
    }

    private void DrawAddToAlbumPage(Rect area)
    {
        var scale = UiScale.Current;
        DrawNavBar(area, Loc.T(L.Photos.AddToAlbum), () => router.Pop());
        if (viewerPaths.Length == 0 || viewerIndex < 0 || viewerIndex >= viewerPaths.Length)
        {
            router.Pop();
            return;
        }

        var path = viewerPaths[viewerIndex];
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        using (AppSurface.Begin(body))
        {
            var available = 0;
            for (var index = 0; index < customAlbums.Count; index++)
            {
                if (!AlbumContains(customAlbums[index], path))
                {
                    available++;
                }
            }

            if (available == 0)
            {
                EmptyState.Draw(body, ui, FontAwesomeIcon.Images, Loc.T(L.Photos.AlreadyInAllAlbums),
                    Loc.T(L.Photos.CreateAlbumHint));
                return;
            }

            var card = GroupCard.Begin(frameTheme, available);
            for (var index = 0; index < customAlbums.Count; index++)
            {
                var album = customAlbums[index];
                if (AlbumContains(album, path))
                {
                    continue;
                }

                if (SettingsRow.Disclosure(card.NextRow(), album.Name, Loc.Plural(L.Photos.Count, album.Count),
                        frameTheme))
                {
                    AddPhotosToCustomAlbum(album.Key, new[] { path });
                    router.Pop();
                    return;
                }
            }

            card.End();
        }
    }

    private bool AlbumContains(CustomAlbum album, string path) =>
        customAlbumPhotos.TryGetValue(album.Name, out var photos) && ContainsOrdinalIgnoreCase(photos, path);

    private void DrawModifyAlbumSheet(
        Rect body,
        string label,
        string buttonLabel,
        ref string draft,
        Func<string, bool> canCommit,
        Action commit,
        Action cancel,
        bool showDuplicateHint = false)
    {
        var scale = UiScale.Current;
        ImGui.SetCursorScreenPos(body.Min);
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(16f * scale, 8f * scale)))
        using (ImRaii.Child("##modifyAlbumSheet", body.Size, false, ImGuiWindowFlags.NoBackground))
        {
            using (ImRaii.PushColor(ImGuiCol.Text, ui.MutedInk))
                Typography.Plain(label);

            var origin = ImGui.GetCursorScreenPos();
            var width = ImGui.GetContentRegionAvail().X;
            var height = 34f * scale;
            var drawList = ImGui.GetWindowDrawList();
            Squircle.Fill(drawList, origin, new Vector2(origin.X + width, origin.Y + height), 9f * scale,
                ImGui.GetColorU32(ui.FieldSurface));
            ImGui.SetCursorScreenPos(new Vector2(origin.X + 12f * scale,
                origin.Y + height * 0.5f - ImGui.GetFrameHeight() * 0.5f));
            ImGui.SetNextItemWidth(width - 24f * scale);
            if (focusAlbumName)
            {
                focusAlbumName = false;
                ImGui.SetKeyboardFocusHere();
            }

            bool submitted;
            using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f))
                       .Push(ImGuiCol.Text, ui.TitleInk))
            {
                submitted = ImGui.InputText("##modifyAlbumName", ref draft, 64,
                    ImGuiInputTextFlags.EnterReturnsTrue);
            }

            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, height));

            ImGui.Dummy(new Vector2(0f, 18f * scale));
            var trimmed = draft.Trim();
            var canProceed = canCommit(trimmed);

            var accent = canProceed ? ui.Accent : Palette.WithAlpha(ui.Accent, 0.4f);
            using (ImRaii.PushColor(ImGuiCol.Button, accent)
                       .Push(ImGuiCol.ButtonHovered,
                           canProceed ? Palette.Mix(ui.Accent, new Vector4(1f, 1f, 1f, 1f), 0.14f) : accent)
                       .Push(ImGuiCol.ButtonActive, accent)
                       .Push(ImGuiCol.Text, new Vector4(1f, 1f, 1f, canProceed ? 1f : 0.72f)))
            {
                if ((ImGui.Button(buttonLabel, new Vector2(-1f, 38f * scale)) || submitted) && canProceed)
                {
                    commit();
                    cancel();
                }
            }

            if (showDuplicateHint && !canProceed && trimmed.Length > 0)
            {
                ImGui.Dummy(new Vector2(0f, 10f * scale));
                using (ImRaii.PushColor(ImGuiCol.Text, ui.Palette.Accent))
                    Typography.Wrapped(Loc.T(L.Photos.AlbumExists));
            }
        }
    }
    
    private void DrawCreateAlbumSheet(Rect body, Action cancel)
    {
        DrawModifyAlbumSheet(
            body,
            Loc.T(L.Photos.AlbumName),
            Loc.T(L.Photos.CreateAlbumButton),
            ref newAlbumDraft,
            CanModifyAlbum,
            CommitCreateAlbum,
            cancel,
            showDuplicateHint: true
        );
    }

    private bool CanModifyAlbum(string name)
    {
        return name.Length > 0
               && !ContainsOrdinalIgnoreCase(customAlbumOrder, name);
    }

    private void CommitCreateAlbum()
    {
        CreateCustomAlbumInternal(newAlbumDraft);
        newAlbumDraft = string.Empty;
    }

    private void DrawRenameAlbumSheet(Rect body, int albumKey, Action cancel)
    {
        DrawModifyAlbumSheet(
            body,
            Loc.T(L.Photos.AlbumName),
            Loc.T(L.Photos.Rename),
            ref renameAlbumDraft,
            CanModifyAlbum,
            () => CommitRenameAlbum(albumKey),
            cancel
        );
    }

    private void CommitRenameAlbum(int albumKey)
    {
        RenameCustomAlbumInternal(albumKey, renameAlbumDraft);
        renameAlbumDraft = string.Empty;
    }

    private void DrawEmpty(Rect body)
    {
        if (EmptyState.Draw(body, ui, FontAwesomeIcon.Image, Loc.T(L.Photos.NoPhotos),
                Loc.T(L.Photos.UseCameraHint), Loc.T(L.Apps.Camera)))
        {
            frameNavigation.Open("camera");
        }
    }

    private void DrawEmptyAlbums(Rect body) =>
        EmptyState.Draw(body, ui, FontAwesomeIcon.Images, Loc.T(L.Photos.NoAlbums), Loc.T(L.Photos.CreateAlbumHint));

    private void DrawPhotoGrid(Rect body, int start, int count)
    {
        var scale = UiScale.Current;
        var gridKey = ImGui.GetID("##photoGrid");
        ImGui.SetCursorScreenPos(body.Min);
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, Vector2.Zero))
        using (var child = ImRaii.Child("##photoGrid", body.Size, false,
                   DragScrollHost.ScrollFlags(ImGuiWindowFlags.NoBackground)))
        {
            if (!child)
            {
                return;
            }

            var surface = DragScrollHost.Begin(gridKey);
            if (resetScroll)
            {
                surface.JumpToTop();
                resetScroll = false;
            }

            var origin = ImGui.GetCursorScreenPos();
            var side = 2f * scale;
            var gap = 3f * scale;
            var avail = ScrollLayout.StableContentWidth();
            var cell = (avail - side * 2f - gap * (Columns - 1)) / Columns;
            var total = LayoutBands(start, count, cell, gap, scale);
            var drawList = ImGui.GetWindowDrawList();
            var scrollY = ImGui.GetScrollY();
            var viewHeight = ImGui.GetWindowSize().Y;
            var margin = cell + 60f * scale;
            for (var index = 0; index < bands.Count; index++)
            {
                var band = bands[index];
                if (band.Top + band.Height < scrollY - margin || band.Top > scrollY + viewHeight + margin)
                {
                    continue;
                }

                var screenTop = origin.Y + band.Top;
                if (band.Header)
                {
                    DrawSectionHeader(drawList, new Vector2(origin.X + side + 4f * scale, screenTop),
                        avail - side * 2f - 8f * scale, band, scale);
                    continue;
                }

                DrawPhotoRow(drawList, band, origin.X + side, screenTop, cell, gap, start, count);
            }

            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(avail, total));
        }
    }

    private float LayoutBands(int start, int count, float cell, float gap, float scale)
    {
        bands.Clear();
        var headerHeight = 40f * scale;
        var rowStride = cell + gap;
        var blockGap = 10f * scale;
        var y = 6f * scale;
        var index = start;
        var end = start + count;
        while (index < end)
        {
            var day = entries[index].Taken.Date;
            var dayStart = index;
            while (index < end && entries[index].Taken.Date == day)
            {
                index++;
            }

            var dayCount = index - dayStart;
            bands.Add(new GridBand
            {
                Header = true,
                Day = entries[dayStart].Taken,
                DayCount = dayCount,
                Top = y,
                Height = headerHeight,
            });
            y += headerHeight;
            var rows = (dayCount + Columns - 1) / Columns;
            for (var row = 0; row < rows; row++)
            {
                var rowStart = dayStart + row * Columns;
                var rowCount = Math.Min(Columns, dayStart + dayCount - rowStart);
                bands.Add(new GridBand
                {
                    Header = false,
                    PhotoStart = rowStart,
                    PhotoCount = rowCount,
                    Top = y,
                    Height = cell,
                });
                y += rowStride;
            }

            y += blockGap;
        }

        return y + 6f * scale;
    }

    private void DrawSectionHeader(ImDrawListPtr drawList, Vector2 topLeft, float width, GridBand band, float scale)
    {
        var label = DayLabel(band.Day);
        var count = Loc.Plural(L.Photos.Count, band.DayCount);
        var centerY = topLeft.Y + 40f * scale * 0.5f + 3f * scale;
        var countSize = Typography.Measure(count, TextStyles.Footnote);
        var nameMax = MathF.Max(24f * scale, width - countSize.X - 12f * scale);
        var name = Typography.FitText(label, nameMax, TextStyles.Headline);
        var nameSize = Typography.Measure(name, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(topLeft.X, centerY - nameSize.Y * 0.5f), name, ui.TitleInk,
            TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(topLeft.X + width - countSize.X, centerY - countSize.Y * 0.5f), count,
            ui.MutedInk, TextStyles.Footnote);
    }

    private void DrawPhotoRow(ImDrawListPtr drawList, GridBand band, float leftX, float top, float cell, float gap,
        int sliceStart, int sliceCount)
    {
        for (var column = 0; column < band.PhotoCount; column++)
        {
            var absolute = band.PhotoStart + column;
            var min = new Vector2(leftX + column * (cell + gap), top);
            var max = new Vector2(min.X + cell, min.Y + cell);
            var hovered = UiInteract.Hover(min, max);
            PhotosChrome.Thumbnail(drawList, GetThumbnail(entries[absolute].Path), min, max, hovered, ui.FieldSurface);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(min, max, hovered))
            {
                OpenViewer(sliceStart, sliceCount, absolute);
            }
        }
    }

    private void DrawAlbumsGrid(Rect body)
    {
        var scale = UiScale.Current;
        var albumsKey = ImGui.GetID("##photoAlbums");
        ImGui.SetCursorScreenPos(body.Min);
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(14f * scale, 6f * scale)))
        using (var child = ImRaii.Child("##photoAlbums", body.Size, false,
                   DragScrollHost.ScrollFlags(ImGuiWindowFlags.NoBackground)))
        {
            if (!child)
            {
                return;
            }

            var surface = DragScrollHost.Begin(albumsKey);
            if (resetScroll)
            {
                surface.JumpToTop();
                resetScroll = false;
            }

            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var gap = 12f * scale;
            const int columns = 2;
            var tileWidth = (width - gap) / columns;
            var coverHeight = tileWidth;
            var cardHeight = coverHeight + 42f * scale;
            var drawList = ImGui.GetWindowDrawList();
            var total = 1 + customAlbums.Count + albums.Count;
            for (var index = 0; index < total; index++)
            {
                var column = index % columns;
                var rowIndex = index / columns;
                var min = new Vector2(origin.X + column * (tileWidth + gap), origin.Y + rowIndex * (cardHeight + gap));
                var rect = new Rect(min, new Vector2(min.X + tileWidth, min.Y + cardHeight));
                if (index == 0)
                {
                    if (DrawAlbumCard(drawList, rect, Loc.T(L.Photos.Recents), 0, entries.Length, coverHeight, scale))
                    {
                        OpenAlbum(PhotoView.RecentsKey);
                    }

                    continue;
                }

                var customIndex = index - 1;
                if (customIndex < customAlbums.Count)
                {
                    DrawCustomAlbumCard(drawList, rect, customAlbums[customIndex], coverHeight, scale, body);
                    continue;
                }

                var monthIndex = index - 1 - customAlbums.Count;
                var album = albums[monthIndex];
                var title = Capitalize(album.Month.ToString("MMMM yyyy", Loc.Culture));
                if (DrawAlbumCard(drawList, rect, title, album.Start, album.Count, coverHeight, scale))
                {
                    OpenAlbum(album.Key);
                }
            }

            var rows = (total + columns - 1) / columns;
            var heightTotal = rows * cardHeight + (rows - 1) * gap;
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, heightTotal + 12f * scale));
        }
    }

    private void DrawOpenFolder(Rect rect)
    {
        if (ComposeFab.Draw(rect, "##openFolderFab", Accent, FontAwesomeIcon.Folder.ToIconString(),
                             Loc.T(L.Photos.OpenFolder), "photos.openFolder"))
        {
            UrlActions.OpenFolder(library.DirectoryPath);
        }
    }

    private bool DrawAlbumCard(ImDrawListPtr drawList, Rect rect, string title, int coverStart, int coverCount,
        float coverHeight, float scale)
    {
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var coverMax = new Vector2(rect.Max.X, rect.Min.Y + coverHeight);
        var rounding = 16f * scale;
        var shadow = new Vector2(0f, 3f * scale);
        drawList.AddRectFilled(rect.Min + shadow, coverMax + shadow,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.30f)), rounding, ImDrawFlags.RoundCornersAll);
        var cover = coverCount > 0 ? GetThumbnail(entries[coverStart].Path) : null;
        if (cover is not null)
        {
            var (uv0, uv1) = ImageFit.CoverSquare(cover.Size);
            drawList.AddImageRounded(cover.Handle, rect.Min, coverMax, uv0, uv1, 0xFFFFFFFFu, rounding,
                ImDrawFlags.RoundCornersAll);
        }
        else
        {
            drawList.AddRectFilled(rect.Min, coverMax, ImGui.GetColorU32(ui.FieldSurface), rounding,
                ImDrawFlags.RoundCornersAll);
            AppSkin.Icon(drawList, new Vector2(rect.Center.X, rect.Min.Y + coverHeight * 0.5f),
                         FontAwesomeIcon.Images.ToIconString(), ui.MutedInk, 1.2f);
        }

        Material.Edge(drawList, rect.Min, coverMax, rounding, scale, hovered ? 1f : 0.7f);
        if (hovered)
        {
            drawList.AddRectFilled(rect.Min, coverMax, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.06f)), rounding,
                ImDrawFlags.RoundCornersAll);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var textTop = coverMax.Y + 7f * scale;
        Marquee.DrawLeft("photos.albumCard." + title, title, rect.Min.X + 2f * scale, textTop,
            rect.Width - 4f * scale, TextStyles.SubheadlineEmphasized, ui.TitleInk, hovered);
        var countLabel = Loc.Plural(L.Photos.Count, coverCount);
        Typography.Draw(drawList, new Vector2(rect.Min.X + 2f * scale, textTop + 19f * scale), countLabel, ui.MutedInk,
            TextStyles.Footnote);
        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    private void OpenAlbum(int key) => router.Push(PhotoView.Album(key));

    private void OpenAlbumPicker(int key)
    {
        pickerSelection.Clear();
        pickerSelectionOrder.Clear();
        router.Push(PhotoView.AlbumPicker(key));
    }
    
    private void DrawNewAlbumFab(Rect rect)
    {
        if (ComposeFab.Draw(rect, "##newAlbumFab", Accent, FontAwesomeIcon.Plus.ToIconString(),
                             Loc.T(L.Photos.CreateAlbum), "photos.newAlbum"))
        {
            newAlbumDraft = string.Empty;
            focusAlbumName = true;
            router.Push(PhotoView.CreateAlbum());
        }
    }

    private void DrawCustomAlbumCard(ImDrawListPtr drawList, Rect rect, CustomAlbum album, float coverHeight,
        float scale, Rect screen)
    {
        var coverCoverMax = new Vector2(rect.Max.X, rect.Min.Y + coverHeight);
        var rounding = 16f * scale;
        var shadow = new Vector2(0f, 3f * scale);
        drawList.AddRectFilled(rect.Min + shadow, coverCoverMax + shadow,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.30f)), rounding, ImDrawFlags.RoundCornersAll);

        string? coverPath = null;
        if (customAlbumPhotos.TryGetValue(album.Name, out var photos) && photos.Count > 0)
        {
            coverPath = photos[0];
        }

        if (coverPath is not null && GetThumbnail(coverPath) is { } cover)
        {
            var (uv0, uv1) = ImageFit.CoverSquare(cover.Size);
            drawList.AddImageRounded(cover.Handle, rect.Min, coverCoverMax, uv0, uv1, 0xFFFFFFFFu, rounding,
                ImDrawFlags.RoundCornersAll);
        }
        else
        {
            drawList.AddRectFilled(rect.Min, coverCoverMax, ImGui.GetColorU32(ui.FieldSurface), rounding,
                ImDrawFlags.RoundCornersAll);
            AppSkin.Icon(drawList, new Vector2(rect.Center.X, rect.Min.Y + coverHeight * 0.5f),
                FontAwesomeIcon.Images.ToIconString(), ui.MutedInk, 1.2f);
        }

        Material.Edge(drawList, rect.Min, coverCoverMax, rounding, scale, 0.7f);
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        if (hovered)
        {
            drawList.AddRectFilled(rect.Min, coverCoverMax, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.06f)), rounding,
                ImDrawFlags.RoundCornersAll);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var textTop = coverCoverMax.Y + 7f * scale;
        Marquee.DrawLeft("photos.customAlbumCard." + album.Key, album.Name, rect.Min.X + 2f * scale, textTop,
            rect.Width - 4f * scale, TextStyles.SubheadlineEmphasized, ui.TitleInk, hovered);
        var countLabel = Loc.Plural(L.Photos.Count, album.Count);
        Typography.Draw(drawList, new Vector2(rect.Min.X + 2f * scale, textTop + 19f * scale), countLabel, ui.MutedInk,
            TextStyles.Footnote);

        var badgeRadius = 12f * scale;
        var badgeCenter = new Vector2(coverCoverMax.X - badgeRadius - 5f * scale,
            rect.Min.Y + badgeRadius + 5f * scale);
        var overBadge = false;
        if (hovered || albumMenu.IsOpenFor("custom:" + album.Key))
        {
            overBadge = UiInteract.Hover(badgeCenter - new Vector2(badgeRadius, badgeRadius),
                badgeCenter + new Vector2(badgeRadius, badgeRadius));
            if (ui.IconButton(badgeCenter, badgeRadius, FontAwesomeIcon.EllipsisH.ToIconString(), ui.TitleInk,
                    Palette.WithAlpha(new Vector4(0f, 0f, 0f, 1f), 0.45f), 0.8f))
            {
                var badgeRect = new Rect(badgeCenter - new Vector2(badgeRadius, badgeRadius),
                    badgeCenter + new Vector2(badgeRadius, badgeRadius));
                albumMenu.Toggle("custom:" + album.Key, badgeRect);
            }
        }

        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            var pos = ImGui.GetMousePos();
            albumMenu.Toggle("custom:" + album.Key, new Rect(pos, pos + new Vector2(1f, 1f)));
        }

        var menuId = "custom:" + album.Key;
        if (albumMenu.IsOpenFor(menuId))
        {
            albumMenu.Gate();
            DrawCustomAlbumContextMenu(album.Key, screen);
        }

        if (UiInteract.Click(rect.Min, rect.Max, hovered && !overBadge))
        {
            OpenAlbum(album.Key);
        }
    }
    
    private ReadOnlySpan<DropdownMenu.Item> GetCustomAlbumMenuItems()
    {
        var currentLocale = Loc.Current.Code;
        if (customAlbumMenuItemsCache is null || customAlbumMenuItemsLocale != currentLocale)
        {
            customAlbumMenuItemsCache =
            [
                new(Loc.T(L.Photos.Rename), FontAwesomeIcon.Pen.ToIconString()),
                new(Loc.T(L.Photos.DeleteAlbum), FontAwesomeIcon.Trash.ToIconString(), Danger: true),
            ];
            customAlbumMenuItemsLocale = currentLocale;
        }

        return customAlbumMenuItemsCache;
    }
    
    private ReadOnlySpan<DropdownMenu.Item> GetPhotoMenuItems()
    {
        var currentLocale = Loc.Current.Code;
        if (photoMenuItemsCache is null || photoMenuItemsLocale != currentLocale)
        {
            photoMenuItemsCache =
            [
                new(Loc.T(L.Photos.RemoveFromAlbum), FontAwesomeIcon.Trash.ToIconString(), Danger: true),
            ];
            photoMenuItemsLocale = currentLocale;
        }

        return photoMenuItemsCache;
    }
    
    private void DrawCustomAlbumContextMenu(int key, Rect screen)
    {
        var items = GetCustomAlbumMenuItems();
        var picked = albumMenu.Draw(screen, frameTheme, items, out var action);
        if (picked < 0)
        {
            return;
        }

        if (action == DropdownMenu.RowAction.Delete || (action == DropdownMenu.RowAction.Select && picked == 1))
        {
            if (!TryFindCustomAlbum(key, out var found))
            {
                return;
            }
            confirm.Ask(new ConfirmRequest
            {
                Message = Loc.T(L.Photos.DeleteAlbumConfirm, found.Name) + "\n" + Loc.T(L.Photos.DeleteAlbumBody),
                ConfirmLabel = Loc.T(L.Photos.DeleteAlbum),
                CancelLabel = Loc.T(L.Common.Cancel),
                Confirm = () => DeleteCustomAlbumInternal(key),
            });
        }
        else if (picked == 0)
        {
            renameAlbumDraft = string.Empty;
            var found = customAlbums.FirstOrDefault(album => album.Key == key);
            if (found.Name is not null)
            {
                renameAlbumDraft = found.Name;
            }
            focusAlbumName = true;
            router.Push(PhotoView.RenameAlbum(key));
        }
    }
    
    private void DrawAlbumPicker(Rect area, int key)
    {
        var scale = UiScale.Current;
        if (!TryFindCustomAlbum(key, out var album))
        {
            router.Pop(false);
            return;
        }

        DrawNavBar(area, album.Name, () =>
        {
            router.Pop();
        });

        if (ui.HeaderAction(area, Loc.T(L.Photos.Done), pickerSelection.Count > 0) && pickerSelection.Count > 0)
        {
            AddPhotosToCustomAlbum(key, pickerSelection.ToArray());
            router.Pop();
        }

        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        if (entries.Length == 0)
        {
            DrawEmpty(body);
            return;
        }

        DrawAlbumPickerGrid(body, key);
    }
    
    private void DrawAlbumPickerGrid(Rect body, int albumKey)
    {
        var scale = UiScale.Current;
        var gridKey = ImGui.GetID("##albumPicker");
        ImGui.SetCursorScreenPos(body.Min);
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, Vector2.Zero))
        using (var child = ImRaii.Child("##albumPicker", body.Size, false,
                   DragScrollHost.ScrollFlags(ImGuiWindowFlags.NoBackground)))
        {
            if (!child)
            {
                return;
            }

            var surface = DragScrollHost.Begin(gridKey);
            if (resetScroll)
            {
                surface.JumpToTop();
                resetScroll = false;
            }

            var origin = ImGui.GetCursorScreenPos();
            var side = 2f * scale;
            var gap = 3f * scale;
            var avail = ScrollLayout.StableContentWidth();
            var cell = (avail - side * 2f - gap * (Columns - 1)) / Columns;
            var drawList = ImGui.GetWindowDrawList();

            EnsurePickerMembership(albumKey);

            var scrollY = ImGui.GetScrollY();
            var viewHeight = ImGui.GetWindowSize().Y;
            var margin = cell + 60f * scale;

            for (var index = 0; index < entries.Length; index++)
            {
                var column = index % Columns;
                var rowIndex = index / Columns;
                var top = 6f * scale + rowIndex * (cell + gap);

                if (top + cell < scrollY - margin || top > scrollY + viewHeight + margin)
                {
                    continue;
                }

                var min = new Vector2(origin.X + side + column * (cell + gap), origin.Y + top);
                var max = new Vector2(min.X + cell, min.Y + cell);
                var path = entries[index].Path;
                var alreadyInAlbum = pickerMembership.Contains(path);
                var isSelected = pickerSelection.Contains(path);
                var canSelect = !alreadyInAlbum;
                var effectiveHovered = canSelect && UiInteract.Hover(min, max);

                PhotosChrome.Thumbnail(drawList, GetThumbnail(path), min, max, effectiveHovered, ui.FieldSurface);

                if (alreadyInAlbum)
                {
                    drawList.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.35f)), 7f * scale);
                    var checkCenter = new Vector2(max.X - 14f * scale, min.Y + 14f * scale);
                    AppSkin.Icon(drawList, checkCenter, FontAwesomeIcon.Check.ToIconString(),
                        new Vector4(0.4f, 0.8f, 0.4f, 0.8f), 0.8f);
                }
                else if (isSelected)
                {
                    drawList.AddRectFilled(min, max, ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.35f)), 7f * scale);
                    var radius = 11f * scale;
                    var badgeCenter = new Vector2(max.X - radius - 6f * scale, min.Y + radius + 6f * scale);
                    drawList.AddCircleFilled(badgeCenter, radius, ImGui.GetColorU32(ui.Accent), 20);
                    var order = pickerSelectionOrder[path];
                    Typography.DrawCentered(drawList, badgeCenter, order.ToString(Loc.Culture),
                        new Vector4(1f, 1f, 1f, 1f), TextStyles.FootnoteEmphasized);
                }

                if (effectiveHovered)
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                    if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                    {
                        if (isSelected)
                        {
                            RemoveFromPickerSelection(path);
                        }
                        else
                        {
                            AddToPickerSelection(path);
                        }
                    }
                }
            }

            var rows = (entries.Length + Columns - 1) / Columns;
            var totalHeight = rows * (cell + gap) + 12f * scale;
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(avail, totalHeight));
        }
    }
        
    private void DrawCustomAlbumGrid(Rect body, string[] paths, int albumKey)
    {
        var scale = UiScale.Current;
        var gridKey = ImGui.GetID("##customAlbumGrid");
        ImGui.SetCursorScreenPos(body.Min);
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, Vector2.Zero))
        using (var child = ImRaii.Child("##customAlbumGrid", body.Size, false,
                   DragScrollHost.ScrollFlags(ImGuiWindowFlags.NoBackground)))
        {
            if (!child)
            {
                return;
            }

            var surface = DragScrollHost.Begin(gridKey);
            if (resetScroll)
            {
                surface.JumpToTop();
                resetScroll = false;
            }

            var origin = ImGui.GetCursorScreenPos();
            var side = 2f * scale;
            var gap = 3f * scale;
            var avail = ScrollLayout.StableContentWidth();
            var cell = (avail - side * 2f - gap * (Columns - 1)) / Columns;
            var drawList = ImGui.GetWindowDrawList();
            var scrollY = ImGui.GetScrollY();
            var viewHeight = ImGui.GetWindowSize().Y;
            var margin = cell + 60f * scale;

            for (var index = 0; index < paths.Length; index++)
            {
                var column = index % Columns;
                var rowIndex = index / Columns;
                var top = 6f * scale + rowIndex * (cell + gap);

                if (top + cell < scrollY - margin || top > scrollY + viewHeight + margin)
                {
                    continue;
                }

                var min = new Vector2(origin.X + side + column * (cell + gap), origin.Y + top);
                var max = new Vector2(min.X + cell, min.Y + cell);
                var path = paths[index];
                var hovered = UiInteract.Hover(min, max);

                PhotosChrome.Thumbnail(drawList, GetThumbnail(path), min, max, hovered, ui.FieldSurface);
                if (hovered)
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                    var iconCenter = new Vector2(max.X - 14f * scale, min.Y + 14f * scale);
                    drawList.AddCircleFilled(iconCenter, 11f * scale, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.45f)), 16);
                    AppSkin.Icon(drawList, iconCenter, FontAwesomeIcon.EllipsisH.ToIconString(),
                                 new Vector4(1f, 1f, 1f, 0.9f), 0.55f);
                }

                if (UiInteract.Click(min, max, hovered))
                {
                    OpenViewerFromPaths(paths, index);
                }
                
                if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                {
                    var pos = ImGui.GetMousePos();
                    photoMenuKey = path;
                    albumMenu.Toggle("photo:" + path, new Rect(pos, pos + new Vector2(1f, 1f)));
                }
            }
            
            if (photoMenuKey is not null && albumMenu.IsOpenFor("photo:" + photoMenuKey))
            {
                var targetPath = photoMenuKey;
                albumMenu.Gate();
                var items = GetPhotoMenuItems();
                var picked = albumMenu.Draw(body, frameTheme, items, out var action);
                if (picked == 0 && (action == DropdownMenu.RowAction.Delete || action == DropdownMenu.RowAction.Select))
                {
                    confirm.Ask(new ConfirmRequest
                    {
                        Message = Loc.T(L.Photos.RemoveFromAlbum),
                        ConfirmLabel = Loc.T(L.Photos.RemoveFromAlbum),
                        CancelLabel = Loc.T(L.Common.Cancel),
                        Confirm = () => RemovePhotoFromCustomAlbum(albumKey, targetPath),
                    });
                }
            }

            var rows = (paths.Length + Columns - 1) / Columns;
            var totalHeight = rows * (cell + gap) + 12f * scale;
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(avail, totalHeight));
        }
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
