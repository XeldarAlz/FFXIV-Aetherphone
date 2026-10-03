using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Shortcuts;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Core.Shell.Home;

internal sealed class HomeGridRenderer
{
    private const float StackTargetGrowUnits = 4f;
    private const float StackTargetFillAlpha = 0.14f;

    private readonly HomeLayoutService layout;
    private readonly Pager pager;
    private readonly TilePoseCache poses;
    private readonly HomeInteractionController interaction;
    private readonly ShortcutStore shortcuts;
    private readonly Func<ShortcutEntry, IDalamudTextureWrap?> shortcutIcon;
    private readonly ConfirmService confirm;
    private readonly Configuration configuration;
    private readonly WidgetHost widgetHost;
    private readonly StackPresenter stacks;
    private bool widgetAnchorReported;
    private bool widgetsInteractive;

    public HomeGridRenderer(HomeLayoutService layout, Pager pager, TilePoseCache poses,
        HomeInteractionController interaction, ShortcutStore shortcuts, ConfirmService confirm,
        Configuration configuration, WidgetHost widgetHost)
    {
        this.widgetHost = widgetHost;
        stacks = new StackPresenter(layout, widgetHost);
        this.layout = layout;
        this.pager = pager;
        this.poses = poses;
        this.interaction = interaction;
        this.shortcuts = shortcuts;
        shortcutIcon = shortcuts.Icon;
        this.confirm = confirm;
        this.configuration = configuration;
    }

    public void DrawPages(in HomeMetrics metrics, PhoneTheme theme, float delta, float labelAlpha, bool showLabels,
        in HomeMotion motion)
    {
        widgetAnchorReported = false;
        WidgetHits.BeginFrame(delta);
        widgetsInteractive = labelAlpha >= 1f && interaction.WidgetsInteractive(motion);
        var drawList = ImGui.GetWindowDrawList();
        drawList.PushClipRect(metrics.Content.Min, new Vector2(metrics.Content.Max.X, metrics.DockBar.Min.Y), true);
        var scroll = pager.Value;
        var first = Math.Max(0, (int)MathF.Floor(scroll) - 1);
        var last = Math.Min(interaction.DisplayPageCount() - 1, (int)MathF.Ceiling(scroll) + 1);
        for (var page = first; page <= last; page++)
        {
            DrawPage(metrics, theme, page, delta, labelAlpha, showLabels, motion);
        }

        drawList.PopClipRect();
    }

    private void DrawPage(in HomeMetrics metrics, PhoneTheme theme, int page, float delta, float labelAlpha,
        bool showLabels, in HomeMotion motion)
    {
        DrawDropTarget(metrics, theme, page, labelAlpha);
        if (page >= layout.PageCount)
        {
            return;
        }

        var tiles = layout.Page(page);
        var cells = layout.Placements(page);
        var pageOffset = new Vector2(metrics.PageOffsetX(page, pager.Value), 0f);
        for (var index = 0; index < tiles.Count && index < cells.Count; index++)
        {
            var tile = tiles[index];
            if (ReferenceEquals(tile, interaction.DragTile) || ReferenceEquals(tile, interaction.SettleTile) ||
                Revealing(tile, motion))
            {
                continue;
            }

            var target = metrics.TileRect(page, page, cells[index], tile);
            var local = new Rect(target.Min - metrics.Grid.Min, target.Max - metrics.Grid.Min);
            var rect = poses.Resolve(tile.Key, page, local, metrics.Grid.Min + pageOffset, delta, motion.Interactive);
            DrawTile(metrics, theme, tile, rect, labelAlpha, showLabels, delta, motion,
                ReferenceEquals(tile, interaction.FolderTarget), widgetsInteractive && page == pager.Page);
        }

        DrawStackTarget(metrics, theme, page, cells, tiles, labelAlpha);
        DrawResizeOutline(metrics, theme, page);
    }

    private void DrawStackTarget(in HomeMetrics metrics, PhoneTheme theme, int page, IReadOnlyList<GridCell> cells,
        IReadOnlyList<HomeTile> tiles, float labelAlpha)
    {
        if (interaction.StackCandidate is not { } candidate || interaction.DragPage != page)
        {
            return;
        }

        var index = IndexOf(tiles, candidate);
        if (index < 0 || index >= cells.Count)
        {
            return;
        }

        var progress = interaction.StackProgress;
        var rect = metrics.TileRect(page, pager.Value, cells[index], candidate);
        var grow = StackTargetGrowUnits * metrics.Scale * progress;
        var min = rect.Min - new Vector2(grow, grow);
        var max = rect.Max + new Vector2(grow, grow);
        var radius = WidgetChrome.Radius(metrics.Scale) + grow;
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, min, max, radius,
            ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, StackTargetFillAlpha * progress * labelAlpha)));
        Squircle.Stroke(drawList, min, max, radius,
            ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, (0.35f + 0.6f * progress) * labelAlpha)),
            (1.5f + progress) * metrics.Scale);
    }

    private void DrawResizeOutline(in HomeMetrics metrics, PhoneTheme theme, int page)
    {
        if (interaction.ResizeTile is null || interaction.ResizePage != page)
        {
            return;
        }

        WidgetResizeHandle.Outline(ImGui.GetWindowDrawList(), interaction.ResizeOutline, theme, metrics.Scale);
    }

    private static int IndexOf(IReadOnlyList<HomeTile> tiles, HomeTile tile)
    {
        for (var index = 0; index < tiles.Count; index++)
        {
            if (ReferenceEquals(tiles[index], tile))
            {
                return index;
            }
        }

        return -1;
    }

    private void DrawDropTarget(in HomeMetrics metrics, PhoneTheme theme, int page, float labelAlpha)
    {
        if (!interaction.DropTargetLive || interaction.DragPage != page || labelAlpha <= 0.01f)
        {
            return;
        }

        var tile = interaction.DragTile!;
        var rect = metrics.TileRect(page, pager.Value, interaction.DropCell, tile);
        var pad = tile.IsWidget ? 0f : metrics.IconSize * 0.08f;
        var min = rect.Min - new Vector2(pad, pad);
        var max = rect.Max + new Vector2(pad, pad);
        var rounding = (tile.IsWidget ? WidgetChrome.Radius(metrics.Scale) : rect.Width * 0.28f) + pad;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, 0.10f * labelAlpha)),
            rounding);
        drawList.AddRect(min, max, ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, 0.32f * labelAlpha)),
            rounding, ImDrawFlags.None, 1.5f * metrics.Scale);
    }


    private void DrawTile(in HomeMetrics metrics, PhoneTheme theme, HomeTile tile, Rect rect, float labelAlpha,
        bool showLabels, float delta, in HomeMotion motion, bool highlight, bool interactive)
    {
        var scale = metrics.Scale;
        var zoom = motion.Zoom;
        var jiggle = interaction.Jiggle(tile, scale);
        var center = rect.Center + jiggle;
        rect = new Rect(rect.Min + jiggle, rect.Max + jiggle);
        if (highlight)
        {
            var ring = metrics.IconSize * 0.5f + 4f * scale;
            ImGui.GetWindowDrawList().AddCircle(center, ring,
                ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, 0.8f)), 32, 2f * scale);
        }

        var pointer = interaction.Pointer(tile, center, rect.Width);
        if (tile.IsWidget)
        {
            var pressScale = pointer.Scale;
            var drawRect = pressScale == 1f ? rect : ScaleRect(rect, pressScale);
            var widgetOpacity = Math.Clamp(labelAlpha + 0.35f, 0f, 1f);
            var drawList = ImGui.GetWindowDrawList();
            if (tile.IsStack)
            {
                stacks.Draw(drawList, drawRect, theme, tile, scale, delta, widgetOpacity, interactive, interactive);
            }
            else
            {
                tile.Widget!.Draw(widgetHost.Tile(drawList, drawRect, theme, tile, scale, delta, widgetOpacity,
                    interactive));
            }

            ReportWidgetAnchor(rect, motion);
            if (!interaction.RemoveBadgesLive(motion))
            {
                return;
            }

            if (WidgetResizeHandle.Resizable(tile) && !ReferenceEquals(tile, interaction.ResizeTile))
            {
                WidgetResizeHandle.Draw(drawList, rect, scale, WidgetResizeHandle.Hovered(rect, scale));
            }

            if (HomeTileView.RemoveBadge(new Vector2(rect.Min.X + 4f * scale, rect.Min.Y + 4f * scale), scale, theme))
            {
                layout.RemoveTile(tile);
                interaction.ConsumeEditGesture();
            }

            return;
        }

        if (tile.IsShortcut)
        {
            HomeTileView.DrawShortcut(center, rect.Width, tile.Shortcut!, shortcuts.Icon(tile.Shortcut!), theme,
                pointer.Scale, labelAlpha, showLabels, metrics.CellWidth, zoom, pointer);
            if (interaction.RemoveBadgesLive(motion) &&
                HomeTileView.RemoveBadge(new Vector2(rect.Min.X + 2f * scale, rect.Min.Y + 2f * scale), scale, theme))
            {
                layout.RemoveTile(tile);
                interaction.ConsumeEditGesture();
            }

            return;
        }

        if (tile.IsFolder)
        {
            HomeTileView.DrawFolder(center, rect.Width, tile, theme, pointer.Scale,
                labelAlpha, showLabels, Loc.T(L.Home.NewFolder), metrics.CellWidth, shortcutIcon, configuration,
                zoom, pointer);
            if (interaction.RemoveBadgesLive(motion) &&
                HomeTileView.RemoveBadge(new Vector2(rect.Min.X + 2f * scale, rect.Min.Y + 2f * scale), scale, theme))
            {
                layout.DisbandFolder(tile);
                interaction.ConsumeEditGesture();
            }

            ReportIconAnchor(tile, center, rect.Width, motion);
            return;
        }

        HomeTileView.DrawApp(center, rect.Width, tile.App!, theme, pointer.Scale,
            labelAlpha, showLabels, metrics.CellWidth, configuration, zoom, pointer);
        if (interaction.RemoveBadgesLive(motion) && HomeLayoutService.CanUninstall(tile.App!.Id) &&
            HomeTileView.RemoveBadge(new Vector2(rect.Min.X + 2f * scale, rect.Min.Y + 2f * scale), scale, theme))
        {
            AskUninstall(tile.App!);
            interaction.ConsumeEditGesture();
        }

        ReportIconAnchor(tile, center, rect.Width, motion);
    }

    private void AskUninstall(IPhoneApp app)
    {
        var appId = app.Id;
        confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Home.RemoveConfirm, app.DisplayName),
            ConfirmLabel = Loc.T(L.Home.Remove),
            CancelLabel = Loc.T(L.Common.Cancel),
            Sheet = true,
            Confirm = () => layout.Uninstall(appId),
        });
    }

    private static Rect ScaleRect(Rect rect, float factor)
    {
        var center = rect.Center;
        var half = rect.Size * 0.5f * factor;
        return new Rect(center - half, center + half);
    }

    public void DrawDock(in HomeMetrics metrics, PhoneTheme theme, float delta, float alpha, in HomeMotion motion)
    {
        var dock = layout.Dock;
        var dragTile = interaction.DragTile;
        var showGap = dragTile is not null && interaction.OverDock && interaction.DockAccepts;
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

            var visualSlot = showGap && slot >= interaction.DockInsertIndex ? slot + 1 : slot;
            var target = metrics.DockSlotRect(Math.Max(1, slotCount), visualSlot);
            var local = new Rect(target.Min - metrics.DockBar.Min, target.Max - metrics.DockBar.Min);
            var rect = poses.Resolve(string.Concat("dock:", tile.Key), -1, local,
                metrics.DockBar.Min, delta, motion.Interactive);
            slot++;
            if (ReferenceEquals(tile, interaction.SettleTile) || Revealing(tile, motion))
            {
                continue;
            }

            var jiggle = interaction.Jiggle(tile, metrics.Scale);
            var pointer = interaction.Pointer(tile, rect.Center, rect.Width);
            HomeTileView.DrawApp(rect.Center + jiggle, rect.Width, tile.App!, theme, pointer.Scale, 0f, true, 0f,
                configuration, motion.Zoom, pointer);
            if (!interaction.Editing && dragTile is null)
            {
                HoverTooltip.Show(string.Concat("dock:", tile.Key), rect, tile.App!.DisplayName,
                    HoverLabelSide.Above);
            }

            ReportIconAnchor(tile, rect.Center, rect.Width, motion);
        }
    }

    public void DrawDragGhost(in HomeMetrics metrics, PhoneTheme theme, float delta)
    {
        if (interaction.DragTile is not { } tile)
        {
            return;
        }

        DrawGhost(metrics, theme, tile, interaction.DragPos, interaction.LiftValue, delta);
    }

    public void DrawSettleGhost(in HomeMetrics metrics, PhoneTheme theme, float delta)
    {
        if (interaction.SettleTile is not { } tile)
        {
            return;
        }

        if (!interaction.StepSettle(metrics, delta, out var position, out var liftScale))
        {
            return;
        }

        DrawGhost(metrics, theme, tile, position, liftScale, delta);
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
            Elevation.Floating(drawList, rect.Min, rect.Max, WidgetChrome.Radius(metrics.Scale), metrics.Scale);
            if (tile.IsStack)
            {
                stacks.DrawGhost(drawList, rect, theme, tile, metrics.Scale, delta);
                return;
            }

            tile.Widget!.Draw(widgetHost.Tile(drawList, rect, theme, tile, metrics.Scale, delta, 1f, false));
            return;
        }

        var iconHalf = metrics.IconSize * 0.5f * scale;
        Elevation.Icon(drawList, position - new Vector2(iconHalf), position + new Vector2(iconHalf),
            metrics.IconSize * scale * 0.26f, metrics.IconSize * scale * 0.5f);
        if (tile.IsShortcut)
        {
            HomeTileView.DrawShortcut(position, metrics.IconSize, tile.Shortcut!, shortcuts.Icon(tile.Shortcut!), theme,
                scale, 0f, true, metrics.CellWidth);
            return;
        }

        if (tile.IsFolder)
        {
            HomeTileView.DrawFolder(position, metrics.IconSize, tile, theme, scale, 0f, true, Loc.T(L.Home.NewFolder),
                metrics.CellWidth, shortcutIcon, configuration);
            return;
        }

        HomeTileView.DrawApp(position, metrics.IconSize, tile.App!, theme, scale, 0f, true, metrics.CellWidth,
            configuration);
    }

    private static bool Revealing(HomeTile tile, in HomeMotion motion) =>
        motion.RevealAppId is not null && tile.App is { } app && motion.Reveals(app.Id);

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
}
