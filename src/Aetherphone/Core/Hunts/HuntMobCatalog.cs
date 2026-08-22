namespace Aetherphone.Core.Hunts;

internal sealed class HuntMobCatalog
{
    private readonly HuntJsonCatalogLoader<Dictionary<string, HuntMobDefinition>> loader;
    private Dictionary<string, HuntMobDefinition> byId = new();

    public HuntMobCatalog(FileInfo source)
    {
        loader = new HuntJsonCatalogLoader<Dictionary<string, HuntMobDefinition>>(source,
            HuntMobCatalogJsonContext.Default.DictionaryStringHuntMobDefinition, "mob catalog",
            parsed => byId = parsed);
        loader.Preload();
    }

    public IReadOnlyDictionary<string, HuntMobDefinition> ById
    {
        get
        {
            loader.EnsureLoaded();
            return byId;
        }
    }

    public HuntMobDefinition? Find(string mobId)
    {
        loader.EnsureLoaded();
        return byId.GetValueOrDefault(mobId);
    }
}
