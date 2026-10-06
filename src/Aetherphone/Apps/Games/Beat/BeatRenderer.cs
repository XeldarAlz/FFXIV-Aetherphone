using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Beat;

internal static class BeatRenderer
{
    private const float LaneInset = 0.10f;
    private const float FlashReach = 0.5f;
    private const float TrailPerSpeed = 0.18f;
    private const float MaxTrail = 0.45f;
    private const float PadAspect = 1.3f;
    private const float HitLineHeight = 3f;
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Warm = new(1f, 0.72f, 0.30f, 1f);
    private static readonly Vector4 LaneEven = new(1f, 1f, 1f, 0.020f);
    private static readonly Vector4 LaneOdd = new(1f, 1f, 1f, 0.035f);
    private static readonly Vector4 LaneSeparator = new(1f, 1f, 1f, 0.06f);
    private static readonly Vector4 TileSheen = new(1f, 1f, 1f, 0.32f);
    private static readonly Vector4 TileBar = new(1f, 1f, 1f, 0.22f);
    private static readonly Vector4[] LaneColors =
    {
        new(0.42f, 0.74f, 0.98f, 1f), new(0.54f, 0.86f, 0.64f, 1f), new(0.98f, 0.74f, 0.38f, 1f),
        new(0.92f, 0.52f, 0.80f, 1f),
    };

    public static Vector4 LaneColor(int lane) => LaneColors[lane];

    public static Vector4 HeatTint(Vector4 cool, float heat) =>
        heat < 0.5f ? Vector4.Lerp(cool, Warm, heat * 2f) : Vector4.Lerp(Warm, White, (heat - 0.5f) * 2f);

    public static Rect LanesRect(in Camera2D camera, float laneWidth) =>
        new(camera.ToScreen(Vector2.Zero),
            camera.ToScreen(new Vector2(BeatBoard.Lanes * laneWidth, BeatBoard.WorldHeight)));

    public static void DrawLanes(ImDrawListPtr drawList, in Camera2D camera, float laneWidth,
        ReadOnlySpan<float> laneFlash, float heat, Vector4 accent, float scale)
    {
        var top = camera.ToScreen(Vector2.Zero).Y;
        var bottom = camera.ToScreen(new Vector2(0f, BeatBoard.WorldHeight)).Y;
        var hitLine = camera.ToScreen(new Vector2(0f, BeatBoard.HitLine)).Y;
        var flashTop = camera.ToScreen(new Vector2(0f, BeatBoard.HitLine - FlashReach)).Y;
        var separator = ImGui.GetColorU32(LaneSeparator);
        var lanesLeft = camera.ToScreen(Vector2.Zero).X;
        var lanesRight = camera.ToScreen(new Vector2(BeatBoard.Lanes * laneWidth, 0f)).X;
        for (var lane = 0; lane < BeatBoard.Lanes; lane++)
        {
            var left = camera.ToScreen(new Vector2(lane * laneWidth, 0f)).X;
            var right = camera.ToScreen(new Vector2((lane + 1) * laneWidth, 0f)).X;
            var min = new Vector2(left, top);
            var max = new Vector2(right, bottom);
            drawList.AddRectFilled(min, max, ImGui.GetColorU32(lane % 2 == 0 ? LaneEven : LaneOdd));
            if (heat > 0f)
            {
                var tint = HeatTint(LaneColors[lane], heat);
                drawList.AddRectFilled(min, max, ImGui.GetColorU32(tint with { W = 0.12f * heat }));
            }

            if (lane > 0)
            {
                drawList.AddLine(min, new Vector2(left, bottom), separator, MathF.Max(1f, scale));
            }

            var flash = laneFlash[lane];
            if (flash <= 0f)
            {
                continue;
            }

            var color = LaneColors[lane];
            var clear = ImGui.GetColorU32(color with { W = 0f });
            var lit = ImGui.GetColorU32(color with { W = 0.30f * flash });
            drawList.AddRectFilledMultiColor(new Vector2(left, flashTop), new Vector2(right, hitLine), clear, clear,
                lit, lit);
        }

        DrawHitLine(drawList, lanesLeft, lanesRight, hitLine, Vector4.Lerp(accent, Warm, heat), scale);
    }

    public static void DrawTiles(ImDrawListPtr drawList, in Camera2D camera, BeatBoard board, float laneWidth,
        float scale)
    {
        var trail = MathF.Min(MaxTrail, TrailPerSpeed * board.Speed);
        for (var index = 0; index < board.Count; index++)
        {
            var tile = board.Tile(index);
            DrawTile(drawList, in camera, in tile, laneWidth, trail, scale);
        }
    }

    public static void DrawPads(ImDrawListPtr drawList, in Camera2D camera, Rect band, float laneWidth,
        ReadOnlySpan<float> laneFlash, ReadOnlySpan<string> labels, bool interactive, Vector4 accent,
        PhoneTheme theme, float scale)
    {
        var gap = GamePad.Gap * scale;
        var lanePixels = camera.Px(laneWidth);
        var height = MathF.Min(GamePad.KeySize * scale, band.Height - gap * 2f);
        var width = MathF.Min(lanePixels - gap, height * PadAspect);
        if (height <= 0f || width <= 0f)
        {
            return;
        }

        var radius = height * 0.28f;
        for (var lane = 0; lane < BeatBoard.Lanes; lane++)
        {
            var centerX = camera.ToScreen(new Vector2(BeatBoard.LaneCenter(lane, laneWidth), 0f)).X;
            var min = new Vector2(centerX - width * 0.5f, band.Center.Y - height * 0.5f);
            var max = new Vector2(centerX + width * 0.5f, band.Center.Y + height * 0.5f);
            var hovered = interactive && UiInteract.Hover(min, max);
            var held = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
            var flash = laneFlash[lane];
            var color = LaneColors[lane];
            Material.Frosted(drawList, min, max, radius, scale, held ? 1f : 0.85f);
            if (flash > 0f)
            {
                Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(color with { W = 0.38f * flash }));
            }

            if (held)
            {
                Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(accent with { W = 0.32f }));
                Squircle.Stroke(drawList, min, max, radius, ImGui.GetColorU32(accent with { W = 0.9f }), 1.5f * scale);
            }
            else
            {
                var stroke = hovered ? accent : GamePalette.Lighten(color, 0.45f);
                Squircle.Stroke(drawList, min, max, radius,
                    ImGui.GetColorU32(stroke with { W = 0.28f + 0.52f * flash }), MathF.Max(1f, scale));
            }

            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            var ink = held ? accent : theme.TextStrong with { W = 0.75f + 0.25f * flash };
            Typography.DrawCentered(drawList, (min + max) * 0.5f, labels[lane], ink, TextStyles.Title3);
        }
    }

    private static void DrawHitLine(ImDrawListPtr drawList, float left, float right, float hitLine, Vector4 tint,
        float scale)
    {
        var glow = 0.55f + 0.25f * Pulse.Wave(Pulse.Calm);
        var height = HitLineHeight * scale;
        var min = new Vector2(left, hitLine - height * 0.5f);
        var max = new Vector2(right, hitLine + height * 0.5f);
        drawList.AddRectFilled(new Vector2(min.X, min.Y - 10f * scale), new Vector2(max.X, max.Y + 10f * scale),
            ImGui.GetColorU32(tint with { W = 0.10f * glow }));
        Squircle.Fill(drawList, min, max, height * 0.5f,
            ImGui.GetColorU32(GamePalette.Lighten(tint, 0.45f) with { W = 0.85f }));
    }

    private static void DrawTile(ImDrawListPtr drawList, in Camera2D camera, in BeatTile tile, float laneWidth,
        float trail, float scale)
    {
        var color = LaneColors[tile.Lane];
        var inset = laneWidth * LaneInset;
        var min = camera.ToScreen(new Vector2(tile.Lane * laneWidth + inset, tile.Y - BeatBoard.TileHeight));
        var max = camera.ToScreen(new Vector2((tile.Lane + 1) * laneWidth - inset, tile.Y));
        var trailTop = camera.ToScreen(new Vector2(0f, tile.Y - BeatBoard.TileHeight - trail)).Y;
        var clear = ImGui.GetColorU32(color with { W = 0f });
        var glow = ImGui.GetColorU32(color with { W = 0.30f });
        drawList.AddRectFilledMultiColor(new Vector2(min.X, trailTop), new Vector2(max.X, min.Y), clear, clear, glow,
            glow);
        var rounding = MathF.Min((max.X - min.X) * 0.22f, (max.Y - min.Y) * 0.32f);
        Elevation.Draw(drawList, min, max, rounding, scale, 14f, 6f, 0.30f);
        Squircle.FillVerticalGradient(drawList, min, max, rounding,
            ImGui.GetColorU32(GamePalette.Lighten(color, 0.28f)), ImGui.GetColorU32(GamePalette.Darken(color, 0.30f)));
        Squircle.Stroke(drawList, min, max, rounding,
            ImGui.GetColorU32(GamePalette.Lighten(color, 0.5f) with { W = 0.45f }), MathF.Max(1f, scale));
        Material.Sheen(drawList, min, max, rounding, ImGui.GetColorU32(TileSheen), MathF.Max(1f, scale), 1.5f * scale);
        var barHeight = MathF.Max(2f * scale, (max.Y - min.Y) * 0.07f);
        var barMin = new Vector2(min.X + rounding * 0.6f, max.Y - barHeight * 2.4f);
        var barMax = new Vector2(max.X - rounding * 0.6f, max.Y - barHeight * 1.2f);
        Squircle.Fill(drawList, barMin, barMax, barHeight * 0.5f, ImGui.GetColorU32(TileBar));
    }
}
