using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.BubbleShooter;

internal sealed class BubbleRenderer
{
    private static readonly Vector4[] BubbleColors =
    {
        new(0.95f, 0.45f, 0.50f, 1f),
        new(0.40f, 0.70f, 0.98f, 1f),
        new(0.46f, 0.86f, 0.62f, 1f),
        new(0.95f, 0.78f, 0.34f, 1f),
        new(0.72f, 0.50f, 0.96f, 1f),
    };

    public static Vector4 ColorOf(int color) => BubbleColors[color % BubbleColors.Length];

    public void Draw(BubbleBoard board, Rect field, float scale, Vector2 aimDirection, PhoneTheme theme)
    {
        var drawList = ImGui.GetWindowDrawList();
        var factor = field.Width;
        var radius = board.Radius * factor;

        var dangerY = field.Min.Y + board.DangerY * factor;
        drawList.AddLine(new Vector2(field.Min.X, dangerY), new Vector2(field.Max.X, dangerY), ImGui.GetColorU32(new Vector4(0.95f, 0.35f, 0.35f, 0.35f)), 1.4f * scale);

        for (var row = 0; row < BubbleBoard.MaxRows; row++)
        {
            for (var column = 0; column < BubbleBoard.Columns; column++)
            {
                var color = board.ColorAt(column, row);
                if (color < 0)
                {
                    continue;
                }

                DrawBubble(drawList, field.Min + board.CellCenter(column, row) * factor, radius, ColorOf(color));
            }
        }

        if (!board.Flying && !board.GameOver && aimDirection != Vector2.Zero)
        {
            DrawAim(drawList, board, field, factor, radius, aimDirection);
        }

        var launcher = field.Min + board.LauncherPosition * factor;
        ProgressRing.Glow(launcher, radius * 1.2f, ColorOf(board.CurrentColor), 0.5f);
        DrawBubble(drawList, launcher, radius * 1.1f, ColorOf(board.CurrentColor));

        var nextCenter = new Vector2(field.Min.X + radius * 1.4f, launcher.Y);
        DrawBubble(drawList, nextCenter, radius * 0.7f, ColorOf(board.NextColor));

        if (board.Flying)
        {
            DrawBubble(drawList, field.Min + board.FlyPosition * factor, radius, ColorOf(board.FlyColor));
        }
    }

    private void DrawBubble(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 color)
    {
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(color));
        drawList.AddCircleFilled(center - new Vector2(radius * 0.3f, radius * 0.3f), radius * 0.32f, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.4f)));
        drawList.AddCircle(center, radius, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.12f)), 0, 1.2f);
    }

    private void DrawAim(ImDrawListPtr drawList, BubbleBoard board, Rect field, float factor, float radius, Vector2 direction)
    {
        var position = board.LauncherPosition;
        var step = Vector2.Normalize(direction) * 0.02f;
        var lineColor = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.32f));

        for (var iteration = 0; iteration < 120; iteration++)
        {
            position += step;
            if (position.X < board.Radius)
            {
                position.X = board.Radius;
                step.X = MathF.Abs(step.X);
            }
            else if (position.X > 1f - board.Radius)
            {
                position.X = 1f - board.Radius;
                step.X = -MathF.Abs(step.X);
            }

            if (position.Y - board.Radius <= 0f)
            {
                break;
            }

            if (iteration % 3 == 0)
            {
                drawList.AddCircleFilled(field.Min + position * factor, 2f, lineColor);
            }
        }
    }
}
