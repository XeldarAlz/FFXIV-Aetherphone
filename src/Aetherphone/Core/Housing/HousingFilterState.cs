namespace Aetherphone.Core.Housing;

internal sealed class HousingFilterState
{
    private bool small = true;
    private bool medium = true;
    private bool large = true;
    private bool mainDivision = true;
    private bool subdivision = true;
    private bool privateBuyers = true;
    private bool freeCompany = true;
    private bool phaseEntry = true;
    private bool phaseResults = true;
    private bool phaseOther = true;
    private bool freshOnly;
    private bool watchedOnly;
    private bool showAllPlots;
    private int maxEntries;

    public int Revision { get; private set; }

    public bool Small
    {
        get => small;
        set => Assign(ref small, value);
    }

    public bool Medium
    {
        get => medium;
        set => Assign(ref medium, value);
    }

    public bool Large
    {
        get => large;
        set => Assign(ref large, value);
    }

    public bool MainDivision
    {
        get => mainDivision;
        set => Assign(ref mainDivision, value);
    }

    public bool Subdivision
    {
        get => subdivision;
        set => Assign(ref subdivision, value);
    }

    public bool PrivateBuyers
    {
        get => privateBuyers;
        set => Assign(ref privateBuyers, value);
    }

    public bool FreeCompany
    {
        get => freeCompany;
        set => Assign(ref freeCompany, value);
    }

    public bool PhaseEntry
    {
        get => phaseEntry;
        set => Assign(ref phaseEntry, value);
    }

    public bool PhaseResults
    {
        get => phaseResults;
        set => Assign(ref phaseResults, value);
    }
    public bool PhaseOther
    {
        get => phaseOther;
        set => Assign(ref phaseOther, value);
    }

    public bool FreshOnly
    {
        get => freshOnly;
        set => Assign(ref freshOnly, value);
    }

    public bool WatchedOnly
    {
        get => watchedOnly;
        set => Assign(ref watchedOnly, value);
    }

    public bool ShowAllPlots
    {
        get => showAllPlots;
        set => Assign(ref showAllPlots, value);
    }

    public int MaxEntries
    {
        get => maxEntries;
        set => Assign(ref maxEntries, Math.Clamp(value, 0, 200));
    }

    public int ActiveCount
    {
        get
        {
            var count = 0;
            if (!small)
            {
                count++;
            }

            if (!medium)
            {
                count++;
            }

            if (!large)
            {
                count++;
            }

            if (!mainDivision)
            {
                count++;
            }

            if (!subdivision)
            {
                count++;
            }

            if (!privateBuyers)
            {
                count++;
            }

            if (!freeCompany)
            {
                count++;
            }

            if (!phaseEntry)
            {
                count++;
            }

            if (!phaseResults)
            {
                count++;
            }

            if (!phaseOther)
            {
                count++;
            }

            if (freshOnly)
            {
                count++;
            }

            if (watchedOnly)
            {
                count++;
            }

            if (maxEntries > 0)
            {
                count++;
            }

            return count;
        }
    }

    public bool HasNarrowingFilters => ActiveCount > 0;

    public void Reset()
    {
        small = true;
        medium = true;
        large = true;
        mainDivision = true;
        subdivision = true;
        privateBuyers = true;
        freeCompany = true;
        phaseEntry = true;
        phaseResults = true;
        phaseOther = true;
        freshOnly = false;
        watchedOnly = false;
        maxEntries = 0;
        Revision++;
    }

    public bool Matches(HousingPlot plot, DateTime nowUtc, HousingFreshnessThresholds thresholds, bool isWatched)
    {
        if (watchedOnly && !isWatched)
        {
            return false;
        }

        if (!MatchesSize(plot.Size))
        {
            return false;
        }

        if (!(plot.IsSubdivision ? subdivision : mainDivision))
        {
            return false;
        }

        if (!MatchesEligibility(plot.Eligibility))
        {
            return false;
        }

        if (!MatchesPhase(plot.Phase))
        {
            return false;
        }

        if (maxEntries > 0 && plot.Entries is { } entries && entries > maxEntries)
        {
            return false;
        }

        if (!freshOnly)
        {
            return true;
        }

        var freshness = thresholds.Classify(plot.LastSeenUtc, nowUtc, HousingProviderKind.Service);
        return freshness is HousingDataFreshness.Live or HousingDataFreshness.Recent;
    }

    private bool MatchesSize(HousingPlotSize size) => size switch
    {
        HousingPlotSize.Small => small,
        HousingPlotSize.Medium => medium,
        HousingPlotSize.Large => large,
        _ => true,
    };

    private bool MatchesEligibility(HousingPurchaseEligibility eligibility) => eligibility switch
    {
        HousingPurchaseEligibility.Private => privateBuyers,
        HousingPurchaseEligibility.FreeCompany => freeCompany,
        HousingPurchaseEligibility.Both => privateBuyers || freeCompany,
        _ => true,
    };

    private bool MatchesPhase(HousingLotteryPhase phase) => phase switch
    {
        HousingLotteryPhase.Entry => phaseEntry,
        HousingLotteryPhase.Results => phaseResults,
        _ => phaseOther,
    };

    private void Assign(ref bool field, bool value)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        Revision++;
    }

    private void Assign(ref int field, int value)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        Revision++;
    }
}
