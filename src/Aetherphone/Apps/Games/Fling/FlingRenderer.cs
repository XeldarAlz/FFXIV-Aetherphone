using Aetherphone.Apps.Games.Framework;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Fling;

internal static class FlingRenderer
{
    public static readonly Vector4 Wood = new(0.82f, 0.58f, 0.33f, 1f);
    public static readonly Vector4 Glass = new(0.72f, 0.92f, 1f, 1f);
    public static readonly Vector4 Stone = new(0.62f, 0.64f, 0.70f, 1f);
    public static readonly Vector4 GoblinSkin = new(0.50f, 0.80f, 0.36f, 1f);
    public static readonly Vector4 NormalBird = new(0.99f, 0.80f, 0.22f, 1f);
    public static readonly Vector4 SplitterBird = new(0.38f, 0.68f, 1f, 1f);
    public static readonly Vector4 HeavyBird = new(0.34f, 0.29f, 0.42f, 1f);
    private const float GrassDepth = 0.2f;
    private const float TuftSpacing = 0.55f;
    private const float PostHalfWidth = 0.11f;
    private const float PreviewDotRadius = 0.07f;
    private const float TrailDotRadius = 0.05f;
    private static readonly Vector2 Trunk = new(0f, -1.55f);
    private static readonly Vector2 BackProng = new(0.3f, -2.25f);
    private static readonly Vector2 FrontProng = new(-0.3f, -2.25f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Pupil = new(0.08f, 0.08f, 0.10f, 1f);
    private static readonly Vector4 Beak = new(1f, 0.56f, 0.16f, 1f);
    private static readonly Vector4 WoodEdge = new(0.46f, 0.29f, 0.14f, 1f);
    private static readonly Vector4 WoodGrain = new(0.64f, 0.42f, 0.21f, 1f);
    private static readonly Vector4 GlassFill = new(0.72f, 0.92f, 1f, 0.38f);
    private static readonly Vector4 GlassEdge = new(0.88f, 0.98f, 1f, 0.95f);
    private static readonly Vector4 StoneEdge = new(0.36f, 0.37f, 0.42f, 1f);
    private static readonly Vector4 StoneSpeck = new(0.47f, 0.49f, 0.55f, 1f);
    private static readonly Vector4 RockFill = new(0.44f, 0.40f, 0.37f, 1f);
    private static readonly Vector4 RockTop = new(0.58f, 0.55f, 0.48f, 1f);
    private static readonly Vector4 RockEdge = new(0.28f, 0.25f, 0.24f, 1f);
    private static readonly Vector4 Crack = new(0.10f, 0.08f, 0.08f, 0.75f);
    private static readonly Vector4 Grass = new(0.38f, 0.72f, 0.30f, 1f);
    private static readonly Vector4 GrassDark = new(0.24f, 0.52f, 0.22f, 1f);
    private static readonly Vector4 DirtTop = new(0.50f, 0.36f, 0.22f, 1f);
    private static readonly Vector4 DirtBottom = new(0.26f, 0.17f, 0.10f, 1f);
    private static readonly Vector4 SlingWood = new(0.56f, 0.36f, 0.20f, 1f);
    private static readonly Vector4 SlingDark = new(0.36f, 0.22f, 0.12f, 1f);
    private static readonly Vector4 Band = new(0.34f, 0.17f, 0.12f, 1f);
    private static readonly Vector4 GoblinDark = new(0.26f, 0.50f, 0.20f, 1f);
    private static readonly Vector4 Helmet = new(0.72f, 0.74f, 0.80f, 1f);
    private static readonly Vector4 HelmetDark = new(0.46f, 0.48f, 0.54f, 1f);
    private static readonly Vector4 Shadow = new(0f, 0f, 0f, 0.22f);

    public static Vector4 BirdColor(FlingBird bird) => bird switch
    {
        FlingBird.Splitter => SplitterBird,
        FlingBird.Heavy => HeavyBird,
        _ => NormalBird,
    };

    public static Vector4 MaterialColor(FlingMaterial material) => material switch
    {
        FlingMaterial.Glass => Glass,
        FlingMaterial.Stone => Stone,
        FlingMaterial.Rock => RockFill,
        _ => Wood,
    };

    public static void DrawGround(ImDrawListPtr drawList, in Camera2D camera)
    {
        var visible = camera.VisibleWorld;
        var left = visible.Min.X - 1f;
        var right = visible.Max.X + 1f;
        var bottom = MathF.Max(visible.Max.Y + 1f, 1f);
        var topLeft = camera.ToScreen(new Vector2(left, 0f));
        var bottomRight = camera.ToScreen(new Vector2(right, bottom));
        var dirtTop = ImGui.GetColorU32(DirtTop);
        var dirtBottom = ImGui.GetColorU32(DirtBottom);
        drawList.AddRectFilledMultiColor(topLeft, bottomRight, dirtTop, dirtTop, dirtBottom, dirtBottom);
        var grassBottom = camera.ToScreen(new Vector2(right, GrassDepth));
        drawList.AddRectFilled(topLeft, grassBottom, ImGui.GetColorU32(Grass));
        drawList.AddLine(topLeft, new Vector2(grassBottom.X, topLeft.Y), ImGui.GetColorU32(GamePalette.Lighten(Grass, 0.25f)),
            MathF.Max(1f, camera.Px(0.04f)));
        var tuft = ImGui.GetColorU32(GrassDark);
        var first = (int)MathF.Floor(left / TuftSpacing);
        var last = (int)MathF.Ceiling(right / TuftSpacing);
        for (var index = first; index <= last; index++)
        {
            var hash = Hash(index);
            var x = index * TuftSpacing + (hash & 15) / 15f * 0.3f;
            var height = 0.12f + ((hash >> 4) & 15) / 15f * 0.14f;
            var baseLeft = camera.ToScreen(new Vector2(x - 0.06f, 0.02f));
            var baseRight = camera.ToScreen(new Vector2(x + 0.06f, 0.02f));
            var tip = camera.ToScreen(new Vector2(x + 0.03f, -height));
            drawList.AddTriangleFilled(baseLeft, tip, baseRight, tuft);
        }
    }

    public static void DrawRock(ImDrawListPtr drawList, in Camera2D camera, Vector2 center, Vector2 half)
    {
        var min = camera.ToScreen(center - half);
        var max = camera.ToScreen(center + half);
        var rounding = camera.Px(0.12f);
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(RockFill), rounding);
        drawList.AddRectFilled(min, new Vector2(max.X, min.Y + camera.Px(0.16f)), ImGui.GetColorU32(RockTop), rounding);
        drawList.AddRect(min, max, ImGui.GetColorU32(RockEdge), rounding, ImDrawFlags.None, MathF.Max(1f, camera.Px(0.05f)));
    }

    public static void DrawBlock(ImDrawListPtr drawList, in Camera2D camera, Vector2 center, float angle, Vector2 half,
        FlingMaterial material, float damage, int seed)
    {
        var axisX = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        var axisY = new Vector2(-axisX.Y, axisX.X);
        var a = camera.ToScreen(center - axisX * half.X - axisY * half.Y);
        var b = camera.ToScreen(center + axisX * half.X - axisY * half.Y);
        var c = camera.ToScreen(center + axisX * half.X + axisY * half.Y);
        var d = camera.ToScreen(center - axisX * half.X + axisY * half.Y);
        var edgeWidth = MathF.Max(1f, camera.Px(0.04f));
        switch (material)
        {
            case FlingMaterial.Glass:
                drawList.AddQuadFilled(a, b, c, d, ImGui.GetColorU32(GlassFill));
                drawList.AddLine(Vector2.Lerp(a, b, 0.15f), Vector2.Lerp(d, c, 0.05f), ImGui.GetColorU32(White with { W = 0.45f }),
                    edgeWidth * 1.4f);
                drawList.AddQuad(a, b, c, d, ImGui.GetColorU32(GlassEdge), edgeWidth);
                break;
            case FlingMaterial.Stone:
                drawList.AddQuadFilled(a, b, c, d, ImGui.GetColorU32(Stone));
                DrawSpecks(drawList, camera, center, axisX, axisY, half, seed);
                drawList.AddQuad(a, b, c, d, ImGui.GetColorU32(StoneEdge), edgeWidth * 1.2f);
                break;
            default:
                drawList.AddQuadFilled(a, b, c, d, ImGui.GetColorU32(Wood));
                DrawGrain(drawList, camera, center, axisX, axisY, half);
                drawList.AddQuad(a, b, c, d, ImGui.GetColorU32(WoodEdge), edgeWidth);
                break;
        }

        if (damage < 0.33f)
        {
            return;
        }

        DrawCrack(drawList, camera, center, axisX, axisY, half, seed, edgeWidth);
        if (damage >= 0.66f)
        {
            DrawCrack(drawList, camera, center, axisX, axisY, half, seed >> 7, edgeWidth);
        }
    }

    private static void DrawGrain(ImDrawListPtr drawList, in Camera2D camera, Vector2 center, Vector2 axisX,
        Vector2 axisY, Vector2 half)
    {
        var along = half.X >= half.Y ? axisX : axisY;
        var across = half.X >= half.Y ? axisY : axisX;
        var length = MathF.Max(half.X, half.Y) * 0.85f;
        var width = MathF.Min(half.X, half.Y);
        var grain = ImGui.GetColorU32(WoodGrain);
        var thickness = MathF.Max(1f, camera.Px(0.025f));
        for (var line = -1; line <= 1; line += 2)
        {
            var offset = across * (width * 0.4f * line);
            drawList.AddLine(camera.ToScreen(center + offset - along * length),
                camera.ToScreen(center + offset + along * length * 0.6f), grain, thickness);
        }
    }

    private static void DrawSpecks(ImDrawListPtr drawList, in Camera2D camera, Vector2 center, Vector2 axisX,
        Vector2 axisY, Vector2 half, int seed)
    {
        var speck = ImGui.GetColorU32(StoneSpeck);
        for (var index = 0; index < 4; index++)
        {
            var hash = Hash(seed + index * 31);
            var u = ((hash & 255) / 255f - 0.5f) * 1.5f;
            var v = (((hash >> 8) & 255) / 255f - 0.5f) * 1.5f;
            var point = center + axisX * (u * half.X) + axisY * (v * half.Y);
            drawList.AddCircleFilled(camera.ToScreen(point), camera.Px(0.05f + (hash >> 16 & 3) * 0.015f), speck, 8);
        }
    }

    private static void DrawCrack(ImDrawListPtr drawList, in Camera2D camera, Vector2 center, Vector2 axisX,
        Vector2 axisY, Vector2 half, int seed, float width)
    {
        var hash = Hash(seed);
        var start = center + axisX * (half.X * (((hash & 255) / 255f) - 0.5f)) - axisY * half.Y;
        var color = ImGui.GetColorU32(Crack);
        var previous = camera.ToScreen(start);
        for (var step = 1; step <= 4; step++)
        {
            var jag = ((Hash(seed + step * 13) & 255) / 255f - 0.5f) * 0.6f;
            var point = start + axisY * (half.Y * 2f * step / 4f) + axisX * (half.X * jag * 0.5f);
            var screen = camera.ToScreen(point);
            drawList.AddLine(previous, screen, color, width);
            previous = screen;
        }
    }

    public static void DrawGoblin(ImDrawListPtr drawList, in Camera2D camera, Vector2 center, float angle, float radius,
        bool knight, Vector2 lookAt, float bob, float damage)
    {
        var screen = camera.ToScreen(center);
        var size = camera.Px(radius);
        var up = new Vector2(MathF.Sin(angle), -MathF.Cos(angle));
        var right = new Vector2(-up.Y, up.X);
        var squash = 1f + 0.05f * bob;
        drawList.AddCircleFilled(screen + new Vector2(0f, size * 0.2f), size * 1.02f, ImGui.GetColorU32(Shadow), 20);
        var skin = damage > 0.4f ? GamePalette.Darken(GoblinSkin, 0.15f) : GoblinSkin;
        var earBase = size * 0.75f;
        for (var side = -1; side <= 1; side += 2)
        {
            var root = screen + right * (side * earBase) - up * (size * 0.1f);
            var tip = screen + right * (side * size * 1.45f) + up * (size * 0.45f);
            drawList.AddTriangleFilled(root + up * (size * 0.28f), tip, root - up * (size * 0.22f),
                ImGui.GetColorU32(GoblinDark));
        }

        drawList.AddCircleFilled(screen, size * squash, ImGui.GetColorU32(GoblinDark), 24);
        drawList.AddCircleFilled(screen - up * (size * 0.04f), size * 0.9f * squash, ImGui.GetColorU32(skin), 24);
        var gaze = lookAt - center;
        var gazeLength = gaze.Length();
        var look = gazeLength > 0.001f ? gaze / gazeLength : -Vector2.UnitX;
        for (var side = -1; side <= 1; side += 2)
        {
            var eye = screen + right * (side * size * 0.34f) + up * (size * 0.12f);
            drawList.AddCircleFilled(eye, size * 0.24f, ImGui.GetColorU32(White), 14);
            drawList.AddCircleFilled(eye + look * (size * 0.1f), size * 0.12f, ImGui.GetColorU32(Pupil), 10);
        }

        var mouth = screen - up * (size * 0.38f);
        drawList.AddLine(mouth - right * (size * 0.28f), mouth + right * (size * 0.28f),
            ImGui.GetColorU32(GoblinDark), MathF.Max(1f, size * 0.1f));
        drawList.AddCircleFilled(screen - up * (size * 0.08f), size * 0.12f, ImGui.GetColorU32(GoblinDark), 10);
        if (!knight)
        {
            return;
        }

        drawList.PathClear();
        drawList.PathArcTo(screen, size * 1.02f, angle + MathF.PI * 1.08f, angle + MathF.PI * 1.92f, 18);
        drawList.PathFillConvex(ImGui.GetColorU32(Helmet));
        var rimLeft = screen + right * (-size * 1.02f) + up * (size * 0.3f);
        var rimRight = screen + right * (size * 1.02f) + up * (size * 0.3f);
        drawList.AddLine(rimLeft, rimRight, ImGui.GetColorU32(HelmetDark), MathF.Max(1.5f, size * 0.16f));
        drawList.AddLine(screen + up * (size * 0.3f), screen + up * (size * 0.02f), ImGui.GetColorU32(HelmetDark),
            MathF.Max(1.5f, size * 0.12f));
        drawList.AddCircleFilled(screen + up * (size * 0.95f), size * 0.14f, ImGui.GetColorU32(HelmetDark), 10);
    }

    public static void DrawBird(ImDrawListPtr drawList, in Camera2D camera, Vector2 center, FlingBird bird,
        Vector2 facing, float stretch)
    {
        var screen = camera.ToScreen(center);
        var size = camera.Px(FlingBoard.BirdRadius(bird));
        var length = facing.Length();
        var forward = length > 0.001f ? facing / length : Vector2.UnitX;
        var side = new Vector2(-forward.Y, forward.X);
        if (side.Y > 0f)
        {
            side = -side;
        }

        var color = BirdColor(bird);
        var body = ImGui.GetColorU32(color);
        var tuft = ImGui.GetColorU32(GamePalette.Darken(color, 0.2f));
        for (var feather = -1; feather <= 1; feather++)
        {
            var root = screen + side * (size * 0.75f) - forward * (size * 0.15f * feather);
            var tip = root + side * (size * 0.55f) - forward * (size * (0.35f + 0.15f * feather));
            drawList.AddTriangleFilled(root - forward * (size * 0.18f), tip, root + forward * (size * 0.18f), tuft);
        }

        var tail = screen - forward * (size * 0.9f);
        drawList.AddTriangleFilled(tail + side * (size * 0.35f), tail - forward * (size * 0.6f) + side * (size * 0.1f),
            tail - side * (size * 0.25f), tuft);
        var radius = size * (1f + 0.12f * stretch);
        drawList.AddCircleFilled(screen, radius, ImGui.GetColorU32(GamePalette.Darken(color, 0.3f)), 24);
        drawList.AddCircleFilled(screen - side * (size * 0.04f), radius * 0.9f, body, 24);
        drawList.AddCircleFilled(screen - side * (size * 0.3f) - forward * (size * 0.1f), size * 0.5f,
            ImGui.GetColorU32(GamePalette.Lighten(color, 0.35f)), 18);
        var eye = screen + forward * (size * 0.38f) + side * (size * 0.22f);
        drawList.AddCircleFilled(eye, size * 0.26f, ImGui.GetColorU32(White), 14);
        drawList.AddCircleFilled(eye + forward * (size * 0.08f), size * 0.13f, ImGui.GetColorU32(Pupil), 10);
        if (bird == FlingBird.Heavy)
        {
            drawList.AddLine(eye + side * (size * 0.3f) - forward * (size * 0.25f), eye + side * (size * 0.18f) + forward * (size * 0.25f),
                ImGui.GetColorU32(Pupil), MathF.Max(1.5f, size * 0.12f));
        }

        var beakBase = screen + forward * (size * 0.82f);
        drawList.AddTriangleFilled(beakBase + side * (size * 0.18f), beakBase + forward * (size * 0.5f),
            beakBase - side * (size * 0.18f), ImGui.GetColorU32(Beak));
    }

    public static void DrawSlingBack(ImDrawListPtr drawList, in Camera2D camera, Vector2 pouch)
    {
        var width = MathF.Max(2f, camera.Px(PostHalfWidth * 2f));
        var wood = ImGui.GetColorU32(SlingWood);
        var dark = ImGui.GetColorU32(SlingDark);
        var anchor = FlingBoard.SlingAnchor;
        var groundPoint = camera.ToScreen(new Vector2(anchor.X, 0.05f));
        var fork = camera.ToScreen(new Vector2(anchor.X, 0f) + Trunk);
        var back = camera.ToScreen(new Vector2(anchor.X, 0f) + BackProng);
        drawList.AddLine(groundPoint, fork, dark, width * 1.15f);
        drawList.AddLine(groundPoint, fork, wood, width);
        drawList.AddLine(fork, back, wood, width * 0.85f);
        drawList.AddLine(back, camera.ToScreen(pouch), ImGui.GetColorU32(Band), MathF.Max(2f, camera.Px(0.09f)));
    }

    public static void DrawSlingFront(ImDrawListPtr drawList, in Camera2D camera, Vector2 pouch)
    {
        var width = MathF.Max(2f, camera.Px(PostHalfWidth * 2f));
        var fork = camera.ToScreen(new Vector2(FlingBoard.SlingAnchor.X, 0f) + Trunk);
        var front = camera.ToScreen(new Vector2(FlingBoard.SlingAnchor.X, 0f) + FrontProng);
        var band = ImGui.GetColorU32(Band);
        var pouchScreen = camera.ToScreen(pouch);
        drawList.AddLine(front, pouchScreen, band, MathF.Max(2f, camera.Px(0.1f)));
        drawList.AddCircleFilled(pouchScreen, camera.Px(0.12f), band, 12);
        drawList.AddLine(fork, front, ImGui.GetColorU32(SlingWood), width * 0.85f);
    }

    public static void DrawPreview(ImDrawListPtr drawList, in Camera2D camera, ReadOnlySpan<Vector2> path, float power)
    {
        var radius = camera.Px(PreviewDotRadius);
        for (var index = 0; index < path.Length; index++)
        {
            var fade = 1f - index / (float)path.Length;
            var size = radius * (0.55f + 0.45f * fade);
            drawList.AddCircleFilled(camera.ToScreen(path[index]), size,
                ImGui.GetColorU32(White with { W = (0.35f + 0.5f * power) * fade }), 10);
        }
    }

    public static void DrawTrail(ImDrawListPtr drawList, in Camera2D camera, FlingBoard board)
    {
        var radius = camera.Px(TrailDotRadius);
        var color = ImGui.GetColorU32(White with { W = 0.32f });
        for (var index = 0; index < board.TrailCount; index++)
        {
            drawList.AddCircleFilled(camera.ToScreen(board.TrailPoint(index)), index % 3 == 0 ? radius * 1.5f : radius,
                color, 8);
        }
    }

    private static int Hash(int value)
    {
        unchecked
        {
            var hash = (uint)value * 2654435761u;
            hash ^= hash >> 15;
            hash *= 2246822519u;
            hash ^= hash >> 13;
            return (int)(hash & 0x7FFFFFFF);
        }
    }
}
