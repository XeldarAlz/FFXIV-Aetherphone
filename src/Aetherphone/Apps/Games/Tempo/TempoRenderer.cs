using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Tempo;

internal static class TempoRenderer
{
    public const float SkyHeight = 24f;
    public const float Depth = -10f;
    private const float SpikeVisualHeight = 0.82f;
    private const float SpikeVisualHalf = 0.36f;
    private const float PortalHalfHeight = 1.6f;
    private const float PortalHalfWidth = 0.42f;
    private static readonly Vector4 BlockTop = new(0.07f, 0.08f, 0.17f, 1f);
    private static readonly Vector4 BlockBottom = new(0.02f, 0.02f, 0.05f, 1f);
    private static readonly Vector4 GridLine = new(1f, 1f, 1f, 0.05f);
    private static readonly Vector4 SpikeFill = new(0.08f, 0.06f, 0.12f, 1f);
    private static readonly Vector4 SpikeEdge = new(1f, 0.42f, 0.62f, 1f);
    private static readonly Vector4 PadColor = new(1f, 0.86f, 0.3f, 1f);
    private static readonly Vector4 PortalUp = new(0.36f, 0.86f, 1f, 1f);
    private static readonly Vector4 PortalDown = new(1f, 0.62f, 0.26f, 1f);
    private static readonly Vector4 Coin = new(1f, 0.82f, 0.3f, 1f);
    private static readonly Vector4 CoinShade = new(0.86f, 0.56f, 0.12f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Checkpoint = new(0.44f, 0.96f, 0.56f, 1f);

    public static Vector4 PortalColor(TempoItem item) => item == TempoItem.GravityUp ? PortalUp : PortalDown;

    public static Vector2 World(float x, float height) => new(x, -height);

    public static void DrawBeatWash(ImDrawListPtr drawList, Rect full, Vector4 accent, float pulse)
    {
        if (pulse <= 0.01f)
        {
            return;
        }

        var lit = ImGui.GetColorU32(accent with { W = 0.10f * pulse });
        var clear = ImGui.GetColorU32(accent with { W = 0f });
        var band = full.Height * 0.4f;
        drawList.AddRectFilledMultiColor(full.Min, new Vector2(full.Max.X, full.Min.Y + band), lit, lit, clear, clear);
        drawList.AddRectFilledMultiColor(new Vector2(full.Min.X, full.Max.Y - band), full.Max, clear, clear, lit, lit);
    }

    public static void DrawLevel(ImDrawListPtr drawList, in Camera2D camera, TempoLevel level, Vector4 accent, float pulse,
        float time)
    {
        var visible = camera.VisibleWorld;
        var first = Math.Max(0, (int)MathF.Floor(visible.Min.X) - 1);
        var last = Math.Min(level.Length - 1, (int)MathF.Ceiling(visible.Max.X) + 1);
        DrawGround(drawList, camera, level, first, last, accent, pulse);
        DrawCeiling(drawList, camera, level, first, last, accent, pulse);
        for (var column = first; column <= last; column++)
        {
            DrawItem(drawList, camera, level, column, accent, time);
        }

        DrawFinish(drawList, camera, level, accent, time);
    }

    private static void DrawGround(ImDrawListPtr drawList, in Camera2D camera, TempoLevel level, int first, int last,
        Vector4 accent, float pulse)
    {
        var top = ImGui.GetColorU32(BlockTop);
        var bottom = ImGui.GetColorU32(BlockBottom);
        var edge = ImGui.GetColorU32(GamePalette.Lighten(accent, 0.25f) with { W = 0.75f + 0.25f * pulse });
        var glow = ImGui.GetColorU32(accent with { W = 0.18f + 0.3f * pulse });
        var grid = ImGui.GetColorU32(GridLine);
        var thickness = MathF.Max(1.5f, camera.Px(0.07f));
        var column = first;
        while (column <= last)
        {
            var height = level.Ground(column);
            var end = column;
            while (end + 1 <= last && level.Ground(end + 1) == height)
            {
                end++;
            }

            if (height != TempoLevel.Pit)
            {
                var min = camera.ToScreen(World(column, height));
                var max = camera.ToScreen(World(end + 1, Depth));
                drawList.AddRectFilledMultiColor(min, max, top, top, bottom, bottom);
                for (var line = column + 1; line <= end; line++)
                {
                    var x = camera.ToScreen(World(line, 0f)).X;
                    drawList.AddLine(new Vector2(x, min.Y), new Vector2(x, max.Y), grid, 1f);
                }

                for (var row = 1; row <= 3; row++)
                {
                    var y = camera.ToScreen(World(0f, height - row)).Y;
                    drawList.AddLine(new Vector2(min.X, y), new Vector2(max.X, y), grid, 1f);
                }

                drawList.AddRectFilled(new Vector2(min.X, min.Y - thickness * 1.5f), new Vector2(max.X, min.Y + thickness * 2.5f),
                    glow);
                drawList.AddLine(min, new Vector2(max.X, min.Y), edge, thickness);
                DrawSide(drawList, camera, level.Ground(column - 1), height, column, edge, thickness);
                DrawSide(drawList, camera, level.Ground(end + 1), height, end + 1, edge, thickness);
            }

            column = end + 1;
        }
    }

    private static void DrawSide(ImDrawListPtr drawList, in Camera2D camera, sbyte neighbour, sbyte height, int x,
        uint edge, float thickness)
    {
        if (neighbour >= height && neighbour != TempoLevel.Pit)
        {
            return;
        }

        var low = neighbour == TempoLevel.Pit ? Depth : neighbour;
        drawList.AddLine(camera.ToScreen(World(x, height)), camera.ToScreen(World(x, low)), edge, thickness);
    }

    private static void DrawCeiling(ImDrawListPtr drawList, in Camera2D camera, TempoLevel level, int first, int last,
        Vector4 accent, float pulse)
    {
        var top = ImGui.GetColorU32(BlockBottom);
        var bottom = ImGui.GetColorU32(BlockTop);
        var edge = ImGui.GetColorU32(GamePalette.Lighten(accent, 0.25f) with { W = 0.75f + 0.25f * pulse });
        var glow = ImGui.GetColorU32(accent with { W = 0.18f + 0.3f * pulse });
        var thickness = MathF.Max(1.5f, camera.Px(0.07f));
        var column = first;
        while (column <= last)
        {
            var height = level.Ceiling(column);
            var end = column;
            while (end + 1 <= last && level.Ceiling(end + 1) == height)
            {
                end++;
            }

            if (height != TempoLevel.Open)
            {
                var min = camera.ToScreen(World(column, SkyHeight));
                var max = camera.ToScreen(World(end + 1, height));
                drawList.AddRectFilledMultiColor(min, max, top, top, bottom, bottom);
                drawList.AddRectFilled(new Vector2(min.X, max.Y - thickness * 2.5f), new Vector2(max.X, max.Y + thickness * 1.5f),
                    glow);
                drawList.AddLine(new Vector2(min.X, max.Y), max, edge, thickness);
                var left = level.Ceiling(column - 1);
                if (left == TempoLevel.Open || left > height)
                {
                    drawList.AddLine(camera.ToScreen(World(column, height)),
                        camera.ToScreen(World(column, left == TempoLevel.Open ? SkyHeight : left)), edge, thickness);
                }

                var right = level.Ceiling(end + 1);
                if (right == TempoLevel.Open || right > height)
                {
                    drawList.AddLine(camera.ToScreen(World(end + 1, height)),
                        camera.ToScreen(World(end + 1, right == TempoLevel.Open ? SkyHeight : right)), edge, thickness);
                }
            }

            column = end + 1;
        }
    }

    private static void DrawItem(ImDrawListPtr drawList, in Camera2D camera, TempoLevel level, int column, Vector4 accent,
        float time)
    {
        var item = level.Item(column);
        var center = column + 0.5f;
        switch (item)
        {
            case TempoItem.Spike:
                DrawSpike(drawList, camera, center, level.Ground(column), 1f);
                return;
            case TempoItem.CeilingSpike:
                DrawSpike(drawList, camera, center, level.Ceiling(column), -1f);
                return;
            case TempoItem.Pad:
                DrawPad(drawList, camera, center, level.Ground(column), 1f, time);
                return;
            case TempoItem.CeilingPad:
                DrawPad(drawList, camera, center, level.Ceiling(column), -1f, time);
                return;
            case TempoItem.GravityUp:
            case TempoItem.GravityDown:
                DrawPortal(drawList, camera, level, column, item, time);
                return;
            default:
                return;
        }
    }

    private static void DrawSpike(ImDrawListPtr drawList, in Camera2D camera, float center, float surface, float side)
    {
        var left = camera.ToScreen(World(center - SpikeVisualHalf, surface));
        var right = camera.ToScreen(World(center + SpikeVisualHalf, surface));
        var tip = camera.ToScreen(World(center, surface + SpikeVisualHeight * side));
        var glowCenter = camera.ToScreen(World(center, surface + SpikeVisualHeight * 0.4f * side));
        drawList.AddCircleFilled(glowCenter, camera.Px(0.5f), ImGui.GetColorU32(SpikeEdge with { W = 0.12f }), 16);
        drawList.AddTriangleFilled(left, tip, right, ImGui.GetColorU32(SpikeFill));
        var edge = ImGui.GetColorU32(SpikeEdge);
        var thickness = MathF.Max(1.2f, camera.Px(0.05f));
        drawList.AddLine(left, tip, edge, thickness);
        drawList.AddLine(tip, right, edge, thickness);
        drawList.AddLine(left, right, edge, thickness);
    }

    private static void DrawPad(ImDrawListPtr drawList, in Camera2D camera, float center, float surface, float side,
        float time)
    {
        var bounce = 0.5f + 0.5f * MathF.Sin(time * 8f);
        var baseCenter = camera.ToScreen(World(center, surface));
        var radiusX = camera.Px(0.4f);
        var radiusY = camera.Px(0.2f + 0.04f * bounce);
        drawList.AddCircleFilled(baseCenter, camera.Px(0.7f), ImGui.GetColorU32(PadColor with { W = 0.14f }), 16);
        drawList.PathClear();
        var start = side > 0f ? MathF.PI : 0f;
        for (var segment = 0; segment <= 12; segment++)
        {
            var angle = start + segment * MathF.PI / 12f;
            drawList.PathLineTo(baseCenter + new Vector2(MathF.Cos(angle) * radiusX, MathF.Sin(angle) * radiusY));
        }

        drawList.PathFillConvex(ImGui.GetColorU32(PadColor));
        for (var chevron = 0; chevron < 2; chevron++)
        {
            var lift = (0.45f + chevron * 0.3f + bounce * 0.12f) * side;
            var apex = camera.ToScreen(World(center, surface + lift + 0.12f * side));
            var wing = camera.Px(0.2f);
            var shoulder = camera.ToScreen(World(center, surface + lift)).Y;
            var alpha = 0.7f - chevron * 0.25f;
            var color = ImGui.GetColorU32(PadColor with { W = alpha });
            drawList.AddLine(new Vector2(apex.X - wing, shoulder), apex, color, MathF.Max(1.2f, camera.Px(0.05f)));
            drawList.AddLine(apex, new Vector2(apex.X + wing, shoulder), color, MathF.Max(1.2f, camera.Px(0.05f)));
        }
    }

    private static void DrawPortal(ImDrawListPtr drawList, in Camera2D camera, TempoLevel level, int column,
        TempoItem item, float time)
    {
        var ground = level.Ground(column);
        var ceiling = level.Ceiling(column);
        var middle = ceiling != TempoLevel.Open && ground != TempoLevel.Pit
            ? (ground + ceiling) * 0.5f
            : (ground == TempoLevel.Pit ? 2f : ground) + PortalHalfHeight;
        var center = camera.ToScreen(World(column + 0.5f, middle));
        var color = PortalColor(item);
        var radiusX = camera.Px(PortalHalfWidth);
        var radiusY = camera.Px(PortalHalfHeight);
        ProgressRing.Glow(center, radiusY * 1.1f, color, 0.35f);
        for (var ring = 0; ring < 3; ring++)
        {
            var phase = (time * 1.4f + ring / 3f) % 1f;
            var shrink = 1f - phase * 0.5f;
            var alpha = (1f - phase) * 0.8f;
            Shapes.StrokeEllipse(drawList, center, new Vector2(radiusX, radiusY) * shrink,
                ImGui.GetColorU32(color with { W = alpha }), MathF.Max(1.2f, camera.Px(0.06f)));
        }

        Shapes.StrokeEllipse(drawList, center, new Vector2(radiusX, radiusY), ImGui.GetColorU32(color),
            MathF.Max(2f, camera.Px(0.1f)));
        var arrow = camera.Px(0.22f) * (item == TempoItem.GravityUp ? -1f : 1f);
        var tip = center + new Vector2(0f, arrow);
        drawList.AddTriangleFilled(tip, center + new Vector2(-MathF.Abs(arrow), -arrow * 0.2f),
            center + new Vector2(MathF.Abs(arrow), -arrow * 0.2f), ImGui.GetColorU32(White with { W = 0.85f }));
    }

    private static void DrawFinish(ImDrawListPtr drawList, in Camera2D camera, TempoLevel level, Vector4 accent, float time)
    {
        var x = level.Length;
        var top = camera.ToScreen(World(x, SkyHeight));
        var bottom = camera.ToScreen(World(x, Depth));
        var margin = camera.Px(2f);
        if (top.X < camera.View.Min.X - margin || top.X > camera.View.Max.X + margin)
        {
            return;
        }

        var width = camera.Px(0.5f);
        var shimmer = 0.6f + 0.4f * MathF.Sin(time * 5f);
        drawList.AddRectFilledMultiColor(new Vector2(top.X - width * 3f, top.Y), new Vector2(top.X, bottom.Y),
            ImGui.GetColorU32(accent with { W = 0f }), ImGui.GetColorU32(accent with { W = 0.35f * shimmer }),
            ImGui.GetColorU32(accent with { W = 0.35f * shimmer }), ImGui.GetColorU32(accent with { W = 0f }));
        var cell = camera.Px(0.5f);
        var rows = (int)((bottom.Y - top.Y) / cell);
        for (var row = 0; row < rows; row++)
        {
            for (var stripe = 0; stripe < 2; stripe++)
            {
                if ((row + stripe) % 2 != 0)
                {
                    continue;
                }

                var min = new Vector2(top.X + stripe * cell, top.Y + row * cell);
                drawList.AddRectFilled(min, min + new Vector2(cell, cell), ImGui.GetColorU32(White with { W = 0.55f }));
            }
        }
    }

    public static void DrawCoins(ImDrawListPtr drawList, in Camera2D camera, TempoLevel level, byte collected,
        float time)
    {
        for (var index = 0; index < TempoLevel.CoinCount && index < level.CoinTotal; index++)
        {
            var column = level.CoinColumn(index);
            var center = camera.ToScreen(World(column + 0.5f, level.Coin(column) + 0.5f));
            var radius = camera.Px(0.36f);
            if ((collected & (1 << index)) != 0)
            {
                drawList.AddCircle(center, radius, ImGui.GetColorU32(Coin with { W = 0.22f }), 20,
                    MathF.Max(1f, camera.Px(0.04f)));
                continue;
            }

            var spin = MathF.Abs(MathF.Cos(time * 3f + index));
            ProgressRing.Glow(center, radius * 1.8f, Coin, 0.35f);
            Shapes.FillEllipse(drawList, center, radius * MathF.Max(0.15f, spin), radius, ImGui.GetColorU32(CoinShade));
            Shapes.FillEllipse(drawList, center, radius * MathF.Max(0.1f, spin) * 0.82f, radius * 0.82f, ImGui.GetColorU32(Coin));
            drawList.AddCircleFilled(center + new Vector2(-radius * 0.25f * spin, -radius * 0.35f), radius * 0.16f,
                ImGui.GetColorU32(White with { W = 0.7f }), 8);
        }
    }

    public static void DrawCheckpoints(ImDrawListPtr drawList, in Camera2D camera, TempoBoard board, float time)
    {
        for (var index = 0; index < board.CheckpointCount; index++)
        {
            var point = board.Checkpoint(index);
            var center = camera.ToScreen(World(point.X, point.Y));
            var size = camera.Px(0.28f) * (index == board.CheckpointCount - 1 ? 1f + 0.1f * MathF.Sin(time * 6f) : 0.8f);
            var color = ImGui.GetColorU32(Checkpoint with { W = index == board.CheckpointCount - 1 ? 1f : 0.5f });
            drawList.AddQuadFilled(center + new Vector2(0f, -size), center + new Vector2(size, 0f), center + new Vector2(0f, size),
                center + new Vector2(-size, 0f), color);
        }
    }

    public static void DrawCube(ImDrawListPtr drawList, Vector2 center, float size, float rotation, Vector4 accent,
        float squash)
    {
        var half = size * 0.5f;
        ProgressRing.Glow(center, size * 1.1f, accent, 0.35f);
        Square(drawList, center, half * (1f + squash * 0.1f), rotation, ImGui.GetColorU32(GamePalette.Lighten(accent, 0.3f)));
        Square(drawList, center, half * 0.82f, rotation, ImGui.GetColorU32(accent));
        Square(drawList, center, half * 0.46f, rotation, ImGui.GetColorU32(GamePalette.Darken(accent, 0.45f)));
        var axis = new Vector2(MathF.Cos(rotation), MathF.Sin(rotation));
        var normal = new Vector2(-axis.Y, axis.X);
        var eye = MathF.Max(1f, size * 0.08f);
        var eyeColor = ImGui.GetColorU32(White);
        drawList.AddCircleFilled(center + axis * half * 0.22f - normal * half * 0.12f, eye, eyeColor, 8);
        drawList.AddCircleFilled(center + axis * half * 0.22f + normal * half * 0.12f, eye, eyeColor, 8);
    }

    private static void Square(ImDrawListPtr drawList, Vector2 center, float half, float rotation, uint color)
    {
        var axis = new Vector2(MathF.Cos(rotation), MathF.Sin(rotation)) * half;
        var normal = new Vector2(-axis.Y, axis.X);
        drawList.AddQuadFilled(center - axis - normal, center + axis - normal, center + axis + normal, center - axis + normal,
            color);
    }

    public static void DrawProgress(ImDrawListPtr drawList, Rect bar, float progress, float best, Vector4 accent,
        float scale)
    {
        var radius = bar.Height * 0.5f;
        drawList.AddRectFilled(bar.Min, bar.Max, ImGui.GetColorU32(White with { W = 0.14f }), radius);
        var fill = new Vector2(bar.Min.X + bar.Width * Math.Clamp(progress, 0f, 1f), bar.Max.Y);
        if (fill.X > bar.Min.X + 1f)
        {
            drawList.AddRectFilled(new Vector2(bar.Min.X, bar.Min.Y - 2f * scale), new Vector2(fill.X, bar.Max.Y + 2f * scale),
                ImGui.GetColorU32(accent with { W = 0.25f }), radius * 2f);
            drawList.AddRectFilled(bar.Min, fill, ImGui.GetColorU32(GamePalette.Lighten(accent, 0.2f)), radius);
        }

        if (best <= 0.001f)
        {
            return;
        }

        var markX = bar.Min.X + bar.Width * Math.Clamp(best, 0f, 1f);
        drawList.AddLine(new Vector2(markX, bar.Min.Y - 3f * scale), new Vector2(markX, bar.Max.Y + 3f * scale),
            ImGui.GetColorU32(White with { W = 0.85f }), MathF.Max(1f, 1.5f * scale));
    }
}
