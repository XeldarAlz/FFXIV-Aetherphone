using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Thrust;

internal static class ThrustRenderer
{
    public const float WallThickness = 0.7f;
    private const float PillarSpacing = 3.6f;
    private const float PillarWidth = 0.55f;
    private const float PillarParallax = 0.5f;
    private const float SeamSpacing = 1.6f;
    private const float LightSpacing = 3.2f;
    private const float NodeRadius = 0.3f;
    private const float BeamGlow = 0.6f;
    private const float BoltJitter = 0.14f;
    private const float BoltWidth = 0.07f;
    private const float CoreWidth = 0.035f;
    private const float BoltRate = 24f;
    private const float MissileLength = 0.95f;
    private const float MarkerInset = 26f;
    private const float MarkerRadius = 13f;
    private const int SpeedLines = 12;
    private static readonly Vector4 Slab = new(0.045f, 0.05f, 0.11f, 1f);
    private static readonly Vector4 SlabEdge = new(0.09f, 0.1f, 0.2f, 1f);
    private static readonly Vector4 Pillar = new(0.07f, 0.08f, 0.17f, 0.75f);
    private static readonly Vector4 NodeMetal = new(0.14f, 0.15f, 0.22f, 1f);
    private static readonly Vector4 Danger = new(0.98f, 0.28f, 0.3f, 1f);
    private static readonly Vector4 MissileBody = new(0.8f, 0.82f, 0.88f, 1f);
    private static readonly Vector4 MissileDark = new(0.4f, 0.42f, 0.5f, 1f);
    private static readonly Vector4 CoinRim = new(0.86f, 0.58f, 0.1f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly float[] LineY = new float[SpeedLines];
    private static readonly float[] LinePhase = new float[SpeedLines];
    private static readonly float[] LineLength = new float[SpeedLines];

    static ThrustRenderer()
    {
        var random = GameRandom.FromSeed(0x7412E57UL);
        for (var line = 0; line < SpeedLines; line++)
        {
            LineY[line] = random.NextFloat();
            LinePhase[line] = random.NextFloat();
            LineLength[line] = random.NextFloat();
        }
    }

    public static void DrawBackWall(ImDrawListPtr drawList, in Camera2D camera, Vector4 accent)
    {
        var view = camera.View;
        var ceiling = camera.ToScreen(Vector2.Zero).Y;
        var floor = camera.ToScreen(new Vector2(0f, ThrustBoard.Height)).Y;
        var offset = camera.Origin.X * PillarParallax;
        var visible = camera.VisibleWorld;
        var first = (int)MathF.Floor((visible.Min.X - offset) / PillarSpacing) - 1;
        var last = (int)MathF.Ceiling((visible.Max.X - offset) / PillarSpacing) + 1;
        var fill = ImGui.GetColorU32(Pillar);
        var edge = ImGui.GetColorU32(accent with { W = 0.16f });
        for (var pillar = first; pillar <= last; pillar++)
        {
            var worldX = pillar * PillarSpacing + offset;
            var left = camera.ToScreen(new Vector2(worldX, 0f)).X;
            var right = left + camera.Px(PillarWidth);
            if (right < view.Min.X || left > view.Max.X)
            {
                continue;
            }

            drawList.AddRectFilled(new Vector2(left, ceiling), new Vector2(right, floor), fill);
            drawList.AddLine(new Vector2(left, ceiling), new Vector2(left, floor), edge, 1f);
            drawList.AddLine(new Vector2(right, ceiling), new Vector2(right, floor), edge, 1f);
        }
    }

    public static void DrawWalls(ImDrawListPtr drawList, in Camera2D camera, Rect full, Vector4 accent, float time, float scale)
    {
        var ceiling = camera.ToScreen(Vector2.Zero).Y;
        var floor = camera.ToScreen(new Vector2(0f, ThrustBoard.Height)).Y;
        var slab = ImGui.GetColorU32(Slab);
        var slabEdge = ImGui.GetColorU32(SlabEdge);
        drawList.AddRectFilledMultiColor(full.Min, new Vector2(full.Max.X, ceiling), slab, slab, slabEdge, slabEdge);
        drawList.AddRectFilledMultiColor(new Vector2(full.Min.X, floor), full.Max, slabEdge, slabEdge, slab, slab);
        DrawSeams(drawList, camera, ceiling, ceiling - camera.Px(WallThickness), accent, time, scale);
        DrawSeams(drawList, camera, floor, floor + camera.Px(WallThickness), accent, time, scale);
        var glow = ImGui.GetColorU32(accent with { W = 0.22f });
        var line = ImGui.GetColorU32(Vector4.Lerp(accent, White, 0.35f) with { W = 0.95f });
        var band = 6f * scale;
        drawList.AddRectFilled(new Vector2(full.Min.X, ceiling), new Vector2(full.Max.X, ceiling + band), glow);
        drawList.AddRectFilled(new Vector2(full.Min.X, floor - band), new Vector2(full.Max.X, floor), glow);
        drawList.AddLine(new Vector2(full.Min.X, ceiling), new Vector2(full.Max.X, ceiling), line, MathF.Max(1f, 2f * scale));
        drawList.AddLine(new Vector2(full.Min.X, floor), new Vector2(full.Max.X, floor), line, MathF.Max(1f, 2f * scale));
    }

    public static void DrawZapper(ImDrawListPtr drawList, in Camera2D camera, in ThrustZapper zapper, int index, float time,
        float danger)
    {
        var direction = zapper.Direction(time);
        var start = camera.ToScreen(zapper.Center - direction * zapper.HalfLength);
        var end = camera.ToScreen(zapper.Center + direction * zapper.HalfLength);
        var pulse = 0.8f + 0.2f * MathF.Sin(time * 14f + index);
        drawList.AddLine(start, end, ImGui.GetColorU32(ThrustArt.Electric with { W = (0.14f + 0.12f * danger) * pulse }),
            camera.Px(BeamGlow));
        var length = Vector2.Distance(start, end);
        var segments = Math.Clamp((int)(zapper.HalfLength * 6f), 4, 18);
        var normal = length > 0.01f ? new Vector2(-(end.Y - start.Y), end.X - start.X) / length : Vector2.UnitY;
        var frame = (int)(time * BoltRate);
        var jitter = camera.Px(BoltJitter);
        var bolt = ImGui.GetColorU32(ThrustArt.Electric);
        var width = MathF.Max(1f, camera.Px(BoltWidth));
        var previous = start;
        for (var segment = 1; segment <= segments; segment++)
        {
            var progress = segment / (float)segments;
            var point = Vector2.Lerp(start, end, progress);
            if (segment < segments)
            {
                point += normal * (Hash(index * 31 + segment, frame) * 2f - 1f) * jitter;
            }

            drawList.AddLine(previous, point, bolt, width);
            previous = point;
        }

        drawList.AddLine(start, end, ImGui.GetColorU32(White with { W = 0.85f }), MathF.Max(1f, camera.Px(CoreWidth)));
        DrawNode(drawList, camera, start, pulse);
        DrawNode(drawList, camera, end, pulse);
        if (!zapper.Rotating)
        {
            return;
        }

        var hub = camera.ToScreen(zapper.Center);
        drawList.AddCircleFilled(hub, camera.Px(NodeRadius * 0.85f), ImGui.GetColorU32(NodeMetal), 16);
        drawList.AddCircle(hub, camera.Px(NodeRadius * 0.85f), ImGui.GetColorU32(ThrustArt.Electric with { W = 0.8f }), 16,
            MathF.Max(1f, camera.Px(0.05f)));
    }

    public static void DrawCoin(ImDrawListPtr drawList, in Camera2D camera, Vector2 coin, float time)
    {
        var center = camera.ToScreen(coin);
        var radius = camera.Px(ThrustGenerator.CoinRadius);
        var spin = MathF.Abs(MathF.Cos(time * 4.5f + coin.X * 0.8f));
        var width = radius * (0.25f + 0.75f * spin);
        drawList.AddCircleFilled(center, radius * 1.8f, ImGui.GetColorU32(ThrustArt.Coin with { W = 0.16f }), 16);
        ThrustArt.FillEllipse(drawList, center, width, radius, ImGui.GetColorU32(CoinRim), 0f);
        ThrustArt.FillEllipse(drawList, center, width * 0.76f, radius * 0.76f, ImGui.GetColorU32(ThrustArt.Coin), 0f);
        if (spin > 0.5f)
        {
            ThrustArt.FillEllipse(drawList, center - new Vector2(width * 0.28f, radius * 0.3f), width * 0.2f, radius * 0.16f,
                ImGui.GetColorU32(White with { W = 0.8f }), 0f);
        }
    }

    public static void DrawVehicle(ImDrawListPtr drawList, in Camera2D camera, Vector2 position, float time)
    {
        var bob = MathF.Sin(time * 3f) * 0.15f;
        var center = camera.ToScreen(position + new Vector2(0f, bob));
        var radius = camera.Px(0.62f);
        for (var layer = 3; layer >= 1; layer--)
        {
            drawList.AddCircleFilled(center, radius * (1f + layer * 0.35f),
                ImGui.GetColorU32(ThrustArt.Plumage with { W = 0.07f * (4 - layer) }), 24);
        }

        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(new Vector4(0.12f, 0.16f, 0.3f, 0.75f)), 28);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(ThrustArt.Electric with { W = 0.85f }), 28,
            MathF.Max(1f, camera.Px(0.05f)));
        ThrustArt.FillEllipse(drawList, center + new Vector2(radius * 0.08f, -radius * 0.05f), radius * 0.55f, radius * 0.2f,
            ImGui.GetColorU32(ThrustArt.Plumage), -0.9f + MathF.Sin(time * 2f) * 0.1f);
        ThrustArt.DrawFeather(drawList, center + new Vector2(-radius * 0.12f, radius * 0.08f), radius * 0.48f,
            -0.9f + MathF.Sin(time * 2f) * 0.1f, 1f);
        var shine = time * 2.2f;
        drawList.PathClear();
        drawList.PathArcTo(center, radius * 0.82f, shine, shine + 0.9f, 10);
        drawList.PathStroke(ImGui.GetColorU32(White with { W = 0.6f }), ImDrawFlags.None, MathF.Max(1f, camera.Px(0.05f)));
    }

    public static void DrawMissile(ImDrawListPtr drawList, in Camera2D camera, in ThrustMissile missile, float time)
    {
        var nose = camera.ToScreen(new Vector2(missile.X, missile.Y));
        var tail = camera.ToScreen(new Vector2(missile.X + MissileLength, missile.Y));
        var thickness = camera.Px(0.34f);
        var flicker = 0.8f + 0.4f * MathF.Sin(time * 53f);
        drawList.AddCircleFilled(tail + new Vector2(camera.Px(0.2f), 0f), camera.Px(0.45f) * flicker,
            ImGui.GetColorU32(ThrustArt.Flame with { W = 0.35f }), 16);
        drawList.AddCircleFilled(tail + new Vector2(camera.Px(0.15f), 0f), camera.Px(0.2f) * flicker,
            ImGui.GetColorU32(new Vector4(1f, 0.92f, 0.6f, 1f)), 12);
        drawList.AddTriangleFilled(tail + new Vector2(-camera.Px(0.25f), 0f), tail + new Vector2(camera.Px(0.12f), -thickness * 0.9f),
            tail + new Vector2(camera.Px(0.12f), thickness * 0.9f), ImGui.GetColorU32(MissileDark));
        drawList.AddLine(nose + new Vector2(camera.Px(0.2f), 0f), tail, ImGui.GetColorU32(MissileBody), thickness);
        drawList.AddLine(nose + new Vector2(camera.Px(0.3f), -thickness * 0.25f), tail + new Vector2(0f, -thickness * 0.25f),
            ImGui.GetColorU32(White with { W = 0.5f }), MathF.Max(1f, thickness * 0.2f));
        drawList.AddTriangleFilled(nose, nose + new Vector2(camera.Px(0.3f), -thickness * 0.5f),
            nose + new Vector2(camera.Px(0.3f), thickness * 0.5f), ImGui.GetColorU32(Danger));
    }

    public static void DrawWarning(ImDrawListPtr drawList, in Camera2D camera, in ThrustMissile missile, float time, float scale)
    {
        var view = camera.View;
        var y = camera.ToScreen(new Vector2(0f, missile.Y)).Y;
        var locked = missile.Phase == MissilePhase.Locked;
        var rate = locked ? 22f : 9f;
        var blink = 0.55f + 0.45f * MathF.Sin(time * rate);
        var center = new Vector2(view.Max.X - MarkerInset * scale, y);
        var radius = MarkerRadius * scale * (locked ? 1.15f : 1f);
        if (locked)
        {
            var dash = 10f * scale;
            var color = ImGui.GetColorU32(Danger with { W = 0.28f * blink });
            for (var x = center.X - radius * 1.6f; x > view.Min.X; x -= dash * 2f)
            {
                drawList.AddLine(new Vector2(x, y), new Vector2(MathF.Max(view.Min.X, x - dash), y), color, MathF.Max(1f, 2f * scale));
            }

            var grow = (time * 3f) % 1f;
            drawList.AddCircle(center, radius * (1.2f + grow * 0.9f), ImGui.GetColorU32(Danger with { W = 0.6f * (1f - grow) }), 24,
                MathF.Max(1f, 2f * scale));
        }

        drawList.AddCircleFilled(center, radius * 1.7f, ImGui.GetColorU32(Danger with { W = 0.18f * blink }), 24);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Danger with { W = 0.75f + 0.25f * blink }), 24);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(White with { W = 0.9f }), 24, MathF.Max(1f, 1.5f * scale));
        var bar = ImGui.GetColorU32(White);
        drawList.AddLine(center - new Vector2(0f, radius * 0.5f), center + new Vector2(0f, radius * 0.12f), bar,
            MathF.Max(1f, radius * 0.22f));
        drawList.AddCircleFilled(center + new Vector2(0f, radius * 0.45f), radius * 0.13f, bar, 8);
        for (var chevron = 0; chevron < 2; chevron++)
        {
            var tip = center - new Vector2(radius * (1.6f + chevron * 0.7f), 0f);
            var arm = new Vector2(radius * 0.4f, radius * 0.45f);
            var color = ImGui.GetColorU32(Danger with { W = blink * (0.8f - chevron * 0.3f) });
            drawList.AddLine(tip, tip + arm, color, MathF.Max(1f, 2.2f * scale));
            drawList.AddLine(tip, tip + new Vector2(arm.X, -arm.Y), color, MathF.Max(1f, 2.2f * scale));
        }
    }

    public static void DrawSpeedLines(ImDrawListPtr drawList, in Camera2D camera, float intensity, float time, float scale)
    {
        if (intensity <= 0.01f)
        {
            return;
        }

        var view = camera.View;
        var top = camera.ToScreen(Vector2.Zero).Y;
        var bottom = camera.ToScreen(new Vector2(0f, ThrustBoard.Height)).Y;
        var thickness = MathF.Max(1f, 1.2f * scale);
        for (var line = 0; line < SpeedLines; line++)
        {
            var travel = Fraction(time * (1.2f + LineLength[line]) * (0.6f + intensity) + LinePhase[line]);
            var x = view.Max.X + view.Width * 0.2f - travel * view.Width * 1.5f;
            var y = top + (bottom - top) * (0.05f + 0.9f * LineY[line]);
            var length = view.Width * (0.08f + 0.14f * LineLength[line]) * intensity;
            drawList.AddLine(new Vector2(x, y), new Vector2(x + length, y),
                ImGui.GetColorU32(White with { W = 0.22f * intensity }), thickness);
        }
    }

    private static void DrawNode(ImDrawListPtr drawList, in Camera2D camera, Vector2 center, float pulse)
    {
        var radius = camera.Px(NodeRadius);
        drawList.AddCircleFilled(center, radius * 1.8f, ImGui.GetColorU32(ThrustArt.Electric with { W = 0.16f * pulse }), 18);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(NodeMetal), 18);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(ThrustArt.Electric), 18, MathF.Max(1f, radius * 0.22f));
        drawList.AddCircleFilled(center, radius * 0.38f, ImGui.GetColorU32(White with { W = pulse }), 10);
    }

    private static void DrawSeams(ImDrawListPtr drawList, in Camera2D camera, float edge, float inner, Vector4 accent,
        float time, float scale)
    {
        var visible = camera.VisibleWorld;
        var first = (int)MathF.Floor(visible.Min.X / SeamSpacing) - 1;
        var last = (int)MathF.Ceiling(visible.Max.X / SeamSpacing) + 1;
        var seam = ImGui.GetColorU32(accent with { W = 0.14f });
        for (var index = first; index <= last; index++)
        {
            var x = camera.ToScreen(new Vector2(index * SeamSpacing, 0f)).X;
            drawList.AddLine(new Vector2(x, edge), new Vector2(x, inner), seam, 1f);
            if (index % 2 != 0)
            {
                continue;
            }

            var phase = 0.5f + 0.5f * MathF.Sin(time * 3f - index * LightSpacing * 0.6f);
            var light = new Vector2(x + camera.Px(SeamSpacing * 0.5f), (edge + inner) * 0.5f);
            drawList.AddCircleFilled(light, 3f * scale, ImGui.GetColorU32(accent with { W = 0.2f * phase }), 10);
            drawList.AddCircleFilled(light, 1.4f * scale, ImGui.GetColorU32(Vector4.Lerp(accent, White, 0.5f) with { W = 0.5f + 0.5f * phase }), 8);
        }
    }

    private static float Hash(int index, int salt)
    {
        var value = (uint)index * 0x9E3779B1u ^ (uint)salt * 0x85EBCA6Bu;
        value ^= value >> 15;
        value *= 0x2C1B3C6Du;
        value ^= value >> 12;
        return (value & 0xFFFFu) / 65535f;
    }

    private static float Fraction(float value) => value - MathF.Floor(value);
}
