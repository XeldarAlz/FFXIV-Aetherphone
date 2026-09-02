using Aetherphone.Core.Hunts;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;
using UIState = FFXIVClientStructs.FFXIV.Client.Game.UI.UIState;

namespace Aetherphone.Core.Maps;

internal sealed class HuntsMapMarkers : IDisposable
{
    private const int MarkerScale = 600;
    private const int FateMarkerScale = 200;
    private const string AreaMapAddonName = "AreaMap";
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(2);

    private readonly Configuration configuration;
    private readonly HuntsService hunts;
    private readonly HuntMobCatalog mobCatalog;
    private readonly HuntZoneCatalog zoneCatalog;
    private readonly HuntCandidateCache candidateCache;
    private readonly List<HuntsMapMarkerPoint> points = new();
    private readonly HashSet<HuntsMapMarkerPoint> lastPlacedPoints = new();
    private bool hasPlacedMarkers;
    private uint cachedTerritoryId;
    private string cachedWorldId = string.Empty;
    private uint cachedWorldRowId;
    private string cachedWorldSlug = string.Empty;
    private DateTime lastRefreshUtc = DateTime.MinValue;
    private bool forceRedraw = true;

    public bool HasActiveMarkers => hasPlacedMarkers;
    public int? ShownInstance { get; private set; }

    public HuntsMapMarkers(Configuration configuration, HuntsService hunts, HuntMobCatalog mobCatalog,
        HuntZoneCatalog zoneCatalog, HuntCandidateCache candidateCache)
    {
        this.configuration = configuration;
        this.hunts = hunts;
        this.mobCatalog = mobCatalog;
        this.zoneCatalog = zoneCatalog;
        this.candidateCache = candidateCache;
        Plugin.Framework.Update += OnFrameworkUpdate;
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, AreaMapAddonName, OnAreaMapOpenedOrChanged);
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostRefresh, AreaMapAddonName, OnAreaMapOpenedOrChanged);
    }

    public unsafe void Dispose()
    {
        Plugin.Framework.Update -= OnFrameworkUpdate;
        Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, AreaMapAddonName, OnAreaMapOpenedOrChanged);
        Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PostRefresh, AreaMapAddonName, OnAreaMapOpenedOrChanged);

        var agentMap = AgentMap.Instance();
        if (agentMap != null && hasPlacedMarkers)
        {
            agentMap->ResetMapMarkers();
            agentMap->ResetMiniMapMarkers();
        }
    }

    private void OnAreaMapOpenedOrChanged(AddonEvent type, AddonArgs args)
    {
        lastRefreshUtc = DateTime.MinValue;
        forceRedraw = true;
    }

    public void ForceRedraw()
    {
        lastRefreshUtc = DateTime.MinValue;
        forceRedraw = true;
    }

    private unsafe void OnFrameworkUpdate(IFramework framework)
    {
        var agentMap = AgentMap.Instance();
        if (agentMap == null)
        {
            return;
        }

        if (!configuration.HuntsNativeMapMarkers)
        {
            ClearNativeMarkersIfNeeded(agentMap);
            return;
        }

        var territoryId = agentMap->SelectedTerritoryId;
        var worldRowId = LocationShare.CurrentWorldId();
        if (worldRowId != cachedWorldRowId)
        {
            cachedWorldRowId = worldRowId;
            cachedWorldSlug = HuntDataCenterWorlds.SlugFor(worldRowId);
        }

        var worldId = cachedWorldSlug;
        var contextChanged = territoryId != cachedTerritoryId ||
            !string.Equals(worldId, cachedWorldId, StringComparison.OrdinalIgnoreCase);
        if (contextChanged)
        {
            forceRedraw = true;
        }

        if (!contextChanged && !forceRedraw && DateTime.UtcNow - lastRefreshUtc < RefreshInterval)
        {
            return;
        }

        cachedTerritoryId = territoryId;
        cachedWorldId = worldId;
        lastRefreshUtc = DateTime.UtcNow;
        var mustRedraw = forceRedraw;
        forceRedraw = false;

        if (!TryResolveTarget(territoryId, worldId, out var map))
        {
            ClearNativeMarkersIfNeeded(agentMap);
            return;
        }

        if (!mustRedraw && hasPlacedMarkers && points.Count == lastPlacedPoints.Count &&
            lastPlacedPoints.SetEquals(points))
        {
            return;
        }

        agentMap->ResetMapMarkers();
        agentMap->ResetMiniMapMarkers();
        hasPlacedMarkers = true;
        lastPlacedPoints.Clear();

        for (var index = 0; index < points.Count; index++)
        {
            var point = points[index];
            lastPlacedPoints.Add(point);
            var worldX = MapPixelMath.ToWorldCoordinate(point.RawX, map.SizeFactor, map.OffsetX);
            var worldZ = MapPixelMath.ToWorldCoordinate(point.RawY, map.SizeFactor, map.OffsetY);
            var worldPosition = new Vector3(worldX, 0f, worldZ);
            var iconId = HuntsMapMarkerIcons.IconFor(point.State);
            var scale = ScaleFor(point.State);
            agentMap->AddMapMarker(worldPosition, iconId, scale);
            agentMap->AddMiniMapMarker(worldPosition, iconId, scale);
        }
    }

    private unsafe bool TryResolveTarget(uint territoryId, string worldId, out Map map)
    {
        map = default;

        var zoneId = zoneCatalog.ZoneIdForTerritory(territoryId);
        if (zoneId is not { Length: > 0 } || worldId.Length == 0)
        {
            return false;
        }

        var explicitInstance = ResolveExplicitInstance(territoryId);
        HuntCandidateResolver.ResolveZoneMarkers(zoneId, worldId, explicitInstance, mobCatalog, zoneCatalog,
            candidateCache, hunts, points, out var shownInstance);
        ShownInstance = shownInstance;
        if (points.Count == 0)
        {
            return false;
        }

        if (!Plugin.DataManager.GetExcelSheet<TerritoryType>().TryGetRow(territoryId, out var territory) ||
            !Plugin.DataManager.GetExcelSheet<Map>().TryGetRow(territory.Map.RowId, out var resolvedMap))
        {
            return false;
        }

        map = resolvedMap;
        return true;
    }

    private unsafe void ClearNativeMarkersIfNeeded(AgentMap* agentMap)
    {
        ShownInstance = null;
        if (!hasPlacedMarkers)
        {
            return;
        }

        agentMap->ResetMapMarkers();
        agentMap->ResetMiniMapMarkers();
        hasPlacedMarkers = false;
        lastPlacedPoints.Clear();
    }

    private static unsafe int? ResolveExplicitInstance(uint territoryId)
    {
        if (Plugin.ClientState.TerritoryType != territoryId)
        {
            return null;
        }

        var uiState = UIState.Instance();
        if (uiState == null)
        {
            return null;
        }

        var instanceId = (int)uiState->PublicInstance.InstanceId;
        return instanceId == 0 ? null : instanceId;
    }

    private static int ScaleFor(HuntsMapMarkerState state) => state switch
    {
        HuntsMapMarkerState.FateInactive => FateMarkerScale,
        HuntsMapMarkerState.FateActive => FateMarkerScale,
        _ => MarkerScale,
    };
}
