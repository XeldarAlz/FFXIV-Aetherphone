namespace Aetherphone.Core.Notifications;

internal sealed class SoundLibrary
{
    public const string BundledRingtoneToken = SoundTokens.FilePrefix + "Signal.mp3";
    public const string BundledNotificationToken = SoundTokens.FilePrefix + "Chime.mp3";

    private static readonly string[] FilePatterns = { "*.mp3", "*.wav" };
    private readonly DirectoryInfo bundledDirectory;
    private readonly DirectoryInfo userDirectory;
    private IReadOnlyList<string> options = Array.Empty<string>();
    private HashSet<string> userTokens = new(StringComparer.OrdinalIgnoreCase);
    private string defaultToken = SoundTokens.Silent;

    public SoundLibrary(DirectoryInfo bundledDirectory, DirectoryInfo userDirectory)
    {
        this.bundledDirectory = bundledDirectory;
        this.userDirectory = userDirectory;
        userDirectory.Create();
        Refresh();
    }

    public IReadOnlyList<string> Options => options;

    public string DefaultToken => defaultToken;

    public void Refresh()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var built = new List<string>();
        var imported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddFiles(ListFiles(bundledDirectory), seen, built, null);
        var bundledCount = built.Count;
        AddFiles(ListFiles(userDirectory), seen, built, imported);
        defaultToken = bundledCount > 0 ? built[0] : SoundTokens.Silent;
        built.Add(SoundTokens.Silent);
        options = built;
        userTokens = imported;
    }

    public bool IsUserFile(string token) => userTokens.Contains(token);

    public bool TryUserPath(string token, out string path)
    {
        if (SoundTokens.TryFile(token, out var fileName))
        {
            var safe = Path.GetFileName(fileName);
            if (!string.IsNullOrEmpty(safe))
            {
                path = Path.Combine(userDirectory.FullName, safe);
                return true;
            }
        }

        path = string.Empty;
        return false;
    }

    public static bool TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public string Resolve(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return defaultToken;
        }

        if (SoundTokens.IsSilent(token))
        {
            return token;
        }

        return SoundTokens.TryFile(token, out var fileName) && TryResolvePath(fileName, out _)
            ? token
            : defaultToken;
    }

    public bool TryResolvePath(string fileName, out string path)
    {
        var safe = Path.GetFileName(fileName);
        if (!string.IsNullOrEmpty(safe))
        {
            var userPath = Path.Combine(userDirectory.FullName, safe);
            if (File.Exists(userPath))
            {
                path = userPath;
                return true;
            }

            var bundledPath = Path.Combine(bundledDirectory.FullName, safe);
            if (File.Exists(bundledPath))
            {
                path = bundledPath;
                return true;
            }
        }

        path = string.Empty;
        return false;
    }

    public string AddUserFile(string sourcePath)
    {
        var fileName = Path.GetFileName(sourcePath);
        var destination = Path.Combine(userDirectory.FullName, fileName);
        File.Copy(sourcePath, destination, true);
        Refresh();
        return SoundTokens.File(fileName);
    }

    public static string PrettyFileName(string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        return string.IsNullOrEmpty(stem) ? fileName : stem.Replace('_', ' ').Replace('-', ' ').Trim();
    }

    private static List<string> ListFiles(DirectoryInfo directory)
    {
        var files = new List<string>();
        directory.Refresh();
        if (!directory.Exists)
        {
            return files;
        }

        for (var index = 0; index < FilePatterns.Length; index++)
        {
            files.AddRange(Directory.GetFiles(directory.FullName, FilePatterns[index]));
        }

        files.Sort(static (left, right) => string.CompareOrdinal(Path.GetFileName(left), Path.GetFileName(right)));
        return files;
    }

    private static void AddFiles(List<string> files, HashSet<string> seen, List<string> built,
        HashSet<string>? imported)
    {
        for (var index = 0; index < files.Count; index++)
        {
            var fileName = Path.GetFileName(files[index]);
            if (!seen.Add(fileName))
            {
                continue;
            }

            var token = SoundTokens.File(fileName);
            built.Add(token);
            imported?.Add(token);
        }
    }
}
