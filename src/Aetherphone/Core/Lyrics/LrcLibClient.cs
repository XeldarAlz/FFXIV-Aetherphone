using System.Globalization;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Lyrics;

internal sealed class LrcLibClient
{
    private const string ApiRoot = "https://lrclib.net/api";
    private const string RepositoryUrl = "https://github.com/XeldarAlz/FFXIV-Aetherphone";
    private const int RateLimitedStatus = 429;
    private const int ServerErrorStatus = 500;
    private static readonly string UserAgent = BuildUserAgent();
    private readonly HttpService http;

    public LrcLibClient(HttpService http)
    {
        this.http = http;
    }

    public static string GetUrl(string artist, string track, int durationSeconds)
    {
        var url = string.Concat(ApiRoot, "/get?track_name=", Uri.EscapeDataString(track), "&artist_name=",
            Uri.EscapeDataString(artist));
        if (durationSeconds <= 0)
        {
            return url;
        }

        return string.Concat(url, "&duration=", durationSeconds.ToString(CultureInfo.InvariantCulture));
    }

    public static string SearchUrl(string query)
    {
        return string.Concat(ApiRoot, "/search?q=", Uri.EscapeDataString(query));
    }

    public async Task<LrcLibResponse> GetAsync(string artist, string track, int durationSeconds,
        CancellationToken token)
    {
        var failure = AepFailure.None;
        var dto = await http.GetJsonAsync(GetUrl(artist, track, durationSeconds),
            LrcLibJsonContext.Default.LrcLibTrackDto, null, token, onFailure: reported => failure = reported,
            userAgent: UserAgent).ConfigureAwait(false);
        if (dto is not null)
        {
            return new LrcLibResponse(LrcLibOutcome.Found, new[] { dto.ToTrack() });
        }

        return Classify(failure);
    }

    public async Task<LrcLibResponse> SearchAsync(string query, CancellationToken token)
    {
        var failure = AepFailure.None;
        var dtos = await http.GetJsonAsync(SearchUrl(query), LrcLibJsonContext.Default.LrcLibTrackDtoArray, null,
            token, onFailure: reported => failure = reported, userAgent: UserAgent).ConfigureAwait(false);
        if (dtos is null)
        {
            return Classify(failure);
        }

        if (dtos.Length == 0)
        {
            return LrcLibResponse.NotFound;
        }

        var tracks = new LrcLibTrack[dtos.Length];
        for (var index = 0; index < dtos.Length; index++)
        {
            tracks[index] = dtos[index].ToTrack();
        }

        return new LrcLibResponse(LrcLibOutcome.Found, tracks);
    }

    internal static LrcLibResponse Classify(AepFailure failure)
    {
        if (failure.Kind == AepFailureKind.RateLimitPaused || failure.StatusCode == RateLimitedStatus)
        {
            return LrcLibResponse.Throttled;
        }

        if (failure.Kind is AepFailureKind.Offline or AepFailureKind.Timeout or AepFailureKind.Cancelled
            || failure.StatusCode >= ServerErrorStatus)
        {
            return LrcLibResponse.Failed;
        }

        return LrcLibResponse.NotFound;
    }

    private static string BuildUserAgent()
    {
        var version = typeof(LrcLibClient).Assembly.GetName().Version;
        var versionText = version is null ? "dev" : version.ToString();
        return string.Concat("Aetherphone/", versionText, " (", RepositoryUrl, ")");
    }
}
