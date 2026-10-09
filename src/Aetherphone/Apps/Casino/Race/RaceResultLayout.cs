using Aetherphone.Core;

namespace Aetherphone.Apps.Casino.Race;

internal readonly struct RaceResultLayout
{
    public const float MaxCardWidth = 520f;
    public const float Pad = 16f;
    public const float Gap = 10f;
    public const float HeadlineHeight = 44f;
    public const float MetaHeight = 22f;
    public const float WinnerHeight = 64f;
    public const float PlaceHeight = 40f;
    public const float SectionHeight = 28f;
    public const float RowHeight = 32f;
    public const float ToggleHeight = 44f;
    public const float NextHeight = 28f;
    public const float AccentHeight = 2f;

    public readonly Rect Card;
    public readonly float ContentWidth;
    public readonly float ContentHeight;
    public readonly Rect Headline;
    public readonly Rect Meta;
    public readonly Rect Winner;
    public readonly Rect Second;
    public readonly Rect Third;
    public readonly Rect PaidHeader;
    public readonly float PaidTop;
    public readonly Rect Toggle;
    public readonly float DividendTop;
    public readonly Rect Next;

    private RaceResultLayout(Rect card, float contentWidth, float contentHeight, Rect headline, Rect meta, Rect winner,
        Rect second, Rect third, Rect paidHeader, float paidTop, Rect toggle, float dividendTop, Rect next)
    {
        Card = card;
        ContentWidth = contentWidth;
        ContentHeight = contentHeight;
        Headline = headline;
        Meta = meta;
        Winner = winner;
        Second = second;
        Third = third;
        PaidHeader = paidHeader;
        PaidTop = paidTop;
        Toggle = toggle;
        DividendTop = dividendTop;
        Next = next;
    }

    public bool HasPaid => PaidHeader.Height > 0f;

    public Rect PaidRow(int index, float scale) => Row(PaidTop, index, scale);

    public Rect DividendRow(int index, float scale) => Row(DividendTop, index, scale);

    public static RaceResultLayout Compute(Rect safe, int paidRows, int dividendRows, bool expanded, float scale)
    {
        var pad = Pad * scale;
        var gap = Gap * scale;
        var cardWidth = MathF.Min(safe.Width, MaxCardWidth * scale);
        var width = MathF.Max(1f, cardWidth - pad * 2f);
        var top = 0f;
        var headline = Band(width, ref top, HeadlineHeight * scale, 0f);
        var meta = Band(width, ref top, MetaHeight * scale, gap);
        var winner = Band(width, ref top, WinnerHeight * scale, 0f);
        var second = Band(width, ref top, PlaceHeight * scale, 0f);
        var third = Band(width, ref top, PlaceHeight * scale, gap);
        var paidHeader = new Rect(new Vector2(0f, top), new Vector2(width, top));
        var paidTop = top;
        if (paidRows > 0)
        {
            paidHeader = Band(width, ref top, SectionHeight * scale, 0f);
            paidTop = top;
            top += paidRows * RowHeight * scale + gap;
        }

        var toggle = new Rect(new Vector2(0f, top), new Vector2(width, top));
        var dividendTop = top;
        if (dividendRows > 0)
        {
            toggle = Band(width, ref top, ToggleHeight * scale, 0f);
            dividendTop = top;
            top += (expanded ? dividendRows * RowHeight * scale : 0f) + gap;
        }

        var next = Band(width, ref top, NextHeight * scale, 0f);
        var contentHeight = top;
        var cardHeight = MathF.Min(safe.Height, contentHeight + pad * 2f + AccentHeight * scale);
        var left = safe.Center.X - cardWidth * 0.5f;
        var card = new Rect(new Vector2(left, safe.Min.Y), new Vector2(left + cardWidth, safe.Min.Y + cardHeight));
        return new RaceResultLayout(card, width, contentHeight, headline, meta, winner, second, third, paidHeader,
            paidTop, toggle, dividendTop, next);
    }

    public Rect Viewport(float scale)
    {
        var pad = Pad * scale;
        var top = Card.Min.Y + pad + AccentHeight * scale;
        return new Rect(new Vector2(Card.Min.X + pad, top),
            new Vector2(Card.Max.X - pad, MathF.Max(top, Card.Max.Y - pad)));
    }

    private Rect Row(float top, int index, float scale)
    {
        var rowTop = top + index * RowHeight * scale;
        return new Rect(new Vector2(0f, rowTop), new Vector2(ContentWidth, rowTop + RowHeight * scale));
    }

    private static Rect Band(float width, ref float top, float height, float gapAfter)
    {
        var band = new Rect(new Vector2(0f, top), new Vector2(width, top + height));
        top = band.Max.Y + gapAfter;
        return band;
    }
}
