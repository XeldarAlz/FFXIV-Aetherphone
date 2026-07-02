using System.Numerics;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Tetris;

internal sealed class TetrisRenderer
{
    private static readonly Vector4[] PieceColors =
    {
        new(0.40f, 0.82f, 0.98f, 1f),
        new(0.95f, 0.84f, 0.36f, 1f),
        new(0.72f, 0.52f, 0.98f, 1f),
        new(0.96f, 0.62f, 0.32f, 1f),
        new(0.42f, 0.70f, 0.98f, 1f),
        new(0.50f, 0.86f, 0.58f, 1f),
        new(0.95f, 0.48f, 0.52f, 1f),
    };

    private static readonly (int X, int Y)[] DrawCells =
    {
        (0, 0),
        (0, 0),
        (0, 0),
        (0, 0),
    };

    public void Draw(TetrisBoard board, GameGrid grid, Vector4 accent, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();

        Elevation.Card(drawList, grid.Bounds.Min, grid.Bounds.Max, 18f * scale, scale, 0.8f);
        Squircle.Fill(drawList, grid.Bounds.Min, grid.Bounds.Max, 18f * scale, ImGui.GetColorU32(new Vector4(0.08f, 0.09f, 0.12f, 1f)));
        Squircle.Fill(drawList, grid.Bounds.Min, new Vector2(grid.Bounds.Max.X, grid.Bounds.Center.Y), 18f * scale, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.04f)));

        DrawGridLines(drawList, grid, scale);

        for (var row = 0; row < TetrisBoard.Rows; row++)
        {
            for (var column = 0; column < TetrisBoard.Columns; column++)
            {
                var colorIndex = board.CellColor(column, row);
                if (colorIndex == 0)
                {
                    continue;
                }

                DrawCell(drawList, grid, column, row, PieceColorOf(colorIndex - 1), 1f, scale);
            }
        }

        if (!board.HasActivePiece)
        {
            return;
        }

        var ghostY = board.GetGhostY();
        DrawActivePiece(drawList, board, grid, ghostY, 0.18f, scale);
        DrawActivePiece(drawList, board, grid, board.ActiveY, 1f, scale);
    }

    public bool DrawHoldSlot(TetrisBoard board, Rect rect, PhoneTheme theme, Vector4 accent, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = ImGui.IsMouseHoveringRect(rect.Min, rect.Max);

        Elevation.Floating(drawList, rect.Min, rect.Max, 18f * scale, scale, hovered ? 1f : 0.8f);
        Squircle.Fill(drawList, rect.Min, rect.Max, 18f * scale, ImGui.GetColorU32(new Vector4(0.08f, 0.09f, 0.12f, 1f)));
        Squircle.Stroke(drawList, rect.Min, rect.Max, 18f * scale, ImGui.GetColorU32(GamePalette.Lighten(accent, 0.32f) with { W = hovered ? 0.75f : 0.35f }), 1f * scale);

        Typography.DrawCentered(new Vector2(rect.Center.X, rect.Min.Y + 12f * scale), Loc.T(L.Games.Saved), theme.TextMuted, TextStyles.Caption2);

        if (board.HeldKind is { } heldKind)
        {
            DrawPiecePreview(drawList, rect, heldKind, accent, scale);
        }
        else
        {
            Typography.DrawCentered(rect.Center, "-", theme.TextMuted, TextStyles.Headline);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    public void DrawNextSlot(TetrisBoard board, Rect rect, PhoneTheme theme, Vector4 accent, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();

        Elevation.Floating(drawList, rect.Min, rect.Max, 18f * scale, scale, 0.8f);
        Squircle.Fill(drawList, rect.Min, rect.Max, 18f * scale, ImGui.GetColorU32(new Vector4(0.08f, 0.09f, 0.12f, 1f)));
        Squircle.Stroke(drawList, rect.Min, rect.Max, 18f * scale, ImGui.GetColorU32(GamePalette.Lighten(accent, 0.32f) with { W = 0.35f }), 1f * scale);

        Typography.DrawCentered(new Vector2(rect.Center.X, rect.Min.Y + 12f * scale), "Next", theme.TextMuted, TextStyles.Caption2);

        DrawPiecePreview(drawList, rect, board.NextPieceKind, accent, scale);
    }

    private static void DrawGridLines(ImDrawListPtr drawList, GameGrid grid, float scale)
    {
        var lineColor = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.06f));
        for (var column = 0; column <= TetrisBoard.Columns; column++)
        {
            var x = grid.Origin.X + column * grid.Pitch;
            drawList.AddLine(new Vector2(x, grid.Origin.Y), new Vector2(x, grid.Origin.Y + grid.Height), lineColor, 1f * scale);
        }

        for (var row = 0; row <= TetrisBoard.Rows; row++)
        {
            var y = grid.Origin.Y + row * grid.Pitch;
            drawList.AddLine(new Vector2(grid.Origin.X, y), new Vector2(grid.Origin.X + grid.Width, y), lineColor, 1f * scale);
        }
    }

    private static void DrawActivePiece(ImDrawListPtr drawList, TetrisBoard board, GameGrid grid, int boardY, float alpha, float scale)
    {
        var tint = GamePalette.Lighten(PieceColorOf((int)board.ActiveKind), 0.12f);
        DrawCells[0] = board.GetCell(0);
        DrawCells[1] = board.GetCell(1);
        DrawCells[2] = board.GetCell(2);
        DrawCells[3] = board.GetCell(3);

        for (var index = 0; index < DrawCells.Length; index++)
        {
            var cell = DrawCells[index];
            DrawCell(drawList, grid, board.ActiveX + cell.X, boardY + cell.Y, tint, alpha, scale);
        }
    }

    private static void DrawCell(ImDrawListPtr drawList, GameGrid grid, int column, int row, Vector4 color, float alpha, float scale)
    {
        var rect = grid.Cell(column, row);
        var rounding = MathF.Max(2f * scale, rect.Height * 0.2f);
        var fill = color with { W = color.W * alpha };
        Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(fill));
        Squircle.Fill(drawList, rect.Min, new Vector2(rect.Max.X, rect.Center.Y), rounding, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.18f * alpha)));
    }

    private static void DrawPiecePreview(ImDrawListPtr drawList, Rect rect, TetrisPieceKind kind, Vector4 accent, float scale)
    {
        var cells = TetrisBoard.GetCells(kind, 0);
        var minX = cells[0].X;
        var minY = cells[0].Y;
        var maxX = cells[0].X;
        var maxY = cells[0].Y;

        for (var index = 1; index < cells.Length; index++)
        {
            var cell = cells[index];
            minX = Math.Min(minX, cell.X);
            minY = Math.Min(minY, cell.Y);
            maxX = Math.Max(maxX, cell.X);
            maxY = Math.Max(maxY, cell.Y);
        }

        var pieceWidth = maxX - minX + 1;
        var pieceHeight = maxY - minY + 1;
        var availableWidth = rect.Width - 18f * scale;
        var availableHeight = rect.Height - 34f * scale;
        var cellSize = MathF.Min(availableWidth / 4f, availableHeight / 4f);
        var previewWidth = cellSize * 4f;
        var previewHeight = cellSize * 4f;
        var origin = new Vector2(
            rect.Center.X - previewWidth * 0.5f,
            rect.Min.Y + 22f * scale + MathF.Max(0f, (availableHeight - previewHeight) * 0.5f));

        var tint = GamePalette.Lighten(PieceColorOf((int)kind), 0.12f);
        var offsetX = (4 - pieceWidth) * 0.5f - minX;
        var offsetY = (4 - pieceHeight) * 0.5f - minY;

        for (var index = 0; index < cells.Length; index++)
        {
            var cell = cells[index];
            var min = origin + new Vector2((cell.X + offsetX) * cellSize, (cell.Y + offsetY) * cellSize);
            var max = min + new Vector2(cellSize - 2f * scale, cellSize - 2f * scale);
            var rounding = MathF.Max(2f * scale, (max.Y - min.Y) * 0.2f);
            Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(tint));
            Squircle.Fill(drawList, min, new Vector2(max.X, min.Y + (max.Y - min.Y) * 0.5f), rounding, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.18f)));
        }
    }

    private static Vector4 PieceColorOf(int index) => PieceColors[index % PieceColors.Length];
}