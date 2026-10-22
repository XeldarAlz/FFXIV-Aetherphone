using System.Text.Json;
using Aetherphone.Harness.Fakes;

namespace Aetherphone.Harness.Host;

internal static class HarnessBackend
{
    public const string Dev = "dev";
    public const string Live = "live";
    private const string DevConfigFolder = "config";
    private const string LiveConfigFolder = "config-live";
    private const string ConfigFile = "Aetherphone.json";
    private const string BaseUrlKey = "AethernetBaseUrl";

    public static string Resolve(string value) => value switch
    {
        Dev => Configuration.TestAethernetBaseUrl,
        Live => Configuration.LiveAethernetBaseUrl,
        _ => CustomUrl(value),
    };

    public static string ConfigFolder(string baseUrl)
    {
        if (Same(baseUrl, Configuration.TestAethernetBaseUrl))
        {
            return DevConfigFolder;
        }

        if (Same(baseUrl, Configuration.LiveAethernetBaseUrl))
        {
            return LiveConfigFolder;
        }

        var parsed = new Uri(baseUrl);
        return $"{DevConfigFolder}-{parsed.Host}-{parsed.Port}";
    }

    public static void SettleConfig(HarnessOptions options)
    {
        var persisted = PersistedBaseUrl(options.ConfigDirectory);
        if (persisted is null || Same(persisted, options.BackendUrl))
        {
            return;
        }

        var home = Path.Combine(options.CacheDirectory, ConfigFolder(persisted));
        if (!options.ConfigDefaulted || Directory.Exists(home))
        {
            throw new ArgumentException(
                $"{options.ConfigDirectory} holds a session for {persisted}, not {options.BackendUrl}. Pass the matching --backend, or move that folder away.");
        }

        Directory.Move(options.ConfigDirectory, home);
        HarnessLog.Note($"moved the {persisted} config to {home}");
    }

    private static string CustomUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var parsed) ||
            (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException($"--backend takes dev, live, or an http(s) URL, not '{value}'.");
        }

        if (parsed.IsLoopback)
        {
            throw new ArgumentException(
                "Release builds of the plugin replace a loopback backend with production. Use this machine's LAN address instead.");
        }

        return value.TrimEnd('/');
    }

    private static string? PersistedBaseUrl(string configDirectory)
    {
        var path = Path.Combine(configDirectory, ConfigFile);
        if (!File.Exists(path))
        {
            return null;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.TryGetProperty(BaseUrlKey, out var value) ? value.GetString() : null;
    }

    private static bool Same(string left, string right) =>
        string.Equals(left.TrimEnd('/'), right.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
}
