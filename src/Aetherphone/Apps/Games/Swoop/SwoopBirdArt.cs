using Aetherphone.Apps.Games.Framework;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Swoop;

internal enum SwoopWing : byte
{
    Folded,
    Flapping,
    Tucked,
}

internal struct SwoopBirdPose
{
    public Vector2 Center;
    public Vector2 Contact;
    public float Radius;
    public float Tilt;
    public float Squash;
    public float Stretch;
    public float FlapPhase;
    public float Altitude;
    public float Sleep;
    public float Fever;
    public SwoopWing Wing;
}

internal static class SwoopBirdArt
{
    private static readonly Vector4 Body = new(0.34f, 0.44f, 0.94f, 1f);
    private static readonly Vector4 WingTone = new(0.24f, 0.32f, 0.78f, 1f);
    private static readonly Vector4 Belly = new(1f, 0.94f, 0.82f, 1f);
    private static readonly Vector4 Beak = new(1f, 0.64f, 0.22f, 1f);
    private static readonly Vector4 Cheek = new(1f, 0.52f, 0.62f, 0.6f);
    private static readonly Vector4 Ink = new(0.10f, 0.11f, 0.20f, 1f);
    private static readonly Vector4 FeverGlow = new(0.80f, 1f, 0.40f, 1f);

    public static void DrawShadow(ImDrawListPtr drawList, in SwoopBirdPose pose, float lightAlpha)
    {
        var fade = 1f - Math.Clamp(pose.Altitude / 12f, 0f, 1f);
        if (fade <= 0f)
        {
            return;
        }

        var color = ImGui.GetColorU32(new Vector4(0.05f, 0.06f, 0.14f, 0.28f * fade * lightAlpha));
        Shapes.FillEllipse(drawList, pose.Contact, pose.Radius * (0.6f + 0.6f * fade), pose.Radius * 0.22f * fade + 1f, 0f,
            color, 20);
    }

    public static void Draw(ImDrawListPtr drawList, in SwoopBirdPose pose, Vector4 light, float time)
    {
        var radius = pose.Radius;
        var radiusX = radius * (1f + pose.Squash * 0.8f + pose.Stretch);
        var radiusY = radius * (1f - pose.Squash - pose.Stretch * 0.45f);
        var center = pose.Center;
        var cosine = MathF.Cos(pose.Tilt);
        var sine = MathF.Sin(pose.Tilt);
        if (pose.Fever > 0f)
        {
            var pulse = 0.75f + 0.25f * MathF.Sin(time * 9f);
            SwoopShapes.Glow(drawList, center, radius * 3f, FeverGlow, 1.6f * pose.Fever * pulse);
        }

        var body = ImGui.GetColorU32(Lit(Body, light));
        var wing = ImGui.GetColorU32(Lit(WingTone, light));
        var tail = ImGui.GetColorU32(Lit(WingTone, light));
        drawList.AddTriangleFilled(Point(center, cosine, sine, -radiusX * 0.82f, -radiusY * 0.05f),
            Point(center, cosine, sine, -radiusX * 1.42f, -radiusY * 0.55f),
            Point(center, cosine, sine, -radiusX * 1.32f, radiusY * 0.2f), tail);
        if (pose.Wing == SwoopWing.Flapping)
        {
            DrawWing(drawList, center, cosine, sine, radius, radiusX, radiusY, pose.Tilt, -0.55f + MathF.Sin(pose.FlapPhase) * 0.95f,
                wing, true);
        }

        Shapes.FillEllipse(drawList, center, radiusX, radiusY, pose.Tilt, body, 28);
        Shapes.FillEllipse(drawList, Point(center, cosine, sine, -radiusX * 0.22f, -radiusY * 0.38f), radiusX * 0.5f, radiusY * 0.28f,
            pose.Tilt - 0.35f, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.16f)), 20);
        Shapes.FillEllipse(drawList, Point(center, cosine, sine, radiusX * 0.14f, radiusY * 0.34f), radiusX * 0.6f, radiusY * 0.5f,
            pose.Tilt, ImGui.GetColorU32(Lit(Belly, light)), 24);
        if (pose.Wing == SwoopWing.Tucked)
        {
            DrawWing(drawList, center, cosine, sine, radius, radiusX, radiusY, pose.Tilt, -0.12f, wing, false);
        }
        else if (pose.Wing == SwoopWing.Folded)
        {
            DrawWing(drawList, center, cosine, sine, radius, radiusX, radiusY, pose.Tilt, 0.22f, wing, false);
        }

        DrawFace(drawList, center, cosine, sine, radius, radiusX, radiusY, pose, light, time);
    }

    private static void DrawWing(ImDrawListPtr drawList, Vector2 center, float cosine, float sine, float radius, float radiusX,
        float radiusY, float tilt, float wingAngle, uint color, bool spread)
    {
        var pivot = Point(center, cosine, sine, -radiusX * 0.12f, -radiusY * (spread ? 0.12f : -0.02f));
        var angle = tilt + wingAngle;
        var reach = spread ? radius * 0.62f : radius * 0.5f;
        var local = new Vector2(-reach * 0.55f, spread ? -reach * 0.45f : 0f);
        var wingCenter = pivot + SwoopShapes.Rotate(local, MathF.Cos(angle), MathF.Sin(angle));
        Shapes.FillEllipse(drawList, wingCenter, reach, radius * (spread ? 0.3f : 0.24f), angle, color, 20);
    }

    private static void DrawFace(ImDrawListPtr drawList, Vector2 center, float cosine, float sine, float radius, float radiusX,
        float radiusY, in SwoopBirdPose pose, Vector4 light, float time)
    {
        var beakColor = ImGui.GetColorU32(Lit(Beak, light));
        drawList.AddTriangleFilled(Point(center, cosine, sine, radiusX * 0.8f, -radiusY * 0.16f),
            Point(center, cosine, sine, radiusX * 1.28f, radiusY * 0.02f),
            Point(center, cosine, sine, radiusX * 0.8f, radiusY * 0.18f), beakColor);
        drawList.AddCircleFilled(Point(center, cosine, sine, radiusX * 0.36f, radiusY * 0.12f), radius * 0.13f,
            ImGui.GetColorU32(Cheek), 14);
        var eye = Point(center, cosine, sine, radiusX * 0.42f, -radiusY * 0.3f);
        var eyeRadius = radius * 0.24f;
        var ink = ImGui.GetColorU32(Ink);
        if (pose.Sleep >= 1f)
        {
            drawList.PathClear();
            drawList.PathArcTo(eye, eyeRadius * 0.75f, pose.Tilt + 0.3f, pose.Tilt + MathF.PI - 0.3f, 12);
            drawList.PathStroke(ink, ImDrawFlags.None, MathF.Max(1.2f, radius * 0.08f));
            DrawSnores(drawList, center, radius, time);
            return;
        }

        drawList.AddCircleFilled(eye, eyeRadius, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f)), 16);
        var gaze = new Vector2(cosine, sine) * eyeRadius * 0.28f;
        drawList.AddCircleFilled(eye + gaze, eyeRadius * 0.5f, ink, 14);
        drawList.AddCircleFilled(eye + gaze + new Vector2(-eyeRadius * 0.18f, -eyeRadius * 0.2f), eyeRadius * 0.16f,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.9f)), 8);
        if (pose.Sleep <= 0f)
        {
            return;
        }

        drawList.PathClear();
        drawList.PathArcTo(eye, eyeRadius * 1.05f, pose.Tilt + MathF.PI, pose.Tilt + MathF.PI * 2f, 12);
        drawList.PathLineTo(eye + SwoopShapes.Rotate(new Vector2(eyeRadius * 1.05f, eyeRadius * 0.1f), cosine, sine));
        drawList.PathLineTo(eye + SwoopShapes.Rotate(new Vector2(-eyeRadius * 1.05f, eyeRadius * 0.1f), cosine, sine));
        drawList.PathFillConvex(ImGui.GetColorU32(Lit(Body, light)));
    }

    private static void DrawSnores(ImDrawListPtr drawList, Vector2 center, float radius, float time)
    {
        for (var snore = 0; snore < 3; snore++)
        {
            var phase = SwoopShapes.Fraction(time * 0.45f + snore / 3f);
            var alpha = MathF.Sin(phase * MathF.PI);
            var size = radius * (0.32f + 0.28f * phase);
            var origin = center + new Vector2(radius * (0.8f + phase * 1.3f), -radius * (1.3f + phase * 2.4f));
            var color = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.85f * alpha));
            var thickness = MathF.Max(1.2f, radius * 0.09f);
            var topLeft = origin;
            var topRight = origin + new Vector2(size, 0f);
            var bottomLeft = origin + new Vector2(0f, size);
            var bottomRight = origin + new Vector2(size, size);
            drawList.AddLine(topLeft, topRight, color, thickness);
            drawList.AddLine(topRight, bottomLeft, color, thickness);
            drawList.AddLine(bottomLeft, bottomRight, color, thickness);
        }
    }

    private static Vector2 Point(Vector2 center, float cosine, float sine, float localX, float localY) =>
        center + SwoopShapes.Rotate(new Vector2(localX, localY), cosine, sine);

    private static Vector4 Lit(Vector4 color, Vector4 light) => SwoopShapes.Mix(color, SwoopShapes.Multiply(color, light), 0.45f);
}
