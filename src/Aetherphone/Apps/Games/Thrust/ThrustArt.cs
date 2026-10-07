using Aetherphone.Apps.Games.Framework;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Thrust;

internal static class ThrustArt
{
    public const int EllipseSegments = 16;
    public static readonly Vector4 Electric = new(0.62f, 0.95f, 1f, 1f);
    public static readonly Vector4 Coin = new(1f, 0.83f, 0.27f, 1f);
    public static readonly Vector4 Flame = new(1f, 0.62f, 0.2f, 1f);
    public static readonly Vector4 Pom = new(1f, 0.36f, 0.46f, 1f);
    public static readonly Vector4 Plumage = new(1f, 0.82f, 0.26f, 1f);
    private static readonly Vector4 Fur = new(0.99f, 0.97f, 0.94f, 1f);
    private static readonly Vector4 FurShade = new(0.86f, 0.83f, 0.86f, 1f);
    private static readonly Vector4 Nose = new(0.95f, 0.42f, 0.46f, 1f);
    private static readonly Vector4 Ink = new(0.22f, 0.18f, 0.24f, 1f);
    private static readonly Vector4 Wing = new(0.56f, 0.42f, 0.78f, 1f);
    private static readonly Vector4 Metal = new(0.62f, 0.66f, 0.76f, 1f);
    private static readonly Vector4 MetalDark = new(0.36f, 0.38f, 0.48f, 1f);
    private static readonly Vector4 Stripe = new(0.92f, 0.3f, 0.36f, 1f);
    private static readonly Vector4 FlameCore = new(1f, 0.95f, 0.7f, 1f);
    private static readonly Vector4 PlumageShade = new(0.92f, 0.62f, 0.14f, 1f);
    private static readonly Vector4 Beak = new(0.98f, 0.58f, 0.16f, 1f);
    private static readonly Vector4 Leg = new(0.98f, 0.66f, 0.22f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    public static void DrawPilot(ImDrawListPtr drawList, Vector2 center, float radius, float tilt, float flap, float thrust,
        float run, bool grounded, float alpha, float time)
    {
        DrawJetpack(drawList, center, radius, tilt, thrust, alpha, time);
        DrawMoogle(drawList, center, radius, tilt, flap, run, grounded, alpha, time);
    }

    public static void DrawRider(ImDrawListPtr drawList, Vector2 center, float radius, float tilt, float run, bool grounded,
        float alpha, float time)
    {
        var unit = radius;
        var legLift = grounded ? MathF.Sin(run) : 0.6f;
        for (var side = -1; side <= 1; side += 2)
        {
            var lift = grounded ? MathF.Max(0f, side * legLift) : 0.55f;
            var hip = Point(center, unit, tilt, side < 0 ? -0.3f : 0.15f, 0.45f);
            var knee = Point(center, unit, tilt, (side < 0 ? -0.15f : 0.35f) + lift * 0.25f, 0.8f - lift * 0.25f);
            var foot = Point(center, unit, tilt, (side < 0 ? -0.25f : 0.2f) + lift * 0.35f, 1f - lift * 0.4f);
            var leg = Color(Leg, alpha);
            drawList.AddLine(hip, knee, leg, MathF.Max(1f, unit * 0.14f));
            drawList.AddLine(knee, foot, leg, MathF.Max(1f, unit * 0.11f));
            Shapes.FillEllipse(drawList, foot + Rotate(new Vector2(unit * 0.12f, 0f), tilt), unit * 0.18f, unit * 0.07f, tilt,
                leg, EllipseSegments);
        }

        for (var feather = 0; feather < 3; feather++)
        {
            var angle = MathF.PI + 0.55f + feather * 0.32f + MathF.Sin(run) * 0.08f;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            Shapes.FillEllipse(drawList, Point(center, unit, tilt, -0.95f + direction.X * 0.35f, -0.1f + direction.Y * 0.35f),
                unit * 0.42f, unit * 0.13f, tilt + angle, Color(Vector4.Lerp(Plumage, White, 0.35f), alpha),
                EllipseSegments);
        }

        Shapes.FillEllipse(drawList, Point(center, unit, tilt, -0.1f, 0f), unit, unit * 0.72f, tilt, Color(PlumageShade, alpha),
            EllipseSegments);
        Shapes.FillEllipse(drawList, Point(center, unit, tilt, -0.1f, -0.06f), unit * 0.94f, unit * 0.62f, tilt,
            Color(Plumage, alpha), EllipseSegments);
        Shapes.FillEllipse(drawList, Point(center, unit, tilt, -0.3f, 0.05f), unit * 0.55f, unit * 0.3f,
            tilt - 0.3f + (grounded ? 0f : MathF.Sin(time * 18f) * 0.4f), Color(PlumageShade, alpha), EllipseSegments);
        var neckBase = Point(center, unit, tilt, 0.55f, -0.35f);
        var head = Point(center, unit, tilt, 1f, -1.25f);
        drawList.AddLine(neckBase, head, Color(Plumage, alpha), unit * 0.42f);
        for (var crest = 0; crest < 3; crest++)
        {
            var angle = -MathF.PI * 0.5f - 0.5f - crest * 0.35f;
            var tip = Point(center, unit, tilt, 0.9f + MathF.Cos(angle) * 0.7f, -1.35f + MathF.Sin(angle) * 0.7f);
            drawList.AddTriangleFilled(Point(center, unit, tilt, 0.95f, -1.5f), tip, Point(center, unit, tilt, 0.8f, -1.3f),
                Color(PlumageShade, alpha));
        }

        drawList.AddCircleFilled(head, unit * 0.36f, Color(Plumage, alpha), 16);
        drawList.AddTriangleFilled(Point(center, unit, tilt, 1.25f, -1.36f), Point(center, unit, tilt, 1.78f, -1.18f),
            Point(center, unit, tilt, 1.25f, -1.08f), Color(Beak, alpha));
        drawList.AddCircleFilled(Point(center, unit, tilt, 1.1f, -1.34f), unit * 0.07f, Color(Ink, alpha), 8);
        var riderCenter = Point(center, unit, tilt, -0.15f, -0.92f);
        DrawMoogle(drawList, riderCenter, unit * 0.42f, tilt, time * 9f, 0f, false, alpha, time);
    }

    public static void DrawFeather(ImDrawListPtr drawList, Vector2 center, float size, float angle, float alpha)
    {
        Shapes.FillEllipse(drawList, center, size, size * 0.32f, angle, Color(Plumage, alpha), EllipseSegments);
        var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        drawList.AddLine(center - direction * size * 1.1f, center + direction * size * 0.9f, Color(PlumageShade, alpha),
            MathF.Max(1f, size * 0.1f));
    }

    private static void DrawJetpack(ImDrawListPtr drawList, Vector2 center, float radius, float tilt, float thrust, float alpha,
        float time)
    {
        var top = Point(center, radius, tilt, -0.72f, -0.35f);
        var bottom = Point(center, radius, tilt, -0.72f, 0.42f);
        var nozzle = Point(center, radius, tilt, -0.72f, 0.82f);
        if (thrust > 0.02f)
        {
            var flicker = 0.85f + 0.3f * MathF.Sin(time * 47f) * MathF.Sin(time * 31f);
            var length = radius * (1f + 0.6f * flicker) * thrust;
            var tip = nozzle + Rotate(new Vector2(-radius * 0.15f, length), tilt);
            var half = Rotate(new Vector2(radius * 0.2f, 0f), tilt);
            drawList.AddCircleFilled(nozzle + (tip - nozzle) * 0.35f, radius * 0.7f * thrust, Color(Flame, 0.22f * alpha), 16);
            drawList.AddTriangleFilled(nozzle - half, nozzle + half, tip, Color(Flame, 0.95f * alpha));
            drawList.AddTriangleFilled(nozzle - half * 0.5f, nozzle + half * 0.5f, nozzle + (tip - nozzle) * 0.6f,
                Color(FlameCore, alpha));
        }

        var tankWidth = radius * 0.62f;
        drawList.AddLine(top, bottom, Color(MetalDark, alpha), tankWidth + MathF.Max(1f, radius * 0.08f));
        drawList.AddCircleFilled(top, tankWidth * 0.5f + radius * 0.04f, Color(MetalDark, alpha), 14);
        drawList.AddCircleFilled(bottom, tankWidth * 0.5f + radius * 0.04f, Color(MetalDark, alpha), 14);
        drawList.AddLine(top, bottom, Color(Metal, alpha), tankWidth);
        drawList.AddCircleFilled(top, tankWidth * 0.5f, Color(Metal, alpha), 14);
        drawList.AddCircleFilled(bottom, tankWidth * 0.5f, Color(Metal, alpha), 14);
        var band = Vector2.Lerp(top, bottom, 0.45f);
        var across = Rotate(new Vector2(tankWidth * 0.5f, 0f), tilt);
        drawList.AddLine(band - across, band + across, Color(Stripe, alpha), MathF.Max(1f, radius * 0.16f));
        drawList.AddLine(top + Rotate(new Vector2(-tankWidth * 0.2f, 0f), tilt), bottom + Rotate(new Vector2(-tankWidth * 0.2f, 0f), tilt),
            Color(White, 0.35f * alpha), MathF.Max(1f, radius * 0.08f));
        var nozzleHalf = Rotate(new Vector2(radius * 0.18f, 0f), tilt);
        var nozzleTop = Point(center, radius, tilt, -0.72f, 0.62f);
        drawList.AddQuadFilled(nozzleTop - nozzleHalf * 0.8f, nozzleTop + nozzleHalf * 0.8f, nozzle + nozzleHalf, nozzle - nozzleHalf,
            Color(MetalDark, alpha));
    }

    private static void DrawMoogle(ImDrawListPtr drawList, Vector2 center, float radius, float tilt, float flap, float run,
        bool grounded, float alpha, float time)
    {
        var beat = MathF.Sin(flap) * 0.5f;
        var shoulder = Point(center, radius, tilt, -0.45f, -0.35f);
        var wingTip = Point(center, radius, tilt, -1.15f, -0.85f - beat * 0.4f);
        var wingBack = Point(center, radius, tilt, -1f, -0.2f + beat * 0.2f);
        drawList.AddTriangleFilled(shoulder, wingTip, wingBack, Color(Wing, alpha));
        if (grounded)
        {
            for (var side = -1; side <= 1; side += 2)
            {
                var lift = MathF.Max(0f, side * MathF.Sin(run)) * 0.18f;
                Shapes.FillEllipse(drawList, Point(center, radius, tilt, side * 0.32f, 0.92f - lift), radius * 0.24f,
                    radius * 0.14f, tilt, Color(FurShade, alpha), EllipseSegments);
            }
        }

        drawList.AddCircleFilled(center, radius, Color(FurShade, alpha), 24);
        drawList.AddCircleFilled(Point(center, radius, tilt, -0.06f, -0.06f), radius * 0.93f, Color(Fur, alpha), 24);
        drawList.AddCircleFilled(Point(center, radius, tilt, -0.32f, -0.42f), radius * 0.28f, Color(White, 0.7f * alpha), 12);
        var eye = Color(Ink, alpha);
        var thickness = MathF.Max(1f, radius * 0.1f);
        for (var index = 0; index < 2; index++)
        {
            var eyeCenter = Point(center, radius, tilt, 0.28f + index * 0.36f, -0.1f);
            drawList.PathClear();
            drawList.PathArcTo(eyeCenter, radius * 0.12f, MathF.PI * 0.15f + tilt, MathF.PI * 0.85f + tilt, 8);
            drawList.PathStroke(eye, ImDrawFlags.None, thickness);
        }

        Shapes.FillEllipse(drawList, Point(center, radius, tilt, 0.84f, 0.14f), radius * 0.2f, radius * 0.15f, tilt,
            Color(Nose, alpha), EllipseSegments);
        var sway = MathF.Sin(time * 5f) * 0.12f;
        var antennaBase = Point(center, radius, tilt, 0.05f, -0.92f);
        var antennaTip = Point(center, radius, tilt, 0.22f + sway, -1.55f);
        drawList.AddLine(antennaBase, antennaTip, Color(Ink, alpha), MathF.Max(1f, radius * 0.07f));
        drawList.AddCircleFilled(antennaTip, radius * 0.5f, Color(Pom, 0.18f * alpha), 14);
        drawList.AddCircleFilled(antennaTip, radius * 0.26f, Color(Pom, alpha), 14);
        drawList.AddCircleFilled(antennaTip - new Vector2(radius * 0.08f, radius * 0.08f), radius * 0.08f, Color(White, 0.8f * alpha), 8);
    }

    private static Vector2 Point(Vector2 center, float radius, float tilt, float x, float y) =>
        center + Rotate(new Vector2(x * radius, y * radius), tilt);

    private static Vector2 Rotate(Vector2 value, float angle)
    {
        var cosine = MathF.Cos(angle);
        var sine = MathF.Sin(angle);
        return new Vector2(value.X * cosine - value.Y * sine, value.X * sine + value.Y * cosine);
    }

    private static uint Color(Vector4 color, float alpha) => ImGui.GetColorU32(color with { W = color.W * alpha });
}
