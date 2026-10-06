using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core.Animation;

namespace Aetherphone.Apps.Games.Snake;

internal enum SnakeState : byte
{
    Playing,
    Dying,
    Over,
}

internal enum SnakeDirection : byte
{
    Up,
    Right,
    Down,
    Left,
}

internal enum FruitKind : byte
{
    None,
    Apple,
    Gold,
    Bomb,
}

internal sealed class SnakeBoard
{
    public const int Columns = 13;
    public const int Rows = 22;
    public const int CellCount = Columns * Rows;
    public const int StartLength = 4;
    public const int MinLength = 3;
    public const int ApplePoints = 1;
    public const int GoldPoints = 3;
    public const int BombShrink = 2;
    public const int FruitPerLevel = 4;
    public const float GoldSeconds = 6f;
    public const float BombSeconds = 8f;
    public const float BaseStepSeconds = 0.17f;
    public const float MinStepSeconds = 0.075f;
    public const float SpeedRamp = 0.88f;
    public const float DyingSeconds = 0.3f;
    public const float CrashReach = 0.45f;
    private const float GoldChance = 0.18f;
    private const float BombChance = 0.22f;
    private const int QueueDepth = 2;
    private static readonly int[] StepX = { 0, 1, 0, -1 };
    private static readonly int[] StepY = { -1, 0, 1, 0 };

    private readonly int[] cells = new int[CellCount];
    private readonly SnakeDirection[] queue = new SnakeDirection[QueueDepth];
    private GameRandom random;
    private int head;
    private int queued;
    private int tailPrevious;
    private int growth;
    private float stepSeconds = BaseStepSeconds;
    private float moveTimer;
    private float dyingLeft;

    public SnakeState State { get; private set; }

    public bool Wrap { get; private set; }

    public SnakeDirection Direction { get; private set; }

    public int Length { get; private set; }

    public int Score { get; private set; }

    public int Eaten { get; private set; }

    public float PlaySeconds { get; private set; }

    public int FruitCell { get; private set; }

    public int SpecialCell { get; private set; }

    public FruitKind SpecialKind { get; private set; }

    public float SpecialLeft { get; private set; }

    public FruitKind AteThisStep { get; private set; }

    public bool DiedThisStep { get; private set; }

    public bool LevelledThisStep { get; private set; }

    public bool WrappedThisStep { get; private set; }

    public int Level => 1 + Eaten / FruitPerLevel;

    public float StepSeconds => stepSeconds;

    public float Alpha => Easing.Clamp01(moveTimer / stepSeconds);

    public int HeadCell => cells[head];

    public Vector2 Heading => new(StepX[(int)Direction], StepY[(int)Direction]);

    public Vector2 HeadWorld => SegmentWorld(0);

    public float SpecialFraction => SpecialKind switch
    {
        FruitKind.Gold => SpecialLeft / GoldSeconds,
        FruitKind.Bomb => SpecialLeft / BombSeconds,
        _ => 0f,
    };

    public float CrashReachNow =>
        State == SnakeState.Playing ? 0f : CrashReach * Easing.EaseOutCubic(1f - dyingLeft / DyingSeconds);

    public static int CellIndex(int x, int y) => y * Columns + x;

    public static int CellX(int cell) => cell % Columns;

    public static int CellY(int cell) => cell / Columns;

    public static Vector2 CellCenter(int cell) => new(CellX(cell) + 0.5f, CellY(cell) + 0.5f);

    public int SegmentCell(int segment) => cells[(head - segment + CellCount) % CellCount];

    public Vector2 SegmentWorld(int segment)
    {
        var current = SegmentCell(segment);
        var previous = segment + 1 < Length ? SegmentCell(segment + 1) : tailPrevious;
        var position = Interpolate(previous, current, Alpha);
        return segment == 0 ? position + Heading * CrashReachNow : position;
    }

    public void Reset(GameRandom seededRandom, bool wrap)
    {
        random = seededRandom;
        Wrap = wrap;
        State = SnakeState.Playing;
        Direction = SnakeDirection.Up;
        queued = 0;
        growth = 0;
        stepSeconds = BaseStepSeconds;
        moveTimer = 0f;
        dyingLeft = 0f;
        Score = 0;
        Eaten = 0;
        PlaySeconds = 0f;
        SpecialKind = FruitKind.None;
        SpecialCell = -1;
        SpecialLeft = 0f;
        AteThisStep = FruitKind.None;
        DiedThisStep = false;
        LevelledThisStep = false;
        WrappedThisStep = false;
        Length = StartLength;
        head = StartLength - 1;
        var startColumn = Columns / 2;
        var startRow = Rows * 2 / 3;
        for (var segment = 0; segment < StartLength; segment++)
        {
            cells[StartLength - 1 - segment] = CellIndex(startColumn, startRow + segment);
        }

        tailPrevious = CellIndex(startColumn, startRow + StartLength);
        FruitCell = -1;
        SpawnFruit();
    }

    public void Steer(SnakeDirection direction)
    {
        if (State != SnakeState.Playing)
        {
            return;
        }

        var last = queued > 0 ? queue[queued - 1] : Direction;
        if (direction == last || IsOpposite(direction, last) || queued == QueueDepth)
        {
            return;
        }

        queue[queued++] = direction;
    }

    public void Step(float deltaSeconds)
    {
        AteThisStep = FruitKind.None;
        DiedThisStep = false;
        LevelledThisStep = false;
        WrappedThisStep = false;
        if (deltaSeconds <= 0f || State == SnakeState.Over)
        {
            return;
        }

        if (State == SnakeState.Dying)
        {
            dyingLeft = MathF.Max(0f, dyingLeft - deltaSeconds);
            if (dyingLeft <= 0f)
            {
                State = SnakeState.Over;
            }

            return;
        }

        PlaySeconds += deltaSeconds;
        TickSpecial(deltaSeconds);
        moveTimer += deltaSeconds;
        while (moveTimer >= stepSeconds && State == SnakeState.Playing)
        {
            moveTimer -= stepSeconds;
            Advance();
        }
    }

    public void PlaceFruit(int cell)
    {
        FruitCell = cell;
    }

    public void PlaceSpecial(int cell, FruitKind kind)
    {
        if (kind == FruitKind.None || cell < 0)
        {
            ClearSpecial();
            return;
        }

        SpecialCell = cell;
        SpecialKind = kind;
        SpecialLeft = kind == FruitKind.Gold ? GoldSeconds : BombSeconds;
    }

    private void Advance()
    {
        if (queued > 0)
        {
            Direction = queue[0];
            queue[0] = queue[1];
            queued--;
        }

        var headCell = cells[head];
        var x = CellX(headCell) + StepX[(int)Direction];
        var y = CellY(headCell) + StepY[(int)Direction];
        if (x < 0 || x >= Columns || y < 0 || y >= Rows)
        {
            if (!Wrap)
            {
                Die();
                return;
            }

            x = (x + Columns) % Columns;
            y = (y + Rows) % Rows;
            WrappedThisStep = true;
        }

        var next = CellIndex(x, y);
        var growing = growth > 0;
        if (Occupied(next, growing))
        {
            Die();
            return;
        }

        var tailCell = SegmentCell(Length - 1);
        head = (head + 1) % CellCount;
        cells[head] = next;
        tailPrevious = tailCell;
        if (growing)
        {
            growth--;
            Length++;
        }

        if (next == FruitCell)
        {
            Eat(FruitKind.Apple);
            SpawnFruit();
        }
        else if (next == SpecialCell && SpecialKind != FruitKind.None)
        {
            Eat(SpecialKind);
            ClearSpecial();
        }
    }

    private bool Occupied(int cell, bool growing)
    {
        var last = growing ? Length : Length - 1;
        for (var segment = 0; segment < last; segment++)
        {
            if (SegmentCell(segment) == cell)
            {
                return true;
            }
        }

        return false;
    }

    private void Die()
    {
        State = SnakeState.Dying;
        dyingLeft = DyingSeconds;
        moveTimer = stepSeconds;
        DiedThisStep = true;
    }

    private void Eat(FruitKind kind)
    {
        AteThisStep = kind;
        switch (kind)
        {
            case FruitKind.Apple:
                Score += ApplePoints;
                growth++;
                CountFruit();
                return;
            case FruitKind.Gold:
                Score += GoldPoints;
                growth++;
                CountFruit();
                return;
            case FruitKind.Bomb:
                Shrink(BombShrink);
                return;
            default:
                return;
        }
    }

    private void CountFruit()
    {
        var levelBefore = Level;
        Eaten++;
        if (Level == levelBefore)
        {
            return;
        }

        stepSeconds = MathF.Max(MinStepSeconds, BaseStepSeconds * MathF.Pow(SpeedRamp, Level - 1));
        LevelledThisStep = true;
    }

    private void Shrink(int amount)
    {
        growth = 0;
        var target = Math.Max(MinLength, Length - amount);
        if (target == Length)
        {
            return;
        }

        Length = target;
        tailPrevious = SegmentCell(Length - 1);
    }

    private void TickSpecial(float deltaSeconds)
    {
        if (SpecialKind == FruitKind.None)
        {
            return;
        }

        SpecialLeft -= deltaSeconds;
        if (SpecialLeft <= 0f)
        {
            ClearSpecial();
        }
    }

    private void ClearSpecial()
    {
        SpecialKind = FruitKind.None;
        SpecialCell = -1;
        SpecialLeft = 0f;
    }

    private void SpawnFruit()
    {
        FruitCell = FreeCell(SpecialCell);
        if (SpecialKind != FruitKind.None)
        {
            return;
        }

        if (random.Chance(GoldChance))
        {
            PlaceSpecial(FreeCell(FruitCell), FruitKind.Gold);
        }
        else if (random.Chance(BombChance))
        {
            PlaceSpecial(FreeCell(FruitCell), FruitKind.Bomb);
        }
    }

    private int FreeCell(int exclude)
    {
        Span<bool> occupied = stackalloc bool[CellCount];
        for (var segment = 0; segment < Length; segment++)
        {
            occupied[SegmentCell(segment)] = true;
        }

        if (exclude >= 0)
        {
            occupied[exclude] = true;
        }

        var free = 0;
        for (var cell = 0; cell < CellCount; cell++)
        {
            if (!occupied[cell])
            {
                free++;
            }
        }

        if (free == 0)
        {
            return -1;
        }

        var pick = random.Next(free);
        for (var cell = 0; cell < CellCount; cell++)
        {
            if (occupied[cell])
            {
                continue;
            }

            if (pick == 0)
            {
                return cell;
            }

            pick--;
        }

        return -1;
    }

    private static Vector2 Interpolate(int previousCell, int currentCell, float alpha)
    {
        var current = CellCenter(currentCell);
        if (previousCell < 0 || alpha >= 1f)
        {
            return current;
        }

        var previous = CellCenter(previousCell);
        if (MathF.Abs(current.X - previous.X) > 1f || MathF.Abs(current.Y - previous.Y) > 1f)
        {
            return current;
        }

        return Vector2.Lerp(previous, current, alpha);
    }

    private static bool IsOpposite(SnakeDirection first, SnakeDirection second) =>
        ((int)first + 2) % 4 == (int)second;
}
