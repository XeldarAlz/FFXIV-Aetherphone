namespace Aetherphone.Apps.Games.Framework.World;

internal sealed class WaveSpawner
{
    public const int DefaultCapacity = 256;
    public const int MaxLanes = 256;
    private const float SpawnJitter = 0.4f;

    private readonly WaveEntry[] entries;
    private readonly int[] laneSpent;
    private int count;
    private int cursor;
    private float elapsed;

    public WaveSpawner(int laneCount, int capacity = DefaultCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(laneCount);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(laneCount, MaxLanes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        LaneCount = laneCount;
        entries = new WaveEntry[capacity];
        laneSpent = new int[laneCount];
    }

    public int LaneCount { get; }

    public int Capacity => entries.Length;

    public int Count => count;

    public int Spawned => cursor;

    public int Remaining => count - cursor;

    public bool Finished => cursor >= count;

    public float Elapsed => elapsed;

    public float Duration => count == 0 ? 0f : entries[count - 1].Time;

    public int SpentBudget { get; private set; }

    public ReadOnlySpan<WaveEntry> Entries => new(entries, 0, count);

    public void Load(ReadOnlySpan<WaveEntry> table)
    {
        count = Math.Min(table.Length, entries.Length);
        table[..count].CopyTo(entries);
        SortByTime();
        SpentBudget = 0;
        Rewind();
    }

    public int Endless(ref GameRandom random, int wave, in EnemyBudget budget)
    {
        Span<byte> candidates = stackalloc byte[EnemyBudget.MaxKinds];
        Array.Clear(laneSpent);
        var remaining = budget.BudgetFor(wave);
        var spent = 0;
        count = 0;
        while (count < entries.Length)
        {
            var candidateCount = 0;
            for (var kind = 0; kind < budget.KindCount; kind++)
            {
                if (budget.Available(kind, wave) && budget.Cost(kind) <= remaining)
                {
                    candidates[candidateCount++] = (byte)kind;
                }
            }

            if (candidateCount == 0)
            {
                break;
            }

            var chosen = candidates[random.Next(candidateCount)];
            var cost = budget.Cost(chosen);
            var lane = PickLane(ref random);
            remaining -= cost;
            spent += cost;
            laneSpent[lane] += cost;
            entries[count++] = new WaveEntry(0f, (byte)lane, chosen);
        }

        var spacing = budget.WaveSeconds / Math.Max(1, count);
        for (var entryIndex = 0; entryIndex < count; entryIndex++)
        {
            var time = spacing * (entryIndex + 0.5f + random.Range(-SpawnJitter, SpawnJitter));
            entries[entryIndex] = new WaveEntry(time, entries[entryIndex].Lane, entries[entryIndex].EnemyKind);
        }

        SpentBudget = spent;
        Rewind();
        return count;
    }

    public int Advance(float deltaSeconds, Span<WaveEntry> due)
    {
        if (deltaSeconds > 0f)
        {
            elapsed += deltaSeconds;
        }

        var written = 0;
        while (cursor < count && written < due.Length && entries[cursor].Time <= elapsed)
        {
            due[written++] = entries[cursor++];
        }

        return written;
    }

    public void Rewind()
    {
        cursor = 0;
        elapsed = 0f;
    }

    public void Clear()
    {
        count = 0;
        SpentBudget = 0;
        Rewind();
    }

    private int PickLane(ref GameRandom random)
    {
        var first = random.Next(LaneCount);
        var second = random.Next(LaneCount);
        return laneSpent[second] < laneSpent[first] ? second : first;
    }

    private void SortByTime()
    {
        for (var entryIndex = 1; entryIndex < count; entryIndex++)
        {
            var entry = entries[entryIndex];
            var slot = entryIndex - 1;
            while (slot >= 0 && entries[slot].Time > entry.Time)
            {
                entries[slot + 1] = entries[slot];
                slot--;
            }

            entries[slot + 1] = entry;
        }
    }
}
