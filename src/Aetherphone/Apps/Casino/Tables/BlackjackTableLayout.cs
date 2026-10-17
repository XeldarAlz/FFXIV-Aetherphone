using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Casino.Tables;

internal sealed class BlackjackTableLayout
{
    public const float SideInset = 10f;
    public const float FitFloor = 0.6f;
    public const float RegularFitFloor = 0.7f;
    public const float MinimumFit = 0.45f;
    public const float LastResortFit = 0.3f;
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
    public const float RailRingGap = 4f;
    public const float RailPlateGap = 7f;
    public const float RailPlateHeight = 32f;
    public const float RailGutter = 2f;
    public const float RailArcLift = 12f;
    public const float SpotRadiusMax = 28f;
    public const float SpotRadiusMin = 18f;
    public const float SpotGap = 6f;
    public const float SideStakeHeight = 14f;
    public const float SideStakeGap = 3f;
    public const float SideStakeOverlap = 1f;
    public const float SideStakeSlide = 4f;
    public const float TimeBankPipLift = 4f;
    public const float RailTitleDrop = 6f;

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
    public const float CapsuleRingGap = 3.5f;
    public const float CapsuleBottomInset = 4f;
    public const float SideSpotOffset = 70f;
    public const float SpotCaptionShare = 1.8f;
    public const float GhostBottomInset = 12f;
    public const float HeroSlotWidthCap = 150f;
    public const float HeroSlotPad = 8f;

    public const float ShoeInsetX = 30f;
    public const float ShoeInsetY = 26f;
    public const float ShoeCardWidth = 22f;
    public const float ShoeLift = 2.5f;

    public const float DealerTopInset = 4f;
    public const float DealerTotalBesideGap = 6f;
    public const float CompactDealerPuckRadius = 12f;
    public const float CompactStateBandHeight = 24f;
    public const float CompactRailPuckRadius = 16f;
    public const float CompactSpotRadiusMin = 16f;
    public const float CompactSpotRadiusMax = 22f;
    public const float CompactRailCardWidth = 16f;
    public const float CompactRailSplitCardWidth = 12f;
    public const float CompactPlateRise = 0.5f;
    public const float CompactBetSpotRadius = 19f;
    public const float CompactBetSpotRadiusMax = 22f;
    public const float CompactCapsuleHeight = 36f;
    public const float CompactTotalOverlap = 8f;

    private const float FitTolerance = 0.0001f;
    private const float Leftward = -1f;
    private const float Rightward = 1f;

    private readonly struct Tier
    {
        public readonly bool Compact;
        public readonly float DealerPuckRadius;
        public readonly float StateBandHeight;
        public readonly float RailPuckRadius;
        public readonly float SpotRadiusMin;
        public readonly float SpotRadiusMax;
        public readonly float RailCardWidth;
        public readonly float RailSplitCardWidth;
        public readonly float BetSpotRadius;
        public readonly float BetSpotRadiusMax;
        public readonly float CapsuleHeight;

        public Tier(bool compact, float dealerPuckRadius, float stateBandHeight, float railPuckRadius,
            float spotRadiusMin, float spotRadiusMax, float railCardWidth, float railSplitCardWidth,
            float betSpotRadius, float betSpotRadiusMax, float capsuleHeight)
        {
            Compact = compact;
            DealerPuckRadius = dealerPuckRadius;
            StateBandHeight = stateBandHeight;
            RailPuckRadius = railPuckRadius;
            SpotRadiusMin = spotRadiusMin;
            SpotRadiusMax = spotRadiusMax;
            RailCardWidth = railCardWidth;
            RailSplitCardWidth = railSplitCardWidth;
            BetSpotRadius = betSpotRadius;
            BetSpotRadiusMax = betSpotRadiusMax;
            CapsuleHeight = capsuleHeight;
        }

        public float PlateTop(float puckRadius, float scale) =>
            Compact ? puckRadius * CompactPlateRise : puckRadius + RailPlateGap * scale;
    }

    private static readonly Tier RegularTier = new(false, DealerPuckRadius, StateBandHeight, RailPuckRadius,
        SpotRadiusMin, SpotRadiusMax, RailCardWidth, RailSplitCardWidth, BetSpotRadius, BetSpotRadiusMax,
        CapsuleHeight);

    private static readonly Tier CompactTier = new(true, CompactDealerPuckRadius, CompactStateBandHeight,
        CompactRailPuckRadius, CompactSpotRadiusMin, CompactSpotRadiusMax, CompactRailCardWidth,
        CompactRailSplitCardWidth, CompactBetSpotRadius, CompactBetSpotRadiusMax, CompactCapsuleHeight);

    private readonly Vector2[] railPucks = new Vector2[BlackjackRules.SeatCount];

    private Tier tier = RegularTier;

    public Rect Felt { get; private set; }

    public float Scale { get; private set; }

    public bool Compact { get; private set; }

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

    public float ShoePixels { get; private set; }

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
        var available = felt.Height / MathF.Max(0.0001f, scale);
        Adopt(RegularTier);
        if (!Roomy(available))
        {
            Adopt(CompactTier);
        }

        Solve(available);

        ComputeDealer(felt, scale);
        ComputeHero(felt, scale);
        ComputeRail(felt, scale);
    }

    public Vector2 RailPuckCenter(int slot) => railPucks[Math.Clamp(slot, 0, BlackjackRules.SeatCount - 1)];

    public float RaisePixels => RaiseReserve * GapFit * Scale;

    public float PillPixels => PillHeight * Scale;

    public float TotalCapsulePixels => TotalCapsuleHeight * Scale;

    public bool ShowsRailTitles => !Compact;

    public float MainPlateMaxWidth => (SideSpotOffset * Scale - BetSpotPixels - SpotGap * Scale) * 2f;

    public float SpotCaptionWidth => BetSpotPixels * SpotCaptionShare;

    public float RailCardPixels(int handCount) =>
        (handCount > 1 ? tier.RailSplitCardWidth : tier.RailCardWidth) * Scale;

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

    public float DealerTotalBesideX(float fanRight, float pillHalfWidth)
    {
        var gap = DealerTotalBesideGap * Scale;
        var shoeLeft = ShoeAnchor.X - ShoePixels * 0.5f;
        return MathF.Min(fanRight + gap + pillHalfWidth, shoeLeft - gap - pillHalfWidth);
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
        var half = RailPlateWidth * 0.5f;
        var top = puck.Y + RailPlateTop;
        return new Rect(new Vector2(puck.X - half, top), new Vector2(puck.X + half, top + RailPlateHeight * Scale));
    }

    public Rect RailPlateRect(int slot, bool leftEmpty, bool rightEmpty)
    {
        var plate = RailPlateRect(slot);
        var gutter = RailGutter * Scale;
        var first = slot <= 0;
        var last = slot >= RailCount - 1;
        var left = first
            ? FeltEdgeRoom(plate.Min.X - Felt.Min.X, rightEmpty)
            : NeighbourLend(plate, slot - 1, leftEmpty, Leftward);
        var right = last
            ? FeltEdgeRoom(Felt.Max.X - plate.Max.X, leftEmpty)
            : NeighbourLend(plate, slot + 1, rightEmpty, Rightward);
        if (!first && !last)
        {
            left = MathF.Min(left, right);
            right = left;
        }

        var spare = ColumnWidth * 2f - gutter * 2f - plate.Width;
        if (left + right > spare)
        {
            var trim = spare / (left + right);
            left *= trim;
            right *= trim;
        }

        return new Rect(new Vector2(plate.Min.X - left, plate.Min.Y), new Vector2(plate.Max.X + right, plate.Max.Y));
    }

    public float RailPlateWidth =>
        MathF.Min(ColumnWidth - RailGutter * Scale, (ColumnWidth - PuckRadius - Scale) * 2f);

    public float RailPlateReach => ColumnWidth * 0.5f - RailGutter * Scale;

    public float RailPlateTextWidth => RailPlateTextWidthFor(RailPlateWidth);

    public float RailPlateTextWidthFor(float plateWidth) => plateWidth - Stage.StageText.TightPadX * 2f * Scale;

    public float RailBetPlateWidth => ColumnWidth - RailGutter * Scale;

    public float RailHandWidth(int handCount) => ColumnWidth / (handCount > 0 ? handCount : 1);

    public float SideStakeFloor => Seated ? HeroTop + RaisePixels : HeroTop;

    public Rect RailSideStakeRect(int slot, BlackjackSideBet bet, bool both) =>
        SideStakeRect(RailPlateRect(slot), SideStakeFloor, bet, both, Scale);

    public static Rect SideStakeRect(in Rect plate, float floor, BlackjackSideBet bet, bool both, float scale)
    {
        var height = SideStakeHeight * scale;
        var attached = plate.Max.Y - SideStakeOverlap * scale;
        var top = MathF.Min(attached, MathF.Max(floor - height, attached - SideStakeSlide * scale));
        var width = both ? MathF.Max(1f, (plate.Width - SideStakeGap * scale) * 0.5f) : plate.Width;
        var left = !both || bet == BlackjackSideBet.PerfectPairs ? plate.Min.X : plate.Max.X - width;
        return new Rect(new Vector2(left, top), new Vector2(left + width, top + height));
    }

    public static Vector2 TimeBankPips(Vector2 puck, float puckRadius, float scale) =>
        new(puck.X, puck.Y - puckRadius + TimeBankPipLift * scale);

    public static Vector2 RailTitleCenter(Vector2 puck, float puckRadius, float scale) =>
        new(puck.X, puck.Y + puckRadius - RailTitleDrop * scale);

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

    public static float RailColumnWidth(in Rect felt, int railCount, float scale) =>
        RailColumnWidth(felt.Width, railCount, scale);

    public static float RailColumnWidth(float feltWidth, int railCount, float scale)
    {
        if (railCount <= 0)
        {
            return feltWidth;
        }

        return (feltWidth - SideInset * 2f * scale) / railCount;
    }

    public static float MinimumHeight(float feltWidth, int railCount, bool seated, float scale) =>
        RequiredHeight(RegularTier, feltWidth, railCount, seated, scale, MinimumFit, GapFloor);

    public static float CompactMinimumHeight(float feltWidth, int railCount, bool seated, float scale) =>
        RequiredHeight(CompactTier, feltWidth, railCount, seated, scale, MinimumFit, GapFloor);

    public static float LastResortHeight(float feltWidth, int railCount, bool seated, float scale) =>
        RequiredHeight(CompactTier, feltWidth, railCount, seated, scale, LastResortFit, 0f);

    public static float FeltTop(float rowBottom, float knockReserve, float safeBottom, float minimumHeight) =>
        MathF.Max(rowBottom, MathF.Min(rowBottom + knockReserve, safeBottom - minimumHeight));

    public static float FeltBottom(float feltTop, float safeBottom, float noticeReserve, float minimumHeight) =>
        MathF.Min(safeBottom, MathF.Max(safeBottom - noticeReserve, feltTop + minimumHeight));

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

    private static float SpotRadiusFor(float columnWidth, float scale, in Tier tier) =>
        Math.Clamp((columnWidth - SpotGap * scale) * 0.5f, tier.SpotRadiusMin * scale, tier.SpotRadiusMax * scale);

    private static float RequiredHeight(in Tier tier, float feltWidth, int railCount, bool seated, float scale,
        float fit, float gapFit)
    {
        var count = Math.Clamp(railCount, 0, BlackjackRules.SeatCount);
        var column = RailColumnWidth(feltWidth, count, scale);
        var stagger = RailStagger(column, SpotRadiusFor(column, scale, tier), count, scale);
        var units = FixedUnits(tier, count, seated, stagger / scale) + GapUnits(tier, count, seated) * gapFit
                    + VariableUnits(tier, fit);
        return units * scale;
    }

    private void Adopt(in Tier next)
    {
        tier = next;
        Compact = next.Compact;
        SpotRadius = SpotRadiusFor(ColumnWidth, Scale, next);
        SpotScale = SpotRadius / Stage.SeatSpot.MinimumRadius;
        PuckRadius = MathF.Min(next.RailPuckRadius * Scale, SpotRadius);
        Stagger = RailStagger(ColumnWidth, SpotRadius, RailCount, Scale);
    }

    private float RailHandHalf() => PlayingCards.HeightFor(tier.RailCardWidth * Scale) * 0.5f;

    private float FeltEdgeRoom(float distance, bool innerNeighbourEmpty) =>
        innerNeighbourEmpty ? MathF.Max(0f, distance - RailGutter * Scale) : 0f;

    private float NeighbourLend(in Rect plate, int neighbourSlot, bool neighbourEmpty, float direction)
    {
        if (!neighbourEmpty)
        {
            return 0f;
        }

        var neighbour = RailPuckCenter(neighbourSlot);
        var bound = neighbour.X - direction * (SitHalfChord(neighbour, plate.Min.Y) + RailGutter * Scale);
        var edge = direction > 0f ? plate.Max.X : plate.Min.X;
        return Math.Clamp((bound - edge) * direction, 0f, RailPlateReach);
    }

    private float SitHalfChord(Vector2 neighbour, float plateTop)
    {
        var radius = SpotRadius;
        var dip = neighbour.Y + radius - plateTop;
        if (dip <= 0f)
        {
            return 0f;
        }

        var rise = Math.Clamp(radius - dip, 0f, radius);
        return MathF.Sqrt(radius * radius - rise * rise);
    }

    private bool Roomy(float available)
    {
        var fixedHeight = FixedUnits(tier, RailCount, Seated, Stagger / Scale);
        var gapHeight = GapUnits(tier, RailCount, Seated);
        return fixedHeight + gapHeight + VariableUnits(tier, RegularFitFloor) <= available + FitTolerance;
    }

    private void Solve(float available)
    {
        var fixedHeight = FixedUnits(tier, RailCount, Seated, Stagger / Scale);
        var gapHeight = GapUnits(tier, RailCount, Seated);
        Fit = 1f;
        GapFit = 1f;
        var full = fixedHeight + gapHeight + VariableUnits(tier, 1f);
        if (full <= available)
        {
            BetSpotPixels = MathF.Min(tier.BetSpotRadiusMax, tier.BetSpotRadius + (available - full) * 0.25f) * Scale;
            return;
        }

        BetSpotPixels = tier.BetSpotRadius * Scale;
        Fit = Math.Clamp(FitFor(tier, available - fixedHeight - gapHeight), FitFloor, 1f);
        if (Fits(fixedHeight, gapHeight, available))
        {
            return;
        }

        GapFit = Math.Clamp((available - fixedHeight - VariableUnits(tier, Fit)) / gapHeight, GapFloor, 1f);
        if (Fits(fixedHeight, gapHeight, available))
        {
            return;
        }

        Fit = MathF.Max(MinimumFit, FitFor(tier, available - fixedHeight - gapHeight * GapFit));
        if (Fits(fixedHeight, gapHeight, available))
        {
            return;
        }

        GapFit = Math.Clamp((available - fixedHeight - VariableUnits(tier, Fit)) / gapHeight, 0f, GapFit);
        if (Fits(fixedHeight, gapHeight, available))
        {
            return;
        }

        Fit = MathF.Max(LastResortFit, FitFor(tier, available - fixedHeight - gapHeight * GapFit));
    }

    private bool Fits(float fixedHeight, float gapHeight, float available) =>
        fixedHeight + gapHeight * GapFit + VariableUnits(tier, Fit) <= available + FitTolerance;

    private static float FixedUnits(in Tier tier, int railCount, bool seated, float staggerUnits)
    {
        var top = tier.Compact
            ? DealerTopInset + tier.StateBandHeight
            : DealerPuckDrop + tier.DealerPuckRadius + PillHeight + tier.StateBandHeight;
        var rail = railCount > 0
            ? PlayingCards.HeightFor(tier.RailCardWidth) + BetPlateHeight + tier.RailPuckRadius
              + tier.PlateTop(tier.RailPuckRadius, 1f) + RailPlateHeight + staggerUnits
            : 0f;
        var totalReach = tier.Compact ? TotalCapsuleHeight - CompactTotalOverlap : TotalCapsuleHeight;
        var hero = seated
            ? totalReach + tier.BetSpotRadius * 2f + BetPlateHeight * 0.5f + tier.CapsuleHeight
            : 0f;
        return top + rail + hero;
    }

    private static float GapUnits(in Tier tier, int railCount, bool seated)
    {
        var top = tier.Compact ? StateGap : DealerFanGap + DealerTotalGap + StateGap;
        var rail = railCount > 0 ? ZoneGap + RailInnerGap * 2f + (railCount > 1 ? RailArcLift : 0f) : 0f;
        var cardsGap = tier.Compact ? 0f : CardsTotalGap;
        var hero = seated
            ? ZoneGap + RaiseReserve + cardsGap + TotalBetGap + BetCapsuleGap + CapsuleBottomInset
            : ZoneGap + GhostBottomInset;
        return top + rail + hero;
    }

    private static float VariableUnits(in Tier tier, float fit)
    {
        var dealer = PlayingCards.HeightFor(DealerCardWidth) * fit;
        if (tier.Compact)
        {
            dealer = MathF.Max(dealer, tier.DealerPuckRadius * 2f);
        }

        return dealer + PlayingCards.HeightFor(HeroCardWidth(1)) * fit;
    }

    private static float FitFor(in Tier tier, float budget)
    {
        var dealerHeight = PlayingCards.HeightFor(DealerCardWidth);
        var heroHeight = PlayingCards.HeightFor(HeroCardWidth(1));
        var fit = budget / (dealerHeight + heroHeight);
        var puckRow = tier.DealerPuckRadius * 2f;
        if (tier.Compact && dealerHeight * fit < puckRow)
        {
            fit = (budget - puckRow) / heroHeight;
        }

        return fit;
    }

    private void ComputeDealer(in Rect felt, float scale)
    {
        ShoeAnchor = new Vector2(felt.Max.X - ShoeInsetX * scale, felt.Min.Y + ShoeInsetY * scale);
        ShoePixels = ShoeCardWidth * scale;
        DealerPuckPixels = tier.DealerPuckRadius * scale;
        DealerCardPixels = DealerCardWidth * Fit * scale;
        var cardHalf = PlayingCards.HeightFor(DealerCardPixels) * 0.5f;
        var stateGap = StateGap * GapFit * scale;
        float stateTop;
        if (Compact)
        {
            var rowHalf = MathF.Max(cardHalf, DealerPuckPixels);
            DealerFanCenter = new Vector2(felt.Center.X, felt.Min.Y + DealerTopInset * scale + rowHalf);
            DealerPuck = new Vector2(felt.Min.X + SideInset * scale + DealerPuckPixels, DealerFanCenter.Y);
            SpeechArea = new Rect(DealerPuck, DealerPuck);
            DealerTotalY = DealerFanCenter.Y;
            stateTop = DealerFanCenter.Y + rowHalf + stateGap;
            var lift = ShoeLift * scale;
            ShoePixels = MathF.Min(ShoePixels, PlayingCards.WidthFor(rowHalf * 2f - lift));
            ShoeAnchor = new Vector2(ShoeAnchor.X, DealerFanCenter.Y - lift * 0.5f);
        }
        else
        {
            DealerPuck = new Vector2(felt.Center.X, felt.Min.Y + DealerPuckDrop * scale);
            var right = DealerPuck.X - DealerPuckPixels - SpeechGap * scale;
            var left = felt.Min.X + SideInset * 2f * scale;
            SpeechArea = new Rect(new Vector2(left, DealerPuck.Y - DealerPuckPixels),
                new Vector2(MathF.Max(left, right), DealerPuck.Y + DealerPuckPixels));
            var fanTop = DealerPuck.Y + DealerPuckPixels + DealerFanGap * GapFit * scale;
            DealerFanCenter = new Vector2(felt.Center.X, fanTop + cardHalf);
            var pillHalf = PillHeight * 0.5f * scale;
            DealerTotalY = DealerFanCenter.Y + cardHalf + DealerTotalGap * GapFit * scale + pillHalf;
            stateTop = DealerTotalY + pillHalf + stateGap;
        }

        StateBand = new Rect(new Vector2(felt.Min.X, stateTop),
            new Vector2(felt.Max.X, stateTop + tier.StateBandHeight * scale));
    }

    private void ComputeHero(in Rect felt, float scale)
    {
        var cardHalf = PlayingCards.HeightFor(HeroCardPixels(1)) * 0.5f;
        CapsulePixels = tier.CapsuleHeight * scale;
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
        var cardsBottom = Compact
            ? HeroTotalY - totalHalf + CompactTotalOverlap * scale
            : HeroTotalY - totalHalf - CardsTotalGap * GapFit * scale;
        HeroFanY = cardsBottom - cardHalf;
        HeroTop = HeroFanY - cardHalf - RaiseReserve * GapFit * scale;
    }

    private void ComputeRail(in Rect felt, float scale)
    {
        RailBetLift = PuckRadius + RailInnerGap * GapFit * scale + BetPlateHeight * 0.5f * scale;
        RailCardsLift = RailBetLift + BetPlateHeight * 0.5f * scale + RailInnerGap * GapFit * scale + RailHandHalf();
        RailPlateTop = tier.PlateTop(PuckRadius, scale);
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
