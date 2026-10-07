using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games.Broadside;

internal sealed class BroadsideAi
{
    private const int Size = BroadsideFleet.Size;
    private const int CellCount = BroadsideFleet.CellCount;
    private const int HitWeight = 24;
    private static readonly int[] StepColumns = { 1, -1, 0, 0 };
    private static readonly int[] StepRows = { 0, 0, 1, -1 };

    private readonly int[] density = new int[CellCount];
    private readonly int[] candidates = new int[CellCount];
    private readonly bool[] listed = new bool[CellCount];

    public ReadOnlySpan<int> Density => density;

    public int ChooseShot(BroadsideFleet target, bool hard, ref GameRandom random)
    {
        var choice = hard ? DensityShot(target, ref random) : TargetShot(target, ref random);
        return choice >= 0 ? choice : AnyOpen(target, false, ref random);
    }

    public void BuildDensity(BroadsideFleet target)
    {
        Array.Clear(density);
        var openHits = 0;
        for (var cell = 0; cell < CellCount; cell++)
        {
            if (IsOpenHit(target, cell))
            {
                openHits++;
            }
        }

        for (var ship = 0; ship < BroadsideFleet.ShipCount; ship++)
        {
            if (target.IsSunk(ship))
            {
                continue;
            }

            var length = BroadsideFleet.Length(ship);
            for (var orientation = 0; orientation < 2; orientation++)
            {
                var across = orientation == 0;
                var lastColumn = across ? Size - length : Size - 1;
                var lastRow = across ? Size - 1 : Size - length;
                for (var row = 0; row <= lastRow; row++)
                {
                    for (var column = 0; column <= lastColumn; column++)
                    {
                        AddPlacement(target, column, row, across, length, openHits);
                    }
                }
            }
        }
    }

    private void AddPlacement(BroadsideFleet target, int column, int row, bool across, int length, int openHits)
    {
        var covered = 0;
        for (var step = 0; step < length; step++)
        {
            var cell = across ? BroadsideFleet.CellOf(column + step, row) : BroadsideFleet.CellOf(column, row + step);
            var mark = target.MarkAt(cell);
            if (mark == CellMark.Miss || target.IsSunkCell(cell))
            {
                return;
            }

            if (mark == CellMark.Hit)
            {
                covered++;
            }
        }

        if (openHits > 0 && covered == 0)
        {
            return;
        }

        var weight = openHits > 0 ? 1 + covered * HitWeight : 1;
        for (var step = 0; step < length; step++)
        {
            var cell = across ? BroadsideFleet.CellOf(column + step, row) : BroadsideFleet.CellOf(column, row + step);
            if (target.MarkAt(cell) == CellMark.None)
            {
                density[cell] += weight;
            }
        }
    }

    private int DensityShot(BroadsideFleet target, ref GameRandom random)
    {
        BuildDensity(target);
        var best = 0;
        var count = 0;
        for (var cell = 0; cell < CellCount; cell++)
        {
            if (target.MarkAt(cell) != CellMark.None || density[cell] < best || density[cell] == 0)
            {
                continue;
            }

            if (density[cell] > best)
            {
                best = density[cell];
                count = 0;
            }

            candidates[count++] = cell;
        }

        return count == 0 ? -1 : candidates[random.Next(count)];
    }

    private int TargetShot(BroadsideFleet target, ref GameRandom random)
    {
        Array.Clear(listed);
        var count = 0;
        for (var cell = 0; cell < CellCount; cell++)
        {
            if (!IsOpenHit(target, cell))
            {
                continue;
            }

            var column = BroadsideFleet.ColumnOf(cell);
            var row = BroadsideFleet.RowOf(cell);
            for (var direction = 0; direction < StepColumns.Length; direction++)
            {
                var nextColumn = column + StepColumns[direction];
                var nextRow = row + StepRows[direction];
                if (!BroadsideFleet.InBounds(nextColumn, nextRow))
                {
                    continue;
                }

                var next = BroadsideFleet.CellOf(nextColumn, nextRow);
                if (listed[next] || target.MarkAt(next) != CellMark.None)
                {
                    continue;
                }

                listed[next] = true;
                candidates[count++] = next;
            }
        }

        return count > 0 ? candidates[random.Next(count)] : AnyOpen(target, true, ref random);
    }

    private int AnyOpen(BroadsideFleet target, bool parity, ref GameRandom random)
    {
        var count = 0;
        for (var cell = 0; cell < CellCount; cell++)
        {
            if (target.MarkAt(cell) != CellMark.None)
            {
                continue;
            }

            if (parity && (BroadsideFleet.ColumnOf(cell) + BroadsideFleet.RowOf(cell)) % 2 != 0)
            {
                continue;
            }

            candidates[count++] = cell;
        }

        if (count == 0)
        {
            return parity ? AnyOpen(target, false, ref random) : -1;
        }

        return candidates[random.Next(count)];
    }

    private static bool IsOpenHit(BroadsideFleet target, int cell) =>
        target.MarkAt(cell) == CellMark.Hit && !target.IsSunkCell(cell);
}
