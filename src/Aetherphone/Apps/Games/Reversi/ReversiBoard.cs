namespace Aetherphone.Apps.Games.Reversi;

internal sealed class ReversiBoard
{
    public const int Size = 8;
    public const int CellCount = Size * Size;
    public const int Dark = 1;
    public const int Light = 2;
    public const int MaxFlips = 24;
    public const int MaxSearchDepth = 8;
    private const int MobilityWeight = 6;
    private const int TerminalDiscValue = 200;
    private static readonly int[] DirectionRow = { -1, -1, -1, 0, 0, 1, 1, 1 };
    private static readonly int[] DirectionColumn = { -1, 0, 1, -1, 1, -1, 0, 1 };

    private static readonly int[] Weights =
    {
        120, -20, 20, 5, 5, 20, -20, 120, -20, -40, -5, -5, -5, -5, -40, -20, 20, -5, 15, 3, 3, 15, -5, 20, 5, -5,
        3, 3, 3, 3, -5, 5, 5, -5, 3, 3, 3, 3, -5, 5, 20, -5, 15, 3, 3, 15, -5, 20, -20, -40, -5, -5, -5, -5, -40,
        -20, 120, -20, 20, 5, 5, 20, -20, 120,
    };

    private readonly sbyte[] cells = new sbyte[CellCount];
    private readonly int[] flipStack = new int[(MaxSearchDepth + 1) * MaxFlips];
    private int rootPlayer = Light;

    public int Cell(int index) => cells[index];

    public static int Opponent(int player) => player == Dark ? Light : Dark;

    public static int RowOf(int cell) => cell / Size;

    public static int ColumnOf(int cell) => cell % Size;

    public static bool IsCorner(int cell) => cell == 0 || cell == Size - 1 || cell == CellCount - Size || cell == CellCount - 1;

    public void Reset()
    {
        Array.Clear(cells, 0, CellCount);
        cells[3 * Size + 3] = Light;
        cells[3 * Size + 4] = Dark;
        cells[4 * Size + 3] = Dark;
        cells[4 * Size + 4] = Light;
    }

    public void CopyFrom(ReversiBoard other)
    {
        Array.Copy(other.cells, cells, CellCount);
    }

    public bool IsLegal(int cell, int player) => cells[cell] == 0 && CanFlip(cell, player);

    public bool HasAnyMove(int player)
    {
        for (var cell = 0; cell < CellCount; cell++)
        {
            if (cells[cell] == 0 && CanFlip(cell, player))
            {
                return true;
            }
        }

        return false;
    }

    public ulong LegalMask(int player)
    {
        var mask = 0UL;
        for (var cell = 0; cell < CellCount; cell++)
        {
            if (cells[cell] == 0 && CanFlip(cell, player))
            {
                mask |= 1UL << cell;
            }
        }

        return mask;
    }

    public int ApplyMove(int cell, int player, Span<int> flippedOut)
    {
        if (cells[cell] != 0)
        {
            return 0;
        }

        var count = CollectFlips(cell, player, flippedOut);
        if (count == 0)
        {
            return 0;
        }

        Place(cell, player, flippedOut, count);
        return count;
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

    public int BestMove(int player, int depth)
    {
        rootPlayer = player;
        var searchDepth = Math.Clamp(depth, 1, MaxSearchDepth);
        var flips = flipStack.AsSpan(0, MaxFlips);
        var best = -1;
        var bestValue = int.MinValue;
        for (var cell = 0; cell < CellCount; cell++)
        {
            if (cells[cell] != 0)
            {
                continue;
            }

            var count = CollectFlips(cell, player, flips);
            if (count == 0)
            {
                continue;
            }

            Place(cell, player, flips, count);
            var value = Search(Opponent(player), searchDepth - 1, int.MinValue, int.MaxValue, 1);
            Unplace(cell, player, flips, count);
            if (value > bestValue)
            {
                bestValue = value;
                best = cell;
            }
        }

        return best;
    }

    private int Search(int player, int depth, int alpha, int beta, int ply)
    {
        if (depth == 0)
        {
            return Evaluate();
        }

        if (!HasAnyMove(player))
        {
            if (!HasAnyMove(Opponent(player)))
            {
                return TerminalValue();
            }

            return Search(Opponent(player), depth - 1, alpha, beta, ply);
        }

        var maximizing = player == rootPlayer;
        var best = maximizing ? int.MinValue : int.MaxValue;
        var flips = flipStack.AsSpan(ply * MaxFlips, MaxFlips);
        for (var cell = 0; cell < CellCount; cell++)
        {
            if (cells[cell] != 0)
            {
                continue;
            }

            var count = CollectFlips(cell, player, flips);
            if (count == 0)
            {
                continue;
            }

            Place(cell, player, flips, count);
            var value = Search(Opponent(player), depth - 1, alpha, beta, ply + 1);
            Unplace(cell, player, flips, count);
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

    private int Evaluate()
    {
        var positional = 0;
        for (var index = 0; index < CellCount; index++)
        {
            if (cells[index] == rootPlayer)
            {
                positional += Weights[index];
            }
            else if (cells[index] != 0)
            {
                positional -= Weights[index];
            }
        }

        var mobility = (CountMoves(rootPlayer) - CountMoves(Opponent(rootPlayer))) * MobilityWeight;
        return positional + mobility;
    }

    private int TerminalValue()
    {
        Counts(out var dark, out var light);
        var mine = rootPlayer == Dark ? dark : light;
        var theirs = rootPlayer == Dark ? light : dark;
        return (mine - theirs) * TerminalDiscValue;
    }

    private int CountMoves(int player)
    {
        var count = 0;
        for (var cell = 0; cell < CellCount; cell++)
        {
            if (cells[cell] == 0 && CanFlip(cell, player))
            {
                count++;
            }
        }

        return count;
    }

    private void Place(int cell, int player, ReadOnlySpan<int> flips, int count)
    {
        cells[cell] = (sbyte)player;
        for (var index = 0; index < count; index++)
        {
            cells[flips[index]] = (sbyte)player;
        }
    }

    private void Unplace(int cell, int player, ReadOnlySpan<int> flips, int count)
    {
        cells[cell] = 0;
        var opponent = (sbyte)Opponent(player);
        for (var index = 0; index < count; index++)
        {
            cells[flips[index]] = opponent;
        }
    }

    private bool CanFlip(int cell, int player)
    {
        var row = RowOf(cell);
        var column = ColumnOf(cell);
        var opponent = Opponent(player);
        for (var direction = 0; direction < 8; direction++)
        {
            if (RunLength(row, column, DirectionRow[direction], DirectionColumn[direction], player, opponent) > 0)
            {
                return true;
            }
        }

        return false;
    }

    private int CollectFlips(int cell, int player, Span<int> output)
    {
        var row = RowOf(cell);
        var column = ColumnOf(cell);
        var opponent = Opponent(player);
        var total = 0;
        for (var direction = 0; direction < 8; direction++)
        {
            var stepRow = DirectionRow[direction];
            var stepColumn = DirectionColumn[direction];
            var run = RunLength(row, column, stepRow, stepColumn, player, opponent);
            if (run == 0)
            {
                continue;
            }

            var flipRow = row + stepRow;
            var flipColumn = column + stepColumn;
            for (var step = 0; step < run; step++)
            {
                output[total++] = flipRow * Size + flipColumn;
                flipRow += stepRow;
                flipColumn += stepColumn;
            }
        }

        return total;
    }

    private int RunLength(int row, int column, int stepRow, int stepColumn, int player, int opponent)
    {
        var probeRow = row + stepRow;
        var probeColumn = column + stepColumn;
        var run = 0;
        while (probeRow >= 0 && probeRow < Size && probeColumn >= 0 && probeColumn < Size &&
               cells[probeRow * Size + probeColumn] == opponent)
        {
            probeRow += stepRow;
            probeColumn += stepColumn;
            run++;
        }

        if (run == 0 || probeRow < 0 || probeRow >= Size || probeColumn < 0 || probeColumn >= Size ||
            cells[probeRow * Size + probeColumn] != player)
        {
            return 0;
        }

        return run;
    }
}
