using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Gloop;

internal static class GloopRenderer
{
    public const float BlobRadius = 0.46f;
    private const float BridgeThickness = 0.58f;
    private const float EyeMinRadius = 6f;
    private const float WellRadius = 10f;
    private const int MaxIncomingIcons = 6;
    private const int RowOfRocks = GloopRules.Columns;
    private static readonly Vector4[] Colors =
    {
        new(0.96f, 0.38f, 0.44f, 1f), new(0.42f, 0.86f, 0.46f, 1f), new(0.38f, 0.62f, 0.99f, 1f),
        new(0.99f, 0.82f, 0.30f, 1f),
    };
    private static readonly Vector4 RockColor = new(0.56f, 0.55f, 0.60f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Pupil = new(0.10f, 0.10f, 0.16f, 1f);
    private static readonly Vector4 Marker = new(0.95f, 0.34f, 0.36f, 0.32f);
    private static readonly Vector4 GridDot = new(1f, 1f, 1f, 0.05f);

    public static Vector4 ColorOf(byte value) =>
        GloopRules.IsColor(value) ? Colors[value - 1] : RockColor;

    public static Vector2 CellCenter(Rect well, float cell, int column, float row) =>
        new(well.Min.X + (column + 0.5f) * cell, well.Min.Y + (row - GloopRules.FirstVisibleRow + 0.5f) * cell);

    public static Vector2 CenterOf(Rect well, float cell, int index) =>
        CellCenter(well, cell, GloopRules.ColumnOf(index), GloopRules.RowOf(index));

    public static void DrawWell(ImDrawListPtr drawList, Rect well, float cell, float scale)
    {
        StageCell.Draw(drawList, well, GamePalette.CellSunken, CellDepth.Sunken, WellRadius * scale, scale);
        var dot = ImGui.GetColorU32(GridDot);
        for (var row = 1; row < GloopRules.VisibleRows; row++)
        {
            for (var column = 1; column < GloopRules.Columns; column++)
            {
                drawList.AddCircleFilled(new Vector2(well.Min.X + column * cell, well.Min.Y + row * cell),
                    MathF.Max(1f, cell * 0.04f), dot, 6);
            }
        }

        var spawn = CellCenter(well, cell, GloopRules.SpawnColumn, GloopRules.SpawnRow);
        var arm = cell * 0.22f;
        var marker = ImGui.GetColorU32(Marker);
        var thickness = MathF.Max(1f, cell * 0.06f);
        drawList.AddLine(spawn - new Vector2(arm, arm), spawn + new Vector2(arm, arm), marker, thickness);
        drawList.AddLine(spawn - new Vector2(arm, -arm), spawn + new Vector2(arm, -arm), marker, thickness);
    }

    public static void DrawBlobs(ImDrawListPtr drawList, GloopBoard board, Rect well, float cell, float popProgress,
        float collapse, float scale)
    {
        drawList.PushClipRect(well.Min, well.Max, true);
        var radius = cell * BlobRadius;
        var drop = collapse * collapse * cell * GloopRules.Rows;
        var alpha = 1f - Math.Clamp(collapse * 1.4f, 0f, 1f);
        var eyes = radius >= EyeMinRadius * scale;
        if (collapse <= 0f)
        {
            DrawBridges(drawList, board, well, cell, radius);
        }

        for (var index = 0; index < GloopRules.Cells; index++)
        {
            var value = board.Cell(index);
            if (value == GloopRules.Empty)
            {
                continue;
            }

            var column = GloopRules.ColumnOf(index);
            var row = GloopRules.RowOf(index) - board.Offset(index);
            var center = CellCenter(well, cell, column, row) + new Vector2(0f, drop * (1f + column * 0.12f));
            var popping = board.IsPopping(index) ? popProgress : 0f;
            DrawBlob(drawList, center, radius, value, board.Squash(index), popping, alpha, eyes);
        }

        drawList.PopClipRect();
    }

    public static void DrawGhost(ImDrawListPtr drawList, GloopBoard board, Rect well, float cell, float scale)
    {
        board.Landing(out var pivotCell, out var satelliteCell);
        DrawGhostCell(drawList, well, cell, pivotCell, board.PivotColor, scale);
        DrawGhostCell(drawList, well, cell, satelliteCell, board.SatelliteColor, scale);
    }

    public static void DrawPiece(ImDrawListPtr drawList, GloopBoard board, Rect well, float cell, float column,
        float angle, float scale)
    {
        drawList.PushClipRect(well.Min - new Vector2(0f, cell), well.Max, true);
        var radius = cell * BlobRadius;
        var eyes = radius >= EyeMinRadius * scale;
        var pivot = new Vector2(well.Min.X + (column + 0.5f) * cell,
            well.Min.Y + (board.PieceRow + board.PieceFraction - GloopRules.FirstVisibleRow + 0.5f) * cell);
        var satellite = pivot + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * cell;
        DrawBlob(drawList, satellite, radius, board.SatelliteColor, 0f, 0f, 1f, eyes);
        DrawBlob(drawList, pivot, radius, board.PivotColor, 0f, 0f, 1f, eyes);
        drawList.AddCircle(pivot, radius * 1.08f, ImGui.GetColorU32(White with { W = 0.55f }), 20,
            MathF.Max(1f, 1.4f * scale));
        drawList.PopClipRect();
    }

    public static void DrawPair(ImDrawListPtr drawList, Vector2 center, float cell, byte pivot, byte satellite,
        float alpha, float scale)
    {
        var radius = cell * BlobRadius;
        var eyes = radius >= EyeMinRadius * scale;
        DrawBlob(drawList, center - new Vector2(0f, cell * 0.5f), radius, satellite, 0f, 0f, alpha, eyes);
        DrawBlob(drawList, center + new Vector2(0f, cell * 0.5f), radius, pivot, 0f, 0f, alpha, eyes);
    }

    public static void DrawIncoming(ImDrawListPtr drawList, Vector2 left, float size, int count, float scale)
    {
        var x = left.X + size * 0.5f;
        var remaining = count;
        for (var icon = 0; icon < MaxIncomingIcons && remaining > 0; icon++)
        {
            var big = remaining >= RowOfRocks;
            var radius = size * (big ? 0.5f : 0.32f);
            DrawBlob(drawList, new Vector2(x, left.Y), radius, GloopRules.Rock, 0f, 0f, 1f, false);
            remaining -= big ? RowOfRocks : 1;
            x += radius * 2f + 2f * scale;
        }
    }

    public static void DrawBlob(ImDrawListPtr drawList, Vector2 center, float radius, byte value, float squash,
        float pop, float alpha, bool eyes)
    {
        if (alpha <= 0f)
        {
            return;
        }

        var color = ColorOf(value);
        var rock = value == GloopRules.Rock;
        if (pop > 0f)
        {
            radius *= 1f + 0.32f * pop;
            color = Vector4.Lerp(color, White, MathF.Min(1f, pop * 1.1f));
            alpha *= 1f - pop * pop;
        }

        var halfWidth = radius * (1f + 0.24f * squash);
        var halfHeight = radius * (1f - 0.22f * squash);
        center.Y += radius - halfHeight;
        var min = new Vector2(center.X - halfWidth, center.Y - halfHeight);
        var max = new Vector2(center.X + halfWidth, center.Y + halfHeight);
        var rounding = MathF.Min(halfWidth, halfHeight);
        Squircle.Fill(drawList, min, max, rounding,
            ImGui.GetColorU32(GamePalette.Darken(color, 0.38f) with { W = alpha }));
        var lift = radius * 0.12f;
        Squircle.Fill(drawList, min, new Vector2(max.X, max.Y - lift), rounding,
            ImGui.GetColorU32(color with { W = alpha }));
        drawList.AddCircleFilled(center + new Vector2(-radius * 0.36f, -radius * 0.42f), radius * 0.24f,
            ImGui.GetColorU32(White with { W = 0.42f * alpha }), 12);
        drawList.AddCircleFilled(center + new Vector2(-radius * 0.44f, -radius * 0.5f), radius * 0.09f,
            ImGui.GetColorU32(White with { W = 0.9f * alpha }), 8);
        if (rock)
        {
            var crack = ImGui.GetColorU32(GamePalette.Darken(color, 0.45f) with { W = alpha });
            var thickness = MathF.Max(1f, radius * 0.1f);
            var bend = center + new Vector2(radius * 0.12f, 0f);
            drawList.AddLine(center + new Vector2(-radius * 0.1f, -radius * 0.55f), bend, crack, thickness);
            drawList.AddLine(bend, center + new Vector2(-radius * 0.18f, radius * 0.5f), crack, thickness);
            return;
        }

        if (!eyes)
        {
            return;
        }

        DrawEyes(drawList, center, radius, squash, alpha);
    }

    private static void DrawEyes(ImDrawListPtr drawList, Vector2 center, float radius, float squash, float alpha)
    {
        var left = center + new Vector2(-radius * 0.3f, -radius * 0.04f);
        var right = center + new Vector2(radius * 0.3f, -radius * 0.04f);
        if (squash > 0.45f)
        {
            var closed = ImGui.GetColorU32(Pupil with { W = alpha });
            var width = radius * 0.16f;
            var thickness = MathF.Max(1f, radius * 0.09f);
            drawList.AddLine(left - new Vector2(width, 0f), left + new Vector2(width, 0f), closed, thickness);
            drawList.AddLine(right - new Vector2(width, 0f), right + new Vector2(width, 0f), closed, thickness);
            return;
        }

        var white = ImGui.GetColorU32(White with { W = alpha });
        var pupil = ImGui.GetColorU32(Pupil with { W = alpha });
        var look = new Vector2(0f, radius * 0.05f);
        drawList.AddCircleFilled(left, radius * 0.2f, white, 10);
        drawList.AddCircleFilled(right, radius * 0.2f, white, 10);
        drawList.AddCircleFilled(left + look, radius * 0.1f, pupil, 8);
        drawList.AddCircleFilled(right + look, radius * 0.1f, pupil, 8);
    }

    private static void DrawBridges(ImDrawListPtr drawList, GloopBoard board, Rect well, float cell, float radius)
    {
        var half = radius * BridgeThickness;
        for (var index = 0; index < GloopRules.Cells; index++)
        {
            var value = board.Cell(index);
            if (!GloopRules.IsColor(value) || !Settled(board, index))
            {
                continue;
            }

            var column = GloopRules.ColumnOf(index);
            var row = GloopRules.RowOf(index);
            var center = CellCenter(well, cell, column, row);
            var color = ImGui.GetColorU32(ColorOf(value));
            var right = index + 1;
            if (column + 1 < GloopRules.Columns && board.Cell(right) == value && Settled(board, right))
            {
                drawList.AddRectFilled(center - new Vector2(0f, half), center + new Vector2(cell, half * 0.9f), color);
            }

            var below = index + GloopRules.Columns;
            if (below < GloopRules.Cells && board.Cell(below) == value && Settled(board, below))
            {
                drawList.AddRectFilled(center - new Vector2(half, 0f), center + new Vector2(half, cell), color);
            }
        }
    }

    private static bool Settled(GloopBoard board, int cell) =>
        board.Offset(cell) <= 0f && !board.IsPopping(cell) && board.Squash(cell) <= 0.05f;

    private static void DrawGhostCell(ImDrawListPtr drawList, Rect well, float cell, int index, byte value,
        float scale)
    {
        if (index < 0 || GloopRules.RowOf(index) < GloopRules.FirstVisibleRow)
        {
            return;
        }

        var center = CenterOf(well, cell, index);
        drawList.AddCircle(center, cell * BlobRadius * 0.82f, ImGui.GetColorU32(ColorOf(value) with { W = 0.5f }), 20,
            MathF.Max(1f, 1.6f * scale));
    }
}
