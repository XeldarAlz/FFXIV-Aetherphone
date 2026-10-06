using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games.Breakout;

internal enum PowerUpKind : byte
{
    MultiBall,
    Wide,
}

internal enum BrickKind : byte
{
    Normal,
    Armoured,
    Explosive,
}

internal struct Ball
{
    public Vector2 Position;
    public Vector2 Velocity;
    public int Id;
}

internal struct PowerUp
{
    public Vector2 Position;
    public PowerUpKind Kind;
}

internal sealed class BreakoutBoard
{
    public const int Columns = 7;
    public const int MaxRows = 8;
    public const int CellCount = Columns * MaxRows;
    public const int StartingLives = 3;
    public const int MaxBalls = 10;
    public const int MaxPowerUps = 8;
    public const int ArmouredHits = 2;
    public const int NormalPoints = 10;
    public const int ArmouredPoints = 20;
    public const int ExplosivePoints = 15;
    public const float FieldWidth = 1f;
    public const float FieldHeight = 1.7f;
    public const float MarginX = 0.05f;
    public const float BrickTop = 0.09f;
    public const float BrickHeight = 0.045f;
    public const float BrickGap = 0.012f;
    public const float BrickWidth = (FieldWidth - MarginX * 2f - (Columns - 1) * BrickGap) / Columns;
    public const float BallRadius = 0.018f;
    public const float PaddleHeight = 0.024f;
    public const float PaddleY = FieldHeight - 0.06f;
    public const float DefaultPaddleHalfWidth = 0.12f;
    public const float WidePaddleHalfWidth = 0.19f;
    public const float PowerUpRadius = 0.026f;
    public const float PowerUpChance = 0.10f;
    public const float ComboWindowSeconds = 3f;
    public const float LevelClearHoldSeconds = 0.9f;
    public const float BaseSpeed = 0.92f;
    private const float SpeedPerLevel = 0.06f;
    private const float PowerUpFallSpeed = 0.45f;
    private const float EnglishAngle = MathF.PI * 0.38f;
    private const float LaunchSpread = 0.5f;
    private const float MaxStepTravel = BallRadius * 0.6f;
    private const int MaxSubsteps = 8;
    private const float MultiBallSpread = 0.4f;
    private const int MultiBallSpawns = 2;
    private const float ExplosiveChance = 0.07f;
    private const float ArmouredChanceBase = 0.06f;
    private const float ArmouredChancePerLevel = 0.03f;
    private const float ArmouredChanceCap = 0.30f;
    private const int ColorCount = 6;
    private static readonly int[] NeighbourColumnStep = { -1, 0, 1, -1, 1, -1, 0, 1 };
    private static readonly int[] NeighbourRowStep = { -1, -1, -1, 0, 0, 1, 1, 1 };

    private readonly bool[] alive = new bool[CellCount];
    private readonly BrickKind[] kinds = new BrickKind[CellCount];
    private readonly int[] hits = new int[CellCount];
    private readonly int[] colors = new int[CellCount];
    private readonly Ball[] balls = new Ball[MaxBalls];
    private readonly PowerUp[] powerUps = new PowerUp[MaxPowerUps];
    private readonly Vector2[] breakPositions = new Vector2[CellCount];
    private readonly int[] breakColors = new int[CellCount];
    private readonly BrickKind[] breakKinds = new BrickKind[CellCount];
    private readonly Vector2[] dentPositions = new Vector2[CellCount];
    private readonly Vector2[] explosionPositions = new Vector2[CellCount];
    private readonly PowerUpKind[] spawnedKinds = new PowerUpKind[MaxPowerUps];
    private GameRandom random;
    private ComboMeter combo = new(ComboWindowSeconds);
    private float ballSpeed = BaseSpeed;
    private float clearHold;
    private int nextBallId;
    private int aliveBricks;

    public int Rows { get; private set; }

    public int BallCount { get; private set; }

    public int PowerUpCount { get; private set; }

    public int BreakCount { get; private set; }

    public int DentCount { get; private set; }

    public int ExplosionCount { get; private set; }

    public int SpawnCount { get; private set; }

    public bool LostLifeThisFrame { get; private set; }

    public Vector2 LostBallPosition { get; private set; }

    public bool CaughtPowerThisFrame { get; private set; }

    public PowerUpKind CaughtKind { get; private set; }

    public bool PaddleHitThisFrame { get; private set; }

    public bool LevelCleared { get; private set; }

    public bool LevelStarted { get; private set; }

    public float PaddleX { get; private set; } = 0.5f;

    public float PaddleHalfWidth { get; private set; } = DefaultPaddleHalfWidth;

    public int Score { get; private set; }

    public int Lives { get; private set; }

    public int Level { get; private set; }

    public int BricksBroken { get; private set; }

    public int BestCombo { get; private set; }

    public bool Attached { get; private set; }

    public bool GameOver { get; private set; }

    public bool Holding => clearHold > 0f;

    public int AliveBricks => aliveBricks;

    public ComboMeter Combo => combo;

    public Ball GetBall(int index) => balls[index];

    public PowerUp GetPowerUp(int index) => powerUps[index];

    public bool BrickAlive(int column, int row) => alive[row * Columns + column];

    public BrickKind BrickKindAt(int column, int row) => kinds[row * Columns + column];

    public int BrickHits(int column, int row) => hits[row * Columns + column];

    public int BrickColor(int column, int row) => colors[row * Columns + column];

    public Vector2 BreakPosition(int index) => breakPositions[index];

    public int BreakColor(int index) => breakColors[index];

    public BrickKind BreakKind(int index) => breakKinds[index];

    public Vector2 DentPosition(int index) => dentPositions[index];

    public Vector2 ExplosionPosition(int index) => explosionPositions[index];

    public PowerUpKind SpawnedKind(int index) => spawnedKinds[index];

    public static Vector2 BrickCenter(int column, int row)
    {
        var x = MarginX + column * (BrickWidth + BrickGap) + BrickWidth * 0.5f;
        var y = BrickTop + row * (BrickHeight + BrickGap) + BrickHeight * 0.5f;
        return new Vector2(x, y);
    }

    public static int PointsFor(BrickKind kind) => kind switch
    {
        BrickKind.Armoured => ArmouredPoints,
        BrickKind.Explosive => ExplosivePoints,
        _ => NormalPoints,
    };

    public void StartGame(GameRandom seededRandom)
    {
        random = seededRandom;
        Score = 0;
        Lives = StartingLives;
        Level = 1;
        BricksBroken = 0;
        BestCombo = 0;
        ballSpeed = BaseSpeed;
        PaddleX = 0.5f;
        PaddleHalfWidth = DefaultPaddleHalfWidth;
        GameOver = false;
        PowerUpCount = 0;
        clearHold = 0f;
        nextBallId = 0;
        combo.Reset();
        ClearEvents();
        BuildLevel();
        AttachBall();
    }

    public void SetPaddle(float x)
    {
        PaddleX = Math.Clamp(x, PaddleHalfWidth, FieldWidth - PaddleHalfWidth);
    }

    public void Launch()
    {
        if (!Attached || BallCount == 0 || GameOver)
        {
            return;
        }

        Attached = false;
        var angle = -MathF.PI * 0.5f + (random.NextFloat() - 0.5f) * LaunchSpread;
        balls[0].Velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * ballSpeed;
    }

    public void ClearBricks()
    {
        Array.Clear(alive);
        aliveBricks = 0;
    }

    public void PlaceBrick(int column, int row, BrickKind kind, int color = 0)
    {
        var cell = row * Columns + column;
        if (!alive[cell])
        {
            aliveBricks++;
        }

        alive[cell] = true;
        kinds[cell] = kind;
        hits[cell] = 0;
        colors[cell] = color;
        Rows = Math.Max(Rows, row + 1);
    }

    public void PlaceBall(Vector2 position, Vector2 velocity)
    {
        BallCount = 1;
        balls[0] = new Ball { Position = position, Velocity = velocity, Id = nextBallId++ };
        Attached = false;
    }

    public void Update(float deltaSeconds)
    {
        ClearEvents();
        if (GameOver || deltaSeconds <= 0f)
        {
            return;
        }

        combo.Update(deltaSeconds);
        if (Attached)
        {
            balls[0].Position = RestPosition();
            return;
        }

        var substeps = new Substeps(deltaSeconds, MaxStepTravel / ballSpeed, MaxSubsteps);
        for (var step = 0; step < substeps.Count && !Attached; step++)
        {
            StepBalls(substeps.Step);
        }

        if (Holding)
        {
            clearHold -= deltaSeconds;
            if (clearHold <= 0f || BallCount == 0)
            {
                clearHold = 0f;
                NextLevel();
            }

            return;
        }

        UpdatePowerUps(deltaSeconds);
        if (aliveBricks > 0)
        {
            return;
        }

        LevelCleared = true;
        clearHold = LevelClearHoldSeconds;
        PowerUpCount = 0;
    }

    private void ClearEvents()
    {
        BreakCount = 0;
        DentCount = 0;
        ExplosionCount = 0;
        SpawnCount = 0;
        LostLifeThisFrame = false;
        CaughtPowerThisFrame = false;
        PaddleHitThisFrame = false;
        LevelCleared = false;
        LevelStarted = false;
    }

    private void StepBalls(float deltaSeconds)
    {
        for (var index = BallCount - 1; index >= 0; index--)
        {
            ref var ball = ref balls[index];
            ball.Position += ball.Velocity * deltaSeconds;
            if (ball.Position.X < BallRadius)
            {
                ball.Position.X = BallRadius;
                ball.Velocity.X = MathF.Abs(ball.Velocity.X);
            }
            else if (ball.Position.X > FieldWidth - BallRadius)
            {
                ball.Position.X = FieldWidth - BallRadius;
                ball.Velocity.X = -MathF.Abs(ball.Velocity.X);
            }

            if (ball.Position.Y < BallRadius)
            {
                ball.Position.Y = BallRadius;
                ball.Velocity.Y = MathF.Abs(ball.Velocity.Y);
            }

            BouncePaddle(ref ball);
            BounceBricks(ref ball);
            if (ball.Position.Y <= FieldHeight + BallRadius)
            {
                continue;
            }

            LostBallPosition = ball.Position;
            balls[index] = balls[BallCount - 1];
            BallCount--;
        }

        if (BallCount == 0 && !Holding)
        {
            LoseLife();
        }
    }

    private void BouncePaddle(ref Ball ball)
    {
        if (ball.Velocity.Y <= 0f)
        {
            return;
        }

        var paddleTop = PaddleY - PaddleHeight * 0.5f;
        if (ball.Position.Y + BallRadius < paddleTop || ball.Position.Y - BallRadius > PaddleY + PaddleHeight * 0.5f)
        {
            return;
        }

        if (ball.Position.X < PaddleX - PaddleHalfWidth - BallRadius ||
            ball.Position.X > PaddleX + PaddleHalfWidth + BallRadius)
        {
            return;
        }

        var english = Math.Clamp((ball.Position.X - PaddleX) / PaddleHalfWidth, -1f, 1f);
        var angle = -MathF.PI * 0.5f + english * EnglishAngle;
        ball.Velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * ballSpeed;
        ball.Position.Y = paddleTop - BallRadius;
        PaddleHitThisFrame = true;
    }

    private void BounceBricks(ref Ball ball)
    {
        for (var row = 0; row < Rows; row++)
        {
            for (var column = 0; column < Columns; column++)
            {
                var cell = row * Columns + column;
                if (!alive[cell])
                {
                    continue;
                }

                var center = BrickCenter(column, row);
                var halfWidth = BrickWidth * 0.5f;
                var halfHeight = BrickHeight * 0.5f;
                var nearestX = Math.Clamp(ball.Position.X, center.X - halfWidth, center.X + halfWidth);
                var nearestY = Math.Clamp(ball.Position.Y, center.Y - halfHeight, center.Y + halfHeight);
                var offsetX = ball.Position.X - nearestX;
                var offsetY = ball.Position.Y - nearestY;
                if (offsetX * offsetX + offsetY * offsetY > BallRadius * BallRadius)
                {
                    continue;
                }

                if (MathF.Abs(offsetX) > MathF.Abs(offsetY))
                {
                    ball.Velocity.X = -ball.Velocity.X;
                }
                else
                {
                    ball.Velocity.Y = -ball.Velocity.Y;
                }

                HitBrick(cell, center);
                return;
            }
        }
    }

    private void HitBrick(int cell, Vector2 center)
    {
        if (kinds[cell] == BrickKind.Armoured && hits[cell] < ArmouredHits - 1)
        {
            hits[cell]++;
            dentPositions[DentCount] = center;
            DentCount++;
            return;
        }

        BreakBrick(cell);
    }

    private void BreakBrick(int cell)
    {
        if (!alive[cell])
        {
            return;
        }

        alive[cell] = false;
        aliveBricks--;
        var kind = kinds[cell];
        var center = BrickCenter(cell % Columns, cell / Columns);
        var multiplier = combo.Hit();
        Score += PointsFor(kind) * multiplier;
        BricksBroken++;
        BestCombo = Math.Max(BestCombo, combo.Count);
        breakPositions[BreakCount] = center;
        breakColors[BreakCount] = colors[cell];
        breakKinds[BreakCount] = kind;
        BreakCount++;
        RollPowerUp(center);
        if (kind == BrickKind.Explosive)
        {
            Explode(cell, center);
        }
    }

    private void Explode(int cell, Vector2 center)
    {
        explosionPositions[ExplosionCount] = center;
        ExplosionCount++;
        var column = cell % Columns;
        var row = cell / Columns;
        for (var side = 0; side < NeighbourColumnStep.Length; side++)
        {
            var neighbourColumn = column + NeighbourColumnStep[side];
            var neighbourRow = row + NeighbourRowStep[side];
            if (neighbourColumn < 0 || neighbourColumn >= Columns || neighbourRow < 0 || neighbourRow >= Rows)
            {
                continue;
            }

            BreakBrick(neighbourRow * Columns + neighbourColumn);
        }
    }

    private void RollPowerUp(Vector2 center)
    {
        var roll = random.NextFloat();
        if (roll >= PowerUpChance * 2f || PowerUpCount >= MaxPowerUps)
        {
            return;
        }

        var kind = roll < PowerUpChance ? PowerUpKind.MultiBall : PowerUpKind.Wide;
        powerUps[PowerUpCount] = new PowerUp { Position = center, Kind = kind };
        PowerUpCount++;
        spawnedKinds[SpawnCount] = kind;
        SpawnCount++;
    }

    private void UpdatePowerUps(float deltaSeconds)
    {
        for (var index = PowerUpCount - 1; index >= 0; index--)
        {
            powerUps[index].Position.Y += PowerUpFallSpeed * deltaSeconds;
            var position = powerUps[index].Position;
            var caught = position.Y >= PaddleY - PaddleHeight && position.Y <= PaddleY + PaddleHeight &&
                         position.X >= PaddleX - PaddleHalfWidth && position.X <= PaddleX + PaddleHalfWidth;
            if (caught)
            {
                CaughtKind = powerUps[index].Kind;
                CaughtPowerThisFrame = true;
                ApplyPowerUp(CaughtKind);
                powerUps[index] = powerUps[PowerUpCount - 1];
                PowerUpCount--;
            }
            else if (position.Y > FieldHeight + PowerUpRadius)
            {
                powerUps[index] = powerUps[PowerUpCount - 1];
                PowerUpCount--;
            }
        }
    }

    private void ApplyPowerUp(PowerUpKind kind)
    {
        if (kind == PowerUpKind.Wide)
        {
            PaddleHalfWidth = WidePaddleHalfWidth;
            SetPaddle(PaddleX);
            return;
        }

        if (BallCount == 0)
        {
            return;
        }

        var source = balls[0];
        var heading = source.Velocity == Vector2.Zero ? new Vector2(0f, -1f) : Vector2.Normalize(source.Velocity);
        var baseAngle = MathF.Atan2(heading.Y, heading.X);
        var spawnCount = Math.Min(MultiBallSpawns, MaxBalls - BallCount);
        for (var spawn = 0; spawn < spawnCount; spawn++)
        {
            var spread = (spawn + 1) * MultiBallSpread * (spawn % 2 == 0 ? 1f : -1f);
            var angle = baseAngle + spread;
            balls[BallCount] = new Ball
            {
                Position = source.Position,
                Velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * ballSpeed,
                Id = nextBallId++,
            };
            BallCount++;
        }

        Attached = false;
    }

    private void LoseLife()
    {
        Lives--;
        combo.Reset();
        LostLifeThisFrame = true;
        PaddleHalfWidth = DefaultPaddleHalfWidth;
        SetPaddle(PaddleX);
        PowerUpCount = 0;
        if (Lives <= 0)
        {
            GameOver = true;
            return;
        }

        AttachBall();
    }

    private void AttachBall()
    {
        BallCount = 1;
        balls[0] = new Ball { Position = RestPosition(), Velocity = Vector2.Zero, Id = nextBallId++ };
        Attached = true;
    }

    private Vector2 RestPosition() => new(PaddleX, PaddleY - PaddleHeight * 0.5f - BallRadius);

    private void NextLevel()
    {
        Level++;
        ballSpeed = BaseSpeed + SpeedPerLevel * (Level - 1);
        PaddleHalfWidth = DefaultPaddleHalfWidth;
        SetPaddle(PaddleX);
        PowerUpCount = 0;
        BuildLevel();
        AttachBall();
        LevelStarted = true;
    }

    private void BuildLevel()
    {
        Rows = Math.Min(MaxRows, 3 + Level);
        Array.Clear(alive);
        aliveBricks = 0;
        var armouredChance = MathF.Min(ArmouredChanceCap, ArmouredChanceBase + ArmouredChancePerLevel * (Level - 1));
        for (var row = 0; row < Rows; row++)
        {
            for (var column = 0; column < Columns; column++)
            {
                var cell = row * Columns + column;
                alive[cell] = true;
                aliveBricks++;
                colors[cell] = row % ColorCount;
                hits[cell] = 0;
                kinds[cell] = RollKind(armouredChance);
            }
        }
    }

    private BrickKind RollKind(float armouredChance)
    {
        var roll = random.NextFloat();
        if (roll < ExplosiveChance)
        {
            return BrickKind.Explosive;
        }

        return roll < ExplosiveChance + armouredChance ? BrickKind.Armoured : BrickKind.Normal;
    }
}
