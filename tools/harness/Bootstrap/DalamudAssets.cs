using System.Text.Json;

namespace Aetherphone.Harness.Bootstrap;

internal static class DalamudAssets
{
    private const string MetaUrl = "https://kamori.goats.dev/Dalamud/Asset/Meta";
    private const string UserAgent = "Aetherphone-Harness-Bootstrap";
    private static readonly string[] FontExtensions = { ".otf", ".ttf" };

    public static async Task<string> EnsureFontsAsync(string cacheDirectory, bool refresh)
    {
        var assetDirectory = Path.Combine(cacheDirectory, "assets");
        var marker = Path.Combine(assetDirectory, "UIRes", "FontAwesomeFreeSolid.otf");
        if (!refresh && File.Exists(marker))
        {
            Console.WriteLine($"Dalamud fonts already cached at {assetDirectory}");
            return assetDirectory;
        }

        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        using var meta = JsonDocument.Parse(await client.GetStringAsync(MetaUrl));
        var assets = meta.RootElement.GetProperty("assets");
        var downloaded = 0;
        foreach (var asset in assets.EnumerateArray())
        {
            var fileName = asset.GetProperty("fileName").GetString() ?? string.Empty;
            if (!IsFont(fileName))
            {
                continue;
            }

            var url = asset.GetProperty("url").GetString() ?? string.Empty;
            var target = Path.Combine(assetDirectory, fileName.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await using (var download = await client.GetStreamAsync(url))
            await using (var file = File.Create(target))
            {
                await download.CopyToAsync(file);
            }

            downloaded += 1;
        }

        Console.WriteLine($"Downloaded {downloaded} Dalamud font assets to {assetDirectory}");
        return assetDirectory;
    }

    private static bool IsFont(string fileName)
    {
        for (var index = 0; index < FontExtensions.Length; index++)
        {
            if (fileName.EndsWith(FontExtensions[index], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
