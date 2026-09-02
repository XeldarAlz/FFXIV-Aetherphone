using System.IO.Compression;

namespace Aetherphone.Harness.Bootstrap;

internal static class DalamudDownload
{
    private const string UserAgent = "Aetherphone-Harness-Bootstrap";
    private const string MarkerFile = "Dalamud.dll";

    public static async Task EnsureAsync(string url, string targetDirectory, bool refresh)
    {
        if (!refresh && File.Exists(Path.Combine(targetDirectory, MarkerFile)))
        {
            Console.WriteLine($"Dalamud already cached at {targetDirectory}");
            return;
        }

        Console.WriteLine($"Downloading Dalamud from {url}");
        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        var archivePath = Path.Combine(Path.GetTempPath(), $"dalamud-{Guid.NewGuid():N}.zip");
        await using (var download = await client.GetStreamAsync(url))
        await using (var file = File.Create(archivePath))
        {
            await download.CopyToAsync(file);
        }

        if (Directory.Exists(targetDirectory))
        {
            Directory.Delete(targetDirectory, true);
        }

        Directory.CreateDirectory(targetDirectory);
        ZipFile.ExtractToDirectory(archivePath, targetDirectory, true);
        File.Delete(archivePath);
        Console.WriteLine($"Dalamud extracted to {targetDirectory}");
    }

    public static string ReadCommitHash(string dalamudDirectory)
    {
        var path = Path.Combine(dalamudDirectory, "commit_hash.txt");
        return File.Exists(path) ? File.ReadAllText(path).Trim() : string.Empty;
    }
}
