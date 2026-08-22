using Aetherphone.Core.Game;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Aethernet;

internal sealed class AppAvailability : IDisposable
{
    private const long RefreshIntervalMilliseconds = 5 * 60 * 1000;
    private const long RetryIntervalMilliseconds = 60 * 1000;

    private static readonly string[] AlwaysAvailable = { "appstore", "settings", "announcements" };
    private static readonly string[] HiddenUntilLaunched = { "muster", "coin", "casino" };
    private static readonly string[] UnavailableInChina = { "music", "aetherstream", "news", "hunts" };

    private static AppAvailability? current;

    private readonly HttpService http;
    private readonly AethernetSession session;
    private readonly Configuration configuration;
    private readonly GameData gameData;
    private readonly CancellationTokenSource cancellation = new();
    private volatile Dictionary<string, bool> flags;
    private long nextFetchTick;
    private int fetching;

    public AppAvailability(HttpService http, AethernetSession session, Configuration configuration,
        GameData gameData)
    {
        this.http = http;
        this.session = session;
        this.configuration = configuration;
        this.gameData = gameData;
        flags = new Dictionary<string, bool>(configuration.AppFlags, StringComparer.Ordinal);
        current = this;
    }

    public static bool IsEnabled(string appId) => current is null || current.Enabled(appId);

    public bool Enabled(string appId)
    {
        if (IsUnavailableInChina(appId) && gameData.IsChineseGameClient())
        {
            return false;
        }

        EnsureFresh();
        if (IsAlwaysAvailable(appId))
        {
            return true;
        }

        return flags.TryGetValue(appId, out var enabled) ? enabled : !IsHiddenUntilLaunched(appId);
    }

    private static bool IsAlwaysAvailable(string appId)
    {
        for (var index = 0; index < AlwaysAvailable.Length; index++)
        {
            if (string.Equals(appId, AlwaysAvailable[index], StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsHiddenUntilLaunched(string appId)
    {
        for (var index = 0; index < HiddenUntilLaunched.Length; index++)
        {
            if (string.Equals(appId, HiddenUntilLaunched[index], StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsUnavailableInChina(string appId)
    {
        for (var index = 0; index < UnavailableInChina.Length; index++)
        {
            if (string.Equals(appId, UnavailableInChina[index], StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private void EnsureFresh()
    {
        if (session.IsBanned || Environment.TickCount64 < Volatile.Read(ref nextFetchTick) ||
            Interlocked.Exchange(ref fetching, 1) == 1)
        {
            return;
        }

        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var response = await http.GetJsonAsync($"{session.BaseUrl.TrimEnd('/')}/flags",
                    AethernetJsonContext.Default.FeatureFlagsDto, null, token).ConfigureAwait(false);
                if (response?.Apps is { Count: > 0 } served)
                {
                    Apply(served);
                    Volatile.Write(ref nextFetchTick, Environment.TickCount64 + RefreshIntervalMilliseconds);
                    return;
                }

                Volatile.Write(ref nextFetchTick, Environment.TickCount64 + RetryIntervalMilliseconds);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "App availability fetch failed");
                Volatile.Write(ref nextFetchTick, Environment.TickCount64 + RetryIntervalMilliseconds);
            }
            finally
            {
                Volatile.Write(ref fetching, 0);
            }
        });
    }

    private void Apply(Dictionary<string, bool> served)
    {
        if (Matches(served))
        {
            return;
        }

        flags = new Dictionary<string, bool>(served, StringComparer.Ordinal);
        configuration.AppFlags = new Dictionary<string, bool>(served, StringComparer.Ordinal);
        configuration.Save();
    }

    private bool Matches(Dictionary<string, bool> served)
    {
        var known = flags;
        if (known.Count != served.Count)
        {
            return false;
        }

        foreach (var entry in served)
        {
            if (!known.TryGetValue(entry.Key, out var enabled) || enabled != entry.Value)
            {
                return false;
            }
        }

        return true;
    }

    public void Dispose()
    {
        if (ReferenceEquals(current, this))
        {
            current = null;
        }

        cancellation.Cancel();
        cancellation.Dispose();
    }
}
