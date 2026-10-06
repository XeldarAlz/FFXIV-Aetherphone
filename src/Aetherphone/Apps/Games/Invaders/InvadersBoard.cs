using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games.Invaders;

internal sealed class InvadersBoard
{
    public const float Width = 100f;
    public const float Height = 140f;
    public const int Columns = 7;
    public const int Rows = 5;
    public const int InvaderCount = Columns * Rows;
    public const float InvaderWidth = 8f;
    public const float InvaderHeight = 6f;
    public const float PlayerWidth = 9f;
    public const float PlayerHeight = 4.5f;
    public const float PlayerY = 126f;
    public const int ShieldCount = 4;
    public const int ShieldColumns = 5;
    public const int ShieldRows = 3;
    public const int ShieldCellCount = ShieldCount * ShieldColumns * ShieldRows;
    public const float ShieldCell = 2.2f;
    public const float ShieldY = 104f;
    public const float ColumnPitch = 12f;
    public const float RowPitch = 10f;
    public const float StepX = 2.6f;
    public const float StepY = 5f;
    public const float LandingY = PlayerY - InvaderHeight;
    public const float BulletSpeed = 120f;
    public const float BombSpeed = 38f;
    public const float PlayerSpeed = 62f;
    public const int MaxBombs = 3;
    public const int StartingLives = 3;
    public const int WaveClearBonus = 200;
    public const float SaucerY = 8f;
    public const float SaucerHalfWidth = 5f;
    public const float SaucerHalfHeight = 2f;
    public const float SaucerSpeed = 18f;
    public const int SaucerPoints = 300;
    public const float SaucerMinInterval = 18f;
    public const float SaucerMaxInterval = 30f;
    public static readonly int[] RowPoints = { 30, 20, 20, 10, 10 };
    public static readonly int[] RowKinds = { 0, 1, 1, 2, 2 };
    public static readonly float[] ShieldX = { 14f, 38f, 62f, 86f };
    private const float RespawnSeconds = 1f;
    private const float FirstBombDelay = 1.2f;
    private const float FormationStartX = 6f;
    private const float FormationStartY = 14f;
    private const float FormationDropPerWave = 3f;
    private const int FormationDropWaveCap = 6;
    private const float EdgeMargin = 2f;
    private const float PlayerMinX = PlayerWidth * 0.5f;
    private const float PlayerMaxX = Width - PlayerWidth * 0.5f;
    private readonly bool[] invaders = new bool[InvaderCount];
    private readonly bool[] shields = new bool[ShieldCellCount];
    private readonly Vector2[] bombs = new Vector2[MaxBombs];
    private readonly Vector2[] bombCandidates = new Vector2[Columns];
    private readonly Vector2[] killPositions = new Vector2[InvaderCount];
    private readonly int[] killKinds = new int[InvaderCount];
    private readonly Vector2[] chipPositions = new Vector2[MaxBombs + 1];
    private GameRandom random;
    private int bombCount;
    private bool hasBullet;
    private Vector2 bullet;
    private float formationX;
    private float formationY;
    private int direction = 1;
    private float stepTimer;
    private float bombTimer;
    private float respawnTimer;
    private float saucerTimer;
    private int saucerDirection = 1;
    public int Score { get; private set; }
    public int Wave { get; private set; }
    public int Lives { get; private set; }
    public int AliveCount { get; private set; }
    public bool GameOver { get; private set; }
    public bool AnimFrame { get; private set; }
    public float PlayerX { get; private set; } = Width * 0.5f;
    public float FormationY => formationY;
    public bool Respawning => respawnTimer > 0f;
    public bool SaucerActive { get; private set; }
    public float SaucerX { get; private set; }
    public int SaucerDirection => saucerDirection;
    public int BombCount => bombCount;
    public bool HasBullet => hasBullet;
    public Vector2 Bullet => bullet;
    public int ShotsFired { get; private set; }
    public int ShotsHit { get; private set; }
    public int SaucersHit { get; private set; }
    public int KillCount { get; private set; }
    public int ChipCount { get; private set; }
    public bool PlayerHitThisFrame { get; private set; }
    public bool WaveStartedThisFrame { get; private set; }
    public bool WaveClearedThisFrame { get; private set; }
    public bool LandedThisFrame { get; private set; }
    public bool SaucerKilledThisFrame { get; private set; }
    public bool ShotFiredThisFrame { get; private set; }
    public bool SteppedThisFrame { get; private set; }
    public Vector2 SaucerKillPosition { get; private set; }
    public bool InvaderAlive(int column, int row) => invaders[row * Columns + column];
    public bool ShieldCellAlive(int shield, int column, int row) => shields[ShieldIndex(shield, column, row)];
    public Vector2 GetBomb(int index) => bombs[index];
    public Vector2 KillPosition(int index) => killPositions[index];
    public int KillKind(int index) => killKinds[index];
    public Vector2 ChipPosition(int index) => chipPositions[index];

    public Vector2 InvaderPosition(int column, int row) =>
        new(formationX + column * ColumnPitch, formationY + row * RowPitch);

    public static Vector2 ShieldCellPosition(int shield, int column, int row) =>
        new(ShieldX[shield] - ShieldCell * ShieldColumns * 0.5f + column * ShieldCell, ShieldY + row * ShieldCell);

    public static int ShieldIndex(int shield, int column, int row) =>
        shield * ShieldColumns * ShieldRows + row * ShieldColumns + column;

    public static float StepInterval(int wave, int aliveCount) =>
        MathF.Max(0.055f, (0.62f - wave * 0.03f) * (aliveCount / (float)InvaderCount));

    public static int PerfectWavePoints()
    {
        var total = WaveClearBonus;
        for (var row = 0; row < Rows; row++)
        {
            total += RowPoints[row] * Columns;
        }

        return total;
    }

    public void StartGame(GameRandom seededRandom)
    {
        random = seededRandom;
        Score = 0;
        Wave = 0;
        Lives = StartingLives;
        GameOver = false;
        ShotsFired = 0;
        ShotsHit = 0;
        SaucersHit = 0;
        ClearFrameEvents();
        StartWave();
    }

    public void Move(float direction, float deltaSeconds)
    {
        if (GameOver || direction == 0f)
        {
            return;
        }

        PlayerX = Math.Clamp(PlayerX + direction * PlayerSpeed * deltaSeconds, PlayerMinX, PlayerMaxX);
    }

    public bool Fire()
    {
        if (GameOver || hasBullet || respawnTimer > 0f)
        {
            return false;
        }

        bullet = new Vector2(PlayerX, PlayerY - PlayerHeight);
        hasBullet = true;
        ShotsFired++;
        ShotFiredThisFrame = true;
        return true;
    }

    public void Update(float deltaSeconds)
    {
        ClearFrameEvents();
        if (GameOver || deltaSeconds <= 0f)
        {
            return;
        }

        if (respawnTimer > 0f)
        {
            respawnTimer -= deltaSeconds;
            return;
        }

        StepFormation(deltaSeconds);
        if (GameOver)
        {
            return;
        }

        DropBombs(deltaSeconds);
        TickSaucer(deltaSeconds);
        var substeps = new Substeps(deltaSeconds, ShieldCell * 0.5f / BulletSpeed);
        for (var step = 0; step < substeps.Count; step++)
        {
            MoveBullet(substeps.Step);
            MoveBombs(substeps.Step);
            if (GameOver || respawnTimer > 0f)
            {
                break;
            }
        }

        if (AliveCount == 0)
        {
            Score += WaveClearBonus;
            WaveClearedThisFrame = true;
            StartWave();
        }
    }

    private void ClearFrameEvents()
    {
        KillCount = 0;
        ChipCount = 0;
        PlayerHitThisFrame = false;
        WaveStartedThisFrame = false;
        WaveClearedThisFrame = false;
        LandedThisFrame = false;
        SaucerKilledThisFrame = false;
        ShotFiredThisFrame = false;
        SteppedThisFrame = false;
    }

    private void StartWave()
    {
        Wave++;
        for (var index = 0; index < InvaderCount; index++)
        {
            invaders[index] = true;
        }

        for (var index = 0; index < ShieldCellCount; index++)
        {
            shields[index] = true;
        }

        AliveCount = InvaderCount;
        bombCount = 0;
        hasBullet = false;
        formationX = FormationStartX;
        formationY = FormationStartY + Math.Min(Wave - 1, FormationDropWaveCap) * FormationDropPerWave;
        direction = 1;
        stepTimer = 0f;
        bombTimer = FirstBombDelay;
        PlayerX = Width * 0.5f;
        respawnTimer = 0f;
        SaucerActive = false;
        saucerTimer = NextSaucerDelay();
        WaveStartedThisFrame = true;
    }

    private float NextSaucerDelay() => SaucerMinInterval + Chance() * (SaucerMaxInterval - SaucerMinInterval);

    private void StepFormation(float deltaSeconds)
    {
        if (AliveCount == 0)
        {
            return;
        }

        stepTimer += deltaSeconds;
        if (stepTimer < StepInterval(Wave, AliveCount))
        {
            return;
        }

        stepTimer = 0f;
        AnimFrame = !AnimFrame;
        SteppedThisFrame = true;
        FormationBounds(out var minX, out var maxX);
        var nextMin = minX + StepX * direction;
        var nextMax = maxX + StepX * direction;
        if (nextMin < EdgeMargin || nextMax > Width - EdgeMargin)
        {
            direction = -direction;
            formationY += StepY;
            if (formationY + (Rows - 1) * RowPitch + InvaderHeight >= LandingY)
            {
                GameOver = true;
                LandedThisFrame = true;
            }

            return;
        }

        formationX += StepX * direction;
    }

    public void FormationBounds(out float minX, out float maxX)
    {
        minX = float.MaxValue;
        maxX = float.MinValue;
        for (var column = 0; column < Columns; column++)
        {
            var anyAlive = false;
            for (var row = 0; row < Rows; row++)
            {
                if (invaders[row * Columns + column])
                {
                    anyAlive = true;
                    break;
                }
            }

            if (!anyAlive)
            {
                continue;
            }

            var x = formationX + column * ColumnPitch;
            minX = MathF.Min(minX, x);
            maxX = MathF.Max(maxX, x + InvaderWidth);
        }
    }

    private void DropBombs(float deltaSeconds)
    {
        bombTimer -= deltaSeconds;
        if (bombTimer > 0f || bombCount >= MaxBombs)
        {
            return;
        }

        bombTimer = MathF.Max(0.35f, 1.6f - Wave * 0.12f) * (0.5f + Chance());
        var candidateCount = 0;
        for (var column = 0; column < Columns; column++)
        {
            for (var row = Rows - 1; row >= 0; row--)
            {
                if (!invaders[row * Columns + column])
                {
                    continue;
                }

                var position = InvaderPosition(column, row);
                bombCandidates[candidateCount++] = new Vector2(position.X + InvaderWidth * 0.5f, position.Y + InvaderHeight);
                break;
            }
        }

        if (candidateCount == 0)
        {
            return;
        }

        bombs[bombCount++] = bombCandidates[random.Next(candidateCount)];
    }

    private void TickSaucer(float deltaSeconds)
    {
        if (SaucerActive)
        {
            SaucerX += saucerDirection * SaucerSpeed * deltaSeconds;
            if (SaucerX < -SaucerHalfWidth * 2f || SaucerX > Width + SaucerHalfWidth * 2f)
            {
                SaucerActive = false;
                saucerTimer = NextSaucerDelay();
            }

            return;
        }

        saucerTimer -= deltaSeconds;
        if (saucerTimer > 0f)
        {
            return;
        }

        SaucerActive = true;
        saucerDirection = Chance() < 0.5f ? 1 : -1;
        SaucerX = saucerDirection > 0 ? -SaucerHalfWidth * 2f : Width + SaucerHalfWidth * 2f;
    }

    private void MoveBullet(float step)
    {
        if (!hasBullet)
        {
            return;
        }

        bullet.Y -= BulletSpeed * step;
        if (bullet.Y < 0f)
        {
            hasBullet = false;
            return;
        }

        if (HitShield(bullet))
        {
            hasBullet = false;
            return;
        }

        if (SaucerActive && MathF.Abs(bullet.X - SaucerX) <= SaucerHalfWidth &&
            MathF.Abs(bullet.Y - SaucerY) <= SaucerHalfHeight)
        {
            SaucerActive = false;
            saucerTimer = NextSaucerDelay();
            Score += SaucerPoints;
            ShotsHit++;
            SaucersHit++;
            SaucerKilledThisFrame = true;
            SaucerKillPosition = new Vector2(SaucerX, SaucerY);
            hasBullet = false;
            return;
        }

        for (var column = 0; column < Columns; column++)
        {
            for (var row = 0; row < Rows; row++)
            {
                var index = row * Columns + column;
                if (!invaders[index])
                {
                    continue;
                }

                var position = InvaderPosition(column, row);
                if (bullet.X < position.X || bullet.X > position.X + InvaderWidth ||
                    bullet.Y < position.Y || bullet.Y > position.Y + InvaderHeight)
                {
                    continue;
                }

                invaders[index] = false;
                AliveCount--;
                Score += RowPoints[row];
                ShotsHit++;
                killPositions[KillCount] = position + new Vector2(InvaderWidth * 0.5f, InvaderHeight * 0.5f);
                killKinds[KillCount] = RowKinds[row];
                KillCount++;
                hasBullet = false;
                return;
            }
        }
    }

    private bool HitShield(Vector2 point)
    {
        if (point.Y < ShieldY || point.Y > ShieldY + ShieldRows * ShieldCell)
        {
            return false;
        }

        for (var shield = 0; shield < ShieldCount; shield++)
        {
            for (var column = 0; column < ShieldColumns; column++)
            {
                for (var row = 0; row < ShieldRows; row++)
                {
                    var index = ShieldIndex(shield, column, row);
                    if (!shields[index])
                    {
                        continue;
                    }

                    var position = ShieldCellPosition(shield, column, row);
                    if (point.X < position.X || point.X > position.X + ShieldCell ||
                        point.Y < position.Y || point.Y > position.Y + ShieldCell)
                    {
                        continue;
                    }

                    shields[index] = false;
                    if (ChipCount < chipPositions.Length)
                    {
                        chipPositions[ChipCount++] = position + new Vector2(ShieldCell * 0.5f, ShieldCell * 0.5f);
                    }

                    return true;
                }
            }
        }

        return false;
    }

    private void MoveBombs(float step)
    {
        for (var index = bombCount - 1; index >= 0; index--)
        {
            bombs[index].Y += BombSpeed * step;
            var bomb = bombs[index];
            if (HitShield(bomb))
            {
                bombs[index] = bombs[--bombCount];
                continue;
            }

            if (bomb.Y >= PlayerY - PlayerHeight && bomb.Y <= PlayerY && MathF.Abs(bomb.X - PlayerX) <= PlayerWidth * 0.5f)
            {
                LoseLife();
                return;
            }

            if (bomb.Y > Height)
            {
                bombs[index] = bombs[--bombCount];
            }
        }
    }

    private void LoseLife()
    {
        Lives--;
        hasBullet = false;
        bombCount = 0;
        PlayerHitThisFrame = true;
        if (Lives <= 0)
        {
            GameOver = true;
            return;
        }

        PlayerX = Width * 0.5f;
        respawnTimer = RespawnSeconds;
    }

    private float Chance() => random.NextFloat();
}
