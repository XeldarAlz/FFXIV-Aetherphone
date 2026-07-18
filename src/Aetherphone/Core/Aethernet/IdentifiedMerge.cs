using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Aethernet;

internal static class IdentifiedMerge
{
    public static T[] MergeById<T>(T[] existing, T[] incoming, Comparison<T> order) where T : class, IIdentified
    {
        if (existing.Length == 0)
        {
            return incoming;
        }

        if (incoming.Length == 0)
        {
            return existing;
        }

        var incomingIds = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < incoming.Length; index++)
        {
            incomingIds.Add(incoming[index].Id);
        }

        var merged = new List<T>(existing.Length + incoming.Length);
        for (var index = 0; index < existing.Length; index++)
        {
            if (!incomingIds.Contains(existing[index].Id))
            {
                merged.Add(existing[index]);
            }
        }

        for (var index = 0; index < incoming.Length; index++)
        {
            merged.Add(incoming[index]);
        }

        merged.Sort(order);
        return merged.ToArray();
    }

    public static T[] ReconcileNewestPage<T>(T[] existing, T[] incoming, Comparison<T> order)
        where T : class, IIdentified
    {
        if (existing.Length == 0)
        {
            return incoming;
        }

        if (incoming.Length == 0)
        {
            return existing;
        }

        var oldestIncoming = incoming[0];
        for (var index = 1; index < incoming.Length; index++)
        {
            if (order(incoming[index], oldestIncoming) > 0)
            {
                oldestIncoming = incoming[index];
            }
        }

        var incomingIds = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < incoming.Length; index++)
        {
            incomingIds.Add(incoming[index].Id);
        }

        var merged = new List<T>(existing.Length + incoming.Length);
        for (var index = 0; index < existing.Length; index++)
        {
            var candidate = existing[index];
            if (incomingIds.Contains(candidate.Id))
            {
                continue;
            }

            if (order(oldestIncoming, candidate) < 0)
            {
                merged.Add(candidate);
            }
        }

        for (var index = 0; index < incoming.Length; index++)
        {
            merged.Add(incoming[index]);
        }

        merged.Sort(order);
        return merged.ToArray();
    }
}
