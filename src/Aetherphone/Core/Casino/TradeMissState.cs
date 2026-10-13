namespace Aetherphone.Core.Casino;

internal sealed class TradeMissState
{
    public const long RepeatWindowMilliseconds = 15_000;

    private volatile TradeMiss? lastMiss;
    private volatile GuestOffer? pendingGuest;
    private GuestOffer? handledGuest;
    private long handledAtTick;

    public TradeMiss? LastMiss => lastMiss;

    public GuestOffer? PendingGuest => pendingGuest;

    public bool Repeats(TradeMiss miss, long nowTick)
    {
        var handled = handledGuest;
        return handled is not null && nowTick - handledAtTick <= RepeatWindowMilliseconds
            && GuestLedger.SameTrade(handled, miss);
    }

    public bool Miss(TradeMiss miss, GuestOffer? offer, long nowTick)
    {
        if (Repeats(miss, nowTick))
        {
            return false;
        }

        lastMiss = miss;
        pendingGuest = offer;
        return true;
    }

    public GuestOffer? TakeGuest()
    {
        return Interlocked.Exchange(ref pendingGuest, null);
    }

    public bool Handled(GuestOffer offer, TradeMiss? answered, long nowTick)
    {
        handledGuest = offer;
        handledAtTick = nowTick;
        if (ReferenceEquals(pendingGuest, offer))
        {
            pendingGuest = null;
        }

        return answered is not null && Answered(answered);
    }

    public bool Answered(TradeMiss miss)
    {
        if (!ReferenceEquals(lastMiss, miss))
        {
            return false;
        }

        lastMiss = null;
        pendingGuest = null;
        return true;
    }

    public void Clear()
    {
        lastMiss = null;
        pendingGuest = null;
    }
}
