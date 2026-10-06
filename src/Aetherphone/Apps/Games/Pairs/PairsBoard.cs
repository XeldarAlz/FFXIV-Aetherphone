using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games.Pairs;

internal enum CardState : byte
{
    FaceDown,
    FaceUp,
    Matched,
}

internal enum PairsPhase : byte
{
    Selecting,
    Revealing,
    Matched,
    Mismatched,
    Won,
}

internal sealed class PairsBoard
{
    public const int Columns = 4;
    public const int Rows = 4;
    public const int CardCount = Columns * Rows;
    public const int PairCount = CardCount / 2;
    public const float RevealSeconds = 0.55f;
    public const float MatchSeconds = 0.45f;
    public const float MismatchSeconds = 0.4f;
    private const int NoCard = -1;

    private readonly int[] symbols = new int[CardCount];
    private readonly CardState[] states = new CardState[CardCount];
    private readonly int[] traySlots = new int[PairCount];
    private GameRandom random;
    private float phaseTimer;

    public PairsPhase Phase { get; private set; }

    public int Attempts { get; private set; }

    public int Streak { get; private set; }

    public int BestStreak { get; private set; }

    public int MatchedPairs { get; private set; }

    public int FirstCard { get; private set; } = NoCard;

    public int SecondCard { get; private set; } = NoCard;

    public float Elapsed { get; private set; }

    public bool MatchedThisStep { get; private set; }

    public bool MismatchedThisStep { get; private set; }

    public bool Over => Phase == PairsPhase.Won;

    public bool HasPair => FirstCard >= 0 && SecondCard >= 0;

    public bool SelectionMatches => HasPair && symbols[FirstCard] == symbols[SecondCard];

    public int Symbol(int index) => symbols[index];

    public CardState State(int index) => states[index];

    public int TraySlot(int symbol) => traySlots[symbol];

    public bool IsSelected(int index) => index == FirstCard || index == SecondCard;

    public void Reset(GameRandom seededRandom)
    {
        random = seededRandom;
        for (var index = 0; index < CardCount; index++)
        {
            symbols[index] = index / 2;
            states[index] = CardState.FaceDown;
        }

        for (var pair = 0; pair < PairCount; pair++)
        {
            traySlots[pair] = NoCard;
        }

        Shuffle();
        Phase = PairsPhase.Selecting;
        Attempts = 0;
        Streak = 0;
        BestStreak = 0;
        MatchedPairs = 0;
        FirstCard = NoCard;
        SecondCard = NoCard;
        Elapsed = 0f;
        phaseTimer = 0f;
        MatchedThisStep = false;
        MismatchedThisStep = false;
    }

    public bool CanReveal(int index) =>
        Phase == PairsPhase.Selecting && states[index] == CardState.FaceDown && index != FirstCard;

    public bool Reveal(int index)
    {
        if (!CanReveal(index))
        {
            return false;
        }

        states[index] = CardState.FaceUp;
        if (FirstCard < 0)
        {
            FirstCard = index;
            return true;
        }

        SecondCard = index;
        Attempts++;
        Phase = PairsPhase.Revealing;
        phaseTimer = RevealSeconds;
        return true;
    }

    public void Step(float deltaSeconds)
    {
        MatchedThisStep = false;
        MismatchedThisStep = false;
        if (Over || deltaSeconds <= 0f)
        {
            return;
        }

        Elapsed += deltaSeconds;
        if (Phase == PairsPhase.Selecting)
        {
            return;
        }

        phaseTimer -= deltaSeconds;
        if (phaseTimer > 0f)
        {
            return;
        }

        switch (Phase)
        {
            case PairsPhase.Revealing:
                Resolve();
                return;
            case PairsPhase.Matched:
                ConfirmMatch();
                return;
            case PairsPhase.Mismatched:
                HideSelection();
                return;
            default:
                return;
        }
    }

    private void Resolve()
    {
        if (SelectionMatches)
        {
            Streak++;
            BestStreak = Math.Max(BestStreak, Streak);
            MatchedThisStep = true;
            Phase = PairsPhase.Matched;
            phaseTimer = MatchSeconds;
            return;
        }

        Streak = 0;
        MismatchedThisStep = true;
        Phase = PairsPhase.Mismatched;
        phaseTimer = MismatchSeconds;
    }

    private void ConfirmMatch()
    {
        states[FirstCard] = CardState.Matched;
        states[SecondCard] = CardState.Matched;
        traySlots[symbols[FirstCard]] = MatchedPairs;
        MatchedPairs++;
        FirstCard = NoCard;
        SecondCard = NoCard;
        Phase = MatchedPairs >= PairCount ? PairsPhase.Won : PairsPhase.Selecting;
    }

    private void HideSelection()
    {
        states[FirstCard] = CardState.FaceDown;
        states[SecondCard] = CardState.FaceDown;
        FirstCard = NoCard;
        SecondCard = NoCard;
        Phase = PairsPhase.Selecting;
    }

    private void Shuffle()
    {
        for (var index = CardCount - 1; index > 0; index--)
        {
            var swap = random.Next(index + 1);
            (symbols[index], symbols[swap]) = (symbols[swap], symbols[index]);
        }
    }
}
