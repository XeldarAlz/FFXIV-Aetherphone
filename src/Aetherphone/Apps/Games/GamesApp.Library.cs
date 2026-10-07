using Aetherphone.Apps.Games.Hub;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Games;

internal readonly struct VisibleRows
{
    public readonly int First;
    public readonly int End;

    public VisibleRows(int first, int end)
    {
        First = first;
        End = end;
    }

    public static int Count(int itemCount, int columns) =>
        itemCount <= 0 || columns <= 0 ? 0 : (itemCount + columns - 1) / columns;

    public static VisibleRows Between(int rowCount, float top, float pitch, float clipTop, float clipBottom)
    {
        if (rowCount <= 0 || pitch <= 0f || clipBottom <= clipTop)
        {
            return default;
        }

        var first = Math.Clamp((int)MathF.Floor((clipTop - top) / pitch), 0, rowCount);
        var end = Math.Clamp((int)MathF.Floor((clipBottom - top) / pitch) + 1, first, rowCount);
        return new VisibleRows(first, end);
    }
}

internal sealed partial class GamesApp
{
    private const string LibraryNavId = "games.library.nav";
    private const string LibraryScopeId = "games.library";
    private const string LibraryGridId = "games.library.grid";
    private const string LibrarySortMenuId = "games.library.sort";
    private const string LibrarySearchId = "##gamesLibrarySearch";
    private const int SearchMaxLength = 40;
    private const float LibraryEmptyBandHeight = 300f;

    private readonly ChipRail libraryFilterRail = new();
    private readonly DropdownMenu librarySortMenu = new();
    private readonly string[] libraryFilterLabels = new string[GamesFilters.Count];
    private readonly bool[] libraryFilterActive = new bool[GamesFilters.Count];
    private readonly DropdownMenu.Item[] librarySortItems = new DropdownMenu.Item[GamesSorts.Count];
    private readonly NavBarButton[] libraryButtons = new NavBarButton[1];
    private LanguageInfo? libraryLanguage;
    private GamesFilter libraryFilter;
    private GamesSort librarySort;
    private bool focusSearch;
    private string searchText = string.Empty;
    private string libraryQuery = string.Empty;

    private void DrawLibrary(in PhoneContext context)
    {
        librarySortMenu.Gate();
        SyncLibraryLabels();
        var navBar = AppHeader.BeginLargeTitle(context, false);
        using (ImRaii.PushId(LibraryScopeId))
        using (AppSurface.Begin(navBar.Body))
        {
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var gap = Metrics.Space.Md * scale;
            var y = DrawLibrarySearch(drawList, origin, width, scale) + gap;
            y = DrawLibraryFilters(new Vector2(origin.X, y), width, scale) + gap;
            var entries = library.View(libraryFilter, librarySort, libraryQuery);
            y = entries.Length == 0
                ? DrawLibraryEmpty(new Vector2(origin.X, y), width, scale)
                : DrawLibraryResults(drawList, entries, new Vector2(origin.X, y), width, scale);
            FinishPage(origin, width, y, scale);
        }

        libraryButtons[0] = new NavBarButton(PhoneIcons.ArrowsSort, Loc.T(L.GamesHub.Sort));
        var sortAnchor = AppHeader.LargeTitleButtonRect(navBar, 0, libraryButtons.Length);
        if (AppHeader.EndLargeTitle(in navBar, context, LibraryNavId, Loc.T(L.GamesHub.TabLibrary),
                NavBarStyle.From(ui), libraryButtons) == 0)
        {
            OpenLibrarySort(sortAnchor);
        }

        var picked = librarySortMenu.Draw(screenRect, theme, librarySortItems);
        if (picked >= 0)
        {
            librarySort = (GamesSort)picked;
        }
    }

    private float DrawLibrarySearch(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var field = new Rect(origin, new Vector2(origin.X + width, origin.Y + SearchBar.HeightUnits * scale));
        var ink = ui.Ink;
        SearchBar.Surface(drawList, field, ink);
        ImGui.BeginDisabled(librarySortMenu.Open);
        GlassField.Search(drawList, field, LibrarySearchId, Loc.T(L.Games.SearchHint), ref searchText, ink.Ink,
            ink.Muted, scale, SearchMaxLength, focusSearch);
        ImGui.EndDisabled();
        focusSearch = false;
        if (!string.Equals(searchText, libraryQuery, StringComparison.Ordinal))
        {
            libraryQuery = searchText;
        }

        return field.Max.Y;
    }

    private float DrawLibraryFilters(Vector2 origin, float width, float scale)
    {
        for (var index = 0; index < libraryFilterActive.Length; index++)
        {
            libraryFilterActive[index] = index == (int)libraryFilter;
        }

        var row = new Rect(origin, new Vector2(origin.X + width, origin.Y + ChipRail.RowHeight * scale));
        var tapped = libraryFilterRail.Draw(row, ui, libraryFilterLabels, libraryFilterActive);
        if (tapped >= 0)
        {
            libraryFilter = (GamesFilter)tapped;
        }

        return row.Max.Y;
    }

    private float DrawLibraryEmpty(Vector2 origin, float width, float scale)
    {
        var band = new Rect(origin, new Vector2(origin.X + width, origin.Y + LibraryEmptyBandHeight * scale));
        if (EmptyState.Draw(band, ui, FontAwesomeIcon.Search, Loc.T(L.Games.SearchEmpty),
                Loc.T(L.Games.SearchEmptyHint), Loc.T(L.GamesHub.ClearSearch)))
        {
            searchText = string.Empty;
            libraryQuery = string.Empty;
            focusSearch = true;
        }

        return band.Max.Y;
    }

    private float DrawLibraryResults(ImDrawListPtr drawList, ReadOnlySpan<int> entries, Vector2 origin, float width,
        float scale)
    {
        Typography.Draw(drawList, origin, CountLabel(entries.Length), ui.MutedInk, TextStyles.Footnote);
        var top = origin.Y + Typography.LineHeight(TextStyles.Footnote) + Metrics.Space.Md * scale;
        return DrawLibraryGrid(drawList, entries, new Vector2(origin.X, top), width, scale);
    }

    private float DrawLibraryGrid(ImDrawListPtr drawList, ReadOnlySpan<int> entries, Vector2 origin, float width,
        float scale)
    {
        var gapX = HubMetrics.GridGapX * scale;
        var gapY = HubMetrics.GridGapY * scale;
        var columns = HubMetrics.GridColumns(width, HubMetrics.GridMinTile * scale, gapX);
        var side = HubMetrics.GridTile(width, columns, gapX);
        var pitchX = side + gapX;
        var pitchY = GameTileView.Height(side, scale) + gapY;
        var rowCount = VisibleRows.Count(entries.Length, columns);
        var rows = VisibleRows.Between(rowCount, origin.Y, pitchY, drawList.GetClipRectMin().Y - gapY,
            drawList.GetClipRectMax().Y + gapY);
        var tapped = -1;
        var tappedIcon = default(Rect);
        using (ImRaii.PushId(LibraryGridId))
        {
            for (var row = rows.First; row < rows.End; row++)
            {
                var rowTop = origin.Y + row * pitchY;
                var rowStart = row * columns;
                var rowEnd = Math.Min(rowStart + columns, entries.Length);
                for (var position = rowStart; position < rowEnd; position++)
                {
                    var topLeft = new Vector2(origin.X + (position - rowStart) * pitchX, rowTop);
                    if (GameTileView.Draw(drawList, ui, library, entries[position], topLeft, side, true))
                    {
                        tapped = entries[position];
                        tappedIcon = GameTileView.IconRect(topLeft, side);
                    }
                }
            }
        }

        if (tapped >= 0)
        {
            Activate(tapped, tappedIcon);
        }

        return origin.Y + rowCount * pitchY - gapY;
    }

    private void OpenLibrarySort(Rect anchor)
    {
        for (var index = 0; index < librarySortItems.Length; index++)
        {
            librarySortItems[index] = new DropdownMenu.Item(Loc.T(GamesSorts.Label((GamesSort)index)),
                Selected: index == (int)librarySort);
        }

        librarySortMenu.Header = Loc.T(L.GamesHub.Sort);
        librarySortMenu.Toggle(LibrarySortMenuId, anchor);
    }

    private void SyncLibraryLabels()
    {
        if (ReferenceEquals(libraryLanguage, Loc.Current))
        {
            return;
        }

        libraryLanguage = Loc.Current;
        for (var index = 0; index < libraryFilterLabels.Length; index++)
        {
            libraryFilterLabels[index] = Loc.T(GamesFilters.Label((GamesFilter)index));
        }
    }

    private void FocusLibrarySearch() => focusSearch = true;

    private void ResetLibrary()
    {
        focusSearch = false;
        searchText = string.Empty;
        libraryQuery = string.Empty;
        libraryFilter = GamesFilter.All;
        libraryFilterRail.Reset();
        librarySortMenu.Close();
    }
}
