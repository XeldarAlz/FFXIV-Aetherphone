using System.Numerics;
using Aetherphone.Apps.Games.Framework.Cards;
using Aetherphone.Core;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CardTableLayoutTests
{
    private const float Tolerance = 0.01f;
    private static readonly Vector2 HandCenter = new(200f, 300f);
    private static readonly Rect Table = new(new Vector2(0f, 0f), new Vector2(400f, 300f));

    [Fact]
    public void FanSpreadsTheHandSymmetricallyAcrossTheWidth()
    {
        Span<FanSlot> slots = stackalloc FanSlot[5];

        HandLayout.Fan(5, HandCenter, 200f, 0.3f, slots);

        Assert.Equal(100f, slots[0].Center.X, Tolerance);
        Assert.Equal(300f, slots[4].Center.X, Tolerance);
        Assert.Equal(HandCenter.X, slots[2].Center.X, Tolerance);
        Assert.Equal(HandCenter.Y, slots[2].Center.Y, Tolerance);
        Assert.Equal(0f, slots[2].Angle, Tolerance);
        Assert.Equal(-0.3f, slots[0].Angle, Tolerance);
        Assert.Equal(0.3f, slots[4].Angle, Tolerance);
        Assert.Equal(slots[0].Center.Y, slots[4].Center.Y, Tolerance);
        Assert.True(slots[0].Center.Y > slots[1].Center.Y);
        Assert.True(slots[1].Center.Y > slots[2].Center.Y);
        for (var index = 1; index < slots.Length; index++)
        {
            Assert.True(slots[index].Center.X > slots[index - 1].Center.X);
            Assert.True(slots[index].Angle > slots[index - 1].Angle);
        }
    }

    [Fact]
    public void AFlatFanIsAStraightRowAndOneCardSitsInTheMiddle()
    {
        var single = HandLayout.Fan(0, 1, HandCenter, 200f, 0.3f);
        var first = HandLayout.Fan(0, 3, HandCenter, 120f, 0f);
        var last = HandLayout.Fan(2, 3, HandCenter, 120f, 0f);

        Assert.Equal(HandCenter, single.Center);
        Assert.Equal(0f, single.Angle);
        Assert.Equal(140f, first.Center.X, Tolerance);
        Assert.Equal(260f, last.Center.X, Tolerance);
        Assert.Equal(HandCenter.Y, first.Center.Y, Tolerance);
        Assert.Equal(0f, last.Angle);
    }

    [Fact]
    public void AStepCapClosesAShortHandOnTheSameArc()
    {
        var left = HandLayout.Fan(0, 2, HandCenter, 200f, 0.3f, 40f);
        var right = HandLayout.Fan(1, 2, HandCenter, 200f, 0.3f, 40f);
        var capped = HandLayout.Fan(4, 5, HandCenter, 200f, 0.3f, 40f);
        var wide = HandLayout.Fan(4, 5, HandCenter, 200f, 0.3f, 60f);

        Assert.Equal(40f, right.Center.X - left.Center.X, Tolerance);
        Assert.Equal(-left.Angle, right.Angle, Tolerance);
        Assert.True(right.Angle > 0f && right.Angle < 0.3f);
        Assert.Equal(280f, capped.Center.X, Tolerance);
        Assert.Equal(300f, wide.Center.X, Tolerance);
        var radius = 100f / MathF.Sin(0.3f);
        var arcCenter = HandCenter + new Vector2(0f, radius);
        Assert.Equal(radius, Vector2.Distance(arcCenter, right.Center), 0.05f);
    }

    [Fact]
    public void HitTestPicksTheTopmostCardUnderThePointer()
    {
        Span<FanSlot> slots = stackalloc FanSlot[3];
        HandLayout.Fan(3, HandCenter, 60f, 0f, slots);
        Span<CardPose> poses = stackalloc CardPose[3];
        for (var index = 0; index < poses.Length; index++)
        {
            poses[index] = slots[index].Pose(50f);
        }

        Assert.Equal(2, HandLayout.HitTest(poses, slots[2].Center));
        Assert.Equal(1, HandLayout.HitTest(poses, slots[1].Center));
        Assert.Equal(0, HandLayout.HitTest(poses, slots[0].Center - new Vector2(20f, 0f)));
        Assert.Equal(-1, HandLayout.HitTest(poses, HandCenter + new Vector2(0f, 80f)));
    }

    [Fact]
    public void ACardPoseHonoursItsRotationSquashAndLift()
    {
        var upright = new CardPose(Vector2.Zero, 40f);
        var turned = new CardPose(Vector2.Zero, 40f, MathF.PI * 0.5f);
        var edgeOn = new CardPose(Vector2.Zero, 40f, 0f, true, 0.1f);

        Assert.Equal(56f, upright.Height, Tolerance);
        Assert.True(upright.Contains(new Vector2(0f, 25f)));
        Assert.False(upright.Contains(new Vector2(25f, 0f)));
        Assert.True(turned.Contains(new Vector2(25f, 0f)));
        Assert.False(turned.Contains(new Vector2(0f, 25f)));
        Assert.False(edgeOn.Contains(new Vector2(10f, 0f)));
        Assert.Equal(new Vector2(0f, -10f), upright.Lifted(10f).Center);
        Assert.Equal(10f, turned.Lifted(10f).Center.X, Tolerance);
    }

    [Fact]
    public void TheRingSeatsTheFirstPlayerAtTheBottomAndRunsClockwise()
    {
        Span<Vector2> seats = stackalloc Vector2[4];

        SeatLayout.Ring(4, Table, seats);

        AssertNear(new Vector2(200f, 300f), seats[0]);
        AssertNear(new Vector2(0f, 150f), seats[1]);
        AssertNear(new Vector2(200f, 0f), seats[2]);
        AssertNear(new Vector2(400f, 150f), seats[3]);
        AssertNear(new Vector2(200f, 300f), SeatLayout.Seat(2, 4, Table, bottomSeat: 2));
        AssertNear(new Vector2(200f, 0f), SeatLayout.Seat(1, 2, Table));
    }

    [Fact]
    public void EverySeatCountFromTwoToSixLandsOnTheTableEdgeAndMirrors()
    {
        Span<Vector2> buffer = stackalloc Vector2[SeatLayout.MaxSeats];
        for (var count = SeatLayout.MinSeats; count <= SeatLayout.MaxSeats; count++)
        {
            var seats = buffer[..count];
            SeatLayout.Ring(count, Table, seats);
            for (var seat = 0; seat < count; seat++)
            {
                var normalized = (seats[seat] - Table.Center) / (Table.Size * 0.5f);
                Assert.Equal(1f, normalized.LengthSquared(), 0.001f);
                var mirror = (count - seat) % count;
                Assert.Equal(Table.Center.X - seats[seat].X, seats[mirror].X - Table.Center.X, 0.01f);
                Assert.Equal(seats[seat].Y, seats[mirror].Y, 0.01f);
                for (var other = 0; other < seat; other++)
                {
                    Assert.True(Vector2.Distance(seats[seat], seats[other]) > 1f);
                }
            }
        }
    }

    [Fact]
    public void InwardStepsTowardTheTableCentreWithoutPassingIt()
    {
        var seat = new Vector2(200f, 300f);

        AssertNear(new Vector2(200f, 260f), SeatLayout.Inward(seat, Table, 40f));
        AssertNear(Table.Center, SeatLayout.Inward(seat, Table, 900f));
    }

    [Fact]
    public void PilesThickenWithTheirCountAndScatterDeterministically()
    {
        Assert.Equal(0, PileLayout.Layers(0));
        Assert.Equal(1, PileLayout.Layers(1));
        Assert.Equal(1, PileLayout.Layers(PileLayout.CardsPerLayer));
        Assert.Equal(2, PileLayout.Layers(PileLayout.CardsPerLayer + 1));
        Assert.Equal(PileLayout.MaxLayers, PileLayout.Layers(500));
        var top = PileLayout.Top(HandCenter, 50f, 20, 2f);
        Assert.Equal(HandCenter.Y - 3 * PileLayout.LayerStep * 2f, top.Center.Y, Tolerance);
        Assert.False(top.FaceUp);
        var first = PileLayout.Scatter(HandCenter, 50f, 7);
        var again = PileLayout.Scatter(HandCenter, 50f, 7);
        var next = PileLayout.Scatter(HandCenter, 50f, 8);
        Assert.Equal(first.Center, again.Center);
        Assert.Equal(first.Angle, again.Angle);
        Assert.NotEqual(first.Angle, next.Angle);
        Assert.InRange(MathF.Abs(first.Angle), 0f, 0.181f);
        Assert.InRange(Vector2.Distance(first.Center, HandCenter), 0f, 50f * 0.1f);
    }

    [Fact]
    public void FlightsWaitOutTheirDelayArcAndFlipMidway()
    {
        var flight = new CardFlight(4);
        var from = new CardPose(new Vector2(0f, 100f), 40f, 0f, false);
        var to = new CardPose(new Vector2(200f, 100f), 60f, 0.2f, true);

        flight.Launch(7, from, to, delay: 0.1f, flip: true, tag: 3, seconds: 1f);
        flight.Advance(0.05f);
        Assert.True(flight.Waiting(0));
        Assert.Equal(from.Center, flight.Pose(0).Center);
        flight.Advance(0.05f + 0.5f);
        var midway = flight.Pose(0);

        Assert.False(flight.Waiting(0));
        Assert.Equal(0.5f, flight.Progress(0), Tolerance);
        Assert.True(midway.Center.Y < 100f);
        Assert.True(midway.Squash < 0.5f);
        Assert.True(midway.Width > 40f && midway.Width < 60f);
        flight.Advance(0.2f);
        Assert.True(flight.Pose(0).FaceUp);
        Assert.False(flight.TryTakeLanded(out _));
        flight.Advance(0.5f);

        Assert.Equal(0, flight.Count);
        Assert.True(flight.TryTakeLanded(out var landing));
        Assert.Equal(7, landing.Card);
        Assert.Equal(3, landing.Tag);
        Assert.Equal(to.Center, landing.Pose.Center);
        Assert.False(flight.TryTakeLanded(out _));
    }

    [Fact]
    public void AHiddenFlightStaysOutOfSightUntilItsDelayRunsOut()
    {
        var flight = new CardFlight(4);
        var from = new CardPose(Vector2.Zero, 40f);
        var to = new CardPose(new Vector2(100f, 0f), 40f);

        flight.Launch(4, from, to, delay: 0.2f, hideWhileWaiting: true);
        flight.Launch(5, from, to, delay: 0.2f);
        flight.Launch(6, from, to, hideWhileWaiting: true);

        Assert.False(flight.Visible(0));
        Assert.True(flight.Visible(1));
        Assert.True(flight.Visible(2));
        flight.Advance(0.1f);
        Assert.False(flight.Visible(0));
        Assert.True(flight.Waiting(0));
        flight.Advance(0.15f);
        Assert.True(flight.Visible(0));
        Assert.False(flight.Waiting(0));
        Assert.True(flight.Progress(0) > 0f);
    }

    [Fact]
    public void AFullFlightPoolLandsItsOldestCardFirst()
    {
        var flight = new CardFlight(2);
        var from = new CardPose(Vector2.Zero, 40f);
        var to = new CardPose(new Vector2(100f, 0f), 40f);

        flight.Launch(1, from, to);
        flight.Launch(2, from, to, delay: 0.2f);
        flight.Launch(3, from, to);

        Assert.Equal(2, flight.Count);
        Assert.Equal(2, flight.Card(0));
        Assert.Equal(3, flight.Card(1));
        Assert.True(flight.TryTakeLanded(out var forced));
        Assert.Equal(1, forced.Card);
        flight.Advance(CardFlight.DefaultSeconds + 0.01f);
        Assert.True(flight.TryTakeLanded(out var quick));
        Assert.Equal(3, quick.Card);
        Assert.Equal(1, flight.Count);
        Assert.Equal(1f, flight.Pose(0).Squash);
        flight.Advance(1f);
        Assert.True(flight.TryTakeLanded(out var late));
        Assert.Equal(2, late.Card);
        Assert.False(flight.Busy);
    }

    private static void AssertNear(Vector2 expected, Vector2 actual)
    {
        Assert.Equal(expected.X, actual.X, 0.01f);
        Assert.Equal(expected.Y, actual.Y, 0.01f);
    }
}
