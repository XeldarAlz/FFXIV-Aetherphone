using Aetherphone.Apps.Games.Drift;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Crawler;

internal static class CrawlerRenderer
{
    public static readonly Vector4 BodyColor = new(1f, 0.62f, 0.28f, 1f);
    public static readonly Vector4 HeadColor = new(1f, 0.86f, 0.42f, 1f);
    public static readonly Vector4 SpiderColor = new(0.98f, 0.40f, 0.70f, 1f);
    public static readonly Vector4 FleaColor = new(0.72f, 0.56f, 1f, 1f);
    public static readonly Vector4 PlayerColor = new(0.92f, 1f, 0.97f, 1f);
    private const float StrokeWidth = 1.4f;
    private const float FrameAlpha = 0.14f;
    private const float ZoneAlpha = 0.05f;
    private const float ZoneLineAlpha = 0.28f;
    private const int DashCount = 15;
    private const int CapPoints = 9;
    private const float CapRadius = 0.4f;
    private const float SegmentRadius = 0.38f;
    private const float FillAlpha = 0.16f;
    private static readonly Vector2[] PlayerOutline =
    {
        new(0f, -0.48f), new(0.36f, 0.34f), new(0.12f, 0.2f), new(-0.12f, 0.2f), new(-0.36f, 0.34f),
    };

    private static readonly Vector2[] FleaOutline =
    {
        new(-0.32f, -0.3f), new(0.32f, -0.3f), new(0f, 0.42f),
    };

    public static Rect FieldRect(in Camera2D camera) =>
        new(camera.ToScreen(Vector2.Zero), camera.ToScreen(new Vector2(CrawlerBoard.Columns, CrawlerBoard.Rows)));

    public static void DrawField(ImDrawListPtr drawList, Rect field, in Camera2D camera, Vector4 accent, float scale,
        float zoneHeat)
    {
        drawList.AddRect(field.Min, field.Max, ImGui.GetColorU32(accent with { W = FrameAlpha }), 0f,
            ImDrawFlags.None, MathF.Max(1f, scale));
        var zoneTop = camera.ToScreen(new Vector2(0f, CrawlerBoard.ZoneTop)).Y;
        var heat = Math.Clamp(zoneHeat, 0f, 1f);
        var tint = Vector4.Lerp(accent, SpiderColor, heat);
        drawList.AddRectFilled(new Vector2(field.Min.X, zoneTop), field.Max,
            ImGui.GetColorU32(tint with { W = ZoneAlpha + 0.08f * heat }));
        var dash = field.Width / (DashCount * 2f);
        var width = MathF.Max(1f, scale);
        for (var index = 0; index < DashCount; index++)
        {
            var left = field.Min.X + dash * (index * 2f + 0.5f);
            NeonStroke.Line(drawList, new Vector2(left, zoneTop), new Vector2(left + dash, zoneTop),
                tint with { W = ZoneLineAlpha + 0.4f * heat }, width);
        }
    }

    public static void DrawMushrooms(ImDrawListPtr drawList, CrawlerBoard board, in Camera2D camera, Vector4 accent,
        float scale, ReadOnlySpan<float> pops)
    {
        var width = MathF.Max(1f, StrokeWidth * scale);
        Span<Vector2> cap = stackalloc Vector2[CapPoints];
        Span<Vector2> stem = stackalloc Vector2[4];
        for (var row = 0; row < CrawlerBoard.Rows; row++)
        {
            for (var column = 0; column < CrawlerBoard.Columns; column++)
            {
                var health = board.MushroomAt(column, row);
                if (health == 0)
                {
                    continue;
                }

                var pop = pops[CrawlerBoard.CellIndex(column, row)];
                var grow = pop > 0f ? GameJuice.PopIn(1f - MathF.Min(pop, 1f)) : 1f;
                DrawMushroom(drawList, CrawlerBoard.CellCenter(column, row), health, grow, in camera, accent, width,
                    cap, stem);
            }
        }
    }

    public static void DrawMushroom(ImDrawListPtr drawList, Vector2 center, int health, float grow, in Camera2D camera,
        Vector4 color, float width, Span<Vector2> cap, Span<Vector2> stem)
    {
        var strength = health / (float)CrawlerBoard.MushroomHealth;
        var radius = CapRadius * (0.55f + 0.45f * strength) * MathF.Max(0.05f, grow);
        var capCenter = center + new Vector2(0f, 0.08f);
        for (var point = 0; point < CapPoints; point++)
        {
            var angle = MathF.PI + point * MathF.PI / (CapPoints - 1);
            cap[point] = camera.ToScreen(capCenter + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius);
        }

        var tint = color with { W = 0.55f + 0.45f * strength };
        NeonStroke.Fill(drawList, cap, tint, FillAlpha);
        NeonStroke.Path(drawList, cap, true, tint, width);
        if (health < CrawlerBoard.MushroomHealth - 1)
        {
            return;
        }

        var stemHalf = radius * 0.32f;
        var stemDepth = radius * (health == CrawlerBoard.MushroomHealth ? 0.8f : 0.45f);
        stem[0] = camera.ToScreen(capCenter + new Vector2(-stemHalf, 0f));
        stem[1] = camera.ToScreen(capCenter + new Vector2(-stemHalf, stemDepth));
        stem[2] = camera.ToScreen(capCenter + new Vector2(stemHalf, stemDepth));
        stem[3] = camera.ToScreen(capCenter + new Vector2(stemHalf, 0f));
        NeonStroke.Path(drawList, stem, false, tint, width);
    }

    public static void DrawCrawler(ImDrawListPtr drawList, CrawlerBoard board, in Camera2D camera, float scale,
        float time)
    {
        var width = MathF.Max(1f, StrokeWidth * scale);
        var radius = camera.Px(SegmentRadius);
        for (var index = 0; index < board.SegmentCapacity; index++)
        {
            ref readonly var segment = ref board.SegmentAt(index);
            if (!segment.Alive)
            {
                continue;
            }

            var position = board.SegmentPosition(index);
            var center = camera.ToScreen(position);
            var color = segment.Head ? HeadColor : BodyColor;
            var wiggle = MathF.Sin(time * 14f + index * 1.3f) * 0.12f;
            for (var side = -1; side <= 1; side += 2)
            {
                var root = position + new Vector2(0f, side * SegmentRadius * 0.8f);
                var tip = root + new Vector2(wiggle * side - segment.StepX * 0.12f, side * 0.24f);
                NeonStroke.Line(drawList, camera.ToScreen(root), camera.ToScreen(tip), color with { W = 0.7f },
                    width * 0.8f);
            }

            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(color with { W = FillAlpha }), 16);
            NeonStroke.Circle(drawList, center, radius, color, width);
            if (!segment.Head)
            {
                NeonStroke.Dot(drawList, center, MathF.Max(1f, radius * 0.18f), color);
                continue;
            }

            var forward = new Vector2(segment.StepX, 0f);
            for (var side = -1; side <= 1; side += 2)
            {
                var eye = position + forward * 0.16f + new Vector2(0f, side * 0.14f);
                NeonStroke.Dot(drawList, camera.ToScreen(eye), MathF.Max(1f, radius * 0.14f), PlayerColor);
                var antennaRoot = position + forward * 0.3f + new Vector2(0f, side * 0.2f);
                var antennaTip = antennaRoot + forward * 0.26f + new Vector2(0f, side * (0.2f + wiggle));
                NeonStroke.Line(drawList, camera.ToScreen(antennaRoot), camera.ToScreen(antennaTip), color, width * 0.8f);
            }
        }
    }

    public static void DrawSpider(ImDrawListPtr drawList, CrawlerBoard board, in Camera2D camera, float scale,
        float time)
    {
        if (!board.SpiderActive)
        {
            return;
        }

        var width = MathF.Max(1f, StrokeWidth * scale);
        var body = board.Spider;
        Span<Vector2> leg = stackalloc Vector2[3];
        for (var side = -1; side <= 1; side += 2)
        {
            for (var pair = 0; pair < 4; pair++)
            {
                var stride = MathF.Sin(time * 18f + pair * 1.7f + side) * 0.08f;
                var spread = (pair - 1.5f) * 0.22f;
                leg[0] = camera.ToScreen(body + new Vector2(side * 0.2f, spread * 0.5f));
                leg[1] = camera.ToScreen(body + new Vector2(side * 0.52f, spread - 0.28f + stride));
                leg[2] = camera.ToScreen(body + new Vector2(side * 0.78f, spread * 1.4f + 0.12f));
                NeonStroke.Path(drawList, leg, false, SpiderColor with { W = 0.85f }, width * 0.8f);
            }
        }

        var center = camera.ToScreen(body);
        var radius = camera.Px(0.3f);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(SpiderColor with { W = FillAlpha }), 16);
        NeonStroke.Circle(drawList, center, radius, SpiderColor, width);
        NeonStroke.Dot(drawList, camera.ToScreen(body + new Vector2(-0.1f, -0.08f)), MathF.Max(1f, radius * 0.16f),
            PlayerColor);
        NeonStroke.Dot(drawList, camera.ToScreen(body + new Vector2(0.1f, -0.08f)), MathF.Max(1f, radius * 0.16f),
            PlayerColor);
    }

    public static void DrawFlea(ImDrawListPtr drawList, CrawlerBoard board, in Camera2D camera, float scale, float time)
    {
        if (!board.FleaActive)
        {
            return;
        }

        var width = MathF.Max(1f, StrokeWidth * scale);
        var color = board.FleaWounded ? Vector4.Lerp(FleaColor, PlayerColor, 0.4f) : FleaColor;
        Span<Vector2> outline = stackalloc Vector2[FleaOutline.Length];
        for (var index = 0; index < FleaOutline.Length; index++)
        {
            outline[index] = camera.ToScreen(board.Flea + FleaOutline[index]);
        }

        NeonStroke.Fill(drawList, outline, color, FillAlpha);
        NeonStroke.Path(drawList, outline, true, color, width);
        var flap = MathF.Sin(time * 40f) * 0.14f;
        for (var side = -1; side <= 1; side += 2)
        {
            var root = board.Flea + new Vector2(side * 0.2f, -0.24f);
            var tip = root + new Vector2(side * 0.32f, -0.22f + flap);
            NeonStroke.Line(drawList, camera.ToScreen(root), camera.ToScreen(tip), color with { W = 0.7f }, width * 0.8f);
        }
    }

    public static void DrawPlayer(ImDrawListPtr drawList, Vector2 position, in Camera2D camera, Vector4 accent,
        float scale, float alpha)
    {
        var width = MathF.Max(1f, StrokeWidth * scale);
        Span<Vector2> outline = stackalloc Vector2[PlayerOutline.Length];
        for (var index = 0; index < PlayerOutline.Length; index++)
        {
            outline[index] = camera.ToScreen(position + PlayerOutline[index]);
        }

        var tint = Vector4.Lerp(PlayerColor, accent, 0.35f) with { W = alpha };
        NeonStroke.Fill(drawList, outline, tint, FillAlpha * 1.4f);
        NeonStroke.Path(drawList, outline, true, tint, width);
        NeonStroke.Dot(drawList, camera.ToScreen(position + new Vector2(0f, -0.48f)), MathF.Max(1f, 1.4f * scale),
            tint);
    }

    public static void DrawBullet(ImDrawListPtr drawList, CrawlerBoard board, in Camera2D camera, Vector4 accent,
        float scale)
    {
        if (!board.HasBullet)
        {
            return;
        }

        var tip = camera.ToScreen(board.Bullet);
        var tail = camera.ToScreen(board.Bullet + new Vector2(0f, 0.6f));
        var color = Vector4.Lerp(PlayerColor, accent, 0.25f);
        NeonStroke.Line(drawList, tail, tip, color, MathF.Max(1f, 1.6f * scale));
        NeonStroke.Dot(drawList, tip, MathF.Max(1f, 1.2f * scale), color);
    }
}
