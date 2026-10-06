using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Tetris;

internal static class TetrisRenderer
{
    public const float GapFraction = 0.08f;
    public const float BandHeight = 26f;
    public const float ClearFlashSeconds = 0.32f;
    public const float PreviewCapsuleWidth = CapsulePadX * 2f + SlotWidth * 2f + SlotGap * 2f + DividerWidth;
    private const float CapsulePadX = 10f;
    private const float SlotWidth = 26f;
    private const float SlotGap = 8f;
    private const float DividerWidth = 1f;
    private const float CaptionCenterY = 7f;
    private const float PreviewTop = 13f;
    private const float PreviewBottom = 3f;
    private const float PreviewCell = 5.5f;
    private const float PreviewGap = 1f;
    private const float BandInsetX = 14f;
    private const float BandValueGap = 5f;
    private const float WellRadius = 10f;
    private const float CellRoundingFraction = 0.22f;
    private const float MinRounding = 2f;
    private const float GhostFillAlpha = 0.10f;
    private const float GhostStrokeAlpha = 0.5f;
    private const float CellStrokeAlpha = 0.4f;
    private const float FlashAlpha = 0.6f;
    private const float EmptySlotAlpha = 0.08f;
    private const float MutedAlpha = 0.62f;
    private static readonly Vector4[] PieceColors =
    {
        new(0.40f, 0.82f, 0.98f, 1f), new(0.95f, 0.84f, 0.36f, 1f), new(0.72f, 0.52f, 0.98f, 1f),
        new(0.96f, 0.62f, 0.32f, 1f), new(0.42f, 0.70f, 0.98f, 1f), new(0.50f, 0.86f, 0.58f, 1f),
        new(0.95f, 0.48f, 0.52f, 1f),
    };
    private static readonly Vector4 GridLine = new(1f, 1f, 1f, 0.05f);
    private static readonly Vector4 Divider = new(1f, 1f, 1f, 0.14f);
    private static readonly Vector4 FlashInk = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 PreviewSheen = new(1f, 1f, 1f, 0.18f);
    private static readonly TextStyle BandStyle = TextStyles.FootnoteEmphasized;
    private static readonly TextStyle CaptionStyle = TextStyles.Caption2;

    public static Vector4 PieceColor(TetrisPieceKind kind) => PieceColors[(int)kind];

    public static Rect PlateRect(in GameGrid grid, float scale) =>
        BoardPlate.Around(new Rect(grid.Bounds.Min - new Vector2(0f, BandHeight * scale), grid.Bounds.Max), scale);

    public static Rect HoldRect(Rect capsule, float scale)
    {
        var left = capsule.Center.X + (DividerWidth * 0.5f + SlotGap) * scale;
        return new Rect(new Vector2(left, capsule.Min.Y), new Vector2(left + SlotWidth * scale, capsule.Max.Y));
    }

    public static void DrawField(ImDrawListPtr drawList, TetrisBoard board, in GameGrid grid, Vector4 accent,
        StageInk ink, string levelLabel, string linesCaption, string linesValue, int clearedMask, float clearFlash,
        bool showActive, float scale)
    {
        var plate = PlateRect(grid, scale);
        BoardPlate.Draw(drawList, plate, BoardPlate.Radius * scale, scale, accent, ink);
        DrawBand(drawList, plate, grid, ink, levelLabel, linesCaption, linesValue, scale);
        StageCell.Draw(drawList, grid.Bounds, GamePalette.CellSunken, CellDepth.Sunken, WellRadius * scale, scale);
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

                DrawCell(drawList, grid.Cell(column, row), PieceColors[(colorIndex - 1) % PieceColors.Length], 1f, scale);
            }
        }

        if (clearedMask != 0 && clearFlash > 0f)
        {
            DrawClearFlash(drawList, grid, clearedMask, clearFlash);
        }

        if (!showActive)
        {
            return;
        }

        var tint = GamePalette.Lighten(PieceColor(board.ActiveKind), 0.12f);
        DrawGhost(drawList, board, grid, tint, scale);
        DrawPiece(drawList, board, grid, board.ActiveY, tint, scale);
    }

    public static void DrawPreviewCapsule(ImDrawListPtr drawList, TetrisBoard board, Rect rect, Vector4 accent,
        bool holdHovered, float scale)
    {
        StageHud.Capsule(drawList, rect, scale);
        var muted = GamePalette.InkLight with { W = MutedAlpha };
        var nextLeft = rect.Min.X + CapsulePadX * scale;
        var nextSlot = new Rect(new Vector2(nextLeft, rect.Min.Y), new Vector2(nextLeft + SlotWidth * scale, rect.Max.Y));
        var holdSlot = HoldRect(rect, scale);
        var dividerX = rect.Center.X;
        drawList.AddLine(new Vector2(dividerX, rect.Min.Y + PreviewBottom * scale * 2f),
            new Vector2(dividerX, rect.Max.Y - PreviewBottom * scale * 2f), ImGui.GetColorU32(Divider), DividerWidth * scale);
        var captionY = rect.Min.Y + CaptionCenterY * scale;
        Typography.DrawCentered(drawList, new Vector2(nextSlot.Center.X, captionY), Loc.T(L.Games.Next), muted, CaptionStyle);
        Typography.DrawCentered(drawList, new Vector2(holdSlot.Center.X, captionY), Loc.T(L.Games.Saved), muted, CaptionStyle);
        DrawPiecePreview(drawList, PreviewArea(nextSlot, rect, scale), board.NextPieceKind, 1f, scale);
        var holdArea = PreviewArea(holdSlot, rect, scale);
        if (board.HeldKind is { } heldKind)
        {
            DrawPiecePreview(drawList, holdArea, heldKind, 1f, scale);
        }
        else
        {
            Squircle.Fill(drawList, holdArea.Min, holdArea.Max, MinRounding * scale,
                ImGui.GetColorU32(GamePalette.InkLight with { W = EmptySlotAlpha }));
        }

        if (holdHovered)
        {
            Squircle.Stroke(drawList, holdSlot.Min, holdSlot.Max, holdSlot.Height * 0.5f,
                ImGui.GetColorU32(accent with { W = 0.7f }), 1f * scale);
        }
    }

    private static Rect PreviewArea(Rect slot, Rect capsule, float scale) =>
        new(new Vector2(slot.Min.X, capsule.Min.Y + PreviewTop * scale),
            new Vector2(slot.Max.X, capsule.Max.Y - PreviewBottom * scale));

    private static void DrawBand(ImDrawListPtr drawList, Rect plate, in GameGrid grid, StageInk ink, string levelLabel,
        string linesCaption, string linesValue, float scale)
    {
        var strong = ink == StageInk.Dark ? GamePalette.InkDark : GamePalette.InkLight;
        var muted = strong with { W = MutedAlpha };
        var centerY = grid.Bounds.Min.Y - BandHeight * scale * 0.5f;
        var textTop = centerY - Typography.LineHeight(BandStyle) * 0.5f;
        var inset = BandInsetX * scale;
        Typography.Draw(drawList, new Vector2(plate.Min.X + inset, textTop), levelLabel, strong, BandStyle);
        var valueWidth = Typography.Measure(linesValue, BandStyle).X;
        var captionWidth = Typography.Measure(linesCaption, BandStyle).X;
        var valueLeft = plate.Max.X - inset - valueWidth;
        Typography.Draw(drawList, new Vector2(valueLeft, textTop), linesValue, strong, BandStyle);
        Typography.Draw(drawList, new Vector2(valueLeft - BandValueGap * scale - captionWidth, textTop), linesCaption,
            muted, BandStyle);
    }

    private static void DrawGridLines(ImDrawListPtr drawList, in GameGrid grid, float scale)
    {
        var lineColor = ImGui.GetColorU32(GridLine);
        for (var column = 1; column < TetrisBoard.Columns; column++)
        {
            var x = grid.Origin.X + column * grid.Pitch;
            drawList.AddLine(new Vector2(x, grid.Origin.Y), new Vector2(x, grid.Origin.Y + grid.Height), lineColor,
                1f * scale);
        }

        for (var row = 1; row < TetrisBoard.Rows; row++)
        {
            var y = grid.Origin.Y + row * grid.Pitch;
            drawList.AddLine(new Vector2(grid.Origin.X, y), new Vector2(grid.Origin.X + grid.Width, y), lineColor,
                1f * scale);
        }
    }

    private static void DrawClearFlash(ImDrawListPtr drawList, in GameGrid grid, int clearedMask, float clearFlash)
    {
        var halfBand = grid.Pitch * 0.5f * clearFlash;
        var color = ImGui.GetColorU32(FlashInk with { W = FlashAlpha * clearFlash });
        for (var row = 0; row < TetrisBoard.Rows; row++)
        {
            if ((clearedMask & (1 << row)) == 0)
            {
                continue;
            }

            var centerY = grid.Origin.Y + (row + 0.5f) * grid.Pitch;
            drawList.AddRectFilled(new Vector2(grid.Origin.X, centerY - halfBand),
                new Vector2(grid.Origin.X + grid.Width, centerY + halfBand), color);
        }
    }

    private static void DrawGhost(ImDrawListPtr drawList, TetrisBoard board, in GameGrid grid, Vector4 tint, float scale)
    {
        var ghostY = board.GetGhostY();
        if (ghostY == board.ActiveY)
        {
            return;
        }

        var cells = board.ActiveCells();
        var fill = ImGui.GetColorU32(tint with { W = GhostFillAlpha });
        var stroke = ImGui.GetColorU32(tint with { W = GhostStrokeAlpha });
        for (var index = 0; index < cells.Length; index++)
        {
            var cell = cells[index];
            var rect = grid.Cell(board.ActiveX + cell.X, ghostY + cell.Y);
            var rounding = Rounding(rect, scale);
            Squircle.Fill(drawList, rect.Min, rect.Max, rounding, fill);
            Squircle.Stroke(drawList, rect.Min, rect.Max, rounding, stroke, 1.2f * scale);
        }
    }

    private static void DrawPiece(ImDrawListPtr drawList, TetrisBoard board, in GameGrid grid, int boardY, Vector4 tint,
        float scale)
    {
        var cells = board.ActiveCells();
        for (var index = 0; index < cells.Length; index++)
        {
            var cell = cells[index];
            DrawCell(drawList, grid.Cell(board.ActiveX + cell.X, boardY + cell.Y), tint, 1f, scale);
        }
    }

    private static void DrawCell(ImDrawListPtr drawList, Rect rect, Vector4 color, float alpha, float scale)
    {
        var rounding = Rounding(rect, scale);
        StageCell.Draw(drawList, rect, color with { W = color.W * alpha }, CellDepth.Raised, rounding, scale);
        Squircle.Stroke(drawList, rect.Min, rect.Max, rounding,
            ImGui.GetColorU32(GamePalette.Darken(color, 0.35f) with { W = CellStrokeAlpha * alpha }), 1f * scale);
    }

    private static float Rounding(Rect rect, float scale) =>
        MathF.Max(MinRounding * scale, rect.Height * CellRoundingFraction);

    private static void DrawPiecePreview(ImDrawListPtr drawList, Rect area, TetrisPieceKind kind, float alpha, float scale)
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
        var cellSize = MathF.Min(PreviewCell * scale, MathF.Min(area.Width / pieceWidth, area.Height / pieceHeight));
        var origin = area.Center - new Vector2(pieceWidth, pieceHeight) * cellSize * 0.5f;
        var tint = GamePalette.Lighten(PieceColor(kind), 0.12f) with { W = alpha };
        var gap = PreviewGap * scale;
        var rounding = MathF.Max(1f * scale, cellSize * CellRoundingFraction);
        for (var index = 0; index < cells.Length; index++)
        {
            var cell = cells[index];
            var min = origin + new Vector2((cell.X - minX) * cellSize, (cell.Y - minY) * cellSize);
            var max = min + new Vector2(cellSize - gap, cellSize - gap);
            Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(tint));
            Squircle.Fill(drawList, min, new Vector2(max.X, min.Y + (max.Y - min.Y) * 0.5f), rounding,
                ImGui.GetColorU32(PreviewSheen with { W = PreviewSheen.W * alpha }));
        }
    }
}
