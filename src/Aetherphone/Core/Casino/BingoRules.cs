namespace Aetherphone.Core.Casino;

internal static class BingoRules
{
    public const int Balls = 75;

    public const int Columns = 5;

    public const int Cells = 25;

    public const int CardNumbers = 24;

    public const int FreeCell = 12;

    public const int NumbersPerColumn = 15;

    public const long CardPrice = 1000;

    public const int MaxCards = 6;

    public const int PrizeCardCap = 125;

    public const int StageLine = 0;

    public const int StageTwoLines = 1;

    public const int StageFullHouse = 2;

    public const int StageCount = 3;

    public const int BallIntervalMs = 1400;

    public const int EarlyBirdBall = 45;

    public const int ReturnTenths = 940;

    public const int FreeMask = 1 << FreeCell;

    public const int FullMask = (1 << Cells) - 1;

    private static readonly int[] BandFloors = { 1, 2, 3, 5, 12, 20, 30, 50, 80 };

    private static readonly int[][] BandRateTenths =
    {
        new[] { 1799, 2389, 5151 },
        new[] { 1599, 2125, 4579 },
        new[] { 1554, 2063, 4448 },
        new[] { 1530, 2032, 4381 },
        new[] { 1515, 2011, 4337 },
        new[] { 1498, 1989, 4289 },
        new[] { 1479, 1964, 4235 },
        new[] { 1472, 1955, 4214 },
        new[] { 1454, 1931, 4162 },
    };

    public static readonly int[] CardCells =
    {
        0, 1, 2, 3, 4,
        5, 6, 7, 8, 9,
        10, 11, 13, 14,
        15, 16, 17, 18, 19,
        20, 21, 22, 23, 24,
    };

    public static readonly int[] LineMasks = BuildLineMasks();

    public static bool IsBall(int ball)
    {
        return ball >= 1 && ball <= Balls;
    }

    public static bool IsCell(int cell)
    {
        return cell >= 0 && cell < Cells;
    }

    public static bool IsStage(int stage)
    {
        return stage >= 0 && stage < StageCount;
    }

    public static bool IsValidCardCount(int cardCount)
    {
        return cardCount >= 1 && cardCount <= MaxCards;
    }

    public static long StakeFor(int cardCount)
    {
        return CardPrice * cardCount;
    }

    public static int CellForSlot(int slot)
    {
        return slot >= 0 && slot < CardNumbers ? CardCells[slot] : -1;
    }

    public static int SlotForCell(int cell)
    {
        if (!IsCell(cell) || cell == FreeCell)
        {
            return -1;
        }

        return cell < FreeCell ? cell : cell - 1;
    }

    public static int ColumnFloorFor(int column)
    {
        return column * NumbersPerColumn + 1;
    }

    public static int ColumnOfBall(int ball)
    {
        return IsBall(ball) ? (ball - 1) / NumbersPerColumn : -1;
    }

    public static int ColumnOfCell(int cell)
    {
        return IsCell(cell) ? cell / Columns : -1;
    }

    public static int RowOfCell(int cell)
    {
        return IsCell(cell) ? cell % Columns : -1;
    }

    public static long PrizeFor(int stage, int cardsInPlay)
    {
        if (!IsStage(stage) || cardsInPlay <= 0)
        {
            return 0;
        }

        var scaled = cardsInPlay < PrizeCardCap ? cardsInPlay : PrizeCardCap;
        return (BandRateTenths[BandFor(scaled)][stage] * (long)scaled + 9) / 10;
    }

    private static int BandFor(int scaledCards)
    {
        var band = 0;
        for (var index = 1; index < BandFloors.Length; index++)
        {
            if (scaledCards < BandFloors[index])
            {
                break;
            }

            band = index;
        }

        return band;
    }

    public static void PrizeLadder(int cardsInPlay, Span<long> ladder)
    {
        for (var stage = 0; stage < StageCount && stage < ladder.Length; stage++)
        {
            ladder[stage] = PrizeFor(stage, cardsInPlay);
        }
    }

    public static bool IsEarlyBird(int fullHouseBall)
    {
        return fullHouseBall > 0 && fullHouseBall <= EarlyBirdBall;
    }

    public static int CallReaching(int[]? card, int[]? balls, int stage)
    {
        if (card is null || balls is null || !IsStage(stage))
        {
            return 0;
        }

        var mask = FreeMask;
        for (var ballIndex = 0; ballIndex < balls.Length; ballIndex++)
        {
            var slot = SlotOf(card, balls[ballIndex]);
            if (slot < 0)
            {
                continue;
            }

            mask |= 1 << CardCells[slot];
            if (StageReached(mask) >= stage)
            {
                return ballIndex + 1;
            }
        }

        return 0;
    }

    public static int SlotOf(int[] card, int ball)
    {
        var slots = card.Length < CardNumbers ? card.Length : CardNumbers;
        for (var slot = 0; slot < slots; slot++)
        {
            if (card[slot] == ball)
            {
                return slot;
            }
        }

        return -1;
    }

    public static bool PrizesFrozen(int cardsInPlay)
    {
        return cardsInPlay >= PrizeCardCap;
    }

    public static void MarkCalled(int[]? balls, Span<bool> called)
    {
        called.Clear();
        if (balls is null)
        {
            return;
        }

        for (var ballIndex = 0; ballIndex < balls.Length; ballIndex++)
        {
            var ball = balls[ballIndex];
            if (IsBall(ball) && ball < called.Length)
            {
                called[ball] = true;
            }
        }
    }

    public static int AutoMask(int[]? card, ReadOnlySpan<bool> called)
    {
        var mask = FreeMask;
        if (card is null)
        {
            return mask;
        }

        var slots = card.Length < CardNumbers ? card.Length : CardNumbers;
        for (var slot = 0; slot < slots; slot++)
        {
            var number = card[slot];
            if (number >= 0 && number < called.Length && called[number])
            {
                mask |= 1 << CardCells[slot];
            }
        }

        return mask;
    }

    public static int AutoMask(int[]? card, int[]? balls)
    {
        Span<bool> called = stackalloc bool[Balls + 1];
        MarkCalled(balls, called);
        return AutoMask(card, called);
    }

    public static bool IsMarked(int mask, int cell)
    {
        return IsCell(cell) && (mask & (1 << cell)) != 0;
    }

    public static int LinesOn(int mask)
    {
        var lines = 0;
        for (var index = 0; index < LineMasks.Length; index++)
        {
            if ((mask & LineMasks[index]) == LineMasks[index])
            {
                lines++;
            }
        }

        return lines;
    }

    public static int NextGoalGap(int mask, out int goalStage)
    {
        var remaining = CellsRemaining(mask);
        var lines = LinesOn(mask);
        if (lines >= 2)
        {
            goalStage = StageFullHouse;
            return remaining;
        }

        goalStage = lines >= 1 ? StageTwoLines : StageLine;
        return SecondClosestLineGap(mask, lines);
    }

    public static int SecondClosestLineGap(int mask, int completedLines)
    {
        var closest = Columns;
        for (var index = 0; index < LineMasks.Length; index++)
        {
            var missing = CountBits(LineMasks[index] & ~mask);
            if (missing == 0)
            {
                continue;
            }

            if (missing < closest)
            {
                closest = missing;
            }
        }

        return completedLines >= 1 ? closest : ClosestLineGap(mask);
    }

    public static int CellsRemaining(int mask)
    {
        return CountBits(FullMask & ~mask);
    }

    public static int ClosestLineGap(int mask)
    {
        var closest = Columns;
        for (var index = 0; index < LineMasks.Length; index++)
        {
            var missing = CountBits(LineMasks[index] & ~mask);
            if (missing < closest)
            {
                closest = missing;
            }
        }

        return closest;
    }

    public static int StageReached(int mask)
    {
        if (CellsRemaining(mask) == 0)
        {
            return StageFullHouse;
        }

        var lines = LinesOn(mask);
        if (lines >= 2)
        {
            return StageTwoLines;
        }

        return lines >= 1 ? StageLine : -1;
    }

    internal static int CountBits(int mask)
    {
        var count = 0;
        var remaining = mask;
        while (remaining != 0)
        {
            remaining &= remaining - 1;
            count++;
        }

        return count;
    }

    private static int[] BuildLineMasks()
    {
        var masks = new int[12];
        var index = 0;
        for (var row = 0; row < Columns; row++)
        {
            var mask = 0;
            for (var column = 0; column < Columns; column++)
            {
                mask |= 1 << (column * Columns + row);
            }

            masks[index] = mask;
            index++;
        }

        for (var column = 0; column < Columns; column++)
        {
            var mask = 0;
            for (var row = 0; row < Columns; row++)
            {
                mask |= 1 << (column * Columns + row);
            }

            masks[index] = mask;
            index++;
        }

        var falling = 0;
        var rising = 0;
        for (var step = 0; step < Columns; step++)
        {
            falling |= 1 << (step * Columns + step);
            rising |= 1 << (step * Columns + (Columns - 1 - step));
        }

        masks[index] = falling;
        masks[index + 1] = rising;
        return masks;
    }
}
