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
    private const float TournamentFeltHeight = 330f;
    private const float RoomyFeltHeight = 520f;
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
        new(272f, 330f),
        new(272f, 300f),
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
        AssertZonesStack(layout, railCount, seated);
        Assert.True(layout.HeroTop >= layout.Felt.Min.Y);
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
        var totalHalf = layout.TotalCapsulePixels * 0.5f;
        var plateHalf = BlackjackTableLayout.BetPlateHeight * 0.5f * scale;
        var capsuleHalf = layout.CapsulePixels * 0.5f;
        var cardsBottom = layout.HeroFanY + cardHalf;
        var totalTop = layout.HeroTotalY - totalHalf;
        if (layout.Compact)
        {
            Assert.Equal(BlackjackTableLayout.CompactTotalOverlap * scale, cardsBottom - totalTop, 3);
            Assert.True(layout.HeroTotalY > cardsBottom);
        }
        else
        {
            Assert.True(cardsBottom < totalTop);
        }

        Assert.True(layout.HeroTotalY + totalHalf < layout.BetSpot.Y - layout.BetSpotPixels);
        Assert.True(layout.BetPlateY + plateHalf < layout.CapsuleCenter.Y - capsuleHalf);
        Assert.True(layout.CapsuleCenter.Y + capsuleHalf <= felt.Max.Y + Epsilon);
        Assert.True(layout.BetSpotPixels >= BetSpotFloor(layout) - Epsilon);
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
    public void SpotCaptionsSitInsideTheirCircles(float scale, float width, float height, int railCount,
        bool seated)
    {
        if (!seated)
        {
            return;
        }

        var layout = Compute(scale, width, height, railCount, seated);
        var radius = layout.BetSpotPixels;
        Assert.True(layout.SpotCaptionWidth < radius * 2f);
        Assert.True(layout.SpotCaptionWidth >= 34f * scale);
        var captionHalf = layout.SpotCaptionWidth * 0.5f;
        var lineHalf = 7f * scale;
        Assert.True(captionHalf * captionHalf + lineHalf * lineHalf <= radius * radius);
    }

    [Theory]
    [MemberData(nameof(Tables))]
    public void RailSeatsNeverTouchEachOtherOrLeaveTheFelt(float scale, float width, float height, int railCount,
        bool seated)
    {
        var layout = Compute(scale, width, height, railCount, seated);
        var felt = layout.Felt;
        var spotFloor = layout.Compact ? BlackjackTableLayout.CompactSpotRadiusMin : BlackjackTableLayout.SpotRadiusMin;
        Assert.True(layout.SpotRadius >= spotFloor * scale - Epsilon);
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
        var shoe = ShoeRect(layout);
        var fanHalf = new Vector2(layout.Felt.Width * BlackjackTableLayout.DealerFanShare,
            PlayingCards.HeightFor(layout.DealerCardPixels)) * 0.5f;
        Assert.False(Overlaps(shoe, new Rect(layout.DealerFanCenter - fanHalf, layout.DealerFanCenter + fanHalf)));
        Assert.True(shoe.Max.Y < layout.StateBand.Min.Y);
        Assert.True(shoe.Max.X <= layout.Felt.Max.X);
        for (var slot = 0; slot < railCount; slot++)
        {
            Assert.False(Overlaps(shoe, layout.RailSeatRect(slot)));
        }
    }

    [Fact]
    public void TheOwnersPhoneKeepsReadableCards()
    {
        var layout = Compute(1f, 272f, 489f, BlackjackRules.SeatCount - 1, true);
        Assert.False(layout.Compact);
        Assert.True(layout.Fit >= 0.7f);
        Assert.Equal(1f, layout.GapFit);
    }

    [Theory]
    [MemberData(nameof(ScaleData))]
    public void ATournamentPhoneGoesCompactAndKeepsItsCardsReadable(float scale)
    {
        const int railCount = BlackjackRules.SeatCount - 1;
        var layout = Compute(scale, 272f, TournamentFeltHeight, railCount, true);
        Assert.True(layout.Compact);
        Assert.True(layout.Fit >= BlackjackTableLayout.FitFloor - Epsilon);
        AssertZonesStack(layout, railCount, true);
        AssertRailSeatsStayApart(layout, railCount);
        for (var handCount = 1; handCount <= BlackjackRules.MaxHandsPerSeat; handCount++)
        {
            AssertHeroHandsClearTheRail(layout, railCount, handCount);
        }

        AssertBetSpotsClearTheRailAndTheTotals(layout, railCount);
    }

    [Theory]
    [MemberData(nameof(ScaleData))]
    public void TheCompactFloorSitsWellBelowTheRegularOne(float scale)
    {
        const int railCount = BlackjackRules.SeatCount - 1;
        var width = 272f * scale;
        var regular = BlackjackTableLayout.MinimumHeight(width, railCount, true, scale);
        var compact = BlackjackTableLayout.CompactMinimumHeight(width, railCount, true, scale);
        var lastResort = BlackjackTableLayout.LastResortHeight(width, railCount, true, scale);
        Assert.True(compact <= regular - 90f * scale);
        Assert.True(lastResort < compact);
        Assert.True(compact <= TournamentFeltHeight * scale);
    }

    [Theory]
    [MemberData(nameof(ScaleData))]
    public void TheRegularTableHoldsOnlyWhileItKeepsItsGapsAndSeventyPercentCards(float scale)
    {
        const int railCount = BlackjackRules.SeatCount - 1;
        for (var height = 300f; height <= 600f; height += 5f)
        {
            var layout = Compute(scale, 272f, height, railCount, true);
            if (layout.Compact)
            {
                continue;
            }

            Assert.Equal(1f, layout.GapFit);
            Assert.True(layout.Fit >= BlackjackTableLayout.RegularFitFloor - Epsilon);
        }

        Assert.False(Compute(scale, 272f, 489f, railCount, true).Compact);
        var floor = BlackjackTableLayout.MinimumHeight(272f * scale, railCount, true, scale) / scale;
        var squeezed = Compute(scale, 272f, floor, railCount, true);
        Assert.True(squeezed.Compact);
        Assert.True(squeezed.Fit > BlackjackTableLayout.FitFloor);
    }

    [Theory]
    [MemberData(nameof(ScaleData))]
    public void GoingCompactNeverShrinksTheCards(float scale)
    {
        const int railCount = BlackjackRules.SeatCount - 1;
        var previous = Compute(scale, 272f, 600f, railCount, true);
        for (var height = 595f; height >= 300f; height -= 5f)
        {
            var layout = Compute(scale, 272f, height, railCount, true);
            if (layout.Compact && !previous.Compact)
            {
                Assert.True(layout.Fit >= previous.Fit - Epsilon);
            }

            previous = layout;
        }
    }

    [Theory]
    [MemberData(nameof(ScaleData))]
    public void TheCompactDealerRowSeatsThePuckLeftAndTheTotalRightOfTheFan(float scale)
    {
        var layout = Compute(scale, 272f, TournamentFeltHeight, BlackjackRules.SeatCount - 1, true);
        Assert.True(layout.Compact);
        var felt = layout.Felt;
        var fanHalfWidth = felt.Width * BlackjackTableLayout.DealerFanShare * 0.5f;
        var fanLeft = layout.DealerFanCenter.X - fanHalfWidth;
        var fanRight = layout.DealerFanCenter.X + fanHalfWidth;
        Assert.Equal(layout.DealerFanCenter.Y, layout.DealerPuck.Y, 3);
        Assert.True(layout.DealerPuck.X - layout.DealerPuckPixels >= felt.Min.X);
        Assert.True(layout.DealerPuck.X + layout.DealerPuckPixels < fanLeft);
        Assert.Equal(0f, layout.SpeechArea.Width, 3);
        Assert.Equal(layout.DealerFanCenter.Y, layout.DealerTotalY, 3);

        var pillHalf = 18f * scale;
        var shoeLeft = layout.ShoeAnchor.X - layout.ShoePixels * 0.5f;
        var nearPill = layout.DealerTotalBesideX(layout.DealerFanCenter.X + 30f * scale, pillHalf);
        Assert.True(nearPill - pillHalf > layout.DealerFanCenter.X + 30f * scale);
        Assert.True(nearPill + pillHalf < shoeLeft);
        var farPill = layout.DealerTotalBesideX(fanRight, pillHalf);
        Assert.True(farPill + pillHalf < shoeLeft);
        Assert.True(layout.DealerTotalY + layout.PillPixels * 0.5f <= layout.StateBand.Min.Y);
    }

    [Theory]
    [MemberData(nameof(ScaleData))]
    public void TheCompactRailPlateCoversOnlyTheLowerHalfOfTheAvatar(float scale)
    {
        const int railCount = BlackjackRules.SeatCount - 1;
        var layout = Compute(scale, 272f, TournamentFeltHeight, railCount, true);
        Assert.True(layout.Compact);
        Assert.False(layout.ShowsRailTitles);
        for (var slot = 0; slot < railCount; slot++)
        {
            var puck = layout.RailPuckCenter(slot);
            var plate = layout.RailPlateRect(slot);
            Assert.True(plate.Min.Y >= puck.Y + layout.PuckRadius * 0.5f - Epsilon);
            Assert.True(plate.Min.Y < puck.Y + layout.PuckRadius);
            var pips = BlackjackTableLayout.TimeBankPips(puck, layout.PuckRadius, scale);
            Assert.True(pips.Y + BlackjackTableArt.TimeBankPipHalfHeight(scale) < plate.Min.Y);
        }
    }

    [Theory]
    [InlineData(BlackjackRules.SeatCount - 1, 36f)]
    [InlineData(BlackjackRules.SeatCount - 2, 44f)]
    public void TheSeatArcBowsTowardThePlayer(int railCount, float puckFloor)
    {
        var layout = Compute(1f, 272f, 489f, railCount, true);
        var edge = layout.RailPuckCenter(0);
        var middle = layout.RailPuckCenter(railCount / 2);
        var last = layout.RailPuckCenter(railCount - 1);
        Assert.Equal(0f, layout.Stagger);
        Assert.True(edge.Y < middle.Y);
        Assert.Equal(edge.Y, last.Y, 3);
        Assert.True(layout.PuckRadius * 2f >= puckFloor - Epsilon);
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

    public static TheoryData<float, float, int, bool> BorrowRails()
    {
        var data = new TheoryData<float, float, int, bool>();
        for (var scaleIndex = 0; scaleIndex < FloorScales.Length; scaleIndex++)
        {
            data.Add(FloorScales[scaleIndex], 336f, BlackjackRules.SeatCount, false);
            data.Add(FloorScales[scaleIndex], 272f, BlackjackRules.SeatCount - 1, true);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(BorrowRails))]
    public void AMiddleSeatBetweenEmptySeatsWidensItsPlateAroundThePuckWithinAColumnEachSide(float scale,
        float width, int railCount, bool seated)
    {
        var layout = Compute(scale, width, RoomyFeltHeight, railCount, seated);
        Assert.False(layout.Compact);
        Assert.Equal(0f, layout.Stagger);
        var slot = railCount / 2;
        var puck = layout.RailPuckCenter(slot);
        var basePlate = layout.RailPlateRect(slot);
        var plate = layout.RailPlateRect(slot, true, true);
        Assert.True(plate.Width > basePlate.Width + layout.ColumnWidth * 0.5f);
        Assert.True(plate.Width <= layout.ColumnWidth * 2f - BlackjackTableLayout.RailGutter * 2f * scale + Epsilon);
        Assert.Equal(puck.X, plate.Center.X, 3);
        Assert.Equal(basePlate.Min.Y, plate.Min.Y, 3);
        Assert.Equal(basePlate.Max.Y, plate.Max.Y, 3);
        Assert.True(plate.Min.X >= puck.X - layout.ColumnWidth - Epsilon);
        Assert.True(plate.Max.X <= puck.X + layout.ColumnWidth + Epsilon);
        Assert.Equal(basePlate, layout.RailPlateRect(slot, false, false));
        Assert.Equal(basePlate, layout.RailPlateRect(slot, true, false));
        Assert.Equal(basePlate, layout.RailPlateRect(slot, false, true));
        Assert.Equal(plate.Width - StageText.TightPadX * 2f * scale, layout.RailPlateTextWidthFor(plate.Width), 3);
    }

    [Theory]
    [MemberData(nameof(Tables))]
    public void PlatesBorrowingTheSameEmptySeatStopShortOfItsCentre(float scale, float width, float height,
        int railCount, bool seated)
    {
        var layout = Compute(scale, width, height, railCount, seated);
        for (var slot = 0; slot + 2 < railCount; slot++)
        {
            var between = layout.RailPuckCenter(slot + 1).X;
            var left = layout.RailPlateRect(slot, true, true);
            var right = layout.RailPlateRect(slot + 2, true, true);
            Assert.True(left.Max.X < between);
            Assert.True(right.Min.X > between);
            Assert.False(Overlaps(left, right));
        }
    }

    [Theory]
    [MemberData(nameof(Tables))]
    public void BorrowedPlatesNeverTouchAnEmptyNeighboursSitCircle(float scale, float width, float height,
        int railCount, bool seated)
    {
        var layout = Compute(scale, width, height, railCount, seated);
        for (var slot = 0; slot < railCount; slot++)
        {
            var plate = layout.RailPlateRect(slot, true, true);
            for (var neighbour = slot - 1; neighbour <= slot + 1; neighbour += 2)
            {
                if (neighbour < 0 || neighbour >= railCount)
                {
                    continue;
                }

                Assert.True(DistanceToRect(layout.RailPuckCenter(neighbour), plate) >= layout.SpotRadius - Epsilon);
            }
        }
    }

    [Fact]
    public void AStaggeredRailLendsLoweredSeatsTheFullReachAndRaisedSeatsNothing()
    {
        var layout = Compute(1f, 272f, 489f, BlackjackRules.SeatCount, false);
        Assert.True(layout.Stagger > 0f);
        var raised = layout.RailPuckCenter(2);
        var lowered = layout.RailPuckCenter(3);
        Assert.True(lowered.Y + layout.SpotRadius > raised.Y + layout.RailPlateTop);
        Assert.Equal(layout.RailPlateRect(2), layout.RailPlateRect(2, true, true));
        Assert.True(raised.Y + layout.SpotRadius <= lowered.Y + layout.RailPlateTop);
        var borrowed = layout.RailPlateRect(3, true, true);
        Assert.Equal(layout.RailPlateWidth + layout.RailPlateReach * 2f, borrowed.Width, 3);
        Assert.Equal(lowered.X, borrowed.Center.X, 3);
    }

    [Theory]
    [MemberData(nameof(ScaleData))]
    public void ACompactRailBorrowsOnlyTheSliverOutsideTheSitCirclesChords(float scale)
    {
        const int railCount = BlackjackRules.SeatCount - 1;
        var layout = Compute(scale, 272f, TournamentFeltHeight, railCount, true);
        Assert.True(layout.Compact);
        Assert.True(layout.RailPlateTop < layout.SpotRadius);
        var fullReach = layout.RailPlateWidth + layout.RailPlateReach * 2f;
        for (var slot = 0; slot < railCount; slot++)
        {
            var plate = layout.RailPlateRect(slot, true, true);
            Assert.True(plate.Width > layout.RailPlateWidth);
            Assert.True(plate.Width < fullReach);
        }
    }

    [Theory]
    [MemberData(nameof(Tables))]
    public void BorrowedPlatesNeverLeaveTheFeltOrEscapeTheirWidthBounds(float scale, float width, float height,
        int railCount, bool seated)
    {
        var layout = Compute(scale, width, height, railCount, seated);
        var felt = layout.Felt;
        var cap = layout.ColumnWidth * 2f - BlackjackTableLayout.RailGutter * 2f * scale;
        for (var slot = 0; slot < railCount; slot++)
        {
            var plate = layout.RailPlateRect(slot, true, true);
            Assert.True(plate.Min.X >= felt.Min.X - Epsilon);
            Assert.True(plate.Max.X <= felt.Max.X + Epsilon);
            Assert.True(plate.Width >= layout.RailPlateWidth - Epsilon);
            Assert.True(plate.Width <= cap + Epsilon);
        }
    }

    [Theory]
    [MemberData(nameof(ScaleData))]
    public void AnEndSeatOnAWideSevenSeatRailHugsTheFeltEdgeAndReachesInwardTheFullReach(float scale)
    {
        const int last = BlackjackRules.SeatCount - 1;
        var layout = Compute(scale, 336f, RoomyFeltHeight, BlackjackRules.SeatCount, false);
        Assert.Equal(0f, layout.Stagger);
        var felt = layout.Felt;
        var gutter = BlackjackTableLayout.RailGutter * scale;
        var firstBase = layout.RailPlateRect(0);
        var first = layout.RailPlateRect(0, false, true);
        Assert.Equal(felt.Min.X + gutter, first.Min.X, 3);
        Assert.Equal(firstBase.Max.X + layout.RailPlateReach, first.Max.X, 3);
        Assert.True(first.Center.X > layout.RailPuckCenter(0).X);
        var lastBase = layout.RailPlateRect(last);
        var lastPlate = layout.RailPlateRect(last, true, false);
        Assert.Equal(felt.Max.X - gutter, lastPlate.Max.X, 3);
        Assert.Equal(lastBase.Min.X - layout.RailPlateReach, lastPlate.Min.X, 3);
        Assert.Equal(first.Width, lastPlate.Width, 3);
        Assert.Equal(firstBase, layout.RailPlateRect(0, false, false));
        Assert.Equal(lastBase, layout.RailPlateRect(last, false, false));
    }

    [Theory]
    [MemberData(nameof(ScaleData))]
    public void AnEndSeatOnAStaggeredRailOnlyGainsTheFeltEdgeRoom(float scale)
    {
        const int last = BlackjackRules.SeatCount - 1;
        var layout = Compute(scale, 272f, RoomyFeltHeight, BlackjackRules.SeatCount, false);
        Assert.True(layout.Stagger > 0f);
        var felt = layout.Felt;
        var gutter = BlackjackTableLayout.RailGutter * scale;
        var first = layout.RailPlateRect(0, false, true);
        Assert.Equal(felt.Min.X + gutter, first.Min.X, 3);
        Assert.Equal(layout.RailPlateRect(0).Max.X, first.Max.X, 3);
        var lastPlate = layout.RailPlateRect(last, true, false);
        Assert.Equal(felt.Max.X - gutter, lastPlate.Max.X, 3);
        Assert.Equal(layout.RailPlateRect(last).Min.X, lastPlate.Min.X, 3);
    }

    [Theory]
    [MemberData(nameof(ScaleData))]
    public void ALoneRailSeatHasNoEmptyNeighbourSoItsPlateStaysAtTheBaseWidthOnItsPuck(float scale)
    {
        var layout = Compute(scale, 272f, 489f, 1, true);
        var plate = layout.RailPlateRect(0, false, false);
        Assert.Equal(layout.RailPlateRect(0), plate);
        Assert.Equal(layout.RailPuckCenter(0).X, plate.Center.X, 3);
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
        Assert.Equal(3, BlackjackTableLayout.RailSlotOf(6, mySeat));
        Assert.Equal(4, BlackjackTableLayout.RailSlotOf(0, mySeat));
        Assert.Equal(5, BlackjackTableLayout.RailSlotOf(1, mySeat));
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
        AssertLaysOutCleanly(scale, width, floor, railCount, seated);
    }

    [Theory]
    [MemberData(nameof(FloorTables))]
    public void EverySeatCountLaysOutWithoutOverlapDownToTheCompactFloor(float scale, float width, int railCount,
        bool seated)
    {
        var floor = BlackjackTableLayout.CompactMinimumHeight(width * scale, railCount, seated, scale) / scale;
        AssertLaysOutCleanly(scale, width, floor, railCount, seated);
        var layout = Compute(scale, width, floor, railCount, seated);
        Assert.True(layout.Fit >= BlackjackTableLayout.MinimumFit - Epsilon);
    }

    [Theory]
    [MemberData(nameof(FloorTables))]
    public void EvenTheLastResortFeltKeepsItsZonesApart(float scale, float width, int railCount, bool seated)
    {
        var floor = BlackjackTableLayout.LastResortHeight(width * scale, railCount, seated, scale) / scale;
        var layout = Compute(scale, width, floor, railCount, seated);
        Assert.True(layout.Compact);
        Assert.True(layout.Fit >= BlackjackTableLayout.LastResortFit - Epsilon);
        AssertZonesStack(layout, railCount, seated);
        AssertRailSeatsStayApart(layout, railCount);
    }

    [Theory]
    [MemberData(nameof(FloorTables))]
    public void SideStakesHangUnderEachSeatPlateInsideItsColumn(float scale, float width, int railCount,
        bool seated)
    {
        var floor = BlackjackTableLayout.CompactMinimumHeight(width * scale, railCount, seated, scale) / scale;
        var regularFloor = BlackjackTableLayout.MinimumHeight(width * scale, railCount, seated, scale) / scale;
        var span = regularFloor + FloorSlack - floor;
        for (var extra = 0f; extra <= span; extra += span * 0.25f)
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

    [Theory]
    [InlineData(BlackjackRules.SeatCount - 1, 18f)]
    [InlineData(BlackjackRules.SeatCount - 2, 20f)]
    public void TheOwnersPhoneKeepsSideStakesOffTheStackLineAndTheHeroCards(int railCount, float stakeFloor)
    {
        var layout = Compute(1f, 272f, 489f, railCount, true);
        for (var slot = 0; slot < railCount; slot++)
        {
            var plate = layout.RailPlateRect(slot);
            var stake = layout.RailSideStakeRect(slot, BlackjackSideBet.PerfectPairs, true);
            Assert.Equal(plate.Max.Y - BlackjackTableLayout.SideStakeOverlap, stake.Min.Y, 3);
            Assert.True(stake.Max.Y <= layout.SideStakeFloor + Epsilon);
            Assert.True(stake.Width >= stakeFloor);
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
            (BlackjackTableLayout.CompactRailPuckRadius, BlackjackTableLayout.RailRingGap),
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

    [Theory]
    [MemberData(nameof(ScaleData))]
    public void BelowItsFloorTheRegularTableTurnsCompactInsteadOfRunningTheRailIntoTheHero(float scale)
    {
        const int railCount = BlackjackRules.SeatCount - 1;
        var floor = BlackjackTableLayout.MinimumHeight(285f * scale, railCount, true, scale) / scale;
        var squeezed = Compute(scale, 285f, floor - 40f, railCount, true);

        Assert.True(squeezed.Compact);
        Assert.True(squeezed.RailBottom <= squeezed.HeroTop + Epsilon);
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

    private static void AssertLaysOutCleanly(float scale, float width, float floor, int railCount, bool seated)
    {
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

    private static void AssertZonesStack(BlackjackTableLayout layout, int railCount, bool seated)
    {
        var felt = layout.Felt;
        var dealerCardHalf = PlayingCards.HeightFor(layout.DealerCardPixels) * 0.5f;
        var pillHalf = layout.PillPixels * 0.5f;
        Assert.True(layout.DealerPuck.Y - layout.DealerPuckPixels >= felt.Min.Y - Epsilon);
        Assert.True(layout.DealerFanCenter.Y - dealerCardHalf >= felt.Min.Y - Epsilon);
        if (layout.Compact)
        {
            var fanLeft = layout.DealerFanCenter.X - felt.Width * BlackjackTableLayout.DealerFanShare * 0.5f;
            Assert.True(layout.DealerPuck.X + layout.DealerPuckPixels <= fanLeft + Epsilon);
            Assert.True(layout.DealerPuck.Y + layout.DealerPuckPixels <= layout.StateBand.Min.Y + Epsilon);
            Assert.True(layout.DealerFanCenter.Y + dealerCardHalf <= layout.StateBand.Min.Y + Epsilon);
        }
        else
        {
            Assert.True(layout.DealerPuck.Y + layout.DealerPuckPixels <= layout.DealerFanCenter.Y - dealerCardHalf
                + Epsilon);
            Assert.True(layout.DealerFanCenter.Y + dealerCardHalf <= layout.DealerTotalY - pillHalf + Epsilon);
        }

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
            var handHalf = PlayingCards.HeightFor(layout.RailCardPixels(1)) * 0.5f;
            var cards = new Rect(new Vector2(seat.Min.X, seat.Min.Y),
                new Vector2(seat.Max.X, puck.Y - layout.RailCardsLift + handHalf));
            Assert.True(seat.Min.X >= felt.Min.X - Epsilon && seat.Max.X <= felt.Max.X + Epsilon);
            Assert.True(puck.X - layout.SpotRadius >= felt.Min.X - Epsilon);
            Assert.True(puck.X + layout.SpotRadius <= felt.Max.X + Epsilon);
            Assert.True(seat.Min.Y >= layout.StateBand.Max.Y - Epsilon);
            Assert.True(seat.Max.Y <= layout.HeroTop + Epsilon);
            Assert.True(Contains(seat, plate) && Contains(seat, betPlate) && Contains(seat, cards));
            Assert.False(Overlaps(cards, betPlate));
            Assert.False(Overlaps(betPlate, PuckBox(puck, layout.PuckRadius)));
            if (layout.Compact)
            {
                Assert.True(plate.Min.Y >= puck.Y + layout.PuckRadius * BlackjackTableLayout.CompactPlateRise
                    - Epsilon);
            }
            else
            {
                Assert.False(Overlaps(plate, PuckBox(puck, layout.PuckRadius)));
            }

            for (var other = slot + 1; other < railCount; other++)
            {
                var otherPuck = layout.RailPuckCenter(other);
                Assert.False(Overlaps(seat, layout.RailSeatRect(other)));
                Assert.True(Vector2.Distance(puck, otherPuck) >= layout.SpotRadius * 2f - Epsilon);
                Assert.False(Overlaps(plate, layout.RailPlateRect(other)));
                Assert.False(Overlaps(plate, PuckBox(otherPuck, layout.PuckRadius)));
                Assert.False(Overlaps(layout.RailPlateRect(other), PuckBox(puck, layout.PuckRadius)));
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
        var radius = layout.BetSpotPixels;
        var totalsBottom = layout.HeroTotalY + layout.TotalCapsulePixels * 0.5f;
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

    private static float BetSpotFloor(BlackjackTableLayout layout) =>
        (layout.Compact ? BlackjackTableLayout.CompactBetSpotRadius : BlackjackTableLayout.BetSpotRadius)
        * layout.Scale;

    private static Rect ShoeRect(BlackjackTableLayout layout)
    {
        var width = layout.ShoePixels;
        var lift = BlackjackTableLayout.ShoeLift * layout.Scale;
        var half = new Vector2(width, PlayingCards.HeightFor(width)) * 0.5f;
        return new Rect(layout.ShoeAnchor - half, layout.ShoeAnchor + half + new Vector2(0f, lift));
    }

    private static float BetPlateHalf(float scale) => BlackjackTableLayout.BetPlateHeight * 0.5f * scale;

    private static bool Contains(in Rect outer, in Rect inner)
    {
        return inner.Min.X >= outer.Min.X - Epsilon && inner.Max.X <= outer.Max.X + Epsilon
            && inner.Min.Y >= outer.Min.Y - Epsilon && inner.Max.Y <= outer.Max.Y + Epsilon;
    }

    internal static BlackjackTableLayout Compute(float scale, float width, float height, int railCount, bool seated)
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

    private static float DistanceToRect(Vector2 point, in Rect rect) =>
        Vector2.Distance(point, Vector2.Clamp(point, rect.Min, rect.Max));

    private static bool Overlaps(in Rect first, in Rect second)
    {
        return first.Min.X < second.Max.X - Epsilon && second.Min.X < first.Max.X - Epsilon
            && first.Min.Y < second.Max.Y - Epsilon && second.Min.Y < first.Max.Y - Epsilon;
    }
}
