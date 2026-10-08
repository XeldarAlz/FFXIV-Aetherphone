using Aetherphone.Core;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Casino.Tables;

internal static class PrimaryAction
{
    public const float Pad = BetComposer.Pad;
    public const float Gap = BetComposer.Gap;

    public static Rect Row(Rect deck, float scale)
    {
        var top = deck.Max.Y - (Pad + Button.LargeHeight) * scale;
        return new Rect(new Vector2(deck.Min.X + Pad * scale, top),
            new Vector2(deck.Max.X - Pad * scale, top + Button.LargeHeight * scale));
    }

    public static Rect Above(Rect row, float height, float scale)
    {
        var bottom = row.Min.Y - Gap * scale;
        return new Rect(new Vector2(row.Min.X, bottom - height), new Vector2(row.Max.X, bottom));
    }

    public static bool Draw(Rect row, string label, in ControlInk ink, bool enabled, string id)
    {
        return Button.Draw(row, label, ink, ButtonStyle.Prominent, enabled: enabled, id: id) && enabled;
    }

    public static bool Secondary(Rect rect, string label, in ControlInk ink, bool enabled, string id)
    {
        return Button.Draw(rect, label, ink, ButtonStyle.Gray, enabled: enabled, id: id) && enabled;
    }

    public static Rect Slice(Rect row, int index, int count, float scale)
    {
        var gap = Gap * scale;
        var width = (row.Width - gap * (count - 1)) / count;
        var left = row.Min.X + index * (width + gap);
        return new Rect(new Vector2(left, row.Min.Y), new Vector2(left + width, row.Max.Y));
    }
}
