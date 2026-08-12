namespace Aetherphone.Apps.Games.Syrcus;

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

    public void OpenFiles(string? initialPath, IReadOnlyList<string> extensions)
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < extensions.Count; index++)
        {
            var extension = extensions[index];
            if (string.IsNullOrWhiteSpace(extension))
            {
                continue;
            }

            allowed.Add(extension.StartsWith('.') ? extension : $".{extension}");
        }

        allowedExtensions = allowed;
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
            path = DefaultStartPath();
        }

        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            OpenDrives();
            return;
        }

        Navigate(path);
    }


    private static string DefaultStartPath()
    {
        const string preferredDrive = @"C:\";
        if (Directory.Exists(preferredDrive))
        {
            return preferredDrive;
        }

        var systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var systemRoot = string.IsNullOrWhiteSpace(systemDirectory)
            ? string.Empty
            : Path.GetPathRoot(systemDirectory);
        if (!string.IsNullOrWhiteSpace(systemRoot) && Directory.Exists(systemRoot))
        {
            return systemRoot;
        }

        var currentRoot = Path.GetPathRoot(Environment.CurrentDirectory);
        return !string.IsNullOrWhiteSpace(currentRoot) && Directory.Exists(currentRoot)
            ? currentRoot
            : string.Empty;
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
            var foundDirectories = new List<string>(Directory.EnumerateDirectories(fullPath, "*", options));
            foundDirectories.Sort(CompareFileNames);
            directories = foundDirectories;
            var foundFiles = new List<string>();
            if (allowedExtensions is not null)
            {
                foreach (var file in Directory.EnumerateFiles(fullPath, "*", options))
                {
                    if (allowedExtensions.Contains(Path.GetExtension(file)))
                    {
                        foundFiles.Add(file);
                    }
                }

                foundFiles.Sort(CompareFileNames);
            }

            files = foundFiles;
            CurrentPath = fullPath;
            Error = string.Empty;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Error = exception.Message;
        }
    }

    private static int CompareFileNames(string left, string right) =>
        string.Compare(Path.GetFileName(left), Path.GetFileName(right), StringComparison.CurrentCultureIgnoreCase);

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
