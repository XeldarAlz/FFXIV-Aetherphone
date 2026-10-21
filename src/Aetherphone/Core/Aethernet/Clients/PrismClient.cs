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

    internal static string EntryPath(string weekId, string track = "") =>
        $"/prism/weeks/{Uri.EscapeDataString(weekId)}/entry{TrackQuery('?', track)}";

    internal static string BallotsPath(string weekId, string track = "") =>
        $"/prism/weeks/{Uri.EscapeDataString(weekId)}/ballots{TrackQuery('?', track)}";

    internal static string BallotPath(string ballotId) => $"/prism/ballots/{Uri.EscapeDataString(ballotId)}";

    internal static string OutfitPath(string weekId, string track = "") =>
        $"/prism/weeks/{Uri.EscapeDataString(weekId)}/entry/outfit{TrackQuery('?', track)}";

    internal static string ResultsPath(string weekId, string lang, string track = "") =>
        $"/prism/weeks/{Uri.EscapeDataString(weekId)}/results?lang={Uri.EscapeDataString(lang)}{TrackQuery('&', track)}";

    private static string TrackQuery(char separator, string track) =>
        track.Length == 0 ? string.Empty : $"{separator}track={Uri.EscapeDataString(track)}";

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

    public Task<PrismEntryDto?> ShareOutfitAsync(string weekId, string track, PrismOutfitRequest request,
        CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.SendJsonAsync(HttpMethod.Put, OutfitPath(weekId, track), request,
            AethernetJsonContext.Default.PrismOutfitRequest, AethernetJsonContext.Default.PrismEntryDto, token, null,
            onFailure);
    }

    public Task<bool> WithdrawAsync(string weekId, string track, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendAsync(HttpMethod.Delete, EntryPath(weekId, track), token, null, onFailure);
    }

    public Task<PrismBallotDto?> BallotAsync(string weekId, string track, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.RequestAsync(HttpMethod.Post, BallotsPath(weekId, track),
            AethernetJsonContext.Default.PrismBallotDto, token, null, onFailure);
    }

    public Task<PrismWeekDto?> SubmitBallotAsync(string ballotId, PrismBallotRequest request,
        CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.SendJsonAsync(HttpMethod.Put, BallotPath(ballotId), request,
            AethernetJsonContext.Default.PrismBallotRequest, AethernetJsonContext.Default.PrismWeekDto, token, null,
            onFailure);
    }

    public Task<PrismResultsDto?> ResultsAsync(string weekId, string lang, string track, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync(ResultsPath(weekId, lang, track), AethernetJsonContext.Default.PrismResultsDto, token,
            null, onFailure);
    }
}
