using Aetherphone.Core;
using Aetherphone.Core.Casino;

namespace Aetherphone.Apps.Casino.Race;

internal readonly struct RaceOpenLayout
{
    public const float Gap = 8f;
    public const float CardHeight = 60f;
    public const float CardGap = 6f;
    public const float StripHeader = 40f;
    public const float StripRow = 34f;
    public const float RailShare = 0.42f;
    public const float RailMin = 280f;
    public const float RailMax = 360f;
    public const float RailInset = 8f;

    public readonly bool Landscape;
    public readonly Rect List;
    public readonly Rect Notice;
    public readonly Rect Tickets;
    public readonly Rect Deck;
    public readonly Rect Rail;
    public readonly int TicketRows;

    private RaceOpenLayout(bool landscape, Rect list, Rect notice, Rect tickets, Rect deck, Rect rail, int ticketRows)
    {
        Landscape = landscape;
        List = list;
        Notice = notice;
        Tickets = tickets;
        Deck = deck;
        Rail = rail;
        TicketRows = ticketRows;
    }

    public bool HasNotice => Notice.Height > 0f;

    public bool HasTickets => Tickets.Height > 0f;

    public bool HasRail => Rail.Width > 0f;

    public static float ColumnWidth(Rect safe, bool landscape, float scale) =>
        landscape ? RailWidth(safe, scale) : safe.Width;

    public static RaceOpenLayout Compute(Rect safe, Rect stageDeck, bool landscape, int ticketCount, bool expanded,
        float noticeHeight, float scale)
    {
        if (landscape)
        {
            return Landscaped(safe, ticketCount, expanded, noticeHeight, scale);
        }

        var column = new Rect(safe.Min, safe.Max);
        var bottom = Stack(column, safe.Max.Y, (CardHeight + Gap) * scale, ticketCount, expanded, noticeHeight, scale,
            out var tickets, out var notice, out var rows);
        var list = Band(column.Min.X, column.Max.X, column.Min.Y, bottom, column.Max.Y);
        return new RaceOpenLayout(false, list, notice, tickets, stageDeck, Empty(safe), rows);
    }

    private static RaceOpenLayout Landscaped(Rect safe, int ticketCount, bool expanded, float noticeHeight,
        float scale)
    {
        var gap = Gap * scale;
        var railWidth = RailWidth(safe, scale);
        var railLeft = safe.Max.X - railWidth;
        var rail = new Rect(new Vector2(railLeft, safe.Min.Y), safe.Max);
        var deckTop = MathF.Max(safe.Min.Y, safe.Max.Y - RaceDeckLayout.Height * scale);
        var deck = new Rect(new Vector2(railLeft, deckTop), safe.Max);
        var column = new Rect(new Vector2(railLeft, safe.Min.Y), new Vector2(safe.Max.X, deckTop));
        Stack(column, MathF.Max(safe.Min.Y, deckTop - gap), 0f, ticketCount, expanded, noticeHeight, scale,
            out var tickets, out var notice, out var rows);
        var list = Band(safe.Min.X, MathF.Max(safe.Min.X, railLeft - gap - RailInset * scale), safe.Min.Y, safe.Max.Y,
            safe.Max.Y);
        return new RaceOpenLayout(true, list, notice, tickets, deck, rail, rows);
    }

    private static float Stack(Rect column, float bottom, float keep, int ticketCount, bool expanded,
        float noticeHeight, float scale, out Rect tickets, out Rect notice, out int rows)
    {
        var gap = Gap * scale;
        var top = column.Min.Y;
        var available = MathF.Max(0f, bottom - top - keep);
        tickets = new Rect(new Vector2(column.Min.X, bottom), new Vector2(column.Max.X, bottom));
        notice = tickets;
        rows = 0;
        if (noticeHeight > 0f && available >= noticeHeight)
        {
            notice = new Rect(new Vector2(column.Min.X, bottom - noticeHeight), new Vector2(column.Max.X, bottom));
            bottom = notice.Min.Y - gap;
            available = MathF.Max(0f, available - noticeHeight - gap);
        }

        if (ticketCount > 0 && available >= StripHeader * scale)
        {
            var wanted = expanded ? Math.Min(ticketCount, RaceRules.MaxTickets) : 0;
            var fit = (int)((available - StripHeader * scale) / (StripRow * scale));
            rows = Math.Clamp(wanted, 0, Math.Max(0, fit));
            var height = (StripHeader + rows * StripRow) * scale;
            tickets = new Rect(new Vector2(column.Min.X, bottom - height), new Vector2(column.Max.X, bottom));
            bottom = tickets.Min.Y - gap;
        }

        return MathF.Max(top, bottom);
    }

    private static float RailWidth(Rect safe, float scale)
    {
        var width = Math.Clamp(safe.Width * RailShare, RailMin * scale, RailMax * scale);
        return MathF.Min(width, safe.Width * 0.5f);
    }

    private static Rect Empty(Rect safe) => new(safe.Min, safe.Min);

    private static Rect Band(float left, float right, float top, float bottom, float limit)
    {
        var clampedTop = MathF.Min(top, limit);
        var clampedBottom = Math.Clamp(bottom, clampedTop, limit);
        return new Rect(new Vector2(left, clampedTop), new Vector2(MathF.Max(left, right), clampedBottom));
    }
}

internal readonly struct RaceDeckLayout
{
    public const float Pad = 12f;
    public const float Gap = 8f;
    public const float SegmentHeight = 36f;
    public const float AmountHeight = 40f;
    public const float ActionHeight = 56f;
    public const float QuickWidth = 54f;
    public const float PrimaryMinWidth = 150f;
    public const float Height = Pad * 2f + SegmentHeight + AmountHeight + ActionHeight + Gap * 2f;

    public readonly Rect Segment;
    public readonly Rect Field;
    public readonly Rect Half;
    public readonly Rect Double;
    public readonly Rect Max;
    public readonly Rect Ride;
    public readonly Rect Primary;
    public readonly Rect Message;

    private RaceDeckLayout(Rect segment, Rect field, Rect half, Rect twice, Rect max, Rect ride, Rect primary,
        Rect message)
    {
        Segment = segment;
        Field = field;
        Half = half;
        Double = twice;
        Max = max;
        Ride = ride;
        Primary = primary;
        Message = message;
    }

    public bool HasRide => Ride.Width > 0f;

    public static RaceDeckLayout Compute(Rect deck, float rideWidth, float scale)
    {
        var pad = Pad * scale;
        var gap = Gap * scale;
        var left = deck.Min.X + pad;
        var right = MathF.Max(left, deck.Max.X - pad);
        var top = deck.Min.Y + pad;
        var segment = new Rect(new Vector2(left, top), new Vector2(right, top + SegmentHeight * scale));
        var amountTop = segment.Max.Y + gap;
        var amountBottom = amountTop + AmountHeight * scale;
        var quick = MathF.Min(QuickWidth * scale, MathF.Max(0f, (right - left - gap * 3f) * 0.2f));
        var maxLeft = right - quick;
        var doubleLeft = maxLeft - gap - quick;
        var halfLeft = doubleLeft - gap - quick;
        var field = new Rect(new Vector2(left, amountTop), new Vector2(MathF.Max(left, halfLeft - gap), amountBottom));
        var half = new Rect(new Vector2(halfLeft, amountTop), new Vector2(halfLeft + quick, amountBottom));
        var twice = new Rect(new Vector2(doubleLeft, amountTop), new Vector2(doubleLeft + quick, amountBottom));
        var max = new Rect(new Vector2(maxLeft, amountTop), new Vector2(right, amountBottom));
        var actionTop = MathF.Max(amountBottom + gap, deck.Max.Y - pad - ActionHeight * scale);
        var actionBottom = actionTop + ActionHeight * scale;
        var rideRight = left;
        if (rideWidth > 0f)
        {
            var limit = right - PrimaryMinWidth * scale - gap;
            rideRight = Math.Clamp(left + rideWidth, left, MathF.Max(left, limit));
        }

        var ride = new Rect(new Vector2(left, actionTop), new Vector2(rideRight, actionBottom));
        var primaryLeft = ride.Width > 0f ? ride.Max.X + gap : left;
        var primary = new Rect(new Vector2(primaryLeft, actionTop), new Vector2(right, actionBottom));
        var message = new Rect(new Vector2(left, top), new Vector2(right, amountBottom));
        return new RaceDeckLayout(segment, field, half, twice, max, ride, primary, message);
    }
}

internal static class RaceCountdownLayout
{
    public const float Height = 32f;
    public const float WideWidth = 84f;
    public const float NarrowWidth = 40f;
    public const float Gap = 8f;
    public const float CapsuleChrome = 52f;

    public static Rect Compute(Vector2 capsuleCenter, float capsuleWidth, Vector2 infoCenter, float chipRadius,
        float scale, out bool wide)
    {
        var gap = Gap * scale;
        var right = infoCenter.X - chipRadius - gap;
        var left = capsuleCenter.X + capsuleWidth * 0.5f + gap;
        var half = Height * scale * 0.5f;
        var room = right - left;
        wide = room >= WideWidth * scale;
        var width = wide ? WideWidth * scale : NarrowWidth * scale;
        if (room < width)
        {
            return new Rect(new Vector2(right, infoCenter.Y), new Vector2(right, infoCenter.Y));
        }

        return new Rect(new Vector2(right - width, infoCenter.Y - half), new Vector2(right, infoCenter.Y + half));
    }

    public static float CapsuleWidth(float textWidth, float maxWidth, float scale) =>
        MathF.Min(maxWidth, CapsuleChrome * scale + textWidth);
}
