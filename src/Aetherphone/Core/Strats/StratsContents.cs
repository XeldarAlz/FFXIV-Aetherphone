namespace Aetherphone.Core.Strats;

internal readonly record struct StratsContentsEntry(int PhaseIndex, int MechIndex, string Label, bool ForYou)
{
    public bool IsPhase => MechIndex < 0;
}

internal static class StratsContents
{
    public static StratsContentsEntry[] Build(ResolvedFight fight)
    {
        var count = 0;
        for (var phaseIndex = 0; phaseIndex < fight.Phases.Length; phaseIndex++)
        {
            count += 1 + fight.Phases[phaseIndex].Mechs.Length;
        }

        var entries = new StratsContentsEntry[count];
        var cursor = 0;
        for (var phaseIndex = 0; phaseIndex < fight.Phases.Length; phaseIndex++)
        {
            var phase = fight.Phases[phaseIndex];
            entries[cursor++] = new StratsContentsEntry(phaseIndex, -1, phase.Name, false);
            for (var mechIndex = 0; mechIndex < phase.Mechs.Length; mechIndex++)
            {
                var mech = phase.Mechs[mechIndex];
                entries[cursor++] = new StratsContentsEntry(phaseIndex, mechIndex, mech.Name,
                    mech.PlayerText is not null || mech.PlayerImage is not null);
            }
        }

        return entries;
    }

    public static int IndexAt(ReadOnlySpan<float> tops, float line)
    {
        if (tops.Length == 0)
        {
            return -1;
        }

        var low = 0;
        var high = tops.Length - 1;
        var found = 0;
        while (low <= high)
        {
            var middle = (low + high) >> 1;
            if (tops[middle] <= line)
            {
                found = middle;
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return found;
    }

    public static float Progress(float scrollY, float scrollMaxY) =>
        scrollMaxY <= 0f ? 0f : Math.Clamp(scrollY / scrollMaxY, 0f, 1f);

    public static int CountForYou(StratsContentsEntry[] entries)
    {
        var count = 0;
        for (var index = 0; index < entries.Length; index++)
        {
            if (entries[index].ForYou)
            {
                count++;
            }
        }

        return count;
    }
}
