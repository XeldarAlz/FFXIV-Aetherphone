namespace Aetherphone.Core.Hunts;

internal readonly record struct HuntPoiState(HuntPoiEntry Poi, HuntsMapMarkerState State);

internal static class HuntCandidateResolver
{
    private const string MobPoiType = "mob";

    public static HashSet<int> ResolveCandidatePoiIds(HuntMobDefinition mob,
        (int WindowNum, int PhaseNum)? activePhase, out bool finalPhase)
    {
        var poiIds = new HashSet<int>();
        finalPhase = false;
        if (activePhase is { } phase && mob.Windows.Length > 0)
        {
            var windowIndex = Math.Clamp(phase.WindowNum - 1, 0, mob.Windows.Length - 1);
            var phases = mob.Windows[windowIndex].Phases;
            if (phases.Length > 0)
            {
                var phaseIndex = Math.Clamp(phase.PhaseNum - 1, 0, phases.Length - 1);
                var activePhaseEntry = phases[phaseIndex];
                if (OwnsPhase(mob, activePhaseEntry))
                {
                    finalPhase = phaseIndex > 0;
                    AddPoiIds(poiIds, activePhaseEntry.ZonePoiIds);
                }
            }

            return poiIds;
        }

        for (var windowIndex = 0; windowIndex < mob.Windows.Length; windowIndex++)
        {
            var phases = mob.Windows[windowIndex].Phases;
            for (var phaseIndex = 0; phaseIndex < phases.Length; phaseIndex++)
            {
                var phaseEntry = phases[phaseIndex];
                if (!OwnsPhase(mob, phaseEntry))
                {
                    continue;
                }

                if (mob.Rank == "SS" && phases.Length > 1 && phaseIndex == phases.Length - 1)
                {
                    continue;
                }

                AddPoiIds(poiIds, phaseEntry.ZonePoiIds);
            }
        }

        return poiIds;
    }

    private static bool OwnsPhase(HuntMobDefinition mob, HuntMobPhase phase) =>
        phase.MobId is null || string.Equals(phase.MobId, mob.Id, StringComparison.Ordinal);

    public static HuntPoiEntry? ResolveFinalPhasePoint(HuntMobDefinition mob,
        (int WindowNum, int PhaseNum)? activePhase, string zoneId, HuntZoneCatalog zoneCatalog)
    {
        if (mob.Rank != "SS" || zoneId.Length == 0 || mob.Windows.Length == 0)
        {
            return null;
        }

        var windowIndex = activePhase is { } phase
            ? Math.Clamp(phase.WindowNum - 1, 0, mob.Windows.Length - 1)
            : 0;
        var phases = mob.Windows[windowIndex].Phases;
        if (phases.Length < 2)
        {
            return null;
        }

        var finalPhasePoiIds = phases[^1].ZonePoiIds;
        for (var poiIndex = 0; poiIndex < finalPhasePoiIds.Length; poiIndex++)
        {
            if (zoneCatalog.FindPoi(finalPhasePoiIds[poiIndex]) is { } resolved &&
                string.Equals(resolved.ZoneId, zoneId, StringComparison.Ordinal))
            {
                return resolved.Poi;
            }
        }

        return null;
    }

    public static string ResolveBestZoneId(HuntMobDefinition mob, string worldId, int zoneInstance,
        HuntZoneCatalog zoneCatalog, HuntsService hunts, out bool zoneConfirmed)
    {
        var confirmedZoneId = hunts.ZoneIdFor(mob.Id, worldId, zoneInstance);
        if (confirmedZoneId is { Length: > 0 } && Array.IndexOf(mob.ZoneIds, confirmedZoneId) >= 0)
        {
            zoneConfirmed = true;
            return confirmedZoneId;
        }

        zoneConfirmed = false;
        if (mob.ZoneIds.Length <= 1)
        {
            return mob.ZoneIds.Length == 1 ? mob.ZoneIds[0] : string.Empty;
        }

        var activePhase = hunts.PhaseFor(mob.Id, worldId, zoneInstance);
        var poiIds = ResolveCandidatePoiIds(mob, activePhase, out _);
        var bestZoneId = mob.ZoneIds[0];
        var bestCount = -1;
        for (var zoneIndex = 0; zoneIndex < mob.ZoneIds.Length; zoneIndex++)
        {
            var candidateZoneId = mob.ZoneIds[zoneIndex];
            var count = 0;
            foreach (var poiId in poiIds)
            {
                if (zoneCatalog.FindPoi(poiId) is { } resolved &&
                    string.Equals(resolved.ZoneId, candidateZoneId, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            if (count > bestCount)
            {
                bestCount = count;
                bestZoneId = candidateZoneId;
            }
        }

        return bestZoneId;
    }

    public static void ResolveMobZoneStates(HuntMobDefinition mob, string worldId, int zoneInstance,
        string targetZoneId, HuntZoneCatalog zoneCatalog, HuntsService hunts,
        List<HuntPoiState> results, out int? reportedPoiId)
    {
        results.Clear();
        reportedPoiId = null;
        if (targetZoneId.Length == 0 || Array.IndexOf(mob.ZoneIds, targetZoneId) < 0)
        {
            return;
        }

        var confirmedZoneId = hunts.ZoneIdFor(mob.Id, worldId, zoneInstance);
        var zoneConfirmed = string.Equals(confirmedZoneId, targetZoneId, StringComparison.Ordinal);
        if (confirmedZoneId is { Length: > 0 } && !zoneConfirmed)
        {
            return;
        }

        if (mob.Rank == "F")
        {
            ResolveFateStates(mob, worldId, zoneInstance, targetZoneId, zoneCatalog, hunts, results);
            return;
        }

        if (mob.Rank == "SS" && !zoneConfirmed)
        {
            return;
        }

        var activePhase = hunts.PhaseFor(mob.Id, worldId, zoneInstance);
        var poiIds = ResolveCandidatePoiIds(mob, activePhase, out var finalPhase);

        var points = new List<HuntPoiEntry>();
        foreach (var poiId in poiIds)
        {
            if (zoneCatalog.FindPoi(poiId) is { } resolved &&
                string.Equals(resolved.ZoneId, targetZoneId, StringComparison.Ordinal))
            {
                points.Add(resolved.Poi);
            }
        }

        var spawned = hunts.IsSpawned(mob.Id, worldId, zoneInstance);
        var finalLocationResolved = spawned && finalPhase && zoneConfirmed && points.Count == 1;
        var isActiveSsMinion = mob.Rank == "SS" && zoneConfirmed && !finalPhase;
        var isActiveSsSpawn = mob.Rank == "SS" && zoneConfirmed && finalPhase;

        if (spawned)
        {
            reportedPoiId = hunts.ConfirmedPoiIdFor(mob.Id, worldId, zoneInstance);
            if (reportedPoiId is null && finalLocationResolved)
            {
                reportedPoiId = points[0].Id;
            }
        }

        var unsightedCount = 0;
        var soleUnsightedPoiId = 0;
        for (var index = 0; index < points.Count; index++)
        {
            if (hunts.IsPoiSighted(mob.Id, worldId, zoneInstance, points[index].Id))
            {
                continue;
            }

            unsightedCount++;
            soleUnsightedPoiId = points[index].Id;
        }

        var confirmedPoiId = reportedPoiId ?? (unsightedCount == 1 ? soleUnsightedPoiId : (int?)null);
        for (var index = 0; index < points.Count; index++)
        {
            var poi = points[index];
            HuntsMapMarkerState state;
            if (confirmedPoiId is { } confirmed)
            {
                state = poi.Id == confirmed ? HuntsMapMarkerState.Confirmed : HuntsMapMarkerState.Sighted;
            }
            else if (hunts.IsPoiSighted(mob.Id, worldId, zoneInstance, poi.Id))
            {
                state = HuntsMapMarkerState.Sighted;
            }
            else if (isActiveSsMinion)
            {
                state = HuntsMapMarkerState.ActiveMinion;
            }
            else if (isActiveSsSpawn)
            {
                state = HuntsMapMarkerState.SsSpawn;
            }
            else
            {
                state = HuntsMapMarkerState.Candidate;
            }

            results.Add(new HuntPoiState(poi, state));
        }

        if (mob.Rank == "SS" && zoneConfirmed &&
            ResolveFinalPhasePoint(mob, activePhase, targetZoneId, zoneCatalog) is { } finalPoint &&
            results.TrueForAll(existing => existing.Poi.Id != finalPoint.Id))
        {
            HuntsMapMarkerState previewState;
            if (reportedPoiId == finalPoint.Id)
            {
                previewState = HuntsMapMarkerState.Confirmed;
            }
            else if (reportedPoiId is not null || hunts.IsPoiSighted(mob.Id, worldId, zoneInstance, finalPoint.Id))
            {
                previewState = HuntsMapMarkerState.Sighted;
            }
            else
            {
                previewState = HuntsMapMarkerState.SsSpawn;
            }

            results.Add(new HuntPoiState(finalPoint, previewState));
        }
    }

    public static void AppendLandmineOnlyStates(HuntMobDefinition mob, string targetZoneId,
        HuntMobCatalog mobCatalog, HuntZoneCatalog zoneCatalog, List<HuntPoiState> results)
    {
        if (mob.Rank != "S" || Array.IndexOf(mob.ZoneIds, targetZoneId) < 0 ||
            zoneCatalog.FindZone(targetZoneId) is not { } zone)
        {
            return;
        }

        var zonePois = zone.Pois;
        for (var index = 0; index < zonePois.Length; index++)
        {
            var poi = zonePois[index];
            if (!string.Equals(poi.Type, MobPoiType, StringComparison.Ordinal) ||
                !mobCatalog.IsLandminePoi(poi.Id) || !results.TrueForAll(existing => existing.Poi.Id != poi.Id))
            {
                continue;
            }

            results.Add(new HuntPoiState(poi, HuntsMapMarkerState.Sighted));
        }
    }

    public static List<HuntPoiState> ResolveLandmineOnlyStates(HuntMobDefinition mob, string targetZoneId,
        HuntMobCatalog mobCatalog, HuntZoneCatalog zoneCatalog)
    {
        var states = new List<HuntPoiState>();
        AppendLandmineOnlyStates(mob, targetZoneId, mobCatalog, zoneCatalog, states);
        return states;
    }

    private static void ResolveFateStates(HuntMobDefinition mob, string worldId, int zoneInstance,
        string targetZoneId, HuntZoneCatalog zoneCatalog, HuntsService hunts, List<HuntPoiState> results)
    {
        var fateState = hunts.IsSpawned(mob.Id, worldId, zoneInstance)
            ? HuntsMapMarkerState.FateActive
            : HuntsMapMarkerState.FateInactive;
        for (var windowIndex = 0; windowIndex < mob.Windows.Length; windowIndex++)
        {
            var phases = mob.Windows[windowIndex].Phases;
            for (var phaseIndex = 0; phaseIndex < phases.Length; phaseIndex++)
            {
                var zonePoiIds = phases[phaseIndex].ZonePoiIds;
                for (var poiIndex = 0; poiIndex < zonePoiIds.Length; poiIndex++)
                {
                    if (zoneCatalog.FindPoi(zonePoiIds[poiIndex]) is { } resolved &&
                        string.Equals(resolved.ZoneId, targetZoneId, StringComparison.Ordinal) &&
                        results.TrueForAll(existing => existing.Poi.Id != resolved.Poi.Id))
                    {
                        results.Add(new HuntPoiState(resolved.Poi, fateState));
                    }
                }
            }
        }
    }

    private static void AddPoiIds(HashSet<int> poiIds, int[] zonePoiIds)
    {
        for (var index = 0; index < zonePoiIds.Length; index++)
        {
            poiIds.Add(zonePoiIds[index]);
        }
    }

    public static void ResolveZoneMarkers(string zoneId, string worldId, int? explicitInstance,
        HuntMobCatalog mobCatalog, HuntZoneCatalog zoneCatalog, HuntCandidateCache candidateCache, HuntsService hunts,
        List<HuntsMapMarkerPoint> results, out int? shownInstance)
    {
        results.Clear();
        shownInstance = null;
        if (worldId.Length == 0)
        {
            return;
        }

        var statesByInstance = new Dictionary<int, Dictionary<int, (HuntsMapMarkerState State, int Priority)>>();
        var windows = hunts.Windows;
        for (var index = 0; index < windows.Length; index++)
        {
            var window = windows[index];
            if (!string.Equals(window.WorldId, worldId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var mob = mobCatalog.Find(window.MobId);
            if (mob is null)
            {
                continue;
            }

            var (states, _) = candidateCache.ResolveFor(mob, window.WorldId, window.ZoneInstance, zoneId,
                includeLandmineOnlySpots: true);
            if (states.Count == 0)
            {
                continue;
            }

            if (!statesByInstance.TryGetValue(window.ZoneInstance, out var statesByPoiId))
            {
                statesByPoiId = new Dictionary<int, (HuntsMapMarkerState, int)>();
                statesByInstance[window.ZoneInstance] = statesByPoiId;
            }

            var rankTier = RankTier(mob.Rank);
            for (var stateIndex = 0; stateIndex < states.Count; stateIndex++)
            {
                var (poi, state) = states[stateIndex];
                var priority = rankTier * TrackedRankPriorityBoost + Priority(state);
                if (!statesByPoiId.TryGetValue(poi.Id, out var existing) || priority > existing.Priority)
                {
                    statesByPoiId[poi.Id] = (state, priority);
                }
            }
        }

        var zoneIsInstanced = hunts.ZoneInstanceCountFor(zoneId) > 1;
        int targetInstance;
        if (explicitInstance is { } known)
        {
            targetInstance = known;
        }
        else if (zoneIsInstanced)
        {
            targetInstance = 1;
        }
        else
        {
            targetInstance = 0;
        }

        if (zoneIsInstanced)
        {
            shownInstance = targetInstance;
        }

        if (!statesByInstance.TryGetValue(targetInstance, out var chosen))
        {
            return;
        }

        foreach (var entry in chosen)
        {
            if (zoneCatalog.FindPoi(entry.Key) is not { } resolved)
            {
                continue;
            }

            var (rawX, rawY) = resolved.Poi.ParsedLocation();
            results.Add(new HuntsMapMarkerPoint(rawX, rawY, entry.Value.State));
        }
    }

    private const int TrackedRankPriorityBoost = 10;

    private static int RankTier(string rank) => rank is "S" or "SS" or "F" ? 1 : 0;

    public static int Priority(HuntsMapMarkerState state) => state switch
    {
        HuntsMapMarkerState.Confirmed => 3,
        HuntsMapMarkerState.Sighted => 2,
        HuntsMapMarkerState.ActiveMinion => 1,
        HuntsMapMarkerState.SsSpawn => 1,
        HuntsMapMarkerState.FateActive => 1,
        _ => 0,
    };
}
