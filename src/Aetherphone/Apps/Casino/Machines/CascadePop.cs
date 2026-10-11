using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Casino.Machines;

internal struct CascadeSpark
{
    public Vector2 Origin;
    public Vector2 Velocity;
    public Vector4 Color;
    public float Age;
    public float Life;
    public float Size;
    public float Spin;
}

internal struct CascadeFlash
{
    public Vector2 Center;
    public Vector4 Color;
    public float Age;
    public float Radius;
}

internal sealed class CascadePop
{
    public const int CellCapacity = MachineRoundPlayback.MaxCells;
    public const int SparksPerCell = 4;
    public const int SparkCapacity = CellCapacity * SparksPerCell;
    public const float FlashSeconds = 0.24f;
    public const float SparkShortest = 0.36f;
    public const float SparkLongest = 0.6f;
    public const float Drag = 3.2f;
    public const float SpeedLow = 1.3f;
    public const float SpeedHigh = 2.1f;
    public const float SparkSize = 0.13f;
    public const float SizeJitter = 0.2f;
    public const float SpinRange = 9f;
    public const float AngleJitter = 0.2f;

    private readonly CascadeSpark[] sparks = new CascadeSpark[SparkCapacity];
    private readonly CascadeFlash[] flashes = new CascadeFlash[CellCapacity];

    private int sparkCount;
    private int flashCount;

    public int SparkCount => sparkCount;

    public int FlashCount => flashCount;

    public bool Active => sparkCount > 0 || flashCount > 0;

    public ref readonly CascadeSpark SparkAt(int index) => ref sparks[index];

    public ref readonly CascadeFlash FlashAt(int index) => ref flashes[index];

    public static float MaxTravel(float extent) => extent * SpeedHigh / Drag;

    public static float MaxSize(float extent) => extent * SparkSize * (1f + SizeJitter);

    public static Vector2 PositionOf(in CascadeSpark spark) =>
        spark.Origin + spark.Velocity * ((1f - MathF.Exp(-Drag * spark.Age)) / Drag);

    public void Burst(Vector2 center, float extent, Vector4 color, ref GameRandom random)
    {
        if (flashCount < CellCapacity)
        {
            flashes[flashCount++] = new CascadeFlash { Center = center, Color = color, Radius = extent };
        }

        var turn = random.NextFloat() * MathF.Tau;
        for (var index = 0; index < SparksPerCell && sparkCount < SparkCapacity; index++)
        {
            var angle = turn + MathF.Tau * (index + random.Range(-AngleJitter, AngleJitter)) / SparksPerCell;
            var speed = extent * random.Range(SpeedLow, SpeedHigh);
            sparks[sparkCount++] = new CascadeSpark
            {
                Origin = center,
                Velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed,
                Color = color,
                Life = random.Range(SparkShortest, SparkLongest),
                Size = extent * SparkSize * random.Range(1f - SizeJitter, 1f + SizeJitter),
                Spin = random.Range(-SpinRange, SpinRange),
            };
        }
    }

    public void Update(float deltaSeconds)
    {
        var step = MathF.Max(0f, deltaSeconds);
        for (var index = flashCount - 1; index >= 0; index--)
        {
            flashes[index].Age += step;
            if (flashes[index].Age >= FlashSeconds)
            {
                flashes[index] = flashes[--flashCount];
            }
        }

        for (var index = sparkCount - 1; index >= 0; index--)
        {
            sparks[index].Age += step;
            if (sparks[index].Age >= sparks[index].Life)
            {
                sparks[index] = sparks[--sparkCount];
            }
        }
    }

    public void Clear()
    {
        sparkCount = 0;
        flashCount = 0;
    }
}
