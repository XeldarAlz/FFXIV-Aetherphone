namespace Aetherphone.Apps.Games.Tempo;

internal sealed class TempoAutopilot
{
    public const int DefaultMaxTicks = 4096;
    private const int MaxSegments = 3;
    private const int SegmentWidth = 3;
    private const int SegmentInts = MaxSegments * SegmentWidth;
    private const int CoinMasks = 8;
    private const int NoParent = -1;

    private readonly int maxTicks;
    private readonly int slots;
    private readonly bool trackCoins;
    private readonly bool[] reached;
    private readonly float[] stateX;
    private readonly float[] stateY;
    private readonly int[] parents;
    private readonly int[] costs;
    private readonly int[] segments;
    private readonly byte[] segmentCounts;
    private readonly int[] work = new int[SegmentInts];
    private readonly int[] finishSegments = new int[SegmentInts];
    private TempoLevel? level;
    private int workCount;
    private int finishParent;
    private int finishCount;
    private int finishCoins;
    private int finishCost;
    private int finishTick;
    private bool found;

    public TempoAutopilot(int maxTicks = DefaultMaxTicks, bool trackCoins = false)
    {
        this.maxTicks = maxTicks;
        this.trackCoins = trackCoins;
        slots = trackCoins ? 2 * CoinMasks : 2;
        var states = maxTicks * slots;
        reached = new bool[states];
        stateX = new float[states];
        stateY = new float[states];
        parents = new int[states];
        costs = new int[states];
        segments = new int[states * SegmentInts];
        segmentCounts = new byte[states];
    }

    public int PlannedCoins { get; private set; }

    public int PlannedTicks { get; private set; }

    public float FurthestX { get; private set; }

    public bool Plan(TempoLevel target, TempoPlan plan)
    {
        level = target;
        plan.Clear();
        Array.Clear(reached);
        found = false;
        finishCoins = -1;
        finishCost = 0;
        PlannedCoins = 0;
        PlannedTicks = 0;
        FurthestX = 0f;
        var start = TempoPhysics.Start(target);
        workCount = 0;
        Record(start, NoParent);
        for (var tick = 0; tick < maxTicks; tick++)
        {
            for (var slot = 0; slot < slots; slot++)
            {
                var index = tick * slots + slot;
                if (!reached[index])
                {
                    continue;
                }

                var runner = Rebuild(tick, slot, index);
                workCount = 0;
                Advance(runner, index, false, false);
                workCount = 0;
                AddWork(tick + 1, tick + 2, true);
                Advance(runner, index, true, true);
            }
        }

        if (!found)
        {
            return false;
        }

        for (var segment = finishCount - 1; segment >= 0; segment--)
        {
            AddSegment(plan, finishSegments, segment * SegmentWidth);
        }

        var parent = finishParent;
        while (parent != NoParent)
        {
            for (var segment = segmentCounts[parent] - 1; segment >= 0; segment--)
            {
                AddSegment(plan, segments, parent * SegmentInts + segment * SegmentWidth);
            }

            parent = parents[parent];
        }

        plan.Reverse();
        PlannedCoins = finishCoins;
        PlannedTicks = finishTick;
        return !plan.Overflowed;
    }

    private TempoRunner Rebuild(int tick, int slot, int index)
    {
        var runner = default(TempoRunner);
        runner.X = stateX[index];
        runner.Y = stateY[index];
        runner.PreviousX = runner.X;
        runner.PreviousY = runner.Y;
        runner.Tick = tick;
        runner.Gravity = (slot & 1) == 0 ? (sbyte)1 : (sbyte)-1;
        runner.Coins = trackCoins ? (byte)(slot >> 1) : (byte)0;
        runner.Grounded = true;
        runner.Alive = true;
        return runner;
    }

    private int SlotOf(in TempoRunner runner) =>
        (runner.Gravity > 0 ? 0 : 1) + (trackCoins ? runner.Coins * 2 : 0);

    private void Advance(TempoRunner runner, int parent, bool held, bool pressed)
    {
        var signals = TempoPhysics.Tick(ref runner, level!, held, pressed);
        Continue(runner, signals, parent);
    }

    private void Continue(TempoRunner runner, TempoSignal signals, int parent)
    {
        while (true)
        {
            if ((signals & TempoSignal.Died) != 0 || runner.Tick >= maxTicks)
            {
                return;
            }

            if ((signals & TempoSignal.Finished) != 0)
            {
                RecordFinish(runner, parent);
                return;
            }

            if (runner.Grounded)
            {
                Record(runner, parent);
                return;
            }

            if ((signals & TempoSignal.Pad) != 0 && workCount < SegmentInts)
            {
                var saved = workCount;
                Hold(runner, parent);
                workCount = saved;
            }

            signals = TempoPhysics.Tick(ref runner, level!, false, false);
        }
    }

    private void Hold(TempoRunner runner, int parent)
    {
        var first = runner.Tick + 1;
        var signals = TempoSignal.None;
        while (runner.VelocityY * runner.Gravity > 0f && runner.Alive && !runner.Finished && !runner.Grounded &&
               runner.Tick < maxTicks)
        {
            signals = TempoPhysics.Tick(ref runner, level!, true, false);
        }

        AddWork(first, runner.Tick + 1, false);
        Continue(runner, signals & ~TempoSignal.Pad, parent);
    }

    private static void AddSegment(TempoPlan plan, int[] source, int offset)
    {
        plan.Add(source[offset], source[offset + 1], source[offset + 2] != 0);
    }

    private void AddWork(int start, int end, bool press)
    {
        if (workCount >= SegmentInts)
        {
            return;
        }

        work[workCount++] = start;
        work[workCount++] = end;
        work[workCount++] = press ? 1 : 0;
    }

    private void Record(in TempoRunner runner, int parent)
    {
        if (runner.Tick >= maxTicks)
        {
            return;
        }

        var index = runner.Tick * slots + SlotOf(runner);
        var cost = CostThrough(parent);
        if (reached[index] && cost >= costs[index])
        {
            return;
        }

        reached[index] = true;
        costs[index] = cost;
        FurthestX = MathF.Max(FurthestX, runner.X);
        stateX[index] = runner.X;
        stateY[index] = runner.Y;
        parents[index] = parent;
        segmentCounts[index] = (byte)(workCount / SegmentWidth);
        Array.Copy(work, 0, segments, index * SegmentInts, workCount);
    }

    private int CostThrough(int parent) => (parent == NoParent ? 0 : costs[parent]) + workCount / SegmentWidth;

    private void RecordFinish(in TempoRunner runner, int parent)
    {
        var coins = runner.CoinsCollected;
        var cost = CostThrough(parent);
        if (found && (coins < finishCoins || (coins == finishCoins && cost >= finishCost)))
        {
            return;
        }

        found = true;
        finishCoins = coins;
        finishCost = cost;
        finishParent = parent;
        finishTick = runner.Tick;
        finishCount = workCount / SegmentWidth;
        Array.Copy(work, finishSegments, workCount);
    }
}
