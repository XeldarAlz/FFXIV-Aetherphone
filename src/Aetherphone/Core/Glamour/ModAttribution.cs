using Aetherphone.Core.Aethernet.Contracts;
using Newtonsoft.Json.Linq;

namespace Aetherphone.Core.Glamour;

internal static class ModAttribution
{
    private const string MetaFile = "meta.json";

    public static string ModFolder(string root, string actualPath)
    {
        if (root.Length == 0 || !Path.IsPathRooted(actualPath))
        {
            return string.Empty;
        }

        var trimmedRoot = root.AsSpan().TrimEnd('\\').TrimEnd('/');
        var actual = actualPath.AsSpan();
        if (actual.Length <= trimmedRoot.Length + 1 || !actual.StartsWith(trimmedRoot, StringComparison.OrdinalIgnoreCase) ||
            actual[trimmedRoot.Length] is not ('\\' or '/'))
        {
            return string.Empty;
        }

        var rest = actual[(trimmedRoot.Length + 1)..];
        var end = rest.IndexOfAny('\\', '/');
        return end <= 0 ? string.Empty : rest[..end].ToString();
    }

    public static void Collect(Dictionary<string, HashSet<string>> paths, string root,
        Dictionary<int, HashSet<string>> foldersBySlot)
    {
        foreach (var (actual, gamePaths) in paths)
        {
            var folder = ModFolder(root, actual);
            if (folder.Length == 0)
            {
                continue;
            }

            var placed = false;
            foreach (var gamePath in gamePaths)
            {
                var slot = GearSlots.FromGamePath(gamePath);
                if (slot == GearSlots.None)
                {
                    continue;
                }

                Add(foldersBySlot, slot, folder);
                placed = true;
            }

            if (!placed)
            {
                Add(foldersBySlot, GearSlots.None, folder);
            }
        }
    }

    public static GlamModDto Describe(string root, string folder, string listedName)
    {
        var name = listedName.Length > 0 ? listedName : folder;
        var metaPath = Path.Combine(root, folder, MetaFile);
        if (!File.Exists(metaPath))
        {
            return new GlamModDto(name);
        }

        try
        {
            var meta = JObject.Parse(File.ReadAllText(metaPath));
            return new GlamModDto(
                Text(meta, "Name", name),
                Text(meta, "Author", string.Empty),
                Text(meta, "Version", string.Empty),
                Text(meta, "Website", string.Empty));
        }
        catch (Exception exception)
        {
            AepLog.Warning($"Could not read the Penumbra meta for {folder}: {exception.Message}");
            return new GlamModDto(name);
        }
    }

    public static GlamOptionDto[]? Options(Dictionary<string, List<string>>? settings)
    {
        if (settings is null || settings.Count == 0)
        {
            return null;
        }

        var options = new List<GlamOptionDto>(settings.Count);
        foreach (var (group, picked) in settings)
        {
            if (picked.Count > 0)
            {
                options.Add(new GlamOptionDto(group, picked.ToArray()));
            }
        }

        return options.Count == 0 ? null : options.ToArray();
    }

    public static Dictionary<string, IReadOnlyList<string>> Settings(GlamOptionDto[]? options)
    {
        var settings = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        if (options is null)
        {
            return settings;
        }

        for (var index = 0; index < options.Length; index++)
        {
            var option = options[index];
            if (option.Group.Length > 0 && option.Picked is { Length: > 0 } picked)
            {
                settings[option.Group] = picked;
            }
        }

        return settings;
    }

    public static string MatchFolder(Dictionary<string, string> installed, GlamModDto mod)
    {
        foreach (var (folder, name) in installed)
        {
            if (string.Equals(name, mod.Name, StringComparison.OrdinalIgnoreCase))
            {
                return folder;
            }
        }

        return string.Empty;
    }

    private static void Add(Dictionary<int, HashSet<string>> foldersBySlot, int slot, string folder)
    {
        if (!foldersBySlot.TryGetValue(slot, out var folders))
        {
            folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foldersBySlot[slot] = folders;
        }

        folders.Add(folder);
    }

    private static string Text(JObject meta, string key, string fallback) =>
        meta.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out var token) && token.Type == JTokenType.String &&
        token.ToString() is { Length: > 0 } value
            ? value
            : fallback;
}
