namespace Aetherphone.Core.Strats;

internal static class StratsLibrary
{
    public static bool TryFindByTerritory(StratsManifest? manifest, uint territoryId, out ManifestFight fight)
    {
        fight = null!;
        if (manifest is null || territoryId == 0)
        {
            return false;
        }

        for (var groupIndex = 0; groupIndex < manifest.Groups.Length; groupIndex++)
        {
            var fights = manifest.Groups[groupIndex].Fights;
            for (var fightIndex = 0; fightIndex < fights.Length; fightIndex++)
            {
                var territories = fights[fightIndex].TerritoryIds;
                for (var territoryIndex = 0; territoryIndex < territories.Length; territoryIndex++)
                {
                    if (territories[territoryIndex] != territoryId)
                    {
                        continue;
                    }

                    fight = fights[fightIndex];
                    return true;
                }
            }
        }

        return false;
    }

    public static bool TryMostRecent(StratsManifest? manifest, StratsSnapshot snapshot, out ManifestFight fight,
        out StratsFightSelection selection)
    {
        fight = null!;
        selection = null!;
        if (manifest is null)
        {
            return false;
        }

        var bestUnix = 0L;
        for (var groupIndex = 0; groupIndex < manifest.Groups.Length; groupIndex++)
        {
            var fights = manifest.Groups[groupIndex].Fights;
            for (var fightIndex = 0; fightIndex < fights.Length; fightIndex++)
            {
                if (!snapshot.Fights.TryGetValue(fights[fightIndex].Key, out var saved) ||
                    saved.OpenedUnix <= bestUnix)
                {
                    continue;
                }

                bestUnix = saved.OpenedUnix;
                fight = fights[fightIndex];
                selection = saved;
            }
        }

        return bestUnix > 0;
    }

    public static bool Matches(ManifestFight fight, ReadOnlySpan<char> query)
    {
        var remaining = query.Trim();
        while (remaining.Length > 0)
        {
            var space = remaining.IndexOf(' ');
            var token = space < 0 ? remaining : remaining[..space];
            remaining = space < 0 ? ReadOnlySpan<char>.Empty : remaining[(space + 1)..].TrimStart();
            if (!Contains(fight.Title, token) && !Contains(fight.Abbrev, token) && !Contains(fight.Subtitle, token))
            {
                return false;
            }
        }

        return true;
    }

    private static bool Contains(string field, ReadOnlySpan<char> token) =>
        field.AsSpan().Contains(token, StringComparison.OrdinalIgnoreCase);
}
