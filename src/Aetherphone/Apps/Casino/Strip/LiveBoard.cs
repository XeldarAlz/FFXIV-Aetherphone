using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Casino.Strip;

internal enum LiveFilter : byte
{
    All,
    OpenSeats,
    Practice,
    Gil,
    Friends,
    HighRoller,
}

internal enum LiveRowKind : byte
{
    Room,
    Table,
}

internal readonly record struct LiveRow(
    LiveRowKind Kind,
    string GameId,
    string RoomId,
    CasinoRoomListItemDto? Room,
    CasinoTableRowDto? Table);

internal static class LiveBoard
{
    public const int Capacity = 96;

    public static readonly LiveFilter[] Filters =
    {
        LiveFilter.All,
        LiveFilter.OpenSeats,
        LiveFilter.Practice,
        LiveFilter.Gil,
        LiveFilter.Friends,
        LiveFilter.HighRoller,
    };

    public static LocString LabelOf(LiveFilter filter) => filter switch
    {
        LiveFilter.OpenSeats => L.Strip.LiveOpenSeats,
        LiveFilter.Practice => L.Tables.FilterPractice,
        LiveFilter.Gil => L.Tables.FilterGil,
        LiveFilter.Friends => L.Strip.LiveFriends,
        LiveFilter.HighRoller => L.Strip.LiveHighRoller,
        _ => L.Casino.TableFilterAll,
    };

    public static int Collect(CasinoRoomListItemDto[] rooms, CasinoTableRowDto[] house, CasinoTableRowDto[] listed,
        CasinoTableRowDto[] holdem, CasinoStateDto? state, LiveRow[] into)
    {
        var count = 0;
        for (var index = 0; index < rooms.Length && count < into.Length; index++)
        {
            var room = rooms[index];
            var gameId = RoomGameOf(room.GameKind);
            if (gameId.Length == 0 || !CasinoGameGate.IsOpen(state, gameId))
            {
                continue;
            }

            into[count++] = new LiveRow(LiveRowKind.Room, gameId, room.RoomId, room, null);
        }

        count = AddTables(house, state, into, count);
        count = AddTables(holdem, state, into, count);
        return AddTables(listed, state, into, count);
    }

    public static string RoomGameOf(string gameKind) => gameKind switch
    {
        CasinoWire.WheelKind => CasinoGames.Wheel,
        CasinoWire.BingoKind => CasinoGames.Bingo,
        CasinoWire.RaceKind => CasinoGames.Race,
        _ => string.Empty,
    };

    public static string TableGameOf(string gameKind)
    {
        if (string.Equals(gameKind, HoldemRules.Kind, StringComparison.Ordinal))
        {
            return CasinoGames.Holdem;
        }

        return VenueKinds.IsVenue(gameKind)
            ? Venue.VenueCabinet.GameIdOf(VenueKinds.Of(gameKind))
            : CasinoGames.Blackjack;
    }

    public static bool Matches(LiveFilter filter, in LiveRow row, IReadOnlySet<string> friends)
    {
        if (row.Kind == LiveRowKind.Room)
        {
            return filter switch
            {
                LiveFilter.All or LiveFilter.OpenSeats => true,
                LiveFilter.Practice => row.Room?.Practice ?? false,
                _ => false,
            };
        }

        var table = row.Table!;
        return filter switch
        {
            LiveFilter.OpenSeats => VenueKinds.IsVenue(table.GameKind) || CasinoTableFilters.HasOpenSeat(table),
            LiveFilter.Practice => CasinoCurrencies.Of(table) == CasinoCurrencies.Practice,
            LiveFilter.Gil => CasinoCurrencies.Of(table) == CasinoCurrencies.Gil,
            LiveFilter.Friends => table.OwnerName.Length > 0 && friends.Contains(table.OwnerName),
            LiveFilter.HighRoller => IsHighRoller(table),
            _ => true,
        };
    }

    public static bool IsHighRoller(CasinoTableRowDto table)
    {
        if (CasinoCurrencies.Of(table) != CasinoCurrencies.Chips)
        {
            return false;
        }

        return table.MinBet >= CasinoTableFilters.HighStakeFloor
               || (table.Kind == CasinoTableKinds.House && table.StakeTier >= CasinoHouseTiers.Vault);
    }

    public static int Filter(LiveRow[] rows, int count, LiveFilter filter, IReadOnlySet<string> friends,
        LiveRow[] into)
    {
        var kept = 0;
        for (var index = 0; index < count && kept < into.Length; index++)
        {
            if (Matches(filter, rows[index], friends))
            {
                into[kept++] = rows[index];
            }
        }

        return kept;
    }

    private static int AddTables(CasinoTableRowDto[] rows, CasinoStateDto? state, LiveRow[] into, int count)
    {
        for (var index = 0; index < rows.Length && count < into.Length; index++)
        {
            var row = rows[index];
            if (!CasinoTableFilters.Shown(row) || !CasinoGameGate.RoomOpen(state, row.GameKind)
                || Contains(into, count, row.TableId))
            {
                continue;
            }

            into[count++] = new LiveRow(LiveRowKind.Table, TableGameOf(row.GameKind), row.TableId, null, row);
        }

        return count;
    }

    private static bool Contains(LiveRow[] rows, int count, string tableId)
    {
        for (var index = 0; index < count; index++)
        {
            if (rows[index].Kind == LiveRowKind.Table
                && string.Equals(rows[index].RoomId, tableId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
