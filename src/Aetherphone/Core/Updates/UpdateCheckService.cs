using Aetherphone.Core.Net;
using Dalamud.Plugin;

namespace Aetherphone.Core.Updates;

internal sealed class UpdateCheckService : IDisposable
{
    private static readonly TimeSpan FreshFor = TimeSpan.FromMinutes(30);
    private readonly HttpService http;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly CancellationTokenSource cancellation = new();
    private readonly Version installed;
    private readonly string? manifestUrl;
    private DateTime lastCheckUtc = DateTime.MinValue;
    private volatile bool checking;

    public UpdateCheckService(HttpService http, IDalamudPluginInterface pluginInterface)
    {
        this.http = http;
        this.pluginInterface = pluginInterface;
        installed = Normalize(typeof(UpdateCheckService).Assembly.GetName().Version);
        manifestUrl = ResolveManifestUrl(pluginInterface);
    }

    public Version? Latest { get; private set; }

    public bool UpdateAvailable => Latest is { } latest && latest > installed;

    public string LatestText => Latest is { } latest ? latest.ToString(3) : string.Empty;

    public void Poll()
    {
        if (manifestUrl is null || checking || DateTime.UtcNow - lastCheckUtc < FreshFor)
        {
            return;
        }

        checking = true;
        lastCheckUtc = DateTime.UtcNow;
        _ = CheckAsync(manifestUrl);
    }

    private async Task CheckAsync(string url)
    {
        try
        {
            var entries = await http
                .GetJsonAsync(url, PluginManifestJsonContext.Default.ManifestEntries, null, cancellation.Token)
                .ConfigureAwait(false);
            if (entries is null)
            {
                return;
            }

            Latest = FindLatest(entries);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            AepLog.Warning($"Update check failed for {url}: {exception.Message}");
        }
        finally
        {
            checking = false;
        }
    }

    private Version? FindLatest(PluginManifestEntry[] entries)
    {
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            if (!string.Equals(entry.InternalName, AepConstants.Name, StringComparison.Ordinal))
            {
                continue;
            }

            var text = pluginInterface.IsTesting
                ? entry.TestingAssemblyVersion ?? entry.AssemblyVersion
                : entry.AssemblyVersion;
            return Version.TryParse(text, out var parsed) ? Normalize(parsed) : null;
        }

        return null;
    }

    private static string? ResolveManifestUrl(IDalamudPluginInterface pluginInterface)
    {
        if (pluginInterface.IsDev)
        {
            return null;
        }

        var source = pluginInterface.SourceRepository;
        if (string.IsNullOrEmpty(source) || !Uri.TryCreate(source, UriKind.Absolute, out var uri))
        {
            return null;
        }

        return uri.Scheme is "http" or "https" ? source : null;
    }

    private static Version Normalize(Version? version)
    {
        if (version is null)
        {
            return new Version(0, 0, 0, 0);
        }

        return new Version(version.Major, version.Minor, Math.Max(version.Build, 0), Math.Max(version.Revision, 0));
    }

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
