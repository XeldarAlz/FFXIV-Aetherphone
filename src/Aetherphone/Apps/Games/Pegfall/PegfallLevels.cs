using System.Globalization;

namespace Aetherphone.Apps.Games.Pegfall;

internal readonly struct PegSpot
{
    public readonly Vector2 Position;
    public readonly int Ring;
    public readonly float Angle;

    public PegSpot(Vector2 position, int ring, float angle)
    {
        Position = position;
        Ring = ring;
        Angle = angle;
    }

    public bool Rotates => Ring >= 0;
}

internal readonly struct PegRing
{
    public readonly Vector2 Center;
    public readonly float Radius;
    public readonly float Speed;

    public PegRing(Vector2 center, float radius, float speed)
    {
        Center = center;
        Radius = radius;
        Speed = speed;
    }

    public Vector2 PointAt(float angle) => Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * Radius;
}

internal readonly struct PegLayout
{
    private const int TwoStarPointsPerPeg = 400;
    private const int ThreeStarPointsPerPeg = 900;

    public readonly PegSpot[] Pegs;
    public readonly PegRing[] Rings;
    public readonly float BucketSpeed;

    public PegLayout(PegSpot[] pegs, PegRing[] rings, float bucketSpeed)
    {
        Pegs = pegs;
        Rings = rings;
        BucketSpeed = bucketSpeed;
    }

    public int Count => Pegs.Length;

    public int TwoStarScore => Pegs.Length * TwoStarPointsPerPeg;

    public int ThreeStarScore => Pegs.Length * ThreeStarPointsPerPeg;
}

internal static class PegfallLevels
{
    public const int Count = 20;
    private const float DegreesToRadians = MathF.PI / 180f;

    private static readonly string[] Sources =
    {
        "h 1.0 3.4 10 8 0.9 1.25",
        "a 5 1.4 3.9 28 152 12;a 5 4.3 3.9 28 152 12;a 5 7.2 3.9 28 152 12;h 1.2 12.2 10 2 0.85 0.8",
        "c 2.6 5.2 1.3 8;c 7.4 5.2 1.3 8;c 5 8.4 1.5 10;c 2.6 11.2 1.1 8;c 7.4 11.2 1.1 8;p 5 5.2;p 2.6 5.2;" +
        "p 7.4 5.2;p 5 8.4;l 0.8 13.2 9.2 13.2 11",
        "l 1 3.2 4.2 6.4 6;l 9 3.2 5.8 6.4 6;l 1 7.4 4.2 10.6 6;l 9 7.4 5.8 10.6 6;h 1.6 11.8 9 2 0.85 1.0;" +
        "p 5 3.6;p 5 4.6;p 5 7.2;p 5 8.4",
        "r 5 6.6 1.6 10 0.6;h 0.8 3.0 11 2 0.84 1.0;l 0.8 9.6 9.2 9.6 11;h 1.0 11.2 10 2 0.9 1.1;p 1.2 6.6;" +
        "p 8.8 6.6;p 1.6 5.4;p 8.4 5.4;p 1.6 7.8;p 8.4 7.8",
        "g 1.2 3.2 9 4 0.95 1.05;b 1.4;a 5 6.4 4.1 25 155 13;g 1.2 11.4 9 2 0.95 1.05",
        "c 5 7.4 3.6 22;c 5 7.4 2.4 14 12.86;c 5 7.4 1.2 7;p 5 7.4;l 0.8 12.9 9.2 12.9 10",
        "r 2.8 5 1.2 7 -0.9;r 7.2 5 1.2 7 0.9;r 5 9.4 1.5 9 0.7;l 0.6 3 0.6 12.6 9;l 9.4 3 9.4 12.6 9;" +
        "h 2.0 12.6 7 1 1.0 1.0;p 5 5;p 5 3.4;p 2.8 7.2;p 7.2 7.2",
        "a 5 13.5 4.4 200 340 12;a 5 13.5 3.2 205 335 9;a 5 13.5 2.0 210 330 6;h 1.0 3.0 10 3 0.9 1.0",
        "l 1 3 9 3 9;l 1 4.2 9 4.2 9;l 1.5 5.4 8.5 5.4 8;l 2 6.6 8 6.6 7;l 2.5 7.8 7.5 7.8 6;l 3 9 7 9 5;" +
        "l 3.5 10.2 6.5 10.2 4;l 4 11.4 6 11.4 3;l 1 12.6 9 12.6 9",
        "r 5 6 2.2 14 0.5;r 5 6 1.0 6 -1.1;h 0.9 9.4 10 4 0.9 1.05;p 1.1 3.2;p 8.9 3.2;p 1.4 4.6;p 8.6 4.6;" +
        "p 1.1 6;p 8.9 6",
        "c 2.4 4.6 1.1 7;c 5 4.6 1.1 7;c 7.6 4.6 1.1 7;c 3.7 8 1.1 7;c 6.3 8 1.1 7;c 2.4 11.4 1.1 7;" +
        "c 5 11.4 1.1 7;c 7.6 11.4 1.1 7;b 1.3",
        "l 0.8 3.0 4.4 4.8 6;l 9.2 3.0 5.6 4.8 6;p 5 5.1;l 0.8 6.0 4.4 7.8 6;l 9.2 6.0 5.6 7.8 6;p 5 8.1;" +
        "l 0.8 9.0 4.4 10.8 6;l 9.2 9.0 5.6 10.8 6;p 5 11.1;l 1 12.9 9 12.9 10;p 2.6 5.6;p 7.4 5.6;" +
        "p 2.6 8.6;p 7.4 8.6",
        "r 5 5.4 2.0 12 0.8;r 5 10.4 1.8 12 -0.8;l 0.7 3.2 2.0 3.2 2;l 8.0 3.2 9.3 3.2 2;l 0.7 8 2.2 8 3;" +
        "l 7.8 8 9.3 8 3;l 0.7 12.9 9.3 12.9 11;p 5 5.4;p 5 10.4;p 1.0 5.4;p 9.0 5.4;p 1.0 10.6;p 9.0 10.6",
        "l 1 3 1 12.6 10;l 9 3 9 12.6 10;l 2.6 4 7.4 4 6;l 2.6 6.4 7.4 6.4 6;l 2.6 8.8 7.4 8.8 6;" +
        "l 2.6 11.2 7.4 11.2 6;p 5 5.2;p 5 7.6;p 5 10;p 3.8 5.2;p 6.2 5.2;p 3.8 10;p 6.2 10;b 1.6",
        "c 5 7.6 4.2 26;c 5 7.6 3.0 18 10;r 5 7.6 1.5 8 1.2;p 5 7.6;p 0.8 3.0;p 9.2 3.0;p 0.8 12.4;p 9.2 12.4",
        "a 5 2.0 4.0 25 155 13;a 5 5.6 2.6 30 150 7;a 5 6.5 4.0 25 155 13;a 5 10.1 2.6 30 150 7;" +
        "l 0.8 13.2 2.4 13.2 3;l 7.6 13.2 9.2 13.2 3;p 0.8 8.0;p 9.2 8.0;p 0.9 11.5;p 9.1 11.5",
        "r 2.6 4.4 1.3 8 1.0;r 7.4 4.4 1.3 8 -1.0;r 2.6 9.8 1.3 8 -1.0;r 7.4 9.8 1.3 8 1.0;l 5 3 5 11.4 8;" +
        "l 0.7 7.1 3.6 7.1 4;l 6.4 7.1 9.3 7.1 4;l 0.7 12.9 9.3 12.9 11;b 1.5",
        "h 1.0 3.2 10 10 0.89 1.0;b 1.8",
        "r 5 7.2 3.4 20 0.45;r 5 7.2 2.2 13 -0.7;r 5 7.2 1.0 6 1.3;p 5 7.2;l 0.6 3.0 2.4 3.0 3;" +
        "l 7.6 3.0 9.4 3.0 3;l 0.6 12.8 9.4 12.8 12;p 0.7 7.2;p 9.3 7.2;b 2.0",
    };

    private static readonly PegLayout[] Layouts = BuildLayouts();

    public static PegLayout Get(int level) => Layouts[Math.Clamp(level, 1, Count) - 1];

    public static PegLayout Parse(string source)
    {
        var pegs = new List<PegSpot>();
        var rings = new List<PegRing>();
        var bucketSpeed = 1f;
        var shapes = source.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var shapeIndex = 0; shapeIndex < shapes.Length; shapeIndex++)
        {
            var tokens = shapes[shapeIndex].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var values = new float[tokens.Length - 1];
            for (var valueIndex = 0; valueIndex < values.Length; valueIndex++)
            {
                values[valueIndex] = float.Parse(tokens[valueIndex + 1], NumberStyles.Float,
                    CultureInfo.InvariantCulture);
            }

            switch (tokens[0])
            {
                case "p":
                    Expect(values, 2, shapes[shapeIndex]);
                    pegs.Add(new PegSpot(new Vector2(values[0], values[1]), -1, 0f));
                    break;
                case "l":
                    Expect(values, 5, shapes[shapeIndex]);
                    AddLine(pegs, new Vector2(values[0], values[1]), new Vector2(values[2], values[3]), (int)values[4]);
                    break;
                case "a":
                    Expect(values, 6, shapes[shapeIndex]);
                    AddArc(pegs, new Vector2(values[0], values[1]), values[2], values[3], values[4], (int)values[5]);
                    break;
                case "c":
                    ExpectRange(values, 4, 5, shapes[shapeIndex]);
                    AddCircle(pegs, new Vector2(values[0], values[1]), values[2], (int)values[3],
                        values.Length > 4 ? values[4] : 0f);
                    break;
                case "r":
                    Expect(values, 5, shapes[shapeIndex]);
                    AddRing(pegs, rings, new PegRing(new Vector2(values[0], values[1]), values[2], values[4]),
                        (int)values[3]);
                    break;
                case "g":
                case "h":
                    Expect(values, 6, shapes[shapeIndex]);
                    AddGrid(pegs, new Vector2(values[0], values[1]), (int)values[2], (int)values[3], values[4],
                        values[5], tokens[0] == "h");
                    break;
                case "b":
                    Expect(values, 1, shapes[shapeIndex]);
                    bucketSpeed = values[0];
                    break;
                default:
                    throw new FormatException("Unknown peg shape: " + shapes[shapeIndex]);
            }
        }

        return new PegLayout(pegs.ToArray(), rings.ToArray(), bucketSpeed);
    }

    private static PegLayout[] BuildLayouts()
    {
        var layouts = new PegLayout[Sources.Length];
        for (var index = 0; index < Sources.Length; index++)
        {
            layouts[index] = Parse(Sources[index]);
        }

        return layouts;
    }

    private static void Expect(float[] values, int count, string shape)
    {
        ExpectRange(values, count, count, shape);
    }

    private static void ExpectRange(float[] values, int minimum, int maximum, string shape)
    {
        if (values.Length < minimum || values.Length > maximum)
        {
            throw new FormatException("Wrong field count in peg shape: " + shape);
        }
    }

    private static void AddLine(List<PegSpot> pegs, Vector2 from, Vector2 to, int count)
    {
        for (var index = 0; index < count; index++)
        {
            var fraction = count > 1 ? index / (float)(count - 1) : 0f;
            pegs.Add(new PegSpot(Vector2.Lerp(from, to, fraction), -1, 0f));
        }
    }

    private static void AddArc(List<PegSpot> pegs, Vector2 center, float radius, float fromDegrees, float toDegrees,
        int count)
    {
        for (var index = 0; index < count; index++)
        {
            var fraction = count > 1 ? index / (float)(count - 1) : 0f;
            var angle = (fromDegrees + (toDegrees - fromDegrees) * fraction) * DegreesToRadians;
            pegs.Add(new PegSpot(center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius, -1, 0f));
        }
    }

    private static void AddCircle(List<PegSpot> pegs, Vector2 center, float radius, int count, float phaseDegrees)
    {
        for (var index = 0; index < count; index++)
        {
            var angle = (phaseDegrees + 360f * index / count) * DegreesToRadians;
            pegs.Add(new PegSpot(center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius, -1, 0f));
        }
    }

    private static void AddRing(List<PegSpot> pegs, List<PegRing> rings, in PegRing ring, int count)
    {
        var ringIndex = rings.Count;
        rings.Add(ring);
        for (var index = 0; index < count; index++)
        {
            var angle = MathF.Tau * index / count;
            pegs.Add(new PegSpot(ring.PointAt(angle), ringIndex, angle));
        }
    }

    private static void AddGrid(List<PegSpot> pegs, Vector2 origin, int columns, int rows, float stepX, float stepY,
        bool staggered)
    {
        for (var row = 0; row < rows; row++)
        {
            var shifted = staggered && row % 2 == 1;
            var count = shifted ? columns - 1 : columns;
            var offsetX = shifted ? stepX * 0.5f : 0f;
            for (var column = 0; column < count; column++)
            {
                pegs.Add(new PegSpot(origin + new Vector2(offsetX + column * stepX, row * stepY), -1, 0f));
            }
        }
    }
}
