using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Stage;

internal enum QuickChip : byte
{
    None,
    Half,
    Double,
    Max,
}

internal readonly record struct DeckQuickLabels(float Half, float Double, float Max);

internal readonly struct DeckAmountRow
{
    public const float FieldMinWidth = 88f;
    public const string HalfLabel = "½";
    public const string DoubleLabel = "2×";

    public readonly Rect Field;
    public readonly Rect Half;
    public readonly Rect Double;
    public readonly Rect Max;

    private DeckAmountRow(Rect field, Rect half, Rect twice, Rect max)
    {
        Field = field;
        Half = half;
        Double = twice;
        Max = max;
    }

    public bool HasHalf => Half.Width > 0f;

    public bool HasDouble => Double.Width > 0f;

    public bool HasMax => Max.Width > 0f;

    public static DeckAmountRow Empty(Vector2 at)
    {
        var empty = new Rect(at, at);
        return new DeckAmountRow(empty, empty, empty, empty);
    }

    public static DeckQuickLabels Measure(float rowHeight)
    {
        var style = Button.LabelStyle(rowHeight);
        return new DeckQuickLabels(Typography.Measure(HalfLabel, style).X, Typography.Measure(DoubleLabel, style).X,
            Typography.Measure(Loc.T(L.Casino.BetMax), style).X);
    }

    public static Rect FieldSurface(ImDrawListPtr drawList, Rect field, in ControlInk ink)
    {
        var fill = Surfaces.Fill(ink, FillLevel.Tertiary);
        Squircle.Fill(drawList, field.Min, field.Max, field.Height * 0.5f, ImGui.GetColorU32(fill));
        return field;
    }

    public static float FieldInset(float scale) => DeckActions.PillPadX * scale;

    public static TextStyle AmountStyle(string text, float room)
    {
        if (CurrencyGlyph.MeasureAmount(text, TextStyles.Headline).X <= room)
        {
            return TextStyles.Headline;
        }

        return CurrencyGlyph.MeasureAmount(text, TextStyles.SubheadlineEmphasized).X <= room
            ? TextStyles.SubheadlineEmphasized
            : TextStyles.FootnoteEmphasized;
    }

    public QuickChip DrawChips(in ControlInk ink, bool enabled)
    {
        var tapped = QuickChip.None;
        if (HasHalf && DeckActions.DrawPill(Half, HalfLabel, ink, ButtonStyle.Gray, enabled))
        {
            tapped = QuickChip.Half;
        }

        if (HasDouble && DeckActions.DrawPill(Double, DoubleLabel, ink, ButtonStyle.Gray, enabled))
        {
            tapped = QuickChip.Double;
        }

        if (HasMax && DeckActions.DrawPill(Max, Loc.T(L.Casino.BetMax), ink, ButtonStyle.Gray, enabled))
        {
            tapped = QuickChip.Max;
        }

        return tapped;
    }

    public static DeckAmountRow Compute(Rect row, in DeckQuickLabels labels, float scale)
    {
        var gap = DeckActions.Gap * scale;
        var fieldMin = FieldMinWidth * scale;
        var half = DeckActions.PillWidth(labels.Half, scale);
        var twice = DeckActions.PillWidth(labels.Double, scale);
        var max = DeckActions.PillWidth(labels.Max, scale);
        var showHalf = true;
        var showDouble = true;
        var showMax = true;
        if (row.Width - Chips(half, twice, max, true, true, true, gap) < fieldMin)
        {
            showHalf = false;
        }

        if (row.Width - Chips(half, twice, max, false, true, true, gap) < fieldMin)
        {
            showDouble = false;
        }

        if (row.Width - Chips(half, twice, max, false, false, true, gap) < MathF.Min(fieldMin, max))
        {
            showMax = false;
        }

        var right = row.Max.X;
        var maxRect = Place(row, ref right, showMax ? max : 0f, gap);
        var doubleRect = Place(row, ref right, showDouble ? twice : 0f, gap);
        var halfRect = Place(row, ref right, showHalf ? half : 0f, gap);
        var field = new Rect(row.Min, new Vector2(MathF.Max(row.Min.X, right), row.Max.Y));
        return new DeckAmountRow(field, halfRect, doubleRect, maxRect);
    }

    private static float Chips(float half, float twice, float max, bool withHalf, bool withDouble, bool withMax,
        float gap)
    {
        var total = 0f;
        if (withHalf)
        {
            total += half + gap;
        }

        if (withDouble)
        {
            total += twice + gap;
        }

        if (withMax)
        {
            total += max + gap;
        }

        return total;
    }

    private static Rect Place(Rect row, ref float right, float width, float gap)
    {
        if (width <= 0f)
        {
            return new Rect(new Vector2(right, row.Min.Y), new Vector2(right, row.Max.Y));
        }

        var rect = new Rect(new Vector2(right - width, row.Min.Y), new Vector2(right, row.Max.Y));
        right = rect.Min.X - gap;
        return rect;
    }
}
