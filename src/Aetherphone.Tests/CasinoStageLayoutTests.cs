using System.Numerics;
using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoStageLayoutTests
{
    private static readonly Rect Phone = new(new Vector2(100f, 50f), new Vector2(460f, 830f));

    [Fact]
    public void TheChromeBandIsTheTopFiftyTwo()
    {
        var layout = CasinoStageLayout.Compute(Phone, false, false, 0f, 1f);
        Assert.Equal(50f, layout.Band.Min.Y);
        Assert.Equal(102f, layout.Band.Max.Y);
        Assert.Equal(new Vector2(128f, 76f), layout.BackCenter);
        Assert.Equal(new Vector2(432f, 76f), layout.InfoCenter);
        Assert.Equal(Phone.Center.X, layout.CapsuleCenter.X);
    }

    [Fact]
    public void TheDeckTakesTheBottomAndSafeSitsBetweenItAndTheBand()
    {
        var layout = CasinoStageLayout.Compute(Phone, false, false, CasinoStageLayout.DeckHeight, 1f);
        Assert.Equal(830f - CasinoStageLayout.DeckHeight, layout.Deck.Min.Y);
        Assert.Equal(102f + CasinoStageLayout.SafeInset, layout.Safe.Min.Y);
        Assert.Equal(layout.Deck.Min.Y - CasinoStageLayout.SafeInset, layout.Safe.Max.Y);
        Assert.Equal(112f, layout.Safe.Min.X);
        Assert.Equal(448f, layout.Safe.Max.X);
    }

    [Fact]
    public void RoomsAndPracticeTablesPushSafeDown()
    {
        var plain = CasinoStageLayout.Compute(Phone, false, false, CasinoStageLayout.DeckHeight, 1f);
        var room = CasinoStageLayout.Compute(Phone, true, true, CasinoStageLayout.DeckHeight, 1f);
        Assert.True(room.HasRibbon);
        Assert.True(room.HasPractice);
        Assert.Equal(plain.Safe.Min.Y + CasinoStageLayout.RibbonHeight + CasinoStageLayout.PracticeRibbonHeight,
            room.Safe.Min.Y);
        Assert.Equal(room.Ribbon.Max.Y, room.Practice.Min.Y);
    }

    [Fact]
    public void ScaleStretchesEveryRegion()
    {
        var layout = CasinoStageLayout.Compute(Phone, false, false, CasinoStageLayout.DeckHeight, 2f);
        Assert.Equal(50f + CasinoStageLayout.ChromeBand * 2f, layout.Band.Max.Y);
        Assert.Equal(830f - CasinoStageLayout.DeckHeight * 2f, layout.Deck.Min.Y);
    }

    [Fact]
    public void ChromeHitsCoverTheChipsAndTheCapsuleOnly()
    {
        var layout = CasinoStageLayout.Compute(Phone, false, false, CasinoStageLayout.DeckHeight, 1f);
        Assert.True(layout.ChromeContains(layout.BackCenter));
        Assert.True(layout.ChromeContains(layout.InfoCenter));
        Assert.True(layout.ChromeContains(layout.CapsuleCenter));
        Assert.False(layout.ChromeContains(layout.Safe.Center));
    }

    [Fact]
    public void ATinyScreenNeverInvertsSafe()
    {
        var tiny = new Rect(Vector2.Zero, new Vector2(80f, 120f));
        var layout = CasinoStageLayout.Compute(tiny, true, true, CasinoStageLayout.DeckHeight, 1f);
        Assert.True(layout.Safe.Max.X >= layout.Safe.Min.X);
        Assert.True(layout.Safe.Max.Y >= layout.Safe.Min.Y);
    }

    [Fact]
    public void TheBetsLogKeepsTheNewestFiftyOncePerRound()
    {
        var log = new CasinoBetsLog();
        for (var index = 0; index < 60; index++)
        {
            log.Record(new CasinoBetRecord(L.Casino.GameScratch, 1000, index * 100, "round" + index, index));
        }

        log.Record(new CasinoBetRecord(L.Casino.GameScratch, 1000, 5000, "round59", 99));
        log.Record(new CasinoBetRecord(L.Casino.GameScratch, 0, 5000, "free", 99));
        Assert.Equal(CasinoBetsLog.Capacity, log.Count);
        Assert.Equal("round59", log.Newest(0).RoundId);
        Assert.Equal("round10", log.Newest(CasinoBetsLog.Capacity - 1).RoundId);
    }

    [Fact]
    public void ABetRecordReadsItsMultipleInHundredths()
    {
        var win = new CasinoBetRecord(L.Casino.GameWheel, 1000, 2500, "a", 0);
        var push = new CasinoBetRecord(L.Casino.GameWheel, 1000, 1000, "b", 0);
        Assert.Equal(250, win.MultipleHundredths);
        Assert.True(win.Won);
        Assert.False(push.Won);
    }

    [Fact]
    public void BalanceTitlesFollowTheStandardLadder()
    {
        Assert.Equal(BalanceTitle.None, StatusTitle.For(999_999));
        Assert.Equal(BalanceTitle.Shark, StatusTitle.For(1_000_000));
        Assert.Equal(BalanceTitle.HighRoller, StatusTitle.For(10_000_000));
        Assert.Equal(BalanceTitle.Vip, StatusTitle.For(250_000_000));
        Assert.Equal(BalanceTitle.Whale, StatusTitle.For(1_000_000_000));
        Assert.Equal(BalanceTitle.Legend, StatusTitle.For(3_000_000_000_000));
    }

    [Fact]
    public void ChipStacksUseTheTenDenominationsAndFiveDiscColumns()
    {
        Span<int> counts = stackalloc int[ChipStack.DenominationCount];
        Assert.Equal(10, ChipStack.Denominations.Length);
        Assert.Equal(3, ChipStack.Breakdown(111_000_000, counts));
        Assert.Equal(1, counts[0]);
        Assert.Equal(1, counts[1]);
        Assert.Equal(1, counts[2]);
        Assert.Equal(2, ChipStack.Breakdown(900, counts));
        Assert.Equal(1, counts[8]);
        Assert.Equal(4, counts[9]);
        Assert.Equal(1, ChipStack.Breakdown(900_000_000, counts));
        Assert.Equal(ChipStack.MaxDiscsPerColumn, counts[0]);
        Assert.Equal(1, ChipStack.Breakdown(50, counts));
        Assert.Equal(1, counts[ChipStack.DenominationCount - 1]);
    }
}
