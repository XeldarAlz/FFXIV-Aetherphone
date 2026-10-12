using System.Reflection;
using Aetherphone.Apps.Casino;
using Aetherphone.Apps.Casino.Strip;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoFloorLayoutTests
{
    private const float FloorWidth = 296f;
    private const float HeadlineHeight = 22f;
    private const float FootnoteHeight = 18f;
    private const float TitleHeight = 25f;

    [Fact]
    public void PostersShowAboutTwoAndAQuarterTilesAcrossThePhone()
    {
        var gap = StripShelves.TileGap;
        var width = PosterTile.Width(FloorWidth, gap, 1f);
        var visible = (FloorWidth + gap) / (width + gap);
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
        var width = PosterTile.Width(FloorWidth, StripShelves.TileGap, 1f);
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
        var width = PosterTile.Width(FloorWidth * scale, StripShelves.TileGap * scale, scale);
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

    [Fact]
    public void TheHeroOffersEachClaimOnlyWhenItIsThere()
    {
        var bonus = new CasinoBonusDto(Kind: CasinoBonusKinds.Timed, Ready: true, Amount: 5_000, Eligible: true);
        Assert.Equal(StripHeroClaims.None, StripHero.ClaimsOf(Hero(null, null, false)));
        Assert.Equal(StripHeroClaims.Timed, StripHero.ClaimsOf(Hero(bonus, null, false)));
        Assert.Equal(StripHeroClaims.Streak, StripHero.ClaimsOf(Hero(null, bonus, false)));
        Assert.Equal(StripHeroClaims.Spin, StripHero.ClaimsOf(Hero(null, null, true)));
        Assert.Equal(StripHeroClaims.Timed | StripHeroClaims.Streak | StripHeroClaims.Spin,
            StripHero.ClaimsOf(Hero(bonus, bonus, true)));
        Assert.Equal(0f, StripHero.ClaimsHeight(StripHeroClaims.None, 44f));
        Assert.Equal(0f, StripHero.ClaimsHeight(StripHeroClaims.Streak, 44f));
        Assert.Equal(44f, StripHero.ClaimsHeight(StripHeroClaims.Timed, 44f));
        Assert.Equal(44f, StripHero.ClaimsHeight(StripHeroClaims.Spin, 44f));
        Assert.Equal(44f, StripHero.ClaimsHeight(StripHeroClaims.Timed | StripHeroClaims.Streak | StripHeroClaims.Spin,
            44f));
    }

    [Theory]
    [InlineData(0.75f)]
    [InlineData(1f)]
    [InlineData(1.5f)]
    public void GetChipsSitsBesideTheBalanceUntilItNeedsItsOwnRow(float scale)
    {
        var row = (FloorWidth - StripHero.Pad * 2f) * scale;
        var gap = 8f * scale;
        var getChips = 110f * scale;
        Assert.True(StripHero.GetChipsInline(row, 90f * scale, getChips, gap));
        Assert.True(StripHero.GetChipsInline(row, row - gap - getChips, getChips, gap));
        Assert.False(StripHero.GetChipsInline(row, row - gap - getChips + 2f, getChips, gap));
        Assert.Equal(34f * scale, StripHero.BalanceRowHeight(34f * scale, 34f * scale));
        Assert.Equal(34f * scale, StripHero.BalanceRowHeight(34f * scale, 0f));
        Assert.Equal(24f * scale, StripHero.DetailRowHeight(17f * scale, 24f * scale));
        Assert.Equal(17f * scale, StripHero.DetailRowHeight(17f * scale, 0f));
    }

    [Fact]
    public void MissionsAndClubShareARowOfHalfTilesOnAPhone()
    {
        var half = (FloorWidth - StripCarousel.CardGap) * 0.5f;
        Assert.Equal(half - (FloorCapsule.HalfPad * 2f + FloorCapsule.HalfTile + 8f),
            FloorCapsule.HalfTextWidth(half, 1f), 3);
        Assert.True(FloorCapsule.HalfTextWidth(half, 1f) >= 80f);
        Assert.Equal(1f, FloorCapsule.HalfTextWidth(10f, 1f));
    }

    [Theory]
    [InlineData(300f, 120f, 100f, 8f, 146f)]
    [InlineData(300f, 180f, 100f, 8f, 180f)]
    [InlineData(300f, 100f, 170f, 8f, 100f)]
    public void SharedClaimsSplitEvenlyUnlessOneNeedsMore(float row, float timed, float spin, float gap,
        float expected)
    {
        var split = StripHero.SharedClaimSplit(row, timed, spin, gap);
        Assert.Equal(expected, split, 3);
        Assert.True(split >= timed - 0.001f);
        Assert.True(row - gap - split >= spin - 0.001f);
    }

    [Fact]
    public void TheStreakLabelGetsItsOwnLineAboveTheDots()
    {
        var label = 18f;
        var dot = StripHero.StreakDot;
        Assert.Equal(label + StripHero.StreakLabelGap + dot, StripHero.StreakHeight(label, dot, StripHero.StreakLabelGap));
        Assert.True(StripHero.StreakDays * StripHero.StreakDot + (StripHero.StreakDays - 1) * StripHero.StreakGap
                    <= FloorWidth - StripHero.Pad * 2f);
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
        Assert.True(MissionsCard.TextWidth(FloorWidth * scale, 90f * scale, scale) > 100f * scale);
        Assert.Equal(0f, MissionsCard.Height(0, 20f, 100f, scale));
        Assert.True(MissionsCard.Height(2, 20f, one + two, scale) > one + two);
    }

    [Fact]
    public void TheMissionsCapsuleSaysReadyOnlyWhenAClaimWaits()
    {
        var none = MissionsCapsule.Tally(null);
        Assert.Equal(new MissionsTally(0, 0, 0), none);
        Assert.False(MissionsCapsule.ShowsReady(none));
        Assert.Equal(0f, MissionsCapsule.Fraction(none));

        var running = new CasinoMissionsDto(7, 0,
            new[] { Mission("a", false, false), Mission("b", true, true), Mission("c", false, false) });
        var tally = MissionsCapsule.Tally(running);
        Assert.Equal(new MissionsTally(1, 3, 0), tally);
        Assert.False(MissionsCapsule.ShowsReady(tally));
        Assert.Equal(1f / 3f, MissionsCapsule.Fraction(tally), 3);

        var waiting = new CasinoMissionsDto(7, 0,
            new[] { Mission("a", true, false), Mission("b", true, true), Mission("c", false, false) });
        var ready = MissionsCapsule.Tally(waiting);
        Assert.Equal(new MissionsTally(2, 3, 1), ready);
        Assert.True(MissionsCapsule.ShowsReady(ready));
        Assert.Equal(ClubCapsule.Height, MissionsCapsule.Height);
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
        var tile = (FloorWidth * scale - 24f * scale) / 3f;
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
        var phone = StripCarousel.CardHeight(FloorWidth, 1f);
        Assert.InRange(phone, 196f, 260f);
        Assert.True(StripCarousel.BlockHeight(FloorWidth, 2, 1f) > phone);
        Assert.Equal(phone, StripCarousel.BlockHeight(FloorWidth, 1, 1f));
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
        Assert.Equal(0f, StripShelves.RailContentWidth(0, 120f, 12f, 4f));
        Assert.Equal(392f, StripShelves.RailContentWidth(3, 120f, 12f, 4f));
        var block = PosterTile.TextBlock(1f, HeadlineHeight, FootnoteHeight);
        Assert.True(StripShelves.ShelfHeight(FloorWidth, 1f, TitleHeight, block)
                    > PosterTile.Height(PosterTile.Width(FloorWidth, 12f, 1f), block) + TitleHeight);
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
    [InlineData(CasinoGames.Scratch)]
    [InlineData(CasinoGames.Barkeep)]
    public void EveryShelfGameIsOneTapFromTheFloor(string gameId)
    {
        Assert.True(StripCatalog.Reaches(gameId));
    }

    [Fact]
    public void TheClockedRoomsLiveInTheLiveRailInsteadOfAShelf()
    {
        Assert.Equal(new[] { CasinoGames.Race, CasinoGames.Wheel, CasinoGames.Bingo }, CasinoApp.LiveRooms);
        for (var index = 0; index < CasinoApp.LiveRooms.Length; index++)
        {
            Assert.False(StripCatalog.Reaches(CasinoApp.LiveRooms[index]));
        }
    }

    [Fact]
    public void TheOldSingleSlotsTileIsGone()
    {
        Assert.False(StripCatalog.Reaches(CasinoGames.Slots));
    }

    [Fact]
    public void TheDailySpinTileIsGone()
    {
        Assert.False(StripCatalog.Reaches(CasinoGames.DailySpin));
    }

    [Fact]
    public void TheShelvesAndTheLiveRoomsReachEveryPlayableGameExactlyOnce()
    {
        string[] unlisted =
        {
            CasinoGames.DailySpin, CasinoGames.Slots, CasinoGames.SlotsGamble, CasinoGames.DiceTable,
            CasinoGames.Deathroll, CasinoGames.Raffle, CasinoGames.Book,
        };
        var fields = typeof(CasinoGames).GetFields(BindingFlags.Public | BindingFlags.Static);
        var shelved = 0;
        for (var fieldIndex = 0; fieldIndex < fields.Length; fieldIndex++)
        {
            var gameId = (string)fields[fieldIndex].GetValue(null)!;
            var listed = Array.IndexOf(unlisted, gameId) < 0;
            var inLiveRail = Array.IndexOf(CasinoApp.LiveRooms, gameId) >= 0;
            Assert.Equal(listed ? 1 : 0, Occurrences(gameId) + (inLiveRail ? 1 : 0));
            shelved += listed && !inLiveRail ? 1 : 0;
        }

        Assert.Equal(shelved, StripCatalog.Playable.Length);
        Assert.Equal(shelved, StripCatalog.PlayableIds.Length);
        Assert.Equal(shelved, StripCatalog.EntryCount);
    }

    [Fact]
    public void ShelvesFollowTheStandardOrder()
    {
        Assert.Equal(new[] { StripShelf.Cards, StripShelf.Machines, StripShelf.QuickBets }, StripCatalog.Shelves);
        Assert.Equal(3, StripCatalog.EntriesOf(StripShelf.Cards).Length);
        Assert.Equal(3, StripCatalog.EntriesOf(StripShelf.Machines).Length);
        Assert.Equal(8, StripCatalog.EntriesOf(StripShelf.QuickBets).Length);
        Assert.Equal(14, StripCatalog.EntryCount);
        Assert.Equal(CasinoGames.Blackjack, StripCatalog.EntriesOf(StripShelf.Cards)[0].GameId);
        Assert.Equal(CasinoGames.Plinko, StripCatalog.EntriesOf(StripShelf.QuickBets)[0].GameId);
        Assert.Equal(CasinoGames.Scratch, StripCatalog.EntriesOf(StripShelf.QuickBets)[6].GameId);
        Assert.Equal(CasinoGames.Barkeep, StripCatalog.EntriesOf(StripShelf.QuickBets)[7].GameId);
        Assert.Equal(L.Floor.ShelfCards.Key, StripCatalog.TitleOf(StripShelf.Cards).Key);
        Assert.Equal(L.Strip.ShelfMachines.Key, StripCatalog.TitleOf(StripShelf.Machines).Key);
        Assert.Equal(L.Floor.ShelfQuickBets.Key, StripCatalog.TitleOf(StripShelf.QuickBets).Key);
    }

    [Fact]
    public void OnlyBarkeepWearsTheSkillBadge()
    {
        for (var index = 0; index < StripCatalog.PlayableIds.Length; index++)
        {
            var gameId = StripCatalog.PlayableIds[index];
            Assert.Equal(gameId == CasinoGames.Barkeep, StripCatalog.WearsSkillBadge(gameId));
        }
    }

    [Fact]
    public void JumpBackInPicksShelfGamesNewestFirstAndStopsAtFour()
    {
        var rounds = new[]
        {
            Played("casino.plinko"), Played("casino.race"), Played("casino.plinko"), Played("casino.slots.bird"),
            Played("casino.blackjack"), Played("casino.bartender"), Played("casino.keno"),
        };
        Span<int> picked = stackalloc int[CasinoApp.RecentGamesCap];
        var count = CasinoRecentGames.Collect(rounds, StripCatalog.PlayableIds, picked);
        Assert.Equal(4, CasinoApp.RecentGamesCap);
        Assert.Equal(4, count);
        Assert.Equal(CasinoGames.Plinko, StripCatalog.Playable[picked[0]].GameId);
        Assert.Equal(CasinoGames.SlotsBird, StripCatalog.Playable[picked[1]].GameId);
        Assert.Equal(CasinoGames.Blackjack, StripCatalog.Playable[picked[2]].GameId);
        Assert.Equal(CasinoGames.Barkeep, StripCatalog.Playable[picked[3]].GameId);
        Assert.Equal(0, CasinoRecentGames.Collect(Array.Empty<CasinoRoundHistoryDto>(), StripCatalog.PlayableIds, picked));
    }

    [Fact]
    public void MachinePostersRunLiveIdleReels()
    {
        var machines = StripCatalog.EntriesOf(StripShelf.Machines);
        for (var index = 0; index < machines.Length; index++)
        {
            Assert.True(machines[index].LiveIdle);
        }

        Assert.False(StripCatalog.EntriesOf(StripShelf.Cards)[0].LiveIdle);
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
        for (var index = 0; index < StripCatalog.PlayableIds.Length; index++)
        {
            Assert.True(CasinoApp.MinimumStakeOf(StripCatalog.PlayableIds[index]) > 0, StripCatalog.PlayableIds[index]);
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

    private static CasinoRoundHistoryDto Played(string kind) => new(RoundId: kind, GameKind: kind);

    private static StripHeroModel Hero(CasinoBonusDto? timed, CasinoBonusDto? streak, bool spinReady) =>
        new(0, false, 1, 0f, 0, false, timed, streak, spinReady, false, 0);

    private static int Occurrences(string gameId)
    {
        var count = 0;
        for (var index = 0; index < StripCatalog.Playable.Length; index++)
        {
            count += StripCatalog.Playable[index].GameId == gameId ? 1 : 0;
        }

        return count;
    }

    private static CasinoFloorTickDto Win(string roundId, int tenths) =>
        new(Kind: CasinoTickKinds.Win, RoundId: roundId, GameKind: "casino.plinko", MultiplierTenths: tenths,
            Stake: 100, Payout: tenths * 10L, AtUnixMs: 1);

    [Fact]
    public void SeatDotsGiveWayToTheSeatsLineWhenBothDoNotFit()
    {
        var dots = CasinoArt.SeatDotsWidth(6, 1f) + 8f;
        Assert.True(CasinoArt.SeatDotsWidth(6, 1f) > 0f);
        Assert.Equal(0f, CasinoArt.SeatDotsWidth(0, 1f));
        Assert.True(TableRow.ShowsSeatDots(dots, 80f, dots + 80f));
        Assert.False(TableRow.ShowsSeatDots(dots, 80f, dots + 79f));
        Assert.True(TableRow.ShowsSeatDots(dots, 0f, 10f));
        Assert.False(TableRow.ShowsSeatDots(0f, 80f, 500f));
    }

    [Fact]
    public void AMissionReservesAButtonOnlyWhenItCanBeClaimed()
    {
        Assert.Equal(100f, MissionsCard.ActionColumn(true, 100f, 30f));
        Assert.Equal(30f, MissionsCard.ActionColumn(false, 100f, 30f));
        Assert.Equal(100f, MissionsCard.ActionColumn(false, 100f, 140f));
    }

    [Theory]
    [InlineData(0.75f)]
    [InlineData(1f)]
    [InlineData(1.5f)]
    public void TheSignScrimCoversTheWholeSignBand(float scale)
    {
        Assert.True(PosterTile.SignBandBottom(scale)
                    >= PosterTile.SignBandTop(scale) + PosterTile.SignHeight * scale);
    }

    private static CasinoMissionDto Mission(string id, bool complete, bool claimed) =>
        new(id, 0, CasinoMissionMetrics.Rounds, "casino.slots", 25, 0, 5000, complete ? 25 : 3, complete, claimed);
}
