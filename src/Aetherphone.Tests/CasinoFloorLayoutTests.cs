using Aetherphone.Apps.Casino;
using Aetherphone.Apps.Casino.Strip;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoFloorLayoutTests
{
    private const float PhoneWidth = 330f;
    private const float HeadlineHeight = 22f;
    private const float FootnoteHeight = 18f;
    private const float TitleHeight = 25f;

    [Fact]
    public void PostersShowAboutTwoAndAQuarterTilesAcrossThePhone()
    {
        var gap = StripShelves.TileGap;
        var width = PosterTile.Width(PhoneWidth, gap, 1f);
        var visible = (PhoneWidth + gap) / (width + gap);
        Assert.InRange(visible, 2f, 2.5f);
        Assert.True(PosterTile.Height(width, PosterTile.TextBlock(1f, HeadlineHeight, FootnoteHeight)) > width);
    }

    [Theory]
    [InlineData(200f)]
    [InlineData(330f)]
    [InlineData(900f)]
    public void PosterWidthStaysInsideItsBounds(float rowWidth)
    {
        var width = PosterTile.Width(rowWidth, StripShelves.TileGap, 1f);
        Assert.InRange(width, PosterTile.MinWidth, PosterTile.MaxWidth);
    }

    [Fact]
    public void PostersLeaveRoomForTheTitleBlockAndATouchTarget()
    {
        var width = PosterTile.Width(PhoneWidth, StripShelves.TileGap, 1f);
        var block = PosterTile.TextBlock(1f, HeadlineHeight, FootnoteHeight);
        var height = PosterTile.Height(width, block);
        Assert.True(height - block >= 88f);
        Assert.True(width >= 44f);
    }

    [Theory]
    [InlineData(0.75f)]
    [InlineData(1f)]
    [InlineData(1.25f)]
    [InlineData(1.5f)]
    public void PosterTextBlockHoldsTwoTitleLinesAndTwoDetailLines(float scale)
    {
        var headline = HeadlineHeight * scale;
        var footnote = FootnoteHeight * scale;
        var block = PosterTile.TextBlock(scale, headline, footnote);
        Assert.True(block >= headline * 2f + footnote * 2f + PosterTile.Pad * scale * 2f - 0.01f);
    }

    [Theory]
    [InlineData(0.75f)]
    [InlineData(1f)]
    [InlineData(1.25f)]
    [InlineData(1.5f)]
    public void PosterSignClearsTheCornerCapsulesAndTheArtSitsBetweenSignAndText(float scale)
    {
        var width = PosterTile.Width(PhoneWidth * scale, StripShelves.TileGap * scale, scale);
        var block = PosterTile.TextBlock(scale, HeadlineHeight * scale, FootnoteHeight * scale);
        var tile = new Rect(System.Numerics.Vector2.Zero,
            new System.Numerics.Vector2(width, PosterTile.Height(width, block)));
        var capsuleBottom = (PosterTile.Pad * 0.6f + PosterTile.CrowdHeight) * scale;
        var signTop = PosterTile.SignBandTop(scale);
        var signBottom = signTop + PosterTile.SignHeight * scale;
        Assert.True(signTop >= capsuleBottom);
        var art = PosterTile.ArtCenter(tile, block, scale);
        var glyphHalf = width * PosterTile.GlyphFraction * 0.9f;
        Assert.True(art.Y - glyphHalf >= signBottom - 0.01f);
        Assert.True(art.Y + glyphHalf <= tile.Max.Y - block + 0.01f);
    }

    [Theory]
    [InlineData(0.75f)]
    [InlineData(1f)]
    [InlineData(1.5f)]
    public void HeroButtonsSitSideBySideOnlyWhenBothLabelsFitWhole(float scale)
    {
        var row = (PhoneWidth - StripHero.Pad * 2f) * scale;
        var gap = 8f * scale;
        var button = 44f * scale;
        Assert.False(StripHero.StacksButtons(row, 120f * scale, 110f * scale, gap));
        Assert.True(StripHero.StacksButtons(row, 200f * scale, 150f * scale, gap));
        Assert.Equal(button, StripHero.ButtonsHeight(true, false, button, gap));
        Assert.Equal(button * 2f + gap, StripHero.ButtonsHeight(true, true, button, gap));
        Assert.Equal(button, StripHero.ButtonsHeight(false, true, button, gap));
    }

    [Fact]
    public void TheStreakLabelGetsItsOwnLineAboveTheDots()
    {
        var label = 18f;
        var dot = StripHero.StreakDot;
        Assert.Equal(label + StripHero.StreakLabelGap + dot, StripHero.StreakHeight(label, dot, StripHero.StreakLabelGap));
        Assert.True(StripHero.StreakDays * StripHero.StreakDot + (StripHero.StreakDays - 1) * StripHero.StreakGap
                    <= PhoneWidth - StripHero.Pad * 2f);
    }

    [Theory]
    [InlineData(0.75f)]
    [InlineData(1f)]
    [InlineData(1.5f)]
    public void MissionRowsGrowWithTheirWrappedSentence(float scale)
    {
        var line = 18f * scale;
        var footnote = 16f * scale;
        var one = MissionsCard.RowHeightFor(1, line, footnote, scale);
        var two = MissionsCard.RowHeightFor(2, line, footnote, scale);
        var three = MissionsCard.RowHeightFor(3, line, footnote, scale);
        Assert.True(one >= MissionsCard.RowHeight * scale - 0.01f);
        Assert.True(three > two);
        Assert.True(three >= line * 3f + footnote + MissionsCard.BarHeight * scale);
        Assert.True(MissionsCard.TextWidth(PhoneWidth * scale, 90f * scale, scale) > 100f * scale);
        Assert.Equal(0f, MissionsCard.Height(0, 20f, 100f, scale));
        Assert.True(MissionsCard.Height(2, 20f, one + two, scale) > one + two);
    }

    [Theory]
    [InlineData(0.75f)]
    [InlineData(1f)]
    [InlineData(1.5f)]
    public void RecordTilesGrowToHoldEveryWrappedLabelLine(float scale)
    {
        var line = 19f * scale;
        var one = CasinoApp.RecordsTileHeightFor(1, line, scale);
        var three = CasinoApp.RecordsTileHeightFor(3, line, scale);
        Assert.True(three - one >= line * 2f - 0.01f || one > 84f * scale);
        Assert.True(three >= line * 3f + 34f * scale);
        var tile = (PhoneWidth * scale - 24f * scale) / 3f;
        Assert.True(CasinoApp.RecordsLabelWidth(tile, scale) < tile);
        Assert.True(CasinoApp.RecordsLabelWidth(tile, scale) > 60f * scale);
    }

    [Fact]
    public void TheLimitSaveButtonStacksAboveRemoveWhenBothCannotSitWhole()
    {
        Assert.False(CasinoApp.LimitButtonsStack(290f, 150f, 100f, 12f));
        Assert.True(CasinoApp.LimitButtonsStack(290f, 210f, 110f, 12f));
    }

    [Fact]
    public void HeroCardsScaleWithTheWidthAndClamp()
    {
        Assert.Equal(196f, StripCarousel.CardHeight(200f, 1f));
        Assert.Equal(260f, StripCarousel.CardHeight(1000f, 1f));
        var phone = StripCarousel.CardHeight(PhoneWidth, 1f);
        Assert.InRange(phone, 196f, 260f);
        Assert.True(StripCarousel.BlockHeight(PhoneWidth, 1f) > phone);
    }

    [Fact]
    public void HeroCarouselSnapsOnAFlingAndStaysOnAShortDrag()
    {
        Assert.Equal(1, StripCarousel.SnapTarget(0f, 0.3f, 4));
        Assert.Equal(0, StripCarousel.SnapTarget(0f, 0.1f, 4));
        Assert.Equal(2, StripCarousel.SnapTarget(3f, 2.6f, 4));
        Assert.Equal(3, StripCarousel.SnapTarget(3f, 3.5f, 4));
        Assert.Equal(0, StripCarousel.SnapTarget(0f, 0.9f, 1));
        Assert.Equal(1, StripCarousel.Next(0, 3));
        Assert.Equal(0, StripCarousel.Next(2, 3));
    }

    [Fact]
    public void ShelfRowsAddUpTilesAndGaps()
    {
        Assert.Equal(0f, StripShelves.RowWidth(0, 120f, 12f));
        Assert.Equal(120f, StripShelves.RowWidth(1, 120f, 12f));
        Assert.Equal(384f, StripShelves.RowWidth(3, 120f, 12f));
        var block = PosterTile.TextBlock(1f, HeadlineHeight, FootnoteHeight);
        Assert.True(StripShelves.ShelfHeight(PhoneWidth, 1f, TitleHeight, block)
                    > PosterTile.Height(PosterTile.Width(PhoneWidth, 12f, 1f), block) + TitleHeight);
    }

    [Theory]
    [InlineData(CasinoGames.Blackjack)]
    [InlineData(CasinoGames.Holdem)]
    [InlineData(CasinoGames.DealerHoldem)]
    [InlineData(CasinoGames.SlotsBird)]
    [InlineData(CasinoGames.SlotsCascade)]
    [InlineData(CasinoGames.SlotsMoogle)]
    [InlineData(CasinoGames.Plinko)]
    [InlineData(CasinoGames.Mines)]
    [InlineData(CasinoGames.Dice)]
    [InlineData(CasinoGames.Limbo)]
    [InlineData(CasinoGames.Keno)]
    [InlineData(CasinoGames.HiLo)]
    [InlineData(CasinoGames.Race)]
    [InlineData(CasinoGames.Wheel)]
    [InlineData(CasinoGames.Bingo)]
    [InlineData(CasinoGames.Scratch)]
    [InlineData(CasinoGames.DailySpin)]
    [InlineData(CasinoGames.Barkeep)]
    public void EveryGameIsOneTapFromTheFloor(string gameId)
    {
        Assert.True(StripCatalog.Reaches(gameId));
    }

    [Fact]
    public void TheOldSingleSlotsTileIsGone()
    {
        Assert.False(StripCatalog.Reaches(CasinoGames.Slots));
    }

    [Fact]
    public void ShelvesFollowTheStandardOrder()
    {
        Assert.Equal(new[]
        {
            StripShelf.Tables, StripShelf.Machines, StripShelf.Originals, StripShelf.LiveFloor, StripShelf.Instant,
            StripShelf.Skill, StripShelf.Venue,
        }, StripCatalog.Shelves);
        Assert.Equal(3, StripCatalog.EntriesOf(StripShelf.Machines).Length);
        Assert.Equal(6, StripCatalog.EntriesOf(StripShelf.Originals).Length);
        Assert.Equal(4, StripCatalog.EntriesOf(StripShelf.Tables).Length);
        Assert.Equal(22, StripCatalog.EntryCount);
    }

    [Fact]
    public void MachinePostersRunLiveIdleReels()
    {
        var machines = StripCatalog.EntriesOf(StripShelf.Machines);
        for (var index = 0; index < machines.Length; index++)
        {
            Assert.True(machines[index].LiveIdle);
        }

        Assert.False(StripCatalog.EntriesOf(StripShelf.Tables)[0].LiveIdle);
    }

    [Fact]
    public void EveryHouseGamePrintsItsReturn()
    {
        string[] printed =
        {
            CasinoGames.Blackjack, CasinoGames.SlotsBird, CasinoGames.SlotsCascade, CasinoGames.SlotsMoogle,
            CasinoGames.Plinko, CasinoGames.Mines, CasinoGames.Race, CasinoGames.Wheel, CasinoGames.Bingo,
            CasinoGames.Scratch, CasinoGames.Barkeep, CasinoGames.DealerHoldem,
        };
        for (var index = 0; index < printed.Length; index++)
        {
            Assert.InRange(CasinoAppReturns(printed[index]), 900, 999);
        }
    }

    [Fact]
    public void EveryPaidGamePosterPrintsAMinimumBet()
    {
        for (var shelfIndex = 0; shelfIndex < StripCatalog.Shelves.Length; shelfIndex++)
        {
            var entries = StripCatalog.EntriesOf(StripCatalog.Shelves[shelfIndex]);
            for (var index = 0; index < entries.Length; index++)
            {
                var entry = entries[index];
                if (entry.Action != StripAction.Game || entry.GameId == CasinoGames.DailySpin)
                {
                    continue;
                }

                Assert.True(CasinoApp.MinimumStakeOf(entry.GameId) > 0, entry.GameId);
            }
        }
    }

    [Fact]
    public void TheDealerHoldemPosterPrintsTheMinimumAnteAndThePublishedReturn()
    {
        Assert.Equal(DealerHoldemRules.MinAnte, CasinoApp.MinimumStakeOf(CasinoGames.DealerHoldem));
        Assert.Equal(DealerHoldemRules.ReturnTenths, CasinoAppReturns(CasinoGames.DealerHoldem));
    }

    [Fact]
    public void TheHeroCarouselHoldsEveryPageAtOnce()
    {
        var pages = Enum.GetValues<StripPage>();
        Assert.Equal(StripCarousel.MaxPages, pages.Length);
        var carousel = new StripCarousel();
        for (var index = 0; index < pages.Length; index++)
        {
            carousel.Add(pages[index]);
        }

        Assert.Equal(StripCarousel.MaxPages, carousel.Count);
        Assert.Equal(StripPage.Dealer, carousel.PageAt((int)StripPage.Dealer));
    }

    [Fact]
    public void AMachineTilePrintsItsReturnRoundedLikeTheRulesSheet()
    {
        Assert.Equal(964, CasinoAppReturns(CasinoGames.SlotsBird));
        Assert.Equal(962, CasinoAppReturns(CasinoGames.SlotsCascade));
        Assert.Equal(968, CasinoAppReturns(CasinoGames.SlotsMoogle));
    }

    [Fact]
    public void TheIntroWalksThreeCardsThenFinishes()
    {
        Assert.Equal(3, StripIntro.PageCount);
        Assert.False(StripIntro.IsLast(0));
        Assert.Equal(1, StripIntro.NextPage(0));
        Assert.True(StripIntro.IsLast(2));
        Assert.Equal(3, StripIntro.NextPage(3));
        Assert.Equal("app.casino.1300", Core.Changelog.NewFeaturePins.Casino);
    }

    [Fact]
    public void TheTickerKeepsTheNewestTwentyAndDropsRepeats()
    {
        var held = Array.Empty<CasinoFloorTickDto>();
        for (var index = 0; index < 25; index++)
        {
            held = CasinoFloorTicker.Prepend(held, Win("r" + index, 1200), CasinoFloorRules.TickerLength);
        }

        Assert.Equal(CasinoFloorRules.TickerLength, held.Length);
        Assert.Equal("r24", held[0].RoundId);
        var again = CasinoFloorTicker.Prepend(held, Win("r24", 1200), CasinoFloorRules.TickerLength);
        Assert.Same(held, again);
    }

    [Fact]
    public void TheTickerOnlyCarriesHundredXWins()
    {
        var held = CasinoFloorTicker.Prepend(Array.Empty<CasinoFloorTickDto>(), Win("small", 999),
            CasinoFloorRules.TickerLength);
        Assert.Empty(held);
        var seeded = CasinoFloorTicker.Seed(new[]
        {
            Win("a", 1000), Win("a", 1000), Win("b", 10),
            new CasinoFloorTickDto(Kind: CasinoTickKinds.Rain, Amount: 1500, Recipients: 12, AtUnixMs: 5),
        }, CasinoFloorRules.TickerLength);
        Assert.Equal(2, seeded.Length);
    }

    [Fact]
    public void AMissionThatJustFinishedIsReportedOnce()
    {
        var before = new CasinoMissionsDto(7, 0, new[] { Mission("spin-25", false, false) });
        var after = new CasinoMissionsDto(7, 0, new[] { Mission("spin-25", true, false) });
        Assert.Equal("spin-25", CasinoFloorStore.NewlyComplete(before, after));
        Assert.Equal(string.Empty, CasinoFloorStore.NewlyComplete(after, after));
        Assert.Equal(string.Empty, CasinoFloorStore.NewlyComplete(null, after));
        var nextDay = after with { DayIndex = 8 };
        Assert.Equal(string.Empty, CasinoFloorStore.NewlyComplete(before, nextDay));
    }

    [Fact]
    public void AClaimMarksTheMissionPaid()
    {
        var held = new CasinoMissionsDto(7, 0, new[] { Mission("hit-10x", true, false), Mission("games-3", false, false) });
        var claimed = CasinoFloorStore.Claimed(held, "hit-10x");
        Assert.True(claimed.Missions![0].Claimed);
        Assert.False(claimed.Missions[1].Claimed);
    }

    private static int CasinoAppReturns(string gameId) => CasinoApp.ReturnTenthsOf(gameId);

    private static CasinoFloorTickDto Win(string roundId, int tenths) =>
        new(Kind: CasinoTickKinds.Win, RoundId: roundId, GameKind: "casino.plinko", MultiplierTenths: tenths,
            Stake: 100, Payout: tenths * 10L, AtUnixMs: 1);

    private static CasinoMissionDto Mission(string id, bool complete, bool claimed) =>
        new(id, 0, CasinoMissionMetrics.Rounds, "casino.slots", 25, 0, 5000, complete ? 25 : 3, complete, claimed);
}
