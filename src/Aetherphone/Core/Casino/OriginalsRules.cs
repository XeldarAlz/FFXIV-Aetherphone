namespace Aetherphone.Core.Casino;

internal enum HiLoCall : byte
{
    Higher,
    Lower,
    Above,
    Below,
    Same,
    Skip,
}

internal static class OriginalsRules
{
    public const long MinBet = 100;
    public const int ReturnTenths = 990;
    public const long MultiplierUnit = 10_000;

    public const int MinesTiles = 25;
    public const int MinesSide = 5;
    public const int MinMines = 1;
    public const int MaxMines = 24;
    public const int DefaultMines = 3;
    public const long MinesTopMultiple = 5_200_000;

    public const int DiceScale = 10_000;
    public const uint DiceRollBound = 10_001;
    public const int DiceMinChance = 1;
    public const int DiceMaxChance = 9_800;
    public const int DiceDefaultTarget = 5_050;
    public const long DiceTopMultiple = 9_900;
    private const long DiceMultiplierNumerator = 99_000_000;
    private const long DicePayoutNumerator = 9_900;

    public const uint LimboBound = 1u << 24;
    public const int LimboMinTarget = 101;
    public const int LimboMaxTarget = 100_000_000;
    public const int LimboMinResult = 100;
    public const int LimboDefaultTarget = 200;
    public const long LimboTopMultiple = 1_000_000;
    private const long ChanceFromMultiplierNumerator = 990_000;

    public const int KenoTiles = 40;
    public const int KenoDraws = 10;
    public const int KenoMaxPicks = 10;
    public const int KenoRiskCount = 4;
    public const int KenoClassic = 0;
    public const int KenoLow = 1;
    public const int KenoMedium = 2;
    public const int KenoHigh = 3;
    public const long KenoTopMultiple = 1_000;

    public const int HiLoDeck = 52;
    public const int HiLoRanks = 13;
    public const int HiLoSuits = 4;
    public const int HiLoMaxMoves = HiLoDeck - 1;
    public const long HiLoChainCap = 1_000_000;
    public const long HiLoTopMultiple = HiLoChainCap;
    private const long HiLoCallNumerator = 128_700;

    public const int PhaseLive = 0;
    public const int PhaseCashedOut = 1;
    public const int PhaseBusted = 2;
    public const int PhaseVoided = 3;

    private static readonly string[] CallWires = { "higher", "lower", "above", "below", "same", "skip" };

    private static readonly int[][] KenoClassicTable =
    {
        new[] { 0, 396 },
        new[] { 0, 190, 450 },
        new[] { 0, 100, 310, 1040 },
        new[] { 0, 80, 180, 500, 2250 },
        new[] { 0, 25, 140, 410, 1650, 3600 },
        new[] { 0, 0, 100, 368, 700, 1650, 4000 },
        new[] { 0, 0, 47, 300, 450, 1400, 3100, 6000 },
        new[] { 0, 0, 0, 220, 400, 1300, 2200, 5500, 7000 },
        new[] { 0, 0, 0, 155, 300, 800, 1500, 4400, 6000, 8500 },
        new[] { 0, 0, 0, 140, 225, 450, 800, 1700, 5000, 8000, 10000 },
    };

    private static readonly int[][] KenoLowTable =
    {
        new[] { 70, 185 },
        new[] { 0, 200, 380 },
        new[] { 0, 110, 138, 2600 },
        new[] { 0, 0, 220, 790, 9000 },
        new[] { 0, 0, 150, 420, 1300, 30000 },
        new[] { 0, 0, 110, 200, 620, 10000, 70000 },
        new[] { 0, 0, 110, 160, 350, 1500, 22500, 70000 },
        new[] { 0, 0, 110, 150, 200, 550, 3900, 10000, 80000 },
        new[] { 0, 0, 110, 130, 170, 250, 750, 5000, 25000, 100000 },
        new[] { 0, 0, 110, 120, 130, 180, 350, 1300, 5000, 25000, 100000 },
    };

    private static readonly int[][] KenoMediumTable =
    {
        new[] { 40, 275 },
        new[] { 0, 180, 510 },
        new[] { 0, 0, 280, 5000 },
        new[] { 0, 0, 170, 1000, 10000 },
        new[] { 0, 0, 140, 400, 1400, 39000 },
        new[] { 0, 0, 0, 300, 900, 18000, 71000 },
        new[] { 0, 0, 0, 200, 700, 3000, 40000, 80000 },
        new[] { 0, 0, 0, 200, 400, 1100, 6700, 40000, 90000 },
        new[] { 0, 0, 0, 200, 250, 500, 1500, 10000, 50000, 100000 },
        new[] { 0, 0, 0, 160, 200, 400, 700, 2600, 10000, 50000, 100000 },
    };

    private static readonly int[][] KenoHighTable =
    {
        new[] { 0, 396 },
        new[] { 0, 0, 1710 },
        new[] { 0, 0, 0, 8150 },
        new[] { 0, 0, 0, 1000, 25900 },
        new[] { 0, 0, 0, 450, 4800, 45000 },
        new[] { 0, 0, 0, 0, 1100, 35000, 71000 },
        new[] { 0, 0, 0, 0, 700, 9000, 40000, 80000 },
        new[] { 0, 0, 0, 0, 500, 2000, 27000, 60000, 90000 },
        new[] { 0, 0, 0, 0, 400, 1100, 5600, 50000, 80000, 100000 },
        new[] { 0, 0, 0, 0, 350, 800, 1300, 6300, 50000, 80000, 100000 },
    };

    private static readonly int[][][] KenoTables = { KenoClassicTable, KenoLowTable, KenoMediumTable, KenoHighTable };

    public static long Binomial(int total, int chosen)
    {
        if (chosen < 0 || chosen > total)
        {
            return 0;
        }

        var result = 1L;
        var smaller = Math.Min(chosen, total - chosen);
        for (var step = 1; step <= smaller; step++)
        {
            result = result * (total - smaller + step) / step;
        }

        return result;
    }

    public static bool IsMineCount(int mines) => mines >= MinMines && mines <= MaxMines;

    public static int SafeTiles(int mines) => MinesTiles - mines;

    public static long MinesHundredths(int mines, int picks)
    {
        if (!IsMineCount(mines) || picks <= 0 || picks > SafeTiles(mines))
        {
            return 0;
        }

        return 99 * Binomial(MinesTiles, picks) / Binomial(MinesTiles - mines, picks);
    }

    public static long MinesPayout(long stake, int mines, int picks) => stake * MinesHundredths(mines, picks) / 100;

    public static int MinesNextChanceBasisPoints(int mines, int picks)
    {
        var hidden = MinesTiles - picks;
        var safe = SafeTiles(mines) - picks;
        return hidden <= 0 || safe <= 0 ? 0 : safe * 10_000 / hidden;
    }

    public static int DiceChance(int target, bool over) => over ? DiceScale - target : target;

    public static int DiceTargetFor(int chance, bool over) => over ? DiceScale - chance : chance;

    public static bool IsDiceChance(int chance) => chance >= DiceMinChance && chance <= DiceMaxChance;

    public static bool IsDiceTarget(int target, bool over) => IsDiceChance(DiceChance(target, over));

    public static int ClampDiceChance(int chance) => Math.Clamp(chance, DiceMinChance, DiceMaxChance);

    public static long DiceMultiplierTenThousandths(int chance) =>
        IsDiceChance(chance) ? DiceMultiplierNumerator / chance : 0;

    public static int DiceChanceForMultiplier(long multiplierHundredths) =>
        multiplierHundredths <= 0
            ? DiceMaxChance
            : ClampDiceChance((int)Math.Min(DiceMaxChance, ChanceFromMultiplierNumerator / multiplierHundredths));

    public static long DicePayout(long stake, int chance) =>
        IsDiceChance(chance) ? stake * DicePayoutNumerator / chance : 0;

    public static bool DiceWins(int roll, int target, bool over) => over ? roll > target : roll < target;

    public static bool IsLimboTarget(int target) => target >= LimboMinTarget && target <= LimboMaxTarget;

    public static int ClampLimboTarget(int target) => Math.Clamp(target, LimboMinTarget, LimboMaxTarget);

    public static int LimboResult(uint draw)
    {
        var raw = 99L * LimboBound / ((long)draw + 1);
        return (int)Math.Clamp(raw, LimboMinResult, LimboMaxTarget);
    }

    public static int LimboChanceBasisPoints(int target) =>
        target <= 0 ? 0 : (int)Math.Min(DiceScale, ChanceFromMultiplierNumerator / target);

    public static long LimboPayout(long stake, int target) => stake * target / 100;

    public static bool LimboWins(int result, int target) => result >= target;

    public static bool IsKenoRisk(int risk) => risk >= 0 && risk < KenoRiskCount;

    public static int KenoPayHundredths(int risk, int picks, int hits)
    {
        if (!IsKenoRisk(risk) || picks < 1 || picks > KenoMaxPicks || hits < 0 || hits > picks)
        {
            return 0;
        }

        return KenoTables[risk][picks - 1][hits];
    }

    public static long KenoPayout(long stake, int risk, int picks, int hits) =>
        stake * KenoPayHundredths(risk, picks, hits) / 100;

    public static int KenoHits(ReadOnlySpan<int> picks, ReadOnlySpan<int> drawn)
    {
        var hits = 0;
        for (var pickIndex = 0; pickIndex < picks.Length; pickIndex++)
        {
            if (drawn.IndexOf(picks[pickIndex]) >= 0)
            {
                hits++;
            }
        }

        return hits;
    }

    public static bool AreKenoPicks(ReadOnlySpan<int> picks)
    {
        if (picks.Length < 1 || picks.Length > KenoMaxPicks)
        {
            return false;
        }

        for (var pickIndex = 0; pickIndex < picks.Length; pickIndex++)
        {
            var tile = picks[pickIndex];
            if (tile < 0 || tile >= KenoTiles || picks[..pickIndex].IndexOf(tile) >= 0)
            {
                return false;
            }
        }

        return true;
    }

    public static double KenoReturn(int risk, int picks)
    {
        var total = (double)Binomial(KenoTiles, KenoDraws);
        var sum = 0d;
        for (var hits = 0; hits <= picks; hits++)
        {
            var ways = (double)Binomial(picks, hits) * Binomial(KenoTiles - picks, KenoDraws - hits);
            sum += ways / total * KenoPayHundredths(risk, picks, hits) / 100d;
        }

        return sum;
    }

    public static int HiLoRank(int card) => card / HiLoSuits + 1;

    public static int HiLoSuit(int card) => card % HiLoSuits;

    public static bool IsHiLoCard(int card) => card >= 0 && card < HiLoDeck;

    public static string CallWire(HiLoCall call) => CallWires[(int)call];

    public static bool TryParseCall(string wire, out HiLoCall call)
    {
        for (var index = 0; index < CallWires.Length; index++)
        {
            if (string.Equals(CallWires[index], wire, StringComparison.Ordinal))
            {
                call = (HiLoCall)index;
                return true;
            }
        }

        call = HiLoCall.Skip;
        return false;
    }

    public static int WinningRanks(HiLoCall call, int rank) => call switch
    {
        HiLoCall.Higher => HiLoRanks + 1 - rank,
        HiLoCall.Lower => rank,
        HiLoCall.Above => HiLoRanks - rank,
        HiLoCall.Below => rank - 1,
        HiLoCall.Same => 1,
        _ => 0,
    };

    public static bool IsLegalCall(HiLoCall call, int rank)
    {
        var count = WinningRanks(call, rank);
        var endCardEcho = (call == HiLoCall.Lower && rank == 1) || (call == HiLoCall.Higher && rank == HiLoRanks);
        return call != HiLoCall.Skip && !endCardEcho && count >= 1 && count <= HiLoRanks - 1;
    }

    public static bool CallWins(HiLoCall call, int shownRank, int nextRank) => call switch
    {
        HiLoCall.Higher => nextRank >= shownRank,
        HiLoCall.Lower => nextRank <= shownRank,
        HiLoCall.Above => nextRank > shownRank,
        HiLoCall.Below => nextRank < shownRank,
        HiLoCall.Same => nextRank == shownRank,
        _ => false,
    };

    public static int CallChanceBasisPoints(HiLoCall call, int rank)
    {
        var count = WinningRanks(call, rank);
        return count <= 0 ? 0 : count * 10_000 / HiLoRanks;
    }

    public static long CallMultiplierTenThousandths(HiLoCall call, int rank)
    {
        var count = WinningRanks(call, rank);
        return count <= 0 ? 0 : HiLoCallNumerator / count;
    }

    public static double CallFactor(HiLoCall call, int rank)
    {
        var count = WinningRanks(call, rank);
        return count <= 0 ? 0d : 12.87d / count;
    }

    public static long TopMultiple(string wireKind) => wireKind switch
    {
        CasinoWire.MinesKind => MinesTopMultiple,
        CasinoWire.DiceKind => DiceTopMultiple,
        CasinoWire.LimboKind => LimboTopMultiple,
        CasinoWire.KenoKind => KenoTopMultiple,
        CasinoWire.HiLoKind => HiLoTopMultiple,
        _ => 0,
    };
}
