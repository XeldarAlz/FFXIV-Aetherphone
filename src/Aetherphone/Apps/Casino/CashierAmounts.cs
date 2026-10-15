using System.Globalization;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;

namespace Aetherphone.Apps.Casino;

internal enum CashierMode : byte
{
    Buy,
    CashOut,
}

internal enum CashierAmountState : byte
{
    Idle,
    Ready,
    Over,
}

internal enum CashierTone : byte
{
    Idle,
    Ready,
    Warn,
}

internal enum CashOutRoute : byte
{
    None,
    Whole,
    Partial,
}

internal enum CashierConfirmOutcome : byte
{
    Proceed,
    Changed,
    Gone,
}

internal readonly record struct CashOutPlan(CashierAmountState State, CashOutRoute Route, long Coins);

internal readonly record struct CashierPreview(long Pay, long Get, long WalletAfter, long ChipsAfter);

internal static class CashierAmounts
{
    public const int TileCount = 4;
    public const int FieldDigits = 12;
    public const int FieldLength = FieldDigits + 1;
    public const int MaxTileIndex = TileCount - 1;

    public static readonly long[] BuySteps = { 50, 200, 2_000 };
    public static readonly int[] CashOutPercents = { 25, 50, 75 };

    public static long Parse(string buffer)
    {
        if (buffer.Length == 0 || buffer.Length > FieldLength)
        {
            return 0;
        }

        return long.TryParse(buffer, NumberStyles.None, CultureInfo.InvariantCulture, out var coins) && coins > 0
            ? coins
            : 0;
    }

    public static string Format(long coins) =>
        coins > 0 ? coins.ToString(CultureInfo.InvariantCulture) : string.Empty;

    public static int BuyTiles(long walletCoins, Span<long> into)
    {
        for (var stepIndex = 0; stepIndex < BuySteps.Length; stepIndex++)
        {
            into[stepIndex] = BuySteps[stepIndex];
        }

        into[MaxTileIndex] = Math.Max(0, walletCoins);
        return TileCount;
    }

    public static int CashOutTiles(long wholeCoins, bool partial, Span<long> into)
    {
        var whole = Math.Max(0, wholeCoins);
        if (!partial)
        {
            into[0] = whole;
            return 1;
        }

        for (var percentIndex = 0; percentIndex < CashOutPercents.Length; percentIndex++)
        {
            into[percentIndex] = whole * CashOutPercents[percentIndex] / 100;
        }

        into[MaxTileIndex] = whole;
        return TileCount;
    }

    public static bool TileOn(long tileCoins, long fieldCoins) => tileCoins > 0 && tileCoins == fieldCoins;

    public static CashierAmountState BuyState(long coins, long walletCoins)
    {
        if (coins < ChipsAmounts.MinimumCoins)
        {
            return CashierAmountState.Idle;
        }

        return coins > walletCoins ? CashierAmountState.Over : CashierAmountState.Ready;
    }

    public static long BankrollRate(CasinoSittingDto? bankroll, long fallbackRate) =>
        bankroll is { RateChipsPerCoin: > 0 } ? bankroll.RateChipsPerCoin : fallbackRate;

    public static bool PartialAllowed(bool flagged, bool walletFrozen) => flagged && !walletFrozen;

    public static bool IsDust(long stackChips, long rate) => stackChips > 0 && ChipValue.Coins(stackChips, rate) == 0;

    public static CashOutPlan PlanCashOut(long coins, long stackChips, long rate, bool partial)
    {
        if (coins <= 0)
        {
            return IsDust(stackChips, rate)
                ? new CashOutPlan(CashierAmountState.Ready, CashOutRoute.Whole, 0)
                : new CashOutPlan(CashierAmountState.Idle, CashOutRoute.None, 0);
        }

        var whole = ChipValue.Coins(Math.Max(0, stackChips), rate);
        if (coins > whole)
        {
            return new CashOutPlan(CashierAmountState.Over, CashOutRoute.None, coins);
        }

        if (coins == whole)
        {
            return new CashOutPlan(CashierAmountState.Ready, CashOutRoute.Whole, coins);
        }

        return partial
            ? new CashOutPlan(CashierAmountState.Ready, CashOutRoute.Partial, coins)
            : new CashOutPlan(CashierAmountState.Idle, CashOutRoute.None, coins);
    }

    public static CashierConfirmOutcome Reconfirm(CashOutRoute confirmedRoute, long confirmedCoins, long stackChips,
        long rate, bool partial, out CashOutPlan plan)
    {
        var coins = confirmedRoute == CashOutRoute.Whole
            ? ChipValue.Coins(Math.Max(0, stackChips), rate)
            : confirmedCoins;
        plan = PlanCashOut(coins, stackChips, rate, partial);
        if (plan.State != CashierAmountState.Ready)
        {
            return CashierConfirmOutcome.Gone;
        }

        return plan.Route == confirmedRoute && plan.Coins == confirmedCoins
            ? CashierConfirmOutcome.Proceed
            : CashierConfirmOutcome.Changed;
    }

    public static CashierPreview BuyPreview(long coins, long rate, long walletCoins, long stackChips)
    {
        var chips = coins * rate;
        return new CashierPreview(coins, chips, walletCoins - coins, stackChips + chips);
    }

    public static CashierPreview CashOutPreview(in CashOutPlan plan, long rate, long walletCoins, long stackChips)
    {
        var taken = plan.Route == CashOutRoute.Whole ? stackChips : plan.Coins * rate;
        return new CashierPreview(taken, plan.Coins, walletCoins + plan.Coins, stackChips - taken);
    }
}
