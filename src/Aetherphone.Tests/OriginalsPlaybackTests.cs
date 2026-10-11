using Aetherphone.Apps.Casino.Originals;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class OriginalsPlaybackTests
{
    [Fact]
    public void MinesFlipsEachSafeRevealAndSettlesOnceOnCashOut()
    {
        var board = new MinesBoard();
        Assert.True(board.Apply(Mines(0, Array.Empty<int>()), false));
        Assert.True(board.Live);
        Assert.False(board.CanCashOut);

        Assert.True(board.Apply(Mines(0, new[] { 12 }, multiplier: 11400), false));
        Assert.Equal(MinesTile.Safe, board.Tile(12));
        Assert.True(board.Animating);
        Assert.True(board.TakeSafeReveal(out var step));
        Assert.Equal(1, step);
        Assert.Equal(12, board.NewestTile);
        Assert.False(board.TakeSafeReveal(out _));
        Assert.True(board.CanCashOut);
        Assert.Equal(1140, board.CashOutValue);

        board.Apply(Mines(1, new[] { 12 }, new[] { 0, 5, 9 }, multiplier: 11400, payout: 1140), false);
        Assert.False(board.Live);
        Assert.False(board.TakeSettled(out _));
        Advance(board, 2f);
        Assert.Equal(MinesTile.Mine, board.Tile(5));
        Assert.True(board.TakeSettled(out var outcome));
        Assert.Equal(1000, outcome.Stake);
        Assert.Equal(1140, outcome.Payout);
        Assert.Equal("m1", outcome.RoundId);
        Assert.False(board.TakeSettled(out _));
    }

    [Fact]
    public void MinesMarksTheMineThatBustedAndRevealsTheRest()
    {
        var board = new MinesBoard();
        board.Apply(Mines(0, new[] { 3 }), true);
        board.Apply(Mines(2, new[] { 3, 7 }, new[] { 7, 8, 9 }), false);
        Assert.Equal(MinesTile.Boom, board.Tile(7));
        Assert.Equal(MinesTile.Mine, board.Tile(8));
        Assert.Equal(1, board.SafePicks);
        Assert.True(board.TakeBust());
        Assert.False(board.TakeBust());
        board.Snap();
        Assert.False(board.Animating);
        Assert.True(board.TakeSettled(out var outcome));
        Assert.Equal(0, outcome.Payout);
    }

    [Fact]
    public void MinesResumesAnOpenRoundWithoutReplayingOrSettling()
    {
        var board = new MinesBoard();
        Assert.True(board.Resume(Mines(0, new[] { 1, 2, 3 }, multiplier: 14700)));
        Assert.True(board.Live);
        Assert.False(board.Animating);
        Assert.False(board.TakeSafeReveal(out _));
        Assert.Equal(3, board.SafePicks);
        Assert.False(board.TakeSettled(out _));
        Assert.False(board.Resume(Mines(1, new[] { 1 })));
    }

    [Fact]
    public void MinesRejectsTilesOffTheBoard()
    {
        var board = new MinesBoard();
        Assert.False(board.Apply(Mines(0, new[] { 25 }), false));
        Assert.False(board.Apply(Mines(0, new[] { -1 }), false));
        Assert.False(board.Apply(Mines(0, Array.Empty<int>()) with { Mines = 25 }, false));
        Assert.False(board.Apply(Mines(0, Array.Empty<int>()) with { Granted = false }, false));
        Assert.False(board.HasRound);
    }

    [Fact]
    public void DiceSlidesOntoTheServerRollAndSettlesOnce()
    {
        var playback = new DiceRollPlayback();
        Assert.True(playback.Begin(Dice(7312, true, 2000), false));
        Assert.True(playback.Rolling);
        Assert.False(playback.TakeSettled(out _));
        var ticks = 0;
        for (var frame = 0; frame < 60; frame++)
        {
            playback.Advance(1f / 60f);
            if (playback.TakeTick())
            {
                ticks++;
            }
        }

        Assert.False(playback.Rolling);
        Assert.Equal(7312f, playback.Marker);
        Assert.True(ticks > 0);
        Assert.True(playback.TakeSettled(out var outcome));
        Assert.Equal(2000, outcome.Payout);
        Assert.False(playback.TakeSettled(out _));
    }

    [Fact]
    public void DiceInstantModeLandsAtOnce()
    {
        var playback = new DiceRollPlayback();
        Assert.True(playback.Begin(Dice(150, false, 0), true));
        Assert.False(playback.Rolling);
        Assert.Equal(150, playback.Roll);
        Assert.True(playback.TakeSettled(out _));
        Assert.False(playback.Begin(Dice(10001, false, 0), true));
    }

    [Fact]
    public void LimboClimbsMonotonicallyAndStopsOnTheResult()
    {
        var playback = new LimboClimbPlayback();
        Assert.True(playback.Begin(new CasinoLimboDto(true, string.Empty, "l1", 1000, 200, 34700, true, 2000), false));
        var previous = playback.Display;
        Assert.Equal(OriginalsRules.LimboMinResult, previous);
        while (playback.Climbing)
        {
            playback.Advance(1f / 60f);
            Assert.True(playback.Display >= previous);
            previous = playback.Display;
        }

        Assert.Equal(34700, playback.Display);
        Assert.True(playback.TakeSettled(out var outcome));
        Assert.Equal(2000, outcome.Payout);
        Assert.InRange(LimboClimbPlayback.DurationFor(100), LimboClimbPlayback.MinSeconds,
            LimboClimbPlayback.MinSeconds);
        Assert.Equal(LimboClimbPlayback.MaxSeconds, LimboClimbPlayback.DurationFor(100_000_000));
    }

    [Fact]
    public void KenoPopsTheTenDrawnTilesInOrderAndCountsHits()
    {
        var playback = new KenoDrawPlayback();
        Assert.True(playback.Begin(Keno(), false));
        Assert.False(playback.IsShown(17));
        var hits = new List<bool>();
        for (var frame = 0; frame < 240 && playback.Drawing; frame++)
        {
            playback.Advance(1f / 60f);
            while (playback.TakeReveal(out var hit, out _))
            {
                hits.Add(hit);
            }
        }

        Assert.Equal(10, hits.Count);
        Assert.True(hits[0]);
        Assert.False(hits[1]);
        Assert.True(hits[3]);
        Assert.Equal(2, playback.HitsShown);
        Assert.True(playback.IsHit(9));
        Assert.True(playback.IsShown(2));
        Assert.False(playback.IsHit(2));
        Assert.True(playback.TakeSettled(out var outcome));
        Assert.Equal(3100, outcome.Payout);
    }

    [Fact]
    public void KenoInstantModeShowsTheWholeDraw()
    {
        var playback = new KenoDrawPlayback();
        Assert.True(playback.Begin(Keno(), true));
        Assert.False(playback.Drawing);
        Assert.Equal(10, playback.Shown);
        Assert.Equal(2, playback.HitsShown);
        Assert.False(playback.Begin(Keno() with { Drawn = new[] { 1, 1, 2, 3, 4, 5, 6, 7, 8, 9 } }, true));
    }

    [Fact]
    public void HiLoBuildsTheChainFromTheMovesAndOffersTheServerCalls()
    {
        var chain = new HiLoChain();
        Assert.True(chain.Apply(HiLo(0, new[] { 19 }, Array.Empty<string>()), false));
        Assert.True(chain.Live);
        Assert.False(chain.CanCashOut);
        Assert.Equal(2, chain.OptionCount);
        Assert.Equal(HiLoCall.Higher, chain.Option(0).Call);

        Assert.True(chain.Apply(HiLo(0, new[] { 19, 21, 15 }, new[] { "higher", "lower" }, 30673), false));
        Assert.True(chain.Animating);
        Assert.Equal(HiLoStep.Won, chain.StepResult(0));
        Assert.Equal(HiLoStep.Won, chain.StepResult(1));
        Assert.Equal(143, chain.ChainHundredths(1));
        Assert.Equal(306, chain.ChainHundredths(2));
        Assert.Equal(2, chain.Step);
        chain.Advance(1f);
        Assert.True(chain.TakeLanded());
        Assert.True(chain.CanCashOut);
        Assert.Equal(3067, chain.CashOutValue);
    }

    [Fact]
    public void HiLoBustSettlesQuietlyAfterTheFlip()
    {
        var chain = new HiLoChain();
        chain.Apply(HiLo(0, new[] { 51 }, Array.Empty<string>()), true);
        chain.Apply(HiLo(2, new[] { 51, 42 }, new[] { "same" }), false);
        Assert.False(chain.TakeSettled(out _));
        chain.Advance(1f);
        Assert.True(chain.TakeBust());
        Assert.True(chain.TakeSettled(out var outcome));
        Assert.Equal(0, outcome.Payout);
        Assert.Equal(HiLoStep.Lost, chain.StepResult(0));
    }

    [Fact]
    public void HiLoResumesMidRoundWithoutFlippingOrSettling()
    {
        var chain = new HiLoChain();
        Assert.True(chain.Resume(HiLo(0, new[] { 21, 18, 26, 32 }, new[] { "skip", "skip", "skip" })));
        Assert.False(chain.Animating);
        Assert.False(chain.TakeLanded());
        Assert.False(chain.TakeSettled(out _));
        Assert.Equal(32, chain.Current);
        Assert.False(chain.Resume(HiLo(1, new[] { 21 }, Array.Empty<string>())));
        Assert.False(chain.Apply(HiLo(0, new[] { 21, 18 }, Array.Empty<string>()), true));
        Assert.False(chain.Apply(HiLo(0, new[] { 52 }, Array.Empty<string>()), true));
    }

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = OriginalsPicker.FromSeed(42);
        var second = OriginalsPicker.FromSeed(42);
        var open = new bool[OriginalsRules.MinesTiles];
        Array.Fill(open, true);
        open[0] = false;
        open[24] = false;
        Span<int> firstPicks = stackalloc int[OriginalsRules.KenoMaxPicks];
        Span<int> secondPicks = stackalloc int[OriginalsRules.KenoMaxPicks];
        for (var round = 0; round < 20; round++)
        {
            var tile = first.PickOne(open);
            Assert.Equal(tile, second.PickOne(open));
            Assert.InRange(tile, 1, 23);
            Assert.Equal(first.PickDistinct(firstPicks, OriginalsRules.KenoTiles),
                second.PickDistinct(secondPicks, OriginalsRules.KenoTiles));
            Assert.True(firstPicks.SequenceEqual(secondPicks));
            Assert.True(OriginalsRules.AreKenoPicks(firstPicks));
        }

        Assert.Equal(-1, first.PickOne(new bool[3]));
    }

    private static void Advance(MinesBoard board, float seconds)
    {
        for (var elapsed = 0f; elapsed < seconds; elapsed += 1f / 60f)
        {
            board.Advance(1f / 60f);
        }
    }

    private static CasinoMinesDto Mines(int phase, int[] revealed, int[]? mineTiles = null, long multiplier = 0,
        long payout = 0) =>
        new(true, string.Empty, "m1", 1000, 3, phase, revealed, mineTiles ?? Array.Empty<int>(), multiplier, 0,
            payout, "hex", 199000);

    private static CasinoDiceDto Dice(int roll, bool won, long payout) =>
        new(true, string.Empty, "d1", 1000, 5050, true, 4950, roll, won, 20000, payout, "hex", 201000);

    private static CasinoKenoDto Keno() =>
        new(true, string.Empty, "k1", 1000, 0, new[] { 3, 9, 17 }, new[] { 17, 2, 30, 9, 11, 0, 25, 39, 14, 6 }, 2,
            31000, 3100, "hex", 202100);

    private static CasinoHiLoDto HiLo(int phase, int[] cards, string[] moves, long multiplier = 10000)
    {
        var calls = phase == OriginalsRules.PhaseLive
            ? new[]
            {
                new CasinoHiLoCallDto("higher", 7692, 12870),
                new CasinoHiLoCallDto("lower", 3846, 25740),
            }
            : Array.Empty<CasinoHiLoCallDto>();
        return new CasinoHiLoDto(true, string.Empty, "h1", 1000, phase, moves.Length, cards, moves,
            cards[^1], multiplier, calls, 0, "hex", 199000);
    }
}
