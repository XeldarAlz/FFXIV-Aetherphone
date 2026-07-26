using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal static class SwatchStrip
{
    public static bool NeedsTwoRows(string label, int optionCount, float availableWidth)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var radius = 11f * scale;
        var gap = 12f * scale;
        var swatchWidth = optionCount * (radius * 2f + gap) - gap;
        var labelWidth = Typography.Measure(label).X;
        return labelWidth + gap + swatchWidth > availableWidth;
    }

    public static int Draw(Rect row, string label, IReadOnlyList<NamedColor> options, int selected, PhoneTheme theme,
        bool stacked)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var radius = 11f * scale;
        var gap = 12f * scale;
        var step = radius * 2f + gap;
        var startX = row.Max.X - (options.Count * step - gap) + radius;
        var labelSize = Typography.Measure(label);
        float swatchCenterY;
        if (stacked)
        {
            var labelRowHeight = row.Height * 0.5f;
            Typography.Draw(new Vector2(row.Min.X, row.Min.Y + labelRowHeight * 0.5f - labelSize.Y * 0.5f), label,
                theme.TextStrong);
            swatchCenterY = row.Min.Y + labelRowHeight + (row.Height - labelRowHeight) * 0.5f;
        }
        else
        {
            Typography.Draw(new Vector2(row.Min.X, row.Center.Y - labelSize.Y * 0.5f), label, theme.TextStrong);
            swatchCenterY = row.Center.Y;
        }

        var result = selected;
        var dl = ImGui.GetWindowDrawList();
        for (var index = 0; index < options.Count; index++)
        {
            var center = new Vector2(startX + index * step, swatchCenterY);
            dl.AddCircleFilled(center, radius, ImGui.GetColorU32(options[index].Color), 24);
            if (index == selected)
            {
                dl.AddCircle(center, radius + 3f * scale, ImGui.GetColorU32(theme.TextStrong), 24, 2f * scale);
            }

            var min = center - new Vector2(radius, radius);
            var max = center + new Vector2(radius, radius);
            if (!ImGui.IsMouseHoveringRect(min, max))
            {
                continue;
            }

            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                result = index;
            }
        }

        return result;
    }
}
