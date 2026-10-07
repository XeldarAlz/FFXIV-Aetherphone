using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games.Claim;

internal enum ClaimMove : byte
{
    None,
    Up,
    Right,
    Down,
    Left,
}

internal enum ClaimDraw : byte
{
    None,
    Fast,
    Slow,
}

internal enum ClaimState : byte
{
    Playing,
    Dying,
    Cleared,
    Over,
}

internal enum ClaimDeath : byte
{
    None,
    Spark,
    Boss,
    Fuse,
}

internal enum ClaimCell : byte
{
    Open,
    Claimed,
    Trail,
}

internal sealed partial class ClaimBoard
{
    public const int Width = 160;
    public const int Height = 240;
    public const int CellCount = Width * Height;
    public const int InteriorCells = (Width - 2) * (Height - 2);
    public const int TargetPercent = 75;
    public const int StartLives = 3;
    public const int MaxLives = 5;
    public const int PointsDivisor = 8;
    public const int ClearBonusPerLevel = 1000;
    public const int ClearBonusPerPercent = 250;
    public const int ExtraLifeEvery = 3;
    public const float FixedStep = 1f / 120f;
    public const float BorderSpeed = 52f;
    public const float FastSpeed = 50f;
    public const float SlowSpeed = 25f;
    public const float FuseDelay = 0.3f;
    public const float FuseSpeed = 36f;
    public const float FuseSnuffSeconds = 0.8f;
    public const float DyingSeconds = 1.3f;
    public const float ClearSeconds = 2.6f;
    public const float ShieldSeconds = 1.8f;
    public const float ComboWindowSeconds = 6f;
    public const ushort Unrevealed = ushort.MaxValue;
    private const float MaxCatchUpSeconds = 0.1f;
    private static readonly int[] DirectionX = { 0, 0, 1, 0, -1 };
    private static readonly int[] DirectionY = { 0, -1, 0, 1, 0 };

    private readonly ClaimCell[] cells = new ClaimCell[CellCount];
    private readonly bool[] fresh = new bool[CellCount];
    private readonly ushort[] reveal = new ushort[CellCount];
    private readonly int[] visit = new int[CellCount];
    private readonly int[] queue = new int[CellCount];
    private readonly int[] trail = new int[CellCount];
    private GameRandom random;
    private FixedStepClock clock = new(FixedStep, MaxCatchUpSeconds);
    private ComboMeter combo = new(ComboWindowSeconds);
    private int visitStamp;
    private int trailCount;
    private int trailStart = -1;
    private int playerCell;
    private float moveTravel;
    private bool blocked;
    private bool moving;
    private ClaimMove lastMove;
    private float stillSeconds;
    private float movingSeconds;
    private float shieldLeft;
    private float stateLeft;

    public ClaimState State { get; private set; }

    public int Level { get; private set; }

    public int Lives { get; private set; }

    public int Score { get; private set; }

    public int ClaimedInterior { get; private set; }

    public int Claims { get; private set; }

    public int SlowLines { get; private set; }

    public int BestClaimCells { get; private set; }

    public float PlaySeconds { get; private set; }

    public uint MosaicSeed { get; private set; }

    public int ClaimVersion { get; private set; }

    public int RevealDepth { get; private set; }

    public bool LineSlow { get; private set; }

    public bool FuseLit { get; private set; }

    public float FuseTravel { get; private set; }

    public int ClaimedThisStep { get; private set; }

    public int PointsThisStep { get; private set; }

    public bool ClaimSlowThisStep { get; private set; }

    public Vector2 ClaimCenterThisStep { get; private set; }

    public bool ComboTierUpThisStep { get; private set; }

    public ClaimDeath DeathThisStep { get; private set; }

    public Vector2 DeathPosition { get; private set; }

    public bool ClearedThisStep { get; private set; }

    public int ClearBonusThisStep { get; private set; }

    public bool LevelStartedThisStep { get; private set; }

    public bool LifeGainedThisStep { get; private set; }

    public bool FuseLitThisStep { get; private set; }

    public bool FuseOutThisStep { get; private set; }

    public bool LineStartedThisStep { get; private set; }

    public bool RespawnedThisStep { get; private set; }

    public ComboMeter Combo => combo;

    public ReadOnlySpan<ClaimCell> Cells => cells;

    public ReadOnlySpan<int> Trail => new(trail, 0, trailCount);

    public int TrailStart => trailStart;

    public bool Drawing => trailCount > 0;

    public int PlayerCell => playerCell;

    public Vector2 PlayerPosition => CellCenter(playerCell);

    public bool Moving => moving;

    public bool Shielded => shieldLeft > 0f;

    public float ShieldLeft => shieldLeft;

    public float StateLeft => stateLeft;

    public int Percent => ClaimedInterior * 100 / InteriorCells;

    public float PercentExact => ClaimedInterior * 100f / InteriorCells;

    public Vector2 FusePosition => TrailPoint(FuseTravel);

    public static int Index(int column, int row) => row * Width + column;

    public static int ColumnOf(int cell) => cell % Width;

    public static int RowOf(int cell) => cell / Width;

    public static Vector2 CellCenter(int cell) => new(ColumnOf(cell) + 0.5f, RowOf(cell) + 0.5f);

    public static int PercentOf(int cells) => cells * 100 / InteriorCells;

    public ClaimCell CellAt(int column, int row) => cells[Index(column, row)];

    public bool IsFresh(int cell) => fresh[cell];

    public ushort RevealAt(int cell) => reveal[cell];

    public void Reset(GameRandom seededRandom)
    {
        random = seededRandom;
        clock.Reset();
        Level = 1;
        Lives = StartLives;
        Score = 0;
        Claims = 0;
        SlowLines = 0;
        BestClaimCells = 0;
        PlaySeconds = 0f;
        ClaimVersion = 0;
        ClearEvents();
        SetupLevel();
    }

    public void Step(float deltaSeconds, ClaimMove move, ClaimDraw draw)
    {
        ClearEvents();
        if (deltaSeconds <= 0f || State == ClaimState.Over)
        {
            return;
        }

        var ticks = clock.Advance(deltaSeconds);
        for (var tick = 0; tick < ticks && State != ClaimState.Over; tick++)
        {
            Tick(FixedStep, move, draw);
        }
    }

    public bool IsWalkable(int cell)
    {
        if (cell < 0 || cells[cell] != ClaimCell.Claimed)
        {
            return false;
        }

        var column = ColumnOf(cell);
        var row = RowOf(cell);
        for (var offsetY = -1; offsetY <= 1; offsetY++)
        {
            var neighbourRow = row + offsetY;
            if ((uint)neighbourRow >= Height)
            {
                continue;
            }

            for (var offsetX = -1; offsetX <= 1; offsetX++)
            {
                var neighbourColumn = column + offsetX;
                if ((uint)neighbourColumn >= Width)
                {
                    continue;
                }

                if (cells[Index(neighbourColumn, neighbourRow)] != ClaimCell.Claimed)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static int Neighbour(int cell, ClaimMove move)
    {
        var column = ColumnOf(cell) + DirectionX[(int)move];
        var row = RowOf(cell) + DirectionY[(int)move];
        return (uint)column < Width && (uint)row < Height ? Index(column, row) : -1;
    }

    public static ClaimMove Opposite(ClaimMove move) => move switch
    {
        ClaimMove.Up => ClaimMove.Down,
        ClaimMove.Down => ClaimMove.Up,
        ClaimMove.Left => ClaimMove.Right,
        ClaimMove.Right => ClaimMove.Left,
        _ => ClaimMove.None,
    };

    public static ClaimMove TurnRight(ClaimMove move) => move switch
    {
        ClaimMove.Up => ClaimMove.Right,
        ClaimMove.Right => ClaimMove.Down,
        ClaimMove.Down => ClaimMove.Left,
        ClaimMove.Left => ClaimMove.Up,
        _ => ClaimMove.None,
    };

    public static ClaimMove TurnLeft(ClaimMove move) => Opposite(TurnRight(move));

    internal void PlacePlayer(int column, int row)
    {
        playerCell = Index(column, row);
        trailCount = 0;
        trailStart = -1;
        moveTravel = 0f;
        blocked = false;
    }

    internal void EndShield()
    {
        shieldLeft = 0f;
    }

    private void Tick(float deltaSeconds, ClaimMove move, ClaimDraw draw)
    {
        switch (State)
        {
            case ClaimState.Dying:
                MoveBoss(deltaSeconds);
                stateLeft -= deltaSeconds;
                if (stateLeft <= 0f)
                {
                    Respawn();
                }

                return;
            case ClaimState.Cleared:
                stateLeft -= deltaSeconds;
                if (stateLeft <= 0f)
                {
                    Level++;
                    SetupLevel();
                }

                return;
            case ClaimState.Over:
                return;
        }

        PlaySeconds += deltaSeconds;
        combo.Update(deltaSeconds);
        shieldLeft = MathF.Max(0f, shieldLeft - deltaSeconds);
        MovePlayer(deltaSeconds, move, draw);
        if (State != ClaimState.Playing)
        {
            return;
        }

        UpdateFuse(deltaSeconds);
        if (State != ClaimState.Playing)
        {
            return;
        }

        MoveBoss(deltaSeconds);
        if (Drawing && BossTouchesLine(out var contact))
        {
            Die(ClaimDeath.Boss, contact);
            return;
        }

        MoveSparks(deltaSeconds);
        if (!Drawing && shieldLeft <= 0f && SparkTouchesPlayer())
        {
            Die(ClaimDeath.Spark, PlayerPosition);
        }
    }

    private void MovePlayer(float deltaSeconds, ClaimMove move, ClaimDraw draw)
    {
        if (move == ClaimMove.None || (Drawing && draw == ClaimDraw.None))
        {
            moving = false;
            moveTravel = 0f;
            blocked = false;
            lastMove = move;
            return;
        }

        if (move != lastMove)
        {
            lastMove = move;
            blocked = false;
        }

        if (blocked)
        {
            moving = false;
            return;
        }

        var speed = Drawing ? (draw == ClaimDraw.Slow ? SlowSpeed : FastSpeed) : BorderSpeed;
        moveTravel += speed * deltaSeconds;
        moving = true;
        while (moveTravel >= 1f)
        {
            moveTravel -= 1f;
            var wasDrawing = Drawing;
            if (!TryStep(move, draw))
            {
                blocked = true;
                moving = false;
                moveTravel = 0f;
                return;
            }

            if (wasDrawing && !Drawing)
            {
                moveTravel = 0f;
                return;
            }
        }
    }

    private bool TryStep(ClaimMove move, ClaimDraw draw)
    {
        var target = Neighbour(playerCell, move);
        if (target < 0)
        {
            return false;
        }

        var state = cells[target];
        if (!Drawing)
        {
            if (state == ClaimCell.Claimed)
            {
                if (!IsWalkable(target))
                {
                    return false;
                }

                playerCell = target;
                return true;
            }

            if (state != ClaimCell.Open || draw == ClaimDraw.None)
            {
                return false;
            }

            trailStart = playerCell;
            LineSlow = draw == ClaimDraw.Slow;
            stillSeconds = 0f;
            movingSeconds = 0f;
            FuseLit = false;
            FuseTravel = 0f;
            LineStartedThisStep = true;
            PushTrail(target);
            return true;
        }

        if (draw == ClaimDraw.None)
        {
            return false;
        }

        if (state == ClaimCell.Claimed)
        {
            if (draw == ClaimDraw.Fast)
            {
                LineSlow = false;
            }

            playerCell = target;
            Complete();
            return true;
        }

        if (state != ClaimCell.Open || TouchesTrail(target))
        {
            return false;
        }

        if (draw == ClaimDraw.Fast)
        {
            LineSlow = false;
        }

        PushTrail(target);
        return true;
    }

    private void PushTrail(int cell)
    {
        cells[cell] = ClaimCell.Trail;
        trail[trailCount++] = cell;
        playerCell = cell;
    }

    private bool TouchesTrail(int target)
    {
        for (var move = ClaimMove.Up; move <= ClaimMove.Left; move++)
        {
            var neighbour = Neighbour(target, move);
            if (neighbour >= 0 && neighbour != playerCell && cells[neighbour] == ClaimCell.Trail)
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateFuse(float deltaSeconds)
    {
        if (!Drawing)
        {
            FuseLit = false;
            FuseTravel = 0f;
            return;
        }

        if (moving)
        {
            stillSeconds = 0f;
            if (!FuseLit)
            {
                return;
            }

            movingSeconds += deltaSeconds;
            if (movingSeconds < FuseSnuffSeconds)
            {
                return;
            }

            FuseLit = false;
            FuseTravel = 0f;
            FuseOutThisStep = true;
            return;
        }

        movingSeconds = 0f;
        stillSeconds += deltaSeconds;
        if (stillSeconds < FuseDelay)
        {
            return;
        }

        if (!FuseLit)
        {
            FuseLit = true;
            FuseTravel = 0f;
            FuseLitThisStep = true;
        }

        FuseTravel += FuseSpeed * deltaSeconds;
        if (FuseTravel >= trailCount)
        {
            FuseTravel = trailCount;
            Die(ClaimDeath.Fuse, PlayerPosition);
        }
    }

    private Vector2 TrailPoint(float travel)
    {
        if (trailCount == 0 || trailStart < 0)
        {
            return PlayerPosition;
        }

        var clamped = Math.Clamp(travel, 0f, trailCount);
        var index = (int)clamped;
        var from = index == 0 ? CellCenter(trailStart) : CellCenter(trail[index - 1]);
        if (index >= trailCount)
        {
            return from;
        }

        return Vector2.Lerp(from, CellCenter(trail[index]), clamped - index);
    }

    private void Complete()
    {
        var anchor = BossAnchorCell();
        visitStamp++;
        if (anchor >= 0)
        {
            MarkRegion(anchor);
        }

        Array.Clear(fresh);
        var head = 0;
        var tail = 0;
        var claimed = 0;
        var sum = Vector2.Zero;
        for (var index = 0; index < trailCount; index++)
        {
            var cell = trail[index];
            cells[cell] = ClaimCell.Claimed;
            fresh[cell] = true;
            reveal[cell] = 0;
            queue[tail++] = cell;
            sum += CellCenter(cell);
            claimed++;
        }

        for (var cell = 0; cell < CellCount; cell++)
        {
            if (cells[cell] != ClaimCell.Open || visit[cell] == visitStamp)
            {
                continue;
            }

            cells[cell] = ClaimCell.Claimed;
            fresh[cell] = true;
            reveal[cell] = Unrevealed;
            sum += CellCenter(cell);
            claimed++;
        }

        RevealDepth = SpreadReveal(head, tail);
        trailCount = 0;
        FuseLit = false;
        FuseTravel = 0f;
        ClaimVersion++;
        Claims++;
        var slow = LineSlow;
        if (slow)
        {
            SlowLines++;
        }

        var multiplierBefore = combo.Multiplier;
        var multiplier = combo.Hit();
        ComboTierUpThisStep |= multiplier > multiplierBefore;
        var points = (claimed * (slow ? 2 : 1) * multiplier + PointsDivisor / 2) / PointsDivisor;
        Score += points;
        ClaimedInterior += claimed;
        BestClaimCells = Math.Max(BestClaimCells, claimed);
        ClaimedThisStep += claimed;
        PointsThisStep += points;
        ClaimSlowThisStep = slow;
        ClaimCenterThisStep = claimed > 0 ? sum / claimed : PlayerPosition;
        LineSlow = false;
        if (!IsWalkable(playerCell))
        {
            playerCell = NearestWalkable(playerCell);
        }

        SettleBoss(anchor);
        SettleSparks();
        if (ClaimedInterior * 100 < TargetPercent * InteriorCells)
        {
            return;
        }

        var bonus = ClearBonusPerLevel * Level + ClearBonusPerPercent * (Percent - TargetPercent);
        Score += bonus;
        ClearBonusThisStep = bonus;
        ClearedThisStep = true;
        State = ClaimState.Cleared;
        stateLeft = ClearSeconds;
        if (Level % ExtraLifeEvery == 0 && Lives < MaxLives)
        {
            Lives++;
            LifeGainedThisStep = true;
        }
    }

    private int SpreadReveal(int head, int tail)
    {
        var deepest = 0;
        while (head < tail)
        {
            var cell = queue[head++];
            var next = reveal[cell] + 1;
            for (var move = ClaimMove.Up; move <= ClaimMove.Left; move++)
            {
                var neighbour = Neighbour(cell, move);
                if (neighbour < 0 || !fresh[neighbour] || reveal[neighbour] != Unrevealed)
                {
                    continue;
                }

                reveal[neighbour] = (ushort)Math.Min(next, Unrevealed - 1);
                deepest = Math.Max(deepest, next);
                queue[tail++] = neighbour;
            }
        }

        for (var cell = 0; cell < CellCount; cell++)
        {
            if (fresh[cell] && reveal[cell] == Unrevealed)
            {
                reveal[cell] = 0;
            }
        }

        return deepest;
    }

    private int MarkRegion(int start)
    {
        var head = 0;
        var tail = 0;
        visit[start] = visitStamp;
        queue[tail++] = start;
        while (head < tail)
        {
            var cell = queue[head++];
            for (var move = ClaimMove.Up; move <= ClaimMove.Left; move++)
            {
                var neighbour = Neighbour(cell, move);
                if (neighbour < 0 || cells[neighbour] != ClaimCell.Open || visit[neighbour] == visitStamp)
                {
                    continue;
                }

                visit[neighbour] = visitStamp;
                queue[tail++] = neighbour;
            }
        }

        return tail;
    }

    private int LargestOpenRegion()
    {
        visitStamp++;
        var largest = -1;
        var largestSize = 0;
        for (var cell = 0; cell < CellCount; cell++)
        {
            if (cells[cell] != ClaimCell.Open || visit[cell] == visitStamp)
            {
                continue;
            }

            var size = MarkRegion(cell);
            if (size <= largestSize)
            {
                continue;
            }

            largestSize = size;
            largest = cell;
        }

        return largest;
    }

    private int NearestWalkable(int start)
    {
        visitStamp++;
        var head = 0;
        var tail = 0;
        visit[start] = visitStamp;
        queue[tail++] = start;
        while (head < tail)
        {
            var cell = queue[head++];
            if (IsWalkable(cell))
            {
                return cell;
            }

            for (var move = ClaimMove.Up; move <= ClaimMove.Left; move++)
            {
                var neighbour = Neighbour(cell, move);
                if (neighbour < 0 || visit[neighbour] == visitStamp)
                {
                    continue;
                }

                visit[neighbour] = visitStamp;
                queue[tail++] = neighbour;
            }
        }

        return start;
    }

    private int FarthestWalkable(int start)
    {
        visitStamp++;
        var head = 0;
        var tail = 0;
        visit[start] = visitStamp;
        queue[tail++] = start;
        var farthest = start;
        while (head < tail)
        {
            var cell = queue[head++];
            farthest = cell;
            for (var move = ClaimMove.Up; move <= ClaimMove.Left; move++)
            {
                var neighbour = Neighbour(cell, move);
                if (neighbour < 0 || visit[neighbour] == visitStamp || !IsWalkable(neighbour))
                {
                    continue;
                }

                visit[neighbour] = visitStamp;
                queue[tail++] = neighbour;
            }
        }

        return farthest;
    }

    private void Die(ClaimDeath reason, Vector2 position)
    {
        DeathThisStep = reason;
        DeathPosition = position;
        State = ClaimState.Dying;
        stateLeft = DyingSeconds;
        Lives = Math.Max(0, Lives - 1);
        combo.Reset();
        moving = false;
        moveTravel = 0f;
    }

    private void Respawn()
    {
        if (Lives <= 0)
        {
            State = ClaimState.Over;
            return;
        }

        if (Drawing)
        {
            for (var index = 0; index < trailCount; index++)
            {
                cells[trail[index]] = ClaimCell.Open;
            }

            playerCell = trailStart;
        }

        trailCount = 0;
        FuseLit = false;
        FuseTravel = 0f;
        LineSlow = false;
        blocked = false;
        moveTravel = 0f;
        if (!IsWalkable(playerCell))
        {
            playerCell = NearestWalkable(playerCell);
        }

        ScatterSparks();
        shieldLeft = ShieldSeconds;
        State = ClaimState.Playing;
        RespawnedThisStep = true;
    }

    private void SetupLevel()
    {
        Array.Fill(cells, ClaimCell.Open);
        Array.Clear(fresh);
        for (var column = 0; column < Width; column++)
        {
            cells[Index(column, 0)] = ClaimCell.Claimed;
            cells[Index(column, Height - 1)] = ClaimCell.Claimed;
        }

        for (var row = 0; row < Height; row++)
        {
            cells[Index(0, row)] = ClaimCell.Claimed;
            cells[Index(Width - 1, row)] = ClaimCell.Claimed;
        }

        ClaimedInterior = 0;
        RevealDepth = 0;
        ClaimVersion++;
        trailCount = 0;
        trailStart = -1;
        FuseLit = false;
        FuseTravel = 0f;
        LineSlow = false;
        moving = false;
        blocked = false;
        moveTravel = 0f;
        lastMove = ClaimMove.None;
        playerCell = Index(Width / 2, Height - 1);
        MosaicSeed = random.NextUInt();
        SpawnBoss();
        SpawnSparks();
        combo.Reset();
        shieldLeft = ShieldSeconds;
        stateLeft = 0f;
        State = ClaimState.Playing;
        LevelStartedThisStep = true;
    }

    private void ClearEvents()
    {
        ClaimedThisStep = 0;
        PointsThisStep = 0;
        ClaimSlowThisStep = false;
        ComboTierUpThisStep = false;
        DeathThisStep = ClaimDeath.None;
        ClearedThisStep = false;
        ClearBonusThisStep = 0;
        LevelStartedThisStep = false;
        LifeGainedThisStep = false;
        FuseLitThisStep = false;
        FuseOutThisStep = false;
        LineStartedThisStep = false;
        RespawnedThisStep = false;
    }
}
