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
    Deathroll,
    Raffle,
}

internal static class CasinoSigns
{
    public const float GridWidth = 4f;
    public const float GridHeight = 6f;
    public const float LetterGap = 1.6f;
    public const float SpaceAdvance = 3f;
    public const float StrokeFraction = 0.085f;
    public const int MaxStrokePoints = 16;

    private const int FirstChar = 32;
    private const int LastChar = 90;

    private static readonly string[] Words =
    {
        "GAMBA", "SLOTS", "RACE", "HOLD'EM", "PLINKO", "MINES", "DICE", "BINGO", "WHEEL", "21", "BAR", "KENO",
        "SCRATCH", "LIMBO", "HI-LO", "FREE SPIN", "JACKPOT", "LIFTOFF", "DEATHROLL", "RAFFLE",
    };

    private static readonly Vector2[][]?[] Glyphs = new Vector2[][]?[LastChar - FirstChar + 1];
    private static readonly float[] Advances = new float[LastChar - FirstChar + 1];

    static CasinoSigns()
    {
        Define('A', "0,6 0,2 2,0 4,2 4,6|0,3.6 4,3.6");
        Define('B', "0,3 0,0 3,0 4,1 4,2 3,3 0,3 0,6 3,6 4,5 4,4 3,3");
        Define('C', "4,1 3,0 1,0 0,1 0,5 1,6 3,6 4,5");
        Define('D', "0,0 0,6 2.4,6 4,4.4 4,1.6 2.4,0 0,0");
        Define('E', "4,0 0,0 0,6 4,6|0,3 3,3");
        Define('F', "4,0 0,0 0,6|0,3 3,3");
        Define('G', "4,1 3,0 1,0 0,1 0,5 1,6 3,6 4,5 4,3.5 2.4,3.5");
        Define('H', "0,0 0,6|4,0 4,6|0,3 4,3");
        Define('I', "1,0 3,0|2,0 2,6|1,6 3,6");
        Define('J', "1,0 4,0|3,0 3,5 2,6 1,6 0,5");
        Define('K', "0,0 0,6|4,0 0,3.6|1.3,2.8 4,6");
        Define('L', "0,0 0,6 4,6");
        Define('M', "0,6 0,0 2,3 4,0 4,6");
        Define('N', "0,6 0,0 4,6 4,0");
        Define('O', "1,0 3,0 4,1 4,5 3,6 1,6 0,5 0,1 1,0");
        Define('P', "0,6 0,0 3,0 4,1 4,2 3,3 0,3");
        Define('Q', "1,0 3,0 4,1 4,5 3,6 1,6 0,5 0,1 1,0|2.6,4.6 4,6");
        Define('R', "0,6 0,0 3,0 4,1 4,2 3,3 0,3|1.6,3 4,6");
        Define('S', "4,1 3,0 1,0 0,1 0,2 1,3 3,3 4,4 4,5 3,6 1,6 0,5");
        Define('T', "0,0 4,0|2,0 2,6");
        Define('U', "0,0 0,5 1,6 3,6 4,5 4,0");
        Define('V', "0,0 2,6 4,0");
        Define('W', "0,0 1,6 2,3 3,6 4,0");
        Define('X', "0,0 4,6|4,0 0,6");
        Define('Y', "0,0 2,3 4,0|2,3 2,6");
        Define('Z', "0,0 4,0 0,6 4,6");
        Define('0', "1,0 3,0 4,1 4,5 3,6 1,6 0,5 0,1 1,0|3.4,1 0.6,5");
        Define('1', "1,1.2 2,0 2,6|1,6 3,6");
        Define('2', "0,1 1,0 3,0 4,1 4,2 0,6 4,6");
        Define('3', "0,1 1,0 3,0 4,1 4,2 3,3 4,4 4,5 3,6 1,6 0,5|1.6,3 3,3");
        Define('4', "3,6 3,0 0,4 4,4");
        Define('5', "4,0 0,0 0,3 3,3 4,4 4,5 3,6 0,6");
        Define('6', "4,1 3,0 1,0 0,1 0,5 1,6 3,6 4,5 4,4 3,3 0,3");
        Define('7', "0,0 4,0 1.6,6");
        Define('8', "1,3 0,2 0,1 1,0 3,0 4,1 4,2 3,3 1,3 0,4 0,5 1,6 3,6 4,5 4,4 3,3");
        Define('9', "4,3 1,3 0,2 0,1 1,0 3,0 4,1 4,5 3,6 1,6 0,5");
        Define('\'', "1,0 1,1.6", 2f);
        Define('-', "0.5,3.2 3.5,3.2");
        Define('!', "1,0 1,4|1,5.6 1,6", 2f);
        Advances[' ' - FirstChar] = SpaceAdvance;
    }

    public static string Text(CasinoSign sign) => Words[(int)sign];

    public static bool HasGlyph(char character) => character == ' ' || GlyphFor(character) is not null;

    public static float Measure(CasinoSign sign, float height) => Measure(Words[(int)sign], height);

    public static float Measure(ReadOnlySpan<char> text, float height)
    {
        var unit = height / GridHeight;
        var width = 0f;
        for (var index = 0; index < text.Length; index++)
        {
            width += Advance(text[index]) * unit;
            if (index < text.Length - 1)
            {
                width += LetterGap * unit;
            }
        }

        return width;
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
        if (height <= 0f || text.Length == 0)
        {
            return;
        }

        var unit = height / GridHeight;
        var width = Measure(text, height);
        var origin = new Vector2(center.X - width * 0.5f, center.Y - height * 0.5f);
        var tint = color with { W = color.W * Math.Clamp(lit, 0f, 1f) };
        var stroke = MathF.Max(1f, height * StrokeFraction);
        Span<Vector2> points = stackalloc Vector2[MaxStrokePoints];
        for (var index = 0; index < text.Length; index++)
        {
            var glyph = GlyphFor(text[index]);
            if (glyph is not null)
            {
                for (var strokeIndex = 0; strokeIndex < glyph.Length; strokeIndex++)
                {
                    var source = glyph[strokeIndex];
                    var count = Math.Min(source.Length, MaxStrokePoints);
                    for (var pointIndex = 0; pointIndex < count; pointIndex++)
                    {
                        points[pointIndex] = origin + source[pointIndex] * unit;
                    }

                    CasinoLights.NeonTube(drawList, points[..count], tint, stroke, lit);
                }
            }

            origin.X += (Advance(text[index]) + LetterGap) * unit;
        }
    }

    private static float Advance(char character)
    {
        var slot = character - FirstChar;
        if (slot < 0 || slot >= Advances.Length)
        {
            return GridWidth;
        }

        var advance = Advances[slot];
        return advance > 0f ? advance : GridWidth;
    }

    private static Vector2[][]? GlyphFor(char character)
    {
        var slot = character - FirstChar;
        return slot < 0 || slot >= Glyphs.Length ? null : Glyphs[slot];
    }

    private static void Define(char character, string strokes, float advance = GridWidth)
    {
        var parts = strokes.Split('|');
        var glyph = new Vector2[parts.Length][];
        for (var partIndex = 0; partIndex < parts.Length; partIndex++)
        {
            var pairs = parts[partIndex].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var points = new Vector2[pairs.Length];
            for (var pairIndex = 0; pairIndex < pairs.Length; pairIndex++)
            {
                var comma = pairs[pairIndex].IndexOf(',');
                points[pairIndex] = new Vector2(
                    float.Parse(pairs[pairIndex].AsSpan(0, comma), CultureInfo.InvariantCulture),
                    float.Parse(pairs[pairIndex].AsSpan(comma + 1), CultureInfo.InvariantCulture));
            }

            glyph[partIndex] = points;
        }

        Glyphs[character - FirstChar] = glyph;
        Advances[character - FirstChar] = advance;
    }
}
