namespace Aetherphone.Apps.Games.BubbleShooter;

internal enum BubbleKind
{
    Normal,
    Bomb,
    Rainbow,
}

internal struct FallingBubble
{
    public Vector2 Position;
    public Vector2 Velocity;
    public float Spin;
    public int Color;
}

internal sealed class BubbleBoard
{
    public const int Columns = 8;
    public const int RowCapacity = 20;
    public const int MaxColors = 6;
    public const int MinClusterSize = 3;
    private const int CellCapacity = RowCapacity * Columns;
    private const int FallerCapacity = 96;
    private const int StartRows = 4;
    private const int TracePointCapacity = 96;
    private const float Speed = 1.75f;
    private const float FlightStepFactor = 0.25f;
    private const float RowSpacingFactor = 0.866f;
    private const float ContactFactor = 0.96f;
    private const float LauncherClearance = 1.15f;
    private const float CeilingInset = 0.032f;
    private const float DescentSpeed = 4.2f;
    private const float FallGravity = 3.4f;
    private const float FallWallDamping = 0.55f;
    private const float ClusterRunChance = 0.45f;
    private const float ChargePerPop = 0.035f;
    private const float ChargePerDrop = 0.05f;
    private const float ComboStep = 0.25f;
    private const int ComboCap = 8;
    private const int DangerRows = 4;
    public readonly float Diameter = 1f / (Columns + 0.5f);
    private readonly int[] cells = new int[CellCapacity];
    private readonly int[] queue = new int[CellCapacity];
    private readonly bool[] connected = new bool[CellCapacity];
    private readonly bool[] clusterMark = new bool[CellCapacity];
    private readonly bool[] blastMark = new bool[CellCapacity];
    private readonly Vector2[] popPositions = new Vector2[CellCapacity];
    private readonly int[] popColors = new int[CellCapacity];
    private readonly FallingBubble[] fallers = new FallingBubble[FallerCapacity];
    private readonly Random random = new();
    private float firstRowY;
    private float descentProgress;
    private float flightRemainder;
    private Vector2 flyHeading;
    private int rowShift;
    private int rowLimit = RowCapacity;
    private int shotsFired;
    private int rowsAdded;
    private int poppedThisShot;
    private int droppedThisShot;
    public float FieldHeight { get; private set; } = 1.7f;
    public float Radius => Diameter * 0.5f;
    public float RowSpacing => Diameter * RowSpacingFactor;
    private float FlightStep => Radius * FlightStepFactor;
    public int RowLimit => rowLimit;
    public int CurrentColor { get; private set; }
    public int NextColor { get; private set; }
    public BubbleKind CurrentKind { get; private set; }
    public BubbleKind NextKind { get; private set; }
    public bool Flying { get; private set; }
    public Vector2 FlyPosition { get; private set; }
    public int FlyColor { get; private set; }
    public BubbleKind FlyKind { get; private set; }
    public int Score { get; private set; }
    public bool GameOver { get; private set; }
    public int PopCount { get; private set; }
    public int FallerCount { get; private set; }
    public int Combo { get; private set; }
    public float Charge { get; private set; }
    public float DangerLevel { get; private set; }
    public int ShotsUntilRow { get; private set; }
    public int RowInterval { get; private set; } = 7;
    public int ActiveColors { get; private set; } = 4;
    public int LastShotScore { get; private set; }
    public bool BlastedThisShot { get; private set; }
    public Vector2 BlastPosition { get; private set; }
    public float Multiplier => 1f + Math.Min(Math.Max(Combo - 1, 0), ComboCap) * ComboStep;
    public float DescentOffset => descentProgress * descentProgress * RowSpacing;
    public Vector2 LauncherPosition => new(0.5f, FieldHeight - Radius - 0.03f);
    public float DangerY => firstRowY + (rowLimit - 1) * RowSpacing + Radius;
    public float CeilingY => firstRowY - Radius;
    public int ColorAt(int column, int row) => cells[row * Columns + column];
    public Vector2 PopPosition(int index) => popPositions[index];
    public int PopColor(int index) => popColors[index];
    public ref readonly FallingBubble Faller(int index) => ref fallers[index];

    public Vector2 CellCenter(int column, int row)
    {
        var x = Radius + column * Diameter + (Parity(row) == 1 ? Radius : 0f);
        return new Vector2(x, firstRowY + row * RowSpacing);
    }

    private int Parity(int row) => (row + rowShift) & 1;

    public void Reset(float fieldHeight)
    {
        for (var index = 0; index < cells.Length; index++)
        {
            cells[index] = -1;
        }

        rowShift = 0;
        ActiveColors = 4;
        RowInterval = 7;
        ApplyFieldHeight(fieldHeight);
        for (var row = 0; row < StartRows; row++)
        {
            for (var column = 0; column < Columns; column++)
            {
                cells[row * Columns + column] = SeedColor(column, row);
            }
        }

        shotsFired = 0;
        rowsAdded = 0;
        ShotsUntilRow = RowInterval;
        Flying = false;
        GameOver = false;
        Score = 0;
        Combo = 0;
        Charge = 0f;
        LastShotScore = 0;
        PopCount = 0;
        FallerCount = 0;
        poppedThisShot = 0;
        droppedThisShot = 0;
        descentProgress = 0f;
        flightRemainder = 0f;
        BlastedThisShot = false;
        CurrentKind = BubbleKind.Normal;
        NextKind = BubbleKind.Normal;
        CurrentColor = PickPlayableColor();
        NextColor = PickPlayableColor();
        UpdateDangerLevel();
    }

    private int SeedColor(int column, int row)
    {
        if (column > 0 && random.NextDouble() < ClusterRunChance)
        {
            return cells[row * Columns + column - 1];
        }

        return random.Next(ActiveColors);
    }

    public void SetFieldHeight(float fieldHeight)
    {
        if (MathF.Abs(fieldHeight - FieldHeight) < 0.0005f)
        {
            return;
        }

        ApplyFieldHeight(fieldHeight);
    }

    private void ApplyFieldHeight(float fieldHeight)
    {
        FieldHeight = fieldHeight;
        firstRowY = Radius + CeilingInset;
        var launcherY = fieldHeight - Radius - 0.03f;
        var usable = launcherY - Diameter * LauncherClearance - Radius - firstRowY;
        var limit = 1 + (int)MathF.Floor(usable / RowSpacing);
        rowLimit = Math.Clamp(limit, StartRows + 2, RowCapacity);
        rowLimit = Math.Max(rowLimit, DeepestOccupiedRow() + 1);
    }

    private int DeepestOccupiedRow()
    {
        for (var index = cells.Length - 1; index >= 0; index--)
        {
            if (cells[index] >= 0)
            {
                return index / Columns;
            }
        }

        return -1;
    }

    public void Fire(Vector2 direction)
    {
        if (Flying || GameOver || direction.Y >= -0.05f)
        {
            return;
        }

        Flying = true;
        FlyColor = CurrentColor;
        FlyKind = CurrentKind;
        FlyPosition = LauncherPosition;
        flyHeading = Vector2.Normalize(direction);
        flightRemainder = 0f;
        CurrentColor = NextColor;
        CurrentKind = NextKind;
        NextColor = PickPlayableColor();
        NextKind = BubbleKind.Normal;
    }

    public bool Swap()
    {
        if (Flying || GameOver)
        {
            return false;
        }

        (CurrentColor, NextColor) = (NextColor, CurrentColor);
        (CurrentKind, NextKind) = (NextKind, CurrentKind);
        return true;
    }

    public void Update(float deltaSeconds)
    {
        PopCount = 0;
        LastShotScore = 0;
        BlastedThisShot = false;
        poppedThisShot = 0;
        droppedThisShot = 0;
        UpdateFallers(deltaSeconds);
        if (descentProgress > 0f)
        {
            descentProgress = MathF.Max(0f, descentProgress - deltaSeconds * DescentSpeed);
        }

        if (!Flying || GameOver)
        {
            return;
        }

        flightRemainder += Speed * deltaSeconds;
        var steps = (int)(flightRemainder / FlightStep);
        if (steps <= 0)
        {
            return;
        }

        flightRemainder -= steps * FlightStep;
        var position = FlyPosition;
        var heading = flyHeading;
        var landed = Travel(ref position, ref heading, steps, out var landingCell);
        FlyPosition = position;
        flyHeading = heading;
        if (!landed)
        {
            return;
        }

        Flying = false;
        Land(landingCell);
    }

    private bool Travel(ref Vector2 position, ref Vector2 heading, int steps, out int landingCell)
    {
        landingCell = -1;
        for (var index = 0; index < steps; index++)
        {
            position += heading * FlightStep;
            if (position.X < Radius)
            {
                position.X = Radius;
                heading.X = MathF.Abs(heading.X);
            }
            else if (position.X > 1f - Radius)
            {
                position.X = 1f - Radius;
                heading.X = -MathF.Abs(heading.X);
            }

            if (position.Y <= firstRowY)
            {
                position.Y = firstRowY;
                landingCell = FindLandingCell(position);
                return true;
            }

            if (TouchesBubble(position))
            {
                landingCell = FindLandingCell(position);
                return true;
            }
        }

        return false;
    }

    public int TracePath(Vector2 direction, Span<Vector2> points, out int landingCell)
    {
        landingCell = -1;
        if (direction.Y >= -0.05f)
        {
            return 0;
        }

        var position = LauncherPosition;
        var heading = Vector2.Normalize(direction);
        var count = 0;
        for (var iteration = 0; iteration < TracePointCapacity * 3; iteration++)
        {
            if (Travel(ref position, ref heading, 2, out landingCell))
            {
                if (count < points.Length)
                {
                    points[count++] = position;
                }

                return count;
            }

            if (count < points.Length && iteration % 2 == 0)
            {
                points[count++] = position;
            }
        }

        return count;
    }

    private bool TouchesBubble(Vector2 position)
    {
        var threshold = Diameter * ContactFactor;
        var thresholdSquared = threshold * threshold;
        var centerRow = (int)MathF.Round((position.Y - firstRowY) / RowSpacing);
        var firstRow = Math.Max(0, centerRow - 2);
        var lastRow = Math.Min(rowLimit - 1, centerRow + 2);
        for (var row = firstRow; row <= lastRow; row++)
        {
            for (var column = 0; column < Columns; column++)
            {
                if (cells[row * Columns + column] < 0)
                {
                    continue;
                }

                if (Vector2.DistanceSquared(CellCenter(column, row), position) < thresholdSquared)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private int FindLandingCell(Vector2 position)
    {
        var bestCell = -1;
        var bestDistance = float.MaxValue;
        for (var row = 0; row < rowLimit; row++)
        {
            for (var column = 0; column < Columns; column++)
            {
                var index = row * Columns + column;
                if (cells[index] >= 0 || !IsAttachable(index))
                {
                    continue;
                }

                var distance = Vector2.DistanceSquared(CellCenter(column, row), position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestCell = index;
                }
            }
        }

        return bestCell;
    }

    private bool IsAttachable(int cell)
    {
        if (cell < Columns)
        {
            return true;
        }

        Span<int> neighborCells = stackalloc int[6];
        var count = Neighbors(cell, neighborCells);
        for (var index = 0; index < count; index++)
        {
            if (cells[neighborCells[index]] >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private void Land(int cell)
    {
        if (cell < 0)
        {
            GameOver = true;
            return;
        }

        shotsFired++;
        var cleared = false;
        if (FlyKind == BubbleKind.Bomb)
        {
            cells[cell] = FlyColor;
            BlastPosition = CellCenter(cell % Columns, cell / Columns);
            BlastedThisShot = true;
            Detonate(cell);
            DropFloating();
            cleared = true;
        }
        else
        {
            cells[cell] = FlyKind == BubbleKind.Rainbow ? ResolveRainbowColor(cell) : FlyColor;
            cleared = ResolveCluster(cell);
            if (cleared)
            {
                DropFloating();
            }
        }

        Combo = cleared ? Combo + 1 : 0;
        ScoreShot();
        ChargeUp();
        RefreshLauncherColors();
        ShotsUntilRow--;
        if (ShotsUntilRow <= 0)
        {
            ShiftDown();
        }

        UpdateDifficulty();
        UpdateDangerLevel();
    }

    private void ScoreShot()
    {
        if (poppedThisShot == 0 && droppedThisShot == 0)
        {
            return;
        }

        var popScore = poppedThisShot * 10f * (1f + Math.Max(0, poppedThisShot - MinClusterSize) * 0.25f);
        var dropScore = droppedThisShot * 25f * (1f + droppedThisShot * 0.15f);
        LastShotScore = (int)MathF.Round((popScore + dropScore) * Multiplier);
        Score += LastShotScore;
    }

    private void ChargeUp()
    {
        if (poppedThisShot == 0 && droppedThisShot == 0)
        {
            return;
        }

        Charge = MathF.Min(1f, Charge + poppedThisShot * ChargePerPop + droppedThisShot * ChargePerDrop);
        if (Charge < 1f || NextKind != BubbleKind.Normal)
        {
            return;
        }

        NextKind = rowsAdded % 2 == 0 ? BubbleKind.Rainbow : BubbleKind.Bomb;
        Charge = 0f;
    }

    private void UpdateDifficulty()
    {
        ActiveColors = Score >= 6000 ? 6 : Score >= 2000 ? 5 : 4;
        RowInterval = rowsAdded >= 12 ? 5 : rowsAdded >= 6 ? 6 : 7;
        ShotsUntilRow = Math.Min(ShotsUntilRow, RowInterval);
    }

    private void UpdateDangerLevel()
    {
        var deepest = DeepestOccupiedRow();
        if (deepest < 0)
        {
            DangerLevel = 0f;
            return;
        }

        var reach = deepest + 1 - (rowLimit - DangerRows);
        DangerLevel = Math.Clamp(reach / (float)DangerRows, 0f, 1f);
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

        if (write < MinClusterSize)
        {
            return false;
        }

        for (var index = 0; index < write; index++)
        {
            PopCell(queue[index]);
        }

        return true;
    }

    private int MeasureCluster(int startCell, int color)
    {
        Array.Clear(blastMark, 0, blastMark.Length);
        var read = 0;
        var write = 0;
        queue[write++] = startCell;
        blastMark[startCell] = true;
        Span<int> neighborCells = stackalloc int[6];
        while (read < write)
        {
            var cell = queue[read++];
            var count = Neighbors(cell, neighborCells);
            for (var index = 0; index < count; index++)
            {
                var neighbor = neighborCells[index];
                if (!blastMark[neighbor] && cells[neighbor] == color)
                {
                    blastMark[neighbor] = true;
                    queue[write++] = neighbor;
                }
            }
        }

        return write;
    }

    private int ResolveRainbowColor(int cell)
    {
        Span<int> neighborCells = stackalloc int[6];
        var count = Neighbors(cell, neighborCells);
        var bestColor = -1;
        var bestSize = 0;
        for (var index = 0; index < count; index++)
        {
            var color = cells[neighborCells[index]];
            if (color < 0)
            {
                continue;
            }

            var size = MeasureCluster(neighborCells[index], color);
            if (size > bestSize)
            {
                bestSize = size;
                bestColor = color;
            }
        }

        return bestColor >= 0 ? bestColor : PickPlayableColor();
    }

    private void Detonate(int cell)
    {
        Array.Clear(clusterMark, 0, clusterMark.Length);
        clusterMark[cell] = true;
        ExpandBlast();
        ExpandBlast();
        for (var index = 0; index < cells.Length; index++)
        {
            if (clusterMark[index] && cells[index] >= 0)
            {
                PopCell(index);
            }
        }
    }

    private void ExpandBlast()
    {
        Array.Copy(clusterMark, blastMark, clusterMark.Length);
        Span<int> neighborCells = stackalloc int[6];
        for (var index = 0; index < blastMark.Length; index++)
        {
            if (!blastMark[index])
            {
                continue;
            }

            var count = Neighbors(index, neighborCells);
            for (var neighbor = 0; neighbor < count; neighbor++)
            {
                clusterMark[neighborCells[neighbor]] = true;
            }
        }
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
            if (cells[index] < 0 || connected[index])
            {
                continue;
            }

            SpawnFaller(index);
            cells[index] = -1;
            droppedThisShot++;
        }
    }

    private void PopCell(int cell)
    {
        popPositions[PopCount] = CellCenter(cell % Columns, cell / Columns);
        popColors[PopCount] = cells[cell];
        PopCount++;
        poppedThisShot++;
        cells[cell] = -1;
    }

    private void SpawnFaller(int cell)
    {
        if (FallerCount >= FallerCapacity)
        {
            return;
        }

        ref var faller = ref fallers[FallerCount];
        faller.Position = CellCenter(cell % Columns, cell / Columns);
        faller.Velocity = new Vector2(((float)random.NextDouble() - 0.5f) * 0.35f,
            0.05f + (float)random.NextDouble() * 0.25f);
        faller.Spin = ((float)random.NextDouble() - 0.5f) * 6f;
        faller.Color = cells[cell];
        FallerCount++;
    }

    private void UpdateFallers(float deltaSeconds)
    {
        if (deltaSeconds <= 0f)
        {
            return;
        }

        for (var index = FallerCount - 1; index >= 0; index--)
        {
            ref var faller = ref fallers[index];
            faller.Velocity.Y += FallGravity * deltaSeconds;
            faller.Position += faller.Velocity * deltaSeconds;
            if (faller.Position.X < Radius)
            {
                faller.Position.X = Radius;
                faller.Velocity.X = MathF.Abs(faller.Velocity.X) * FallWallDamping;
            }
            else if (faller.Position.X > 1f - Radius)
            {
                faller.Position.X = 1f - Radius;
                faller.Velocity.X = -MathF.Abs(faller.Velocity.X) * FallWallDamping;
            }

            if (faller.Position.Y - Radius <= FieldHeight)
            {
                continue;
            }

            fallers[index] = fallers[FallerCount - 1];
            FallerCount--;
        }
    }

    private void ShiftDown()
    {
        for (var column = 0; column < Columns; column++)
        {
            if (cells[(rowLimit - 1) * Columns + column] >= 0)
            {
                GameOver = true;
                return;
            }
        }

        for (var row = rowLimit - 1; row >= 1; row--)
        {
            for (var column = 0; column < Columns; column++)
            {
                cells[row * Columns + column] = cells[(row - 1) * Columns + column];
            }
        }

        rowShift++;
        for (var column = 0; column < Columns; column++)
        {
            cells[column] = SeedColor(column, 0);
        }

        rowsAdded++;
        descentProgress = 1f;
        ShotsUntilRow = RowInterval;
    }

    private int Neighbors(int cell, Span<int> output)
    {
        var column = cell % Columns;
        var row = cell / Columns;
        var count = 0;
        count = Add(output, count, column - 1, row);
        count = Add(output, count, column + 1, row);
        if (Parity(row) == 0)
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
        if (column < 0 || column >= Columns || row < 0 || row >= rowLimit)
        {
            return count;
        }

        output[count] = row * Columns + column;
        return count + 1;
    }

    private void RefreshLauncherColors()
    {
        if (!HasColorOnBoard(CurrentColor) && CurrentKind == BubbleKind.Normal)
        {
            CurrentColor = PickPlayableColor();
        }

        if (!HasColorOnBoard(NextColor) && NextKind == BubbleKind.Normal)
        {
            NextColor = PickPlayableColor();
        }
    }

    private bool HasColorOnBoard(int color)
    {
        for (var index = 0; index < cells.Length; index++)
        {
            if (cells[index] == color)
            {
                return true;
            }
        }

        return false;
    }

    private int PickPlayableColor()
    {
        Span<int> present = stackalloc int[MaxColors];
        var count = 0;
        Span<bool> seen = stackalloc bool[MaxColors];
        for (var index = 0; index < cells.Length; index++)
        {
            var color = cells[index];
            if (color < 0 || seen[color])
            {
                continue;
            }

            seen[color] = true;
            present[count++] = color;
        }

        if (count == 0)
        {
            return random.Next(ActiveColors);
        }

        return present[random.Next(count)];
    }
}
