namespace Aetherphone.Core.Collections;

internal static class CollectionCategories
{
    public static readonly CollectionCategory[] All =
    {
        CollectionCategory.Mounts,
        CollectionCategory.Minions,
        CollectionCategory.Emotes,
        CollectionCategory.Orchestrions,
        CollectionCategory.Hairstyles,
        CollectionCategory.Facewear,
        CollectionCategory.Achievements,
        CollectionCategory.TriadCards,
    };

    public static string CatalogPath(CollectionCategory category) => category switch
    {
        CollectionCategory.Mounts => "mounts",
        CollectionCategory.Minions => "minions",
        CollectionCategory.Emotes => "emotes",
        CollectionCategory.Orchestrions => "orchestrions",
        CollectionCategory.Hairstyles => "hairstyles",
        CollectionCategory.Facewear => "facewear",
        CollectionCategory.Achievements => "achievements",
        _ => "triad/cards",
    };

    public static string OwnedPath(CollectionCategory category) => category switch
    {
        CollectionCategory.Mounts => "mounts",
        CollectionCategory.Minions => "minions",
        CollectionCategory.Emotes => "emotes",
        CollectionCategory.Orchestrions => "orchestrions",
        CollectionCategory.Hairstyles => "hairstyles",
        CollectionCategory.Facewear => "facewear",
        CollectionCategory.Achievements => "achievements",
        _ => "cards",
    };
}
