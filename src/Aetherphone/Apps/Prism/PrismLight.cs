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
