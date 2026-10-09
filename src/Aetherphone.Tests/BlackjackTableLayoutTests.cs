using System.Numerics;
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

    private static readonly float[] Scales = { 0.75f, 1f, 1.5f };

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
