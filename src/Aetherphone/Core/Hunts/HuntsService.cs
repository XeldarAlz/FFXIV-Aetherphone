using System.Globalization;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;

namespace Aetherphone.Core.Hunts;

internal readonly struct HuntSpawnKey : IEquatable<HuntSpawnKey>
{
    public readonly string MobId;
    public readonly string WorldId;
    public readonly int ZoneInstance;

    public HuntSpawnKey(string mobId, string worldId, int zoneInstance)
    {
        MobId = mobId;
        WorldId = worldId;
        ZoneInstance = zoneInstance;
    }

    public bool Equals(HuntSpawnKey other) =>
        ZoneInstance == other.ZoneInstance &&
        string.Equals(MobId, other.MobId, StringComparison.Ordinal) &&
        string.Equals(WorldId, other.WorldId, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) => obj is HuntSpawnKey other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(MobId, StringComparer.OrdinalIgnoreCase.GetHashCode(WorldId), ZoneInstance);
}

internal sealed class HuntsService : IDisposable
{
    private const string AppId = "hunts";

    private readonly HuntsClient client;
    private readonly HuntsAuthTokenStore tokens;
    private readonly HuntMobCatalog mobCatalog;
    private readonly GameData gameData;
    private readonly CharacterWatch characterWatch;
    private readonly NotificationService notifications;
    private readonly Configuration configuration;
    private readonly StoreWork work;
    private readonly HuntsRealtimeClient realtimeClient;
    private readonly HuntsLoginFlow loginFlow;
    private readonly SettingsSnapshotStore<HuntsNotificationSnapshot> notificationSettingsStore;
    private readonly object windowsGate = new();
    private readonly object activeSpawnGate = new();
    private readonly object historyGate = new();
    private readonly object realtimeStateGate = new();
    private readonly HashSet<HuntSpawnKey> activeSpawnKeys = new();

    private readonly HashSet<HuntSpawnKey> scheduledSpawnKeys = new();
    private readonly Dictionary<HuntSpawnKey, DateTimeOffset> activeSpawnTimestamps = new();
    private const int MaxHistoryEntries = 200;

    private volatile HuntWindowDto[] windows = Array.Empty<HuntWindowDto>();
    private volatile HuntLogEntryDto[] history = Array.Empty<HuntLogEntryDto>();
    private volatile Dictionary<string, int> zoneInstanceCounts = new(StringComparer.Ordinal);
    private volatile Dictionary<HuntSpawnKey, int> realtimeSpawnLocations = new();

    private volatile Dictionary<HuntSpawnKey, (int WindowNum, int PhaseNum)> realtimePhases = new();

    private volatile Dictionary<HuntSpawnKey, string> realtimeZoneIds = new();

    private volatile Dictionary<HuntSpawnKey, string[]> realtimeReporterNames = new();
    private volatile bool loading;
    private volatile bool loaded;
    private volatile bool failed;
    private volatile bool historyLoading;
    private volatile bool historyLoaded;
    private volatile bool realtimeConnected;
    private volatile string? currentDataCenter;
    private volatile int activeSpawnVersion;
    private string? lastAppSessionId;
    private DateTimeOffset? maintenanceStartAt;

    private Dictionary<string, DateTimeOffset>? worldMaintenanceOverrides;
    private HashSet<string>? allowedRanks;

    public HuntsService(HuntsClient client, HuntsAuthTokenStore tokens, HuntMobCatalog mobCatalog, GameData gameData,
        CharacterWatch characterWatch, NotificationService notifications, Configuration configuration)
    {
        this.client = client;
        this.tokens = tokens;
        this.mobCatalog = mobCatalog;
        this.gameData = gameData;
        this.characterWatch = characterWatch;
        this.notifications = notifications;
        this.configuration = configuration;
        work = new StoreWork("Hunts");
        realtimeClient = new HuntsRealtimeClient(tokens);
        realtimeClient.MobReportReceived += OnMobReportReceived;
        realtimeClient.MobWorldKillReceived += OnMobWorldKillReceived;
        realtimeClient.ConnectedChanged += OnRealtimeConnectedChanged;
        loginFlow = new HuntsLoginFlow(client, tokens);
        loginFlow.Succeeded += OnLoginSucceeded;
        characterWatch.Changed += OnCharacterChanged;

        notificationSettingsStore = new SettingsSnapshotStore<HuntsNotificationSnapshot>(configuration,
            static config => config.HuntsNotificationSettings,
            static (config, snapshot) => config.HuntsNotificationSettings = snapshot);
        NotificationSettings = new HuntsNotificationSettings();
        if (notificationSettingsStore.Load() is { } savedNotificationSettings)
        {
            NotificationSettings.ApplySnapshot(savedNotificationSettings);
        }

        NotificationSettings.Changed += OnNotificationSettingsChanged;
        EnsureRealtimeStarted();
    }

    private void EnsureRealtimeStarted()
    {
        if (!configuration.HuntsAppOpened || !tokens.IsAuthenticated)
        {
            return;
        }

        realtimeClient.Start();
    }

    public HuntWindowDto[] Windows => windows;
    public bool Loading => loading;
    public bool Loaded => loaded;
    public bool Failed => failed;

    public HuntLogEntryDto[] History => history;
    public bool HistoryLoading => historyLoading;
    public bool HistoryLoaded => historyLoaded;

    public string? CurrentDataCenter => currentDataCenter;
    public bool IsAuthenticated => tokens.IsAuthenticated;
    public bool RealtimeConnected => realtimeConnected;
    public int ActiveSpawnVersion => activeSpawnVersion;

    public int ActiveSpawnCount
    {
        get
        {
            lock (activeSpawnGate)
            {
                var count = 0;
                foreach (var key in activeSpawnKeys)
                {
                    if (NotificationSettings.IsEnabledFor(mobCatalog.Find(key.MobId), key.WorldId))
                    {
                        count++;
                    }
                }

                return count;
            }
        }
    }

    public HuntsLoginFlow LoginFlow => loginFlow;

    public HuntsNotificationSettings NotificationSettings { get; }

    public void SaveNotificationSettings() => notificationSettingsStore.Save(NotificationSettings.ToSnapshot());

    public bool IsSpawned(string mobId, string worldId, int zoneInstance)
    {
        lock (activeSpawnGate)
        {
            return activeSpawnKeys.Contains(new HuntSpawnKey(mobId, worldId, zoneInstance));
        }
    }

    public bool IsScheduled(string mobId, string worldId, int zoneInstance)
    {
        lock (activeSpawnGate)
        {
            return scheduledSpawnKeys.Contains(new HuntSpawnKey(mobId, worldId, zoneInstance));
        }
    }

    public DateTimeOffset? SpawnedSince(string mobId, string worldId, int zoneInstance)
    {
        lock (activeSpawnGate)
        {
            return activeSpawnTimestamps.TryGetValue(new HuntSpawnKey(mobId, worldId, zoneInstance),
                out var timestamp)
                ? timestamp
                : null;
        }
    }

    public int? ConfirmedPoiIdFor(string mobId, string worldId, int zoneInstance) =>
        realtimeSpawnLocations.TryGetValue(new HuntSpawnKey(mobId, worldId, zoneInstance), out var poiId)
            ? poiId
            : null;

    public (int WindowNum, int PhaseNum)? PhaseFor(string mobId, string worldId, int zoneInstance) =>
        realtimePhases.TryGetValue(new HuntSpawnKey(mobId, worldId, zoneInstance), out var phase) ? phase : null;

    public string? ZoneIdFor(string mobId, string worldId, int zoneInstance) =>
        realtimeZoneIds.TryGetValue(new HuntSpawnKey(mobId, worldId, zoneInstance), out var zoneId) ? zoneId : null;

    public string[]? ReporterNamesFor(string mobId, string worldId, int zoneInstance) =>
        realtimeReporterNames.TryGetValue(new HuntSpawnKey(mobId, worldId, zoneInstance), out var names)
            ? names
            : null;

    public int ZoneInstanceCountFor(HuntMobDefinition mob)
    {
        var maxInstances = 1;
        var counts = zoneInstanceCounts;
        for (var index = 0; index < mob.ZoneIds.Length; index++)
        {
            if (counts.TryGetValue(mob.ZoneIds[index], out var count) && count > maxInstances)
            {
                maxInstances = count;
            }
        }

        return maxInstances;
    }

    public void EnsureActive()
    {
        if (!configuration.HuntsAppOpened)
        {
            configuration.HuntsAppOpened = true;
            configuration.Save();
        }

        EnsureRealtimeStarted();

        if (currentDataCenter is null)
        {
            ResolveDataCenter();
        }

        if (currentDataCenter is not null && !loaded && !loading)
        {
            Refresh();
        }

        EnsureHistoryLoaded();
    }

    public void EnsureHistoryLoaded()
    {
        if (!tokens.IsAuthenticated || historyLoaded || historyLoading || currentDataCenter is not { } dataCenter)
        {
            return;
        }

        historyLoading = true;
        work.Run("hunts history fetch", async token =>
        {
            var worlds = HuntDataCenterWorlds.WorldsFor(dataCenter);
            var result = await client.RecentLogsAsync(dataCenter, worlds, token).ConfigureAwait(false);
            if (result?.Data?.Logs is { } logs)
            {
                lock (historyGate)
                {
                    history = logs;
                }
            }
        }, () =>
        {
            historyLoading = false;
            historyLoaded = true;
        });
    }

    public void Retry() => Refresh();

    private void OnLoginSucceeded() => realtimeClient.Restart();

    public void Logout()
    {
        tokens.Logout();
        realtimeClient.Stop();
        loaded = false;
        failed = false;
        Refresh();
    }

    public void SelectDataCenter(string dataCenter)
    {
        if (string.IsNullOrEmpty(dataCenter) || string.Equals(dataCenter, currentDataCenter, StringComparison.Ordinal))
        {
            return;
        }

        currentDataCenter = dataCenter;
        loaded = false;
        failed = false;
        ClearActiveSpawns();
        Refresh();
    }

    private void OnCharacterChanged(ulong contentId)
    {
        if (contentId == 0)
        {
            return;
        }

        _ = Plugin.Framework.RunOnFrameworkThread(ResolveDataCenter);
    }

    private void ResolveDataCenter()
    {
        var player = gameData.LocalPlayer;
        if (player is null)
        {
            return;
        }

        SelectDataCenter(gameData.DataCenterName(player.HomeWorld.RowId));
    }

    private void Refresh()
    {
        var dataCenter = currentDataCenter;
        if (dataCenter is null || loading)
        {
            return;
        }

        loading = true;
        work.Run("hunts fetch", async token =>
        {
            var result = await client.DataCenterAsync(dataCenter, token).ConfigureAwait(false);
            var status = result?.Data?.Status;
            var fetched = status?.Windows;
            if (fetched is null)
            {
                failed = true;
                return;
            }

            await RefreshAppSessionAsync(token).ConfigureAwait(false);

            var combined = new List<HuntWindowDto>(fetched);
            SynthesizeMissingWindows(combined, dataCenter);
            var merged = combined.ToArray();
            Array.Sort(merged, static (left, right) => right.StartedAt.CompareTo(left.StartedAt));
            lock (windowsGate)
            {
                windows = merged;
            }
            SeedActiveSpawnsFromPoll(status?.Spawns);
            EnsureWindowEntriesForActiveSpawns();
            failed = false;
        }, () =>
        {
            loading = false;
            loaded = true;
        });
    }

    private void SynthesizeMissingWindows(List<HuntWindowDto> combined, string dataCenter)
    {
        if (maintenanceStartAt is not { } maintenanceStart || allowedRanks is not { } ranks)
        {
            return;
        }

        var worldsInDataCenter = HuntDataCenterWorlds.WorldsFor(dataCenter);
        if (worldsInDataCenter.Length == 0)
        {
            return;
        }

        var coveredPairs = new HashSet<(string MobId, string WorldId)>();
        var coveredInstances = new HashSet<(string MobId, string WorldId, int Instance)>();
        for (var index = 0; index < combined.Count; index++)
        {
            var window = combined[index];
            var worldKey = window.WorldId.ToLowerInvariant();
            coveredPairs.Add((window.MobId, worldKey));
            coveredInstances.Add((window.MobId, worldKey, window.ZoneInstance));
        }

        foreach (var mob in mobCatalog.ById.Values)
        {
            if (!ranks.Contains(mob.Rank) || mob.Windows.Length == 0)
            {
                continue;
            }

            var instanceCount = ZoneInstanceCountFor(mob);
            for (var worldIndex = 0; worldIndex < worldsInDataCenter.Length; worldIndex++)
            {
                var worldId = worldsInDataCenter[worldIndex];
                var worldMaintenanceStart = ResolveMaintenanceStart(worldId, maintenanceStart);
                if (instanceCount <= 1)
                {
                    if (coveredPairs.Add((mob.Id, worldId)))
                    {
                        combined.Add(BuildMaintenanceWindow(mob.Id, worldId, 0, worldMaintenanceStart));
                    }

                    continue;
                }

                for (var instance = 1; instance <= instanceCount; instance++)
                {
                    if (coveredInstances.Add((mob.Id, worldId, instance)))
                    {
                        combined.Add(BuildMaintenanceWindow(mob.Id, worldId, instance, worldMaintenanceStart));
                    }
                }
            }
        }
    }

    private DateTimeOffset ResolveMaintenanceStart(string worldId, DateTimeOffset dataCenterMaintenanceStart) =>
        worldMaintenanceOverrides is { } overrides && overrides.TryGetValue(worldId, out var worldRestart)
            ? worldRestart
            : dataCenterMaintenanceStart;

    private static HuntWindowDto BuildMaintenanceWindow(string mobId, string worldId, int zoneInstance,
        DateTimeOffset maintenanceStart) =>
        new()
        {
            Num = 1,
            StartedAtNormal = maintenanceStart,
            MobId = mobId,
            WorldId = worldId,
            ZoneInstance = zoneInstance,
            UseMaintenanceTiming = true,
        };

    private async Task RefreshAppSessionAsync(CancellationToken cancelToken)
    {
        var sessionId = tokens.SessionId;
        if (string.IsNullOrEmpty(sessionId) || string.Equals(sessionId, lastAppSessionId, StringComparison.Ordinal))
        {
            return;
        }

        var result = await client.AppSessionAsync(sessionId, cancelToken).ConfigureAwait(false);
        var status = result?.Data?.Status;
        if (status is null)
        {
            return;
        }

        lastAppSessionId = sessionId;

        var timeline = status.Maintenance?.Restarts?.Timeline;
        if (timeline is { Length: > 0 })
        {
            DateTimeOffset? latest = null;
            Dictionary<string, DateTimeOffset>? overrides = null;
            for (var index = 0; index < timeline.Length; index++)
            {
                var entry = timeline[index];
                if (string.IsNullOrEmpty(entry.WorldId))
                {
                    if (latest is null || entry.Timestamp > latest.Value)
                    {
                        latest = entry.Timestamp;
                    }

                    continue;
                }

                overrides ??= new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
                var worldId = entry.WorldId;
                if (!overrides.TryGetValue(worldId, out var existing) || entry.Timestamp > existing)
                {
                    overrides[worldId] = entry.Timestamp;
                }
            }

            if (latest is { } resolved)
            {
                maintenanceStartAt = resolved;
            }

            worldMaintenanceOverrides = overrides;
        }

        if (status.Zones is not null)
        {
            var zones = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var zone in status.Zones)
            {
                zones[zone.Key] = zone.Value.NumInstances;
            }

            zoneInstanceCounts = zones;
        }

        var windowRanks = result?.Data?.Session?.Access?.MobStatus?.Windows;
        if (windowRanks is not null)
        {
            allowedRanks = new HashSet<string>(windowRanks.Keys, StringComparer.OrdinalIgnoreCase);
        }
    }

    private void OnRealtimeConnectedChanged(bool value)
    {
        var reconnected = value && !realtimeConnected;
        realtimeConnected = value;
        if (reconnected)
        {
            Refresh();
        }
    }

    private void OnMobReportReceived(HuntsSocketMobReport report)
    {
        if (report.Id is not { } identity)
        {
            return;
        }

        var dataCenter = currentDataCenter;
        if (dataCenter is null || !ContainsWorld(HuntDataCenterWorlds.WorldsFor(dataCenter), identity.WorldId))
        {
            return;
        }

        if (report.Data?.Reporters is { Length: > 0 } reporters)
        {
            SetRealtimeReporters(identity, reporters);
        }

        switch (report.Action)
        {
            case "death" when report.Data is { } data:
                ApplyDeathReport(identity, data);
                return;
            case "spawn_location" when report.Data is { ZonePoiId: { } poiId }:
                SetRealtimeLocation(identity, poiId);
                SetRealtimeZone(identity, identity.ZoneId);
                return;
            case "spawn_release":
                SetRealtimePhase(identity);
                HandleSpawnRelease(identity);
                return;
            case "spawn_claim":
                return;
            case "spawn":
                SetRealtimePhase(identity);
                SetRealtimeZone(identity, identity.ZoneId);
                if (report.Data?.IsScheduled == true)
                {
                    HandleSpawnScheduled(identity);
                }
                else
                {
                    HandleSpawnRelease(identity);
                }

                if (report.Data?.ZonePoiIds is { Length: 1 } zonePoiIds)
                {
                    SetRealtimeLocation(identity, zonePoiIds[0]);
                }

                return;
            case "spawn_progress":
                SetRealtimePhase(identity);
                return;
        }
    }

    private void SeedActiveSpawnsFromPoll(HuntSpawnEntryDto[]? spawns)
    {
        if (spawns is null)
        {
            return;
        }

        for (var index = 0; index < spawns.Length; index++)
        {
            var spawn = spawns[index];
            if (spawn.MobId.Length == 0 || spawn.WorldId.Length == 0)
            {
                continue;
            }

            var identity = new HuntsSocketMobIdentity
            {
                MobId = spawn.MobId,
                WorldId = spawn.WorldId,
                ZoneInstance = spawn.ZoneInstance,
                WindowNum = spawn.WindowNum,
                PhaseNum = spawn.PhaseNum,
                ZoneId = spawn.ZoneId,
            };
            SetRealtimePhase(identity);
            SetRealtimeZone(identity, identity.ZoneId);
            if (spawn.IsScheduled)
            {
                HandleSpawnScheduled(identity, spawn.Timestamp);
            }
            else
            {
                HandleSpawnRelease(identity, spawn.Timestamp);
            }

            if (spawn.ZonePoiIds is { Length: 1 } zonePoiIds)
            {
                SetRealtimeLocation(identity, zonePoiIds[0]);
            }
        }
    }

    private void HandleSpawnRelease(HuntsSocketMobIdentity identity, DateTimeOffset? timestampOverride = null)
    {
        EnsureWindowEntry(identity);

        var key = new HuntSpawnKey(identity.MobId, identity.WorldId, identity.ZoneInstance);
        bool alreadyReleased;
        lock (activeSpawnGate)
        {
            alreadyReleased = activeSpawnKeys.Contains(key) && !scheduledSpawnKeys.Contains(key);
            activeSpawnKeys.Add(key);
            scheduledSpawnKeys.Remove(key);
            if (!alreadyReleased)
            {
                activeSpawnTimestamps[key] = timestampOverride ?? DateTimeOffset.UtcNow;
            }

            activeSpawnVersion++;
        }

        if (alreadyReleased)
        {
            return;
        }

        var mob = mobCatalog.Find(identity.MobId);
        if (!NotificationSettings.IsEnabledFor(mob, identity.WorldId))
        {
            return;
        }

        var mobName = ResolveMobName(identity.MobId);
        var worldName = Prettify(identity.WorldId);
        notifications.Notify(new PhoneNotification(AppId, Loc.T(L.Hunts.SpawnReleaseNotifyTitle),
            string.Format(Loc.T(L.Hunts.SpawnReleaseNotifyBody), mobName, worldName), DateTime.Now,
            AppAccents.For(AppId), NotificationGroupKeyFor(key)));
    }

    private void HandleSpawnScheduled(HuntsSocketMobIdentity identity, DateTimeOffset? timestampOverride = null)
    {
        EnsureWindowEntry(identity);

        var key = new HuntSpawnKey(identity.MobId, identity.WorldId, identity.ZoneInstance);
        lock (activeSpawnGate)
        {
            if (activeSpawnKeys.Contains(key) && !scheduledSpawnKeys.Contains(key))
            {
                return;
            }

            activeSpawnKeys.Add(key);
            scheduledSpawnKeys.Add(key);
            if (!activeSpawnTimestamps.ContainsKey(key))
            {
                activeSpawnTimestamps[key] = timestampOverride ?? DateTimeOffset.UtcNow;
            }

            activeSpawnVersion++;
        }
    }

    private void ClearSpawnRelease(HuntsSocketMobIdentity identity)
    {
        var key = new HuntSpawnKey(identity.MobId, identity.WorldId, identity.ZoneInstance);
        ClearRealtimeLocation(key);
        ClearRealtimePhase(key);
        ClearRealtimeZone(key);
        ClearRealtimeReporters(key);

        bool wasActive;
        lock (activeSpawnGate)
        {
            scheduledSpawnKeys.Remove(key);
            activeSpawnTimestamps.Remove(key);
            wasActive = activeSpawnKeys.Remove(key);
            activeSpawnVersion++;
        }

        if (!wasActive)
        {
            return;
        }

        notifications.RemoveGroup(NotificationGroupKeyFor(key));
    }

    private void OnNotificationSettingsChanged()
    {
        List<HuntSpawnKey>? nowMuted = null;
        lock (activeSpawnGate)
        {
            foreach (var key in activeSpawnKeys)
            {
                if (NotificationSettings.IsEnabledFor(mobCatalog.Find(key.MobId), key.WorldId))
                {
                    continue;
                }

                nowMuted ??= new List<HuntSpawnKey>();
                nowMuted.Add(key);
            }
        }

        if (nowMuted is null)
        {
            return;
        }

        for (var index = 0; index < nowMuted.Count; index++)
        {
            notifications.RemoveGroup(NotificationGroupKeyFor(nowMuted[index]));
        }
    }

    private void ClearActiveSpawns()
    {
        realtimeSpawnLocations = new Dictionary<HuntSpawnKey, int>();
        realtimePhases = new Dictionary<HuntSpawnKey, (int WindowNum, int PhaseNum)>();
        realtimeZoneIds = new Dictionary<HuntSpawnKey, string>();
        realtimeReporterNames = new Dictionary<HuntSpawnKey, string[]>();
        lock (historyGate)
        {
            history = Array.Empty<HuntLogEntryDto>();
        }

        historyLoaded = false;
        historyLoading = false;

        HuntSpawnKey[] cleared;
        lock (activeSpawnGate)
        {
            scheduledSpawnKeys.Clear();
            activeSpawnTimestamps.Clear();
            activeSpawnVersion++;
            if (activeSpawnKeys.Count == 0)
            {
                return;
            }

            cleared = new HuntSpawnKey[activeSpawnKeys.Count];
            activeSpawnKeys.CopyTo(cleared);
            activeSpawnKeys.Clear();
        }

        for (var index = 0; index < cleared.Length; index++)
        {
            notifications.RemoveGroup(NotificationGroupKeyFor(cleared[index]));
        }
    }

    private void SetRealtimeLocation(HuntsSocketMobIdentity identity, int zonePoiId)
    {
        var key = new HuntSpawnKey(identity.MobId, identity.WorldId, identity.ZoneInstance);
        lock (realtimeStateGate)
        {
            var next = new Dictionary<HuntSpawnKey, int>(realtimeSpawnLocations) { [key] = zonePoiId };
            realtimeSpawnLocations = next;
        }
    }

    private void ClearRealtimeLocation(HuntSpawnKey key)
    {
        lock (realtimeStateGate)
        {
            if (!realtimeSpawnLocations.ContainsKey(key))
            {
                return;
            }

            var next = new Dictionary<HuntSpawnKey, int>(realtimeSpawnLocations);
            next.Remove(key);
            realtimeSpawnLocations = next;
        }
    }

    private void SetRealtimePhase(HuntsSocketMobIdentity identity)
    {
        var key = new HuntSpawnKey(identity.MobId, identity.WorldId, identity.ZoneInstance);
        var phase = (identity.WindowNum ?? 1, identity.PhaseNum ?? 1);
        lock (realtimeStateGate)
        {
            var next = new Dictionary<HuntSpawnKey, (int, int)>(realtimePhases) { [key] = phase };
            realtimePhases = next;
        }
    }

    private void ClearRealtimePhase(HuntSpawnKey key)
    {
        lock (realtimeStateGate)
        {
            if (!realtimePhases.ContainsKey(key))
            {
                return;
            }

            var next = new Dictionary<HuntSpawnKey, (int WindowNum, int PhaseNum)>(realtimePhases);
            next.Remove(key);
            realtimePhases = next;
        }
    }

    private void SetRealtimeZone(HuntsSocketMobIdentity identity, string? zoneId)
    {
        var resolvedZoneId = zoneId;
        if (string.IsNullOrEmpty(resolvedZoneId) && mobCatalog.Find(identity.MobId) is { ZoneIds.Length: > 0 } mob)
        {
            resolvedZoneId = mob.ZoneIds[0];
        }

        if (string.IsNullOrEmpty(resolvedZoneId))
        {
            return;
        }

        var key = new HuntSpawnKey(identity.MobId, identity.WorldId, identity.ZoneInstance);
        lock (realtimeStateGate)
        {
            var next = new Dictionary<HuntSpawnKey, string>(realtimeZoneIds) { [key] = resolvedZoneId };
            realtimeZoneIds = next;
        }
    }

    private void ClearRealtimeZone(HuntSpawnKey key)
    {
        lock (realtimeStateGate)
        {
            if (!realtimeZoneIds.ContainsKey(key))
            {
                return;
            }

            var next = new Dictionary<HuntSpawnKey, string>(realtimeZoneIds);
            next.Remove(key);
            realtimeZoneIds = next;
        }
    }

    private void SetRealtimeReporters(HuntsSocketMobIdentity identity, HuntsSocketReporter[] reporters)
    {
        var names = new List<string>(reporters.Length);
        for (var index = 0; index < reporters.Length; index++)
        {
            var name = reporters[index].Name;
            if (!string.IsNullOrEmpty(name))
            {
                names.Add(name);
            }
        }

        if (names.Count == 0)
        {
            return;
        }

        var key = new HuntSpawnKey(identity.MobId, identity.WorldId, identity.ZoneInstance);
        lock (realtimeStateGate)
        {
            var next = new Dictionary<HuntSpawnKey, string[]>(realtimeReporterNames) { [key] = names.ToArray() };
            realtimeReporterNames = next;
        }
    }

    private void ClearRealtimeReporters(HuntSpawnKey key)
    {
        lock (realtimeStateGate)
        {
            if (!realtimeReporterNames.ContainsKey(key))
            {
                return;
            }

            var next = new Dictionary<HuntSpawnKey, string[]>(realtimeReporterNames);
            next.Remove(key);
            realtimeReporterNames = next;
        }
    }

    private static string NotificationGroupKeyFor(HuntSpawnKey key) =>
        string.Create(CultureInfo.InvariantCulture,
            $"{AppId}:{key.MobId}:{key.WorldId.ToLowerInvariant()}:{key.ZoneInstance}");

    public static bool TryParseGroupKey(string groupKey, out string mobId, out string worldId, out int zoneInstance)
    {
        mobId = string.Empty;
        worldId = string.Empty;
        zoneInstance = 0;
        var parts = groupKey.Split(':');
        if (parts.Length != 4 || !string.Equals(parts[0], AppId, StringComparison.Ordinal))
        {
            return false;
        }

        if (!int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out zoneInstance))
        {
            return false;
        }

        mobId = parts[1];
        worldId = parts[2];
        return true;
    }

    private string ResolveMobName(string mobId) =>
        mobCatalog.Find(mobId) is { } def
            ? def.Name.GetValueOrDefault(Loc.Current.Code) ?? def.Name.GetValueOrDefault("en") ?? Prettify(mobId)
            : Prettify(mobId);

    private static string Prettify(string slug)
    {
        if (slug.Length == 0)
        {
            return slug;
        }

        var parts = slug.Split('_', StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < parts.Length; index++)
        {
            var part = parts[index];
            parts[index] = part.Length > 0 ? char.ToUpperInvariant(part[0]) + part[1..] : part;
        }

        return string.Join(' ', parts);
    }

    private void OnMobWorldKillReceived(HuntLogEntryDto entry)
    {
        var dataCenter = currentDataCenter;
        if (dataCenter is null || !ContainsWorld(HuntDataCenterWorlds.WorldsFor(dataCenter), entry.WorldId))
        {
            return;
        }

        if (mobCatalog.Find(entry.MobId) is not { Rank: "S" or "F" })
        {
            return;
        }

        lock (historyGate)
        {
            var current = history;
            for (var index = 0; index < current.Length; index++)
            {
                if (current[index].Id == entry.Id)
                {
                    return;
                }
            }

            var next = new HuntLogEntryDto[Math.Min(current.Length + 1, MaxHistoryEntries)];
            next[0] = entry;
            Array.Copy(current, 0, next, 1, next.Length - 1);
            history = next;
        }
    }

    private void ApplyDeathReport(HuntsSocketMobIdentity identity, HuntsSocketMobData data)
    {
        ClearSpawnRelease(identity);

        var num = data.Num > 0 ? data.Num : 1;
        var startedAtNormal = data.StartedAt;
        var startedAtSniped = data.StartedAtSniped;
        var useMaintenanceTiming = false;
        if (startedAtNormal is null && startedAtSniped is null)
        {
            if (!TryResolveSnipedFallback(identity, num, data.SnipedNum, out startedAtNormal, out startedAtSniped,
                    out useMaintenanceTiming))
            {
                return;
            }
        }

        var window = new HuntWindowDto
        {
            Num = num,
            StartedAtNormal = startedAtNormal,
            StartedAtSniped = startedAtSniped,
            PrevStartedAt = data.PrevStartedAt,
            SnipedNum = data.SnipedNum,
            UseMaintenanceTiming = useMaintenanceTiming,
            MobId = identity.MobId,
            WorldId = identity.WorldId,
            ZoneInstance = identity.ZoneInstance,
        };

        lock (windowsGate)
        {
            var current = windows;
            var index = FindWindowIndex(current, identity);
            if (index >= 0)
            {
                current[index] = window;
                return;
            }

            var appended = new HuntWindowDto[current.Length + 1];
            Array.Copy(current, appended, current.Length);
            appended[current.Length] = window;
            windows = appended;
        }
    }

    private void EnsureWindowEntriesForActiveSpawns()
    {
        HuntSpawnKey[] keys;
        lock (activeSpawnGate)
        {
            keys = new HuntSpawnKey[activeSpawnKeys.Count];
            activeSpawnKeys.CopyTo(keys);
        }

        for (var index = 0; index < keys.Length; index++)
        {
            var key = keys[index];
            EnsureWindowEntry(new HuntsSocketMobIdentity
            {
                MobId = key.MobId,
                WorldId = key.WorldId,
                ZoneInstance = key.ZoneInstance,
            });
        }
    }

    private void EnsureWindowEntry(HuntsSocketMobIdentity identity)
    {
        lock (windowsGate)
        {
            var current = windows;
            if (FindExactWindowIndex(current, identity) >= 0)
            {
                return;
            }

            var appended = new HuntWindowDto[current.Length + 1];
            Array.Copy(current, appended, current.Length);
            appended[current.Length] = new HuntWindowDto
            {
                MobId = identity.MobId,
                WorldId = identity.WorldId,
                ZoneInstance = identity.ZoneInstance,
            };
            windows = appended;
        }
    }

    private static int FindExactWindowIndex(HuntWindowDto[] current, HuntsSocketMobIdentity identity)
    {
        for (var index = 0; index < current.Length; index++)
        {
            var candidate = current[index];
            if (string.Equals(candidate.MobId, identity.MobId, StringComparison.Ordinal) &&
                string.Equals(candidate.WorldId, identity.WorldId, StringComparison.OrdinalIgnoreCase) &&
                candidate.ZoneInstance == identity.ZoneInstance)
            {
                return index;
            }
        }

        return -1;
    }

    private static int FindWindowIndex(HuntWindowDto[] current, HuntsSocketMobIdentity identity)
    {
        for (var index = 0; index < current.Length; index++)
        {
            var candidate = current[index];
            if (!string.Equals(candidate.MobId, identity.MobId, StringComparison.Ordinal) ||
                !string.Equals(candidate.WorldId, identity.WorldId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (identity.ZoneInstance == 0 || candidate.ZoneInstance == 0 ||
                candidate.ZoneInstance == identity.ZoneInstance)
            {
                return index;
            }
        }

        return -1;
    }

    private bool TryResolveSnipedFallback(HuntsSocketMobIdentity identity, int num, int? snipedNum,
        out DateTimeOffset? startedAtNormal, out DateTimeOffset? startedAtSniped, out bool useMaintenanceTiming)
    {
        startedAtNormal = null;
        startedAtSniped = null;
        useMaintenanceTiming = false;

        if (maintenanceStartAt is not { } maintenanceStart)
        {
            return false;
        }

        var worldMaintenanceStart = ResolveMaintenanceStart(identity.WorldId, maintenanceStart);
        if (snipedNum is not { } snipes)
        {
            startedAtNormal = worldMaintenanceStart;
            useMaintenanceTiming = true;
            return true;
        }

        var mob = mobCatalog.Find(identity.MobId);
        var index = num - 1;
        var windowDef = mob is { Windows.Length: > 0 }
            ? (index >= 0 && index < mob.Windows.Length ? mob.Windows[index] : mob.Windows[0])
            : null;
        if (windowDef?.Timing?.Normal is not { } normal)
        {
            startedAtNormal = worldMaintenanceStart;
            useMaintenanceTiming = true;
            return true;
        }

        var maintenanceMin = windowDef.Timing.Maintenance?.Min ?? normal.Min;
        var shiftHours = maintenanceMin + normal.Min * (snipes - 1);
        startedAtSniped = worldMaintenanceStart + TimeSpan.FromHours(shiftHours);
        return true;
    }

    private static bool ContainsWorld(string[] worlds, string worldId)
    {
        for (var index = 0; index < worlds.Length; index++)
        {
            if (string.Equals(worlds[index], worldId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public void Dispose()
    {
        characterWatch.Changed -= OnCharacterChanged;
        realtimeClient.MobReportReceived -= OnMobReportReceived;
        realtimeClient.MobWorldKillReceived -= OnMobWorldKillReceived;
        realtimeClient.ConnectedChanged -= OnRealtimeConnectedChanged;
        NotificationSettings.Changed -= OnNotificationSettingsChanged;
        loginFlow.Succeeded -= OnLoginSucceeded;
        realtimeClient.Dispose();
        work.Dispose();
    }
}
