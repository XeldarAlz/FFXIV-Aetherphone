namespace Aetherphone.Apps.Games.Reversi;

internal sealed class ReversiBoard
{
    public const int Size = 8;

    public const int CellCount = Size * Size;

    public const int Dark = 1;

    public const int Light = 2;

    private const int CpuColor = Light;

    private const int SearchDepth = 4;

    private static readonly int[] DirectionRow = { -1, -1, -1, 0, 0, 1, 1, 1 };

    private static readonly int[] DirectionColumn = { -1, 0, 1, -1, 1, -1, 0, 1 };

    private static readonly int[] Weights =
    {
        120, -20, 20, 5, 5, 20, -20, 120,
        -20, -40, -5, -5, -5, -5, -40, -20,
        20, -5, 15, 3, 3, 15, -5, 20,
        5, -5, 3, 3, 3, 3, -5, 5,
        5, -5, 3, 3, 3, 3, -5, 5,
        20, -5, 15, 3, 3, 15, -5, 20,
        -20, -40, -5, -5, -5, -5, -40, -20,
        120, -20, 20, 5, 5, 20, -20, 120,
    };

    private readonly sbyte[] cells = new sbyte[CellCount];

    public int Cell(int index) => cells[index];

    public static int Opponent(int player) => player == Dark ? Light : Dark;

    public void Reset()
    {
        Array.Clear(cells, 0, CellCount);
        cells[3 * Size + 3] = Light;
        cells[3 * Size + 4] = Dark;
        cells[4 * Size + 3] = Dark;
        cells[4 * Size + 4] = Light;
    }

    public bool IsLegal(int cell, int player) => cells[cell] == 0 && ComputeFlips(cells, cell, player, null) > 0;

    public bool HasAnyMove(int player) => AnyMove(cells, player);

    public bool ApplyMove(int cell, int player, List<int> flippedOut)
    {
        flippedOut.Clear();
        if (cells[cell] != 0 || ComputeFlips(cells, cell, player, flippedOut) == 0)
        {
            return false;
        }

        cells[cell] = (sbyte)player;
        for (var index = 0; index < flippedOut.Count; index++)
        {
            cells[flippedOut[index]] = (sbyte)player;
        }

        return true;
    }

    public void Counts(out int dark, out int light)
    {
        dark = 0;
        light = 0;
        for (var index = 0; index < CellCount; index++)
        {
            if (cells[index] == Dark)
            {
                dark++;
            }
            else if (cells[index] == Light)
            {
                light++;
            }
        }
    }

    public int BestMove(int player)
    {
        var best = -1;
        var bestValue = int.MinValue;
        for (var cell = 0; cell < CellCount; cell++)
        {
            if (cells[cell] != 0 || ComputeFlips(cells, cell, player, null) == 0)
            {
                continue;
            }

            var next = (sbyte[])cells.Clone();
            PlaceInternal(next, cell, player);
            var value = Search(next, Opponent(player), SearchDepth - 1, int.MinValue, int.MaxValue);
            if (value > bestValue)
            {
                bestValue = value;
                best = cell;
            }
        }

        return best;
    }

    private int Search(sbyte[] state, int player, int depth, int alpha, int beta)
    {
        if (depth == 0)
        {
            return Evaluate(state);
        }

        if (!AnyMove(state, player))
        {
            if (!AnyMove(state, Opponent(player)))
            {
                return TerminalValue(state);
            }

            return Search(state, Opponent(player), depth - 1, alpha, beta);
        }

        var maximizing = player == CpuColor;
        var best = maximizing ? int.MinValue : int.MaxValue;

        for (var cell = 0; cell < CellCount; cell++)
        {
            if (state[cell] != 0 || ComputeFlips(state, cell, player, null) == 0)
            {
                continue;
            }

            var next = (sbyte[])state.Clone();
            PlaceInternal(next, cell, player);
            var value = Search(next, Opponent(player), depth - 1, alpha, beta);

            if (maximizing)
            {
                if (value > best)
                {
                    best = value;
                }

                if (best > alpha)
                {
                    alpha = best;
                }
            }
            else
            {
                if (value < best)
                {
                    best = value;
                }

                if (best < beta)
                {
                    beta = best;
                }
            }

            if (alpha >= beta)
            {
                break;
            }
        }

        return best;
    }

    private int Evaluate(sbyte[] state)
    {
        var positional = 0;
        for (var index = 0; index < CellCount; index++)
        {
            if (state[index] == CpuColor)
            {
                positional += Weights[index];
            }
            else if (state[index] != 0)
            {
                positional -= Weights[index];
            }
        }

        var mobility = (CountMoves(state, CpuColor) - CountMoves(state, Opponent(CpuColor))) * 6;
        return positional + mobility;
    }

    private int TerminalValue(sbyte[] state)
    {
        var cpu = 0;
        var human = 0;
        for (var index = 0; index < CellCount; index++)
        {
            if (state[index] == CpuColor)
            {
                cpu++;
            }
            else if (state[index] != 0)
            {
                human++;
            }
        }

        return (cpu - human) * 200;
    }

    private int CountMoves(sbyte[] state, int player)
    {
        var count = 0;
        for (var cell = 0; cell < CellCount; cell++)
        {
            if (state[cell] == 0 && ComputeFlips(state, cell, player, null) > 0)
            {
                count++;
            }
        }

        return count;
    }

    private bool AnyMove(sbyte[] state, int player)
    {
        for (var cell = 0; cell < CellCount; cell++)
        {
            if (state[cell] == 0 && ComputeFlips(state, cell, player, null) > 0)
            {
                return true;
            }
        }

        return false;
    }

    private void PlaceInternal(sbyte[] state, int cell, int player)
    {
        state[cell] = (sbyte)player;
        var row = cell / Size;
        var column = cell % Size;
        var opponent = Opponent(player);

        for (var direction = 0; direction < 8; direction++)
        {
            var stepRow = DirectionRow[direction];
            var stepColumn = DirectionColumn[direction];
            var row2 = row + stepRow;
            var column2 = column + stepColumn;
            var run = 0;

            while (row2 >= 0 && row2 < Size && column2 >= 0 && column2 < Size && state[row2 * Size + column2] == opponent)
            {
                row2 += stepRow;
                column2 += stepColumn;
                run++;
            }

            if (run == 0 || row2 < 0 || row2 >= Size || column2 < 0 || column2 >= Size || state[row2 * Size + column2] != player)
            {
                continue;
            }

            var flipRow = row + stepRow;
            var flipColumn = column + stepColumn;
            for (var step = 0; step < run; step++)
            {
                state[flipRow * Size + flipColumn] = (sbyte)player;
                flipRow += stepRow;
                flipColumn += stepColumn;
            }
        }
    }

    private int ComputeFlips(sbyte[] state, int cell, int player, List<int>? output)
    {
        if (state[cell] != 0)
        {
            return 0;
        }

        var row = cell / Size;
        var column = cell % Size;
        var opponent = Opponent(player);
        var total = 0;

        for (var direction = 0; direction < 8; direction++)
        {
            var stepRow = DirectionRow[direction];
            var stepColumn = DirectionColumn[direction];
            var row2 = row + stepRow;
            var column2 = column + stepColumn;
            var run = 0;

            while (row2 >= 0 && row2 < Size && column2 >= 0 && column2 < Size && state[row2 * Size + column2] == opponent)
            {
                row2 += stepRow;
                column2 += stepColumn;
                run++;
            }

            if (run == 0 || row2 < 0 || row2 >= Size || column2 < 0 || column2 >= Size || state[row2 * Size + column2] != player)
            {
                continue;
            }

            total += run;
            if (output is null)
            {
                continue;
            }

            var flipRow = row + stepRow;
            var flipColumn = column + stepColumn;
            for (var step = 0; step < run; step++)
            {
                output.Add(flipRow * Size + flipColumn);
                flipRow += stepRow;
                flipColumn += stepColumn;
            }
        }

        return total;
    }
}
