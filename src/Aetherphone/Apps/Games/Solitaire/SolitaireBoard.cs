namespace Aetherphone.Apps.Games.Solitaire;

internal sealed class SolitaireBoard
{
    public const int TableauPiles = 7;

    public const int SuitCount = 4;

    private readonly List<int> stock = new(52);

    private readonly List<int> waste = new(52);

    private readonly List<int>[] foundations = new List<int>[SuitCount];

    private readonly List<int>[] tableau = new List<int>[TableauPiles];

    private readonly int[] faceDown = new int[TableauPiles];

    private readonly int[] deck = new int[52];

    private readonly Random random = new();

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

    public int StockCount => stock.Count;

    public int WasteCount => waste.Count;

    public int WasteTop() => waste.Count > 0 ? waste[waste.Count - 1] : -1;

    public int WastePeek(int fromTop) => waste.Count > fromTop ? waste[waste.Count - 1 - fromTop] : -1;

    public int FoundationCount(int suit) => foundations[suit].Count;

    public int FoundationTop(int suit) => foundations[suit].Count > 0 ? foundations[suit][foundations[suit].Count - 1] : -1;

    public int FoundationPeek(int suit, int fromTop) => foundations[suit].Count > fromTop ? foundations[suit][foundations[suit].Count - 1 - fromTop] : -1;

    public int TableauCount(int pile) => tableau[pile].Count;

    public int TableauCardAt(int pile, int index) => tableau[pile][index];

    public int TableauFaceDownCount(int pile) => faceDown[pile];

    public bool IsTableauFaceUp(int pile, int index) => index >= faceDown[pile];

    public bool IsWon
    {
        get
        {
            var total = 0;
            for (var suit = 0; suit < SuitCount; suit++)
            {
                total += foundations[suit].Count;
            }

            return total == 52;
        }
    }

    public void Deal()
    {
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

        for (var card = 0; card < 52; card++)
        {
            deck[card] = card;
        }

        for (var index = 51; index > 0; index--)
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

        for (; cursor < 52; cursor++)
        {
            stock.Add(deck[cursor]);
        }
    }

    public bool DrawStock()
    {
        LastFlippedPile = -1;
        if (stock.Count == 0)
        {
            if (waste.Count == 0)
            {
                return false;
            }

            for (var index = waste.Count - 1; index >= 0; index--)
            {
                stock.Add(waste[index]);
            }

            waste.Clear();
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
