using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games.Slice;

internal enum SliceMode : byte
{
    Classic,
    Arcade,
    Preview,
}

internal enum SliceState : byte
{
    Playing,
    Over,
}

internal enum SliceKind : byte
{
    Crystal,
    Egg,
    Moogle,
    Bomb,
    Freeze,
    Frenzy,
    Double,
}

internal struct SliceObject
{
    public SliceKind Kind;
    public byte Tint;
    public Vector2 Position;
    public Vector2 Velocity;
    public float Radius;
    public float Rotation;
    public float Spin;
}

internal struct SliceHalf
{
    public SliceKind Kind;
    public byte Tint;
    public Vector2 Position;
    public Vector2 Velocity;
    public Vector2 CutNormal;
    public float Side;
    public float Radius;
    public float Rotation;
    public float Spin;
    public float Life;
}

internal readonly struct SliceHit
{
    public readonly Vector2 Position;
    public readonly Vector2 Direction;
    public readonly SliceKind Kind;
    public readonly byte Tint;
    public readonly int Points;

    public SliceHit(Vector2 position, Vector2 direction, SliceKind kind, byte tint, int points)
    {
        Position = position;
        Direction = direction;
        Kind = kind;
        Tint = tint;
        Points = points;
    }
}

internal sealed class SliceBoard
{
    public const float WorldWidth = 9f;
    public const float WorldHeight = 16f;
    public const float Gravity = 14f;
    public const float SpawnY = WorldHeight + 2.4f;
    public const float MissY = SpawnY + 0.8f;
    public const float ApexMin = 3.2f;
    public const float ApexMax = 7.6f;
    public const int StartLives = 3;
    public const int MissesPerLife = 3;
    public const float ArcadeSeconds = 60f;
    public const int BombPenaltySeconds = 10;
    public const int ComboMinimum = 3;
    public const float MinBladeSpeed = 6f;
    public const float StrokeBreakSeconds = 0.1f;
    public const float FreezeSeconds = 5f;
    public const float FreezeScale = 0.35f;
    public const float FrenzySeconds = 4.5f;
    public const float DoubleSeconds = 7f;
    public const float HalfLife = 2.6f;
    public const float CentroidOffset = 0.42f;
    public const int CrystalTints = 6;
    private const int ObjectCapacity = 40;
    private const int HalfCapacity = 72;
    private const int PendingCapacity = 16;
    private const int HitCapacity = 24;
    private const int MissCapacity = 8;
    private const float SideMargin = 1.3f;
    private const float SplitSpeed = 1.8f;
    private const float SplitKick = 0.9f;
    private const float SplitSpin = 3.2f;
    private const float ComboWindowSeconds = 1.2f;
    private const float FrenzyInterval = 0.16f;
    private const float PickupChance = 0.16f;
    private const float PickupCooldownSeconds = 9f;
    private const float ClassicSafeSeconds = 5f;
    private const float ArcadeSafeSeconds = 3f;
    private const float PreviewInterval = 1.25f;
    private const float FirstWaveDelay = 0.4f;

    private readonly SliceObject[] objects = new SliceObject[ObjectCapacity];
    private readonly SliceHalf[] halves = new SliceHalf[HalfCapacity];
    private readonly PendingThrow[] pending = new PendingThrow[PendingCapacity];
    private readonly SliceHit[] hits = new SliceHit[HitCapacity];
    private readonly Vector2[] misses = new Vector2[MissCapacity];
    private GameRandom random;
    private ComboMeter combo = new(ComboWindowSeconds);
    private Vector2 bladePoint;
    private Vector2 lastHitPosition;
    private float waveTimer;
    private float frenzyTimer;
    private float pickupCooldown;
    private float dullSeconds;
    private int pendingCount;
    private int strokeCount;
    private bool bladeDown;
    private bool frenzyFromLeft;

    private struct PendingThrow
    {
        public float Delay;
        public SliceKind Kind;
        public byte Tint;
        public float StartX;
        public float ApexX;
        public float ApexY;
    }

    public SliceMode Mode { get; private set; }

    public SliceState State { get; private set; }

    public int Score { get; private set; }

    public int Lives { get; private set; }

    public int Misses { get; private set; }

    public float TimeLeft { get; private set; }

    public float Elapsed { get; private set; }

    public int Sliced { get; private set; }

    public int BestSwipe { get; private set; }

    public int BestStreak { get; private set; }

    public int Combos { get; private set; }

    public int BombsHit { get; private set; }

    public float FreezeLeft { get; private set; }

    public float FrenzyLeft { get; private set; }

    public float DoubleLeft { get; private set; }

    public int ObjectCount { get; private set; }

    public int HalfCount { get; private set; }

    public int HitCount { get; private set; }

    public int MissCount { get; private set; }

    public int ComboThisFrame { get; private set; }

    public int ComboBonusThisFrame { get; private set; }

    public Vector2 ComboPosition { get; private set; }

    public bool BombThisFrame { get; private set; }

    public Vector2 BombPosition { get; private set; }

    public bool LifeLostThisFrame { get; private set; }

    public bool TimeUpThisFrame { get; private set; }

    public bool WaveThisFrame { get; private set; }

    public SliceKind PickupThisFrame { get; private set; }

    public bool PickupActivated { get; private set; }

    public bool BladeSharp { get; private set; }

    public bool BladeDown => bladeDown;

    public ComboMeter Combo => combo;

    public bool Classic => Mode == SliceMode.Classic;

    public bool Arcade => Mode == SliceMode.Arcade;

    public SliceObject Object(int index) => objects[index];

    public SliceHalf Half(int index) => halves[index];

    public SliceHit Hit(int index) => hits[index];

    public Vector2 Miss(int index) => misses[index];

    public static bool IsPickup(SliceKind kind) => kind is SliceKind.Freeze or SliceKind.Frenzy or SliceKind.Double;

    public static float RadiusOf(SliceKind kind) => kind switch
    {
        SliceKind.Crystal => 0.62f,
        SliceKind.Egg => 0.58f,
        SliceKind.Moogle => 0.7f,
        SliceKind.Bomb => 0.6f,
        _ => 0.6f,
    };

    public void Reset(GameRandom seededRandom, SliceMode mode)
    {
        random = seededRandom;
        Mode = mode;
        State = SliceState.Playing;
        Score = 0;
        Lives = StartLives;
        Misses = 0;
        TimeLeft = ArcadeSeconds;
        Elapsed = 0f;
        Sliced = 0;
        BestSwipe = 0;
        BestStreak = 0;
        Combos = 0;
        BombsHit = 0;
        FreezeLeft = 0f;
        FrenzyLeft = 0f;
        DoubleLeft = 0f;
        ObjectCount = 0;
        HalfCount = 0;
        pendingCount = 0;
        waveTimer = FirstWaveDelay;
        frenzyTimer = 0f;
        pickupCooldown = PickupCooldownSeconds * 0.5f;
        strokeCount = 0;
        dullSeconds = 0f;
        bladeDown = false;
        frenzyFromLeft = false;
        combo.Reset();
        BeginFrame();
    }

    public void BeginFrame()
    {
        HitCount = 0;
        MissCount = 0;
        ComboThisFrame = 0;
        ComboBonusThisFrame = 0;
        BombThisFrame = false;
        LifeLostThisFrame = false;
        TimeUpThisFrame = false;
        WaveThisFrame = false;
        PickupActivated = false;
    }

    public int Spawn(SliceKind kind, byte tint, Vector2 position, Vector2 velocity)
    {
        if (ObjectCount >= ObjectCapacity)
        {
            return -1;
        }

        ref var item = ref objects[ObjectCount];
        item.Kind = kind;
        item.Tint = tint;
        item.Position = position;
        item.Velocity = velocity;
        item.Radius = RadiusOf(kind);
        item.Rotation = random.Range(0f, MathF.Tau);
        item.Spin = random.Range(-2.6f, 2.6f);
        return ObjectCount++;
    }

    public static Vector2 LaunchVelocity(float startX, float apexX, float apexY)
    {
        var rise = MathF.Max(0.5f, SpawnY - apexY);
        var flight = MathF.Sqrt(2f * rise / Gravity);
        return new Vector2((apexX - startX) / flight, -MathF.Sqrt(2f * Gravity * rise));
    }

    public void MoveBlade(Vector2 point, bool held, float deltaSeconds)
    {
        if (!held || State != SliceState.Playing)
        {
            if (bladeDown)
            {
                EndStroke();
            }

            bladeDown = false;
            BladeSharp = false;
            return;
        }

        if (!bladeDown)
        {
            bladeDown = true;
            bladePoint = point;
            strokeCount = 0;
            dullSeconds = 0f;
            BladeSharp = false;
            return;
        }

        if (deltaSeconds <= 0f)
        {
            return;
        }

        var speed = Vector2.Distance(point, bladePoint) / deltaSeconds;
        if (speed < MinBladeSpeed)
        {
            BladeSharp = false;
            dullSeconds += deltaSeconds;
            if (dullSeconds >= StrokeBreakSeconds && strokeCount > 0)
            {
                EndStroke();
            }

            bladePoint = point;
            return;
        }

        BladeSharp = true;
        dullSeconds = 0f;
        Cut(bladePoint, point);
        bladePoint = point;
    }

    public void Step(float deltaSeconds)
    {
        if (deltaSeconds <= 0f)
        {
            return;
        }

        FreezeLeft = MathF.Max(0f, FreezeLeft - deltaSeconds);
        FrenzyLeft = MathF.Max(0f, FrenzyLeft - deltaSeconds);
        DoubleLeft = MathF.Max(0f, DoubleLeft - deltaSeconds);
        pickupCooldown = MathF.Max(0f, pickupCooldown - deltaSeconds);
        var worldDelta = FreezeLeft > 0f ? deltaSeconds * FreezeScale : deltaSeconds;
        StepHalves(worldDelta);
        if (State != SliceState.Playing)
        {
            StepObjects(worldDelta, false);
            return;
        }

        combo.Update(deltaSeconds);
        Elapsed += worldDelta;
        if (Mode == SliceMode.Arcade && FreezeLeft <= 0f)
        {
            TimeLeft = MathF.Max(0f, TimeLeft - deltaSeconds);
            if (TimeLeft <= 0f)
            {
                TimeUpThisFrame = true;
                End();
                return;
            }
        }

        StepSchedule(worldDelta);
        StepFrenzy(deltaSeconds);
        StepObjects(worldDelta, true);
    }

    private void StepSchedule(float deltaSeconds)
    {
        waveTimer -= deltaSeconds;
        if (waveTimer <= 0f)
        {
            LaunchWave();
        }

        for (var index = pendingCount - 1; index >= 0; index--)
        {
            ref var entry = ref pending[index];
            entry.Delay -= deltaSeconds;
            if (entry.Delay > 0f)
            {
                continue;
            }

            Throw(entry.Kind, entry.Tint, entry.StartX, entry.ApexX, entry.ApexY);
            pending[index] = pending[pendingCount - 1];
            pendingCount--;
        }
    }

    private void StepFrenzy(float deltaSeconds)
    {
        if (FrenzyLeft <= 0f)
        {
            return;
        }

        frenzyTimer -= deltaSeconds;
        if (frenzyTimer > 0f)
        {
            return;
        }

        frenzyTimer = FrenzyInterval;
        frenzyFromLeft = !frenzyFromLeft;
        var side = frenzyFromLeft ? 1f : -1f;
        var startX = frenzyFromLeft ? -0.7f : WorldWidth + 0.7f;
        var position = new Vector2(startX, random.Range(9f, 13f));
        var velocity = new Vector2(side * random.Range(4.5f, 6.5f), -random.Range(8f, 11f));
        var kind = TargetKind();
        Spawn(kind, TintFor(kind), position, velocity);
    }

    private void StepObjects(float deltaSeconds, bool countMisses)
    {
        for (var index = ObjectCount - 1; index >= 0; index--)
        {
            ref var item = ref objects[index];
            item.Velocity.Y += Gravity * deltaSeconds;
            item.Position += item.Velocity * deltaSeconds;
            item.Rotation += item.Spin * deltaSeconds;
            if (item.Velocity.Y <= 0f || item.Position.Y < MissY)
            {
                continue;
            }

            var missed = item;
            RemoveObject(index);
            if (countMisses)
            {
                OnFell(missed);
            }
        }
    }

    private void StepHalves(float deltaSeconds)
    {
        for (var index = HalfCount - 1; index >= 0; index--)
        {
            ref var half = ref halves[index];
            half.Life -= deltaSeconds;
            if (half.Life <= 0f || half.Position.Y > MissY + 2f)
            {
                halves[index] = halves[HalfCount - 1];
                HalfCount--;
                continue;
            }

            half.Velocity.Y += Gravity * deltaSeconds;
            half.Position += half.Velocity * deltaSeconds;
            half.Rotation += half.Spin * deltaSeconds;
        }
    }

    private void OnFell(in SliceObject fallen)
    {
        if (State != SliceState.Playing || fallen.Kind == SliceKind.Bomb || IsPickup(fallen.Kind) ||
            Mode == SliceMode.Preview)
        {
            return;
        }

        combo.Reset();
        if (MissCount < MissCapacity)
        {
            misses[MissCount++] = new Vector2(Math.Clamp(fallen.Position.X, 0.4f, WorldWidth - 0.4f), WorldHeight);
        }

        if (Mode != SliceMode.Classic)
        {
            return;
        }

        Misses++;
        if (Misses < MissesPerLife)
        {
            return;
        }

        Misses = 0;
        Lives--;
        LifeLostThisFrame = true;
        if (Lives <= 0)
        {
            End();
        }
    }

    private void LaunchWave()
    {
        WaveThisFrame = true;
        var size = Mode == SliceMode.Preview ? 1 + random.Next(3) : 1 + random.Next(MaxExtra() + 1);
        var bombsLeft = BombsAllowed();
        var pickupSlot = -1;
        if (Mode == SliceMode.Arcade && pickupCooldown <= 0f && !PowerActive && random.Chance(PickupChance))
        {
            pickupSlot = random.Next(size);
            pickupCooldown = PickupCooldownSeconds;
        }

        var pattern = random.Next(3);
        for (var slot = 0; slot < size; slot++)
        {
            if (pendingCount >= PendingCapacity)
            {
                break;
            }

            SliceKind kind;
            if (slot == pickupSlot)
            {
                kind = (SliceKind)((int)SliceKind.Freeze + random.Next(3));
            }
            else if (bombsLeft > 0 && random.Chance(BombChance()))
            {
                kind = SliceKind.Bomb;
                bombsLeft--;
            }
            else
            {
                kind = TargetKind();
            }

            ref var entry = ref pending[pendingCount++];
            entry.Kind = kind;
            entry.Tint = TintFor(kind);
            entry.ApexY = random.Range(ApexMin, ApexMax);
            switch (pattern)
            {
                case 1:
                {
                    var spread = size <= 1 ? 0.5f : slot / (float)(size - 1);
                    entry.StartX = WorldWidth * 0.5f + random.Range(-1f, 1f);
                    entry.ApexX = SideMargin + spread * (WorldWidth - SideMargin * 2f);
                    entry.Delay = random.Range(0f, 0.12f);
                    break;
                }
                case 2:
                {
                    var spread = size <= 1 ? 0.5f : slot / (float)(size - 1);
                    entry.StartX = SideMargin + spread * (WorldWidth - SideMargin * 2f);
                    entry.ApexX = Math.Clamp(entry.StartX + random.Range(-0.8f, 0.8f), SideMargin, WorldWidth - SideMargin);
                    entry.Delay = slot * 0.18f;
                    break;
                }
                default:
                    entry.StartX = random.Range(SideMargin, WorldWidth - SideMargin);
                    entry.ApexX = Math.Clamp(entry.StartX + random.Range(-2.4f, 2.4f), SideMargin, WorldWidth - SideMargin);
                    entry.Delay = random.Range(0f, 0.32f);
                    break;
            }
        }

        waveTimer = WaveInterval() + random.Range(0f, 0.25f);
    }

    private void Throw(SliceKind kind, byte tint, float startX, float apexX, float apexY)
    {
        Spawn(kind, tint, new Vector2(startX, SpawnY), LaunchVelocity(startX, apexX, apexY));
    }

    private SliceKind TargetKind()
    {
        var roll = random.Next(100);
        if (roll < 52)
        {
            return SliceKind.Crystal;
        }

        return roll < 78 ? SliceKind.Egg : SliceKind.Moogle;
    }

    private byte TintFor(SliceKind kind) => kind == SliceKind.Crystal ? (byte)random.Next(CrystalTints) : (byte)0;

    private bool PowerActive => FreezeLeft > 0f || FrenzyLeft > 0f || DoubleLeft > 0f;

    private int MaxExtra() => Mode == SliceMode.Arcade
        ? Math.Min(4, 2 + (int)(Elapsed / 15f))
        : Math.Min(4, 1 + (int)(Elapsed / 15f));

    private float WaveInterval() => Mode switch
    {
        SliceMode.Arcade => MathF.Max(0.85f, 1.5f - Elapsed * 0.011f),
        SliceMode.Classic => MathF.Max(1f, 1.9f - Elapsed * 0.008f),
        _ => PreviewInterval,
    };

    private int BombsAllowed() => Mode switch
    {
        SliceMode.Arcade => Elapsed < ArcadeSafeSeconds ? 0 : 2,
        SliceMode.Classic => Elapsed < ClassicSafeSeconds ? 0 : Elapsed < 40f ? 1 : 2,
        _ => 0,
    };

    private float BombChance() => Mode == SliceMode.Arcade
        ? MathF.Min(0.25f, 0.12f + Elapsed * 0.002f)
        : MathF.Min(0.22f, 0.08f + Elapsed * 0.0012f);

    private void Cut(Vector2 from, Vector2 to)
    {
        var travel = to - from;
        var direction = travel.LengthSquared() > 0.000001f ? Vector2.Normalize(travel) : Vector2.UnitX;
        Span<int> hitIndices = stackalloc int[ObjectCapacity];
        Span<float> hitAlong = stackalloc float[ObjectCapacity];
        Span<SliceObject> cut = stackalloc SliceObject[ObjectCapacity];
        var count = 0;
        for (var index = 0; index < ObjectCount; index++)
        {
            ref readonly var item = ref objects[index];
            if (!Geometry2D.SegmentCircle(from, to, item.Position, item.Radius))
            {
                continue;
            }

            hitIndices[count] = index;
            hitAlong[count] = Vector2.Dot(item.Position - from, direction);
            cut[count] = item;
            count++;
        }

        for (var hit = count - 1; hit >= 0; hit--)
        {
            RemoveObject(hitIndices[hit]);
        }

        SortAlong(cut[..count], hitAlong[..count]);
        for (var hit = 0; hit < count; hit++)
        {
            Resolve(cut[hit], direction);
        }
    }

    private static void SortAlong(Span<SliceObject> cut, Span<float> along)
    {
        for (var index = 1; index < cut.Length; index++)
        {
            var item = cut[index];
            var key = along[index];
            var slot = index - 1;
            while (slot >= 0 && along[slot] > key)
            {
                cut[slot + 1] = cut[slot];
                along[slot + 1] = along[slot];
                slot--;
            }

            cut[slot + 1] = item;
            along[slot + 1] = key;
        }
    }

    private void Resolve(in SliceObject item, Vector2 direction)
    {
        if (item.Kind == SliceKind.Bomb)
        {
            if (State == SliceState.Playing)
            {
                OnBomb(item.Position);
            }

            return;
        }

        Split(item, direction);
        if (State != SliceState.Playing)
        {
            return;
        }

        if (IsPickup(item.Kind))
        {
            Activate(item.Kind);
        }

        Score += Award(direction, item);
    }

    private int Award(Vector2 direction, in SliceObject item)
    {
        var multiplier = combo.Hit();
        BestStreak = Math.Max(BestStreak, combo.Count);
        var points = multiplier * (DoubleLeft > 0f ? 2 : 1);
        Sliced++;
        strokeCount++;
        lastHitPosition = item.Position;
        if (HitCount < HitCapacity)
        {
            hits[HitCount++] = new SliceHit(item.Position, direction, item.Kind, item.Tint, points);
        }

        return points;
    }

    private void Activate(SliceKind kind)
    {
        PickupThisFrame = kind;
        PickupActivated = true;
        switch (kind)
        {
            case SliceKind.Freeze:
                FreezeLeft = FreezeSeconds;
                break;
            case SliceKind.Frenzy:
                FrenzyLeft = FrenzySeconds;
                frenzyTimer = 0f;
                break;
            default:
                DoubleLeft = DoubleSeconds;
                break;
        }
    }

    private void OnBomb(Vector2 position)
    {
        BombThisFrame = true;
        BombPosition = position;
        BombsHit++;
        combo.Reset();
        strokeCount = 0;
        if (Mode == SliceMode.Classic)
        {
            End();
            return;
        }

        TimeLeft = MathF.Max(0f, TimeLeft - BombPenaltySeconds);
        if (TimeLeft > 0f)
        {
            return;
        }

        TimeUpThisFrame = true;
        End();
    }

    private void Split(in SliceObject item, Vector2 direction)
    {
        var normal = new Vector2(-direction.Y, direction.X);
        var cosine = MathF.Cos(-item.Rotation);
        var sine = MathF.Sin(-item.Rotation);
        var localNormal = new Vector2(normal.X * cosine - normal.Y * sine, normal.X * sine + normal.Y * cosine);
        for (var side = -1; side <= 1; side += 2)
        {
            if (HalfCount >= HalfCapacity)
            {
                return;
            }

            ref var half = ref halves[HalfCount++];
            half.Kind = item.Kind;
            half.Tint = item.Tint;
            half.Radius = item.Radius;
            half.Side = side;
            half.CutNormal = localNormal;
            half.Position = item.Position + normal * (side * item.Radius * CentroidOffset);
            half.Velocity = item.Velocity * 0.55f + normal * (side * SplitSpeed) + new Vector2(0f, -SplitKick);
            half.Rotation = item.Rotation;
            half.Spin = item.Spin * 0.5f + side * SplitSpin;
            half.Life = HalfLife;
        }
    }

    private void EndStroke()
    {
        BestSwipe = Math.Max(BestSwipe, strokeCount);
        if (strokeCount >= ComboMinimum && State == SliceState.Playing)
        {
            var bonus = strokeCount * (DoubleLeft > 0f ? 2 : 1);
            Score += bonus;
            Combos++;
            ComboThisFrame = strokeCount;
            ComboBonusThisFrame = bonus;
            ComboPosition = lastHitPosition;
        }

        strokeCount = 0;
        dullSeconds = 0f;
    }

    private void End()
    {
        if (bladeDown)
        {
            EndStroke();
        }

        bladeDown = false;
        BladeSharp = false;
        pendingCount = 0;
        State = SliceState.Over;
    }

    private void RemoveObject(int index)
    {
        objects[index] = objects[ObjectCount - 1];
        ObjectCount--;
    }
}
