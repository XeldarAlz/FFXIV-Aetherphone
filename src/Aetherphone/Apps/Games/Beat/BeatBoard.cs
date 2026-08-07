namespace Aetherphone.Apps.Games.Beat;

internal enum BeatState : byte
{
    Ready,
    Playing,
    Over,
}

internal enum BeatJudgement : byte
{
    None,
    Perfect,
    Good,
    Wrong,
    Missed,
}

internal struct BeatTile
{
    public float Y;
    public int Lane;
}

internal sealed class BeatBoard
{
    public const int Lanes = 4;
    public const int Capacity = 24;
    public const float HitLine = 1f;
    public const float TileHeight = 0.155f;
    public const float PerfectWindow = 0.048f;
    public const float GoodWindow = 0.135f;
    private const float MissLine = HitLine + 0.16f;
    private const float SpawnY = -TileHeight;
    private const float Spacing = 0.315f;
    private const float StartSpeed = 0.62f;
    private const float SpeedStep = 1.055f;
    private const float MaxSpeed = 2.35f;
    private const int TilesPerLevel = 10;
    private const int StartLives = 3;
    private const int MaxMultiplier = 5;
    private readonly BeatTile[] tiles = new BeatTile[Capacity];
    private readonly Random random = new();
    private float spawnTimer;
    private int hitsThisLevel;
    private int lastLane = -1;
    public BeatState State { get; private set; } = BeatState.Ready;
    public int Count { get; private set; }
    public int Score { get; private set; }
    public int Combo { get; private set; }
    public int Lives { get; private set; } = StartLives;
    public int Level { get; private set; } = 1;
    public float Speed { get; private set; } = StartSpeed;
    public int MissedLane { get; private set; }
    public int Multiplier => 1 + Math.Min(MaxMultiplier - 1, Combo / 8);
    public BeatTile Tile(int index) => tiles[index];

    public void Reset()
    {
        State = BeatState.Ready;
        Count = 0;
        Score = 0;
        Combo = 0;
        Lives = StartLives;
        Level = 1;
        Speed = StartSpeed;
        MissedLane = -1;
        hitsThisLevel = 0;
        lastLane = -1;
        spawnTimer = 0f;
        Spawn();
    }

    public void Begin()
    {
        if (State != BeatState.Ready)
        {
            return;
        }

        State = BeatState.Playing;
    }

    public BeatJudgement Step(float deltaSeconds)
    {
        MissedLane = -1;
        if (State != BeatState.Playing)
        {
            return BeatJudgement.None;
        }

        spawnTimer -= deltaSeconds;
        if (spawnTimer <= 0f)
        {
            Spawn();
        }

        var judgement = BeatJudgement.None;
        for (var index = Count - 1; index >= 0; index--)
        {
            ref var tile = ref tiles[index];
            tile.Y += Speed * deltaSeconds;
            if (tile.Y < MissLine)
            {
                continue;
            }

            MissedLane = tile.Lane;
            judgement = BeatJudgement.Missed;
            RemoveAt(index);
            LoseLife();
        }

        return judgement;
    }

    public BeatJudgement Tap(int lane)
    {
        if (State != BeatState.Playing)
        {
            return BeatJudgement.None;
        }

        var bestIndex = -1;
        var bestDistance = GoodWindow;
        for (var index = 0; index < Count; index++)
        {
            ref readonly var tile = ref tiles[index];
            if (tile.Lane != lane)
            {
                continue;
            }

            var distance = MathF.Abs(tile.Y - HitLine);
            if (distance > bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestIndex = index;
        }

        if (bestIndex < 0)
        {
            Combo = 0;
            return BeatJudgement.Wrong;
        }

        var perfect = bestDistance <= PerfectWindow;
        RemoveAt(bestIndex);
        Combo++;
        Score += (perfect ? 3 : 1) * Multiplier;
        hitsThisLevel++;
        if (hitsThisLevel >= TilesPerLevel)
        {
            hitsThisLevel = 0;
            Level++;
            Speed = MathF.Min(MaxSpeed, Speed * SpeedStep);
        }

        return perfect ? BeatJudgement.Perfect : BeatJudgement.Good;
    }

    public float LaneCenter(int lane) => (lane + 0.5f) / Lanes;

    private void LoseLife()
    {
        Combo = 0;
        Lives--;
        if (Lives > 0)
        {
            return;
        }

        Lives = 0;
        State = BeatState.Over;
    }

    private void Spawn()
    {
        spawnTimer += Spacing / Speed;
        if (Count >= Capacity)
        {
            return;
        }

        var lane = random.Next(Lanes);
        if (lane == lastLane)
        {
            lane = (lane + 1 + random.Next(Lanes - 1)) % Lanes;
        }

        lastLane = lane;
        ref var tile = ref tiles[Count];
        tile.Lane = lane;
        tile.Y = SpawnY;
        Count++;
    }

    private void RemoveAt(int index)
    {
        tiles[index] = tiles[Count - 1];
        Count--;
    }
}
