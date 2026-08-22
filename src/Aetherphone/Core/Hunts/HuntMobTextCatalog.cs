using System.Text.Json.Serialization;

namespace Aetherphone.Core.Hunts;

internal sealed class HuntMobTextCatalog
{
    private readonly HuntJsonCatalogLoader<Dictionary<string, Dictionary<string, string>>> loader;
    private Dictionary<string, Dictionary<string, string>> byLocale = new();

    public HuntMobTextCatalog(FileInfo source)
    {
        loader = new HuntJsonCatalogLoader<Dictionary<string, Dictionary<string, string>>>(source,
            HuntMobTextCatalogJsonContext.Default.DictionaryStringDictionaryStringString, "mob text catalog",
            parsed => byLocale = parsed);
        loader.Preload();
    }

    public string? TextFor(string mobId, string localeCode)
    {
        loader.EnsureLoaded();
        if (byLocale.TryGetValue(localeCode, out var locale) && locale.TryGetValue(mobId, out var text))
        {
            return text;
        }

        if (localeCode != "en" && byLocale.TryGetValue("en", out var english) && english.TryGetValue(mobId, out var fallback))
        {
            return fallback;
        }

        return null;
    }

    public bool HasNativeText(string mobId, string localeCode)
    {
        loader.EnsureLoaded();
        return byLocale.TryGetValue(localeCode, out var locale) && locale.ContainsKey(mobId);
    }
}

[JsonSerializable(typeof(Dictionary<string, Dictionary<string, string>>))]
internal partial class HuntMobTextCatalogJsonContext : JsonSerializerContext
{
}
