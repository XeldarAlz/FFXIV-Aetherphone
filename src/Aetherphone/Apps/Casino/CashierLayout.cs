using Aetherphone.Core;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Casino;

internal readonly record struct CashierBlocks(
    float Caption,
    float Amount,
    float Worth,
    float Rate,
    float Heading,
    float Status,
    float CashLine,
    float Notice,
    float Note);

internal readonly struct CashierLayout
{
    public const float Pad = 16f;
    public const float Disc = 36f;
    public const float DiscGap = 6f;
    public const float Arrow = 28f;
    public const float Gap = 8f;
    public const float SectionGap = 16f;
    public const float FieldHeight = 44f;
    public const float ControlHeight = Button.LargeHeight;
    public const float NotePad = 12f;

    public readonly Rect Notice;
    public readonly Rect Balances;
    public readonly Rect WalletColumn;
    public readonly Rect ChipsColumn;
    public readonly Rect ArrowSlot;
    public readonly Rect RateLine;
    public readonly Rect Note;
    public readonly Rect BuyHeading;
    public readonly Rect Field;
    public readonly Rect TileRow;
    public readonly Rect Status;
    public readonly Rect Buy;
    public readonly float Divider;
    public readonly Rect CashLine;
    public readonly Rect CashOut;
    public readonly float Bottom;
    public readonly int TileCount;
    public readonly float TileGap;

    private CashierLayout(Rect notice, Rect balances, Rect walletColumn, Rect chipsColumn, Rect arrowSlot,
        Rect rateLine, Rect note, Rect buyHeading, Rect field, Rect tileRow, Rect status, Rect buy, float divider,
        Rect cashLine, Rect cashOut, float bottom, int tileCount, float tileGap)
    {
        TileGap = tileGap;
        Notice = notice;
        Balances = balances;
        WalletColumn = walletColumn;
        ChipsColumn = chipsColumn;
        ArrowSlot = arrowSlot;
        RateLine = rateLine;
        Note = note;
        BuyHeading = buyHeading;
        Field = field;
        TileRow = tileRow;
        Status = status;
        Buy = buy;
        Divider = divider;
        CashLine = cashLine;
        CashOut = cashOut;
        Bottom = bottom;
        TileCount = tileCount;
    }

    public bool HasNotice => Notice.Height > 0f;

    public bool HasNote => Note.Height > 0f;

    public bool HasBuy => Buy.Height > 0f;

    public bool HasTiles => TileCount > 0 && TileRow.Height > 0f;

    public bool HasCashOut => CashOut.Height > 0f;

    public Rect Tile(int index)
    {
        var count = Math.Max(1, TileCount);
        var width = MathF.Max(0f, (TileRow.Width - TileGap * (count - 1)) / count);
        var left = TileRow.Min.X + index * (width + TileGap);
        return new Rect(new Vector2(left, TileRow.Min.Y), new Vector2(left + width, TileRow.Max.Y));
    }

    public static CashierLayout Compute(float left, float top, float width, in CashierBlocks blocks, bool buy,
        bool cashOut, int tileCount, float scale)
    {
        var right = left + width;
        var pad = Pad * scale;
        var gap = Gap * scale;
        var sectionGap = SectionGap * scale;
        var y = top;
        var notice = Empty(left, y);
        if (blocks.Notice > 0f)
        {
            notice = new Rect(new Vector2(left, y), new Vector2(right, y + blocks.Notice));
            y = notice.Max.Y + sectionGap;
        }

        var disc = Disc * scale;
        var columnHeight = disc + DiscGap * scale + blocks.Caption + blocks.Amount + blocks.Worth;
        var balancesHeight = pad * 2f + columnHeight + gap + blocks.Rate;
        var balances = new Rect(new Vector2(left, y), new Vector2(right, y + balancesHeight));
        var arrow = Arrow * scale;
        var columnWidth = MathF.Max(0f, (width - pad * 2f - arrow - gap * 2f) * 0.5f);
        var columnTop = balances.Min.Y + pad;
        var walletColumn = new Rect(new Vector2(left + pad, columnTop),
            new Vector2(left + pad + columnWidth, columnTop + columnHeight));
        var chipsColumn = new Rect(new Vector2(right - pad - columnWidth, columnTop),
            new Vector2(right - pad, columnTop + columnHeight));
        var arrowCenter = new Vector2(left + width * 0.5f, columnTop + disc * 0.5f);
        var arrowSlot = new Rect(arrowCenter - new Vector2(arrow * 0.5f, arrow * 0.5f),
            arrowCenter + new Vector2(arrow * 0.5f, arrow * 0.5f));
        var rateTop = walletColumn.Max.Y + gap;
        var rateLine = new Rect(new Vector2(left + pad, rateTop), new Vector2(right - pad, rateTop + blocks.Rate));
        y = balances.Max.Y;

        var note = Empty(left, y);
        if (blocks.Note > 0f)
        {
            var noteTop = y + gap;
            note = new Rect(new Vector2(left, noteTop),
                new Vector2(right, noteTop + blocks.Note + NotePad * 2f * scale));
            y = note.Max.Y;
        }

        var control = ControlHeight * scale;
        var buyHeading = Empty(left, y);
        var field = Empty(left, y);
        var tileRow = Empty(left, y);
        var status = Empty(left, y);
        var buyButton = Empty(left, y);
        var tiles = buy ? Math.Max(0, tileCount) : 0;
        if (buy)
        {
            var headingTop = y + sectionGap;
            buyHeading = new Rect(new Vector2(left, headingTop), new Vector2(right, headingTop + blocks.Heading));
            var fieldTop = buyHeading.Max.Y + gap;
            field = new Rect(new Vector2(left, fieldTop), new Vector2(right, fieldTop + FieldHeight * scale));
            y = field.Max.Y;
            if (tiles > 0)
            {
                tileRow = new Rect(new Vector2(left, y + gap), new Vector2(right, y + gap + control));
                y = tileRow.Max.Y;
            }

            status = new Rect(new Vector2(left, y + gap), new Vector2(right, y + gap + blocks.Status));
            buyButton = new Rect(new Vector2(left, status.Max.Y + gap),
                new Vector2(right, status.Max.Y + gap + control));
            y = buyButton.Max.Y;
        }

        var divider = y;
        var cashLine = Empty(left, y);
        var cashButton = Empty(left, y);
        if (cashOut)
        {
            divider = y + sectionGap;
            var lineTop = divider + sectionGap;
            var lineHeight = MathF.Max(blocks.CashLine, blocks.Amount);
            cashLine = new Rect(new Vector2(left, lineTop), new Vector2(right, lineTop + lineHeight));
            cashButton = new Rect(new Vector2(left, cashLine.Max.Y + gap),
                new Vector2(right, cashLine.Max.Y + gap + control));
            y = cashButton.Max.Y;
        }

        return new CashierLayout(notice, balances, walletColumn, chipsColumn, arrowSlot, rateLine, note, buyHeading,
            field, tileRow, status, buyButton, divider, cashLine, cashButton, y, tiles, gap);
    }

    private static Rect Empty(float left, float y) => new(new Vector2(left, y), new Vector2(left, y));
}
