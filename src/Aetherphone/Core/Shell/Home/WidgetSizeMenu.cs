using Aetherphone.Core.Animation;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Core.Shell.Home;

internal sealed class WidgetSizeMenu
{
    private const float PopSmoothTime = 0.14f;
    private const float WidthUnits = 168f;
    private const float RowUnits = 36f;

    private readonly HomeLayoutService layout;
    private HomeTile? tile;
    private Spring pop;
    private bool closing;
    private bool openedThisFrame;

    public WidgetSizeMenu(HomeLayoutService layout)
    {
        this.layout = layout;
    }

    public bool Active => tile is not null;
    public HomeTile? Tile => tile;

    public void Open(HomeTile target)
    {
        tile = target;
        closing = false;
        openedThisFrame = true;
        pop.SnapTo(0f);
    }

    public void Close()
    {
        closing = true;
    }

    public void Draw(Rect content, Rect anchor, PhoneTheme theme, float delta, float scale)
    {
        if (tile is null)
        {
            return;
        }

        pop.Step(closing ? 0f : 1f, PopSmoothTime, delta);
        if (closing && pop.Value < 0.03f)
        {
            tile = null;
            closing = false;
            return;
        }

        var current = tile;
        var eased = Math.Clamp(pop.Value, 0f, 1f);
        var width = WidthUnits * scale;
        var rowHeight = RowUnits * scale;
        var rowCount = CountRows(current);
        var height = rowCount * rowHeight + 8f * scale;
        var gap = 8f * scale;
        var below = anchor.Max.Y + gap + height <= content.Max.Y;
        var top = below ? anchor.Max.Y + gap : anchor.Min.Y - gap - height;
        var left = Math.Clamp(anchor.Center.X - width * 0.5f, content.Min.X + 4f * scale,
            content.Max.X - width - 4f * scale);
        var panel = new Rect(new Vector2(left, top), new Vector2(left + width, top + height));
        var pivot = below ? new Vector2(anchor.Center.X, panel.Min.Y) : new Vector2(anchor.Center.X, panel.Max.Y);
        var scaled = new Rect(pivot + (panel.Min - pivot) * eased, pivot + (panel.Max - pivot) * eased);
        var drawList = ImGui.GetWindowDrawList();
        Elevation.Floating(drawList, scaled.Min, scaled.Max, 16f * scale, scale);
        Material.Frosted(drawList, scaled.Min, scaled.Max, 16f * scale, scale, eased);
        var interactive = !closing && eased > 0.9f;
        if (!interactive)
        {
            return;
        }

        var rowIndex = 0;
        var handled = DrawSizeRow(panel, current, WidgetSize.Small, L.Home.SizeSmall, theme, rowHeight, scale,
            ref rowIndex) || DrawSizeRow(panel, current, WidgetSize.Medium, L.Home.SizeMedium, theme, rowHeight,
            scale, ref rowIndex) || DrawSizeRow(panel, current, WidgetSize.Large, L.Home.SizeLarge, theme, rowHeight,
            scale, ref rowIndex);
        if (!handled && DrawRow(panel, Loc.T(L.Home.Remove), theme.Danger, false, theme, rowHeight, scale,
                ref rowIndex))
        {
            layout.RemoveTile(current);
            Close();
            handled = true;
        }

        if (!handled && !openedThisFrame && ImGui.IsMouseClicked(ImGuiMouseButton.Left) &&
            !scaled.Contains(ImGui.GetMousePos()))
        {
            Close();
        }

        openedThisFrame = false;
    }

    private bool DrawSizeRow(Rect panel, HomeTile current, WidgetSize size, LocString label, PhoneTheme theme,
        float rowHeight, float scale, ref int rowIndex)
    {
        if (!WidgetSizes.Contains(current.Widget!.Sizes, size))
        {
            return false;
        }

        if (!DrawRow(panel, Loc.T(label), theme.TextStrong, current.Size == size, theme, rowHeight, scale,
                ref rowIndex))
        {
            return false;
        }

        layout.ResizeWidget(current, size);
        Close();
        return true;
    }

    private static bool DrawRow(Rect panel, string label, Vector4 color, bool selected, PhoneTheme theme,
        float rowHeight, float scale, ref int rowIndex)
    {
        var top = panel.Min.Y + 4f * scale + rowIndex * rowHeight;
        var row = new Rect(new Vector2(panel.Min.X + 4f * scale, top),
            new Vector2(panel.Max.X - 4f * scale, top + rowHeight));
        rowIndex++;
        var drawList = ImGui.GetWindowDrawList();
        var hovered = ImGui.IsMouseHoveringRect(row.Min, row.Max);
        if (hovered)
        {
            Squircle.Fill(drawList, row.Min, row.Max, 11f * scale,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.08f)));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        Typography.Draw(drawList, new Vector2(row.Min.X + 12f * scale,
            row.Center.Y - Typography.Measure(label, TextStyles.Body).Y * 0.5f), label, color, TextStyles.Body);
        if (selected)
        {
            var checkCenter = new Vector2(row.Max.X - 16f * scale, row.Center.Y);
            var ink = ImGui.GetColorU32(theme.Accent);
            drawList.AddLine(checkCenter + new Vector2(-5f, 0f) * scale, checkCenter + new Vector2(-1.5f, 3.5f) * scale,
                ink, 2f * scale);
            drawList.AddLine(checkCenter + new Vector2(-1.5f, 3.5f) * scale, checkCenter + new Vector2(5f, -3.5f) * scale,
                ink, 2f * scale);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private static int CountRows(HomeTile current)
    {
        var rows = 1;
        if (WidgetSizes.Contains(current.Widget!.Sizes, WidgetSize.Small))
        {
            rows++;
        }

        if (WidgetSizes.Contains(current.Widget!.Sizes, WidgetSize.Medium))
        {
            rows++;
        }

        if (WidgetSizes.Contains(current.Widget!.Sizes, WidgetSize.Large))
        {
            rows++;
        }

        return rows;
    }
}
