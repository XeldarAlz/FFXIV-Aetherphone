using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Sudoku;

internal readonly struct SudokuLayout
{
    public readonly Vector2 Origin;
    public readonly float CellSize;

    public SudokuLayout(Vector2 origin, float cellSize)
    {
        Origin = origin;
        CellSize = cellSize;
    }

    public float BoardSize => CellSize * SudokuBoard.Size;

    public Vector2 Center => Origin + new Vector2(BoardSize * 0.5f, BoardSize * 0.5f);

    public Rect Bounds => new(Origin, Origin + new Vector2(BoardSize, BoardSize));

    public Vector2 CellMin(int column, int row) =>
        new(Origin.X + column * CellSize, Origin.Y + row * CellSize);

    public Vector2 CellCenter(int cell) =>
        new(Origin.X + (SudokuBoard.ColumnOf(cell) + 0.5f) * CellSize,
            Origin.Y + (SudokuBoard.RowOf(cell) + 0.5f) * CellSize);

    public int HitTest(Vector2 point)
    {
        var column = (int)MathF.Floor((point.X - Origin.X) / CellSize);
        var row = (int)MathF.Floor((point.Y - Origin.Y) / CellSize);
        if (column < 0 || column >= SudokuBoard.Size || row < 0 || row >= SudokuBoard.Size)
        {
            return -1;
        }

        return row * SudokuBoard.Size + column;
    }
}

internal sealed class SudokuRenderer
{
    private static readonly Vector4 BoxWash = new(1f, 1f, 1f, 0.030f);
    private static readonly Vector4 PeerWash = new(1f, 1f, 1f, 0.055f);
    private static readonly Vector4 ThinLine = new(1f, 1f, 1f, 0.10f);
    private static readonly Vector4 BoldLine = new(1f, 1f, 1f, 0.32f);

    public static SudokuLayout Layout(Rect area, float scale)
    {
        var span = MathF.Min(area.Width, area.Height);
        var cellSize = span / SudokuBoard.Size;
        var origin = new Vector2(area.Center.X - span * 0.5f, area.Center.Y - span * 0.5f);
        return new SudokuLayout(origin, cellSize);
    }

    public void Draw(SudokuBoard board, in SudokuLayout layout, int selected, int hovered, float[] pop,
        PhoneTheme theme, Vector4 accent, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var cellSize = layout.CellSize;
        var boardMin = layout.Origin;
        var boardMax = boardMin + new Vector2(layout.BoardSize, layout.BoardSize);
        var rounding = Metrics.Radius.Md * scale;
        GameScene.Arena(drawList, new Rect(boardMin, boardMax), rounding, scale, accent);
        DrawBoxWash(drawList, layout, cellSize);
        var selectedDigit = selected >= 0 ? board.Value(selected) : (byte)0;
        DrawHighlights(drawList, board, layout, selected, hovered, selectedDigit, theme, accent, cellSize, scale);
        DrawGridLines(drawList, layout, boardMin, boardMax, cellSize, scale);
        var digitScale = MathF.Max(0.7f, MathF.Min(1.6f, cellSize / (26f * scale)));
        var noteScale = digitScale * 0.44f;
        for (var cell = 0; cell < SudokuBoard.CellCount; cell++)
        {
            DrawCellContent(board, layout, cell, pop[cell], theme, accent, digitScale, noteScale, cellSize);
        }
    }

    private void DrawBoxWash(ImDrawListPtr drawList, in SudokuLayout layout, float cellSize)
    {
        var wash = ImGui.GetColorU32(BoxWash);
        for (var boxRow = 0; boxRow < SudokuBoard.BoxSize; boxRow++)
        {
            for (var boxColumn = 0; boxColumn < SudokuBoard.BoxSize; boxColumn++)
            {
                if ((boxRow + boxColumn) % 2 != 0)
                {
                    continue;
                }

                var min = layout.CellMin(boxColumn * SudokuBoard.BoxSize, boxRow * SudokuBoard.BoxSize);
                var max = min + new Vector2(cellSize * SudokuBoard.BoxSize, cellSize * SudokuBoard.BoxSize);
                drawList.AddRectFilled(min, max, wash);
            }
        }
    }

    private void DrawHighlights(ImDrawListPtr drawList, SudokuBoard board, in SudokuLayout layout, int selected,
        int hovered, byte selectedDigit, PhoneTheme theme, Vector4 accent, float cellSize, float scale)
    {
        var peerWash = ImGui.GetColorU32(PeerWash);
        var digitWash = ImGui.GetColorU32(accent with { W = 0.17f });
        var conflictWash = ImGui.GetColorU32(theme.Danger with { W = 0.22f });
        var hoverWash = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.07f));
        var selectedWash = ImGui.GetColorU32(accent with { W = 0.32f });
        for (var cell = 0; cell < SudokuBoard.CellCount; cell++)
        {
            var min = layout.CellMin(SudokuBoard.ColumnOf(cell), SudokuBoard.RowOf(cell));
            var max = min + new Vector2(cellSize, cellSize);
            if (selected >= 0 && SudokuBoard.ArePeers(cell, selected))
            {
                drawList.AddRectFilled(min, max, peerWash);
            }

            if (selectedDigit != 0 && cell != selected && board.Value(cell) == selectedDigit)
            {
                drawList.AddRectFilled(min, max, digitWash);
            }

            if (board.IsConflicting(cell) || board.IsWrong(cell))
            {
                drawList.AddRectFilled(min, max, conflictWash);
            }

            if (cell == hovered && cell != selected)
            {
                drawList.AddRectFilled(min, max, hoverWash);
            }

            if (cell != selected)
            {
                continue;
            }

            drawList.AddRectFilled(min, max, selectedWash);
            drawList.AddRect(min, max, ImGui.GetColorU32(GamePalette.Lighten(accent, 0.35f)), 0f,
                ImDrawFlags.RoundCornersAll, 2f * scale);
        }
    }

    private void DrawGridLines(ImDrawListPtr drawList, in SudokuLayout layout, Vector2 boardMin, Vector2 boardMax,
        float cellSize, float scale)
    {
        var thin = ImGui.GetColorU32(ThinLine);
        var bold = ImGui.GetColorU32(BoldLine);
        for (var line = 1; line < SudokuBoard.Size; line++)
        {
            var heavy = line % SudokuBoard.BoxSize == 0;
            var color = heavy ? bold : thin;
            var thickness = (heavy ? 2f : 1f) * scale;
            var x = boardMin.X + line * cellSize;
            drawList.AddLine(new Vector2(x, boardMin.Y), new Vector2(x, boardMax.Y), color, thickness);
            var y = boardMin.Y + line * cellSize;
            drawList.AddLine(new Vector2(boardMin.X, y), new Vector2(boardMax.X, y), color, thickness);
        }
    }

    private void DrawCellContent(SudokuBoard board, in SudokuLayout layout, int cell, float pop, PhoneTheme theme,
        Vector4 accent, float digitScale, float noteScale, float cellSize)
    {
        var center = layout.CellCenter(cell);
        var value = board.Value(cell);
        if (value == 0)
        {
            DrawNotes(board.Notes(cell), center, theme, noteScale, cellSize);
            return;
        }

        var color = DigitColor(board, cell, theme, accent);
        var scalePop = 1f + 0.32f * Easing.EaseOutCubic(pop);
        Typography.DrawCentered(center, GameNumber.Label(value), color, digitScale * scalePop,
            board.IsGiven(cell) ? FontWeight.Bold : FontWeight.SemiBold);
    }

    private static Vector4 DigitColor(SudokuBoard board, int cell, PhoneTheme theme, Vector4 accent)
    {
        if (board.IsGiven(cell))
        {
            return theme.TextStrong;
        }

        if (board.IsWrong(cell))
        {
            return theme.Danger;
        }

        if (board.IsRevealed(cell))
        {
            return Core.Theme.Accent.Mint;
        }

        return GamePalette.Lighten(accent, 0.38f);
    }

    private void DrawNotes(ushort notes, Vector2 center, PhoneTheme theme, float noteScale, float cellSize)
    {
        if (notes == 0)
        {
            return;
        }

        var step = cellSize / SudokuBoard.BoxSize;
        var corner = center - new Vector2(step, step);
        for (var digit = 1; digit <= SudokuBoard.Size; digit++)
        {
            if ((notes & (1 << digit)) == 0)
            {
                continue;
            }

            var slot = digit - 1;
            var noteCenter = corner + new Vector2(slot % SudokuBoard.BoxSize * step + step * 0.5f,
                slot / SudokuBoard.BoxSize * step + step * 0.5f);
            Typography.DrawCentered(noteCenter, GameNumber.Label(digit), theme.TextMuted, noteScale,
                FontWeight.Medium);
        }
    }
}
