using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Framework.Physics;

namespace Aetherphone.Apps.Games.Snip;

internal enum SnipState : byte
{
    Playing,
    Eaten,
    Lost,
}

internal enum SnipLoss : byte
{
    None,
    Spikes,
    Fell,
}

internal enum SnipTap : byte
{
    None,
    Pop,
    Puff,
}

internal enum BubbleState : byte
{
    Free,
    Holding,
    Popped,
}

internal sealed class SnipBoard
{
    public const float WorldWidth = 6f;
    public const float WorldHeight = 10f;
    public const float CandyRadius = 0.26f;
    public const float StarRadius = 0.3f;
    public const float BubbleRadius = 0.5f;
    public const float CushionRadius = 0.42f;
    public const float CushionReach = 3.4f;
    public const float MouthRadius = 0.5f;
    public const float SenseRadius = 1.9f;
    public const float SpikeHalfWidth = 0.12f;
    public const float AnchorRadius = 0.12f;
    public const float PuffSeconds = 0.45f;
    private const float TapSlack = 0.25f;
    private const float SegmentLength = 0.24f;
    private const int MinSegments = 4;
    private const int MaxSegments = 24;
    private const float AirDamping = 0.05f;
    private const float BubbleGravityScale = -0.36f;
    private const float BubbleDamping = 1.6f;
    private const float BubbleCatch = 0.8f;
    private const float CaptureKeep = 0.3f;
    private const float PuffSpeed = 5.4f;
    private const float PuffFalloff = 0.55f;
    private const float PuffConeCosine = 0.72f;
    private const float FallMargin = 1.2f;
    private const float BlinkMinSeconds = 1.8f;
    private const float BlinkMaxSeconds = 4.6f;
    private const float BlinkSeconds = 0.14f;
    private static readonly PhysicsMaterial CandyMaterial = new(1f, 0f, 0.2f);

    private readonly PhysicsWorld world = new();
    private readonly int[] ropes = new int[SnipLevel.MaxRopes];
    private readonly BubbleState[] bubbleStates = new BubbleState[SnipLevel.MaxBubbles];
    private readonly float[] puffAges = new float[SnipLevel.MaxCushions];
    private SnipLevel? level;
    private GameRandom random;
    private Vector2 previous;
    private int candy;
    private int ropeCount;
    private int heldBubble = -1;
    private float blinkTimer;
    private float blinkLeft;

    public SnipState State { get; private set; }

    public SnipLoss Loss { get; private set; }

    public int StarMask { get; private set; }

    public float Elapsed { get; private set; }

    public int Cuts { get; private set; }

    public float MouthOpen { get; private set; }

    public Vector2 EndPosition { get; private set; }

    public int CutThisFrame { get; private set; }

    public Vector2 CutPoint { get; private set; }

    public bool FreedThisFrame { get; private set; }

    public int StarsThisFrame { get; private set; }

    public int CapturedThisFrame { get; private set; } = -1;

    public bool PoppedThisFrame { get; private set; }

    public int PuffedThisFrame { get; private set; } = -1;

    public bool PuffHit { get; private set; }

    public bool EatenThisFrame { get; private set; }

    public bool LostThisFrame { get; private set; }

    public SnipLevel Level => level!;

    public PhysicsWorld World => world;

    public int RopeCount => ropeCount;

    public int Candy => candy;

    public int HeldBubble => heldBubble;

    public bool InBubble => heldBubble >= 0;

    public bool Blinking => blinkLeft > 0f;

    public int StarCount => BitOperations.PopCount((uint)StarMask);

    public Vector2 CandyPosition => world.Position(candy);

    public Vector2 CandyRenderPosition => world.RenderPosition(candy);

    public float CandyAngle => world.RenderAngle(candy);

    public int Rope(int index) => ropes[index];

    public bool RopeHolds(int index) => world.RopeHolds(ropes[index]);

    public BubbleState Bubble(int index) => bubbleStates[index];

    public float PuffAge(int index) => puffAges[index];

    public bool StarTaken(int index) => (StarMask & (1 << index)) != 0;

    public void Load(SnipLevel source, GameRandom seed)
    {
        level = source;
        random = seed;
        world.Clear();
        candy = world.CreateCircle(BodyType.Dynamic, source.Candy, CandyRadius, CandyMaterial);
        world.SetDamping(candy, AirDamping, AirDamping);
        ropeCount = source.Ropes.Length;
        for (var index = 0; index < ropeCount; index++)
        {
            var rope = source.Ropes[index];
            var segments = Math.Clamp((int)MathF.Round(rope.Length / SegmentLength), MinSegments, MaxSegments);
            ropes[index] = world.CreateRope(rope.Anchor, candy, segments, rope.Length);
        }

        for (var index = 0; index < bubbleStates.Length; index++)
        {
            bubbleStates[index] = BubbleState.Free;
        }

        for (var index = 0; index < puffAges.Length; index++)
        {
            puffAges[index] = PuffSeconds;
        }

        previous = source.Candy;
        heldBubble = -1;
        State = SnipState.Playing;
        Loss = SnipLoss.None;
        StarMask = 0;
        Elapsed = 0f;
        Cuts = 0;
        MouthOpen = 0f;
        EndPosition = source.Candy;
        blinkLeft = 0f;
        blinkTimer = random.Range(BlinkMinSeconds, BlinkMaxSeconds);
        BeginFrame();
    }

    public void BeginFrame()
    {
        CutThisFrame = 0;
        FreedThisFrame = false;
        StarsThisFrame = 0;
        CapturedThisFrame = -1;
        PoppedThisFrame = false;
        PuffedThisFrame = -1;
        PuffHit = false;
        EatenThisFrame = false;
        LostThisFrame = false;
    }

    public void Step(float deltaSeconds)
    {
        if (deltaSeconds <= 0f || level is null)
        {
            return;
        }

        AdvanceBlink(deltaSeconds);
        for (var index = 0; index < puffAges.Length; index++)
        {
            puffAges[index] = MathF.Min(PuffSeconds, puffAges[index] + deltaSeconds);
        }

        world.Step(deltaSeconds);
        if (State != SnipState.Playing)
        {
            MouthOpen = MathF.Max(0f, MouthOpen - deltaSeconds * 4f);
            return;
        }

        Elapsed += deltaSeconds;
        var current = world.Position(candy);
        CollectStars(previous, current);
        CatchBubble(previous, current);
        UpdateMouth(current);
        if (TouchesSpikes(previous, current))
        {
            Lose(SnipLoss.Spikes, current);
        }
        else if (Geometry2D.SegmentCircle(previous, current, level.Moogle, MouthRadius))
        {
            Eat(current);
        }
        else if (OutOfBounds(current))
        {
            Lose(SnipLoss.Fell, current);
        }

        previous = current;
    }

    public int Cut(Vector2 from, Vector2 to)
    {
        if (level is null || from == to)
        {
            return 0;
        }

        var wasHeld = AnyRopeHolds();
        var cut = world.CutRopes(from, to);
        if (cut == 0)
        {
            return 0;
        }

        Cuts += cut;
        CutThisFrame += cut;
        CutPoint = to;
        if (State == SnipState.Playing && wasHeld && !AnyRopeHolds())
        {
            FreedThisFrame = true;
        }

        return cut;
    }

    public bool AnyRopeHolds()
    {
        for (var index = 0; index < ropeCount; index++)
        {
            if (world.RopeHolds(ropes[index]))
            {
                return true;
            }
        }

        return false;
    }

    public SnipTap Tap(Vector2 point)
    {
        if (level is null || State != SnipState.Playing)
        {
            return SnipTap.None;
        }

        if (heldBubble >= 0 && Vector2.Distance(point, world.Position(candy)) <= BubbleRadius + TapSlack)
        {
            Pop();
            return SnipTap.Pop;
        }

        for (var index = 0; index < level.Cushions.Length; index++)
        {
            if (Vector2.Distance(point, level.Cushions[index].Position) > CushionRadius + TapSlack)
            {
                continue;
            }

            Puff(index);
            return SnipTap.Puff;
        }

        return SnipTap.None;
    }

    public void Pop()
    {
        if (heldBubble < 0)
        {
            return;
        }

        bubbleStates[heldBubble] = BubbleState.Popped;
        heldBubble = -1;
        PoppedThisFrame = true;
        world.SetGravityScale(candy, 1f);
        world.SetDamping(candy, AirDamping, AirDamping);
    }

    public void Puff(int index)
    {
        if (level is null || index < 0 || index >= level.Cushions.Length)
        {
            return;
        }

        var cushion = level.Cushions[index];
        puffAges[index] = 0f;
        PuffedThisFrame = index;
        var offset = world.Position(candy) - cushion.Position;
        var distance = offset.Length();
        if (State != SnipState.Playing || distance > CushionReach || distance <= 0.0001f ||
            Vector2.Dot(offset / distance, cushion.Direction) < PuffConeCosine)
        {
            return;
        }

        var strength = PuffSpeed * (1f - PuffFalloff * distance / CushionReach);
        world.ApplyImpulse(candy, cushion.Direction * strength * world.Mass(candy));
        PuffHit = true;
    }

    private void AdvanceBlink(float deltaSeconds)
    {
        if (blinkLeft > 0f)
        {
            blinkLeft -= deltaSeconds;
            return;
        }

        blinkTimer -= deltaSeconds;
        if (blinkTimer > 0f)
        {
            return;
        }

        blinkLeft = BlinkSeconds;
        blinkTimer = random.Range(BlinkMinSeconds, BlinkMaxSeconds);
    }

    private void CollectStars(Vector2 from, Vector2 to)
    {
        var stars = level!.Stars;
        for (var index = 0; index < stars.Length; index++)
        {
            var bit = 1 << index;
            if ((StarMask & bit) != 0 || !Geometry2D.SegmentCircle(from, to, stars[index], StarRadius + CandyRadius))
            {
                continue;
            }

            StarMask |= bit;
            StarsThisFrame |= bit;
        }
    }

    private void CatchBubble(Vector2 from, Vector2 to)
    {
        if (heldBubble >= 0)
        {
            return;
        }

        var bubbles = level!.Bubbles;
        for (var index = 0; index < bubbles.Length; index++)
        {
            if (bubbleStates[index] != BubbleState.Free ||
                !Geometry2D.SegmentCircle(from, to, bubbles[index], BubbleRadius * BubbleCatch))
            {
                continue;
            }

            bubbleStates[index] = BubbleState.Holding;
            heldBubble = index;
            CapturedThisFrame = index;
            world.SetVelocity(candy, world.Velocity(candy) * CaptureKeep);
            world.SetGravityScale(candy, BubbleGravityScale);
            world.SetDamping(candy, BubbleDamping, BubbleDamping);
            return;
        }
    }

    private void UpdateMouth(Vector2 position)
    {
        var distance = Vector2.Distance(position, level!.Moogle);
        MouthOpen = Math.Clamp(1f - (distance - MouthRadius) / (SenseRadius - MouthRadius), 0f, 1f);
    }

    private bool TouchesSpikes(Vector2 from, Vector2 to)
    {
        var spikes = level!.Spikes;
        var reach = CandyRadius + SpikeHalfWidth;
        for (var index = 0; index < spikes.Length; index++)
        {
            var spike = spikes[index];
            if (Geometry2D.SegmentDistance(to, spike.From, spike.To) <= reach ||
                Geometry2D.SegmentDistance((from + to) * 0.5f, spike.From, spike.To) <= reach)
            {
                return true;
            }
        }

        return false;
    }

    private static bool OutOfBounds(Vector2 position) =>
        position.X < -FallMargin || position.X > WorldWidth + FallMargin || position.Y > WorldHeight + FallMargin ||
        position.Y < -FallMargin;

    private void Eat(Vector2 position)
    {
        State = SnipState.Eaten;
        EatenThisFrame = true;
        EndPosition = position;
        MouthOpen = 1f;
        Release();
    }

    private void Lose(SnipLoss loss, Vector2 position)
    {
        State = SnipState.Lost;
        Loss = loss;
        LostThisFrame = true;
        EndPosition = position;
        if (loss == SnipLoss.Spikes)
        {
            Release();
        }
    }

    private void Release()
    {
        for (var index = 0; index < ropeCount; index++)
        {
            var rope = ropes[index];
            if (world.RopeHolds(rope))
            {
                world.CutRope(rope, world.RopeSegments(rope) - 1);
            }
        }

        if (heldBubble >= 0)
        {
            bubbleStates[heldBubble] = BubbleState.Popped;
            heldBubble = -1;
        }

        world.SetGravityScale(candy, 0f);
        world.SetVelocity(candy, Vector2.Zero);
    }
}
