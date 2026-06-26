using System.Numerics;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Breakout;

internal sealed class BreakoutRenderer
{
    private static readonly Vector4[] BrickColors =
    {
        new(0.95f, 0.45f, 0.50f, 1f),
        new(0.96f, 0.62f, 0.32f, 1f),
        new(0.92f, 0.82f, 0.36f, 1f),
        new(0.46f, 0.86f, 0.62f, 1f),
        new(0.40f, 0.70f, 0.98f, 1f),
        new(0.72f, 0.50f, 0.96f, 1f),
    };

    public static Vector4 BrickColorOf(int color) => BrickColors[color % BrickColors.Length];

    public void Draw(BreakoutBoard board, Rect field, Vector4 accent, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var factor = field.Width;

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

                Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(color));
                Squircle.Fill(drawList, min, new Vector2(max.X, center.Y), rounding, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.16f)));
            }
        }

        for (var index = 0; index < board.PowerUpCount; index++)
        {
            var power = board.GetPowerUp(index);
            var center = field.Min + power.Position * factor;
            var radius = 0.026f * factor;
            var color = power.Kind == PowerUpKind.MultiBall ? new Vector4(0.46f, 0.86f, 0.66f, 1f) : new Vector4(0.96f, 0.74f, 0.34f, 1f);
            ProgressRing.Glow(center, radius, color, 0.7f);
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(color));
            var glyph = power.Kind == PowerUpKind.MultiBall ? "+" : "W";
            Typography.DrawCentered(center, glyph, new Vector4(0.1f, 0.12f, 0.14f, 1f), radius / (9f * scale), FontWeight.Bold);
        }

        var paddleCenter = field.Min + new Vector2(board.PaddleX, board.PaddleY) * factor;
        var paddleHalf = new Vector2(board.PaddleHalfWidth * factor, BreakoutBoard.PaddleHeight * 0.5f * factor);
        var paddleMin = paddleCenter - paddleHalf;
        var paddleMax = paddleCenter + paddleHalf;
        Elevation.Card(drawList, paddleMin, paddleMax, paddleHalf.Y, scale, 0.7f);
        Squircle.Fill(drawList, paddleMin, paddleMax, paddleHalf.Y, ImGui.GetColorU32(accent));
        Squircle.Fill(drawList, paddleMin, new Vector2(paddleMax.X, paddleCenter.Y), paddleHalf.Y, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.22f)));

        for (var index = 0; index < board.BallCount; index++)
        {
            var ball = board.GetBall(index);
            var center = field.Min + ball.Position * factor;
            var radius = BreakoutBoard.BallRadius * factor;
            ProgressRing.Glow(center, radius, GamePalette.Lighten(accent, 0.4f), 0.7f);
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(new Vector4(0.99f, 0.99f, 1f, 1f)));
        }
    }
}
