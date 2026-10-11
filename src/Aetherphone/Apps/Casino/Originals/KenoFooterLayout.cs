using Aetherphone.Core;

namespace Aetherphone.Apps.Casino.Originals;

internal static class KenoFooterLayout
{
    public const float ButtonHeight = 44f;
    public const float Gap = 8f;

    public static float Height(float statusHeight, float scale) => statusHeight + Gap * scale + ButtonHeight * scale;

    public static Rect Status(Rect footer, float statusHeight) =>
        new(footer.Min, new Vector2(footer.Max.X, footer.Min.Y + statusHeight));

    public static void Buttons(Rect footer, float statusHeight, float firstWidth, float secondWidth, float scale,
        out Rect first, out Rect second)
    {
        var gap = Gap * scale;
        var top = footer.Min.Y + statusHeight + gap;
        var bottom = top + ButtonHeight * scale;
        var room = MathF.Max(0f, footer.Width - gap);
        var natural = firstWidth + secondWidth;
        var shrink = natural > room && natural > 0f ? room / natural : 1f;
        var firstShown = firstWidth * shrink;
        var secondShown = secondWidth * shrink;
        var left = footer.Center.X - (firstShown + gap + secondShown) * 0.5f;
        first = new Rect(new Vector2(left, top), new Vector2(left + firstShown, bottom));
        second = new Rect(new Vector2(first.Max.X + gap, top), new Vector2(first.Max.X + gap + secondShown, bottom));
    }
}
