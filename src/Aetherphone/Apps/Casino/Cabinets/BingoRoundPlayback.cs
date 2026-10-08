using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;

namespace Aetherphone.Apps.Casino.Cabinets;

internal enum BingoStage : byte
{
    Waiting,
    Selling,
    Calling,
    Wrapped,
}

[Flags]
internal enum BingoCue : byte
{
    None = 0,
    BallPopped = 1,
    BallLanded = 2,
    Daubed = 4,
    OneAway = 8,
    StageWon = 16,
}

internal sealed class BingoRoundPlayback
{
    public const float FlightSeconds = 0.85f;

    public const float StampDelaySeconds = 0.30f;

    public const float StampAfterSeconds = FlightSeconds + StampDelaySeconds;

    public const float PopSeconds = 0.40f;

    private readonly bool[] called = new bool[BingoRules.Balls + 1];
    private readonly int[] autoMasks = new int[BingoRules.MaxCards];
    private readonly int[] stampedMasks = new int[BingoRules.MaxCards];
    private readonly int[] latestCells = new int[BingoRules.MaxCards];
    private readonly float[] popSeconds = new float[BingoRules.MaxCards * BingoRules.Cells];

    private string roundId = string.Empty;
    private BingoStage stage;
    private BingoCue cues;
    private int cardCount;
    private int ballCount;
    private int latestBall;
    private int stagesSeen;
    private int wonStages;
    private int heroChoice = -1;
    private float sinceBall = StampAfterSeconds;
    private bool primed;
    private bool latestLive;
    private bool calledLive;

    public BingoRoundPlayback()
    {
        ClearRound();
    }

    public string RoundId => roundId;

    public BingoStage Stage => stage;

    public int CardCount => cardCount;

    public int BallCount => ballCount;

    public int LatestBall => latestBall;

    public float SinceBall => sinceBall;

    public bool CalledLive => calledLive;

    public bool Flying => latestLive && latestBall > 0 && sinceBall < FlightSeconds;

    public float FlightProgress => Math.Clamp(sinceBall / FlightSeconds, 0f, 1f);

    public int WonStages => wonStages;

    public int HeroIndex
    {
        get
        {
            if (heroChoice >= 0 && heroChoice < cardCount)
            {
                return heroChoice;
            }

            return BestCard(out _, out _);
        }
    }

    public void Reset()
    {
        roundId = string.Empty;
        ClearRound();
    }

    public BingoCue TakeCues()
    {
        var taken = cues;
        cues = BingoCue.None;
        return taken;
    }

    public void Promote(int cardIndex)
    {
        if (cardIndex >= 0 && cardIndex < cardCount)
        {
            heroChoice = cardIndex;
        }
    }

    public int AutoMaskOf(int cardIndex)
    {
        return cardIndex >= 0 && cardIndex < autoMasks.Length ? autoMasks[cardIndex] : BingoRules.FreeMask;
    }

    public int VisibleMaskOf(int cardIndex)
    {
        var mask = AutoMaskOf(cardIndex);
        if (!Flying || cardIndex < 0 || cardIndex >= latestCells.Length)
        {
            return mask;
        }

        return mask & ~latestCells[cardIndex];
    }

    public int StampedMaskOf(int cardIndex)
    {
        return cardIndex >= 0 && cardIndex < stampedMasks.Length ? stampedMasks[cardIndex] : BingoRules.FreeMask;
    }

    public float PopOf(int cardIndex, int cell)
    {
        var slot = PopSlot(cardIndex, cell);
        return slot < 0 ? 0f : popSeconds[slot];
    }

    public bool IsCalled(int ball)
    {
        return BingoRules.IsBall(ball) && called[ball];
    }

    public bool IsLit(int ball)
    {
        if (!IsCalled(ball))
        {
            return false;
        }

        return ball != latestBall || !Flying;
    }

    public bool WonStage(int stageIndex)
    {
        return BingoRules.IsStage(stageIndex) && (wonStages & (1 << stageIndex)) != 0;
    }

    public int GapOf(int cardIndex, out int goalStage)
    {
        return BingoRules.NextGoalGap(VisibleMaskOf(cardIndex), out goalStage);
    }

    public bool OneAway(int cardIndex)
    {
        return cardIndex >= 0 && cardIndex < cardCount && GapOf(cardIndex, out _) == 1;
    }

    public int BestCard(out int gap, out int goalStage)
    {
        var best = -1;
        gap = int.MaxValue;
        goalStage = BingoRules.StageLine;
        for (var cardIndex = 0; cardIndex < cardCount; cardIndex++)
        {
            var cardGap = GapOf(cardIndex, out var cardGoal);
            if (cardGap < gap)
            {
                best = cardIndex;
                gap = cardGap;
                goalStage = cardGoal;
            }
        }

        return best < 0 ? 0 : best;
    }

    internal static string RoundKeyOf(CasinoRoomSnapshotDto snapshot) => RoundKeys.Of(snapshot);

    internal static int[] CalledBalls(CasinoBingoRoomStateDto? board)
    {
        return board?.Balls ?? Array.Empty<int>();
    }

    internal static int[]? CardAt(CasinoBingoCardsDto? mine, int cardIndex)
    {
        var cards = mine?.Cards;
        if (cards is null || cardIndex < 0 || cardIndex >= cards.Length)
        {
            return null;
        }

        return cards[cardIndex];
    }

    internal static BingoStage StageOf(int phase) => phase switch
    {
        CasinoRoomPhases.Open => BingoStage.Selling,
        CasinoRoomPhases.Locked => BingoStage.Calling,
        CasinoRoomPhases.Result => BingoStage.Wrapped,
        _ => BingoStage.Waiting,
    };

    public void Update(CasinoRoomSnapshotDto? snapshot, CasinoBingoRoomStateDto? board,
        CasinoBingoCardsDto? mine, float deltaSeconds, bool manualDaub = false)
    {
        var wasFlying = Flying;
        sinceBall += deltaSeconds;
        AdvancePops(deltaSeconds);
        if (snapshot is null)
        {
            stage = BingoStage.Waiting;
            return;
        }

        var nextRoundId = RoundKeyOf(snapshot);
        if (!string.Equals(nextRoundId, roundId, StringComparison.Ordinal))
        {
            roundId = nextRoundId;
            ClearRound();
        }

        stage = StageOf(snapshot.Phase);
        var balls = CalledBalls(board);
        var drawn = balls.Length;
        var held = mine?.Cards?.Length ?? 0;
        if (held > BingoRules.MaxCards)
        {
            held = BingoRules.MaxCards;
        }

        if (drawn != ballCount || held != cardCount)
        {
            AbsorbBoard(balls, mine, drawn, held);
        }

        AbsorbStages(board, mine, balls);
        if (!primed)
        {
            StampEverything();
            primed = true;
            return;
        }

        if (wasFlying && !Flying)
        {
            Land();
        }

        if (manualDaub && stage != BingoStage.Wrapped)
        {
            return;
        }

        if ((sinceBall >= StampAfterSeconds || stage == BingoStage.Wrapped) && StampEverything())
        {
            cues |= BingoCue.Daubed;
        }
    }

    public void Snap()
    {
        if (Flying)
        {
            sinceBall = FlightSeconds;
            Land();
        }

        sinceBall = MathF.Max(sinceBall, StampAfterSeconds);
        Array.Clear(popSeconds);
    }

    public bool Stamp(int cardIndex, int cell)
    {
        if (cardIndex < 0 || cardIndex >= cardCount || !BingoRules.IsCell(cell))
        {
            return false;
        }

        var bit = 1 << cell;
        if ((VisibleMaskOf(cardIndex) & bit) == 0 || (stampedMasks[cardIndex] & bit) != 0)
        {
            return false;
        }

        stampedMasks[cardIndex] |= bit;
        PopCell(cardIndex, cell);
        cues |= BingoCue.Daubed;
        return true;
    }

    private void Land()
    {
        cues |= BingoCue.BallLanded;
        for (var cardIndex = 0; cardIndex < cardCount; cardIndex++)
        {
            if (OneAway(cardIndex))
            {
                cues |= BingoCue.OneAway;
                return;
            }
        }
    }

    private void ClearRound()
    {
        stage = BingoStage.Waiting;
        cues = BingoCue.None;
        cardCount = 0;
        ballCount = 0;
        latestBall = 0;
        stagesSeen = 0;
        wonStages = 0;
        heroChoice = -1;
        sinceBall = StampAfterSeconds;
        primed = false;
        latestLive = false;
        calledLive = false;
        Array.Clear(called);
        Array.Clear(popSeconds);
        Array.Clear(latestCells);
        Array.Fill(autoMasks, BingoRules.FreeMask);
        Array.Fill(stampedMasks, BingoRules.FreeMask);
    }

    private void AbsorbBoard(int[] balls, CasinoBingoCardsDto? mine, int drawn, int held)
    {
        var fresh = drawn - ballCount;
        if (fresh > 0)
        {
            latestBall = balls[drawn - 1];
            var live = primed && fresh == 1;
            latestLive = live;
            sinceBall = live ? 0f : StampAfterSeconds;
            if (live)
            {
                calledLive = true;
                cues |= BingoCue.BallPopped;
            }
        }
        else if (drawn < ballCount)
        {
            latestBall = drawn > 0 ? balls[drawn - 1] : 0;
            latestLive = false;
            sinceBall = StampAfterSeconds;
        }

        if (held != cardCount)
        {
            primed = false;
        }

        ballCount = drawn;
        cardCount = held;
        if (heroChoice >= held)
        {
            heroChoice = -1;
        }

        BingoRules.MarkCalled(balls, called);
        for (var cardIndex = 0; cardIndex < autoMasks.Length; cardIndex++)
        {
            var card = cardIndex < held ? CardAt(mine, cardIndex) : null;
            autoMasks[cardIndex] = BingoRules.AutoMask(card, called);
            stampedMasks[cardIndex] &= autoMasks[cardIndex];
            latestCells[cardIndex] = LatestCellOf(card);
        }
    }

    private int LatestCellOf(int[]? card)
    {
        if (card is null || latestBall <= 0)
        {
            return 0;
        }

        var slot = BingoRules.SlotOf(card, latestBall);
        return slot < 0 ? 0 : 1 << BingoRules.CardCells[slot];
    }

    private void AbsorbStages(CasinoBingoRoomStateDto? board, CasinoBingoCardsDto? mine, int[] balls)
    {
        var stages = board?.Stages;
        var count = stages?.Length ?? 0;
        if (count == stagesSeen)
        {
            return;
        }

        var announce = primed && count > stagesSeen;
        stagesSeen = count;
        var won = 0;
        for (var index = 0; index < count; index++)
        {
            var awarded = stages![index];
            if (!BingoRules.IsStage(awarded.Stage) || !HoldsStage(mine, balls, awarded.Stage, awarded.Ball))
            {
                continue;
            }

            won |= 1 << awarded.Stage;
        }

        if (announce && (won & ~wonStages) != 0)
        {
            cues |= BingoCue.StageWon;
        }

        wonStages = won;
    }

    private bool HoldsStage(CasinoBingoCardsDto? mine, int[] balls, int stageIndex, int ball)
    {
        for (var cardIndex = 0; cardIndex < cardCount; cardIndex++)
        {
            var reached = BingoRules.CallReaching(CardAt(mine, cardIndex), balls, stageIndex);
            if (reached > 0 && reached == ball)
            {
                return true;
            }
        }

        return false;
    }

    private bool StampEverything()
    {
        var stampedAny = false;
        for (var cardIndex = 0; cardIndex < cardCount; cardIndex++)
        {
            var pending = VisibleMaskOf(cardIndex) & ~stampedMasks[cardIndex];
            if (pending == 0)
            {
                continue;
            }

            stampedAny = true;
            stampedMasks[cardIndex] |= pending;
            if (!primed)
            {
                continue;
            }

            for (var cell = 0; cell < BingoRules.Cells; cell++)
            {
                if ((pending & (1 << cell)) != 0)
                {
                    PopCell(cardIndex, cell);
                }
            }
        }

        return stampedAny && primed;
    }

    private void PopCell(int cardIndex, int cell)
    {
        var slot = PopSlot(cardIndex, cell);
        if (slot >= 0)
        {
            popSeconds[slot] = PopSeconds;
        }
    }

    private void AdvancePops(float deltaSeconds)
    {
        for (var index = 0; index < popSeconds.Length; index++)
        {
            if (popSeconds[index] <= 0f)
            {
                continue;
            }

            popSeconds[index] = MathF.Max(0f, popSeconds[index] - deltaSeconds);
        }
    }

    private static int PopSlot(int cardIndex, int cell)
    {
        if (cardIndex < 0 || cardIndex >= BingoRules.MaxCards || !BingoRules.IsCell(cell))
        {
            return -1;
        }

        return cardIndex * BingoRules.Cells + cell;
    }
}
