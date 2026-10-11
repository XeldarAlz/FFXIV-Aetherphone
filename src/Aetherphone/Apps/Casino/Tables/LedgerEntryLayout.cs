using Aetherphone.Core;

namespace Aetherphone.Apps.Casino.Tables;

internal static class LedgerEntryLayout
{
    public const float Pad = 14f;
    public const float LineGap = 2f;
    public const float StateGap = 4f;
    public const float ButtonGap = 8f;
    public const float Touch = 44f;
    public const float FitSlack = 0.5f;

    public static int ButtonRows(float innerWidth, float confirmWidth, float disputeWidth, float scale)
    {
        var count = (confirmWidth > 0f ? 1 : 0) + (disputeWidth > 0f ? 1 : 0);
        if (count < 2)
        {
            return count;
        }

        return confirmWidth + ButtonGap * scale + disputeWidth <= innerWidth + FitSlack ? 1 : 2;
    }

    public static float ButtonBlock(int rows, float scale) =>
        rows <= 0 ? 0f : rows * Touch * scale + (rows - 1) * ButtonGap * scale;

    public static float Height(float titleLine, float partiesLine, float stateLine, int buttonRows, float scale)
    {
        var buttons = ButtonBlock(buttonRows, scale);
        return Pad * 2f * scale + titleLine + LineGap * scale + partiesLine + StateGap * scale + stateLine
            + (buttons > 0f ? ButtonGap * scale + buttons : 0f);
    }

    public static float StateTop(float cardTop, float titleLine, float partiesLine, float scale) =>
        cardTop + Pad * scale + titleLine + LineGap * scale + partiesLine + StateGap * scale;

    public static float ButtonsTop(in Rect card, int buttonRows, float scale) =>
        card.Max.Y - Pad * scale - ButtonBlock(buttonRows, scale);

    public static Rect StackedButton(in Rect card, int index, int buttonRows, float scale)
    {
        var pad = Pad * scale;
        var touch = Touch * scale;
        var y = ButtonsTop(card, buttonRows, scale) + index * (touch + ButtonGap * scale);
        return new Rect(new Vector2(card.Min.X + pad, y), new Vector2(card.Max.X - pad, y + touch));
    }

    public static Rect RowButton(in Rect card, float right, float width, float scale)
    {
        var top = ButtonsTop(card, 1, scale);
        return new Rect(new Vector2(right - width, top), new Vector2(right, top + Touch * scale));
    }
}
