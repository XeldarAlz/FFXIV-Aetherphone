using Aetherphone.Apps.Casino.Cabinets;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ScratchPlaybackTests
{
    private static readonly int[] WinnerCells = { 2, 0, 0, 1, 1, 3, 3, 2, 2 };
    private static readonly int[] LoserCells = { 0, 0, 1, 1, 2, 2, 3, 3, 4 };

    [Fact]
    public void BeginRejectsRefusalsAndMalformedCards()
    {
        var playback = new ScratchCardPlayback();
        Assert.False(playback.Begin(Card(WinnerCells, prize: 50) with { Granted = false }));
        Assert.False(playback.Begin(Card(new[] { 0, 1, 2 }, prize: 0)));
        Assert.False(playback.Begin(Card(new[] { 0, 1, 2, 3, 4, 5, 9, 0, 1 }, prize: 0)));
        Assert.False(playback.Begin(Card(WinnerCells, prize: 0)));
        Assert.False(playback.Begin(Card(LoserCells, prize: 50)));
        Assert.Equal(ScratchPhase.Idle, playback.Phase);
    }

    [Fact]
    public void ThePrizeStaysSealedUntilTheThirdMatchIsOnTheTable()
    {
        var playback = new ScratchCardPlayback();
        Assert.True(playback.Begin(Card(WinnerCells, prize: 50, stack: 140)));
        Assert.Equal(0, playback.PrizeOnceRevealed);
        Assert.Equal(-1, playback.WinningSymbolOnceMatched);

        playback.Rub(0, 1f);
        playback.Rub(7, 1f);
        Assert.Equal(0, playback.PrizeOnceRevealed);
        Assert.Equal(-1, playback.WinningSymbolOnceMatched);
        Assert.False(playback.TakeWinCelebration(out _));

        playback.Rub(8, 1f);
        Assert.True(playback.TakeWinCelebration(out var prize));
        Assert.Equal(50, prize);
        Assert.Equal(50, playback.PrizeOnceRevealed);
        Assert.Equal(2, playback.WinningSymbolOnceMatched);
        Assert.False(playback.TakeWinCelebration(out _));
    }

    [Fact]
    public void RevealingDecoysFirstNeverCelebratesEarly()
    {
        var playback = new ScratchCardPlayback();
        Assert.True(playback.Begin(Card(WinnerCells, prize: 50)));
        playback.Rub(1, 1f);
        playback.Rub(2, 1f);
        playback.Rub(3, 1f);
        playback.Rub(4, 1f);
        playback.Rub(5, 1f);
        playback.Rub(6, 1f);
        Assert.False(playback.TakeWinCelebration(out _));
        Assert.Equal(0, playback.PrizeOnceRevealed);
    }

    [Fact]
    public void TheHeldBackPrizeLastsExactlyUntilEveryCellIsRevealed()
    {
        var playback = new ScratchCardPlayback();
        Assert.True(playback.Begin(Card(WinnerCells, prize: 50, stack: 140)));
        for (var cellIndex = 0; cellIndex < ScratchRules.CellCount - 1; cellIndex++)
        {
            playback.Rub(cellIndex, 1f);
            Assert.Equal(50, playback.PrizeStillUnderFoil);
        }

        playback.Rub(ScratchRules.CellCount - 1, 1f);
        Assert.True(playback.RevealComplete);
        Assert.Equal(0, playback.PrizeStillUnderFoil);
    }

    [Fact]
    public void ALosingCardStaysQuietAndHoldsNothingBack()
    {
        var playback = new ScratchCardPlayback();
        Assert.True(playback.Begin(Card(LoserCells, prize: 0, stack: 90)));
        Assert.Equal(0, playback.PrizeStillUnderFoil);
        playback.RevealAll();
        Assert.True(playback.RevealComplete);
        Assert.False(playback.TakeWinCelebration(out _));
        Assert.Equal(0, playback.PrizeOnceRevealed);
        Assert.Equal(0, playback.PrizeStillUnderFoil);
    }

    [Fact]
    public void PartialRubsLeaveTheFoilInPlace()
    {
        var playback = new ScratchCardPlayback();
        Assert.True(playback.Begin(Card(WinnerCells, prize: 50)));
        playback.Rub(0, 0.5f);
        Assert.False(playback.IsRevealed(0));
        Assert.Equal(0.5f, playback.FoilRemaining(0), 3);
        playback.Rub(0, 0.6f);
        Assert.True(playback.IsRevealed(0));
        Assert.Equal(0f, playback.FoilRemaining(0));
    }

    [Fact]
    public void RevealAllSettlesTheCardAndFiresTheCelebrationForWinners()
    {
        var playback = new ScratchCardPlayback();
        Assert.True(playback.Begin(Card(WinnerCells, prize: 50, stack: 140)));
        playback.RevealAll();
        Assert.True(playback.RevealComplete);
        Assert.True(playback.TakeWinCelebration(out var prize));
        Assert.Equal(50, prize);
        Assert.Equal(0, playback.PrizeStillUnderFoil);
    }

    [Fact]
    public void RevealNextUncoversOneCellAtATimeAndSettlesOnTheLast()
    {
        var playback = new ScratchCardPlayback();
        Assert.True(playback.Begin(Card(WinnerCells, prize: 50)));
        for (var step = 1; step < ScratchRules.CellCount; step++)
        {
            playback.RevealNext();
            Assert.True(playback.IsRevealed(step - 1));
            Assert.False(playback.IsRevealed(step));
            Assert.Equal(ScratchPhase.Scratching, playback.Phase);
        }

        playback.RevealNext();
        Assert.Equal(ScratchPhase.Settled, playback.Phase);
        Assert.True(playback.TakeWinCelebration(out _));
        playback.RevealNext();
        Assert.Equal(ScratchPhase.Settled, playback.Phase);
    }

    [Fact]
    public void MatchedCellHighlightsOnlyLightAfterTheThirdMatch()
    {
        var playback = new ScratchCardPlayback();
        Assert.True(playback.Begin(Card(WinnerCells, prize: 50)));
        playback.Rub(0, 1f);
        Assert.False(playback.IsMatchedCell(0));
        playback.Rub(7, 1f);
        playback.Rub(8, 1f);
        Assert.True(playback.IsMatchedCell(0));
        Assert.True(playback.IsMatchedCell(8));
        Assert.False(playback.IsMatchedCell(1));
    }

    [Fact]
    public void ClearReturnsTheCardToIdleWithNothingPending()
    {
        var playback = new ScratchCardPlayback();
        Assert.True(playback.Begin(Card(WinnerCells, prize: 50)));
        playback.RevealAll();
        playback.Clear();
        Assert.Equal(ScratchPhase.Idle, playback.Phase);
        Assert.False(playback.TakeWinCelebration(out _));
        Assert.Equal(0, playback.PrizeStillUnderFoil);
    }

    [Fact]
    public void ThePrizeIsHeldBackByTheCardOnScreenAndNeverByTheStoredStack()
    {
        var playback = new ScratchCardPlayback();
        Assert.True(playback.Begin(Card(WinnerCells, prize: 50, stack: 140)));
        Assert.Equal(50, playback.PrizeStillUnderFoil);

        var refreshed = State(sittingId: "sit1", stack: 140);
        var absorbed = CasinoStore.StackAbsorbedInto(refreshed, "sit1", 140);
        Assert.Null(absorbed);
        Assert.Equal(140, refreshed.Sitting!.Stack);
        Assert.Equal(90, refreshed.Sitting.Stack - playback.PrizeStillUnderFoil);

        playback.RevealAll();
        Assert.Equal(140, refreshed.Sitting.Stack - playback.PrizeStillUnderFoil);
    }

    private static CasinoStateDto State(string sittingId, long stack)
    {
        return new CasinoStateDto(
            Sitting: new CasinoSittingDto(Id: sittingId, GameKind: CasinoWire.ScratchKind, Stack: stack));
    }

    private static CasinoScratchCardDto Card(int[] cells, long prize, long stack = 100)
    {
        return new CasinoScratchCardDto(
            Granted: true,
            Reason: string.Empty,
            RoundId: "r2",
            Tier: 1,
            Cells: cells,
            Prize: prize,
            NextSeedHash: "next",
            Stack: stack);
    }
}
