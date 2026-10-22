using Aetherphone.Core;

namespace Aetherphone.Harness.Inspection;

internal readonly record struct ScreenLine(string Text, Rect Bounds, float Size, uint Color, bool Clipped, bool Covered,
    float[] Lefts, float[] Rights)
{
    public Rect Span(int start, int length) =>
        new(new Vector2(Lefts[start], Bounds.Min.Y), new Vector2(Rights[start + length - 1], Bounds.Max.Y));

    public string ColorHex() =>
        $"#{Color & 0xFF:x2}{(Color >> 8) & 0xFF:x2}{(Color >> 16) & 0xFF:x2}{Color >> 24:x2}";
}
