using System.Text;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Hunts;

internal sealed class HuntsAuthTokenStore
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromSeconds(30);
    private const string RefreshUrl = "https://faloop.app/api/auth/user/refresh";

    private const string ProtectionEntropy = "aetherphone.hunts.session";

    private readonly HttpService http;
    private readonly Configuration configuration;
    private readonly object sync = new();
    private string? token;
    private string? sessionId;
    private bool authenticated;
    private DateTime expiresAtUtc = DateTime.MinValue;
    private Task<string?>? inFlightRefresh;

    public HuntsAuthTokenStore(HttpService http, Configuration configuration)
    {
        this.http = http;
        this.configuration = configuration;
        RestorePersistedSession();
    }

    public string? SessionId
    {
        get
        {
            lock (sync)
            {
                return sessionId;
            }
        }
    }

    public bool IsAuthenticated
    {
        get
        {
            lock (sync)
            {
                return authenticated;
            }
        }
    }

    public async Task<string?> GetTokenAsync(CancellationToken cancelToken)
    {
        lock (sync)
        {
            if (token is not null && DateTime.UtcNow < expiresAtUtc - RefreshMargin)
            {
                return token;
            }
        }

        Task<string?> refreshTask;
        lock (sync)
        {
            inFlightRefresh ??= RefreshAsync(cancelToken);
            refreshTask = inFlightRefresh;
        }

        try
        {
            return await refreshTask.ConfigureAwait(false);
        }
        finally
        {
            lock (sync)
            {
                if (ReferenceEquals(inFlightRefresh, refreshTask))
                {
                    inFlightRefresh = null;
                }
            }
        }
    }

    public void Invalidate()
    {
        lock (sync)
        {
            token = null;
            expiresAtUtc = DateTime.MinValue;
        }
    }

    public void ApplyLogin(string newToken, string? newSessionId)
    {
        lock (sync)
        {
            token = newToken;
            expiresAtUtc = DateTime.UtcNow + TokenLifetime;
            authenticated = true;
            if (!string.IsNullOrEmpty(newSessionId))
            {
                sessionId = newSessionId;
            }

            PersistSession();
        }
    }

    public void Logout()
    {
        lock (sync)
        {
            token = null;
            sessionId = null;
            authenticated = false;
            expiresAtUtc = DateTime.MinValue;
            configuration.HuntsSessionCache = string.Empty;
            configuration.HuntsAuthenticated = false;
            configuration.Save();
        }
    }

    private void RestorePersistedSession()
    {
        if (!configuration.HuntsAuthenticated || configuration.HuntsSessionCache.Length == 0)
        {
            return;
        }

        var bytes = LocalKeyProtector.Unprotect(configuration.HuntsSessionCache, ProtectionEntropy);
        if (bytes is null)
        {
            AepLog.Warning("[Hunts] stored session could not be unprotected; starting as a guest.");
            return;
        }

        sessionId = Encoding.UTF8.GetString(bytes);
        authenticated = true;
    }

    private void PersistSession()
    {
        if (string.IsNullOrEmpty(sessionId))
        {
            return;
        }

        configuration.HuntsSessionCache =
            LocalKeyProtector.Protect(Encoding.UTF8.GetBytes(sessionId), ProtectionEntropy);
        configuration.HuntsAuthenticated = true;
        configuration.Save();
    }

    private async Task<string?> RefreshAsync(CancellationToken cancelToken)
    {
        string? currentSessionId;
        lock (sync)
        {
            currentSessionId = sessionId;
        }

        var request = new HuntsRefreshRequest { SessionId = currentSessionId };
        var response = await http
            .PostJsonAsync(RefreshUrl, request, HuntsJsonContext.Default.HuntsRefreshRequest,
                HuntsJsonContext.Default.HuntsRefreshResponse, bearer: null, cancelToken,
                onFailure: failure => AepLog.Warning($"Hunts token refresh failed: {failure.Describe()}"))
            .ConfigureAwait(false);

        var newToken = response?.Data?.Token;
        if (string.IsNullOrEmpty(newToken))
        {
            lock (sync)
            {
                authenticated = false;
            }

            return null;
        }

        lock (sync)
        {
            token = newToken;
            expiresAtUtc = DateTime.UtcNow + TokenLifetime;
            if (!string.IsNullOrEmpty(response?.Data?.SessionId))
            {
                sessionId = response.Data.SessionId;
            }
        }

        return newToken;
    }
}
