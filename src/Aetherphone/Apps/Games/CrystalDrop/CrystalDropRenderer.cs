using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core.Animation;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.CrystalDrop;

internal static class CrystalDropRenderer
{
    public const float TopBand = 0.36f;
    public const float WorldHeight = CrystalDropBoard.JarHeight + TopBand;
    public const float HeldLift = 0.06f;
    private const float JarRounding = 0.10f;
    private const int CornerSegments = 8;
    private const float GuideDash = 8f;
    private const float DangerDash = 9f;
    private static readonly Vector4 JarFill = new(0.05f, 0.06f, 0.09f, 0.55f);
    private static readonly Vector4 JarRim = new(1f, 1f, 1f, 0.14f);
    private static readonly Vector4 DangerCool = new(0.9f, 0.92f, 1f, 1f);
    private static readonly Vector4 DangerHot = new(0.98f, 0.34f, 0.34f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4[] TierColors =
    {
        new(0.52f, 0.86f, 0.72f, 1f), new(0.44f, 0.76f, 0.98f, 1f), new(0.42f, 0.58f, 0.96f, 1f),
        new(0.66f, 0.50f, 0.98f, 1f), new(0.90f, 0.46f, 0.86f, 1f), new(0.96f, 0.42f, 0.56f, 1f),
        new(0.98f, 0.52f, 0.36f, 1f), new(0.98f, 0.72f, 0.32f, 1f), new(0.96f, 0.88f, 0.40f, 1f),
        new(0.72f, 0.94f, 0.52f, 1f), new(0.92f, 0.96f, 0.99f, 1f),
    };

    public static Vector2 WorldCenter => new(0.5f, (CrystalDropBoard.JarHeight - TopBand) * 0.5f);

    public static Vector4 TierColor(int tier) => TierColors[tier];

    public static Vector2 Project(in Camera2D camera, Vector2 world, float skewPixels)
    {
        var lean = skewPixels * Easing.Clamp01(1f - world.Y / CrystalDropBoard.JarHeight);
        return camera.ToScreen(world) + new Vector2(lean, 0f);
    }

    public static void DrawJar(ImDrawListPtr drawList, in Camera2D camera, float skewPixels, Vector4 accent,
        float scale)
    {
        var width = camera.Px(1f);
        var rounding = width * JarRounding;
        var topLeft = Project(in camera, Vector2.Zero, skewPixels);
        var topRight = Project(in camera, new Vector2(1f, 0f), skewPixels);
        var bottomLeft = camera.ToScreen(new Vector2(0f, CrystalDropBoard.JarHeight));
        var bottomRight = camera.ToScreen(new Vector2(1f, CrystalDropBoard.JarHeight));
        ProgressRing.Glow(new Vector2((bottomLeft.X + bottomRight.X) * 0.5f, bottomLeft.Y - rounding), width * 0.42f,
            accent, 0.22f);
        TraceJar(drawList, topLeft, topRight, bottomRight, bottomLeft, rounding);
        drawList.PathFillConvex(ImGui.GetColorU32(JarFill));
        TraceJar(drawList, topLeft, topRight, bottomRight, bottomLeft, rounding);
        drawList.PathStroke(ImGui.GetColorU32(JarRim), ImDrawFlags.Closed, 1.5f * scale);
        var railColor = ImGui.GetColorU32(GamePalette.Lighten(accent, 0.4f) with { W = 0.35f });
        var railInset = rounding * 0.5f;
        drawList.AddLine(topLeft + new Vector2(railInset, 0f), topLeft + new Vector2(0f, railInset), railColor,
            2f * scale);
        drawList.AddLine(topRight - new Vector2(railInset, 0f), topRight + new Vector2(0f, railInset), railColor,
            2f * scale);
    }

    private static void TraceJar(ImDrawListPtr drawList, Vector2 topLeft, Vector2 topRight, Vector2 bottomRight,
        Vector2 bottomLeft, float rounding)
    {
        drawList.PathLineTo(topLeft);
        drawList.PathLineTo(topRight);
        TraceCorner(drawList, bottomRight + new Vector2(-rounding, -rounding), rounding, 0f);
        TraceCorner(drawList, bottomLeft + new Vector2(rounding, -rounding), rounding, MathF.PI * 0.5f);
    }

    private static void TraceCorner(ImDrawListPtr drawList, Vector2 center, float radius, float startAngle)
    {
        for (var segment = 0; segment <= CornerSegments; segment++)
        {
            var angle = startAngle + MathF.PI * 0.5f * segment / CornerSegments;
            drawList.PathLineTo(center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius);
        }
    }

    public static void DrawDangerLine(ImDrawListPtr drawList, in Camera2D camera, CrystalDropBoard board,
        float skewPixels, float scale)
    {
        var heat = Math.Clamp(board.OverflowFraction, 0f, 1f);
        var alpha = 0.16f + 0.5f * heat * (0.6f + 0.4f * Pulse.Wave(Pulse.Fast));
        var color = ImGui.GetColorU32(Vector4.Lerp(DangerCool, DangerHot, heat) with { W = alpha });
        var left = Project(in camera, new Vector2(0f, CrystalDropBoard.DangerLine), skewPixels);
        var right = Project(in camera, new Vector2(1f, CrystalDropBoard.DangerLine), skewPixels);
        var dash = DangerDash * scale;
        var thickness = MathF.Max(1f, (1f + heat) * scale);
        for (var x = left.X + dash; x < right.X - dash; x += dash * 2f)
        {
            drawList.AddLine(new Vector2(x, left.Y), new Vector2(MathF.Min(x + dash, right.X - dash), left.Y), color,
                thickness);
        }
    }

    public static void DrawHeld(ImDrawListPtr drawList, in Camera2D camera, CrystalDropBoard board, float pointerX,
        float skewPixels, float scale)
    {
        var radiusWorld = CrystalDropBoard.RadiusOf(board.HeldTier);
        var dropX = board.ClampDropX(pointerX);
        var ready = board.CanDrop;
        var guideColor = ImGui.GetColorU32(White with { W = ready ? 0.18f : 0.07f });
        var dash = GuideDash * scale;
        var top = Project(in camera, new Vector2(dropX, 0f), skewPixels).Y + 2f * scale;
        var bottom = camera.ToScreen(new Vector2(dropX, CrystalDropBoard.JarHeight)).Y;
        for (var cursor = top; cursor < bottom; cursor += dash * 2f)
        {
            var start = Project(in camera, camera.ToWorld(new Vector2(0f, cursor)) with { X = dropX }, skewPixels);
            var end = Project(in camera, camera.ToWorld(new Vector2(0f, MathF.Min(cursor + dash, bottom))) with { X = dropX },
                skewPixels);
            drawList.AddLine(start, end, guideColor, MathF.Max(1f, scale));
        }

        var bob = ready ? MathF.Sin((float)ImGui.GetTime() * 3.4f) * 2.4f * scale : 0f;
        var appear = ready ? 1f : 0.55f + 0.45f * board.DropProgress;
        var center = Project(in camera, new Vector2(dropX, -radiusWorld - HeldLift), skewPixels);
        DrawCrystal(drawList, center + new Vector2(0f, bob), camera.Px(radiusWorld) * appear, board.HeldTier, scale);
    }

    public static void DrawCrystals(ImDrawListPtr drawList, in Camera2D camera, CrystalDropBoard board,
        float skewPixels, float scale)
    {
        var slack = MathF.Abs(skewPixels) + camera.Px(0.02f);
        var clipMin = camera.ToScreen(new Vector2(0f, -TopBand)) - new Vector2(slack, 0f);
        var clipMax = camera.ToScreen(new Vector2(1f, CrystalDropBoard.JarHeight)) + new Vector2(slack, 0f);
        drawList.PushClipRect(clipMin, clipMax, true);
        for (var index = 0; index < board.Count; index++)
        {
            var crystal = board.At(index);
            var radius = camera.Px(CrystalDropBoard.RadiusOf(crystal.Tier));
            var center = Project(in camera, crystal.Position, skewPixels);
            DrawCrystal(drawList, center, radius * (1f + 0.22f * Easing.EaseOutCubic(crystal.Pop)), crystal.Tier,
                scale);
        }

        drawList.PopClipRect();
    }

    public static void DrawCrystal(ImDrawListPtr drawList, Vector2 center, float radius, int tier, float scale)
    {
        var color = TierColors[tier];
        if (tier >= 6)
        {
            ProgressRing.Glow(center, radius * 0.9f, color, 0.35f + 0.05f * tier);
        }

        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(GamePalette.Darken(color, 0.28f)), 0);
        drawList.AddCircleFilled(center - new Vector2(0f, radius * 0.10f), radius * 0.88f, ImGui.GetColorU32(color), 0);
        var facet = ImGui.GetColorU32(GamePalette.Lighten(color, 0.45f) with { W = 0.55f });
        var facetThickness = MathF.Max(1f, radius * 0.07f);
        drawList.AddLine(center + new Vector2(-radius * 0.55f, -radius * 0.18f),
            center + new Vector2(0f, -radius * 0.72f), facet, facetThickness);
        drawList.AddLine(center + new Vector2(0f, -radius * 0.72f), center + new Vector2(radius * 0.55f, -radius * 0.18f),
            facet, facetThickness);
        drawList.AddCircleFilled(center + new Vector2(-radius * 0.34f, -radius * 0.38f), radius * 0.16f,
            ImGui.GetColorU32(White with { W = 0.75f }), 0);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(GamePalette.Lighten(color, 0.5f) with { W = 0.5f }), 0,
            MathF.Max(1f, scale));
    }
}
