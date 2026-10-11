using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Casino;

internal static class CasinoFloorTicker
{
    public static CasinoFloorTickDto[] Seed(CasinoFloorTickDto[]? ticks, int capacity)
    {
        if (ticks is null || ticks.Length == 0 || capacity <= 0)
        {
            return Array.Empty<CasinoFloorTickDto>();
        }

        var kept = new List<CasinoFloorTickDto>(Math.Min(ticks.Length, capacity));
        for (var index = 0; index < ticks.Length && kept.Count < capacity; index++)
        {
            var tick = ticks[index];
            if (Shown(tick) && IndexOf(kept, tick) < 0)
            {
                kept.Add(tick);
            }
        }

        return kept.ToArray();
    }

    public static CasinoFloorTickDto[] Prepend(CasinoFloorTickDto[] held, CasinoFloorTickDto tick, int capacity)
    {
        if (!Shown(tick) || capacity <= 0)
        {
            return held;
        }

        for (var index = 0; index < held.Length; index++)
        {
            if (Same(held[index], tick))
            {
                return held;
            }
        }

        var length = Math.Min(capacity, held.Length + 1);
        var next = new CasinoFloorTickDto[length];
        next[0] = tick;
        Array.Copy(held, 0, next, 1, length - 1);
        return next;
    }

    public static bool Shown(CasinoFloorTickDto tick)
    {
        return tick.Kind switch
        {
            CasinoTickKinds.Win => tick.MultiplierTenths >= CasinoFloorRules.BigWinTenths,
            CasinoTickKinds.Jackpot => tick.Amount > 0,
            CasinoTickKinds.Rain => tick.Amount > 0 && tick.Recipients > 0,
            CasinoTickKinds.Challenge => tick.ChallengeId.Length > 0,
            _ => false,
        };
    }

    public static bool Same(CasinoFloorTickDto first, CasinoFloorTickDto second)
    {
        return string.Equals(first.Kind, second.Kind, StringComparison.Ordinal)
               && string.Equals(first.RoundId, second.RoundId, StringComparison.Ordinal)
               && string.Equals(first.ChallengeId, second.ChallengeId, StringComparison.Ordinal)
               && first.AtUnixMs == second.AtUnixMs;
    }

    private static int IndexOf(List<CasinoFloorTickDto> kept, CasinoFloorTickDto tick)
    {
        for (var index = 0; index < kept.Count; index++)
        {
            if (Same(kept[index], tick))
            {
                return index;
            }
        }

        return -1;
    }
}
