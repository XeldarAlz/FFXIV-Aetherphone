using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class TradeContextTests
{
    private const string Host = "u-host";
    private const string Player = "u-mira";
    private const string Table = "table-a1";
    private const string Raffle = "room-r1";

    private static readonly CasinoTableRowDto GilCard = new(TableId: Table, OwnerUserId: Host, OwnerName: "Ciri",
        Currency: CasinoCurrencies.Gil);

    private static readonly CasinoTableRowDto RaffleCard = new(TableId: Raffle, GameKind: VenueKinds.Raffle,
        OwnerUserId: Host, OwnerName: "Ciri", Currency: CasinoCurrencies.Gil);

    private const string Book = "table-book";

    private static readonly CasinoTableRowDto BookCard = new(TableId: Book, GameKind: CasinoWire.BookKind,
        Kind: CasinoTableKinds.Private, OwnerUserId: Host, OwnerName: "Ciri", Currency: CasinoCurrencies.Gil);

    private static readonly CasinoBlackjackRoomStateDto GilBoard = new(Seats: new[]
    {
        new CasinoBlackjackSeatDto(0, Player, "Xeldar"),
    }, Currency: CasinoCurrencies.Gil);

    private static TradeTableView View(string me, string roomId = "", CasinoBlackjackRoomStateDto? board = null,
        VenueRoomView? venue = null, CasinoTableDoorDto? door = null, CasinoTableRowDto[]? listed = null,
        CasinoTableLedgerDto? ledger = null, string ledgerRoomId = "")
    {
        var rows = listed ?? new[] { GilCard, RaffleCard, BookCard };
        return new TradeTableView(me, roomId, board, venue, door, rows,
            tableId => CasinoTablesStore.CardIn(rows, 0, null, 0, tableId),
            tableId => ledger is not null && string.Equals(ledger.TableId, tableId, StringComparison.Ordinal)
                ? ledger
                : null, ledgerRoomId);
    }

    private static VenueRoomView RaffleRoom(int currency) =>
        new(Raffle, VenueRoomKind.Raffle, 1, 1, new CasinoRoomSnapshotDto(RoomId: Raffle), null, null,
            new CasinoRaffleStateDto(Raffle: new CasinoRaffleDto(Entrants: new[]
            {
                new CasinoRaffleEntrantDto(Player, "Xeldar", 2),
            }), Currency: currency));

    [Fact]
    public void AGilBlackjackRoomIsTheTableAndItsSeatsAndLedgerPartiesAreTheCandidates()
    {
        var ledger = new CasinoTableLedgerDto(Table, true, Currency: CasinoCurrencies.Gil, Entries: new[]
        {
            new CasinoLedgerEntryDto("e1", Table, CasinoLedgerKinds.CashOut, 80_000, Host, "Ciri", "u-gone", "Lou"),
        });
        var view = View(Host, Table, GilBoard, ledger: ledger);
        var table = TradeContexts.Locate(view);
        Assert.True(table.Known);
        Assert.Equal(Host, table.HostUserId);
        Assert.False(table.Venue);

        var context = TradeContexts.Build(table, view);
        Assert.True(context.Hosting);
        Assert.Contains(context.Seats, seat => seat.UserId == Player && seat.DisplayName == "Xeldar");
        Assert.Contains(context.Seats, seat => seat.UserId == "u-gone" && seat.DisplayName == "Lou");
        Assert.Single(context.Entries);
    }

    [Fact]
    public void ChipAndPracticeRoomsAreNoTradeTable()
    {
        var chips = GilBoard with { Currency = CasinoCurrencies.Chips };
        Assert.False(TradeContexts.Locate(View(Player, Table, chips)).Located);
        var practice = GilBoard with { Currency = CasinoCurrencies.Practice };
        Assert.False(TradeContexts.Locate(View(Player, Table, practice)).Located);
        Assert.False(TradeContexts.Locate(View(Player, venue: RaffleRoom(CasinoCurrencies.Practice))).Located);
    }

    [Fact]
    public void AGilVenueRoomIsAVenueTableWithItsEntrantsAsCandidates()
    {
        var view = View(Host, venue: RaffleRoom(CasinoCurrencies.Gil));
        var table = TradeContexts.Locate(view);
        Assert.True(table.Known);
        Assert.Equal(Raffle, table.TableId);
        Assert.True(table.Venue);
        var context = TradeContexts.Build(table, view);
        Assert.True(context.Venue);
        Assert.Contains(context.Seats, seat => seat.UserId == Player);
    }

    [Fact]
    public void TheHostsDoorPageIsTheirTable()
    {
        var door = new CasinoTableDoorDto(Table, true, Seated: new[] { new CasinoTableSeatedDto(Player, "Xeldar", 0) });
        var view = View(Host, door: door);
        var table = TradeContexts.Locate(view);
        Assert.Equal(Table, table.TableId);
        Assert.Equal(Host, table.HostUserId);
        Assert.Contains(TradeContexts.Build(table, view).Seats, seat => seat.UserId == Player);
        Assert.False(TradeContexts.Locate(View(Player, door: door with { Owner = false })).Located);
    }

    [Fact]
    public void AnUnknownCardIsLocatedButNotKnownUnlessTheLedgerSaysIOwnIt()
    {
        var empty = Array.Empty<CasinoTableRowDto>();
        var unknown = TradeContexts.Locate(View(Player, Table, GilBoard, listed: empty));
        Assert.True(unknown.Located);
        Assert.False(unknown.Known);

        var mine = new CasinoTableLedgerDto(Table, true, Currency: CasinoCurrencies.Gil);
        var owned = TradeContexts.Locate(View(Host, Table, GilBoard, listed: empty, ledger: mine));
        Assert.True(owned.Known);
        Assert.Equal(Host, owned.HostUserId);
    }

    [Fact]
    public void TheLastTableIsRememberedAfterTheCasinoClosesForTheSameAccountOnly()
    {
        var memory = new TradeTableMemory();
        Assert.Equal(Table, memory.Resolve(View(Player, Table, GilBoard), 1_000).TableId);

        var closed = View(Player, listed: new[] { GilCard });
        var remembered = memory.Resolve(closed, 60_000);
        Assert.True(remembered.Known);
        Assert.Equal(Table, remembered.TableId);
        Assert.Equal(Host, remembered.HostUserId);

        Assert.False(memory.Resolve(View("u-other", listed: new[] { GilCard }), 60_000).Located);
        Assert.False(memory.Resolve(closed, 2_000 + TradeTableMemory.KeepMilliseconds).Located);
    }

    [Fact]
    public void AnotherLiveGilRoomWinsOverTheRememberedTable()
    {
        var memory = new TradeTableMemory();
        memory.Resolve(View(Player, Table, GilBoard), 0);
        var other = memory.Resolve(View(Player, venue: RaffleRoom(CasinoCurrencies.Gil)), 10);
        Assert.Equal(Raffle, other.TableId);
    }

    [Fact]
    public void AHostWithNothingOpenFallsBackToTheirOwnGilTable()
    {
        var memory = new TradeTableMemory();
        var table = memory.Resolve(View(Host, listed: new[] { GilCard }), 0);
        Assert.True(table.Known);
        Assert.Equal(Table, table.TableId);
        Assert.False(memory.Resolve(View(Player, listed: new[] { GilCard }), 0).Located);
    }

    [Fact]
    public void TheLedgerPageOfABookIsTheTradeTableForTheHostAndThePatron()
    {
        var ledger = new CasinoTableLedgerDto(Book, false, Currency: CasinoCurrencies.Gil, Entries: new[]
        {
            new CasinoLedgerEntryDto("b1", Book, CasinoLedgerKinds.BuyIn, 50_000, Player, "Xeldar", Host, "Ciri"),
            new CasinoLedgerEntryDto("g1", Book, CasinoLedgerKinds.BuyIn, 20_000, string.Empty, "Chea Thorne", Host,
                "Ciri", Settled: true, Guest: true, GuestName: "Chea Thorne"),
        });
        var patron = View(Player, ledger: ledger, ledgerRoomId: Book);
        var table = TradeContexts.Locate(patron);
        Assert.True(table.Known);
        Assert.Equal(Book, table.TableId);
        Assert.Equal(Host, table.HostUserId);
        Assert.False(table.Venue);

        var context = TradeContexts.Build(table, patron);
        Assert.False(context.Hosting);
        Assert.False(context.Venue);
        Assert.Contains(context.Seats, seat => seat.UserId == Player);
        Assert.DoesNotContain(context.Seats, seat => seat.UserId.Length == 0);
        Assert.Equal(2, context.Entries.Length);

        var host = TradeContexts.Locate(View(Host, ledger: ledger with { Owner = true }, ledgerRoomId: Book));
        Assert.True(host.Known);
        Assert.Equal(Host, host.HostUserId);

        Assert.False(TradeContexts.Locate(View(Player, ledgerRoomId: Table)).Located);
        Assert.False(TradeContexts.Locate(View(Player, ledgerRoomId: "table-unknown")).Located);
    }

    [Fact]
    public void ABookIsRememberedLikeAGilRoomAndAHostFallsBackToTheirOwnBook()
    {
        var memory = new TradeTableMemory();
        Assert.Equal(Book, memory.Resolve(View(Player, ledgerRoomId: Book), 1_000).TableId);

        var away = memory.Resolve(View(Player), 60_000);
        Assert.True(away.Known);
        Assert.Equal(Book, away.TableId);
        Assert.False(memory.Resolve(View(Player), 2_000 + TradeTableMemory.KeepMilliseconds).Located);

        var hosted = new TradeTableMemory().Resolve(View(Host, listed: new[] { BookCard }), 0);
        Assert.True(hosted.Known);
        Assert.Equal(Book, hosted.TableId);
        Assert.False(hosted.Venue);
    }

    [Fact]
    public void AVenueTableConfirmsATicketButNeverProposesABuyIn()
    {
        var ticket = new CasinoLedgerEntryDto("t1", Raffle, CasinoLedgerKinds.Ticket, 30_000, Player, "Xeldar", Host,
            "Ciri");
        var context = new TradeTableContext(Raffle, Host, Host, "Ciri", new[] { new TradeSeat(Player, "Xeldar") },
            new[] { ticket }, true);
        var paid = TradeLedgerMatcher.Match(new TradeCompletion(new[] { "Xeldar" }, 30_000, 0), context);
        Assert.Equal(TradeProposalKind.Confirm, paid.Kind);
        Assert.Equal("t1", paid.EntryId);

        var unknown = TradeLedgerMatcher.Match(new TradeCompletion(new[] { "Xeldar" }, 45_000, 0), context);
        Assert.Equal(TradeProposalKind.None, unknown.Kind);

        var prize = TradeLedgerMatcher.Match(new TradeCompletion(new[] { "Xeldar" }, 0, 45_000), context);
        Assert.Equal(TradeProposalKind.Propose, prize.Kind);
        Assert.Equal(CasinoLedgerKinds.Payout, prize.LedgerKind);

        var part = TradeLedgerMatcher.Match(new TradeCompletion(new[] { "Xeldar" }, 10_000, 0), context);
        Assert.Equal(TradeProposalKind.Propose, part.Kind);
        Assert.Equal(CasinoLedgerKinds.BuyIn, part.LedgerKind);
        Assert.Equal(10_000, part.Amount);
    }
}
