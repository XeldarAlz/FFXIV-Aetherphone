using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core.Animation;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Snake;

internal sealed class SnakeRenderer
{
    private const float GridSpacing = 34f;
    private static readonly Vector4 HeadColor = new(0.42f, 0.84f, 0.48f, 1f);
    private static readonly Vector4 TailColor = new(0.20f, 0.52f, 0.32f, 1f);
    private static readonly Vector4 FoodColor = new(0.96f, 0.42f, 0.44f, 1f);
    private static readonly Vector4 LeafColor = new(0.44f, 0.78f, 0.42f, 1f);

    public void Draw(SnakeBoard board, Rect area, float scale, Vector2 shake, float eatPulse)
    {
        var drawList = ImGui.GetWindowDrawList();
        var shaken = new Rect(area.Min + shake, area.Max + shake);
        GameScene.Arena(drawList, shaken, 16f * scale, scale, HeadColor);
        drawList.PushClipRect(shaken.Min, shaken.Max, true);
        DrawGrid(drawList, shaken, scale);
        DrawFood(drawList, board, area, shake);
        DrawBody(drawList, board, area, shake, eatPulse);
        drawList.PopClipRect();
    }

    private static void DrawGrid(ImDrawListPtr drawList, Rect area, float scale)
    {
        var spacing = GridSpacing * scale;
        var color = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.028f));
        for (var x = area.Min.X + spacing; x < area.Max.X; x += spacing)
        {
            drawList.AddLine(new Vector2(x, area.Min.Y), new Vector2(x, area.Max.Y), color, 1f);
        }

        for (var y = area.Min.Y + spacing; y < area.Max.Y; y += spacing)
        {
            drawList.AddLine(new Vector2(area.Min.X, y), new Vector2(area.Max.X, y), color, 1f);
        }
    }

    private void DrawFood(ImDrawListPtr drawList, SnakeBoard board, Rect area, Vector2 shake)
    {
        var center = board.Food + shake;
        var radius = SnakeBoard.FoodRadiusOf(area);
        var pulse = 0.85f + 0.15f * Pulse.Wave(Pulse.Fast);
        ProgressRing.Glow(center, radius * 2f, FoodColor, 0.7f);
        drawList.AddCircleFilled(center, radius * pulse, ImGui.GetColorU32(FoodColor), 24);
        drawList.AddCircleFilled(center - new Vector2(radius * 0.3f, radius * 0.3f), radius * 0.34f,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.6f)), 16);
        var leafBase = center + new Vector2(radius * 0.15f, -radius * pulse);
        drawList.AddTriangleFilled(leafBase, leafBase + new Vector2(radius * 0.55f, -radius * 0.65f),
            leafBase + new Vector2(radius * 0.05f, -radius * 0.5f), ImGui.GetColorU32(LeafColor));
        var sparkleAngle = (float)ImGui.GetTime() * 1.8f;
        var sparkleArm = radius * 1.7f;
        var sparkleCenter = center + new Vector2(MathF.Cos(sparkleAngle), MathF.Sin(sparkleAngle)) * sparkleArm;
        drawList.AddCircleFilled(sparkleCenter, radius * 0.16f, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.5f)));
    }

    private void DrawBody(ImDrawListPtr drawList, SnakeBoard board, Rect area, Vector2 shake, float eatPulse)
    {
        var radius = SnakeBoard.SegRadiusOf(area);
        var count = board.SampleCount;
        if (count == 0)
        {
            return;
        }

        var outline = ImGui.GetColorU32(GamePalette.Darken(TailColor, 0.55f));
        for (var index = 0; index < count; index++)
        {
            var fraction = count > 1 ? index / (float)(count - 1) : 1f;
            var segRadius = radius * (0.6f + 0.4f * fraction);
            drawList.AddCircleFilled(board.Sample(index) + shake, segRadius + 1.5f, outline, 20);
        }

        for (var index = 0; index < count; index++)
        {
            var fraction = count > 1 ? index / (float)(count - 1) : 1f;
            var color = Vector4.Lerp(TailColor, HeadColor, fraction);
            var segRadius = radius * (0.6f + 0.4f * fraction);
            var position = board.Sample(index) + shake;
            drawList.AddCircleFilled(position, segRadius, ImGui.GetColorU32(color), 20);
            drawList.AddCircleFilled(position - new Vector2(segRadius * 0.25f, segRadius * 0.3f), segRadius * 0.32f,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f + 0.08f * fraction)), 12);
        }

        DrawHead(drawList, board, radius, shake, eatPulse);
    }

    private void DrawHead(ImDrawListPtr drawList, SnakeBoard board, float radius, Vector2 shake, float eatPulse)
    {
        var head = board.Head + shake;
        var headRadius = radius * (1.18f + 0.30f * eatPulse);
        ProgressRing.Glow(head, headRadius * 1.3f, HeadColor, 0.35f + 0.5f * eatPulse);
        drawList.AddCircleFilled(head, headRadius, ImGui.GetColorU32(HeadColor), 24);
        drawList.AddCircle(head, headRadius, ImGui.GetColorU32(GamePalette.Darken(HeadColor, 0.3f)), 24, 1.4f);
        var forward = new Vector2(MathF.Cos(board.Angle), MathF.Sin(board.Angle));
        var side = new Vector2(-forward.Y, forward.X);
        var eyeBase = head + forward * headRadius * 0.4f;
        var leftEye = eyeBase + side * headRadius * 0.42f;
        var rightEye = eyeBase - side * headRadius * 0.42f;
        var white = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f));
        var pupil = ImGui.GetColorU32(new Vector4(0.1f, 0.12f, 0.12f, 1f));
        drawList.AddCircleFilled(leftEye, headRadius * 0.3f, white, 14);
        drawList.AddCircleFilled(rightEye, headRadius * 0.3f, white, 14);
        drawList.AddCircleFilled(leftEye + forward * headRadius * 0.1f, headRadius * 0.15f, pupil, 10);
        drawList.AddCircleFilled(rightEye + forward * headRadius * 0.1f, headRadius * 0.15f, pupil, 10);
    }
}
