namespace Aetherphone.Core.Inventory;

internal static class InventorySourceMerge
{
    public static StoredSource Merge(StoredSource? existing, StoredSource incoming)
    {
        if (existing is null || existing.LoadedPages == 0 || incoming.LoadedPages == 0)
        {
            return incoming;
        }

        var keptPages = existing.LoadedPages & ~incoming.LoadedPages;
        if (keptPages == 0)
        {
            return incoming;
        }

        var kept = 0;
        for (var index = 0; index < existing.Stacks.Length; index++)
        {
            if (HasPage(keptPages, existing.Stacks[index].Page))
            {
                kept++;
            }
        }

        var stacks = new StoredStack[incoming.Stacks.Length + kept];
        var cursor = 0;
        for (var index = 0; index < incoming.Stacks.Length; index++)
        {
            stacks[cursor++] = incoming.Stacks[index];
        }

        for (var index = 0; index < existing.Stacks.Length; index++)
        {
            if (HasPage(keptPages, existing.Stacks[index].Page))
            {
                stacks[cursor++] = existing.Stacks[index];
            }
        }

        Array.Sort(stacks, static (left, right) =>
            left.Page != right.Page ? left.Page.CompareTo(right.Page) : left.Slot.CompareTo(right.Slot));
        var mergedPages = existing.LoadedPages | incoming.LoadedPages;
        return new StoredSource
        {
            Kind = incoming.Kind,
            OwnerName = incoming.OwnerName,
            OwnerId = incoming.OwnerId,
            CapturedUnix = incoming.CapturedUnix,
            Capacity = MergedCapacity(incoming, existing, mergedPages),
            LoadedPages = mergedPages,
            Stacks = stacks,
        };
    }

    public static bool SameContent(StoredSource left, StoredSource right)
    {
        if (left.LoadedPages != right.LoadedPages || left.Capacity != right.Capacity ||
            left.Stacks.Length != right.Stacks.Length ||
            !string.Equals(left.OwnerName, right.OwnerName, StringComparison.Ordinal))
        {
            return false;
        }

        for (var index = 0; index < left.Stacks.Length; index++)
        {
            var leftStack = left.Stacks[index];
            var rightStack = right.Stacks[index];
            if (leftStack.ItemId != rightStack.ItemId || leftStack.Quantity != rightStack.Quantity ||
                leftStack.HighQuality != rightStack.HighQuality || leftStack.Slot != rightStack.Slot ||
                leftStack.Page != rightStack.Page)
            {
                return false;
            }
        }

        return true;
    }

    public static int SlotPages(InventorySourceKind kind, int pages)
    {
        var count = 0;
        for (var page = 0; page < 16; page++)
        {
            if (HasPage(pages, page) && !InventoryPages.IsCrystalPage(kind, page))
            {
                count++;
            }
        }

        return count;
    }

    private static int MergedCapacity(StoredSource incoming, StoredSource existing, int mergedPages)
    {
        var incomingPages = SlotPages(incoming.Kind, incoming.LoadedPages);
        if (incomingPages == 0)
        {
            return existing.Capacity;
        }

        var perPage = incoming.Capacity / incomingPages;
        return perPage * SlotPages(incoming.Kind, mergedPages);
    }

    private static bool HasPage(int pages, int page) => page is >= 0 and < 16 && (pages & (1 << page)) != 0;
}
