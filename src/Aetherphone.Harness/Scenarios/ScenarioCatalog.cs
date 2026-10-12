namespace Aetherphone.Harness.Scenarios;

internal static class ScenarioCatalog
{
    public const string None = "none";
    public const string Default = "endgame";
    private const string Folder = "scenarios";
    private const string Extension = ".json";

    public static string? Resolve(string nameOrPath, string cacheDirectory)
    {
        if (File.Exists(nameOrPath))
        {
            return Path.GetFullPath(nameOrPath);
        }

        var directories = Directories(cacheDirectory);
        for (var index = 0; index < directories.Length; index++)
        {
            var candidate = Path.Combine(directories[index], nameOrPath + Extension);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    public static List<string> Names(string cacheDirectory)
    {
        var names = new List<string>();
        var directories = Directories(cacheDirectory);
        for (var index = 0; index < directories.Length; index++)
        {
            if (!Directory.Exists(directories[index]))
            {
                continue;
            }

            var files = Directory.GetFiles(directories[index], "*" + Extension);
            for (var fileIndex = 0; fileIndex < files.Length; fileIndex++)
            {
                var name = Path.GetFileNameWithoutExtension(files[fileIndex]);
                if (!names.Contains(name))
                {
                    names.Add(name);
                }
            }
        }

        names.Sort(StringComparer.Ordinal);
        return names;
    }

    private static string[] Directories(string cacheDirectory) =>
    [
        Path.Combine(cacheDirectory, Folder),
        Path.Combine(AppContext.BaseDirectory, Folder),
    ];
}
