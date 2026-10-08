using Aetherphone.Core;

namespace Aetherphone.Apps.Casino.Race;

internal readonly struct RaceLayout
{
    public const float HeaderHeight = 40f;
    public const float Gap = 8f;
    public const float SideShare = 0.38f;
    public const float SideMin = 250f;
    public const float SideMax = 330f;
    public const float TickerHeight = 30f;
    public const float TickerHeightStacked = 64f;
    public const float TicketsStripHeight = 74f;
    public const float RideHeight = 30f;

    public readonly bool Landscape;
    public readonly bool Racing;
    public readonly Rect Header;
    public readonly Rect Main;
    public readonly Rect Side;
    public readonly Rect Deck;
    public readonly Rect Ticker;

    private RaceLayout(bool landscape, bool racing, Rect header, Rect main, Rect side, Rect deck, Rect ticker)
    {
        Landscape = landscape;
        Racing = racing;
        Header = header;
        Main = main;
        Side = side;
        Deck = deck;
        Ticker = ticker;
    }

    public bool HasHeader => Header.Height > 0f;

    public bool HasSide => Side.Height > 0f && Side.Width > 0f;

    public bool HasDeck => Deck.Height > 0f;

    public static RaceLayout Compute(Rect safe, Rect stageDeck, bool landscape, bool racing, bool deck,
        float deckHeight, float scale)
    {
        var gap = Gap * scale;
        var empty = new Rect(safe.Min, safe.Min);
        var top = safe.Min.Y;
        var bottom = safe.Max.Y;
        if (racing)
        {
            var header = empty;
            if (landscape)
            {
                header = Band(safe.Min.X, safe.Max.X, top, top + HeaderHeight * scale, bottom);
                top = MathF.Min(bottom, header.Max.Y + gap * 0.5f);
            }

            var tickerTop = Math.Clamp(bottom - (landscape ? TickerHeight : TickerHeightStacked) * scale, top, bottom);
            var ticker = Band(safe.Min.X, safe.Max.X, tickerTop, bottom, bottom);
            var main = Band(safe.Min.X, safe.Max.X, top, tickerTop - gap, bottom);
            return new RaceLayout(landscape, true, header, main, empty, empty, ticker);
        }

        if (landscape)
        {
            var sideWidth = Math.Clamp(safe.Width * SideShare, SideMin * scale, SideMax * scale);
            sideWidth = MathF.Min(sideWidth, safe.Width * 0.5f);
            var sideLeft = safe.Max.X - sideWidth;
            var header = Band(sideLeft, safe.Max.X, top, top + HeaderHeight * scale, bottom);
            var deckTop = deck ? Math.Clamp(bottom - deckHeight * scale, header.Max.Y, bottom) : bottom;
            var deckRect = Band(sideLeft, safe.Max.X, deckTop, bottom, bottom);
            var side = Band(sideLeft, safe.Max.X, header.Max.Y + gap * 0.5f, deckTop - gap * 0.5f, bottom);
            var main = Band(safe.Min.X, MathF.Max(safe.Min.X, sideLeft - gap), top, bottom, bottom);
            return new RaceLayout(true, false, header, main, side, deckRect, empty);
        }

        var stripTop = Math.Clamp(bottom - TicketsStripHeight * scale, top, bottom);
        var strip = Band(safe.Min.X, safe.Max.X, stripTop, bottom, bottom);
        var board = Band(safe.Min.X, safe.Max.X, top, stripTop - gap, bottom);
        return new RaceLayout(false, false, empty, board, strip, deck ? stageDeck : empty, empty);
    }

    private static Rect Band(float left, float right, float top, float bottom, float limit)
    {
        var clampedTop = MathF.Min(top, limit);
        var clampedBottom = Math.Clamp(bottom, clampedTop, limit);
        return new Rect(new Vector2(left, clampedTop), new Vector2(MathF.Max(left, right), clampedBottom));
    }
}
