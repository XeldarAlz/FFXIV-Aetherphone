using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Fuse;

internal static class FuseArt
{
    public static readonly Vector4 Fur = new(0.99f, 0.97f, 0.94f, 1f);
    public static readonly Vector4 FurShade = new(0.84f, 0.81f, 0.86f, 1f);
    public static readonly Vector4 Ink = new(0.22f, 0.18f, 0.24f, 1f);
    public static readonly Vector4 Nose = new(0.95f, 0.42f, 0.46f, 1f);
    public static readonly Vector4 Wing = new(0.58f, 0.44f, 0.80f, 1f);
    public static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    public static readonly Vector4 Shadow = new(0f, 0f, 0f, 0.28f);
    public static readonly Vector4 BombBody = new(0.17f, 0.19f, 0.30f, 1f);
    public static readonly Vector4 BombHot = new(0.98f, 0.30f, 0.22f, 1f);
    public static readonly Vector4 BombCap = new(0.62f, 0.64f, 0.72f, 1f);
    public static readonly Vector4 Cord = new(0.72f, 0.58f, 0.36f, 1f);
    public static readonly Vector4 Spark = new(1f, 0.82f, 0.32f, 1f);
    public static readonly Vector4 Flame = new(1f, 0.48f, 0.12f, 1f);
    public static readonly Vector4 Gold = new(1f, 0.82f, 0.30f, 1f);
    public static readonly Vector4 ExtraBombColor = new(0.40f, 0.58f, 1f, 1f);
    public static readonly Vector4 RangeColor = new(1f, 0.50f, 0.16f, 1f);
    public static readonly Vector4 SpeedColor = new(0.30f, 0.88f, 0.58f, 1f);
    public static readonly Vector4 KickColor = new(0.78f, 0.48f, 1f, 1f);
    private const int EllipseSegments = 20;

    public static Vector4 PowerUpColor(PowerUp kind) => kind switch
    {
        PowerUp.ExtraBomb => ExtraBombColor,
        PowerUp.Range => RangeColor,
        PowerUp.Speed => SpeedColor,
        PowerUp.Kick => KickColor,
        _ => White,
    };

    public static uint Color(Vector4 color, float alpha) => ImGui.GetColorU32(color with { W = color.W * alpha });

    public static void FillEllipse(ImDrawListPtr drawList, Vector2 center, float radiusX, float radiusY, uint color)
    {
        if (radiusX <= 0.2f || radiusY <= 0.2f)
        {
            return;
        }

        drawList.PathClear();
        for (var segment = 0; segment < EllipseSegments; segment++)
        {
            var theta = MathF.Tau * segment / EllipseSegments;
            drawList.PathLineTo(new Vector2(center.X + MathF.Cos(theta) * radiusX, center.Y + MathF.Sin(theta) * radiusY));
        }

        drawList.PathFillConvex(color);
    }

    public static void DrawMoogle(ImDrawListPtr drawList, Vector2 feet, float radius, Vector4 team, FuseDirection facing,
        float stride, bool moving, float alpha, float hop, float time)
    {
        var bob = moving ? MathF.Abs(MathF.Sin(stride)) * radius * 0.16f : MathF.Sin(time * 2.4f) * radius * 0.04f;
        var lift = bob + hop * radius;
        FillEllipse(drawList, feet, radius * (0.82f - hop * 0.2f), radius * 0.3f, Color(Shadow, alpha));
        var center = feet - new Vector2(0f, radius * 0.95f + lift);
        var side = facing switch
        {
            FuseDirection.Left => -1f,
            FuseDirection.Right => 1f,
            _ => 0f,
        };
        var away = facing == FuseDirection.Up;
        var flap = moving ? MathF.Sin(stride * 1.6f) * 0.35f : MathF.Sin(time * 3f) * 0.12f;
        for (var wingSide = -1; wingSide <= 1; wingSide += 2)
        {
            var root = center + new Vector2(wingSide * radius * 0.62f, radius * 0.05f);
            var tip = root + new Vector2(wingSide * radius * (0.62f + flap * 0.2f), -radius * (0.55f + flap * 0.5f));
            var back = root + new Vector2(wingSide * radius * 0.42f, radius * 0.32f);
            drawList.AddTriangleFilled(root, tip, back, Color(Wing, alpha));
        }

        var footSwing = moving ? MathF.Sin(stride) * radius * 0.16f : 0f;
        FillEllipse(drawList, feet + new Vector2(-radius * 0.34f, -radius * 0.12f - MathF.Max(0f, footSwing) - lift * 0.4f),
            radius * 0.22f, radius * 0.14f, Color(FurShade, alpha));
        FillEllipse(drawList, feet + new Vector2(radius * 0.34f, -radius * 0.12f - MathF.Max(0f, -footSwing) - lift * 0.4f),
            radius * 0.22f, radius * 0.14f, Color(FurShade, alpha));
        drawList.AddCircleFilled(center, radius * 0.9f, Color(FurShade, alpha), 28);
        drawList.AddCircleFilled(center - new Vector2(radius * 0.05f, radius * 0.06f), radius * 0.84f, Color(Fur, alpha), 28);
        drawList.AddCircleFilled(center + new Vector2(-radius * 0.3f, -radius * 0.38f), radius * 0.24f,
            Color(White, 0.75f * alpha), 14);
        var collar = center + new Vector2(0f, radius * 0.7f);
        FillEllipse(drawList, collar, radius * 0.62f, radius * 0.2f, Color(team, alpha));
        FillEllipse(drawList, collar - new Vector2(0f, radius * 0.05f), radius * 0.46f, radius * 0.09f,
            Color(White, 0.35f * alpha));
        if (!away)
        {
            var faceShift = side * radius * 0.26f;
            var eyeColor = Color(Ink, alpha);
            var thickness = MathF.Max(1f, radius * 0.11f);
            for (var eye = -1; eye <= 1; eye += 2)
            {
                var eyeCenter = center + new Vector2(faceShift + eye * radius * 0.28f, -radius * 0.06f);
                drawList.PathClear();
                drawList.PathArcTo(eyeCenter, radius * 0.13f, MathF.PI * 1.1f, MathF.PI * 1.9f, 8);
                drawList.PathStroke(eyeColor, ImDrawFlags.None, thickness);
            }

            FillEllipse(drawList, center + new Vector2(faceShift, radius * 0.2f), radius * 0.18f, radius * 0.13f,
                Color(Nose, alpha));
        }

        var sway = MathF.Sin(time * 4.2f + team.X * 7f) * radius * 0.12f - side * radius * 0.08f;
        var antennaBase = center + new Vector2(0f, -radius * 0.82f);
        var antennaTip = center + new Vector2(sway, -radius * 1.5f);
        drawList.AddLine(antennaBase, antennaTip, Color(Ink, alpha), MathF.Max(1f, radius * 0.08f));
        drawList.AddCircleFilled(antennaTip, radius * 0.5f, Color(team, 0.22f * alpha), 16);
        drawList.AddCircleFilled(antennaTip, radius * 0.28f, Color(team, alpha), 16);
        drawList.AddCircleFilled(antennaTip - new Vector2(radius * 0.09f, radius * 0.09f), radius * 0.09f,
            Color(White, 0.85f * alpha), 8);
    }

    public static void DrawGhost(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 team, float alpha,
        float time)
    {
        var body = Color(White, 0.72f * alpha);
        drawList.AddCircleFilled(center, radius * 1.4f, Color(team, 0.14f * alpha), 20);
        drawList.PathClear();
        drawList.PathArcTo(center, radius, MathF.PI, MathF.Tau, 14);
        drawList.PathLineTo(center + new Vector2(radius, radius * 0.7f));
        drawList.PathLineTo(center + new Vector2(-radius, radius * 0.7f));
        drawList.PathFillConvex(body);
        for (var lobe = 0; lobe < 3; lobe++)
        {
            var wave = MathF.Sin(time * 6f + lobe * 2f) * radius * 0.08f;
            drawList.AddCircleFilled(center + new Vector2((lobe - 1) * radius * 0.66f, radius * 0.72f + wave),
                radius * 0.34f, body, 12);
        }
        var eye = Color(Ink, 0.8f * alpha);
        drawList.AddCircleFilled(center + new Vector2(-radius * 0.32f, -radius * 0.05f), radius * 0.13f, eye, 10);
        drawList.AddCircleFilled(center + new Vector2(radius * 0.32f, -radius * 0.05f), radius * 0.13f, eye, 10);
        drawList.AddCircleFilled(center + new Vector2(0f, radius * 0.32f), radius * 0.1f, eye, 10);
        var tip = center + new Vector2(MathF.Sin(time * 4f) * radius * 0.15f, -radius * 1.55f);
        drawList.AddLine(center + new Vector2(0f, -radius * 0.95f), tip, Color(Ink, 0.5f * alpha),
            MathF.Max(1f, radius * 0.07f));
        drawList.AddCircleFilled(tip, radius * 0.24f, Color(team, 0.8f * alpha), 12);
    }

    public static void DrawCrown(ImDrawListPtr drawList, Vector2 center, float size, float alpha)
    {
        var half = size * 0.5f;
        var baseLeft = center + new Vector2(-half, half * 0.5f);
        var baseRight = center + new Vector2(half, half * 0.5f);
        var color = Color(Gold, alpha);
        drawList.AddRectFilled(baseLeft - new Vector2(0f, half * 0.35f), baseRight, color, half * 0.1f);
        drawList.AddTriangleFilled(baseLeft - new Vector2(0f, half * 0.3f), center + new Vector2(-half * 0.55f, -half * 0.75f),
            center + new Vector2(-half * 0.1f, half * 0.2f), color);
        drawList.AddTriangleFilled(center + new Vector2(-half * 0.35f, half * 0.2f), center + new Vector2(0f, -half),
            center + new Vector2(half * 0.35f, half * 0.2f), color);
        drawList.AddTriangleFilled(center + new Vector2(half * 0.1f, half * 0.2f), center + new Vector2(half * 0.55f, -half * 0.75f),
            baseRight - new Vector2(0f, half * 0.3f), color);
        drawList.AddCircleFilled(center + new Vector2(0f, -half), half * 0.16f, Color(White, alpha), 8);
    }

    public static void DrawBomb(ImDrawListPtr drawList, Vector2 center, float radius, float squash, float heat,
        float alpha, float time)
    {
        FillEllipse(drawList, center + new Vector2(0f, radius * 0.86f), radius * 0.9f, radius * 0.3f, Color(Shadow, alpha));
        var radiusX = radius * (1f + squash);
        var radiusY = radius * (1f - squash);
        var body = center + new Vector2(0f, radius * squash);
        if (heat > 0f)
        {
            drawList.AddCircleFilled(body, radius * (1.25f + heat * 0.25f), Color(BombHot, 0.22f * heat * alpha), 24);
        }

        FillEllipse(drawList, body, radiusX, radiusY, Color(Vector4.Lerp(BombBody, BombHot, heat * 0.75f), alpha));
        FillEllipse(drawList, body + new Vector2(radiusX * 0.05f, radiusY * 0.08f), radiusX * 0.86f, radiusY * 0.84f,
            Color(Vector4.Lerp(new Vector4(0.24f, 0.27f, 0.42f, 1f), BombHot, heat), alpha));
        FillEllipse(drawList, body + new Vector2(-radiusX * 0.36f, -radiusY * 0.38f), radiusX * 0.24f, radiusY * 0.16f,
            Color(White, 0.7f * alpha));
        var capCenter = body + new Vector2(radiusX * 0.42f, -radiusY * 0.82f);
        drawList.AddRectFilled(capCenter - new Vector2(radius * 0.2f, radius * 0.14f),
            capCenter + new Vector2(radius * 0.2f, radius * 0.14f), Color(BombCap, alpha), radius * 0.06f);
        var cordEnd = capCenter + new Vector2(radius * 0.48f, -radius * 0.5f);
        drawList.AddBezierQuadratic(capCenter, capCenter + new Vector2(radius * 0.1f, -radius * 0.55f), cordEnd,
            Color(Cord, alpha), MathF.Max(1f, radius * 0.12f), 8);
        var flicker = 0.75f + 0.25f * MathF.Sin(time * 37f + center.X);
        drawList.AddCircleFilled(cordEnd, radius * 0.42f * flicker, Color(Spark, 0.25f * alpha), 12);
        drawList.AddCircleFilled(cordEnd, radius * 0.2f * flicker, Color(Spark, alpha), 10);
        drawList.AddCircleFilled(cordEnd, radius * 0.09f, Color(White, alpha), 8);
    }

    public static void DrawPowerUp(ImDrawListPtr drawList, Vector2 center, float radius, PowerUp kind, float alpha,
        float time)
    {
        var tint = PowerUpColor(kind);
        drawList.AddCircleFilled(center, radius * 1.5f, Color(tint, 0.18f * alpha), 24);
        drawList.AddCircleFilled(center, radius, Color(tint, alpha), 28);
        drawList.AddCircleFilled(center, radius * 0.8f, Color(new Vector4(0.12f, 0.12f, 0.2f, 1f), 0.92f * alpha), 28);
        drawList.AddCircle(center, radius * 0.92f, Color(White, 0.55f * alpha), 28, MathF.Max(1f, radius * 0.08f));
        DrawGlyph(drawList, center, radius * 0.6f, kind, tint, alpha, time);
    }

    public static void DrawGlyph(ImDrawListPtr drawList, Vector2 center, float size, PowerUp kind, Vector4 tint,
        float alpha, float time)
    {
        switch (kind)
        {
            case PowerUp.ExtraBomb:
                drawList.AddCircleFilled(center + new Vector2(-size * 0.08f, size * 0.12f), size * 0.62f, Color(tint, alpha), 18);
                drawList.AddCircleFilled(center + new Vector2(-size * 0.3f, -size * 0.12f), size * 0.16f,
                    Color(White, 0.7f * alpha), 8);
                drawList.AddLine(center + new Vector2(size * 0.3f, -size * 0.38f), center + new Vector2(size * 0.62f, -size * 0.72f),
                    Color(Cord, alpha), MathF.Max(1f, size * 0.14f));
                drawList.AddCircleFilled(center + new Vector2(size * 0.66f, -size * 0.76f), size * (0.16f + 0.05f * MathF.Sin(time * 20f)),
                    Color(Spark, alpha), 8);
                return;
            case PowerUp.Range:
                DrawFlame(drawList, center + new Vector2(0f, size * 0.15f), size, tint, alpha, time);
                return;
            case PowerUp.Speed:
                for (var chevron = 0; chevron < 2; chevron++)
                {
                    var offset = new Vector2(-size * 0.32f + chevron * size * 0.5f, 0f);
                    var thickness = MathF.Max(1f, size * 0.22f);
                    var color = Color(tint, alpha);
                    drawList.AddLine(center + offset + new Vector2(-size * 0.2f, -size * 0.5f), center + offset + new Vector2(size * 0.22f, 0f),
                        color, thickness);
                    drawList.AddLine(center + offset + new Vector2(size * 0.22f, 0f), center + offset + new Vector2(-size * 0.2f, size * 0.5f),
                        color, thickness);
                }

                return;
            case PowerUp.Kick:
                var boot = Color(tint, alpha);
                drawList.AddRectFilled(center + new Vector2(-size * 0.42f, -size * 0.66f), center + new Vector2(size * 0.04f, size * 0.3f),
                    boot, size * 0.12f);
                drawList.AddRectFilled(center + new Vector2(-size * 0.42f, size * 0.02f), center + new Vector2(size * 0.62f, size * 0.46f),
                    boot, size * 0.2f);
                drawList.AddRectFilled(center + new Vector2(-size * 0.46f, size * 0.38f), center + new Vector2(size * 0.66f, size * 0.56f),
                    Color(White, 0.8f * alpha), size * 0.08f);
                return;
            default:
                return;
        }
    }

    public static void DrawFlame(ImDrawListPtr drawList, Vector2 center, float size, Vector4 tint, float alpha, float time)
    {
        var flicker = 1f + 0.08f * MathF.Sin(time * 18f);
        var outer = Color(tint, alpha);
        drawList.AddCircleFilled(center + new Vector2(0f, size * 0.12f), size * 0.5f, outer, 18);
        drawList.AddTriangleFilled(center + new Vector2(-size * 0.48f, size * 0.05f),
            center + new Vector2(size * 0.06f, -size * 0.85f * flicker), center + new Vector2(size * 0.48f, size * 0.05f), outer);
        var inner = Color(Spark, alpha);
        drawList.AddCircleFilled(center + new Vector2(0f, size * 0.22f), size * 0.27f, inner, 14);
        drawList.AddTriangleFilled(center + new Vector2(-size * 0.25f, size * 0.18f),
            center + new Vector2(0f, -size * 0.3f * flicker), center + new Vector2(size * 0.25f, size * 0.18f), inner);
    }
}
