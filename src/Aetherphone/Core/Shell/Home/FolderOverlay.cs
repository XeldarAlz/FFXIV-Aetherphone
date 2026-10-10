using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Shortcuts;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Core.Shell.Home;

internal sealed class FolderOverlay
{
    private const float VeilDim = 0.35f;
    private const float PanelRadiusUnits = 22f;
    private const float PanelWidthFraction = 0.84f;
    private const float PanelMaxHeightFraction = 0.55f;
    private const float PanelPadUnits = 18f;
    private const float HeaderGapUnits = 14f;
    private const float SwatchRowHeightUnits = 34f;
    private const float IconCellFraction = 0.62f;
    private const float IconMaxUnits = 52f;
    private const float CellLabelBandUnits = 26f;
    private const float TileTintAlpha = 0.34f;
    private const float TintAlpha = 0.18f;
    private const float HeaderFadeStart = 0.35f;
    private const float LabelFadeStart = 0.55f;
    private const float InteractiveThreshold = 0.95f;
    private const float CloseSnap = 0.01f;
    private const float WheelStepUnits = 40f;
    private const float DragThresholdUnits = 7f;
    private const float DragLiftScale = 1.08f;
    private const int NameMaxLength = 64;
    private const int Columns = HomeTileView.FolderMiniColumns;
    private static readonly Vector4 NoTintSwatch = new(0.55f, 0.56f, 0.60f, 1f);

    private readonly HomeLayoutService layout;
    private readonly ShortcutStore shortcuts;
    private readonly ShortcutRunner runner;
    private readonly Configuration configuration;
    private HomeTile? folder;
    private bool closing;
    private Spring anim;
    private Rect origin;
    private string nameBuffer = string.Empty;
    private float scrollY;
    private int openedFrame;
    private int pressIndex = -1;
    private Vector2 pressMouse;
    private int dragIndex = -1;
    private int dragTarget = -1;
    private Vector2 dragGrab;
    private Vector2 dragCenter;
    private Spring[] slideX = Array.Empty<Spring>();
    private Spring[] slideY = Array.Empty<Spring>();

    public FolderOverlay(HomeLayoutService layout, ShortcutStore shortcuts, ShortcutRunner runner,
        Configuration configuration)
    {
        this.layout = layout;
        this.shortcuts = shortcuts;
        this.runner = runner;
        this.configuration = configuration;
    }

    public bool Active => folder is not null;
    public HomeTile? Folder => folder;
    public float Presence => folder is null ? 0f : Math.Clamp(anim.Value, 0f, 1f);

    public void Open(HomeTile tile, Rect originRect)
    {
        folder = tile;
        closing = false;
        anim.SnapTo(0f);
        origin = originRect;
        nameBuffer = tile.FolderName;
        scrollY = 0f;
        openedFrame = ImGui.GetFrameCount();
        pressIndex = -1;
        dragIndex = -1;
    }

    public void RequestClose()
    {
        ApplyRename();
        closing = true;
        pressIndex = -1;
        dragIndex = -1;
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
        if (closing && anim.Value < CloseSnap)
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
        var side = MathF.Min(content.Width * PanelWidthFraction, content.Height * PanelMaxHeightFraction);
        var headerHeight = (GlassField.HeightUnits + SwatchRowHeightUnits) * scale;
        var headerGap = HeaderGapUnits * scale;
        var left = content.Center.X - side * 0.5f;
        var top = content.Center.Y - (headerHeight + headerGap + side) * 0.5f;
        var header = new Rect(new Vector2(left, top), new Vector2(left + side, top + headerHeight));
        var targetTop = header.Max.Y + headerGap;
        var target = new Rect(new Vector2(left, targetTop), new Vector2(left + side, targetTop + side));
        var panel = new Rect(Vector2.Lerp(origin.Min, target.Min, progress),
            Vector2.Lerp(origin.Max, target.Max, progress));
        var interactive = !closing && progress > InteractiveThreshold;
        DrawPanel(drawList, panel, current, theme, scale, progress);
        DrawHeader(drawList, header, target.Min.Y, theme, current, scale, progress, interactive);
        DrawMembers(drawList, panel, target, theme, navigation, current, editing, currentPage, scale, progress,
            interactive, delta);
        if (interactive && ImGui.GetFrameCount() != openedFrame &&
            UiInteract.ClickedOutside(header.Min, target.Max, false))
        {
            RequestClose();
        }

        drawList.PopClipRect();
    }

    private void DrawPanel(ImDrawListPtr drawList, Rect panel, HomeTile current, PhoneTheme theme, float scale,
        float progress)
    {
        var radius = Easing.Lerp(origin.Width * Metrics.Radius.HomeTileFactor, PanelRadiusUnits * scale, progress);
        Material.LiquidGlass(drawList, panel.Min, panel.Max, radius, scale, GlassTone.Dark, 0f);
        Material.LiquidGlass(drawList, panel.Min, panel.Max, radius, scale, GlassTone.Light,
            WallpaperLegibility.Strength(theme), 1f - progress);
        if (string.IsNullOrEmpty(current.FolderTint))
        {
            return;
        }

        Squircle.Fill(drawList, panel.Min, panel.Max, radius,
            ImGui.GetColorU32(Palette.WithAlpha(ThemeCatalog.ResolveAccent(current.FolderTint),
                Easing.Lerp(TileTintAlpha, TintAlpha, progress))));
    }

    private void DrawHeader(ImDrawListPtr drawList, Rect header, float panelTop, PhoneTheme theme, HomeTile current,
        float scale, float progress, bool interactive)
    {
        var alpha = Easing.Segment(progress, HeaderFadeStart, 1f);
        if (alpha <= 0.01f)
        {
            return;
        }

        var vertexStart = drawList.VtxBuffer.Size;
        var nameField = new Rect(header.Min,
            new Vector2(header.Max.X, header.Min.Y + GlassField.HeightUnits * scale));
        GlassField.Surface(drawList, nameField, GlassField.Radius(nameField), scale,
            WallpaperLegibility.Strength(theme), 1f);
        if (GlassField.Title(nameField, "##folderName", Loc.T(L.Home.NewFolder), ref nameBuffer, theme, scale,
                NameMaxLength, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            ApplyRename();
        }

        DrawTintRow(drawList, header, current, (nameField.Max.Y + panelTop) * 0.5f, scale, interactive);
        LayerCompositor.Fade(drawList, vertexStart, alpha);
    }

    private void DrawMembers(ImDrawListPtr drawList, Rect panel, Rect target, PhoneTheme theme,
        INavigator navigation, HomeTile current, bool editing, int currentPage, float scale, float progress,
        bool interactive, float delta)
    {
        var pad = PanelPadUnits * scale;
        var cell = GridCell(target.Width, scale);
        var iconSize = IconSize(cell, scale);
        var iconTop = (cell - iconSize - CellLabelBandUnits * scale) * 0.5f;
        var rows = (current.Members.Count + Columns - 1) / Columns;
        if (interactive && UiInteract.Hover(panel.Min, panel.Max))
        {
            scrollY -= ImGui.GetIO().MouseWheel * WheelStepUnits * scale;
        }

        scrollY = Math.Clamp(scrollY, 0f, MathF.Max(0f, rows * cell - (target.Height - pad * 2f)));
        var scroll = scrollY * progress;
        var iconFraction = Easing.Lerp(HomeTileView.FolderMiniIconFraction, iconSize / target.Width, progress);
        var drawFactor = iconFraction * panel.Width / iconSize;
        var labelAlpha = Easing.Segment(progress, LabelFadeStart, 1f);
        var extent = cell * drawFactor;
        var released = TrackDrag(current.Members.Count, editing && interactive, scale);
        EnsureSlideSprings(current.Members.Count);
        if (dragIndex >= 0)
        {
            dragTarget = SlotAt(dragCenter - target.Min + new Vector2(0f, scroll), pad, cell,
                current.Members.Count);
        }

        if (released)
        {
            Drop(current, target.Min + new Vector2(pad + cell * 0.5f, pad + iconTop + iconSize * 0.5f - scroll),
                cell);
        }

        drawList.PushClipRect(panel.Min, panel.Max, true);
        for (var index = 0; index < current.Members.Count; index++)
        {
            var openCenter = new Vector2(pad + (index % Columns + 0.5f) * cell,
                pad + index / Columns * cell + iconTop + iconSize * 0.5f - scroll) / target.Width;
            var center = panel.Min +
                         Vector2.Lerp(HomeTileView.FolderMiniCenter(index), openCenter, progress) * panel.Width;
            center += SlideOffset(index, cell, delta);
            if (index == dragIndex)
            {
                continue;
            }

            if (center.Y + extent < panel.Min.Y || center.Y - extent > panel.Max.Y)
            {
                continue;
            }

            var member = current.Members[index];
            DrawMember(drawList, member, center, iconSize, theme, labelAlpha, cell, drawFactor);
            if (!interactive)
            {
                continue;
            }

            var half = iconSize * 0.5f;
            var iconRect = new Rect(new Vector2(center.X - half, center.Y - half),
                new Vector2(center.X + half, center.Y + half));
            if (editing)
            {
                if (HomeTileView.RemoveBadge(new Vector2(iconRect.Min.X + 2f * scale, iconRect.Min.Y + 2f * scale),
                        scale, theme))
                {
                    layout.RemoveFromFolder(current, member, currentPage);
                    if (current.Members.Count <= 1)
                    {
                        RequestClose();
                    }

                    break;
                }

                if (dragIndex < 0 && pressIndex < 0 && UiInteract.Hover(iconRect.Min, iconRect.Max) &&
                    ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    pressIndex = index;
                    pressMouse = ImGui.GetMousePos();
                    dragGrab = pressMouse - center;
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

                    break;
                }
            }
        }

        if (dragIndex >= 0)
        {
            DrawMember(drawList, current.Members[dragIndex], dragCenter, iconSize, theme, labelAlpha, cell,
                drawFactor * DragLiftScale);
        }

        drawList.PopClipRect();
    }

    private bool TrackDrag(int count, bool enabled, float scale)
    {
        if (!enabled || pressIndex >= count || dragIndex >= count)
        {
            pressIndex = -1;
            dragIndex = -1;
            return false;
        }

        var mouse = ImGui.GetMousePos();
        if (pressIndex >= 0)
        {
            if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                pressIndex = -1;
            }
            else if (Vector2.Distance(mouse, pressMouse) > DragThresholdUnits * scale)
            {
                dragIndex = pressIndex;
                dragTarget = pressIndex;
                pressIndex = -1;
                UiInteract.CancelPendingTap();
            }
        }

        if (dragIndex < 0)
        {
            return false;
        }

        dragCenter = mouse - dragGrab;
        return !ImGui.IsMouseDown(ImGuiMouseButton.Left);
    }

    private void Drop(HomeTile current, Vector2 firstSlotCenter, float cell)
    {
        var from = dragIndex;
        var to = dragTarget;
        dragIndex = -1;
        for (var index = 0; index < slideX.Length; index++)
        {
            slideX[index].SnapTo(0f);
            slideY[index].SnapTo(0f);
        }

        var landing = dragCenter - (firstSlotCenter + SlotPosition(to, cell));
        slideX[to].SnapTo(landing.X);
        slideY[to].SnapTo(landing.Y);
        if (from != to)
        {
            layout.MoveFolderMember(current, from, to);
        }
    }

    private void EnsureSlideSprings(int count)
    {
        if (slideX.Length >= count)
        {
            return;
        }

        Array.Resize(ref slideX, count);
        Array.Resize(ref slideY, count);
    }

    private Vector2 SlideOffset(int index, float cell, float delta)
    {
        var slot = index;
        if (dragIndex >= 0 && index != dragIndex)
        {
            if (index > dragIndex && index <= dragTarget)
            {
                slot = index - 1;
            }
            else if (index < dragIndex && index >= dragTarget)
            {
                slot = index + 1;
            }
        }

        var displacement = SlotPosition(slot, cell) - SlotPosition(index, cell);
        return new Vector2(slideX[index].Step(displacement.X, Motion.PageSettle, delta),
            slideY[index].Step(displacement.Y, Motion.PageSettle, delta));
    }

    private static Vector2 SlotPosition(int slot, float cell) => new(slot % Columns * cell, slot / Columns * cell);

    private static int SlotAt(Vector2 point, float pad, float cell, int count)
    {
        var column = Math.Clamp((int)MathF.Floor((point.X - pad) / cell), 0, Columns - 1);
        var row = Math.Max(0, (int)MathF.Floor((point.Y - pad) / cell));
        return Math.Clamp(row * Columns + column, 0, count - 1);
    }

    private void DrawMember(ImDrawListPtr drawList, HomeTile member, Vector2 center, float iconSize,
        PhoneTheme theme, float labelAlpha, float cell, float drawFactor)
    {
        var vertexStart = drawList.VtxBuffer.Size;
        if (member.IsShortcut)
        {
            HomeTileView.DrawShortcut(center, iconSize, member.Shortcut!, shortcuts.Icon(member.Shortcut!), theme,
                1f, labelAlpha, true, cell);
        }
        else
        {
            HomeTileView.DrawApp(center, iconSize, member.App!, theme, 1f, labelAlpha, true, cell, configuration);
        }

        VertexWarp.Scale(drawList, vertexStart, center, drawFactor);
    }

    private void DrawTintRow(ImDrawListPtr drawList, Rect header, HomeTile current, float rowY, float scale,
        bool interactive)
    {
        var accents = ThemeCatalog.Accents;
        var totalSwatches = accents.Count + 1;
        var gridCell = GridCell(header.Width, scale);
        var iconSize = IconSize(gridCell, scale);
        var span = gridCell * (Columns - 1) + iconSize;
        var swatchRadius = MathF.Min(span / totalSwatches * 0.32f, 11f * scale);
        var step = (span - swatchRadius * 2f) / (totalSwatches - 1);
        var firstCenter = header.Center.X - span * 0.5f + swatchRadius;

        var noneCenter = new Vector2(firstCenter, rowY);
        var noneSelected = string.IsNullOrEmpty(current.FolderTint);
        if (ControlTile.Swatch(drawList, noneCenter, swatchRadius, NoTintSwatch, noneSelected, 1f, interactive) &&
            !noneSelected)
        {
            layout.SetFolderTint(current, string.Empty);
        }

        for (var index = 0; index < accents.Count; index++)
        {
            var accent = accents[index];
            var center = new Vector2(firstCenter + step * (index + 1), rowY);
            var selected = current.FolderTint == accent.Name;
            if (ControlTile.Swatch(drawList, center, swatchRadius, accent.Color, selected, 1f, interactive) &&
                !selected)
            {
                layout.SetFolderTint(current, accent.Name);
            }
        }
    }

    private static float GridCell(float panelWidth, float scale) =>
        (panelWidth - PanelPadUnits * scale * 2f) / Columns;

    private static float IconSize(float cell, float scale) => MathF.Min(cell * IconCellFraction, IconMaxUnits * scale);

    private void ApplyRename()
    {
        if (folder is { IsFolder: true } current &&
            !string.Equals(nameBuffer, current.FolderName, StringComparison.Ordinal))
        {
            layout.Rename(current, nameBuffer);
        }
    }
}
