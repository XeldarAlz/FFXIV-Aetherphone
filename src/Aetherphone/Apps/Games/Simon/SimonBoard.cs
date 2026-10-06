using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games.Simon;

internal enum SimonPhase : byte
{
    Showing,
    Waiting,
    Input,
    Failed,
}

internal enum SimonPress : byte
{
    Ignored,
    Correct,
    RoundComplete,
    Wrong,
}

internal sealed class SimonBoard
{
    public const int PadCount = 4;
    public const int MaxLength = 200;
    public const int RampEvery = 5;
    public const float StartDelaySeconds = 0.55f;
    public const float RewardSeconds = 0.55f;
    public const float BaseOnSeconds = 0.42f;
    public const float BaseGapSeconds = 0.18f;
    private const float RampFactor = 0.82f;
    private const float MinOnSeconds = 0.16f;
    private const float MinGapSeconds = 0.08f;
    private const int NoPad = -1;

    private readonly int[] sequence = new int[MaxLength];
    private GameRandom random;
    private float phaseTimer;

    public SimonPhase Phase { get; private set; } = SimonPhase.Waiting;

    public int Length { get; private set; }

    public int Score { get; private set; }

    public int ShowStep { get; private set; }

    public int LitPad { get; private set; } = NoPad;

    public int InputIndex { get; private set; }

    public bool PadLitThisStep { get; private set; }

    public bool InputOpenedThisStep { get; private set; }

    public int Round => Length;

    public bool Over => Phase == SimonPhase.Failed;

    public int RampTier => Math.Max(0, Length - 1) / RampEvery;

    public float OnSeconds => Ramped(BaseOnSeconds, MinOnSeconds);

    public float GapSeconds => Ramped(BaseGapSeconds, MinGapSeconds);

    public int PadAt(int index) => sequence[index];

    public void Reset(GameRandom seededRandom)
    {
        random = seededRandom;
        Length = 0;
        Score = 0;
        AddStep();
        BeginShow(StartDelaySeconds);
        PadLitThisStep = false;
        InputOpenedThisStep = false;
    }

    public void Step(float deltaSeconds)
    {
        PadLitThisStep = false;
        InputOpenedThisStep = false;
        if (Over || deltaSeconds <= 0f || Phase == SimonPhase.Input)
        {
            return;
        }

        phaseTimer -= deltaSeconds;
        if (phaseTimer > 0f)
        {
            return;
        }

        if (Phase == SimonPhase.Showing)
        {
            LitPad = NoPad;
            ShowStep++;
            Phase = SimonPhase.Waiting;
            phaseTimer = GapSeconds;
            return;
        }

        if (ShowStep >= Length)
        {
            Phase = SimonPhase.Input;
            InputIndex = 0;
            InputOpenedThisStep = true;
            return;
        }

        Phase = SimonPhase.Showing;
        LitPad = sequence[ShowStep];
        phaseTimer = OnSeconds;
        PadLitThisStep = true;
    }

    public SimonPress Press(int pad)
    {
        if (Phase != SimonPhase.Input || pad < 0 || pad >= PadCount)
        {
            return SimonPress.Ignored;
        }

        if (sequence[InputIndex] != pad)
        {
            Phase = SimonPhase.Failed;
            LitPad = NoPad;
            return SimonPress.Wrong;
        }

        InputIndex++;
        if (InputIndex < Length)
        {
            return SimonPress.Correct;
        }

        Score = Length;
        AddStep();
        BeginShow(RewardSeconds + StartDelaySeconds);
        return SimonPress.RoundComplete;
    }

    private void AddStep()
    {
        if (Length >= MaxLength)
        {
            return;
        }

        sequence[Length] = random.Next(PadCount);
        Length++;
    }

    private void BeginShow(float delaySeconds)
    {
        ShowStep = 0;
        LitPad = NoPad;
        InputIndex = 0;
        Phase = SimonPhase.Waiting;
        phaseTimer = delaySeconds;
    }

    private float Ramped(float baseSeconds, float minSeconds) =>
        MathF.Max(minSeconds, baseSeconds * MathF.Pow(RampFactor, RampTier));
}
