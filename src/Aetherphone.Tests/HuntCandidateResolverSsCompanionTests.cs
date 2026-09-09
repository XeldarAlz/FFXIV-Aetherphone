using Aetherphone.Core.Hunts;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HuntCandidateResolverSsCompanionTests
{
    private static FileInfo MobSource() => new(Path.Combine(AppContext.BaseDirectory, "Hunts", "HuntMob.json"));

    [Fact]
    public void FallbackCandidatesExcludeAnEmbeddedSsCompanionsOwnPoiIds()
    {
        var mobCatalog = new HuntMobCatalog(MobSource());
        var burfurlur = mobCatalog.Find("burfurlur_the_canny")!;
        var ker = mobCatalog.Find("ker")!;

        var poiIds = HuntCandidateResolver.ResolveCandidatePoiIds(burfurlur, null, out _);

        var kerOwnIds = HuntCandidateResolver.ResolveCandidatePoiIds(ker, null, out _);
        Assert.Empty(poiIds.Intersect(kerOwnIds));
        Assert.Equal(10, poiIds.Count);
    }

    [Fact]
    public void ActivePhaseOwnedByTheSsCompanionResolvesToNoCandidates()
    {
        var mobCatalog = new HuntMobCatalog(MobSource());
        var burfurlur = mobCatalog.Find("burfurlur_the_canny")!;

        var poiIds = HuntCandidateResolver.ResolveCandidatePoiIds(burfurlur, (1, 3), out var finalPhase);

        Assert.Empty(poiIds);
        Assert.False(finalPhase);
    }

    [Fact]
    public void NoSsRankHostEverSharesAPoiIdWithItsEmbeddedSsCompanionAnywhereInTheCatalog()
    {
        var mobCatalog = new HuntMobCatalog(MobSource());

        foreach (var host in mobCatalog.ById.Values)
        {
            var embeddedIds = new HashSet<int>();
            var hasEmbeddedPhase = false;
            foreach (var window in host.Windows)
            {
                foreach (var phase in window.Phases)
                {
                    if (phase.MobId is not { Length: > 0 })
                    {
                        continue;
                    }

                    hasEmbeddedPhase = true;
                    foreach (var poiId in phase.ZonePoiIds)
                    {
                        embeddedIds.Add(poiId);
                    }
                }
            }

            if (!hasEmbeddedPhase)
            {
                continue;
            }

            var ownIds = HuntCandidateResolver.ResolveCandidatePoiIds(host, null, out _);
            Assert.Empty(ownIds.Intersect(embeddedIds));
        }
    }
}
