using Aetherphone.Core;

namespace Aetherphone.Apps.Casino.Stage;

internal readonly struct BetDeckLayout
{
    public const float Pad = 12f;
    public const float Gap = 8f;
    public const float AmountHeight = 34f;
    public const float ActionHeight = DeckActions.PrimaryHeight;
    public const float KnobHeight = 36f;
    public const float QuickWidth = 52f;
    public const float QuickGapShare = 0.75f;
    public const int QuickCount = 3;
    public const int FieldQuickShare = 2;
    public const float ModeWidth = 112f;
    public const float ModeMinWidth = 84f;
    public const float ModeTrackHeight = 40f;
    public const float GearSize = 40f;
    public const float GearMaxShare = 0.35f;
    public const float PrimaryMinWidth = 120f;
    public const float ClusterMaxShare = 0.6f;

    public readonly Rect Knob;
    public readonly Rect Field;
    public readonly Rect Half;
    public readonly Rect Double;
    public readonly Rect Max;
    public readonly Rect Mode;
    public readonly Rect Gear;
    public readonly Rect Action;
    public readonly bool HasKnob;
    public readonly bool HasAmount;
    public readonly bool HasMode;

    private BetDeckLayout(Rect knob, Rect field, Rect half, Rect twice, Rect max, Rect mode, Rect gear, Rect action,
        bool hasKnob, bool hasAmount, bool hasMode)
    {
        Knob = knob;
        Field = field;
        Half = half;
        Double = twice;
        Max = max;
        Mode = mode;
        Gear = gear;
        Action = action;
        HasKnob = hasKnob;
        HasAmount = hasAmount;
        HasMode = hasMode;
    }

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

        return MathF.Max(CasinoStageLayout.DeckHeight, height);
    }

    public static BetDeckLayout Compute(Rect deck, bool knob, bool fixedAmount, bool auto, float scale)
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

        var empty = new Rect(new Vector2(left, top), new Vector2(left, top));
        var field = empty;
        var half = empty;
        var twice = empty;
        var max = empty;
        if (!fixedAmount)
        {
            var row = new Rect(new Vector2(left, top), new Vector2(right, top + AmountHeight * scale));
            AmountRow(row, scale, out field, out half, out twice, out max);
            top = row.Max.Y + gap;
        }

        var actionTop = MathF.Max(top, deck.Max.Y - (Pad + ActionHeight) * scale);
        var actionRow = new Rect(new Vector2(left, actionTop), new Vector2(right, actionTop + ActionHeight * scale));
        if (!auto)
        {
            return new BetDeckLayout(knobRect, field, half, twice, max, empty, empty, actionRow, knob, !fixedAmount,
                false);
        }

        ModeCluster(actionRow, scale, out var mode, out var gear, out var action);
        return new BetDeckLayout(knobRect, field, half, twice, max, mode, gear, action, knob, !fixedAmount, true);
    }

    private static void AmountRow(Rect row, float scale, out Rect field, out Rect half, out Rect twice, out Rect max)
    {
        var gap = Gap * QuickGapShare * scale;
        var share = QuickCount + FieldQuickShare;
        var quick = MathF.Max(0f, MathF.Min(QuickWidth * scale, (row.Width - gap * QuickCount) / share));
        var fieldRight = row.Max.X - quick * QuickCount - gap * QuickCount;
        field = new Rect(row.Min, new Vector2(MathF.Max(row.Min.X, fieldRight), row.Max.Y));
        var x = field.Max.X + gap;
        half = new Rect(new Vector2(x, row.Min.Y), new Vector2(x + quick, row.Max.Y));
        x = half.Max.X + gap;
        twice = new Rect(new Vector2(x, row.Min.Y), new Vector2(x + quick, row.Max.Y));
        x = twice.Max.X + gap;
        max = new Rect(new Vector2(x, row.Min.Y), new Vector2(x + quick, row.Max.Y));
    }

    private static void ModeCluster(Rect row, float scale, out Rect mode, out Rect gear, out Rect action)
    {
        var gap = Gap * scale;
        var gearSize = GearSize * scale;
        var desired = ModeWidth * scale + gap + gearSize;
        var minimum = ModeMinWidth * scale + gap + gearSize;
        var room = row.Width - gap - PrimaryMinWidth * scale;
        var cluster = MathF.Min(Math.Clamp(room, minimum, desired), row.Width * ClusterMaxShare);
        cluster = MathF.Max(0f, cluster);
        gearSize = MathF.Min(gearSize, cluster * GearMaxShare);
        var modeWidth = MathF.Max(0f, cluster - gap - gearSize);
        var trackHeight = MathF.Min(ModeTrackHeight * scale, row.Height);
        var center = row.Center.Y;
        mode = new Rect(new Vector2(row.Min.X, center - trackHeight * 0.5f),
            new Vector2(row.Min.X + modeWidth, center + trackHeight * 0.5f));
        var gearLeft = mode.Max.X + gap;
        gear = new Rect(new Vector2(gearLeft, center - gearSize * 0.5f),
            new Vector2(gearLeft + gearSize, center + gearSize * 0.5f));
        var actionLeft = MathF.Min(row.Max.X, gear.Max.X + gap);
        action = new Rect(new Vector2(actionLeft, row.Min.Y), row.Max);
    }
}
