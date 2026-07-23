namespace Aetherphone.Apps.Games.GameBoy;

internal sealed class DirectoryBrowser
{
    private IReadOnlyList<string> directories = Array.Empty<string>();
    private IReadOnlyList<string> files = Array.Empty<string>();
    private HashSet<string>? allowedExtensions;

    public string CurrentPath { get; private set; } = string.Empty;
    public IReadOnlyList<string> Directories => directories;
    public IReadOnlyList<string> Files => files;
    public string Error { get; private set; } = string.Empty;
    public bool IsDriveList => CurrentPath.Length == 0;
    public bool ShowsFiles => allowedExtensions is not null;

    public void Open(string? initialPath = null)
    {
        allowedExtensions = null;
        OpenLocation(initialPath);
    }

    public void OpenFiles(string? initialPath, IEnumerable<string> extensions)
    {
        allowedExtensions = extensions
            .Where(extension => !string.IsNullOrWhiteSpace(extension))
            .Select(extension => extension.StartsWith('.') ? extension : $".{extension}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        OpenLocation(initialPath);
    }

    private void OpenLocation(string? initialPath)
    {
        var path = initialPath;
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            path = Path.GetDirectoryName(path);
        }

        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            OpenDrives();
            return;
        }

        Navigate(path);
    }

    public void Navigate(string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            var options = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint,
            };
            directories = Directory.EnumerateDirectories(fullPath, "*", options)
                .OrderBy(Path.GetFileName, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
            files = allowedExtensions is null
                ? Array.Empty<string>()
                : Directory.EnumerateFiles(fullPath, "*", options)
                    .Where(path => allowedExtensions.Contains(Path.GetExtension(path)))
                    .OrderBy(Path.GetFileName, StringComparer.CurrentCultureIgnoreCase)
                    .ToArray();
            CurrentPath = fullPath;
            Error = string.Empty;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Error = exception.Message;
        }
    }

    public void Up()
    {
        if (IsDriveList)
        {
            return;
        }

        var parent = Directory.GetParent(CurrentPath);
        if (parent is null)
        {
            OpenDrives();
        }
        else
        {
            Navigate(parent.FullName);
        }
    }

    public void OpenDrives()
    {
        Error = string.Empty;
        var found = new List<string>();
        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady)
                {
                    found.Add(drive.RootDirectory.FullName);
                }
            }
        }
        catch (IOException exception)
        {
            Error = exception.Message;
        }

        found.Sort(StringComparer.CurrentCultureIgnoreCase);
        directories = found;
        files = Array.Empty<string>();
        CurrentPath = string.Empty;
    }
}
