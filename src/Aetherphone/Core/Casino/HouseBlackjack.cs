using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Casino;

internal readonly record struct HouseBlackjackFloor(CasinoTableRowDto? Lead, int Seated, bool Open)
{
    public long MinBet => Lead is { MinBet: > 0 } ? Lead.MinBet : BlackjackRules.MinBet;

    public long TopBetFor(long playerMaxBet)
    {
        var floor = MinBet;
        var tableTop = Math.Max(floor, Lead is { MaxBet: > 0 } ? Lead.MaxBet : BlackjackRules.MaxBet);
        return playerMaxBet > 0 ? Math.Clamp(playerMaxBet, floor, tableTop) : tableTop;
    }
}

internal static class HouseBlackjack
{
    public static bool Is(CasinoTableRowDto row)
    {
        return row.Kind == CasinoTableKinds.House
            && string.Equals(row.GameKind, CasinoWire.BlackjackKind, StringComparison.Ordinal);
    }

    public static HouseBlackjackFloor Of(CasinoTableRowDto[] rows)
    {
        CasinoTableRowDto? first = null;
        CasinoTableRowDto? busiestOpen = null;
        var seated = 0;
        for (var index = 0; index < rows.Length; index++)
        {
            var row = rows[index];
            if (!Is(row))
            {
                continue;
            }

            first ??= row;
            seated += row.SeatedCount;
            if (CasinoTableFilters.HasOpenSeat(row)
                && (busiestOpen is null || row.SeatedCount > busiestOpen.SeatedCount))
            {
                busiestOpen = row;
            }
        }

        return new HouseBlackjackFloor(busiestOpen ?? first, seated, busiestOpen is not null);
    }
}
