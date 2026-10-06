namespace Aetherphone.Apps.Games.Framework.World;

internal sealed class LaneGrid
{
    public const byte EmptyKind = 0;
    public const int NoEntity = -1;

    private readonly byte[] kinds;
    private readonly int[] entities;

    public LaneGrid(int columns, int rows, float cellSize = 1f)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(columns);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rows);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cellSize);
        Columns = columns;
        Rows = rows;
        CellSize = cellSize;
        kinds = new byte[columns * rows];
        entities = new int[columns * rows];
        Array.Fill(entities, NoEntity);
    }

    public int Columns { get; }

    public int Rows { get; }

    public float CellSize { get; }

    public float WorldWidth => Columns * CellSize;

    public float WorldHeight => Rows * CellSize;

    public bool InBounds(int column, int row) => (uint)column < (uint)Columns && (uint)row < (uint)Rows;

    public bool CanPlace(int column, int row) => InBounds(column, row) && kinds[Index(column, row)] == EmptyKind;

    public bool Place(int column, int row, byte kind, int entity)
    {
        if (kind == EmptyKind || !CanPlace(column, row))
        {
            return false;
        }

        var index = Index(column, row);
        kinds[index] = kind;
        entities[index] = entity;
        return true;
    }

    public bool Remove(int column, int row)
    {
        if (!InBounds(column, row))
        {
            return false;
        }

        var index = Index(column, row);
        if (kinds[index] == EmptyKind)
        {
            return false;
        }

        kinds[index] = EmptyKind;
        entities[index] = NoEntity;
        return true;
    }

    public byte KindAt(int column, int row) => InBounds(column, row) ? kinds[Index(column, row)] : EmptyKind;

    public int EntityAt(int column, int row) => InBounds(column, row) ? entities[Index(column, row)] : NoEntity;

    public int NextOccupied(int column, int fromRow, int step)
    {
        if (step == 0 || (uint)column >= (uint)Columns)
        {
            return -1;
        }

        for (var row = fromRow; (uint)row < (uint)Rows; row += step)
        {
            if (kinds[Index(column, row)] != EmptyKind)
            {
                return row;
            }
        }

        return -1;
    }

    public Vector2 CellCenter(int column, int row) => new((column + 0.5f) * CellSize, (row + 0.5f) * CellSize);

    public bool CellAt(Vector2 world, out int column, out int row)
    {
        column = (int)MathF.Floor(world.X / CellSize);
        row = (int)MathF.Floor(world.Y / CellSize);
        return InBounds(column, row);
    }

    public void Clear()
    {
        Array.Clear(kinds);
        Array.Fill(entities, NoEntity);
    }

    private int Index(int column, int row) => row * Columns + column;
}
