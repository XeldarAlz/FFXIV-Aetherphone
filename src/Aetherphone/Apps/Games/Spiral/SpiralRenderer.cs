using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Spiral;

internal struct SpiralShard
{
    public float Angle;
    public float Arc;
    public float Radial;
    public float Y;
    public float RadialVelocity;
    public float VerticalVelocity;
    public float Spin;
    public float Life;
    public float MaxLife;
    public Vector4 Color;
}

internal struct SpiralSplat
{
    public int Ring;
    public float LocalAngle;
    public float Size;
    public float Age;
    public Vector4 Color;
}

internal static class SpiralRenderer
{
    public const float Tilt = 0.3f;
    public const float PipsWidth = 68f;
    private const int ArcSteps = 4;
    private const int PoleStripes = 6;
    private const float PipRadius = 4.2f;
    private const float PipSpacing = 14f;
    private const float SplatGrowSeconds = 0.12f;
    private static readonly Vector4[] LevelColors =
    {
        new(0.18f, 0.80f, 0.76f, 1f), new(0.40f, 0.62f, 1f, 1f), new(0.70f, 0.52f, 1f, 1f),
        new(1f, 0.74f, 0.30f, 1f), new(0.54f, 0.86f, 0.36f, 1f), new(0.36f, 0.86f, 1f, 1f),
    };

    public static readonly Vector4 Red = new(0.97f, 0.20f, 0.26f, 1f);
    public static readonly Vector4 BallColor = new(1f, 0.95f, 0.84f, 1f);
    public static readonly Vector4 Paint = new(1f, 0.86f, 0.46f, 1f);
    public static readonly Vector4 Fire = new(1f, 0.56f, 0.18f, 1f);
    public static readonly Vector4 FireCore = new(1f, 0.92f, 0.58f, 1f);
    private static readonly Vector4 Pole = new(0.80f, 0.84f, 0.92f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Shadow = new(0f, 0f, 0f, 0.34f);
    private static readonly Vector4 Dim = new(1f, 1f, 1f, 0.2f);

    public static Vector2 Project(float angle, float radius, float y) =>
        new(radius * MathF.Cos(angle), y + radius * MathF.Sin(angle) * Tilt);

    public static Vector2 BallPoint(SpiralBoard board) =>
        Project(SpiralBoard.BallAngle, SpiralBoard.BallOrbit, board.BallY);

    public static Vector4 RingColor(int ring) => LevelColors[ring / SpiralBoard.LevelRings % LevelColors.Length];

    public static void DrawTower(ImDrawListPtr drawList, in Camera2D camera, SpiralBoard board,
        ReadOnlySpan<SpiralSplat> splats, float scale)
    {
        var first = board.CurrentRing;
        var last = first;
        var bottom = camera.VisibleWorld.Max.Y + SpiralBoard.OuterRadius;
        while (last < first + SpiralBoard.LookAhead - 1 && SpiralBoard.RingY(last + 1) < bottom)
        {
            last++;
        }

        var glow = 0.75f + 0.25f * Pulse.Wave(Pulse.Medium);
        for (var ring = last; ring >= first; ring--)
        {
            DrawRing(drawList, in camera, board, ring, false, glow, scale);
            DrawSplats(drawList, in camera, board, splats, ring, false);
        }

        DrawPole(drawList, in camera, board, scale);
        for (var ring = last; ring >= first; ring--)
        {
            DrawRing(drawList, in camera, board, ring, true, glow, scale);
            DrawSplats(drawList, in camera, board, splats, ring, true);
        }
    }

    public static void DrawShards(ImDrawListPtr drawList, in Camera2D camera, ReadOnlySpan<SpiralShard> shards)
    {
        Span<Vector2> outer = stackalloc Vector2[ArcSteps + 1];
        Span<Vector2> inner = stackalloc Vector2[ArcSteps + 1];
        var flags = drawList.Flags;
        drawList.Flags = flags & ~ImDrawListFlags.AntiAliasedFill;
        for (var index = 0; index < shards.Length; index++)
        {
            ref readonly var shard = ref shards[index];
            if (shard.Life <= 0f)
            {
                continue;
            }

            var alpha = Math.Clamp(shard.Life / (shard.MaxLife * 0.6f), 0f, 1f);
            var mid = shard.Angle + shard.Arc * 0.5f;
            var color = Shade(shard.Color, mid) with { W = alpha };
            BuildArc(in camera, shard.Angle, shard.Arc, SpiralBoard.OuterRadius + shard.Radial, shard.Y, outer);
            BuildArc(in camera, shard.Angle, shard.Arc, SpiralBoard.InnerRadius + shard.Radial, shard.Y, inner);
            var packed = ImGui.GetColorU32(color);
            for (var step = 0; step < ArcSteps; step++)
            {
                drawList.AddQuadFilled(inner[step], outer[step], outer[step + 1], inner[step + 1], packed);
            }
        }

        drawList.Flags = flags;
    }

    public static void DrawShadow(ImDrawListPtr drawList, in Camera2D camera, SpiralBoard board)
    {
        var ring = board.CurrentRing;
        var segment = board.SegmentUnderBall(ring);
        if (segment == SpiralSegment.Gap || board.State != SpiralState.Playing)
        {
            return;
        }

        var ringTop = SpiralBoard.RingY(ring);
        var height = ringTop - (board.BallY + SpiralBoard.BallRadius);
        var fade = 1f - Math.Clamp(height / (SpiralBoard.BounceHeight * 1.8f), 0f, 0.7f);
        var center = camera.ToScreen(Project(SpiralBoard.BallAngle, SpiralBoard.BallOrbit, ringTop));
        var radius = camera.Px(SpiralBoard.BallRadius * 1.15f * fade);
        var danger = segment == SpiralSegment.Red && !board.Fireball;
        var color = danger
            ? Red with { W = 0.45f + 0.35f * Pulse.Wave(Pulse.Fast) }
            : Shadow with { W = Shadow.W * fade };
        Shapes.FillEllipse(drawList, center, radius, radius * Tilt * 1.4f, ImGui.GetColorU32(color));
        if (!danger)
        {
            return;
        }

        drawList.AddCircle(center, radius * 1.4f, ImGui.GetColorU32(Red with { W = 0.5f * fade }), 20, MathF.Max(1f, radius * 0.12f));
    }

    public static void DrawBall(ImDrawListPtr drawList, in Camera2D camera, SpiralBoard board, float squash, float time)
    {
        var point = camera.ToScreen(BallPoint(board));
        var radius = camera.Px(SpiralBoard.BallRadius);
        var stretch = Math.Clamp(board.BallVelocity / SpiralBoard.MaxFallSpeed, 0f, 1f);
        var scaleX = 1f + 0.34f * squash - 0.14f * stretch;
        var scaleY = 1f - 0.32f * squash + 0.24f * stretch;
        var center = new Vector2(point.X, point.Y + radius * (1f - scaleY));
        var radiusX = radius * scaleX;
        var radiusY = radius * scaleY;
        if (board.Fireball)
        {
            var flicker = 0.75f + 0.25f * MathF.Sin(time * 31f);
            ProgressRing.Glow(center, radius * 2.6f, Fire, 1.1f * flicker);
            Shapes.FillEllipse(drawList, center, radiusX * 1.25f, radiusY * 1.25f, ImGui.GetColorU32(Fire with { W = 0.55f }));
            Shapes.FillEllipse(drawList, center, radiusX, radiusY, ImGui.GetColorU32(Fire));
            Shapes.FillEllipse(drawList, center - new Vector2(radiusX * 0.18f, radiusY * 0.2f), radiusX * 0.62f, radiusY * 0.62f,
                ImGui.GetColorU32(FireCore));
            return;
        }

        ProgressRing.Glow(center, radius * 1.8f, BallColor, 0.35f);
        Shapes.FillEllipse(drawList, center, radiusX, radiusY, ImGui.GetColorU32(GamePalette.Darken(Paint, 0.18f)));
        Shapes.FillEllipse(drawList, center - new Vector2(radiusX * 0.1f, radiusY * 0.12f), radiusX * 0.86f, radiusY * 0.86f,
            ImGui.GetColorU32(BallColor));
        Shapes.FillEllipse(drawList, center - new Vector2(radiusX * 0.34f, radiusY * 0.38f), radiusX * 0.26f, radiusY * 0.22f,
            ImGui.GetColorU32(White with { W = 0.9f }));
    }

    public static void DrawPips(ImDrawListPtr drawList, Rect rect, int streak, bool fireball, Vector4 accent,
        float scale)
    {
        StageHud.Capsule(drawList, rect, scale);
        var spacing = PipSpacing * scale;
        var radius = PipRadius * scale;
        var origin = new Vector2(rect.Center.X - (SpiralBoard.FireballStreak - 1) * spacing * 0.5f, rect.Center.Y);
        var lit = fireball ? SpiralBoard.FireballStreak : Math.Min(streak, SpiralBoard.FireballStreak);
        var color = fireball ? Vector4.Lerp(Fire, FireCore, Pulse.Wave(Pulse.Fast)) : GamePalette.Lighten(accent, 0.35f);
        for (var pip = 0; pip < SpiralBoard.FireballStreak; pip++)
        {
            var center = new Vector2(origin.X + pip * spacing, origin.Y);
            if (pip >= lit)
            {
                drawList.AddCircle(center, radius, ImGui.GetColorU32(Dim), 12, MathF.Max(1f, scale));
                continue;
            }

            if (fireball)
            {
                ProgressRing.Glow(center, radius * 2f, Fire, 0.8f);
            }

            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(color), 12);
        }
    }

    private static void DrawRing(ImDrawListPtr drawList, in Camera2D camera, SpiralBoard board, int ring, bool front,
        float glow, float scale)
    {
        var y = SpiralBoard.RingY(ring);
        var color = RingColor(ring);
        for (var segment = 0; segment < SpiralBoard.Segments; segment++)
        {
            var kind = board.SegmentAt(ring, segment);
            if (kind == SpiralSegment.Gap)
            {
                continue;
            }

            var start = segment * SpiralBoard.SegmentArc + board.Angle;
            var facesFront = MathF.Sin(start + SpiralBoard.SegmentArc * 0.5f) >= 0f;
            if (facesFront != front)
            {
                continue;
            }

            var tint = kind == SpiralSegment.Red ? Vector4.Lerp(GamePalette.Darken(Red, 0.12f), Red, glow) : color;
            var capStart = board.SegmentAt(ring, segment - 1) == SpiralSegment.Gap;
            var capEnd = board.SegmentAt(ring, segment + 1) == SpiralSegment.Gap;
            DrawSegment(drawList, in camera, start, y, tint, capStart, capEnd, kind == SpiralSegment.Red, scale);
        }
    }

    private static void DrawSegment(ImDrawListPtr drawList, in Camera2D camera, float start, float y, Vector4 color,
        bool capStart, bool capEnd, bool red, float scale)
    {
        Span<Vector2> outerTop = stackalloc Vector2[ArcSteps + 1];
        Span<Vector2> innerTop = stackalloc Vector2[ArcSteps + 1];
        Span<Vector2> outerBottom = stackalloc Vector2[ArcSteps + 1];
        var arc = SpiralBoard.SegmentArc;
        BuildArc(in camera, start, arc, SpiralBoard.OuterRadius, y, outerTop);
        BuildArc(in camera, start, arc, SpiralBoard.InnerRadius, y, innerTop);
        BuildArc(in camera, start, arc, SpiralBoard.OuterRadius, y + SpiralBoard.Thickness, outerBottom);
        var flags = drawList.Flags;
        drawList.Flags = flags & ~ImDrawListFlags.AntiAliasedFill;
        for (var step = 0; step < ArcSteps; step++)
        {
            var mid = start + arc * (step + 0.5f) / ArcSteps;
            if (MathF.Sin(mid) <= 0f)
            {
                continue;
            }

            var rim = ImGui.GetColorU32(Shade(color, mid) * new Vector4(0.6f, 0.6f, 0.6f, 1f));
            drawList.AddQuadFilled(outerTop[step], outerTop[step + 1], outerBottom[step + 1], outerBottom[step], rim);
        }

        var end = start + arc;
        if (capStart && MathF.Cos(start) < 0f)
        {
            DrawCap(drawList, in camera, start, y, color);
        }

        if (capEnd && MathF.Cos(end) > 0f)
        {
            DrawCap(drawList, in camera, end, y, color);
        }

        for (var step = 0; step < ArcSteps; step++)
        {
            var mid = start + arc * (step + 0.5f) / ArcSteps;
            var top = ImGui.GetColorU32(Shade(color, mid));
            drawList.AddQuadFilled(innerTop[step], outerTop[step], outerTop[step + 1], innerTop[step + 1], top);
        }

        drawList.Flags = flags;
        var edge = ImGui.GetColorU32(GamePalette.Lighten(Shade(color, start + arc * 0.5f), red ? 0.4f : 0.28f));
        var thickness = MathF.Max(1f, 1.2f * scale);
        for (var step = 0; step < ArcSteps; step++)
        {
            drawList.AddLine(outerTop[step], outerTop[step + 1], edge, thickness);
        }

        if (!red)
        {
            return;
        }

        Span<Vector2> stripe = stackalloc Vector2[ArcSteps + 1];
        BuildArc(in camera, start + arc * 0.12f, arc * 0.76f,
            (SpiralBoard.InnerRadius + SpiralBoard.OuterRadius) * 0.5f, y, stripe);
        var stripeColor = ImGui.GetColorU32(GamePalette.Lighten(Red, 0.45f) with { W = 0.8f });
        for (var step = 0; step < ArcSteps; step++)
        {
            drawList.AddLine(stripe[step], stripe[step + 1], stripeColor, MathF.Max(1f, camera.Px(0.07f)));
        }
    }

    private static void DrawCap(ImDrawListPtr drawList, in Camera2D camera, float angle, float y, Vector4 color)
    {
        var innerTop = camera.ToScreen(Project(angle, SpiralBoard.InnerRadius, y));
        var outerTop = camera.ToScreen(Project(angle, SpiralBoard.OuterRadius, y));
        var outerBottom = camera.ToScreen(Project(angle, SpiralBoard.OuterRadius, y + SpiralBoard.Thickness));
        var innerBottom = camera.ToScreen(Project(angle, SpiralBoard.InnerRadius, y + SpiralBoard.Thickness));
        drawList.AddQuadFilled(innerTop, outerTop, outerBottom, innerBottom,
            ImGui.GetColorU32(color * new Vector4(0.48f, 0.48f, 0.5f, 1f)));
    }

    private static void DrawPole(ImDrawListPtr drawList, in Camera2D camera, SpiralBoard board, float scale)
    {
        var view = camera.View;
        var left = camera.ToScreen(new Vector2(-SpiralBoard.InnerRadius, 0f)).X;
        var right = camera.ToScreen(new Vector2(SpiralBoard.InnerRadius, 0f)).X;
        var middle = (left + right) * 0.5f;
        var top = MathF.Max(view.Min.Y, camera.ToScreen(new Vector2(0f, SpiralBoard.RingY(0) - 6f)).Y);
        var edge = ImGui.GetColorU32(GamePalette.Darken(Pole, 0.55f));
        var lit = ImGui.GetColorU32(Pole);
        drawList.AddRectFilledMultiColor(new Vector2(left, top), new Vector2(middle, view.Max.Y), edge, lit, lit, edge);
        drawList.AddRectFilledMultiColor(new Vector2(middle, top), new Vector2(right, view.Max.Y), lit, edge, edge, lit);
        for (var stripe = 0; stripe < PoleStripes; stripe++)
        {
            var angle = stripe * MathF.Tau / PoleStripes + board.Angle;
            var facing = MathF.Sin(angle);
            if (facing <= 0.05f)
            {
                continue;
            }

            var x = camera.ToScreen(new Vector2(SpiralBoard.InnerRadius * 0.96f * MathF.Cos(angle), 0f)).X;
            var color = ImGui.GetColorU32(GamePalette.Darken(Pole, 0.35f) with { W = 0.45f * facing });
            drawList.AddLine(new Vector2(x, top), new Vector2(x, view.Max.Y), color, MathF.Max(1f, 2f * scale * facing));
        }
    }

    private static void DrawSplats(ImDrawListPtr drawList, in Camera2D camera, SpiralBoard board,
        ReadOnlySpan<SpiralSplat> splats, int ring, bool front)
    {
        for (var index = 0; index < splats.Length; index++)
        {
            ref readonly var splat = ref splats[index];
            if (splat.Ring != ring || splat.Size <= 0f)
            {
                continue;
            }

            var angle = splat.LocalAngle + board.Angle;
            if ((MathF.Sin(angle) >= 0f) != front || board.SegmentAt(ring, SpiralBoard.IndexAt(splat.LocalAngle)) == SpiralSegment.Gap)
            {
                continue;
            }

            var grow = Math.Clamp(splat.Age / SplatGrowSeconds, 0.3f, 1f);
            var center = camera.ToScreen(Project(angle, SpiralBoard.BallOrbit, SpiralBoard.RingY(ring)));
            var radius = camera.Px(splat.Size * grow);
            var color = ImGui.GetColorU32(Shade(splat.Color, angle) with { W = 0.88f });
            Shapes.FillEllipse(drawList, center, radius, radius * Tilt * 1.3f, color);
            for (var drop = 0; drop < 3; drop++)
            {
                var dropAngle = splat.LocalAngle * 7f + drop * 2.1f;
                var offset = new Vector2(MathF.Cos(dropAngle) * radius * 1.35f, MathF.Sin(dropAngle) * radius * Tilt * 1.5f);
                drawList.AddCircleFilled(center + offset, radius * 0.22f, color, 8);
            }
        }
    }

    private static void BuildArc(in Camera2D camera, float start, float arc, float radius, float y, Span<Vector2> output)
    {
        for (var step = 0; step <= ArcSteps; step++)
        {
            output[step] = camera.ToScreen(Project(start + arc * step / ArcSteps, radius, y));
        }
    }

    private static Vector4 Shade(Vector4 color, float angle)
    {
        var depth = 0.5f + 0.5f * MathF.Sin(angle);
        var light = -MathF.Cos(angle + 0.6f);
        var factor = Math.Clamp(0.56f + 0.36f * depth + 0.1f * light, 0.35f, 1.08f);
        return new Vector4(MathF.Min(1f, color.X * factor), MathF.Min(1f, color.Y * factor),
            MathF.Min(1f, color.Z * factor), color.W);
    }
}
