namespace Aetherphone.Apps.Games.GemSwap;

internal static class GemSwapPowers
{
    public const int FireSize = 4;
    private const int SpecialWeight = 3;

    public static int Fire(GemSwapBoard board, int chain, out int originIndex)
    {
        var bestScore = -1;
        var bestColumn = 0;
        var bestRow = 0;
        for (var row = 0; row <= GemSwapBoard.Rows - FireSize; row++)
        {
            for (var column = 0; column <= GemSwapBoard.Columns - FireSize; column++)
            {
                var score = BusyScore(board, column, row, FireSize, FireSize);
                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                bestColumn = column;
                bestRow = row;
            }
        }

        originIndex = bestRow * GemSwapBoard.Columns + bestColumn;
        board.BeginBlast();
        for (var rowOffset = 0; rowOffset < FireSize; rowOffset++)
        {
            for (var columnOffset = 0; columnOffset < FireSize; columnOffset++)
            {
                board.MarkBlast((bestRow + rowOffset) * GemSwapBoard.Columns + bestColumn + columnOffset);
            }
        }

        return board.FinishBlast(chain);
    }

    public static int Gale(GemSwapBoard board, int chain, out int firstRow, out int secondRow)
    {
        firstRow = board.NextRandom(GemSwapBoard.Rows);
        secondRow = board.NextRandom(GemSwapBoard.Rows - 1);
        if (secondRow >= firstRow)
        {
            secondRow++;
        }

        board.BeginBlast();
        for (var column = 0; column < GemSwapBoard.Columns; column++)
        {
            board.MarkBlast(firstRow * GemSwapBoard.Columns + column);
            board.MarkBlast(secondRow * GemSwapBoard.Columns + column);
        }

        return board.FinishBlast(chain);
    }

    public static int Storm(GemSwapBoard board, int chain, out int column)
    {
        var bestScore = -1;
        column = 0;
        for (var candidate = 0; candidate < GemSwapBoard.Columns; candidate++)
        {
            var score = BusyScore(board, candidate, 0, 1, GemSwapBoard.Rows);
            if (score <= bestScore)
            {
                continue;
            }

            bestScore = score;
            column = candidate;
        }

        board.BeginBlast();
        for (var row = 0; row < GemSwapBoard.Rows; row++)
        {
            board.MarkBlast(row * GemSwapBoard.Columns + column);
        }

        return board.FinishBlast(chain);
    }

    private static int BusyScore(GemSwapBoard board, int column, int row, int width, int height)
    {
        Span<int> counts = stackalloc int[GemSwapBoard.ColorCount];
        var dominant = 0;
        var specials = 0;
        for (var rowOffset = 0; rowOffset < height; rowOffset++)
        {
            for (var columnOffset = 0; columnOffset < width; columnOffset++)
            {
                var index = (row + rowOffset) * GemSwapBoard.Columns + column + columnOffset;
                if (board.Special(index) != GemSpecial.None)
                {
                    specials++;
                }

                var color = board.Color(index);
                if (color < 0 || color >= GemSwapBoard.ColorCount)
                {
                    continue;
                }

                counts[color]++;
                if (counts[color] > dominant)
                {
                    dominant = counts[color];
                }
            }
        }

        return dominant + specials * SpecialWeight;
    }
}
