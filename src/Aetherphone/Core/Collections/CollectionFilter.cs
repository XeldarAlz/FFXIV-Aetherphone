namespace Aetherphone.Core.Collections;

internal enum OwnershipFilter : byte
{
    All,
    Owned,
    Missing,
}

internal static class CollectionFilter
{
    public static void Apply(CollectionItem[] items, List<CollectionItem> output, string search, OwnershipFilter ownership, string sourceType, OwnedEntry? owned)
    {
        output.Clear();
        var hasOwned = owned is { State: OwnedState.Ready };
        var query = search.Trim().ToLowerInvariant();
        var hasQuery = query.Length > 0;
        var hasSource = sourceType.Length > 0;

        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];

            if (hasOwned && ownership != OwnershipFilter.All)
            {
                var isOwned = owned!.Ids.Contains(item.Id);

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

            if (hasQuery && !item.SearchLower.Contains(query))
            {
                continue;
            }

            output.Add(item);
        }
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
}
