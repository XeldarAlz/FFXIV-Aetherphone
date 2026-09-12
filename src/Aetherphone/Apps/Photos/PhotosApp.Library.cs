using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Photos;

internal sealed partial class PhotosApp
{
    private const int LibraryTab = 0;
    private const int AlbumsTab = 1;
    private const float LogoSize = 30f;
    private const float LogoGap = 10f;
    private const float HeaderIconSize = 26f;
    private const float WordmarkPadX = 6f;
    private const float WordmarkPadY = 4f;
    private const float GridGap = 1.5f;
    private const float GridTopPad = 2f;
    private const float GridBottomPad = 24f;
    private const float SectionHeaderHeight = 44f;
    private const float SectionHeaderDrop = 3f;
    private const float SectionBlockGap = 8f;
    private const float CellPadX = SocialChrome.CellPadX;
    private const string SortMenuId = "photos.sort";
    private const int SortMenuItemCount = 6;
    private const int MenuRowFilter = 4;
    private const int MenuRowView = 5;
    private const int PageRowBack = 0;
    private const int FilterRowAll = 1;
    private const int FilterRowFavorites = 2;
    private const int FilterRowNotInAlbum = 3;
    private const int FilterRowCount = 4;
    private const int ViewRowZoomIn = 1;
    private const int ViewRowZoomOut = 2;
    private const int ViewRowAspect = 3;
    private const int ViewRowCount = 4;
    private const float FolderFabRadius = 27f;

    private enum SortMenuPage : byte
    {
        Main,
        Filter,
        View,
    }

    private SortMenuPage sortMenuPage;
    private string sortRowLabel = string.Empty;
    private string sortRowLocale = string.Empty;
    private int sortRowKey = -1;
    private bool sortRowAscending;

    private static readonly TextStyle WordmarkStyle = new(1.4f, FontWeight.Bold);
    private static readonly TextStyle ScreenTitleStyle = new(1.12f, FontWeight.Bold);

    private void DrawRoot(Rect area)
    {
        var scale = UiScale.Current;
        var navRect = new Rect(new Vector2(area.Min.X, area.Max.Y - BottomTabBar.Height * scale), area.Max);
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale),
            new Vector2(area.Max.X, navRect.Min.Y));
        if (selecting)
        {
            DrawSelectHeader(area);
            DrawLibrary(body);
            DrawSelectToolbar(navRect);
            return;
        }

        DrawTopBar(area, segment == AlbumsTab);
        if (segment == AlbumsTab)
        {
            DrawAlbumsTab(body);
        }
        else
        {
            DrawLibrary(body);
        }

        DrawTabBar(navRect);
    }

    private void DrawTopBar(Rect area, bool withNewAlbum)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        var logoSize = LogoSize * scale;
        var logoCenter = new Vector2(area.Min.X + CellPadX * scale + logoSize * 0.5f, rowCenterY);
        if (!AppIconTextures.TryDrawArtwork(drawList, Id, logoCenter, logoSize, Ink.AccentLink))
        {
            PhoneIcon.Draw(drawList, logoCenter, PhoneIcons.Photo, Ink.AccentLink, logoSize);
        }

        var radius = SocialChrome.HeaderIconRadius * scale;
        var libraryTools = !withNewAlbum && entries.Length > 0;
        var lastSlot = SocialChrome.HeaderSlot(area, libraryTools ? 1 : 0);
        var titleLeft = logoCenter.X + logoSize * 0.5f + LogoGap * scale;
        var titleRight = lastSlot.X - radius - Metrics.Space.Sm * scale;
        var titleHeight = Typography.LineHeight(WordmarkStyle);
        var title = Typography.FitText(DisplayName, MathF.Max(1f, titleRight - titleLeft), WordmarkStyle);
        var titleSize = Typography.Measure(title, WordmarkStyle);
        var titleMin = new Vector2(titleLeft - WordmarkPadX * scale, rowCenterY - titleHeight * 0.5f - WordmarkPadY * scale);
        var titleMax = new Vector2(titleLeft + titleSize.X + WordmarkPadX * scale,
            rowCenterY + titleHeight * 0.5f + WordmarkPadY * scale);
        UiInteract.HoverHighlight(drawList, titleMin, titleMax, Metrics.Radius.Sm * scale);
        Typography.Draw(drawList, new Vector2(titleLeft, rowCenterY - titleHeight * 0.5f), title, Ink.TitleInk,
            WordmarkStyle);
        if (UiInteract.HoverClick(titleMin, titleMax))
        {
            Refresh();
            resetScroll = true;
        }

        if (withNewAlbum)
        {
            if (SocialChrome.DrawHeaderIcon(drawList, SocialChrome.HeaderSlot(area, 0), radius, PhoneIcons.Plus,
                    HeaderIconSize, Loc.T(L.Photos.CreateAlbum), Ink, Ink.TitleInk))
            {
                OpenCreateAlbumSheet(null);
            }

            return;
        }

        if (!libraryTools)
        {
            return;
        }

        var sortSlot = SocialChrome.HeaderSlot(area, 0);
        if (SocialChrome.DrawHeaderIcon(drawList, sortSlot, radius, PhoneIcons.ArrowsSort, HeaderIconSize,
                Loc.T(L.Photos.SortBy), Ink, Ink.TitleInk, sortMenu.Open || Filter != PhotoFilter.All))
        {
            var extent = new Vector2(radius, radius);
            sortMenu.Toggle(SortMenuId, new Rect(sortSlot - extent, sortSlot + extent));
        }

        if (DrawSelectHeaderIcon(area, 1))
        {
            BeginSelect(SelectionScope.Library);
        }
    }

    private void DrawSortMenu(Rect screen)
    {
        if (!sortMenu.Open)
        {
            sortMenuPage = SortMenuPage.Main;
            return;
        }

        sortMenu.KeepOpen = true;
        switch (sortMenuPage)
        {
            case SortMenuPage.Filter:
                DrawFilterPage(screen);
                break;
            case SortMenuPage.View:
                DrawViewPage(screen);
                break;
            default:
                DrawSortPage(screen);
                break;
        }
    }

    private void DrawSortPage(Rect screen)
    {
        var key = SortKey;
        sortMenu.Header = string.Empty;
        for (var index = 0; index <= (int)PhotoSortKey.Dimensions; index++)
        {
            var rowKey = (PhotoSortKey)index;
            sortMenuItems[index] = new DropdownMenu.Item(rowKey == key ? ActiveSortLabel(key) : Loc.T(SortLabel(rowKey)),
                Selected: rowKey == key);
        }

        sortMenuItems[MenuRowFilter] = new DropdownMenu.Item(Loc.T(L.Photos.Filter));
        sortMenuItems[MenuRowView] = new DropdownMenu.Item(Loc.T(L.Photos.ViewOptions));
        var picked = sortMenu.Draw(screen, frameTheme, sortMenuItems);
        if (picked < 0)
        {
            return;
        }

        if (picked == MenuRowFilter)
        {
            sortMenuPage = SortMenuPage.Filter;
            return;
        }

        if (picked == MenuRowView)
        {
            sortMenuPage = SortMenuPage.View;
            return;
        }

        var pickedKey = (PhotoSortKey)picked;
        SetSort(pickedKey, pickedKey == key ? !configuration.PhotosSortAscending : DefaultAscending(pickedKey));
    }

    private void DrawFilterPage(Rect screen)
    {
        var filter = Filter;
        sortMenu.Header = Loc.T(L.Photos.Filter);
        sortMenuItems[PageRowBack] = new DropdownMenu.Item(Loc.T(L.Photos.MenuBack),
            IconGlyph.Of(FontAwesomeIcon.ChevronLeft));
        sortMenuItems[FilterRowAll] = new DropdownMenu.Item(Loc.T(L.Photos.FilterAll),
            Selected: filter == PhotoFilter.All);
        sortMenuItems[FilterRowFavorites] = new DropdownMenu.Item(Loc.T(L.Photos.Favorites),
            Selected: filter == PhotoFilter.Favorites);
        sortMenuItems[FilterRowNotInAlbum] = new DropdownMenu.Item(Loc.T(L.Photos.FilterNotInAlbum),
            Selected: filter == PhotoFilter.NotInAlbum);
        switch (sortMenu.Draw(screen, frameTheme, sortMenuItems.AsSpan(0, FilterRowCount)))
        {
            case PageRowBack:
                sortMenuPage = SortMenuPage.Main;
                break;
            case FilterRowAll:
                SetFilter(PhotoFilter.All);
                break;
            case FilterRowFavorites:
                SetFilter(PhotoFilter.Favorites);
                break;
            case FilterRowNotInAlbum:
                SetFilter(PhotoFilter.NotInAlbum);
                break;
        }
    }

    private void DrawViewPage(Rect screen)
    {
        sortMenu.Header = Loc.T(L.Photos.ViewOptions);
        sortMenuItems[PageRowBack] = new DropdownMenu.Item(Loc.T(L.Photos.MenuBack),
            IconGlyph.Of(FontAwesomeIcon.ChevronLeft));
        sortMenuItems[ViewRowZoomIn] = new DropdownMenu.Item(Loc.T(L.Photos.ZoomIn));
        sortMenuItems[ViewRowZoomOut] = new DropdownMenu.Item(Loc.T(L.Photos.ZoomOut));
        sortMenuItems[ViewRowAspect] = new DropdownMenu.Item(Loc.T(L.Photos.AspectRatioGrid),
            Selected: configuration.PhotosAspectGrid);
        switch (sortMenu.Draw(screen, frameTheme, sortMenuItems.AsSpan(0, ViewRowCount)))
        {
            case PageRowBack:
                sortMenuPage = SortMenuPage.Main;
                break;
            case ViewRowZoomIn:
                SetColumns(Columns - 1);
                break;
            case ViewRowZoomOut:
                SetColumns(Columns + 1);
                break;
            case ViewRowAspect:
                configuration.PhotosAspectGrid = !configuration.PhotosAspectGrid;
                configuration.Save();
                break;
        }
    }

    private static LocString SortLabel(PhotoSortKey key) => key switch
    {
        PhotoSortKey.Name => L.Photos.SortName,
        PhotoSortKey.Size => L.Photos.SortSize,
        PhotoSortKey.Dimensions => L.Photos.SortDimensions,
        _ => L.Photos.SortDate,
    };

    private string ActiveSortLabel(PhotoSortKey key)
    {
        var ascending = configuration.PhotosSortAscending;
        if (sortRowKey == (int)key && sortRowAscending == ascending &&
            string.Equals(sortRowLocale, Loc.Current.Code, StringComparison.Ordinal))
        {
            return sortRowLabel;
        }

        sortRowKey = (int)key;
        sortRowAscending = ascending;
        sortRowLocale = Loc.Current.Code;
        sortRowLabel = string.Concat(Loc.T(SortLabel(key)), " · ",
            Loc.T(ascending ? AscendingLabel(key) : DescendingLabel(key)));
        return sortRowLabel;
    }

    private void SetFilter(PhotoFilter filter)
    {
        sortMenu.Close();
        if (Filter == filter)
        {
            return;
        }

        configuration.PhotosFilter = (int)filter;
        configuration.Save();
        Refresh();
        resetScroll = true;
    }

    private void SetColumns(int columns)
    {
        var clamped = Math.Clamp(columns, MinColumns, MaxColumns);
        if (clamped == Columns)
        {
            return;
        }

        configuration.PhotosGridColumns = clamped;
        configuration.Save();
    }

    private static LocString DescendingLabel(PhotoSortKey key) => key switch
    {
        PhotoSortKey.Name => L.Photos.SortZToA,
        PhotoSortKey.Size => L.Photos.SortLargestFirst,
        PhotoSortKey.Dimensions => L.Photos.SortLargestFirst,
        _ => L.Photos.SortNewestFirst,
    };

    private static LocString AscendingLabel(PhotoSortKey key) => key switch
    {
        PhotoSortKey.Name => L.Photos.SortAToZ,
        PhotoSortKey.Size => L.Photos.SortSmallestFirst,
        PhotoSortKey.Dimensions => L.Photos.SortSmallestFirst,
        _ => L.Photos.SortOldestFirst,
    };

    private void SetSort(PhotoSortKey key, bool ascending)
    {
        if (SortKey == key && configuration.PhotosSortAscending == ascending)
        {
            return;
        }

        configuration.PhotosSortKey = (int)key;
        configuration.PhotosSortAscending = ascending;
        configuration.Save();
        Refresh();
        resetScroll = true;
    }

    private void DrawOpenFolderFab(Rect body)
    {
        if (ComposeFab.Draw(body, "##photosFolderFab", Ink.Accent, IconGlyph.Of(FontAwesomeIcon.FolderOpen),
                Loc.T(L.Photos.OpenFolder), "photos.openFolder", Ink.AccentDeep, FolderFabRadius))
        {
            UrlActions.OpenFolder(library.DirectoryPath);
        }
    }

    private void SetMonthlyAlbums(bool enabled)
    {
        configuration.PhotosMonthlyAlbums = enabled;
        configuration.Save();
    }

    private void DrawTabBar(Rect bar)
    {
        SocialChrome.PaintBarBackdrop(ui, ImGui.GetWindowDrawList(), bar, frameScreen);
        navTabs[LibraryTab] = new NavTab(FontAwesomeIcon.Image, Loc.T(L.Photos.Library), Glyph: PhoneIcons.Photo,
            ActiveGlyph: PhoneIcons.PhotoFilled);
        navTabs[AlbumsTab] = new NavTab(FontAwesomeIcon.Images, Loc.T(L.Photos.Albums),
            Glyph: PhoneIcons.LibraryPhoto);
        var picked = tabs.Draw(bar, ui, frameTheme, navTabs, segment);
        if (picked < 0 || picked == segment)
        {
            return;
        }

        segment = picked;
        configuration.PhotosSegment = picked;
        configuration.Save();
        resetScroll = true;
    }

    private void DrawLibrary(Rect body)
    {
        UiAnchors.Report("photos.grid", body);
        if (entries.Length == 0)
        {
            DrawEmpty(body);
            return;
        }

        DrawPhotoGrid(body, 0, entries.Length);
        if (!selecting)
        {
            DrawOpenFolderFab(body);
        }
    }

    private void DrawEmpty(Rect body)
    {
        if (EmptyState.Draw(body, ui, PhoneIcons.Photo, Loc.T(L.Photos.NoPhotos), Loc.T(L.Photos.UseCameraHint),
                Loc.T(L.Apps.Camera)))
        {
            frameNavigation.Open("camera");
        }
    }

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
            var total = LayoutBands(start, count, cell, gap, scale);
            var drawList = ImGui.GetWindowDrawList();
            var scrollY = ImGui.GetScrollY();
            var viewHeight = ImGui.GetWindowSize().Y;
            var margin = cell + SectionHeaderHeight * scale;
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
                    DrawSectionHeader(drawList, origin.X + CellPadX * scale, origin.X + avail - CellPadX * scale,
                        screenTop, band, scale);
                    continue;
                }

                DrawPhotoRow(drawList, band, origin.X, screenTop, cell, gap, start, count, scale);
            }

            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(avail, total));
        }
    }

    private float LayoutBands(int start, int count, float cell, float gap, float scale)
    {
        bands.Clear();
        var headerHeight = SectionHeaderHeight * scale;
        var rowStride = cell + gap;
        var blockGap = SectionBlockGap * scale;
        var y = GridTopPad * scale;
        var index = start;
        var end = start + count;
        if (SortKey != PhotoSortKey.Date)
        {
            var flatRows = (count + Columns - 1) / Columns;
            for (var row = 0; row < flatRows; row++)
            {
                var rowStart = start + row * Columns;
                bands.Add(new GridBand
                {
                    Header = false,
                    PhotoStart = rowStart,
                    PhotoCount = Math.Min(Columns, end - rowStart),
                    Top = y,
                    Height = cell,
                });
                y += rowStride;
            }

            return y + GridBottomPad * scale;
        }

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

        return y + GridBottomPad * scale;
    }

    private void DrawSectionHeader(ImDrawListPtr drawList, float left, float right, float top, GridBand band,
        float scale)
    {
        var label = DayLabel(band.Day);
        var count = Loc.Plural(L.Photos.Count, band.DayCount);
        var centerY = top + SectionHeaderHeight * scale * 0.5f + SectionHeaderDrop * scale;
        var countSize = Typography.Measure(count, TextStyles.Footnote);
        var nameMax = MathF.Max(Metrics.Space.Xl * scale, right - left - countSize.X - Metrics.Space.Md * scale);
        var name = Typography.FitText(label, nameMax, TextStyles.Headline);
        var nameSize = Typography.Measure(name, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(left, centerY - nameSize.Y * 0.5f), name, Ink.TitleInk,
            TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(right - countSize.X, centerY - countSize.Y * 0.5f), count, Ink.MutedInk,
            TextStyles.Footnote);
    }

    private void DrawPhotoRow(ImDrawListPtr drawList, GridBand band, float leftX, float top, float cell, float gap,
        int sliceStart, int sliceCount, float scale)
    {
        for (var column = 0; column < band.PhotoCount; column++)
        {
            var absolute = band.PhotoStart + column;
            var min = new Vector2(leftX + column * (cell + gap), top);
            var max = new Vector2(min.X + cell, min.Y + cell);
            if (DrawTile(drawList, min, max, entries[absolute].Path, false, scale) == TileHit.Open)
            {
                OpenViewer(sliceStart, sliceCount, absolute);
            }
        }
    }
}
