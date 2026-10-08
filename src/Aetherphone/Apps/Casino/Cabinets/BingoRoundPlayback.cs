using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Notifications;

namespace Aetherphone.Apps.Casino.Cabinets;

internal enum BingoStage
{
    Waiting,
    Selling,
    Calling,
    Wrapped,
}

internal sealed class BingoRoundPlayback
{
    public const float StampDelaySeconds = 0.85f;

    public const float PopSeconds = 0.40f;

    public const float BallEntrySeconds = 0.55f;

    public const float BallRollSeconds = 0.62f;

    private readonly bool[] called = new bool[BingoRules.Balls + 1];
    private readonly int[] autoMasks = new int[BingoRules.MaxCards];
    private readonly int[] stampedMasks = new int[BingoRules.MaxCards];
    private readonly float[] popSeconds = new float[BingoRules.MaxCards * BingoRules.Cells];

    private string roundId = string.Empty;
    private int cardCount;
    private int ballCount;
    private int latestBall;
    private float sinceBall = BallEntrySeconds;
    private bool primed;
    private bool calledLive;

    public BingoRoundPlayback()
    {
        ClearRound();
    }

    public string RoundId => roundId;

    public int CardCount => cardCount;

    public int BallCount => ballCount;

    public int LatestBall => latestBall;

    public float SinceBall => sinceBall;

    public bool CalledLive => calledLive;

    public bool Rolling => latestBall > 0 && sinceBall < BallRollSeconds;

    public float RollProgress => BallRollSeconds <= 0f
        ? 1f
        : Math.Clamp(sinceBall / BallRollSeconds, 0f, 1f);

    public int RollingFace(int ballCount)
    {
        var step = (int)(sinceBall * 26f);
        return 1 + ((step * 7 + ballCount * 13) % BingoRules.Balls);
    }

    public void Reset()
    {
        roundId = string.Empty;
        ClearRound();
    }

    public int AutoMaskOf(int cardIndex)
    {
        return cardIndex >= 0 && cardIndex < autoMasks.Length ? autoMasks[cardIndex] : BingoRules.FreeMask;
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

    public void Update(CasinoRoomSnapshotDto? snapshot, CasinoBingoRoomStateDto? board,
        CasinoBingoCardsDto? mine, float deltaSeconds)
    {
        sinceBall += deltaSeconds;
        AdvancePops(deltaSeconds);
        if (snapshot is null)
        {
            return;
        }

        var nextRoundId = RoundKeyOf(snapshot);
        if (!string.Equals(nextRoundId, roundId, StringComparison.Ordinal))
        {
            OpenRound(nextRoundId);
        }

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

        if (!primed)
        {
            StampEverything();
            primed = true;
            return;
        }

        if (sinceBall >= StampDelaySeconds && StampEverything())
        {
            UiFeedback.Play(UiSound.GameCollect);
        }
    }

    public bool Stamp(int cardIndex, int cell)
    {
        if (cardIndex < 0 || cardIndex >= cardCount || !BingoRules.IsCell(cell))
        {
            return false;
        }

        var bit = 1 << cell;
        if ((autoMasks[cardIndex] & bit) == 0 || (stampedMasks[cardIndex] & bit) != 0)
        {
            return false;
        }

        stampedMasks[cardIndex] |= bit;
        PopCell(cardIndex, cell);
        UiFeedback.Play(UiSound.GameCollect);
        return true;
    }

    private void OpenRound(string nextRoundId)
    {
        roundId = nextRoundId;
        ClearRound();
    }

    private void ClearRound()
    {
        cardCount = 0;
        ballCount = 0;
        latestBall = 0;
        sinceBall = BallEntrySeconds;
        primed = false;
        calledLive = false;
        Array.Clear(called);
        Array.Clear(popSeconds);
        Array.Fill(autoMasks, BingoRules.FreeMask);
        Array.Fill(stampedMasks, BingoRules.FreeMask);
    }

    private void AbsorbBoard(int[] balls, CasinoBingoCardsDto? mine, int drawn, int held)
    {
        if (drawn > ballCount && drawn > 0)
        {
            latestBall = balls[drawn - 1];
            sinceBall = 0f;
            if (primed)
            {
                calledLive = true;
                UiFeedback.Play(UiSound.GameTick);
            }
        }

        if (held != cardCount)
        {
            primed = false;
        }

        ballCount = drawn;
        cardCount = held;
        BingoRules.MarkCalled(balls, called);
        for (var cardIndex = 0; cardIndex < autoMasks.Length; cardIndex++)
        {
            var card = cardIndex < held ? CardAt(mine, cardIndex) : null;
            autoMasks[cardIndex] = BingoRules.AutoMask(card, called);
            stampedMasks[cardIndex] &= autoMasks[cardIndex];
        }
    }

    private bool StampEverything()
    {
        var stampedAny = false;
        for (var cardIndex = 0; cardIndex < cardCount; cardIndex++)
        {
            var pending = autoMasks[cardIndex] & ~stampedMasks[cardIndex];
            if (pending == 0)
            {
                continue;
            }

            stampedAny = true;
            stampedMasks[cardIndex] |= pending;
            for (var cell = 0; cell < BingoRules.Cells; cell++)
            {
                if ((pending & (1 << cell)) != 0)
                {
                    PopCell(cardIndex, cell);
                }
            }
        }

        return stampedAny;
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

            popSeconds[index] -= deltaSeconds;
            if (popSeconds[index] < 0f)
            {
                popSeconds[index] = 0f;
            }
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
