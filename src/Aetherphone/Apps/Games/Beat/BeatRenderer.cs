using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Beat;

internal sealed class BeatRenderer
{
    private const float HitLineFactor = 0.84f;
    private static readonly Vector4[] LaneColors =
    {
        new(0.42f, 0.74f, 0.98f, 1f), new(0.54f, 0.86f, 0.64f, 1f), new(0.98f, 0.74f, 0.38f, 1f),
        new(0.92f, 0.52f, 0.80f, 1f),
    };

    public static Vector4 LaneColor(int lane) => LaneColors[lane];

    public static float HitLineOf(Rect field) => field.Min.Y + field.Height * HitLineFactor;

    public static float UnitOf(Rect field) => field.Height * HitLineFactor;

    public static float LaneWidthOf(Rect field) => field.Width / BeatBoard.Lanes;

    public static Vector2 TileCenter(Rect field, int lane, float y)
    {
        var laneWidth = LaneWidthOf(field);
        var unit = UnitOf(field);
        return new Vector2(field.Min.X + (lane + 0.5f) * laneWidth,
            field.Min.Y + (y - BeatBoard.TileHeight * 0.5f) * unit);
    }

    public void Draw(BeatBoard board, Rect field, ReadOnlySpan<float> laneFlash, Vector4 accent, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var laneWidth = LaneWidthOf(field);
        var unit = UnitOf(field);
        var hitLine = HitLineOf(field);
        drawList.PushClipRect(field.Min, field.Max, true);
        DrawLanes(drawList, field, laneFlash, laneWidth, hitLine, scale);
        DrawHitLine(drawList, field, hitLine, accent, scale);
        for (var index = 0; index < board.Count; index++)
        {
            var tile = board.Tile(index);
            DrawTile(drawList, field, tile, laneWidth, unit, scale);
        }

        drawList.PopClipRect();
    }

    private static void DrawLanes(ImDrawListPtr drawList, Rect field, ReadOnlySpan<float> laneFlash, float laneWidth,
        float hitLine, float scale)
    {
        for (var lane = 0; lane < BeatBoard.Lanes; lane++)
        {
            var left = field.Min.X + lane * laneWidth;
            var right = left + laneWidth;
            var tint = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, lane % 2 == 0 ? 0.020f : 0.035f));
            drawList.AddRectFilled(new Vector2(left, field.Min.Y), new Vector2(right, field.Max.Y), tint);
            if (lane > 0)
            {
                drawList.AddLine(new Vector2(left, field.Min.Y), new Vector2(left, field.Max.Y),
                    ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.06f)), MathF.Max(1f, scale));
            }

            var flash = laneFlash[lane];
            if (flash <= 0f)
            {
                continue;
            }

            var color = LaneColors[lane];
            var top = ImGui.GetColorU32(color with { W = 0f });
            var bottom = ImGui.GetColorU32(color with { W = 0.30f * flash });
            drawList.AddRectFilledMultiColor(new Vector2(left, hitLine - field.Height * 0.42f),
                new Vector2(right, hitLine), top, top, bottom, bottom);
        }
    }

    private static void DrawHitLine(ImDrawListPtr drawList, Rect field, float hitLine, Vector4 accent, float scale)
    {
        var glow = 0.55f + 0.25f * Pulse.Wave(Pulse.Calm);
        var height = 3f * scale;
        var min = new Vector2(field.Min.X, hitLine - height * 0.5f);
        var max = new Vector2(field.Max.X, hitLine + height * 0.5f);
        drawList.AddRectFilled(new Vector2(min.X, min.Y - 10f * scale), new Vector2(max.X, max.Y + 10f * scale),
            ImGui.GetColorU32(accent with { W = 0.10f * glow }));
        Squircle.Fill(drawList, min, max, height * 0.5f,
            ImGui.GetColorU32(GamePalette.Lighten(accent, 0.45f) with { W = 0.85f }));
    }

    private static void DrawTile(ImDrawListPtr drawList, Rect field, BeatTile tile, float laneWidth, float unit,
        float scale)
    {
        var color = LaneColors[tile.Lane];
        var left = field.Min.X + tile.Lane * laneWidth + laneWidth * 0.10f;
        var right = field.Min.X + (tile.Lane + 1) * laneWidth - laneWidth * 0.10f;
        var bottom = field.Min.Y + tile.Y * unit;
        var top = bottom - BeatBoard.TileHeight * unit;
        var min = new Vector2(left, top);
        var max = new Vector2(right, bottom);
        var rounding = MathF.Min((right - left) * 0.22f, (bottom - top) * 0.32f);
        Elevation.Draw(drawList, min, max, rounding, scale, 14f, 6f, 0.30f);
        Squircle.FillVerticalGradient(drawList, min, max, rounding,
            ImGui.GetColorU32(GamePalette.Lighten(color, 0.28f)), ImGui.GetColorU32(GamePalette.Darken(color, 0.30f)));
        Squircle.Stroke(drawList, min, max, rounding,
            ImGui.GetColorU32(GamePalette.Lighten(color, 0.5f) with { W = 0.45f }), MathF.Max(1f, scale));
        drawList.AddLine(new Vector2(min.X + rounding, min.Y + 1.5f * scale),
            new Vector2(max.X - rounding, min.Y + 1.5f * scale), ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.32f)),
            MathF.Max(1f, scale));
        var barHeight = MathF.Max(2f * scale, (bottom - top) * 0.07f);
        var barMin = new Vector2(min.X + rounding * 0.6f, max.Y - barHeight * 2.4f);
        var barMax = new Vector2(max.X - rounding * 0.6f, max.Y - barHeight * 1.2f);
        Squircle.Fill(drawList, barMin, barMax, barHeight * 0.5f,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.22f)));
    }
}
