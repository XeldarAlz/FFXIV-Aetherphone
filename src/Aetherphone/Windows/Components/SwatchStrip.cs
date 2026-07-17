using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal static class SwatchStrip
{
    public static int Draw(Rect row, string label, IReadOnlyList<NamedColor> options, int selected, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var labelSize = Typography.Measure(label);
        Typography.Draw(new Vector2(row.Min.X, row.Center.Y - labelSize.Y * 0.5f), label, theme.TextStrong);
        var radius = 11f * scale;
        var gap = 12f * scale;
        var step = radius * 2f + gap;
        var startX = row.Max.X - (options.Count * step - gap) + radius;
        var result = selected;
        var dl = ImGui.GetWindowDrawList();
        for (var index = 0; index < options.Count; index++)
        {
            var center = new Vector2(startX + index * step, row.Center.Y);
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
