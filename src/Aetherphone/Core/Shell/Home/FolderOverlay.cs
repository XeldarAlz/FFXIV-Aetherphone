using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Shortcuts;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Core.Shell.Home;

internal sealed class FolderOverlay
{
    private const float VeilDim = 0.35f;
    private const float PanelRadiusUnits = 22f;
    private const float PanelWidthFraction = 0.84f;
    private const float PanelMaxHeightFraction = 0.82f;
    private const float PanelPadUnits = 18f;
    private const float NameTopUnits = 12f;
    private const float SwatchRowHeightUnits = 34f;
    private const float IconCellFraction = 0.62f;
    private const float IconMaxUnits = 52f;
    private const float CellLabelBandUnits = 26f;
    private const float TintAlpha = 0.18f;
    private const float MiniGridFadeEnd = 0.5f;
    private const float ContentFadeStart = 0.3f;
    private const float InteractiveThreshold = 0.85f;
    private const float WheelStepUnits = 40f;
    private const int NameMaxLength = 64;
    private static readonly Vector4 NoTintSwatch = new(0.55f, 0.56f, 0.60f, 1f);

    private readonly HomeLayoutService layout;
    private readonly ShortcutStore shortcuts;
    private readonly ShortcutRunner runner;
    private readonly Configuration configuration;
    private readonly Func<ShortcutEntry, IDalamudTextureWrap?> shortcutIcon;
    private HomeTile? folder;
    private bool closing;
    private Spring anim;
    private Rect origin;
    private string nameBuffer = string.Empty;
    private float scrollY;
    private int openedFrame;

    public FolderOverlay(HomeLayoutService layout, ShortcutStore shortcuts, ShortcutRunner runner,
        Configuration configuration)
    {
        this.layout = layout;
        this.shortcuts = shortcuts;
        this.runner = runner;
        this.configuration = configuration;
        shortcutIcon = shortcuts.Icon;
    }

    public bool Active => folder is not null;
    public HomeTile? Folder => folder;

    public void Open(HomeTile tile, Rect originRect)
    {
        folder = tile;
        closing = false;
        anim.SnapTo(0f);
        origin = originRect;
        nameBuffer = tile.FolderName;
        scrollY = 0f;
        openedFrame = ImGui.GetFrameCount();
    }

    public void RequestClose()
    {
        ApplyRename();
        closing = true;
    }

    public void Draw(Rect screen, Rect content, in HomeMetrics metrics, PhoneTheme theme, INavigator navigation,
        bool editing, int currentPage, float delta)
    {
        if (folder is null)
        {
            return;
        }

        if (!closing && layout.Locate(folder).Page < 0)
        {
            folder = null;
            return;
        }

        anim.Step(closing ? 0f : 1f, Motion.Sheet, delta);
        if (closing && anim.Value < 0.02f)
        {
            folder = null;
            closing = false;
            return;
        }

        var current = folder;
        var scale = metrics.Scale;
        var progress = Math.Clamp(anim.Value, 0f, 1f);
        var drawList = ImGui.GetWindowDrawList();
        drawList.PushClipRect(screen.Min, screen.Max, true);
        Material.Veil(drawList, screen.Min, screen.Max, VeilDim * progress);
        var columns = current.Members.Count <= 9 ? 3 : 4;
        var rows = (current.Members.Count + columns - 1) / columns;
        var panelWidth = content.Width * PanelWidthFraction;
        var pad = PanelPadUnits * scale;
        var cellWidth = (panelWidth - pad * 2f) / columns;
        var iconSize = MathF.Min(cellWidth * IconCellFraction, IconMaxUnits * scale);
        var cellHeight = iconSize + CellLabelBandUnits * scale;
        var headerHeight = (NameTopUnits + GlassField.HeightUnits + SwatchRowHeightUnits) * scale;
        var panelHeight = MathF.Min(headerHeight + rows * cellHeight + pad, content.Height * PanelMaxHeightFraction);
        var targetMin = new Vector2(content.Center.X - panelWidth * 0.5f, content.Center.Y - panelHeight * 0.5f);
        var target = new Rect(targetMin, targetMin + new Vector2(panelWidth, panelHeight));
        var panel = new Rect(Vector2.Lerp(origin.Min, target.Min, progress),
            Vector2.Lerp(origin.Max, target.Max, progress));
        var radius = Easing.Lerp(origin.Width * Metrics.Radius.HomeTileFactor, PanelRadiusUnits * scale, progress);
        Material.LiquidGlass(drawList, panel.Min, panel.Max, radius, scale, GlassTone.Dark, 0f, progress);
        if (!string.IsNullOrEmpty(current.FolderTint))
        {
            Squircle.Fill(drawList, panel.Min, panel.Max, radius,
                ImGui.GetColorU32(Palette.WithAlpha(ThemeCatalog.ResolveAccent(current.FolderTint),
                    TintAlpha * progress)));
        }

        DrawMiniGrid(drawList, panel, current, scale, progress);
        var contentAlpha = Easing.Segment(progress, ContentFadeStart, 1f);
        var interactive = !closing && progress > InteractiveThreshold;
        if (contentAlpha > 0.01f)
        {
            var vertexStart = drawList.VtxBuffer.Size;
            DrawContents(panel, metrics, theme, navigation, current, editing, currentPage, columns, pad, iconSize,
                cellWidth, cellHeight, headerHeight, interactive);
            LayerCompositor.Fade(drawList, vertexStart, contentAlpha);
        }

        if (interactive && ImGui.GetFrameCount() != openedFrame &&
            UiInteract.ClickedOutside(panel.Min, panel.Max, false))
        {
            RequestClose();
        }

        drawList.PopClipRect();
    }

    private void DrawMiniGrid(ImDrawListPtr drawList, Rect panel, HomeTile current, float scale, float progress)
    {
        var miniAlpha = 1f - Easing.Segment(progress, 0f, MiniGridFadeEnd);
        if (miniAlpha <= 0.01f)
        {
            return;
        }

        var side = MathF.Min(panel.Width, panel.Height);
        var half = new Vector2(side * 0.5f, side * 0.5f);
        var vertexStart = drawList.VtxBuffer.Size;
        HomeTileView.DrawFolderMiniGrid(drawList, panel.Center - half, panel.Center + half, current, shortcutIcon,
            scale);
        LayerCompositor.Fade(drawList, vertexStart, miniAlpha);
    }

    private void DrawContents(Rect panel, in HomeMetrics metrics, PhoneTheme theme, INavigator navigation,
        HomeTile current, bool editing, int currentPage, int columns, float pad, float iconSize, float cellWidth,
        float cellHeight, float headerHeight, bool interactive)
    {
        var scale = metrics.Scale;
        var drawList = ImGui.GetWindowDrawList();
        var nameTop = panel.Min.Y + NameTopUnits * scale;
        var nameField = new Rect(new Vector2(panel.Min.X + pad, nameTop),
            new Vector2(panel.Max.X - pad, nameTop + GlassField.HeightUnits * scale));
        GlassField.Surface(drawList, nameField, GlassField.Radius(nameField), scale,
            WallpaperLegibility.Strength(theme), 1f);
        if (GlassField.Text(nameField, "##folderName", Loc.T(L.Home.NewFolder), ref nameBuffer, theme, scale,
                NameMaxLength, false, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            ApplyRename();
        }

        DrawTintRow(drawList, panel, current, nameField.Max.Y, pad, scale, interactive);

        var gridTop = panel.Min.Y + headerHeight;
        var gridView = new Rect(new Vector2(panel.Min.X, gridTop), panel.Max);
        drawList.PushClipRect(gridView.Min, gridView.Max, true);
        if (interactive && UiInteract.Hover(gridView.Min, gridView.Max))
        {
            scrollY -= ImGui.GetIO().MouseWheel * WheelStepUnits * scale;
        }

        var rows = (current.Members.Count + columns - 1) / columns;
        var contentHeight = rows * cellHeight;
        scrollY = Math.Clamp(scrollY, 0f, MathF.Max(0f, contentHeight - gridView.Height));

        for (var index = 0; index < current.Members.Count; index++)
        {
            var column = index % columns;
            var row = index / columns;
            var center = new Vector2(panel.Min.X + pad + (column + 0.5f) * cellWidth,
                gridTop - scrollY + (row + 0.5f) * cellHeight);
            if (center.Y + cellHeight * 0.5f < gridView.Min.Y || center.Y - cellHeight * 0.5f > gridView.Max.Y)
            {
                continue;
            }

            var member = current.Members[index];
            if (member.IsShortcut)
            {
                HomeTileView.DrawShortcut(center, iconSize, member.Shortcut!, shortcuts.Icon(member.Shortcut!), theme,
                    1f, 1f, true, cellWidth);
            }
            else
            {
                HomeTileView.DrawApp(center, iconSize, member.App!, theme, 1f, 1f, true, cellWidth, configuration);
            }

            if (!interactive)
            {
                continue;
            }

            var half = iconSize * 0.5f;
            var iconRect = new Rect(new Vector2(center.X - half, center.Y - half),
                new Vector2(center.X + half, center.Y + half));
            if (editing)
            {
                if (HomeTileView.RemoveBadge(new Vector2(center.X - half + 2f * scale, center.Y - half + 2f * scale),
                        scale, theme))
                {
                    layout.RemoveFromFolder(current, member, currentPage);
                    if (current.Members.Count <= 1)
                    {
                        RequestClose();
                    }

                    drawList.PopClipRect();
                    return;
                }
            }
            else if (UiInteract.Hover(iconRect.Min, iconRect.Max))
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    RequestClose();
                    if (member.IsShortcut)
                    {
                        runner.Run(member.Shortcut!);
                    }
                    else
                    {
                        navigation.OpenAppFrom(member.App!, iconRect, LaunchOrigin.Icon);
                    }

                    drawList.PopClipRect();
                    return;
                }
            }
        }

        drawList.PopClipRect();
    }

    private void DrawTintRow(ImDrawListPtr drawList, Rect panel, HomeTile current, float rowTop, float pad,
        float scale, bool interactive)
    {
        var rowY = rowTop + SwatchRowHeightUnits * scale * 0.5f;
        var innerLeft = panel.Min.X + pad;
        var innerRight = panel.Max.X - pad;
        var accents = ThemeCatalog.Accents;
        var totalSwatches = accents.Count + 1;
        var cell = (innerRight - innerLeft) / totalSwatches;
        var swatchRadius = MathF.Min(cell * 0.32f, 11f * scale);

        var noneCenter = new Vector2(innerLeft + cell * 0.5f, rowY);
        var noneSelected = string.IsNullOrEmpty(current.FolderTint);
        if (ControlTile.Swatch(drawList, noneCenter, swatchRadius, NoTintSwatch, noneSelected, 1f, interactive) &&
            !noneSelected)
        {
            layout.SetFolderTint(current, string.Empty);
        }

        for (var index = 0; index < accents.Count; index++)
        {
            var accent = accents[index];
            var center = new Vector2(innerLeft + cell * (index + 1.5f), rowY);
            var selected = current.FolderTint == accent.Name;
            if (ControlTile.Swatch(drawList, center, swatchRadius, accent.Color, selected, 1f, interactive) &&
                !selected)
            {
                layout.SetFolderTint(current, accent.Name);
            }
        }
    }

    private void ApplyRename()
    {
        if (folder is { IsFolder: true } current &&
            !string.Equals(nameBuffer, current.FolderName, StringComparison.Ordinal))
        {
            layout.Rename(current, nameBuffer);
        }
    }
}
