using Aetherphone.Core.Collections;

namespace Aetherphone.Apps.Collections;

internal enum CollectionViewKind : byte
{
    Root,
    Category,
    Detail,
}

internal readonly struct CollectionView
{
    public readonly CollectionViewKind Kind;
    public readonly CollectionCategory Category;
    public readonly CollectionItem? Item;

    private CollectionView(CollectionViewKind kind, CollectionCategory category, CollectionItem? item)
    {
        Kind = kind;
        Category = category;
        Item = item;
    }

    public static CollectionView Root() => new(CollectionViewKind.Root, CollectionCategory.Mounts, null);

    public static CollectionView ForCategory(CollectionCategory category) =>
        new(CollectionViewKind.Category, category, null);

    public static CollectionView ForItem(CollectionCategory category, CollectionItem item) =>
        new(CollectionViewKind.Detail, category, item);
}
