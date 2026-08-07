using Dalamud.Game.Config;

namespace Aetherphone.Core.Photos;

internal sealed class ScreenshotImportService : IDisposable
{
    private const int SettleAttempts = 20;
    private const int SettleDelayMilliseconds = 300;
    private const int SeenCapacity = 512;

    private readonly PhotoLibrary library;
    private readonly Configuration configuration;
    private readonly List<FileSystemWatcher> watchers = new();
    private readonly HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
    private volatile bool disposed;

    public ScreenshotImportService(PhotoLibrary library, Configuration configuration)
    {
        this.library = library;
        this.configuration = configuration;
        var directories = ResolveWatchDirectories();
        for (var index = 0; index < directories.Count; index++)
        {
            var (directory, recursive) = directories[index];
            try
            {
                if (!Directory.Exists(directory))
                {
                    continue;
                }

                var watcher = new FileSystemWatcher(directory) { IncludeSubdirectories = recursive };
                watcher.Created += (_, args) => HandleFile(args.FullPath);
                watcher.Renamed += (_, args) => HandleFile(args.FullPath);
                watcher.EnableRaisingEvents = true;
                watchers.Add(watcher);
            }
            catch (Exception exception)
            {
                AepLog.Warning($"[Screenshots] could not watch '{directory}': {exception.Message}");
            }
        }
    }

    public int WatchedFolderCount => watchers.Count;

    private void HandleFile(string path)
    {
        if (disposed || !configuration.ImportScreenshots || !PhotoLibrary.IsSupported(path))
        {
            return;
        }

        lock (seen)
        {
            if (seen.Count > SeenCapacity)
            {
                seen.Clear();
            }

            if (!seen.Add(path))
            {
                return;
            }
        }

        _ = ImportWhenReadyAsync(path);
    }

    private async Task ImportWhenReadyAsync(string path)
    {
        try
        {
            for (var attempt = 0; attempt < SettleAttempts && !disposed; attempt++)
            {
                await Task.Delay(SettleDelayMilliseconds).ConfigureAwait(false);
                if (!IsReadable(path))
                {
                    continue;
                }

                if (!configuration.ImportScreenshots)
                {
                    return;
                }

                library.Import(path, File.GetLastWriteTime(path));
                return;
            }

            AepLog.Warning($"[Screenshots] gave up waiting for '{Path.GetFileName(path)}' to finish writing");
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[Screenshots] import failed for '{Path.GetFileName(path)}': {exception.Message}");
        }
    }

    private static bool IsReadable(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
            return stream.Length > 0;
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

    private static List<(string Directory, bool Recursive)> ResolveWatchDirectories()
    {
        var directories = new List<(string, bool)>();
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Add(GameScreenshotDirectory(), false);
        try
        {
            var gameDirectory = Path.GetDirectoryName(Environment.ProcessPath);
            if (gameDirectory is { Length: > 0 })
            {
                Add(OverlaySavePath(Path.Combine(gameDirectory, "ReShade.ini"), gameDirectory), true);
                Add(OverlaySavePath(Path.Combine(gameDirectory, "GShade.ini"), gameDirectory), true);
            }
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[Screenshots] could not probe overlay folders: {exception.Message}");
        }

        return directories;

        void Add(string? directory, bool recursive)
        {
            if (directory is not { Length: > 0 })
            {
                return;
            }

            var normalized = directory.TrimEnd('\\', '/');
            if (known.Add(normalized))
            {
                directories.Add((normalized, recursive));
            }
        }
    }

    private static string GameScreenshotDirectory()
    {
        try
        {
            if (Plugin.GameConfig.TryGet(SystemConfigOption.ScreenShotDir, out string configured) &&
                configured.Length > 0)
            {
                return configured;
            }
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[Screenshots] could not read the game screenshot folder: {exception.Message}");
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games",
            "FINAL FANTASY XIV - A Realm Reborn", "screenshots");
    }

    private static string? OverlaySavePath(string iniPath, string baseDirectory)
    {
        try
        {
            if (!File.Exists(iniPath))
            {
                return null;
            }

            string? section = null;
            string? legacy = null;
            foreach (var raw in File.ReadLines(iniPath))
            {
                var line = raw.Trim();
                if (line.StartsWith('['))
                {
                    section = line;
                    continue;
                }

                var separator = line.IndexOf('=');
                if (separator <= 0)
                {
                    continue;
                }

                var key = line[..separator].Trim();
                var value = line[(separator + 1)..].Trim();
                if (value.Length == 0)
                {
                    continue;
                }

                if (key.Equals("SavePath", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(section, "[SCREENSHOT]", StringComparison.OrdinalIgnoreCase))
                {
                    return Resolve(value, baseDirectory);
                }

                if (key.Equals("ScreenshotPath", StringComparison.OrdinalIgnoreCase))
                {
                    legacy = value;
                }
            }

            return legacy is { Length: > 0 } ? Resolve(legacy, baseDirectory) : null;
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[Screenshots] could not read '{Path.GetFileName(iniPath)}': {exception.Message}");
            return null;
        }
    }

    private static string? Resolve(string value, string baseDirectory)
    {
        var trimmed = value.Trim('"');
        if (trimmed.Length == 0)
        {
            return null;
        }

        var combined = Path.IsPathRooted(trimmed) ? trimmed : Path.Combine(baseDirectory, trimmed);
        return Path.GetFullPath(combined);
    }

    public void Dispose()
    {
        disposed = true;
        for (var index = 0; index < watchers.Count; index++)
        {
            watchers[index].Dispose();
        }

        watchers.Clear();
    }
}
