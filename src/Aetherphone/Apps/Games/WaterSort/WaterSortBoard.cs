using System;
using System.Collections.Generic;

namespace Aetherphone.Apps.Games.WaterSort;

internal enum TubeAction
{
    None,
    Selected,
    Deselected,
    Poured,
}

internal readonly struct PourInfo
{
    public readonly int FromTube;

    public readonly int ToTube;

    public readonly int Color;

    public readonly int Count;

    public PourInfo(int fromTube, int toTube, int color, int count)
    {
        FromTube = fromTube;
        ToTube = toTube;
        Color = color;
        Count = count;
    }
}

internal sealed class WaterSortBoard
{
    public const int Capacity = 4;

    public const int MaxColors = 9;

    public const int MaxTubes = MaxColors + 2;

    private readonly int[] colors = new int[MaxTubes * Capacity];

    private readonly int[] counts = new int[MaxTubes];

    private readonly List<PourInfo> history = new();

    private readonly Random random = new();

    public int TubeCount { get; private set; }

    public int ColorCount { get; private set; }

    public int Level { get; private set; }

    public int Moves { get; private set; }

    public int Selected { get; private set; } = -1;

    public PourInfo LastPour { get; private set; }

    public int Count(int tube) => counts[tube];

    public int Segment(int tube, int level) => level < counts[tube] ? colors[tube * Capacity + level] : -1;

    public int TopColor(int tube) => counts[tube] > 0 ? colors[tube * Capacity + counts[tube] - 1] : -1;

    public void Reset(int level)
    {
        Level = level;
        ColorCount = Math.Min(MaxColors, 3 + level);
        TubeCount = ColorCount + 2;
        Moves = 0;
        Selected = -1;
        history.Clear();
        Generate();
    }

    public static int ColorsForLevel(int level) => Math.Min(MaxColors, 3 + level);

    public bool IsSolved()
    {
        for (var tube = 0; tube < TubeCount; tube++)
        {
            var count = counts[tube];
            if (count == 0)
            {
                continue;
            }

            if (count != Capacity)
            {
                return false;
            }

            var first = colors[tube * Capacity];
            for (var level = 1; level < count; level++)
            {
                if (colors[tube * Capacity + level] != first)
                {
                    return false;
                }
            }
        }

        return true;
    }

    public bool HasAnyLegalMove()
    {
        for (var from = 0; from < TubeCount; from++)
        {
            for (var to = 0; to < TubeCount; to++)
            {
                if (from != to && CanPour(from, to))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public bool CanPour(int from, int to)
    {
        if (from == to || counts[from] == 0 || counts[to] >= Capacity)
        {
            return false;
        }

        var movingColor = colors[from * Capacity + counts[from] - 1];
        if (counts[to] == 0)
        {
            return true;
        }

        return colors[to * Capacity + counts[to] - 1] == movingColor;
    }

    public TubeAction ClickTube(int tube)
    {
        if (Selected < 0)
        {
            if (counts[tube] == 0)
            {
                return TubeAction.None;
            }

            Selected = tube;
            return TubeAction.Selected;
        }

        if (Selected == tube)
        {
            Selected = -1;
            return TubeAction.Deselected;
        }

        var from = Selected;
        if (CanPour(from, tube))
        {
            LastPour = Pour(from, tube);
            Moves++;
            history.Add(LastPour);
            Selected = -1;
            return TubeAction.Poured;
        }

        if (counts[tube] > 0)
        {
            Selected = tube;
            return TubeAction.Selected;
        }

        Selected = -1;
        return TubeAction.Deselected;
    }

    public bool Undo()
    {
        if (history.Count == 0)
        {
            return false;
        }

        var last = history[history.Count - 1];
        history.RemoveAt(history.Count - 1);

        for (var index = 0; index < last.Count; index++)
        {
            counts[last.ToTube]--;
            colors[last.FromTube * Capacity + counts[last.FromTube]] = last.Color;
            counts[last.FromTube]++;
        }

        Moves = Math.Max(0, Moves - 1);
        Selected = -1;
        return true;
    }

    private PourInfo Pour(int from, int to)
    {
        var movingColor = colors[from * Capacity + counts[from] - 1];
        var moved = 0;
        while (counts[from] > 0 && counts[to] < Capacity && colors[from * Capacity + counts[from] - 1] == movingColor)
        {
            counts[from]--;
            colors[to * Capacity + counts[to]] = movingColor;
            counts[to]++;
            moved++;
        }

        return new PourInfo(from, to, movingColor, moved);
    }

    private void Generate()
    {
        for (var attempt = 0; attempt < 60; attempt++)
        {
            FillRandom();
            if (IsSolvable())
            {
                return;
            }
        }
    }

    private void FillRandom()
    {
        Array.Clear(counts, 0, MaxTubes);

        var pool = new int[ColorCount * Capacity];
        var poolIndex = 0;
        for (var color = 0; color < ColorCount; color++)
        {
            for (var copy = 0; copy < Capacity; copy++)
            {
                pool[poolIndex++] = color;
            }
        }

        for (var index = pool.Length - 1; index > 0; index--)
        {
            var swap = random.Next(index + 1);
            (pool[index], pool[swap]) = (pool[swap], pool[index]);
        }

        poolIndex = 0;
        for (var tube = 0; tube < ColorCount; tube++)
        {
            for (var level = 0; level < Capacity; level++)
            {
                colors[tube * Capacity + level] = pool[poolIndex++];
            }

            counts[tube] = Capacity;
        }
    }

    private bool IsSolvable()
    {
        var working = new int[MaxTubes * Capacity];
        var workingCounts = new int[MaxTubes];
        Array.Copy(colors, working, colors.Length);
        Array.Copy(counts, workingCounts, counts.Length);

        var visited = new HashSet<string>();
        var budget = 40000;
        return Search(working, workingCounts, visited, ref budget);
    }

    private bool Search(int[] state, int[] stateCounts, HashSet<string> visited, ref int budget)
    {
        if (Solved(stateCounts, state))
        {
            return true;
        }

        if (budget-- <= 0)
        {
            return true;
        }

        var key = Canonical(state, stateCounts);
        if (!visited.Add(key))
        {
            return false;
        }

        for (var from = 0; from < TubeCount; from++)
        {
            if (stateCounts[from] == 0)
            {
                continue;
            }

            for (var to = 0; to < TubeCount; to++)
            {
                if (from == to || !CanPourState(state, stateCounts, from, to))
                {
                    continue;
                }

                var nextState = (int[])state.Clone();
                var nextCounts = (int[])stateCounts.Clone();
                PourState(nextState, nextCounts, from, to);
                if (Search(nextState, nextCounts, visited, ref budget))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool Solved(int[] stateCounts, int[] state)
    {
        for (var tube = 0; tube < TubeCount; tube++)
        {
            var count = stateCounts[tube];
            if (count == 0)
            {
                continue;
            }

            if (count != Capacity)
            {
                return false;
            }

            var first = state[tube * Capacity];
            for (var level = 1; level < count; level++)
            {
                if (state[tube * Capacity + level] != first)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private bool CanPourState(int[] state, int[] stateCounts, int from, int to)
    {
        if (stateCounts[from] == 0 || stateCounts[to] >= Capacity)
        {
            return false;
        }

        if (stateCounts[from] == Capacity && IsUniform(state, from) && stateCounts[to] == 0)
        {
            return false;
        }

        var movingColor = state[from * Capacity + stateCounts[from] - 1];
        if (stateCounts[to] == 0)
        {
            return true;
        }

        return state[to * Capacity + stateCounts[to] - 1] == movingColor;
    }

    private bool IsUniform(int[] state, int tube)
    {
        var first = state[tube * Capacity];
        for (var level = 1; level < Capacity; level++)
        {
            if (state[tube * Capacity + level] != first)
            {
                return false;
            }
        }

        return true;
    }

    private void PourState(int[] state, int[] stateCounts, int from, int to)
    {
        var movingColor = state[from * Capacity + stateCounts[from] - 1];
        while (stateCounts[from] > 0 && stateCounts[to] < Capacity && state[from * Capacity + stateCounts[from] - 1] == movingColor)
        {
            stateCounts[from]--;
            state[to * Capacity + stateCounts[to]] = movingColor;
            stateCounts[to]++;
        }
    }

    private string Canonical(int[] state, int[] stateCounts)
    {
        Span<int> codes = stackalloc int[MaxTubes];
        for (var tube = 0; tube < TubeCount; tube++)
        {
            var code = 1;
            for (var level = 0; level < Capacity; level++)
            {
                var value = level < stateCounts[tube] ? state[tube * Capacity + level] + 1 : 0;
                code = code * (MaxColors + 1) + value;
            }

            codes[tube] = code;
        }

        codes.Slice(0, TubeCount).Sort();

        var builder = new System.Text.StringBuilder(TubeCount * 4);
        for (var tube = 0; tube < TubeCount; tube++)
        {
            builder.Append(codes[tube]);
            builder.Append(',');
        }

        return builder.ToString();
    }
}
