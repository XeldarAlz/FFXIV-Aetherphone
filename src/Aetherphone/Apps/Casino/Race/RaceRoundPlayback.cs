using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;

namespace Aetherphone.Apps.Casino.Race;

internal enum RaceStage : byte
{
    Waiting,
    Paddock,
    Gates,
    Running,
    Replay,
    Finished,
    Result,
}

internal enum RaceCue : byte
{
    None,
    Off,
    Surge,
    LeadChange,
    Fade,
    FinalStretch,
    PhotoFinish,
    Winner,
    Finish,
}

internal readonly record struct RaceCueEvent(RaceCue Cue, int Slot);

internal sealed class RaceRoundPlayback
{
    public const int ReplayWindowSubTicks = 30 * RaceScript.SubTicks;
    public const long ReplayMilliseconds = 3000;
    public const long FinalStretchMilliseconds = 5000;
    public const long JumpMilliseconds = 1500;
    public const int LeadChangeQuietTicks = 3 * RaceScript.TicksPerSecond;
    public const int ContenderPlaces = 4;

    private const int CueCapacity = 32;

    private readonly int[] order = new int[RaceRules.FieldSize];
    private readonly int[] positions = new int[RaceRules.FieldSize];
    private readonly int[] ranking = new int[RaceRules.FieldSize];
    private readonly int[] surgeCursor = new int[RaceRules.FieldSize];
    private readonly bool[] fadeSeen = new bool[RaceRules.FieldSize];
    private readonly bool[] crossed = new bool[RaceRules.FieldSize];
    private readonly RaceCueEvent[] cues = new RaceCueEvent[CueCapacity];

    private RaceScriptPlan? plan;
    private string roomId = string.Empty;
    private long roundIndex = -1;
    private string seed = string.Empty;
    private RaceStage stage = RaceStage.Waiting;
    private long raceStartUnixMs;
    private long elapsedMilliseconds = long.MinValue;
    private long subTick;
    private long winnerFinishSubTick;
    private long lastFinishSubTick;
    private int leader = -1;
    private int cueHead;
    private int cueCount;
    private bool offSeen;
    private bool stretchSeen;
    private bool watchedLive;
    private bool observed;
    private float replayProgress;

    public RaceStage Stage => stage;

    public RaceScriptPlan? Plan => plan;

    public string Seed => seed;

    public ReadOnlySpan<int> Positions => positions;

    public ReadOnlySpan<int> Ranking => ranking;

    public ReadOnlySpan<int> Order => order;

    public bool HasOrder => plan is not null;

    public int Leader => leader;

    public long SubTick => subTick;

    public long DisplaySubTick { get; private set; }

    public long ElapsedMilliseconds => elapsedMilliseconds == long.MinValue ? 0 : elapsedMilliseconds;

    public bool WatchedLive => watchedLive;

    public bool PhotoFinish => plan?.PhotoFinish ?? false;

    public float ReplayProgress => replayProgress;

    public long RoundIndex => roundIndex;

    public string RoundKey { get; private set; } = string.Empty;

    public float FinalStretch
    {
        get
        {
            if (plan is null || stage != RaceStage.Running)
            {
                return 0f;
            }

            var left = RaceRules.MillisecondsOf(winnerFinishSubTick) - ElapsedMilliseconds;
            if (left >= FinalStretchMilliseconds)
            {
                return 0f;
            }

            return Math.Clamp(1f - left / (float)FinalStretchMilliseconds, 0f, 1f);
        }
    }

    public bool Crossed(int slot) => RaceRules.IsRunner(slot) && crossed[slot];

    public void Reset()
    {
        plan = null;
        roomId = string.Empty;
        roundIndex = -1;
        seed = string.Empty;
        RoundKey = string.Empty;
        ClearRound();
    }

    public bool TryTakeCue(out RaceCueEvent cue)
    {
        if (cueCount == 0)
        {
            cue = default;
            return false;
        }

        cue = cues[cueHead];
        cueHead = (cueHead + 1) % CueCapacity;
        cueCount--;
        return true;
    }

    public void Update(CasinoRoomSnapshotDto? snapshot, CasinoRaceRoomStateDto? board, long serverNowUnixMs,
        bool instant)
    {
        if (snapshot is null)
        {
            stage = RaceStage.Waiting;
            return;
        }

        if (snapshot.RoundIndex != roundIndex || !string.Equals(snapshot.RoomId, roomId, StringComparison.Ordinal))
        {
            OpenRound(snapshot.RoomId, snapshot.RoundIndex);
        }

        if (snapshot.Phase == CasinoRoomPhases.Open)
        {
            stage = board?.Runners is { Length: RaceRules.FieldSize } ? RaceStage.Paddock : RaceStage.Waiting;
            return;
        }

        TryBuildPlan(board);
        if (board is not null && board.RaceStartUnixMs > 0)
        {
            raceStartUnixMs = board.RaceStartUnixMs;
        }

        if (plan is null)
        {
            stage = snapshot.Phase == CasinoRoomPhases.Result ? RaceStage.Result : RaceStage.Gates;
            return;
        }

        if (snapshot.Phase == CasinoRoomPhases.Result)
        {
            SettleAt(RaceStage.Result, lastFinishSubTick);
            return;
        }

        if (instant)
        {
            SettleAt(RaceStage.Finished, lastFinishSubTick);
            return;
        }

        if (raceStartUnixMs <= 0)
        {
            stage = RaceStage.Gates;
            return;
        }

        Advance(serverNowUnixMs - raceStartUnixMs);
    }

    internal void Advance(long elapsed)
    {
        if (plan is null)
        {
            return;
        }

        var jumped = elapsedMilliseconds == long.MinValue
            ? elapsed > JumpMilliseconds
            : elapsed < elapsedMilliseconds || elapsed - elapsedMilliseconds > JumpMilliseconds;
        elapsedMilliseconds = elapsed;
        var raceSubTick = elapsed <= 0 ? 0 : elapsed * RaceScript.SubTicks / RaceRules.MillisecondsPerTick;
        if (!observed)
        {
            observed = true;
            watchedLive = raceSubTick < winnerFinishSubTick;
        }

        if (raceSubTick < lastFinishSubTick)
        {
            stage = RaceStage.Running;
            replayProgress = 0f;
            Place(raceSubTick, !jumped);
            return;
        }

        Place(lastFinishSubTick, !jumped);
        var replayElapsed = elapsed - RaceRules.MillisecondsOf(lastFinishSubTick);
        if (replayElapsed < ReplayMilliseconds)
        {
            stage = RaceStage.Replay;
            replayProgress = Math.Clamp(replayElapsed / (float)ReplayMilliseconds, 0f, 1f);
            PlacePositions(ReplaySubTick(replayProgress));
            return;
        }

        replayProgress = 1f;
        stage = RaceStage.Finished;
        PlacePositions(lastFinishSubTick);
    }

    public long ReplaySubTick(float progress)
    {
        var start = winnerFinishSubTick - ReplayWindowSubTicks / 2;
        return start + (long)(ReplayWindowSubTicks * Math.Clamp(progress, 0f, 1f));
    }

    private void OpenRound(string nextRoomId, long nextRoundIndex)
    {
        roomId = nextRoomId;
        roundIndex = nextRoundIndex;
        RoundKey = Cabinets.RoundKeys.Of(nextRoomId, nextRoundIndex);
        plan = null;
        seed = string.Empty;
        ClearRound();
    }

    private void ClearRound()
    {
        stage = RaceStage.Waiting;
        raceStartUnixMs = 0;
        elapsedMilliseconds = long.MinValue;
        subTick = 0;
        DisplaySubTick = 0;
        winnerFinishSubTick = 0;
        lastFinishSubTick = 0;
        leader = -1;
        cueHead = 0;
        cueCount = 0;
        offSeen = false;
        stretchSeen = false;
        watchedLive = false;
        observed = false;
        replayProgress = 0f;
        Array.Clear(positions);
        Array.Clear(surgeCursor);
        Array.Clear(fadeSeen);
        Array.Clear(crossed);
        for (var slot = 0; slot < RaceRules.FieldSize; slot++)
        {
            order[slot] = slot;
            ranking[slot] = slot;
        }
    }

    private void TryBuildPlan(CasinoRaceRoomStateDto? board)
    {
        if (plan is not null || board is null || board.Seed.Length == 0
            || board.Order is not { Length: RaceRules.FieldSize } drawn || !IsPermutation(drawn))
        {
            return;
        }

        Array.Copy(drawn, order, RaceRules.FieldSize);
        seed = board.Seed;
        plan = RaceScript.Build(order, seed);
        winnerFinishSubTick = plan.FinishSubTicks[order[0]];
        lastFinishSubTick = plan.FinishSubTicks[order[RaceRules.FieldSize - 1]];
    }

    internal static bool IsPermutation(ReadOnlySpan<int> slots)
    {
        if (slots.Length != RaceRules.FieldSize)
        {
            return false;
        }

        Span<bool> seen = stackalloc bool[RaceRules.FieldSize];
        for (var index = 0; index < slots.Length; index++)
        {
            var slot = slots[index];
            if (!RaceRules.IsRunner(slot) || seen[slot])
            {
                return false;
            }

            seen[slot] = true;
        }

        return true;
    }

    private void SettleAt(RaceStage settled, long finalSubTick)
    {
        if (!observed)
        {
            observed = true;
            watchedLive = false;
        }

        Place(finalSubTick, false);
        replayProgress = 1f;
        stage = settled;
        for (var place = 0; place < RaceRules.FieldSize; place++)
        {
            ranking[place] = order[place];
        }

        leader = order[0];
    }

    private void Place(long nextSubTick, bool announce)
    {
        var current = plan!;
        var previousSubTick = subTick;
        subTick = nextSubTick;
        PlacePositions(nextSubTick);
        var nextLeader = ranking[0];
        if (announce && !offSeen && nextSubTick > 0)
        {
            Push(RaceCue.Off, -1);
        }

        offSeen |= nextSubTick > 0;
        var tick = (int)(nextSubTick / RaceScript.SubTicks);
        var previousTick = (int)(previousSubTick / RaceScript.SubTicks);
        for (var slot = 0; slot < RaceRules.FieldSize; slot++)
        {
            ScanSurges(current, slot, tick, previousTick, announce);
            if (!fadeSeen[slot] && tick >= current.FadeTick[slot])
            {
                fadeSeen[slot] = true;
                if (announce && slot == nextLeader)
                {
                    Push(RaceCue.Fade, slot);
                }
            }
        }

        if (leader >= 0 && nextLeader != leader && announce && tick >= LeadChangeQuietTicks)
        {
            Push(RaceCue.LeadChange, nextLeader);
        }

        leader = nextLeader;
        var stretchAt = winnerFinishSubTick
                        - FinalStretchMilliseconds * RaceScript.SubTicks / RaceRules.MillisecondsPerTick;
        if (!stretchSeen && nextSubTick >= stretchAt && nextSubTick < winnerFinishSubTick)
        {
            stretchSeen = true;
            if (announce)
            {
                Push(RaceCue.FinalStretch, -1);
            }
        }

        for (var place = 0; place < RaceRules.FieldSize; place++)
        {
            var slot = order[place];
            if (crossed[slot] || nextSubTick < current.FinishSubTicks[slot])
            {
                continue;
            }

            crossed[slot] = true;
            stretchSeen = true;
            if (!announce)
            {
                continue;
            }

            if (place == 0)
            {
                if (current.PhotoFinish)
                {
                    Push(RaceCue.PhotoFinish, slot);
                }

                Push(RaceCue.Winner, slot);
                continue;
            }

            Push(RaceCue.Finish, slot);
        }
    }

    private void ScanSurges(RaceScriptPlan current, int slot, int tick, int previousTick, bool announce)
    {
        var starts = current.Surges[slot];
        while (surgeCursor[slot] < starts.Length && starts[surgeCursor[slot]] <= tick)
        {
            var start = starts[surgeCursor[slot]];
            surgeCursor[slot]++;
            if (announce && start > previousTick && RankOf(slot) < ContenderPlaces && !crossed[slot])
            {
                Push(RaceCue.Surge, slot);
            }
        }
    }

    private void PlacePositions(long atSubTick)
    {
        var current = plan!;
        DisplaySubTick = atSubTick;
        for (var slot = 0; slot < RaceRules.FieldSize; slot++)
        {
            positions[slot] = RaceScript.PositionAt(current, slot, atSubTick);
        }

        Rank(current);
    }

    private void Rank(RaceScriptPlan current)
    {
        for (var slot = 0; slot < RaceRules.FieldSize; slot++)
        {
            ranking[slot] = slot;
        }

        for (var outer = 1; outer < RaceRules.FieldSize; outer++)
        {
            var slot = ranking[outer];
            var inner = outer - 1;
            while (inner >= 0 && Ahead(current, slot, ranking[inner]))
            {
                ranking[inner + 1] = ranking[inner];
                inner--;
            }

            ranking[inner + 1] = slot;
        }
    }

    private bool Ahead(RaceScriptPlan current, int slot, int other)
    {
        if (positions[slot] != positions[other])
        {
            return positions[slot] > positions[other];
        }

        return current.FinishSubTicks[slot] < current.FinishSubTicks[other];
    }

    private int RankOf(int slot)
    {
        for (var place = 0; place < RaceRules.FieldSize; place++)
        {
            if (ranking[place] == slot)
            {
                return place;
            }
        }

        return RaceRules.FieldSize;
    }

    private void Push(RaceCue cue, int slot)
    {
        if (cueCount == CueCapacity)
        {
            cueHead = (cueHead + 1) % CueCapacity;
            cueCount--;
        }

        cues[(cueHead + cueCount) % CueCapacity] = new RaceCueEvent(cue, slot);
        cueCount++;
    }
}
