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
    public void LandscapeChromeIsTheBackChipOnly()
    {
        var wide = new Rect(Vector2.Zero, new Vector2(844f, 390f));
        var layout = CasinoStageLayout.Compute(wide, false, false, 0f, 1f);
        Assert.True(layout.Landscape);
        Assert.True(layout.ChromeContains(layout.BackCenter));
        Assert.False(layout.ChromeContains(layout.InfoCenter));
        Assert.False(layout.ChromeContains(layout.CapsuleCenter));
        Assert.False(CasinoStageLayout.Compute(Phone, false, false, 0f, 1f).Landscape);
    }

    [Fact]
    public void TheCapsuleDropsItsCoinValueBeforeItOutgrowsTheBand()
    {
        var center = new Vector2(200f, 26f);
        const float chrome = 50f;
        const float text = 60f;
        const float full = 90f;
        const float compact = 40f;
        const float gap = 8f;
        var roomy = ChromeCapsule.Fit(center, 34f, 400f, chrome, text, full, compact, gap);
        Assert.Equal(CapsuleCoins.Full, roomy.Coins);
        Assert.Equal(chrome + text + gap + full, roomy.Rect.Width, 3);
        Assert.Equal(text, roomy.TextRoom, 3);

        var compactFit = ChromeCapsule.Fit(center, 34f, chrome + text + gap + full - 1f, chrome, text, full, compact,
            gap);
        Assert.Equal(CapsuleCoins.Compact, compactFit.Coins);
        Assert.Equal(chrome + text + gap + compact, compactFit.Rect.Width, 3);

        var bare = ChromeCapsule.Fit(center, 34f, chrome + text + gap + compact - 1f, chrome, text, full, compact, gap);
        Assert.Equal(CapsuleCoins.None, bare.Coins);
        Assert.Equal(chrome + text, bare.Rect.Width, 3);

        var squeezed = ChromeCapsule.Fit(center, 34f, chrome + text - 20f, chrome, text, full, compact, gap);
        Assert.Equal(CapsuleCoins.None, squeezed.Coins);
        Assert.Equal(chrome + text - 20f, squeezed.Rect.Width, 3);
        Assert.Equal(text - 20f, squeezed.TextRoom, 3);
        Assert.Equal(center.X, squeezed.Rect.Center.X, 3);
        Assert.Equal(34f, squeezed.Rect.Height, 3);
    }

    [Theory]
    [InlineData(0.75f)]
    [InlineData(1f)]
    [InlineData(1.5f)]
    public void TheWidestCapsuleStillClearsTheBackAndInfoChips(float scale)
    {
        var screen = new Rect(Vector2.Zero, new Vector2(328f * scale, 760f * scale));
        var content = new Rect(new Vector2(16f * scale, 48f * scale), new Vector2(312f * scale, 730f * scale));
        var layout = CasinoStageLayout.Compute(screen, content, true, false, CasinoStageLayout.DeckHeight, scale);
        var capsule = ChromeCapsule.Fit(layout.CapsuleCenter, CasinoStageLayout.CapsuleHeight * scale,
            layout.CapsuleMaxWidth, 60f * scale, 900f * scale, 120f * scale, 50f * scale, 8f * scale);
        Assert.Equal(CapsuleCoins.None, capsule.Coins);
        Assert.True(capsule.Rect.Min.X >= layout.BackCenter.X + layout.ChipRadiusPixels);
        Assert.True(capsule.Rect.Max.X <= layout.InfoCenter.X - layout.ChipRadiusPixels);
        Assert.True(capsule.Rect.Max.Y <= layout.Band.Max.Y);
        Assert.True(capsule.Rect.Max.Y <= layout.Ribbon.Min.Y);
    }

    [Theory]
    [InlineData(0.75f)]
    [InlineData(1f)]
    [InlineData(1.5f)]
    public void TheCappedNoticeSitsAboveTheDeckAndNeverOverTheRoomStrips(float scale)
    {
        var screen = new Rect(Vector2.Zero, new Vector2(328f * scale, 760f * scale));
        var content = new Rect(new Vector2(16f * scale, 48f * scale), new Vector2(312f * scale, 730f * scale));
        var layout = CasinoStageLayout.Compute(screen, content, true, true, 184f, scale);
        var notice = layout.Notice(900f * scale, 34f * scale, scale);
        Assert.True(notice.Max.Y <= layout.Deck.Min.Y - (BetsRail.HandleHeight * 0.5f + DeckActions.Gap) * scale
            + 0.01f);
        Assert.True(notice.Min.Y > layout.Safe.Center.Y);
        Assert.True(notice.Min.Y >= layout.Practice.Max.Y);
        Assert.True(notice.Width <= layout.Safe.Width + 0.01f);
        Assert.Equal(layout.Safe.Center.X, notice.Center.X, 3);

        var bare = CasinoStageLayout.Compute(screen, content, true, false, 0f, scale);
        var bareNotice = bare.Notice(120f * scale, 34f * scale, scale);
        Assert.Equal(bare.Safe.Max.Y, bareNotice.Max.Y, 3);
        Assert.Equal(120f * scale, bareNotice.Width, 3);
    }

    [Theory]
    [InlineData(0.75f)]
    [InlineData(1f)]
    [InlineData(1.5f)]
    public void TheBetsHandleHoldsItsLabelAndOnlyTakesTapsBetweenTheWorldAndTheDeck(float scale)
    {
        var screen = new Rect(Vector2.Zero, new Vector2(328f * scale, 760f * scale));
        var content = new Rect(new Vector2(16f * scale, 48f * scale), new Vector2(312f * scale, 730f * scale));
        var layout = CasinoStageLayout.Compute(screen, content, false, false, 184f, scale);
        var label = DeckLabelMeasure.Width(DeckLabelMeasure.Widest(L.Strip.Bets, scale), scale) * 0.8f;
        var handle = BetsRail.HandleRect(layout.Deck, label, scale);
        Assert.True(handle.Width - handle.Height >= label - 0.01f);
        Assert.True(handle.Width >= BetsRail.HandleWidth * scale - 0.01f);
        Assert.Equal(layout.Deck.Center.X, handle.Center.X, 3);
        Assert.Equal(layout.Deck.Min.Y, handle.Center.Y, 3);
        var hit = BetsRail.HandleHit(handle, scale);
        Assert.True(hit.Min.Y >= layout.Safe.Max.Y - 0.01f);
        Assert.True(hit.Max.Y <= layout.Deck.Min.Y + DeckActions.Pad * scale);
        Assert.True(hit.Height >= handle.Height - 0.01f);
    }

    [Fact]
    public void ATinyScreenNeverInvertsSafe()
    {
        var tiny = new Rect(Vector2.Zero, new Vector2(80f, 120f));
        var layout = CasinoStageLayout.Compute(tiny, true, true, CasinoStageLayout.DeckHeight, 1f);
        Assert.True(layout.Safe.Max.X >= layout.Safe.Min.X);
        Assert.True(layout.Safe.Max.Y >= layout.Safe.Min.Y);
    }

    [Theory]
    [InlineData(0.75f)]
    [InlineData(1f)]
    [InlineData(1.5f)]
    public void TheStageIsFullBleedWhileTheChromeStaysInsideTheContentAndTheDeckSpansTheScreen(float scale)
    {
        var screen = new Rect(new Vector2(84f, 2f), new Vector2(84f + 393f * scale, 2f + 852f * scale));
        var content = new Rect(new Vector2(screen.Min.X + 16f * scale, screen.Min.Y + 48f * scale),
            new Vector2(screen.Max.X - 16f * scale, screen.Max.Y - 30f * scale));
        var layout = CasinoStageLayout.Compute(screen, content, false, false, CasinoStageLayout.DeckHeight, scale);
        Assert.Equal(screen.Min, layout.Full.Min);
        Assert.Equal(screen.Max, layout.Full.Max);
        Assert.Equal(content.Min.Y + CasinoStageLayout.ChromeBand * scale, layout.Band.Max.Y, 3);
        Assert.True(layout.BackCenter.Y - layout.ChipRadiusPixels >= content.Min.Y);
        Assert.Equal(content.Max.Y, layout.Deck.Max.Y, 3);
        Assert.Equal(screen.Min.X, layout.Deck.Min.X, 3);
        Assert.Equal(screen.Max.X, layout.Deck.Max.X, 3);
        Assert.Equal(screen.Max.Y, layout.DeckPanel.Max.Y, 3);
        Assert.Equal(screen.Min.X, layout.DeckPanel.Min.X, 3);
        Assert.Equal(screen.Max.X, layout.DeckPanel.Max.X, 3);
        Assert.Equal(screen.Min.X + CasinoStageLayout.SafeInset * scale, layout.Safe.Min.X, 3);
        Assert.Equal(screen.Max.X - CasinoStageLayout.SafeInset * scale, layout.Safe.Max.X, 3);
        Assert.Equal(layout.Deck.Min.Y - CasinoStageLayout.SafeInset * scale, layout.Safe.Max.Y, 3);
    }

    [Fact]
    public void WithoutADeckSafeStopsAboveTheHomeIndicator()
    {
        var screen = new Rect(Vector2.Zero, new Vector2(393f, 852f));
        var content = new Rect(new Vector2(16f, 48f), new Vector2(377f, 822f));
        var layout = CasinoStageLayout.Compute(screen, content, true, false, 0f, 1f);
        Assert.False(layout.HasDeck);
        Assert.Equal(822f - CasinoStageLayout.SafeInset, layout.Safe.Max.Y, 3);
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
    public void FloorTiersFollowLevelBracketsNotTheWallet()
    {
        Assert.Equal(BalanceTitle.None, StatusTitle.ForLevel(1));
        Assert.Equal(BalanceTitle.None, StatusTitle.ForLevel(19));
        Assert.Equal(BalanceTitle.Shark, StatusTitle.ForLevel(20));
        Assert.Equal(BalanceTitle.HighRoller, StatusTitle.ForLevel(43));
        Assert.Equal(BalanceTitle.Vip, StatusTitle.ForLevel(60));
        Assert.Equal(BalanceTitle.Vip, StatusTitle.ForLevel(79));
        Assert.Equal(BalanceTitle.Whale, StatusTitle.ForLevel(80));
        Assert.Equal(BalanceTitle.Legend, StatusTitle.ForLevel(100));
        Assert.Equal(BalanceTitle.Legend, StatusTitle.ForLevel(200));
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
