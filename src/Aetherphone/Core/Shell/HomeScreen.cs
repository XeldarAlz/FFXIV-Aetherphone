using System.Numerics;
using Aetherphone.Core.Analytics;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Shell.Home;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Core.Shell;

internal sealed class HomeScreen
{
    private const float LongPressSeconds = 0.42f;
    private const float TapSlop = 6f;
    private const float SwipeThreshold = 12f;
    private const float DragThreshold = 7f;
    private const float ReflowSmoothTime = 0.16f;
    private const float LiftSmoothTime = 0.13f;
    private const float SettleSmoothTime = 0.19f;
    private const float EdgeZone = 0.09f;
    private const float EdgeFlipSeconds = 0.45f;
    private const float IconLift = 1.16f;
    private const float WidgetLift = 1.045f;
    private const float TapPressDepth = 0.10f;
    private const float WidgetTapDepth = 0.03f;
    private const float TapPressInSeconds = 0.11f;
    private const float TapPopSeconds = 0.34f;
    private const float MagnifyBoost = 0.26f;
    private const float MagnifyRadiusCells = 1.35f;
    private const float MagnifyFadeTime = 0.13f;

    private struct TilePose
    {
        public Spring X;
        public Spring Y;
        public Spring W;
        public Spring H;
        public int Page;
        public bool Init;
    }

    private readonly HomeLayoutService layout;
    private readonly WidgetRegistry widgets;
    private readonly HomePager pager = new();
    private readonly FolderOverlay folder;
    private readonly WidgetSizeMenu sizeMenu;
    private readonly WidgetGallery gallery;
    private readonly Dictionary<string, TilePose> poses = new();
    private readonly List<HomeTile> previewTiles = new();
    private readonly List<GridCell> previewCells = new();
    private readonly List<GridCell> scratchCells = new();

    private bool pressActive;
    private Vector2 pressPos;
    private float pressTime;
    private HomeTile? pressTile;
    private bool pressFromDock;

    private HomeTile? tapTile;
    private float tapClock;
    private bool tapHolding;
    private float tapReleaseFrom;
    private float tapScale = 1f;

    private Spring magnifyGate;
    private Vector2 hoverPos;

    private bool editing;
    private float editClock;

    private HomeTile? dragTile;
    private bool dragFromDock;
    private int dragPage;
    private bool extraPage;
    private Vector2 dragPos;
    private Vector2 grabOffset;
    private Spring lift;
    private float edgeDwell;
    private int insertIndex;
    private bool overDock;
    private bool dockAccepts;
    private int dockInsertIndex;
    private HomeTile? folderTarget;

    private HomeTile? settleTile;
    private Spring settleX;
    private Spring settleY;
    private bool widgetAnchorReported;

    public HomeScreen(IReadOnlyList<IPhoneApp> apps, WidgetRegistry widgets)
    {
        this.widgets = widgets;
        layout = new HomeLayoutService(apps, widgets, Plugin.Cfg);
        folder = new FolderOverlay(layout);
        sizeMenu = new WidgetSizeMenu(layout);
        gallery = new WidgetGallery(layout, widgets);
    }

    public void Draw(Rect screen, Rect content, PhoneTheme theme, INavigator navigation, in HomeMotion motion)
    {
        layout.EnsureCurrent();
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        editClock += delta;
        var editReserve = editing && motion.Interactive ? HomeMetrics.EditToolbarBandUnits : 0f;
        var metrics = HomeMetrics.Compute(content, HomeLayoutService.Columns, layout.Rows, ImGuiHelpers.GlobalScale,
            motion, editReserve);
        pager.Step(delta, DisplayPageCount());
        var chromeAlpha = Math.Clamp(1f - motion.Progress * 1.6f, 0f, 1f);
        if (motion.Interactive)
        {
            HandleInput(content, metrics, navigation, delta);
        }
        else
        {
            pressActive = false;
            CancelTap();
        }

        AdvanceTap(delta);
        UpdateMagnify(content, motion, delta);
        widgetAnchorReported = false;
        var labelAlpha = chromeAlpha * (folder.Active ? 0.35f : 1f);
        DrawPages(metrics, theme, delta, labelAlpha, motion);
        DrawDock(metrics, theme, delta, chromeAlpha, motion);
        DrawPageControls(metrics, theme, chromeAlpha, motion.Interactive);
        if (editing && motion.Interactive)
        {
            DrawEditChrome(content, metrics, theme);
        }

        var ghostDrawList = ImGui.GetWindowDrawList();
        ghostDrawList.PushClipRect(screen.Min, screen.Max, true);
        DrawSettleGhost(metrics, theme, delta);
        DrawDragGhost(metrics, theme, delta);
        ghostDrawList.PopClipRect();
        folder.Draw(content, metrics, theme, navigation, editing, pager.Page, delta);
        DrawSizeMenu(content, metrics, theme, delta);
        gallery.Draw(screen, theme, delta, metrics.Scale);
    }

    public void PrepareReveal(string appId)
    {
        gallery.CloseImmediate();
        pressActive = false;
        CancelTap();
        if (dragTile is not null)
        {
            dragTile = null;
            extraPage = false;
        }

        var page = PageContaining(appId);
        if (page >= 0)
        {
            pager.SnapTo(page, layout.PageCount);
        }
    }

    public Rect? RevealRect(string appId, Rect content)
    {
        var metrics = HomeMetrics.Compute(content, HomeLayoutService.Columns, layout.Rows, ImGuiHelpers.GlobalScale,
            HomeMotion.Rest);
        var dock = layout.Dock;
        for (var index = 0; index < dock.Count; index++)
        {
            if (dock[index].App!.Id == appId)
            {
                return metrics.DockSlotRect(dock.Count, index);
            }
        }

        for (var page = 0; page < layout.PageCount; page++)
        {
            var tiles = layout.Page(page);
            var cells = layout.Placements(page);
            for (var index = 0; index < tiles.Count && index < cells.Count; index++)
            {
                if (TileTargets(tiles[index], appId))
                {
                    return metrics.TileRect(page, pager.Value, cells[index], tiles[index]);
                }
            }
        }

        return null;
    }

    private static bool TileTargets(HomeTile tile, string appId)
    {
        if (tile.App is not null)
        {
            return tile.App.Id == appId;
        }

        if (tile.IsWidget)
        {
            return tile.Widget!.AppId == appId;
        }

        for (var index = 0; index < tile.Apps.Count; index++)
        {
            if (tile.Apps[index].Id == appId)
            {
                return true;
            }
        }

        return false;
    }

    private int PageContaining(string appId)
    {
        for (var page = 0; page < layout.PageCount; page++)
        {
            var tiles = layout.Page(page);
            for (var index = 0; index < tiles.Count; index++)
            {
                if (TileTargets(tiles[index], appId))
                {
                    return page;
                }
            }
        }

        return -1;
    }

    private int DisplayPageCount() => layout.PageCount + (dragTile is not null && extraPage ? 1 : 0);

    private void HandleInput(Rect content, in HomeMetrics metrics, INavigator navigation, float delta)
    {
        if (gallery.Active || folder.Active || sizeMenu.Active)
        {
            pressActive = false;
            return;
        }

        if (dragTile is not null)
        {
            HandleDrag(content, metrics, delta);
            return;
        }

        if (pager.Dragging)
        {
            pager.Drag(ImGui.GetMousePos().X, content.Width, DisplayPageCount(), delta);
            if (ImGui.IsMouseReleased(ImGuiMouseButton.Left) || !ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                pager.Release(content.Width, DisplayPageCount());
            }

            return;
        }

        HandlePress(content, metrics, navigation, delta);
    }

    private void HandlePress(Rect content, in HomeMetrics metrics, INavigator navigation, float delta)
    {
        var mouse = ImGui.GetMousePos();
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && content.Contains(mouse))
        {
            if (editing && HandleEditChromeClick(content, metrics, mouse))
            {
                return;
            }

            pressActive = true;
            pressPos = mouse;
            pressTime = 0f;
            pressTile = TileAt(metrics, mouse, out pressFromDock);
            if (pressTile is not null)
            {
                BeginTap(pressTile);
            }
        }

        if (pressActive && ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            pressTime += delta;
            var move = mouse - pressPos;
            var canSwipe = !pressFromDock && !(editing && pressTile is not null);
            if (canSwipe && MathF.Abs(move.X) > SwipeThreshold * metrics.Scale &&
                MathF.Abs(move.X) > MathF.Abs(move.Y) * 1.2f)
            {
                pager.Begin(pressPos.X);
                pressActive = false;
                CancelTap();
                return;
            }

            if (editing && pressTile is not null && move.Length() > DragThreshold * metrics.Scale)
            {
                BeginDrag(metrics, pressTile, mouse);
                return;
            }

            if (!editing && pressTime > LongPressSeconds && move.Length() < TapSlop * metrics.Scale)
            {
                editing = true;
                if (pressTile is not null)
                {
                    BeginDrag(metrics, pressTile, mouse);
                }
                else
                {
                    pressActive = false;
                }

                return;
            }
        }

        if (pressActive && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            ReleaseTap();
            var move = mouse - pressPos;
            if (move.Length() < TapSlop * metrics.Scale && pressTime < LongPressSeconds)
            {
                HandleTap(metrics, navigation);
            }

            pressActive = false;
        }
    }

    private bool RemoveBadgesLive(in HomeMotion motion) =>
        editing && motion.Interactive && !gallery.Active && !sizeMenu.Active && !folder.Active;

    private bool HandleEditChromeClick(Rect content, in HomeMetrics metrics, Vector2 mouse)
    {
        if (DoneRect(content, metrics).Contains(mouse))
        {
            editing = false;
            pressActive = false;
            return true;
        }

        if (AddRect(content, metrics).Contains(mouse))
        {
            gallery.Open(pager.Page);
            pressActive = false;
            return true;
        }

        return false;
    }

    private void HandleTap(in HomeMetrics metrics, INavigator navigation)
    {
        if (pressTile is null)
        {
            if (editing)
            {
                editing = false;
            }

            return;
        }

        var rect = CommittedRect(metrics, pressTile) ?? new Rect(pressPos, pressPos);
        if (pressTile.IsWidget)
        {
            if (editing)
            {
                sizeMenu.Open(pressTile);
            }
            else if (widgets.AppFor(pressTile.Widget!) is { } app)
            {
                navigation.OpenAppFrom(app, AppOpenSource.Widget, rect);
            }

            return;
        }

        if (pressTile.IsFolder)
        {
            folder.Open(pressTile, rect);
            return;
        }

        if (!editing)
        {
            navigation.OpenAppFrom(pressTile.App!, pressFromDock ? AppOpenSource.Dock : AppOpenSource.Home, rect);
        }
    }

    private HomeTile? TileAt(in HomeMetrics metrics, Vector2 mouse, out bool fromDock)
    {
        fromDock = false;
        var dock = layout.Dock;
        if (dock.Count > 0 && Expand(metrics.DockBar, 4f * metrics.Scale).Contains(mouse))
        {
            for (var index = 0; index < dock.Count; index++)
            {
                if (Expand(metrics.DockSlotRect(dock.Count, index), 6f * metrics.Scale).Contains(mouse))
                {
                    fromDock = true;
                    return dock[index];
                }
            }

            return null;
        }

        if (!metrics.Grid.Contains(mouse))
        {
            return null;
        }

        var page = Math.Clamp((int)MathF.Round(pager.Value), 0, layout.PageCount - 1);
        var cell = metrics.CellFromPoint(page, pager.Value, mouse);
        var tiles = layout.Page(page);
        var cells = layout.Placements(page);
        for (var index = 0; index < tiles.Count && index < cells.Count; index++)
        {
            var tile = tiles[index];
            var anchor = cells[index];
            if (cell.Column >= anchor.Column && cell.Column < anchor.Column + tile.ColumnSpan &&
                cell.Row >= anchor.Row && cell.Row < anchor.Row + tile.RowSpan)
            {
                return tile;
            }
        }

        return null;
    }

    private void BeginDrag(in HomeMetrics metrics, HomeTile tile, Vector2 mouse)
    {
        dragTile = tile;
        dragFromDock = pressFromDock;
        dragPage = dragFromDock ? pager.Page : LocatePage(tile);
        extraPage = false;
        var center = CommittedRect(metrics, tile)?.Center ?? mouse;
        grabOffset = mouse - center;
        dragPos = center;
        lift.SnapTo(1f);
        edgeDwell = 0f;
        folderTarget = null;
        overDock = false;
        insertIndex = 0;
        pressActive = false;
        editing = true;
        settleTile = null;
        CancelTap();
        poses.Remove(tile.Key);
        poses.Remove(string.Concat("dock:", tile.Key));
    }

    private int LocatePage(HomeTile tile)
    {
        var (page, _) = layout.Locate(tile);
        return page >= 0 ? page : pager.Page;
    }

    private void HandleDrag(Rect content, in HomeMetrics metrics, float delta)
    {
        var mouse = ImGui.GetMousePos();
        dragPos = mouse - grabOffset;
        lift.Step(dragTile!.IsWidget ? WidgetLift : IconLift, LiftSmoothTime, delta);
        overDock = dragTile.App is not null && Expand(metrics.DockBar, 8f * metrics.Scale).Contains(mouse);
        if (overDock)
        {
            folderTarget = null;
            edgeDwell = 0f;
            var siblings = DockSiblingCount();
            dockAccepts = dragFromDock || layout.CanDock(dragTile);
            var slotWidth = metrics.DockBar.Width / Math.Max(1, siblings + 1);
            dockInsertIndex = Math.Clamp((int)((mouse.X - metrics.DockBar.Min.X) / slotWidth), 0, siblings);
        }
        else
        {
            HandleGridDrag(content, metrics, mouse, delta);
        }

        if (!ImGui.IsMouseReleased(ImGuiMouseButton.Left) && ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            return;
        }

        CommitDrag(metrics);
    }

    private int DockSiblingCount()
    {
        var dock = layout.Dock;
        var count = dock.Count;
        for (var index = 0; index < dock.Count; index++)
        {
            if (ReferenceEquals(dock[index], dragTile))
            {
                count--;
            }
        }

        return count;
    }

    private void HandleGridDrag(Rect content, in HomeMetrics metrics, Vector2 mouse, float delta)
    {
        var edge = EdgeZone * content.Width;
        if (mouse.X < content.Min.X + edge && dragPage > 0)
        {
            edgeDwell += delta;
            if (edgeDwell > EdgeFlipSeconds)
            {
                dragPage--;
                pager.AnimateTo(dragPage, DisplayPageCount());
                edgeDwell = 0f;
            }
        }
        else if (mouse.X > content.Max.X - edge)
        {
            edgeDwell += delta;
            if (edgeDwell > EdgeFlipSeconds)
            {
                if (dragPage < layout.PageCount - 1)
                {
                    dragPage++;
                }
                else if (!extraPage && dragPage == layout.PageCount - 1 && PageHasOthers(dragPage))
                {
                    extraPage = true;
                    dragPage = layout.PageCount;
                }

                pager.AnimateTo(dragPage, DisplayPageCount());
                edgeDwell = 0f;
            }
        }
        else
        {
            edgeDwell = 0f;
        }

        folderTarget = null;
        if (dragPage >= layout.PageCount)
        {
            insertIndex = 0;
            return;
        }

        var tiles = layout.Page(dragPage);
        var cells = layout.Placements(dragPage);
        if (!dragTile!.IsWidget)
        {
            var radius = metrics.IconSize * 0.42f;
            for (var index = 0; index < tiles.Count && index < cells.Count; index++)
            {
                var candidate = tiles[index];
                if (ReferenceEquals(candidate, dragTile) || candidate.IsWidget ||
                    candidate.IsFolder && dragTile.IsFolder)
                {
                    continue;
                }

                var center = metrics.IconCenter(dragPage, pager.Value, cells[index]);
                if (Vector2.Distance(center, mouse) < radius)
                {
                    folderTarget = candidate;
                    return;
                }
            }
        }

        var cursorCell = metrics.CellFromPoint(dragPage, pager.Value, mouse);
        insertIndex = InsertIndexFor(cursorCell);
    }

    private bool PageContainsTile(int page, HomeTile tile)
    {
        if (page >= layout.PageCount)
        {
            return false;
        }

        var tiles = layout.Page(page);
        for (var index = 0; index < tiles.Count; index++)
        {
            if (ReferenceEquals(tiles[index], tile))
            {
                return true;
            }
        }

        return false;
    }

    private bool PageHasOthers(int page)
    {
        var tiles = layout.Page(page);
        for (var index = 0; index < tiles.Count; index++)
        {
            if (!ReferenceEquals(tiles[index], dragTile))
            {
                return true;
            }
        }

        return false;
    }

    private int InsertIndexFor(GridCell cursor)
    {
        BuildSiblings(dragPage);
        HomeGridSolver.Solve(previewTiles, HomeLayoutService.Columns, layout.Rows, scratchCells);
        var cursorOrder = HomeGridSolver.ScanOrder(cursor, HomeLayoutService.Columns);
        var index = 0;
        for (; index < scratchCells.Count; index++)
        {
            if (HomeGridSolver.ScanOrder(scratchCells[index], HomeLayoutService.Columns) >= cursorOrder)
            {
                break;
            }
        }

        return index;
    }

    private void BuildSiblings(int page)
    {
        previewTiles.Clear();
        if (page >= layout.PageCount)
        {
            return;
        }

        var tiles = layout.Page(page);
        for (var index = 0; index < tiles.Count; index++)
        {
            if (!ReferenceEquals(tiles[index], dragTile))
            {
                previewTiles.Add(tiles[index]);
            }
        }
    }

    private void CommitDrag(in HomeMetrics metrics)
    {
        var tile = dragTile!;
        if (folderTarget is not null)
        {
            layout.MakeFolder(folderTarget, tile);
        }
        else if (overDock && dockAccepts)
        {
            layout.MoveToDock(tile, dockInsertIndex);
            BeginSettle(tile, metrics);
        }
        else if (!overDock)
        {
            layout.MoveTile(tile, dragPage, insertIndex);
            BeginSettle(tile, metrics);
        }
        else
        {
            BeginSettle(tile, metrics);
        }

        dragTile = null;
        folderTarget = null;
        extraPage = false;
        overDock = false;
        pager.AnimateTo(pager.Page, layout.PageCount);
    }

    private void BeginSettle(HomeTile tile, in HomeMetrics metrics)
    {
        settleTile = tile;
        settleX.SnapTo(dragPos.X - metrics.Content.Min.X);
        settleY.SnapTo(dragPos.Y - metrics.Content.Min.Y);
    }

    private Rect? CommittedRect(in HomeMetrics metrics, HomeTile tile)
    {
        var dock = layout.Dock;
        for (var index = 0; index < dock.Count; index++)
        {
            if (ReferenceEquals(dock[index], tile))
            {
                return metrics.DockSlotRect(dock.Count, index);
            }
        }

        var (page, tileIndex) = layout.Locate(tile);
        if (page < 0)
        {
            return null;
        }

        var cells = layout.Placements(page);
        if (tileIndex >= cells.Count)
        {
            return null;
        }

        return metrics.TileRect(page, pager.Value, cells[tileIndex], tile);
    }

    private void DrawPages(in HomeMetrics metrics, PhoneTheme theme, float delta, float labelAlpha,
        in HomeMotion motion)
    {
        var drawList = ImGui.GetWindowDrawList();
        drawList.PushClipRect(metrics.Content.Min, new Vector2(metrics.Content.Max.X, metrics.DockBar.Min.Y), true);
        var scroll = pager.Value;
        var first = Math.Max(0, (int)MathF.Floor(scroll) - 1);
        var last = Math.Min(layout.PageCount - 1, (int)MathF.Ceiling(scroll) + 1);
        for (var page = first; page <= last; page++)
        {
            DrawPage(metrics, theme, page, delta, labelAlpha, motion);
        }

        drawList.PopClipRect();
    }

    private void DrawPage(in HomeMetrics metrics, PhoneTheme theme, int page, float delta, float labelAlpha,
        in HomeMotion motion)
    {
        IReadOnlyList<HomeTile> tiles = layout.Page(page);
        IReadOnlyList<GridCell> cells;
        var previewInsert = dragTile is not null && page == dragPage && !overDock && folderTarget is null;
        var previewCompact = dragTile is not null && !previewInsert && PageContainsTile(page, dragTile);
        if (previewInsert || previewCompact)
        {
            BuildSiblings(page);
            if (previewInsert)
            {
                previewTiles.Insert(Math.Clamp(insertIndex, 0, previewTiles.Count), dragTile!);
            }

            HomeGridSolver.Solve(previewTiles, HomeLayoutService.Columns, layout.Rows, previewCells);
            cells = previewCells;
            tiles = previewTiles;
        }
        else
        {
            cells = layout.Placements(page);
        }

        var pageOffset = new Vector2(metrics.PageOffsetX(page, pager.Value), 0f);
        for (var index = 0; index < tiles.Count && index < cells.Count; index++)
        {
            var tile = tiles[index];
            if (ReferenceEquals(tile, dragTile) || ReferenceEquals(tile, settleTile))
            {
                continue;
            }

            var target = metrics.TileRect(page, page, cells[index], tile);
            var local = new Rect(target.Min - metrics.Grid.Min, target.Max - metrics.Grid.Min);
            var rect = PoseRect(tile.Key, page, local, metrics.Grid.Min + pageOffset, delta, motion.Interactive);
            DrawTile(metrics, theme, tile, rect, labelAlpha, delta, motion,
                ReferenceEquals(tile, folderTarget));
        }
    }

    private Rect PoseRect(string key, int page, Rect localTarget, Vector2 origin, float delta, bool animate)
    {
        if (poses.Count > 256)
        {
            poses.Clear();
        }

        var center = localTarget.Center;
        var size = localTarget.Size;
        if (!poses.TryGetValue(key, out var pose) || !pose.Init || pose.Page != page || !animate)
        {
            pose.X = new Spring(center.X);
            pose.Y = new Spring(center.Y);
            pose.W = new Spring(size.X);
            pose.H = new Spring(size.Y);
            pose.Page = page;
            pose.Init = true;
        }
        else
        {
            pose.X.Step(center.X, ReflowSmoothTime, delta);
            pose.Y.Step(center.Y, ReflowSmoothTime, delta);
            pose.W.Step(size.X, ReflowSmoothTime, delta);
            pose.H.Step(size.Y, ReflowSmoothTime, delta);
        }

        poses[key] = pose;
        var posed = new Vector2(pose.X.Value, pose.Y.Value) + origin;
        var half = new Vector2(pose.W.Value, pose.H.Value) * 0.5f;
        return new Rect(posed - half, posed + half);
    }

    private void DrawTile(in HomeMetrics metrics, PhoneTheme theme, HomeTile tile, Rect rect, float labelAlpha,
        float delta, in HomeMotion motion, bool highlight)
    {
        var scale = metrics.Scale;
        var zoom = motion.Zoom;
        var jiggle = Jiggle(tile, scale);
        var center = rect.Center + jiggle;
        rect = new Rect(rect.Min + jiggle, rect.Max + jiggle);
        if (highlight)
        {
            var ring = metrics.IconSize * 0.5f + 4f * scale;
            ImGui.GetWindowDrawList().AddCircle(center, ring,
                ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, 0.8f)), 32, 2f * scale);
        }

        if (tile.IsWidget)
        {
            var pressScale = TapScale(tile);
            var drawRect = pressScale == 1f ? rect : ScaleRect(rect, pressScale);
            tile.Widget!.Draw(new WidgetContext(ImGui.GetWindowDrawList(), drawRect, theme, tile.Size, scale, delta,
                Math.Clamp(labelAlpha + 0.35f, 0f, 1f)));
            ReportWidgetAnchor(rect, motion);
            if (RemoveBadgesLive(motion) &&
                HomeTileView.RemoveBadge(new Vector2(rect.Min.X + 4f * scale, rect.Min.Y + 4f * scale), scale, theme))
            {
                layout.RemoveTile(tile);
                ConsumeEditGesture();
            }

            return;
        }

        if (tile.IsFolder)
        {
            HomeTileView.DrawFolder(center, rect.Width, tile, theme, TapScale(tile) * Magnify(center, metrics.CellWidth),
                labelAlpha, Loc.T(L.Home.NewFolder), metrics.CellWidth, zoom);
            if (RemoveBadgesLive(motion) &&
                HomeTileView.RemoveBadge(new Vector2(rect.Min.X + 2f * scale, rect.Min.Y + 2f * scale), scale, theme))
            {
                layout.DisbandFolder(tile);
                ConsumeEditGesture();
            }

            ReportIconAnchor(tile, center, rect.Width, motion);
            return;
        }

        HomeTileView.DrawApp(center, rect.Width, tile.App!, theme, TapScale(tile) * Magnify(center, metrics.CellWidth),
            labelAlpha, metrics.CellWidth, zoom);
        ReportIconAnchor(tile, center, rect.Width, motion);
    }

    private static Rect ScaleRect(Rect rect, float factor)
    {
        var center = rect.Center;
        var half = rect.Size * 0.5f * factor;
        return new Rect(center - half, center + half);
    }

    private void DrawDock(in HomeMetrics metrics, PhoneTheme theme, float delta, float alpha, in HomeMotion motion)
    {
        var dock = layout.Dock;
        var showGap = dragTile is not null && overDock && dockAccepts;
        var slotTileCount = 0;
        for (var index = 0; index < dock.Count; index++)
        {
            if (!ReferenceEquals(dock[index], dragTile))
            {
                slotTileCount++;
            }
        }

        if (slotTileCount == 0 && !showGap || alpha <= 0.01f)
        {
            return;
        }

        var slotCount = slotTileCount + (showGap ? 1 : 0);
        var drawList = ImGui.GetWindowDrawList();
        var rounding = 34f * metrics.Scale;
        Elevation.Draw(drawList, metrics.DockBar.Min, metrics.DockBar.Max, rounding, metrics.Scale, 26f, 7f, 0.26f,
            alpha);
        Material.Dock(drawList, metrics.DockBar.Min, metrics.DockBar.Max, rounding, metrics.Scale,
            WallpaperLegibility.Strength(theme), alpha);
        var slot = 0;
        for (var index = 0; index < dock.Count; index++)
        {
            var tile = dock[index];
            if (ReferenceEquals(tile, dragTile))
            {
                continue;
            }

            var visualSlot = showGap && slot >= dockInsertIndex ? slot + 1 : slot;
            var target = metrics.DockSlotRect(Math.Max(1, slotCount), visualSlot);
            var local = new Rect(target.Min - metrics.DockBar.Min, target.Max - metrics.DockBar.Min);
            var rect = PoseRect(string.Concat("dock:", tile.Key), -1, local,
                metrics.DockBar.Min, delta, motion.Interactive);
            slot++;
            if (ReferenceEquals(tile, settleTile))
            {
                continue;
            }

            var jiggle = Jiggle(tile, metrics.Scale);
            HomeTileView.DrawApp(rect.Center + jiggle, rect.Width, tile.App!, theme,
                TapScale(tile) * Magnify(rect.Center, metrics.CellWidth), 0f, 0f, motion.Zoom);
            ReportIconAnchor(tile, rect.Center, rect.Width, motion);
        }
    }

    private void DrawPageControls(in HomeMetrics metrics, PhoneTheme theme, float alpha, bool interactive)
    {
        var pageCount = DisplayPageCount();
        if (pageCount <= 1 || alpha <= 0.01f)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var scale = metrics.Scale;
        var spacing = 14f * scale;
        var radius = 3f * scale;
        var totalWidth = (pageCount - 1) * spacing;
        var startX = metrics.Content.Center.X - totalWidth * 0.5f;
        var y = metrics.DotsCenterY;
        var active = Math.Clamp((int)MathF.Round(pager.Value), 0, pageCount - 1);
        for (var index = 0; index < pageCount; index++)
        {
            var center = new Vector2(startX + index * spacing, y);
            var hovered = interactive && dragTile is null &&
                          ImGui.IsMouseHoveringRect(center - new Vector2(spacing * 0.5f),
                              center + new Vector2(spacing * 0.5f));
            var dotAlpha = index == active ? 0.95f : hovered ? 0.55f : 0.32f;
            drawList.AddCircleFilled(center, radius,
                ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, dotAlpha * alpha)), 16);
            if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                pager.AnimateTo(index, pageCount);
                pressActive = false;
            }
        }

        DrawPageArrow(metrics, theme, alpha, interactive, -1, pageCount);
        DrawPageArrow(metrics, theme, alpha, interactive, 1, pageCount);
    }

    private void DrawPageArrow(in HomeMetrics metrics, PhoneTheme theme, float alpha, bool interactive, int direction,
        int pageCount)
    {
        var target = pager.Page + direction;
        if (target < 0 || target > pageCount - 1)
        {
            return;
        }

        var scale = metrics.Scale;
        var tabWidth = 20f * scale;
        var tabHalfHeight = 30f * scale;
        var centerY = metrics.Grid.Center.Y;
        var leftEdge = metrics.Content.Min.X - theme.SidePadding * scale;
        var rightEdge = metrics.Content.Max.X + theme.SidePadding * scale;
        var tab = direction < 0
            ? new Rect(new Vector2(leftEdge, centerY - tabHalfHeight),
                new Vector2(leftEdge + tabWidth, centerY + tabHalfHeight))
            : new Rect(new Vector2(rightEdge - tabWidth, centerY - tabHalfHeight),
                new Vector2(rightEdge, centerY + tabHalfHeight));
        var hit = Expand(tab, 4f * scale);
        var hovered = interactive && dragTile is null && ImGui.IsMouseHoveringRect(hit.Min, hit.Max);
        var drawList = ImGui.GetWindowDrawList();
        var rounding = 7f * scale;
        var corners = direction < 0 ? ImDrawFlags.RoundCornersRight : ImDrawFlags.RoundCornersLeft;
        drawList.AddRectFilled(tab.Min, tab.Max,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, (hovered ? 0.42f : 0.28f) * alpha)), rounding, corners);
        drawList.AddRect(tab.Min, tab.Max,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, (hovered ? 0.35f : 0.18f) * alpha)), rounding, corners,
            1f * scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var center = tab.Center;
        var reach = 4.2f * scale;
        var thickness = 2.2f * scale;
        var tip = new Vector2(center.X + reach * 0.55f * direction, center.Y);
        var upper = new Vector2(tip.X - reach * direction, tip.Y - reach);
        var lower = new Vector2(tip.X - reach * direction, tip.Y + reach);
        var ink = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, (hovered ? 1f : 0.85f) * alpha));
        drawList.AddLine(upper, tip, ink, thickness);
        drawList.AddLine(tip, lower, ink, thickness);
        var cap = thickness * 0.5f;
        drawList.AddCircleFilled(upper, cap, ink, 8);
        drawList.AddCircleFilled(tip, cap, ink, 8);
        drawList.AddCircleFilled(lower, cap, ink, 8);
        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            pager.AnimateTo(target, pageCount);
            pressActive = false;
            CancelTap();
        }
    }

    private void DrawEditChrome(Rect content, in HomeMetrics metrics, PhoneTheme theme)
    {
        var drawList = ImGui.GetWindowDrawList();
        var scale = metrics.Scale;
        var add = AddRect(content, metrics);
        var addHovered = ImGui.IsMouseHoveringRect(add.Min, add.Max);
        var addCenter = add.Center;
        drawList.AddCircleFilled(addCenter, add.Width * 0.5f,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, addHovered ? 0.26f : 0.17f)), 32);
        var arm = add.Width * 0.22f;
        var ink = ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, 0.95f));
        drawList.AddLine(addCenter - new Vector2(arm, 0f), addCenter + new Vector2(arm, 0f), ink, 2f * scale);
        drawList.AddLine(addCenter - new Vector2(0f, arm), addCenter + new Vector2(0f, arm), ink, 2f * scale);
        if (addHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var done = DoneRect(content, metrics);
        var doneHovered = ImGui.IsMouseHoveringRect(done.Min, done.Max);
        Squircle.Fill(drawList, done.Min, done.Max, done.Height * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(theme.Accent, doneHovered ? 1f : 0.88f)));
        Typography.DrawCentered(done.Center, Loc.T(L.Home.Done), new Vector4(1f, 1f, 1f, 1f), 0.82f,
            FontWeight.SemiBold);
        if (doneHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
    }

    private void DrawDragGhost(in HomeMetrics metrics, PhoneTheme theme, float delta)
    {
        if (dragTile is null)
        {
            return;
        }

        DrawGhost(metrics, theme, dragTile, dragPos, lift.Value, delta);
    }

    private void DrawSettleGhost(in HomeMetrics metrics, PhoneTheme theme, float delta)
    {
        if (settleTile is null)
        {
            return;
        }

        var target = CommittedRect(metrics, settleTile);
        if (target is not { } rect)
        {
            settleTile = null;
            return;
        }

        settleX.Step(rect.Center.X - metrics.Content.Min.X, SettleSmoothTime, delta);
        settleY.Step(rect.Center.Y - metrics.Content.Min.Y, SettleSmoothTime, delta);
        lift.Step(1f, SettleSmoothTime, delta);
        var position = metrics.Content.Min + new Vector2(settleX.Value, settleY.Value);
        if (Vector2.Distance(position, rect.Center) < 0.8f && MathF.Abs(lift.Value - 1f) < 0.01f)
        {
            settleTile = null;
            return;
        }

        DrawGhost(metrics, theme, settleTile, position, lift.Value, delta);
    }

    private void DrawGhost(in HomeMetrics metrics, PhoneTheme theme, HomeTile tile, Vector2 position, float scale,
        float delta)
    {
        var drawList = ImGui.GetWindowDrawList();
        if (tile.IsWidget)
        {
            var (page, index) = layout.Locate(tile);
            var size = page >= 0 && index < layout.Placements(page).Count
                ? metrics.TileRect(page, pager.Value, layout.Placements(page)[index], tile).Size
                : new Vector2(metrics.CellWidth * tile.ColumnSpan - (metrics.CellWidth - metrics.IconSize),
                    metrics.CellHeight * tile.RowSpan - HomeMetrics.LabelBandUnits * metrics.Scale);
            var half = size * 0.5f * scale;
            var rect = new Rect(position - half, position + half);
            Elevation.Floating(drawList, rect.Min, rect.Max, WidgetChromeRadius(metrics.Scale), metrics.Scale);
            tile.Widget!.Draw(new WidgetContext(drawList, rect, theme, tile.Size, metrics.Scale, delta, 1f));
            return;
        }

        var iconHalf = metrics.IconSize * 0.5f * scale;
        Elevation.Icon(drawList, position - new Vector2(iconHalf), position + new Vector2(iconHalf),
            metrics.IconSize * scale * 0.26f, metrics.IconSize * scale * 0.5f);
        if (tile.IsFolder)
        {
            HomeTileView.DrawFolder(position, metrics.IconSize, tile, theme, scale, 0f, Loc.T(L.Home.NewFolder),
                metrics.CellWidth);
            return;
        }

        HomeTileView.DrawApp(position, metrics.IconSize, tile.App!, theme, scale, 0f, metrics.CellWidth);
    }

    private static float WidgetChromeRadius(float scale) => 22f * scale;

    private void DrawSizeMenu(Rect content, in HomeMetrics metrics, PhoneTheme theme, float delta)
    {
        if (!sizeMenu.Active)
        {
            return;
        }

        var tile = sizeMenu.Tile!;
        var anchor = CommittedRect(metrics, tile);
        if (anchor is not { } rect)
        {
            sizeMenu.Close();
            return;
        }

        sizeMenu.Draw(content, rect, theme, delta, metrics.Scale);
    }

    private Vector2 Jiggle(HomeTile tile, float scale)
    {
        if (!editing || ReferenceEquals(tile, dragTile))
        {
            return Vector2.Zero;
        }

        var phase = (tile.Key.GetHashCode() & 0x3ff) / 1023f * MathF.PI * 2f;
        var x = MathF.Sin(editClock * 11f + phase);
        var y = MathF.Cos(editClock * 13f + phase * 1.3f);
        return new Vector2(x, y) * 1.1f * scale;
    }

    private void UpdateMagnify(Rect content, in HomeMotion motion, float delta)
    {
        hoverPos = ImGui.GetMousePos();
        var active = motion.Interactive && !editing && dragTile is null && settleTile is null &&
                     !folder.Active && !gallery.Active && !sizeMenu.Active && !pager.Dragging &&
                     content.Contains(hoverPos);
        magnifyGate.Step(active ? 1f : 0f, MagnifyFadeTime, delta);
    }

    private float Magnify(Vector2 center, float cellWidth)
    {
        var strength = magnifyGate.Value;
        if (strength <= 0.001f)
        {
            return 1f;
        }

        var radius = cellWidth * MagnifyRadiusCells;
        var normalized = Math.Clamp(Vector2.Distance(center, hoverPos) / radius, 0f, 1f);
        if (normalized >= 1f)
        {
            return 1f;
        }

        var falloff = 0.5f * (1f + MathF.Cos(MathF.PI * normalized));
        return 1f + MagnifyBoost * falloff * strength;
    }

    private float TapScale(HomeTile tile) => ReferenceEquals(tile, tapTile) ? tapScale : 1f;

    private void BeginTap(HomeTile tile)
    {
        tapTile = tile;
        tapClock = 0f;
        tapHolding = true;
        tapScale = 1f;
    }

    private void ReleaseTap()
    {
        if (tapTile is null || !tapHolding)
        {
            return;
        }

        tapHolding = false;
        tapReleaseFrom = tapScale;
        tapClock = 0f;
    }

    private void CancelTap()
    {
        tapTile = null;
        tapScale = 1f;
    }

    private void ConsumeEditGesture()
    {
        pressActive = false;
        pressTile = null;
        CancelTap();
    }

    private void AdvanceTap(float delta)
    {
        if (tapTile is null)
        {
            tapScale = 1f;
            return;
        }

        var depth = tapTile.IsWidget ? WidgetTapDepth : TapPressDepth;
        tapClock += delta;
        if (tapHolding)
        {
            var progress = Math.Clamp(tapClock / TapPressInSeconds, 0f, 1f);
            tapScale = 1f - depth * Easing.EaseOutCubic(progress);
            return;
        }

        var popProgress = tapClock / TapPopSeconds;
        if (popProgress >= 1f)
        {
            tapScale = 1f;
            tapTile = null;
            return;
        }

        tapScale = tapReleaseFrom + (1f - tapReleaseFrom) * Easing.EaseOutQuint(popProgress);
    }

    private void ReportIconAnchor(HomeTile tile, Vector2 center, float size, in HomeMotion motion)
    {
        if (!UiAnchors.Recording || !motion.Interactive || tile.App is null)
        {
            return;
        }

        var half = size * 0.5f;
        UiAnchors.Report(string.Concat("home.app.", tile.App.Id),
            new Rect(new Vector2(center.X - half, center.Y - half), new Vector2(center.X + half, center.Y + half)));
    }

    private void ReportWidgetAnchor(Rect rect, in HomeMotion motion)
    {
        if (widgetAnchorReported || !UiAnchors.Recording || !motion.Interactive)
        {
            return;
        }

        widgetAnchorReported = true;
        UiAnchors.Report("home.widget", rect);
    }

    private static Rect Expand(Rect rect, float amount) =>
        new(rect.Min - new Vector2(amount, amount), rect.Max + new Vector2(amount, amount));

    private Rect DoneRect(Rect content, in HomeMetrics metrics)
    {
        var width = 64f * metrics.Scale;
        var height = 30f * metrics.Scale;
        var max = new Vector2(content.Max.X - 4f * metrics.Scale, content.Min.Y + height);
        return new Rect(new Vector2(max.X - width, content.Min.Y), max);
    }

    private Rect AddRect(Rect content, in HomeMetrics metrics)
    {
        var size = 30f * metrics.Scale;
        var min = new Vector2(content.Min.X + 4f * metrics.Scale, content.Min.Y);
        return new Rect(min, min + new Vector2(size, size));
    }
}
