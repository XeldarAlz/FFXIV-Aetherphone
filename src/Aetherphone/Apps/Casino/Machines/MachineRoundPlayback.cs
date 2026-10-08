using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Casino;

namespace Aetherphone.Apps.Casino.Machines;

internal enum MachineBeat : byte
{
    Idle,
    Spin,
    Present,
    Tumble,
    Expand,
    Intro,
    Buy,
    Hold,
    Respin,
    Collect,
    Meter,
    Outro,
    Done,
}

internal sealed class MachineRoundPlayback
{
    public const int MaxReels = 6;
    public const int MaxCells = 30;
    public const int PickerLength = 14;
    public const int BlurCycle = 64;

    private const int MaxBeatsPerUpdate = 512;
    private const ulong FnvOffset = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    private static readonly CasinoSlotsStepDto[] NoSteps = Array.Empty<CasinoSlotsStepDto>();

    private readonly float[] stops = new float[MaxReels];
    private readonly int[] blurOffsets = new int[MaxReels];
    private readonly int[] picker = new int[PickerLength];
    private readonly int[] reelCounts = new int[MaxReels];

    private CasinoSlotsStepDto[] steps = NoSteps;
    private SlotsMachineInfo info = SlotsMachines.Bird;
    private MachineBeat beat;
    private int stepIndex;
    private int featureStart = -1;
    private bool introShown;
    private float beatSeconds;
    private float beatLength;
    private int firstAnticipated = -1;
    private int lastAnticipated = -1;
    private long committed;
    private int featureTotal;
    private int featurePlayed;

    public bool Turbo { get; set; }

    public string RoundId { get; private set; } = string.Empty;

    public string MachineId => info.Id;

    public SlotsMachineInfo Info => info;

    public string Mode { get; private set; } = SlotsRules.BaseMode;

    public long Bet { get; private set; }

    public long Cost { get; private set; }

    public long TotalWin { get; private set; }

    public long Jackpot { get; private set; }

    public bool CapApplied { get; private set; }

    public bool BonusTriggered { get; private set; }

    public int Expander { get; private set; } = -1;

    public int FeatureMultiplier { get; private set; }

    public MachineBeat Beat => beat;

    public int StepIndex => stepIndex;

    public int StepCount => steps.Length;

    public float BeatSeconds => beatSeconds;

    public float BeatLength => beatLength;

    public float BeatProgress => beatLength <= 0f ? 1f : Math.Clamp(beatSeconds / beatLength, 0f, 1f);

    public long Committed => committed;

    public int FeatureTotal => featureTotal;

    public int FeaturePlayed => featurePlayed;

    public bool Active => beat != MachineBeat.Idle && beat != MachineBeat.Done;

    public bool Finished => beat == MachineBeat.Done;

    public bool HasRound => steps.Length > 0;

    public bool InFeature => featureStart >= 0 && stepIndex >= featureStart && beat != MachineBeat.Done
        && beat != MachineBeat.Idle;

    public bool InHold => beat is MachineBeat.Hold or MachineBeat.Respin or MachineBeat.Collect
        || (beat == MachineBeat.Present && IsHoldKind(Current.Kind));

    public CasinoSlotsStepDto Current => steps.Length == 0 ? EmptyStep : steps[stepIndex];

    public CasinoSlotsStepDto? Previous => stepIndex > 0 ? steps[stepIndex - 1] : null;

    public int FirstAnticipatedReel => firstAnticipated;

    public int LastAnticipatedReel => lastAnticipated;

    private static readonly CasinoSlotsStepDto EmptyStep = new();

    public CasinoSlotsStepDto StepAt(int index) => steps[index];

    public void Reset()
    {
        steps = NoSteps;
        beat = MachineBeat.Idle;
        stepIndex = 0;
        featureStart = -1;
        introShown = false;
        beatSeconds = 0f;
        beatLength = 0f;
        committed = 0;
        featureTotal = 0;
        featurePlayed = 0;
        firstAnticipated = -1;
        lastAnticipated = -1;
        RoundId = string.Empty;
        TotalWin = 0;
        Jackpot = 0;
        Bet = 0;
        Cost = 0;
    }

    public bool Begin(CasinoSlotsSpinDto round)
    {
        if (!IsPlayable(round))
        {
            return false;
        }

        Reset();
        info = SlotsMachines.For(round.MachineId);
        steps = round.Steps!;
        RoundId = round.RoundId;
        Mode = round.Mode.Length > 0 ? round.Mode : SlotsRules.BaseMode;
        Bet = round.Bet > 0 ? round.Bet : round.Stake;
        Cost = round.Stake;
        TotalWin = round.TotalWin;
        Jackpot = round.Jackpot;
        CapApplied = round.CapApplied;
        BonusTriggered = round.BonusTriggered;
        Expander = round.Expander;
        FeatureMultiplier = round.FeatureMultiplier;
        featureStart = FeatureStartOf(steps);
        Seed(round.RoundId, round.Expander);
        EnterStep(0);
        return true;
    }

    public static bool IsPlayable(CasinoSlotsSpinDto round)
    {
        if (!round.Granted || round.Steps is not { Length: > 0 } list || !SlotsRules.IsMachine(round.MachineId))
        {
            return false;
        }

        var cells = SlotsMachines.For(round.MachineId).CellCount;
        for (var index = 0; index < list.Length; index++)
        {
            var step = list[index];
            if (step is null)
            {
                return false;
            }

            if (HasGrid(step.Kind) && (step.Grid is null || step.Grid.Length != cells))
            {
                return false;
            }
        }

        return true;
    }

    public void Update(float deltaSeconds)
    {
        var remaining = deltaSeconds;
        var guard = 0;
        while (Active && remaining > 0f && guard < MaxBeatsPerUpdate)
        {
            guard++;
            var left = beatLength - beatSeconds;
            if (remaining < left)
            {
                beatSeconds += remaining;
                return;
            }

            remaining -= MathF.Max(0f, left);
            beatSeconds = beatLength;
            Advance();
        }
    }

    public void Skip()
    {
        if (steps.Length == 0)
        {
            return;
        }

        stepIndex = steps.Length - 1;
        CountAllFeatureSteps();

        committed = TotalWin;
        beat = MachineBeat.Done;
        beatSeconds = 0f;
        beatLength = 0f;
        firstAnticipated = -1;
        lastAnticipated = -1;
    }

    public float StopSeconds(int reel) => stops[Math.Clamp(reel, 0, MaxReels - 1)];

    public bool ReelStopped(int reel) => beat != MachineBeat.Spin || beatSeconds >= StopSeconds(reel);

    public float LandingProgress(int reel)
    {
        if (beat != MachineBeat.Spin)
        {
            return 1f;
        }

        var landing = MachineTiming.Scaled(MachineTiming.Landing, Turbo);
        var start = StopSeconds(reel) - landing;
        if (beatSeconds <= start)
        {
            return 0f;
        }

        return Math.Clamp((beatSeconds - start) / landing, 0f, 1f);
    }

    public bool Anticipating(int reel)
    {
        return beat == MachineBeat.Spin && firstAnticipated >= 0 && reel >= firstAnticipated
            && reel <= lastAnticipated && !ReelStopped(reel) && ReelStopped(firstAnticipated - 1);
    }

    public int StoppedReels()
    {
        var stopped = 0;
        while (stopped < info.Reels && ReelStopped(stopped))
        {
            stopped++;
        }

        return stopped;
    }

    public int BlurOffset(int reel) => blurOffsets[Math.Clamp(reel, 0, MaxReels - 1)];

    public int PickerSymbol(float progress)
    {
        var eased = Easing.EaseOutCubic(Math.Clamp(progress, 0f, 1f));
        return picker[Math.Clamp((int)(eased * (PickerLength - 1) + 0.5f), 0, PickerLength - 1)];
    }

    public int PickerAt(int index) => picker[Math.Clamp(index, 0, PickerLength - 1)];

    public int CollectedCoins()
    {
        if (beat != MachineBeat.Collect)
        {
            return beat == MachineBeat.Present || beat == MachineBeat.Done ? int.MaxValue : 0;
        }

        var per = MachineTiming.Scaled(MachineTiming.CollectPerCoin, Turbo);
        var start = MachineTiming.Scaled(MachineTiming.CollectBase, Turbo) * 0.5f;
        return per <= 0f ? int.MaxValue : (int)MathF.Max(0f, (beatSeconds - start) / per);
    }

    public bool IsNewCoin(int cell)
    {
        var previous = Previous;
        if (previous?.Coins is not { } before)
        {
            return true;
        }

        for (var index = 0; index < before.Length; index++)
        {
            if (before[index].Cell == cell)
            {
                return false;
            }
        }

        return true;
    }

    public int SpinsLeftShown()
    {
        if (!InFeature && beat != MachineBeat.Done)
        {
            return 0;
        }

        return Math.Max(0, Current.SpinsLeft);
    }

    private void Advance()
    {
        switch (beat)
        {
            case MachineBeat.Spin:
            case MachineBeat.Tumble:
            case MachineBeat.Expand:
            case MachineBeat.Collect:
                Land();
                Start(MachineBeat.Present, PresentLength(stepIndex));
                return;
            case MachineBeat.Intro:
                StartStepBeat(stepIndex);
                return;
            case MachineBeat.Buy:
            case MachineBeat.Hold:
            case MachineBeat.Respin:
            case MachineBeat.Meter:
                Land();
                NextStep();
                return;
            case MachineBeat.Present:
                NextStep();
                return;
            case MachineBeat.Outro:
                Finish();
                return;
        }
    }

    private void Land()
    {
        committed = Math.Min(Math.Max(committed, Current.Running), TotalWin);
        var current = Current;
        if (string.Equals(current.Kind, SlotsRules.StepBuy, StringComparison.Ordinal))
        {
            introShown = true;
        }

        if (info.Layout == SlotsLayout.Cluster && current.Multiplier > 1 && IsFeatureKindAt(stepIndex)
            && IsLastOfFreeSpin(stepIndex))
        {
            featureTotal = current.Multiplier;
        }
    }

    private void NextStep()
    {
        if (stepIndex + 1 >= steps.Length)
        {
            if (BonusTriggered && beat != MachineBeat.Outro)
            {
                Start(MachineBeat.Outro, MachineTiming.Scaled(MachineTiming.Outro, Turbo));
                return;
            }

            Finish();
            return;
        }

        EnterStep(stepIndex + 1);
    }

    private void Finish()
    {
        committed = TotalWin;
        beat = MachineBeat.Done;
        beatSeconds = 0f;
        beatLength = 0f;
        firstAnticipated = -1;
        lastAnticipated = -1;
    }

    private void EnterStep(int index)
    {
        stepIndex = index;
        var kind = steps[index].Kind;
        if (index == featureStart && !introShown && !string.Equals(kind, SlotsRules.StepBuy, StringComparison.Ordinal))
        {
            introShown = true;
            var length = info.Layout == SlotsLayout.Lines ? MachineTiming.PickerIntro : MachineTiming.Intro;
            Start(MachineBeat.Intro, MachineTiming.Scaled(length, Turbo));
            return;
        }

        StartStepBeat(index);
    }

    private void StartStepBeat(int index)
    {
        var step = steps[index];
        if (IsSpinKind(step.Kind))
        {
            featurePlayed++;
        }

        switch (step.Kind)
        {
            case SlotsRules.StepBase:
            case SlotsRules.StepFree:
            case SlotsRules.StepGame:
                Schedule(index);
                Start(MachineBeat.Spin, stops[info.Reels - 1]);
                return;
            case SlotsRules.StepTumble:
                Start(MachineBeat.Tumble, MachineTiming.Scaled(MachineTiming.Shatter + MachineTiming.Fall, Turbo));
                return;
            case SlotsRules.StepExpand:
                Start(MachineBeat.Expand, MachineTiming.Scaled(MachineTiming.Expand, Turbo));
                return;
            case SlotsRules.StepBuy:
                Start(MachineBeat.Buy, MachineTiming.Scaled(MachineTiming.Buy, Turbo));
                return;
            case SlotsRules.StepHold:
                Start(MachineBeat.Hold, MachineTiming.Scaled(MachineTiming.Hold, Turbo));
                return;
            case SlotsRules.StepRespin:
                var landed = Landed(index);
                Start(MachineBeat.Respin,
                    MachineTiming.Scaled(MachineTiming.Respin + (landed ? MachineTiming.RespinLanded : 0f), Turbo));
                return;
            case SlotsRules.StepCollect:
                var coins = step.Coins?.Length ?? 0;
                var grand = HasWinLine(step, SlotsRules.LineGrand) ? MachineTiming.Grand : 0f;
                Start(MachineBeat.Collect,
                    MachineTiming.Scaled(MachineTiming.CollectBase + MachineTiming.CollectPerCoin * coins + grand,
                        Turbo));
                return;
            case SlotsRules.StepMeter:
                Start(MachineBeat.Meter, MachineTiming.Scaled(MachineTiming.Meter, Turbo));
                return;
            default:
                Start(MachineBeat.Present, PresentLength(index));
                return;
        }
    }

    private static bool IsSpinKind(string kind)
    {
        return kind is SlotsRules.StepFree or SlotsRules.StepGame;
    }

    private void CountAllFeatureSteps()
    {
        featurePlayed = 0;
        for (var scan = 0; scan < steps.Length; scan++)
        {
            if (IsSpinKind(steps[scan].Kind))
            {
                featurePlayed++;
            }
        }

        featureTotal = Math.Max(featureTotal, FeatureMultiplier);
    }

    private void Start(MachineBeat next, float length)
    {
        beat = next;
        beatSeconds = 0f;
        beatLength = MathF.Max(0f, length);
    }

    private float PresentLength(int index)
    {
        var step = steps[index];
        var wins = step.Wins is { Length: > 0 } || step.Pay > 0;
        var length = wins ? MachineTiming.WinPresent : MachineTiming.LossPresent;
        if (string.Equals(step.Kind, SlotsRules.StepTumble, StringComparison.Ordinal) && !wins)
        {
            length = MachineTiming.LossPresent;
        }

        if (step.SpinsAdded > 0 && index > 0 && featureStart >= 0 && index >= featureStart)
        {
            length += MachineTiming.RetriggerPresent;
        }

        if (info.Layout == SlotsLayout.Cluster && step.Multiplier > 1 && IsFeatureKindAt(index)
            && IsLastOfFreeSpin(index))
        {
            length += MachineTiming.OrbFlight;
        }

        return MachineTiming.Scaled(length, Turbo);
    }

    private void Schedule(int index)
    {
        var reels = info.Reels;
        Anticipation(index, out firstAnticipated, out lastAnticipated);
        var extra = 0f;
        for (var reel = 0; reel < MaxReels; reel++)
        {
            if (reel < reels && firstAnticipated >= 0 && reel >= firstAnticipated && reel <= lastAnticipated)
            {
                extra += MachineTiming.Anticipation;
            }

            var stop = MachineTiming.Ramp + MachineTiming.FirstStop + MachineTiming.Stagger * reel + extra;
            if (reel == reels - 1)
            {
                stop += MachineTiming.MicroPause;
            }

            stops[reel] = MachineTiming.Scaled(stop, Turbo);
        }
    }

    private void Anticipation(int index, out int first, out int last)
    {
        first = -1;
        last = -1;
        var step = steps[index];
        var grid = step.Grid;
        if (grid is null)
        {
            return;
        }

        switch (info.Layout)
        {
            case SlotsLayout.Lines:
                if (step.SpinsAdded > 0)
                {
                    CountSymbol(grid, GoldenBirdRules.Scatter);
                    Window(GoldenBirdRules.TriggerScatters, ref first, ref last);
                }

                return;
            case SlotsLayout.Cluster:
                if (ChainTriggers(index))
                {
                    CountSymbol(grid, CrystalCascadeRules.Scatter);
                    var need = string.Equals(step.Kind, SlotsRules.StepBase, StringComparison.Ordinal)
                        ? CrystalCascadeRules.TriggerScatters
                        : CrystalCascadeRules.RetriggerScatters;
                    Window(need, ref first, ref last);
                }

                return;
            default:
                if (index + 1 < steps.Length
                    && string.Equals(steps[index + 1].Kind, SlotsRules.StepHold, StringComparison.Ordinal))
                {
                    CountCoins(step);
                    Window(MoogleMoneyRules.TriggerCoins, ref first, ref last);
                }

                if (step.SpinsAdded > 0)
                {
                    var coinFirst = first;
                    var coinLast = last;
                    CountSymbol(grid, MoogleMoneyRules.Scatter);
                    Window(MoogleMoneyRules.FreeGameScatters, ref first, ref last);
                    if (coinFirst >= 0)
                    {
                        first = first < 0 ? coinFirst : Math.Min(first, coinFirst);
                        last = Math.Max(last, coinLast);
                    }
                }

                return;
        }
    }

    private void Window(int need, ref int first, ref int last)
    {
        var reels = info.Reels;
        var seen = 0;
        var from = -1;
        for (var reel = 0; reel < reels; reel++)
        {
            seen += reelCounts[reel];
            if (from < 0 && seen >= need - 1 && reel + 1 < reels)
            {
                from = reel + 1;
                if (seen >= need)
                {
                    from = -1;
                    break;
                }

                continue;
            }

            if (from >= 0 && seen >= need)
            {
                first = from;
                last = reel;
                return;
            }
        }

        if (from >= 0)
        {
            first = from;
            last = reels - 1;
        }
    }

    private void CountSymbol(int[] grid, int symbol)
    {
        Array.Clear(reelCounts);
        var rows = info.Rows;
        for (var cell = 0; cell < grid.Length; cell++)
        {
            if (grid[cell] == symbol)
            {
                reelCounts[Math.Min(cell / rows, MaxReels - 1)]++;
            }
        }
    }

    private void CountCoins(CasinoSlotsStepDto step)
    {
        Array.Clear(reelCounts);
        if (step.Coins is not { } coins)
        {
            return;
        }

        for (var index = 0; index < coins.Length; index++)
        {
            var cell = coins[index].Cell;
            if (cell >= 0)
            {
                reelCounts[Math.Min(cell / info.Rows, MaxReels - 1)]++;
            }
        }
    }

    private bool ChainTriggers(int index)
    {
        for (var scan = index; scan < steps.Length; scan++)
        {
            if (scan > index && !string.Equals(steps[scan].Kind, SlotsRules.StepTumble, StringComparison.Ordinal))
            {
                return steps[scan - 1].SpinsAdded > 0;
            }
        }

        return steps[^1].SpinsAdded > 0;
    }

    private bool IsLastOfFreeSpin(int index)
    {
        return index + 1 >= steps.Length
            || !string.Equals(steps[index + 1].Kind, SlotsRules.StepTumble, StringComparison.Ordinal);
    }

    private bool IsFeatureKindAt(int index)
    {
        return featureStart >= 0 && index >= featureStart;
    }

    private bool Landed(int index)
    {
        var current = steps[index].Coins?.Length ?? 0;
        var before = index > 0 ? steps[index - 1].Coins?.Length ?? 0 : 0;
        return current > before;
    }

    private void Seed(string roundId, int expander)
    {
        var hash = FnvOffset;
        for (var index = 0; index < roundId.Length; index++)
        {
            hash ^= roundId[index];
            hash *= FnvPrime;
        }

        var random = GameRandom.FromSeed(hash);
        for (var reel = 0; reel < MaxReels; reel++)
        {
            blurOffsets[reel] = random.Next(BlurCycle);
        }

        var previous = -1;
        for (var index = 0; index < PickerLength - 1; index++)
        {
            var symbol = random.Next(GoldenBirdRules.SymbolCount);
            if (symbol == previous)
            {
                symbol = (symbol + 1) % GoldenBirdRules.SymbolCount;
            }

            picker[index] = symbol;
            previous = symbol;
        }

        picker[PickerLength - 1] = expander >= 0 ? expander : 0;
    }

    private static int FeatureStartOf(CasinoSlotsStepDto[] list)
    {
        for (var index = 0; index < list.Length; index++)
        {
            var kind = list[index].Kind;
            if (string.Equals(kind, SlotsRules.StepFree, StringComparison.Ordinal)
                || string.Equals(kind, SlotsRules.StepGame, StringComparison.Ordinal)
                || string.Equals(kind, SlotsRules.StepBuy, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool HasGrid(string kind)
    {
        return kind is SlotsRules.StepBase or SlotsRules.StepFree or SlotsRules.StepGame or SlotsRules.StepTumble
            or SlotsRules.StepExpand;
    }

    public static bool IsHoldKind(string kind)
    {
        return kind is SlotsRules.StepHold or SlotsRules.StepRespin or SlotsRules.StepCollect;
    }

    public static bool HasWinLine(CasinoSlotsStepDto step, int line)
    {
        if (step.Wins is not { } wins)
        {
            return false;
        }

        for (var index = 0; index < wins.Length; index++)
        {
            if (wins[index].Line == line)
            {
                return true;
            }
        }

        return false;
    }
}
