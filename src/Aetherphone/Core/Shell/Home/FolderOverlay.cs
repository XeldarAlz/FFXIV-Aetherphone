using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Core.Shell.Home;

internal sealed class FolderOverlay
{
    private const float OpenSmoothTime = 0.20f;
    private static readonly Vector4 NoTintSwatch = new(0.55f, 0.56f, 0.60f, 1f);

    private readonly HomeLayoutService layout;
    private HomeTile? folder;
    private bool closing;
    private Spring anim;
    private Rect origin;
    private string nameBuffer = string.Empty;
    private float scrollY;

    public FolderOverlay(HomeLayoutService layout)
    {
        this.layout = layout;
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
    }

    public void RequestClose()
    {
        ApplyRename();
        closing = true;
    }

    public void Draw(Rect content, in HomeMetrics metrics, PhoneTheme theme, INavigator navigation, bool editing,
        int currentPage, float delta)
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

        anim.Step(closing ? 0f : 1f, OpenSmoothTime, delta);
        if (closing && anim.Value < 0.02f)
        {
            folder = null;
            closing = false;
            return;
        }

        var current = folder;
        var scale = metrics.Scale;
        var eased = Easing.EaseOutCubic(Math.Clamp(anim.Value, 0f, 1f));
        var drawList = ImGui.GetWindowDrawList();
        drawList.PushClipRect(content.Min, content.Max, true);
        Material.Veil(drawList, content.Min, content.Max, 0.5f * eased);
        var columns = current.Apps.Count <= 9 ? 3 : 4;
        var rows = (current.Apps.Count + columns - 1) / columns;
        var panelWidth = content.Width * 0.84f;
        var pad = 18f * scale;
        var cellWidth = (panelWidth - pad * 2f) / columns;
        var iconSize = MathF.Min(cellWidth * 0.62f, 52f * scale);
        var cellHeight = iconSize + 26f * scale;
        var swatchRowHeight = 34f * scale;
        var headerHeight = 48f * scale + swatchRowHeight;
        var panelHeight = MathF.Min(headerHeight + rows * cellHeight + pad, content.Height * 0.82f);
        var targetMin = new Vector2(content.Center.X - panelWidth * 0.5f, content.Center.Y - panelHeight * 0.5f);
        var target = new Rect(targetMin, targetMin + new Vector2(panelWidth, panelHeight));
        var panel = new Rect(Vector2.Lerp(origin.Min, target.Min, eased), Vector2.Lerp(origin.Max, target.Max, eased));
        Material.Frosted(drawList, panel.Min, panel.Max, 28f * scale, scale, eased);
        var interactive = !closing && eased > 0.85f;
        if (interactive)
        {
            DrawContents(panel, metrics, theme, navigation, current, editing, currentPage, columns, pad, iconSize,
                cellWidth, cellHeight, headerHeight);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !panel.Contains(ImGui.GetMousePos()))
            {
                RequestClose();
            }
        }

        drawList.PopClipRect();
    }

    private void DrawContents(Rect panel, in HomeMetrics metrics, PhoneTheme theme, INavigator navigation,
        HomeTile current, bool editing, int currentPage, int columns, float pad, float iconSize, float cellWidth,
        float cellHeight, float headerHeight)
    {
        var scale = metrics.Scale;
        ImGui.SetCursorScreenPos(new Vector2(panel.Min.X + pad, panel.Min.Y + 12f * scale));
        ImGui.SetNextItemWidth(panel.Width - pad * 2f);
        if (ImGui.InputTextWithHint("##folderName", Loc.T(L.Home.NewFolder), ref nameBuffer, 64,
                ImGuiInputTextFlags.EnterReturnsTrue))
        {
            ApplyRename();
        }

        DrawTintRow(panel, current, pad, scale);

        var gridTop = panel.Min.Y + headerHeight;
        var gridView = new Rect(new Vector2(panel.Min.X, gridTop), panel.Max);
        var drawList = ImGui.GetWindowDrawList();
        drawList.PushClipRect(gridView.Min, gridView.Max, true);
        if (ImGui.IsMouseHoveringRect(gridView.Min, gridView.Max))
        {
            scrollY -= ImGui.GetIO().MouseWheel * 40f * scale;
        }

        var rows = (current.Apps.Count + columns - 1) / columns;
        var contentHeight = rows * cellHeight;
        scrollY = Math.Clamp(scrollY, 0f, MathF.Max(0f, contentHeight - gridView.Height));

        for (var index = 0; index < current.Apps.Count; index++)
        {
            var column = index % columns;
            var row = index / columns;
            var center = new Vector2(panel.Min.X + pad + (column + 0.5f) * cellWidth,
                gridTop - scrollY + (row + 0.5f) * cellHeight);
            if (center.Y + cellHeight * 0.5f < gridView.Min.Y || center.Y - cellHeight * 0.5f > gridView.Max.Y)
            {
                continue;
            }

            var app = current.Apps[index];
            HomeTileView.DrawApp(center, iconSize, app, theme, 1f, 1f, cellWidth);
            var half = iconSize * 0.5f;
            var iconRect = new Rect(new Vector2(center.X - half, center.Y - half),
                new Vector2(center.X + half, center.Y + half));
            if (editing)
            {
                if (HomeTileView.RemoveBadge(new Vector2(center.X - half + 2f * scale, center.Y - half + 2f * scale),
                        scale, theme))
                {
                    layout.RemoveFromFolder(current, app, currentPage, layout.Page(currentPage).Count);
                    if (current.Apps.Count <= 1)
                    {
                        RequestClose();
                    }

                    drawList.PopClipRect();
                    return;
                }
            }
            else if (ImGui.IsMouseHoveringRect(iconRect.Min, iconRect.Max))
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    RequestClose();
                    navigation.OpenAppFrom(app, iconRect);
                    drawList.PopClipRect();
                    return;
                }
            }
        }

        drawList.PopClipRect();
    }

    private void DrawTintRow(Rect panel, HomeTile current, float pad, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rowY = panel.Min.Y + 48f * scale + 17f * scale;
        var innerLeft = panel.Min.X + pad;
        var innerRight = panel.Max.X - pad;
        var accents = ThemeCatalog.Accents;
        var totalSwatches = accents.Count + 1;
        var cell = (innerRight - innerLeft) / totalSwatches;
        var swatchRadius = MathF.Min(cell * 0.32f, 11f * scale);

        var noneCenter = new Vector2(innerLeft + cell * 0.5f, rowY);
        var noneSelected = string.IsNullOrEmpty(current.FolderTint);
        if (ControlTile.Swatch(drawList, noneCenter, swatchRadius, NoTintSwatch, noneSelected, 1f, true) &&
            !noneSelected)
        {
            layout.SetFolderTint(current, string.Empty);
        }

        for (var index = 0; index < accents.Count; index++)
        {
            var accent = accents[index];
            var center = new Vector2(innerLeft + cell * (index + 1.5f), rowY);
            var selected = current.FolderTint == accent.Name;
            if (ControlTile.Swatch(drawList, center, swatchRadius, accent.Color, selected, 1f, true) && !selected)
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
