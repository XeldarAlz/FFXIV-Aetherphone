namespace Aetherphone.Core.Collections;

internal readonly record struct CollectionUnlock(CollectionCategory Category, int Id, long UnlockedUnix);

internal readonly record struct CollectionPin(CollectionCategory Category, int Id, long PinnedUnix);

internal enum UnlockScanOutcome : byte
{
    Baseline,
    Unchanged,
    Gained,
    Rebaseline,
    Skipped,
}

internal sealed class CollectionLedger
{
    public const int MaxRecent = 40;
    public const int MaxPins = 60;
    public const int MaxGainPerScan = 40;
    public const int MinTrustedBaseline = 10;

    public Dictionary<string, List<int>> Owned { get; set; } = new();
    public List<CollectionUnlock> Recent { get; set; } = new();
    public List<CollectionPin> Pins { get; set; } = new();

    public static string KeyOf(CollectionCategory category) => CollectionCategories.OwnedPath(category);

    public UnlockScanOutcome Observe(CollectionCategory category, HashSet<int> current, long nowUnix,
        List<int> gained, bool record)
    {
        gained.Clear();
        var key = KeyOf(category);
        Owned.TryGetValue(key, out var baseline);
        var outcome = Diff(current, baseline, gained);
        if (outcome == UnlockScanOutcome.Skipped)
        {
            return outcome;
        }

        Owned[key] = Sorted(current);
        if (outcome != UnlockScanOutcome.Gained)
        {
            return outcome;
        }

        if (!record)
        {
            gained.Clear();
            return UnlockScanOutcome.Rebaseline;
        }

        Record(category, gained, nowUnix);
        return outcome;
    }

    public static UnlockScanOutcome Diff(HashSet<int> current, List<int>? baseline, List<int> gained)
    {
        gained.Clear();
        if (baseline is null)
        {
            return UnlockScanOutcome.Baseline;
        }

        if (baseline.Count >= MinTrustedBaseline && current.Count * 2 < baseline.Count)
        {
            return UnlockScanOutcome.Skipped;
        }

        var known = new HashSet<int>(baseline);
        foreach (var id in current)
        {
            if (!known.Contains(id))
            {
                gained.Add(id);
            }
        }

        if (gained.Count == 0)
        {
            return UnlockScanOutcome.Unchanged;
        }

        if (gained.Count > MaxGainPerScan)
        {
            gained.Clear();
            return UnlockScanOutcome.Rebaseline;
        }

        gained.Sort();
        return UnlockScanOutcome.Gained;
    }

    public void Record(CollectionCategory category, List<int> ids, long nowUnix)
    {
        for (var index = 0; index < ids.Count; index++)
        {
            RemoveRecent(category, ids[index]);
            Recent.Insert(0, new CollectionUnlock(category, ids[index], nowUnix));
        }

        if (Recent.Count > MaxRecent)
        {
            Recent.RemoveRange(MaxRecent, Recent.Count - MaxRecent);
        }
    }

    public bool IsPinned(CollectionCategory category, int id) => IndexOfPin(category, id) >= 0;

    public bool TogglePin(CollectionCategory category, int id, long nowUnix)
    {
        var index = IndexOfPin(category, id);
        if (index >= 0)
        {
            Pins.RemoveAt(index);
            return false;
        }

        if (Pins.Count >= MaxPins)
        {
            Pins.RemoveAt(Pins.Count - 1);
        }

        Pins.Insert(0, new CollectionPin(category, id, nowUnix));
        return true;
    }

    private int IndexOfPin(CollectionCategory category, int id)
    {
        for (var index = 0; index < Pins.Count; index++)
        {
            if (Pins[index].Category == category && Pins[index].Id == id)
            {
                return index;
            }
        }

        return -1;
    }

    private void RemoveRecent(CollectionCategory category, int id)
    {
        for (var index = Recent.Count - 1; index >= 0; index--)
        {
            if (Recent[index].Category == category && Recent[index].Id == id)
            {
                Recent.RemoveAt(index);
            }
        }
    }

    private static List<int> Sorted(HashSet<int> current)
    {
        var list = new List<int>(current);
        list.Sort();
        return list;
    }
}
