namespace Aetherphone.Core.Video;

internal static class ResolverSiteOptions
{
    internal const string BaseRawOptions = "force-ipv4=,hls-use-mpegts=";

    private const string PlainUserAgent = "user-agent=Mozilla/5.0";

    private static readonly SiteRule[] Rules =
    [
        new("tubitv.com", PlainUserAgent),
        new("tubi.tv", PlainUserAgent),
    ];

    internal static string RawOptionsFor(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed))
        {
            return BaseRawOptions;
        }

        var host = parsed.Host;
        for (var index = 0; index < Rules.Length; index++)
        {
            var rule = Rules[index];
            if (MatchesHost(host, rule.Host))
            {
                return BaseRawOptions + "," + rule.RawOptions;
            }
        }

        return BaseRawOptions;
    }

    private static bool MatchesHost(string host, string ruleHost)
    {
        if (host.Equals(ruleHost, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return host.Length > ruleHost.Length
            && host[host.Length - ruleHost.Length - 1] == '.'
            && host.EndsWith(ruleHost, StringComparison.OrdinalIgnoreCase);
    }

    private readonly record struct SiteRule(string Host, string RawOptions);
}
