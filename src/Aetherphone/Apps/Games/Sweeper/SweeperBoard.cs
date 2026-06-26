using System;

namespace Aetherphone.Apps.Games.Sweeper;

internal enum Difficulty
{
    Easy,
    Medium,
    Hard,
}

internal enum SweeperState
{
    Playing,
    Won,
    Lost,
}

internal sealed class SweeperBoard
{
    public const int MaxCells = 13 * 13;

    private readonly bool[] mines = new bool[MaxCells];

    private readonly bool[] revealed = new bool[MaxCells];

    private readonly bool[] flagged = new bool[MaxCells];

    private readonly int[] adjacent = new int[MaxCells];

    private readonly int[] floodQueue = new int[MaxCells];

    private readonly Random random = new();

    private bool firstClick;

    public int Columns { get; private set; } = 9;

    public int Rows { get; private set; } = 9;

    public int MineCount { get; private set; } = 10;

    public Difficulty Difficulty { get; private set; }

    public SweeperState State { get; private set; }

    public int FlagCount { get; private set; }

    public int ClickedBomb { get; private set; } = -1;

    public int CellCount => Columns * Rows;

    public int MinesRemaining => MineCount - FlagCount;

    public bool IsRevealed(int index) => revealed[index];

    public bool IsFlagged(int index) => flagged[index];

    public bool IsMine(int index) => mines[index];

    public int Adjacent(int index) => adjacent[index];

    public static void Dimensions(Difficulty difficulty, out int columns, out int rows, out int mineCount)
    {
        switch (difficulty)
        {
            case Difficulty.Medium:
                columns = 11;
                rows = 11;
                mineCount = 20;
                break;
            case Difficulty.Hard:
                columns = 13;
                rows = 13;
                mineCount = 30;
                break;
            default:
                columns = 9;
                rows = 9;
                mineCount = 10;
                break;
        }
    }

    public void Reset(Difficulty difficulty)
    {
        Difficulty = difficulty;
        Dimensions(difficulty, out var columns, out var rows, out var mineCount);
        Columns = columns;
        Rows = rows;
        MineCount = mineCount;

        Array.Clear(mines, 0, MaxCells);
        Array.Clear(revealed, 0, MaxCells);
        Array.Clear(flagged, 0, MaxCells);
        Array.Clear(adjacent, 0, MaxCells);

        State = SweeperState.Playing;
        FlagCount = 0;
        ClickedBomb = -1;
        firstClick = true;
    }

    public void Reveal(int index)
    {
        if (State != SweeperState.Playing || revealed[index] || flagged[index])
        {
            return;
        }

        if (firstClick)
        {
            firstClick = false;
            PlaceMines(index);
        }

        if (mines[index])
        {
            Detonate(index);
            return;
        }

        if (adjacent[index] == 0)
        {
            FloodReveal(index);
        }
        else
        {
            revealed[index] = true;
        }

        CheckWin();
    }

    public void ToggleFlag(int index)
    {
        if (State != SweeperState.Playing || revealed[index])
        {
            return;
        }

        if (flagged[index])
        {
            flagged[index] = false;
            FlagCount--;
        }
        else
        {
            flagged[index] = true;
            FlagCount++;
        }
    }

    public bool Chord(int index)
    {
        if (State != SweeperState.Playing || !revealed[index] || adjacent[index] == 0)
        {
            return false;
        }

        var column = index % Columns;
        var row = index / Columns;
        var flaggedNeighbors = 0;

        for (var rowOffset = -1; rowOffset <= 1; rowOffset++)
        {
            for (var columnOffset = -1; columnOffset <= 1; columnOffset++)
            {
                var neighbor = NeighborIndex(column, row, columnOffset, rowOffset);
                if (neighbor >= 0 && flagged[neighbor])
                {
                    flaggedNeighbors++;
                }
            }
        }

        if (flaggedNeighbors != adjacent[index])
        {
            return false;
        }

        var triggered = false;
        for (var rowOffset = -1; rowOffset <= 1; rowOffset++)
        {
            for (var columnOffset = -1; columnOffset <= 1; columnOffset++)
            {
                var neighbor = NeighborIndex(column, row, columnOffset, rowOffset);
                if (neighbor < 0 || revealed[neighbor] || flagged[neighbor])
                {
                    continue;
                }

                triggered = true;
                Reveal(neighbor);
                if (State != SweeperState.Playing)
                {
                    return true;
                }
            }
        }

        return triggered;
    }

    private void PlaceMines(int safeIndex)
    {
        var safeColumn = safeIndex % Columns;
        var safeRow = safeIndex / Columns;
        var placed = 0;

        while (placed < MineCount)
        {
            var index = random.Next(CellCount);
            if (mines[index])
            {
                continue;
            }

            var column = index % Columns;
            var row = index / Columns;
            if (Math.Abs(column - safeColumn) <= 1 && Math.Abs(row - safeRow) <= 1)
            {
                continue;
            }

            mines[index] = true;
            placed++;
        }

        for (var index = 0; index < CellCount; index++)
        {
            adjacent[index] = CountAdjacent(index);
        }
    }

    private int CountAdjacent(int index)
    {
        var column = index % Columns;
        var row = index / Columns;
        var count = 0;

        for (var rowOffset = -1; rowOffset <= 1; rowOffset++)
        {
            for (var columnOffset = -1; columnOffset <= 1; columnOffset++)
            {
                var neighbor = NeighborIndex(column, row, columnOffset, rowOffset);
                if (neighbor >= 0 && mines[neighbor])
                {
                    count++;
                }
            }
        }

        return count;
    }

    private void FloodReveal(int startIndex)
    {
        var read = 0;
        var write = 1;
        floodQueue[0] = startIndex;
        revealed[startIndex] = true;

        while (read < write)
        {
            var index = floodQueue[read];
            read++;

            if (adjacent[index] > 0)
            {
                continue;
            }

            var column = index % Columns;
            var row = index / Columns;
            for (var rowOffset = -1; rowOffset <= 1; rowOffset++)
            {
                for (var columnOffset = -1; columnOffset <= 1; columnOffset++)
                {
                    var neighbor = NeighborIndex(column, row, columnOffset, rowOffset);
                    if (neighbor < 0 || revealed[neighbor] || flagged[neighbor])
                    {
                        continue;
                    }

                    revealed[neighbor] = true;
                    floodQueue[write] = neighbor;
                    write++;
                }
            }
        }
    }

    private void Detonate(int index)
    {
        ClickedBomb = index;
        State = SweeperState.Lost;

        for (var cell = 0; cell < CellCount; cell++)
        {
            if (mines[cell])
            {
                revealed[cell] = true;
            }
        }
    }

    private void CheckWin()
    {
        for (var index = 0; index < CellCount; index++)
        {
            if (!mines[index] && !revealed[index])
            {
                return;
            }
        }

        State = SweeperState.Won;
    }

    private int NeighborIndex(int column, int row, int columnOffset, int rowOffset)
    {
        if (columnOffset == 0 && rowOffset == 0)
        {
            return -1;
        }

        var targetColumn = column + columnOffset;
        var targetRow = row + rowOffset;
        if (targetColumn < 0 || targetColumn >= Columns || targetRow < 0 || targetRow >= Rows)
        {
            return -1;
        }

        return targetRow * Columns + targetColumn;
    }
}
