namespace Aetherphone.Apps.Casino.Machines;

internal struct MachineRollup
{
    private double shown;
    private long target;
    private double compressRate;

    public readonly long Shown => (long)Math.Floor(shown);

    public readonly long Target => target;

    public readonly bool Done => shown >= target;

    public void Snap(long value)
    {
        shown = value;
        target = value;
        compressRate = 0;
    }

    public void Finish()
    {
        shown = target;
    }

    public void Update(long wanted, long bet, float deltaSeconds, bool turbo)
    {
        var next = Math.Max(wanted, 0);
        var moved = next != target;
        target = next;
        if (shown >= target)
        {
            shown = target;
            return;
        }

        var unit = Math.Max(1L, bet);
        var rate = unit * (double)MachineTiming.RollupBetsPerSecond;
        if (shown >= unit * (double)MachineTiming.RollupFullSpeedBets)
        {
            if (compressRate <= 0 || moved)
            {
                compressRate = (target - shown) / MachineTiming.RollupCompressSeconds;
            }

            rate = Math.Max(rate, compressRate);
        }

        if (turbo)
        {
            rate /= MachineTiming.TurboFactor;
        }

        shown = Math.Min(target, shown + rate * deltaSeconds);
    }
}
