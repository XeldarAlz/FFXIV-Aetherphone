using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Home;
using Aetherphone.Core.Shell.Home;
using Aetherphone.Core.Shell.Spotlight;
using Aetherphone.Core.Shortcuts;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Core.Shell;

internal sealed class HomeScreen
{
    private readonly HomeLayoutService layout;
    private readonly Pager pager = new();
    private readonly FolderOverlay folder;
    private readonly WidgetSizeMenu sizeMenu;
    private readonly WidgetGallery gallery;
    private readonly TilePoseCache poses = new();
    private readonly HomeInteractionController interaction;
    private readonly HomeGridRenderer renderer;
    private readonly HomeChrome chrome;
    private readonly SpotlightOverlay spotlight;
    private readonly Configuration configuration;

    public HomeScreen(IReadOnlyList<IPhoneApp> apps, WidgetRegistry widgets, ShortcutStore shortcuts,
        ShortcutRunner runner, Configuration configuration, ConfirmService confirm, SpotlightIndex spotlightIndex)
    {
        this.configuration = configuration;
        layout = new HomeLayoutService(apps, widgets, shortcuts, configuration);
        folder = new FolderOverlay(layout, shortcuts, runner, configuration);
        sizeMenu = new WidgetSizeMenu(layout);
        gallery = new WidgetGallery(layout, widgets);
        spotlight = new SpotlightOverlay(spotlightIndex);
        interaction = new HomeInteractionController(layout, widgets, pager, folder, sizeMenu, gallery, spotlight,
            poses, runner);
        renderer = new HomeGridRenderer(layout, pager, poses, interaction, shortcuts, confirm, configuration);
        chrome = new HomeChrome(pager, interaction, spotlight);
    }

    public bool Editing => interaction.Editing;

    public HomeLayoutService Layout => layout;

    public void Draw(Rect screen, Rect content, PhoneTheme theme, INavigator navigation, in HomeMotion motion)
    {
        layout.EnsureCurrent();
        var delta = FrameClock.Delta;
        interaction.Advance(delta);
        var editReserve = interaction.Editing && motion.Interactive ? HomeMetrics.EditToolbarBandUnits : 0f;
        var metrics = HomeMetrics.Compute(content, HomeLayoutService.Columns, layout.Rows, UiScale.Current,
            motion, editReserve);
        pager.Step(delta, interaction.DisplayPageCount());
        var chromeAlpha = 1f - motion.Recession;
        if (motion.Interactive)
        {
            interaction.HandleInput(content, metrics, navigation, delta);
        }
        else
        {
            interaction.Suspend();
        }

        interaction.AdvanceTap(delta);
        interaction.UpdateMagnify(content, motion, delta);
        if (chromeAlpha > 0.01f)
        {
            var labelAlpha = folder.Active ? 0.35f : 1f;
            var tileDrawList = ImGui.GetWindowDrawList();
            var tileVertexStart = tileDrawList.VtxBuffer.Size;
            renderer.DrawPages(metrics, theme, delta, labelAlpha, configuration.ShowAppNames, motion);
            renderer.DrawDock(metrics, theme, delta, 1f, motion);
            chrome.DrawPageControls(metrics, theme, 1f, motion.Interactive);
            LayerCompositor.Fade(tileDrawList, tileVertexStart, chromeAlpha);
        }

        if (interaction.Editing && motion.Interactive)
        {
            chrome.DrawEditChrome(content, metrics, theme);
        }

        var ghostDrawList = ImGui.GetWindowDrawList();
        ghostDrawList.PushClipRect(screen.Min, screen.Max, true);
        renderer.DrawSettleGhost(metrics, theme, delta);
        renderer.DrawDragGhost(metrics, theme, delta);
        ghostDrawList.PopClipRect();
        folder.Draw(content, metrics, theme, navigation, interaction.Editing, pager.Page, delta);
        DrawSizeMenu(content, metrics, theme, delta);
        gallery.Draw(screen, theme, delta, metrics.Scale);
        spotlight.Draw(screen, theme, navigation, delta, metrics.Scale);
        if (!motion.Interactive)
        {
            spotlight.Close();
        }
    }

    public void PrepareReveal(string appId)
    {
        gallery.CloseImmediate();
        spotlight.CloseImmediate();
        interaction.ResetForReveal();
        var page = PageContaining(appId);
        if (page >= 0)
        {
            pager.SnapTo(page, layout.PageCount);
        }
    }

    public Rect? RevealRect(string appId, Rect content, out LaunchOrigin kind)
    {
        kind = LaunchOrigin.Icon;
        var metrics = HomeMetrics.Compute(content, HomeLayoutService.Columns, layout.Rows, UiScale.Current,
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
                var tile = tiles[index];
                if (!TileTargets(tile, appId))
                {
                    continue;
                }

                kind = tile.App is not null ? LaunchOrigin.Icon : LaunchOrigin.Surface;
                return metrics.TileRect(page, pager.Value, cells[index], tile);
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

        for (var index = 0; index < tile.Members.Count; index++)
        {
            if (tile.Members[index].App?.Id == appId)
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

    private void DrawSizeMenu(Rect content, in HomeMetrics metrics, PhoneTheme theme, float delta)
    {
        if (!sizeMenu.Active)
        {
            return;
        }

        var tile = sizeMenu.Tile!;
        var anchor = interaction.CommittedRect(metrics, tile);
        if (anchor is not { } rect)
        {
            sizeMenu.Close();
            return;
        }

        sizeMenu.Draw(content, rect, theme, delta, metrics.Scale);
    }
}
