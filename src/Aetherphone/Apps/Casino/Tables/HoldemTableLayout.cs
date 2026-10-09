using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Casino.Tables;

internal static class HoldemSeatRing
{
    private const float BottomAngle = MathF.PI * 0.5f;

    public static int Clamp(int seats) => Math.Clamp(seats, HoldemRules.MinSeats, HoldemRules.MaxSeats);

    public static int SlotOf(int seat, int seats, int bottomSeat)
    {
        var count = Clamp(seats);
        return ((seat - bottomSeat) % count + count) % count;
    }

    public static Vector2 Seat(int seat, int seats, Rect rect, int bottomSeat)
    {
        var angle = BottomAngle + MathF.Tau * SlotOf(seat, seats, bottomSeat) / Clamp(seats);
        var center = rect.Center;
        return new Vector2(center.X + MathF.Cos(angle) * rect.Width * 0.5f,
            center.Y + MathF.Sin(angle) * rect.Height * 0.5f);
    }
}

internal enum HoldemPodSide : byte
{
    Top,
    LowerLeft,
    LowerRight,
    UpperLeft,
    UpperRight,
    Bottom,
}

internal sealed class HoldemTableLayout
{
    public const float ShelfHeight = 40f;
    public const float FeltGap = 6f;
    public const float RingInset = 30f;
    public const float SideInset = 4f;
    public const float PodGap = 5f;
    public const float PodWidthWide = 100f;
    public const float PodWidthMedium = 84f;
    public const float PodWidthCompact = 64f;
    public const float PuckRadius = 17f;
    public const float CompactPuckRadius = 15f;
    public const float TagReach = 12f;
    public const float CapsuleGap = 2f;
    public const float CapsuleHeight = 44f;
    public const float BetGap = 2f;
    public const float BetHeight = 20f;
    public const float SitSpotRadius = 28f;
    public const float HeroCapsuleWidth = 132f;
    public const float HeroAvatarRadius = 15f;
    public const float HeroCapsulePad = 6f;
    public const float HeroCardWidth = 46f;
    public const float HeroCardMin = 27f;
    public const float HeroCardOverlap = 0.66f;
    public const float HeroLift = 6f;
    public const float HeroCardsGap = 4f;
    public const float BottomInset = 2f;
    public const float BoardCardMax = 46f;
    public const float BoardCardMin = 26f;
    public const float BoardCardFloor = 22f;
    public const float BoardGap = 4f;
    public const float BoardShare = 0.92f;
    public const float PotHeight = 28f;
    public const float StateHeight = 28f;
    public const float ZoneGap = 6f;
    public const float ZoneGapFloor = 1.5f;
    public const float SeatCardWidth = 14f;
    public const float ShownCardWidth = 26f;
    public const float ShownCardStep = 0.9f;
    public const float DealerButtonRadius = 9f;

    private readonly Vector2[] pucks = new Vector2[HoldemRules.MaxSeats];
    private readonly HoldemPodSide[] sides = new HoldemPodSide[HoldemRules.MaxSeats];
    private readonly int[] slotSeats = new int[HoldemRules.MaxSeats];

    public Rect Felt { get; private set; }

    public Rect Ring { get; private set; }

    public Rect Shelf { get; private set; }

    public int SeatCount { get; private set; }

    public int BottomSeat { get; private set; }

    public bool Seated { get; private set; }

    public float Scale { get; private set; }

    public float PodWidth { get; private set; }

    public float PodPuckRadius { get; private set; }

    public float Gap { get; private set; }

    public int TopCount { get; private set; }

    public int LowerCount { get; private set; }

    public int UpperCount { get; private set; }

    public float BoardCardWidth { get; private set; }

    public float HeroCardPixels { get; private set; }

    public Vector2 BoardCenter { get; private set; }

    public Vector2 PotCenter { get; private set; }

    public Rect StateBand { get; private set; }

    public Vector2 HeroCardsCenter { get; private set; }

    public Rect HeroCapsule { get; private set; }

    public Vector2 HeroBet { get; private set; }

    public float BottomTop { get; private set; }

    public void Compute(Rect safe, int seatCount, int bottomSeat, bool seated, float scale)
    {
        Scale = scale;
        SeatCount = HoldemSeatRing.Clamp(seatCount);
        BottomSeat = Math.Clamp(bottomSeat, 0, SeatCount - 1);
        Seated = seated;
        var shelfTop = MathF.Max(safe.Min.Y, safe.Max.Y - ShelfHeight * scale);
        Shelf = new Rect(new Vector2(safe.Min.X, shelfTop), safe.Max);
        Felt = new Rect(safe.Min, new Vector2(safe.Max.X, MathF.Max(safe.Min.Y, shelfTop - FeltGap * scale)));
        var inset = new Vector2(RingInset * scale, RingInset * scale);
        Ring = new Rect(Felt.Min + inset, new Vector2(MathF.Max(Felt.Min.X + inset.X, Felt.Max.X - inset.X),
            MathF.Max(Felt.Min.Y + inset.Y, Felt.Max.Y - inset.Y)));
        Distribute(SeatCount - 1);
        SizePods(scale);
        Solve(scale);
        PlaceBottom(scale);
        PlaceBoard(scale);
        PlacePods(scale);
    }

    public bool IsBottom(int seat) => seat == BottomSeat;

    public bool IsHero(int seat) => Seated && seat == BottomSeat;

    public HoldemPodSide SideOf(int seat) => sides[Math.Clamp(seat, 0, HoldemRules.MaxSeats - 1)];

    public Vector2 SeatCenter(int seat) => pucks[Math.Clamp(seat, 0, HoldemRules.MaxSeats - 1)];

    public float PuckFor(int seat) => IsHero(seat) ? HeroAvatarRadius * Scale : PodPuckRadius;

    public Rect CapsuleRect(int seat)
    {
        if (IsHero(seat))
        {
            return HeroCapsule;
        }

        var puck = SeatCenter(seat);
        var top = puck.Y + PodPuckRadius + CapsuleGap * Scale;
        var half = PodWidth * 0.5f;
        return new Rect(new Vector2(puck.X - half, top), new Vector2(puck.X + half, top + CapsuleHeight * Scale));
    }

    public Rect PodRect(int seat)
    {
        if (IsHero(seat))
        {
            return HeroCapsule;
        }

        var puck = SeatCenter(seat);
        var capsule = CapsuleRect(seat);
        return new Rect(new Vector2(capsule.Min.X, puck.Y - PodPuckRadius - TagReach * Scale), capsule.Max);
    }

    public Vector2 TagCenter(int seat)
    {
        var puck = SeatCenter(seat);
        return new Vector2(puck.X, puck.Y - PuckFor(seat));
    }

    public Vector2 BetAnchor(int seat)
    {
        if (IsHero(seat))
        {
            return HeroBet;
        }

        var pod = PodRect(seat);
        var half = BetHeight * 0.5f * Scale;
        var gap = BetGap * Scale;
        return SideOf(seat) == HoldemPodSide.Bottom
            ? new Vector2(pod.Center.X, pod.Min.Y - gap - half)
            : new Vector2(pod.Center.X, pod.Max.Y + gap + half);
    }

    public Rect BetRect(int seat)
    {
        var anchor = BetAnchor(seat);
        var half = new Vector2(IsHero(seat) ? HeroCapsule.Width * 0.5f : PodWidth * 0.5f, BetHeight * 0.5f * Scale);
        return new Rect(anchor - half, anchor + half);
    }

    public Vector2 SeatCardsAnchor(int seat)
    {
        var puck = SeatCenter(seat);
        var cardWidth = SeatCardWidth * Scale;
        return new Vector2(puck.X - PodPuckRadius - cardWidth * 0.3f, puck.Y + PodPuckRadius * 0.3f);
    }

    public float SpotRadius => MathF.Min(SitSpotRadius * Scale,
        MathF.Min(PodWidth * 0.5f - Scale, PodPuckRadius + TagReach * Scale));

    public float SpotScale => SpotRadius / Stage.SeatSpot.MinimumRadius;

    public float ShownCardPixels => MathF.Min(MathF.Min(ShownCardWidth * Scale,
            PodWidth / (1f + ShownCardStep) - 2f * Scale),
        PlayingCards.WidthFor((PodPuckRadius + CapsuleGap * Scale) * 2f));

    public Vector2 DealerButton(int seat)
    {
        if (IsHero(seat))
        {
            var radius = DealerButtonRadius * Scale;
            return new Vector2(HeroCapsule.Max.X - HeroCapsulePad * Scale - radius, HeroCapsule.Center.Y);
        }

        var puck = SeatCenter(seat);
        var button = DealerButtonRadius * Scale;
        var reach = MathF.Min(PodPuckRadius + button, PodWidth * 0.5f - button);
        return new Vector2(puck.X + reach, puck.Y + PodPuckRadius - button);
    }

    public Vector2 BoardSlot(int index)
    {
        var width = BoardCardWidth;
        var gap = BoardGap * Scale;
        var total = width * HoldemRules.BoardSize + gap * (HoldemRules.BoardSize - 1);
        var left = BoardCenter.X - total * 0.5f + width * 0.5f;
        return new Vector2(left + index * (width + gap), BoardCenter.Y);
    }

    public Rect BoardRect()
    {
        var width = BoardCardWidth;
        var total = width * HoldemRules.BoardSize + BoardGap * Scale * (HoldemRules.BoardSize - 1);
        var half = new Vector2(total * 0.5f, PlayingCards.HeightFor(width) * 0.5f);
        return new Rect(BoardCenter - half, BoardCenter + half);
    }

    public Rect PotRect()
    {
        var half = new Vector2(BoardInterior() * 0.5f, PotHeight * 0.5f * Scale);
        return new Rect(PotCenter - half, PotCenter + half);
    }

    public Rect HeroCardsRect()
    {
        var width = HeroCardPixels * (1f + HeroCardOverlap);
        var height = PlayingCards.HeightFor(HeroCardPixels);
        var min = new Vector2(HeroCardsCenter.X - width * 0.5f, HeroCardsCenter.Y - height * 0.5f - HeroLift * Scale);
        return new Rect(min, new Vector2(min.X + width, HeroCardsCenter.Y + height * 0.5f));
    }

    public float BoardInterior()
    {
        var width = Felt.Width - SideInset * 2f * Scale;
        if (UpperCount > 0)
        {
            width -= (PodWidth + PodGap * Scale) * 2f;
        }

        return MathF.Max(0f, width);
    }

    private float BottomInterior()
    {
        var width = Felt.Width - SideInset * 2f * Scale;
        if (LowerCount > 0)
        {
            width -= (PodWidth + PodGap * Scale) * 2f;
        }

        return MathF.Max(0f, width);
    }

    private void Distribute(int opponents)
    {
        UpperCount = Math.Clamp(opponents - 6, 0, 2);
        LowerCount = opponents >= 3 ? 2 : 0;
        TopCount = opponents - UpperCount - LowerCount;
    }

    private void SizePods(float scale)
    {
        var columns = Math.Max(TopCount, LowerCount > 0 ? 3 : 1);
        var preferred = TopCount <= 2 && LowerCount == 0 ? PodWidthWide
            : columns <= 3 ? PodWidthMedium
            : PodWidthCompact;
        var room = (Felt.Width - SideInset * 2f * scale - PodGap * scale * (columns - 1)) / columns;
        PodWidth = MathF.Max(1f, MathF.Min(preferred * scale, room));
        PodPuckRadius = (PodWidth >= PodWidthMedium * scale ? PuckRadius : CompactPuckRadius) * scale;
    }

    private float PodBand() => PodPuckRadius * 2f + (TagReach + CapsuleGap + CapsuleHeight + BetGap + BetHeight) * Scale;

    private float HeroStack(float heroCard) =>
        (BottomInset + CapsuleHeight + HeroCardsGap + HeroLift + BetGap + BetHeight) * Scale
        + PlayingCards.HeightFor(heroCard);

    private float BottomHeight(float heroCard)
    {
        if (!Seated)
        {
            return PodBand();
        }

        var hero = HeroStack(heroCard);
        return LowerCount > 0 ? MathF.Max(hero, PodBand()) : hero;
    }

    private float Required(float boardCard, float heroCard, float gap)
    {
        var top = TopCount > 0 ? PodBand() + gap : 0f;
        var band = (PotHeight + StateHeight) * Scale + gap * 2f + PlayingCards.HeightFor(boardCard);
        return top + band + gap + BottomHeight(heroCard);
    }

    private void Solve(float scale)
    {
        var available = Felt.Height;
        var boardCap = MathF.Min(BoardCardMax * scale,
            (BoardInterior() * BoardShare - BoardGap * scale * (HoldemRules.BoardSize - 1)) / HoldemRules.BoardSize);
        var boardFloor = MathF.Min(boardCap, (UpperCount > 0 ? BoardCardFloor : BoardCardMin) * scale);
        var heroCap = MathF.Min(HeroCardWidth * scale, BottomInterior() / (1f + HeroCardOverlap));
        var heroFloor = MathF.Min(heroCap, HeroCardMin * scale);
        Gap = ZoneGap * scale;
        BoardCardWidth = boardCap;
        HeroCardPixels = heroCap;
        if (Required(boardCap, heroCap, Gap) <= available)
        {
            return;
        }

        var fit = 1f;
        for (var step = 0; step < 24; step++)
        {
            fit -= 1f / 24f;
            BoardCardWidth = boardFloor + (boardCap - boardFloor) * fit;
            HeroCardPixels = heroFloor + (heroCap - heroFloor) * fit;
            if (Required(BoardCardWidth, HeroCardPixels, Gap) <= available)
            {
                return;
            }
        }

        BoardCardWidth = boardFloor;
        HeroCardPixels = heroFloor;
        var over = Required(boardFloor, heroFloor, Gap) - available;
        var gapCount = (TopCount > 0 ? 1f : 0f) + 3f;
        Gap = MathF.Max(ZoneGapFloor * scale, Gap - over / gapCount);
    }

    private void PlaceBottom(float scale)
    {
        var bottom = Felt.Max.Y;
        BottomTop = bottom - BottomHeight(HeroCardPixels);
        var center = Felt.Center.X;
        sides[BottomSeat] = HoldemPodSide.Bottom;
        if (!Seated)
        {
            var puckY = BottomTop + (BetHeight + BetGap + TagReach) * scale + PodPuckRadius;
            pucks[BottomSeat] = new Vector2(center, puckY);
            HeroCapsule = new Rect(new Vector2(center, bottom), new Vector2(center, bottom));
            HeroCardsCenter = pucks[BottomSeat];
            HeroBet = pucks[BottomSeat];
            return;
        }

        var width = MathF.Min(HeroCapsuleWidth * scale, BottomInterior());
        var capsuleBottom = bottom - BottomInset * scale;
        HeroCapsule = new Rect(new Vector2(center - width * 0.5f, capsuleBottom - CapsuleHeight * scale),
            new Vector2(center + width * 0.5f, capsuleBottom));
        var avatar = HeroAvatarRadius * scale;
        pucks[BottomSeat] = new Vector2(HeroCapsule.Min.X + HeroCapsulePad * scale + avatar, HeroCapsule.Center.Y);
        var cardHeight = PlayingCards.HeightFor(HeroCardPixels);
        HeroCardsCenter = new Vector2(center, HeroCapsule.Min.Y - HeroCardsGap * scale - cardHeight * 0.5f);
        var cardsTop = HeroCardsCenter.Y - cardHeight * 0.5f - HeroLift * scale;
        HeroBet = new Vector2(center, cardsTop - (BetGap + BetHeight * 0.5f) * scale);
    }

    private void PlaceBoard(float scale)
    {
        var top = Felt.Min.Y + (TopCount > 0 ? PodBand() + Gap : 0f);
        var bottom = BottomTop - Gap;
        var boardHeight = PlayingCards.HeightFor(BoardCardWidth);
        var band = (PotHeight + StateHeight) * scale + Gap * 2f + boardHeight;
        var start = top + MathF.Max(0f, bottom - top - band) * 0.5f;
        PotCenter = new Vector2(Felt.Center.X, start + PotHeight * 0.5f * scale);
        BoardCenter = new Vector2(Felt.Center.X, start + PotHeight * scale + Gap + boardHeight * 0.5f);
        var stateTop = BoardCenter.Y + boardHeight * 0.5f + Gap;
        var stateHalf = BoardInterior() * 0.5f;
        StateBand = new Rect(new Vector2(Felt.Center.X - stateHalf, stateTop),
            new Vector2(Felt.Center.X + stateHalf, stateTop + StateHeight * scale));
    }

    private void PlacePods(float scale)
    {
        for (var seat = 0; seat < SeatCount; seat++)
        {
            slotSeats[HoldemSeatRing.SlotOf(seat, SeatCount, BottomSeat)] = seat;
        }

        var tagTop = TagReach * scale + PodPuckRadius;
        var leftColumn = Felt.Min.X + SideInset * scale + PodWidth * 0.5f;
        var rightColumn = Felt.Max.X - SideInset * scale - PodWidth * 0.5f;
        var lowerPuckY = BottomTop + tagTop;
        var upperPuckY = BoardCenter.Y - (PodBand() * 0.5f) + tagTop;
        var slot = 1;
        if (LowerCount > 0)
        {
            Assign(slotSeats[slot++], new Vector2(leftColumn, lowerPuckY), HoldemPodSide.LowerLeft);
        }

        if (UpperCount > 0)
        {
            Assign(slotSeats[slot++], new Vector2(leftColumn, upperPuckY), HoldemPodSide.UpperLeft);
        }

        var rowWidth = TopCount * PodWidth + (TopCount - 1) * PodGap * scale;
        var rowLeft = Felt.Center.X - rowWidth * 0.5f + PodWidth * 0.5f;
        for (var index = 0; index < TopCount; index++)
        {
            Assign(slotSeats[slot++], new Vector2(rowLeft + index * (PodWidth + PodGap * scale), Felt.Min.Y + tagTop),
                HoldemPodSide.Top);
        }

        if (UpperCount > 1)
        {
            Assign(slotSeats[slot++], new Vector2(rightColumn, upperPuckY), HoldemPodSide.UpperRight);
        }

        if (LowerCount > 0)
        {
            Assign(slotSeats[slot], new Vector2(rightColumn, lowerPuckY), HoldemPodSide.LowerRight);
        }
    }

    private void Assign(int seat, Vector2 puck, HoldemPodSide side)
    {
        pucks[seat] = puck;
        sides[seat] = side;
    }
}
