using System.Diagnostics;
using System.Text.Json;

namespace Aetherphone.Harness.Bootstrap;

internal static class NativeImGuiBuild
{
    private const string Repository = "https://github.com/goatcorp/gc-cimgui";
    private const string FallbackCommit = "ecd32e56fe2f1d9072e01a090bc447b4f932e5d4";
    private const string UserAgent = "Aetherphone-Harness-Bootstrap";

    private static readonly string[] Sources =
    {
        "cimgui.cpp", "cimgui_impl.cpp", "imgui/imgui.cpp", "imgui/imgui_draw.cpp", "imgui/imgui_demo.cpp",
        "imgui/imgui_tables.cpp", "imgui/imgui_widgets.cpp",
    };

    public static async Task<string> EnsureAsync(string dalamudDirectory, string cacheDirectory, bool refresh)
    {
        if (OperatingSystem.IsWindows())
        {
            var bundled = Path.Combine(dalamudDirectory, "cimgui.dll");
            Console.WriteLine($"Using the Dalamud native ImGui at {bundled}");
            return bundled;
        }

        var nativeDirectory = Path.Combine(cacheDirectory, "native");
        var target = Path.Combine(nativeDirectory, OperatingSystem.IsMacOS() ? "libcimgui.dylib" : "libcimgui.so");
        if (!refresh && File.Exists(target))
        {
            Console.WriteLine($"Native ImGui already built at {target}");
            return target;
        }

        var commit = await ResolveCommitAsync(DalamudDownload.ReadCommitHash(dalamudDirectory));
        var sourceDirectory = Path.Combine(cacheDirectory, "gc-cimgui");
        if (!Directory.Exists(Path.Combine(sourceDirectory, ".git")))
        {
            Run("git", $"clone --quiet {Repository} \"{sourceDirectory}\"", cacheDirectory);
        }
        else
        {
            Run("git", "fetch --quiet", sourceDirectory);
        }

        Run("git", $"checkout --quiet {commit}", sourceDirectory);
        Run("git", "submodule update --init --quiet", sourceDirectory);
        Directory.CreateDirectory(nativeDirectory);
        Run(Compiler(), CompilerArguments(target), sourceDirectory);
        Console.WriteLine($"Native ImGui built at {target} from gc-cimgui {commit}");
        return target;
    }

    private static async Task<string> ResolveCommitAsync(string dalamudCommit)
    {
        if (dalamudCommit.Length == 0)
        {
            return FallbackCommit;
        }

        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
            var url = $"https://api.github.com/repos/goatcorp/Dalamud/contents/lib/cimgui?ref={dalamudCommit}";
            using var document = JsonDocument.Parse(await client.GetStringAsync(url));
            var sha = document.RootElement.GetProperty("sha").GetString();
            return string.IsNullOrEmpty(sha) ? FallbackCommit : sha;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or KeyNotFoundException)
        {
            Console.WriteLine($"Could not resolve the cimgui commit for Dalamud {dalamudCommit}, using {FallbackCommit}");
            return FallbackCommit;
        }
    }

    private static string Compiler() => OperatingSystem.IsMacOS() ? "clang++" : "c++";

    private static string CompilerArguments(string target)
    {
        var platform = OperatingSystem.IsMacOS()
            ? "-dynamiclib -arch arm64 -arch x86_64 -mmacosx-version-min=12.0"
            : "-shared";
        var defines = "-DCIMGUI_EXPORTS -DIMGUI_DISABLE_OBSOLETE_FUNCTIONS=1 -DIMGUI_USER_CONFIG=\\\"cimgui_user.h\\\"";
        return $"-O2 -std=c++17 -fPIC -fvisibility=default {platform} {defines} -Iimgui -I. {string.Join(' ', Sources)} -o \"{target}\"";
    }

    private static void Run(string fileName, string arguments, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo(fileName, arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Could not start {fileName}.");
        var standardError = process.StandardError.ReadToEnd();
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{fileName} {arguments} failed:\n{standardError}");
        }
    }
}
