using Aetherphone.Core;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Casino.Stage;

internal static class DeckActions
{
    public const float Pad = 12f;
    public const float Gap = 8f;
    public const float PrimaryHeight = 56f;
    public const float PillMinWidth = 64f;
    public const float PillMaxWidth = 132f;
    public const float PrimaryMinWidth = 140f;
    public const int MaxSecondary = 2;

    public static Rect Row(Rect deck, float scale)
    {
        var pad = Pad * scale;
        var bottom = deck.Max.Y - pad;
        var top = MathF.Max(deck.Min.Y + pad, bottom - PrimaryHeight * scale);
        return new Rect(new Vector2(deck.Min.X + pad, top), new Vector2(deck.Max.X - pad, bottom));
    }

    public static Rect Above(Rect row, float height, float scale)
    {
        var bottom = row.Min.Y - Gap * scale;
        return new Rect(new Vector2(row.Min.X, bottom - height), new Vector2(row.Max.X, bottom));
    }

    public static Rect Slice(Rect row, int index, int count, float scale)
    {
        var gap = Gap * scale;
        var width = (row.Width - gap * (count - 1)) / count;
        var left = row.Min.X + index * (width + gap);
        return new Rect(new Vector2(left, row.Min.Y), new Vector2(left + width, row.Max.Y));
    }

    public static bool DrawPrimary(Rect rect, string label, in ControlInk ink, bool enabled, string id) =>
        Button.Draw(rect, label, ink, ButtonStyle.Prominent, enabled: enabled, id: id);

    public static bool DrawSecondary(Rect rect, string label, in ControlInk ink, bool enabled, string id) =>
        Button.Draw(rect, label, ink, ButtonStyle.Gray, enabled: enabled, id: id);

    public static float PillWidth(float labelWidth, float rowHeight, float scale) =>
        Math.Clamp(labelWidth + rowHeight * 0.6f, PillMinWidth * scale, PillMaxWidth * scale);

    public static Rect Secondary(Rect row, float cursor, float width, float scale)
    {
        var limit = row.Max.X - PrimaryMinWidth * scale - Gap * scale;
        var left = MathF.Min(cursor, limit);
        var right = MathF.Min(left + width, limit);
        return new Rect(new Vector2(left, row.Min.Y), new Vector2(MathF.Max(left, right), row.Max.Y));
    }

    public static float Advance(Rect taken, float scale) => taken.Max.X + Gap * scale;

    public static Rect Primary(Rect row, float cursor) =>
        new(new Vector2(MathF.Min(MathF.Max(row.Min.X, cursor), row.Max.X), row.Min.Y), row.Max);

    public static bool DrawSecondary(Rect row, ref float cursor, ref int count, string label, bool enabled,
        in ControlInk ink, float scale)
    {
        if (count >= MaxSecondary)
        {
            return false;
        }

        var style = Button.LabelStyle(row.Height);
        var width = PillWidth(Typography.Measure(label, style).X, row.Height, scale);
        var rect = Secondary(row, cursor, width, scale);
        cursor = Advance(rect, scale);
        count++;
        return Button.Draw(rect, label, ink, ButtonStyle.Gray, enabled: enabled);
    }

    public static bool DrawPrimary(Rect row, float cursor, string label, bool enabled, in ControlInk ink,
        ButtonStyle style = ButtonStyle.Prominent, string? id = null) =>
        Button.Draw(Primary(row, cursor), label, ink, style, enabled: enabled, id: id);
}
