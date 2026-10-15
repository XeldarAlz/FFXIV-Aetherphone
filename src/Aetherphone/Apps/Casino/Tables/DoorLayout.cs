using Aetherphone.Core;
using Aetherphone.Core.Casino;

namespace Aetherphone.Apps.Casino.Tables;

internal readonly record struct DoorCodeRow(float TileWidth, float TileHeight, float TileGap, Rect Copy)
{
    public Rect Tile(float left, float top, int index)
    {
        var x = left + index * (TileWidth + TileGap);
        return new Rect(new Vector2(x, top), new Vector2(x + TileWidth, top + TileHeight));
    }
}

internal readonly record struct KnockButtons(Rect Decline, Rect Approve);

internal static class DoorLayout
{
    public const float Touch = 44f;
    public const float Pad = 14f;
    public const float Avatar = 36f;
    public const float TextGap = 12f;
    public const float ButtonGap = 8f;
    public const float RowGap = 8f;
    public const float LedgerColumnGap = 10f;
    public const float LedgerNameShare = 0.34f;
    public const float StatusTile = 48f;
    public const float CodeTileMax = 36f;
    public const float CodeTileAspect = 1.22f;
    public const float CodeTileGap = 6f;
    public const float KnockPadX = 12f;
    public const float KnockPadY = 11f;
    public const float KnockPill = 34f;
    public const float KnockTextMin = 96f;
    public const float MemberPadY = 10f;
    public const float MenuButton = 36f;
    public const float ManageIcon = 38f;
    public const float ManagePadY = 12f;
    public const float ManageLineGap = 2f;

    public static DoorCodeRow CodeRow(float left, float top, float inner, float copyWidth, float scale)
    {
        var count = CasinoRoomCodes.Length;
        var gap = CodeTileGap * scale;
        var copyGap = ButtonGap * scale;
        var room = inner - copyWidth - copyGap - gap * (count - 1);
        var tileWidth = MathF.Max(1f, MathF.Min(CodeTileMax * scale, room / count));
        var tileHeight = MathF.Min(Touch * scale, tileWidth * CodeTileAspect);
        var rowHeight = MathF.Max(tileHeight, Touch * scale);
        var copy = new Rect(new Vector2(left + inner - copyWidth, top + (rowHeight - Touch * scale) * 0.5f),
            new Vector2(left + inner, top + (rowHeight + Touch * scale) * 0.5f));
        return new DoorCodeRow(tileWidth, tileHeight, gap, copy);
    }

    public static float CodeRowHeight(in DoorCodeRow row, float scale) => MathF.Max(row.TileHeight, Touch * scale);

    public static float PrimaryShare(float inner, float scale) => (inner - ButtonGap * scale) * 1.4f / 2.4f;

    public static bool KnockInline(float cardWidth, float declineWidth, float approveWidth, float scale)
    {
        var text = cardWidth - KnockPadX * 2f * scale - Avatar * scale - TextGap * scale * 2f - declineWidth
            - approveWidth - ButtonGap * scale;
        return text >= KnockTextMin * scale;
    }

    public static float KnockHeight(float scale, float nameHeight, float lineHeight, bool inline)
    {
        var block = MathF.Max(Avatar * scale, nameHeight + lineHeight);
        return inline
            ? KnockPadY * 2f * scale + MathF.Max(block, KnockPill * scale)
            : KnockPadY * 2f * scale + block + ButtonGap * scale + KnockPill * scale;
    }

    public static Vector2 KnockAvatar(in Rect card, float nameHeight, float lineHeight, bool inline, float scale)
    {
        var x = card.Min.X + KnockPadX * scale + Avatar * 0.5f * scale;
        if (inline)
        {
            return new Vector2(x, card.Center.Y);
        }

        var block = MathF.Max(Avatar * scale, nameHeight + lineHeight);
        return new Vector2(x, card.Min.Y + KnockPadY * scale + block * 0.5f);
    }

    public static float KnockTextLeft(in Rect card, float scale) =>
        card.Min.X + KnockPadX * scale + Avatar * scale + TextGap * scale;

    public static KnockButtons KnockButtonRects(in Rect card, float declineWidth, float approveWidth, bool inline,
        float scale)
    {
        var height = KnockPill * scale;
        var gap = ButtonGap * scale;
        var right = card.Max.X - KnockPadX * scale;
        if (inline)
        {
            var top = card.Center.Y - height * 0.5f;
            var approve = new Rect(new Vector2(right - approveWidth, top), new Vector2(right, top + height));
            var decline = new Rect(new Vector2(approve.Min.X - gap - declineWidth, top),
                new Vector2(approve.Min.X - gap, top + height));
            return new KnockButtons(decline, approve);
        }

        var left = card.Min.X + KnockPadX * scale;
        var bottom = card.Max.Y - KnockPadY * scale;
        var half = (right - left - gap) * 0.5f;
        return new KnockButtons(new Rect(new Vector2(left, bottom - height), new Vector2(left + half, bottom)),
            new Rect(new Vector2(right - half, bottom - height), new Vector2(right, bottom)));
    }

    public static float PlayerHeight(float scale, float nameHeight, float lineHeight) =>
        MathF.Max(MathF.Max(Touch, Avatar) * scale + Pad * scale, nameHeight + lineHeight + Pad * 2f * scale);

    public static float MemberHeight(float scale, float nameHeight, float lineHeight) =>
        MathF.Max(MathF.Max(Touch, Avatar) * scale, nameHeight + lineHeight) + MemberPadY * 2f * scale;

    public static Vector2 AvatarCenter(in Rect row, float scale) =>
        new(row.Min.X + Pad * scale + Avatar * 0.5f * scale, row.Center.Y);

    public static float TextLeft(in Rect row, float scale) => row.Min.X + Pad * scale + Avatar * scale + TextGap * scale;

    public static Rect TrailingButton(in Rect row, float buttonWidth, float scale)
    {
        var touch = Touch * scale;
        var max = new Vector2(row.Max.X - Pad * scale, row.Center.Y + touch * 0.5f);
        return new Rect(new Vector2(max.X - buttonWidth, max.Y - touch), max);
    }

    public static Vector2 MenuCenter(in Rect row, float scale) =>
        new(row.Max.X - Pad * scale - MenuButton * 0.5f * scale, row.Center.Y);

    public static float MemberTextRight(in Rect row, bool hasMenu, float scale) =>
        hasMenu ? row.Max.X - Pad * scale - MenuButton * scale - TextGap * scale : row.Max.X - Pad * scale;

    public static float ManageTextLeft(float left, float scale) =>
        left + Pad * scale + ManageIcon * scale + TextGap * scale;

    public static float ManageHeight(float scale, float titleHeight, float lineHeight) =>
        MathF.Max(ManageIcon * scale, titleHeight + ManageLineGap * scale + lineHeight) + ManagePadY * 2f * scale;

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
