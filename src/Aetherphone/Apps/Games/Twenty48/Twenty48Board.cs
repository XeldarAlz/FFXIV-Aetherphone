using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games.Twenty48;

internal enum SwipeDirection : byte
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
    public const int UndoCharges = 1;
    private const int FourChanceOneIn = 10;
    private readonly int[] slideFrom = new int[CellCount];
    private readonly bool[] merged = new bool[CellCount];
    private readonly bool[] slotLocked = new bool[Size];
    private readonly int[] slotValue = new int[Size];
    private int[] values = new int[CellCount];
    private int[] previous = new int[CellCount];
    private int[] undoValues = new int[CellCount];
    private GameRandom random = GameRandom.Fresh();
    private GameRandom undoRandom;
    private int undoScore;
    private int undoMoves;
    private int undoMaxTile;
    private bool undoReady;

    public int Score { get; private set; }

    public int Moves { get; private set; }

    public int MaxTile { get; private set; }

    public int UndoLeft { get; private set; } = UndoCharges;

    public int SpawnIndex { get; private set; } = -1;

    public int LastMergeMax { get; private set; }

    public bool CanUndo => undoReady && UndoLeft > 0;

    public int Value(int index) => values[index];

    public int SlideFrom(int index) => slideFrom[index];

    public bool Merged(int index) => merged[index];

    public void Reset(GameRandom seededRandom)
    {
        random = seededRandom;
        Array.Clear(values, 0, CellCount);
        ClearTransients();
        Score = 0;
        Moves = 0;
        MaxTile = 0;
        LastMergeMax = 0;
        UndoLeft = UndoCharges;
        undoReady = false;
        SpawnRandom();
        SpawnRandom();
        ClearTransients();
        SpawnIndex = -1;
    }

    public void SetCell(int index, int value)
    {
        values[index] = value;
        if (value > MaxTile)
        {
            MaxTile = value;
        }
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
        var randomBefore = random;
        var scoreBefore = Score;
        var maxTileBefore = MaxTile;
        Array.Copy(values, previous, CellCount);
        Array.Clear(values, 0, CellCount);
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

        if (!moved)
        {
            SpawnIndex = -1;
            return false;
        }

        (undoValues, previous) = (previous, undoValues);
        undoRandom = randomBefore;
        undoScore = scoreBefore;
        undoMoves = Moves;
        undoMaxTile = maxTileBefore;
        undoReady = true;
        Moves++;
        SpawnRandom();
        return true;
    }

    public bool Undo()
    {
        if (!CanUndo)
        {
            return false;
        }

        UndoLeft--;
        undoReady = false;
        (values, undoValues) = (undoValues, values);
        random = undoRandom;
        Score = undoScore;
        Moves = undoMoves;
        MaxTile = undoMaxTile;
        LastMergeMax = 0;
        ClearTransients();
        SpawnIndex = -1;
        return true;
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

                if (combined > MaxTile)
                {
                    MaxTile = combined;
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
                var value = random.Next(FourChanceOneIn) == 0 ? 4 : 2;
                values[index] = value;
                if (value > MaxTile)
                {
                    MaxTile = value;
                }

                SpawnIndex = index;
                return;
            }

            seen++;
        }
    }
}
