using System.Net.Http.Headers;
using System.Net.WebSockets;
using Aetherphone.Core.Updates;

namespace Aetherphone.Core.Net;

internal sealed class AethernetClientIdentity
{
    public const string SourceHeader = "X-Aep-Source";
    public const string BuildHeader = "X-Aep-Build";
    public const string StatusHeader = "X-Aep-Source-Status";
    public const string StatusWarned = "warned";
    public const string StatusBlocked = "blocked";

    private readonly Func<string> baseUrl;
    private readonly Action<string> onSourceStatus;
    private ResolvedHost resolved = new(string.Empty, string.Empty);

    public AethernetClientIdentity(Func<string> baseUrl, Action<string> onSourceStatus)
    {
        this.baseUrl = baseUrl;
        this.onSourceStatus = onSourceStatus;
    }

    public bool Matches(Uri? uri)
    {
        if (uri is null)
        {
            return false;
        }

        var host = CurrentHost();
        return host.Length > 0 && string.Equals(uri.Host, host, StringComparison.OrdinalIgnoreCase);
    }

    private string CurrentHost()
    {
        var current = baseUrl();
        var cached = resolved;
        if (string.Equals(cached.BaseUrl, current, StringComparison.Ordinal))
        {
            return cached.Host;
        }

        var host = Uri.TryCreate(current, UriKind.Absolute, out var uri) ? uri.Host : string.Empty;
        resolved = new ResolvedHost(current, host);
        return host;
    }

    private sealed record ResolvedHost(string BaseUrl, string Host);

    public void Apply(HttpRequestHeaders headers)
    {
        if (InstallSource.Repository.Length > 0)
        {
            headers.TryAddWithoutValidation(SourceHeader, InstallSource.Repository);
        }

        headers.TryAddWithoutValidation(BuildHeader, InstallSource.Build);
    }

    public static void Apply(ClientWebSocketOptions options)
    {
        if (InstallSource.Repository.Length > 0)
        {
            options.SetRequestHeader(SourceHeader, InstallSource.Repository);
        }

        options.SetRequestHeader(BuildHeader, InstallSource.Build);
    }

    public void Observe(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues(StatusHeader, out var values))
        {
            return;
        }

        foreach (var value in values)
        {
            onSourceStatus(value);
            return;
        }
    }
}

internal sealed class AethernetIdentityHandler : DelegatingHandler
{
    private readonly AethernetClientIdentity identity;

    public AethernetIdentityHandler(AethernetClientIdentity identity, HttpMessageHandler inner) : base(inner)
    {
        this.identity = identity;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        var aethernet = identity.Matches(request.RequestUri);
        if (aethernet)
        {
            identity.Apply(request.Headers);
        }

        var response = await base.SendAsync(request, token).ConfigureAwait(false);
        if (aethernet)
        {
            identity.Observe(response);
        }

        return response;
    }
}
