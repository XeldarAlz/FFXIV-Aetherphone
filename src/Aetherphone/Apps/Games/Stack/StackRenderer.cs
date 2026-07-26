using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Stack;

internal sealed class StackRenderer
{
    private const float LevelHeightFactor = 0.072f;
    private const float BaselineFactor = 0.70f;
    private static readonly Vector4[] Ramp =
    {
        new(0.36f, 0.72f, 0.98f, 1f), new(0.44f, 0.86f, 0.78f, 1f), new(0.58f, 0.88f, 0.48f, 1f),
        new(0.96f, 0.82f, 0.38f, 1f), new(0.97f, 0.56f, 0.36f, 1f), new(0.94f, 0.42f, 0.58f, 1f),
        new(0.72f, 0.48f, 0.96f, 1f),
    };

    public static float LevelHeightOf(Rect area) => area.Height * LevelHeightFactor;

    public static float BaselineOf(Rect area) => area.Min.Y + area.Height * BaselineFactor;

    public static Vector4 ColorOf(int level)
    {
        var position = level * 0.13f % Ramp.Length;
        var index = (int)position;
        var next = (index + 1) % Ramp.Length;
        return Vector4.Lerp(Ramp[index], Ramp[next], position - index);
    }

    public static Vector2 ScreenPosition(Rect area, float camera, float centerX, float level)
    {
        var levelHeight = LevelHeightOf(area);
        var x = area.Min.X + centerX * area.Width;
        var y = BaselineOf(area) + (camera - level) * levelHeight;
        return new Vector2(x, y);
    }

    public void Draw(StackBoard board, Rect area, float camera, Vector2 shake, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        drawList.PushClipRect(area.Min, area.Max, true);
        var levelHeight = LevelHeightOf(area);
        var baseline = BaselineOf(area) + shake.Y;
        var offsetX = shake.X;
        var lowest = board.Level - StackBoard.VisibleLevels;
        if (lowest < 0)
        {
            lowest = 0;
        }

        for (var level = lowest; level < board.Level; level++)
        {
            var block = board.Block(level);
            var top = baseline + (camera - level - 1) * levelHeight;
            DrawBlock(drawList, area, block.CenterX + offsetX / area.Width, block.Width, top, levelHeight,
                ColorOf(level), scale, 1f);
        }

        DrawSlices(drawList, board, area, camera, baseline, levelHeight, scale);
        if (board.State == StackState.Over)
        {
            drawList.PopClipRect();
            return;
        }

        var movingTop = baseline + (camera - board.Level - 1) * levelHeight;
        DrawGuides(drawList, area, board, baseline, levelHeight, camera, scale);
        DrawBlock(drawList, area, board.MovingCenterX + offsetX / area.Width, board.MovingWidth, movingTop, levelHeight,
            ColorOf(board.Level), scale, 1.12f);
        drawList.PopClipRect();
    }

    private static void DrawGuides(ImDrawListPtr drawList, Rect area, StackBoard board, float baseline,
        float levelHeight, float camera, float scale)
    {
        var below = board.Block(board.Level - 1);
        var top = baseline + (camera - board.Level) * levelHeight;
        var leftX = area.Min.X + (below.CenterX - below.Width * 0.5f) * area.Width;
        var rightX = area.Min.X + (below.CenterX + below.Width * 0.5f) * area.Width;
        var color = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.14f));
        var thickness = MathF.Max(1f, scale);
        drawList.AddLine(new Vector2(leftX, top - levelHeight * 1.6f), new Vector2(leftX, top), color, thickness);
        drawList.AddLine(new Vector2(rightX, top - levelHeight * 1.6f), new Vector2(rightX, top), color, thickness);
    }

    private static void DrawBlock(ImDrawListPtr drawList, Rect area, float centerX, float width, float top,
        float levelHeight, Vector4 color, float scale, float lift)
    {
        var halfWidth = width * 0.5f * area.Width;
        var center = area.Min.X + centerX * area.Width;
        var min = new Vector2(center - halfWidth, top);
        var max = new Vector2(center + halfWidth, top + levelHeight);
        var rounding = MathF.Min(levelHeight * 0.32f, halfWidth * 0.5f);
        if (lift > 1f)
        {
            Elevation.Draw(drawList, min, max, rounding, scale, 16f, 7f, 0.34f);
            ProgressRing.Glow(new Vector2(center, (min.Y + max.Y) * 0.5f), halfWidth * 0.6f, color, 0.35f);
        }

        Squircle.FillVerticalGradient(drawList, min, max, rounding,
            ImGui.GetColorU32(GamePalette.Lighten(color, 0.22f)), ImGui.GetColorU32(GamePalette.Darken(color, 0.26f)));
        Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(GamePalette.Darken(color, 0.45f) with { W = 0.6f }),
            MathF.Max(1f, scale));
        var inset = MathF.Min(rounding, halfWidth);
        drawList.AddLine(new Vector2(min.X + inset, min.Y + 1.5f * scale), new Vector2(max.X - inset, min.Y + 1.5f * scale),
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.34f)), MathF.Max(1f, scale));
    }

    private static void DrawSlices(ImDrawListPtr drawList, StackBoard board, Rect area, float camera, float baseline,
        float levelHeight, float scale)
    {
        for (var index = 0; index < board.SliceCount; index++)
        {
            var slice = board.Slice(index);
            var center = new Vector2(area.Min.X + slice.CenterX * area.Width,
                baseline + (camera - slice.Level - 0.5f) * levelHeight);
            var halfWidth = slice.Width * 0.5f * area.Width;
            var halfHeight = levelHeight * 0.5f;
            var color = ColorOf(slice.ColorLevel);
            var fade = MathF.Min(1f, slice.Life * 1.4f);
            var right = new Vector2(MathF.Cos(slice.Rotation), MathF.Sin(slice.Rotation));
            var up = new Vector2(-right.Y, right.X);
            var extentX = right * halfWidth;
            var extentY = up * halfHeight;
            drawList.AddQuadFilled(center - extentX - extentY, center + extentX - extentY, center + extentX + extentY,
                center - extentX + extentY, ImGui.GetColorU32(GamePalette.Darken(color, 0.16f) with { W = fade }));
            drawList.AddLine(center - extentX - extentY, center + extentX - extentY,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.25f * fade)), MathF.Max(1f, scale));
        }
    }
}
