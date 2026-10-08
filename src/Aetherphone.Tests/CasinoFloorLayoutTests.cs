using Aetherphone.Apps.Casino;
using Aetherphone.Apps.Casino.Strip;
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
        Assert.True(PosterTile.Height(width) > width);
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
        var height = PosterTile.Height(width);
        Assert.True(height - PosterTile.TextBlock(1f, HeadlineHeight, FootnoteHeight) >= 88f);
        Assert.True(width >= 44f);
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
        Assert.True(StripShelves.ShelfHeight(PhoneWidth, 1f, TitleHeight)
                    > PosterTile.Height(PosterTile.Width(PhoneWidth, 12f, 1f)) + TitleHeight);
    }

    [Theory]
    [InlineData(CasinoGames.Blackjack)]
    [InlineData(CasinoGames.Holdem)]
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
        Assert.Equal(21, StripCatalog.EntryCount);
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
            CasinoGames.Scratch, CasinoGames.Barkeep,
        };
        for (var index = 0; index < printed.Length; index++)
        {
            Assert.InRange(CasinoAppReturns(printed[index]), 900, 999);
        }
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
