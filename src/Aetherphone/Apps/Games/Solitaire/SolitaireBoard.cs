using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games.Solitaire;

internal enum SolitaireAutoMove : byte
{
    None,
    Draw,
    WasteToFoundation,
    TableauToFoundation,
}

internal sealed class SolitaireBoard
{
    public const int TableauPiles = 7;
    public const int SuitCount = 4;
    public const int DeckSize = 52;
    public const int StockSize = 24;
    public const int VegasRecycles = 2;
    public const int VegasDeckCost = 52;
    public const int VegasCardValue = 5;
    private readonly List<int> stock = new(DeckSize);
    private readonly List<int> waste = new(DeckSize);
    private readonly List<int>[] foundations = new List<int>[SuitCount];
    private readonly List<int>[] tableau = new List<int>[TableauPiles];
    private readonly int[] faceDown = new int[TableauPiles];
    private readonly int[] deck = new int[DeckSize];
    private bool vegas;
    private int recyclesLeft;
    public int Moves { get; private set; }
    public int LastFlippedPile { get; private set; } = -1;

    public SolitaireBoard()
    {
        for (var suit = 0; suit < SuitCount; suit++)
        {
            foundations[suit] = new List<int>(13);
        }

        for (var pile = 0; pile < TableauPiles; pile++)
        {
            tableau[pile] = new List<int>(20);
        }
    }

    public static int Rank(int card) => card % 13;
    public static int Suit(int card) => card / 13;
    public static bool IsRed(int card) => card / 13 == 1 || card / 13 == 2;
    public bool Vegas => vegas;
    public int RecyclesLeft => recyclesLeft;
    public int StockCount => stock.Count;
    public int WasteCount => waste.Count;
    public int StockPeek(int fromTop) => stock.Count > fromTop ? stock[stock.Count - 1 - fromTop] : -1;
    public int WasteTop() => waste.Count > 0 ? waste[waste.Count - 1] : -1;
    public int WastePeek(int fromTop) => waste.Count > fromTop ? waste[waste.Count - 1 - fromTop] : -1;
    public int FoundationCount(int suit) => foundations[suit].Count;

    public int FoundationTop(int suit) =>
        foundations[suit].Count > 0 ? foundations[suit][foundations[suit].Count - 1] : -1;

    public int FoundationPeek(int suit, int fromTop) =>
        foundations[suit].Count > fromTop ? foundations[suit][foundations[suit].Count - 1 - fromTop] : -1;

    public int TableauCount(int pile) => tableau[pile].Count;
    public int TableauCardAt(int pile, int index) => tableau[pile][index];
    public int TableauFaceDownCount(int pile) => faceDown[pile];
    public bool IsTableauFaceUp(int pile, int index) => index >= faceDown[pile];
    public bool CanRecycle => stock.Count == 0 && waste.Count > 0 && (!vegas || recyclesLeft > 0);

    public int FoundationTotal
    {
        get
        {
            var total = 0;
            for (var suit = 0; suit < SuitCount; suit++)
            {
                total += foundations[suit].Count;
            }

            return total;
        }
    }

    public int Score => vegas ? FoundationTotal * VegasCardValue - VegasDeckCost : 0;

    public bool IsWon => FoundationTotal == DeckSize;

    public bool AllFaceUp
    {
        get
        {
            for (var pile = 0; pile < TableauPiles; pile++)
            {
                if (faceDown[pile] > 0)
                {
                    return false;
                }
            }

            return true;
        }
    }

    public bool IsAutoCompletable => !IsWon && AllFaceUp && (!vegas || (stock.Count == 0 && waste.Count == 0));

    public void Deal(GameRandom random, bool vegasRules)
    {
        vegas = vegasRules;
        recyclesLeft = vegasRules ? VegasRecycles : 0;
        Moves = 0;
        LastFlippedPile = -1;
        stock.Clear();
        waste.Clear();
        for (var suit = 0; suit < SuitCount; suit++)
        {
            foundations[suit].Clear();
        }

        for (var pile = 0; pile < TableauPiles; pile++)
        {
            tableau[pile].Clear();
            faceDown[pile] = 0;
        }

        for (var card = 0; card < DeckSize; card++)
        {
            deck[card] = card;
        }

        for (var index = DeckSize - 1; index > 0; index--)
        {
            var swap = random.Next(index + 1);
            (deck[index], deck[swap]) = (deck[swap], deck[index]);
        }

        var cursor = 0;
        for (var pile = 0; pile < TableauPiles; pile++)
        {
            for (var depth = 0; depth <= pile; depth++)
            {
                tableau[pile].Add(deck[cursor++]);
            }

            faceDown[pile] = pile;
        }

        for (; cursor < DeckSize; cursor++)
        {
            stock.Add(deck[cursor]);
        }
    }

    public bool DrawStock()
    {
        LastFlippedPile = -1;
        if (stock.Count == 0)
        {
            if (!CanRecycle)
            {
                return false;
            }

            for (var index = waste.Count - 1; index >= 0; index--)
            {
                stock.Add(waste[index]);
            }

            waste.Clear();
            if (vegas)
            {
                recyclesLeft--;
            }

            Moves++;
            return true;
        }

        var card = stock[stock.Count - 1];
        stock.RemoveAt(stock.Count - 1);
        waste.Add(card);
        Moves++;
        return true;
    }

    public bool SendWasteToFoundation()
    {
        LastFlippedPile = -1;
        var card = WasteTop();
        if (card < 0 || !CanFoundation(card))
        {
            return false;
        }

        waste.RemoveAt(waste.Count - 1);
        foundations[Suit(card)].Add(card);
        Moves++;
        return true;
    }

    public bool SendTableauToFoundation(int pile)
    {
        LastFlippedPile = -1;
        if (tableau[pile].Count == 0)
        {
            return false;
        }

        var card = tableau[pile][tableau[pile].Count - 1];
        if (!CanFoundation(card))
        {
            return false;
        }

        tableau[pile].RemoveAt(tableau[pile].Count - 1);
        foundations[Suit(card)].Add(card);
        FlipIfNeeded(pile);
        Moves++;
        return true;
    }

    public bool MoveWasteToTableau(int destPile)
    {
        LastFlippedPile = -1;
        var card = WasteTop();
        if (card < 0 || !CanTableau(card, destPile))
        {
            return false;
        }

        waste.RemoveAt(waste.Count - 1);
        tableau[destPile].Add(card);
        Moves++;
        return true;
    }

    public bool MoveFoundationToTableau(int suit, int destPile)
    {
        LastFlippedPile = -1;
        var card = FoundationTop(suit);
        if (card < 0 || !CanTableau(card, destPile))
        {
            return false;
        }

        foundations[suit].RemoveAt(foundations[suit].Count - 1);
        tableau[destPile].Add(card);
        Moves++;
        return true;
    }

    public bool MoveTableauToTableau(int srcPile, int srcIndex, int destPile)
    {
        LastFlippedPile = -1;
        if (srcPile == destPile || !IsRunStart(srcPile, srcIndex))
        {
            return false;
        }

        var first = tableau[srcPile][srcIndex];
        if (!CanTableau(first, destPile))
        {
            return false;
        }

        for (var index = srcIndex; index < tableau[srcPile].Count; index++)
        {
            tableau[destPile].Add(tableau[srcPile][index]);
        }

        tableau[srcPile].RemoveRange(srcIndex, tableau[srcPile].Count - srcIndex);
        FlipIfNeeded(srcPile);
        Moves++;
        return true;
    }

    public bool CanFoundation(int card)
    {
        return foundations[Suit(card)].Count == Rank(card);
    }

    public bool CanTableau(int card, int destPile)
    {
        var pile = tableau[destPile];
        if (pile.Count == 0)
        {
            return Rank(card) == 12;
        }

        var top = pile[pile.Count - 1];
        return IsRed(card) != IsRed(top) && Rank(card) == Rank(top) - 1;
    }

    public bool IsRunStart(int pile, int index)
    {
        if (index < faceDown[pile] || index >= tableau[pile].Count)
        {
            return false;
        }

        for (var current = index; current < tableau[pile].Count - 1; current++)
        {
            var card = tableau[pile][current];
            var below = tableau[pile][current + 1];
            if (IsRed(card) == IsRed(below) || Rank(below) != Rank(card) - 1)
            {
                return false;
            }
        }

        return true;
    }

    public bool HasAnyMove()
    {
        if (stock.Count > 0 || CanRecycle)
        {
            return true;
        }

        var wasteCard = WasteTop();
        if (wasteCard >= 0)
        {
            if (CanFoundation(wasteCard))
            {
                return true;
            }

            for (var pile = 0; pile < TableauPiles; pile++)
            {
                if (CanTableau(wasteCard, pile))
                {
                    return true;
                }
            }
        }

        for (var pile = 0; pile < TableauPiles; pile++)
        {
            var cards = tableau[pile];
            var count = cards.Count;
            if (count == 0)
            {
                continue;
            }

            if (CanFoundation(cards[count - 1]))
            {
                return true;
            }

            for (var index = faceDown[pile]; index < count; index++)
            {
                var card = cards[index];
                var wholeColumn = index == 0;
                for (var destPile = 0; destPile < TableauPiles; destPile++)
                {
                    if (destPile == pile || (wholeColumn && tableau[destPile].Count == 0))
                    {
                        continue;
                    }

                    if (CanTableau(card, destPile))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    public SolitaireAutoMove PlanAutoStep(out int pile)
    {
        pile = -1;
        var wasteCard = WasteTop();
        if (wasteCard >= 0 && CanFoundation(wasteCard))
        {
            return SolitaireAutoMove.WasteToFoundation;
        }

        for (var candidate = 0; candidate < TableauPiles; candidate++)
        {
            var cards = tableau[candidate];
            if (cards.Count > 0 && CanFoundation(cards[cards.Count - 1]))
            {
                pile = candidate;
                return SolitaireAutoMove.TableauToFoundation;
            }
        }

        return stock.Count > 0 || CanRecycle ? SolitaireAutoMove.Draw : SolitaireAutoMove.None;
    }

    private void FlipIfNeeded(int pile)
    {
        var cards = tableau[pile];
        if (cards.Count > 0 && faceDown[pile] >= cards.Count)
        {
            faceDown[pile] = cards.Count - 1;
            LastFlippedPile = pile;
        }
    }
}
