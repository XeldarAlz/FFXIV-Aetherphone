using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games.Broadside;

internal enum ShotResult : byte
{
    Invalid,
    Miss,
    Hit,
    Sunk,
}

internal enum CellMark : byte
{
    None,
    Miss,
    Hit,
}

internal sealed class BroadsideFleet
{
    public const int Size = 10;
    public const int CellCount = Size * Size;
    public const int ShipCount = 5;
    public const int ShipCells = 17;
    public const int LongestShip = 5;
    public const int NoShip = -1;
    private const int PlacementAttempts = 400;
    private static readonly int[] Lengths = { 5, 4, 3, 3, 2 };

    private readonly sbyte[] occupant = new sbyte[CellCount];
    private readonly CellMark[] marks = new CellMark[CellCount];
    private readonly int[] columns = new int[ShipCount];
    private readonly int[] rows = new int[ShipCount];
    private readonly bool[] horizontal = new bool[ShipCount];
    private readonly bool[] placed = new bool[ShipCount];
    private readonly int[] hits = new int[ShipCount];

    public BroadsideFleet()
    {
        Clear();
    }

    public int ShotsTaken { get; private set; }

    public int LastShot { get; private set; } = -1;

    public bool AllPlaced
    {
        get
        {
            for (var ship = 0; ship < ShipCount; ship++)
            {
                if (!placed[ship])
                {
                    return false;
                }
            }

            return true;
        }
    }

    public bool AllSunk
    {
        get
        {
            for (var ship = 0; ship < ShipCount; ship++)
            {
                if (!IsSunk(ship))
                {
                    return false;
                }
            }

            return true;
        }
    }

    public int ShipsAfloat
    {
        get
        {
            var afloat = 0;
            for (var ship = 0; ship < ShipCount; ship++)
            {
                if (!IsSunk(ship))
                {
                    afloat++;
                }
            }

            return afloat;
        }
    }

    public static int Length(int ship) => Lengths[ship];

    public static int CellOf(int column, int row) => row * Size + column;

    public static int ColumnOf(int cell) => cell % Size;

    public static int RowOf(int cell) => cell / Size;

    public static bool InBounds(int column, int row) => column >= 0 && column < Size && row >= 0 && row < Size;

    public static bool Fits(int ship, int column, int row, bool across)
    {
        var length = Lengths[ship];
        var endColumn = across ? column + length - 1 : column;
        var endRow = across ? row : row + length - 1;
        return InBounds(column, row) && InBounds(endColumn, endRow);
    }

    public bool IsPlaced(int ship) => placed[ship];

    public int Column(int ship) => columns[ship];

    public int Row(int ship) => rows[ship];

    public bool Horizontal(int ship) => horizontal[ship];

    public int OccupantAt(int cell) => occupant[cell];

    public CellMark MarkAt(int cell) => marks[cell];

    public int HitsOn(int ship) => hits[ship];

    public bool IsSunk(int ship) => placed[ship] && hits[ship] >= Lengths[ship];

    public bool IsSunkCell(int cell) => marks[cell] == CellMark.Hit && occupant[cell] >= 0 && IsSunk(occupant[cell]);

    public void Clear()
    {
        Array.Fill(occupant, (sbyte)NoShip);
        Array.Clear(marks);
        Array.Clear(placed);
        Array.Clear(hits);
        ShotsTaken = 0;
        LastShot = -1;
    }

    public bool CanPlace(int ship, int column, int row, bool across)
    {
        if (!Fits(ship, column, row, across))
        {
            return false;
        }

        var length = Lengths[ship];
        for (var step = 0; step < length; step++)
        {
            var cell = across ? CellOf(column + step, row) : CellOf(column, row + step);
            var other = occupant[cell];
            if (other != NoShip && other != ship)
            {
                return false;
            }
        }

        return true;
    }

    public bool Place(int ship, int column, int row, bool across)
    {
        if (!CanPlace(ship, column, row, across))
        {
            return false;
        }

        Remove(ship);
        columns[ship] = column;
        rows[ship] = row;
        horizontal[ship] = across;
        placed[ship] = true;
        var length = Lengths[ship];
        for (var step = 0; step < length; step++)
        {
            occupant[across ? CellOf(column + step, row) : CellOf(column, row + step)] = (sbyte)ship;
        }

        return true;
    }

    public void Remove(int ship)
    {
        if (!placed[ship])
        {
            return;
        }

        placed[ship] = false;
        for (var cell = 0; cell < CellCount; cell++)
        {
            if (occupant[cell] == ship)
            {
                occupant[cell] = NoShip;
            }
        }
    }

    public void AutoPlace(ref GameRandom random)
    {
        for (var pass = 0; pass < PlacementAttempts; pass++)
        {
            Clear();
            if (TryPlaceAll(ref random))
            {
                return;
            }
        }
    }

    public int ShipCellsOf(int ship, Span<int> cells)
    {
        if (!placed[ship])
        {
            return 0;
        }

        var length = Math.Min(Lengths[ship], cells.Length);
        for (var step = 0; step < length; step++)
        {
            cells[step] = horizontal[ship] ? CellOf(columns[ship] + step, rows[ship]) : CellOf(columns[ship], rows[ship] + step);
        }

        return length;
    }

    public ShotResult Fire(int cell, out int ship)
    {
        ship = NoShip;
        if (cell < 0 || cell >= CellCount || marks[cell] != CellMark.None)
        {
            return ShotResult.Invalid;
        }

        ShotsTaken++;
        LastShot = cell;
        var target = occupant[cell];
        if (target == NoShip)
        {
            marks[cell] = CellMark.Miss;
            return ShotResult.Miss;
        }

        marks[cell] = CellMark.Hit;
        hits[target]++;
        ship = target;
        return IsSunk(target) ? ShotResult.Sunk : ShotResult.Hit;
    }

    private bool TryPlaceAll(ref GameRandom random)
    {
        for (var ship = 0; ship < ShipCount; ship++)
        {
            var done = false;
            for (var attempt = 0; attempt < PlacementAttempts && !done; attempt++)
            {
                var across = random.Next(2) == 0;
                var length = Lengths[ship];
                var column = random.Next(across ? Size - length + 1 : Size);
                var row = random.Next(across ? Size : Size - length + 1);
                done = Place(ship, column, row, across);
            }

            if (!done)
            {
                return false;
            }
        }

        return true;
    }
}
