using Aetherphone.Apps.Games.Framework;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Trailblaze;

internal struct TrailblazePose
{
    public Vector2 Feet;
    public Vector2 Ground;
    public float Scale;
    public float Angle;
    public float RunPhase;
    public float Slide;
    public float Air;
    public float Squash;
    public float Wings;
    public float Flap;
    public float Alpha;
    public float Glow;
    public float Height;
    public float Look;
    public float Shadow;
}

internal static class TrailblazeChocobo
{
    private const int TailFeathers = 5;
    private const int CrestFeathers = 3;
    private const int BigFeathers = 5;
    private static readonly Vector4 Plumage = new(1f, 0.82f, 0.26f, 1f);
    private static readonly Vector4 PlumageShade = new(0.92f, 0.62f, 0.14f, 1f);
    private static readonly Vector4 PlumageLight = new(1f, 0.94f, 0.62f, 1f);
    private static readonly Vector4 TailCream = new(1f, 0.92f, 0.62f, 1f);
    private static readonly Vector4 Crest = new(0.98f, 0.70f, 0.18f, 1f);
    private static readonly Vector4 Beak = new(0.98f, 0.58f, 0.16f, 1f);
    private static readonly Vector4 Leg = new(0.98f, 0.66f, 0.22f, 1f);
    private static readonly Vector4 Talon = new(0.45f, 0.30f, 0.18f, 1f);
    private static readonly Vector4 Cloth = new(0.24f, 0.44f, 0.86f, 1f);
    private static readonly Vector4 ClothTrim = new(0.97f, 0.97f, 1f, 1f);
    private static readonly Vector4 BigWing = new(0.86f, 0.95f, 1f, 1f);
    private static readonly Vector4 BigWingTip = new(0.55f, 0.82f, 1f, 1f);
    private static readonly Vector4 Gold = new(1f, 0.84f, 0.3f, 1f);
    private static readonly Vector4 Shade = new(0f, 0f, 0f, 1f);

    public static void Draw(ImDrawListPtr drawList, in TrailblazePose pose)
    {
        if (pose.Alpha <= 0.01f || pose.Scale <= 0.5f)
        {
            return;
        }

        DrawShadow(drawList, pose);
        var bob = (1f - pose.Air) * (1f - pose.Slide) * 0.07f * MathF.Abs(MathF.Sin(pose.RunPhase));
        var bodyY = Lerp(1.15f, 0.62f, pose.Slide) + bob;
        var bodyRadiusX = Lerp(0.5f, 0.6f, pose.Slide);
        var bodyRadiusY = Lerp(0.42f, 0.3f, pose.Slide);
        if (pose.Glow > 0.01f)
        {
            drawList.AddCircleFilled(Point(pose, 0f, bodyY + 0.2f), 1.2f * pose.Scale,
                Color(Gold, 0.16f * pose.Glow * pose.Alpha), 24);
        }

        if (pose.Wings > 0.01f)
        {
            DrawBigWings(drawList, pose, bodyY);
        }

        DrawNeckAndHead(drawList, pose, bodyY, bodyRadiusY);
        DrawLegs(drawList, pose, bodyY);
        DrawBody(drawList, pose, bodyY, bodyRadiusX, bodyRadiusY);
        DrawSmallWings(drawList, pose, bodyY, bodyRadiusX);
        DrawTail(drawList, pose, bodyY);
    }

    private static void DrawShadow(ImDrawListPtr drawList, in TrailblazePose pose)
    {
        if (pose.Shadow <= 0.01f)
        {
            return;
        }

        var shrink = 1f / (1f + MathF.Max(0f, pose.Height) * 0.4f);
        Shapes.FillEllipse(drawList, pose.Ground, 0.58f * pose.Scale * shrink, 0.17f * pose.Scale * shrink,
            Color(Shade, 0.3f * shrink * pose.Alpha * pose.Shadow), 18);
    }

    private static void DrawBigWings(ImDrawListPtr drawList, in TrailblazePose pose, float bodyY)
    {
        var beat = MathF.Sin(pose.Flap) * 0.35f;
        for (var side = -1; side <= 1; side += 2)
        {
            var shoulder = new Vector2(side * 0.3f, bodyY + 0.22f);
            for (var feather = BigFeathers - 1; feather >= 0; feather--)
            {
                var spread = 0.55f - feather * 0.2f + beat;
                var length = (1.45f - feather * 0.12f) * pose.Wings;
                var direction = new Vector2(side * MathF.Cos(spread), MathF.Sin(spread));
                var middle = shoulder + direction * length * 0.5f;
                var color = Vector4.Lerp(BigWing, BigWingTip, feather / (float)BigFeathers);
                Feather(drawList, pose, middle, length * 0.5f, 0.15f * pose.Wings, direction, Color(color, pose.Alpha * 0.92f));
            }
        }
    }

    private static void DrawNeckAndHead(ImDrawListPtr drawList, in TrailblazePose pose, float bodyY, float bodyRadiusY)
    {
        var head = new Vector2(Lerp(0.05f, 0.02f, pose.Slide) + pose.Look * 0.06f, Lerp(1.98f, 1.02f, pose.Slide) + bodyY - 1.15f);
        var headRadius = Lerp(0.24f, 0.21f, pose.Slide);
        var neckBase = new Vector2(0f, bodyY + bodyRadiusY * 0.5f);
        var plumage = Color(Plumage, pose.Alpha);
        drawList.AddLine(Point(pose, neckBase.X, neckBase.Y), Point(pose, head.X, head.Y), plumage, 0.3f * pose.Scale);
        for (var crest = 0; crest < CrestFeathers; crest++)
        {
            var angle = (crest - 1) * 0.42f - pose.Look * 0.2f;
            var direction = new Vector2(MathF.Sin(angle), MathF.Cos(angle));
            var tip = head + direction * (headRadius + 0.3f);
            var baseLeft = head + new Vector2(-direction.Y, direction.X) * 0.08f + direction * headRadius * 0.4f;
            var baseRight = head - new Vector2(-direction.Y, direction.X) * 0.08f + direction * headRadius * 0.4f;
            drawList.AddTriangleFilled(Point(pose, baseLeft.X, baseLeft.Y), Point(pose, tip.X, tip.Y),
                Point(pose, baseRight.X, baseRight.Y), Color(Crest, pose.Alpha));
        }

        var beakSide = pose.Look >= 0f ? 1f : -1f;
        var beakBase = head + new Vector2(beakSide * headRadius * 0.6f, -headRadius * 0.1f);
        var beakTip = head + new Vector2(beakSide * (headRadius + 0.2f), -headRadius * 0.3f);
        var beakLow = head + new Vector2(beakSide * headRadius * 0.5f, -headRadius * 0.5f);
        drawList.AddTriangleFilled(Point(pose, beakBase.X, beakBase.Y), Point(pose, beakTip.X, beakTip.Y),
            Point(pose, beakLow.X, beakLow.Y), Color(Beak, pose.Alpha));
        drawList.AddCircleFilled(Point(pose, head.X, head.Y), headRadius * pose.Scale, plumage, 20);
        drawList.AddCircleFilled(Point(pose, head.X - 0.06f, head.Y + 0.08f), headRadius * 0.45f * pose.Scale,
            Color(PlumageLight, pose.Alpha * 0.7f), 14);
    }

    private static void DrawLegs(ImDrawListPtr drawList, in TrailblazePose pose, float bodyY)
    {
        var visible = (1f - pose.Slide) * pose.Alpha;
        if (visible <= 0.05f)
        {
            return;
        }

        var leg = Color(Leg, visible);
        var talon = Color(Talon, visible);
        var thickness = MathF.Max(1f, 0.09f * pose.Scale);
        for (var side = -1; side <= 1; side += 2)
        {
            var stride = side < 0 ? MathF.Max(0f, MathF.Sin(pose.RunPhase)) : MathF.Max(0f, -MathF.Sin(pose.RunPhase));
            var lift = Lerp(stride, 0.55f, pose.Air);
            var hip = new Vector2(side * 0.2f, bodyY - 0.28f);
            var knee = new Vector2(side * 0.29f, Lerp(0.45f, 0.5f, pose.Air) + lift * 0.22f);
            var foot = new Vector2(side * Lerp(0.22f, 0.16f, pose.Air), lift * 0.34f);
            drawList.AddLine(Point(pose, hip.X, hip.Y), Point(pose, knee.X, knee.Y), leg, thickness * 1.3f);
            drawList.AddLine(Point(pose, knee.X, knee.Y), Point(pose, foot.X, foot.Y), leg, thickness);
            Shapes.FillEllipse(drawList, Point(pose, foot.X, foot.Y), 0.12f * pose.Scale, 0.055f * pose.Scale,
                pose.Angle, leg, 10);
            drawList.AddLine(Point(pose, foot.X - 0.07f, foot.Y - 0.02f), Point(pose, foot.X - 0.1f, foot.Y - 0.07f), talon,
                MathF.Max(1f, thickness * 0.5f));
            drawList.AddLine(Point(pose, foot.X + 0.07f, foot.Y - 0.02f), Point(pose, foot.X + 0.1f, foot.Y - 0.07f), talon,
                MathF.Max(1f, thickness * 0.5f));
        }
    }

    private static void DrawBody(ImDrawListPtr drawList, in TrailblazePose pose, float bodyY, float radiusX, float radiusY)
    {
        var squashX = 1f + 0.14f * pose.Squash;
        var squashY = 1f - 0.16f * pose.Squash;
        var center = Point(pose, 0f, bodyY);
        Shapes.FillEllipse(drawList, center, radiusX * squashX * pose.Scale, radiusY * squashY * pose.Scale,
            pose.Angle, Color(PlumageShade, pose.Alpha), 24);
        Shapes.FillEllipse(drawList, Point(pose, 0f, bodyY + radiusY * 0.12f), radiusX * squashX * 0.94f * pose.Scale,
            radiusY * squashY * 0.86f * pose.Scale, pose.Angle, Color(Plumage, pose.Alpha), 24);
        Shapes.FillEllipse(drawList, Point(pose, -radiusX * 0.25f, bodyY + radiusY * 0.45f),
            radiusX * 0.42f * pose.Scale, radiusY * 0.3f * pose.Scale, pose.Angle, Color(PlumageLight, pose.Alpha * 0.75f), 16);
        var clothY = bodyY + radiusY * 0.52f;
        Shapes.FillEllipse(drawList, Point(pose, 0f, clothY), radiusX * 0.62f * pose.Scale, radiusY * 0.3f * pose.Scale,
            pose.Angle, Color(ClothTrim, pose.Alpha), 18);
        Shapes.FillEllipse(drawList, Point(pose, 0f, clothY), radiusX * 0.54f * pose.Scale, radiusY * 0.22f * pose.Scale,
            pose.Angle, Color(Cloth, pose.Alpha), 18);
    }

    private static void DrawSmallWings(ImDrawListPtr drawList, in TrailblazePose pose, float bodyY, float radiusX)
    {
        if (pose.Wings > 0.6f)
        {
            return;
        }

        var flap = pose.Air * (0.35f + 0.55f * MathF.Abs(MathF.Sin(pose.Flap * 1.6f)));
        for (var side = -1; side <= 1; side += 2)
        {
            var angle = side * (0.4f + flap);
            var direction = new Vector2(MathF.Sin(angle), MathF.Cos(angle));
            var middle = new Vector2(side * radiusX * 0.92f, bodyY + 0.05f) + direction * 0.12f;
            Feather(drawList, pose, middle, 0.28f, 0.13f, direction, Color(PlumageShade, pose.Alpha));
            Feather(drawList, pose, middle + new Vector2(side * 0.02f, 0.03f), 0.22f, 0.09f, direction,
                Color(Plumage, pose.Alpha));
        }
    }

    private static void DrawTail(ImDrawListPtr drawList, in TrailblazePose pose, float bodyY)
    {
        var sway = MathF.Sin(pose.RunPhase) * 0.1f * (1f - pose.Air);
        var root = new Vector2(0f, bodyY - Lerp(0.12f, 0.02f, pose.Slide));
        for (var feather = 0; feather < TailFeathers; feather++)
        {
            var angle = (feather - (TailFeathers - 1) * 0.5f) * 0.34f + sway;
            var direction = new Vector2(MathF.Sin(angle), MathF.Cos(angle) * 0.85f);
            var length = feather == TailFeathers / 2 ? 0.34f : 0.28f;
            var middle = root + direction * length * 0.6f;
            Feather(drawList, pose, middle, length * 0.6f, 0.085f, direction, Color(TailCream, pose.Alpha));
        }

        drawList.AddCircleFilled(Point(pose, root.X, root.Y), 0.12f * pose.Scale, Color(TailCream, pose.Alpha), 12);
    }

    private static void Feather(ImDrawListPtr drawList, in TrailblazePose pose, Vector2 middle, float halfLength,
        float halfWidth, Vector2 direction, uint color)
    {
        var screenDirection = Rotate(new Vector2(direction.X, -direction.Y), pose.Angle);
        Shapes.FillEllipse(drawList, Point(pose, middle.X, middle.Y), halfLength * pose.Scale, halfWidth * pose.Scale,
            MathF.Atan2(screenDirection.Y, screenDirection.X), color, 12);
    }

    private static Vector2 Point(in TrailblazePose pose, float x, float y)
    {
        var local = new Vector2(x * pose.Scale, -y * pose.Scale);
        return pose.Feet + Rotate(local, pose.Angle);
    }

    private static Vector2 Rotate(Vector2 value, float angle)
    {
        var cosine = MathF.Cos(angle);
        var sine = MathF.Sin(angle);
        return new Vector2(value.X * cosine - value.Y * sine, value.X * sine + value.Y * cosine);
    }

    private static uint Color(Vector4 color, float alpha) => ImGui.GetColorU32(color with { W = color.W * alpha });

    private static float Lerp(float from, float to, float amount) => from + (to - from) * amount;
}
