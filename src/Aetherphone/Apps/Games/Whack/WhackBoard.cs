using Aetherphone.Core.Animation;

namespace Aetherphone.Apps.Games.Whack;

internal enum WhackResult
{
    None,
    Mole,
    Bomb,
}

internal enum Occupant : byte
{
    None,
    Mole,
    Bomb,
}

internal sealed class WhackBoard
{
    public const int Columns = 3;
    public const int Rows = 3;
    public const int HoleCount = Columns * Rows;
    public const float GameDuration = 60f;
    private const float WhackThreshold = 0.3f;
    private readonly Occupant[] kind = new Occupant[HoleCount];
    private readonly float[] age = new float[HoleCount];
    private readonly float[] life = new float[HoleCount];
    private readonly bool[] whacked = new bool[HoleCount];
    private readonly Random random = new();
    private float spawnTimer;
    public int Score { get; private set; }
    public int Combo { get; private set; }
    public float TimeLeft { get; private set; }
    public bool Over { get; private set; }
    public Occupant KindAt(int hole) => kind[hole];
    public bool WhackedAt(int hole) => whacked[hole];

    public float HeightAt(int hole)
    {
        if (kind[hole] == Occupant.None)
        {
            return 0f;
        }

        PhaseDurations(life[hole], out var rise, out var hold, out var fall);
        var current = age[hole];
        if (current < rise)
        {
            return Easing.EaseOutCubic(current / rise);
        }

        if (current < rise + hold)
        {
            return 1f;
        }

        var fallProgress = (current - rise - hold) / fall;
        return MathF.Max(0f, 1f - Easing.EaseInCubic(fallProgress));
    }

    public void Reset()
    {
        Array.Clear(kind, 0, HoleCount);
        Array.Clear(age, 0, HoleCount);
        Array.Clear(whacked, 0, HoleCount);
        Score = 0;
        Combo = 0;
        TimeLeft = GameDuration;
        Over = false;
        spawnTimer = 0.6f;
    }

    public int Step(float deltaSeconds)
    {
        if (Over)
        {
            return 0;
        }

        TimeLeft -= deltaSeconds;
        if (TimeLeft <= 0f)
        {
            TimeLeft = 0f;
            Over = true;
            return 0;
        }

        var escaped = 0;
        for (var hole = 0; hole < HoleCount; hole++)
        {
            if (kind[hole] == Occupant.None)
            {
                continue;
            }

            age[hole] += deltaSeconds;
            if (age[hole] < life[hole])
            {
                continue;
            }

            if (kind[hole] == Occupant.Mole && !whacked[hole])
            {
                Combo = 0;
                escaped++;
            }

            kind[hole] = Occupant.None;
        }

        spawnTimer -= deltaSeconds;
        if (spawnTimer <= 0f)
        {
            Spawn();
            spawnTimer = SpawnInterval() * (0.8f + (float)random.NextDouble() * 0.4f);
        }

        return escaped;
    }

    public WhackResult Whack(int hole)
    {
        if (Over || kind[hole] == Occupant.None || whacked[hole] || HeightAt(hole) < WhackThreshold)
        {
            return WhackResult.None;
        }

        PhaseDurations(life[hole], out var rise, out var hold, out _);
        whacked[hole] = true;
        age[hole] = rise + hold;
        if (kind[hole] == Occupant.Bomb)
        {
            Combo = 0;
            Score = Math.Max(0, Score - 30);
            TimeLeft = MathF.Max(0f, TimeLeft - 2f);
            return WhackResult.Bomb;
        }

        Combo = Math.Min(Combo + 1, 10);
        Score += 10 * Combo;
        return WhackResult.Mole;
    }

    private void Spawn()
    {
        Span<int> empties = stackalloc int[HoleCount];
        var count = 0;
        for (var hole = 0; hole < HoleCount; hole++)
        {
            if (kind[hole] == Occupant.None)
            {
                empties[count++] = hole;
            }
        }

        if (count == 0)
        {
            return;
        }

        var target = empties[random.Next(count)];
        kind[target] = random.NextDouble() < BombChance() ? Occupant.Bomb : Occupant.Mole;
        life[target] = MoleLife();
        age[target] = 0f;
        whacked[target] = false;
    }

    private float Elapsed => GameDuration - TimeLeft;
    private float SpawnInterval() => Math.Clamp(0.85f - Elapsed * 0.009f, 0.34f, 0.85f);
    private float MoleLife() => Math.Clamp(1.5f - Elapsed * 0.012f, 0.75f, 1.5f);
    private double BombChance() => Math.Clamp(0.08f + Elapsed * 0.0025f, 0.08f, 0.26f);

    private static void PhaseDurations(float total, out float rise, out float hold, out float fall)
    {
        rise = total * 0.16f;
        fall = total * 0.22f;
        hold = total - rise - fall;
    }
}
