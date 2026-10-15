using System.Numerics;
using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoDoorLayoutTests
{
    private const float PhoneWidth = 330f;

    public static TheoryData<float> Scales => new() { 0.75f, 0.9f, 1f, 1.25f, 1.5f };

    [Theory]
    [MemberData(nameof(Scales))]
    public void TheCodeTilesAndTheCopyPillShareOneRowInsideTheCard(float scale)
    {
        var inner = (PhoneWidth - DoorLayout.Pad * 2f) * scale;
        for (var copy = 60; copy <= 140; copy += 40)
        {
            var copyWidth = copy * scale;
            var row = DoorLayout.CodeRow(0f, 0f, inner, copyWidth, scale);
            var lastTile = row.Tile(0f, 0f, CasinoRoomCodes.Length - 1);

            Assert.True(row.TileWidth <= DoorLayout.CodeTileMax * scale + 0.01f);
            Assert.True(row.TileHeight <= DoorLayout.Touch * scale + 0.01f);
            Assert.True(lastTile.Max.X + DoorLayout.ButtonGap * scale <= row.Copy.Min.X + 0.01f);
            Assert.Equal(inner, row.Copy.Max.X, 2);
            Assert.True(row.Copy.Height >= DoorLayout.Touch * scale - 0.01f);
            Assert.True(DoorLayout.CodeRowHeight(row, scale) >= row.TileHeight - 0.01f);
        }
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void KnockCardsKeepTheirButtonsInsideAndClearOfTheName(float scale)
    {
        var name = 22f * scale;
        var line = 18f * scale;
        var width = PhoneWidth * scale;
        var shortLabel = 70f * scale;
        var longLabel = 125f * scale;
        Assert.True(DoorLayout.KnockInline(width, shortLabel, shortLabel, scale));
        Assert.False(DoorLayout.KnockInline(width, longLabel, longLabel, scale));
        for (var arrangement = 0; arrangement < 2; arrangement++)
        {
            var inline = arrangement == 0;
            var label = inline ? shortLabel : longLabel;
            var height = DoorLayout.KnockHeight(scale, name, line, inline);
            var card = new Rect(Vector2.Zero, new Vector2(width, height));
            var buttons = DoorLayout.KnockButtonRects(card, label, label, inline, scale);
            var avatar = DoorLayout.KnockAvatar(card, name, line, inline, scale);
            var textLeft = DoorLayout.KnockTextLeft(card, scale);

            Assert.True(buttons.Decline.Max.X <= buttons.Approve.Min.X || buttons.Decline.Max.Y <= buttons.Approve.Min.Y);
            Assert.True(buttons.Approve.Max.X <= card.Max.X && buttons.Approve.Max.Y <= card.Max.Y);
            Assert.True(buttons.Decline.Min.X >= card.Min.X && buttons.Decline.Min.Y >= card.Min.Y);
            Assert.True(avatar.X + DoorLayout.Avatar * 0.5f * scale < textLeft);
            if (inline)
            {
                Assert.True(buttons.Decline.Min.X - textLeft >= DoorLayout.KnockTextMin * scale);
            }
            else
            {
                Assert.True(avatar.Y + (name + line) * 0.5f <= buttons.Decline.Min.Y);
                Assert.True(buttons.Decline.Width >= label * 0.5f);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void MemberRowsKeepTheMenuClearOfTheName(float scale)
    {
        var height = DoorLayout.MemberHeight(scale, 22f * scale, 17f * scale);
        var row = new Rect(Vector2.Zero, new Vector2(PhoneWidth * scale, height));
        var menu = DoorLayout.MenuCenter(row, scale);

        Assert.True(height >= DoorLayout.Touch * scale);
        Assert.True(DoorLayout.MemberTextRight(row, true, scale) < menu.X - DoorLayout.MenuButton * 0.5f * scale);
        Assert.Equal(row.Max.X - DoorLayout.Pad * scale, DoorLayout.MemberTextRight(row, false, scale), 3);
        Assert.True(DoorLayout.ManageHeight(scale, 22f * scale, 40f * scale)
            >= 22f * scale + 40f * scale + DoorLayout.ManagePadY * 2f * scale);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void PlayerRowsKeepTheRemoveButtonClearOfTheName(float scale)
    {
        var height = DoorLayout.PlayerHeight(scale, 22f * scale, 20f * scale);
        var row = new Rect(Vector2.Zero, new Vector2(PhoneWidth * scale, height));
        var button = DoorLayout.TrailingButton(row, 90f * scale, scale);
        var avatar = DoorLayout.AvatarCenter(row, scale);

        Assert.True(button.Height >= DoorLayout.Touch * scale - 0.01f);
        Assert.True(button.Min.Y >= row.Min.Y && button.Max.Y <= row.Max.Y);
        Assert.True(avatar.X + DoorLayout.Avatar * 0.5f * scale < DoorLayout.TextLeft(row, scale));
        Assert.True(DoorLayout.TextLeft(row, scale) < button.Min.X - DoorLayout.TextGap * scale);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void LedgerColumnsReadLeftToRightWithoutOverlap(float scale)
    {
        var width = PhoneWidth * scale;
        var number = DoorLayout.LedgerNumberWidth(width, scale);
        var nameRight = DoorLayout.LedgerNameRight(0f, width, scale);
        Assert.True(number > 40f * scale);
        Assert.True(nameRight > DoorLayout.Pad * scale);
        var previousRight = nameRight;
        for (var column = 0; column < 3; column++)
        {
            var right = DoorLayout.LedgerColumnRight(0f, width, column, scale);
            Assert.True(right - number >= previousRight - 0.01f);
            previousRight = right;
        }

        Assert.Equal(width - DoorLayout.Pad * scale, previousRight, 2);
    }

    [Fact]
    public void ChipTablesOfferRenameAndCloseOnly()
    {
        var controls = new DoorControl[5];
        Assert.Equal(2, TableDoor.Controls(false, false, controls));
        Assert.Equal(DoorControl.Rename, controls[0]);
        Assert.Equal(DoorControl.Close, controls[1]);
    }

    [Fact]
    public void OnlyChipTablesExplainWhyPauseDealAndCoDealersAreMissing()
    {
        Assert.True(TableDoor.ExplainsHostLimits(new CasinoTableRowDto(TableId: "t1")));
        Assert.False(TableDoor.ExplainsHostLimits(new CasinoTableRowDto(TableId: "t1", Practice: true)));
        Assert.False(TableDoor.ExplainsHostLimits(
            new CasinoTableRowDto(TableId: "t1", Currency: CasinoCurrencies.Gil)));
    }

    [Fact]
    public void HostDealtTablesOfferEveryControl()
    {
        var controls = new DoorControl[5];
        Assert.Equal(5, TableDoor.Controls(true, true, controls));
        Assert.Equal(DoorControl.Pause, controls[0]);
        Assert.Equal(DoorControl.Deal, controls[1]);
        Assert.Equal(DoorControl.CoDealers, controls[2]);
        Assert.Equal(4, TableDoor.Controls(true, false, controls));
    }

    [Fact]
    public void APokerDoorHidesCoDealersAndKeepsTheRest()
    {
        var holdem = new CasinoTableRowDto(TableId: "t1", GameKind: HoldemRules.Kind, Practice: true);
        var blackjack = new CasinoTableRowDto(TableId: "t2", GameKind: CasinoWire.BlackjackKind, Practice: true);
        Assert.False(TableDoor.TakesCoDealers(holdem));
        Assert.True(TableDoor.TakesCoDealers(blackjack));

        var controls = new DoorControl[5];
        var count = TableDoor.WithoutCoDealers(controls, TableDoor.Controls(true, false, controls));
        Assert.Equal(3, count);
        Assert.Equal(DoorControl.Pause, controls[0]);
        Assert.Equal(DoorControl.Rename, controls[1]);
        Assert.Equal(DoorControl.Close, controls[2]);
    }

    [Fact]
    public void ABookDoorOffersRenameAndCloseWithItsOwnNoteAndNoSeats()
    {
        var book = new CasinoTableRowDto(TableId: "t1", GameKind: CasinoWire.BookKind, Currency: CasinoCurrencies.Gil,
            Config: new CasinoTableConfigDto(GameKind: CasinoWire.BookKind, Currency: CasinoCurrencies.Gil,
                DealerMode: CasinoDealerModes.Host));
        var controls = new DoorControl[5];
        Assert.Equal(2, TableDoor.ControlsFor(book, controls));
        Assert.Equal(DoorControl.Rename, controls[0]);
        Assert.Equal(DoorControl.Close, controls[1]);
        Assert.Equal(L.Book.DoorNote.Key, TableDoor.NoteFor(book)!.Value.Key);
        Assert.False(TableDoor.HasSeats(book));
        Assert.False(TableDoor.TakesCoDealers(book));
        Assert.False(TableDoor.ExplainsHostLimits(book));

        var chips = new CasinoTableRowDto(TableId: "t2", GameKind: CasinoWire.BlackjackKind);
        Assert.Equal(2, TableDoor.ControlsFor(chips, controls));
        Assert.Equal(L.Tables.ChipHostNote.Key, TableDoor.NoteFor(chips)!.Value.Key);

        var holdem = new CasinoTableRowDto(TableId: "t3", GameKind: HoldemRules.Kind, Practice: true);
        Assert.Equal(3, TableDoor.ControlsFor(holdem, controls));
        Assert.Equal(L.Tables.CoDealersHoldemNote.Key, TableDoor.NoteFor(holdem)!.Value.Key);

        var gil = new CasinoTableRowDto(TableId: "t4", GameKind: CasinoWire.BlackjackKind,
            Currency: CasinoCurrencies.Gil,
            Config: new CasinoTableConfigDto(Currency: CasinoCurrencies.Gil, DealerMode: CasinoDealerModes.Host));
        Assert.Equal(5, TableDoor.ControlsFor(gil, controls));
        Assert.Null(TableDoor.NoteFor(gil));
    }

    [Fact]
    public void AVenueDoorListsTheRoomInsteadOfSeatsAndTheHostIsNeverRemovable()
    {
        Assert.False(TableDoor.HasSeats(new CasinoTableRowDto(TableId: "t1", GameKind: VenueKinds.Raffle)));
        Assert.False(TableDoor.HasSeats(new CasinoTableRowDto(TableId: "t1", GameKind: VenueKinds.DiceTable)));
        Assert.True(TableDoor.HasSeats(new CasinoTableRowDto(TableId: "t1", GameKind: CasinoWire.BlackjackKind)));
        Assert.True(TableDoor.HasSeats(null));

        var card = new CasinoTableRowDto(TableId: "t1", OwnerUserId: "host");
        Assert.False(TableDoor.Removable("host", card));
        Assert.True(TableDoor.Removable("guest", card));
    }

    [Fact]
    public void TheDoorPrefersItsOwnCodeOverTheCard()
    {
        var door = new CasinoTableDoorDto(RoomId: "t1", JoinCode: "ABCDEF");
        var card = new CasinoTableRowDto(TableId: "t1", JoinCode: "GHJKMN");
        Assert.Equal("ABCDEF", TableDoor.JoinCode(door, card));
        Assert.Equal("GHJKMN", TableDoor.JoinCode(new CasinoTableDoorDto(RoomId: "t1"), card));
        Assert.Equal(string.Empty, TableDoor.JoinCode(null, null));
    }

    [Fact]
    public void AnEmptyDoorOnATableTheHostHoldsSendsThemToCheckWhetherItClosed()
    {
        var mine = new CasinoTableRowDto(TableId: "t1", OwnerUserId: "me");
        var theirs = new CasinoTableRowDto(TableId: "t1", OwnerUserId: "them");
        var shut = new CasinoTableDoorDto(RoomId: "t1", Owner: false);
        var open = new CasinoTableDoorDto(RoomId: "t1", Owner: true);

        Assert.True(CasinoTablesStore.LostOwnership(shut, mine, "me"));
        Assert.False(CasinoTablesStore.LostOwnership(open, mine, "me"));
        Assert.False(CasinoTablesStore.LostOwnership(shut, theirs, "me"));
        Assert.False(CasinoTablesStore.LostOwnership(shut, null, "me"));
        Assert.False(CasinoTablesStore.LostOwnership(shut, mine, string.Empty));
    }

    [Fact]
    public void TheHostedAnswerShowsTheCodeBeforeTheDoorOrListingLoads()
    {
        var hosted = new CasinoTableRowDto(TableId: "t1", JoinCode: "ABCDEF");
        var nothingListed = Array.Empty<CasinoTableRowDto>();
        var card = CasinoTablesStore.CardIn(nothingListed, 0, hosted, 1, "t1");
        Assert.Same(hosted, card);
        Assert.Equal("ABCDEF", TableDoor.JoinCode(null, card));
        Assert.Null(CasinoTablesStore.CardIn(nothingListed, 0, hosted, 1, "t2"));
        Assert.Null(CasinoTablesStore.CardIn(nothingListed, 0, hosted, 1, string.Empty));
        var listed = new[] { new CasinoTableRowDto(TableId: "t1", SeatedCount: 1, JoinCode: "ABCDEF") };
        Assert.Same(listed[0], CasinoTablesStore.CardIn(listed, 2, hosted, 1, "t1"));
    }

    [Fact]
    public void TheFresherOfTheCardAndTheListingWins()
    {
        var fetched = new CasinoTableRowDto(TableId: "t1", Name: "Renamed", Paused: true);
        var listed = new[] { new CasinoTableRowDto(TableId: "t1", Name: "Old name") };

        Assert.Same(fetched, CasinoTablesStore.CardIn(listed, 1_000, fetched, 5_000, "t1"));
        Assert.Same(listed[0], CasinoTablesStore.CardIn(listed, 9_000, fetched, 5_000, "t1"));
        Assert.Same(fetched, CasinoTablesStore.CardIn(listed, 5_000, fetched, 5_000, "t1"));
        Assert.Same(listed[0], CasinoTablesStore.CardIn(listed, 1_000, null, 0, "t1"));
        Assert.Same(listed[0], CasinoTablesStore.CardIn(listed, 1_000, fetched with { TableId = "t2" }, 5_000, "t1"));
    }

    [Fact]
    public void ABookDoorNamesTheBookAndOpensTheLedger()
    {
        var book = new CasinoTableRowDto(TableId: "b1", GameKind: CasinoWire.BookKind);
        var blackjack = new CasinoTableRowDto(TableId: "t1");
        Assert.Equal(L.Book.DoorTitle.Key, TableDoor.TitleFor(book).Key);
        Assert.Equal(L.Book.OpenLedger.Key, TableDoor.OpenLabelFor(book).Key);
        Assert.Equal(L.Casino.DoorTitle.Key, TableDoor.TitleFor(blackjack).Key);
        Assert.Equal(L.HostDesk.GoToTable.Key, TableDoor.OpenLabelFor(blackjack).Key);
        Assert.Equal(L.Casino.DoorTitle.Key, TableDoor.TitleFor(null).Key);
        Assert.Equal(L.Book.CloseTitle.Key, TableDoor.CloseTitleFor(book).Key);
        Assert.Equal(L.Book.CloseBody.Key, TableDoor.CloseBodyFor(book).Key);
        Assert.Equal(L.HostDesk.CloseBook.Key, TableDoor.CloseLabelFor(book).Key);
        Assert.Equal(L.HostDesk.CloseNamed.Key, TableDoor.CloseTitleFor(blackjack).Key);
        Assert.Equal(L.HostDesk.CloseBody.Key, TableDoor.CloseBodyFor(blackjack).Key);
        Assert.Equal(L.HostDesk.CloseTitle.Key, TableDoor.CloseLabelFor(blackjack).Key);
        var gil = blackjack with { Currency = CasinoCurrencies.Gil };
        Assert.Equal(L.HostDesk.CloseGilBody.Key, TableDoor.CloseBodyFor(gil).Key);
        Assert.Equal(L.HostDesk.CloseGilLine.Key, TableDoor.CloseLineFor(gil).Key);
        Assert.Equal(L.HostDesk.CloseLine.Key, TableDoor.CloseLineFor(blackjack).Key);
    }
}
