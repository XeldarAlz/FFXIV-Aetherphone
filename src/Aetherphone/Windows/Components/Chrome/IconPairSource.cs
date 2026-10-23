namespace Aetherphone.Windows.Components;

internal sealed class IconPairSource
{
    public const string ForegroundSuffix = ".fg.png";
    public const string FinishedSuffix = ".png";

    private readonly Dictionary<string, string> overrides = new(StringComparer.Ordinal);

    public IconPairSource(string bundledDirectory)
    {
        BundledDirectory = bundledDirectory;
    }

    public string BundledDirectory { get; }

    public bool HasOverride(string appId) => overrides.ContainsKey(appId);

    public string DirectoryFor(string appId) =>
        overrides.TryGetValue(appId, out var directory) ? directory : BundledDirectory;

    public bool TryUseOverride(string appId, string directory)
    {
        if (!PairExists(directory, appId))
        {
            return false;
        }

        overrides[appId] = directory;
        return true;
    }

    public bool ClearOverride(string appId) => overrides.Remove(appId);

    public static string FinishedPath(string directory, string key) => Path.Combine(directory, key + FinishedSuffix);

    public static string ForegroundPath(string directory, string key) =>
        Path.Combine(directory, key + ForegroundSuffix);

    public static bool PairExists(string directory, string key) =>
        File.Exists(ForegroundPath(directory, key)) && File.Exists(FinishedPath(directory, key));
}
