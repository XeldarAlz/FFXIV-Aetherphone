using System.Numerics;
using Aetherphone.Apps.Casino.Plinko;
using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class PlinkoPlaybackTests
{
    private static readonly int[] SixteenPath = { 1, 0, 0, 1, 1, 0, 1, 0, 1, 1, 0, 0, 1, 0, 1, 1 };

    [Fact]
    public void TheBallAlwaysLandsInTheServerSlot()
    {
        for (var mask = 0; mask < 256; mask += 7)
        {
            var path = new int[8];
            for (var row = 0; row < 8; row++)
            {
                path[row] = (mask >> row) & 1;
            }

            var flight = new PlinkoFlight();
            var slot = PlinkoRules.SlotOf(path);
            Assert.True(flight.Launch(Drop(8, slot), path, (ulong)mask * 31UL + 5UL, 0f, out var ball));
            var landed = Run(flight, 8, out var landing);
            Assert.True(landed);
            Assert.Equal(slot, landing.Drop.Slot);
            var rest = PlinkoFlight.PositionAt(8, Bits(path), new float[17], PlinkoFlight.FlightSeconds(8));
            Assert.Equal(PlinkoBoardLayout.SlotUnit(8, slot).X, rest.X, 4);
            Assert.False(flight.IsActive(ball));
        }
    }

    [Fact]
    public void EveryRowTicksOnceInOrderOnThePegItHits()
    {
        var flight = new PlinkoFlight();
        Assert.True(flight.Launch(Drop(16, PlinkoRules.SlotOf(SixteenPath)), SixteenPath, 9UL, 0f, out _));
        var rows = new List<int>();
        var columns = new List<int>();
        var landings = 0;
        for (var frame = 0; frame < 400; frame++)
        {
            flight.Advance(1f / 60f);
            for (var index = 0; index < flight.EventCount; index++)
            {
                var entry = flight.Event(index);
                if (entry.Kind == PlinkoEventKind.Peg)
                {
                    rows.Add(entry.Row);
                    columns.Add(entry.Column);
                    continue;
                }

                landings++;
            }
        }

        Assert.Equal(Enumerable.Range(0, 16), rows);
        Assert.Equal(1, landings);
        var rights = 0;
        for (var row = 0; row < 16; row++)
        {
            Assert.Equal(rights + 1, columns[row]);
            rights += SixteenPath[row];
        }
    }

    [Fact]
    public void AMidFlightJoinSkipsTheRowsAlreadyPassed()
    {
        var flight = new PlinkoFlight();
        var start = PlinkoFlight.SegmentSeconds * 5.5f;
        Assert.True(flight.Launch(Drop(16, PlinkoRules.SlotOf(SixteenPath)), SixteenPath, 3UL, start, out var ball));
        flight.Advance(0f);
        Assert.Equal(0, flight.EventCount);
        flight.Advance(PlinkoFlight.SegmentSeconds);
        Assert.Equal(1, flight.EventCount);
        Assert.Equal(5, flight.Event(0).Row);
        Assert.True(flight.IsActive(ball));
    }

    [Fact]
    public void AJoinPastTheFlightLandsOnTheNextFrame()
    {
        var flight = new PlinkoFlight();
        Assert.True(flight.Launch(Drop(8, 4), new[] { 1, 1, 0, 0, 1, 0, 1, 0 }, 3UL, 30f, out _));
        flight.Advance(0f);
        Assert.Equal(1, flight.EventCount);
        Assert.Equal(PlinkoEventKind.Landed, flight.Event(0).Kind);
        Assert.Equal(0, flight.ActiveCount);
    }

    [Fact]
    public void SnapToTruthLandsEveryBallWithoutTicks()
    {
        var flight = new PlinkoFlight();
        for (var ball = 0; ball < 3; ball++)
        {
            Assert.True(flight.Launch(Drop(16, PlinkoRules.SlotOf(SixteenPath)), SixteenPath, (ulong)ball, 0f, out _));
        }

        flight.Advance(0.5f);
        flight.SnapAll();
        Assert.Equal(3, flight.EventCount);
        for (var index = 0; index < flight.EventCount; index++)
        {
            Assert.Equal(PlinkoEventKind.Landed, flight.Event(index).Kind);
        }

        Assert.Equal(0, flight.ActiveCount);
    }

    [Fact]
    public void TenBallsFlyAtOnceAndTheEleventhWaits()
    {
        var flight = new PlinkoFlight();
        for (var ball = 0; ball < PlinkoRules.MaxInFlight; ball++)
        {
            Assert.True(flight.Launch(Drop(8, 4), new[] { 1, 1, 0, 0, 1, 0, 1, 0 }, (ulong)ball, 0f, out _));
        }

        Assert.Equal(PlinkoRules.MaxInFlight, flight.ActiveCount);
        Assert.False(flight.Launch(Drop(8, 4), new[] { 1, 1, 0, 0, 1, 0, 1, 0 }, 99UL, 0f, out var refused));
        Assert.Equal(-1, refused);
    }

    [Fact]
    public void AMalformedPathIsNeverAnimated()
    {
        var flight = new PlinkoFlight();
        Assert.False(flight.Launch(Drop(8, 5), new[] { 1, 1, 0, 0, 1, 0, 1, 0 }, 1UL, 0f, out _));
        Assert.False(flight.Launch(Drop(8, 4), new[] { 1, 1, 0, 0 }, 1UL, 0f, out _));
        Assert.Equal(0, flight.ActiveCount);
    }

    [Fact]
    public void TheWobbleStaysUnderATenthOfAPitch()
    {
        var flight = new PlinkoFlight();
        Assert.True(flight.Launch(Drop(16, PlinkoRules.SlotOf(SixteenPath)), SixteenPath, 77UL, 0f, out var ball));
        var bits = Bits(SixteenPath);
        var still = new float[17];
        for (var frame = 0; frame < 220; frame++)
        {
            flight.Advance(1f / 60f);
            if (!flight.IsActive(ball))
            {
                break;
            }

            var elapsed = flight.Ball(ball).Elapsed;
            var straight = PlinkoFlight.PositionAt(16, bits, still, elapsed);
            Assert.True(MathF.Abs(flight.Position(ball).X - straight.X) < 0.1f);
        }
    }

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = new PlinkoFlight();
        var second = new PlinkoFlight();
        var seed = PlinkoFlight.SeedOf("round-42");
        Assert.Equal(seed, PlinkoFlight.SeedOf("round-42"));
        Assert.NotEqual(seed, PlinkoFlight.SeedOf("round-43"));
        Assert.True(first.Launch(Drop(16, PlinkoRules.SlotOf(SixteenPath)), SixteenPath, seed, 0f, out var one));
        Assert.True(second.Launch(Drop(16, PlinkoRules.SlotOf(SixteenPath)), SixteenPath, seed, 0f, out var two));
        for (var frame = 0; frame < 200; frame++)
        {
            first.Advance(1f / 60f);
            second.Advance(1f / 60f);
            if (!first.IsActive(one))
            {
                Assert.False(second.IsActive(two));
                break;
            }

            Assert.Equal(first.Position(one), second.Position(two));
        }
    }

    [Fact]
    public void TheFlightTakesOneBeatPerRowPlusTheLanding()
    {
        Assert.Equal(0.22f, PlinkoFlight.SegmentSeconds);
        Assert.Equal(17 * 0.22f, PlinkoFlight.FlightSeconds(16), 4);
    }

    [Fact]
    public void BoardFxFlashesPegsAndSquashesTheLandingSlot()
    {
        var fx = new PlinkoBoardFx();
        fx.Flash(3, 2);
        fx.Land(16, true, true);
        Assert.Equal(1f, fx.PegGlow(3, 2));
        Assert.Equal(1f, fx.SlotPop(16));
        Assert.True(fx.SlotWon(16));
        Assert.Equal(1f, fx.Edge);
        fx.Advance(0.1f);
        Assert.True(fx.SlotSquash(16) > 0f);
        fx.Advance(5f);
        Assert.Equal(0f, fx.PegGlow(3, 2));
        Assert.Equal(0f, fx.SlotSquash(16));
        Assert.Equal(0f, fx.Edge);
        fx.Flash(2, 9);
        fx.Land(40, true, false);
        Assert.Equal(0f, fx.PegGlow(2, 9));
    }

    [Fact]
    public void TheResultRailKeepsTheLastTwentyFourNewestFirst()
    {
        var rail = new PlinkoResultRail();
        for (var drop = 0; drop < 30; drop++)
        {
            rail.Push(new PlinkoResult(drop, 16, PlinkoRules.High, false));
        }

        Assert.Equal(PlinkoResultRail.Capacity, rail.Count);
        Assert.Equal(24, PlinkoResultRail.Capacity);
        Assert.Equal(29, rail.Newest(0).Tenths);
        Assert.Equal(6, rail.Newest(23).Tenths);
    }

    [Fact]
    public void TheBoardFillsTheWidthAndKeepsEveryPegInside()
    {
        var area = new Rect(new Vector2(10f, 20f), new Vector2(370f, 620f));
        for (var rowsIndex = 0; rowsIndex < PlinkoRules.RowCounts.Length; rowsIndex++)
        {
            var rows = PlinkoRules.RowCounts[rowsIndex];
            var layout = PlinkoBoardLayout.Compute(area, rows);
            Assert.True(layout.Bounds.Width >= area.Width - 0.5f || layout.Bounds.Height >= area.Height - 0.5f);
            Assert.True(layout.Bounds.Min.X >= area.Min.X - 0.01f && layout.Bounds.Max.X <= area.Max.X + 0.01f);
            Assert.True(layout.Bounds.Min.Y >= area.Min.Y - 0.01f && layout.Bounds.Max.Y <= area.Max.Y + 0.01f);
            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < PlinkoBoardLayout.PegsInRow(row); column++)
                {
                    var peg = layout.PegCenter(row, column);
                    Assert.True(peg.X > layout.Bounds.Min.X && peg.X < layout.Bounds.Max.X);
                }
            }

            Assert.Equal(layout.Pitch, layout.SlotCenter(1).X - layout.SlotCenter(0).X, 3);
            Assert.True(layout.SlotRect(rows).Max.Y <= layout.Bounds.Max.Y + 0.01f);
        }
    }

    [Fact]
    public void ALegendReservesRoomUnderTheSlots()
    {
        var area = new Rect(Vector2.Zero, new Vector2(360f, 500f));
        var plain = PlinkoBoardLayout.Compute(area, 16);
        var legend = PlinkoBoardLayout.Compute(area, 16, 40f);
        Assert.False(plain.HasLegend);
        Assert.True(legend.HasLegend);
        Assert.True(legend.SlotsBottom + 40f <= legend.Bounds.Max.Y + 0.01f);
    }

    [Fact]
    public void PegIndicesAreDenseAcrossTheTriangle()
    {
        Assert.Equal(168, PlinkoBoardLayout.PegCount(16));
        var expected = 0;
        for (var row = 0; row < 16; row++)
        {
            for (var column = 0; column < PlinkoBoardLayout.PegsInRow(row); column++)
            {
                Assert.Equal(expected, PlinkoBoardLayout.PegIndex(row, column));
                expected++;
            }
        }
    }

    [Fact]
    public void AResendTargetsOnlyTheSameBankroll()
    {
        var held = new PlinkoDropRequest("s1", "c1", 16, PlinkoRules.High, 1000, 0);
        Assert.True(CasinoPlinkoStore.Resends(held, "s1"));
        Assert.False(CasinoPlinkoStore.Resends(held, "s2"));
        Assert.False(CasinoPlinkoStore.Resends(null, "s1"));
        Assert.False(CasinoPlinkoStore.Resends(held, string.Empty));
    }

    private static PlinkoDrop Drop(int rows, int slot)
    {
        var tenths = PlinkoRules.MultiplierTenths(rows, PlinkoRules.Medium, slot);
        return new PlinkoDrop(rows, PlinkoRules.Medium, slot, tenths, 1000, PlinkoRules.Payout(1000, tenths), "r");
    }

    private static int Bits(int[] path)
    {
        var bits = 0;
        for (var row = 0; row < path.Length; row++)
        {
            bits |= path[row] << row;
        }

        return bits;
    }

    private static bool Run(PlinkoFlight flight, int rows, out PlinkoEvent landing)
    {
        landing = default;
        for (var frame = 0; frame < 600; frame++)
        {
            flight.Advance(1f / 60f);
            for (var index = 0; index < flight.EventCount; index++)
            {
                var entry = flight.Event(index);
                if (entry.Kind == PlinkoEventKind.Landed && entry.Drop.Rows == rows)
                {
                    landing = entry;
                    return true;
                }
            }
        }

        return false;
    }
}
