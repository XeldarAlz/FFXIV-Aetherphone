using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Breakout;

internal sealed class BreakoutRenderer
{
    private static readonly Vector4[] BrickColors =
    {
        new(0.95f, 0.45f, 0.50f, 1f), new(0.96f, 0.62f, 0.32f, 1f), new(0.92f, 0.82f, 0.36f, 1f),
        new(0.46f, 0.86f, 0.62f, 1f), new(0.40f, 0.70f, 0.98f, 1f), new(0.72f, 0.50f, 0.96f, 1f),
    };

    public static Vector4 BrickColorOf(int color) => BrickColors[color % BrickColors.Length];

    public void Draw(BreakoutBoard board, Rect field, Vector4 accent, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        drawList.PushClipRect(field.Min, field.Max, true);
        var factor = field.Width;
        DrawBricks(drawList, board, field, factor, scale);
        DrawPowerUps(drawList, board, field, factor, scale);
        DrawPaddle(drawList, board, field, factor, accent, scale);
        DrawBalls(drawList, board, field, factor, accent);
        drawList.PopClipRect();
    }

    private static void DrawBricks(ImDrawListPtr drawList, BreakoutBoard board, Rect field, float factor, float scale)
    {
        for (var row = 0; row < board.Rows; row++)
        {
            for (var column = 0; column < BreakoutBoard.Columns; column++)
            {
                if (!board.BrickAlive(column, row))
                {
                    continue;
                }

                var center = field.Min + board.BrickCenter(column, row) * factor;
                var halfWidth = board.BrickWidth * 0.5f * factor;
                var halfHeight = BreakoutBoard.BrickHeight * 0.5f * factor;
                var min = new Vector2(center.X - halfWidth, center.Y - halfHeight);
                var max = new Vector2(center.X + halfWidth, center.Y + halfHeight);
                var color = BrickColorOf(board.BrickColor(column, row));
                var rounding = halfHeight * 0.5f;
                Squircle.FillVerticalGradient(drawList, min, max, rounding,
                    ImGui.GetColorU32(GamePalette.Lighten(color, 0.18f)),
                    ImGui.GetColorU32(GamePalette.Darken(color, 0.22f)));
                drawList.AddLine(new Vector2(min.X + rounding, min.Y + 1f * scale),
                    new Vector2(max.X - rounding, min.Y + 1f * scale),
                    ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.35f)), 1f * scale);
                Squircle.Stroke(drawList, min, max, rounding,
                    ImGui.GetColorU32(GamePalette.Darken(color, 0.4f) with { W = 0.5f }), 1f * scale);
            }
        }
    }

    private static void DrawPowerUps(ImDrawListPtr drawList, BreakoutBoard board, Rect field, float factor, float scale)
    {
        for (var index = 0; index < board.PowerUpCount; index++)
        {
            var power = board.GetPowerUp(index);
            var center = field.Min + power.Position * factor;
            var radius = 0.026f * factor;
            var color = power.Kind == PowerUpKind.MultiBall
                ? new Vector4(0.46f, 0.86f, 0.66f, 1f)
                : new Vector4(0.96f, 0.74f, 0.34f, 1f);
            var bob = MathF.Sin((float)ImGui.GetTime() * 6f + index) * radius * 0.15f;
            var bobbed = center + new Vector2(0f, bob);
            ProgressRing.Glow(bobbed, radius, color, 0.7f);
            drawList.AddCircleFilled(bobbed, radius, ImGui.GetColorU32(color));
            drawList.AddCircleFilled(bobbed - new Vector2(radius * 0.3f, radius * 0.3f), radius * 0.3f,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.5f)));
            var glyph = power.Kind == PowerUpKind.MultiBall ? "+" : "W";
            Typography.DrawCentered(bobbed, glyph, new Vector4(0.1f, 0.12f, 0.14f, 1f), radius / (9f * scale),
                FontWeight.Bold);
        }
    }

    private static void DrawPaddle(ImDrawListPtr drawList, BreakoutBoard board, Rect field, float factor, Vector4 accent,
        float scale)
    {
        var paddleCenter = field.Min + new Vector2(board.PaddleX, board.PaddleY) * factor;
        var paddleHalf = new Vector2(board.PaddleHalfWidth * factor, BreakoutBoard.PaddleHeight * 0.5f * factor);
        var paddleMin = paddleCenter - paddleHalf;
        var paddleMax = paddleCenter + paddleHalf;
        Elevation.Card(drawList, paddleMin, paddleMax, paddleHalf.Y, scale, 0.7f);
        Squircle.FillVerticalGradient(drawList, paddleMin, paddleMax, paddleHalf.Y,
            ImGui.GetColorU32(GamePalette.Lighten(accent, 0.22f)), ImGui.GetColorU32(GamePalette.Darken(accent, 0.18f)));
        drawList.AddLine(new Vector2(paddleMin.X + paddleHalf.Y, paddleMin.Y + 1f * scale),
            new Vector2(paddleMax.X - paddleHalf.Y, paddleMin.Y + 1f * scale),
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.4f)), 1f * scale);
        ProgressRing.Glow(paddleCenter, paddleHalf.X * 0.5f, accent, 0.25f);
    }

    private static void DrawBalls(ImDrawListPtr drawList, BreakoutBoard board, Rect field, float factor, Vector4 accent)
    {
        for (var index = 0; index < board.BallCount; index++)
        {
            var ball = board.GetBall(index);
            var center = field.Min + ball.Position * factor;
            var radius = BreakoutBoard.BallRadius * factor;
            var speed = ball.Velocity.Length();
            if (!board.Attached && speed > 0.01f)
            {
                var direction = ball.Velocity / speed;
                for (var ghost = 1; ghost <= 3; ghost++)
                {
                    var ghostCenter = center - direction * radius * 1.5f * ghost * factor * 0.01f
                        - direction * radius * ghost * 0.9f;
                    var ghostAlpha = 0.30f - ghost * 0.08f;
                    drawList.AddCircleFilled(ghostCenter, radius * (1f - ghost * 0.18f),
                        ImGui.GetColorU32(GamePalette.Lighten(accent, 0.4f) with { W = ghostAlpha }));
                }
            }

            ProgressRing.Glow(center, radius, GamePalette.Lighten(accent, 0.4f), 0.7f);
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(new Vector4(0.99f, 0.99f, 1f, 1f)));
            drawList.AddCircleFilled(center - new Vector2(radius * 0.3f, radius * 0.3f), radius * 0.32f,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.85f)));
        }
    }
}
