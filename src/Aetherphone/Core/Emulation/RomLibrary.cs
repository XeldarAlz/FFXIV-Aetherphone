namespace Aetherphone.Core.Emulation;

internal sealed class RomLibrary
{
    private readonly string romDirectory;

    public RomLibrary(string emulatorRoot)
    {
        romDirectory = Path.Combine(emulatorRoot, "roms");
        Directory.CreateDirectory(romDirectory);
    }

    public IReadOnlyList<RomEntry> Scan(IEnumerable<string>? additionalDirectories = null)
    {
        var found = new Dictionary<string, RomEntry>(StringComparer.OrdinalIgnoreCase);
        AddDirectory(romDirectory, recursive: true, found, null);
        if (additionalDirectories is not null)
        {
            foreach (var directory in additionalDirectories)
            {
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    AddDirectory(directory, recursive: true, found, null);
                }
            }
        }

        var sorted = found.Values.ToList();
        sorted.Sort(static (left, right) => string.Compare(Path.GetFileName(left.Path), Path.GetFileName(right.Path),
            StringComparison.CurrentCultureIgnoreCase));
        return sorted;
    }

    public IReadOnlyList<RomEntry> Scan(EmulatorSystemDefinition system,
        IEnumerable<string>? additionalDirectories = null, IEnumerable<string>? importedFiles = null)
    {
        var found = new Dictionary<string, RomEntry>(StringComparer.OrdinalIgnoreCase);
        AddDirectory(Path.Combine(romDirectory, system.Id), recursive: false, found, system);
        AddDirectory(romDirectory, recursive: false, found, system);
        if (additionalDirectories is not null)
        {
            foreach (var directory in additionalDirectories)
            {
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    AddDirectory(directory, recursive: true, found, system);
                }
            }
        }

        if (importedFiles is not null)
        {
            foreach (var path in importedFiles)
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path) && system.Supports(path))
                {
                    var fullPath = Path.GetFullPath(path);
                    found[fullPath] = new RomEntry(fullPath, system);
                }
            }
        }

        var sorted = found.Values.ToList();
        sorted.Sort(static (left, right) => string.Compare(Path.GetFileName(left.Path), Path.GetFileName(right.Path),
            StringComparison.CurrentCultureIgnoreCase));
        return sorted;
    }

    public RomEntry Import(string sourcePath)
    {
        var system = EmulatorSystemCatalog.Resolve(sourcePath);
        if (system is null)
        {
            throw new InvalidOperationException($"Unsupported or ambiguous ROM. Supported formats: " +
                                                EmulatorSystemCatalog.SupportedExtensionsText);
        }

        var fileName = Path.GetFileName(sourcePath);
        var destination = Path.Combine(romDirectory, fileName);
        if (!string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(destination),
                StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(sourcePath, destination, true);
        }

        return new RomEntry(destination, system);
    }

    public RomEntry Import(string sourcePath, EmulatorSystemDefinition system)
    {
        if (!File.Exists(sourcePath) || !system.Supports(sourcePath))
        {
            throw new InvalidOperationException($"This file is not supported by {system.Name}. Supported formats: " +
                                                string.Join(", ", system.Extensions));
        }

        // Descriptor/playlist files must remain beside their tracks. Keep those paths external instead of
        // copying only the descriptor and silently breaking the game.
        var extension = Path.GetExtension(sourcePath);
        if (extension.Equals(".cue", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".m3u", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".toc", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".ccd", StringComparison.OrdinalIgnoreCase))
        {
            return new RomEntry(Path.GetFullPath(sourcePath), system);
        }

        var systemDirectory = Path.Combine(romDirectory, system.Id);
        Directory.CreateDirectory(systemDirectory);
        var destination = Path.Combine(systemDirectory, Path.GetFileName(sourcePath));
        if (!string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(destination),
                StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(sourcePath, destination, true);
        }

        return new RomEntry(destination, system);
    }

    private static void AddDirectory(string directory, bool recursive, Dictionary<string, RomEntry> found,
        EmulatorSystemDefinition? forcedSystem)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        try
        {
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = recursive,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint,
            };
            foreach (var path in Directory.EnumerateFiles(directory, "*", options))
            {
                var fullPath = Path.GetFullPath(path);
                var system = forcedSystem is null
                    ? EmulatorSystemCatalog.Resolve(fullPath)
                    : forcedSystem.Supports(fullPath) ? forcedSystem : null;
                if (system is not null)
                {
                    found[fullPath] = new RomEntry(fullPath, system);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            AepLog.Warning($"[Emulator] could not scan ROM folder '{directory}': {exception.Message}");
        }
    }
}
