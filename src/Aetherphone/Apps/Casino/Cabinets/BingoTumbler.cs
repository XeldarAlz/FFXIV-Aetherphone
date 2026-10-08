using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Framework.Physics;

namespace Aetherphone.Apps.Casino.Cabinets;

internal sealed class BingoTumbler
{
    public const int BallCount = 20;
    public const float DrumRadius = 1f;
    public const float BallRadius = 0.13f;
    public const ulong DefaultSeed = 0xB1A60B11UL;

    private const int DrumPoints = 32;
    private const int BodyCapacity = BallCount + 4;
    private const int ContactCapacity = 256;
    private const int JointCapacity = 1;
    private const int EventCapacity = 32;
    private const float BlowerLine = 0.3f;
    private const float LiftMin = 14f;
    private const float LiftMax = 26f;
    private const float SideForce = 7f;
    private const float CageTurnRate = 0.9f;
    private const float RespawnDepth = 0.7f;

    private static readonly PhysicsMaterial BallMaterial = new(1f, 0.6f, 0.1f);
    private static readonly PhysicsMaterial DrumMaterial = new(1f, 0.5f, 0.1f);

    private readonly PhysicsWorld world = new(BodyCapacity, ContactCapacity, JointCapacity, DrumPoints * 2,
        EventCapacity);
    private readonly int[] balls = new int[BallCount];
    private readonly int[] tints = new int[BallCount];
    private readonly ulong seed;

    private GameRandom random;
    private int hiddenBall = -1;
    private float hiddenSeconds;
    private float cageAngle;

    public BingoTumbler(ulong seed = DefaultSeed)
    {
        this.seed = seed;
        Build();
    }

    public float CageAngle => cageAngle;

    public int HiddenBall => hiddenBall;

    public static Vector2 Exit => new(0f, -DrumRadius);

    public Vector2 BallPosition(int index) => world.RenderPosition(balls[index]);

    public int TintOf(int index) => tints[index];

    public void Reset()
    {
        world.Clear();
        Build();
    }

    public void Update(float deltaSeconds)
    {
        if (deltaSeconds <= 0f)
        {
            return;
        }

        cageAngle += deltaSeconds * CageTurnRate;
        if (hiddenBall >= 0)
        {
            hiddenSeconds -= deltaSeconds;
            if (hiddenSeconds <= 0f)
            {
                world.SetTransform(balls[hiddenBall], new Vector2(0f, RespawnDepth), 0f);
                world.SetVelocity(balls[hiddenBall], Vector2.Zero);
                hiddenBall = -1;
            }
        }

        Blow();
        world.Step(deltaSeconds);
    }

    public int Pop(float hideSeconds)
    {
        var top = -1;
        var highest = float.MaxValue;
        for (var index = 0; index < BallCount; index++)
        {
            if (index == hiddenBall)
            {
                continue;
            }

            var y = world.Position(balls[index]).Y;
            if (y < highest)
            {
                highest = y;
                top = index;
            }
        }

        if (top < 0)
        {
            return -1;
        }

        if (hiddenBall >= 0)
        {
            world.SetTransform(balls[hiddenBall], new Vector2(0f, RespawnDepth), 0f);
        }

        hiddenBall = top;
        hiddenSeconds = hideSeconds;
        return top;
    }

    private void Blow()
    {
        for (var index = 0; index < BallCount; index++)
        {
            var body = balls[index];
            if (index == hiddenBall || world.Position(body).Y < BlowerLine)
            {
                continue;
            }

            var mass = world.Mass(body);
            var force = new Vector2(random.Range(-SideForce, SideForce), -random.Range(LiftMin, LiftMax)) * mass;
            world.ApplyForce(body, force);
        }
    }

    private void Build()
    {
        random = GameRandom.FromSeed(seed);
        hiddenBall = -1;
        hiddenSeconds = 0f;
        cageAngle = 0f;
        Span<Vector2> rim = stackalloc Vector2[DrumPoints];
        for (var point = 0; point < DrumPoints; point++)
        {
            var angle = point * MathF.PI * 2f / DrumPoints;
            rim[point] = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * DrumRadius;
        }

        world.CreatePolyline(rim, DrumMaterial, true);
        for (var index = 0; index < BallCount; index++)
        {
            var column = index % 5;
            var row = index / 5;
            var position = new Vector2((column - 2f) * BallRadius * 2.4f, row * BallRadius * 2.2f - 0.3f);
            balls[index] = world.CreateCircle(BodyType.Dynamic, position, BallRadius, BallMaterial);
            tints[index] = random.Next(5);
        }
    }
}
