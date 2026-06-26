using System.Numerics;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.WaterSort;

internal sealed class WaterSortRenderer
{
    private static readonly Vector4[] LiquidColors =
    {
        new(0.95f, 0.45f, 0.78f, 1f),
        new(0.40f, 0.68f, 0.98f, 1f),
        new(0.46f, 0.86f, 0.66f, 1f),
        new(0.95f, 0.62f, 0.30f, 1f),
        new(0.72f, 0.46f, 0.96f, 1f),
        new(0.93f, 0.42f, 0.50f, 1f),
        new(0.36f, 0.82f, 0.82f, 1f),
        new(0.92f, 0.84f, 0.36f, 1f),
        new(0.62f, 0.66f, 0.74f, 1f),
    };

    public static Vector4 ColorOf(int color) => LiquidColors[color % LiquidColors.Length];

    public static Rect TubeRect(Rect area, int index, int tubeCount, float scale)
    {
        var rowCount = tubeCount <= 5 ? 1 : 2;
        var firstRow = (tubeCount + rowCount - 1) / rowCount;

        var row = index < firstRow ? 0 : 1;
        var indexInRow = row == 0 ? index : index - firstRow;
        var tubesInRow = row == 0 ? firstRow : tubeCount - firstRow;

        var slotWidth = area.Width / tubesInRow;
        var rowHeight = area.Height / rowCount;
        var centerX = area.Min.X + slotWidth * (indexInRow + 0.5f);
        var centerY = area.Min.Y + rowHeight * (row + 0.5f);

        var tubeWidth = MathF.Min(slotWidth * 0.6f, rowHeight * 0.3f);
        var tubeHeight = rowHeight * 0.76f;

        var min = new Vector2(centerX - tubeWidth * 0.5f, centerY - tubeHeight * 0.5f);
        var max = new Vector2(centerX + tubeWidth * 0.5f, centerY + tubeHeight * 0.5f);
        return new Rect(min, max);
    }

    public void Draw(WaterSortBoard board, Rect area, float scale, PhoneTheme theme, float selectedLift)
    {
        var drawList = ImGui.GetWindowDrawList();
        for (var tube = 0; tube < board.TubeCount; tube++)
        {
            var rect = TubeRect(area, tube, board.TubeCount, scale);
            var lifted = tube == board.Selected;
            if (lifted)
            {
                rect = new Rect(rect.Min - new Vector2(0f, selectedLift), rect.Max - new Vector2(0f, selectedLift));
            }

            DrawTube(drawList, board, tube, rect, scale, theme, lifted);
        }
    }

    private void DrawTube(ImDrawListPtr drawList, WaterSortBoard board, int tube, Rect rect, float scale, PhoneTheme theme, bool lifted)
    {
        var rounding = rect.Width * 0.5f;
        var wall = MathF.Max(1.5f, rect.Width * 0.06f);

        if (lifted)
        {
            ProgressRing.Glow(new Vector2(rect.Center.X, rect.Max.Y - rect.Height * 0.3f), rect.Width * 0.9f, theme.Accent, 0.6f);
        }

        drawList.AddRectFilled(rect.Min, rect.Max, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.05f)), rounding, ImDrawFlags.RoundCornersBottom);

        var innerTop = rect.Min.Y + rect.Height * 0.08f;
        var innerBottom = rect.Max.Y - wall;
        var segmentHeight = (innerBottom - innerTop) / WaterSortBoard.Capacity;
        var left = rect.Min.X + wall;
        var right = rect.Max.X - wall;

        for (var level = 0; level < board.Count(tube); level++)
        {
            var color = board.Segment(tube, level);
            if (color < 0)
            {
                continue;
            }

            var bandBottom = innerBottom - level * segmentHeight;
            var bandTop = bandBottom - segmentHeight;
            var bandMin = new Vector2(left, bandTop);
            var bandMax = new Vector2(right, bandBottom);
            var flags = level == 0 ? ImDrawFlags.RoundCornersBottom : ImDrawFlags.RoundCornersNone;
            drawList.AddRectFilled(bandMin, bandMax, ImGui.GetColorU32(ColorOf(color)), level == 0 ? rounding - wall : 0f, flags);
        }

        drawList.AddRectFilled(new Vector2(left, innerTop), new Vector2(left + wall * 0.9f, innerBottom), ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.16f)), 0f);
        drawList.AddRect(rect.Min, rect.Max, ImGui.GetColorU32(lifted ? theme.Accent : Styling.BorderDim), rounding, ImDrawFlags.RoundCornersBottom, (lifted ? 2.2f : 1.4f) * scale);
    }

    public void DrawPourStream(Rect fromTube, Rect toTube, Vector4 color, float progress, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var spout = new Vector2(fromTube.Center.X, fromTube.Min.Y + fromTube.Height * 0.06f);
        var target = new Vector2(toTube.Center.X, toTube.Min.Y + toTube.Height * 0.18f);

        var head = Vector2.Lerp(spout, target, progress);
        drawList.AddLine(spout, head, ImGui.GetColorU32(color with { W = 0.85f }), 3f * scale);
        drawList.AddCircleFilled(head, 3.2f * scale, ImGui.GetColorU32(color));
    }
}
