using Aetherphone.Core.Net;

namespace Aetherphone.Core.Message;

internal enum ReadMarkOutcome : byte
{
    Confirmed = 0,
    Rejected = 1,
    Unsupported = 2,
    Failed = 3,
}

internal sealed class ReadConfirmations
{
    public static readonly TimeSpan SettleDelay = TimeSpan.FromSeconds(3);
    public static readonly TimeSpan FirstRetryDelay = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(60);

    private readonly object gate = new();
    private readonly Dictionary<string, Entry> pending = new(StringComparer.Ordinal);
    private string? inFlightKey;
    private int nextGeneration;
    private bool unsupported;

    private struct Entry
    {
        public DateTime DueUtc;
        public int Attempts;
        public int Generation;
        public int Account;
    }

    public bool Unsupported
    {
        get
        {
            lock (gate)
            {
                return unsupported;
            }
        }
    }

    public int PendingCount
    {
        get
        {
            lock (gate)
            {
                return pending.Count;
            }
        }
    }

    public bool IsPending(string key)
    {
        lock (gate)
        {
            return pending.ContainsKey(key);
        }
    }

    public void Request(string key, DateTime nowUtc, TimeSpan delay, int account)
    {
        lock (gate)
        {
            if (unsupported)
            {
                return;
            }

            var due = nowUtc + delay;
            if (pending.TryGetValue(key, out var existing) && existing.Account == account && existing.DueUtc < due)
            {
                due = existing.DueUtc;
            }

            pending[key] = new Entry { DueUtc = due, Attempts = 0, Generation = ++nextGeneration, Account = account };
        }
    }

    public int GenerationOf(string key)
    {
        lock (gate)
        {
            return pending.TryGetValue(key, out var entry) ? entry.Generation : 0;
        }
    }

    public void Satisfy(string key, int generation)
    {
        lock (gate)
        {
            if (generation == 0 || string.Equals(inFlightKey, key, StringComparison.Ordinal))
            {
                return;
            }

            if (pending.TryGetValue(key, out var entry) && entry.Generation == generation)
            {
                pending.Remove(key);
            }
        }
    }

    public bool TryTakeDue(DateTime nowUtc, out string key, out int generation, out int account)
    {
        lock (gate)
        {
            key = string.Empty;
            generation = 0;
            account = 0;
            if (inFlightKey is not null || unsupported || pending.Count == 0)
            {
                return false;
            }

            foreach (var pair in pending)
            {
                if (pair.Value.DueUtc > nowUtc)
                {
                    continue;
                }

                inFlightKey = pair.Key;
                key = pair.Key;
                generation = pair.Value.Generation;
                account = pair.Value.Account;
                return true;
            }

            return false;
        }
    }

    public void Complete(string key, int generation, ReadMarkOutcome outcome, DateTime nowUtc)
    {
        lock (gate)
        {
            if (string.Equals(inFlightKey, key, StringComparison.Ordinal))
            {
                inFlightKey = null;
            }

            if (outcome == ReadMarkOutcome.Unsupported)
            {
                unsupported = true;
                pending.Clear();
                return;
            }

            if (!pending.TryGetValue(key, out var entry) || entry.Generation != generation)
            {
                return;
            }

            if (outcome != ReadMarkOutcome.Failed)
            {
                pending.Remove(key);
                return;
            }

            entry.Attempts++;
            entry.DueUtc = nowUtc + RetryDelay(entry.Attempts);
            pending[key] = entry;
        }
    }

    public void Reset()
    {
        lock (gate)
        {
            pending.Clear();
            inFlightKey = null;
            unsupported = false;
        }
    }

    public static TimeSpan RetryDelay(int attempts)
    {
        var delay = FirstRetryDelay;
        for (var step = 1; step < attempts && delay < MaxRetryDelay; step++)
        {
            delay += delay;
        }

        return delay < MaxRetryDelay ? delay : MaxRetryDelay;
    }

    public static ReadMarkOutcome Classify(bool succeeded, AepFailure failure, bool missingRouteIsUnsupported)
    {
        if (succeeded)
        {
            return ReadMarkOutcome.Confirmed;
        }

        var status = failure.StatusCode;
        if (status == 404 && missingRouteIsUnsupported)
        {
            return ReadMarkOutcome.Unsupported;
        }

        if (status is >= 400 and < 500 and not 408 and not 429)
        {
            return ReadMarkOutcome.Rejected;
        }

        return ReadMarkOutcome.Failed;
    }
}
