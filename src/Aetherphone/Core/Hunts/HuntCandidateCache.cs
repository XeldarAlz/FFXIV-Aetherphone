namespace Aetherphone.Core.Hunts;

internal sealed class HuntCandidateCache
{
    private const int MaxEntries = 256;

    private readonly HuntMobCatalog mobCatalog;
    private readonly HuntZoneCatalog zoneCatalog;
    private readonly HuntsService hunts;
    private readonly object entriesGate = new();
    private readonly Dictionary<(string MobId, string WorldId, int ZoneInstance, string ZoneId), Entry> entries = new();
    private readonly Dictionary<(string MobId, string ZoneId), List<HuntPoiState>> landmineOnlyStates = new();

    private readonly struct Entry
    {
        public readonly HuntCandidateStateToken Token;
        public readonly List<HuntPoiState> States;
        public readonly int? ReportedPoiId;

        public Entry(HuntCandidateStateToken token, List<HuntPoiState> states, int? reportedPoiId)
        {
            Token = token;
            States = states;
            ReportedPoiId = reportedPoiId;
        }
    }

    public HuntCandidateCache(HuntMobCatalog mobCatalog, HuntZoneCatalog zoneCatalog, HuntsService hunts)
    {
        this.mobCatalog = mobCatalog;
        this.zoneCatalog = zoneCatalog;
        this.hunts = hunts;
    }

    public (IReadOnlyList<HuntPoiState> States, int? ReportedPoiId) ResolveFor(HuntMobDefinition mob,
        string worldId, int zoneInstance, string zoneId, bool includeLandmineOnlySpots)
    {
        var token = hunts.CandidateStateToken;
        var key = (mob.Id, worldId, zoneInstance, zoneId);
        Entry entry;
        lock (entriesGate)
        {
            if (!entries.TryGetValue(key, out entry) || !entry.Token.Equals(token))
            {
                if (entries.Count >= MaxEntries)
                {
                    entries.Clear();
                }

                var states = new List<HuntPoiState>();
                HuntCandidateResolver.ResolveMobZoneStates(mob, worldId, zoneInstance, zoneId, zoneCatalog, hunts,
                    states, out var reportedPoiId);
                entry = new Entry(token, states, reportedPoiId);
                entries[key] = entry;
            }
        }

        if (!includeLandmineOnlySpots)
        {
            return (entry.States, entry.ReportedPoiId);
        }

        var landmineStates = GetLandmineOnlyStates(mob, zoneId);
        if (landmineStates.Count == 0)
        {
            return (entry.States, entry.ReportedPoiId);
        }

        var withLandmines = new List<HuntPoiState>(entry.States);
        for (var landmineIndex = 0; landmineIndex < landmineStates.Count; landmineIndex++)
        {
            var landmine = landmineStates[landmineIndex];
            var alreadyPresent = false;
            for (var existingIndex = 0; existingIndex < withLandmines.Count; existingIndex++)
            {
                if (withLandmines[existingIndex].Poi.Id == landmine.Poi.Id)
                {
                    alreadyPresent = true;
                    break;
                }
            }

            if (!alreadyPresent)
            {
                withLandmines.Add(landmine);
            }
        }

        return (withLandmines, entry.ReportedPoiId);
    }

    private List<HuntPoiState> GetLandmineOnlyStates(HuntMobDefinition mob, string zoneId)
    {
        var key = (mob.Id, zoneId);
        lock (entriesGate)
        {
            if (landmineOnlyStates.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var states = HuntCandidateResolver.ResolveLandmineOnlyStates(mob, zoneId, mobCatalog, zoneCatalog);
            landmineOnlyStates[key] = states;
            return states;
        }
    }
}
