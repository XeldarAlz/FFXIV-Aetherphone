using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Shell.Home;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

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
    private readonly Configuration configuration;

    public HomeScreen(IReadOnlyList<IPhoneApp> apps, WidgetRegistry widgets, Configuration configuration)
    {
        this.configuration = configuration;
        layout = new HomeLayoutService(apps, widgets, configuration);
        folder = new FolderOverlay(layout);
        sizeMenu = new WidgetSizeMenu(layout);
        gallery = new WidgetGallery(layout, widgets);
        interaction = new HomeInteractionController(layout, widgets, pager, folder, sizeMenu, gallery, poses);
        renderer = new HomeGridRenderer(layout, pager, poses, interaction);
        chrome = new HomeChrome(pager, interaction);
    }

    public bool Editing => interaction.Editing;

    public HomeLayoutService Layout => layout;

    public void Draw(Rect screen, Rect content, PhoneTheme theme, INavigator navigation, in HomeMotion motion)
    {
        layout.EnsureCurrent();
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        interaction.Advance(delta);
        var editReserve = interaction.Editing && motion.Interactive ? HomeMetrics.EditToolbarBandUnits : 0f;
        var metrics = HomeMetrics.Compute(content, HomeLayoutService.Columns, layout.Rows, ImGuiHelpers.GlobalScale,
            motion, editReserve);
        pager.Step(delta, interaction.DisplayPageCount());
        var chromeAlpha = Math.Clamp(1f - motion.Progress * 1.6f, 0f, 1f);
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
        var labelAlpha = chromeAlpha * (folder.Active ? 0.35f : 1f);
        renderer.DrawPages(metrics, theme, delta, labelAlpha, configuration.ShowAppNames, motion);
        renderer.DrawDock(metrics, theme, delta, chromeAlpha, motion);
        chrome.DrawPageControls(metrics, theme, chromeAlpha, motion.Interactive);
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
    }

    public void PrepareReveal(string appId)
    {
        gallery.CloseImmediate();
        interaction.ResetForReveal();
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
