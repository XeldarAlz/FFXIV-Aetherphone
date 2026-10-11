using Aetherphone.Core;

namespace Aetherphone.Apps.Casino.Stage;

internal readonly struct BetDeckLayout
{
    public const float Pad = DeckActions.Pad;
    public const float Gap = DeckActions.Gap;
    public const float RowHeight = DeckActions.SecondaryHeight;
    public const float AmountHeight = RowHeight;
    public const float KnobHeight = RowHeight;
    public const float ActionHeight = DeckActions.PrimaryHeight;
    public const float FieldMinWidth = DeckAmountRow.FieldMinWidth;
    public const float PrimaryMinWidth = 120f;

    public readonly Rect Knob;
    public readonly DeckAmountRow Amount;
    public readonly Rect Auto;
    public readonly Rect Action;
    public readonly bool HasKnob;
    public readonly bool HasAmount;
    public readonly bool AutoCompact;

    private BetDeckLayout(Rect knob, in DeckAmountRow amount, Rect auto, Rect action, bool hasKnob, bool hasAmount,
        bool autoCompact)
    {
        Knob = knob;
        Amount = amount;
        Auto = auto;
        Action = action;
        HasKnob = hasKnob;
        HasAmount = hasAmount;
        AutoCompact = autoCompact;
    }

    public bool HasAuto => Auto.Width > 0f;

    public Rect Field => Amount.Field;

    public Rect Half => Amount.Half;

    public Rect Double => Amount.Double;

    public Rect Max => Amount.Max;

    public static float DeckHeightFor(bool knob, bool fixedAmount)
    {
        var height = Pad * 2f + ActionHeight;
        if (!fixedAmount)
        {
            height += AmountHeight + Gap;
        }

        if (knob)
        {
            height += KnobHeight + Gap;
        }

        return height;
    }

    public static float AutoWidth(float labelWidth, float scale) =>
        (DeckActions.PillPadX * 2f + DeckActions.IconSize + DeckActions.IconGap) * scale + labelWidth;

    public static BetDeckLayout Compute(Rect deck, bool knob, bool fixedAmount, bool auto, in DeckQuickLabels quick,
        float autoLabelWidth, float scale)
    {
        var left = deck.Min.X + Pad * scale;
        var right = MathF.Max(left, deck.Max.X - Pad * scale);
        var top = deck.Min.Y + Pad * scale;
        var gap = Gap * scale;
        var knobRect = new Rect(new Vector2(left, top), new Vector2(left, top));
        if (knob)
        {
            knobRect = new Rect(new Vector2(left, top), new Vector2(right, top + KnobHeight * scale));
            top = knobRect.Max.Y + gap;
        }

        var amount = DeckAmountRow.Empty(new Vector2(left, top));
        if (!fixedAmount)
        {
            var row = new Rect(new Vector2(left, top), new Vector2(right, top + AmountHeight * scale));
            amount = DeckAmountRow.Compute(row, quick, scale);
            top = row.Max.Y + gap;
        }

        var actionTop = MathF.Max(top, deck.Max.Y - (Pad + ActionHeight) * scale);
        var actionRow = new Rect(new Vector2(left, actionTop), new Vector2(right, actionTop + ActionHeight * scale));
        if (!auto)
        {
            return new BetDeckLayout(knobRect, amount, new Rect(actionRow.Min, actionRow.Min), actionRow, knob,
                !fixedAmount, false);
        }

        var full = AutoWidth(autoLabelWidth, scale);
        var compact = actionRow.Width - full - gap < PrimaryMinWidth * scale;
        var pill = MathF.Min(compact ? ActionHeight * scale : full, MathF.Max(0f, actionRow.Width - gap));
        var autoRect = new Rect(actionRow.Min, new Vector2(actionRow.Min.X + pill, actionRow.Max.Y));
        var action = new Rect(new Vector2(MathF.Min(actionRow.Max.X, autoRect.Max.X + gap), actionRow.Min.Y),
            actionRow.Max);
        return new BetDeckLayout(knobRect, amount, autoRect, action, knob, !fixedAmount, compact);
    }
}
