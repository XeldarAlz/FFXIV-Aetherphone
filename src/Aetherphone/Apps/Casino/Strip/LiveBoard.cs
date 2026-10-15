using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Casino.Strip;

internal enum LiveFilter : byte
{
    All,
    Chips,
    Gil,
    FreePlay,
}

internal enum LiveJoinInput : byte
{
    None,
    Code,
    Token,
}

internal readonly record struct LiveTable(CasinoTableRowDto Row, string GameId, bool Here, bool Open);

internal readonly record struct LiveTableView(
    string Name,
    string Line,
    int Currency,
    bool Knock,
    bool Paused,
    bool Full);

internal static class LiveBoard
{
    public const int Capacity = 96;
    public const int RailThreshold = 3;

    public static readonly LiveFilter[] Filters =
    {
        LiveFilter.All,
        LiveFilter.Chips,
        LiveFilter.Gil,
        LiveFilter.FreePlay,
    };

    public static LocString LabelOf(LiveFilter filter) => filter switch
    {
        LiveFilter.Chips => L.LivePage.FilterChips,
        LiveFilter.Gil => L.LivePage.FilterGil,
        LiveFilter.FreePlay => L.LivePage.FilterFreePlay,
        _ => L.LivePage.FilterAll,
    };

    public static LocString EmptyOf(LiveFilter filter) => filter switch
    {
        LiveFilter.Chips => L.LivePage.EmptyChips,
        LiveFilter.Gil => L.LivePage.EmptyGil,
        LiveFilter.FreePlay => L.LivePage.EmptyFreePlay,
        _ => L.LivePage.EmptyAll,
    };

    public static bool ShowsRail(int tables) => tables > RailThreshold;

    public static int Collect(CasinoTableRowDto[] listed, CasinoTableRowDto[] holdem, CasinoTableRowDto[] nearby,
        CasinoFeatureSet features, string pinnedTableId, string myUserId, LiveTable[] into)
    {
        var count = Add(listed, nearby, features, pinnedTableId, myUserId, into, 0);
        count = Add(holdem, nearby, features, pinnedTableId, myUserId, into, count);
        count = Add(nearby, nearby, features, pinnedTableId, myUserId, into, count);
        Sort(into, count);
        return count;
    }

    public static bool Admits(CasinoTableRowDto row, CasinoFeatureSet features, string pinnedTableId,
        string myUserId)
    {
        if (row.TableId.Length == 0 || !CasinoTableFilters.Shown(row) || HouseBlackjack.Is(row) || IsHouseHoldem(row)
            || !CasinoGameGate.RoomOpen(features, row.GameKind)
            || string.Equals(row.TableId, pinnedTableId, StringComparison.Ordinal))
        {
            return false;
        }

        return myUserId.Length == 0 || !string.Equals(row.OwnerUserId, myUserId, StringComparison.Ordinal);
    }

    public static bool IsHouseHoldem(CasinoTableRowDto row) =>
        string.Equals(row.GameKind, HoldemRules.Kind, StringComparison.Ordinal)
        && (row.Kind == CasinoTableKinds.House || HoldemRules.IsHouseRoom(row.TableId));

    public static bool OpenOf(CasinoTableRowDto row) =>
        !row.Paused && (VenueKinds.IsVenue(row.GameKind) || CasinoBookRules.IsBook(row.GameKind)
                        || CasinoTableFilters.HasOpenSeat(row));

    public static int RankOf(in LiveTable table) => (table.Here ? 0 : 2) + (table.Open ? 0 : 1);

    public static void Sort(LiveTable[] rows, int count)
    {
        for (var index = 1; index < count; index++)
        {
            var held = rows[index];
            var rank = RankOf(held);
            var slot = index - 1;
            while (slot >= 0 && RankOf(rows[slot]) > rank)
            {
                rows[slot + 1] = rows[slot];
                slot--;
            }

            rows[slot + 1] = held;
        }
    }

    public static bool Matches(LiveFilter filter, CasinoTableRowDto row) => filter switch
    {
        LiveFilter.Chips => CasinoCurrencies.Of(row) == CasinoCurrencies.Chips,
        LiveFilter.Gil => CasinoCurrencies.Of(row) == CasinoCurrencies.Gil,
        LiveFilter.FreePlay => CasinoCurrencies.Of(row) == CasinoCurrencies.Practice,
        _ => true,
    };

    public static int Filter(LiveTable[] rows, int count, LiveFilter filter, int[] into)
    {
        var kept = 0;
        for (var index = 0; index < count && kept < into.Length; index++)
        {
            if (Matches(filter, rows[index].Row))
            {
                into[kept++] = index;
            }
        }

        return kept;
    }

    public static string TableGameOf(string gameKind)
    {
        if (string.Equals(gameKind, HoldemRules.Kind, StringComparison.Ordinal))
        {
            return CasinoGames.Holdem;
        }

        if (CasinoBookRules.IsBook(gameKind))
        {
            return CasinoGames.Book;
        }

        return VenueKinds.IsVenue(gameKind)
            ? Venue.VenueCabinet.GameIdOf(VenueKinds.Of(gameKind))
            : CasinoGames.Blackjack;
    }

    public static LiveJoinInput Classify(string buffer)
    {
        if (CasinoRoomCodes.IsCode(buffer))
        {
            return LiveJoinInput.Code;
        }

        return buffer.AsSpan().Trim().Length > CasinoRoomCodes.RawMaxLength && CasinoShare.TryParse(buffer, out _)
            ? LiveJoinInput.Token
            : LiveJoinInput.None;
    }

    public static int WindowSeconds(string gameId, int phase) => gameId switch
    {
        CasinoGames.Bingo => CasinoRoomCadence.BingoWindow(phase),
        CasinoGames.Race => CasinoRoomCadence.RaceWindow(phase),
        _ => CasinoRoomCadence.WheelWindow(phase),
    };

    public static float RingFraction(long remainingMilliseconds, int windowSeconds)
    {
        if (remainingMilliseconds <= 0 || windowSeconds <= 0)
        {
            return 0f;
        }

        return Math.Clamp(remainingMilliseconds / (windowSeconds * 1000f), 0f, 1f);
    }

    public static int HouseHoldemSeated(CasinoTableRowDto[] rows)
    {
        var seated = 0;
        for (var index = 0; index < rows.Length; index++)
        {
            if (IsHouseHoldem(rows[index]))
            {
                seated += rows[index].SeatedCount;
            }
        }

        return seated;
    }

    public static bool IsHostedSeat(CasinoTableRowDto? row, CasinoSittingDto? rack)
    {
        return row is not null && rack is not null && CasinoTableFilters.IsPrivate(row)
               && string.Equals(row.TableId, rack.TableId, StringComparison.Ordinal);
    }

    public static int HereOf(CasinoTableRowDto row, CasinoTableDoorDto? door)
    {
        if (door is null)
        {
            return Math.Max(row.Occupancy, row.SeatedCount);
        }

        var owner = row.OwnerUserId;
        var seated = door.Seated ?? Array.Empty<CasinoTableSeatedDto>();
        var count = 0;
        for (var index = 0; index < seated.Length; index++)
        {
            var userId = seated[index].UserId;
            if (!Same(userId, owner) && !SeatedBefore(seated, index, userId))
            {
                count++;
            }
        }

        var present = door.Present ?? Array.Empty<CasinoTablePresentDto>();
        for (var index = 0; index < present.Length; index++)
        {
            var member = present[index];
            if (member.Attached && !Same(member.UserId, owner)
                && !SeatedBefore(seated, seated.Length, member.UserId) && !PresentBefore(present, index, member.UserId))
            {
                count++;
            }
        }

        return count;
    }

    public static int KnockingOf(CasinoTableDoorDto? door) => door?.Knocks?.Length ?? 0;

    public static bool KnocksOf(CasinoTableRowDto row) =>
        CasinoTableFilters.IsPrivate(row) && row.Listing == CasinoListings.Knock;

    public static LiveTableView ViewOf(CasinoTableRowDto row)
    {
        var gameId = TableGameOf(row.GameKind);
        var room = VenueKinds.IsVenue(row.GameKind);
        var book = CasinoBookRules.IsBook(row.GameKind);
        var currency = CasinoCurrencies.Of(row);
        return new LiveTableView(TableRowViews.NameOf(row, gameId), LineOf(row, gameId, currency, room, book), currency,
            KnocksOf(row), row.Paused, !room && !book && !CasinoTableFilters.HasOpenSeat(row));
    }

    public static string LineOf(CasinoTableRowDto row, string gameId, int currency, bool room, bool book)
    {
        var hostNamed = row.Name.Length > 0 && row.OwnerName.Length > 0;
        if (book)
        {
            return hostNamed ? Loc.T(L.Book.HostedBy, row.OwnerName) : Loc.T(L.Book.Plays);
        }

        var lead = hostNamed ? row.OwnerName : Loc.T(CasinoGameNames.Of(gameId));
        var stakes = currency == CasinoCurrencies.Chips && !room ? StakesOf(row, gameId) : string.Empty;
        var count = room
            ? Loc.T(L.LivePage.InRoom, row.Occupancy.ToString(Loc.Culture))
            : row.MaxSeats > 0
                ? Loc.T(L.Casino.TableSeats, row.SeatedCount.ToString(Loc.Culture),
                    row.MaxSeats.ToString(Loc.Culture))
                : string.Empty;
        return Join(Join(lead, stakes), count);
    }

    public static string Join(string first, string second)
    {
        if (first.Length == 0)
        {
            return second;
        }

        return second.Length == 0 ? first : Loc.T(L.LivePage.LineJoin, first, second);
    }

    public static CasinoTableRowDto? Find(CasinoTableRowDto[] rows, string tableId)
    {
        for (var index = 0; index < rows.Length; index++)
        {
            if (string.Equals(rows[index].TableId, tableId, StringComparison.Ordinal))
            {
                return rows[index];
            }
        }

        return null;
    }

    private static string StakesOf(CasinoTableRowDto row, string gameId)
    {
        if (row.MinBet <= 0 || row.MaxBet <= 0)
        {
            return string.Empty;
        }

        return Loc.T(string.Equals(gameId, CasinoGames.Holdem, StringComparison.Ordinal)
                ? L.LivePage.Blinds
                : L.LivePage.Stakes,
            NumberText.Compact(row.MinBet), NumberText.Compact(row.MaxBet));
    }

    private static int Add(CasinoTableRowDto[] rows, CasinoTableRowDto[] nearby, CasinoFeatureSet features,
        string pinnedTableId, string myUserId, LiveTable[] into, int count)
    {
        for (var index = 0; index < rows.Length && count < into.Length; index++)
        {
            var row = rows[index];
            if (!Admits(row, features, pinnedTableId, myUserId) || Contains(into, count, row.TableId))
            {
                continue;
            }

            into[count++] = new LiveTable(row, TableGameOf(row.GameKind), Find(nearby, row.TableId) is not null, OpenOf(row));
        }

        return count;
    }

    private static bool Contains(LiveTable[] rows, int count, string tableId)
    {
        for (var index = 0; index < count; index++)
        {
            if (string.Equals(rows[index].Row.TableId, tableId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Same(string userId, string other) =>
        userId.Length > 0 && string.Equals(userId, other, StringComparison.Ordinal);

    private static bool SeatedBefore(CasinoTableSeatedDto[] seated, int end, string userId)
    {
        for (var index = 0; index < end; index++)
        {
            if (Same(userId, seated[index].UserId))
            {
                return true;
            }
        }

        return false;
    }

    private static bool PresentBefore(CasinoTablePresentDto[] present, int end, string userId)
    {
        for (var index = 0; index < end; index++)
        {
            if (present[index].Attached && Same(userId, present[index].UserId))
            {
                return true;
            }
        }

        return false;
    }
}
