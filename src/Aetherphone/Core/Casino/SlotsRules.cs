namespace Aetherphone.Core.Casino;

internal static class SlotsRules
{
    public const string BirdId = "slots.bird";
    public const string CascadeId = "slots.cascade";
    public const string MoogleId = "slots.moogle";

    public const string BaseMode = "base";
    public const string AnteMode = "ante";
    public const string BuyMode = "buy";

    public const string StepBase = "base";
    public const string StepTumble = "tumble";
    public const string StepFree = "free";
    public const string StepBuy = "buy";
    public const string StepExpand = "expand";
    public const string StepGame = "game";
    public const string StepHold = "hold";
    public const string StepRespin = "respin";
    public const string StepCollect = "collect";
    public const string StepMeter = "meter";

    public const int LineScatter = -1;
    public const int LineCluster = -2;
    public const int LineExpander = -3;
    public const int LineGrand = -4;

    public const int CoinCash = 0;
    public const int CoinMini = 1;
    public const int CoinMinor = 2;
    public const int CoinMajor = 3;
    public const int CoinOrb = 10;

    public const string MeterMini = "mini";
    public const string MeterMinor = "minor";

    public const long MinStake = 100;
    public const long DefaultBet = 1_000;
    public const long UnitsPerBet = 10_000;
    public const int ReelCount = 5;
    public const int RowCount = 3;
    public const int CellCount = 15;
    public const int PaylineCount = 10;
    public const int MinLineMatch = 3;
    public const int AnteDivisor = 4;
    public const long JackpotChipsPerHit = 1_500_000_000;

    public const long LegacyJackpotChipsPerHit = 150_000_000;
    public const int LegacyStopsPerReel = 40;
    public const int LegacyFreeSpinCap = 40;

    public const int GambleRed = 0;
    public const int GambleBlack = 1;
    public const int GambleMaxSteps = 5;
    public const long GambleEligibleBelowBets = 20;

    public static readonly int[][] Paylines =
    {
        new[] { 1, 1, 1, 1, 1 },
        new[] { 0, 0, 0, 0, 0 },
        new[] { 2, 2, 2, 2, 2 },
        new[] { 0, 1, 2, 1, 0 },
        new[] { 2, 1, 0, 1, 2 },
        new[] { 0, 0, 1, 2, 2 },
        new[] { 2, 2, 1, 0, 0 },
        new[] { 1, 0, 1, 2, 1 },
        new[] { 1, 2, 1, 0, 1 },
        new[] { 2, 1, 1, 1, 0 },
    };

    public static bool IsBet(long bet)
    {
        return bet >= MinStake && CasinoLadder.IsRung(bet);
    }

    public static bool IsMachine(string machineId)
    {
        return string.Equals(machineId, BirdId, StringComparison.Ordinal)
            || string.Equals(machineId, CascadeId, StringComparison.Ordinal)
            || string.Equals(machineId, MoogleId, StringComparison.Ordinal);
    }

    public static bool Offers(string machineId, string mode)
    {
        if (string.Equals(mode, BaseMode, StringComparison.Ordinal))
        {
            return IsMachine(machineId);
        }

        return string.Equals(machineId, CascadeId, StringComparison.Ordinal)
            && (string.Equals(mode, AnteMode, StringComparison.Ordinal)
                || string.Equals(mode, BuyMode, StringComparison.Ordinal));
    }

    public static long CostOf(string mode, long bet)
    {
        if (string.Equals(mode, AnteMode, StringComparison.Ordinal))
        {
            return bet + bet / AnteDivisor;
        }

        if (string.Equals(mode, BuyMode, StringComparison.Ordinal))
        {
            return bet * CrystalCascadeRules.BuyCostMultiple;
        }

        return bet;
    }

    public static long ChipsFor(long bet, long units)
    {
        return (long)((Int128)bet * units / UnitsPerBet);
    }

    public static bool GambleEligible(string machineId, long bet, long totalWin)
    {
        return string.Equals(machineId, BirdId, StringComparison.Ordinal) && bet > 0 && totalWin > 0
            && totalWin < bet * GambleEligibleBelowBets;
    }

    public static long JackpotSpinsPerHit(long cost)
    {
        return cost <= 0 ? 0 : JackpotChipsPerHit / cost;
    }
}
