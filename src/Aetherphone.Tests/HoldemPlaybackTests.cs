using System.Numerics;
using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HoldemPlaybackTests
{
    [Fact]
    public void JoiningMidHandSnapsWithoutFlights()
    {
        var playback = new HoldemPlayback();
        playback.Advance(State("h1", HoldemPhases.Turn, new[] { 1, 2, 3, 4 }), false);

        Assert.False(playback.TryTakeLaunch(out _));
        Assert.True(playback.BoardDealt(3));
        Assert.True(playback.HoleDealt(0));
        Assert.False(playback.TakeHandStarted());
    }

    [Fact]
    public void ANewHandDealsTwoPassesFromLeftOfTheButton()
    {
        var playback = new HoldemPlayback();
        playback.Advance(State(string.Empty, HoldemPhases.Waiting, Array.Empty<int>()), false);
        playback.Advance(State("h1", HoldemPhases.Preflop, Array.Empty<int>()), false);

        Assert.True(playback.TakeHandStarted());
        var seats = new List<int>();
        var lastDelay = -1f;
        while (playback.TryTakeLaunch(out var launch))
        {
            Assert.Equal(HoldemLaunchKind.Hole, launch.Kind);
            Assert.True(launch.Delay > lastDelay);
            lastDelay = launch.Delay;
            seats.Add(launch.Seat);
        }

        Assert.Equal(new[] { 1, 2, 0, 1, 2, 0 }, seats);
    }

    [Fact]
    public void StreetsRevealAtTheServerPace()
    {
        var playback = new HoldemPlayback();
        playback.Advance(State("h1", HoldemPhases.Preflop, Array.Empty<int>()), false);
        while (playback.TryTakeLaunch(out _))
        {
        }

        playback.Advance(State("h1", HoldemPhases.Flop, new[] { 5, 6, 7 }), false);
        var delays = new List<float>();
        while (playback.TryTakeLaunch(out var launch))
        {
            Assert.Equal(HoldemLaunchKind.Board, launch.Kind);
            delays.Add(launch.Delay);
        }

        Assert.Equal(new[] { 0f, 0.8f, 1.6f }, delays);
        playback.Advance(State("h1", HoldemPhases.Turn, new[] { 5, 6, 7, 8 }), false);
        Assert.True(playback.TryTakeLaunch(out var turn));
        Assert.Equal(3, turn.Slot);
        Assert.Equal(0f, turn.Delay);
    }

    [Fact]
    public void BetsSweepIntoThePotWhenTheStreetCloses()
    {
        var playback = new HoldemPlayback();
        playback.Advance(State("h1", HoldemPhases.Preflop, Array.Empty<int>(), 100), false);
        playback.Advance(State("h1", HoldemPhases.Flop, new[] { 5, 6, 7 }), false);
        var amounts = new long[HoldemRules.MaxSeats];

        Assert.True(playback.TryTakeSweep(amounts));
        Assert.Equal(100, amounts[0]);
        Assert.False(playback.TryTakeSweep(amounts));
    }

    [Fact]
    public void TheResultFiresOnceAndOnlyLiveWhenWitnessed()
    {
        var live = new HoldemPlayback();
        live.Advance(State("h1", HoldemPhases.River, new[] { 1, 2, 3, 4, 5 }), false);
        live.Advance(State("h1", HoldemPhases.Intermission, new[] { 1, 2, 3, 4, 5 }), false);
        Assert.True(live.TryTakeResult(out var witnessed));
        Assert.True(witnessed);
        live.Advance(State("h1", HoldemPhases.Intermission, new[] { 1, 2, 3, 4, 5 }), false);
        Assert.False(live.TryTakeResult(out _));

        var joined = new HoldemPlayback();
        joined.Advance(State("h1", HoldemPhases.Intermission, new[] { 1, 2, 3, 4, 5 }), false);
        Assert.True(joined.TryTakeResult(out var late));
        Assert.False(late);
    }

    [Fact]
    public void SnapToTruthNeverLaunches()
    {
        var playback = new HoldemPlayback();
        playback.Advance(State("h1", HoldemPhases.Preflop, Array.Empty<int>()), false);
        while (playback.TryTakeLaunch(out _))
        {
        }

        playback.Advance(State("h1", HoldemPhases.River, new[] { 1, 2, 3, 4, 5 }), true);
        Assert.False(playback.TryTakeLaunch(out _));
        Assert.True(playback.BoardDealt(4));
    }

    [Fact]
    public void AVoidedHandIsAnnouncedOnce()
    {
        var playback = new HoldemPlayback();
        playback.Advance(State("h1", HoldemPhases.Flop, new[] { 1, 2, 3 }), false);
        playback.Advance(State("h1", HoldemPhases.Voided, new[] { 1, 2, 3 }), false);
        Assert.True(playback.TakeVoided());
        Assert.False(playback.TakeVoided());
    }

    [Fact]
    public void ShownCardsAreRevealedOnce()
    {
        var playback = new HoldemPlayback();
        playback.Advance(State("h1", HoldemPhases.River, new[] { 1, 2, 3, 4, 5 }), false);
        var showdown = State("h1", HoldemPhases.Showdown, new[] { 1, 2, 3, 4, 5 }) with
        {
            Seats = new[]
            {
                new CasinoHoldemSeatDto(0, "a", State: HoldemSeatStates.InHand, Cards: new[] { 10, 11 }, Shown: true),
            },
        };
        playback.Advance(showdown, false);
        Assert.True(playback.TryTakeReveal(out var seat));
        Assert.Equal(0, seat);
        Assert.False(playback.TryTakeReveal(out _));
    }

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = new Vector2[HoldemPotScatter.Count];
        var second = new Vector2[HoldemPotScatter.Count];
        HoldemPotScatter.Fill(HoldemPotScatter.SeedOf(42), first);
        HoldemPotScatter.Fill(HoldemPotScatter.SeedOf(42), second);
        Assert.Equal(first, second);
        var other = new Vector2[HoldemPotScatter.Count];
        HoldemPotScatter.Fill(HoldemPotScatter.SeedOf(43), other);
        Assert.NotEqual(first, other);
    }

    [Fact]
    public void TheRingSeatsTheHeroAtTheBottomForEveryTableSize()
    {
        var layout = new HoldemTableLayout();
        var safe = new Rect(new Vector2(0f, 0f), new Vector2(340f, 460f));
        for (var seats = HoldemRules.MinSeats; seats <= HoldemRules.MaxSeats; seats++)
        {
            for (var hero = 0; hero < seats; hero++)
            {
                layout.Compute(safe, seats, hero, 1f);
                var bottom = layout.SeatCenter(hero);
                for (var seat = 0; seat < seats; seat++)
                {
                    Assert.True(layout.SeatCenter(seat).Y <= bottom.Y + 0.01f);
                    Assert.InRange(layout.SeatCenter(seat).X, safe.Min.X, safe.Max.X);
                    Assert.InRange(layout.SeatCenter(seat).Y, safe.Min.Y, safe.Max.Y);
                }

                Assert.InRange(layout.BoardCardWidth, HoldemTableLayout.BoardCardMin, HoldemTableLayout.BoardCardMax);
                Assert.True(layout.Shelf.Max.Y <= safe.Max.Y + 0.01f);
                Assert.True(layout.HeroCardsCenter.Y < bottom.Y);
            }
        }
    }

    [Fact]
    public void TheBoardStaysClearOfSideSeats()
    {
        var layout = new HoldemTableLayout();
        layout.Compute(new Rect(new Vector2(0f, 0f), new Vector2(340f, 460f)), 9, 0, 1f);
        var right = layout.BoardSlot(HoldemRules.BoardSize - 1).X + layout.BoardCardWidth * 0.5f;
        Assert.True(right <= 340f);
        Assert.True(layout.BoardSlot(0).X - layout.BoardCardWidth * 0.5f >= 0f);
    }

    private static CasinoHoldemRoomStateDto State(string handId, int phase, int[] board, long bet = 0)
    {
        return new CasinoHoldemRoomStateDto(
            HandId: handId,
            Phase: phase,
            Button: 0,
            BigBlind: 100,
            Board: board,
            Seats: new[]
            {
                new CasinoHoldemSeatDto(0, "a", State: HoldemSeatStates.InHand, Bet: bet, Cards: new[] { -1, -1 }),
                new CasinoHoldemSeatDto(1, "b", State: HoldemSeatStates.InHand, Cards: new[] { -1, -1 }),
                new CasinoHoldemSeatDto(2, "c", State: HoldemSeatStates.InHand, Cards: new[] { -1, -1 }),
            });
    }
}
