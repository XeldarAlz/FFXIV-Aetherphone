using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class SwatchStrip
{
    private const float SwatchRadius = 11f;
    private const float SwatchGap = 12f;
    private const float RingOffset = 3f;
    private const int WheelSegments = 12;
    private const int CircleSegments = 24;

    public static bool NeedsTwoRows(string label, int optionCount, float availableWidth)
    {
        var scale = UiScale.Current;
        var radius = SwatchRadius * scale;
        var gap = SwatchGap * scale;
        var swatchWidth = optionCount * (radius * 2f + gap) - gap;
        var labelWidth = Typography.Measure(label).X;
        return labelWidth + gap + swatchWidth > availableWidth;
    }

    public static int Draw(Rect row, string label, IReadOnlyList<NamedColor> options, int selected, PhoneTheme theme,
        bool stacked, Vector4 customColor, bool customSelected)
    {
        var scale = UiScale.Current;
        var radius = SwatchRadius * scale;
        var gap = SwatchGap * scale;
        var step = radius * 2f + gap;
        var slots = options.Count + 1;
        var startX = row.Max.X - (slots * step - gap) + radius;
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
        for (var index = 0; index < slots; index++)
        {
            var center = new Vector2(startX + index * step, swatchCenterY);
            var isCustom = index == options.Count;
            if (isCustom && !customSelected)
            {
                DrawWheel(dl, center, radius);
            }
            else
            {
                dl.AddCircleFilled(center, radius,
                    ImGui.GetColorU32(isCustom ? customColor : options[index].Color), CircleSegments);
            }

            if (index == selected || (isCustom && customSelected))
            {
                dl.AddCircle(center, radius + RingOffset * scale, ImGui.GetColorU32(theme.TextStrong), CircleSegments,
                    Metrics.Stroke.Ring * scale);
            }

            var min = center - new Vector2(radius, radius);
            var max = center + new Vector2(radius, radius);
            if (!UiInteract.Hover(min, max))
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

    private static void DrawWheel(ImDrawListPtr drawList, Vector2 center, float radius)
    {
        for (var segment = 0; segment < WheelSegments; segment++)
        {
            var from = MathF.Tau * segment / WheelSegments;
            var to = MathF.Tau * (segment + 1) / WheelSegments;
            drawList.PathLineTo(center);
            drawList.PathArcTo(center, radius, from, to, 4);
            drawList.PathFillConvex(ImGui.GetColorU32(new HsvColor(segment / (float)WheelSegments, 0.85f, 1f).ToRgb()));
        }
    }
}
