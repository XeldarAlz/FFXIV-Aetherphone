using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Shortcuts;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Core.Shell.Home;

internal sealed class HomeInteractionController
{
    private const float LongPressSeconds = 0.42f;
    private const float TapSlop = 6f;
    private const float SwipeThreshold = 12f;
    private const float DragThreshold = 7f;
    private const float EdgeZone = 0.09f;
    private const float EdgeFlipSeconds = 0.45f;
    private const float IconLift = 1.16f;
    private const float WidgetLift = 1.045f;
    private const float TapPressDepth = 1f - Motion.PressScaleControl;
    private const float WidgetTapDepth = 1f - Motion.PressScaleCard;
    private const float PressDim = 0.14f;
    private const float WidgetJiggle = 0.45f;
    private const float StackHoldSeconds = 0.5f;
    private const float StackZoneInset = 0.18f;
    private static readonly WidgetSize[] ResizeSizes = { WidgetSize.Small, WidgetSize.Medium, WidgetSize.Large };

    private sealed class PointerEntry
    {
        public HomeTile Tile = null!;
        public Spring Spring;
    }

    private readonly HomeLayoutService layout;
    private readonly WidgetHost widgetHost;
    private readonly Pager pager;
    private readonly FolderOverlay folder;
    private readonly WidgetContextMenu widgetMenu;
    private readonly WidgetGallery gallery;
    private readonly Spotlight.SpotlightOverlay spotlight;
    private readonly TilePoseCache poses;
    private readonly ShortcutRunner runner;

    private bool pressActive;
    private Vector2 pressPos;
    private float pressTime;
    private HomeTile? pressTile;
    private bool pressFromDock;
    private bool pressOnControl;
    private WidgetHit pressHit;

    private HomeTile? tapTile;
    private bool tapHolding;
    private Spring tapSpring;

    private readonly List<PointerEntry> pointerEntries = new();
    private readonly Stack<PointerEntry> pointerPool = new();
    private HomeTile? hoverTile;
    private Vector2 hoverPointer;

    private bool editing;
    private float editClock;

    private HomeTile? dragTile;
    private bool dragFromDock;
    private int dragPage;
    private Vector2 dragPos;
    private Vector2 grabOffset;
    private Spring lift;
    private float edgeDwell;
    private GridCell dropCell;
    private bool dropValid;
    private bool overDock;
    private bool dockAccepts;
    private int dockInsertIndex;
    private HomeTile? folderTarget;
    private HomeTile? stackCandidate;
    private float stackDwell;

    private HomeTile? resizeTile;
    private int resizePage;
    private WidgetSize resizeSize;
    private Rect resizeOutline;

    private HomeTile? settleTile;
    private Spring settleX;
    private Spring settleY;

    public HomeInteractionController(HomeLayoutService layout, Pager pager, FolderOverlay folder,
        WidgetContextMenu widgetMenu, WidgetGallery gallery, Spotlight.SpotlightOverlay spotlight, TilePoseCache poses,
        ShortcutRunner runner, WidgetHost widgetHost)
    {
        this.layout = layout;
        this.widgetHost = widgetHost;
        this.pager = pager;
        this.folder = folder;
        this.widgetMenu = widgetMenu;
        this.gallery = gallery;
        this.spotlight = spotlight;
        this.poses = poses;
        this.runner = runner;
    }

    public bool Editing => editing;
    public HomeTile? DragTile => dragTile;
    public int DragPage => dragPage;
    public GridCell DropCell => dropCell;
    public bool DropTargetLive =>
        dragTile is not null && dropValid && !overDock && folderTarget is null && stackCandidate is null;
    public HomeTile? StackCandidate => stackCandidate;
    public float StackProgress => stackCandidate is null ? 0f : Math.Clamp(stackDwell / StackHoldSeconds, 0f, 1f);
    public HomeTile? ResizeTile => resizeTile;
    public Rect ResizeOutline => resizeOutline;
    public int ResizePage => resizePage;
    public HomeTile? SettleTile => settleTile;
    public HomeTile? FolderTarget => folderTarget;
    public bool OverDock => overDock;
    public bool DockAccepts => dockAccepts;
    public int DockInsertIndex => dockInsertIndex;
    public Vector2 DragPos => dragPos;
    public float LiftValue => lift.Value;

    public void Advance(float delta) => editClock += delta;

    public int DisplayPageCount() => dragTile is null ? layout.PageCount : layout.PageCount + 1;

    public void Suspend()
    {
        pressActive = false;
        CancelTap();
    }

    public void ResetForReveal()
    {
        pressActive = false;
        editing = false;
        resizeTile = null;
        stackCandidate = null;
        CancelTap();
        if (dragTile is not null)
        {
            dragTile = null;
        }
    }

    public bool WidgetsInteractive(in HomeMotion motion) =>
        motion.Interactive && !editing && dragTile is null && settleTile is null && resizeTile is null &&
        !folder.Active &&
        !gallery.Active && !widgetMenu.Active && !spotlight.Active && !pager.Dragging &&
        MathF.Abs(pager.Value - pager.Page) < 0.001f;

    public void HandleInput(Rect content, in HomeMetrics metrics, INavigator navigation, PhoneTheme theme,
        float delta)
    {
        if (gallery.Active || folder.Active || widgetMenu.Active || spotlight.Active)
        {
            if (pressActive)
            {
                pressActive = false;
                CancelTap();
            }

            return;
        }

        if (resizeTile is not null)
        {
            HandleResize(metrics);
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

        HandlePress(content, metrics, navigation, theme, delta);
    }

    private void HandlePress(Rect content, in HomeMetrics metrics, INavigator navigation, PhoneTheme theme,
        float delta)
    {
        var mouse = ImGui.GetMousePos();
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && UiInteract.Hover(content.Min, content.Max))
        {
            if (editing && HandleEditChromeClick(content, metrics, mouse))
            {
                return;
            }

            if (editing && TryBeginResize(metrics, mouse))
            {
                return;
            }

            pressActive = true;
            pressPos = mouse;
            pressTime = 0f;
            pressTile = TileAt(metrics, mouse, out pressFromDock);
            pressOnControl = !editing && pressTile is { IsWidget: true } && WidgetHits.TryFind(mouse, out pressHit);
            if (pressOnControl)
            {
                WidgetHits.Press(pressHit.Id);
            }
            else if (pressTile is not null)
            {
                BeginTap(pressTile);
            }
        }

        if (pressActive && ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            pressTime += delta;
            var move = mouse - pressPos;
            if (pressOnControl && move.Length() >= TapSlop * metrics.Scale)
            {
                CancelControl();
            }

            var canSwipe = !pressFromDock && !(editing && pressTile is not null);
            if (canSwipe && MathF.Abs(move.X) > SwipeThreshold * metrics.Scale &&
                MathF.Abs(move.X) > MathF.Abs(move.Y) * 1.2f)
            {
                pager.Begin(pressPos.X);
                pressActive = false;
                CancelTap();
                return;
            }

            if (!editing && pressTile is null && move.Y > SwipeThreshold * metrics.Scale &&
                move.Y > MathF.Abs(move.X) * 1.2f)
            {
                pressActive = false;
                CancelTap();
                spotlight.Open();
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
                HandleTap(metrics, navigation, theme, mouse);
            }

            CancelControl();
            pressActive = false;
        }
    }

    public bool RemoveBadgesLive(in HomeMotion motion) =>
        editing && motion.Interactive && !gallery.Active && !widgetMenu.Active && !folder.Active;

    private bool HandleEditChromeClick(Rect content, in HomeMetrics metrics, Vector2 mouse)
    {
        if (HomeChrome.DoneRect(content, metrics).Contains(mouse))
        {
            editing = false;
            pressActive = false;
            return true;
        }

        if (HomeChrome.AddRect(content, metrics).Contains(mouse))
        {
            gallery.Open(pager.Page);
            pressActive = false;
            return true;
        }

        return false;
    }

    private bool TryBeginResize(in HomeMetrics metrics, Vector2 mouse)
    {
        var page = pager.Page;
        if (page < 0 || page >= layout.PageCount)
        {
            return false;
        }

        var tiles = layout.Page(page);
        for (var index = 0; index < tiles.Count; index++)
        {
            var tile = tiles[index];
            if (!tile.IsWidget || !WidgetResizeHandle.Resizable(tile) || CommittedRect(metrics, tile) is not { } rect ||
                !WidgetResizeHandle.Hit(rect, mouse, metrics.Scale))
            {
                continue;
            }

            resizeTile = tile;
            resizePage = page;
            resizeSize = tile.Size;
            resizeOutline = rect;
            pressActive = false;
            CancelTap();
            return true;
        }

        return false;
    }

    private void HandleResize(in HomeMetrics metrics)
    {
        var tile = resizeTile!;
        var mouse = ImGui.GetMousePos();
        var bestDistance = float.MaxValue;
        for (var index = 0; index < ResizeSizes.Length; index++)
        {
            var size = ResizeSizes[index];
            var cell = tile.Cell;
            if (size != tile.Size && !layout.TryFitSize(tile, size, out cell))
            {
                continue;
            }

            var rect = metrics.WidgetRect(resizePage, pager.Value, cell, WidgetSizes.ColumnSpan(size),
                WidgetSizes.RowSpan(size));
            var distance = Vector2.DistanceSquared(rect.Max, mouse);
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            resizeSize = size;
            resizeOutline = rect;
        }

        if (ImGui.IsMouseDown(ImGuiMouseButton.Left) && !ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            return;
        }

        layout.TryResizeInPlace(tile, resizeSize);
        resizeTile = null;
    }

    private void HandleTap(in HomeMetrics metrics, INavigator navigation, PhoneTheme theme, Vector2 mouse)
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
                widgetMenu.Open(pressTile);
                return;
            }

            if (pressOnControl)
            {
                ActivateControl(rect, mouse);
                return;
            }

            widgetHost.OpenTarget(pressTile, rect, theme, metrics.Scale);
            return;
        }

        if (pressTile.IsShortcut)
        {
            if (!editing)
            {
                runner.Run(pressTile.Shortcut!);
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
            navigation.OpenAppFrom(pressTile.App!, DrawnRect(rect, pressTile, metrics), LaunchOrigin.Icon);
        }
    }

    private void ActivateControl(Rect tileRect, Vector2 mouse)
    {
        if (!pressHit.Rect.Contains(mouse))
        {
            return;
        }

        if (pressHit.Kind == WidgetHitKind.Link)
        {
            widgetHost.Actions.Open(pressHit.Route, tileRect);
            return;
        }

        WidgetHits.Fire(pressHit.Id);
    }

    private void CancelControl()
    {
        if (!pressOnControl)
        {
            return;
        }

        pressOnControl = false;
        WidgetHits.Cancel();
    }

    private Rect DrawnRect(Rect rect, HomeTile tile, in HomeMetrics metrics)
    {
        var factor = Pointer(tile, rect.Center, rect.Width).Scale;
        var half = rect.Size * 0.5f * factor;
        return new Rect(rect.Center - half, rect.Center + half);
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
        var center = CommittedRect(metrics, tile)?.Center ?? mouse;
        grabOffset = mouse - center;
        dragPos = center;
        lift.SnapTo(1f);
        edgeDwell = 0f;
        folderTarget = null;
        stackCandidate = null;
        stackDwell = 0f;
        overDock = false;
        dropValid = false;
        pressActive = false;
        editing = true;
        settleTile = null;
        CancelTap();
        poses.Forget(tile.Key);
        poses.Forget(string.Concat("dock:", tile.Key));
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
        lift.Step(dragTile!.IsWidget ? WidgetLift : IconLift, Motion.HoverLift, delta);
        overDock = dragTile.App is not null && Expand(metrics.DockBar, 8f * metrics.Scale).Contains(mouse);
        if (overDock)
        {
            folderTarget = null;
            stackCandidate = null;
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
        else if (mouse.X > content.Max.X - edge && dragPage < DisplayPageCount() - 1)
        {
            edgeDwell += delta;
            if (edgeDwell > EdgeFlipSeconds)
            {
                dragPage++;
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
            stackCandidate = null;
            dropValid = layout.TryResolveDrop(dragPage, dragTile!, HoveredCell(metrics), out dropCell);
            return;
        }

        var tiles = layout.Page(dragPage);
        var cells = layout.Placements(dragPage);
        if (dragTile!.IsWidget)
        {
            TrackStackCandidate(metrics, tiles, cells, mouse, delta);
        }
        else
        {
            var radius = metrics.IconSize * 0.42f;
            for (var index = 0; index < tiles.Count && index < cells.Count; index++)
            {
                var candidate = tiles[index];
                if (ReferenceEquals(candidate, dragTile) || candidate.IsWidget
                    || candidate.IsFolder && dragTile.IsFolder)
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

        dropValid = layout.TryResolveDrop(dragPage, dragTile, HoveredCell(metrics), out dropCell);
    }

    private void TrackStackCandidate(in HomeMetrics metrics, IReadOnlyList<HomeTile> tiles,
        IReadOnlyList<GridCell> cells, Vector2 mouse, float delta)
    {
        HomeTile? candidate = null;
        for (var index = 0; index < tiles.Count && index < cells.Count; index++)
        {
            var tile = tiles[index];
            if (!tile.IsWidget || !layout.CanStack(tile, dragTile!))
            {
                continue;
            }

            var rect = metrics.TileRect(dragPage, pager.Value, cells[index], tile);
            var inset = MathF.Min(rect.Width, rect.Height) * StackZoneInset;
            if (rect.Inset(inset).Contains(mouse))
            {
                candidate = tile;
                break;
            }
        }

        if (!ReferenceEquals(candidate, stackCandidate))
        {
            stackCandidate = candidate;
            stackDwell = 0f;
            return;
        }

        if (candidate is not null)
        {
            stackDwell += delta;
        }
    }

    private GridCell HoveredCell(in HomeMetrics metrics)
    {
        var anchor = dragPos - new Vector2(metrics.CellWidth * (dragTile!.ColumnSpan - 1) * 0.5f,
            metrics.CellHeight * (dragTile.RowSpan - 1) * 0.5f);
        var cell = metrics.CellFromPoint(dragPage, pager.Value, anchor);
        return new GridCell(Math.Min(cell.Column, HomeLayoutService.Columns - dragTile.ColumnSpan),
            Math.Min(cell.Row, layout.Rows - dragTile.RowSpan));
    }

    private void CommitDrag(in HomeMetrics metrics)
    {
        var tile = dragTile!;
        if (stackCandidate is not null && StackProgress >= 1f)
        {
            BeginSettle(layout.MakeStack(stackCandidate, tile) ?? tile, metrics);
        }
        else if (folderTarget is not null)
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
            if (dropValid)
            {
                layout.MoveTile(tile, dragPage, dropCell);
            }

            BeginSettle(tile, metrics);
        }
        else
        {
            BeginSettle(tile, metrics);
        }

        dragTile = null;
        folderTarget = null;
        stackCandidate = null;
        stackDwell = 0f;
        overDock = false;
        dropValid = false;
        pager.AnimateTo(pager.Page, layout.PageCount);
    }

    private void BeginSettle(HomeTile tile, in HomeMetrics metrics)
    {
        settleTile = tile;
        settleX.SnapTo(dragPos.X - metrics.Content.Min.X);
        settleY.SnapTo(dragPos.Y - metrics.Content.Min.Y);
    }

    public void SettleFrom(HomeTile tile, Vector2 center, float startScale, in HomeMetrics metrics)
    {
        settleTile = tile;
        settleX.SnapTo(center.X - metrics.Content.Min.X);
        settleY.SnapTo(center.Y - metrics.Content.Min.Y);
        lift.SnapTo(startScale);
        var (page, _) = layout.Locate(tile);
        if (page >= 0)
        {
            pager.AnimateTo(page, layout.PageCount);
        }
    }

    public bool StepSettle(in HomeMetrics metrics, float delta, out Vector2 position, out float liftScale)
    {
        position = default;
        liftScale = 1f;
        if (settleTile is null)
        {
            return false;
        }

        var target = CommittedRect(metrics, settleTile);
        if (target is not { } rect)
        {
            settleTile = null;
            return false;
        }

        settleX.Step(rect.Center.X - metrics.Content.Min.X, Motion.PageSettle, delta);
        settleY.Step(rect.Center.Y - metrics.Content.Min.Y, Motion.PageSettle, delta);
        lift.Step(1f, Motion.PageSettle, delta);
        position = metrics.Content.Min + new Vector2(settleX.Value, settleY.Value);
        if (Vector2.Distance(position, rect.Center) < 0.8f && MathF.Abs(lift.Value - 1f) < 0.01f)
        {
            settleTile = null;
            return false;
        }

        liftScale = lift.Value;
        return true;
    }

    public Rect? CommittedRect(in HomeMetrics metrics, HomeTile tile)
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

    public Vector2 Jiggle(HomeTile tile, float scale)
    {
        if (!editing || ReferenceEquals(tile, dragTile))
        {
            return Vector2.Zero;
        }

        var offset = JiggleOffset(tile.Key.GetHashCode(), editClock, scale);
        return tile.IsWidget ? offset * WidgetJiggle : offset;
    }

    public static Vector2 JiggleOffset(int seed, float clock, float scale)
    {
        var phase = (seed & 0x3ff) / 1023f * MathF.PI * 2f;
        var x = MathF.Sin(clock * 11f + phase);
        var y = MathF.Cos(clock * 13f + phase * 1.3f);
        return new Vector2(x, y) * 1.1f * scale;
    }

    public void UpdatePointer(Rect content, in HomeMetrics metrics, in HomeMotion motion, float delta)
    {
        hoverPointer = ImGui.GetMousePos();
        var active = motion.Interactive && !editing && dragTile is null && settleTile is null &&
                     !folder.Active && !gallery.Active && !widgetMenu.Active && !spotlight.Active &&
                     !pager.Dragging && UiInteract.Hover(content.Min, content.Max);
        hoverTile = active ? TileAt(metrics, hoverPointer, out _) : null;
        if (hoverTile is not null && FindPointer(hoverTile) is null)
        {
            var entry = pointerPool.Count > 0 ? pointerPool.Pop() : new PointerEntry();
            entry.Tile = hoverTile;
            entry.Spring.SnapTo(0f);
            pointerEntries.Add(entry);
        }

        for (var index = pointerEntries.Count - 1; index >= 0; index--)
        {
            var entry = pointerEntries[index];
            var target = ReferenceEquals(entry.Tile, hoverTile) ? 1f : 0f;
            entry.Spring.Step(target, Motion.HoverLift, delta);
            if (target > 0f || entry.Spring.Value > 0.005f)
            {
                continue;
            }

            pointerEntries.RemoveAt(index);
            entry.Tile = null!;
            pointerPool.Push(entry);
        }
    }

    private PointerEntry? FindPointer(HomeTile tile)
    {
        for (var index = 0; index < pointerEntries.Count; index++)
        {
            if (ReferenceEquals(pointerEntries[index].Tile, tile))
            {
                return pointerEntries[index];
            }
        }

        return null;
    }

    public PointerState Pointer(HomeTile tile, Vector2 center, float size)
    {
        var tap = TapScale(tile);
        var dim = TapDim(tile);
        var lift = FindPointer(tile)?.Spring.Value ?? 0f;
        if (lift <= 0.001f)
        {
            return new PointerState(tap, 0f, Vector2.Zero, dim);
        }

        var half = MathF.Max(size * 0.5f, 1f);
        var tilt = (hoverPointer - center) / half;
        tilt = new Vector2(Math.Clamp(tilt.X, -1f, 1f), Math.Clamp(tilt.Y, -1f, 1f)) * lift;
        var gain = tile.IsWidget ? Motion.HoverLiftCard : Motion.HoverLiftIcon;
        return new PointerState(tap * (1f + gain * lift), lift, tilt, dim);
    }

    public float TapScale(HomeTile tile)
    {
        if (!ReferenceEquals(tile, tapTile))
        {
            return 1f;
        }

        var depth = tile.IsWidget ? WidgetTapDepth : TapPressDepth;
        return 1f - depth * tapSpring.Value;
    }

    private float TapDim(HomeTile tile) =>
        ReferenceEquals(tile, tapTile) && !tile.IsWidget ? PressDim * tapSpring.Value : 0f;

    private void BeginTap(HomeTile tile)
    {
        tapTile = tile;
        tapHolding = true;
        tapSpring.SnapTo(0f);
    }

    private void ReleaseTap()
    {
        if (tapTile is null || !tapHolding)
        {
            return;
        }

        tapHolding = false;
    }

    public void CancelTap()
    {
        CancelControl();
        tapTile = null;
        tapHolding = false;
        tapSpring.SnapTo(0f);
    }

    public void CancelPress()
    {
        pressActive = false;
    }

    public void ConsumeEditGesture()
    {
        pressActive = false;
        pressTile = null;
        CancelTap();
    }

    public void AdvanceTap(float delta)
    {
        if (tapTile is null)
        {
            tapSpring.SnapTo(0f);
            return;
        }

        tapSpring.Step(tapHolding ? 1f : 0f, tapHolding ? Motion.PressIn : Motion.Release, delta);
        if (!tapHolding && tapSpring.Value < 0.01f)
        {
            tapTile = null;
            tapSpring.SnapTo(0f);
        }
    }

    private static Rect Expand(Rect rect, float amount) =>
        new(rect.Min - new Vector2(amount, amount), rect.Max + new Vector2(amount, amount));
}
