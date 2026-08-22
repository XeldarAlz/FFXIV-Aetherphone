using Aetherphone.Core.Net;

namespace Aetherphone.Core.Hunts;

internal sealed class HuntsClient
{
    private static readonly string[] RecentLogsMobIds =
    {
        "laideronnette", "wulgaru", "mindflayer", "thousand_cast_theda", "zona_seeker", "brontes",
        "lampalagua", "nunyunuwi", "minhocao", "croque_mitaine", "croakadile", "the_garlok",
        "bonnacon", "nandi", "chernobog", "safat", "agrippa_the_mighty", "kaiser_behemoth",
        "senmurv", "the_pale_rider", "gandarewa", "bird_of_paradise", "leucrotta", "okina",
        "gamma", "orghana", "udumbara", "bone_crawler", "salt_and_light", "aglaope", "ixtab",
        "gunitt", "tarchia", "tyger", "forgiven_pedantry", "forgiven_rebellion", "ker",
        "burfurlur_the_canny", "sphatika", "armstrong", "ruminator", "ophioneus", "narrow_rift",
        "kirlirger_the_abhorrent", "ihnuxokiy", "neyoozoteel", "sansheya",
        "atticus_the_primogenitor", "the_forecaster", "arch_aethereater",
    };

    private readonly HttpService http;
    private readonly HuntsAuthTokenStore tokens;

    public HuntsClient(HttpService http, HuntsAuthTokenStore tokens)
    {
        this.http = http;
        this.tokens = tokens;
    }

    public async Task<HuntsLoginResponse?> LoginAsync(string username, string password, string? sessionId,
        CancellationToken cancelToken)
    {
        var bearer = await tokens.GetTokenAsync(cancelToken).ConfigureAwait(false);
        var request = new HuntsLoginRequest
        {
            Username = username, Password = password, RememberMe = true, SessionId = sessionId,
        };
        const string url = "https://faloop.app/api/auth/user/login";
        var response = await http.PostJsonAsync(url, request, HuntsJsonContext.Default.HuntsLoginRequest,
            HuntsJsonContext.Default.HuntsLoginResponse, bearer, cancelToken, appScope: "hunts",
            rawAuthorization: true,
            onFailure: failure => AepLog.Warning($"Hunts login failed: {failure.Describe()}")
        ).ConfigureAwait(false);

        if (response is { Success: false })
        {
            AepLog.Warning("Hunts login response reported success=false");
            return null;
        }

        return response;
    }

    public async Task<HuntsDataCenterResponse?> DataCenterAsync(string dataCenterName, CancellationToken cancelToken)
    {
        var token = await tokens.GetTokenAsync(cancelToken).ConfigureAwait(false);
        if (token is null)
        {
            AepLog.Warning("Hunts data-center fetch skipped: no auth token");
            return null;
        }

        var url = $"https://faloop.app/api/app/data-center/{Uri.EscapeDataString(dataCenterName.ToLowerInvariant())}";
        var response = await http.GetJsonAsync(url, HuntsJsonContext.Default.HuntsDataCenterResponse, token,
            cancelToken, appScope: "hunts", rawAuthorization: true,
            onFailure: failure => AepLog.Warning($"Hunts data-center fetch failed: {failure.Describe()}")
        ).ConfigureAwait(false);

        if (response is { Success: false })
        {
            AepLog.Warning("Hunts data-center response reported success=false");
            return null;
        }

        return response;
    }

    public async Task<HuntsRecentLogsResponse?> RecentLogsAsync(string dataCenterName, string[] worldIds,
        CancellationToken cancelToken)
    {
        var token = await tokens.GetTokenAsync(cancelToken).ConfigureAwait(false);
        if (token is null)
        {
            AepLog.Warning("Hunts recent logs fetch skipped: no auth token");
            return null;
        }

        var request = new HuntsRecentLogsRequest
        {
            DataCenterId = dataCenterName.ToLowerInvariant(),
            Scopes = new[]
            {
                new HuntsRecentLogsScope { WorldIds = worldIds, MobIds = Array.Empty<string>() },
                new HuntsRecentLogsScope { WorldIds = worldIds, MobIds = RecentLogsMobIds },
            },
        };

        const string url = "https://faloop.app/api/mobs/logs/recent";
        var response = await http.PostJsonAsync(url, request, HuntsJsonContext.Default.HuntsRecentLogsRequest,
            HuntsJsonContext.Default.HuntsRecentLogsResponse, token, cancelToken, appScope: "hunts",
            rawAuthorization: true,
            onFailure: failure => AepLog.Warning($"Hunts recent logs fetch failed: {failure.Describe()}")
        ).ConfigureAwait(false);

        if (response is { Success: false })
        {
            AepLog.Warning("Hunts recent logs response reported success=false");
            return null;
        }

        return response;
    }

    public async Task<HuntsAppSessionResponse?> AppSessionAsync(string sessionId, CancellationToken cancelToken)
    {
        var token = await tokens.GetTokenAsync(cancelToken).ConfigureAwait(false);
        if (token is null)
        {
            AepLog.Warning("Hunts app-session fetch skipped: no auth token");
            return null;
        }

        var url = $"https://faloop.app/api/app?sessionId={Uri.EscapeDataString(sessionId)}";
        var response = await http.GetJsonAsync(url, HuntsJsonContext.Default.HuntsAppSessionResponse, token,
            cancelToken, appScope: "hunts", rawAuthorization: true,
            onFailure: failure => AepLog.Warning($"Hunts app-session fetch failed: {failure.Describe()}")
        ).ConfigureAwait(false);

        if (response is { Success: false })
        {
            AepLog.Warning("Hunts app-session response reported success=false");
            return null;
        }

        return response;
    }
}
