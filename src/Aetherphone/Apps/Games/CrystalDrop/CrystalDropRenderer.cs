using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.CrystalDrop;

internal sealed class CrystalDropRenderer
{
    private const float JarHeightFactor = 0.80f;
    private const float JarWidthFactor = 0.92f;
    private const float BottomMargin = 6f;
    private static readonly Vector4[] TierColors =
    {
        new(0.52f, 0.86f, 0.72f, 1f), new(0.44f, 0.76f, 0.98f, 1f), new(0.42f, 0.58f, 0.96f, 1f),
        new(0.66f, 0.50f, 0.98f, 1f), new(0.90f, 0.46f, 0.86f, 1f), new(0.96f, 0.42f, 0.56f, 1f),
        new(0.98f, 0.52f, 0.36f, 1f), new(0.98f, 0.72f, 0.32f, 1f), new(0.96f, 0.88f, 0.40f, 1f),
        new(0.72f, 0.94f, 0.52f, 1f), new(0.92f, 0.96f, 0.99f, 1f),
    };

    public static Vector4 TierColor(int tier) => TierColors[tier];

    public static Rect JarOf(Rect area, float scale)
    {
        var width = MathF.Min(area.Width * JarWidthFactor, area.Height * JarHeightFactor / CrystalDropBoard.JarHeight);
        var height = width * CrystalDropBoard.JarHeight;
        var bottom = area.Max.Y - BottomMargin * scale;
        var left = area.Center.X - width * 0.5f;
        return new Rect(new Vector2(left, bottom - height), new Vector2(left + width, bottom));
    }

    public static Vector2 ToScreen(Rect jar, Vector2 position)
    {
        return new Vector2(jar.Min.X + position.X * jar.Width, jar.Min.Y + position.Y * jar.Width);
    }

    public void Draw(CrystalDropBoard board, Rect jar, float pointerX, Vector4 accent, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        DrawJar(drawList, jar, accent, scale);
        DrawDangerLine(drawList, board, jar, scale);
        if (!board.GameOver)
        {
            DrawHeld(drawList, board, jar, pointerX, scale);
        }

        drawList.PushClipRect(new Vector2(jar.Min.X, jar.Min.Y - jar.Height), jar.Max, true);
        for (var index = 0; index < board.Count; index++)
        {
            var crystal = board.At(index);
            var radius = CrystalDropBoard.RadiusOf(crystal.Tier) * jar.Width;
            var center = ToScreen(jar, crystal.Position);
            DrawCrystal(drawList, center, radius * (1f + 0.22f * Easing.EaseOutCubic(crystal.Pop)), crystal.Tier, scale);
        }

        drawList.PopClipRect();
    }

    private static void DrawJar(ImDrawListPtr drawList, Rect jar, Vector4 accent, float scale)
    {
        var rounding = jar.Width * 0.10f;
        var min = jar.Min;
        var max = jar.Max;
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(new Vector4(0.05f, 0.06f, 0.09f, 0.55f)));
        Squircle.FillVerticalGradient(drawList, new Vector2(min.X, max.Y - jar.Height * 0.45f), max, rounding,
            ImGui.GetColorU32(accent with { W = 0f }), ImGui.GetColorU32(accent with { W = 0.10f }));
        Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.14f)),
            1.5f * scale);
        var railColor = ImGui.GetColorU32(GamePalette.Lighten(accent, 0.4f) with { W = 0.35f });
        var railInset = rounding * 0.5f;
        drawList.AddLine(new Vector2(min.X + railInset, min.Y), new Vector2(min.X, min.Y + railInset), railColor,
            2f * scale);
        drawList.AddLine(new Vector2(max.X - railInset, min.Y), new Vector2(max.X, min.Y + railInset), railColor,
            2f * scale);
    }

    private static void DrawDangerLine(ImDrawListPtr drawList, CrystalDropBoard board, Rect jar, float scale)
    {
        var y = jar.Min.Y + CrystalDropBoard.DangerLine * jar.Width;
        var heat = Math.Clamp(board.OverflowFraction, 0f, 1f);
        var alpha = 0.16f + 0.5f * heat * (0.6f + 0.4f * Pulse.Wave(Pulse.Fast));
        var color = ImGui.GetColorU32(Vector4.Lerp(new Vector4(0.9f, 0.92f, 1f, 1f), new Vector4(0.98f, 0.34f, 0.34f, 1f),
            heat) with { W = alpha });
        var dash = 9f * scale;
        var thickness = MathF.Max(1f, (1f + heat) * scale);
        for (var x = jar.Min.X + dash; x < jar.Max.X - dash; x += dash * 2f)
        {
            drawList.AddLine(new Vector2(x, y), new Vector2(MathF.Min(x + dash, jar.Max.X - dash), y), color, thickness);
        }
    }

    private static void DrawHeld(ImDrawListPtr drawList, CrystalDropBoard board, Rect jar, float pointerX, float scale)
    {
        var radius = CrystalDropBoard.RadiusOf(board.HeldTier) * jar.Width;
        var x = jar.Min.X + board.ClampDropX(pointerX) * jar.Width;
        var y = jar.Min.Y - radius - 8f * scale;
        var ready = board.CanDrop;
        var guideColor = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, ready ? 0.18f : 0.07f));
        var dash = 8f * scale;
        for (var cursor = jar.Min.Y + 2f * scale; cursor < jar.Max.Y; cursor += dash * 2f)
        {
            drawList.AddLine(new Vector2(x, cursor), new Vector2(x, MathF.Min(cursor + dash, jar.Max.Y)), guideColor,
                MathF.Max(1f, scale));
        }

        var bob = ready ? MathF.Sin((float)ImGui.GetTime() * 3.4f) * 2.4f * scale : 0f;
        var appear = ready ? 1f : 0.55f + 0.45f * board.DropProgress;
        DrawCrystal(drawList, new Vector2(x, y + bob), radius * appear, board.HeldTier, scale);
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
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.75f)), 0);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(GamePalette.Lighten(color, 0.5f) with { W = 0.5f }), 0,
            MathF.Max(1f, scale));
    }
}
