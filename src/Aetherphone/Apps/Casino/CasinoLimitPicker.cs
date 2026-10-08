using Aetherphone.Core.Casino;

namespace Aetherphone.Apps.Casino;

internal enum LimitChange : byte
{
    None,
    StartsNow,
    NextDay,
}

internal static class CasinoLimitPicker
{
    public const long Floor = CasinoLimits.SelfLimitFloor;
    public const long FineStep = 10 * CasinoChipLots.ChipPerCoin;
    public const long MediumStep = 100 * CasinoChipLots.ChipPerCoin;
    public const long CoarseStep = 1_000 * CasinoChipLots.ChipPerCoin;
    public const long MediumFrom = 500 * CasinoChipLots.ChipPerCoin;
    public const long CoarseFrom = 5_000 * CasinoChipLots.ChipPerCoin;

    public static long CeilingFor(long dailyBuyInCap) =>
        dailyBuyInCap > Floor ? dailyBuyInCap : CasinoLimits.FallbackCeiling;

    public static long StepFor(long value)
    {
        if (value < MediumFrom)
        {
            return FineStep;
        }

        return value < CoarseFrom ? MediumStep : CoarseStep;
    }

    public static long Snap(long value, long ceiling)
    {
        var clamped = Math.Clamp(value, Floor, ceiling);
        var step = StepFor(clamped);
        var snapped = (clamped + step / 2) / step * step;
        return Math.Clamp(snapped, Floor, ceiling);
    }

    public static float FractionOf(long value, long ceiling)
    {
        if (ceiling <= Floor)
        {
            return 0f;
        }

        var clamped = Math.Clamp(value, Floor, ceiling);
        return (float)(Math.Log((double)clamped / Floor) / Math.Log((double)ceiling / Floor));
    }

    public static long FromFraction(float fraction, long ceiling)
    {
        if (ceiling <= Floor)
        {
            return Floor;
        }

        var clamped = Math.Clamp(fraction, 0f, 1f);
        var raw = Floor * Math.Pow((double)ceiling / Floor, clamped);
        return Snap((long)Math.Round(raw), ceiling);
    }

    public static long Nudge(long value, int direction, long ceiling)
    {
        if (direction > 0)
        {
            return Snap(value + StepFor(value), ceiling);
        }

        if (direction < 0)
        {
            return Snap(value - StepFor(value - 1), ceiling);
        }

        return Snap(value, ceiling);
    }

    public static LimitChange ChangeOf(long? chosen, long? currentSelf, bool raisePending, long? pendingRaise)
    {
        if (chosen == currentSelf || (raisePending && chosen == pendingRaise))
        {
            return LimitChange.None;
        }

        if (chosen is null)
        {
            return LimitChange.NextDay;
        }

        return currentSelf is null || chosen.Value < currentSelf.Value ? LimitChange.StartsNow : LimitChange.NextDay;
    }
}
