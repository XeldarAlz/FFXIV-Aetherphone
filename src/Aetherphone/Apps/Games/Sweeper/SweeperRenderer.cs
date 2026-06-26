using System.Numerics;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Sweeper;

internal sealed class SweeperRenderer
{
    private static readonly Vector4[] NumberColors =
    {
        new(0.40f, 0.68f, 0.98f, 1f),
        new(0.46f, 0.86f, 0.66f, 1f),
        new(0.95f, 0.45f, 0.50f, 1f),
        new(0.75f, 0.50f, 0.95f, 1f),
        new(0.95f, 0.62f, 0.30f, 1f),
        new(0.40f, 0.78f, 0.82f, 1f),
        new(0.86f, 0.86f, 0.90f, 1f),
        new(0.62f, 0.62f, 0.68f, 1f),
    };

    private static readonly Vector4 MineColor = new(0.93f, 0.42f, 0.50f, 1f);

    public void Draw(SweeperBoard board, GameGrid grid, int hoveredIndex, float[] flagAnim, PhoneTheme theme, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rounding = 4f * scale;

        for (var row = 0; row < board.Rows; row++)
        {
            for (var column = 0; column < board.Columns; column++)
            {
                var index = row * board.Columns + column;
                var cell = grid.Cell(column, row);

                if (board.IsRevealed(index))
                {
                    DrawRevealed(drawList, board, index, cell, rounding, theme, scale);
                }
                else
                {
                    DrawCovered(drawList, board, index, cell, rounding, index == hoveredIndex, flagAnim[index], theme, scale);
                }
            }
        }
    }

    private void DrawRevealed(ImDrawListPtr drawList, SweeperBoard board, int index, Rect cell, float rounding, PhoneTheme theme, float scale)
    {
        if (board.IsMine(index))
        {
            var detonated = index == board.ClickedBomb;
            var fill = detonated ? new Vector4(1f, 0.32f, 0.32f, 1f) : MineColor;
            Squircle.Fill(drawList, cell.Min, cell.Max, rounding, ImGui.GetColorU32(fill));
            DrawMine(drawList, cell.Center, cell.Width * 0.22f, GamePalette.InkOn(fill));
            return;
        }

        Squircle.Fill(drawList, cell.Min, cell.Max, rounding, ImGui.GetColorU32(GamePalette.CellSunken));
        var adjacent = board.Adjacent(index);
        if (adjacent > 0)
        {
            Typography.DrawCentered(cell.Center, GameNumber.Label(adjacent), NumberColors[adjacent - 1], 1.2f, FontWeight.Bold);
        }
    }

    private void DrawCovered(ImDrawListPtr drawList, SweeperBoard board, int index, Rect cell, float rounding, bool hovered, float flagPop, PhoneTheme theme, float scale)
    {
        var fill = hovered ? GamePalette.CellHover : GamePalette.Cell;
        Squircle.Fill(drawList, cell.Min, cell.Max, rounding, ImGui.GetColorU32(fill));
        Squircle.Fill(drawList, cell.Min, new Vector2(cell.Max.X, cell.Min.Y + cell.Height * 0.5f), rounding, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.045f)));
        Squircle.Stroke(drawList, cell.Min, cell.Max, rounding, ImGui.GetColorU32(Styling.BorderDim), 1f * scale);

        if (board.IsFlagged(index))
        {
            var pop = 1f + flagPop * 0.25f;
            DrawFlag(drawList, cell.Center, cell.Width * 0.26f * pop, theme.Accent);
        }
    }

    private void DrawMine(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 color)
    {
        var packed = ImGui.GetColorU32(color);
        for (var spoke = 0; spoke < 4; spoke++)
        {
            var angle = spoke * MathF.PI / 4f;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius * 1.5f;
            drawList.AddLine(center - direction, center + direction, packed, radius * 0.35f);
        }

        drawList.AddCircleFilled(center, radius, packed);
        drawList.AddCircleFilled(center - new Vector2(radius * 0.3f, radius * 0.3f), radius * 0.32f, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.55f)));
    }

    private void DrawFlag(ImDrawListPtr drawList, Vector2 center, float size, Vector4 accent)
    {
        var poleTop = new Vector2(center.X - size * 0.35f, center.Y - size);
        var poleBottom = new Vector2(center.X - size * 0.35f, center.Y + size);
        drawList.AddLine(poleTop, poleBottom, ImGui.GetColorU32(new Vector4(0.85f, 0.85f, 0.88f, 1f)), MathF.Max(1.5f, size * 0.18f));

        var flagTip = new Vector2(center.X + size * 0.75f, center.Y - size * 0.45f);
        drawList.AddTriangleFilled(poleTop, new Vector2(poleTop.X, center.Y), flagTip, ImGui.GetColorU32(accent));
    }
}
