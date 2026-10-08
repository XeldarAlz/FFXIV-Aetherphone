using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Casino;

internal static class CasinoFeatures
{
    public const string EconomyV3 = "economy.v3";

    public const string Bonus = "bonus";

    public const string Levels = "levels";

    public const string Club = "club";

    public const string HostingV2 = "hosting.v2";

    public const string Venue = "venue";

    public const string GilTables = "gil.tables";

    public const string Machines = "machines";

    public const string Missions = "missions";

    public const string Challenges = "challenges";

    public const string Fame = "fame";

    public const string Feed = "feed";

    public const string Rain = "rain";

    public const string Holdem = "holdem";

    public const string Race = "race";

    public const string Plinko = "plinko";

    public const string Originals = "originals";

    public static bool Has(CasinoStateDto? state, string feature)
    {
        var features = state?.Features;
        if (features is null)
        {
            return false;
        }

        for (var index = 0; index < features.Length; index++)
        {
            if (string.Equals(features[index], feature, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}

internal static class CasinoBonusKinds
{
    public const string Welcome = "welcome";

    public const string Timed = "timed";

    public const string Reload = "reload";

    public const string Streak = "streak";

    public const string LevelUp = "levelup";

    public const string Broke = "broke";

    public const string Rebate = "rebate";

    public static readonly string[] All = { Welcome, Timed, Reload, Streak, LevelUp, Broke, Rebate };

    public static int IndexOf(string kind)
    {
        for (var index = 0; index < All.Length; index++)
        {
            if (string.Equals(All[index], kind, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }
}

internal static class CasinoFaucets
{
    public const long WelcomeChips = 50_000;

    public const long TimedChips = 5_000;

    public const int TimedHours = 3;

    public const long BrokeChips = 10_000;

    public const long BrokeBelowChips = 100;

    public const int BrokeMinutes = 30;

    public const long LevelUpPerLevel = 1_000;

    public const long LevelUpCap = 50_000;

    public const long RebateCap = 100_000;

    public const int StreakDays = 7;

    public static readonly long[] StreakChips = { 2_000, 4_000, 6_000, 8_000, 10_000, 15_000, 25_000 };

    public static long LevelUpGrant(int level)
    {
        return level <= 0 ? 0 : Math.Min(LevelUpPerLevel * level, LevelUpCap);
    }

    public static long StreakGrant(int day, int multiplierPercent)
    {
        if (day < 1)
        {
            return 0;
        }

        var index = Math.Min(day, StreakDays) - 1;
        return StreakChips[index] * Math.Max(100, multiplierPercent) / 100;
    }
}

internal static class CasinoClubTiers
{
    public const int Bronze = 0;

    public const int Silver = 1;

    public const int Gold = 2;

    public const int Platinum = 3;

    public const int Diamond = 4;

    public const int Royal = 5;

    public const int Obsidian = 6;

    public const int Count = 7;

    public const int RebateFloor = Silver;

    public const int ReloadFloor = Platinum;

    public static readonly long[] Floors = { 0, 10_000, 50_000, 250_000, 1_000_000, 5_000_000, 25_000_000 };

    public static readonly int[] MultiplierPercents = { 100, 125, 150, 200, 250, 300, 300 };

    public static readonly int[] RebateBasisPoints = { 0, 500, 750, 750, 1000, 1000, 1000 };

    public static int Clamp(int tier)
    {
        return Math.Clamp(tier, Bronze, Obsidian);
    }

    public static float Progress(CasinoClubDto club)
    {
        if (club.NextTierPoints <= club.TierFloor)
        {
            return 1f;
        }

        var span = club.NextTierPoints - club.TierFloor;
        var into = Math.Clamp(club.Points - club.TierFloor, 0, span);
        return (float)into / span;
    }
}

internal static class CasinoCashier
{
    public const long DailyNetCashOutCoinsFallback = 500;

    public static long WholeCoins(long chips, long rate)
    {
        return chips <= 0 || rate <= 0 ? 0 : chips / rate;
    }

    public static long ConvertsNow(long stack, long allowanceCoins, long rate)
    {
        if (rate <= 0)
        {
            return 0;
        }

        return Math.Min(WholeCoins(stack, rate), Math.Max(0, allowanceCoins));
    }

    public static long WaitsChips(long stack, long allowanceCoins, long rate)
    {
        var converts = ConvertsNow(stack, allowanceCoins, rate);
        if (converts >= WholeCoins(stack, rate))
        {
            return 0;
        }

        return Math.Max(0, stack - converts * rate);
    }

    public static long Rate(CasinoStateDto? state)
    {
        if (state is null)
        {
            return CasinoChipLots.ChipPerCoin;
        }

        if (state.RateChipsPerCoin > 0)
        {
            return state.RateChipsPerCoin;
        }

        return state.Sitting is { RateChipsPerCoin: > 0 } sitting ? sitting.RateChipsPerCoin : CasinoChipLots.ChipPerCoin;
    }
}
