using Aetherphone.Core.Game;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Aethernet;

internal sealed class AppAvailability : IDisposable
{
    private const long RefreshIntervalMilliseconds = 5 * 60 * 1000;
    private const long RetryIntervalMilliseconds = 60 * 1000;
    private const long PushDebounceMilliseconds = 1500;

    private static readonly string[] AlwaysAvailable = { "appstore", "settings", "announcements" };
    private static readonly string[] HiddenUntilLaunched = { "muster", "coin", "casino", "prism", "loveisland" };
    private static readonly string[] UnavailableInChina = Array.Empty<string>();

    private static AppAvailability? current;

    private readonly HttpService http;
    private readonly AethernetSession session;
    private readonly Configuration configuration;
    private readonly GameData gameData;
    private readonly CancellationTokenSource cancellation = new();
    private volatile Dictionary<string, bool> flags;
    private RealtimeSignalBus? signals;
    private long nextFetchTick;
    private int fetching;
    private int pushedDuringFetch;

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

    public static bool IsEnabled(string appId) => GameSheets.Supports(appId) && (current is null || current.Enabled(appId));

    public void Follow(RealtimeSignalBus bus)
    {
        signals = bus;
        bus.FlagsChanged += OnFlagsChanged;
    }

    internal static long ScheduleAfterPush(long nowTick, long scheduledTick) =>
        Math.Min(scheduledTick, nowTick + PushDebounceMilliseconds);

    internal static long ScheduleAfterFetch(long nowTick, bool served, bool pushedMeanwhile)
    {
        if (pushedMeanwhile)
        {
            return nowTick + PushDebounceMilliseconds;
        }

        return nowTick + (served ? RefreshIntervalMilliseconds : RetryIntervalMilliseconds);
    }

    private void OnFlagsChanged()
    {
        Volatile.Write(ref pushedDuringFetch, 1);
        var scheduled = Volatile.Read(ref nextFetchTick);
        Interlocked.CompareExchange(ref nextFetchTick, ScheduleAfterPush(Environment.TickCount64, scheduled), scheduled);
    }

    public bool Enabled(string appId)
    {
        if (!GameSheets.Supports(appId))
        {
            return false;
        }

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

        Volatile.Write(ref pushedDuringFetch, 0);
        StartFetch(cancellation.Token);
    }

    private void StartFetch(CancellationToken token)
    {
        _ = Task.Run(async () =>
        {
            var served = false;
            try
            {
                var response = await http.GetJsonAsync($"{session.BaseUrl.TrimEnd('/')}/flags",
                    AethernetJsonContext.Default.FeatureFlagsDto, null, token).ConfigureAwait(false);
                if (response?.Apps is { Count: > 0 } apps)
                {
                    Apply(apps);
                    served = true;
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "App availability fetch failed");
            }
            finally
            {
                var pushed = Interlocked.Exchange(ref pushedDuringFetch, 0) == 1;
                Volatile.Write(ref nextFetchTick, ScheduleAfterFetch(Environment.TickCount64, served, pushed));
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
        if (signals is not null)
        {
            signals.FlagsChanged -= OnFlagsChanged;
        }

        if (ReferenceEquals(current, this))
        {
            current = null;
        }

        cancellation.Cancel();
        cancellation.Dispose();
    }
}
