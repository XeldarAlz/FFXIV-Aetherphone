using Aetherphone.Core;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Casino.Tables;

internal static class HostFlowLayout
{
    public const int Columns = 3;
    public const float Touch = 44f;
    public const float SectionGap = 18f;
    public const float HeadingGap = 8f;
    public const float CardPad = 14f;
    public const float CardPadTop = 12f;
    public const float CardPadBottom = 8f;
    public const float CardGap = 8f;
    public const float LineGap = 4f;
    public const float HeaderPad = 12f;
    public const float HeaderTile = 52f;
    public const float HeaderGap = 12f;
    public const float SwitcherGap = 6f;
    public const float SwitcherHeight = 40f;
    public const float SwitcherGlyph = 18f;
    public const float ChoiceHeight = 88f;
    public const float ChoiceIcon = 36f;
    public const float ChoiceGap = 8f;
    public const float ChoiceInnerGap = 7f;
    public const float CheckSize = 18f;
    public const float CheckInset = 7f;
    public const float JoinHeight = 70f;
    public const float JoinIcon = 20f;
    public const float JoinInnerGap = 6f;
    public const float SeatDisc = 30f;
    public const float SeatGap = 6f;
    public const float FieldHeight = 46f;
    public const float RulesIcon = 38f;
    public const float RulesPad = 12f;
    public const float Chevron = 12f;
    public const float SegmentTrack = 32f;
    public const float RowPad = 12f;
    public const float TogglePad = 11f;
    public const float FooterPad = 12f;
    public const float FooterGap = 10f;

    public static float ColumnWidth(float width, int columns, float gap) =>
        MathF.Max(1f, (width - gap * (columns - 1)) / columns);

    public static Rect GridCell(float left, float top, float width, int index, int columns, float height, float gap)
    {
        var columnWidth = ColumnWidth(width, columns, gap);
        var row = index / columns;
        var column = index % columns;
        var x = left + column * (columnWidth + gap);
        var y = top + row * (height + gap);
        return new Rect(new Vector2(x, y), new Vector2(x + columnWidth, y + height));
    }

    public static float GridHeight(int count, int columns, float height, float gap)
    {
        var rows = (count + columns - 1) / columns;
        return rows <= 0 ? 0f : rows * height + (rows - 1) * gap;
    }

    public static float HeaderHeight(float scale, float nameHeight, float lineBlockHeight) =>
        HeaderPad * 2f * scale + MathF.Max(HeaderTile * scale, nameHeight + LineGap * scale + lineBlockHeight);

    public static float ChoiceTileHeight(float scale, float labelBlockHeight) =>
        MathF.Max(ChoiceHeight * scale,
            CardPad * 2f * scale + ChoiceIcon * scale + ChoiceInnerGap * scale + labelBlockHeight);

    public static float JoinTileHeight(float scale, float labelBlockHeight) =>
        MathF.Max(JoinHeight * scale,
            CardPad * 2f * scale + JoinIcon * scale + JoinInnerGap * scale + labelBlockHeight);

    public static Vector2 CheckCenter(in Rect tile, float scale)
    {
        var offset = (CheckInset + CheckSize * 0.5f) * scale;
        return new Vector2(tile.Max.X - offset, tile.Min.Y + offset);
    }

    public static float SeatDiscSize(float width, int seats, float scale)
    {
        var fit = (width - SeatGap * scale * (seats - 1)) / Math.Max(1, seats);
        return MathF.Max(1f, MathF.Min(SeatDisc * scale, fit));
    }

    public static Rect SeatSlot(float left, float top, float width, int index, int seats, float scale)
    {
        var disc = SeatDiscSize(width, seats, scale);
        var gap = SeatGap * scale;
        var center = left + disc * 0.5f + index * (disc + gap);
        var half = (disc + gap) * 0.5f;
        var slotLeft = MathF.Max(left, center - half);
        var slotRight = MathF.Min(left + width, center + half);
        return new Rect(new Vector2(slotLeft, top), new Vector2(slotRight, top + Touch * scale));
    }

    public static Vector2 SeatCenter(float left, float top, float width, int index, int seats, float scale)
    {
        var disc = SeatDiscSize(width, seats, scale);
        return new Vector2(left + disc * 0.5f + index * (disc + SeatGap * scale), top + Touch * scale * 0.5f);
    }

    public static float SeatCardHeight(float scale, float headHeight) =>
        (CardPadTop + CardPadBottom) * scale + headHeight + Touch * scale;

    public static float LadderHeight(float scale, float labelHeight, float valueHeight) =>
        labelHeight + valueHeight + LadderSlider.RowHeight * scale;

    public static Rect LadderRow(float left, float top, float width, float scale, float labelHeight,
        float valueHeight)
    {
        var y = top + labelHeight + valueHeight;
        return new Rect(new Vector2(left, y), new Vector2(left + width, y + LadderSlider.RowHeight * scale));
    }

    public static float LadderBlockHeight(float scale, float labelHeight, float valueHeight, float hintHeight) =>
        LadderHeight(scale, labelHeight, valueHeight) + (hintHeight > 0f ? hintHeight + LineGap * scale : 0f);

    public static float LadderCardHeight(float scale, float blockHeight) =>
        CardPadTop * scale + blockHeight + CardPadBottom * scale;

    public static float RulesRowHeight(float scale, float titleHeight, float summaryBlockHeight) =>
        RulesPad * 2f * scale + MathF.Max(RulesIcon * scale, titleHeight + 2f * scale + summaryBlockHeight);

    public static float RulesTextWidth(float width, float scale) =>
        MathF.Max(1f, width - (CardPad * 2f + RulesIcon + HeaderGap + Chevron + LineGap * 2f) * scale);

    public static float SegmentRowHeight(float scale, float labelHeight, float hintHeight) =>
        RowPad * 2f * scale + labelHeight + CardGap * scale + SegmentTrack * scale
        + (hintHeight > 0f ? CardGap * scale + hintHeight : 0f);

    public static float ToggleTextWidth(float innerWidth, float scale) =>
        MathF.Max(1f, innerWidth - (Metrics.Size.ToggleWidth + HeaderGap) * scale);

    public static float ToggleRowHeight(float scale, float textBlockHeight) =>
        TogglePad * 2f * scale + MathF.Max(Touch * scale, textBlockHeight);

    public static float LadderRowHeight(float scale, float blockHeight) =>
        RowPad * scale + blockHeight + CardPadBottom * scale;

    public static float FooterHeight(float scale, float blockHeight) =>
        FooterPad * 2f * scale + blockHeight + FooterGap * scale + Touch * scale;

    public static Rect FooterButton(in Rect footer, float scale)
    {
        var pad = FooterPad * scale;
        return new Rect(new Vector2(footer.Min.X, footer.Max.Y - pad - Touch * scale),
            new Vector2(footer.Max.X, footer.Max.Y - pad));
    }
}
