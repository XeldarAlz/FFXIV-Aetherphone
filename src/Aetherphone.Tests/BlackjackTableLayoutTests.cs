using System.Numerics;
using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class BlackjackTableLayoutTests
{
    private static readonly Rect Felt = new(new Vector2(0f, 0f), new Vector2(340f, 540f));

    [Fact]
    public void RailSlotsAreAPermutationForEverySeatIMightHold()
    {
        for (var mySeat = 0; mySeat < BlackjackRules.SeatCount; mySeat++)
        {
            var seen = new bool[BlackjackRules.SeatCount - 1];
            for (var seatIndex = 0; seatIndex < BlackjackRules.SeatCount; seatIndex++)
            {
                if (seatIndex == mySeat)
                {
                    continue;
                }

                var slot = BlackjackTableLayout.RailSlotOf(seatIndex, mySeat);
                Assert.InRange(slot, 0, BlackjackRules.SeatCount - 2);
                Assert.False(seen[slot]);
                seen[slot] = true;
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
    public void RailColumnsStayInsideTheFeltAndNeverOverlap()
    {
        for (var railCount = BlackjackRules.SeatCount - 1; railCount <= BlackjackRules.SeatCount; railCount++)
        {
            var columnWidth = BlackjackTableLayout.RailColumnWidth(Felt, railCount, 1f);
            var previousRight = float.NegativeInfinity;
            for (var slot = 0; slot < railCount; slot++)
            {
                var puck = BlackjackTableLayout.RailPuckCenter(Felt, slot, railCount, 1f);
                var columnLeft = puck.X - columnWidth * 0.5f;
                var columnRight = puck.X + columnWidth * 0.5f;
                Assert.True(columnLeft >= Felt.Min.X);
                Assert.True(columnRight <= Felt.Max.X);
                Assert.True(columnLeft >= previousRight - 0.001f);
                previousRight = columnRight;
            }
        }
    }

    [Fact]
    public void TheDealerZoneEndsAboveTheRailCards()
    {
        var fanCenter = BlackjackTableLayout.DealerFanCenter(Felt, 1f);
        var dealerBottom = fanCenter.Y + PlayingCards.HeightFor(BlackjackTableLayout.DealerCardWidth) * 0.5f
            + BlackjackTableLayout.DealerTotalDrop + 9f;
        for (var slot = 0; slot < BlackjackRules.SeatCount; slot++)
        {
            var puck = BlackjackTableLayout.RailPuckCenter(Felt, slot, BlackjackRules.SeatCount, 1f);
            var railCardsTop = puck.Y - BlackjackTableLayout.RailCardsLift
                - PlayingCards.HeightFor(BlackjackTableLayout.RailCardWidth) * 0.5f;
            Assert.True(dealerBottom < railCardsTop);
        }
    }

    [Fact]
    public void TheSeatArcBowsTowardThePlayer()
    {
        var edge = BlackjackTableLayout.RailPuckCenter(Felt, 0, BlackjackRules.SeatCount, 1f);
        var middle = BlackjackTableLayout.RailPuckCenter(Felt, 2, BlackjackRules.SeatCount, 1f);
        Assert.True(edge.Y < middle.Y);
        Assert.True(BlackjackTableLayout.RailPuckRadius * 2f >= 44f);
    }

    [Fact]
    public void TheRailPlateEndsAboveTheHeroCards()
    {
        var railBottom = BlackjackTableLayout.RailPuckY(Felt) + BlackjackTableLayout.RailPuckRadius
            + BlackjackTableLayout.RailPlateGap + BlackjackTableLayout.RailPlateHeight;
        var heroCardsTop = BlackjackTableLayout.HeroFanY(Felt)
            - PlayingCards.HeightFor(BlackjackTableLayout.HeroCardWidth(1)) * 0.5f;
        Assert.True(railBottom < heroCardsTop);
    }

    [Fact]
    public void TheHeroBetRowEndsAboveTheCapsule()
    {
        var spot = BlackjackTableLayout.HeroBetSpot(Felt, 1f);
        var rowBottom = spot.Y + BlackjackTableLayout.MainSpotRadius + 9f;
        var capsuleTop = Felt.Max.Y - BlackjackTableLayout.CapsuleDrop - BlackjackTableLayout.CapsuleHeight * 0.5f;
        Assert.True(rowBottom < capsuleTop);
    }

    [Fact]
    public void TheSideSpotsFlankTheMainSpotWithoutTouching()
    {
        var main = BlackjackTableLayout.HeroBetSpot(Felt, 1f);
        var pairs = BlackjackTableLayout.SideSpot(Felt, BlackjackSideBet.PerfectPairs, 1f);
        var three = BlackjackTableLayout.SideSpot(Felt, BlackjackSideBet.TwentyOnePlusThree, 1f);
        Assert.True(pairs.X < main.X && three.X > main.X);
        Assert.True(main.X - pairs.X >= BlackjackTableLayout.SideSpotRadius + BlackjackTableLayout.MainSpotRadius);
        Assert.True(pairs.X - BlackjackTableLayout.SideSpotRadius > Felt.Min.X);
        Assert.True(three.X + BlackjackTableLayout.SideSpotRadius < Felt.Max.X);
    }

    [Fact]
    public void TheHeroTotalPillClearsTheChipColumn()
    {
        const float columnSink = 8f;
        var pillBottom = BlackjackTableLayout.HeroTotalDrop + 11f;
        var chipColumnTop = BlackjackTableLayout.HeroChipsDrop + columnSink
            - (BlackjackTableArt.ChipColumnCapacity - 1) * 4.2f - 7f;
        Assert.True(pillBottom < chipColumnTop);
    }

    [Fact]
    public void TheRailOutcomeBadgeStaysBetweenTheFanTopAndTheBetPlate()
    {
        const float badgeHalfHeight = 11f;
        var badgeBottom = -BlackjackTableLayout.RailBadgeLift + badgeHalfHeight;
        var betPlateTop = -BlackjackTableLayout.RailBetLift - 8.5f;
        Assert.True(badgeBottom < betPlateTop);

        var badgeTop = -BlackjackTableLayout.RailBadgeLift - badgeHalfHeight;
        var fanTop = -BlackjackTableLayout.RailCardsLift
            - PlayingCards.HeightFor(BlackjackTableLayout.RailCardWidth) * 0.5f;
        Assert.True(badgeTop > fanTop);
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
        var single = BlackjackTableLayout.HeroHandCenter(Felt, 1, 0, 1f);
        Assert.Equal(Felt.Center.X, single.X, 3);

        var leftHand = BlackjackTableLayout.HeroHandCenter(Felt, 2, 0, 1f);
        var rightHand = BlackjackTableLayout.HeroHandCenter(Felt, 2, 1, 1f);
        Assert.True(leftHand.X < rightHand.X);
        Assert.Equal(Felt.Center.X - leftHand.X, rightHand.X - Felt.Center.X, 3);
    }
}
