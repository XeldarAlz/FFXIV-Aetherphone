namespace Aetherphone.Core.Collections;

internal static class CollectionSuggestions
{
    private static readonly string[] UnobtainableTypes = { "Premium", "Limited", "Event" };
    private static readonly Comparison<CollectionItem> MostCollectedFirst = CompareMostCollected;

    public static bool IsObtainable(CollectionSource[] sources)
    {
        for (var index = 0; index < sources.Length; index++)
        {
            var type = sources[index].Type;
            if (!string.IsNullOrEmpty(type) && !IsUnobtainable(type))
            {
                return true;
            }
        }

        return false;
    }

    public static void Collect(CollectionItem[] items, HashSet<int> owned, List<CollectionItem> into)
    {
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            if (!item.Obtainable || item.Rarity < 0f || owned.Contains(item.Id))
            {
                continue;
            }

            into.Add(item);
        }
    }

    public static void Rank(List<CollectionItem> candidates, int limit)
    {
        candidates.Sort(MostCollectedFirst);
        if (candidates.Count > limit)
        {
            candidates.RemoveRange(limit, candidates.Count - limit);
        }
    }

    private static bool IsUnobtainable(string type)
    {
        for (var index = 0; index < UnobtainableTypes.Length; index++)
        {
            if (string.Equals(type, UnobtainableTypes[index], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static int CompareMostCollected(CollectionItem left, CollectionItem right)
    {
        var byRarity = right.Rarity.CompareTo(left.Rarity);
        if (byRarity != 0)
        {
            return byRarity;
        }

        var byCategory = left.Category.CompareTo(right.Category);
        return byCategory != 0 ? byCategory : left.Id.CompareTo(right.Id);
    }
}
