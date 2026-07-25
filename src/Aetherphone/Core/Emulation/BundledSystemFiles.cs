namespace Aetherphone.Core.Emulation;

/// <summary>
/// Installs redistributable system assets shipped beside libretro cores into the
/// per-user emulator system directory. Files are refreshed only when the bundled
/// copy differs, so normal startup does not rewrite the entire asset tree.
/// </summary>
internal static class BundledSystemFiles
{
    public static void Install(string coresDirectory, string emulatorRoot)
    {
        if (string.IsNullOrWhiteSpace(coresDirectory) || string.IsNullOrWhiteSpace(emulatorRoot))
        {
            return;
        }

        var sourceRoot = Path.Combine(coresDirectory, "SystemFiles");
        if (!Directory.Exists(sourceRoot))
        {
            return;
        }

        var destinationRoot = Path.Combine(emulatorRoot, "system");
        try
        {
            Directory.CreateDirectory(destinationRoot);
            CopyDirectory(sourceRoot, destinationRoot);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            AepLog.Warning($"[Emulator] could not install bundled system files: {exception.Message}");
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var sourceFile in Directory.EnumerateFiles(source))
        {
            var destinationFile = Path.Combine(destination, Path.GetFileName(sourceFile));
            CopyIfDifferent(sourceFile, destinationFile);
        }

        foreach (var sourceDirectory in Directory.EnumerateDirectories(source))
        {
            CopyDirectory(sourceDirectory, Path.Combine(destination, Path.GetFileName(sourceDirectory)));
        }
    }

    private static void CopyIfDifferent(string source, string destination)
    {
        try
        {
            if (File.Exists(destination))
            {
                var sourceInfo = new FileInfo(source);
                var destinationInfo = new FileInfo(destination);
                if (sourceInfo.Length == destinationInfo.Length && FilesEqual(source, destination))
                {
                    return;
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? string.Empty);
            File.Copy(source, destination, true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            AepLog.Warning($"[Emulator] could not install bundled system file '{destination}': {exception.Message}");
        }
    }

    private static bool FilesEqual(string leftPath, string rightPath)
    {
        const int BufferSize = 64 * 1024;
        using var left = File.OpenRead(leftPath);
        using var right = File.OpenRead(rightPath);
        if (left.Length != right.Length)
        {
            return false;
        }

        var leftBuffer = new byte[BufferSize];
        var rightBuffer = new byte[BufferSize];
        while (true)
        {
            var leftRead = left.Read(leftBuffer, 0, leftBuffer.Length);
            var rightRead = right.Read(rightBuffer, 0, rightBuffer.Length);
            if (leftRead != rightRead)
            {
                return false;
            }

            if (leftRead == 0)
            {
                return true;
            }

            if (!leftBuffer.AsSpan(0, leftRead).SequenceEqual(rightBuffer.AsSpan(0, rightRead)))
            {
                return false;
            }
        }
    }
}
