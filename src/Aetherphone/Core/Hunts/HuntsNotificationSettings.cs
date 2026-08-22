namespace Aetherphone.Core.Hunts;

internal sealed class HuntsNotificationSettings
{
    private readonly object gate = new();
    private bool rankF = true;
    private bool rankB;
    private bool rankA;
    private bool rankS = true;
    private bool rankSS = true;
    private readonly bool[] expansionActive = CreateAllActive();
    private readonly Dictionary<string, bool> worldEnabled = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, HuntMobOverride> mobOverrides = new(StringComparer.Ordinal);

    public event Action? Changed;

    public bool RankF
    {
        get { lock (gate) { return rankF; } }
        set { lock (gate) { rankF = value; } RaiseChanged(); }
    }

    public bool RankB
    {
        get { lock (gate) { return rankB; } }
        set { lock (gate) { rankB = value; } RaiseChanged(); }
    }

    public bool RankA
    {
        get { lock (gate) { return rankA; } }
        set { lock (gate) { rankA = value; } RaiseChanged(); }
    }

    public bool RankS
    {
        get { lock (gate) { return rankS; } }
        set { lock (gate) { rankS = value; } RaiseChanged(); }
    }

    public bool RankSS
    {
        get { lock (gate) { return rankSS; } }
        set { lock (gate) { rankSS = value; } RaiseChanged(); }
    }

    public bool IsExpansionActive(int expansionIndex)
    {
        lock (gate)
        {
            return expansionActive[expansionIndex];
        }
    }

    public void ToggleExpansion(int expansionIndex)
    {
        lock (gate)
        {
            expansionActive[expansionIndex] = !expansionActive[expansionIndex];
        }

        RaiseChanged();
    }

    public bool IsWorldEnabled(string worldId)
    {
        lock (gate)
        {
            return IsWorldEnabledUnlocked(worldId);
        }
    }

    public void ToggleWorld(string worldId)
    {
        lock (gate)
        {
            worldEnabled[worldId] = !IsWorldEnabledUnlocked(worldId);
        }

        RaiseChanged();
    }

    public HuntMobNotificationMode MobOverrideModeFor(string mobId)
    {
        lock (gate)
        {
            return mobOverrides.TryGetValue(mobId, out var entry) ? entry.Mode : HuntMobNotificationMode.Default;
        }
    }

    public string? MobOverrideWorldFor(string mobId)
    {
        lock (gate)
        {
            return mobOverrides.TryGetValue(mobId, out var entry) ? entry.WorldId : null;
        }
    }

    public void SetMobOverride(string mobId, HuntMobNotificationMode mode, string? worldId)
    {
        lock (gate)
        {
            if (mode == HuntMobNotificationMode.Default)
            {
                mobOverrides.Remove(mobId);
            }
            else
            {
                mobOverrides[mobId] =
                    new HuntMobOverride(mode, mode == HuntMobNotificationMode.EnabledOnWorld ? worldId : null);
            }
        }

        RaiseChanged();
    }

    public int MobOverrideCount
    {
        get
        {
            lock (gate)
            {
                return mobOverrides.Count;
            }
        }
    }

    public void CollectMobOverrides(List<HuntMobOverrideEntry> into)
    {
        into.Clear();
        lock (gate)
        {
            foreach (var pair in mobOverrides)
            {
                into.Add(new HuntMobOverrideEntry(pair.Key, pair.Value.Mode, pair.Value.WorldId));
            }
        }
    }

    public bool IsEnabledFor(HuntMobDefinition? mob, string worldId)
    {
        lock (gate)
        {
            if (mob is not null && mobOverrides.TryGetValue(mob.Id, out var mobOverride) &&
                mobOverride.Mode != HuntMobNotificationMode.Default)
            {
                return mobOverride.Mode switch
                {
                    HuntMobNotificationMode.Enabled => true,
                    HuntMobNotificationMode.Disabled => false,
                    HuntMobNotificationMode.EnabledOnWorld =>
                        string.Equals(mobOverride.WorldId, worldId, StringComparison.OrdinalIgnoreCase),
                    _ => true,
                };
            }

            if (!IsWorldEnabledUnlocked(worldId))
            {
                return false;
            }

            if (!MatchesRankUnlocked(mob?.Rank))
            {
                return false;
            }

            return MatchesExpansionUnlocked(mob?.ExpansionId);
        }
    }

    public HuntsNotificationSnapshot ToSnapshot()
    {
        lock (gate)
        {
            var mobOverrideSnapshots = new Dictionary<string, HuntMobOverrideSnapshot>(StringComparer.Ordinal);
            foreach (var pair in mobOverrides)
            {
                mobOverrideSnapshots[pair.Key] = new HuntMobOverrideSnapshot
                {
                    Mode = pair.Value.Mode,
                    WorldId = pair.Value.WorldId,
                };
            }

            return new HuntsNotificationSnapshot
            {
                RankF = rankF,
                RankB = rankB,
                RankA = rankA,
                RankS = rankS,
                RankSS = rankSS,
                ExpansionActive = new List<bool>(expansionActive),
                WorldEnabled = new Dictionary<string, bool>(worldEnabled, StringComparer.OrdinalIgnoreCase),
                MobOverrides = mobOverrideSnapshots,
            };
        }
    }

    public void ApplySnapshot(HuntsNotificationSnapshot snapshot)
    {
        lock (gate)
        {
            rankF = snapshot.RankF;
            rankB = snapshot.RankB;
            rankA = snapshot.RankA;
            rankS = snapshot.RankS;
            rankSS = snapshot.RankSS;

            var savedExpansions = snapshot.ExpansionActive;
            var overlap = Math.Min(savedExpansions.Count, expansionActive.Length);
            for (var index = 0; index < overlap; index++)
            {
                expansionActive[index] = savedExpansions[index];
            }

            worldEnabled.Clear();
            foreach (var pair in snapshot.WorldEnabled)
            {
                worldEnabled[pair.Key] = pair.Value;
            }

            mobOverrides.Clear();
            foreach (var pair in snapshot.MobOverrides)
            {
                if (pair.Value.Mode == HuntMobNotificationMode.Default)
                {
                    continue;
                }

                var worldId = pair.Value.Mode == HuntMobNotificationMode.EnabledOnWorld ? pair.Value.WorldId : null;
                mobOverrides[pair.Key] = new HuntMobOverride(pair.Value.Mode, worldId);
            }
        }

        RaiseChanged();
    }

    public void ResetToDefault()
    {
        lock (gate)
        {
            rankF = true;
            rankB = false;
            rankA = false;
            rankS = true;
            rankSS = true;
            Array.Fill(expansionActive, true);
            worldEnabled.Clear();
        }

        RaiseChanged();
    }

    private void RaiseChanged() => Changed?.Invoke();

    private bool IsWorldEnabledUnlocked(string worldId) =>
        !worldEnabled.TryGetValue(worldId, out var enabled) || enabled;

    private bool MatchesRankUnlocked(string? rank) => rank switch
    {
        "F" => rankF,
        "B" => rankB,
        "A" => rankA,
        "S" => rankS,
        "SS" => rankSS,
        _ => true,
    };

    private bool MatchesExpansionUnlocked(string? expansionId)
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
}

internal readonly struct HuntMobOverride
{
    public readonly HuntMobNotificationMode Mode;
    public readonly string? WorldId;

    public HuntMobOverride(HuntMobNotificationMode mode, string? worldId)
    {
        Mode = mode;
        WorldId = worldId;
    }
}

internal readonly struct HuntMobOverrideEntry
{
    public readonly string MobId;
    public readonly HuntMobNotificationMode Mode;
    public readonly string? WorldId;

    public HuntMobOverrideEntry(string mobId, HuntMobNotificationMode mode, string? worldId)
    {
        MobId = mobId;
        Mode = mode;
        WorldId = worldId;
    }
}
