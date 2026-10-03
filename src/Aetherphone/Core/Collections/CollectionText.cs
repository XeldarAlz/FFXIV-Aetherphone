using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Collections;

internal static class CollectionText
{
    public static LocString Label(CollectionCategory category) => category switch
    {
        CollectionCategory.Mounts => L.Collections.Mounts,
        CollectionCategory.Minions => L.Collections.Minions,
        CollectionCategory.Emotes => L.Collections.Emotes,
        CollectionCategory.Orchestrions => L.Collections.Orchestrions,
        CollectionCategory.Hairstyles => L.Collections.Hairstyles,
        CollectionCategory.Facewear => L.Collections.Facewear,
        CollectionCategory.Achievements => L.Collections.Achievements,
        _ => L.Collections.TriadCards,
    };

    public static LocString NewTitle(CollectionCategory category) => category switch
    {
        CollectionCategory.Mounts => L.Collections.NewMount,
        CollectionCategory.Minions => L.Collections.NewMinion,
        CollectionCategory.Emotes => L.Collections.NewEmote,
        CollectionCategory.Orchestrions => L.Collections.NewOrchestrion,
        CollectionCategory.Hairstyles => L.Collections.NewHairstyle,
        CollectionCategory.Facewear => L.Collections.NewFacewear,
        _ => L.Collections.NewTriadCard,
    };

    public static LocString SortLabel(CollectionSort sort) => sort switch
    {
        CollectionSort.Newest => L.Collections.SortNewest,
        CollectionSort.Rarest => L.Collections.SortRarest,
        CollectionSort.MostCollected => L.Collections.SortMostCollected,
        _ => L.Collections.SortDefault,
    };

    public static string Percent(float value) => Loc.T(L.Collections.Percent, CollectionRarity.Display(value));
}
