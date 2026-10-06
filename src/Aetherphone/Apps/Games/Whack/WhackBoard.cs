using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core.Animation;

namespace Aetherphone.Apps.Games.Whack;

internal enum WhackResult : byte
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
    public const float RoundSeconds = 60f;
    public const int MolePoints = 10;
    public const int BombPenalty = 30;
    public const float BombTimePenalty = 2f;
    public const int FrenzyCombo = 8;
    public const float FrenzySeconds = 5f;
    public const float WhackThreshold = 0.3f;
    private const float FirstSpawnDelay = 0.6f;
    private const float FrenzySpawnInterval = 0.4f;
    private const float RiseFraction = 0.16f;
    private const float FallFraction = 0.22f;
    private static readonly int[] NeighbourColumnStep = { 1, -1, 0, 0 };
    private static readonly int[] NeighbourRowStep = { 0, 0, 1, -1 };

    private readonly Occupant[] kind = new Occupant[HoleCount];
    private readonly float[] age = new float[HoleCount];
    private readonly float[] life = new float[HoleCount];
    private readonly bool[] whacked = new bool[HoleCount];
    private readonly int[] gain = new int[HoleCount];
    private GameRandom random;
    private ComboMeter combo = ComboMeter.Create();
    private float spawnTimer;
    private float frenzyLeft;

    public int Score { get; private set; }

    public int BestCombo { get; private set; }

    public int MolesWhacked { get; private set; }

    public float TimeLeft { get; private set; }

    public bool Over { get; private set; }

    public bool FrenzyStarted { get; private set; }

    public int ChainMask { get; private set; }

    public ComboMeter Combo => combo;

    public bool Frenzy => frenzyLeft > 0f;

    public float FrenzyFraction => frenzyLeft / FrenzySeconds;

    public Occupant KindAt(int hole) => kind[hole];

    public bool WhackedAt(int hole) => whacked[hole];

    public int GainAt(int hole) => gain[hole];

    public bool AliveAt(int hole) =>
        kind[hole] != Occupant.None && !whacked[hole] && HeightAt(hole) >= WhackThreshold;

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

        return MathF.Max(0f, 1f - Easing.EaseInCubic((current - rise - hold) / fall));
    }

    public float SquashAt(int hole)
    {
        if (kind[hole] == Occupant.None || !whacked[hole])
        {
            return 0f;
        }

        PhaseDurations(life[hole], out var rise, out var hold, out var fall);
        var fallProgress = (age[hole] - rise - hold) / fall;
        return Easing.EaseOutCubic(Easing.Clamp01(fallProgress * 4f));
    }

    public void Reset(GameRandom seededRandom)
    {
        random = seededRandom;
        Array.Clear(kind);
        Array.Clear(age);
        Array.Clear(life);
        Array.Clear(whacked);
        Array.Clear(gain);
        combo.Reset();
        Score = 0;
        BestCombo = 0;
        MolesWhacked = 0;
        TimeLeft = RoundSeconds;
        Over = false;
        FrenzyStarted = false;
        ChainMask = 0;
        frenzyLeft = 0f;
        spawnTimer = FirstSpawnDelay;
    }

    public void Step(float deltaSeconds)
    {
        if (Over || deltaSeconds <= 0f)
        {
            return;
        }

        TimeLeft -= deltaSeconds;
        if (TimeLeft <= 0f)
        {
            TimeLeft = 0f;
            Over = true;
            return;
        }

        combo.Update(deltaSeconds);
        frenzyLeft = MathF.Max(0f, frenzyLeft - deltaSeconds);
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
                combo.Reset();
            }

            kind[hole] = Occupant.None;
        }

        spawnTimer -= deltaSeconds;
        if (spawnTimer > 0f)
        {
            return;
        }

        Spawn();
        spawnTimer = SpawnInterval() * random.Range(0.8f, 1.2f);
    }

    public WhackResult Whack(int hole)
    {
        ChainMask = 0;
        FrenzyStarted = false;
        if (Over || !AliveAt(hole))
        {
            return WhackResult.None;
        }

        if (kind[hole] == Occupant.Bomb)
        {
            combo.Reset();
            Score = Math.Max(0, Score - BombPenalty);
            TimeLeft = MathF.Max(0f, TimeLeft - BombTimePenalty);
            Detonate(hole);
            return WhackResult.Bomb;
        }

        Hit(hole);
        return WhackResult.Mole;
    }

    public void Place(int hole, Occupant occupant)
    {
        kind[hole] = occupant;
        life[hole] = MoleLife() * random.Range(0.85f, 1.15f);
        age[hole] = 0f;
        whacked[hole] = false;
        gain[hole] = 0;
    }

    private void Hit(int hole)
    {
        Knock(hole);
        var points = MolePoints * combo.Hit() * (Frenzy ? 2 : 1);
        gain[hole] = points;
        Score += points;
        MolesWhacked++;
        BestCombo = Math.Max(BestCombo, combo.Count);
        if (Frenzy || combo.Count % FrenzyCombo != 0)
        {
            return;
        }

        frenzyLeft = FrenzySeconds;
        spawnTimer = 0f;
        FrenzyStarted = true;
    }

    private void Knock(int hole)
    {
        PhaseDurations(life[hole], out var rise, out var hold, out _);
        whacked[hole] = true;
        age[hole] = rise + hold;
        gain[hole] = 0;
    }

    private void Detonate(int hole)
    {
        Knock(hole);
        var column = hole % Columns;
        var row = hole / Columns;
        for (var side = 0; side < NeighbourColumnStep.Length; side++)
        {
            var neighbourColumn = column + NeighbourColumnStep[side];
            var neighbourRow = row + NeighbourRowStep[side];
            if (neighbourColumn < 0 || neighbourColumn >= Columns || neighbourRow < 0 || neighbourRow >= Rows)
            {
                continue;
            }

            var neighbour = neighbourRow * Columns + neighbourColumn;
            if (!AliveAt(neighbour))
            {
                continue;
            }

            ChainMask |= 1 << neighbour;
            if (kind[neighbour] == Occupant.Bomb)
            {
                Detonate(neighbour);
            }
            else
            {
                Hit(neighbour);
            }
        }
    }

    private void Spawn()
    {
        if (Frenzy)
        {
            for (var hole = 0; hole < HoleCount; hole++)
            {
                if (kind[hole] == Occupant.None)
                {
                    Place(hole, Occupant.Mole);
                }
            }

            return;
        }

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
        Place(target, random.Chance(BombChance()) ? Occupant.Bomb : Occupant.Mole);
    }

    private float Elapsed => RoundSeconds - TimeLeft;

    private float SpawnInterval() => Frenzy ? FrenzySpawnInterval : Math.Clamp(0.85f - Elapsed * 0.009f, 0.34f, 0.85f);

    private float MoleLife() => Math.Clamp(1.5f - Elapsed * 0.012f, 0.75f, 1.5f);

    private float BombChance() => Frenzy ? 0f : Math.Clamp(0.08f + Elapsed * 0.0025f, 0.08f, 0.26f);

    private static void PhaseDurations(float total, out float rise, out float hold, out float fall)
    {
        rise = total * RiseFraction;
        fall = total * FallFraction;
        hold = total - rise - fall;
    }
}
