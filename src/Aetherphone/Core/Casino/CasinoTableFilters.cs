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
}

internal static class CasinoTableKinds
{
    public const int Solo = 0;

    public const int House = 1;

    public const int Private = 2;
}

internal static class CasinoHouseTiers
{
    public const int Pit = 0;

    public const int Parlour = 1;

    public const int Salon = 2;

    public const int Vault = 3;

    public const int Count = 3;

    public static readonly int[] All = { Pit, Parlour, Salon };

    public static readonly int[] PitOrder = { Pit, Parlour, Salon, Vault };
}

internal static class CasinoStakeTiers
{
    public const int Any = 0;

    public const int Low = 1;

    public const int High = 3;

    public static int ForHouseTier(int houseTier)
    {
        return houseTier + 1;
    }

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
        CasinoTableFilter.LowStakes,
        CasinoTableFilter.HighStakes,
        CasinoTableFilter.Practice,
        CasinoTableFilter.Gil,
        CasinoTableFilter.Mine,
    };

    public static bool Shown(CasinoTableRowDto row)
    {
        return string.Equals(row.GameKind, CasinoWire.BlackjackKind, StringComparison.Ordinal)
            || string.Equals(row.GameKind, HoldemKind, StringComparison.Ordinal);
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

    public static bool HasOpenSeat(CasinoTableRowDto row)
    {
        return row.MaxSeats > 0 && row.SeatedCount < row.MaxSeats;
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
}
