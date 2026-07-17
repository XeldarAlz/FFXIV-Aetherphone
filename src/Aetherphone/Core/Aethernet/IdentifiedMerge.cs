using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Message;

internal static class MessageMerge
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
}
