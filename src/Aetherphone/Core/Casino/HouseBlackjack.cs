using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Casino;

internal readonly record struct HouseBlackjackFloor(CasinoTableRowDto? Lead, int Seated, bool Open, long LowestBet,
    long HighestBet)
{
    public long MinBet => LowestBet > 0 ? LowestBet : BlackjackRules.MinBet;

    public long TopBetFor(long playerMaxBet)
    {
        var floor = MinBet;
        var tableTop = Math.Max(floor, HighestBet > 0 ? HighestBet : BlackjackRules.MaxBet);
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

    public static bool Reaches(long playerMaxBet, long tableMinBet)
    {
        return playerMaxBet <= 0 || playerMaxBet >= tableMinBet;
    }

    public static HouseBlackjackFloor Of(CasinoTableRowDto[] rows)
    {
        CasinoTableRowDto? first = null;
        CasinoTableRowDto? busiestOpen = null;
        var seated = 0;
        var lowestBet = 0L;
        var highestBet = 0L;
        for (var index = 0; index < rows.Length; index++)
        {
            var row = rows[index];
            if (!Is(row))
            {
                continue;
            }

            first ??= row;
            seated += row.SeatedCount;
            if (row.MinBet > 0 && (lowestBet == 0 || row.MinBet < lowestBet))
            {
                lowestBet = row.MinBet;
            }

            highestBet = Math.Max(highestBet, row.MaxBet);
            if (CasinoTableFilters.HasOpenSeat(row)
                && (busiestOpen is null || row.SeatedCount > busiestOpen.SeatedCount))
            {
                busiestOpen = row;
            }
        }

        return new HouseBlackjackFloor(busiestOpen ?? first, seated, busiestOpen is not null, lowestBet, highestBet);
    }
}
