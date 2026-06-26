using System;
using System.Numerics;

namespace Aetherphone.Apps.Games.Breakout;

internal enum PowerUpKind
{
    MultiBall,
    Wide,
}

internal struct Ball
{
    public Vector2 Position;

    public Vector2 Velocity;
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

    public const int MaxBalls = 10;

    public const int MaxPowerUps = 8;

    public const float MarginX = 0.05f;

    public const float BrickTop = 0.09f;

    public const float BrickHeight = 0.045f;

    public const float BrickGap = 0.012f;

    public const float BallRadius = 0.018f;

    public const float PaddleHeight = 0.024f;

    private const float BaseSpeed = 0.92f;

    private const float SpeedPerLevel = 0.06f;

    private const float PowerUpFallSpeed = 0.45f;

    private const float WideMultiplier = 1.6f;

    private readonly bool[] alive = new bool[Columns * MaxRows];

    private readonly int[] colors = new int[Columns * MaxRows];

    private readonly Ball[] balls = new Ball[MaxBalls];

    private readonly PowerUp[] powerUps = new PowerUp[MaxPowerUps];

    private readonly Vector2[] breakPositions = new Vector2[Columns * MaxRows];

    private readonly int[] breakColors = new int[Columns * MaxRows];

    private readonly Random random = new();

    private float defaultPaddleHalf = 0.12f;

    private float ballSpeed = BaseSpeed;

    public float FieldHeight { get; private set; } = 1.6f;

    public int Rows { get; private set; }

    public int BallCount { get; private set; }

    public int PowerUpCount { get; private set; }

    public int BreakCount { get; private set; }

    public bool LostLifeThisFrame { get; private set; }

    public bool CaughtPowerThisFrame { get; private set; }

    public bool LevelCleared { get; private set; }

    public float PaddleX { get; private set; } = 0.5f;

    public float PaddleHalfWidth { get; private set; } = 0.12f;

    public float PaddleY => FieldHeight - 0.06f;

    public int Score { get; private set; }

    public int Lives { get; private set; }

    public int Level { get; private set; }

    public int Combo { get; private set; }

    public bool Attached { get; private set; }

    public bool GameOver { get; private set; }

    public Ball GetBall(int index) => balls[index];

    public PowerUp GetPowerUp(int index) => powerUps[index];

    public bool BrickAlive(int column, int row) => alive[row * Columns + column];

    public int BrickColor(int column, int row) => colors[row * Columns + column];

    public Vector2 BreakPosition(int index) => breakPositions[index];

    public int BreakColor(int index) => breakColors[index];

    public float BrickWidth => (1f - MarginX * 2f - (Columns - 1) * BrickGap) / Columns;

    public Vector2 BrickCenter(int column, int row)
    {
        var x = MarginX + column * (BrickWidth + BrickGap) + BrickWidth * 0.5f;
        var y = BrickTop + row * (BrickHeight + BrickGap) + BrickHeight * 0.5f;
        return new Vector2(x, y);
    }

    public void StartGame(float fieldHeight)
    {
        FieldHeight = fieldHeight;
        Score = 0;
        Lives = 3;
        Level = 1;
        ballSpeed = BaseSpeed;
        PaddleHalfWidth = defaultPaddleHalf;
        GameOver = false;
        PowerUpCount = 0;
        BuildLevel();
        AttachBall();
    }

    public void SetFieldHeight(float fieldHeight)
    {
        FieldHeight = fieldHeight;
    }

    public void SetPaddle(float normalizedX)
    {
        PaddleX = Math.Clamp(normalizedX, PaddleHalfWidth, 1f - PaddleHalfWidth);
    }

    public void Launch()
    {
        if (!Attached || BallCount == 0)
        {
            return;
        }

        Attached = false;
        var angle = -MathF.PI * 0.5f + ((float)random.NextDouble() - 0.5f) * 0.5f;
        balls[0].Velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * ballSpeed;
    }

    public void Update(float deltaSeconds)
    {
        BreakCount = 0;
        LostLifeThisFrame = false;
        CaughtPowerThisFrame = false;
        LevelCleared = false;

        if (GameOver)
        {
            return;
        }

        if (Attached)
        {
            balls[0].Position = new Vector2(PaddleX, PaddleY - PaddleHeight * 0.5f - BallRadius);
            return;
        }

        var substeps = 1 + (int)(ballSpeed * deltaSeconds / (BallRadius * 0.6f));
        substeps = Math.Clamp(substeps, 1, 8);
        var subDelta = deltaSeconds / substeps;
        for (var step = 0; step < substeps; step++)
        {
            StepBalls(subDelta);
        }

        UpdatePowerUps(deltaSeconds);

        if (CountAliveBricks() == 0)
        {
            LevelCleared = true;
            NextLevel();
        }
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
            else if (ball.Position.X > 1f - BallRadius)
            {
                ball.Position.X = 1f - BallRadius;
                ball.Velocity.X = -MathF.Abs(ball.Velocity.X);
            }

            if (ball.Position.Y < BallRadius)
            {
                ball.Position.Y = BallRadius;
                ball.Velocity.Y = MathF.Abs(ball.Velocity.Y);
            }

            BouncePaddle(ref ball);
            BounceBricks(ref ball);

            if (ball.Position.Y > FieldHeight + BallRadius)
            {
                balls[index] = balls[BallCount - 1];
                BallCount--;
            }
        }

        if (BallCount == 0)
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

        if (ball.Position.X < PaddleX - PaddleHalfWidth || ball.Position.X > PaddleX + PaddleHalfWidth)
        {
            return;
        }

        var offset = (ball.Position.X - PaddleX) / PaddleHalfWidth;
        var angle = -MathF.PI * 0.5f + offset * (MathF.PI * 0.38f);
        ball.Velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * ballSpeed;
        ball.Position.Y = paddleTop - BallRadius;
        Combo = 0;
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
                var dx = ball.Position.X - nearestX;
                var dy = ball.Position.Y - nearestY;
                if (dx * dx + dy * dy > BallRadius * BallRadius)
                {
                    continue;
                }

                if (MathF.Abs(dx) > MathF.Abs(dy))
                {
                    ball.Velocity.X = -ball.Velocity.X;
                }
                else
                {
                    ball.Velocity.Y = -ball.Velocity.Y;
                }

                BreakBrick(column, row, center);
                return;
            }
        }
    }

    private void BreakBrick(int column, int row, Vector2 center)
    {
        var cell = row * Columns + column;
        alive[cell] = false;
        Combo++;
        Score += 10 + Combo;

        breakPositions[BreakCount] = center;
        breakColors[BreakCount] = colors[cell];
        BreakCount++;

        if (PowerUpCount < MaxPowerUps && random.Next(100) < 12)
        {
            powerUps[PowerUpCount] = new PowerUp
            {
                Position = center,
                Kind = random.Next(2) == 0 ? PowerUpKind.MultiBall : PowerUpKind.Wide,
            };
            PowerUpCount++;
        }
    }

    private void UpdatePowerUps(float deltaSeconds)
    {
        for (var index = PowerUpCount - 1; index >= 0; index--)
        {
            powerUps[index].Position.Y += PowerUpFallSpeed * deltaSeconds;
            var position = powerUps[index].Position;

            var caught = position.Y >= PaddleY - PaddleHeight
                && position.Y <= PaddleY + PaddleHeight
                && position.X >= PaddleX - PaddleHalfWidth
                && position.X <= PaddleX + PaddleHalfWidth;

            if (caught)
            {
                ApplyPowerUp(powerUps[index].Kind);
                CaughtPowerThisFrame = true;
                powerUps[index] = powerUps[PowerUpCount - 1];
                PowerUpCount--;
            }
            else if (position.Y > FieldHeight + 0.05f)
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
            PaddleHalfWidth = MathF.Min(0.24f, defaultPaddleHalf * WideMultiplier);
            return;
        }

        var spawnCount = Math.Min(2, MaxBalls - BallCount);
        if (BallCount == 0)
        {
            return;
        }

        var source = balls[0];
        for (var index = 0; index < spawnCount; index++)
        {
            var spread = (index + 1) * 0.4f * (index % 2 == 0 ? 1f : -1f);
            var direction = Vector2.Normalize(source.Velocity == Vector2.Zero ? new Vector2(0f, -1f) : source.Velocity);
            var angle = MathF.Atan2(direction.Y, direction.X) + spread;
            balls[BallCount] = new Ball
            {
                Position = source.Position,
                Velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * ballSpeed,
            };
            BallCount++;
        }
    }

    private void LoseLife()
    {
        Lives--;
        Combo = 0;
        LostLifeThisFrame = true;
        PaddleHalfWidth = defaultPaddleHalf;
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
        balls[0] = new Ball
        {
            Position = new Vector2(PaddleX, PaddleY - PaddleHeight * 0.5f - BallRadius),
            Velocity = Vector2.Zero,
        };
        Attached = true;
    }

    private void NextLevel()
    {
        Level++;
        ballSpeed = BaseSpeed + SpeedPerLevel * (Level - 1);
        PaddleHalfWidth = defaultPaddleHalf;
        PowerUpCount = 0;
        BuildLevel();
        AttachBall();
    }

    private void BuildLevel()
    {
        Rows = Math.Min(MaxRows, 3 + Level);
        Array.Clear(alive, 0, alive.Length);
        for (var row = 0; row < Rows; row++)
        {
            for (var column = 0; column < Columns; column++)
            {
                var cell = row * Columns + column;
                alive[cell] = true;
                colors[cell] = row % 6;
            }
        }
    }

    private int CountAliveBricks()
    {
        var count = 0;
        for (var index = 0; index < Rows * Columns; index++)
        {
            if (alive[index])
            {
                count++;
            }
        }

        return count;
    }
}
