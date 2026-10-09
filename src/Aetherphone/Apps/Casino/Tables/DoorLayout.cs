using Aetherphone.Core;

namespace Aetherphone.Apps.Casino.Tables;

internal static class DoorLayout
{
    public const float Touch = 44f;
    public const float ControlButton = 52f;
    public const float ControlMinCell = 68f;
    public const float ControlGap = 8f;
    public const float LabelGap = 6f;
    public const float Pad = 14f;
    public const float Avatar = 36f;
    public const float TextGap = 12f;
    public const float ButtonGap = 8f;
    public const float RowGap = 8f;
    public const float LedgerColumnGap = 10f;
    public const float LedgerNameShare = 0.34f;

    public static int ControlColumns(float width, int count, float scale)
    {
        var fits = (int)MathF.Floor((width + ControlGap * scale) / ((ControlMinCell + ControlGap) * scale));
        return Math.Clamp(fits, 1, Math.Max(1, count));
    }

    public static float ControlCellWidth(float width, int columns, float scale) =>
        (width - ControlGap * scale * (columns - 1)) / columns;

    public static float ControlCellHeight(float scale, float labelBlock) =>
        ControlButton * scale + LabelGap * scale + labelBlock;

    public static Rect ControlCell(float left, float top, float width, int index, int count, float scale,
        float labelBlock)
    {
        var columns = ControlColumns(width, count, scale);
        var cellWidth = ControlCellWidth(width, columns, scale);
        var cellHeight = ControlCellHeight(scale, labelBlock);
        var row = index / columns;
        var column = index % columns;
        var x = left + column * (cellWidth + ControlGap * scale);
        var y = top + row * (cellHeight + ControlGap * scale);
        return new Rect(new Vector2(x, y), new Vector2(x + cellWidth, y + cellHeight));
    }

    public static float ControlGridHeight(float width, int count, float scale, float labelBlock)
    {
        var columns = ControlColumns(width, count, scale);
        var rows = (count + columns - 1) / columns;
        return rows * ControlCellHeight(scale, labelBlock) + (rows - 1) * ControlGap * scale;
    }

    public static Vector2 ControlCenter(in Rect cell, float scale) =>
        new(cell.Center.X, cell.Min.Y + ControlButton * 0.5f * scale);

    public static float PlayerHeight(float scale, float nameHeight, float lineHeight) =>
        MathF.Max(MathF.Max(Touch, Avatar) * scale + Pad * scale, nameHeight + lineHeight + Pad * 2f * scale);

    public static Vector2 AvatarCenter(in Rect row, float scale) =>
        new(row.Min.X + Pad * scale + Avatar * 0.5f * scale, row.Center.Y);

    public static float TextLeft(in Rect row, float scale) => row.Min.X + Pad * scale + Avatar * scale + TextGap * scale;

    public static Rect TrailingButton(in Rect row, float buttonWidth, float scale)
    {
        var touch = Touch * scale;
        var max = new Vector2(row.Max.X - Pad * scale, row.Center.Y + touch * 0.5f);
        return new Rect(new Vector2(max.X - buttonWidth, max.Y - touch), max);
    }

    public static float KnockHeight(float scale, float nameHeight, float lineHeight) =>
        Pad * 2f * scale + MathF.Max(Avatar * scale, nameHeight + lineHeight) + ButtonGap * 1.5f * scale + Touch * scale;

    public static Rect KnockButton(in Rect card, int index, float scale)
    {
        var pad = Pad * scale;
        var gap = ButtonGap * scale;
        var width = (card.Width - pad * 2f - gap) * 0.5f;
        var x = card.Min.X + pad + index * (width + gap);
        var bottom = card.Max.Y - pad;
        return new Rect(new Vector2(x, bottom - Touch * scale), new Vector2(x + width, bottom));
    }

    public static Vector2 KnockAvatar(in Rect card, float scale) =>
        new(card.Min.X + Pad * scale + Avatar * 0.5f * scale, card.Min.Y + Pad * scale + Avatar * 0.5f * scale);

    public static float LedgerNumberWidth(float width, float scale) =>
        (width - Pad * 2f * scale - width * LedgerNameShare - LedgerColumnGap * 3f * scale) / 3f;

    public static float LedgerColumnRight(float left, float width, int column, float scale)
    {
        var right = left + width - Pad * scale;
        var number = LedgerNumberWidth(width, scale);
        return right - (2 - column) * (number + LedgerColumnGap * scale);
    }

    public static float LedgerNameRight(float left, float width, float scale) =>
        LedgerColumnRight(left, width, 0, scale) - LedgerNumberWidth(width, scale) - LedgerColumnGap * scale;
}
