using System.Globalization;
using Aetherphone.Apps.Casino.Stage;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino;

internal enum CasinoSign : byte
{
    Gamba,
    Slots,
    Race,
    Holdem,
    Plinko,
    Mines,
    Dice,
    Bingo,
    Wheel,
    TwentyOne,
    Bar,
    Keno,
    Scratch,
    Limbo,
    HiLo,
    FreeSpin,
    Jackpot,
    Liftoff,
    GoldenBird,
    Cascade,
    Moogle,
    Deathroll,
    Raffle,
    DealerHoldem,
    Book,
}

internal static class CasinoSigns
{
    public const float GridHeight = 6f;
    public const float LetterGap = 1.5f;
    public const float SpaceAdvance = 3f;
    public const float StrokeFraction = 0.085f;

    private const int FirstChar = 32;
    private const int LastChar = 90;
    private const float CornerRadius = 1.6f;
    private const float ArcStep = MathF.PI / 12f;
    private const float MinTurn = 0.001f;
    private const float SamePoint = 0.0001f;

    private static readonly string[] Words =
    {
        "GAMBA", "SLOTS", "RACE", "HOLD'EM", "PLINKO", "MINES", "DICE", "BINGO", "WHEEL", "21", "BAR", "KENO",
        "SCRATCH", "LIMBO", "HI-LO", "FREE SPIN", "JACKPOT", "LIFTOFF",
        "GOLDEN BIRD", "CASCADE", "MOOGLE", "DEATHROLL", "RAFFLE", "DEALER HOLD'EM", "BOOK",
    };

    private static readonly GlyphShape[] Shapes = new GlyphShape[LastChar - FirstChar + 1];
    private static readonly StrokeRange[] Strokes;
    private static readonly Vector2[] Points;
    private static readonly int LongestStroke;

    static CasinoSigns()
    {
        var strokes = new List<StrokeRange>();
        var points = new List<Vector2>();
        Define(strokes, points, 'A', "0,6 2.3,0 4.6,6|0.85,3.8 3.75,3.8", -0.4f, -0.4f);
        Define(strokes, points, 'B', "2.35,2.9 4.2,2.9r 4.2,6r 0,6 0,0 3.8,0r 3.8,2.9r 0,2.9");
        Define(strokes, points, 'C', "4.2,1.6 4.2,0r 0,0r 0,6r 4.2,6r 4.2,4.4", 0f, -0.1f);
        Define(strokes, points, 'D', "0,0 4.2,0r 4.2,6r 0,6 0,0");
        Define(strokes, points, 'E', "3.6,0 0,0 0,6 3.6,6|0,3 3,3", 0f, -0.2f);
        Define(strokes, points, 'F', "3.6,0 0,0 0,6|0,3 3,3", 0f, -0.4f);
        Define(strokes, points, 'G', "4.2,1.6 4.2,0r 0,0r 0,6r 4.2,6r 4.2,3.2 2.4,3.2");
        Define(strokes, points, 'H', "0,0 0,6|4.2,0 4.2,6|0,3 4.2,3");
        Define(strokes, points, 'I', "0,0 0,6");
        Define(strokes, points, 'J', "3.2,0 3.2,6r 0,6r 0,4.4", -0.3f);
        Define(strokes, points, 'K', "0,0 0,6|4,0 0,3.2 4.2,6", 0f, -0.2f);
        Define(strokes, points, 'L', "0,0 0,6 3.6,6", 0f, -0.15f);
        Define(strokes, points, 'M', "0,6 0,0 2.6,3.6 5.2,0 5.2,6");
        Define(strokes, points, 'N', "0,6 0,0 4.2,6 4.2,0");
        Define(strokes, points, 'O', "0,0r 4.4,0r 4.4,6r 0,6r 0,0r");
        Define(strokes, points, 'P', "0,6 0,0 4,0r 4,3.2r 0,3.2", 0f, -0.2f);
        Define(strokes, points, 'Q', "0,0r 4.4,0r 4.4,6r 0,6r 0,0r|2.8,4.4 4.4,6");
        Define(strokes, points, 'R', "0,6 0,0 4,0r 4,3.2r 0,3.2|2.2,3.2 4.2,6");
        Define(strokes, points, 'S', "4.2,1.6 4.2,0r 0,0r 0,3r 4.2,3r 4.2,6r 0,6r 0,4.4");
        Define(strokes, points, 'T', "0,0 4.4,0|2.2,0 2.2,6", -0.5f, -0.5f);
        Define(strokes, points, 'U', "0,0 0,6r 4.2,6r 4.2,0");
        Define(strokes, points, 'V', "0,0 2.3,6 4.6,0", -0.4f, -0.4f);
        Define(strokes, points, 'W', "0,0 1.4,6 2.9,1.4 4.4,6 5.8,0", -0.2f, -0.2f);
        Define(strokes, points, 'X', "0,0 4.2,6|4.2,0 0,6", -0.2f, -0.2f);
        Define(strokes, points, 'Y', "0,0 2.2,3.2 4.4,0|2.2,3.2 2.2,6", -0.3f, -0.3f);
        Define(strokes, points, 'Z', "0,0 4.2,0 0,6 4.2,6");
        Define(strokes, points, '0', "0,0r 4,0r 4,6r 0,6r 0,0r");
        Define(strokes, points, '1', "0,1.4 1.6,0 1.6,6", -0.2f);
        Define(strokes, points, '2', "0,1.6 0,0r 4,0r 4,2.6r 0,6 4,6");
        Define(strokes, points, '3', "0,0 4,0r 4,6r 0,6|1.4,3 4,3");
        Define(strokes, points, '4', "3,6 3,0 0,4 4.2,4");
        Define(strokes, points, '5', "4,0 0,0 0,2.8 4,2.8r 4,6r 0,6");
        Define(strokes, points, '6', "4,0 0,0r 0,6r 4,6r 4,2.8r 0,2.8");
        Define(strokes, points, '7', "0,0 4,0 1.4,6", 0f, -0.4f);
        Define(strokes, points, '8', "0,0r 4,0r 4,6r 0,6r 0,0r|0,3 4,3");
        Define(strokes, points, '9', "4,3.2 0,3.2r 0,0r 4,0r 4,6r 0,6");
        Define(strokes, points, '\'', "0,0 0,1.6");
        Define(strokes, points, '-', "0,3.2 2.6,3.2");
        Define(strokes, points, '!', "0,0 0,4|0,5.9 0,6");
        Strokes = strokes.ToArray();
        Points = points.ToArray();
        for (var index = 0; index < Strokes.Length; index++)
        {
            LongestStroke = Math.Max(LongestStroke, Strokes[index].Count);
        }
    }

    public static string Text(CasinoSign sign) => Words[(int)sign];

    public static bool HasGlyph(char character) => character == ' ' || SlotOf(character) >= 0;

    public static float Measure(CasinoSign sign, float height) => Measure(Words[(int)sign], height);

    public static float Measure(ReadOnlySpan<char> text, float height)
    {
        var typesetter = new Typesetter(text);
        var placed = false;
        while (typesetter.Next(out _, out _))
        {
            placed = true;
        }

        return placed ? (typesetter.Pen + StrokeFraction * GridHeight) * height / GridHeight : 0f;
    }

    public static float HeightToFit(CasinoSign sign, float maxWidth, float maxHeight)
    {
        var atOne = Measure(sign, 1f);
        if (atOne <= 0f)
        {
            return maxHeight;
        }

        return MathF.Min(maxHeight, maxWidth / atOne);
    }

    public static void Draw(ImDrawListPtr drawList, CasinoSign sign, Vector2 center, float height, Vector4 color,
        float lit)
    {
        Draw(drawList, Words[(int)sign], center, height, color, lit);
    }

    public static void Draw(ImDrawListPtr drawList, ReadOnlySpan<char> text, Vector2 center, float height,
        Vector4 color, float lit)
    {
        if (height <= 0f || text.Length == 0 || color.W * lit <= 0f)
        {
            return;
        }

        var unit = height / GridHeight;
        var left = center.X - Measure(text, height) * 0.5f + height * StrokeFraction * 0.5f;
        var top = center.Y - height * 0.5f;
        var stroke = MathF.Max(1f, height * StrokeFraction);
        Span<uint> palette = stackalloc uint[NeonTubeMesh.PaletteSize];
        NeonTubeMesh.Palette(color, lit, palette);
        Span<Vector2> path = stackalloc Vector2[LongestStroke];
        var typesetter = new Typesetter(text);
        while (typesetter.Next(out var slot, out var pen))
        {
            var shape = Shapes[slot];
            var origin = new Vector2(left + pen * unit, top);
            for (var strokeIndex = 0; strokeIndex < shape.StrokeCount; strokeIndex++)
            {
                var range = Strokes[shape.FirstStroke + strokeIndex];
                for (var pointIndex = 0; pointIndex < range.Count; pointIndex++)
                {
                    path[pointIndex] = origin + Points[range.First + pointIndex] * unit;
                }

                NeonTubeMesh.Stroke(drawList, path[..range.Count], range.Closed, stroke, palette);
            }
        }
    }

    private static int SlotOf(char character)
    {
        var slot = character - FirstChar;
        return slot >= 0 && slot < Shapes.Length && Shapes[slot].StrokeCount > 0 ? slot : -1;
    }

    private static void Define(List<StrokeRange> strokes, List<Vector2> points, char character, string paths,
        float leading = 0f, float trailing = 0f)
    {
        var firstStroke = strokes.Count;
        var firstPoint = points.Count;
        var parts = paths.Split('|');
        for (var partIndex = 0; partIndex < parts.Length; partIndex++)
        {
            var start = points.Count;
            var closed = Trace(parts[partIndex], points);
            strokes.Add(new StrokeRange(start, points.Count - start, closed));
        }

        var minimum = float.MaxValue;
        var maximum = float.MinValue;
        for (var pointIndex = firstPoint; pointIndex < points.Count; pointIndex++)
        {
            minimum = MathF.Min(minimum, points[pointIndex].X);
            maximum = MathF.Max(maximum, points[pointIndex].X);
        }

        for (var pointIndex = firstPoint; pointIndex < points.Count; pointIndex++)
        {
            points[pointIndex] -= new Vector2(minimum, 0f);
        }

        Shapes[character - FirstChar] =
            new GlyphShape(firstStroke, parts.Length, maximum - minimum, leading, trailing);
    }

    private static bool Trace(string path, List<Vector2> output)
    {
        var tokens = path.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var corners = new Vector2[tokens.Length];
        var rounded = new bool[tokens.Length];
        for (var tokenIndex = 0; tokenIndex < tokens.Length; tokenIndex++)
        {
            var token = tokens[tokenIndex].AsSpan();
            rounded[tokenIndex] = token[^1] == 'r';
            var pair = rounded[tokenIndex] ? token[..^1] : token;
            var comma = pair.IndexOf(',');
            corners[tokenIndex] = new Vector2(
                float.Parse(pair[..comma], CultureInfo.InvariantCulture),
                float.Parse(pair[(comma + 1)..], CultureInfo.InvariantCulture));
        }

        var closed = corners.Length > 2 && corners[0] == corners[^1];
        var count = closed ? corners.Length - 1 : corners.Length;
        if (closed)
        {
            rounded[0] |= rounded[^1];
        }

        var start = output.Count;
        for (var index = 0; index < count; index++)
        {
            if (!rounded[index] || !IsInterior(index, count, closed))
            {
                Append(output, start, corners[index]);
                continue;
            }

            var previous = (index + count - 1) % count;
            var next = (index + 1) % count;
            var incomingReach = Reach(previous, count, closed, rounded);
            var outgoingReach = Reach(next, count, closed, rounded);
            Round(output, start, corners[previous], corners[index], corners[next], incomingReach, outgoingReach);
        }

        if (closed && output.Count - start > 1 &&
            Vector2.DistanceSquared(output[^1], output[start]) < SamePoint)
        {
            output.RemoveAt(output.Count - 1);
        }

        return closed;
    }

    private static bool IsInterior(int index, int count, bool closed) => closed || (index > 0 && index < count - 1);

    private static float Reach(int neighbor, int count, bool closed, bool[] rounded) =>
        rounded[neighbor] && IsInterior(neighbor, count, closed) ? 0.5f : 1f;

    private static void Round(List<Vector2> output, int start, Vector2 previous, Vector2 corner, Vector2 next,
        float incomingReach, float outgoingReach)
    {
        var incoming = corner - previous;
        var outgoing = next - corner;
        var incomingLength = incoming.Length();
        var outgoingLength = outgoing.Length();
        incoming /= incomingLength;
        outgoing /= outgoingLength;
        var turn = MathF.Acos(Math.Clamp(Vector2.Dot(incoming, outgoing), -1f, 1f));
        if (turn < MinTurn)
        {
            Append(output, start, corner);
            return;
        }

        var halfTangent = MathF.Tan(turn * 0.5f);
        var tangent = MathF.Min(CornerRadius * halfTangent,
            MathF.Min(incomingLength * incomingReach, outgoingLength * outgoingReach));
        var radius = tangent / halfTangent;
        var arcStart = corner - incoming * tangent;
        var side = incoming.X * outgoing.Y - incoming.Y * outgoing.X > 0f ? 1f : -1f;
        var center = arcStart + new Vector2(-incoming.Y, incoming.X) * side * radius;
        var startAngle = MathF.Atan2(arcStart.Y - center.Y, arcStart.X - center.X);
        var steps = Math.Max(2, (int)MathF.Ceiling(turn / ArcStep));
        for (var step = 0; step <= steps; step++)
        {
            var angle = startAngle + side * turn * step / steps;
            Append(output, start, center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius);
        }
    }

    private static void Append(List<Vector2> output, int start, Vector2 point)
    {
        if (output.Count > start && Vector2.DistanceSquared(output[^1], point) < SamePoint)
        {
            return;
        }

        output.Add(point);
    }

    private readonly record struct StrokeRange(int First, int Count, bool Closed);

    private readonly record struct GlyphShape(int FirstStroke, int StrokeCount, float Width, float Leading,
        float Trailing);

    private ref struct Typesetter(ReadOnlySpan<char> text)
    {
        private readonly ReadOnlySpan<char> text = text;
        private int index;
        private int previous = -1;
        private bool spaced;

        public float Pen { get; private set; }

        public bool Next(out int slot, out float pen)
        {
            while (index < text.Length)
            {
                var current = SlotOf(text[index]);
                index++;
                if (current < 0)
                {
                    spaced = true;
                    continue;
                }

                if (previous >= 0)
                {
                    Pen += spaced
                        ? SpaceAdvance + LetterGap
                        : LetterGap + Shapes[previous].Trailing + Shapes[current].Leading;
                }

                spaced = false;
                slot = current;
                pen = Pen;
                Pen += Shapes[current].Width;
                previous = current;
                return true;
            }

            slot = -1;
            pen = Pen;
            return false;
        }
    }
}
