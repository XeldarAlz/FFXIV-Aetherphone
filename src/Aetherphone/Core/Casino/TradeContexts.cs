using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Casino;

internal readonly record struct TradeTableRef(string TableId, string HostUserId, string HostName, bool Venue)
{
    public bool Known => !string.IsNullOrEmpty(TableId) && !string.IsNullOrEmpty(HostUserId);

    public bool Located => !string.IsNullOrEmpty(TableId);
}

internal readonly record struct TradeTableView(
    string MyUserId,
    string RoomId,
    CasinoBlackjackRoomStateDto? Board,
    VenueRoomView? Venue,
    CasinoTableDoorDto? Door,
    CasinoTableRowDto[] Listed,
    Func<string, CasinoTableRowDto?> CardFor,
    Func<string, CasinoTableLedgerDto?> LedgerFor,
    string LedgerRoomId = "");

internal static class TradeContexts
{
    public static TradeTableRef Locate(in TradeTableView view)
    {
        if (string.IsNullOrEmpty(view.MyUserId))
        {
            return default;
        }

        if (view.Board is not null && !string.IsNullOrEmpty(view.RoomId)
            && CasinoCurrencies.Of(view.Board) == CasinoCurrencies.Gil)
        {
            return Table(view, view.RoomId, false);
        }

        var venue = view.Venue;
        if (venue is not null && venue.Gil && venue.RoomId.Length > 0)
        {
            return Table(view, venue.RoomId, true);
        }

        var door = Door(view);
        return door.Located ? door : Book(view);
    }

    private static TradeTableRef Door(in TradeTableView view)
    {
        var door = view.Door;
        if (door is null || !door.Owner || string.IsNullOrEmpty(door.RoomId))
        {
            return default;
        }

        var card = view.CardFor(door.RoomId);
        if (card is null || CasinoCurrencies.Of(card) != CasinoCurrencies.Gil)
        {
            return default;
        }

        return new TradeTableRef(door.RoomId, view.MyUserId, card.OwnerName ?? string.Empty,
            VenueKinds.IsVenue(card.GameKind ?? string.Empty));
    }

    private static TradeTableRef Book(in TradeTableView view)
    {
        var tableId = view.LedgerRoomId;
        if (string.IsNullOrEmpty(tableId))
        {
            return default;
        }

        var card = view.CardFor(tableId);
        if (card is null || !CasinoBookRules.IsBook(card.GameKind) || CasinoCurrencies.Of(card) != CasinoCurrencies.Gil)
        {
            return default;
        }

        return Table(view, tableId, false);
    }

    public static TradeTableRef Hosted(in TradeTableView view)
    {
        if (string.IsNullOrEmpty(view.MyUserId))
        {
            return default;
        }

        var rows = view.Listed;
        for (var index = 0; index < rows.Length; index++)
        {
            var row = rows[index];
            if (string.Equals(row.OwnerUserId, view.MyUserId, StringComparison.Ordinal)
                && CasinoCurrencies.Of(row) == CasinoCurrencies.Gil && !string.IsNullOrEmpty(row.TableId))
            {
                return new TradeTableRef(row.TableId, view.MyUserId, row.OwnerName ?? string.Empty,
                    VenueKinds.IsVenue(row.GameKind ?? string.Empty));
            }
        }

        return default;
    }

    public static TradeTableContext Build(in TradeTableRef table, in TradeTableView view)
    {
        if (!table.Known)
        {
            return default;
        }

        var seats = new List<TradeSeat>();
        if (view.Board is not null && string.Equals(view.RoomId, table.TableId, StringComparison.Ordinal))
        {
            AddBoard(seats, view.Board.Seats);
        }

        if (view.Venue is not null && string.Equals(view.Venue.RoomId, table.TableId, StringComparison.Ordinal))
        {
            AddVenue(seats, view.Venue);
        }

        if (view.Door is not null && string.Equals(view.Door.RoomId, table.TableId, StringComparison.Ordinal))
        {
            AddSeated(seats, view.Door.Seated);
            if (Seatless(table, view))
            {
                AddPresent(seats, view.Door.Present);
            }
        }

        var ledger = view.LedgerFor(table.TableId);
        if (ledger is not null)
        {
            AddLedger(seats, ledger);
        }

        return new TradeTableContext(table.TableId, view.MyUserId, table.HostUserId, table.HostName,
            seats.ToArray(), ledger?.Entries ?? Array.Empty<CasinoLedgerEntryDto>(), table.Venue);
    }

    private static TradeTableRef Table(in TradeTableView view, string tableId, bool venue)
    {
        var card = view.CardFor(tableId);
        if (card is not null && !string.IsNullOrEmpty(card.OwnerUserId))
        {
            return new TradeTableRef(tableId, card.OwnerUserId, card.OwnerName ?? string.Empty, venue);
        }

        var ledger = view.LedgerFor(tableId);
        return ledger is not null && ledger.Owner
            ? new TradeTableRef(tableId, view.MyUserId, string.Empty, venue)
            : new TradeTableRef(tableId, string.Empty, string.Empty, venue);
    }

    private static void AddBoard(List<TradeSeat> seats, CasinoBlackjackSeatDto[]? board)
    {
        if (board is null)
        {
            return;
        }

        for (var index = 0; index < board.Length; index++)
        {
            Add(seats, board[index].UserId, board[index].DisplayName);
        }
    }

    private static void AddSeated(List<TradeSeat> seats, CasinoTableSeatedDto[]? seated)
    {
        if (seated is null)
        {
            return;
        }

        for (var index = 0; index < seated.Length; index++)
        {
            Add(seats, seated[index].UserId, seated[index].DisplayName);
        }
    }

    private static bool Seatless(in TradeTableRef table, in TradeTableView view)
    {
        return table.Venue || CasinoBookRules.IsBook(view.CardFor(table.TableId)?.GameKind);
    }

    private static void AddPresent(List<TradeSeat> seats, CasinoTablePresentDto[]? present)
    {
        if (present is null)
        {
            return;
        }

        for (var index = 0; index < present.Length; index++)
        {
            Add(seats, present[index].UserId, present[index].DisplayName);
        }
    }

    private static void AddLedger(List<TradeSeat> seats, CasinoTableLedgerDto ledger)
    {
        var rows = ledger.Rows ?? Array.Empty<CasinoTableLedgerRowDto>();
        for (var index = 0; index < rows.Length; index++)
        {
            Add(seats, rows[index].UserId, rows[index].DisplayName);
        }

        var entries = ledger.Entries ?? Array.Empty<CasinoLedgerEntryDto>();
        for (var index = 0; index < entries.Length; index++)
        {
            Add(seats, entries[index].PayerUserId, entries[index].PayerName);
            Add(seats, entries[index].PayeeUserId, entries[index].PayeeName);
        }
    }

    private static void AddVenue(List<TradeSeat> seats, VenueRoomView venue)
    {
        var rolls = venue.Dice?.Rolls ?? Array.Empty<CasinoVenueRollDto>();
        for (var index = 0; index < rolls.Length; index++)
        {
            Add(seats, rolls[index].UserId, rolls[index].DisplayName);
        }

        AddRound(seats, venue.Dice?.Round);
        AddRound(seats, venue.Dice?.LastRound);
        var deathroll = venue.Deathroll;
        if (deathroll is not null)
        {
            AddDuel(seats, deathroll.Duel);
            var recent = deathroll.Recent ?? Array.Empty<CasinoDeathrollDuelDto>();
            for (var index = 0; index < recent.Length; index++)
            {
                AddDuel(seats, recent[index]);
            }

            var stacks = deathroll.Stacks ?? Array.Empty<CasinoVenueStackDto>();
            for (var index = 0; index < stacks.Length; index++)
            {
                Add(seats, stacks[index].UserId, stacks[index].DisplayName);
            }
        }

        AddRaffle(seats, venue.Raffle?.Raffle);
        AddRaffle(seats, venue.Raffle?.Last);
    }

    private static void AddRound(List<TradeSeat> seats, CasinoDiceRoundDto? round)
    {
        if (round is not null)
        {
            Add(seats, round.WinnerUserId, round.WinnerName);
        }
    }

    private static void AddDuel(List<TradeSeat> seats, CasinoDeathrollDuelDto? duel)
    {
        if (duel is null)
        {
            return;
        }

        Add(seats, duel.ChallengerUserId, duel.ChallengerName);
        Add(seats, duel.OpponentUserId, duel.OpponentName);
    }

    private static void AddRaffle(List<TradeSeat> seats, CasinoRaffleDto? raffle)
    {
        if (raffle is null)
        {
            return;
        }

        AddEntrants(seats, raffle.Entrants);
        AddEntrants(seats, raffle.WinnersDrawn);
    }

    private static void AddEntrants(List<TradeSeat> seats, CasinoRaffleEntrantDto[]? entrants)
    {
        if (entrants is null)
        {
            return;
        }

        for (var index = 0; index < entrants.Length; index++)
        {
            Add(seats, entrants[index].UserId, entrants[index].DisplayName);
        }
    }

    private static void Add(List<TradeSeat> seats, string? userId, string? displayName)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        for (var index = 0; index < seats.Count; index++)
        {
            if (!string.Equals(seats[index].UserId, userId, StringComparison.Ordinal))
            {
                continue;
            }

            if (seats[index].DisplayName.Length == 0 && !string.IsNullOrEmpty(displayName))
            {
                seats[index] = new TradeSeat(userId, displayName);
            }

            return;
        }

        seats.Add(new TradeSeat(userId, displayName ?? string.Empty));
    }
}

internal sealed class TradeTableMemory
{
    public const long KeepMilliseconds = 4L * 60 * 60 * 1000;

    private TradeTableRef held;
    private string heldFor = string.Empty;
    private long heldAtTick;

    public TradeTableRef Resolve(in TradeTableView view, long nowTick)
    {
        var live = TradeContexts.Locate(view);
        if (live.Known)
        {
            held = live;
            heldFor = view.MyUserId;
            heldAtTick = nowTick;
            return live;
        }

        var fresh = Fresh(view.MyUserId, nowTick);
        if (live.Located)
        {
            return fresh && string.Equals(live.TableId, held.TableId, StringComparison.Ordinal) ? held : live;
        }

        return fresh ? held : TradeContexts.Hosted(view);
    }

    private bool Fresh(string myUserId, long nowTick)
    {
        return held.Known && !string.IsNullOrEmpty(myUserId)
            && string.Equals(heldFor, myUserId, StringComparison.Ordinal)
            && nowTick - heldAtTick <= KeepMilliseconds;
    }
}
