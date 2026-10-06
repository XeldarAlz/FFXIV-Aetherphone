using Aetherphone.Apps.Games.Framework;

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
    private const int GenerateAttempts = 60;
    private const int SolveNodeCap = 200_000;
    private const int SolveMaxDepth = 512;
    private const int HistoryCapacity = 128;
    private const ulong FnvOffset = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;
    private const ulong LevelMix = 0x9E3779B97F4A7C15UL;
    private readonly int[] colors = new int[MaxTubes * Capacity];
    private readonly int[] counts = new int[MaxTubes];
    private readonly int[] pool = new int[MaxColors * Capacity];
    private readonly List<PourInfo> history = new(HistoryCapacity);
    private readonly List<int[]> searchStates = new();
    private readonly List<int[]> searchCounts = new();
    private readonly HashSet<ulong> visited = new();
    private GameRandom random;
    private int searchBudget;
    public int TubeCount { get; private set; }
    public int ColorCount { get; private set; }
    public int Level { get; private set; }
    public int Moves { get; private set; }
    public int Selected { get; private set; } = -1;
    public PourInfo LastPour { get; private set; }
    public bool CanUndo => history.Count > 0;
    public int Count(int tube) => counts[tube];
    public int Segment(int tube, int level) => level < counts[tube] ? colors[tube * Capacity + level] : -1;
    public int TopColor(int tube) => counts[tube] > 0 ? colors[tube * Capacity + counts[tube] - 1] : -1;

    public static ulong SeedFor(ulong seed, int level) => seed ^ ((ulong)level * LevelMix);

    public static int ColorsForLevel(int level) => Math.Min(MaxColors, 3 + level);

    public void Reset(int level, GameRandom source)
    {
        random = source;
        Level = level;
        ColorCount = ColorsForLevel(level);
        TubeCount = ColorCount + 2;
        Moves = 0;
        Selected = -1;
        history.Clear();
        Generate();
    }

    public bool IsTubeSorted(int tube) => counts[tube] == Capacity && IsUniform(colors, counts, tube);

    public bool IsSolved() => Solved(colors, counts);

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

    public bool IsSolvable()
    {
        visited.Clear();
        searchBudget = SolveNodeCap;
        var state = Buffer(searchStates, 0, MaxTubes * Capacity);
        var stateCounts = Buffer(searchCounts, 0, MaxTubes);
        Array.Copy(colors, state, colors.Length);
        Array.Copy(counts, stateCounts, counts.Length);
        return Search(0);
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
        for (var attempt = 0; attempt < GenerateAttempts; attempt++)
        {
            FillRandom();
            if (!IsSolved() && IsSolvable())
            {
                return;
            }
        }
    }

    private void FillRandom()
    {
        Array.Clear(counts, 0, MaxTubes);
        var poolSize = ColorCount * Capacity;
        var poolIndex = 0;
        for (var color = 0; color < ColorCount; color++)
        {
            for (var copy = 0; copy < Capacity; copy++)
            {
                pool[poolIndex++] = color;
            }
        }

        for (var index = poolSize - 1; index > 0; index--)
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

    private static int[] Buffer(List<int[]> buffers, int depth, int length)
    {
        while (buffers.Count <= depth)
        {
            buffers.Add(new int[length]);
        }

        return buffers[depth];
    }

    private bool Search(int depth)
    {
        var state = searchStates[depth];
        var stateCounts = searchCounts[depth];
        if (Solved(state, stateCounts))
        {
            return true;
        }

        if (depth >= SolveMaxDepth || searchBudget-- <= 0)
        {
            return false;
        }

        if (!visited.Add(Canonical(state, stateCounts)))
        {
            return false;
        }

        var nextState = Buffer(searchStates, depth + 1, MaxTubes * Capacity);
        var nextCounts = Buffer(searchCounts, depth + 1, MaxTubes);
        for (var from = 0; from < TubeCount; from++)
        {
            if (stateCounts[from] == 0 || (stateCounts[from] == Capacity && IsUniform(state, stateCounts, from)))
            {
                continue;
            }

            for (var to = 0; to < TubeCount; to++)
            {
                if (from == to || !CanPourState(state, stateCounts, from, to))
                {
                    continue;
                }

                Array.Copy(state, nextState, state.Length);
                Array.Copy(stateCounts, nextCounts, stateCounts.Length);
                PourState(nextState, nextCounts, from, to);
                if (Search(depth + 1))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool Solved(int[] state, int[] stateCounts)
    {
        for (var tube = 0; tube < TubeCount; tube++)
        {
            var count = stateCounts[tube];
            if (count == 0)
            {
                continue;
            }

            if (count != Capacity || !IsUniform(state, stateCounts, tube))
            {
                return false;
            }
        }

        return true;
    }

    private static bool CanPourState(int[] state, int[] stateCounts, int from, int to)
    {
        if (stateCounts[from] == 0 || stateCounts[to] >= Capacity)
        {
            return false;
        }

        var movingColor = state[from * Capacity + stateCounts[from] - 1];
        if (stateCounts[to] == 0)
        {
            return !IsUniform(state, stateCounts, from);
        }

        return state[to * Capacity + stateCounts[to] - 1] == movingColor;
    }

    private static bool IsUniform(int[] state, int[] stateCounts, int tube)
    {
        var first = state[tube * Capacity];
        for (var level = 1; level < stateCounts[tube]; level++)
        {
            if (state[tube * Capacity + level] != first)
            {
                return false;
            }
        }

        return true;
    }

    private static void PourState(int[] state, int[] stateCounts, int from, int to)
    {
        var movingColor = state[from * Capacity + stateCounts[from] - 1];
        while (stateCounts[from] > 0 && stateCounts[to] < Capacity &&
               state[from * Capacity + stateCounts[from] - 1] == movingColor)
        {
            stateCounts[from]--;
            state[to * Capacity + stateCounts[to]] = movingColor;
            stateCounts[to]++;
        }
    }

    private ulong Canonical(int[] state, int[] stateCounts)
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

        var sorted = codes.Slice(0, TubeCount);
        sorted.Sort();
        var hash = FnvOffset;
        for (var tube = 0; tube < sorted.Length; tube++)
        {
            hash ^= (uint)sorted[tube];
            hash *= FnvPrime;
        }

        return hash;
    }
}
