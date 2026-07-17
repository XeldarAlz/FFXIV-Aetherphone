using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Analytics;

internal sealed class AnalyticsClient
{
    private readonly HttpService http;
    private readonly AethernetSession session;

    public AnalyticsClient(HttpService http, AethernetSession session)
    {
        this.http = http;
        this.session = session;
    }

    public Task<AnalyticsAckDto?> SendAsync(AnalyticsBatchRequest batch, CancellationToken token)
    {
        return http.PostJsonAsync(
            Url("/analytics/events"),
            batch,
            AethernetJsonContext.Default.AnalyticsBatchRequest,
            AethernetJsonContext.Default.AnalyticsAckDto,
            null,
            token);
    }

    private string Url(string path) => $"{session.BaseUrl.TrimEnd('/')}{path}";
}
