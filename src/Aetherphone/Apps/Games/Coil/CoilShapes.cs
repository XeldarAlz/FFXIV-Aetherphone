namespace Aetherphone.Apps.Games.Coil;

internal enum CoilShapeKind : byte
{
    Spiral,
    Heart,
    Twin,
    Path,
}

internal readonly struct CoilStroke
{
    public readonly float Length;
    public readonly float Turn;

    public CoilStroke(float length, float turn)
    {
        Length = length;
        Turn = turn;
    }

    public static CoilStroke Line(float length) => new(length, 0f);

    public static CoilStroke Arc(float radius, float turn) => new(radius * MathF.Abs(turn), turn);
}

internal readonly struct CoilShape
{
    public readonly CoilShapeKind Kind;
    public readonly Vector2 Launcher;
    public readonly float Turns;
    public readonly float StartAngle;
    public readonly float Direction;
    public readonly float Inner;
    public readonly float StretchY;
    public readonly int Lobes;
    public readonly float LobeAmplitude;
    public readonly float LobePhase;
    public readonly float Squareness;
    public readonly float Offset;
    public readonly Vector2 Start;
    public readonly float Heading;
    public readonly CoilStroke[] Strokes;

    private CoilShape(CoilShapeKind kind, Vector2 launcher, float turns, float startAngle, float direction, float inner,
        float stretchY, int lobes, float lobeAmplitude, float lobePhase, float squareness, float offset, Vector2 start,
        float heading, CoilStroke[] strokes)
    {
        Kind = kind;
        Launcher = launcher;
        Turns = turns;
        StartAngle = startAngle;
        Direction = direction;
        Inner = inner;
        StretchY = stretchY;
        Lobes = lobes;
        LobeAmplitude = lobeAmplitude;
        LobePhase = lobePhase;
        Squareness = squareness;
        Offset = offset;
        Start = start;
        Heading = heading;
        Strokes = strokes;
    }

    public static CoilShape Spiral(float turns, float startAngle, bool clockwise, float inner, float stretchY,
        int lobes = 0, float lobeAmplitude = 0f, float lobePhase = 0f, float squareness = 2f) =>
        new(CoilShapeKind.Spiral, Vector2.Zero, turns, startAngle, clockwise ? 1f : -1f, inner, stretchY, lobes,
            lobeAmplitude, lobePhase, squareness, 0f, Vector2.Zero, 0f, Array.Empty<CoilStroke>());

    public static CoilShape Heart(float turns, float inner, Vector2 launcher) =>
        new(CoilShapeKind.Heart, launcher, turns, 0f, 1f, inner, 1f, 0, 0f, 0f, 2f, 0f, Vector2.Zero, 0f,
            Array.Empty<CoilStroke>());

    public static CoilShape Twin(float turns, float inner, float lobeOffset, float lobeHeight, Vector2 launcher) =>
        new(CoilShapeKind.Twin, launcher, turns, 0f, 1f, inner, lobeHeight, 0, 0f, 0f, 2f, lobeOffset, Vector2.Zero, 0f,
            Array.Empty<CoilStroke>());

    public static CoilShape Path(Vector2 start, float heading, Vector2 launcher, CoilStroke[] strokes) =>
        new(CoilShapeKind.Path, launcher, 0f, 0f, 1f, 1f, 1f, 0, 0f, 0f, 2f, 0f, start, heading, strokes);
}

internal static class CoilShapes
{
    private const float HalfPi = MathF.PI * 0.5f;

    public static readonly CoilShape[] All =
    {
        CoilShape.Spiral(2f, -HalfPi, true, 0.42f, 1.4f),
        CoilShape.Path(new Vector2(-0.72f, -1.3f), 0f, new Vector2(0f, 0.98f), new[]
        {
            CoilStroke.Line(1.44f), CoilStroke.Arc(0.27f, MathF.PI), CoilStroke.Line(1.44f),
            CoilStroke.Arc(0.27f, -MathF.PI), CoilStroke.Line(1.44f), CoilStroke.Arc(0.27f, MathF.PI),
            CoilStroke.Line(1.2f),
        }),
        CoilShape.Twin(1.25f, 0.34f, 0.92f, 0.5f, new Vector2(-0.2f, 0f)),
        CoilShape.Heart(1.5f, 0.45f, new Vector2(0f, 0.3f)),
        CoilShape.Path(new Vector2(-0.82f, -1.4f), 0.36f, new Vector2(0f, 1.05f), new[]
        {
            CoilStroke.Line(1.5f), CoilStroke.Arc(0.17f, MathF.PI - 0.72f), CoilStroke.Line(1.5f),
            CoilStroke.Arc(0.17f, -(MathF.PI - 0.72f)), CoilStroke.Line(1.5f), CoilStroke.Arc(0.17f, MathF.PI - 0.72f),
            CoilStroke.Line(1.3f),
        }),
        CoilShape.Spiral(1.8f, HalfPi, false, 0.42f, 1.35f, 5, 0.1f),
        CoilShape.Spiral(1.6f, -HalfPi, true, 0.46f, 1.45f, 2, 0.15f, MathF.PI),
        CoilShape.Spiral(1.8f, MathF.PI, true, 0.42f, 1.3f, 3, 0.16f, HalfPi),
        CoilShape.Spiral(2f, 0f, false, 0.42f, 1.4f, 0, 0f, 0f, 5f),
        CoilShape.Path(new Vector2(-0.9f, -1.4f), HalfPi, new Vector2(0.15f, 0.35f), new[]
        {
            CoilStroke.Line(1.9f), CoilStroke.Arc(0.9f, -MathF.PI), CoilStroke.Line(1.3f),
            CoilStroke.Arc(0.6f, -MathF.PI), CoilStroke.Line(1.3f), CoilStroke.Arc(0.45f, -MathF.PI),
            CoilStroke.Line(1f),
        }),
        CoilShape.Path(new Vector2(-0.62f, -0.7f), -HalfPi, new Vector2(0f, -0.7f), new[]
        {
            CoilStroke.Arc(0.62f, MathF.PI * 1.5f), CoilStroke.Arc(0.62f, -MathF.PI * 1.5f),
            CoilStroke.Arc(0.38f, -MathF.PI * 1.25f),
        }),
        CoilShape.Spiral(2f, HalfPi, false, 0.38f, 1.4f, 9, 0.05f),
    };

    public static int Count => All.Length;

    public static int Sample(int shapeIndex, Span<Vector2> output, out Vector2 launcher)
    {
        ref readonly var shape = ref All[shapeIndex];
        launcher = shape.Launcher;
        return shape.Kind switch
        {
            CoilShapeKind.Heart => SampleHeart(shape, output),
            CoilShapeKind.Twin => SampleTwin(shape, output),
            CoilShapeKind.Path => SamplePath(shape, output),
            _ => SampleSpiral(shape, output),
        };
    }

    private static int SampleSpiral(in CoilShape shape, Span<Vector2> output)
    {
        var count = output.Length;
        for (var index = 0; index < count; index++)
        {
            var progress = index / (float)(count - 1);
            var angle = shape.StartAngle + shape.Direction * shape.Turns * MathF.Tau * progress;
            var radius = 1f + (shape.Inner - 1f) * progress;
            var lobe = 1f + shape.LobeAmplitude * MathF.Cos(shape.Lobes * angle + shape.LobePhase);
            var exponent = 2f / shape.Squareness;
            var cosine = MathF.Cos(angle);
            var sine = MathF.Sin(angle);
            var x = MathF.Sign(cosine) * MathF.Pow(MathF.Abs(cosine), exponent);
            var y = MathF.Sign(sine) * MathF.Pow(MathF.Abs(sine), exponent);
            output[index] = new Vector2(x, y * shape.StretchY) * (radius * lobe);
        }

        return count;
    }

    private static int SampleHeart(in CoilShape shape, Span<Vector2> output)
    {
        var count = output.Length;
        for (var index = 0; index < count; index++)
        {
            var progress = index / (float)(count - 1);
            var angle = shape.Turns * MathF.Tau * progress;
            var radius = 1f + (shape.Inner - 1f) * progress;
            var sine = MathF.Sin(angle);
            var heartX = 16f * sine * sine * sine / 16f;
            var heartY = -(13f * MathF.Cos(angle) - 5f * MathF.Cos(2f * angle) - 2f * MathF.Cos(3f * angle) -
                MathF.Cos(4f * angle)) / 16f;
            var roundX = sine;
            var roundY = -MathF.Cos(angle);
            var point = new Vector2(heartX * 0.72f + roundX * 0.28f, (heartY * 0.72f + roundY * 0.28f) * 1.3f);
            output[index] = shape.Launcher + (point - shape.Launcher) * radius;
        }

        return count;
    }

    private static int SampleTwin(in CoilShape shape, Span<Vector2> output)
    {
        var count = output.Length;
        var lobeCount = count * 2 / 5;
        var bridgeCount = count - lobeCount * 2;
        var offset = shape.Offset;
        var radiusY = shape.StretchY;
        var write = 0;
        for (var index = 0; index < lobeCount; index++)
        {
            var progress = index / (float)lobeCount;
            var angle = -shape.Turns * MathF.Tau * (1f - progress);
            var radius = shape.Inner + (1f - shape.Inner) * progress;
            output[write++] = new Vector2(MathF.Cos(angle) * radius, -offset + MathF.Sin(angle) * radius * radiusY);
        }

        for (var index = 0; index < bridgeCount; index++)
        {
            var progress = index / (float)bridgeCount;
            output[write++] = new Vector2(1f, -offset + 2f * offset * progress);
        }

        for (var index = 0; index < lobeCount; index++)
        {
            var progress = index / (float)(lobeCount - 1);
            var angle = shape.Turns * MathF.Tau * progress;
            var radius = 1f + (shape.Inner - 1f) * progress;
            output[write++] = new Vector2(MathF.Cos(angle) * radius, offset + MathF.Sin(angle) * radius * radiusY);
        }

        return write;
    }

    private static int SamplePath(in CoilShape shape, Span<Vector2> output)
    {
        var strokes = shape.Strokes;
        var total = 0f;
        for (var index = 0; index < strokes.Length; index++)
        {
            total += strokes[index].Length;
        }

        var position = shape.Start;
        var heading = shape.Heading;
        var write = 0;
        output[write++] = position;
        var budget = output.Length - 1;
        for (var strokeIndex = 0; strokeIndex < strokes.Length; strokeIndex++)
        {
            var stroke = strokes[strokeIndex];
            var steps = Math.Max(2, (int)(budget * stroke.Length / total) - 1);
            if (write + steps > output.Length)
            {
                steps = output.Length - write;
            }

            if (stroke.Turn == 0f)
            {
                var direction = new Vector2(MathF.Cos(heading), MathF.Sin(heading));
                for (var step = 1; step <= steps; step++)
                {
                    output[write++] = position + direction * (stroke.Length * step / steps);
                }

                position += direction * stroke.Length;
                continue;
            }

            var side = MathF.Sign(stroke.Turn);
            var radius = stroke.Length / MathF.Abs(stroke.Turn);
            var center = position + Polar(heading + side * HalfPi) * radius;
            for (var step = 1; step <= steps; step++)
            {
                var angle = heading + stroke.Turn * step / steps - side * HalfPi;
                output[write++] = center + Polar(angle) * radius;
            }

            heading += stroke.Turn;
            position = center + Polar(heading - side * HalfPi) * radius;
        }

        return write;
    }

    public static Vector2 Polar(float angle) => new(MathF.Cos(angle), MathF.Sin(angle));
}
