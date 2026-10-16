using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class PrismClient
{
    internal const string MediaScope = "prism";
    internal const string ReportTarget = "prism_look";

    private readonly AethernetTransport net;

    public PrismClient(AethernetTransport net)
    {
        this.net = net;
    }

    internal static string WeekPath(string lang) => $"/prism/week?lang={Uri.EscapeDataString(lang)}";

    internal static string EntryPath(string weekId) => $"/prism/weeks/{Uri.EscapeDataString(weekId)}/entry";

    internal static string BallotsPath(string weekId) => $"/prism/weeks/{Uri.EscapeDataString(weekId)}/ballots";

    internal static string BallotPath(string ballotId) => $"/prism/ballots/{Uri.EscapeDataString(ballotId)}";

    internal static string ResultsPath(string weekId, string lang) =>
        $"/prism/weeks/{Uri.EscapeDataString(weekId)}/results?lang={Uri.EscapeDataString(lang)}";

    public Task<PrismWeekDto?> WeekAsync(string lang, CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync(WeekPath(lang), AethernetJsonContext.Default.PrismWeekDto, token, null, onFailure);
    }

    public Task<PrismEntryDto?> EnterAsync(string weekId, PrismEntryRequest request, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendJsonAsync(HttpMethod.Put, EntryPath(weekId), request,
            AethernetJsonContext.Default.PrismEntryRequest, AethernetJsonContext.Default.PrismEntryDto, token, null,
            onFailure);
    }

    public Task<bool> WithdrawAsync(string weekId, CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.SendAsync(HttpMethod.Delete, EntryPath(weekId), token, null, onFailure);
    }

    public Task<PrismBallotDto?> BallotAsync(string weekId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.RequestAsync(HttpMethod.Post, BallotsPath(weekId), AethernetJsonContext.Default.PrismBallotDto,
            token, null, onFailure);
    }

    public Task<PrismWeekDto?> SubmitBallotAsync(string ballotId, PrismBallotRequest request,
        CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.SendJsonAsync(HttpMethod.Put, BallotPath(ballotId), request,
            AethernetJsonContext.Default.PrismBallotRequest, AethernetJsonContext.Default.PrismWeekDto, token, null,
            onFailure);
    }

    public Task<PrismResultsDto?> ResultsAsync(string weekId, string lang, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync(ResultsPath(weekId, lang), AethernetJsonContext.Default.PrismResultsDto, token, null,
            onFailure);
    }
}
