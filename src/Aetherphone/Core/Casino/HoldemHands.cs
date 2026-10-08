namespace Aetherphone.Core.Casino;

internal static class HoldemHands
{
    public const int HighCard = 0;

    public const int Pair = 1;

    public const int TwoPair = 2;

    public const int Trips = 3;

    public const int Straight = 4;

    public const int Flush = 5;

    public const int FullHouse = 6;

    public const int Quads = 7;

    public const int StraightFlush = 8;

    public const int RoyalFlush = 9;

    public const int CategoryShift = 20;

    public const int Ace = 14;

    public const int HandSize = 5;

    private const int WheelTop = 5;

    private const int RanksPerSuit = 13;

    private const int Suits = 4;

    private const int AceLowBit = 1;

    private const int StraightRun = 0b11111;

    public static int RankValue(int card)
    {
        var rank = card % RanksPerSuit;
        return rank == 0 ? Ace : rank + 1;
    }

    public static int SuitOf(int card) => card / RanksPerSuit;

    public static int CategoryOf(int strength) => strength >> CategoryShift;

    public static int KickerOf(int strength, int index) => (strength >> (16 - index * 4)) & 0xF;

    public static int DisplayCategoryOf(int strength)
    {
        var category = CategoryOf(strength);
        return category == StraightFlush && KickerOf(strength, 0) == Ace ? RoyalFlush : category;
    }

    public static int Pack(int category, int k0, int k1, int k2, int k3, int k4) =>
        (category << CategoryShift) | (k0 << 16) | (k1 << 12) | (k2 << 8) | (k3 << 4) | k4;

    public static bool AllCards(ReadOnlySpan<int> cards)
    {
        for (var index = 0; index < cards.Length; index++)
        {
            if (cards[index] < 0 || cards[index] >= RanksPerSuit * Suits)
            {
                return false;
            }
        }

        return true;
    }

    public static int Evaluate5(int card0, int card1, int card2, int card3, int card4)
    {
        Span<int> ranks = stackalloc int[HandSize];
        ranks[0] = RankValue(card0);
        ranks[1] = RankValue(card1);
        ranks[2] = RankValue(card2);
        ranks[3] = RankValue(card3);
        ranks[4] = RankValue(card4);
        SortDescending(ranks);
        var suit = SuitOf(card0);
        var flush = SuitOf(card1) == suit && SuitOf(card2) == suit && SuitOf(card3) == suit
            && SuitOf(card4) == suit;
        var distinct = ranks[0] != ranks[1] && ranks[1] != ranks[2] && ranks[2] != ranks[3]
            && ranks[3] != ranks[4];
        var straightTop = 0;
        if (distinct && ranks[0] - ranks[4] == 4)
        {
            straightTop = ranks[0];
        }
        else if (distinct && ranks[0] == Ace && ranks[1] == 5 && ranks[4] == 2)
        {
            straightTop = WheelTop;
        }

        if (straightTop > 0)
        {
            return Pack(flush ? StraightFlush : Straight, straightTop, 0, 0, 0, 0);
        }

        if (flush)
        {
            return Pack(Flush, ranks[0], ranks[1], ranks[2], ranks[3], ranks[4]);
        }

        Span<int> groupRank = stackalloc int[HandSize];
        Span<int> groupCount = stackalloc int[HandSize];
        var groups = 0;
        for (var index = 0; index < HandSize; index++)
        {
            if (groups > 0 && groupRank[groups - 1] == ranks[index])
            {
                groupCount[groups - 1]++;
                continue;
            }

            groupRank[groups] = ranks[index];
            groupCount[groups] = 1;
            groups++;
        }

        SortGroups(groupRank, groupCount, groups);
        if (groupCount[0] == 4)
        {
            return Pack(Quads, groupRank[0], groupRank[1], 0, 0, 0);
        }

        if (groupCount[0] == 3 && groupCount[1] == 2)
        {
            return Pack(FullHouse, groupRank[0], groupRank[1], 0, 0, 0);
        }

        if (groupCount[0] == 3)
        {
            return Pack(Trips, groupRank[0], groupRank[1], groupRank[2], 0, 0);
        }

        if (groupCount[0] == 2 && groupCount[1] == 2)
        {
            return Pack(TwoPair, groupRank[0], groupRank[1], groupRank[2], 0, 0);
        }

        if (groupCount[0] == 2)
        {
            return Pack(Pair, groupRank[0], groupRank[1], groupRank[2], groupRank[3], 0);
        }

        return Pack(HighCard, ranks[0], ranks[1], ranks[2], ranks[3], ranks[4]);
    }

    public static int Evaluate(ReadOnlySpan<int> cards)
    {
        if (cards.Length < HandSize)
        {
            return -1;
        }

        Span<int> counts = stackalloc int[Ace + 1];
        Span<int> suitMasks = stackalloc int[Suits];
        Span<int> suitCounts = stackalloc int[Suits];
        var rankMask = 0;
        for (var index = 0; index < cards.Length; index++)
        {
            var value = RankValue(cards[index]);
            var suit = SuitOf(cards[index]);
            counts[value]++;
            suitCounts[suit]++;
            suitMasks[suit] |= 1 << value;
            rankMask |= 1 << value;
        }

        for (var suit = 0; suit < Suits; suit++)
        {
            if (suitCounts[suit] < HandSize)
            {
                continue;
            }

            var straightFlushTop = StraightTop(suitMasks[suit]);
            if (straightFlushTop > 0)
            {
                return Pack(StraightFlush, straightFlushTop, 0, 0, 0, 0);
            }
        }

        var quads = 0;
        var tripsHigh = 0;
        var tripsLow = 0;
        var pairHigh = 0;
        var pairLow = 0;
        for (var value = Ace; value >= 2; value--)
        {
            var count = counts[value];
            if (count == 4)
            {
                quads = value;
            }
            else if (count == 3)
            {
                if (tripsHigh == 0)
                {
                    tripsHigh = value;
                }
                else if (tripsLow == 0)
                {
                    tripsLow = value;
                }
            }
            else if (count == 2)
            {
                if (pairHigh == 0)
                {
                    pairHigh = value;
                }
                else if (pairLow == 0)
                {
                    pairLow = value;
                }
            }
        }

        if (quads > 0)
        {
            return Pack(Quads, quads, HighestExcept(rankMask, quads, 0, 0), 0, 0, 0);
        }

        if (tripsHigh > 0 && (tripsLow > 0 || pairHigh > 0))
        {
            return Pack(FullHouse, tripsHigh, Math.Max(tripsLow, pairHigh), 0, 0, 0);
        }

        Span<int> top = stackalloc int[HandSize];
        for (var suit = 0; suit < Suits; suit++)
        {
            if (suitCounts[suit] < HandSize)
            {
                continue;
            }

            TopBits(suitMasks[suit], top);
            return Pack(Flush, top[0], top[1], top[2], top[3], top[4]);
        }

        var straightTop = StraightTop(rankMask);
        if (straightTop > 0)
        {
            return Pack(Straight, straightTop, 0, 0, 0, 0);
        }

        if (tripsHigh > 0)
        {
            var kicker0 = HighestExcept(rankMask, tripsHigh, 0, 0);
            var kicker1 = HighestExcept(rankMask, tripsHigh, kicker0, 0);
            return Pack(Trips, tripsHigh, kicker0, kicker1, 0, 0);
        }

        if (pairHigh > 0 && pairLow > 0)
        {
            return Pack(TwoPair, pairHigh, pairLow, HighestExcept(rankMask, pairHigh, pairLow, 0), 0, 0);
        }

        if (pairHigh > 0)
        {
            var kicker0 = HighestExcept(rankMask, pairHigh, 0, 0);
            var kicker1 = HighestExcept(rankMask, pairHigh, kicker0, 0);
            var kicker2 = HighestExcept(rankMask, pairHigh, kicker0, kicker1);
            return Pack(Pair, pairHigh, kicker0, kicker1, kicker2, 0);
        }

        TopBits(rankMask, top);
        return Pack(HighCard, top[0], top[1], top[2], top[3], top[4]);
    }

    public static int Best5(ReadOnlySpan<int> cards, Span<int> output)
    {
        if (cards.Length < HandSize || output.Length < HandSize)
        {
            return -1;
        }

        var best = -1;
        Span<int> pick = stackalloc int[HandSize];
        var count = cards.Length;
        for (var first = 0; first < count; first++)
        {
            for (var second = first + 1; second < count; second++)
            {
                for (var third = second + 1; third < count; third++)
                {
                    for (var fourth = third + 1; fourth < count; fourth++)
                    {
                        for (var fifth = fourth + 1; fifth < count; fifth++)
                        {
                            pick[0] = cards[first];
                            pick[1] = cards[second];
                            pick[2] = cards[third];
                            pick[3] = cards[fourth];
                            pick[4] = cards[fifth];
                            var strength = Evaluate5(pick[0], pick[1], pick[2], pick[3], pick[4]);
                            if (strength <= best)
                            {
                                continue;
                            }

                            best = strength;
                            pick.CopyTo(output);
                        }
                    }
                }
            }
        }

        OrderForDisplay(output[..HandSize], best);
        return best;
    }

    private static void OrderForDisplay(Span<int> cards, int strength)
    {
        var category = CategoryOf(strength);
        var wheel = (category == Straight || category == StraightFlush) && KickerOf(strength, 0) == WheelTop;
        Span<int> multiplicity = stackalloc int[Ace + 1];
        for (var index = 0; index < cards.Length; index++)
        {
            multiplicity[RankValue(cards[index])]++;
        }

        for (var outer = 1; outer < cards.Length; outer++)
        {
            var held = cards[outer];
            var inner = outer - 1;
            while (inner >= 0 && DisplayBefore(held, cards[inner], multiplicity, wheel))
            {
                cards[inner + 1] = cards[inner];
                inner--;
            }

            cards[inner + 1] = held;
        }
    }

    private static bool DisplayBefore(int left, int right, ReadOnlySpan<int> multiplicity, bool wheel)
    {
        var leftValue = RankValue(left);
        var rightValue = RankValue(right);
        if (wheel)
        {
            leftValue = leftValue == Ace ? AceLowBit : leftValue;
            rightValue = rightValue == Ace ? AceLowBit : rightValue;
        }
        else if (multiplicity[RankValue(left)] != multiplicity[RankValue(right)])
        {
            return multiplicity[RankValue(left)] > multiplicity[RankValue(right)];
        }

        if (leftValue != rightValue)
        {
            return leftValue > rightValue;
        }

        return SuitOf(left) < SuitOf(right);
    }

    private static int StraightTop(int mask)
    {
        var withLowAce = (mask & (1 << Ace)) != 0 ? mask | (1 << AceLowBit) : mask;
        for (var top = Ace; top >= WheelTop; top--)
        {
            var run = StraightRun << (top - 4);
            if ((withLowAce & run) == run)
            {
                return top;
            }
        }

        return 0;
    }

    private static int HighestExcept(int mask, int skip0, int skip1, int skip2)
    {
        for (var value = Ace; value >= 2; value--)
        {
            if (value == skip0 || value == skip1 || value == skip2)
            {
                continue;
            }

            if ((mask & (1 << value)) != 0)
            {
                return value;
            }
        }

        return 0;
    }

    private static void TopBits(int mask, Span<int> top)
    {
        var filled = 0;
        for (var value = Ace; value >= 2 && filled < top.Length; value--)
        {
            if ((mask & (1 << value)) != 0)
            {
                top[filled] = value;
                filled++;
            }
        }
    }

    private static void SortDescending(Span<int> values)
    {
        for (var outer = 1; outer < values.Length; outer++)
        {
            var held = values[outer];
            var inner = outer - 1;
            while (inner >= 0 && values[inner] < held)
            {
                values[inner + 1] = values[inner];
                inner--;
            }

            values[inner + 1] = held;
        }
    }

    private static void SortGroups(Span<int> ranks, Span<int> counts, int groups)
    {
        for (var outer = 1; outer < groups; outer++)
        {
            var heldRank = ranks[outer];
            var heldCount = counts[outer];
            var inner = outer - 1;
            while (inner >= 0 && (counts[inner] < heldCount
                || (counts[inner] == heldCount && ranks[inner] < heldRank)))
            {
                ranks[inner + 1] = ranks[inner];
                counts[inner + 1] = counts[inner];
                inner--;
            }

            ranks[inner + 1] = heldRank;
            counts[inner + 1] = heldCount;
        }
    }
}
