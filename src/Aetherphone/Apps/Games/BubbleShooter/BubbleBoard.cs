using System;
using System.Numerics;

namespace Aetherphone.Apps.Games.BubbleShooter;

internal sealed class BubbleBoard
{
    public const int Columns = 8;

    public const int MaxRows = 14;

    public const int ColorCount = 5;

    public const int NewRowEvery = 6;

    private const float Speed = 1.7f;

    public readonly float Diameter = 1f / (Columns + 0.5f);

    private readonly int[] cells = new int[MaxRows * Columns];

    private readonly int[] queue = new int[MaxRows * Columns];

    private readonly bool[] connected = new bool[MaxRows * Columns];

    private readonly bool[] clusterMark = new bool[MaxRows * Columns];

    private readonly Vector2[] popPositions = new Vector2[MaxRows * Columns];

    private readonly int[] popColors = new int[MaxRows * Columns];

    private readonly Random random = new();

    private float firstRowY;

    private Vector2 flyVelocity;

    private int shotCount;

    public float FieldHeight { get; private set; } = 1.7f;

    public float Radius => Diameter * 0.5f;

    public int CurrentColor { get; private set; }

    public int NextColor { get; private set; }

    public bool Flying { get; private set; }

    public Vector2 FlyPosition { get; private set; }

    public int FlyColor { get; private set; }

    public int Score { get; private set; }

    public bool GameOver { get; private set; }

    public int PopCount { get; private set; }

    public bool DroppedThisShot { get; private set; }

    public Vector2 LauncherPosition => new(0.5f, FieldHeight - 0.10f);

    public float DangerY => FieldHeight - 0.20f;

    public int ColorAt(int column, int row) => cells[row * Columns + column];

    public Vector2 PopPosition(int index) => popPositions[index];

    public int PopColor(int index) => popColors[index];

    public Vector2 CellCenter(int column, int row)
    {
        var x = Radius + column * Diameter + (row % 2 == 1 ? Radius : 0f);
        var y = firstRowY + row * Diameter * 0.86f;
        return new Vector2(x, y);
    }

    public void Reset(float fieldHeight)
    {
        FieldHeight = fieldHeight;
        firstRowY = Radius + 0.02f;
        Array.Clear(cells, 0, cells.Length);
        for (var index = 0; index < cells.Length; index++)
        {
            cells[index] = -1;
        }

        for (var row = 0; row < 4; row++)
        {
            for (var column = 0; column < Columns; column++)
            {
                cells[row * Columns + column] = random.Next(ColorCount);
            }
        }

        shotCount = 0;
        Flying = false;
        GameOver = false;
        Score = 0;
        PopCount = 0;
        CurrentColor = PickPlayableColor();
        NextColor = PickPlayableColor();
    }

    public void SetFieldHeight(float fieldHeight)
    {
        FieldHeight = fieldHeight;
        firstRowY = Radius + 0.02f;
    }

    public void Fire(Vector2 direction)
    {
        if (Flying || GameOver)
        {
            return;
        }

        if (direction.Y > -0.15f)
        {
            return;
        }

        Flying = true;
        FlyColor = CurrentColor;
        FlyPosition = LauncherPosition;
        flyVelocity = Vector2.Normalize(direction) * Speed;
        CurrentColor = NextColor;
        NextColor = PickPlayableColor();
    }

    public void Update(float deltaSeconds)
    {
        PopCount = 0;
        DroppedThisShot = false;

        if (!Flying || GameOver)
        {
            return;
        }

        var substeps = Math.Clamp(1 + (int)(Speed * deltaSeconds / (Radius * 0.5f)), 1, 10);
        var subDelta = deltaSeconds / substeps;
        for (var step = 0; step < substeps && Flying; step++)
        {
            FlyPosition += flyVelocity * subDelta;
            var position = FlyPosition;

            if (position.X < Radius)
            {
                position.X = Radius;
                flyVelocity.X = MathF.Abs(flyVelocity.X);
            }
            else if (position.X > 1f - Radius)
            {
                position.X = 1f - Radius;
                flyVelocity.X = -MathF.Abs(flyVelocity.X);
            }

            FlyPosition = position;

            if (position.Y - Radius <= 0f || TouchesBubble(position))
            {
                Snap(position);
                Flying = false;
            }
        }
    }

    private bool TouchesBubble(Vector2 position)
    {
        var threshold = Diameter * 0.9f;
        for (var index = 0; index < cells.Length; index++)
        {
            if (cells[index] < 0)
            {
                continue;
            }

            var center = CellCenter(index % Columns, index / Columns);
            if (Vector2.DistanceSquared(center, position) < threshold * threshold)
            {
                return true;
            }
        }

        return false;
    }

    private void Snap(Vector2 position)
    {
        var bestCell = -1;
        var bestDistance = float.MaxValue;
        for (var index = 0; index < cells.Length; index++)
        {
            if (cells[index] >= 0)
            {
                continue;
            }

            var center = CellCenter(index % Columns, index / Columns);
            var distance = Vector2.DistanceSquared(center, position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestCell = index;
            }
        }

        if (bestCell < 0)
        {
            GameOver = true;
            return;
        }

        cells[bestCell] = FlyColor;
        shotCount++;

        if (ResolveCluster(bestCell))
        {
            DropFloating();
        }

        if (shotCount % NewRowEvery == 0)
        {
            ShiftDown();
        }

        CheckLose();
    }

    private bool ResolveCluster(int startCell)
    {
        Array.Clear(clusterMark, 0, clusterMark.Length);
        var color = cells[startCell];
        var read = 0;
        var write = 0;
        queue[write++] = startCell;
        clusterMark[startCell] = true;

        Span<int> neighborCells = stackalloc int[6];
        while (read < write)
        {
            var cell = queue[read++];
            var count = Neighbors(cell, neighborCells);
            for (var index = 0; index < count; index++)
            {
                var neighbor = neighborCells[index];
                if (!clusterMark[neighbor] && cells[neighbor] == color)
                {
                    clusterMark[neighbor] = true;
                    queue[write++] = neighbor;
                }
            }
        }

        if (write < 3)
        {
            return false;
        }

        for (var index = 0; index < write; index++)
        {
            var cell = queue[index];
            RecordPop(cell);
            cells[cell] = -1;
        }

        Score += write * 10;
        return true;
    }

    private void DropFloating()
    {
        Array.Clear(connected, 0, connected.Length);
        var write = 0;
        for (var column = 0; column < Columns; column++)
        {
            if (cells[column] >= 0)
            {
                connected[column] = true;
                queue[write++] = column;
            }
        }

        var read = 0;
        Span<int> neighborCells = stackalloc int[6];
        while (read < write)
        {
            var cell = queue[read++];
            var count = Neighbors(cell, neighborCells);
            for (var index = 0; index < count; index++)
            {
                var neighbor = neighborCells[index];
                if (!connected[neighbor] && cells[neighbor] >= 0)
                {
                    connected[neighbor] = true;
                    queue[write++] = neighbor;
                }
            }
        }

        for (var index = 0; index < cells.Length; index++)
        {
            if (cells[index] >= 0 && !connected[index])
            {
                RecordPop(index);
                cells[index] = -1;
                Score += 20;
                DroppedThisShot = true;
            }
        }
    }

    private void RecordPop(int cell)
    {
        popPositions[PopCount] = CellCenter(cell % Columns, cell / Columns);
        popColors[PopCount] = cells[cell];
        PopCount++;
    }

    private void ShiftDown()
    {
        for (var row = MaxRows - 1; row >= 1; row--)
        {
            for (var column = 0; column < Columns; column++)
            {
                cells[row * Columns + column] = cells[(row - 1) * Columns + column];
            }
        }

        for (var column = 0; column < Columns; column++)
        {
            cells[column] = random.Next(ColorCount);
        }
    }

    private void CheckLose()
    {
        for (var index = 0; index < cells.Length; index++)
        {
            if (cells[index] < 0)
            {
                continue;
            }

            if (CellCenter(index % Columns, index / Columns).Y + Radius >= DangerY)
            {
                GameOver = true;
                return;
            }
        }
    }

    private int Neighbors(int cell, Span<int> output)
    {
        var column = cell % Columns;
        var row = cell / Columns;
        var even = row % 2 == 0;
        var count = 0;

        count = Add(output, count, column - 1, row);
        count = Add(output, count, column + 1, row);
        if (even)
        {
            count = Add(output, count, column - 1, row - 1);
            count = Add(output, count, column, row - 1);
            count = Add(output, count, column - 1, row + 1);
            count = Add(output, count, column, row + 1);
        }
        else
        {
            count = Add(output, count, column, row - 1);
            count = Add(output, count, column + 1, row - 1);
            count = Add(output, count, column, row + 1);
            count = Add(output, count, column + 1, row + 1);
        }

        return count;
    }

    private int Add(Span<int> output, int count, int column, int row)
    {
        if (column < 0 || column >= Columns || row < 0 || row >= MaxRows)
        {
            return count;
        }

        output[count] = row * Columns + column;
        return count + 1;
    }

    private int PickPlayableColor()
    {
        Span<bool> present = stackalloc bool[ColorCount];
        var found = false;
        for (var index = 0; index < cells.Length; index++)
        {
            var color = cells[index];
            if (color >= 0)
            {
                present[color] = true;
                found = true;
            }
        }

        if (!found)
        {
            return random.Next(ColorCount);
        }

        for (var attempt = 0; attempt < 16; attempt++)
        {
            var candidate = random.Next(ColorCount);
            if (present[candidate])
            {
                return candidate;
            }
        }

        return random.Next(ColorCount);
    }
}
