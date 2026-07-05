namespace Aetherphone.Core.Notifications;

internal sealed class SoundLibrary
{
    private static readonly string[] FilePatterns = { "*.mp3", "*.wav" };
    private readonly DirectoryInfo bundledDirectory;
    private readonly DirectoryInfo userDirectory;
    private IReadOnlyList<SoundOption> options = Array.Empty<SoundOption>();

    public SoundLibrary(DirectoryInfo bundledDirectory, DirectoryInfo userDirectory)
    {
        this.bundledDirectory = bundledDirectory;
        this.userDirectory = userDirectory;
        userDirectory.Create();
        Refresh();
    }

    public IReadOnlyList<SoundOption> Options => options;

    public void Refresh()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var built = new List<SoundOption>();
        var games = RingtoneCatalog.Options;
        for (var index = 0; index < games.Count; index++)
        {
            var game = games[index];
            if (game.SoundId == 0)
            {
                continue;
            }

            built.Add(new SoundOption(SoundTokens.Game(game.SoundId), SoundSource.Game));
        }

        AddFiles(bundledDirectory, SoundSource.Bundled, seen, built);
        AddFiles(userDirectory, SoundSource.User, seen, built);
        built.Add(new SoundOption(SoundTokens.Silent, SoundSource.Game));
        options = built;
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

    private static void AddFiles(DirectoryInfo directory, SoundSource source, HashSet<string> seen,
        List<SoundOption> built)
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
            if (!seen.Add(fileName))
            {
                continue;
            }

            built.Add(new SoundOption(SoundTokens.File(fileName), source));
        }
    }
}
