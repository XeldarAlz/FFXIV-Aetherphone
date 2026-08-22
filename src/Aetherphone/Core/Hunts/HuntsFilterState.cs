namespace Aetherphone.Core.Hunts;

internal sealed class HuntsFilterState
{
    private bool rankF = true;
    private bool rankB;
    private bool rankA;
    private bool rankS = true;
    private bool rankSS = true;
    private bool statusClosed = true;
    private bool statusOpen = true;
    private bool statusCapped = true;
    private bool statusUnmet = true;
    private readonly HashSet<string> worlds = new(StringComparer.OrdinalIgnoreCase);
    private readonly bool[] expansionActive = CreateAllActive();

    public int Revision { get; private set; }

    public bool RankF
    {
        get => rankF;
        set => Assign(ref rankF, value);
    }

    public bool RankB
    {
        get => rankB;
        set => Assign(ref rankB, value);
    }

    public bool RankA
    {
        get => rankA;
        set => Assign(ref rankA, value);
    }

    public bool RankS
    {
        get => rankS;
        set => Assign(ref rankS, value);
    }

    public bool RankSS
    {
        get => rankSS;
        set => Assign(ref rankSS, value);
    }

    public bool StatusClosed
    {
        get => statusClosed;
        set => Assign(ref statusClosed, value);
    }

    public bool StatusOpen
    {
        get => statusOpen;
        set => Assign(ref statusOpen, value);
    }

    public bool StatusCapped
    {
        get => statusCapped;
        set => Assign(ref statusCapped, value);
    }

    public bool StatusUnmet
    {
        get => statusUnmet;
        set => Assign(ref statusUnmet, value);
    }

    public IReadOnlyCollection<string> Worlds => worlds;

    public bool IsExpansionActive(int expansionIndex) => expansionActive[expansionIndex];

    public void ToggleExpansion(int expansionIndex)
    {
        expansionActive[expansionIndex] = !expansionActive[expansionIndex];
        Revision++;
    }

    public bool IsWorldSelected(string worldId) => worlds.Contains(worldId);

    public void ToggleWorld(string worldId)
    {
        if (!worlds.Remove(worldId))
        {
            worlds.Add(worldId);
        }

        Revision++;
    }

    public void ClearWorlds()
    {
        if (worlds.Count == 0)
        {
            return;
        }

        worlds.Clear();
        Revision++;
    }

    public int ActiveCount
    {
        get
        {
            var count = 0;
            if (!rankF)
            {
                count++;
            }

            if (!rankB)
            {
                count++;
            }

            if (!rankA)
            {
                count++;
            }

            if (!rankS)
            {
                count++;
            }

            if (!rankSS)
            {
                count++;
            }

            if (!statusClosed)
            {
                count++;
            }

            if (!statusOpen)
            {
                count++;
            }

            if (!statusCapped)
            {
                count++;
            }

            if (!statusUnmet)
            {
                count++;
            }

            for (var index = 0; index < expansionActive.Length; index++)
            {
                if (!expansionActive[index])
                {
                    count++;
                }
            }

            return count + worlds.Count;
        }
    }

    public bool HasNarrowingFilters => ActiveCount > 0;

    public void Reset()
    {
        rankF = true;
        rankB = true;
        rankA = true;
        rankS = true;
        rankSS = true;
        statusClosed = true;
        statusOpen = true;
        statusCapped = true;
        statusUnmet = true;
        Array.Fill(expansionActive, true);
        worlds.Clear();
        Revision++;
    }

    public HuntsFilterSnapshot ToSnapshot() =>
        new()
        {
            RankF = rankF,
            RankB = rankB,
            RankA = rankA,
            RankS = rankS,
            RankSS = rankSS,
            StatusClosed = statusClosed,
            StatusOpen = statusOpen,
            StatusCapped = statusCapped,
            StatusUnmet = statusUnmet,
            ExpansionActive = new List<bool>(expansionActive),
            Worlds = new List<string>(worlds),
        };

    public void ApplySnapshot(HuntsFilterSnapshot snapshot)
    {
        rankF = snapshot.RankF;
        rankB = snapshot.RankB;
        rankA = snapshot.RankA;
        rankS = snapshot.RankS;
        rankSS = snapshot.RankSS;
        statusClosed = snapshot.StatusClosed;
        statusOpen = snapshot.StatusOpen;
        statusCapped = snapshot.StatusCapped;
        statusUnmet = snapshot.StatusUnmet;

        var savedExpansions = snapshot.ExpansionActive;
        var overlap = Math.Min(savedExpansions.Count, expansionActive.Length);
        for (var index = 0; index < overlap; index++)
        {
            expansionActive[index] = savedExpansions[index];
        }

        worlds.Clear();
        foreach (var worldId in snapshot.Worlds)
        {
            worlds.Add(worldId);
        }

        Revision++;
    }

    public bool Matches(HuntWindowDto window, HuntMobDefinition? mob, HuntWindowStatus status)
    {
        if (worlds.Count > 0 && !worlds.Contains(window.WorldId))
        {
            return false;
        }

        if (!MatchesRank(mob?.Rank))
        {
            return false;
        }

        if (!MatchesExpansion(mob?.ExpansionId))
        {
            return false;
        }

        return MatchesStatus(status);
    }

    public static void CollectWorlds(HuntWindowDto[] windows, SortedSet<string> into)
    {
        into.Clear();

        for (var index = 0; index < windows.Length; index++)
        {
            var worldId = windows[index].WorldId;
            if (worldId.Length > 0)
            {
                into.Add(worldId);
            }
        }
    }

    private bool MatchesRank(string? rank) => rank switch
    {
        "F" => rankF,
        "B" => rankB,
        "A" => rankA,
        "S" => rankS,
        "SS" => rankSS,
        _ => true,
    };

    private bool MatchesStatus(HuntWindowStatus status) => status switch
    {
        HuntWindowStatus.Closed => statusClosed,
        HuntWindowStatus.Open => statusOpen,
        HuntWindowStatus.Capped => statusCapped,
        HuntWindowStatus.Unmet => statusUnmet,
        _ => true,
    };

    private bool MatchesExpansion(string? expansionId)
    {
        if (string.IsNullOrEmpty(expansionId))
        {
            return true;
        }

        var index = Array.IndexOf(HuntExpansions.Ids, expansionId);
        return index < 0 || expansionActive[index];
    }

    private static bool[] CreateAllActive()
    {
        var active = new bool[HuntExpansions.Ids.Length];
        Array.Fill(active, true);
        return active;
    }

    private void Assign(ref bool field, bool value)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        Revision++;
    }
}
