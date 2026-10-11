using Aetherphone.Core;

namespace Aetherphone.Apps.Casino.Tables;

internal static class HostFlowLayout
{
    public const float Touch = 44f;
    public const float SectionGap = 22f;
    public const float StepGap = 10f;
    public const float StepDisc = 26f;
    public const float CardPad = 14f;
    public const float CardGap = 10f;
    public const float TileGap = 12f;
    public const float TileMinWidth = 132f;
    public const float TileMaxWidth = 168f;
    public const float TileArt = 96f;
    public const float VisibleTiles = 2.2f;
    public const float RailPad = 4f;
    public const float ChoiceIcon = 40f;
    public const float CheckSize = 22f;
    public const float SeatDisc = 30f;
    public const float SeatGap = 6f;
    public const float JoinIcon = 30f;
    public const float LineGap = 4f;
    public const float FooterPad = 12f;

    public static float StepHeight(float scale, float titleHeight) => MathF.Max(StepDisc * scale, titleHeight);

    public static float TileWidth(float width, float scale) =>
        Math.Clamp((width - TileGap * scale) / VisibleTiles, TileMinWidth * scale, TileMaxWidth * scale);

    public static float TileHeight(float scale, float headlineHeight, float lineBlockHeight) =>
        TileArt * scale + CardPad * scale + headlineHeight + LineGap * scale + lineBlockHeight + CardPad * scale;

    public static float TileTextWidth(float tileWidth, float scale) => MathF.Max(1f, tileWidth - CardPad * 2f * scale);

    public static Rect Tile(float left, float top, int index, float tileWidth, float tileHeight, float scale)
    {
        var x = left + index * (tileWidth + TileGap * scale);
        return new Rect(new Vector2(x, top), new Vector2(x + tileWidth, top + tileHeight));
    }

    public static float RailWidth(int count, float tileWidth, float scale) =>
        count <= 0 ? 0f : count * tileWidth + (count - 1) * TileGap * scale;

    public static float ChoiceTextLeft(float left, float scale) => left + CardPad * scale + ChoiceIcon * scale + CardPad * scale;

    public static float ChoiceTextWidth(float width, float scale) =>
        MathF.Max(1f, width - CardPad * 3f * scale - ChoiceIcon * scale - CheckSize * scale - CardPad * scale);

    public static float ChoiceHeight(float scale, float titleHeight, float lineBlockHeight) =>
        MathF.Max(MathF.Max(Touch, ChoiceIcon + CardPad * 2f) * scale,
            CardPad * 2f * scale + titleHeight + LineGap * scale + lineBlockHeight);

    public static Rect ChoiceIconRect(in Rect card, float scale)
    {
        var size = ChoiceIcon * scale;
        var min = new Vector2(card.Min.X + CardPad * scale, card.Center.Y - size * 0.5f);
        return new Rect(min, min + new Vector2(size, size));
    }

    public static Vector2 ChoiceCheck(in Rect card, float scale) =>
        new(card.Max.X - CardPad * scale - CheckSize * 0.5f * scale, card.Center.Y);

    public static int SeatsPerRow(float width, float scale) =>
        Math.Max(1, (int)MathF.Floor((width + SeatGap * scale) / ((Touch + SeatGap) * scale)));

    public static int SeatRows(float width, int seats, float scale)
    {
        var perRow = SeatsPerRow(width, scale);
        return (seats + perRow - 1) / perRow;
    }

    public static Rect SeatTarget(float left, float top, float width, int index, int seats, float scale)
    {
        var perRow = Math.Min(seats, SeatsPerRow(width, scale));
        var row = index / perRow;
        var column = index % perRow;
        var target = Touch * scale;
        var stride = MathF.Min((width - target) / MathF.Max(1, perRow - 1), target * 1.6f);
        if (perRow == 1)
        {
            stride = 0f;
        }

        var x = left + column * stride;
        var y = top + row * (target + SeatGap * scale);
        return new Rect(new Vector2(x, y), new Vector2(x + target, y + target));
    }

    public static float SeatBlockHeight(float width, int seats, float scale)
    {
        var rows = SeatRows(width, seats, scale);
        return rows * Touch * scale + (rows - 1) * SeatGap * scale;
    }

    public static Rect JoinColumn(float left, float top, float width, int index, int count, float height, float scale)
    {
        var gap = CardGap * scale;
        var columnWidth = (width - gap * (count - 1)) / count;
        var x = left + index * (columnWidth + gap);
        return new Rect(new Vector2(x, top), new Vector2(x + columnWidth, top + height));
    }

    public static float JoinColumnWidth(float width, int count, float scale) =>
        (width - CardGap * scale * (count - 1)) / count;

    public static float JoinHeight(float scale, float titleBlockHeight) =>
        MathF.Max(Touch * scale, CardPad * 2f * scale + JoinIcon * scale + LineGap * scale + titleBlockHeight);

    public static float LadderHeight(float scale, float labelHeight, float valueHeight) =>
        labelHeight + valueHeight + LadderSlider.RowHeight * scale;

    public static Rect LadderRow(float left, float top, float width, float scale, float labelHeight,
        float valueHeight)
    {
        var y = top + labelHeight + valueHeight;
        return new Rect(new Vector2(left, y), new Vector2(left + width, y + LadderSlider.RowHeight * scale));
    }

    public static float FooterHeight(float scale, float summaryBlockHeight) =>
        FooterPad * 2f * scale + summaryBlockHeight + StepGap * scale + Touch * scale;

    public static Rect FooterButton(in Rect footer, float scale)
    {
        var pad = FooterPad * scale;
        return new Rect(new Vector2(footer.Min.X, footer.Max.Y - pad - Touch * scale),
            new Vector2(footer.Max.X, footer.Max.Y - pad));
    }
}
