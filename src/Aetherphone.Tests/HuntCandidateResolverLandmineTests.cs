using Aetherphone.Core.Hunts;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HuntCandidateResolverLandmineTests
{
    private static FileInfo MobSource() => new(Path.Combine(AppContext.BaseDirectory, "Hunts", "HuntMob.json"));
    private static FileInfo PoiSource() => new(Path.Combine(AppContext.BaseDirectory, "Hunts", "HuntPOI.json"));

    [Fact]
    public void LandmineAppendNeverOverwritesAnSRankSOwnPoiState()
    {
        var mobCatalog = new HuntMobCatalog(MobSource());
        var zoneCatalog = new HuntZoneCatalog(PoiSource());

        var agrippa = mobCatalog.Find("agrippa_the_mighty")!;
        var poiIds = HuntCandidateResolver.ResolveCandidatePoiIds(agrippa, null, out _);

        var results = new List<HuntPoiState>();
        foreach (var poiId in poiIds)
        {
            if (zoneCatalog.FindPoi(poiId) is { } resolved)
            {
                results.Add(new HuntPoiState(resolved.Poi, HuntsMapMarkerState.Candidate));
            }
        }

        HuntCandidateResolver.AppendLandmineOnlyStates(agrippa, "mor_dhona", mobCatalog, zoneCatalog, results);

        foreach (var poiId in poiIds)
        {
            var match = Assert.Single(results, r => r.Poi.Id == poiId);
            Assert.Equal(HuntsMapMarkerState.Candidate, match.State);
        }
    }

    [Fact]
    public void LandmineAppendDoesNothingForAnSRankMobThatDoesNotOwnTheZone()
    {
        var mobCatalog = new HuntMobCatalog(MobSource());
        var zoneCatalog = new HuntZoneCatalog(PoiSource());

        var unrelatedSRank = mobCatalog.Find("zona_seeker")!;
        Assert.DoesNotContain("mor_dhona", unrelatedSRank.ZoneIds);

        var results = new List<HuntPoiState>();
        HuntCandidateResolver.AppendLandmineOnlyStates(unrelatedSRank, "mor_dhona", mobCatalog, zoneCatalog, results);

        Assert.Empty(results);
    }

    [Fact]
    public void CrossMobMergeGivesTrackedRankPriorityOverLandmineOnlyRanks()
    {
        var mobCatalog = new HuntMobCatalog(MobSource());
        var zoneCatalog = new HuntZoneCatalog(PoiSource());

        var agrippa = mobCatalog.Find("agrippa_the_mighty")!;
        var kurrea = mobCatalog.Find("kurrea")!;

        var agrippaPoiIds = HuntCandidateResolver.ResolveCandidatePoiIds(agrippa, null, out _);
        var kurreaPoiIds = HuntCandidateResolver.ResolveCandidatePoiIds(kurrea, null, out _);

        var sharedPoiId = agrippaPoiIds.First(id => kurreaPoiIds.Contains(id));

        var agrippaStates = new List<HuntPoiState>();
        foreach (var poiId in agrippaPoiIds)
        {
            if (zoneCatalog.FindPoi(poiId) is { } resolved)
            {
                agrippaStates.Add(new HuntPoiState(resolved.Poi, HuntsMapMarkerState.Candidate));
            }
        }

        var kurreaStates = new List<HuntPoiState>();
        foreach (var poiId in kurreaPoiIds)
        {
            if (zoneCatalog.FindPoi(poiId) is { } resolved)
            {
                kurreaStates.Add(new HuntPoiState(resolved.Poi, HuntsMapMarkerState.Sighted));
            }
        }

        var statesByPoiId = new Dictionary<int, (HuntsMapMarkerState State, int Priority)>();
        foreach (var (mob, states) in new (HuntMobDefinition, List<HuntPoiState>)[]
                 {
                     (agrippa, agrippaStates), (kurrea, kurreaStates),
                 })
        {
            var rankTier = mob.Rank is "S" or "SS" or "F" ? 1 : 0;
            foreach (var (poi, state) in states)
            {
                var priority = rankTier * 10 + HuntCandidateResolver.Priority(state);
                if (!statesByPoiId.TryGetValue(poi.Id, out var existing) || priority > existing.Priority)
                {
                    statesByPoiId[poi.Id] = (state, priority);
                }
            }
        }

        Assert.Equal(HuntsMapMarkerState.Candidate, statesByPoiId[sharedPoiId].State);
    }
}
