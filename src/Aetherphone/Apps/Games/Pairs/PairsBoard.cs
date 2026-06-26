using System;

namespace Aetherphone.Apps.Games.Pairs;

internal enum CardState
{
    FaceDown,
    FaceUp,
    Matched,
}

internal sealed class PairsBoard
{
    public const int Columns = 4;

    public const int Rows = 4;

    public const int CardCount = Columns * Rows;

    private readonly int[] symbols = new int[CardCount];

    private readonly CardState[] states = new CardState[CardCount];

    private readonly Random random = new();

    public int Attempts { get; private set; }

    public int FirstCard { get; private set; } = -1;

    public int SecondCard { get; private set; } = -1;

    public int Symbol(int index) => symbols[index];

    public CardState State(int index) => states[index];

    public void Reset()
    {
        for (var index = 0; index < CardCount; index++)
        {
            symbols[index] = index / 2;
            states[index] = CardState.FaceDown;
        }

        Shuffle();
        Attempts = 0;
        FirstCard = -1;
        SecondCard = -1;
    }

    public bool CanReveal(int index)
    {
        return states[index] == CardState.FaceDown && SecondCard < 0 && index != FirstCard;
    }

    public bool RevealFirst(int index)
    {
        if (FirstCard >= 0)
        {
            return false;
        }

        FirstCard = index;
        states[index] = CardState.FaceUp;
        return true;
    }

    public void RevealSecond(int index)
    {
        SecondCard = index;
        states[index] = CardState.FaceUp;
        Attempts++;
    }

    public bool HasPair => FirstCard >= 0 && SecondCard >= 0;

    public bool SelectionMatches => HasPair && symbols[FirstCard] == symbols[SecondCard];

    public void ConfirmMatch()
    {
        if (!HasPair)
        {
            return;
        }

        states[FirstCard] = CardState.Matched;
        states[SecondCard] = CardState.Matched;
        FirstCard = -1;
        SecondCard = -1;
    }

    public void HideSelection()
    {
        if (FirstCard >= 0)
        {
            states[FirstCard] = CardState.FaceDown;
        }

        if (SecondCard >= 0)
        {
            states[SecondCard] = CardState.FaceDown;
        }

        FirstCard = -1;
        SecondCard = -1;
    }

    public bool AllMatched()
    {
        for (var index = 0; index < CardCount; index++)
        {
            if (states[index] != CardState.Matched)
            {
                return false;
            }
        }

        return true;
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
