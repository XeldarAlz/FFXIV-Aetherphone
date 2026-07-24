using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Core.Shell.Home;

internal sealed class HomeInteractionController
{
    private const float LongPressSeconds = 0.42f;
    private const float TapSlop = 6f;
    private const float SwipeThreshold = 12f;
    private const float DragThreshold = 7f;
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

    private readonly HomeLayoutService layout;
    private readonly WidgetRegistry widgets;
    private readonly Pager pager;
    private readonly FolderOverlay folder;
    private readonly WidgetSizeMenu sizeMenu;
    private readonly WidgetGallery gallery;
    private readonly TilePoseCache poses;

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

    private HomeTile? settleTile;
    private Spring settleX;
    private Spring settleY;

    public HomeInteractionController(HomeLayoutService layout, WidgetRegistry widgets, Pager pager,
        FolderOverlay folder, WidgetSizeMenu sizeMenu, WidgetGallery gallery, TilePoseCache poses)
    {
        this.layout = layout;
        this.widgets = widgets;
        this.pager = pager;
        this.folder = folder;
        this.sizeMenu = sizeMenu;
        this.gallery = gallery;
        this.poses = poses;
    }

    public bool Editing => editing;
    public HomeTile? DragTile => dragTile;
    public int DragPage => dragPage;
    public GridCell DropCell => dropCell;
    public bool DropTargetLive => dragTile is not null && dropValid && !overDock && folderTarget is null;
    public HomeTile? SettleTile => settleTile;
    public HomeTile? FolderTarget => folderTarget;
    public bool OverDock => overDock;
    public bool DockAccepts => dockAccepts;
    public int DockInsertIndex => dockInsertIndex;
    public Vector2 DragPos => dragPos;
    public float LiftValue => lift.Value;

    public void Advance(float delta) => editClock += delta;

    public int DisplayPageCount() => layout.TotalPageCount;

    public bool CanAddWidget => pager.Page < layout.HomePageCount;

    public void Suspend()
    {
        pressActive = false;
        CancelTap();
    }

    public void ResetForReveal()
    {
        pressActive = false;
        editing = false;
        CancelTap();
        if (dragTile is not null)
        {
            dragTile = null;
        }
    }

    public void HandleInput(Rect content, in HomeMetrics metrics, INavigator navigation, float delta)
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

    public bool RemoveBadgesLive(in HomeMotion motion) =>
        editing && motion.Interactive && !gallery.Active && !sizeMenu.Active && !folder.Active;

    private bool HandleEditChromeClick(Rect content, in HomeMetrics metrics, Vector2 mouse)
    {
        if (HomeChrome.DoneRect(content, metrics).Contains(mouse))
        {
            editing = false;
            pressActive = false;
            return true;
        }

        if (CanAddWidget && HomeChrome.AddRect(content, metrics).Contains(mouse))
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
                navigation.OpenAppFrom(app, rect);
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
            navigation.OpenAppFrom(pressTile.App!, rect);
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

        var page = Math.Clamp((int)MathF.Round(pager.Value), 0, layout.TotalPageCount - 1);
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
        else if (mouse.X > content.Max.X - edge && dragPage < layout.TotalPageCount - 1)
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
        if (dragPage >= layout.TotalPageCount)
        {
            dropValid = false;
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

        dropValid = layout.TryResolveDrop(dragPage, dragTile, HoveredCell(metrics), out dropCell);
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
        overDock = false;
        dropValid = false;
        pager.AnimateTo(pager.Page, layout.TotalPageCount);
    }

    private void BeginSettle(HomeTile tile, in HomeMetrics metrics)
    {
        settleTile = tile;
        settleX.SnapTo(dragPos.X - metrics.Content.Min.X);
        settleY.SnapTo(dragPos.Y - metrics.Content.Min.Y);
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

        settleX.Step(rect.Center.X - metrics.Content.Min.X, SettleSmoothTime, delta);
        settleY.Step(rect.Center.Y - metrics.Content.Min.Y, SettleSmoothTime, delta);
        lift.Step(1f, SettleSmoothTime, delta);
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

        var phase = (tile.Key.GetHashCode() & 0x3ff) / 1023f * MathF.PI * 2f;
        var x = MathF.Sin(editClock * 11f + phase);
        var y = MathF.Cos(editClock * 13f + phase * 1.3f);
        return new Vector2(x, y) * 1.1f * scale;
    }

    public void UpdateMagnify(Rect content, in HomeMotion motion, float delta)
    {
        hoverPos = ImGui.GetMousePos();
        var active = motion.Interactive && !editing && dragTile is null && settleTile is null &&
                     !folder.Active && !gallery.Active && !sizeMenu.Active && !pager.Dragging &&
                     content.Contains(hoverPos);
        magnifyGate.Step(active ? 1f : 0f, MagnifyFadeTime, delta);
    }

    public float Magnify(Vector2 center, float cellWidth)
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

    public float TapScale(HomeTile tile) => ReferenceEquals(tile, tapTile) ? tapScale : 1f;

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

    public void CancelTap()
    {
        tapTile = null;
        tapScale = 1f;
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

    private static Rect Expand(Rect rect, float amount) =>
        new(rect.Min - new Vector2(amount, amount), rect.Max + new Vector2(amount, amount));
}
