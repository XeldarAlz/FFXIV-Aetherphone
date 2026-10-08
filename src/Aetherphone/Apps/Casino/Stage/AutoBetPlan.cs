using Aetherphone.Core.Casino;

namespace Aetherphone.Apps.Casino.Stage;

internal enum AutoAdjust : byte
{
    Reset,
    Increase,
}

internal enum AutoStop : byte
{
    None,
    Manual,
    Count,
    Profit,
    Loss,
    Bonus,
    Chips,
    Refused,
}

internal sealed class AutoBetPlan
{
    public const int DefaultRounds = 10;
    public const int MaxRounds = 1000;
    public const int MaxPercent = 1000;

    public int Rounds { get; set; } = DefaultRounds;

    public AutoAdjust OnWin { get; set; }

    public int OnWinPercent { get; set; }

    public AutoAdjust OnLoss { get; set; }

    public int OnLossPercent { get; set; }

    public long StopOnProfit { get; set; }

    public long StopOnLoss { get; set; }

    public bool StopOnBonus { get; set; } = true;

    public bool Running { get; private set; }

    public int Played { get; private set; }

    public long Net { get; private set; }

    public long Base { get; private set; }

    public long Next { get; private set; }

    public AutoStop Stopped { get; private set; }

    public int Remaining => Rounds <= 0 ? -1 : Math.Max(0, Rounds - Played);

    public void Start(long amount)
    {
        if (amount <= 0)
        {
            return;
        }

        Running = true;
        Played = 0;
        Net = 0;
        Base = amount;
        Next = amount;
        Stopped = AutoStop.None;
    }

    public void Stop(AutoStop reason)
    {
        if (!Running)
        {
            return;
        }

        Running = false;
        Stopped = reason;
    }

    public void Acknowledge()
    {
        Stopped = AutoStop.None;
    }

    public AutoStop Settle(long stake, long payout, bool bonus, long minimumBet, long maximumBet, long stack)
    {
        if (!Running)
        {
            return Stopped;
        }

        Played++;
        Net += payout - stake;
        if (StopOnBonus && bonus)
        {
            Stop(AutoStop.Bonus);
            return Stopped;
        }

        if (StopOnProfit > 0 && Net >= StopOnProfit)
        {
            Stop(AutoStop.Profit);
            return Stopped;
        }

        if (StopOnLoss > 0 && -Net >= StopOnLoss)
        {
            Stop(AutoStop.Loss);
            return Stopped;
        }

        if (Rounds > 0 && Played >= Rounds)
        {
            Stop(AutoStop.Count);
            return Stopped;
        }

        var won = payout > stake;
        var next = Adjust(stake, won ? OnWin : OnLoss, won ? OnWinPercent : OnLossPercent);
        var clamped = CasinoLadder.Clamp(next, minimumBet, maximumBet, stack);
        if (clamped <= 0 || clamped > stack)
        {
            Stop(AutoStop.Chips);
            return Stopped;
        }

        Next = clamped;
        return AutoStop.None;
    }

    public long Adjust(long current, AutoAdjust adjust, int percent)
    {
        if (adjust == AutoAdjust.Reset || percent <= 0)
        {
            return Base;
        }

        var bounded = Math.Min(percent, MaxPercent);
        var raised = current + current * bounded / 100;
        var floored = CasinoLadder.FloorToRung(raised);
        var stepped = CasinoLadder.StepUp(current);
        return floored > stepped ? floored : stepped;
    }
}
