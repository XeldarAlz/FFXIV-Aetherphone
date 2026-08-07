using System.Collections.Concurrent;
using Aetherphone.Core.Game;
using Aetherphone.Core.Home;
using Aetherphone.Core.Net;
using Dalamud.Plugin.Services;
using System.Globalization;

namespace Aetherphone.Core.Housing;

internal sealed class HousingService : IDisposable
{
    private const long TickIntervalMilliseconds = 5000;
    private const int MaxBackoffMinutes = 30;
    public const string AppId = "housing";

    private readonly HttpService http;
    private readonly Configuration configuration;
    private readonly GameData gameData;
    private readonly PhoneVisibility visibility;
    private readonly IFramework framework;
    private readonly HousingRestProvider api;
    private readonly HousingCache cache;
    private readonly SemaphoreSlim refreshGate = new(1, 1);
    private readonly CancellationTokenSource cancellation = new();
    private readonly ConcurrentDictionary<long, HousingDistrictSnapshot> snapshots = new();
    private readonly FrameworkTicker ticker;
    private IReadOnlyList<HousingWorld> worlds = Array.Empty<HousingWorld>();
    private volatile bool refreshing;
    private volatile bool worldsLoading;
    private DateTime lastRefreshAttemptUtc;
    private DateTime retryNotBeforeUtc;
    private int consecutiveFailures;
    private int wardAutoPickPending;
    private uint followedWorldId;
    private volatile bool foreground;

    public HousingService(HttpService http, Configuration configuration, GameData gameData, IFramework framework,
        HousingGameMaps gameMaps, PhoneVisibility visibility, DirectoryInfo cacheRoot, AppGate gate)
    {
        this.http = http;
        this.configuration = configuration;
        this.gameData = gameData;
        this.visibility = visibility;
        this.framework = framework;
        GameMaps = gameMaps;
        api = new HousingRestProvider(http, HousingProviderKind.Service, HousingEndpoints.DisplayName,
            () => HousingEndpoints.BaseUrl);
        cache = new HousingCache(cacheRoot);
        Watch = new HousingWatchStore(configuration);
        Filters = new HousingFilterState
        {
            Small = configuration.HousingFilterSmall,
            Medium = configuration.HousingFilterMedium,
            Large = configuration.HousingFilterLarge,
            ShowAllPlots = configuration.HousingShowAllPlots,
        };
        ticker = new FrameworkTicker(framework, TickIntervalMilliseconds, OnTick, gate);
        AepLog.Debug($"Housing provider chain ready: {api.DisplayName} -> {cache.DisplayName}");
    }

    public int Revision { get; private set; }

    public HousingFilterState Filters { get; }

    public HousingWatchStore Watch { get; }

    public HousingGameMaps GameMaps { get; }

    public HousingGameDistrictMap? GameMap => GameMaps.For(DistrictId);

    public string GameMapDetail => GameMaps.DetailFor(DistrictId);

    public HousingGameMapFailure GameMapFailure => GameMaps.FailureFor(DistrictId);

    public string DescribeGameMap() => GameMaps.Describe(DistrictId);

    public IReadOnlyList<HousingWorld> Worlds => worlds;

    public bool WorldsLoading => worldsLoading;

    public bool IsRefreshing => refreshing;

    public HousingLoadState State { get; private set; } = HousingLoadState.Idle;

    public HousingProviderKind ActiveSource { get; private set; } = HousingProviderKind.None;

    public DateTime LastSuccessUtc { get; private set; }

    public string? LastError { get; private set; }

    public int LastStatusCode => api.LastStatusCode;

    public string ProviderName => api.DisplayName;

    public string ApiBaseUrl => api.BaseUrl;

    public int? ProxyCacheAgeSeconds => api.LastProxyCacheAge;

    public HousingProviderStatus Status => new(ActiveSource, State, LastSuccessUtc, LastError);

    public HousingFreshnessThresholds Thresholds =>
        new(configuration.HousingLiveMinutes, configuration.HousingRecentMinutes);

    public uint WorldId => configuration.HousingWorldId;

    public uint DistrictId => HousingDistricts.Resolve(configuration.HousingDistrictId).Id;

    public int Ward => HousingDistricts.ClampWard(DistrictId, configuration.HousingWard);

    public string WorldName => WorldNameOf(configuration.HousingWorldId);

    public string WorldNameOf(uint worldId)
    {
        if (worldId == 0)
        {
            return string.Empty;
        }

        if (TryFindWorld(worldId, out var world))
        {
            return world.Name;
        }

        var name = gameData.WorldName(worldId);
        return string.IsNullOrEmpty(name) ? worldId.ToString(CultureInfo.InvariantCulture) : name;
    }

    public string DistrictName => HousingDistricts.DisplayName(configuration.HousingDistrictId);

    public bool HasWorldSelected => WorldId != 0;

    public HousingDistrictSnapshot? Snapshot => Lookup(WorldId, DistrictId);

    public HousingDistrictSnapshot? Lookup(uint worldId, uint districtId) =>
        worldId == 0 || districtId == 0
            ? null
            : snapshots.TryGetValue(CacheKey(worldId, districtId), out var snapshot)
                ? snapshot
                : null;

    public void EnsureStarted()
    {
        ResolvePreferredWorld();
        EnsureWorlds();
        if (Snapshot is null)
        {
            PrimeFromCache(WorldId, DistrictId);
            wardAutoPickPending = 1;
        }

        Refresh(false);
    }

    public void SelectWorld(uint worldId)
    {
        if (worldId == 0)
        {
            return;
        }

        if (configuration.HousingWorldId == worldId)
        {
            return;
        }

        configuration.HousingWorldId = worldId;
        configuration.Save();
        consecutiveFailures = 0;
        retryNotBeforeUtc = default;
        wardAutoPickPending = 1;
        PrimeFromCache(worldId, DistrictId);
        Bump();
        Refresh(true);
    }

    public void SelectDistrict(uint districtId)
    {
        if (!HousingDistricts.TryGet(districtId, out _) || configuration.HousingDistrictId == districtId)
        {
            return;
        }

        configuration.HousingDistrictId = districtId;
        configuration.HousingWard = HousingDistricts.ClampWard(districtId, configuration.HousingWard);
        configuration.Save();
        wardAutoPickPending = 1;
        PrimeFromCache(WorldId, districtId);
        Bump();
        Refresh(true);
    }

    public void SelectWard(int ward)
    {
        var clamped = HousingDistricts.ClampWard(DistrictId, ward);
        if (configuration.HousingWard == clamped)
        {
            return;
        }

        configuration.HousingWard = clamped;
        configuration.Save();
        wardAutoPickPending = 0;
        Bump();
    }

    public void SetFollowCurrentWorld(bool enabled)
    {
        if (configuration.HousingFollowCurrentWorld == enabled)
        {
            return;
        }

        configuration.HousingFollowCurrentWorld = enabled;
        configuration.Save();
        followedWorldId = 0;
        Bump();
    }

    public void PersistFilterDefaults()
    {
        configuration.HousingFilterSmall = Filters.Small;
        configuration.HousingFilterMedium = Filters.Medium;
        configuration.HousingFilterLarge = Filters.Large;
        configuration.HousingShowAllPlots = Filters.ShowAllPlots;
        configuration.Save();
    }

    public void ClearCache()
    {
        cache.Clear();
        snapshots.Clear();
        State = HousingLoadState.Idle;
        ActiveSource = HousingProviderKind.None;
        Bump();
        Refresh(true);
    }

    public int RefreshMinutes => Math.Clamp(configuration.HousingRefreshMinutes,
        HousingDefaults.MinRefreshMinutes, HousingDefaults.MaxRefreshMinutes);

    public void SetRefreshMinutes(int minutes)
    {
        var clamped = Math.Clamp(minutes, HousingDefaults.MinRefreshMinutes, HousingDefaults.MaxRefreshMinutes);
        if (clamped == configuration.HousingRefreshMinutes)
        {
            return;
        }

        configuration.HousingRefreshMinutes = clamped;
        configuration.Save();
    }

    public void Refresh(bool force)
    {
        var worldId = WorldId;
        var districtId = DistrictId;
        if (worldId == 0 || districtId == 0)
        {
            return;
        }

        if (refreshing)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (!force && retryNotBeforeUtc != default && now < retryNotBeforeUtc)
        {
            return;
        }

        if (lastRefreshAttemptUtc != default)
        {
            var elapsed = now - lastRefreshAttemptUtc;
            var floor = force
                ? TimeSpan.FromSeconds(HousingDefaults.ManualRefreshSeconds)
                : TimeSpan.FromMinutes(RefreshMinutes);
            if (elapsed < floor && Lookup(worldId, districtId) is not null)
            {
                return;
            }
        }

        refreshing = true;
        lastRefreshAttemptUtc = now;
        State = HousingLoadState.Loading;
        Bump();
        _ = RefreshAsync(worldId, districtId);
    }

    public void RefreshAfterExpiry()
    {
        if (refreshing ||
            DateTime.UtcNow - lastRefreshAttemptUtc < TimeSpan.FromMinutes(HousingDefaults.ExpiryRefreshMinutes))
        {
            return;
        }

        Refresh(true);
    }

    public void EnsureWorlds()
    {
        if (worlds.Count > 0 || worldsLoading)
        {
            return;
        }

        var cached = cache.ReadWorlds();
        if (cached is { Count: > 0 })
        {
            worlds = cached;
            Bump();
        }

        worldsLoading = true;
        _ = LoadWorldsAsync();
    }

    public bool TryFindWorld(uint worldId, out HousingWorld world)
    {
        var list = worlds;
        for (var index = 0; index < list.Count; index++)
        {
            if (list[index].Id == worldId)
            {
                world = list[index];
                return true;
            }
        }

        world = null!;
        return false;
    }

    public string DataCenterName(uint worldId) =>
        TryFindWorld(worldId, out var world) ? world.DataCenterName : gameData.DataCenterName(worldId);

    public string RegionName(uint worldId) =>
        TryFindWorld(worldId, out var world)
            ? world.RegionName
            : HousingRegions.For(gameData.DataCenterName(worldId));

    public void CollectWardOpenings(Span<int> counts)
    {
        counts.Clear();
        if (Snapshot is not { } snapshot)
        {
            return;
        }

        var plots = snapshot.Plots;
        for (var index = 0; index < plots.Count; index++)
        {
            var ward = plots[index].Key.Ward;
            if (ward >= 1 && ward <= counts.Length)
            {
                counts[ward - 1]++;
            }
        }
    }

    public uint HomeWorldId => gameData.LocalHomeWorldId;

    public uint CurrentWorldId => gameData.LocalCurrentWorldId;

    public bool IsOnScreen => foreground && visibility.IsVisible;

    public void SetForeground(bool value)
    {
        foreground = value;
    }

    private bool HasBackgroundWork => Watch.Watched.Count > 0 || Watch.PendingReminderCount > 0;

    private void OnTick()
    {
        var onScreen = IsOnScreen;
        if (onScreen)
        {
            FollowWorldIfRequested();
        }

        if (!configuration.HousingAutoRefresh || refreshing || WorldId == 0)
        {
            return;
        }

        if (!onScreen && !HasBackgroundWork)
        {
            return;
        }

        var interval = TimeSpan.FromMinutes(onScreen
            ? RefreshMinutes
            : Math.Max(RefreshMinutes, HousingDefaults.IdleRefreshMinutes));
        if (DateTime.UtcNow - lastRefreshAttemptUtc < interval)
        {
            return;
        }

        Refresh(false);
    }

    private void FollowWorldIfRequested()
    {
        if (!configuration.HousingFollowCurrentWorld)
        {
            return;
        }

        var current = gameData.LocalCurrentWorldId;
        if (current == 0 || current == followedWorldId || current == configuration.HousingWorldId)
        {
            followedWorldId = current;
            return;
        }

        followedWorldId = current;
        SelectWorld(current);
    }

    private void ResolvePreferredWorld()
    {
        if (configuration.HousingWorldId != 0)
        {
            return;
        }

        var home = gameData.LocalHomeWorldId;
        if (home == 0)
        {
            return;
        }

        configuration.HousingWorldId = home;
        configuration.Save();
        wardAutoPickPending = 1;
        Bump();
    }

    private void PrimeFromCache(uint worldId, uint districtId)
    {
        if (worldId == 0 || districtId == 0)
        {
            return;
        }

        if (snapshots.ContainsKey(CacheKey(worldId, districtId)))
        {
            return;
        }

        var stored = cache.Read(worldId, districtId);
        if (stored is null)
        {
            return;
        }

        snapshots[CacheKey(worldId, districtId)] = stored;
        ActiveSource = HousingProviderKind.Cache;
        State = stored.Plots.Count == 0 ? HousingLoadState.Empty : HousingLoadState.Ready;
        Bump();
    }

    private async Task RefreshAsync(uint worldId, uint districtId)
    {
        var entered = false;
        try
        {
            entered = await refreshGate.WaitAsync(0, cancellation.Token).ConfigureAwait(false);
            if (!entered)
            {
                return;
            }

            var token = cancellation.Token;
            var active = api;
            var snapshot = await active.GetDistrictAsync(worldId, districtId, token).ConfigureAwait(false);
            if (snapshot is not null)
            {
                consecutiveFailures = 0;
                retryNotBeforeUtc = default;
                cache.Write(snapshot);
                Apply(snapshot, active.Kind, null);
                return;
            }

            consecutiveFailures++;
            var backoff = TimeSpan.FromMinutes(Math.Min(MaxBackoffMinutes, 1 << Math.Min(5, consecutiveFailures)));
            retryNotBeforeUtc = DateTime.UtcNow.Add(backoff);
            AepLog.Warning(
                $"Housing refresh failed for world {worldId} district {districtId} via {active.DisplayName} " +
                $"(status {active.LastStatusCode}); retrying in {backoff.TotalMinutes:F0}m.");
            var fallback = cache.Read(worldId, districtId);
            if (fallback is not null)
            {
                Apply(fallback, HousingProviderKind.Cache, LookupErrorText());
                return;
            }

            LastError = LookupErrorText();
            State = HousingLoadState.Failed;
            ActiveSource = HousingProviderKind.None;
            Bump();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            LastError = exception.Message;
            State = HousingLoadState.Failed;
            AepLog.Warning($"Housing refresh threw: {exception.Message}");
            Bump();
        }
        finally
        {
            refreshing = false;
            if (entered)
            {
                try
                {
                    refreshGate.Release();
                }
                catch (ObjectDisposedException)
                {
                }
            }
        }
    }

    private void Apply(HousingDistrictSnapshot? snapshot, HousingProviderKind source, string? error)
    {
        if (snapshot is null)
        {
            State = HousingLoadState.Failed;
            LastError = error;
            Bump();
            return;
        }

        snapshots[CacheKey(snapshot.WorldId, snapshot.DistrictId)] = snapshot;

        RunOnFrameworkThread(() =>
        {
            ActiveSource = source;
            LastError = error;
            State = snapshot.Plots.Count == 0 ? HousingLoadState.Empty : HousingLoadState.Ready;
            if (source != HousingProviderKind.Cache)
            {
                LastSuccessUtc = DateTime.UtcNow;
            }

            Watch.Reconcile(snapshot);
            AutoPickWard(snapshot);
            AepLog.Debug($"Housing loaded {snapshot.Plots.Count} open plots for {snapshot.DistrictName} " +
                         $"from {source}.");
            Bump();
        });
    }

    private void RunOnFrameworkThread(Action action)
    {
        if (framework.IsInFrameworkUpdateThread)
        {
            action();
            return;
        }

        _ = framework.RunOnFrameworkThread(action);
    }

    private void AutoPickWard(HousingDistrictSnapshot snapshot)
    {
        if (Interlocked.Exchange(ref wardAutoPickPending, 0) == 0 || snapshot.Plots.Count == 0)
        {
            return;
        }

        var wards = HousingDistricts.Resolve(snapshot.DistrictId).Wards;
        Span<int> counts = stackalloc int[wards];
        counts.Clear();
        var plots = snapshot.Plots;
        for (var index = 0; index < plots.Count; index++)
        {
            var ward = plots[index].Key.Ward;
            if (ward >= 1 && ward <= wards)
            {
                counts[ward - 1]++;
            }
        }

        var current = HousingDistricts.ClampWard(snapshot.DistrictId, configuration.HousingWard);
        if (counts[current - 1] > 0)
        {
            return;
        }

        var best = 0;
        var bestWard = current;
        for (var index = 0; index < counts.Length; index++)
        {
            if (counts[index] > best)
            {
                best = counts[index];
                bestWard = index + 1;
            }
        }

        if (best == 0 || bestWard == current)
        {
            return;
        }

        configuration.HousingWard = bestWard;
        configuration.Save();
    }

    private async Task LoadWorldsAsync()
    {
        try
        {
            var token = cancellation.Token;
            var loaded = await api.GetWorldsAsync(token).ConfigureAwait(false);
            if (loaded is { Count: > 0 })
            {
                worlds = loaded;
                cache.WriteWorlds(loaded);
            }
            else if (worlds.Count == 0)
            {
                AepLog.Warning("Housing world list unavailable; the world picker will stay empty until it loads.");
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            AepLog.Warning($"Housing world list failed: {exception.Message}");
        }
        finally
        {
            worldsLoading = false;
            Bump();
        }
    }

    private string LookupErrorText() =>
        api.LastStatusCode > 0
            ? string.Concat("HTTP ", api.LastStatusCode.ToString(CultureInfo.InvariantCulture))
            : "unreachable";

    private static long CacheKey(uint worldId, uint districtId) => ((long)worldId << 32) | districtId;

    private void Bump() => Revision++;

    public void Dispose()
    {
        ticker.Dispose();
        cancellation.Cancel();
        api.Dispose();
        refreshGate.Dispose();
        cancellation.Dispose();
    }
}
