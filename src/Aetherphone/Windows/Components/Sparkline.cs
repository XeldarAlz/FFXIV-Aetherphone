using Aetherphone.Core;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class Sparkline
{
    public static void Draw(Rect area, ReadOnlySpan<float> values, Vector4 line, Vector4 fill)
    {
        if (values.Length < 2 || area.Width <= 2f || area.Height <= 2f)
        {
            return;
        }

        var min = values[0];
        var max = values[0];
        for (var index = 1; index < values.Length; index++)
        {
            if (values[index] < min)
            {
                min = values[index];
            }

            if (values[index] > max)
            {
                max = values[index];
            }
        }

        var range = max - min;
        if (range <= 0f)
        {
            range = 1f;
        }

        var count = values.Length;
        var stepX = area.Width / (count - 1);
        var usableHeight = area.Height - 2f;
        var baseY = area.Max.Y;
        Span<Vector2> points = count <= 128 ? stackalloc Vector2[count] : new Vector2[count];
        for (var index = 0; index < count; index++)
        {
            var normalized = (values[index] - min) / range;
            points[index] = new Vector2(area.Min.X + stepX * index, baseY - normalized * usableHeight - 1f);
        }

        var drawList = ImGui.GetWindowDrawList();
        var fillColor = ImGui.GetColorU32(fill);
        var lineColor = ImGui.GetColorU32(line);
        for (var index = 0; index < count - 1; index++)
        {
            var leftBase = new Vector2(points[index].X, baseY);
            var rightBase = new Vector2(points[index + 1].X, baseY);
            drawList.AddQuadFilled(points[index], points[index + 1], rightBase, leftBase, fillColor);
        }

        for (var index = 0; index < count - 1; index++)
        {
            drawList.AddLine(points[index], points[index + 1], lineColor, 2f);
        }
    }
}
