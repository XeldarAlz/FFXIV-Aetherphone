using Aetherphone.Apps.Games.Framework;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Updraft;

internal readonly struct ShapeFrame
{
    public readonly Vector2 Origin;
    public readonly Vector2 AxisX;
    public readonly Vector2 AxisY;

    public ShapeFrame(Vector2 origin, Vector2 axisX, Vector2 axisY)
    {
        Origin = origin;
        AxisX = axisX;
        AxisY = axisY;
    }

    public static ShapeFrame Rotated(Vector2 origin, float scaleX, float scaleY, float rotation)
    {
        var cosine = MathF.Cos(rotation);
        var sine = MathF.Sin(rotation);
        return new ShapeFrame(origin, new Vector2(cosine, sine) * scaleX, new Vector2(-sine, cosine) * scaleY);
    }

    public bool Mirrored => AxisX.X * AxisY.Y - AxisX.Y * AxisY.X < 0f;

    public Vector2 Point(float x, float y) => Origin + AxisX * x + AxisY * y;

    public Vector2 Point(Vector2 local) => Origin + AxisX * local.X + AxisY * local.Y;
}

internal static class UpdraftArt
{
    public static readonly Vector4 CrystalColor = new(0.50f, 0.93f, 1f, 1f);
    public static readonly Vector4 GoldColor = new(1f, 0.84f, 0.38f, 1f);
    public static readonly Vector4 FeatherColor = new(1f, 0.96f, 0.86f, 1f);
    public static readonly Vector4 BubbleColor = new(0.70f, 0.90f, 1f, 1f);
    public static readonly Vector4 StormGlow = new(0.62f, 0.56f, 1f, 1f);
    public static readonly Vector4 BoltColor = new(0.92f, 0.95f, 1f, 1f);
    public static readonly Vector4 CoilColor = new(0.98f, 0.52f, 0.64f, 1f);
    private const int EllipseSegments = 22;
    private const int CoilTurns = 6;

    private readonly struct Puff
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Radius;

        public Puff(float x, float y, float radius)
        {
            X = x;
            Y = y;
            Radius = radius;
        }
    }

    private static readonly Puff[] Puffs =
    {
        new(-0.62f, 0.48f, 0.36f), new(-0.25f, 0.30f, 0.48f), new(0.22f, 0.26f, 0.46f), new(0.62f, 0.46f, 0.36f),
        new(0f, 0.56f, 0.42f),
    };

    private static readonly Vector2[] CrackPoints =
    {
        new(-0.58f, 0.36f), new(-0.32f, 0.56f), new(-0.12f, 0.38f), new(0.10f, 0.62f), new(0.34f, 0.42f),
        new(0.56f, 0.60f),
    };

    private static readonly Vector4[] BodyColors =
    {
        new(0.98f, 0.99f, 1f, 1f), new(0.90f, 0.96f, 1f, 1f), new(0.90f, 0.87f, 0.96f, 1f), new(1f, 0.86f, 0.44f, 1f),
        new(0.90f, 0.99f, 0.96f, 1f), new(0.31f, 0.32f, 0.44f, 1f),
    };

    private static readonly Vector4[] ShadowColors =
    {
        new(0.70f, 0.78f, 0.92f, 1f), new(0.56f, 0.72f, 0.94f, 1f), new(0.62f, 0.56f, 0.74f, 1f), new(0.88f, 0.56f, 0.16f, 1f),
        new(0.52f, 0.80f, 0.74f, 1f), new(0.15f, 0.15f, 0.24f, 1f),
    };

    private static readonly Vector4[] HighlightColors =
    {
        new(1f, 1f, 1f, 1f), new(1f, 1f, 1f, 1f), new(0.99f, 0.98f, 1f, 1f), new(1f, 0.97f, 0.80f, 1f),
        new(1f, 1f, 1f, 1f), new(0.47f, 0.48f, 0.62f, 1f),
    };

    public static float Hash(float value)
    {
        var scrambled = MathF.Sin(value * 12.9898f) * 43758.5453f;
        return scrambled - MathF.Floor(scrambled);
    }

    public static uint Color(Vector4 color) => ImGui.GetColorU32(color);

    public static uint Color(Vector4 color, float alpha) => ImGui.GetColorU32(color with { W = color.W * alpha });

    public static Vector4 Lit(Vector4 color, Vector4 light) =>
        new(color.X * light.X, color.Y * light.Y, color.Z * light.Z, color.W);

    public static void FillEllipse(ImDrawListPtr drawList, in ShapeFrame frame, Vector2 center, Vector2 radii,
        float rotation, uint color)
    {
        var cosine = MathF.Cos(rotation);
        var sine = MathF.Sin(rotation);
        var mirrored = frame.Mirrored;
        drawList.PathClear();
        for (var segment = 0; segment < EllipseSegments; segment++)
        {
            var angle = (mirrored ? EllipseSegments - segment : segment) * MathF.Tau / EllipseSegments;
            var localX = MathF.Cos(angle) * radii.X;
            var localY = MathF.Sin(angle) * radii.Y;
            drawList.PathLineTo(frame.Point(center.X + localX * cosine - localY * sine,
                center.Y + localX * sine + localY * cosine));
        }

        drawList.PathFillConvex(color);
    }

    public static void FillTriangle(ImDrawListPtr drawList, in ShapeFrame frame, Vector2 first, Vector2 second,
        Vector2 third, uint color)
    {
        if (frame.Mirrored)
        {
            drawList.AddTriangleFilled(frame.Point(first), frame.Point(third), frame.Point(second), color);
            return;
        }

        drawList.AddTriangleFilled(frame.Point(first), frame.Point(second), frame.Point(third), color);
    }

    public static void Twinkle(ImDrawListPtr drawList, Vector2 center, float size, uint color)
    {
        var thickness = MathF.Max(1f, size * 0.28f);
        drawList.AddLine(center - new Vector2(size, 0f), center + new Vector2(size, 0f), color, thickness);
        drawList.AddLine(center - new Vector2(0f, size), center + new Vector2(0f, size), color, thickness);
    }

    public static void DrawCloud(ImDrawListPtr drawList, Vector2 top, float halfWidth, in UpdraftCloud cloud,
        Vector4 light, float time, float charge)
    {
        var kind = (int)cloud.Kind;
        var squash = cloud.Squash * cloud.Squash;
        var springy = cloud.Kind == UpdraftCloudKind.Spring ? 1.6f : 1f;
        var scaleX = 1f + 0.16f * squash * springy;
        var scaleY = 1f - 0.30f * squash * springy;
        var spread = cloud.Broken ? cloud.Dissolve : 0f;
        var alpha = 1f - spread * spread;
        var bob = MathF.Sin(time * 1.3f + cloud.Look * 0.01f) * 0.03f * halfWidth;
        var origin = new Vector2(top.X, top.Y + bob + spread * 0.55f * halfWidth);
        var storm = cloud.Kind == UpdraftCloudKind.Storm;
        var tint = storm ? Vector4.Lerp(light, Vector4.One, 0.5f) : light;
        var body = Color(Lit(BodyColors[kind], tint), alpha);
        var shadow = Color(Lit(ShadowColors[kind], tint), alpha);
        var highlight = Color(Lit(HighlightColors[kind], tint), alpha);
        switch (cloud.Kind)
        {
            case UpdraftCloudKind.Spring:
                DrawCoil(drawList, origin, halfWidth, squash, alpha);
                break;
            case UpdraftCloudKind.Golden:
                ProgressRing.Glow(origin + new Vector2(0f, 0.45f * halfWidth), halfWidth * 1.3f, GoldColor,
                    (0.55f + 0.25f * MathF.Sin(time * 3.2f + cloud.Look)) * alpha);
                break;
            case UpdraftCloudKind.Storm:
            {
                var flicker = 0.6f + 0.4f * Hash(MathF.Floor(time * 12f) + cloud.Look);
                ProgressRing.Glow(origin + new Vector2(0f, 0.45f * halfWidth), halfWidth * 1.35f, StormGlow,
                    (0.2f + charge * 0.75f * flicker) * alpha);
                break;
            }
            case UpdraftCloudKind.Drifting:
                DrawWisps(drawList, origin, halfWidth, cloud.DriftSpeed, time, cloud.Look, alpha, light);
                break;
        }

        DrawPuffs(drawList, origin, halfWidth, scaleX, scaleY, spread, cloud.Look, 0.12f * halfWidth, shadow);
        DrawPuffs(drawList, origin, halfWidth, scaleX, scaleY, spread, cloud.Look, 0f, body);
        DrawHighlights(drawList, origin, halfWidth, scaleX, scaleY, spread, cloud.Look, highlight);
        switch (cloud.Kind)
        {
            case UpdraftCloudKind.Fragile:
                DrawCracks(drawList, origin, halfWidth, scaleX, scaleY, spread, alpha);
                break;
            case UpdraftCloudKind.Golden:
                DrawGoldTwinkles(drawList, origin, halfWidth, time, cloud.Look, alpha);
                break;
            case UpdraftCloudKind.Spring:
                DrawSwirl(drawList, origin, halfWidth, scaleY, shadow);
                break;
            case UpdraftCloudKind.Storm:
                DrawStormBolts(drawList, origin, halfWidth, time, cloud, charge, alpha);
                break;
        }
    }

    private static void DrawPuffs(ImDrawListPtr drawList, Vector2 origin, float halfWidth, float scaleX, float scaleY,
        float spread, int look, float drop, uint color)
    {
        var averageScale = (scaleX + scaleY) * 0.5f;
        var shrink = 1f - 0.3f * spread;
        for (var index = 0; index < Puffs.Length; index++)
        {
            ref readonly var puff = ref Puffs[index];
            var jitter = 0.92f + 0.16f * (((look >> (index * 3)) & 7) / 7f);
            var separation = MathF.Sign(puff.X) * spread * 0.7f * halfWidth;
            var center = new Vector2(origin.X + puff.X * halfWidth * scaleX + separation,
                origin.Y + puff.Y * halfWidth * scaleY + drop + spread * index * 0.08f * halfWidth);
            drawList.AddCircleFilled(center, puff.Radius * halfWidth * jitter * averageScale * shrink, color);
        }

        if (spread > 0f)
        {
            return;
        }

        var baseMin = new Vector2(origin.X - 0.78f * halfWidth * scaleX, origin.Y + 0.42f * halfWidth * scaleY + drop);
        var baseMax = new Vector2(origin.X + 0.78f * halfWidth * scaleX, origin.Y + 0.80f * halfWidth * scaleY + drop);
        drawList.AddRectFilled(baseMin, baseMax, color, (baseMax.Y - baseMin.Y) * 0.5f);
    }

    private static void DrawHighlights(ImDrawListPtr drawList, Vector2 origin, float halfWidth, float scaleX,
        float scaleY, float spread, int look, uint color)
    {
        var averageScale = (scaleX + scaleY) * 0.5f;
        for (var index = 0; index < 4; index++)
        {
            ref readonly var puff = ref Puffs[index];
            var jitter = 0.92f + 0.16f * (((look >> (index * 3)) & 7) / 7f);
            var radius = puff.Radius * halfWidth * jitter * averageScale * (1f - 0.3f * spread);
            var separation = MathF.Sign(puff.X) * spread * 0.7f * halfWidth;
            var center = new Vector2(origin.X + puff.X * halfWidth * scaleX + separation - radius * 0.12f,
                origin.Y + puff.Y * halfWidth * scaleY - radius * 0.2f + spread * index * 0.08f * halfWidth);
            drawList.AddCircleFilled(center, radius * 0.62f, color);
        }
    }

    private static void DrawCracks(ImDrawListPtr drawList, Vector2 origin, float halfWidth, float scaleX, float scaleY,
        float spread, float alpha)
    {
        if (spread > 0.35f)
        {
            return;
        }

        var color = Color(new Vector4(0.42f, 0.36f, 0.56f, 0.85f), alpha);
        drawList.PathClear();
        for (var index = 0; index < CrackPoints.Length; index++)
        {
            var point = CrackPoints[index];
            var separation = MathF.Sign(point.X) * spread * 0.7f * halfWidth;
            drawList.PathLineTo(new Vector2(origin.X + point.X * halfWidth * scaleX + separation,
                origin.Y + point.Y * halfWidth * scaleY));
        }

        drawList.PathStroke(color, ImDrawFlags.None, MathF.Max(1f, halfWidth * 0.06f));
        var branch = new Vector2(origin.X + CrackPoints[2].X * halfWidth * scaleX, origin.Y + CrackPoints[2].Y * halfWidth * scaleY);
        drawList.AddLine(branch, branch + new Vector2(0.06f, -0.2f) * halfWidth, color, MathF.Max(1f, halfWidth * 0.045f));
    }

    private static void DrawGoldTwinkles(ImDrawListPtr drawList, Vector2 origin, float halfWidth, float time, int look,
        float alpha)
    {
        for (var index = 0; index < 3; index++)
        {
            ref readonly var puff = ref Puffs[index + 1];
            var pulse = 0.5f + 0.5f * MathF.Sin(time * 4.2f + index * 2.1f + look);
            var center = new Vector2(origin.X + puff.X * halfWidth * 1.1f,
                origin.Y + (puff.Y - puff.Radius * 0.8f) * halfWidth);
            Twinkle(drawList, center, halfWidth * (0.06f + 0.08f * pulse), Color(new Vector4(1f, 1f, 0.9f, 0.9f), alpha * pulse));
        }
    }

    private static void DrawCoil(ImDrawListPtr drawList, Vector2 origin, float halfWidth, float squash, float alpha)
    {
        var top = origin.Y + 0.6f * halfWidth;
        var bottom = origin.Y + (1.3f - 0.35f * squash) * halfWidth;
        var color = Color(CoilColor, alpha);
        var thickness = MathF.Max(1.5f, halfWidth * 0.1f);
        drawList.PathClear();
        for (var index = 0; index <= CoilTurns; index++)
        {
            var side = index % 2 == 0 ? -1f : 1f;
            var y = top + (bottom - top) * index / CoilTurns;
            drawList.PathLineTo(new Vector2(origin.X + side * 0.26f * halfWidth, y));
        }

        drawList.PathStroke(color, ImDrawFlags.None, thickness);
        var plateMin = new Vector2(origin.X - 0.36f * halfWidth, bottom - thickness * 0.5f);
        var plateMax = new Vector2(origin.X + 0.36f * halfWidth, bottom + thickness * 1.3f);
        drawList.AddRectFilled(plateMin, plateMax, Color(GamePalette.Darken(CoilColor, 0.2f), alpha), thickness);
    }

    private static void DrawSwirl(ImDrawListPtr drawList, Vector2 origin, float halfWidth, float scaleY, uint color)
    {
        var center = new Vector2(origin.X, origin.Y + 0.5f * halfWidth * scaleY);
        drawList.PathClear();
        drawList.PathArcTo(center, 0.2f * halfWidth, -MathF.PI * 0.2f, MathF.PI * 1.4f, 14);
        drawList.PathStroke(color, ImDrawFlags.None, MathF.Max(1f, halfWidth * 0.06f));
        drawList.PathClear();
        drawList.PathArcTo(center, 0.09f * halfWidth, MathF.PI * 0.4f, MathF.PI * 1.9f, 10);
        drawList.PathStroke(color, ImDrawFlags.None, MathF.Max(1f, halfWidth * 0.05f));
    }

    private static void DrawWisps(ImDrawListPtr drawList, Vector2 origin, float halfWidth, float driftSpeed, float time,
        int look, float alpha, Vector4 light)
    {
        var behind = driftSpeed >= 0f ? -1f : 1f;
        var color = Color(Lit(new Vector4(1f, 1f, 1f, 0.55f), light), alpha);
        for (var index = 0; index < 3; index++)
        {
            var phase = (time * 1.4f + index * 0.37f + look * 0.013f) % 1f;
            var y = origin.Y + (0.28f + index * 0.2f) * halfWidth;
            var start = origin.X + behind * (0.92f + phase * 0.25f) * halfWidth;
            var end = start + behind * (0.3f + 0.25f * (1f - phase)) * halfWidth;
            drawList.AddLine(new Vector2(start, y), new Vector2(end, y), color, MathF.Max(1f, halfWidth * 0.05f));
        }
    }

    private static void DrawStormBolts(ImDrawListPtr drawList, Vector2 origin, float halfWidth, float time,
        in UpdraftCloud cloud, float charge, float alpha)
    {
        var bucket = MathF.Floor(time * 14f);
        var threshold = cloud.Cooldown > 0f ? 0f : charge > 0.1f ? 1f - charge * 0.85f : 0.94f;
        var sparkColor = Color(BoltColor, alpha * (0.4f + 0.6f * charge));
        for (var spark = 0; spark < 3; spark++)
        {
            var sparkRoll = Hash(bucket * 1.7f + spark * 5.3f + cloud.Look);
            if (sparkRoll < 1f - (0.25f + charge * 0.6f))
            {
                continue;
            }

            var sparkCenter = new Vector2(origin.X + (sparkRoll * 1.2f - 0.6f) * halfWidth,
                origin.Y + (0.1f + 0.2f * Hash(bucket + spark)) * halfWidth);
            Twinkle(drawList, sparkCenter, halfWidth * 0.07f, sparkColor);
        }

        if (Hash(bucket + cloud.Look * 0.37f) < threshold)
        {
            return;
        }

        var glow = Color(StormGlow with { W = 0.45f }, alpha);
        var core = Color(BoltColor, alpha);
        var thickness = MathF.Max(1.2f, halfWidth * 0.07f);
        var point = new Vector2(origin.X + (Hash(bucket * 3.1f + cloud.Look) - 0.5f) * halfWidth, origin.Y + 0.78f * halfWidth);
        for (var segment = 0; segment < 4; segment++)
        {
            var next = point + new Vector2((Hash(bucket + segment * 7.7f + cloud.Look) - 0.5f) * 0.36f * halfWidth,
                0.22f * halfWidth);
            drawList.AddLine(point, next, glow, thickness * 2.6f);
            drawList.AddLine(point, next, core, thickness);
            point = next;
        }
    }

    public static void DrawCrystal(ImDrawListPtr drawList, Vector2 center, float size, float spin)
    {
        var width = size * (0.35f + 0.65f * MathF.Abs(MathF.Cos(spin)));
        var height = size * 1.3f;
        ProgressRing.Glow(center, size * 1.7f, CrystalColor, 0.5f);
        var top = center - new Vector2(0f, height);
        var bottom = center + new Vector2(0f, height);
        var left = center - new Vector2(width, 0f);
        var right = center + new Vector2(width, 0f);
        var facing = MathF.Cos(spin) >= 0f;
        var light = Color(GamePalette.Lighten(CrystalColor, 0.35f));
        var dark = Color(GamePalette.Darken(CrystalColor, 0.25f));
        drawList.AddTriangleFilled(top, right, bottom, facing ? dark : light);
        drawList.AddTriangleFilled(top, bottom, left, facing ? light : dark);
        drawList.AddLine(top, bottom, Color(new Vector4(1f, 1f, 1f, 0.55f)), MathF.Max(1f, size * 0.08f));
        Twinkle(drawList, center + new Vector2(-width * 0.3f, -height * 0.45f), size * 0.22f,
            Color(new Vector4(1f, 1f, 1f, 0.9f)));
    }

    public static void DrawFeather(ImDrawListPtr drawList, Vector2 center, float size, float sway)
    {
        ProgressRing.Glow(center, size * 1.9f, GoldColor, 0.45f);
        var frame = ShapeFrame.Rotated(center, size, size, -0.6f + sway);
        FillEllipse(drawList, frame, new Vector2(0f, -0.15f), new Vector2(0.42f, 1.05f), 0f, Color(FeatherColor));
        FillEllipse(drawList, frame, new Vector2(0.12f, -0.2f), new Vector2(0.16f, 0.78f), 0f,
            Color(new Vector4(1f, 1f, 1f, 0.8f)));
        var quillColor = Color(GamePalette.Darken(GoldColor, 0.2f));
        drawList.AddLine(frame.Point(0f, -1.05f), frame.Point(0f, 1.3f), quillColor, MathF.Max(1f, size * 0.1f));
        for (var notch = 0; notch < 3; notch++)
        {
            var y = -0.5f + notch * 0.4f;
            drawList.AddLine(frame.Point(0f, y), frame.Point(-0.38f, y - 0.22f), quillColor, MathF.Max(1f, size * 0.05f));
        }
    }

    public static void DrawBubble(ImDrawListPtr drawList, Vector2 center, float radius, float time, float alpha)
    {
        var wobble = 1f + 0.04f * MathF.Sin(time * 5.1f);
        var radiusX = radius * wobble;
        drawList.AddCircleFilled(center, radiusX, Color(BubbleColor with { W = 0.16f * alpha }));
        drawList.AddCircle(center, radiusX, Color(BubbleColor with { W = 0.75f * alpha }), 0, MathF.Max(1f, radius * 0.08f));
        drawList.PathClear();
        drawList.PathArcTo(center, radiusX * 0.72f, MathF.PI * 1.1f, MathF.PI * 1.45f, 8);
        drawList.PathStroke(Color(new Vector4(1f, 1f, 1f, 0.8f * alpha)), ImDrawFlags.None, MathF.Max(1f, radius * 0.12f));
        drawList.AddCircleFilled(center + new Vector2(radiusX * 0.42f, -radiusX * 0.5f), radius * 0.08f,
            Color(new Vector4(1f, 1f, 1f, 0.7f * alpha)));
    }
}
