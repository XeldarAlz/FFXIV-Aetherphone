using Aetherphone.Core;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Casino;

internal readonly record struct CashierBlocks(
    float Notice,
    float Caption,
    float Amount,
    float Sub,
    float Rate,
    float Heading,
    float Field,
    float Status,
    float Note,
    float Error,
    float AutoText,
    float LimitsText);

internal readonly record struct CashierBalancesLayout(Rect Card, Rect WalletColumn, Rect ChipsColumn, Rect Exchange,
    Rect RateRow);

internal readonly record struct CashierAmountLayout(Rect Card, Rect Heading, Rect Field, Rect TileRow, int TileCount,
    float TileGap, Rect Status, Rect Note, Rect Error, Rect Primary)
{
    public bool HasTiles => TileCount > 0 && TileRow.Height > 0f;

    public bool HasNote => Note.Height > 0f;

    public bool HasError => Error.Height > 0f;

    public Rect Tile(int index)
    {
        var count = Math.Max(1, TileCount);
        var width = MathF.Max(0f, (TileRow.Width - TileGap * (count - 1)) / count);
        var left = TileRow.Min.X + index * (width + TileGap);
        return new Rect(new Vector2(left, TileRow.Min.Y), new Vector2(left + width, TileRow.Max.Y));
    }
}

internal readonly record struct CashierRowLayout(Rect Row, Vector2 IconCenter, Rect Text, Rect Trailing);

internal readonly record struct CashierExtrasLayout(Rect Card, CashierRowLayout Auto, CashierRowLayout Limits,
    float Divider);

internal readonly struct CashierLayout
{
    public const float CardPad = 14f;
    public const float BalancesTop = 16f;
    public const float BalancesBottom = 12f;
    public const float Disc = 46f;
    public const float ColumnGap = 4f;
    public const float Exchange = 38f;
    public const float RateGap = 12f;
    public const float RatePadX = 10f;
    public const float RatePadY = 5f;
    public const float SectionGap = 14f;
    public const float SegmentHeight = 48f;
    public const float PanelGap = 12f;
    public const float HeadingGap = 8f;
    public const float FieldHeight = 58f;
    public const float FieldPad = 8f;
    public const float TileHeight = Button.LargeHeight;
    public const float TileGap = 6f;
    public const float PrimaryHeight = 52f;
    public const float ErrorPad = 12f;
    public const float RowPad = 12f;
    public const float Icon = 38f;
    public const float RowGap = 12f;
    public const float Chevron = 16f;

    public readonly Rect Notice;
    public readonly CashierBalancesLayout Balances;
    public readonly Rect Segment;
    public readonly CashierAmountLayout Amount;
    public readonly CashierExtrasLayout Extras;
    public readonly float Bottom;

    private CashierLayout(Rect notice, in CashierBalancesLayout balances, Rect segment, in CashierAmountLayout amount,
        in CashierExtrasLayout extras, float bottom)
    {
        Notice = notice;
        Balances = balances;
        Segment = segment;
        Amount = amount;
        Extras = extras;
        Bottom = bottom;
    }

    public bool HasNotice => Notice.Height > 0f;

    public bool HasSegment => Segment.Height > 0f;

    public static float PanelTextWidth(float width, float scale) => MathF.Max(1f, width - CardPad * 2f * scale);

    public static float ErrorTextWidth(float width, float scale) =>
        MathF.Max(1f, PanelTextWidth(width, scale) - ErrorPad * 2f * scale);

    public static float AutoTextWidth(float width, float scale) =>
        RowTextWidth(width, Metrics.Size.ToggleWidth * scale, scale);

    public static float LimitsTextWidth(float width, float scale) => RowTextWidth(width, Chevron * scale, scale);

    public static float FieldHeightFor(float numberLine, float scale) =>
        MathF.Max(FieldHeight * scale, numberLine + FieldPad * 2f * scale);

    public static CashierLayout Compute(float left, float top, float width, in CashierBlocks blocks, bool segment,
        int tileCount, float scale)
    {
        var right = left + width;
        var sectionGap = SectionGap * scale;
        var y = top;
        var notice = Empty(left, y);
        if (blocks.Notice > 0f)
        {
            notice = new Rect(new Vector2(left, y), new Vector2(right, y + blocks.Notice));
            y = notice.Max.Y + sectionGap;
        }

        var balances = ComputeBalances(left, y, width, blocks, scale);
        y = balances.Card.Max.Y;
        var segmentRect = Empty(left, y);
        if (segment)
        {
            segmentRect = new Rect(new Vector2(left, y + sectionGap),
                new Vector2(right, y + sectionGap + SegmentHeight * scale));
            y = segmentRect.Max.Y;
        }

        var amount = ComputeAmount(left, y + sectionGap, width, blocks, tileCount, scale);
        var extras = ComputeExtras(left, amount.Card.Max.Y + sectionGap, width, blocks, scale);
        return new CashierLayout(notice, balances, segmentRect, amount, extras, extras.Card.Max.Y);
    }

    private static CashierBalancesLayout ComputeBalances(float left, float top, float width, in CashierBlocks blocks,
        float scale)
    {
        var right = left + width;
        var pad = CardPad * scale;
        var gap = ColumnGap * scale;
        var exchange = Exchange * scale;
        var columnHeight = Disc * scale + blocks.Caption + blocks.Amount + blocks.Sub + gap * 3f;
        var columnTop = top + BalancesTop * scale;
        var columnWidth = MathF.Max(0f, (width - pad * 2f - exchange - RowGap * 2f * scale) * 0.5f);
        var walletColumn = new Rect(new Vector2(left + pad, columnTop),
            new Vector2(left + pad + columnWidth, columnTop + columnHeight));
        var chipsColumn = new Rect(new Vector2(right - pad - columnWidth, columnTop),
            new Vector2(right - pad, columnTop + columnHeight));
        var exchangeCenter = new Vector2(left + width * 0.5f, columnTop + columnHeight * 0.5f);
        var half = new Vector2(exchange * 0.5f, exchange * 0.5f);
        var rateTop = walletColumn.Max.Y + RateGap * scale;
        var rateRow = new Rect(new Vector2(left + pad, rateTop),
            new Vector2(right - pad, rateTop + blocks.Rate + RatePadY * 2f * scale));
        var card = new Rect(new Vector2(left, top), new Vector2(right, rateRow.Max.Y + BalancesBottom * scale));
        return new CashierBalancesLayout(card, walletColumn, chipsColumn,
            new Rect(exchangeCenter - half, exchangeCenter + half), rateRow);
    }

    private static CashierAmountLayout ComputeAmount(float left, float top, float width, in CashierBlocks blocks,
        int tileCount, float scale)
    {
        var pad = CardPad * scale;
        var gap = PanelGap * scale;
        var innerLeft = left + pad;
        var innerRight = left + width - pad;
        var heading = new Rect(new Vector2(innerLeft, top + pad), new Vector2(innerRight, top + pad + blocks.Heading));
        var fieldTop = heading.Max.Y + HeadingGap * scale;
        var field = new Rect(new Vector2(innerLeft, fieldTop), new Vector2(innerRight, fieldTop + blocks.Field));
        var y = field.Max.Y;
        var tiles = Math.Max(0, tileCount);
        var tileRow = Empty(innerLeft, y);
        if (tiles > 0)
        {
            tileRow = new Rect(new Vector2(innerLeft, y + gap), new Vector2(innerRight, y + gap + TileHeight * scale));
            y = tileRow.Max.Y;
        }

        var status = new Rect(new Vector2(innerLeft, y + gap), new Vector2(innerRight, y + gap + blocks.Status));
        y = status.Max.Y;
        var note = Empty(innerLeft, y);
        if (blocks.Note > 0f)
        {
            note = new Rect(new Vector2(innerLeft, y + gap), new Vector2(innerRight, y + gap + blocks.Note));
            y = note.Max.Y;
        }

        var error = Empty(innerLeft, y);
        if (blocks.Error > 0f)
        {
            error = new Rect(new Vector2(innerLeft, y + gap),
                new Vector2(innerRight, y + gap + blocks.Error + ErrorPad * 2f * scale));
            y = error.Max.Y;
        }

        var primary = new Rect(new Vector2(innerLeft, y + gap), new Vector2(innerRight, y + gap + PrimaryHeight * scale));
        var card = new Rect(new Vector2(left, top), new Vector2(left + width, primary.Max.Y + pad));
        return new CashierAmountLayout(card, heading, field, tileRow, tiles, TileGap * scale, status, note, error,
            primary);
    }

    private static CashierExtrasLayout ComputeExtras(float left, float top, float width, in CashierBlocks blocks,
        float scale)
    {
        var auto = ComputeRow(left, top, width, blocks.AutoText,
            new Vector2(Metrics.Size.ToggleWidth, Metrics.Size.ToggleHeight) * scale, scale);
        var limits = ComputeRow(left, auto.Row.Max.Y, width, blocks.LimitsText,
            new Vector2(Chevron, Chevron) * scale, scale);
        var card = new Rect(new Vector2(left, top), new Vector2(left + width, limits.Row.Max.Y));
        return new CashierExtrasLayout(card, auto, limits, auto.Row.Max.Y);
    }

    private static CashierRowLayout ComputeRow(float left, float top, float width, float textHeight, Vector2 trailing,
        float scale)
    {
        var pad = CardPad * scale;
        var icon = Icon * scale;
        var rowGap = RowGap * scale;
        var content = MathF.Max(MathF.Max(icon, textHeight), trailing.Y);
        var row = new Rect(new Vector2(left, top), new Vector2(left + width, top + content + RowPad * 2f * scale));
        var centerY = row.Center.Y;
        var iconCenter = new Vector2(left + pad + icon * 0.5f, centerY);
        var trailingRect = new Rect(new Vector2(row.Max.X - pad - trailing.X, centerY - trailing.Y * 0.5f),
            new Vector2(row.Max.X - pad, centerY + trailing.Y * 0.5f));
        var textLeft = left + pad + icon + rowGap;
        var text = new Rect(new Vector2(textLeft, centerY - textHeight * 0.5f),
            new Vector2(MathF.Max(textLeft, trailingRect.Min.X - rowGap), centerY + textHeight * 0.5f));
        return new CashierRowLayout(row, iconCenter, text, trailingRect);
    }

    private static float RowTextWidth(float width, float trailing, float scale) =>
        MathF.Max(1f, width - CardPad * 2f * scale - Icon * scale - RowGap * 2f * scale - trailing);

    private static Rect Empty(float left, float y) => new(new Vector2(left, y), new Vector2(left, y));
}
