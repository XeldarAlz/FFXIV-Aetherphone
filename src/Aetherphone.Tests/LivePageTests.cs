using Aetherphone.Apps.Casino;
using Aetherphone.Apps.Casino.Live;
using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Casino.Strip;
using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Xunit;

namespace Aetherphone.Tests;

public sealed class LivePageTests
{
    [Fact]
    public void JoinLightsUpForACodeOrAPastedInviteOnly()
    {
        Assert.Equal(LiveJoinInput.Code, LiveBoard.Classify("YUCPHJ"));
        Assert.Equal(LiveJoinInput.Code, LiveBoard.Classify("yuc-phj"));
        Assert.Equal(LiveJoinInput.None, LiveBoard.Classify("YUCPH"));
        Assert.Equal(LiveJoinInput.None, LiveBoard.Classify("YUCPHJK"));
        Assert.Equal(LiveJoinInput.None, LiveBoard.Classify("YUCPH0"));
        Assert.Equal(LiveJoinInput.None, LiveBoard.Classify(string.Empty));
        Assert.Equal(LiveJoinInput.Token, LiveBoard.Classify(CasinoShare.Compose("table-0123456789abcdef")));
        Assert.Equal(LiveJoinInput.Token, LiveBoard.Classify("  table-0123456789abcdef  "));
        Assert.Equal(LiveJoinInput.None, LiveBoard.Classify("table-0123"));
        Assert.Equal(LiveJoinInput.None, LiveBoard.Classify("[aep.casino.v1:not a table id]"));
    }

    [Fact]
    public void TheCountdownRingDrainsOverThePhaseWindow()
    {
        var window = LiveBoard.WindowSeconds(CasinoGames.Wheel, CasinoRoomPhases.Open);

        Assert.Equal(CasinoRoomCadence.WheelOpenSeconds, window);
        Assert.Equal(1f, LiveBoard.RingFraction(window * 1000L, window));
        Assert.Equal(0.5f, LiveBoard.RingFraction(window * 500L, window), 3);
        Assert.Equal(1f, LiveBoard.RingFraction(window * 4000L, window));
        Assert.Equal(0f, LiveBoard.RingFraction(0, window));
        Assert.Equal(0f, LiveBoard.RingFraction(1_000, 0));
        Assert.Equal(CasinoRoomCadence.BingoLockedSeconds,
            LiveBoard.WindowSeconds(CasinoGames.Bingo, CasinoRoomPhases.Locked));
        Assert.Equal(CasinoRoomCadence.RaceResultSeconds,
            LiveBoard.WindowSeconds(CasinoGames.Race, CasinoRoomPhases.Result));
    }

    [Fact]
    public void YourTableCountsTheDoorWhenItIsCachedAndTheCardOtherwise()
    {
        var row = Hosted() with { SeatedCount = 2, Occupancy = 5 };
        var door = new CasinoTableDoorDto("t-1", true,
            Knocks: new[] { new CasinoTableKnockDto("u-knock"), new CasinoTableKnockDto("u-other") },
            Seated: new[] { new CasinoTableSeatedDto("u-anna") },
            Present: new[] { new CasinoTablePresentDto("u-bran", Attached: true) });

        Assert.Equal(2, LiveBoard.HereOf(row, door));
        Assert.Equal(2, LiveBoard.KnockingOf(door));
        Assert.Equal(5, LiveBoard.HereOf(row, null));
        Assert.Equal(0, LiveBoard.KnockingOf(null));
        Assert.Equal(4, LiveBoard.HereOf(row with { Occupancy = 0, SeatedCount = 4 }, null));
    }

    [Fact]
    public void HereCountsEachAttachedPlayerOnceAndNeverTheHost()
    {
        var row = Hosted();
        var door = new CasinoTableDoorDto("t-1", true,
            Seated: new[]
            {
                new CasinoTableSeatedDto(row.OwnerUserId), new CasinoTableSeatedDto("u-anna"),
                new CasinoTableSeatedDto("u-anna"),
            },
            Present: new[]
            {
                new CasinoTablePresentDto("u-anna", Attached: true), new CasinoTablePresentDto("u-bran", Attached: true),
                new CasinoTablePresentDto("u-bran", Attached: true), new CasinoTablePresentDto("u-away"),
                new CasinoTablePresentDto(row.OwnerUserId, Attached: true),
            });

        Assert.Equal(2, LiveBoard.HereOf(row, door));
        Assert.Equal(0, LiveBoard.HereOf(row, new CasinoTableDoorDto("t-1", true)));
    }

    [Fact]
    public void OnlyASeatAtAHostedTablePinsYourTable()
    {
        var hosted = Hosted();
        var rack = new CasinoSittingDto(TableId: "t-1");

        Assert.True(LiveBoard.IsHostedSeat(hosted, rack));
        Assert.False(LiveBoard.IsHostedSeat(hosted with { Kind = CasinoTableKinds.House }, rack));
        Assert.False(LiveBoard.IsHostedSeat(hosted, rack with { TableId = "t-2" }));
        Assert.False(LiveBoard.IsHostedSeat(null, rack));
        Assert.False(LiveBoard.IsHostedSeat(hosted, null));
    }

    [Fact]
    public void ANamedTableNamesItsHostInTheLine()
    {
        var view = LiveBoard.ViewOf(Hosted() with { Name = "Velvet Room 21", Currency = CasinoCurrencies.Gil });

        Assert.Equal("Velvet Room 21", view.Name);
        Assert.Equal(Loc.T(L.LivePage.LineJoin, "Ysolde Vance", Loc.T(L.Casino.TableSeats, "3", "6")), view.Line);
        Assert.Equal(CasinoCurrencies.Gil, view.Currency);
        Assert.False(view.Full);
    }

    [Fact]
    public void AnUnnamedChipTableLeadsWithTheGameAndItsStakes()
    {
        var view = LiveBoard.ViewOf(Hosted());
        var stakes = Loc.T(L.LivePage.Stakes, NumberText.Compact(25_000), NumberText.Compact(1_000_000));
        var lead = Loc.T(L.LivePage.LineJoin, Loc.T(L.Casino.GameBlackjack), stakes);

        Assert.Equal(Loc.T(L.Casino.TableHostedBy, "Ysolde Vance"), view.Name);
        Assert.Equal(Loc.T(L.LivePage.LineJoin, lead, Loc.T(L.Casino.TableSeats, "3", "6")), view.Line);
        Assert.Equal(CasinoCurrencies.Chips, view.Currency);
    }

    [Fact]
    public void HoldemShowsBlindsAndVenueRoomsCountTheRoom()
    {
        var holdem = LiveBoard.ViewOf(Hosted() with { GameKind = HoldemRules.Kind, MinBet = 50, MaxBet = 100 });
        Assert.Contains(Loc.T(L.LivePage.Blinds, NumberText.Compact(50), NumberText.Compact(100)), holdem.Line,
            StringComparison.Ordinal);

        var raffle = LiveBoard.ViewOf(Hosted() with
        {
            GameKind = VenueKinds.Raffle,
            Name = "Lucky Raffle",
            Currency = CasinoCurrencies.Gil,
            Occupancy = 12,
            SeatedCount = 0,
            MaxSeats = 0,
        });
        Assert.Equal(Loc.T(L.LivePage.LineJoin, "Ysolde Vance", Loc.T(L.LivePage.InRoom, "12")), raffle.Line);
        Assert.False(raffle.Full);
    }

    [Fact]
    public void ABookReadsAsALedgerAndAPracticeTableAsFreePlay()
    {
        var book = LiveBoard.ViewOf(Hosted() with
        {
            GameKind = CasinoWire.BookKind,
            Currency = CasinoCurrencies.Gil,
            MaxSeats = 0,
            SeatedCount = 0,
        });
        Assert.Equal(Loc.T(L.Book.HostedBy, "Ysolde Vance"), book.Name);
        Assert.Equal(Loc.T(L.Book.Plays), book.Line);
        Assert.False(book.Full);

        var practice = LiveBoard.ViewOf(Hosted() with { Currency = CasinoCurrencies.Practice, Practice = true });
        Assert.Equal(CasinoCurrencies.Practice, practice.Currency);
        Assert.Equal(L.LivePage.FilterFreePlay, LiveArt.CurrencyName(practice.Currency));
        Assert.DoesNotContain(Loc.T(L.LivePage.Stakes, NumberText.Compact(25_000), NumberText.Compact(1_000_000)),
            practice.Line, StringComparison.Ordinal);
    }

    [Fact]
    public void KnockListingsFullTablesAndPausesShowOnTheRow()
    {
        Assert.True(LiveBoard.ViewOf(Hosted() with { Listing = CasinoListings.Knock }).Knock);
        Assert.False(LiveBoard.ViewOf(Hosted() with { Listing = CasinoListings.Open }).Knock);
        Assert.True(LiveBoard.ViewOf(Hosted() with { SeatedCount = 6 }).Full);
        Assert.True(LiveBoard.ViewOf(Hosted() with { Paused = true }).Paused);
    }

    [Fact]
    public void EveryFilterHasItsOwnEmptyLine()
    {
        Assert.Equal(L.LivePage.EmptyAll, LiveBoard.EmptyOf(LiveFilter.All));
        Assert.Equal(L.LivePage.EmptyChips, LiveBoard.EmptyOf(LiveFilter.Chips));
        Assert.Equal(L.LivePage.EmptyGil, LiveBoard.EmptyOf(LiveFilter.Gil));
        Assert.Equal(L.LivePage.EmptyFreePlay, LiveBoard.EmptyOf(LiveFilter.FreePlay));
        Assert.Equal(L.LivePage.FilterFreePlay, LiveBoard.LabelOf(LiveFilter.FreePlay));
    }

    [Fact]
    public void TheHoldemSheetSeatsUntilARoomIsFullOrLocked()
    {
        var open = HoldemPit.HouseView(HoldemRules.LowTier, null, true);
        var full = HoldemPit.HouseView(HoldemRules.MidTier,
            new CasinoTableRowDto(HoldemRules.MidRoom, HoldemRules.Kind, CasinoTableKinds.House, MaxSeats: 6,
                SeatedCount: 6), true);
        var royal = HoldemPit.HouseView(HoldemRules.RoyalTier, null, true);

        Assert.True(HoldemRoomRows.Seats(open));
        Assert.False(HoldemRoomRows.Seats(full));
        Assert.False(HoldemRoomRows.Seats(royal));
        Assert.True(HoldemRoomRows.Seats(HoldemPit.HouseView(HoldemRules.RoyalTier, null, false)));
        Assert.Equal(Loc.T(L.LivePage.Watch), HoldemRoomRows.RowOf(full, HoldemRules.MidTier).Action);
        Assert.True(HoldemRoomRows.RowOf(royal, HoldemRules.RoyalTier).Rim);
        Assert.False(HoldemRoomRows.RowOf(open, HoldemRules.LowTier).Rim);
    }

    [Fact]
    public void TheBlackjackSheetSeatsAtAnyOpenTableUntilYouHoldASeat()
    {
        var pit = new CasinoTableRowDto(CasinoRoomIds.BlackjackPit, CasinoWire.BlackjackKind, CasinoTableKinds.House,
            MinBet: 500, MaxBet: 10_000, MaxSeats: 7, SeatedCount: 3);

        var open = BlackjackRoomRows.RowOf(0, pit, string.Empty, 100_000);
        Assert.Equal(CasinoRoomIds.BlackjackPit, open.RoomId);
        Assert.True(open.Seats);
        Assert.False(open.Dim);
        Assert.False(open.Rim);
        Assert.Equal(Loc.T(L.LivePage.Sit), open.Action);
        Assert.Equal(Loc.T(L.Casino.TableStakes, NumberText.Compact(500), NumberText.Compact(10_000)), open.Detail);
        Assert.Equal(Loc.T(L.Casino.TableSeats, "3", "7"), open.Note);

        var full = BlackjackRoomRows.RowOf(0, pit with { SeatedCount = 7 }, string.Empty, 100_000);
        Assert.False(full.Seats);
        Assert.Equal(Loc.T(L.LivePage.Watch), full.Action);

        var mine = BlackjackRoomRows.RowOf(0, pit with { SeatedCount = 7 }, CasinoRoomIds.BlackjackPit, 100_000);
        Assert.True(mine.Seats);
        Assert.Equal(Loc.T(L.Strip.ResumeAction), mine.Action);

        var elsewhere = BlackjackRoomRows.RowOf(1, null, CasinoRoomIds.BlackjackPit, 100_000);
        Assert.Equal(CasinoRoomIds.BlackjackParlour, elsewhere.RoomId);
        Assert.False(elsewhere.Seats);
        Assert.Equal(BlackjackRules.SeatCount, elsewhere.MaxSeats);
        Assert.Equal(string.Empty, elsewhere.Detail);
        Assert.False(BlackjackRoomRows.RowOf(2, null, "table-a1b2", 100_000).Seats);
    }

    [Fact]
    public void ABlackjackTableAboveYourMaxBetIsWatchOnly()
    {
        var vault = new CasinoTableRowDto(CasinoRoomIds.BlackjackVault, CasinoWire.BlackjackKind,
            CasinoTableKinds.House, MinBet: 500_000, MaxBet: 10_000_000, MaxSeats: 6, SeatedCount: 1);

        var above = BlackjackRoomRows.RowOf(3, vault, string.Empty, 100_000);
        Assert.False(above.Seats);
        Assert.True(above.Dim);
        Assert.True(above.Rim);
        Assert.Equal(Loc.T(L.LivePage.Watch), above.Action);
        Assert.Equal(Loc.T(L.Casino.TableStakes, NumberText.Compact(500_000), NumberText.Compact(10_000_000)),
            above.Detail);
        Assert.Equal(Loc.T(L.LivePage.BlackjackAboveMaxBet, NumberText.Compact(100_000)), above.Note);

        Assert.True(BlackjackRoomRows.RowOf(3, vault, string.Empty, 500_000).Seats);
        Assert.True(BlackjackRoomRows.RowOf(3, vault, string.Empty, 0).Seats);
        var held = BlackjackRoomRows.RowOf(3, vault, CasinoRoomIds.BlackjackVault, 100_000);
        Assert.True(held.Seats);
        Assert.False(held.Dim);
        Assert.Equal(Loc.T(L.Strip.ResumeAction), held.Action);
    }

    [Fact]
    public void EveryHouseBlackjackTableIsListedUnderItsOwnName()
    {
        var rows = new BlackjackRoomRows();
        rows.Refresh(Array.Empty<CasinoTableRowDto>(), string.Empty, 0);

        Assert.Equal(CasinoRoomIds.BlackjackHouse.Length, rows.Rows.Length);
        for (var index = 0; index < rows.Rows.Length; index++)
        {
            Assert.Equal(CasinoRoomIds.BlackjackHouse[index], rows.Rows[index].RoomId);
            Assert.False(string.IsNullOrEmpty(rows.Rows[index].Name));
            for (var other = 0; other < index; other++)
            {
                Assert.NotEqual(rows.Rows[other].Name, rows.Rows[index].Name);
            }
        }
    }

    [Fact]
    public void LiveListsRunningAndUpcomingChallengesOnly()
    {
        var feed = new CasinoChallengesDto(new[]
        {
            Challenge(),
            Challenge() with { Id = "c-2", State = CasinoChallengeStates.Scheduled },
            Challenge() with { Id = "c-3", State = CasinoChallengeStates.Settled },
            Challenge() with { Id = "c-4", State = CasinoChallengeStates.Cancelled },
            Challenge() with { Id = "c-5", State = CasinoChallengeStates.Ended },
            Challenge() with { Id = "c-6", GameKind = string.Empty },
        });
        var challenges = new LiveChallenges();

        var views = challenges.Refresh(feed);

        Assert.Equal(2, views.Length);
        Assert.Same(views, challenges.Refresh(feed));
        Assert.Empty(challenges.Refresh(null));
    }

    [Fact]
    public void AChallengeRunsFromItsStartUntilItsEnd()
    {
        var view = LiveChallenges.ViewOf(Challenge());

        Assert.Equal(ChallengePhase.Upcoming, LiveChallenges.PhaseOf(view, 999));
        Assert.Equal(ChallengePhase.Running, LiveChallenges.PhaseOf(view, 1_000));
        Assert.Equal(ChallengePhase.Running, LiveChallenges.PhaseOf(view, 4_599));
        Assert.Equal(ChallengePhase.Over, LiveChallenges.PhaseOf(view, 4_600));
        Assert.Equal(3_600, LiveChallenges.SecondsUntil(view.EndsAtUnix, 1_000));
        Assert.Equal(0, LiveChallenges.SecondsUntil(view.EndsAtUnix, 9_000));
    }

    [Fact]
    public void AMostWinsChallengeCountsTheLeaderAndYourOwnWins()
    {
        var view = LiveChallenges.ViewOf(Challenge());

        Assert.Equal(CasinoGames.Slots, view.GameId);
        Assert.Equal(Loc.T(L.LivePage.ChallengeMostGoal, CasinoMultiples.Label(1_000),
            Loc.T(L.Casino.GameSlots)), view.Goal);
        Assert.Equal(Loc.T(L.LivePage.ChallengeMinStake, NumberText.Compact(1_000)), view.Stake);
        Assert.Equal(Loc.T(L.LivePage.ChallengeLeader, "Ysolde Vance", Loc.Plural(L.LivePage.ChallengeWins, 12)),
            view.Leader);
        Assert.Equal(Loc.T(L.LivePage.ChallengeYours, Loc.Plural(L.LivePage.ChallengeWins, 3)), view.Yours);
        Assert.Equal(Loc.T(L.LivePage.ChallengePrizeBadge, NumberText.Compact(50_000)), view.Prize);
    }

    [Fact]
    public void AFirstToWinChallengeNamesTheWinningMultiple()
    {
        var view = LiveChallenges.ViewOf(Challenge() with
        {
            Kind = CasinoChallengeKinds.First, LeaderValue = 525, MinStake = 0, BadgeId = string.Empty,
        });

        Assert.Equal(Loc.T(L.LivePage.ChallengeFirstGoal, CasinoMultiples.Label(1_000),
            Loc.T(L.Casino.GameSlots)), view.Goal);
        Assert.Equal(Loc.T(L.LivePage.ChallengeLeader, "Ysolde Vance", CasinoMultiples.Label(5_250)), view.Leader);
        Assert.Empty(view.Stake);
        Assert.Empty(view.Yours);
        Assert.Equal(Loc.T(L.LivePage.ChallengePrize, NumberText.Compact(50_000)), view.Prize);
    }

    [Fact]
    public void AChallengeOpenToEveryGameSaysAnyGame()
    {
        var first = LiveChallenges.ViewOf(Challenge() with { GameKind = string.Empty, Kind = CasinoChallengeKinds.First });
        var most = LiveChallenges.ViewOf(Challenge() with { GameKind = string.Empty });

        Assert.Equal(Loc.T(L.LivePage.ChallengeFirstGoalAny, CasinoMultiples.Label(1_000)), first.Goal);
        Assert.Equal(Loc.T(L.LivePage.ChallengeMostGoalAny, CasinoMultiples.Label(1_000)), most.Goal);
    }

    [Fact]
    public void AChallengeWithoutAWinnerOrAPrizeSaysSo()
    {
        var view = LiveChallenges.ViewOf(Challenge() with { Leader = null, LeaderValue = 0, MyValue = 0 });

        Assert.Equal(Loc.T(L.LivePage.ChallengeNoLeader), view.Leader);
        Assert.Empty(view.Yours);
        Assert.Equal(Loc.T(L.LivePage.ChallengeBadge), LiveChallenges.PrizeOf(0, true));
        Assert.Empty(LiveChallenges.PrizeOf(0, false));
    }

    private static CasinoChallengeDto Challenge() =>
        new("c-1", "Slot sprint", CasinoChallengeKinds.Most, CasinoWire.SlotsKind, ThresholdTenths: 100,
            MinStake: 1_000, StartsAtUnix: 1_000, EndsAtUnix: 4_600, State: CasinoChallengeStates.Live,
            Reward: 50_000, BadgeId: "slot-sprint", Leader: new CasinoPlayerRefDto("u-ysolde", "Ysolde Vance"),
            LeaderValue: 12, MyValue: 3);

    private static CasinoTableRowDto Hosted() =>
        new("t-1", CasinoWire.BlackjackKind, CasinoTableKinds.Private, OwnerUserId: "u-ysolde",
            OwnerName: "Ysolde Vance", MinBet: 25_000, MaxBet: 1_000_000, MaxSeats: 6, SeatedCount: 3, Occupancy: 3);
}
