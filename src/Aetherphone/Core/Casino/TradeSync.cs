namespace Aetherphone.Core.Casino;

internal readonly record struct TradeWindowSample(
    bool Open,
    string PartnerName,
    string PartnerWorld,
    long MyOffer,
    long TheirOffer,
    long WalletGil,
    bool WalletRead);

internal readonly record struct TradeCompletion(string PartnerName, string PartnerWorld, long Received, long Sent)
{
    public long Net => Received - Sent;
}

internal sealed class TradeSyncTracker
{
    public const long ArmedMilliseconds = 30_000;
    public const int MaxArmed = 4;

    private const int NoMatch = -2;
    private const int OpenSlot = -1;

    private readonly TradeSnapshot[] armed = new TradeSnapshot[MaxArmed];

    private int armedCount;
    private TradeSnapshot open;
    private bool windowOpen;
    private bool openDone;
    private long nextSequence;
    private long lastWallet;
    private bool walletKnown;

    private readonly record struct TradeSnapshot(string PartnerName, string PartnerWorld, long MyOffer,
        long TheirOffer, long Sequence, long ClosedAtTick)
    {
        public long Net => TheirOffer - MyOffer;

        public bool SamePartner(string partnerName, string partnerWorld) =>
            string.Equals(PartnerName, partnerName, StringComparison.Ordinal)
            && string.Equals(PartnerWorld, partnerWorld, StringComparison.Ordinal);
    }

    public TradeCompletion? Observe(in TradeWindowSample sample, long nowTick)
    {
        Expire(nowTick);
        if (sample.Open)
        {
            Show(sample, nowTick);
        }
        else if (windowOpen)
        {
            Hide(nowTick);
        }

        return Settle(sample);
    }

    public void Reset()
    {
        Array.Clear(armed);
        armedCount = 0;
        open = default;
        windowOpen = false;
        openDone = false;
        lastWallet = 0;
        walletKnown = false;
    }

    private void Show(in TradeWindowSample sample, long nowTick)
    {
        if (!windowOpen)
        {
            windowOpen = true;
            openDone = false;
            open = Fresh();
        }

        var partnerChanged = sample.PartnerName.Length > 0
                             && !string.Equals(open.PartnerName, sample.PartnerName, StringComparison.Ordinal);
        if (openDone)
        {
            if (!partnerChanged || open.PartnerName.Length == 0)
            {
                return;
            }

            open = Fresh();
            openDone = false;
        }
        else if (partnerChanged && open.PartnerName.Length > 0)
        {
            Arm(open with { ClosedAtTick = nowTick });
            open = Fresh();
        }

        if (partnerChanged && TryTakeArmed(sample.PartnerName, sample.PartnerWorld, out var continued))
        {
            open = continued with { ClosedAtTick = 0 };
        }

        if (sample.PartnerName.Length > 0)
        {
            open = open with { PartnerName = sample.PartnerName, PartnerWorld = sample.PartnerWorld };
        }

        open = open with { MyOffer = Math.Max(0, sample.MyOffer), TheirOffer = Math.Max(0, sample.TheirOffer) };
    }

    private void Hide(long nowTick)
    {
        windowOpen = false;
        if (openDone)
        {
            openDone = false;
            return;
        }

        AepLog.Info($"[TradeSync] Trade window closed: offered {open.MyOffer} gil, received {open.TheirOffer} gil");
        Arm(open with { ClosedAtTick = nowTick });
    }

    private TradeSnapshot Fresh()
    {
        nextSequence++;
        return new TradeSnapshot(string.Empty, string.Empty, 0, 0, nextSequence, 0);
    }

    private void Arm(in TradeSnapshot snapshot)
    {
        if (armedCount == MaxArmed)
        {
            RemoveArmedAt(0);
        }

        var slot = armedCount;
        while (slot > 0 && armed[slot - 1].Sequence > snapshot.Sequence)
        {
            armed[slot] = armed[slot - 1];
            slot--;
        }

        armed[slot] = snapshot;
        armedCount++;
    }

    private bool TryTakeArmed(string partnerName, string partnerWorld, out TradeSnapshot snapshot)
    {
        for (var index = 0; index < armedCount; index++)
        {
            if (!armed[index].SamePartner(partnerName, partnerWorld))
            {
                continue;
            }

            snapshot = armed[index];
            RemoveArmedAt(index);
            return true;
        }

        snapshot = default;
        return false;
    }

    private void RemoveArmedAt(int index)
    {
        for (var next = index + 1; next < armedCount; next++)
        {
            armed[next - 1] = armed[next];
        }

        armedCount--;
        armed[armedCount] = default;
    }

    private void Expire(long nowTick)
    {
        var index = 0;
        while (index < armedCount)
        {
            if (nowTick - armed[index].ClosedAtTick > ArmedMilliseconds)
            {
                RemoveArmedAt(index);
                continue;
            }

            index++;
        }
    }

    private TradeCompletion? Settle(in TradeWindowSample sample)
    {
        if (!sample.WalletRead)
        {
            return null;
        }

        if (!walletKnown)
        {
            walletKnown = true;
            lastWallet = sample.WalletGil;
            return null;
        }

        var delta = sample.WalletGil - lastWallet;
        lastWallet = sample.WalletGil;
        if (delta == 0)
        {
            return null;
        }

        var matched = OldestMatching(delta, windowOpen && !openDone);
        if (matched == NoMatch)
        {
            if (armedCount > 0 || (windowOpen && !openDone))
            {
                AepLog.Info($"[TradeSync] The wallet moved by {delta} gil and matched none of the {armedCount} armed trades");
            }

            return null;
        }

        TradeSnapshot snapshot;
        if (matched == OpenSlot)
        {
            snapshot = open;
            openDone = true;
        }
        else
        {
            snapshot = armed[matched];
            RemoveArmedAt(matched);
        }

        return new TradeCompletion(snapshot.PartnerName, snapshot.PartnerWorld, delta > 0 ? delta : 0,
            delta < 0 ? -delta : 0);
    }

    private int OldestMatching(long delta, bool openCandidate)
    {
        var matched = NoMatch;
        var sequence = long.MaxValue;
        for (var index = 0; index < armedCount; index++)
        {
            if (armed[index].Net == delta && armed[index].Sequence < sequence)
            {
                matched = index;
                sequence = armed[index].Sequence;
            }
        }

        if (openCandidate && open.Net == delta && open.Sequence < sequence)
        {
            matched = OpenSlot;
        }

        return matched;
    }
}
