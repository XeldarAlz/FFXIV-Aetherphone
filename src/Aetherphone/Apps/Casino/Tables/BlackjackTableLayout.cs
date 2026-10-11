using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Casino.Tables;

internal sealed class BlackjackTableLayout
{
    public const float SideInset = 10f;
    public const float FitFloor = 0.6f;
    public const float MinimumFit = 0.45f;
    public const float GapFloor = 0.3f;

    public const float DealerPuckDrop = 18f;
    public const float DealerPuckRadius = 16f;
    public const float DealerFanGap = 6f;
    public const float DealerCardWidth = 38f;
    public const float DealerTotalGap = 2f;
    public const float PillHeight = 20f;
    public const float StateGap = 6f;
    public const float StateBandHeight = 28f;
    public const float SpeechGap = 10f;
    public const float DealerFanShare = 0.6f;

    public const float ZoneGap = 6f;
    public const float RailCardWidth = 18f;
    public const float RailSplitCardWidth = 14f;
    public const float RailInnerGap = 2f;
    public const float RailPuckRadius = 22f;
    public const float RailPlateGap = 7f;
    public const float RailPlateHeight = 32f;
    public const float RailArcLift = 12f;
    public const float SpotRadiusMax = 28f;
    public const float SpotRadiusMin = 22f;
    public const float SpotGap = 6f;

    public const float RaiseReserve = 8f;
    public const float CardsTotalGap = 4f;
    public const float TotalCapsuleHeight = 24f;
    public const float TotalBetGap = 8f;
    public const float BetSpotRadius = 22f;
    public const float BetSpotRadiusMax = 26f;
    public const float BetPlateHeight = 20f;
    public const float BetCapsuleGap = 6f;
    public const float CapsuleHeight = 40f;
    public const float CapsulePuckRadius = 16f;
    public const float CapsuleBottomInset = 4f;
    public const float SideSpotOffset = 70f;
    public const float GhostBottomInset = 12f;
    public const float HeroSlotWidthCap = 150f;
    public const float HeroSlotPad = 8f;

    public const float ShoeInsetX = 30f;
    public const float ShoeInsetY = 26f;

    private readonly Vector2[] railPucks = new Vector2[BlackjackRules.SeatCount];

    public Rect Felt { get; private set; }

    public float Scale { get; private set; }

    public float Fit { get; private set; } = 1f;

    public float GapFit { get; private set; } = 1f;

    public bool Seated { get; private set; }

    public int RailCount { get; private set; }

    public float ColumnWidth { get; private set; }

    public float PuckRadius { get; private set; }

    public float SpotRadius { get; private set; }

    public float SpotScale { get; private set; }

    public float Stagger { get; private set; }

    public Vector2 ShoeAnchor { get; private set; }

    public Vector2 DealerPuck { get; private set; }

    public float DealerPuckPixels { get; private set; }

    public Rect SpeechArea { get; private set; }

    public Vector2 DealerFanCenter { get; private set; }

    public float DealerCardPixels { get; private set; }

    public float DealerTotalY { get; private set; }

    public Rect StateBand { get; private set; }

    public float RailCardsLift { get; private set; }

    public float RailBetLift { get; private set; }

    public float RailPlateTop { get; private set; }

    public float RailTop { get; private set; }

    public float RailBottom { get; private set; }

    public float HeroTop { get; private set; }

    public float HeroFanY { get; private set; }

    public float HeroTotalY { get; private set; }

    public Vector2 BetSpot { get; private set; }

    public float BetSpotPixels { get; private set; }

    public float BetPlateY { get; private set; }

    public Vector2 CapsuleCenter { get; private set; }

    public float CapsulePixels { get; private set; }

    public float CapsulePuckPixels { get; private set; }

    public float CapsuleMaxWidth { get; private set; }

    public void Compute(in Rect felt, int railCount, bool seated, float scale)
    {
        Felt = felt;
        Scale = scale;
        Seated = seated;
        RailCount = Math.Clamp(railCount, 0, BlackjackRules.SeatCount);
        ColumnWidth = RailColumnWidth(felt, RailCount, scale);
        SpotRadius = Math.Clamp((ColumnWidth - SpotGap * scale) * 0.5f, SpotRadiusMin * scale, SpotRadiusMax * scale);
        SpotScale = SpotRadius / Stage.SeatSpot.MinimumRadius;
        PuckRadius = RailPuckRadius * scale;
        Stagger = RailStagger(ColumnWidth, SpotRadius, RailCount, scale);
        Solve(felt.Height / MathF.Max(0.0001f, scale));
        ComputeDealer(felt, scale);
        ComputeHero(felt, scale);
        ComputeRail(felt, scale);
    }

    public Vector2 RailPuckCenter(int slot) => railPucks[Math.Clamp(slot, 0, BlackjackRules.SeatCount - 1)];

    public float RaisePixels => RaiseReserve * GapFit * Scale;

    public float MainPlateMaxWidth => (SideSpotOffset * Scale - BetSpotPixels - SpotGap * Scale) * 2f;

    public float SurrenderBand => MathF.Max(0f, HeroTotalY - TotalCapsuleHeight * 0.5f * Scale - HeroTop - Scale);

    public float SurrenderCenterY => (HeroTop + HeroTotalY - TotalCapsuleHeight * 0.5f * Scale) * 0.5f;

    public float HeroCardPixels(int handCount) => HeroCardWidth(handCount) * Fit * Scale;

    public float HeroSlotWidth(int handCount)
    {
        var count = handCount > 0 ? handCount : 1;
        var available = (Felt.Width - SideInset * 2f * Scale) / count;
        var cap = HeroSlotWidthCap * Scale;
        return available < cap ? available : cap;
    }

    public Vector2 HeroHandCenter(int handCount, int handIndex)
    {
        var count = handCount > 0 ? handCount : 1;
        var slotWidth = HeroSlotWidth(count);
        var x = Felt.Center.X + (handIndex - (count - 1) * 0.5f) * slotWidth;
        return new Vector2(x, HeroFanY);
    }

    public Vector2 SideSpot(BlackjackSideBet bet)
    {
        var offset = SideSpotOffset * Scale;
        return new Vector2(bet == BlackjackSideBet.PerfectPairs ? BetSpot.X - offset : BetSpot.X + offset, BetSpot.Y);
    }

    public Rect RailSeatRect(int slot)
    {
        var puck = RailPuckCenter(slot);
        var half = ColumnWidth * 0.5f;
        return new Rect(new Vector2(puck.X - half, puck.Y - RailCardsLift - RailHandHalf()),
            new Vector2(puck.X + half, puck.Y + RailPlateTop + RailPlateHeight * Scale));
    }

    public Rect RailPlateRect(int slot)
    {
        var puck = RailPuckCenter(slot);
        var half = (ColumnWidth - 2f * Scale) * 0.5f;
        var top = puck.Y + RailPlateTop;
        return new Rect(new Vector2(puck.X - half, top), new Vector2(puck.X + half, top + RailPlateHeight * Scale));
    }

    public Rect DealerZone()
    {
        var fanHalfWidth = Felt.Width * DealerFanShare * 0.5f;
        return new Rect(new Vector2(Felt.Center.X - fanHalfWidth, Felt.Min.Y),
            new Vector2(Felt.Center.X + fanHalfWidth, StateBand.Max.Y));
    }

    public static int RailSeatCount(int mySeat) => RailSeatCount(mySeat, BlackjackRules.SeatCount);

    public static int RailSeatCount(int mySeat, int seatLimit)
    {
        var limit = Math.Clamp(seatLimit, 1, BlackjackRules.SeatCount);
        return BlackjackRules.IsSeat(mySeat) && mySeat < limit ? limit - 1 : limit;
    }

    public static int RailSlotOf(int seatIndex, int mySeat) => RailSlotOf(seatIndex, mySeat, BlackjackRules.SeatCount);

    public static int RailSlotOf(int seatIndex, int mySeat, int seatLimit)
    {
        var limit = Math.Clamp(seatLimit, 1, BlackjackRules.SeatCount);
        if (!BlackjackRules.IsSeat(mySeat) || mySeat >= limit)
        {
            return seatIndex;
        }

        var slot = (seatIndex - mySeat - 1) % limit;
        return slot < 0 ? slot + limit : slot;
    }

    public static float RailColumnWidth(in Rect felt, int railCount, float scale)
    {
        if (railCount <= 0)
        {
            return felt.Width;
        }

        return (felt.Width - SideInset * 2f * scale) / railCount;
    }

    public static float RailStagger(float columnWidth, float spotRadius, int railCount, float scale)
    {
        if (railCount <= 1)
        {
            return 0f;
        }

        var pitch = spotRadius * 2f + SpotGap * scale;
        if (columnWidth >= pitch)
        {
            return 0f;
        }

        return MathF.Sqrt(pitch * pitch - columnWidth * columnWidth);
    }

    public static float HeroCardWidth(int handCount)
    {
        return handCount switch
        {
            <= 1 => 44f,
            2 => 36f,
            3 => 30f,
            _ => 24f,
        };
    }

    public static float FanStep(float cardWidth, int cardCount, float maxWidth)
    {
        if (cardCount <= 1)
        {
            return 0f;
        }

        var step = cardWidth * 0.45f;
        var width = cardWidth + step * (cardCount - 1);
        if (width <= maxWidth)
        {
            return step;
        }

        var squeezed = (maxWidth - cardWidth) / (cardCount - 1);
        return squeezed > 0f ? squeezed : 0f;
    }

    private float RailHandHalf() => PlayingCards.HeightFor(RailCardWidth * Scale) * 0.5f;

    private void Solve(float available)
    {
        var fixedHeight = FixedUnits();
        var gapHeight = GapUnits();
        var variableHeight = PlayingCards.HeightFor(DealerCardWidth) + PlayingCards.HeightFor(HeroCardWidth(1));
        Fit = 1f;
        GapFit = 1f;
        if (fixedHeight + gapHeight + variableHeight <= available)
        {
            BetSpotPixels = MathF.Min(BetSpotRadiusMax,
                BetSpotRadius + (available - fixedHeight - gapHeight - variableHeight) * 0.25f) * Scale;
            return;
        }

        BetSpotPixels = BetSpotRadius * Scale;
        Fit = Math.Clamp((available - fixedHeight - gapHeight) / variableHeight, FitFloor, 1f);
        if (fixedHeight + gapHeight + variableHeight * Fit <= available)
        {
            return;
        }

        GapFit = Math.Clamp((available - fixedHeight - variableHeight * Fit) / gapHeight, GapFloor, 1f);
        if (fixedHeight + gapHeight * GapFit + variableHeight * Fit <= available)
        {
            return;
        }

        Fit = MathF.Max(MinimumFit, (available - fixedHeight - gapHeight * GapFit) / variableHeight);
    }

    private float FixedUnits()
    {
        var top = DealerPuckDrop + DealerPuckRadius + PillHeight + StateBandHeight;
        var rail = RailCount > 0
            ? PlayingCards.HeightFor(RailCardWidth) + BetPlateHeight + RailPuckRadius * 2f + RailPlateGap
              + RailPlateHeight + Stagger / Scale
            : 0f;
        var hero = Seated
            ? TotalCapsuleHeight + BetSpotRadius * 2f + BetPlateHeight * 0.5f + CapsuleHeight
            : 0f;
        return top + rail + hero;
    }

    private float GapUnits()
    {
        var top = DealerFanGap + DealerTotalGap + StateGap;
        var rail = RailCount > 0 ? ZoneGap + RailInnerGap * 2f + (RailCount > 1 ? RailArcLift : 0f) : 0f;
        var hero = Seated
            ? ZoneGap + RaiseReserve + CardsTotalGap + TotalBetGap + BetCapsuleGap + CapsuleBottomInset
            : ZoneGap + GhostBottomInset;
        return top + rail + hero;
    }

    private void ComputeDealer(in Rect felt, float scale)
    {
        ShoeAnchor = new Vector2(felt.Max.X - ShoeInsetX * scale, felt.Min.Y + ShoeInsetY * scale);
        DealerPuckPixels = DealerPuckRadius * scale;
        DealerPuck = new Vector2(felt.Center.X, felt.Min.Y + DealerPuckDrop * scale);
        var right = DealerPuck.X - DealerPuckPixels - SpeechGap * scale;
        var left = felt.Min.X + SideInset * 2f * scale;
        SpeechArea = new Rect(new Vector2(left, DealerPuck.Y - DealerPuckPixels),
            new Vector2(MathF.Max(left, right), DealerPuck.Y + DealerPuckPixels));
        DealerCardPixels = DealerCardWidth * Fit * scale;
        var cardHalf = PlayingCards.HeightFor(DealerCardPixels) * 0.5f;
        var fanTop = DealerPuck.Y + DealerPuckPixels + DealerFanGap * GapFit * scale;
        DealerFanCenter = new Vector2(felt.Center.X, fanTop + cardHalf);
        var pillHalf = PillHeight * 0.5f * scale;
        DealerTotalY = DealerFanCenter.Y + cardHalf + DealerTotalGap * GapFit * scale + pillHalf;
        var stateTop = DealerTotalY + pillHalf + StateGap * GapFit * scale;
        StateBand = new Rect(new Vector2(felt.Min.X, stateTop),
            new Vector2(felt.Max.X, stateTop + StateBandHeight * scale));
    }

    private void ComputeHero(in Rect felt, float scale)
    {
        var cardHalf = PlayingCards.HeightFor(HeroCardPixels(1)) * 0.5f;
        CapsulePixels = CapsuleHeight * scale;
        CapsulePuckPixels = CapsulePuckRadius * scale;
        CapsuleMaxWidth = felt.Width - SideInset * 2f * scale;
        if (!Seated)
        {
            HeroFanY = felt.Max.Y - GhostBottomInset * GapFit * scale - cardHalf;
            HeroTop = HeroFanY - cardHalf;
            HeroTotalY = HeroFanY + cardHalf;
            BetSpot = new Vector2(felt.Center.X, felt.Max.Y);
            BetPlateY = felt.Max.Y;
            CapsuleCenter = new Vector2(felt.Center.X, felt.Max.Y);
            return;
        }

        var capsuleBottom = felt.Max.Y - CapsuleBottomInset * GapFit * scale;
        CapsuleCenter = new Vector2(felt.Center.X, capsuleBottom - CapsulePixels * 0.5f);
        var plateHalf = BetPlateHeight * 0.5f * scale;
        var plateBottom = capsuleBottom - CapsulePixels - BetCapsuleGap * GapFit * scale;
        BetPlateY = plateBottom - plateHalf;
        BetSpot = new Vector2(felt.Center.X, BetPlateY - BetSpotPixels);
        var rowTop = BetSpot.Y - BetSpotPixels;
        var totalHalf = TotalCapsuleHeight * 0.5f * scale;
        HeroTotalY = rowTop - TotalBetGap * GapFit * scale - totalHalf;
        HeroFanY = HeroTotalY - totalHalf - CardsTotalGap * GapFit * scale - cardHalf;
        HeroTop = HeroFanY - cardHalf - RaiseReserve * GapFit * scale;
    }

    private void ComputeRail(in Rect felt, float scale)
    {
        RailBetLift = PuckRadius + RailInnerGap * GapFit * scale + BetPlateHeight * 0.5f * scale;
        RailCardsLift = RailBetLift + BetPlateHeight * 0.5f * scale + RailInnerGap * GapFit * scale + RailHandHalf();
        RailPlateTop = PuckRadius + RailPlateGap * scale;
        var above = RailCardsLift + RailHandHalf();
        var below = RailPlateTop + RailPlateHeight * scale;
        var lift = RailCount > 1 ? RailArcLift * GapFit * scale : 0f;
        var bandHeight = above + lift + Stagger + below;
        var zoneTop = StateBand.Max.Y + ZoneGap * GapFit * scale;
        var zoneBottom = HeroTop - ZoneGap * GapFit * scale;
        var slack = MathF.Max(0f, zoneBottom - zoneTop - bandHeight);
        RailTop = zoneTop + slack * 0.5f;
        RailBottom = RailTop + bandHeight;
        var edgeY = RailTop + above;
        var outer = (RailCount - 1) * 0.5f * ColumnWidth;
        for (var slot = 0; slot < railPucks.Length; slot++)
        {
            var x = felt.Min.X + SideInset * scale + ColumnWidth * (slot + 0.5f);
            var offset = x - felt.Center.X;
            var spread = outer > 0f ? offset / outer : 0f;
            var y = edgeY + lift * (1f - spread * spread) + ((slot & 1) == 1 ? Stagger : 0f);
            railPucks[slot] = new Vector2(x, y);
        }
    }
}
