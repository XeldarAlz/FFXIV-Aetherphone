namespace Aetherphone.Apps.Games.Tempo;

internal sealed class TempoPlan
{
    private readonly int[] starts;
    private readonly int[] ends;
    private readonly bool[] presses;

    public TempoPlan(int capacity)
    {
        starts = new int[capacity];
        ends = new int[capacity];
        presses = new bool[capacity];
    }

    public int Count { get; private set; }

    public bool Overflowed { get; private set; }

    public int Start(int index) => starts[index];

    public int End(int index) => ends[index];

    public bool IsPress(int index) => presses[index];

    public void Clear()
    {
        Count = 0;
        Overflowed = false;
    }

    public void Add(int start, int end, bool press)
    {
        if (Count >= starts.Length)
        {
            Overflowed = true;
            return;
        }

        starts[Count] = start;
        ends[Count] = end;
        presses[Count] = press;
        Count++;
    }

    public void Reverse()
    {
        Array.Reverse(starts, 0, Count);
        Array.Reverse(ends, 0, Count);
        Array.Reverse(presses, 0, Count);
    }

    public bool Held(int tick)
    {
        var index = Find(tick);
        return index >= 0 && tick < ends[index];
    }

    public bool Pressed(int tick)
    {
        var index = Find(tick);
        return index >= 0 && presses[index] && starts[index] == tick;
    }

    private int Find(int tick)
    {
        var low = 0;
        var high = Count - 1;
        var found = -1;
        while (low <= high)
        {
            var middle = (low + high) >> 1;
            if (starts[middle] <= tick)
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
}
