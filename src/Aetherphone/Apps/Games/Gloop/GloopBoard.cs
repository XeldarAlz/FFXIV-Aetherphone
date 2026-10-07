using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games.Gloop;

internal enum GloopPhase : byte
{
    Falling,
    Settling,
    Popping,
    Over,
}

internal sealed class GloopBoard
{
    public const float LockSeconds = 0.42f;
    public const float SoftDropRowsPerSecond = 22f;
    public const float PopSeconds = 0.42f;
    public const float SettleGravity = 80f;
    public const float MaxSettleSpeed = 26f;
    public const float SquashSeconds = 0.24f;
    public const int QueuedPieces = 3;
    private const int MaxLockResets = 10;
    private const float SoftLockFactor = 3f;
    private const float RockDropHeight = 2f;
    private const float RockColumnStagger = 0.35f;
    private const int Cells = GloopRules.Cells;

    private readonly byte[] grid = new byte[Cells];
    private readonly float[] offset = new float[Cells];
    private readonly float[] velocity = new float[Cells];
    private readonly float[] squash = new float[Cells];
    private readonly bool[] pop = new bool[Cells];
    private readonly bool[] seen = new bool[Cells];
    private readonly int[] group = new int[Cells];
    private readonly byte[] fall = new byte[Cells];
    private readonly byte[] queue = new byte[QueuedPieces * 2];
    private readonly int[] landedCells = new int[Cells];
    private readonly int[] poppedCells = new int[Cells];
    private readonly byte[] poppedColors = new byte[Cells];
    private readonly int[] perColumn = new int[GloopRules.Columns];
    private GameRandom pieceRandom;
    private GameRandom garbageRandom;
    private float fallProgress;
    private float lockTimer;
    private int lockResets;
    private bool softDrop;
    private float popTimer;
    private int popCount;
    private int garbageRemainder;
    private bool garbageDropped;

    public GloopPhase Phase { get; private set; } = GloopPhase.Over;

    public bool Versus { get; private set; }

    public float Gravity { get; set; } = 1f;

    public int Score { get; private set; }

    public int Chain { get; private set; }

    public int BestChain { get; private set; }

    public int Popped { get; private set; }

    public int Pieces { get; private set; }

    public int Spawns { get; private set; }

    public int Incoming { get; private set; }

    public int Outgoing { get; private set; }

    public int Sent { get; private set; }

    public int PieceColumn { get; private set; }

    public int PieceRow { get; private set; }

    public int PieceOrientation { get; private set; }

    public byte PivotColor { get; private set; }

    public byte SatelliteColor { get; private set; }

    public bool Resting { get; private set; }

    public int LandedCount { get; private set; }

    public int PoppedCount { get; private set; }

    public int ClearedCount { get; private set; }

    public int PopChain { get; private set; }

    public int PopGain { get; private set; }

    public int ChainEnded { get; private set; }

    public bool AllClearNow { get; private set; }

    public int GarbageDropped { get; private set; }

    public bool LockedNow { get; private set; }

    public bool DiedNow { get; private set; }

    public bool Falling => Phase == GloopPhase.Falling;

    public bool Over => Phase == GloopPhase.Over;

    public float PieceFraction => Resting ? 0f : Math.Clamp(fallProgress, 0f, 1f);

    public float PopProgress => Phase == GloopPhase.Popping ? Math.Clamp(popTimer / PopSeconds, 0f, 1f) : 0f;

    public ReadOnlySpan<byte> Grid => grid;

    public byte Cell(int cell) => grid[cell];

    public float Offset(int cell) => offset[cell];

    public float Squash(int cell) => squash[cell];

    public bool IsPopping(int cell) => Phase == GloopPhase.Popping && pop[cell];

    public int LandedCell(int index) => landedCells[index];

    public int PoppedCell(int index) => poppedCells[index];

    public byte PoppedColor(int index) => poppedColors[index];

    public byte NextPivot(int index) => queue[index * 2];

    public byte NextSatellite(int index) => queue[index * 2 + 1];

    public void Reset(GameRandom pieces, GameRandom garbage, bool versus)
    {
        pieceRandom = pieces;
        garbageRandom = garbage;
        Versus = versus;
        Array.Clear(grid);
        Array.Clear(offset);
        Array.Clear(velocity);
        Array.Clear(squash);
        Array.Clear(pop);
        Score = 0;
        Chain = 0;
        BestChain = 0;
        Popped = 0;
        Pieces = 0;
        Spawns = 0;
        Incoming = 0;
        Outgoing = 0;
        Sent = 0;
        garbageRemainder = 0;
        garbageDropped = false;
        softDrop = false;
        Gravity = 1f;
        for (var slot = 0; slot < queue.Length; slot++)
        {
            queue[slot] = RandomColor();
        }

        ClearEvents();
        Spawn();
    }

    public void Load(ReadOnlySpan<byte> cells)
    {
        cells.CopyTo(grid);
        Array.Clear(offset);
        Array.Clear(velocity);
        Array.Clear(pop);
    }

    public void SetPiece(byte pivot, byte satellite)
    {
        PivotColor = pivot;
        SatelliteColor = satellite;
    }

    public void SetNext(int index, byte pivot, byte satellite)
    {
        queue[index * 2] = pivot;
        queue[index * 2 + 1] = satellite;
    }

    public void Step(float deltaSeconds)
    {
        ClearEvents();
        if (deltaSeconds <= 0f || Phase == GloopPhase.Over)
        {
            return;
        }

        for (var cell = 0; cell < Cells; cell++)
        {
            squash[cell] = MathF.Max(0f, squash[cell] - deltaSeconds / SquashSeconds);
        }

        switch (Phase)
        {
            case GloopPhase.Falling:
                StepFalling(deltaSeconds);
                return;
            case GloopPhase.Settling:
                StepSettling(deltaSeconds);
                return;
            case GloopPhase.Popping:
                StepPopping(deltaSeconds);
                return;
            default:
                return;
        }
    }

    public bool Shift(int direction)
    {
        if (Phase != GloopPhase.Falling || !CanOccupy(PieceColumn + direction, PieceRow, PieceOrientation))
        {
            return false;
        }

        PieceColumn += direction;
        TouchLock();
        return true;
    }

    public bool Rotate(int direction)
    {
        if (Phase != GloopPhase.Falling)
        {
            return false;
        }

        var next = (PieceOrientation + direction + GloopRules.Orientations) % GloopRules.Orientations;
        var sideStep = GloopRules.SatelliteColumn(next);
        if (TryTurn(PieceColumn, PieceRow, next))
        {
            return true;
        }

        if (sideStep != 0 && TryTurn(PieceColumn - sideStep, PieceRow, next))
        {
            return true;
        }

        if (GloopRules.SatelliteRow(next) > 0 && TryTurn(PieceColumn, PieceRow - 1, next))
        {
            return true;
        }

        if (sideStep == 0)
        {
            return false;
        }

        var flipped = (PieceOrientation + 2) % GloopRules.Orientations;
        return TryTurn(PieceColumn, PieceRow, flipped) || TryTurn(PieceColumn, PieceRow - 1, flipped);
    }

    public void SoftDrop(bool held)
    {
        softDrop = held;
    }

    public void HardDrop()
    {
        if (Phase != GloopPhase.Falling)
        {
            return;
        }

        while (CanOccupy(PieceColumn, PieceRow + 1, PieceOrientation))
        {
            PieceRow++;
            Score++;
        }

        Lock();
    }

    public void AddIncoming(int count)
    {
        if (count <= 0)
        {
            return;
        }

        Incoming += count;
    }

    public int TakeOutgoing()
    {
        var count = Outgoing;
        Outgoing = 0;
        return count;
    }

    public void Landing(out int pivotCell, out int satelliteCell)
    {
        var satelliteColumn = PieceColumn + GloopRules.SatelliteColumn(PieceOrientation);
        if (GloopRules.SatelliteColumn(PieceOrientation) == 0)
        {
            var floor = GloopRules.Rows - 1 - GloopRules.Height(grid, PieceColumn);
            var lower = floor;
            var upper = floor - 1;
            var pivotLow = PieceOrientation == 0;
            pivotCell = CellOrMissing(PieceColumn, pivotLow ? lower : upper);
            satelliteCell = CellOrMissing(PieceColumn, pivotLow ? upper : lower);
            return;
        }

        pivotCell = CellOrMissing(PieceColumn, GloopRules.Rows - 1 - GloopRules.Height(grid, PieceColumn));
        satelliteCell = CellOrMissing(satelliteColumn, GloopRules.Rows - 1 - GloopRules.Height(grid, satelliteColumn));
    }

    private static int CellOrMissing(int column, int row) =>
        row < 0 || column < 0 || column >= GloopRules.Columns ? -1 : GloopRules.Index(column, row);

    private void ClearEvents()
    {
        LandedCount = 0;
        PoppedCount = 0;
        ClearedCount = 0;
        PopChain = 0;
        PopGain = 0;
        ChainEnded = 0;
        AllClearNow = false;
        GarbageDropped = 0;
        LockedNow = false;
        DiedNow = false;
    }

    private void StepFalling(float deltaSeconds)
    {
        var speed = softDrop ? MathF.Max(Gravity, SoftDropRowsPerSecond) : Gravity;
        if (!CanOccupy(PieceColumn, PieceRow + 1, PieceOrientation))
        {
            Resting = true;
            fallProgress = 0f;
            lockTimer += deltaSeconds * (softDrop ? SoftLockFactor : 1f);
            if (lockTimer >= LockSeconds)
            {
                Lock();
            }

            return;
        }

        Resting = false;
        fallProgress += speed * deltaSeconds;
        while (fallProgress >= 1f)
        {
            if (!CanOccupy(PieceColumn, PieceRow + 1, PieceOrientation))
            {
                fallProgress = 0f;
                Resting = true;
                return;
            }

            PieceRow++;
            fallProgress -= 1f;
            if (softDrop)
            {
                Score++;
            }
        }
    }

    private void StepSettling(float deltaSeconds)
    {
        var moving = false;
        for (var cell = 0; cell < Cells; cell++)
        {
            if (offset[cell] <= 0f)
            {
                continue;
            }

            velocity[cell] = MathF.Min(MaxSettleSpeed, velocity[cell] + SettleGravity * deltaSeconds);
            offset[cell] -= velocity[cell] * deltaSeconds;
            if (offset[cell] > 0f)
            {
                moving = true;
                continue;
            }

            offset[cell] = 0f;
            velocity[cell] = 0f;
            squash[cell] = 1f;
            landedCells[LandedCount++] = cell;
        }

        if (moving)
        {
            return;
        }

        var popped = GloopRules.FindPops(grid, pop, group, seen, out var colorMask, out var groupBonus);
        if (popped > 0)
        {
            BeginPop(popped, colorMask, groupBonus);
            return;
        }

        if (Chain > 0)
        {
            ChainEnded = Chain;
            if (GloopRules.IsClear(grid))
            {
                AllClearNow = true;
                Score += GloopRules.AllClearBonus;
                SendGarbage(GloopRules.AllClearGarbage);
            }
        }

        Chain = 0;
        if (Versus && Incoming > 0 && !garbageDropped)
        {
            garbageDropped = true;
            DropGarbage();
            return;
        }

        Spawn();
    }

    private void BeginPop(int popped, int colorMask, int groupBonus)
    {
        Chain++;
        BestChain = Math.Max(BestChain, Chain);
        var gain = GloopRules.StepScore(popped, Chain, GloopRules.CountColors(colorMask), groupBonus);
        Score += gain;
        Popped += popped;
        PopChain = Chain;
        PopGain = gain;
        PoppedCount = 0;
        for (var cell = 0; cell < Cells; cell++)
        {
            if (!pop[cell])
            {
                continue;
            }

            poppedCells[PoppedCount] = cell;
            poppedColors[PoppedCount] = grid[cell];
            PoppedCount++;
        }

        popCount = PoppedCount;

        if (Versus)
        {
            var total = gain + garbageRemainder;
            garbageRemainder = total % GloopRules.NuisanceRate;
            SendGarbage(total / GloopRules.NuisanceRate);
        }

        popTimer = 0f;
        Phase = GloopPhase.Popping;
    }

    private void StepPopping(float deltaSeconds)
    {
        popTimer += deltaSeconds;
        if (popTimer < PopSeconds)
        {
            return;
        }

        GloopRules.ClearPopped(grid, pop);
        Array.Clear(pop);
        ClearedCount = popCount;
        ApplyCollapse();
        Phase = GloopPhase.Settling;
    }

    private void ApplyCollapse()
    {
        GloopRules.Collapse(grid, fall);
        for (var cell = 0; cell < Cells; cell++)
        {
            if (fall[cell] == 0)
            {
                continue;
            }

            offset[cell] = fall[cell];
            velocity[cell] = 0f;
        }
    }

    private void SendGarbage(int count)
    {
        if (!Versus || count <= 0)
        {
            return;
        }

        var cancelled = Math.Min(Incoming, count);
        Incoming -= cancelled;
        count -= cancelled;
        Outgoing += count;
        Sent += count;
    }

    private void DropGarbage()
    {
        var count = Math.Min(Incoming, GloopRules.MaxGarbageDrop);
        Incoming -= count;
        GarbageDropped = count;
        GloopRules.PlanGarbage(count, ref garbageRandom, perColumn);
        for (var column = 0; column < GloopRules.Columns; column++)
        {
            var top = GloopRules.Rows - 1 - GloopRules.Height(grid, column);
            for (var stacked = 0; stacked < perColumn[column]; stacked++)
            {
                var row = top - stacked;
                if (row < 0)
                {
                    break;
                }

                var cell = GloopRules.Index(column, row);
                grid[cell] = GloopRules.Rock;
                offset[cell] = row + RockDropHeight + column * RockColumnStagger;
                velocity[cell] = 0f;
            }
        }

        Phase = GloopPhase.Settling;
    }

    private void Lock()
    {
        Place(PieceColumn, PieceRow, PivotColor);
        Place(PieceColumn + GloopRules.SatelliteColumn(PieceOrientation),
            PieceRow + GloopRules.SatelliteRow(PieceOrientation), SatelliteColor);
        Pieces++;
        LockedNow = true;
        Chain = 0;
        garbageDropped = false;
        ApplyCollapse();
        Phase = GloopPhase.Settling;
    }

    private void Place(int column, int row, byte color)
    {
        if (row < 0 || column < 0 || column >= GloopRules.Columns)
        {
            return;
        }

        var cell = GloopRules.Index(column, row);
        grid[cell] = color;
        squash[cell] = 1f;
    }

    private void Spawn()
    {
        PivotColor = queue[0];
        SatelliteColor = queue[1];
        for (var slot = 2; slot < queue.Length; slot++)
        {
            queue[slot - 2] = queue[slot];
        }

        queue[^2] = RandomColor();
        queue[^1] = RandomColor();
        PieceColumn = GloopRules.SpawnColumn;
        PieceRow = GloopRules.SpawnRow;
        PieceOrientation = 0;
        fallProgress = 0f;
        lockTimer = 0f;
        lockResets = 0;
        Resting = false;
        Spawns++;
        if (GloopRules.SpawnBlocked(grid))
        {
            Phase = GloopPhase.Over;
            DiedNow = true;
            return;
        }

        Phase = GloopPhase.Falling;
    }

    private bool TryTurn(int column, int row, int orientation)
    {
        if (!CanOccupy(column, row, orientation))
        {
            return false;
        }

        PieceColumn = column;
        PieceRow = row;
        PieceOrientation = orientation;
        TouchLock();
        return true;
    }

    private void TouchLock()
    {
        if (!Resting || lockResets >= MaxLockResets)
        {
            return;
        }

        lockTimer = 0f;
        lockResets++;
    }

    private bool CanOccupy(int column, int row, int orientation) =>
        Free(column, row) && Free(column + GloopRules.SatelliteColumn(orientation),
            row + GloopRules.SatelliteRow(orientation));

    private bool Free(int column, int row)
    {
        if (column < 0 || column >= GloopRules.Columns || row >= GloopRules.Rows)
        {
            return false;
        }

        return row < 0 || grid[GloopRules.Index(column, row)] == GloopRules.Empty;
    }

    private byte RandomColor() => (byte)(1 + pieceRandom.Next(GloopRules.ColorCount));
}
