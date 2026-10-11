namespace Aetherphone.Core.Casino;

internal sealed class CasinoFeatureSet
{
    public static readonly CasinoFeatureSet Empty = new(0, Array.Empty<string>());

    public static readonly string[] Known =
    {
        CasinoFeatures.EconomyV3,
        CasinoFeatures.Bonus,
        CasinoFeatures.Levels,
        CasinoFeatures.Club,
        CasinoFeatures.HostingV2,
        CasinoFeatures.Venue,
        CasinoFeatures.GilTables,
        CasinoFeatures.Machines,
        CasinoFeatures.Missions,
        CasinoFeatures.Challenges,
        CasinoFeatures.Fame,
        CasinoFeatures.Feed,
        CasinoFeatures.Rain,
        CasinoFeatures.Holdem,
        CasinoFeatures.Race,
        CasinoFeatures.Plinko,
        CasinoFeatures.Originals,
    };

    private readonly ulong mask;
    private readonly string[] extra;

    private CasinoFeatureSet(ulong mask, string[] extra)
    {
        this.mask = mask;
        this.extra = extra;
    }

    public bool IsEmpty => mask == 0 && extra.Length == 0;

    public static CasinoFeatureSet From(string[]? features)
    {
        if (features is null || features.Length == 0)
        {
            return Empty;
        }

        ulong mask = 0;
        var unknown = 0;
        for (var index = 0; index < features.Length; index++)
        {
            var slot = Array.IndexOf(Known, features[index]);
            if (slot < 0)
            {
                unknown++;
                continue;
            }

            mask |= 1UL << slot;
        }

        var extra = unknown == 0 ? Array.Empty<string>() : new string[unknown];
        var next = 0;
        for (var index = 0; index < features.Length && next < extra.Length; index++)
        {
            if (Array.IndexOf(Known, features[index]) < 0)
            {
                extra[next++] = features[index];
            }
        }

        return new CasinoFeatureSet(mask, extra);
    }

    public bool Has(string feature)
    {
        var slot = Array.IndexOf(Known, feature);
        if (slot >= 0)
        {
            return (mask & (1UL << slot)) != 0;
        }

        for (var index = 0; index < extra.Length; index++)
        {
            if (string.Equals(extra[index], feature, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
