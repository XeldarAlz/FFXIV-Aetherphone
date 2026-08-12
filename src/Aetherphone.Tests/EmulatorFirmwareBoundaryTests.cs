using System.Text.RegularExpressions;
using Aetherphone.Core.Emulation;
using Xunit;

namespace Aetherphone.Tests;

public sealed class EmulatorFirmwareBoundaryTests
{
    private static readonly string[] ForbiddenNames =
    {
        "bios", "firmware", "syscard", "scph", "neogeo.zip", "aes.zip", "neocd", "disksys.rom",
        "dmg_boot", "cgb_boot", "sgb.boot", "dsp1", "dsp2", "dsp3", "dsp4", "st010", "st011", "st018",
        "cx4", "gba_bios", "bios7", "bios9", "ipl.n64",
    };

    private static readonly string[] AllowedExtensions = { ".cs", ".txt", ".md", ".json", ".csproj" };

    [Fact]
    public void NoConsoleFirmwareIsShippedWithThePlugin()
    {
        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(PluginRoot(), "*", SearchOption.AllDirectories))
        {
            if (IsBuildOutput(file))
            {
                continue;
            }

            var name = Path.GetFileName(file);
            if (!Array.Exists(ForbiddenNames, term => name.Contains(term, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (Array.Exists(AllowedExtensions,
                    extension => name.EndsWith(extension, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            offenders.Add(Path.GetRelativePath(PluginRoot(), file));
        }

        Assert.True(offenders.Count == 0,
            $"Console firmware must never ship with the plugin. Found: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void EveryDeclaredFirmwareFileIsSuppliedByTheUser()
    {
        var declared = new List<string>();
        for (var index = 0; index < EmulatorSystemCatalog.All.Count; index++)
        {
            var system = EmulatorSystemCatalog.All[index];
            for (var entry = 0; entry < system.Firmware.Count; entry++)
            {
                declared.Add(system.Firmware[entry].FileName);
            }
        }

        Assert.NotEmpty(declared);
        foreach (var fileName in declared)
        {
            var matches = Directory.EnumerateFiles(PluginRoot(), Path.GetFileName(fileName),
                SearchOption.AllDirectories);
            Assert.True(!matches.Any(candidate => !IsBuildOutput(candidate)),
                $"{fileName} is declared as user-supplied firmware but ships with the plugin.");
        }
    }

    [Fact]
    public void NothingInTheSourceDownloadsConsoleFirmware()
    {
        var urls = new List<string>();
        foreach (var file in Directory.EnumerateFiles(PluginRoot(), "*.cs", SearchOption.AllDirectories))
        {
            if (IsBuildOutput(file))
            {
                continue;
            }

            foreach (Match match in Regex.Matches(File.ReadAllText(file), @"https?://[^\s""']+"))
            {
                urls.Add(match.Value);
            }
        }

        Assert.NotEmpty(urls);
        foreach (var url in urls)
        {
            Assert.False(
                Array.Exists(ForbiddenNames, term => url.Contains(term, StringComparison.OrdinalIgnoreCase)),
                $"{url} looks like a console firmware download. Firmware must always come from the user.");
        }
    }

    private static bool IsBuildOutput(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
            StringComparison.OrdinalIgnoreCase) ||
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
            StringComparison.OrdinalIgnoreCase);

    private static string PluginRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "Aetherphone");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException($"Could not locate src/Aetherphone above '{AppContext.BaseDirectory}'.");
    }
}
