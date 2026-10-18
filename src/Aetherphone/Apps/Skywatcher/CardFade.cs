using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Skywatcher;

internal readonly record struct CardFade(float Edge, float Height, float Strength)
{
    private const uint ColorMask = 0x00FFFFFF;
    private const int AlphaShift = 24;
    private const float SliceHeight = 1.5f;
    private const int MaxSlices = 16;
    private const float SideSlackUnits = 2f;
    private const float MinimumOpacity = 0.004f;

    public bool Active => Strength > 0f && Height > 0f;

    public float Bottom => Edge + Height;

    public static CardFade Under(float edge, float cardsTop, float height, float ramp) =>
        new(edge, height, ramp <= 0f ? 1f : Math.Clamp((edge - cardsTop) / ramp, 0f, 1f));

    public float Visibility(float y)
    {
        if (y >= Bottom || Height <= 0f)
        {
            return 1f;
        }

        if (y <= Edge)
        {
            return 1f - Strength;
        }

        var fraction = (y - Edge) / Height;
        var rise = fraction * fraction * (3f - 2f * fraction);
        return 1f - Strength * (1f - rise);
    }

    public void Apply(ImDrawListPtr drawList, int fromVertex)
    {
        if (!Active || drawList.IsNull)
        {
            return;
        }

        var bottom = Bottom;
        var vertices = drawList.VtxBuffer.AsSpan();
        for (var index = Math.Max(0, fromVertex); index < vertices.Length; index++)
        {
            ref var vertex = ref vertices[index];
            if (vertex.Pos.Y >= bottom)
            {
                continue;
            }

            var alpha = (uint)((vertex.Col >> AlphaShift) * Visibility(vertex.Pos.Y) + 0.5f);
            vertex.Col = (vertex.Col & ColorMask) | (alpha << AlphaShift);
        }
    }

    public void Panel(ImDrawListPtr drawList, Rect card, in SkyPalette palette, float density, float scale)
    {
        var slack = SideSlackUnits * scale;
        if (!Active || card.Min.Y - slack >= Bottom)
        {
            WeatherCard.Panel(drawList, card, palette, density, scale);
            return;
        }

        var left = card.Min.X - slack;
        var right = card.Max.X + slack;
        var bandBottom = MathF.Round(Bottom);
        if (card.Max.Y + slack > bandBottom)
        {
            drawList.PushClipRect(new Vector2(left, bandBottom), new Vector2(right, card.Max.Y + slack), true);
            WeatherCard.Panel(drawList, card, palette, density, scale);
            drawList.PopClipRect();
        }

        var top = MathF.Floor(MathF.Max(card.Min.Y - slack, Edge));
        var bottom = MathF.Min(MathF.Ceiling(card.Max.Y + slack), bandBottom);
        if (bottom <= top)
        {
            return;
        }

        var slices = Math.Clamp((int)MathF.Ceiling((bottom - top) / SliceHeight), 1, MaxSlices);
        for (var sliceIndex = 0; sliceIndex < slices; sliceIndex++)
        {
            var sliceTop = SliceBoundary(top, bottom, sliceIndex, slices);
            var sliceBottom = SliceBoundary(top, bottom, sliceIndex + 1, slices);
            var opacity = Visibility((sliceTop + sliceBottom) * 0.5f);
            if (sliceBottom <= sliceTop || opacity <= MinimumOpacity)
            {
                continue;
            }

            drawList.PushClipRect(new Vector2(left, sliceTop), new Vector2(right, sliceBottom), true);
            WeatherCard.Panel(drawList, card, palette, density, scale, opacity: opacity);
            drawList.PopClipRect();
        }
    }

    internal static float SliceBoundary(float top, float bottom, int boundaryIndex, int slices) =>
        boundaryIndex >= slices ? bottom : MathF.Round(top + (bottom - top) * boundaryIndex / slices);
}
