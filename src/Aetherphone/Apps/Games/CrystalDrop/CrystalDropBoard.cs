namespace Aetherphone.Apps.Games.CrystalDrop;

internal struct Crystal
{
    public Vector2 Position;
    public Vector2 Velocity;
    public float Age;
    public float Pop;
    public int Tier;
    public bool Alive;
}

internal struct CrystalMerge
{
    public Vector2 Position;
    public int Tier;
    public int Points;
    public int Chain;
    public bool Cleared;
}

internal sealed class CrystalDropBoard
{
    public const float JarHeight = 1.32f;
    public const float DangerLine = 0.15f;
    public const int MaxTier = 10;
    public const int Capacity = 96;
    private const int MergeCapacity = 24;
    private const int SpawnTiers = 5;
    private const float Gravity = 3.2f;
    private const float FixedStep = 1f / 120f;
    private const int MaxStepsPerFrame = 8;
    private const float Restitution = 0.14f;
    private const float Damping = 0.55f;
    private const float FloorFriction = 0.88f;
    private const float SeparationBias = 0.82f;
    private const float SpawnGrace = 0.6f;
    private const float OverflowLimit = 1.7f;
    private const float DropCooldown = 0.28f;
    private const float ChainWindow = 0.5f;
    private const int MaxChain = 5;
    private const int ClearPoints = 120;
    private static readonly float[] Radii =
    {
        0.052f, 0.062f, 0.073f, 0.087f, 0.103f, 0.122f, 0.144f, 0.171f, 0.202f, 0.240f, 0.284f,
    };

    private readonly Crystal[] crystals = new Crystal[Capacity];
    private readonly CrystalMerge[] merges = new CrystalMerge[MergeCapacity];
    private readonly Random random = new();
    private float accumulator;
    private float cooldown;
    private float chainTimer;
    private int chain;
    public int Count { get; private set; }
    public int MergeCount { get; private set; }
    public int Score { get; private set; }
    public int HeldTier { get; private set; }
    public int NextTier { get; private set; }
    public float OverflowSeconds { get; private set; }
    public bool GameOver { get; private set; }
    public bool CanDrop => !GameOver && cooldown <= 0f && Count < Capacity;
    public float DropProgress => cooldown <= 0f ? 1f : 1f - cooldown / DropCooldown;
    public float OverflowFraction => OverflowSeconds / OverflowLimit;
    public Crystal At(int index) => crystals[index];
    public CrystalMerge Merge(int index) => merges[index];
    public static float RadiusOf(int tier) => Radii[tier];

    public void Reset()
    {
        Count = 0;
        MergeCount = 0;
        Score = 0;
        OverflowSeconds = 0f;
        GameOver = false;
        accumulator = 0f;
        cooldown = 0f;
        chainTimer = 0f;
        chain = 0;
        HeldTier = RollTier();
        NextTier = RollTier();
    }

    public void ClearMerges()
    {
        MergeCount = 0;
    }

    public float ClampDropX(float x)
    {
        var radius = Radii[HeldTier];
        return Math.Clamp(x, radius, 1f - radius);
    }

    public bool Drop(float x)
    {
        if (!CanDrop)
        {
            return false;
        }

        Add(new Vector2(ClampDropX(x), -Radii[HeldTier] * 0.4f), new Vector2(0f, 0.35f), HeldTier);
        HeldTier = NextTier;
        NextTier = RollTier();
        cooldown = DropCooldown;
        return true;
    }

    public void Step(float deltaSeconds)
    {
        if (cooldown > 0f)
        {
            cooldown = MathF.Max(0f, cooldown - deltaSeconds);
        }

        if (chainTimer > 0f)
        {
            chainTimer = MathF.Max(0f, chainTimer - deltaSeconds);
            if (chainTimer <= 0f)
            {
                chain = 0;
            }
        }

        for (var index = 0; index < Count; index++)
        {
            ref var crystal = ref crystals[index];
            crystal.Pop = MathF.Max(0f, crystal.Pop - deltaSeconds * 3.4f);
        }

        if (GameOver)
        {
            return;
        }

        accumulator += deltaSeconds;
        var steps = 0;
        while (accumulator >= FixedStep && steps < MaxStepsPerFrame)
        {
            accumulator -= FixedStep;
            steps++;
            Integrate(FixedStep);
            ResolveContacts();
            Compact();
        }

        if (accumulator > FixedStep * MaxStepsPerFrame)
        {
            accumulator = 0f;
        }

        UpdateOverflow(deltaSeconds);
    }

    private void Integrate(float step)
    {
        for (var index = 0; index < Count; index++)
        {
            ref var crystal = ref crystals[index];
            var radius = Radii[crystal.Tier];
            crystal.Age += step;
            crystal.Velocity.Y += Gravity * step;
            crystal.Velocity *= MathF.Max(0f, 1f - Damping * step);
            crystal.Position += crystal.Velocity * step;
            if (crystal.Position.X < radius)
            {
                crystal.Position.X = radius;
                crystal.Velocity.X = MathF.Abs(crystal.Velocity.X) * Restitution;
            }
            else if (crystal.Position.X > 1f - radius)
            {
                crystal.Position.X = 1f - radius;
                crystal.Velocity.X = -MathF.Abs(crystal.Velocity.X) * Restitution;
            }

            var floor = JarHeight - radius;
            if (crystal.Position.Y > floor)
            {
                crystal.Position.Y = floor;
                crystal.Velocity.Y = -MathF.Abs(crystal.Velocity.Y) * Restitution;
                crystal.Velocity.X *= FloorFriction;
            }
        }
    }

    private void ResolveContacts()
    {
        for (var first = 0; first < Count; first++)
        {
            if (!crystals[first].Alive)
            {
                continue;
            }

            for (var second = first + 1; second < Count; second++)
            {
                if (!crystals[first].Alive)
                {
                    break;
                }

                if (!crystals[second].Alive)
                {
                    continue;
                }

                ref var a = ref crystals[first];
                ref var b = ref crystals[second];
                var radiusA = Radii[a.Tier];
                var radiusB = Radii[b.Tier];
                var sum = radiusA + radiusB;
                var delta = b.Position - a.Position;
                var distanceSquared = delta.X * delta.X + delta.Y * delta.Y;
                if (distanceSquared >= sum * sum)
                {
                    continue;
                }

                var distance = MathF.Sqrt(distanceSquared);
                var normal = distance > 0.0001f ? delta / distance : new Vector2(0f, 1f);
                if (a.Tier == b.Tier)
                {
                    MergePair(first, second, normal);
                    continue;
                }

                var massA = radiusA * radiusA;
                var massB = radiusB * radiusB;
                var total = massA + massB;
                var penetration = (sum - distance) * SeparationBias;
                a.Position -= normal * (penetration * massB / total);
                b.Position += normal * (penetration * massA / total);
                var relative = Vector2.Dot(b.Velocity - a.Velocity, normal);
                if (relative >= 0f)
                {
                    continue;
                }

                var impulse = -(1f + Restitution) * relative / (1f / massA + 1f / massB);
                a.Velocity -= normal * (impulse / massA);
                b.Velocity += normal * (impulse / massB);
            }
        }
    }

    private void MergePair(int first, int second, Vector2 normal)
    {
        ref var a = ref crystals[first];
        ref var b = ref crystals[second];
        var tier = a.Tier;
        var position = (a.Position + b.Position) * 0.5f;
        var velocity = (a.Velocity + b.Velocity) * 0.5f;
        a.Alive = false;
        b.Alive = false;
        chain = chainTimer > 0f ? Math.Min(MaxChain, chain + 1) : 1;
        chainTimer = ChainWindow;
        var cleared = tier >= MaxTier;
        var points = cleared ? ClearPoints : (tier + 1) * (tier + 2) / 2;
        points *= chain;
        Score += points;
        AddMerge(position, cleared ? tier : tier + 1, points, cleared);
        if (cleared)
        {
            return;
        }

        Add(position, velocity - normal * 0.05f, tier + 1, 1f);
    }

    private void AddMerge(Vector2 position, int tier, int points, bool cleared)
    {
        if (MergeCount >= MergeCapacity)
        {
            return;
        }

        ref var merge = ref merges[MergeCount];
        merge.Position = position;
        merge.Tier = tier;
        merge.Points = points;
        merge.Chain = chain;
        merge.Cleared = cleared;
        MergeCount++;
    }

    private void Add(Vector2 position, Vector2 velocity, int tier, float pop = 0f)
    {
        if (Count >= Capacity)
        {
            return;
        }

        ref var crystal = ref crystals[Count];
        crystal.Position = position;
        crystal.Velocity = velocity;
        crystal.Tier = tier;
        crystal.Age = 0f;
        crystal.Pop = pop;
        crystal.Alive = true;
        Count++;
    }

    private void Compact()
    {
        for (var index = Count - 1; index >= 0; index--)
        {
            if (crystals[index].Alive)
            {
                continue;
            }

            crystals[index] = crystals[Count - 1];
            Count--;
        }
    }

    private void UpdateOverflow(float deltaSeconds)
    {
        var overflowing = false;
        for (var index = 0; index < Count; index++)
        {
            ref readonly var crystal = ref crystals[index];
            if (crystal.Age < SpawnGrace)
            {
                continue;
            }

            if (crystal.Position.Y - Radii[crystal.Tier] < DangerLine)
            {
                overflowing = true;
                break;
            }
        }

        if (!overflowing)
        {
            OverflowSeconds = MathF.Max(0f, OverflowSeconds - deltaSeconds * 2.2f);
            return;
        }

        OverflowSeconds += deltaSeconds;
        if (OverflowSeconds >= OverflowLimit)
        {
            GameOver = true;
        }
    }

    private int RollTier()
    {
        var roll = random.Next(100);
        if (roll < 30)
        {
            return 0;
        }

        if (roll < 56)
        {
            return 1;
        }

        if (roll < 78)
        {
            return 2;
        }

        if (roll < 92)
        {
            return 3;
        }

        return SpawnTiers - 1;
    }
}
