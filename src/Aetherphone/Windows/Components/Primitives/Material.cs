using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class Material
{
    private const float BorderAlpha = 0.09f;
    private const float HighlightAlpha = 0.11f;
    private const float SheenFalloff = 0.18f;
    private const float LensZoom = 1.05f;
    private const float EdgeBandFraction = 0.08f;
    private const float EdgeRefraction = 0.7f;
    private const float PointerReachUnits = 56f;
    private const float FallbackBody = 0.14f;
    private const uint AlphaChannel = 0xFF000000;
    private static readonly Vector4 FrostedFill = new(0.12f, 0.12f, 0.15f, 0.86f);
    private static readonly Vector4 LightGlassCalm = new(0.92f, 0.94f, 1f, 0.20f);
    private static readonly Vector4 LightGlassHarsh = new(0.56f, 0.58f, 0.64f, 0.38f);
    private static readonly Vector4 DarkGlass = new(0.05f, 0.06f, 0.09f, 0.44f);
    private const float DarkGlassHarshBoost = 0.18f;

    public static void TopGlow(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, Vector4 accent,
        float coverage, float strength)
    {
        if (strength <= 0f)
        {
            return;
        }

        var scale = UiScale.Current;
        var tint = ImGui.GetColorU32(accent with { W = strength });
        var clear = ImGui.GetColorU32(accent with { W = 0f });
        var capBottom = min.Y + rounding + scale;
        drawList.AddRectFilled(min, new Vector2(max.X, capBottom), tint, rounding, ImDrawFlags.RoundCornersTop);
        var fadeBottom = min.Y + (max.Y - min.Y) * coverage;
        if (fadeBottom > capBottom)
        {
            drawList.AddRectFilledMultiColor(new Vector2(min.X, capBottom), new Vector2(max.X, fadeBottom), tint, tint,
                clear, clear);
        }
    }

    public static void Veil(ImDrawListPtr drawList, Vector2 min, Vector2 max, float dim, float rounding = 0f)
    {
        if (dim <= 0f)
        {
            return;
        }

        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, dim)));
    }

    public static void Glass(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, Vector4 ink,
        float scale)
    {
        var lightScene = ink.X < 0.5f;
        var fill = lightScene ? new Vector4(0.10f, 0.12f, 0.16f, 0.10f) : new Vector4(1f, 1f, 1f, 0.10f);
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(fill));
        Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(ink with { W = ink.W * 0.14f }), 1f * scale);
        Sheen(drawList, min, max, rounding,
            ImGui.GetColorU32(ink with { W = ink.W * (lightScene ? 0.05f : 0.18f) }), 1f * scale, 1f * scale);
    }

    public static void Frosted(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, float scale,
        float opacity = 1f)
    {
        if (opacity <= 0f)
        {
            return;
        }

        Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(FrostedFill with { W = FrostedFill.W * opacity }));
        EdgeSquircle(drawList, min, max, radius, scale, opacity);
    }

    public static void Dock(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, float scale,
        float brightness, float opacity = 1f) =>
        LiquidGlass(drawList, min, max, radius, scale, GlassTone.Light, brightness, opacity);

    public static void FrostedGlass(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, float scale,
        float opacity = 1f) =>
        LiquidGlass(drawList, min, max, radius, scale, GlassTone.Dark, 0f, opacity);

    public static GlassTone ToneFor(PhoneTheme theme) => ToneFor(theme.AppBackground);

    public static GlassTone ToneFor(Vector4 background) =>
        Palette.Luminance(background) >= 0.5f ? GlassTone.Light : GlassTone.Dark;

    public static void ThemedGlass(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, float scale,
        PhoneTheme theme, float opacity = 1f) =>
        LiquidGlass(drawList, min, max, radius, scale, ToneFor(theme), 0f, opacity);

    public static void AccentGlass(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, float scale,
        Vector4 accent, float opacity = 1f)
    {
        if (opacity <= 0f)
        {
            return;
        }

        Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(accent with { W = opacity }));
        GlassRim(drawList, min, max, radius, scale, GlassTone.Dark, opacity);
        PointerLight(drawList, min, max, radius, scale, GlassTone.Dark, opacity);
    }

    public static void ThemedGlass(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, float scale,
        Vector4 background, float opacity = 1f) =>
        LiquidGlass(drawList, min, max, radius, scale, ToneFor(background), 0f, opacity);

    public static void LiquidGlass(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, float scale,
        GlassTone tone, float brightness, float opacity = 1f)
    {
        if (opacity <= 0f)
        {
            return;
        }

        var band = Math.Clamp(MathF.Min(max.X - min.X, max.Y - min.Y) * EdgeBandFraction, 1.5f * scale,
            10f * scale);
        var backdrop = WallpaperBackdrop.Fill(drawList, min, max, radius, opacity, LensZoom, band, EdgeRefraction);
        if (backdrop)
        {
            var local = WallpaperBackdrop.Brightness(min, max);
            if (local >= 0f)
            {
                brightness = WallpaperLegibility.Normalize(local);
            }
        }

        var tint = BodyTint(tone, brightness);
        if (!backdrop)
        {
            tint = tone == GlassTone.Light ? tint with { W = tint.W + FallbackBody } : FrostedFill;
        }

        Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(tint with { W = tint.W * opacity }));
        GlassRim(drawList, min, max, radius, scale, tone, opacity);
        PointerLight(drawList, min, max, radius, scale, tone, opacity);
    }

    public static bool LiquidGlassBand(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, float band,
        GlassTone tone, float opacity = 1f)
    {
        if (opacity <= 0f || band <= 0f)
        {
            return false;
        }

        if (!WallpaperBackdrop.FillEdge(drawList, min, max, radius, band, opacity))
        {
            return false;
        }

        var local = WallpaperBackdrop.Brightness(min, max);
        var tint = BodyTint(tone, local >= 0f ? WallpaperLegibility.Normalize(local) : 0f);
        Squircle.FillEdge(drawList, min, max, radius, band, ImGui.GetColorU32(tint with { W = tint.W * opacity }));
        return true;
    }

    private static Vector4 BodyTint(GlassTone tone, float brightness)
    {
        brightness = Math.Clamp(brightness, 0f, 1f);
        return tone == GlassTone.Light
            ? Vector4.Lerp(LightGlassCalm, LightGlassHarsh, brightness)
            : DarkGlass with { W = DarkGlass.W + (DarkGlassHarshBoost * brightness) };
    }

    private static void PointerLight(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, float scale,
        GlassTone tone, float opacity)
    {
        var reach = PointerReachUnits * scale;
        if (!UiInteract.HoverWindowOnly(new Vector2(min.X - reach, min.Y - reach), new Vector2(max.X + reach, max.Y + reach)))
        {
            return;
        }

        var strength = (tone == GlassTone.Light ? 0.75f : 0.50f) * opacity;
        Squircle.StrokeNear(drawList, min, max, radius, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, strength)),
            1.4f * scale, ImGui.GetMousePos(), reach);
    }

    public static void PointerHalo(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, float strength,
        float scale)
    {
        if (strength <= 0.001f)
        {
            return;
        }

        var pad = (max.X - min.X) * 0.09f;
        var haloMin = new Vector2(min.X - pad, min.Y - pad);
        var haloMax = new Vector2(max.X + pad, max.Y + pad);
        Squircle.Fill(drawList, haloMin, haloMax, radius + pad,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.14f * strength)));
        Squircle.Stroke(drawList, haloMin, haloMax, radius + pad,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.16f * strength)), 1f * scale);
    }

    public static void PointerSpecular(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius,
        Vector2 direction, float strength, float scale)
    {
        if (strength <= 0.001f)
        {
            return;
        }

        Squircle.StrokeDirectional(drawList, min, max, radius,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.85f * strength)), 1.6f * scale, direction, 2.5f);
    }

    public static void GlassRim(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, float scale,
        GlassTone tone, float opacity = 1f)
    {
        if (opacity <= 0f)
        {
            return;
        }

        var light = tone == GlassTone.Light;
        Squircle.Stroke(drawList, min, max, radius,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, (light ? 0.40f : 0.22f) * opacity)), 1f * scale);
        var inset = 1.5f * scale;
        var innerMin = new Vector2(min.X + inset, min.Y + inset);
        var innerMax = new Vector2(max.X - inset, max.Y - inset);
        if (innerMax.X - innerMin.X <= 1f || innerMax.Y - innerMin.Y <= 1f)
        {
            return;
        }

        var bright = new Vector4(1f, 1f, 1f, (light ? 0.50f : 0.28f) * opacity);
        var dim = new Vector4(0f, 0f, 0f, (light ? 0.16f : 0.30f) * opacity);
        RefractionEdge(drawList, innerMin, innerMax, MathF.Max(radius - inset, 0f), bright, dim, 1.2f * scale);
        SheenBlock(drawList, min, max, radius, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.08f * opacity)),
            1f * scale, 0.5f);
    }

    private static void RefractionEdge(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, Vector4 bright,
        Vector4 dim, float thickness)
    {
        var box = Squircle.CornerBox(min, max, radius);
        var brightColor = ImGui.GetColorU32(bright);
        var dimColor = ImGui.GetColorU32(dim);
        var side = ImGui.GetColorU32(bright with { W = bright.W * 0.45f });
        if (max.X - min.X > 2f * box)
        {
            drawList.AddLine(new Vector2(min.X + box, min.Y), new Vector2(max.X - box, min.Y), brightColor, thickness);
            drawList.AddLine(new Vector2(min.X + box, max.Y), new Vector2(max.X - box, max.Y), dimColor, thickness);
        }

        if (max.Y - min.Y > 2f * box)
        {
            drawList.AddLine(new Vector2(min.X, min.Y + box), new Vector2(min.X, max.Y - box), side, thickness);
            drawList.AddLine(new Vector2(max.X, min.Y + box), new Vector2(max.X, max.Y - box), dimColor, thickness);
        }

        Squircle.StrokeCorner(drawList, min, max, radius, 0, bright, bright, thickness);
        Squircle.StrokeCorner(drawList, min, max, radius, 1, dim, bright, thickness);
        Squircle.StrokeCorner(drawList, min, max, radius, 2, dim, dim, thickness);
        Squircle.StrokeCorner(drawList, min, max, radius, 3, bright, dim, thickness);
    }

    public static void Card(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, Vector4 fill, float scale,
        float opacity = 1f)
    {
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(fill with { W = fill.W * opacity }), rounding);
        Edge(drawList, min, max, rounding, scale, opacity);
    }

    public static void Edge(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, float scale,
        float opacity = 1f)
    {
        if (opacity <= 0f)
        {
            return;
        }

        drawList.AddRect(min, max, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, BorderAlpha * opacity)), rounding,
            ImDrawFlags.RoundCornersAll, 1f * scale);
        SheenRounded(drawList, min, max, rounding, HighlightColor(opacity), 1f * scale, 1f * scale);
    }

    public static void EdgeSquircle(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, float scale,
        float opacity = 1f)
    {
        if (opacity <= 0f)
        {
            return;
        }

        Squircle.Stroke(drawList, min, max, radius, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, BorderAlpha * opacity)),
            1f * scale);
        Sheen(drawList, min, max, radius, HighlightColor(opacity), 1f * scale, 1f * scale);
    }

    private static uint HighlightColor(float opacity) =>
        ImGui.GetColorU32(new Vector4(1f, 1f, 1f, HighlightAlpha * opacity));

    public static void Sheen(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, uint color,
        float thickness, float depth)
    {
        var box = Squircle.CornerBox(min, max, radius);
        DrawSheen(drawList, min, max, box, Squircle.EdgeInset(box, depth), color, thickness, depth);
    }

    public static void SheenRounded(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, uint color,
        float thickness, float depth)
    {
        var box = Squircle.CornerBox(min, max, radius);
        DrawSheen(drawList, min, max, box, RoundedInset(box, depth), color, thickness, depth);
    }

    public static void SheenBlock(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, uint color,
        float depth, float coverage)
    {
        if ((color & AlphaChannel) == 0u)
        {
            return;
        }

        var box = Squircle.CornerBox(min, max, radius);
        var inset = Squircle.EdgeInset(box, depth);
        var left = min.X + inset;
        var right = max.X - inset;
        var top = min.Y + depth;
        var bottom = min.Y + (max.Y - min.Y) * coverage;
        if (right - left <= 1f || bottom <= top)
        {
            return;
        }

        var middle = (left + right) * 0.5f;
        var taper = MathF.Max(box - inset, (right - left) * SheenFalloff);
        var solidLeft = MathF.Min(left + taper, middle);
        var solidRight = MathF.Max(right - taper, middle);
        var clear = color & ~AlphaChannel;
        drawList.AddRectFilledMultiColor(new Vector2(left, top), new Vector2(solidLeft, bottom), clear, color, clear,
            clear);
        if (solidRight > solidLeft)
        {
            drawList.AddRectFilledMultiColor(new Vector2(solidLeft, top), new Vector2(solidRight, bottom), color, color,
                clear, clear);
        }

        drawList.AddRectFilledMultiColor(new Vector2(solidRight, top), new Vector2(right, bottom), color, clear, clear,
            clear);
    }

    private static float RoundedInset(float box, float depth)
    {
        if (box <= 0f || depth <= 0f)
        {
            return MathF.Max(box, 0f);
        }

        if (depth >= box)
        {
            return 0f;
        }

        var reach = box - depth;
        return box - MathF.Sqrt(MathF.Max(box * box - reach * reach, 0f));
    }

    private static void DrawSheen(ImDrawListPtr drawList, Vector2 min, Vector2 max, float box, float inset, uint color,
        float thickness, float depth)
    {
        if ((color & AlphaChannel) == 0u)
        {
            return;
        }

        var left = min.X + inset;
        var right = max.X - inset;
        if (right - left <= 1f)
        {
            return;
        }

        var top = min.Y + depth;
        var bottom = top + MathF.Max(thickness, 1f);
        var middle = (left + right) * 0.5f;
        var taper = MathF.Max(box - inset, (right - left) * SheenFalloff);
        var solidLeft = MathF.Min(left + taper, middle);
        var solidRight = MathF.Max(right - taper, middle);
        var clear = color & ~AlphaChannel;
        drawList.AddRectFilledMultiColor(new Vector2(left, top), new Vector2(solidLeft, bottom), clear, color, color,
            clear);
        if (solidRight > solidLeft)
        {
            drawList.AddRectFilled(new Vector2(solidLeft, top), new Vector2(solidRight, bottom), color);
        }

        drawList.AddRectFilledMultiColor(new Vector2(solidRight, top), new Vector2(right, bottom), color, clear, clear,
            color);
    }
}
