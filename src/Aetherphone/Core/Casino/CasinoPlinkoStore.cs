using System.Collections.Concurrent;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Casino;

internal sealed class CasinoPlinkoStore : IDisposable
{
    public const int PacingMilliseconds = 140;

    private readonly AethernetSession session;
    private readonly CasinoClient casino;
    private readonly CasinoStore store;
    private readonly StoreWork work = new("CasinoPlinko");
    private readonly object gate = new();
    private readonly Queue<PlinkoDropRequest> queue = new(PlinkoRules.MaxInFlight);
    private readonly ConcurrentQueue<CasinoPlinkoDropDto> results = new();

    private PlinkoDropRequest? held;
    private bool pumping;
    private int sending;
    private long queuedStake;
    private long heldPayout;
    private long lastSentTick;
    private int epoch;
    private int failed;

    public CasinoPlinkoStore(AethernetSession session, CasinoClient casino, CasinoStore store)
    {
        this.session = session;
        this.casino = casino;
        this.store = store;
    }

    public int Outstanding
    {
        get
        {
            lock (gate)
            {
                return queue.Count + sending + results.Count;
            }
        }
    }

    public long QueuedStake
    {
        get
        {
            lock (gate)
            {
                return queuedStake;
            }
        }
    }

    public long HeldPayout
    {
        get
        {
            lock (gate)
            {
                return heldPayout;
            }
        }
    }

    public long DisplayStack()
    {
        lock (gate)
        {
            var stack = store.State?.Sitting?.Stack ?? 0;
            return Math.Max(0, stack - heldPayout);
        }
    }

    public bool TryTake(out CasinoPlinkoDropDto result)
    {
        lock (gate)
        {
            return results.TryDequeue(out result!);
        }
    }

    public bool TakeFailure() => Interlocked.Exchange(ref failed, 0) == 1;

    public void Land(long payout)
    {
        lock (gate)
        {
            heldPayout = Math.Max(0, heldPayout - Math.Max(0, payout));
        }
    }

    public void Forget()
    {
        lock (gate)
        {
            epoch++;
            heldPayout = 0;
            results.Clear();
        }
    }

    public bool Drop(int rows, int risk, long stake)
    {
        var sittingId = store.State?.Sitting?.Id ?? string.Empty;
        if (!session.IsSignedIn || sittingId.Length == 0 || !PlinkoRules.IsValidBoard(rows, risk)
            || stake < PlinkoRules.MinBet)
        {
            return false;
        }

        lock (gate)
        {
            if (queue.Count + sending + results.Count >= PlinkoRules.MaxInFlight)
            {
                return false;
            }

            var request = Resends(held, sittingId)
                ? held! with { Epoch = epoch }
                : new PlinkoDropRequest(sittingId, Guid.NewGuid().ToString("N"), rows, risk, stake, epoch);
            held = null;
            queue.Enqueue(request);
            queuedStake += request.Stake;
            if (pumping)
            {
                return true;
            }

            pumping = true;
        }

        work.Run("plinko drop", PumpAsync);
        return true;
    }

    internal static bool Resends(PlinkoDropRequest? request, string sittingId) =>
        request is not null && sittingId.Length > 0
        && string.Equals(request.SittingId, sittingId, StringComparison.Ordinal);

    private async Task PumpAsync(CancellationToken token)
    {
        var finished = false;
        try
        {
            while (true)
            {
                PlinkoDropRequest next;
                lock (gate)
                {
                    if (queue.Count == 0)
                    {
                        pumping = false;
                        finished = true;
                        return;
                    }

                    next = queue.Dequeue();
                    sending = 1;
                }

                var wait = PacingMilliseconds - (Environment.TickCount64 - Interlocked.Read(ref lastSentTick));
                if (wait > 0)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(wait), token).ConfigureAwait(false);
                }

                Interlocked.Exchange(ref lastSentTick, Environment.TickCount64);
                var result = await casino.DropPlinkoAsync(next.SittingId, next.ClientRoundId, next.Rows, next.Risk,
                    next.Stake, token).ConfigureAwait(false);
                Deliver(next, result);
            }
        }
        finally
        {
            if (!finished)
            {
                lock (gate)
                {
                    sending = 0;
                    pumping = false;
                    AbandonQueue();
                }
            }
        }
    }

    private void Deliver(PlinkoDropRequest sent, CasinoPlinkoDropDto? result)
    {
        if (result is null)
        {
            lock (gate)
            {
                sending = 0;
                queuedStake -= sent.Stake;
                held = sent;
                AbandonQueue();
            }

            Interlocked.Exchange(ref failed, 1);
            return;
        }

        lock (gate)
        {
            sending = 0;
            queuedStake -= sent.Stake;
            var current = sent.Epoch == epoch;
            if (result.Granted)
            {
                if (current)
                {
                    heldPayout += Math.Max(0, result.Payout);
                }

                store.AbsorbStack(sent.SittingId, result.Stack);
            }
            else
            {
                AbandonQueue();
            }

            if (current)
            {
                results.Enqueue(result);
            }
        }

        if (result.Granted)
        {
            return;
        }

        if (result.Ceiling > 0)
        {
            store.AbsorbCeiling(result.Ceiling);
        }

        store.RefreshNow();
    }

    private void AbandonQueue()
    {
        while (queue.Count > 0)
        {
            queuedStake -= queue.Dequeue().Stake;
        }

        queuedStake = Math.Max(0, queuedStake);
    }

    public void Dispose()
    {
        work.Dispose();
    }
}

internal sealed record PlinkoDropRequest(
    string SittingId,
    string ClientRoundId,
    int Rows,
    int Risk,
    long Stake,
    int Epoch);
