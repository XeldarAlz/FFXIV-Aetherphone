using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Lander;

internal static class LanderRenderer
{
    public static readonly Vector4 Flame = new(1f, 0.55f, 0.16f, 1f);
    public static readonly Vector4 FlameCore = new(1f, 0.95f, 0.78f, 1f);
    public static readonly Vector4 Gold = new(0.98f, 0.76f, 0.30f, 1f);
    public static readonly Vector4 Danger = new(0.95f, 0.30f, 0.30f, 1f);
    public static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    public static readonly Vector4 Hull = new(0.84f, 0.86f, 0.90f, 1f);
    public const float BeaconRadius = 0.16f;
    private const float RimAlpha = 0.22f;
    private const float LightReach = 9f;
    private const float PadThickness = 0.22f;
    private const float PadGlowHeight = 1.4f;
    private const float LabelLift = 1.5f;
    private static readonly Vector4 HullShade = new(0.52f, 0.55f, 0.62f, 1f);
    private static readonly Vector4 Foil = new(0.86f, 0.64f, 0.22f, 1f);
    private static readonly Vector4 FoilShade = new(0.58f, 0.40f, 0.12f, 1f);
    private static readonly Vector4 Metal = new(0.30f, 0.31f, 0.36f, 1f);
    private static readonly Vector4 Leg = new(0.70f, 0.72f, 0.78f, 1f);
    private static readonly Vector2[] Cabin =
    {
        new(-0.2f, -0.74f), new(0.2f, -0.74f), new(0.42f, -0.52f), new(0.42f, -0.16f), new(0.24f, 0.02f),
        new(-0.24f, 0.02f), new(-0.42f, -0.16f), new(-0.42f, -0.52f),
    };

    private static readonly Vector2[] CabinShade =
    {
        new(0.42f, -0.52f), new(0.42f, -0.16f), new(0.24f, 0.02f), new(0.02f, 0.02f), new(0.18f, -0.5f),
    };

    private static readonly Vector2[] Stage =
    {
        new(-0.56f, 0.0f), new(0.56f, 0.0f), new(0.48f, 0.44f), new(-0.48f, 0.44f),
    };

    private static readonly Vector2[] Nozzle =
    {
        new(-0.13f, 0.42f), new(0.13f, 0.42f), new(0.2f, 0.62f), new(-0.2f, 0.62f),
    };

    public static void DrawLander(ImDrawListPtr drawList, LanderBoard board, in Camera2D camera, Vector4 accent,
        float alpha)
    {
        if (alpha <= 0f)
        {
            return;
        }

        var legColor = ImGui.GetColorU32(Leg with { W = alpha });
        var legWidth = MathF.Max(1.2f, camera.Px(0.08f));
        for (var side = -1; side <= 1; side += 2)
        {
            var hip = Point(board, in camera, 0.42f * side, 0.3f);
            var foot = Point(board, in camera, LanderBoard.FootSpread * side, LanderBoard.FootDrop - 0.04f);
            drawList.AddLine(hip, foot, legColor, legWidth);
            drawList.AddLine(Point(board, in camera, 0.22f * side, 0.42f),
                Point(board, in camera, 0.62f * side, 0.76f), legColor, legWidth * 0.8f);
            drawList.AddLine(Point(board, in camera, (LanderBoard.FootSpread - 0.14f) * side, LanderBoard.FootDrop),
                Point(board, in camera, (LanderBoard.FootSpread + 0.14f) * side, LanderBoard.FootDrop), legColor,
                legWidth * 1.6f);
        }

        Fill(drawList, board, in camera, Stage, Foil with { W = alpha });
        drawList.AddLine(Point(board, in camera, -0.5f, 0.22f), Point(board, in camera, 0.5f, 0.22f),
            ImGui.GetColorU32(FoilShade with { W = alpha }), MathF.Max(1f, camera.Px(0.06f)));
        drawList.AddLine(Point(board, in camera, 0.08f, 0.02f), Point(board, in camera, 0.04f, 0.42f),
            ImGui.GetColorU32(FoilShade with { W = 0.7f * alpha }), MathF.Max(1f, camera.Px(0.05f)));
        Fill(drawList, board, in camera, Nozzle, Metal with { W = alpha });
        Fill(drawList, board, in camera, Cabin, Hull with { W = alpha });
        Fill(drawList, board, in camera, CabinShade, HullShade with { W = 0.55f * alpha });
        var window = Point(board, in camera, -0.08f, -0.42f);
        var windowRadius = MathF.Max(2f, camera.Px(0.15f));
        ProgressRing.Glow(window, windowRadius * 2f, accent, 0.6f * alpha);
        drawList.AddCircleFilled(window, windowRadius, ImGui.GetColorU32(GamePalette.Darken(accent, 0.3f) with { W = alpha }),
            16);
        drawList.AddCircleFilled(window - new Vector2(windowRadius * 0.35f, windowRadius * 0.35f), windowRadius * 0.35f,
            ImGui.GetColorU32(White with { W = 0.8f * alpha }), 10);
        var mast = Point(board, in camera, -0.3f, -0.98f);
        drawList.AddLine(Point(board, in camera, -0.16f, -0.72f), mast, legColor, MathF.Max(1f, camera.Px(0.05f)));
        drawList.AddCircleFilled(mast, MathF.Max(1.5f, camera.Px(0.07f)), ImGui.GetColorU32(accent with { W = alpha }), 8);
    }

    public static void DrawFlame(ImDrawListPtr drawList, LanderBoard board, in Camera2D camera, float power,
        float flicker)
    {
        if (power <= 0.02f)
        {
            return;
        }

        var root = board.ToWorld(new Vector2(0f, LanderBoard.NozzleDrop));
        var down = -board.Up;
        var side = new Vector2(-down.Y, down.X);
        var length = (0.85f + 0.55f * flicker) * power;
        DrawCone(drawList, in camera, root, down, side, 0.32f, length * 1.35f, Flame with { W = 0.28f });
        DrawCone(drawList, in camera, root, down, side, 0.2f, length, Flame with { W = 0.9f });
        DrawCone(drawList, in camera, root, down, side, 0.1f, length * 0.55f, FlameCore);
        ProgressRing.Glow(camera.ToScreen(root + down * 0.25f), camera.Px(0.9f + 0.4f * flicker) * power, Flame, 0.9f);
    }

    private static void DrawCone(ImDrawListPtr drawList, in Camera2D camera, Vector2 root, Vector2 down, Vector2 side,
        float halfWidth, float length, Vector4 color)
    {
        drawList.AddTriangleFilled(camera.ToScreen(root + side * halfWidth), camera.ToScreen(root - side * halfWidth),
            camera.ToScreen(root + down * length), ImGui.GetColorU32(color));
    }

    public static void DrawRim(ImDrawListPtr drawList, LanderTerrain terrain, in Camera2D camera, Vector4 accent,
        Vector2 light, float lightStrength, float skipFrom, float skipTo, float scale)
    {
        var points = terrain.Points;
        var visible = camera.VisibleWorld;
        var width = MathF.Max(1f, 1.4f * scale);
        for (var index = 1; index < points.Length; index++)
        {
            var start = points[index - 1];
            var end = points[index];
            if (end.X < visible.Min.X || start.X > visible.Max.X)
            {
                continue;
            }

            if (end.X > skipFrom && start.X < skipTo)
            {
                continue;
            }

            var middle = (start + end) * 0.5f;
            var lit = lightStrength <= 0f
                ? 0f
                : lightStrength * MathF.Max(0f, 1f - Vector2.Distance(middle, light) / LightReach);
            var color = Vector4.Lerp(GamePalette.Lighten(accent, 0.55f) with { W = RimAlpha },
                Flame with { W = 0.95f }, Math.Clamp(lit, 0f, 1f));
            drawList.AddLine(camera.ToScreen(start), camera.ToScreen(end), ImGui.GetColorU32(color),
                width * (1f + lit * 1.6f));
        }
    }

    public static void DrawPads(ImDrawListPtr drawList, LanderTerrain terrain, in Camera2D camera, Vector4 accent,
        float time, int celebrating, ReadOnlySpan<string> labels, float scale)
    {
        var pads = terrain.Pads;
        for (var index = 0; index < pads.Length; index++)
        {
            ref readonly var pad = ref pads[index];
            var color = MultiplierColor(pad.Multiplier, accent);
            var party = index == celebrating;
            var left = camera.ToScreen(new Vector2(pad.Left, pad.Y));
            var right = camera.ToScreen(new Vector2(pad.Right, pad.Y + PadThickness));
            var glowTop = camera.ToScreen(new Vector2(pad.Left, pad.Y - PadGlowHeight)).Y;
            var clear = ImGui.GetColorU32(color with { W = 0f });
            var glow = ImGui.GetColorU32(color with { W = party ? 0.42f : 0.2f });
            drawList.AddRectFilledMultiColor(new Vector2(left.X, glowTop), new Vector2(right.X, left.Y), clear, clear,
                glow, glow);
            drawList.AddRectFilled(left, right, ImGui.GetColorU32(color), MathF.Max(1f, camera.Px(0.06f)));
            drawList.AddLine(left, new Vector2(right.X, left.Y), ImGui.GetColorU32(White with { W = 0.8f }),
                MathF.Max(1f, scale));
            var blink = party ? MathF.Sin(time * 14f + index) > 0f : MathF.Sin(time * 3.2f + index * 1.7f) > -0.2f;
            var beacon = blink ? color : GamePalette.Darken(color, 0.55f);
            var beaconRadius = MathF.Max(1.5f, camera.Px(BeaconRadius));
            for (var end = 0; end < 2; end++)
            {
                var beaconX = end == 0 ? pad.Left + 0.12f : pad.Right - 0.12f;
                var center = camera.ToScreen(new Vector2(beaconX, pad.Y - 0.18f));
                if (blink)
                {
                    ProgressRing.Glow(center, beaconRadius * 3f, color, 0.8f);
                }

                drawList.AddCircleFilled(center, beaconRadius, ImGui.GetColorU32(beacon), 10);
            }

            var labelCenter = camera.ToScreen(new Vector2(pad.Center, pad.Y - LabelLift));
            Typography.DrawCentered(drawList, labelCenter, labels[index], color with { W = 0.95f },
                TextStyles.FootnoteEmphasized.Scale, FontWeight.Bold);
        }
    }

    public static Vector4 MultiplierColor(int multiplier, Vector4 accent) => multiplier switch
    {
        5 => Gold,
        4 => new Vector4(1f, 0.58f, 0.36f, 1f),
        3 => GamePalette.Lighten(accent, 0.25f),
        _ => new Vector4(0.80f, 0.88f, 0.96f, 1f),
    };

    public static void DrawLight(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 color, float strength)
    {
        if (strength <= 0f || radius <= 0f)
        {
            return;
        }

        for (var layer = 4; layer >= 1; layer--)
        {
            var alpha = 0.16f * strength * (5 - layer) / 4f;
            drawList.AddCircleFilled(center, radius * (0.3f + layer * 0.2f), ImGui.GetColorU32(color with { W = alpha }),
                32);
        }
    }

    private static Vector2 Point(LanderBoard board, in Camera2D camera, float localX, float localY) =>
        camera.ToScreen(board.ToWorld(new Vector2(localX, localY)));

    private static void Fill(ImDrawListPtr drawList, LanderBoard board, in Camera2D camera, ReadOnlySpan<Vector2> shape,
        Vector4 color)
    {
        for (var index = 0; index < shape.Length; index++)
        {
            drawList.PathLineTo(camera.ToScreen(board.ToWorld(shape[index])));
        }

        drawList.PathFillConvex(ImGui.GetColorU32(color));
    }
}
