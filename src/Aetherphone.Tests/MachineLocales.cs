using Aetherphone.Core.Localization;

namespace Aetherphone.Tests;

internal static class MachineLocales
{
    public const float DefaultDeckRowWidth = 393f - 16f * 2f - 12f * 2f;

    private static readonly Lazy<(string Code, StringCatalog Catalog)[]> Catalogs = new(Load);

    public static (string Code, StringCatalog Catalog)[] All => Catalogs.Value;

    public static string Text(StringCatalog catalog, LocString entry, string first = "", string second = "")
    {
        var text = catalog.TryGet(entry.Key, out var found) ? found : entry.Source;
        return text.Replace("{0}", first, StringComparison.Ordinal).Replace("{1}", second, StringComparison.Ordinal);
    }

    private static (string Code, StringCatalog Catalog)[] Load()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "Aetherphone", "Localization");
            if (Directory.Exists(candidate))
            {
                var result = new (string, StringCatalog)[Languages.All.Length];
                for (var index = 0; index < Languages.All.Length; index++)
                {
                    var code = Languages.All[index].Code;
                    result[index] = (code, StringCatalog.Load(Path.Combine(candidate, code + ".json")));
                }

                return result;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("src/Aetherphone/Localization");
    }
}
