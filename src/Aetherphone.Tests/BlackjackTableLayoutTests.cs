using System.Numerics;
using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class BlackjackTableLayoutTests
{
    private const float Epsilon = 0.01f;
    private const float PlateWidthAllowance = 72f;
    private const float ShoeCardWidth = 22f;
    private const float ShoeLift = 2.5f;

    private const float FloorSlack = 60f;

    private static readonly float[] Scales = { 0.75f, 1f, 1.5f };

    private static readonly float[] FloorScales = { 0.75f, 1f, 1.25f, 1.5f };

    private static readonly float[] PhoneWidths = { 256f, 272f, 300f, 336f };

    public static TheoryData<float> ScaleData => new() { 0.75f, 1f, 1.25f, 1.5f };

    private static readonly Vector2[] FeltSizes =
    {
        new(272f, 489f),
        new(272f, 437f),
        new(272f, 415f),
        new(312f, 520f),
        new(336f, 560f),
    };

    public static TheoryData<float, float, float, int, bool> Tables()
    {
        var data = new TheoryData<float, float, float, int, bool>();
        for (var scaleIndex = 0; scaleIndex < Scales.Length; scaleIndex++)
        {
            for (var sizeIndex = 0; sizeIndex < FeltSizes.Length; sizeIndex++)
            {
                var size = FeltSizes[sizeIndex];
                data.Add(Scales[scaleIndex], size.X, size.Y, BlackjackRules.SeatCount - 1, true);
                data.Add(Scales[scaleIndex], size.X, size.Y, BlackjackRules.SeatCount, false);
                data.Add(Scales[scaleIndex], size.X, size.Y, 2, true);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Tables))]
    public void TheZonesStackTopToBottomWithoutTouching(float scale, float width, float height, int railCount,
        bool seated)
    {
        var layout = Compute(scale, width, height, railCount, seated);
        var felt = layout.Felt;
        var dealerCardHalf = PlayingCards.HeightFor(layout.DealerCardPixels) * 0.5f;
        Assert.True(layout.DealerPuck.Y - layout.DealerPuckPixels >= felt.Min.Y - Epsilon);
        Assert.True(layout.DealerPuck.Y + layout.DealerPuckPixels < layout.DealerFanCenter.Y - dealerCardHalf);
        Assert.True(layout.DealerFanCenter.Y + dealerCardHalf
            < layout.DealerTotalY - BlackjackTableLayout.PillHeight * 0.5f * scale + Epsilon);
        Assert.True(layout.DealerTotalY + BlackjackTableLayout.PillHeight * 0.5f * scale < layout.StateBand.Min.Y);
        Assert.True(layout.StateBand.Max.Y < layout.RailTop + Epsilon);
        Assert.True(layout.RailBottom < layout.HeroTop + Epsilon);
        Assert.True(layout.HeroTop >= felt.Min.Y);
        Assert.True(layout.Fit >= BlackjackTableLayout.MinimumFit);
    }

    [Theory]
    [MemberData(nameof(Tables))]
    public void TheHeroZoneStacksCardsTotalBetsAndCapsuleWithGaps(float scale, float width, float height,
        int railCount, bool seated)
    {
        var layout = Compute(scale, width, height, railCount, seated);
        var felt = layout.Felt;
        var cardHalf = PlayingCards.HeightFor(layout.HeroCardPixels(1)) * 0.5f;
        if (!seated)
        {
            Assert.True(layout.HeroTop <= layout.HeroFanY - cardHalf + Epsilon);
            Assert.True(layout.HeroFanY + cardHalf <= felt.Max.Y);
            return;
        }

        Assert.True(layout.HeroTop <= layout.HeroFanY - cardHalf - layout.RaisePixels + Epsilon);
        var totalHalf = BlackjackTableLayout.TotalCapsuleHeight * 0.5f * scale;
        var plateHalf = BlackjackTableLayout.BetPlateHeight * 0.5f * scale;
        var capsuleHalf = layout.CapsulePixels * 0.5f;
        Assert.True(layout.HeroFanY + cardHalf < layout.HeroTotalY - totalHalf);
        Assert.True(layout.HeroTotalY + totalHalf < layout.BetSpot.Y - layout.BetSpotPixels);
        Assert.True(layout.BetPlateY + plateHalf < layout.CapsuleCenter.Y - capsuleHalf);
        Assert.True(layout.CapsuleCenter.Y + capsuleHalf <= felt.Max.Y + Epsilon);
        Assert.True(layout.BetSpotPixels >= BlackjackTableLayout.BetSpotRadius * scale - Epsilon);
        Assert.True(layout.CapsulePuckPixels * 2f < layout.CapsulePixels);
    }

    [Theory]
    [MemberData(nameof(Tables))]
    public void TheSideSpotsFlankTheMainBetAndItsAmountWithoutTouching(float scale, float width, float height,
        int railCount, bool seated)
    {
        if (!seated)
        {
            return;
        }

        var layout = Compute(scale, width, height, railCount, seated);
        var felt = layout.Felt;
        var radius = layout.BetSpotPixels;
        var pairs = layout.SideSpot(BlackjackSideBet.PerfectPairs);
        var three = layout.SideSpot(BlackjackSideBet.TwentyOnePlusThree);
        Assert.Equal(layout.BetSpot.Y, pairs.Y, 3);
        Assert.True(pairs.X < layout.BetSpot.X && three.X > layout.BetSpot.X);
        Assert.True(layout.BetSpot.X - pairs.X - radius > radius);
        Assert.True(layout.BetSpot.X - pairs.X - radius > layout.MainPlateMaxWidth * 0.5f);
        Assert.True(layout.MainPlateMaxWidth >= PlateWidthAllowance * scale);
        Assert.True(pairs.X - radius > felt.Min.X);
        Assert.True(three.X + radius < felt.Max.X);
    }

    [Theory]
    [MemberData(nameof(Tables))]
    public void RailSeatsNeverTouchEachOtherOrLeaveTheFelt(float scale, float width, float height, int railCount,
        bool seated)
    {
        var layout = Compute(scale, width, height, railCount, seated);
        var felt = layout.Felt;
        Assert.True(layout.SpotRadius >= BlackjackTableLayout.SpotRadiusMin * scale - Epsilon);
        Assert.True(layout.PuckRadius <= layout.SpotRadius + Epsilon);
        for (var slot = 0; slot < railCount; slot++)
        {
            var puck = layout.RailPuckCenter(slot);
            var seat = layout.RailSeatRect(slot);
            Assert.True(puck.X - layout.SpotRadius >= felt.Min.X - Epsilon);
            Assert.True(puck.X + layout.SpotRadius <= felt.Max.X + Epsilon);
            Assert.True(puck.Y - layout.SpotRadius >= layout.StateBand.Max.Y);
            Assert.True(seat.Min.Y >= layout.StateBand.Max.Y);
            Assert.True(seat.Max.Y <= layout.HeroTop + Epsilon);
            for (var other = slot + 1; other < railCount; other++)
            {
                var otherPuck = layout.RailPuckCenter(other);
                Assert.True(Vector2.Distance(puck, otherPuck) >= layout.SpotRadius * 2f - Epsilon);
                Assert.False(Overlaps(layout.RailPlateRect(slot), layout.RailPlateRect(other)));
                Assert.False(Overlaps(layout.RailPlateRect(slot), PuckBox(otherPuck, layout.PuckRadius)));
            }
        }
    }

    [Theory]
    [MemberData(nameof(Tables))]
    public void TheShoeStaysClearOfTheDealerCardsAndTheRail(float scale, float width, float height, int railCount,
        bool seated)
    {
        var layout = Compute(scale, width, height, railCount, seated);
        var shoeHalf = new Vector2(ShoeCardWidth, PlayingCards.HeightFor(ShoeCardWidth) + ShoeLift * 2f) * 0.5f
            * scale;
        var shoe = new Rect(layout.ShoeAnchor - shoeHalf, layout.ShoeAnchor + shoeHalf);
        var fanHalf = new Vector2(layout.Felt.Width * BlackjackTableLayout.DealerFanShare,
            PlayingCards.HeightFor(layout.DealerCardPixels)) * 0.5f;
        Assert.False(Overlaps(shoe, new Rect(layout.DealerFanCenter - fanHalf, layout.DealerFanCenter + fanHalf)));
        Assert.True(shoe.Max.Y < layout.StateBand.Min.Y);
        for (var slot = 0; slot < railCount; slot++)
        {
            Assert.False(Overlaps(shoe, layout.RailSeatRect(slot)));
        }
    }

    [Fact]
    public void TheOwnersPhoneKeepsReadableCards()
    {
        var layout = Compute(1f, 272f, 489f, BlackjackRules.SeatCount - 1, true);
        Assert.True(layout.Fit >= 0.7f);
        Assert.Equal(1f, layout.GapFit);
    }

    [Fact]
    public void TheSeatArcBowsTowardThePlayer()
    {
        var layout = Compute(1f, 272f, 489f, BlackjackRules.SeatCount - 1, true);
        var edge = layout.RailPuckCenter(0);
        var middle = layout.RailPuckCenter(2);
        var last = layout.RailPuckCenter(4);
        Assert.True(edge.Y < middle.Y);
        Assert.Equal(edge.Y, last.Y, 3);
        Assert.True(layout.PuckRadius * 2f >= 44f);
    }

    [Fact]
    public void SpotsShrinkAsTheRailFills()
    {
        var sparse = Compute(1f, 312f, 520f, 2, true);
        var full = Compute(1f, 312f, 520f, BlackjackRules.SeatCount, false);
        Assert.True(sparse.SpotRadius > full.SpotRadius);
        Assert.Equal(BlackjackTableLayout.SpotRadiusMax, sparse.SpotRadius, 3);
    }

    [Fact]
    public void ACrowdedNarrowRailStaggersItsSpots()
    {
        var layout = Compute(1f, 272f, 489f, BlackjackRules.SeatCount, false);
        Assert.True(layout.Stagger > 0f);
        Assert.True(layout.RailPuckCenter(1).Y > layout.RailPuckCenter(0).Y);
    }

    [Fact]
    public void RailSlotsAreAPermutationForEverySeatIMightHold()
    {
        for (var limit = 2; limit <= BlackjackRules.SeatCount; limit++)
        {
            for (var mySeat = 0; mySeat < limit; mySeat++)
            {
                var railCount = BlackjackTableLayout.RailSeatCount(mySeat, limit);
                Assert.Equal(limit - 1, railCount);
                var seen = new bool[railCount];
                for (var seatIndex = 0; seatIndex < limit; seatIndex++)
                {
                    if (seatIndex == mySeat)
                    {
                        continue;
                    }

                    var slot = BlackjackTableLayout.RailSlotOf(seatIndex, mySeat, limit);
                    Assert.InRange(slot, 0, railCount - 1);
                    Assert.False(seen[slot]);
                    seen[slot] = true;
                }
            }
        }
    }

    [Fact]
    public void ASpectatorSeesTheSeatsInServerOrder()
    {
        Assert.Equal(BlackjackRules.SeatCount, BlackjackTableLayout.RailSeatCount(-1));
        for (var seatIndex = 0; seatIndex < BlackjackRules.SeatCount; seatIndex++)
        {
            Assert.Equal(seatIndex, BlackjackTableLayout.RailSlotOf(seatIndex, -1));
        }
    }

    [Fact]
    public void TheSeatsToMyLeftRunLeftToRightAroundTheTable()
    {
        const int mySeat = 2;
        Assert.Equal(0, BlackjackTableLayout.RailSlotOf(3, mySeat));
        Assert.Equal(1, BlackjackTableLayout.RailSlotOf(4, mySeat));
        Assert.Equal(2, BlackjackTableLayout.RailSlotOf(5, mySeat));
        Assert.Equal(3, BlackjackTableLayout.RailSlotOf(0, mySeat));
        Assert.Equal(4, BlackjackTableLayout.RailSlotOf(1, mySeat));
    }

    [Fact]
    public void SqueezedFansNeverExceedTheirColumn()
    {
        const float cardWidth = 18f;
        const float maxWidth = 60f;
        const int cardCount = 8;
        var step = BlackjackTableLayout.FanStep(cardWidth, cardCount, maxWidth);
        Assert.True(cardWidth + step * (cardCount - 1) <= maxWidth + 0.001f);
        Assert.Equal(0f, BlackjackTableLayout.FanStep(cardWidth, 1, maxWidth));
    }

    [Fact]
    public void HeroHandsSpreadSymmetricallyAroundTheCentre()
    {
        var layout = Compute(1f, 272f, 489f, BlackjackRules.SeatCount - 1, true);
        var single = layout.HeroHandCenter(1, 0);
        Assert.Equal(layout.Felt.Center.X, single.X, 3);

        var leftHand = layout.HeroHandCenter(2, 0);
        var rightHand = layout.HeroHandCenter(2, 1);
        Assert.True(leftHand.X < rightHand.X);
        Assert.Equal(layout.Felt.Center.X - leftHand.X, rightHand.X - layout.Felt.Center.X, 3);
    }

    [Theory]
    [MemberData(nameof(Tables))]
    public void SideBetCaptionsStayClearOfTheCentreColumnAndInsideTheFelt(float scale, float width, float height,
        int railCount, bool seated)
    {
        var layout = Compute(scale, width, height, railCount, seated);
        if (!seated)
        {
            return;
        }

        var half = layout.SideCaptionWidth * 0.5f;
        var pairs = layout.SideSpot(BlackjackSideBet.PerfectPairs);
        var three = layout.SideSpot(BlackjackSideBet.TwentyOnePlusThree);
        var ghostHalf = layout.HeroCardPixels(1) * 0.75f;
        Assert.True(pairs.X + half <= layout.Felt.Center.X - ghostHalf + Epsilon);
        Assert.True(three.X - half >= layout.Felt.Center.X + ghostHalf - Epsilon);
        Assert.True(pairs.X - half >= layout.Felt.Min.X - Epsilon);
        Assert.True(three.X + half <= layout.Felt.Max.X + Epsilon);
        Assert.True(layout.SideCaptionWidth >= 56f * scale);
    }

    public static TheoryData<float, float, int, bool> FloorTables()
    {
        var data = new TheoryData<float, float, int, bool>();
        for (var scaleIndex = 0; scaleIndex < FloorScales.Length; scaleIndex++)
        {
            for (var widthIndex = 0; widthIndex < PhoneWidths.Length; widthIndex++)
            {
                for (var railCount = 0; railCount <= BlackjackRules.SeatCount; railCount++)
                {
                    data.Add(FloorScales[scaleIndex], PhoneWidths[widthIndex], railCount, false);
                    if (railCount < BlackjackRules.SeatCount)
                    {
                        data.Add(FloorScales[scaleIndex], PhoneWidths[widthIndex], railCount, true);
                    }
                }
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(FloorTables))]
    public void EverySeatCountLaysOutWithoutOverlapDownToTheFeltFloor(float scale, float width, int railCount,
        bool seated)
    {
        var floor = BlackjackTableLayout.MinimumHeight(width * scale, railCount, seated, scale) / scale;
        for (var extra = 0f; extra <= FloorSlack; extra += FloorSlack * 0.5f)
        {
            var layout = Compute(scale, width, floor + extra, railCount, seated);
            AssertZonesStack(layout, railCount, seated);
            AssertRailSeatsStayApart(layout, railCount);
            if (!seated)
            {
                continue;
            }

            for (var handCount = 1; handCount <= BlackjackRules.MaxHandsPerSeat; handCount++)
            {
                AssertHeroHandsClearTheRail(layout, railCount, handCount);
            }

            AssertBetSpotsClearTheRailAndTheTotals(layout, railCount);
        }
    }

    [Theory]
    [MemberData(nameof(FloorTables))]
    public void SideStakesHangUnderEachSeatPlateInsideItsColumn(float scale, float width, int railCount,
        bool seated)
    {
        var floor = BlackjackTableLayout.MinimumHeight(width * scale, railCount, seated, scale) / scale;
        for (var extra = 0f; extra <= FloorSlack; extra += FloorSlack * 0.5f)
        {
            var layout = Compute(scale, width, floor + extra, railCount, seated);
            for (var slot = 0; slot < railCount; slot++)
            {
                var plate = layout.RailPlateRect(slot);
                var pairs = layout.RailSideStakeRect(slot, BlackjackSideBet.PerfectPairs, true);
                var three = layout.RailSideStakeRect(slot, BlackjackSideBet.TwentyOnePlusThree, true);
                var single = layout.RailSideStakeRect(slot, BlackjackSideBet.TwentyOnePlusThree, false);
                Assert.Equal(BlackjackTableLayout.SideStakeHeight * scale, pairs.Height, 3);
                Assert.Equal(pairs.Height, three.Height, 3);
                Assert.Equal(pairs.Min.Y, three.Min.Y, 3);
                Assert.Equal(pairs.Width, three.Width, 3);
                Assert.True(pairs.Min.X >= plate.Min.X - Epsilon);
                Assert.True(pairs.Max.X < three.Min.X);
                Assert.True(three.Max.X <= plate.Max.X + Epsilon);
                Assert.Equal(plate.Min.X, single.Min.X, 3);
                Assert.Equal(plate.Max.X, single.Max.X, 3);
                var attached = plate.Max.Y - BlackjackTableLayout.SideStakeOverlap * scale;
                Assert.True(pairs.Min.Y <= attached + Epsilon);
                Assert.True(pairs.Min.Y >= attached - BlackjackTableLayout.SideStakeSlide * scale - Epsilon);
                var hangs = pairs.Max.Y - plate.Max.Y;
                Assert.True(hangs >= pairs.Height - BlackjackTableLayout.SideStakeOverlap * scale
                    - BlackjackTableLayout.SideStakeSlide * scale - Epsilon);
                if (layout.SideStakeFloor - attached >= pairs.Height)
                {
                    Assert.Equal(attached, pairs.Min.Y, 3);
                    Assert.True(pairs.Max.Y <= layout.SideStakeFloor + Epsilon);
                }
            }
        }
    }

    [Fact]
    public void TheOwnersPhoneKeepsSideStakesOffTheStackLineAndTheHeroCards()
    {
        var layout = Compute(1f, 272f, 489f, BlackjackRules.SeatCount - 1, true);
        for (var slot = 0; slot < BlackjackRules.SeatCount - 1; slot++)
        {
            var plate = layout.RailPlateRect(slot);
            var stake = layout.RailSideStakeRect(slot, BlackjackSideBet.PerfectPairs, true);
            Assert.Equal(plate.Max.Y - BlackjackTableLayout.SideStakeOverlap, stake.Min.Y, 3);
            Assert.True(stake.Max.Y <= layout.SideStakeFloor + Epsilon);
            Assert.True(stake.Width >= 20f);
        }
    }

    [Theory]
    [MemberData(nameof(ScaleData))]
    public void TimeBankPipsSitInsideTheTurnRingAtTheCrownOfThePuck(float scale)
    {
        var puck = new Vector2(100f, 200f);
        var pucks = new[]
        {
            (BlackjackTableLayout.RailPuckRadius, BlackjackTableLayout.RailRingGap),
            (BlackjackTableLayout.CapsulePuckRadius, BlackjackTableLayout.CapsuleRingGap),
        };
        for (var index = 0; index < pucks.Length; index++)
        {
            var radius = pucks[index].Item1 * scale;
            var ring = radius + pucks[index].Item2 * scale;
            var center = BlackjackTableLayout.TimeBankPips(puck, radius, scale);
            var halfHeight = BlackjackTableArt.TimeBankPipHalfHeight(scale);
            var halfWidth = BlackjackTableArt.TimeBankPipHalfWidth(CasinoHostingRules.TimeBankUses, scale);
            Assert.Equal(puck.X, center.X, 3);
            Assert.True(center.Y + halfHeight < puck.Y);
            Assert.True(center.Y - halfHeight >= puck.Y - radius - Epsilon);
            var cornerRise = puck.Y - (center.Y - halfHeight);
            Assert.True(MathF.Sqrt(halfWidth * halfWidth + cornerRise * cornerRise) <= ring + Epsilon);
            Assert.True(halfWidth * 2f <= radius * 2f);
        }
    }

    [Theory]
    [MemberData(nameof(ScaleData))]
    public void TimeBankPipsClearTheBalanceTitleOfASharkSeat(float scale)
    {
        var puck = new Vector2(100f, 200f);
        var radius = BlackjackTableLayout.RailPuckRadius * scale;
        var title = StatusTitle.For(StatusTitle.Shark);
        Assert.NotEqual(BalanceTitle.None, title);
        var badgeHalf = StatusTitle.PillHeight(title, scale) * 0.5f;
        Assert.True(badgeHalf > 0f);
        var badgeCenter = BlackjackTableLayout.RailTitleCenter(puck, radius, scale);
        var badgeTop = badgeCenter.Y - badgeHalf;
        Assert.True(badgeCenter.Y + badgeHalf > puck.Y + radius);
        var pips = BlackjackTableLayout.TimeBankPips(puck, radius, scale);
        var pipBottom = pips.Y + BlackjackTableArt.TimeBankPipHalfHeight(scale);
        Assert.True(pipBottom < badgeTop);
        Assert.True(pipBottom < puck.Y);
    }

    [Fact]
    public void TheOwnersHostedGilTableKeepsItsSeatsOffTheSplitHands()
    {
        const float scale = 1f;
        const float width = 285f;
        const float safeHeight = 465f;
        const float rowGap = 6f;
        const float ledgerWidth = 75f;
        const int railCount = BlackjackRules.SeatCount - 1;
        var floor = BlackjackTableLayout.MinimumHeight(width, railCount, true, scale);
        var buttonRow = Button.LargeHeight + rowGap;
        var bankAndRules = rowGap + StageText.MinimumCapsuleHeight + rowGap + StageText.MinimumCapsuleHeight * 1.5f;
        var mode = HostedInfoLayout.Mode(bankAndRules, rowGap + StageText.MinimumCapsuleHeight,
            safeHeight - rowGap - Button.LargeHeight - floor, width - ledgerWidth - rowGap, scale);
        Assert.Equal(HostedInfoMode.Beside, mode);

        var feltHeight = safeHeight - buttonRow;
        Assert.True(feltHeight >= floor);
        var layout = Compute(scale, width, feltHeight, railCount, true);
        AssertZonesStack(layout, railCount, true);
        AssertRailSeatsStayApart(layout, railCount);
        AssertHeroHandsClearTheRail(layout, railCount, 2);
    }

    [Fact]
    public void BelowItsFloorTheRailWouldRunIntoTheHero()
    {
        const int railCount = BlackjackRules.SeatCount - 1;
        var floor = BlackjackTableLayout.MinimumHeight(285f, railCount, true, 1f);
        var squeezed = Compute(1f, 285f, floor - 40f, railCount, true);

        Assert.True(squeezed.RailBottom > squeezed.HeroTop);
    }

    [Theory]
    [MemberData(nameof(ScaleData))]
    public void AKnockOrANoticeOverlaysTheFeltRatherThanSqueezeItUnderItsFloor(float scale)
    {
        var floor = 400f * scale;
        var safeBottom = 520f * scale;
        var rowBottom = 60f * scale;
        var knock = 120f * scale;

        Assert.Equal(rowBottom + knock, BlackjackTableLayout.FeltTop(rowBottom, knock, 2000f * scale, floor), 3);
        var feltTop = BlackjackTableLayout.FeltTop(rowBottom, knock, safeBottom, floor);
        Assert.Equal(safeBottom - floor, feltTop, 3);
        Assert.True(feltTop >= rowBottom);
        Assert.Equal(rowBottom, BlackjackTableLayout.FeltTop(rowBottom, knock, rowBottom + floor * 0.5f, floor), 3);

        var notice = 80f * scale;
        Assert.Equal(safeBottom, BlackjackTableLayout.FeltBottom(feltTop, safeBottom, notice, floor), 3);
        Assert.Equal(safeBottom - notice, BlackjackTableLayout.FeltBottom(0f, safeBottom, notice, floor), 3);
        Assert.Equal(safeBottom, BlackjackTableLayout.FeltBottom(0f, safeBottom, 0f, floor), 3);
    }

    [Theory]
    [MemberData(nameof(ScaleData))]
    public void TheHostedInfoYieldsItsBlocksBeforeTheFeltLosesItsFloor(float scale)
    {
        var blocks = 84f * scale;
        var line = 34f * scale;
        var beside = HostedInfoLayout.MinimumBesideWidth * scale;

        Assert.Equal(HostedInfoMode.Blocks, HostedInfoLayout.Mode(blocks, line, blocks, 0f, scale));
        Assert.Equal(HostedInfoMode.Beside, HostedInfoLayout.Mode(blocks, line, blocks - 1f, beside, scale));
        Assert.Equal(HostedInfoMode.Line, HostedInfoLayout.Mode(blocks, line, line, beside - 1f, scale));
        Assert.Equal(HostedInfoMode.Hidden, HostedInfoLayout.Mode(blocks, line, line - 1f, beside - 1f, scale));
        Assert.Equal(HostedInfoMode.Hidden, HostedInfoLayout.Mode(blocks, line, -50f * scale, 0f, scale));
    }

    private static void AssertZonesStack(BlackjackTableLayout layout, int railCount, bool seated)
    {
        var felt = layout.Felt;
        var scale = layout.Scale;
        var dealerCardHalf = PlayingCards.HeightFor(layout.DealerCardPixels) * 0.5f;
        var pillHalf = BlackjackTableLayout.PillHeight * 0.5f * scale;
        Assert.True(layout.DealerPuck.Y - layout.DealerPuckPixels >= felt.Min.Y - Epsilon);
        Assert.True(layout.DealerPuck.Y + layout.DealerPuckPixels <= layout.DealerFanCenter.Y - dealerCardHalf
            + Epsilon);
        Assert.True(layout.DealerTotalY + pillHalf <= layout.StateBand.Min.Y + Epsilon);
        if (railCount > 0)
        {
            Assert.True(layout.StateBand.Max.Y <= layout.RailTop + Epsilon);
            Assert.True(layout.RailBottom <= layout.HeroTop + Epsilon);
        }
        else
        {
            Assert.True(layout.StateBand.Max.Y <= layout.HeroTop + Epsilon);
        }

        var cardHalf = PlayingCards.HeightFor(layout.HeroCardPixels(1)) * 0.5f;
        Assert.True(layout.HeroFanY - cardHalf >= layout.HeroTop - Epsilon);
        if (seated)
        {
            Assert.True(layout.CapsuleCenter.Y + layout.CapsulePixels * 0.5f <= felt.Max.Y + Epsilon);
            return;
        }

        Assert.True(layout.HeroFanY + cardHalf <= felt.Max.Y + Epsilon);
    }

    private static void AssertRailSeatsStayApart(BlackjackTableLayout layout, int railCount)
    {
        var felt = layout.Felt;
        var scale = layout.Scale;
        for (var slot = 0; slot < railCount; slot++)
        {
            var puck = layout.RailPuckCenter(slot);
            var seat = layout.RailSeatRect(slot);
            var plate = layout.RailPlateRect(slot);
            var betPlate = new Rect(new Vector2(seat.Min.X, puck.Y - layout.RailBetLift - BetPlateHalf(scale)),
                new Vector2(seat.Max.X, puck.Y - layout.RailBetLift + BetPlateHalf(scale)));
            var cards = new Rect(new Vector2(seat.Min.X, seat.Min.Y),
                new Vector2(seat.Max.X, puck.Y - layout.RailCardsLift + RailHandHalf(scale)));
            Assert.True(seat.Min.X >= felt.Min.X - Epsilon && seat.Max.X <= felt.Max.X + Epsilon);
            Assert.True(puck.X - layout.SpotRadius >= felt.Min.X - Epsilon);
            Assert.True(puck.X + layout.SpotRadius <= felt.Max.X + Epsilon);
            Assert.True(seat.Min.Y >= layout.StateBand.Max.Y - Epsilon);
            Assert.True(seat.Max.Y <= layout.HeroTop + Epsilon);
            Assert.True(Contains(seat, plate) && Contains(seat, betPlate) && Contains(seat, cards));
            Assert.False(Overlaps(cards, betPlate));
            Assert.False(Overlaps(betPlate, PuckBox(puck, layout.PuckRadius)));
            Assert.False(Overlaps(plate, PuckBox(puck, layout.PuckRadius)));
            for (var other = slot + 1; other < railCount; other++)
            {
                Assert.False(Overlaps(seat, layout.RailSeatRect(other)));
                Assert.True(Vector2.Distance(puck, layout.RailPuckCenter(other)) >= layout.SpotRadius * 2f - Epsilon);
            }
        }
    }

    private static void AssertHeroHandsClearTheRail(BlackjackTableLayout layout, int railCount, int handCount)
    {
        var felt = layout.Felt;
        var scale = layout.Scale;
        var cardHalf = PlayingCards.HeightFor(layout.HeroCardPixels(handCount)) * 0.5f;
        var fanHalf = (layout.HeroSlotWidth(handCount) - BlackjackTableLayout.HeroSlotPad * scale) * 0.5f;
        for (var handIndex = 0; handIndex < handCount; handIndex++)
        {
            var center = layout.HeroHandCenter(handCount, handIndex);
            var fan = new Rect(new Vector2(center.X - fanHalf, center.Y - cardHalf - layout.RaisePixels),
                new Vector2(center.X + fanHalf, center.Y + cardHalf));
            Assert.True(fan.Min.X >= felt.Min.X - Epsilon && fan.Max.X <= felt.Max.X + Epsilon);
            Assert.True(fan.Min.Y >= layout.HeroTop - Epsilon);
            for (var slot = 0; slot < railCount; slot++)
            {
                Assert.False(Overlaps(fan, layout.RailSeatRect(slot)));
            }

            if (handIndex > 0)
            {
                var previous = layout.HeroHandCenter(handCount, handIndex - 1);
                Assert.True(center.X - previous.X >= fanHalf * 2f - Epsilon);
            }
        }
    }

    private static void AssertBetSpotsClearTheRailAndTheTotals(BlackjackTableLayout layout, int railCount)
    {
        var scale = layout.Scale;
        var radius = layout.BetSpotPixels;
        var totalsBottom = layout.HeroTotalY + BlackjackTableLayout.TotalCapsuleHeight * 0.5f * scale;
        var spots = new[]
        {
            layout.BetSpot, layout.SideSpot(BlackjackSideBet.PerfectPairs),
            layout.SideSpot(BlackjackSideBet.TwentyOnePlusThree),
        };
        for (var index = 0; index < spots.Length; index++)
        {
            var spot = PuckBox(spots[index], radius);
            Assert.True(spot.Min.Y >= totalsBottom - Epsilon);
            for (var slot = 0; slot < railCount; slot++)
            {
                Assert.False(Overlaps(spot, layout.RailSeatRect(slot)));
            }
        }
    }

    private static float BetPlateHalf(float scale) => BlackjackTableLayout.BetPlateHeight * 0.5f * scale;

    private static float RailHandHalf(float scale) =>
        PlayingCards.HeightFor(BlackjackTableLayout.RailCardWidth * scale) * 0.5f;

    private static bool Contains(in Rect outer, in Rect inner)
    {
        return inner.Min.X >= outer.Min.X - Epsilon && inner.Max.X <= outer.Max.X + Epsilon
            && inner.Min.Y >= outer.Min.Y - Epsilon && inner.Max.Y <= outer.Max.Y + Epsilon;
    }

    private static BlackjackTableLayout Compute(float scale, float width, float height, int railCount, bool seated)
    {
        var origin = new Vector2(40f, 120f);
        var felt = new Rect(origin, origin + new Vector2(width, height) * scale);
        var layout = new BlackjackTableLayout();
        layout.Compute(felt, railCount, seated, scale);
        return layout;
    }

    private static Rect PuckBox(Vector2 center, float radius)
    {
        var half = new Vector2(radius, radius);
        return new Rect(center - half, center + half);
    }

    private static bool Overlaps(in Rect first, in Rect second)
    {
        return first.Min.X < second.Max.X - Epsilon && second.Min.X < first.Max.X - Epsilon
            && first.Min.Y < second.Max.Y - Epsilon && second.Min.Y < first.Max.Y - Epsilon;
    }
}
