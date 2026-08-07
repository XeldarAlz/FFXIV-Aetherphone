namespace Aetherphone.Core.Notifications;

internal sealed class SoundLibrary
{
    public const string BundledRingtoneToken = SoundTokens.FilePrefix + "Ringtone_1.mp3";
    public const string BundledNotificationToken = SoundTokens.FilePrefix + "Notification_1.mp3";

    private static readonly string[] FilePatterns = { "*.mp3", "*.wav" };
    private readonly DirectoryInfo bundledDirectory;
    private readonly DirectoryInfo userDirectory;
    private IReadOnlyList<string> options = Array.Empty<string>();
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
        AddFiles(bundledDirectory, seen, built);
        var bundledCount = built.Count;
        AddFiles(userDirectory, seen, built);
        defaultToken = bundledCount > 0 ? built[0] : SoundTokens.Silent;
        built.Add(SoundTokens.Silent);
        options = built;
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

    private static void AddFiles(DirectoryInfo directory, HashSet<string> seen, List<string> built)
    {
        if (!directory.Exists)
        {
            return;
        }

        var files = new List<string>();
        for (var index = 0; index < FilePatterns.Length; index++)
        {
            files.AddRange(Directory.GetFiles(directory.FullName, FilePatterns[index]));
        }

        files.Sort(static (left, right) => string.CompareOrdinal(Path.GetFileName(left), Path.GetFileName(right)));
        for (var index = 0; index < files.Count; index++)
        {
            var fileName = Path.GetFileName(files[index]);
            if (seen.Add(fileName))
            {
                built.Add(SoundTokens.File(fileName));
            }
        }
    }
}
