using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;

namespace Aetherphone.Apps.Casino.Cabinets;

internal readonly struct BarkeepDeckLayout
{
    public const float RowHeight = 44f;
    public const float MinimumPill = 44f;
    public const int HudCells = 3;

    public readonly Rect Row;
    public readonly Rect Action;

    private BarkeepDeckLayout(Rect row, Rect action)
    {
        Row = row;
        Action = action;
    }

    public static float Height => MathF.Max(BetComposer.DeckHeightFor(true, true),
        DeckActions.Pad * 2f + RowHeight + DeckActions.Gap + DeckActions.PrimaryHeight);

    public static BarkeepDeckLayout Compute(Rect deck, float scale)
    {
        var pad = DeckActions.Pad * scale;
        var row = new Rect(new Vector2(deck.Min.X + pad, deck.Min.Y + pad),
            new Vector2(deck.Max.X - pad, deck.Min.Y + pad + RowHeight * scale));
        return new BarkeepDeckLayout(row, DeckActions.Row(deck, scale));
    }

    public static Rect Pill(Rect row, float labelWidth, float scale)
    {
        var width = Math.Clamp(labelWidth + row.Height, MathF.Min(MinimumPill * scale, row.Width), row.Width);
        return new Rect(new Vector2(row.Center.X - width * 0.5f, row.Min.Y),
            new Vector2(row.Center.X + width * 0.5f, row.Max.Y));
    }

    public static Rect Cell(Rect row, int index)
    {
        var width = row.Width / HudCells;
        var left = row.Min.X + width * index;
        return new Rect(new Vector2(left, row.Min.Y), new Vector2(left + width, row.Max.Y));
    }
}
