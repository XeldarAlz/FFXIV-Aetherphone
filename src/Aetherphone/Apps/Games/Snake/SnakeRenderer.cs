using System.Numerics;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Snake;

internal sealed class SnakeRenderer
{
    private static readonly Vector4 Arena = new(0.10f, 0.13f, 0.12f, 1f);
    private static readonly Vector4 HeadColor = new(0.42f, 0.84f, 0.48f, 1f);
    private static readonly Vector4 TailColor = new(0.20f, 0.52f, 0.32f, 1f);
    private static readonly Vector4 FoodColor = new(0.96f, 0.42f, 0.44f, 1f);

    public void Draw(SnakeBoard board, Rect area, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, area.Min, area.Max, 16f * scale, ImGui.GetColorU32(Arena));
        Squircle.Stroke(drawList, area.Min, area.Max, 16f * scale, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.06f)),
            1.4f * scale);
        DrawFood(drawList, board, area);
        DrawBody(drawList, board, area);
    }

    private void DrawFood(ImDrawListPtr drawList, SnakeBoard board, Rect area)
    {
        var radius = SnakeBoard.FoodRadiusOf(area);
        var pulse = 0.85f + 0.15f * Styling.Pulse(Styling.PulseFast);
        ProgressRing.Glow(board.Food, radius * 2f, FoodColor, 0.7f);
        drawList.AddCircleFilled(board.Food, radius * pulse, ImGui.GetColorU32(FoodColor), 24);
        drawList.AddCircleFilled(board.Food - new Vector2(radius * 0.3f, radius * 0.3f), radius * 0.34f,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.6f)), 16);
    }

    private void DrawBody(ImDrawListPtr drawList, SnakeBoard board, Rect area)
    {
        var radius = SnakeBoard.SegRadiusOf(area);
        var count = board.SampleCount;
        if (count == 0)
        {
            return;
        }

        for (var index = 0; index < count; index++)
        {
            var fraction = count > 1 ? index / (float)(count - 1) : 1f;
            var color = Vector4.Lerp(TailColor, HeadColor, fraction);
            var segRadius = radius * (0.6f + 0.4f * fraction);
            drawList.AddCircleFilled(board.Sample(index), segRadius, ImGui.GetColorU32(color), 20);
        }

        DrawHead(drawList, board, radius);
    }

    private void DrawHead(ImDrawListPtr drawList, SnakeBoard board, float radius)
    {
        var head = board.Head;
        var headRadius = radius * 1.18f;
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
