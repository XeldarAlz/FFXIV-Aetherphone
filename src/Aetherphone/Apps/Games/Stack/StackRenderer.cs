using Aetherphone.Apps.Games.Framework;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Stack;

internal static class StackRenderer
{
    public const float WorldWidth = 7f;
    private const float GuideHeight = 1.6f;
    private static readonly Vector4 Guide = new(1f, 1f, 1f, 0.14f);
    private static readonly Vector4 SheenColor = new(1f, 1f, 1f, 0.34f);
    private static readonly Vector4 SliceEdge = new(1f, 1f, 1f, 0.25f);
    private static readonly Vector4[] Ramp =
    {
        new(0.36f, 0.72f, 0.98f, 1f), new(0.44f, 0.86f, 0.78f, 1f), new(0.58f, 0.88f, 0.48f, 1f),
        new(0.96f, 0.82f, 0.38f, 1f), new(0.97f, 0.56f, 0.36f, 1f), new(0.94f, 0.42f, 0.58f, 1f),
        new(0.72f, 0.48f, 0.96f, 1f),
    };

    public static Vector4 ColorOf(int level)
    {
        var position = level * 0.13f % Ramp.Length;
        var index = (int)position;
        var next = (index + 1) % Ramp.Length;
        return Vector4.Lerp(Ramp[index], Ramp[next], position - index);
    }

    public static Vector2 World(float centerX, float level) => new(centerX * WorldWidth, -level);

    public static Vector2 BlockCenter(in StackBlock block, int level) => World(block.CenterX, level + 0.5f);

    public static void Draw(ImDrawListPtr drawList, in Camera2D camera, StackBoard board, float heat, float scale)
    {
        drawList.PushClipRect(camera.View.Min, camera.View.Max, true);
        var lowest = Math.Max(0, board.Level - StackBoard.VisibleLevels);
        for (var level = lowest; level < board.Level; level++)
        {
            var block = board.Block(level);
            DrawBlock(drawList, in camera, block.CenterX, block.Width, level, ColorOf(level + board.ColorOffset),
                scale, false, 0f);
        }

        DrawSlices(drawList, in camera, board, scale);
        if (board.State == StackState.Over)
        {
            drawList.PopClipRect();
            return;
        }

        DrawGuides(drawList, in camera, board, scale);
        DrawBlock(drawList, in camera, board.MovingCenterX, board.MovingWidth, board.Level,
            ColorOf(board.Level + board.ColorOffset), scale, true, heat);
        drawList.PopClipRect();
    }

    private static void DrawGuides(ImDrawListPtr drawList, in Camera2D camera, StackBoard board, float scale)
    {
        var below = board.Block(board.Level - 1);
        var leftTop = camera.ToScreen(World(below.CenterX - below.Width * 0.5f, board.Level + GuideHeight));
        var leftBottom = camera.ToScreen(World(below.CenterX - below.Width * 0.5f, board.Level));
        var rightTop = camera.ToScreen(World(below.CenterX + below.Width * 0.5f, board.Level + GuideHeight));
        var rightBottom = camera.ToScreen(World(below.CenterX + below.Width * 0.5f, board.Level));
        var color = ImGui.GetColorU32(Guide);
        var thickness = MathF.Max(1f, scale);
        drawList.AddLine(leftTop, leftBottom, color, thickness);
        drawList.AddLine(rightTop, rightBottom, color, thickness);
    }

    private static void DrawBlock(ImDrawListPtr drawList, in Camera2D camera, float centerX, float width, int level,
        Vector4 color, float scale, bool lifted, float heat)
    {
        var min = camera.ToScreen(World(centerX - width * 0.5f, level + 1));
        var max = camera.ToScreen(World(centerX + width * 0.5f, level));
        var levelHeight = max.Y - min.Y;
        var halfWidth = (max.X - min.X) * 0.5f;
        var center = new Vector2((min.X + max.X) * 0.5f, (min.Y + max.Y) * 0.5f);
        var rounding = MathF.Min(levelHeight * 0.32f, halfWidth * 0.5f);
        if (lifted)
        {
            Elevation.Draw(drawList, min, max, rounding, scale, 16f, 7f, 0.34f);
            ProgressRing.Glow(center, halfWidth * (0.6f + 0.5f * heat), color, 0.35f + 0.45f * heat);
        }

        Squircle.FillVerticalGradient(drawList, min, max, rounding,
            ImGui.GetColorU32(GamePalette.Lighten(color, 0.22f)), ImGui.GetColorU32(GamePalette.Darken(color, 0.26f)));
        Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(GamePalette.Darken(color, 0.45f) with { W = 0.6f }),
            MathF.Max(1f, scale));
        Material.Sheen(drawList, min, max, rounding, ImGui.GetColorU32(SheenColor), MathF.Max(1f, scale), 1.5f * scale);
    }

    private static void DrawSlices(ImDrawListPtr drawList, in Camera2D camera, StackBoard board, float scale)
    {
        for (var index = 0; index < board.SliceCount; index++)
        {
            var slice = board.Slice(index);
            var center = camera.ToScreen(World(slice.CenterX, slice.Level + 0.5f));
            var halfWidth = camera.Px(slice.Width * WorldWidth * 0.5f);
            var halfHeight = camera.Px(0.5f);
            var color = ColorOf(slice.ColorLevel + board.ColorOffset);
            var fade = MathF.Min(1f, slice.Life * 1.4f);
            var right = new Vector2(MathF.Cos(slice.Rotation), MathF.Sin(slice.Rotation));
            var up = new Vector2(-right.Y, right.X);
            var extentX = right * halfWidth;
            var extentY = up * halfHeight;
            drawList.AddQuadFilled(center - extentX - extentY, center + extentX - extentY, center + extentX + extentY,
                center - extentX + extentY, ImGui.GetColorU32(GamePalette.Darken(color, 0.16f) with { W = fade }));
            drawList.AddLine(center - extentX - extentY, center + extentX - extentY,
                ImGui.GetColorU32(SliceEdge with { W = SliceEdge.W * fade }), MathF.Max(1f, scale));
        }
    }
}
