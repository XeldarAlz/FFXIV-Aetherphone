using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games.Mahjong;

internal sealed class MahjongBoard
{
    public const int MaxFillAttempts = 160;
    private const int MaxTiles = MahjongLayout.MaxTiles;

    private readonly byte[] faces = new byte[MaxTiles];
    private readonly bool[] present = new bool[MaxTiles];
    private readonly bool[] free = new bool[MaxTiles];
    private readonly int[] solution = new int[MaxTiles];
    private readonly int[] history = new int[MaxTiles];
    private readonly bool[] include = new bool[MaxTiles];
    private readonly bool[] placed = new bool[MaxTiles];
    private readonly int[] order = new int[MaxTiles];
    private readonly int[] candidates = new int[MaxTiles];
    private readonly int[] walk = new int[MaxTiles];
    private readonly int[] walkStamp = new int[MaxTiles];
    private readonly byte[] gathered = new byte[MaxTiles];
    private readonly int[] pairOrder = new int[MahjongTiles.PairCount];
    private readonly int[] groupFree = new int[MahjongTiles.GroupCount];
    private MahjongLayout layout = MahjongLayouts.Moogle;
    private GameRandom random;
    private int historyCount;
    private int solutionCount;
    private int stamp;
    private bool freeDirty = true;
    private bool hasMoves;
    private int freePairs;

    public MahjongLayout Layout => layout;

    public int Remaining { get; private set; }

    public int Matches { get; private set; }

    public int Undos { get; private set; }

    public int Shuffles { get; private set; }

    public int Version { get; private set; }

    public bool Dealt { get; private set; }

    public bool Cleared => Dealt && Remaining == 0;

    public bool CanUndo => historyCount > 0;

    public int SolutionPairs => solutionCount / 2;

    public bool HasMoves
    {
        get
        {
            RefreshFree();
            return hasMoves;
        }
    }

    public int FreePairs
    {
        get
        {
            RefreshFree();
            return freePairs;
        }
    }

    public int Face(int tile) => faces[tile];

    public bool IsPresent(int tile) => tile >= 0 && tile < layout.Count && present[tile];

    public bool IsFree(int tile)
    {
        RefreshFree();
        return IsPresent(tile) && free[tile];
    }

    public void SolutionPair(int index, out int first, out int second)
    {
        first = solution[index * 2];
        second = solution[index * 2 + 1];
    }

    public void Deal(MahjongLayout chosen, GameRandom seeded)
    {
        layout = chosen;
        random = seeded;
        var count = layout.Count;
        Array.Clear(present);
        Array.Clear(faces);
        for (var tile = 0; tile < count; tile++)
        {
            include[tile] = true;
        }

        for (var tile = count; tile < MaxTiles; tile++)
        {
            include[tile] = false;
        }

        if (!TryFill(count))
        {
            FillInDrawOrder(count);
        }

        for (var pair = 0; pair < MahjongTiles.PairCount; pair++)
        {
            pairOrder[pair] = pair;
        }

        Permute(pairOrder.AsSpan());
        for (var pair = 0; pair < count / 2; pair++)
        {
            var source = pairOrder[pair];
            var first = (byte)MahjongTiles.PairFirst(source);
            var second = (byte)MahjongTiles.PairSecond(source);
            var swap = random.Chance(0.5f);
            faces[order[pair * 2]] = swap ? second : first;
            faces[order[pair * 2 + 1]] = swap ? first : second;
        }

        for (var tile = 0; tile < count; tile++)
        {
            present[tile] = true;
        }

        StoreSolution(count);
        Remaining = count;
        Matches = 0;
        Undos = 0;
        Shuffles = 0;
        historyCount = 0;
        Dealt = true;
        Changed();
    }

    public bool CanPair(int first, int second)
    {
        if (first == second || !IsFree(first) || !IsFree(second))
        {
            return false;
        }

        return MahjongTiles.Matches(faces[first], faces[second]);
    }

    public bool TryMatch(int first, int second)
    {
        if (!CanPair(first, second))
        {
            return false;
        }

        present[first] = false;
        present[second] = false;
        history[historyCount++] = first;
        history[historyCount++] = second;
        Remaining -= 2;
        Matches++;
        Changed();
        return true;
    }

    public bool Undo(out int first, out int second)
    {
        first = -1;
        second = -1;
        if (historyCount < 2)
        {
            return false;
        }

        second = history[--historyCount];
        first = history[--historyCount];
        present[first] = true;
        present[second] = true;
        Remaining += 2;
        Undos++;
        Changed();
        return true;
    }

    public bool FindHint(out int first, out int second)
    {
        RefreshFree();
        first = -1;
        second = -1;
        var count = layout.Count;
        for (var index = 0; index < count; index++)
        {
            var tile = layout.DrawOrder(count - 1 - index);
            if (!present[tile] || !free[tile])
            {
                continue;
            }

            for (var otherIndex = index + 1; otherIndex < count; otherIndex++)
            {
                var other = layout.DrawOrder(count - 1 - otherIndex);
                if (!present[other] || !free[other] || !MahjongTiles.Matches(faces[tile], faces[other]))
                {
                    continue;
                }

                first = tile;
                second = other;
                return true;
            }
        }

        return false;
    }

    public bool Shuffle()
    {
        var count = layout.Count;
        if (Remaining <= 0)
        {
            return false;
        }

        for (var tile = 0; tile < MaxTiles; tile++)
        {
            include[tile] = tile < count && present[tile];
        }

        if (!TryFill(Remaining))
        {
            return false;
        }

        var gatheredCount = 0;
        for (var tile = 0; tile < count; tile++)
        {
            if (present[tile])
            {
                gathered[gatheredCount++] = faces[tile];
            }
        }

        SortByGroup(gatheredCount);
        var pairs = gatheredCount / 2;
        for (var pair = 0; pair < pairs; pair++)
        {
            pairOrder[pair] = pair;
        }

        Permute(pairOrder.AsSpan(0, pairs));
        for (var pair = 0; pair < pairs; pair++)
        {
            var source = pairOrder[pair];
            faces[order[pair * 2]] = gathered[source * 2];
            faces[order[pair * 2 + 1]] = gathered[source * 2 + 1];
        }

        StoreSolution(Remaining);
        historyCount = 0;
        Shuffles++;
        Changed();
        return true;
    }

    private void Changed()
    {
        freeDirty = true;
        Version++;
    }

    private void RefreshFree()
    {
        if (!freeDirty)
        {
            return;
        }

        freeDirty = false;
        Array.Clear(groupFree);
        var count = layout.Count;
        for (var tile = 0; tile < count; tile++)
        {
            free[tile] = present[tile] && ComputeFree(tile);
            if (free[tile])
            {
                groupFree[MahjongTiles.Group(faces[tile])]++;
            }
        }

        hasMoves = false;
        freePairs = 0;
        for (var group = 0; group < groupFree.Length; group++)
        {
            var groupCount = groupFree[group];
            if (groupCount < 2)
            {
                continue;
            }

            hasMoves = true;
            freePairs += groupCount * (groupCount - 1) / 2;
        }
    }

    private bool ComputeFree(int tile)
    {
        if (AnyPresent(layout.Above(tile)))
        {
            return false;
        }

        return !AnyPresent(layout.Left(tile)) || !AnyPresent(layout.Right(tile));
    }

    private bool AnyPresent(ReadOnlySpan<short> tiles)
    {
        for (var index = 0; index < tiles.Length; index++)
        {
            if (present[tiles[index]])
            {
                return true;
            }
        }

        return false;
    }

    private void StoreSolution(int count)
    {
        solutionCount = count;
        for (var pair = 0; pair < count / 2; pair++)
        {
            var source = count / 2 - 1 - pair;
            solution[pair * 2] = order[source * 2];
            solution[pair * 2 + 1] = order[source * 2 + 1];
        }
    }

    private bool TryFill(int count)
    {
        for (var attempt = 0; attempt < MaxFillAttempts; attempt++)
        {
            if (FillOnce(count, attempt % 2 == 1))
            {
                return true;
            }
        }

        return false;
    }

    private bool FillOnce(int count, bool lowestFirst)
    {
        Array.Clear(placed);
        var filled = 0;
        while (filled < count)
        {
            var candidateCount = CollectFirst(lowestFirst);
            if (candidateCount == 0)
            {
                return false;
            }

            var first = candidates[random.Next(candidateCount)];
            placed[first] = true;
            candidateCount = CollectSecond(first);
            if (candidateCount == 0)
            {
                return false;
            }

            var second = candidates[random.Next(candidateCount)];
            placed[second] = true;
            order[filled++] = first;
            order[filled++] = second;
        }

        return true;
    }

    private void FillInDrawOrder(int count)
    {
        for (var index = 0; index < count; index++)
        {
            order[index] = layout.DrawOrder(index);
        }
    }

    private int CollectFirst(bool lowestFirst)
    {
        var count = 0;
        var lowest = int.MaxValue;
        var tiles = layout.Count;
        for (var tile = 0; tile < tiles; tile++)
        {
            if (!Addable(tile))
            {
                continue;
            }

            var layer = layout.Layer(tile);
            if (lowestFirst && layer > lowest)
            {
                continue;
            }

            if (lowestFirst && layer < lowest)
            {
                lowest = layer;
                count = 0;
            }

            candidates[count++] = tile;
        }

        return count;
    }

    private int CollectSecond(int first)
    {
        var count = 0;
        var tiles = layout.Count;
        for (var tile = 0; tile < tiles; tile++)
        {
            if (!Addable(tile) || Contains(layout.Above(first), tile))
            {
                continue;
            }

            placed[tile] = true;
            var firstStaysFree = !AnyPlaced(layout.Left(first)) || !AnyPlaced(layout.Right(first));
            placed[tile] = false;
            if (firstStaysFree)
            {
                candidates[count++] = tile;
            }
        }

        return count;
    }

    private bool Addable(int tile)
    {
        if (!include[tile] || placed[tile])
        {
            return false;
        }

        var below = layout.Below(tile);
        for (var index = 0; index < below.Length; index++)
        {
            if (include[below[index]] && !placed[below[index]])
            {
                return false;
            }
        }

        if (AnyPlaced(layout.Above(tile)))
        {
            return false;
        }

        var left = layout.Left(tile);
        var right = layout.Right(tile);
        if (AnyPlaced(left) && AnyPlaced(right))
        {
            return false;
        }

        return !Encloses(left, true) && !Encloses(right, false);
    }

    private bool Encloses(ReadOnlySpan<short> start, bool leftward)
    {
        stamp++;
        var depth = 0;
        for (var index = 0; index < start.Length; index++)
        {
            var tile = start[index];
            if (include[tile] && !placed[tile] && walkStamp[tile] != stamp)
            {
                walkStamp[tile] = stamp;
                walk[depth++] = tile;
            }
        }

        while (depth > 0)
        {
            var tile = walk[--depth];
            var next = leftward ? layout.Left(tile) : layout.Right(tile);
            for (var index = 0; index < next.Length; index++)
            {
                var neighbour = next[index];
                if (!include[neighbour])
                {
                    continue;
                }

                if (placed[neighbour])
                {
                    return true;
                }

                if (walkStamp[neighbour] == stamp || depth >= walk.Length)
                {
                    continue;
                }

                walkStamp[neighbour] = stamp;
                walk[depth++] = neighbour;
            }
        }

        return false;
    }

    private bool AnyPlaced(ReadOnlySpan<short> tiles)
    {
        for (var index = 0; index < tiles.Length; index++)
        {
            if (include[tiles[index]] && placed[tiles[index]])
            {
                return true;
            }
        }

        return false;
    }

    private static bool Contains(ReadOnlySpan<short> tiles, int tile)
    {
        for (var index = 0; index < tiles.Length; index++)
        {
            if (tiles[index] == tile)
            {
                return true;
            }
        }

        return false;
    }

    private void SortByGroup(int count)
    {
        for (var index = 1; index < count; index++)
        {
            var current = gathered[index];
            var slot = index - 1;
            while (slot >= 0 && GroupKey(gathered[slot]) > GroupKey(current))
            {
                gathered[slot + 1] = gathered[slot];
                slot--;
            }

            gathered[slot + 1] = current;
        }
    }

    private static int GroupKey(int face) => MahjongTiles.Group(face) * MahjongTiles.FaceCount + face;

    private void Permute(Span<int> values)
    {
        for (var index = values.Length - 1; index > 0; index--)
        {
            var swap = random.Next(index + 1);
            (values[index], values[swap]) = (values[swap], values[index]);
        }
    }
}
