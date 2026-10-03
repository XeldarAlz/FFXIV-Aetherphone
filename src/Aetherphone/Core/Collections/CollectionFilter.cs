namespace Aetherphone.Core.Collections;

internal enum OwnershipFilter : byte
{
    All,
    Owned,
    Missing,
}

internal enum CollectionSort : byte
{
    Default,
    Newest,
    Rarest,
    MostCollected,
}

internal static class CollectionFilter
{
    public const int SortCount = 4;

    private static readonly Comparison<CollectionItem> ByOrder = CompareOrder;
    private static readonly Comparison<CollectionItem> ByNewest = CompareNewest;
    private static readonly Comparison<CollectionItem> ByRarest = CompareRarest;
    private static readonly Comparison<CollectionItem> ByMostCollected = CompareMostCollected;

    public static string Normalize(string search) => search.Trim().ToLowerInvariant();

    public static void Apply(CollectionItem[] items, List<CollectionItem> output, string normalizedQuery,
        OwnershipFilter ownership, string sourceType, HashSet<int>? owned, CollectionSort sort)
    {
        output.Clear();
        var hasQuery = normalizedQuery.Length > 0;
        var hasSource = sourceType.Length > 0;
        var filterOwnership = owned is not null && ownership != OwnershipFilter.All;

        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];

            if (filterOwnership)
            {
                var isOwned = owned!.Contains(item.Id);
                if (ownership == OwnershipFilter.Owned && !isOwned)
                {
                    continue;
                }

                if (ownership == OwnershipFilter.Missing && isOwned)
                {
                    continue;
                }
            }

            if (hasSource && !string.Equals(item.SourceType, sourceType, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (hasQuery && !item.SearchLower.Contains(normalizedQuery, StringComparison.Ordinal))
            {
                continue;
            }

            output.Add(item);
        }

        output.Sort(sort switch
        {
            CollectionSort.Newest => ByNewest,
            CollectionSort.Rarest => ByRarest,
            CollectionSort.MostCollected => ByMostCollected,
            _ => ByOrder,
        });
    }

    public static void CollectSourceTypes(CollectionItem[] items, SortedSet<string> into)
    {
        into.Clear();

        for (var index = 0; index < items.Length; index++)
        {
            var type = items[index].SourceType;
            if (type.Length > 0)
            {
                into.Add(type);
            }
        }
    }

    private static int CompareOrder(CollectionItem left, CollectionItem right) => left.Order.CompareTo(right.Order);

    private static int CompareNewest(CollectionItem left, CollectionItem right)
    {
        var byPatch = right.PatchOrder.CompareTo(left.PatchOrder);
        return byPatch != 0 ? byPatch : CompareOrder(left, right);
    }

    private static int CompareRarest(CollectionItem left, CollectionItem right)
    {
        var leftKnown = left.Rarity >= 0f;
        var rightKnown = right.Rarity >= 0f;
        if (leftKnown != rightKnown)
        {
            return leftKnown ? -1 : 1;
        }

        var byRarity = left.Rarity.CompareTo(right.Rarity);
        return byRarity != 0 ? byRarity : CompareOrder(left, right);
    }

    private static int CompareMostCollected(CollectionItem left, CollectionItem right)
    {
        var byRarity = right.Rarity.CompareTo(left.Rarity);
        return byRarity != 0 ? byRarity : CompareOrder(left, right);
    }
}
