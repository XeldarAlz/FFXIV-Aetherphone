using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal enum PrismGlow : byte
{
    Pearl,
    Spectrum,
}

internal static class PrismLight
{
    public const float EdgeAlpha = 0.9f;
    public const float PrimaryAlpha = 0.5f;
    public const float PrimaryHoverAlpha = 0.85f;
    public const float FieldAlpha = 0.85f;
    public const float TabAlpha = 0.9f;

    private const float EdgeUnits = 1.35f;
    private const float EdgeGapUnits = 1.6f;
    private const float PrimaryUnits = 1f;
    private const float PrimaryGapUnits = 1.5f;
    private const float Faint = 0.003f;
    private const float MarkUnits = 24f;
    private const float MarkStrokeUnits = 1.6f;
    private const float MarkBackEdge = 0.28f;
    private const float MarkRidge = 0.7f;
    private const float MarkInnerBeam = 0.35f;
    private const float MarkBeamWidth = 0.8f;
    private const float MarkFanWidth = 0.78f;
    private const float MarkFanReach = 8.5f;
    private const float MarkFanTop = 7.6f;
    private const float MarkFanStep = 2.48f;
    private const float MarkFanClosed = 1.6f;
    private const float WeekDeepMix = 0.8f;
    private const float WeekGlowMix = 0.52f;
    private const float WeekFadeShare = 0.62f;
    private const float WeekFadeAlpha = 0.55f;
    private const float WeekGlowAlpha = 0.62f;
    private const float WeekGlowReach = 0.75f;
    private const float WeekGlowX = 0.8f;
    private const float WeekGlowY = 0.12f;
    private const float WeekMarkInk = 0.2f;
    private const float WeekMarkFan = 0.7f;
    private const uint HashSeed = 2166136261u;
    private const uint HashPrime = 16777619u;

    public const float MarkLeftUnits = 1.5f;
    public const float MarkWidthUnits = 25.6f;

    public static readonly Vector4[] Hues =
    {
        AccentRing.Red, AccentRing.Orange, AccentRing.Gold, AccentRing.Green, AccentRing.Azure, AccentRing.Violet,
    };

    private static readonly Vector4[] PearlOnDark =
    {
        new(0.902f, 0.882f, 0.839f, 1f), new(1f, 1f, 1f, 1f), new(0.886f, 0.906f, 0.941f, 1f), new(1f, 1f, 1f, 1f),
        new(0.929f, 0.910f, 0.871f, 1f),
    };

    private static readonly Vector4[] PearlOnLight =
    {
        new(0.812f, 0.788f, 0.745f, 1f), new(0.749f, 0.776f, 0.824f, 1f), new(0.812f, 0.788f, 0.745f, 1f),
    };

    private static readonly uint OpaqueWhite = Palette.Pack(new Vector4(1f, 1f, 1f, 1f));
    private static readonly Vector2 MarkApex = new(11f, 3f);
    private static readonly Vector2 MarkLeft = new(3f, 18.5f);
    private static readonly Vector2 MarkRight = new(20.5f, 16.5f);
    private static readonly Vector2 MarkFront = new(12.5f, 21.5f);
    private static readonly Vector2 MarkEntry = new(6.4f, 11.6f);
    private static readonly Vector2 MarkExit = new(15.6f, 9.2f);
    private static readonly Vector2 MarkSource = new(-MarkLeftUnits, 12.6f);

    private static string weekId = string.Empty;
    private static int weekHue;

    public static float EdgeWidth(float scale) => MathF.Max(1f, EdgeUnits * scale);

    public static void Tint(ImDrawListPtr drawList, int firstVertex, float left, float right, PrismGlow glow,
        float alpha)
    {
        var stops = Stops(glow);
        var last = stops.Length - 1;
        var span = MathF.Max(1f, right - left);
        var vertices = drawList.VtxBuffer.AsSpan();
        for (var index = Math.Max(0, firstVertex); index < vertices.Length; index++)
        {
            ref var vertex = ref vertices[index];
            var at = Math.Clamp((vertex.Pos.X - left) / span, 0f, 1f) * last;
            var stop = Math.Min((int)at, last - 1);
            var color = Vector4.Lerp(stops[stop], stops[stop + 1], at - stop);
            color.W = (vertex.Col >> 24) / 255f * alpha;
            vertex.Col = Palette.Pack(color);
        }
    }

    public static void Edge(ImDrawListPtr drawList, Rect rect, float radius, PrismGlow glow, float alpha, float scale)
    {
        var gap = EdgeGapUnits * scale;
        Outline(drawList, rect, radius, gap, EdgeWidth(scale), glow, alpha);
    }

    public static void PillEdge(ImDrawListPtr drawList, Rect pill, PrismGlow glow, float alpha, float scale) =>
        Edge(drawList, pill, pill.Height * 0.5f, glow, alpha, scale);

    public static void Primary(ImDrawListPtr drawList, Rect button, bool enabled, float scale)
    {
        if (!enabled)
        {
            return;
        }

        var alpha = UiInteract.Hover(button.Min, button.Max) ? PrimaryHoverAlpha : PrimaryAlpha;
        Outline(drawList, button, button.Height * 0.5f, PrimaryGapUnits * scale, MathF.Max(1f, PrimaryUnits * scale),
            PrismGlow.Spectrum, alpha);
    }

    public static void CircleEdge(ImDrawListPtr drawList, Vector2 center, float radius, PrismGlow glow, float alpha,
        float scale)
    {
        if (alpha <= Faint)
        {
            return;
        }

        var outer = radius + EdgeGapUnits * scale;
        var mark = drawList.VtxBuffer.Size;
        drawList.AddCircle(center, outer, OpaqueWhite, 0, EdgeWidth(scale));
        Tint(drawList, mark, center.X - outer, center.X + outer, glow, alpha);
    }

    public static void Strip(ImDrawListPtr drawList, float left, float right, float top, float height, float alpha)
    {
        if (alpha <= Faint || right <= left)
        {
            return;
        }

        var step = (right - left) / (Hues.Length - 1);
        for (var index = 0; index < Hues.Length - 1; index++)
        {
            var from = Palette.Pack(Hues[index] with { W = alpha });
            var to = Palette.Pack(Hues[index + 1] with { W = alpha });
            var x = left + step * index;
            drawList.AddRectFilledMultiColor(new Vector2(x, top), new Vector2(x + step, top + height), from, to, to,
                from);
        }
    }

    public static void Mark(ImDrawListPtr drawList, Vector2 center, float size, Vector4 ink, float fan,
        float fanAlpha = 1f)
    {
        var unit = size / MarkUnits;
        var origin = center - new Vector2(size * 0.5f, size * 0.5f);
        var thickness = MathF.Max(1f, MarkStrokeUnits * unit);
        var solid = ImGui.GetColorU32(ink);
        drawList.AddLine(origin + MarkSource * unit, origin + MarkEntry * unit, solid, thickness * MarkBeamWidth);
        drawList.AddLine(origin + MarkEntry * unit, origin + MarkExit * unit,
            ImGui.GetColorU32(ink with { W = ink.W * MarkInnerBeam }), thickness * MarkBeamWidth);
        if (fan > Faint && fanAlpha > Faint)
        {
            var exit = origin + MarkExit * unit;
            for (var index = 0; index < Hues.Length; index++)
            {
                var end = new Vector2(MarkExit.X + MarkFanReach * fan,
                    MarkFanTop + index * MarkFanStep * fan + (1f - fan) * MarkFanClosed);
                drawList.AddLine(exit, origin + end * unit,
                    ImGui.GetColorU32(Hues[index] with { W = fanAlpha * ink.W }), thickness * MarkFanWidth);
            }
        }

        drawList.AddLine(origin + MarkLeft * unit, origin + MarkRight * unit,
            ImGui.GetColorU32(ink with { W = ink.W * MarkBackEdge }), thickness);
        drawList.PathLineTo(origin + MarkApex * unit);
        drawList.PathLineTo(origin + MarkLeft * unit);
        drawList.PathLineTo(origin + MarkFront * unit);
        drawList.PathLineTo(origin + MarkRight * unit);
        drawList.PathStroke(solid, ImDrawFlags.Closed, thickness);
        drawList.AddLine(origin + MarkApex * unit, origin + MarkFront * unit,
            ImGui.GetColorU32(ink with { W = ink.W * MarkRidge }), thickness);
    }

    public static void Week(ImDrawListPtr drawList, Rect rect, string id, Vector4 page, Vector4 ink, float markSize,
        float markHeightShare, float opacity = 1f)
    {
        var hue = WeekHue(id);
        var deep = Vector4.Lerp(Hues[hue], page, WeekDeepMix) with { W = opacity };
        var glow = Vector4.Lerp(Hues[(hue + 1) % Hues.Length], page, WeekGlowMix);
        var middle = rect.Min.Y + rect.Height * WeekFadeShare;
        var fade = Palette.Pack(deep with { W = WeekFadeAlpha * opacity });
        var top = Palette.Pack(deep);
        var bottom = Palette.Pack(page with { W = opacity });
        drawList.AddRectFilled(rect.Min, rect.Max, bottom);
        drawList.AddRectFilledMultiColor(rect.Min, new Vector2(rect.Max.X, middle), top, top, fade, fade);
        drawList.AddRectFilledMultiColor(new Vector2(rect.Min.X, middle), rect.Max, fade, fade,
            Palette.Pack(deep with { W = 0f }), Palette.Pack(deep with { W = 0f }));
        drawList.PushClipRect(rect.Min, rect.Max, true);
        SoftGlow.Draw(drawList,
            new Vector2(rect.Min.X + rect.Width * WeekGlowX, rect.Min.Y + rect.Height * WeekGlowY),
            MathF.Min(MathF.Max(rect.Width, rect.Height) * WeekGlowReach, rect.Height * (1f - WeekGlowY)),
            glow with { W = WeekGlowAlpha * opacity });
        if (markSize > 0f)
        {
            Mark(drawList, new Vector2(rect.Center.X, rect.Min.Y + rect.Height * markHeightShare), markSize,
                ink with { W = WeekMarkInk * opacity }, 1f, WeekMarkFan);
        }

        drawList.PopClipRect();
    }

    private static int WeekHue(string id)
    {
        if (ReferenceEquals(id, weekId) || id == weekId)
        {
            return weekHue;
        }

        var hash = HashSeed;
        for (var index = 0; index < id.Length; index++)
        {
            hash = (hash ^ id[index]) * HashPrime;
        }

        weekId = id;
        weekHue = (int)(hash % (uint)Hues.Length);
        return weekHue;
    }

    private static void Outline(ImDrawListPtr drawList, Rect rect, float radius, float gap, float thickness,
        PrismGlow glow, float alpha)
    {
        if (alpha <= Faint)
        {
            return;
        }

        var min = rect.Min - new Vector2(gap, gap);
        var max = rect.Max + new Vector2(gap, gap);
        var mark = drawList.VtxBuffer.Size;
        Squircle.Stroke(drawList, min, max, radius + gap, OpaqueWhite, thickness);
        Tint(drawList, mark, min.X, max.X, glow, alpha);
    }

    private static Vector4[] Stops(PrismGlow glow) =>
        glow == PrismGlow.Spectrum ? Hues : PrismArt.Light ? PearlOnLight : PearlOnDark;
}
