using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games.Beat;

internal enum BeatState : byte
{
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
    public const float WorldHeight = 1.25f;
    public const float TileHeight = 0.155f;
    public const float PerfectWindow = 0.048f;
    public const float GoodWindow = 0.135f;
    public const float MissLine = HitLine + 0.16f;
    public const float StartSpeed = 0.62f;
    public const float SpeedStep = 1.055f;
    public const float MaxSpeed = 2.35f;
    public const int StartLives = 3;
    public const int HitsPerLevel = 10;
    public const int PerfectPoints = 3;
    public const int GoodPoints = 1;
    private const float SpawnY = -TileHeight;
    private const float Spacing = 0.315f;
    private readonly BeatTile[] tiles = new BeatTile[Capacity];
    private GameRandom random;
    private ComboMeter combo = ComboMeter.Create();
    private float spawnTimer;
    private int hitsThisLevel;
    private int lastLane;

    public BeatState State { get; private set; }

    public int Count { get; private set; }

    public int Score { get; private set; }

    public int Lives { get; private set; }

    public int Level { get; private set; }

    public float Speed { get; private set; }

    public int MissedLane { get; private set; }

    public int BestCombo { get; private set; }

    public int Hits { get; private set; }

    public int Perfects { get; private set; }

    public bool SpawnedThisStep { get; private set; }

    public ComboMeter Combo => combo;

    public int Multiplier => combo.Multiplier;

    public BeatTile Tile(int index) => tiles[index];

    public static float LaneCenter(int lane, float laneWidth) => (lane + 0.5f) * laneWidth;

    public void Reset(GameRandom seededRandom)
    {
        random = seededRandom;
        State = BeatState.Playing;
        Count = 0;
        Score = 0;
        Lives = StartLives;
        Level = 1;
        Speed = StartSpeed;
        MissedLane = -1;
        BestCombo = 0;
        Hits = 0;
        Perfects = 0;
        SpawnedThisStep = false;
        hitsThisLevel = 0;
        lastLane = -1;
        spawnTimer = 0f;
        combo.Reset();
        Spawn();
    }

    public BeatJudgement Step(float deltaSeconds)
    {
        MissedLane = -1;
        SpawnedThisStep = false;
        if (State != BeatState.Playing || deltaSeconds <= 0f)
        {
            return BeatJudgement.None;
        }

        combo.Update(deltaSeconds);
        spawnTimer -= deltaSeconds;
        if (spawnTimer <= 0f)
        {
            Spawn();
            SpawnedThisStep = true;
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
            combo.Reset();
            return BeatJudgement.Wrong;
        }

        var perfect = bestDistance <= PerfectWindow;
        RemoveAt(bestIndex);
        var multiplier = combo.Hit();
        Score += (perfect ? PerfectPoints : GoodPoints) * multiplier;
        BestCombo = Math.Max(BestCombo, combo.Count);
        Hits++;
        if (perfect)
        {
            Perfects++;
        }

        hitsThisLevel++;
        if (hitsThisLevel >= HitsPerLevel)
        {
            hitsThisLevel = 0;
            Level++;
            Speed = MathF.Min(MaxSpeed, Speed * SpeedStep);
        }

        return perfect ? BeatJudgement.Perfect : BeatJudgement.Good;
    }

    internal void PlaceTile(int lane, float y)
    {
        if (Count >= Capacity)
        {
            return;
        }

        ref var tile = ref tiles[Count];
        tile.Lane = lane;
        tile.Y = y;
        Count++;
    }

    internal void ClearTiles()
    {
        Count = 0;
    }

    private void LoseLife()
    {
        combo.Reset();
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
