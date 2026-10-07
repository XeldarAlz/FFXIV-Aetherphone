using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Nonogram;

internal readonly struct NonogramLayout
{
    public readonly int Size;
    public readonly float CellSize;
    public readonly float ClueSlot;
    public readonly Vector2 GridOrigin;
    public readonly Rect RowBand;
    public readonly Rect ColumnBand;

    public NonogramLayout(int size, float cellSize, float clueSlot, Vector2 gridOrigin, Rect rowBand, Rect columnBand)
    {
        Size = size;
        CellSize = cellSize;
        ClueSlot = clueSlot;
        GridOrigin = gridOrigin;
        RowBand = rowBand;
        ColumnBand = columnBand;
    }

    public Rect GridRect => new(GridOrigin, GridOrigin + new Vector2(Size * CellSize, Size * CellSize));

    public Vector2 CellMin(int column, int row) =>
        new(GridOrigin.X + column * CellSize, GridOrigin.Y + row * CellSize);

    public Vector2 CellCenter(int column, int row) => CellMin(column, row) + new Vector2(CellSize * 0.5f, CellSize * 0.5f);

    public Rect Cell(int column, int row)
    {
        var inset = CellSize * NonogramRenderer.GapFraction * 0.5f;
        var min = CellMin(column, row) + new Vector2(inset, inset);
        return new Rect(min, min + new Vector2(CellSize - inset * 2f, CellSize - inset * 2f));
    }

    public int HitTest(Vector2 point)
    {
        var column = (int)MathF.Floor((point.X - GridOrigin.X) / CellSize);
        var row = (int)MathF.Floor((point.Y - GridOrigin.Y) / CellSize);
        if (column < 0 || column >= Size || row < 0 || row >= Size)
        {
            return -1;
        }

        return row * Size + column;
    }

    public int ClampedHit(Vector2 point)
    {
        var column = Math.Clamp((int)MathF.Floor((point.X - GridOrigin.X) / CellSize), 0, Size - 1);
        var row = Math.Clamp((int)MathF.Floor((point.Y - GridOrigin.Y) / CellSize), 0, Size - 1);
        return row * Size + column;
    }
}

internal readonly struct NonogramView
{
    public readonly float Entrance;
    public readonly int Hovered;
    public readonly int Pressed;
    public readonly int MistakeCell;
    public readonly float MistakeFlash;
    public readonly float Solved;
    public readonly float[] CellPop;

    public NonogramView(float entrance, int hovered, int pressed, int mistakeCell, float mistakeFlash, float solved,
        float[] cellPop)
    {
        Entrance = entrance;
        Hovered = hovered;
        Pressed = pressed;
        MistakeCell = mistakeCell;
        MistakeFlash = mistakeFlash;
        Solved = solved;
        CellPop = cellPop;
    }
}

internal static class NonogramRenderer
{
    public const float GapFraction = 0.12f;
    private const float BandGap = 8f;
    private const float ClueSlotFraction = 0.72f;
    private const float CellRadiusFraction = 0.18f;
    private const float CrossReach = 0.22f;
    private const float MinPop = 0.4f;
    private const float SolvedOverlap = 0.6f;
    private const float SolvedBulge = 0.14f;
    private const int BlockSize = 5;
    private static readonly Vector4 Danger = new(0.95f, 0.30f, 0.30f, 1f);
    private static readonly Vector4 Separator = GamePalette.InkLight with { W = 0.18f };
    private static readonly Vector4 ClueMuted = new(0.97f, 0.97f, 0.98f, 0.38f);

    public static NonogramLayout Layout(Rect area, NonogramBoard board, float scale)
    {
        var pad = BoardPlate.Padding * scale;
        var gap = BandGap * scale;
        var acrossUnits = board.MaxRowClues * ClueSlotFraction + board.Size;
        var downUnits = board.MaxColumnClues * ClueSlotFraction + board.Size;
        var cell = MathF.Max(1f, MathF.Min((area.Width - gap - pad * 2f) / acrossUnits,
            (area.Height - gap - pad * 2f) / downUnits));
        var clueSlot = cell * ClueSlotFraction;
        var leftBand = board.MaxRowClues * clueSlot;
        var topBand = board.MaxColumnClues * clueSlot;
        var gridSize = board.Size * cell;
        var totalWidth = leftBand + gap + pad + gridSize + pad;
        var totalHeight = topBand + gap + pad + gridSize + pad;
        var blockMin = new Vector2(area.Center.X - totalWidth * 0.5f, area.Center.Y - totalHeight * 0.5f);
        var gridOrigin = new Vector2(blockMin.X + leftBand + gap + pad, blockMin.Y + topBand + gap + pad);
        var rowBand = new Rect(new Vector2(blockMin.X, gridOrigin.Y),
            new Vector2(blockMin.X + leftBand, gridOrigin.Y + gridSize));
        var columnBand = new Rect(new Vector2(gridOrigin.X, blockMin.Y),
            new Vector2(gridOrigin.X + gridSize, blockMin.Y + topBand));
        return new NonogramLayout(board.Size, cell, clueSlot, gridOrigin, rowBand, columnBand);
    }

    public static void DrawBoard(ImDrawListPtr drawList, NonogramBoard board, in NonogramLayout layout,
        in NonogramView view, float scale, Vector4 accent, StageInk ink)
    {
        var bandRadius = Metrics.Radius.Md * scale;
        var bandOpacity = 0.9f * (1f - view.Solved);
        Material.Frosted(drawList, layout.RowBand.Min, layout.RowBand.Max, bandRadius, scale, bandOpacity);
        Material.Frosted(drawList, layout.ColumnBand.Min, layout.ColumnBand.Max, bandRadius, scale, bandOpacity);
        var gridRect = layout.GridRect;
        BoardPlate.Draw(drawList, BoardPlate.Around(gridRect, scale), BoardPlate.Radius * scale, scale, accent, ink);
        if (view.Hovered >= 0 && view.Solved <= 0f)
        {
            DrawCrosshair(drawList, layout, view.Hovered, accent);
        }

        var count = board.CellCount;
        var radius = layout.CellSize * CellRadiusFraction;
        for (var index = 0; index < count; index++)
        {
            var lift = StageCell.Lift(GameJuice.Stagger(view.Entrance, index, count)) * scale;
            var rect = layout.Cell(index % board.Size, index / board.Size).Translate(new Vector2(0f, -lift));
            if (view.Solved > 0f)
            {
                DrawFinished(drawList, board, index, rect, view.Solved, count, radius, scale, accent);
                continue;
            }

            DrawCell(drawList, board, index, rect, view, radius, scale);
        }

        if (view.Solved < 1f)
        {
            DrawSeparators(drawList, layout, gridRect, scale, 1f - view.Solved);
            DrawClues(drawList, board, layout, scale, 1f - view.Solved);
        }
    }

    private static void DrawCell(ImDrawListPtr drawList, NonogramBoard board, int index, Rect rect,
        in NonogramView view, float radius, float scale)
    {
        var pop = MathF.Max(MinPop, GameJuice.PopIn(1f - view.CellPop[index]));
        switch (board.MarkAt(index))
        {
            case CellMark.Filled:
                StageCell.Draw(drawList, Scaled(rect, pop), GamePalette.InkLight, CellDepth.Flat, radius, scale);
                break;
            case CellMark.Marked:
                StageCell.Draw(drawList, rect, GamePalette.CellSunken, CellDepth.Sunken, radius, scale);
                DrawCross(drawList, rect.Center, rect.Width * CrossReach * pop, StageInks.Muted, scale);
                break;
            default:
                var fill = index == view.Hovered ? GamePalette.CellHover : GamePalette.Cell;
                var depth = index == view.Pressed ? CellDepth.Pressed : CellDepth.Raised;
                StageCell.Draw(drawList, rect, fill, depth, radius, scale);
                break;
        }

        if (index == view.MistakeCell && view.MistakeFlash > 0f)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, radius,
                ImGui.GetColorU32(Danger with { W = 0.6f * view.MistakeFlash }));
        }
    }

    private static void DrawFinished(ImDrawListPtr drawList, NonogramBoard board, int index, Rect rect, float solved,
        int count, float radius, float scale, Vector4 accent)
    {
        if (board.SolutionAt(index))
        {
            var local = GameJuice.Stagger(solved, index, count, SolvedOverlap);
            var grow = 1f + SolvedBulge * MathF.Sin(local * MathF.PI);
            var fill = Vector4.Lerp(GamePalette.InkLight, accent, local);
            StageCell.Draw(drawList, Scaled(rect, grow), fill, local > 0.5f ? CellDepth.Raised : CellDepth.Flat, radius,
                scale);
            return;
        }

        var alpha = 1f - solved;
        if (alpha <= 0.01f)
        {
            return;
        }

        StageCell.Draw(drawList, rect, GamePalette.Cell with { W = alpha }, CellDepth.Flat, radius, scale);
    }

    private static void DrawCrosshair(ImDrawListPtr drawList, in NonogramLayout layout, int hovered, Vector4 accent)
    {
        var column = hovered % layout.Size;
        var row = hovered / layout.Size;
        var gridRect = layout.GridRect;
        var cellMin = layout.CellMin(column, row);
        var wash = ImGui.GetColorU32(accent with { W = 0.10f });
        var bandWash = ImGui.GetColorU32(accent with { W = 0.18f });
        drawList.AddRectFilled(new Vector2(gridRect.Min.X, cellMin.Y), new Vector2(gridRect.Max.X, cellMin.Y + layout.CellSize),
            wash);
        drawList.AddRectFilled(new Vector2(cellMin.X, gridRect.Min.Y), new Vector2(cellMin.X + layout.CellSize, gridRect.Max.Y),
            wash);
        drawList.AddRectFilled(new Vector2(layout.RowBand.Min.X, cellMin.Y),
            new Vector2(layout.RowBand.Max.X, cellMin.Y + layout.CellSize), bandWash);
        drawList.AddRectFilled(new Vector2(cellMin.X, layout.ColumnBand.Min.Y),
            new Vector2(cellMin.X + layout.CellSize, layout.ColumnBand.Max.Y), bandWash);
    }

    private static void DrawSeparators(ImDrawListPtr drawList, in NonogramLayout layout, Rect gridRect, float scale,
        float alpha)
    {
        var color = ImGui.GetColorU32(Separator with { W = Separator.W * alpha });
        var thickness = 1.5f * scale;
        for (var line = BlockSize; line < layout.Size; line += BlockSize)
        {
            var x = gridRect.Min.X + line * layout.CellSize;
            drawList.AddLine(new Vector2(x, gridRect.Min.Y), new Vector2(x, gridRect.Max.Y), color, thickness);
            var y = gridRect.Min.Y + line * layout.CellSize;
            drawList.AddLine(new Vector2(gridRect.Min.X, y), new Vector2(gridRect.Max.X, y), color, thickness);
        }
    }

    private static void DrawClues(ImDrawListPtr drawList, NonogramBoard board, in NonogramLayout layout, float scale,
        float alpha)
    {
        var clueScale = Math.Clamp(layout.CellSize / (30f * scale), 0.55f, 0.95f);
        var strong = GamePalette.InkLight with { W = alpha };
        var muted = ClueMuted with { W = ClueMuted.W * alpha };
        for (var column = 0; column < board.Size; column++)
        {
            var count = board.ColumnClueCount(column);
            var ink = board.ColumnSatisfied(column) ? muted : strong;
            var centerX = layout.CellCenter(column, 0).X;
            for (var slot = 0; slot < count; slot++)
            {
                var fromBottom = count - slot;
                var centerY = layout.ColumnBand.Max.Y - (fromBottom - 0.5f) * layout.ClueSlot;
                Typography.DrawCentered(drawList, new Vector2(centerX, centerY),
                    GameNumber.Label(board.ColumnClue(column, slot)), ink, clueScale, FontWeight.SemiBold);
            }
        }

        for (var row = 0; row < board.Size; row++)
        {
            var count = board.RowClueCount(row);
            var ink = board.RowSatisfied(row) ? muted : strong;
            var centerY = layout.CellCenter(0, row).Y;
            for (var slot = 0; slot < count; slot++)
            {
                var fromRight = count - slot;
                var centerX = layout.RowBand.Max.X - (fromRight - 0.5f) * layout.ClueSlot;
                Typography.DrawCentered(drawList, new Vector2(centerX, centerY),
                    GameNumber.Label(board.RowClue(row, slot)), ink, clueScale, FontWeight.SemiBold);
            }
        }
    }

    private static void DrawCross(ImDrawListPtr drawList, Vector2 center, float reach, Vector4 color, float scale)
    {
        var packed = ImGui.GetColorU32(color);
        var thickness = MathF.Max(1.5f * scale, reach * 0.34f);
        drawList.AddLine(center - new Vector2(reach, reach), center + new Vector2(reach, reach), packed, thickness);
        drawList.AddLine(center - new Vector2(reach, -reach), center + new Vector2(reach, -reach), packed, thickness);
    }

    private static Rect Scaled(Rect rect, float factor)
    {
        var half = rect.Size * 0.5f * factor;
        return new Rect(rect.Center - half, rect.Center + half);
    }
}
