using System;

namespace Aetherphone.Apps.Games.Twenty48;

internal enum SwipeDirection
{
    Up,
    Right,
    Down,
    Left,
}

internal sealed class Twenty48Board
{
    public const int Size = 4;

    public const int CellCount = Size * Size;

    public const int WinValue = 2048;

    private readonly int[] values = new int[CellCount];

    private readonly int[] previous = new int[CellCount];

    private readonly int[] slideFrom = new int[CellCount];

    private readonly bool[] merged = new bool[CellCount];

    private readonly bool[] slotLocked = new bool[Size];

    private readonly int[] slotValue = new int[Size];

    private readonly Random random = new();

    public int Score { get; private set; }

    public bool Won { get; private set; }

    public int SpawnIndex { get; private set; } = -1;

    public int LastMergeMax { get; private set; }

    public int Value(int index) => values[index];

    public int SlideFrom(int index) => slideFrom[index];

    public bool Merged(int index) => merged[index];

    public void Reset()
    {
        Array.Clear(values, 0, CellCount);
        ClearTransients();
        Score = 0;
        Won = false;
        SpawnIndex = -1;
        LastMergeMax = 0;
        SpawnRandom();
        SpawnRandom();
        ClearTransients();
        SpawnIndex = -1;
    }

    public bool CanMove()
    {
        for (var index = 0; index < CellCount; index++)
        {
            if (values[index] == 0)
            {
                return true;
            }
        }

        for (var row = 0; row < Size; row++)
        {
            for (var column = 0; column < Size; column++)
            {
                var value = values[row * Size + column];
                if (column + 1 < Size && values[row * Size + column + 1] == value)
                {
                    return true;
                }

                if (row + 1 < Size && values[(row + 1) * Size + column] == value)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public bool TryMove(SwipeDirection direction)
    {
        Array.Copy(values, previous, CellCount);
        ClearTransients();
        LastMergeMax = 0;

        for (var line = 0; line < Size; line++)
        {
            CollapseLine(direction, line);
        }

        var moved = false;
        for (var index = 0; index < CellCount; index++)
        {
            if (values[index] != previous[index])
            {
                moved = true;
                break;
            }
        }

        if (moved)
        {
            SpawnRandom();
        }
        else
        {
            SpawnIndex = -1;
        }

        return moved;
    }

    private void CollapseLine(SwipeDirection direction, int line)
    {
        for (var slot = 0; slot < Size; slot++)
        {
            slotLocked[slot] = false;
            slotValue[slot] = 0;
        }

        var write = 0;
        for (var step = 0; step < Size; step++)
        {
            var sourceIndex = LineCell(direction, line, step);
            var value = previous[sourceIndex];
            if (value == 0)
            {
                continue;
            }

            if (write > 0 && !slotLocked[write - 1] && slotValue[write - 1] == value)
            {
                var targetIndex = LineCell(direction, line, write - 1);
                var combined = value * 2;
                values[targetIndex] = combined;
                slotValue[write - 1] = combined;
                slotLocked[write - 1] = true;
                merged[targetIndex] = true;
                slideFrom[targetIndex] = sourceIndex;
                Score += combined;
                if (combined > LastMergeMax)
                {
                    LastMergeMax = combined;
                }

                if (combined >= WinValue)
                {
                    Won = true;
                }
            }
            else
            {
                var targetIndex = LineCell(direction, line, write);
                values[targetIndex] = value;
                slotValue[write] = value;
                if (targetIndex != sourceIndex)
                {
                    slideFrom[targetIndex] = sourceIndex;
                }

                write++;
            }
        }
    }

    private static int LineCell(SwipeDirection direction, int line, int step)
    {
        return direction switch
        {
            SwipeDirection.Left => line * Size + step,
            SwipeDirection.Right => line * Size + (Size - 1 - step),
            SwipeDirection.Up => step * Size + line,
            _ => (Size - 1 - step) * Size + line,
        };
    }

    private void ClearTransients()
    {
        for (var index = 0; index < CellCount; index++)
        {
            slideFrom[index] = -1;
            merged[index] = false;
        }
    }

    private void SpawnRandom()
    {
        var empty = 0;
        for (var index = 0; index < CellCount; index++)
        {
            if (values[index] == 0)
            {
                empty++;
            }
        }

        if (empty == 0)
        {
            SpawnIndex = -1;
            return;
        }

        var target = random.Next(empty);
        var seen = 0;
        for (var index = 0; index < CellCount; index++)
        {
            if (values[index] != 0)
            {
                continue;
            }

            if (seen == target)
            {
                values[index] = random.Next(10) == 0 ? 4 : 2;
                SpawnIndex = index;
                return;
            }

            seen++;
        }
    }
}
