using System.Numerics;
using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Apps.Casino.Venue;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HostDeskTests
{
    private const string Host = "host";
    private const float PhoneWidth = 328f;
    private const float Epsilon = 0.01f;

    public static TheoryData<float> Scales => new() { 0.75f, 1f, 1.25f, 1.5f };

    private static CasinoTableRowDto Practice(params string[] coDealers) => new(TableId: "t1",
        GameKind: CasinoWire.BlackjackKind, OwnerUserId: Host, Practice: true, Currency: CasinoCurrencies.Practice,
        Config: new CasinoTableConfigDto(Practice: true, Currency: CasinoCurrencies.Practice, CoDealers: coDealers));

    [Fact]
    public void ThePlayersListPutsSeatsFirstAndDropsDuplicatesAndTheIdleHost()
    {
        var card = Practice("u2", "u4");
        var door = new CasinoTableDoorDto(RoomId: "t1", Owner: true,
            Seated: new[]
            {
                new CasinoTableSeatedDto("u1", "Aria", 0),
                new CasinoTableSeatedDto("u2", "Darky", 1),
                new CasinoTableSeatedDto("u3", "Tia", 2, true),
            },
            Present: new[]
            {
                new CasinoTablePresentDto("u1", "Aria", true),
                new CasinoTablePresentDto(Host, "Host", true),
                new CasinoTablePresentDto("u4", "Ysolde", false),
                new CasinoTablePresentDto("u5", "Mira", true),
                new CasinoTablePresentDto("u6", "Ciri", false),
            });
        var members = new List<DoorMember>();
        TableDoor.Merge(door, card, members);

        Assert.Equal(6, members.Count);
        Assert.Equal(MemberState.Seat, members[0].State);
        Assert.Equal(0, members[0].SeatIndex);
        Assert.Equal(MemberState.CoDealer, members[1].State);
        Assert.Equal(MemberState.Leaving, members[2].State);
        Assert.Equal(MemberState.CoDealerAway, members[3].State);
        Assert.True(members[3].Watching);
        Assert.Equal(MemberState.InRoom, members[4].State);
        Assert.Equal(MemberState.Away, members[5].State);
        Assert.DoesNotContain(members, member => member.UserId == Host);

        TableDoor.Merge(null, card, members);
        Assert.Empty(members);
    }

    [Fact]
    public void ASeatedHostStaysListedButHasNoMenu()
    {
        var card = Practice();
        var door = new CasinoTableDoorDto(RoomId: "t1", Seated: new[] { new CasinoTableSeatedDto(Host, "Host", 0) });
        var members = new List<DoorMember>();
        TableDoor.Merge(door, card, members);
        var actions = new MemberAction[2];

        Assert.Single(members);
        Assert.False(members[0].Removable);
        Assert.Equal(0, TableDoor.MenuFor(members[0], card, actions));
    }

    [Fact]
    public void ThePlayerMenuOffersCoDealersOnlyWhereTheTableTakesThem()
    {
        var actions = new MemberAction[2];
        var player = new DoorMember("u1", "Aria", MemberState.Seat, 0, false, true);

        Assert.Equal(2, TableDoor.MenuFor(player, Practice(), actions));
        Assert.Equal(MemberAction.ToggleCoDealer, actions[0]);
        Assert.Equal(MemberAction.Remove, actions[1]);

        var chips = new CasinoTableRowDto(TableId: "t2", GameKind: CasinoWire.BlackjackKind, OwnerUserId: Host,
            Config: new CasinoTableConfigDto());
        Assert.Equal(1, TableDoor.MenuFor(player, chips, actions));
        Assert.Equal(MemberAction.Remove, actions[0]);

        var holdem = Practice() with { GameKind = HoldemRules.Kind };
        Assert.Equal(1, TableDoor.MenuFor(player, holdem, actions));

        var leaving = player with { State = MemberState.Leaving };
        Assert.Equal(1, TableDoor.MenuFor(leaving, Practice(), actions));
        Assert.Equal(MemberAction.ToggleCoDealer, actions[0]);
    }

    [Fact]
    public void TheCoDealerSpotsFillAtTheHostingLimit()
    {
        var full = new string[CasinoHostingRules.MaxCoDealers];
        for (var index = 0; index < full.Length; index++)
        {
            full[index] = "u" + index;
        }

        Assert.False(TableDoor.CoDealersFull(null));
        Assert.False(TableDoor.CoDealersFull(new CasinoTableConfigDto(CoDealers: new[] { "u1" })));
        Assert.True(TableDoor.CoDealersFull(new CasinoTableConfigDto(CoDealers: full)));
    }

    [Fact]
    public void TheConsoleShowsTheGilLedgerForGilAndTheSessionLedgerForSeatTables()
    {
        var gil = Practice() with { Currency = CasinoCurrencies.Gil };
        var raffle = Practice() with { GameKind = VenueKinds.Raffle };

        Assert.True(TableDoor.ShowsGilLedger(gil));
        Assert.False(TableDoor.ShowsSessionLedger(gil));
        Assert.True(TableDoor.ShowsSessionLedger(Practice()));
        Assert.False(TableDoor.ShowsSessionLedger(raffle));
        Assert.False(TableDoor.ShowsGilLedger(raffle));
        Assert.Equal(L.Tables.GilLedgerHeading.Key, TableLedger.TitleFor(gil).Key);
        Assert.Equal(L.Tables.LedgerHeading.Key, TableLedger.TitleFor(Practice()).Key);
        Assert.Equal(L.Tables.LedgerHeading.Key, TableLedger.TitleFor(null).Key);
    }

    [Fact]
    public void TheLedgerSummaryCountsSettledGilBothWaysAndEverythingStillOwed()
    {
        var entries = new[]
        {
            new CasinoLedgerEntryDto("e1", Amount: 50_000, PayerUserId: "u1", PayeeUserId: Host, Settled: true),
            new CasinoLedgerEntryDto("e2", Amount: 25_000, PayerUserId: string.Empty, PayeeUserId: Host,
                Settled: true, Guest: true),
            new CasinoLedgerEntryDto("e3", Amount: 120_000, PayerUserId: Host, PayeeUserId: "u1"),
            new CasinoLedgerEntryDto("e4", Amount: 10_000, PayerUserId: Host, PayeeUserId: "u2", Settled: true),
            new CasinoLedgerEntryDto("e5", Amount: 5_000, PayerUserId: "u2", PayeeUserId: Host),
        };

        var sums = LedgerSums.Of(entries, Host);
        Assert.Equal(75_000, sums.TakenIn);
        Assert.Equal(10_000, sums.PaidOut);
        Assert.Equal(125_000, sums.Owed);
        Assert.Equal(new LedgerSums(0, 0, 0), LedgerSums.Of(null, Host));
    }

    [Fact]
    public void ALedgerRowNamesTheOtherSideAndSignsTheGilByDirection()
    {
        var buyIn = new CasinoLedgerEntryDto("e1", Kind: CasinoLedgerKinds.BuyIn, Amount: 50_000,
            PayerUserId: "u1", PayerName: "Darky", PayeeUserId: Host, PayeeName: "Host", Settled: true);
        var prize = new CasinoLedgerEntryDto("e2", Kind: CasinoLedgerKinds.Prize, Amount: 120_000,
            PayerUserId: Host, PayerName: "Host", PayeeUserId: string.Empty, PayeeName: "Tia", Guest: true,
            ProposedBy: CasinoGilLedger.ServerProposer);

        var incoming = LedgerEntryRows.ViewOf(buyIn, Host, true, CasinoCurrencies.Gil);
        Assert.True(incoming.Incoming);
        Assert.False(incoming.Owed);
        Assert.Equal("Darky", incoming.Name);
        Assert.Equal(Loc.T(L.Tables.GilAmount, NumberText.Signed(50_000)), incoming.Amount);
        Assert.Equal(Loc.T(L.HostDesk.PillRecorded), incoming.State);
        Assert.Equal(LedgerEntryOption.Remove, incoming.Option);

        var outgoing = LedgerEntryRows.ViewOf(prize, Host, true, CasinoCurrencies.Gil);
        Assert.False(outgoing.Incoming);
        Assert.True(outgoing.Owed);
        Assert.Equal(Loc.T(L.Book.GuestNamed, "Tia"), outgoing.Name);
        Assert.Equal(Loc.T(L.Tables.GilAmount, NumberText.Group(-120_000)), outgoing.Amount);
        Assert.Equal(Loc.T(L.HostDesk.PillOwed), outgoing.State);
        Assert.Equal(LedgerEntryOption.MarkPaid, outgoing.Option);
        Assert.Equal(LedgerEntryOption.None, LedgerEntryRows.ViewOf(prize, "u1", false, CasinoCurrencies.Gil).Option);
    }

    [Fact]
    public void TheYourTableCrowdReadsTheDoorWhenItIsLoadedAndTheCardOtherwise()
    {
        var blackjack = Practice() with { SeatedCount = 3 };
        var raffle = Practice() with { GameKind = VenueKinds.Raffle, Occupancy = 7, SeatedCount = 0 };
        Assert.Equal(3, HostTableCard.CrowdOf(blackjack, null));
        Assert.Equal(7, HostTableCard.CrowdOf(raffle, null));

        var door = new CasinoTableDoorDto(RoomId: "t1",
            Seated: new[] { new CasinoTableSeatedDto("u1", "Aria", 0) },
            Present: new[]
            {
                new CasinoTablePresentDto("u1", "Aria", true),
                new CasinoTablePresentDto(Host, "Host", true),
                new CasinoTablePresentDto("u2", "Mira", true),
            });
        Assert.Equal(2, HostTableCard.CrowdOf(blackjack, door));
    }

    [Fact]
    public void TheTableLineNamesTheGameTheCurrencyAndWhoCanJoin()
    {
        var raffle = Practice() with
        {
            GameKind = VenueKinds.Raffle, Currency = CasinoCurrencies.Gil, Listing = CasinoListings.Knock,
        };

        Assert.Equal(Loc.T(L.HostDesk.TableLine, Loc.T(L.Venue.GameRaffle), Loc.T(L.Tables.CurrencyGil),
            Loc.T(L.HostSetup.SummaryKnock)), TableRowViews.HostLine(raffle));
        Assert.Equal(L.HostSetup.SummaryAnyone.Key, TableRowViews.ListingWord(CasinoListings.Open).Key);
        Assert.Equal(L.Tables.SummaryInvite.Key, TableRowViews.ListingWord(CasinoListings.Private).Key);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void ThePosterGridLaysThreeColumnsWithoutOverlap(float scale)
    {
        var width = PhoneWidth * scale;
        var tileWidth = HostHomeLayout.GridTileWidth(width, scale);
        var tileHeight = 190f * scale;
        var origin = new Vector2(16f, 40f);
        for (var index = 0; index < 6; index++)
        {
            var cell = HostHomeLayout.GridCell(origin, tileWidth, tileHeight, index, scale);
            Assert.True(cell.Max.X <= origin.X + width + Epsilon);
            for (var other = 0; other < index; other++)
            {
                var earlier = HostHomeLayout.GridCell(origin, tileWidth, tileHeight, other, scale);
                Assert.True(cell.Min.X >= earlier.Max.X - Epsilon || cell.Min.Y >= earlier.Max.Y - Epsilon);
            }
        }

        var last = HostHomeLayout.GridCell(origin, tileWidth, tileHeight, 5, scale);
        Assert.Equal(last.Max.Y - origin.Y, HostHomeLayout.GridHeight(6, tileHeight, scale), 2);
        Assert.Equal(tileHeight, HostHomeLayout.GridHeight(3, tileHeight, scale), 2);
        Assert.Equal(0f, HostHomeLayout.GridHeight(0, tileHeight, scale));
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void TheYourTableCardWrapsTheCrowdLineWhenItCannotSitBesideTheCode(float scale)
    {
        var width = PhoneWidth * scale;
        var pill = new Vector2(50f * scale, 20f * scale);
        var narrow = HostHomeLayout.Table(Vector2.Zero, width, scale, 40f * scale, pill, 60f * scale, 15f * scale);
        var wide = HostHomeLayout.Table(Vector2.Zero, width, scale, 40f * scale, pill, 200f * scale, 15f * scale);

        Assert.True(narrow.CrowdInline);
        Assert.False(wide.CrowdInline);
        Assert.True(wide.Card.Height > narrow.Card.Height);
        for (var pass = 0; pass < 2; pass++)
        {
            var layout = pass == 0 ? narrow : wide;
            var tiles = layout.CodeTileWidth * CasinoRoomCodes.Length
                + HostHomeLayout.CodeGap * scale * (CasinoRoomCodes.Length - 1);
            Assert.True(layout.CodeOrigin.X + tiles <= width - HostHomeLayout.Pad * scale + Epsilon);
            Assert.True(layout.Go.Max.X <= layout.Manage.Min.X);
            Assert.True(layout.Manage.Max.Y <= layout.Card.Max.Y - HostHomeLayout.Pad * scale + Epsilon);
            Assert.True(layout.CodeOrigin.Y + layout.CodeTileHeight <= layout.Go.Min.Y);
            Assert.True(layout.Go.Height >= HostHomeLayout.Touch * scale - Epsilon);
        }
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void TheHostPillMovesTheChipsCapsuleAndCollapsesWhenTheLabelIsTooLong(float scale)
    {
        var back = new Vector2(44f * scale, 26f * scale);
        var info = new Vector2(316f * scale, 26f * scale);
        var chip = CasinoStageLayout.ChipRadius * scale;
        var roomy = CasinoStageLayout.PlaceHost(back, info, chip, 80f * scale, scale);
        var crowded = CasinoStageLayout.PlaceHost(back, info, chip, 140f * scale, scale);

        Assert.False(roomy.Collapsed);
        Assert.True(crowded.Collapsed);
        Assert.Equal(chip * 2f, crowded.Pill.Width, 2);
        for (var pass = 0; pass < 2; pass++)
        {
            var placement = pass == 0 ? roomy : crowded;
            var capsuleLeft = placement.CapsuleCenter.X - placement.CapsuleWidth * 0.5f;
            var capsuleRight = placement.CapsuleCenter.X + placement.CapsuleWidth * 0.5f;
            Assert.True(capsuleLeft >= back.X + chip - Epsilon);
            Assert.True(capsuleRight <= placement.Pill.Min.X - Epsilon);
            Assert.True(placement.Pill.Max.X <= info.X - chip - Epsilon);
            Assert.True(placement.CapsuleWidth >= CasinoStageLayout.CapsuleMinimum * scale - Epsilon
                || placement.Collapsed);
        }
    }

    [Fact]
    public void TheHostPillCountsAsStageChrome()
    {
        var phone = new Rect(new Vector2(0f, 0f), new Vector2(360f, 740f));
        var plain = CasinoStageLayout.Compute(phone, false, false, CasinoStageLayout.DeckHeight, 1f);
        var hosted = plain.WithHost(80f, 1f);

        Assert.False(plain.HasHost);
        Assert.True(hosted.HasHost);
        Assert.True(hosted.ChromeContains(hosted.HostPill.Center));
        Assert.True(hosted.CapsuleMaxWidth < plain.CapsuleMaxWidth);
    }
}
