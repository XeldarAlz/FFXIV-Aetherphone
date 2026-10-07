using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Drift;

internal readonly struct DriftPadInput
{
    public readonly bool Left;
    public readonly bool Right;
    public readonly bool Thrust;
    public readonly bool Fire;
    public readonly bool Warp;

    public DriftPadInput(bool left, bool right, bool thrust, bool fire, bool warp)
    {
        Left = left;
        Right = right;
        Thrust = thrust;
        Fire = fire;
        Warp = warp;
    }
}

internal enum PadGlyph : byte
{
    RotateLeft,
    RotateRight,
    Thrust,
    Fire,
    Warp,
}

internal static class DriftPad
{
    private const float KeySize = 50f;
    private const float Gap = 8f;
    private const float WarpFraction = 0.78f;
    private const float KeysAcross = 4f + WarpFraction;
    private const float GapsAcross = 8f;
    private const float GlyphStroke = 2f;
    private const float ArcSweep = 4.2f;

    public static DriftPadInput Draw(Rect area, Vector4 accent)
    {
        var scale = UiScale.Current;
        var gap = Gap * scale;
        var key = MathF.Min(KeySize * scale,
            MathF.Min(area.Height - gap * 2f, (area.Width - gap * GapsAcross) / KeysAcross));
        var top = area.Center.Y - key * 0.5f;
        var size = new Vector2(key, key);
        var leftMin = new Vector2(area.Min.X + gap * 1.5f, top);
        var rightMin = new Vector2(leftMin.X + key + gap, top);
        var fireMin = new Vector2(area.Max.X - gap * 1.5f - key, top);
        var thrustMin = new Vector2(fireMin.X - gap - key, top);
        var warpSize = key * WarpFraction;
        var warpMin = new Vector2(area.Center.X - warpSize * 0.5f, area.Center.Y - warpSize * 0.5f);
        Key(new Rect(leftMin, leftMin + size), PadGlyph.RotateLeft, accent, scale, out var left);
        Key(new Rect(rightMin, rightMin + size), PadGlyph.RotateRight, accent, scale, out var right);
        var warp = Key(new Rect(warpMin, warpMin + new Vector2(warpSize, warpSize)), PadGlyph.Warp, accent, scale,
            out _);
        Key(new Rect(thrustMin, thrustMin + size), PadGlyph.Thrust, accent, scale, out var thrust);
        Key(new Rect(fireMin, fireMin + size), PadGlyph.Fire, accent, scale, out var fire);
        return new DriftPadInput(left, right, thrust, fire, warp);
    }

    private static bool Key(Rect key, PadGlyph glyph, Vector4 accent, float scale, out bool held)
    {
        var pressed = GamePad.HoldButton(key, accent, out held);
        DrawGlyph(ImGui.GetWindowDrawList(), glyph, key.Center, key.Height * 0.22f,
            ImGui.GetColorU32(GamePad.GlyphInk(held, accent)), GlyphStroke * scale);
        return pressed;
    }

    private static void DrawGlyph(ImDrawListPtr drawList, PadGlyph glyph, Vector2 center, float extent, uint ink,
        float thickness)
    {
        switch (glyph)
        {
            case PadGlyph.RotateLeft:
                DrawTurn(drawList, center, extent, ink, thickness, -1f);
                return;
            case PadGlyph.RotateRight:
                DrawTurn(drawList, center, extent, ink, thickness, 1f);
                return;
            case PadGlyph.Thrust:
                DrawChevron(drawList, center + new Vector2(0f, -extent * 0.35f), extent, ink, thickness);
                DrawChevron(drawList, center + new Vector2(0f, extent * 0.45f), extent, ink, thickness);
                return;
            case PadGlyph.Fire:
                drawList.AddCircleFilled(center, extent * 0.34f, ink, 16);
                for (var ray = 0; ray < 4; ray++)
                {
                    var angle = ray * MathF.PI * 0.5f + MathF.PI * 0.25f;
                    var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                    drawList.AddLine(center + direction * extent * 0.62f, center + direction * extent * 1.05f, ink,
                        thickness);
                }

                return;
            default:
                DrawDiamond(drawList, center, extent, ink, thickness);
                DrawDiamond(drawList, center, extent * 0.42f, ink, thickness);
                return;
        }
    }

    private static void DrawTurn(ImDrawListPtr drawList, Vector2 center, float extent, uint ink, float thickness,
        float side)
    {
        var start = -MathF.PI * 0.5f - side * ArcSweep * 0.5f;
        var end = -MathF.PI * 0.5f + side * ArcSweep * 0.5f;
        drawList.PathClear();
        drawList.PathArcTo(center, extent, MathF.Min(start, end), MathF.Max(start, end), 20);
        drawList.PathStroke(ink, ImDrawFlags.None, thickness);
        var tipAngle = side < 0f ? start : end;
        var tip = center + new Vector2(MathF.Cos(tipAngle), MathF.Sin(tipAngle)) * extent;
        var tangent = new Vector2(-MathF.Sin(tipAngle), MathF.Cos(tipAngle)) * side;
        var normal = new Vector2(-tangent.Y, tangent.X);
        var head = extent * 0.45f;
        drawList.AddTriangleFilled(tip + tangent * head, tip + normal * head * 0.7f, tip - normal * head * 0.7f, ink);
    }

    private static void DrawChevron(ImDrawListPtr drawList, Vector2 center, float extent, uint ink, float thickness)
    {
        var half = extent * 0.7f;
        var rise = extent * 0.45f;
        drawList.AddLine(center + new Vector2(-half, rise * 0.5f), center + new Vector2(0f, -rise * 0.5f), ink,
            thickness);
        drawList.AddLine(center + new Vector2(0f, -rise * 0.5f), center + new Vector2(half, rise * 0.5f), ink,
            thickness);
    }

    private static void DrawDiamond(ImDrawListPtr drawList, Vector2 center, float extent, uint ink, float thickness)
    {
        drawList.AddQuad(center + new Vector2(0f, -extent), center + new Vector2(extent, 0f),
            center + new Vector2(0f, extent), center + new Vector2(-extent, 0f), ink, thickness);
    }
}
