using Aetherphone.Apps.Games.Framework.Cards;
using Aetherphone.Core;
using Aetherphone.Core.Casino;

namespace Aetherphone.Apps.Casino.Tables;

internal static class HoldemSeatRing
{
    private const float BottomAngle = MathF.PI * 0.5f;

    public static int Clamp(int seats) => Math.Clamp(seats, HoldemRules.MinSeats, HoldemRules.MaxSeats);

    public static float Angle(int seat, int seats, int bottomSeat)
    {
        var count = Clamp(seats);
        var slot = ((seat - bottomSeat) % count + count) % count;
        return BottomAngle + MathF.Tau * slot / count;
    }

    public static Vector2 Seat(int seat, int seats, Rect rect, int bottomSeat)
    {
        var angle = Angle(seat, seats, bottomSeat);
        var center = rect.Center;
        return new Vector2(center.X + MathF.Cos(angle) * rect.Width * 0.5f,
            center.Y + MathF.Sin(angle) * rect.Height * 0.5f);
    }

    public static void Ring(int seats, Rect rect, int bottomSeat, Span<Vector2> centers)
    {
        var limit = Math.Min(Clamp(seats), centers.Length);
        for (var seat = 0; seat < limit; seat++)
        {
            centers[seat] = Seat(seat, seats, rect, bottomSeat);
        }
    }

    public static Vector2 Toward(Vector2 from, Vector2 target, float distance)
    {
        var direction = target - from;
        var length = direction.Length();
        if (length <= 0.0001f || length <= distance)
        {
            return target;
        }

        return from + direction / length * distance;
    }
}

internal sealed class HoldemTableLayout
{
    public const float ShelfHeight = 44f;
    public const float FeltGap = 6f;
    public const float RingInsetX = 34f;
    public const float RingInsetTop = 30f;
    public const float RingInsetBottom = 34f;
    public const float PuckRadius = 17f;
    public const float HeroPuckRadius = 20f;
    public const float CapsuleWidth = 92f;
    public const float CapsuleHeight = 40f;
    public const float SitSpotRadius = 24f;
    public const float CapsuleGap = 3f;
    public const float BoardCardMax = 46f;
    public const float BoardCardMin = 26f;
    public const float BoardGap = 4f;
    public const float BetDistance = 52f;
    public const float HeroBetDistance = 112f;
    public const float SeatCardsDistance = 30f;
    public const float SeatCardWidth = 18f;
    public const float ShownCardWidth = 26f;
    public const float HeroCardWidth = 46f;
    public const float HeroCardsLift = 10f;
    public const float PotLift = 18f;

    private readonly Vector2[] seats = new Vector2[HoldemRules.MaxSeats];

    public Rect Felt { get; private set; }

    public Rect Ring { get; private set; }

    public Rect Shelf { get; private set; }

    public int SeatCount { get; private set; }

    public int BottomSeat { get; private set; }

    public float Scale { get; private set; }

    public float BoardCardWidth { get; private set; }

    public Vector2 BoardCenter { get; private set; }

    public Vector2 PotCenter { get; private set; }

    public Vector2 HeroCardsCenter { get; private set; }

    public void Compute(Rect safe, int seatCount, int bottomSeat, float scale)
    {
        Scale = scale;
        SeatCount = HoldemSeatRing.Clamp(seatCount);
        BottomSeat = Math.Clamp(bottomSeat, 0, SeatCount - 1);
        var shelfTop = MathF.Max(safe.Min.Y, safe.Max.Y - ShelfHeight * scale);
        Shelf = new Rect(new Vector2(safe.Min.X, shelfTop), safe.Max);
        Felt = new Rect(safe.Min, new Vector2(safe.Max.X, MathF.Max(safe.Min.Y, shelfTop - FeltGap * scale)));
        var ringMin = new Vector2(Felt.Min.X + RingInsetX * scale, Felt.Min.Y + RingInsetTop * scale);
        var ringMax = new Vector2(Felt.Max.X - RingInsetX * scale, Felt.Max.Y - RingInsetBottom * scale);
        Ring = new Rect(ringMin, new Vector2(MathF.Max(ringMin.X, ringMax.X), MathF.Max(ringMin.Y, ringMax.Y)));
        HoldemSeatRing.Ring(SeatCount, Ring, BottomSeat, seats);
        BoardCenter = new Vector2(Felt.Center.X, Felt.Center.Y - Felt.Height * 0.04f);
        BoardCardWidth = FitBoard(scale);
        PotCenter = new Vector2(BoardCenter.X,
            BoardCenter.Y - BoardCardWidth * CardPose.Aspect * 0.5f - PotLift * scale);
        var hero = seats[BottomSeat];
        HeroCardsCenter = new Vector2(hero.X, hero.Y - HeroPuckRadius * scale - HeroCardsLift * scale
            - HeroCardWidth * scale * CardPose.Aspect * 0.5f);
    }

    public Vector2 SeatCenter(int seat) => seats[Math.Clamp(seat, 0, HoldemRules.MaxSeats - 1)];

    public bool IsBottom(int seat) => seat == BottomSeat;

    public float PuckFor(int seat) => (IsBottom(seat) ? HeroPuckRadius : PuckRadius) * Scale;

    public Vector2 CapsuleCenter(int seat)
    {
        var center = SeatCenter(seat);
        return new Vector2(center.X, center.Y + PuckFor(seat) + (CapsuleGap + CapsuleHeight * 0.5f) * Scale);
    }

    public Rect CapsuleRect(int seat)
    {
        var center = CapsuleCenter(seat);
        var half = new Vector2(CapsuleWidth, CapsuleHeight) * 0.5f * Scale;
        return new Rect(center - half, center + half);
    }

    public Vector2 BetAnchor(int seat)
    {
        var distance = (IsBottom(seat) ? HeroBetDistance : BetDistance) * Scale;
        return HoldemSeatRing.Toward(SeatCenter(seat), BoardCenter, distance);
    }

    public Vector2 SeatCardsAnchor(int seat)
    {
        return HoldemSeatRing.Toward(SeatCenter(seat), BoardCenter, SeatCardsDistance * Scale);
    }

    public Vector2 BoardSlot(int index)
    {
        var width = BoardCardWidth;
        var gap = BoardGap * Scale;
        var total = width * HoldemRules.BoardSize + gap * (HoldemRules.BoardSize - 1);
        var left = BoardCenter.X - total * 0.5f + width * 0.5f;
        return new Vector2(left + index * (width + gap), BoardCenter.Y);
    }

    public Vector2 DealerButton(int seat)
    {
        var center = SeatCenter(seat);
        var inward = HoldemSeatRing.Toward(center, BoardCenter, PuckFor(seat) + 10f * Scale) - center;
        var side = new Vector2(-inward.Y, inward.X);
        var sideLength = side.Length();
        if (sideLength > 0.0001f)
        {
            side = side / sideLength * (PuckFor(seat) + 4f * Scale);
        }

        return center + inward + side;
    }

    private float FitBoard(float scale)
    {
        var width = BoardCardMax * scale;
        var gap = BoardGap * scale;
        var halfLimit = Felt.Width * 0.5f - 8f * scale;
        var cardHeight = width * CardPose.Aspect;
        for (var seat = 0; seat < SeatCount; seat++)
        {
            if (seat == BottomSeat)
            {
                continue;
            }

            var center = seats[seat];
            var top = center.Y - PuckFor(seat);
            var bottom = center.Y + PuckFor(seat) + (CapsuleGap + CapsuleHeight) * scale;
            if (bottom < BoardCenter.Y - cardHeight * 0.5f || top > BoardCenter.Y + cardHeight * 0.5f)
            {
                continue;
            }

            var clearance = MathF.Abs(center.X - BoardCenter.X) - CapsuleWidth * 0.5f * scale - 4f * scale;
            halfLimit = MathF.Min(halfLimit, clearance);
        }

        var fitted = (halfLimit * 2f - gap * (HoldemRules.BoardSize - 1)) / HoldemRules.BoardSize;
        return Math.Clamp(MathF.Min(width, fitted), BoardCardMin * scale, BoardCardMax * scale);
    }
}
