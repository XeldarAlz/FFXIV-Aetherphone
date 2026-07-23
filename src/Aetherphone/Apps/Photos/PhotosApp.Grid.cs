using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Photos;

internal sealed partial class PhotosApp
{
    private void DrawRoot(Rect area)
    {
        DrawNavBar(area, DisplayName, null);
        var scale = ImGuiHelpers.GlobalScale;
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
            return;
        }

        if (entries.Length == 0)
        {
            DrawEmpty(body);
            return;
        }

        DrawAlbumsGrid(body);
    }

    private void DrawAlbum(Rect area, int key)
    {
        var scale = ImGuiHelpers.GlobalScale;
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

    private void DrawEmpty(Rect body) =>
        EmptyState.Draw(body, ui, FontAwesomeIcon.Image, Loc.T(L.Photos.NoPhotos), Loc.T(L.Photos.UseCameraHint));

    private void DrawPhotoGrid(Rect body, int start, int count)
    {
        var scale = ImGuiHelpers.GlobalScale;
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

            DragScrollHost.Begin(gridKey);
            if (resetScroll)
            {
                ImGui.SetScrollY(0f);
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
        var scale = ImGuiHelpers.GlobalScale;
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

            DragScrollHost.Begin(albumsKey);
            if (resetScroll)
            {
                ImGui.SetScrollY(0f);
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
            var total = albums.Count + 1;
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

                var album = albums[index - 1];
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
            ProgressRing.Sweep(new Vector2(rect.Center.X, rect.Min.Y + coverHeight * 0.5f), 10f * scale, 2f * scale,
                ui.MutedInk, 900.0, 1.8f, 0.9f);
        }

        Material.Edge(drawList, rect.Min, coverMax, rounding, scale, hovered ? 1f : 0.7f);
        if (hovered)
        {
            drawList.AddRectFilled(rect.Min, coverMax, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.06f)), rounding,
                ImDrawFlags.RoundCornersAll);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var textTop = coverMax.Y + 7f * scale;
        var name = Typography.FitText(title, rect.Width - 4f * scale, TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(rect.Min.X + 2f * scale, textTop), name, ui.TitleInk,
            TextStyles.SubheadlineEmphasized);
        var countLabel = Loc.Plural(L.Photos.Count, coverCount);
        Typography.Draw(drawList, new Vector2(rect.Min.X + 2f * scale, textTop + 19f * scale), countLabel, ui.MutedInk,
            TextStyles.Footnote);
        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    private void OpenAlbum(int key) => router.Push(PhotoView.Album(key));

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
}
