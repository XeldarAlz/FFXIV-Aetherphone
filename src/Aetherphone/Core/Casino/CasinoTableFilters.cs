using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Casino;

internal enum CasinoTableFilter
{
    All,
    Blackjack,
    Holdem,
    LowStakes,
    HighStakes,
    Practice,
    Gil,
    Mine,
    Rooms,
}

internal static class CasinoTableKinds
{
    public const int Solo = 0;

    public const int House = 1;

    public const int Private = 2;
}

internal static class CasinoStakeTiers
{
    public const int Any = 0;

    public const int Low = 1;

    public const int High = 3;

    public static int From(CasinoTableFilter filter)
    {
        return filter switch
        {
            CasinoTableFilter.LowStakes => Low,
            CasinoTableFilter.HighStakes => High,
            _ => Any,
        };
    }
}

internal static class CasinoTableFilters
{
    public const string HoldemKind = "casino.holdem";

    public const long LowStakeCeiling = 2_500;

    public const long HighStakeFloor = 25_000;

    public static readonly CasinoTableFilter[] All =
    {
        CasinoTableFilter.All,
        CasinoTableFilter.Blackjack,
        CasinoTableFilter.Holdem,
        CasinoTableFilter.Rooms,
        CasinoTableFilter.LowStakes,
        CasinoTableFilter.HighStakes,
        CasinoTableFilter.Practice,
        CasinoTableFilter.Gil,
        CasinoTableFilter.Mine,
    };

    public static bool Shown(CasinoTableRowDto row)
    {
        return string.Equals(row.GameKind, CasinoWire.BlackjackKind, StringComparison.Ordinal)
            || string.Equals(row.GameKind, HoldemKind, StringComparison.Ordinal)
            || VenueKinds.IsVenue(row.GameKind)
            || CasinoBookRules.IsBook(row.GameKind);
    }

    public static bool Matches(CasinoTableFilter filter, CasinoTableRowDto row, string myUserId)
    {
        if (!Shown(row))
        {
            return false;
        }

        return filter switch
        {
            CasinoTableFilter.Blackjack => string.Equals(row.GameKind, CasinoWire.BlackjackKind,
                StringComparison.Ordinal),
            CasinoTableFilter.Holdem => string.Equals(row.GameKind, HoldemKind, StringComparison.Ordinal),
            CasinoTableFilter.Rooms => VenueKinds.IsVenue(row.GameKind),
            CasinoTableFilter.LowStakes => CasinoCurrencies.Of(row) == CasinoCurrencies.Chips && row.MinBet > 0
                && row.MinBet <= LowStakeCeiling,
            CasinoTableFilter.HighStakes => CasinoCurrencies.Of(row) == CasinoCurrencies.Chips
                && row.MinBet >= HighStakeFloor,
            CasinoTableFilter.Practice => CasinoCurrencies.Of(row) == CasinoCurrencies.Practice,
            CasinoTableFilter.Gil => CasinoCurrencies.Of(row) == CasinoCurrencies.Gil,
            CasinoTableFilter.Mine => myUserId.Length > 0
                && string.Equals(row.OwnerUserId, myUserId, StringComparison.Ordinal),
            _ => true,
        };
    }

    public static CasinoTableRowDto[] OfKind(CasinoTableRowDto[] rows, string gameKind)
    {
        var count = 0;
        for (var index = 0; index < rows.Length; index++)
        {
            if (string.Equals(rows[index].GameKind, gameKind, StringComparison.Ordinal))
            {
                count++;
            }
        }

        if (count == rows.Length)
        {
            return rows;
        }

        var kept = new CasinoTableRowDto[count];
        var next = 0;
        for (var index = 0; index < rows.Length; index++)
        {
            if (string.Equals(rows[index].GameKind, gameKind, StringComparison.Ordinal))
            {
                kept[next++] = rows[index];
            }
        }

        return kept;
    }

    public static CasinoTableRowDto[] Without(CasinoTableRowDto[] rows, string tableId)
    {
        if (tableId.Length == 0 || !Contains(rows, tableId))
        {
            return rows;
        }

        var kept = new CasinoTableRowDto[rows.Length - 1];
        var next = 0;
        for (var index = 0; index < rows.Length; index++)
        {
            if (!string.Equals(rows[index].TableId, tableId, StringComparison.Ordinal))
            {
                kept[next++] = rows[index];
            }
        }

        return kept;
    }

    public static CasinoTableRowDto[] Floor(CasinoTableRowDto[] listed, CasinoTableRowDto[] holdem,
        CasinoTableRowDto? houseLead)
    {
        var count = houseLead is null ? 0 : 1;
        for (var index = 0; index < holdem.Length; index++)
        {
            if (!Contains(listed, holdem[index].TableId))
            {
                count++;
            }
        }

        for (var index = 0; index < listed.Length; index++)
        {
            if (!HouseBlackjack.Is(listed[index]))
            {
                count++;
            }
        }

        var rows = new CasinoTableRowDto[count];
        var next = 0;
        if (houseLead is not null)
        {
            rows[next++] = houseLead;
        }

        for (var index = 0; index < holdem.Length; index++)
        {
            if (!Contains(listed, holdem[index].TableId))
            {
                rows[next++] = holdem[index];
            }
        }

        for (var index = 0; index < listed.Length; index++)
        {
            if (!HouseBlackjack.Is(listed[index]))
            {
                rows[next++] = listed[index];
            }
        }

        return rows;
    }

    public static bool HasOpenSeat(CasinoTableRowDto row)
    {
        return row.MaxSeats > 0 && row.SeatedCount < row.MaxSeats;
    }

    private static bool Contains(CasinoTableRowDto[] rows, string tableId)
    {
        for (var index = 0; index < rows.Length; index++)
        {
            if (string.Equals(rows[index].TableId, tableId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsPrivate(CasinoTableRowDto row)
    {
        return row.Kind == CasinoTableKinds.Private;
    }

    public static int SpectatorsOf(CasinoTableRowDto row)
    {
        var watching = row.Occupancy - row.SeatedCount;
        return watching > 0 ? watching : 0;
    }

    public static int PlayersOf(CasinoTableRowDto row)
    {
        return VenueKinds.IsVenue(row.GameKind) ? row.Occupancy : row.SeatedCount;
    }
}
