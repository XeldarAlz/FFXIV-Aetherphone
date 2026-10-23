using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Glamour;

internal sealed class ModMetaCache
{
    private readonly Dictionary<string, (string Root, string ListedName, GlamModDto Mod)> described =
        new(StringComparer.OrdinalIgnoreCase);

    public GlamModDto Describe(string root, string folder, string listedName)
    {
        if (described.TryGetValue(folder, out var cached) && string.Equals(cached.Root, root, StringComparison.Ordinal) &&
            string.Equals(cached.ListedName, listedName, StringComparison.Ordinal))
        {
            return cached.Mod;
        }

        var mod = ModAttribution.Describe(root, folder, listedName);
        described[folder] = (root, listedName, mod);
        return mod;
    }

    public void Clear() => described.Clear();
}
