using System.Globalization;

namespace Aetherphone.Apps.Games.Fling;

internal enum FlingMaterial : byte
{
    Wood,
    Glass,
    Stone,
    Rock,
}

internal enum FlingPieceKind : byte
{
    Block,
    Goblin,
    Knight,
}

internal enum FlingBird : byte
{
    Normal,
    Splitter,
    Heavy,
}

internal readonly struct FlingPiece
{
    public readonly FlingPieceKind Kind;
    public readonly FlingMaterial Material;
    public readonly Vector2 Center;
    public readonly Vector2 HalfExtents;

    public FlingPiece(FlingPieceKind kind, FlingMaterial material, Vector2 center, Vector2 halfExtents)
    {
        Kind = kind;
        Material = material;
        Center = center;
        HalfExtents = halfExtents;
    }

    public bool IsGoblin => Kind != FlingPieceKind.Block;

    public bool IsStatic => Kind == FlingPieceKind.Block && Material == FlingMaterial.Rock;
}

internal readonly struct FlingLevel
{
    private const float TwoStarShare = 0.55f;
    private const float ThreeStarShare = 0.78f;
    private const int StarRounding = 1000;

    public readonly FlingBird[] Birds;
    public readonly FlingPiece[] Pieces;
    public readonly int GoblinCount;
    public readonly int TwoStarScore;
    public readonly int ThreeStarScore;
    public readonly float FocusX;
    public readonly float RightEdge;

    public FlingLevel(FlingBird[] birds, FlingPiece[] pieces)
    {
        Birds = birds;
        Pieces = pieces;
        var goblins = 0;
        var blockPoints = 0;
        var left = float.MaxValue;
        var right = float.MinValue;
        for (var index = 0; index < pieces.Length; index++)
        {
            var piece = pieces[index];
            left = MathF.Min(left, piece.Center.X - piece.HalfExtents.X);
            right = MathF.Max(right, piece.Center.X + piece.HalfExtents.X);
            if (piece.IsGoblin)
            {
                goblins++;
                continue;
            }

            blockPoints += FlingLevels.PointsFor(piece.Material);
        }

        GoblinCount = goblins;
        FocusX = pieces.Length > 0 ? (left + right) * 0.5f : 0f;
        RightEdge = pieces.Length > 0 ? right : 0f;
        var par = goblins * FlingLevels.GoblinPoints + blockPoints +
                  Math.Max(0, birds.Length - 1) * FlingLevels.SpareBirdPoints;
        TwoStarScore = Round(par * TwoStarShare);
        ThreeStarScore = Math.Max(TwoStarScore + StarRounding, Round(par * ThreeStarShare));
    }

    private static int Round(float points) => (int)MathF.Round(points / StarRounding) * StarRounding;
}

internal static class FlingLevels
{
    public const int Count = 40;
    public const float Plank = 0.25f;
    public const float GoblinRadius = 0.35f;
    public const float KnightRadius = 0.38f;
    public const int GoblinPoints = 5000;
    public const int SpareBirdPoints = 10000;
    private const int WoodPoints = 500;
    private const int GlassPoints = 300;
    private const int StonePoints = 800;

    private static readonly string[] Sources =
    {
        "nnn|Hw 16 0 2 1.5;G 16 0",
        "nnn|Bw 17 0 1;Bw 17 1 1;Bw 17 2 1;G 17 3",
        "nnn|Hw 15 0 2 1.5;G 15 0;Hw 18.5 0 2 1.5;G 18.5 0",
        "nnn|Hg 16 0 2 1.5;G 16 0;G 16 1.75;Hw 19.5 0 2 1.5;G 19.5 0",
        "nnn|Bw 16.5 0 1;Bw 17.5 0 1;Bw 18.5 0 1;Bw 17 1 1;Bw 18 1 1;Bw 17.5 2 1;G 17.5 3;G 19.8 0",
        "nns|Hg 15 0 2 1.5;G 15 0;Hg 17.6 0 2 1.5;G 17.6 0;Hg 20.2 0 2 1.5;G 20.2 0",
        "nns|Hw 17 0 2 1.5;G 17 0;Hw 17 1.75 2 1.5;G 17 1.75;G 17 3.5",
        "nnn|R 18 0 4 2;Hw 18 2 2.5 1.5;G 18 2;G 15.4 0",
        "nsn|Vg 15 0 2.5;Vg 16 0 2.5;Pg 15.5 2.5 1.5;G 15.5 2.75;Vg 17.5 0 2.5;Vg 18.5 0 2.5;Pg 18 2.5 1.5;" +
        "G 18 2.75;Vg 20 0 2.5;Vg 21 0 2.5;Pg 20.5 2.5 1.5;G 20.5 2.75",
        "nnh|Hs 17.5 0 2.4 1.6;K 17.5 0;G 17.5 1.85",
        "nns|Bw 15.5 0 1;Bw 15.5 1 1;Bw 19.5 0 1;Bw 19.5 1 1;Pw 17.5 2 5;G 16.6 2.25;G 18.4 2.25;G 17.5 0",
        "nnn|Hw 15 0 2 1.5;G 15 0;R 18.2 0 2.4 1;Hw 18.2 1 2 1.5;G 18.2 1;R 21.4 0 2.4 2;Hw 21.4 2 2 1.5;G 21.4 2",
        "nsh|Hs 17.5 0 3 1.5;K 17.5 0;Hw 17.5 1.75 3 1.5;G 17.5 1.75;G 17.5 3.5",
        "nnss|Hw 15.5 0 2 1.5;Hw 17.5 0 2 1.5;Hw 19.5 0 2 1.5;G 15.5 0;G 17.5 0;G 19.5 0;Pw 17.5 1.75 6;" +
        "Hg 16.5 2 2 1.5;Hg 18.5 2 2 1.5;G 16.5 2;G 18.5 2",
        "nhn|Bs 16.5 0 1;Bs 17.5 0 1;Bs 18.5 0 1;Bs 19.5 0 1;Bs 17 1 1;Bs 18 1 1;Bs 19 1 1;Bs 17.5 2 1;" +
        "Bs 18.5 2 1;Bs 18 3 1;K 18 4;G 15.2 0",
        "snn|Vg 15 0 2;Vg 17 0 2;Vg 19 0 2;Vg 21 0 2;Pg 16 2 2.25;Pg 20 2 2.25;Pg 18 2.25 4;G 16 0;G 18 0;" +
        "G 20 0;G 17 2.5;G 19 2.5",
        "nnh|R 19 0 5 2.5;G 19 2.5;Hw 19 2.5 2.4 1.4;Bw 19 4.15 1;Bw 19 5.15 1;G 19 6.15;G 16 0",
        "nsh|Hw 16 0 2 1.5;Hw 16 1.75 2 1.5;Hg 16 3.5 2 1.5;G 16 0;G 16 5.25;Hw 20 0 2 1.5;Hw 20 1.75 2 1.5;" +
        "Hg 20 3.5 2 1.5;G 20 0;G 20 5.25",
        "nnn|Vw 15.5 0 2;Vw 16.2 0 2;Vw 16.9 0 2;Bw 18.5 0 1;G 18.5 1;G 20.2 0;Bw 21.4 0 1;Bw 21.4 1 1;G 21.4 2",
        "nshn|Hs 17 0 2.4 1.4;Hs 19.4 0 2.4 1.4;K 17 0;K 19.4 0;Pw 18.2 1.65 4.8;Hw 18.2 1.9 3 1.5;G 18.2 1.9;" +
        "Hg 18.2 3.65 1.6 1.2;G 18.2 5.1",
        "nnn|R 17 0 3 1;Bg 16.5 1 1;Bg 17.5 1 1;G 16.5 2;G 17.5 2;R 21 0 3 3;Hw 21 3 2 1.5;G 21 3",
        "ssn|Vg 15 0 1.5;Vg 16.6 0 1.5;Vg 18.2 0 1.5;Vg 19.8 0 1.5;Vg 21.4 0 1.5;Pg 15.8 1.5 1.85;" +
        "Pg 19 1.5 1.85;Pw 21.4 1.5 0.75;G 15.8 0;G 17.4 0;G 19 0;G 20.6 0",
        "nhh|Hs 18 0 3.2 1.5;K 17.5 0;K 18.5 0;Hs 18 1.75 2.4 1.5;K 18 1.75;G 18 3.5",
        "nns|Bw 15.5 0 1;Bw 15.5 1 1;Bw 15.5 2 1;G 15.5 3;Bg 17.5 0 1;Bg 17.5 1 1;Bg 17.5 2 1;Bg 17.5 3 1;" +
        "G 17.5 4;Bw 19.5 0 1;Bw 19.5 1 1;Bw 19.5 2 1;G 19.5 3",
        "nsnh|R 17 0 2.5 1.5;Hw 17 1.5 2 1.5;G 17 1.5;R 20 0 2.5 2.5;Hs 20 2.5 2 1.5;K 20 2.5;R 23 0 2.5 3.5;" +
        "Hg 23 3.5 2 1.5;G 23 3.5",
        "nnss|Bw 16.5 0 1;Bw 17.5 0 1;Bw 18.5 0 1;Bw 19.5 0 1;Bw 17 1 1;Bw 18 1 1;Bw 19 1 1;Bw 17.5 2 1;" +
        "Bw 18.5 2 1;Bw 18 3 1;G 18 4;G 15 0;G 21 0;Hg 15 0 1.6 1.2;Hg 21 0 1.6 1.2",
        "nhn|Hs 16 0 2 1.5;K 16 0;G 16 1.75;Hw 16 1.75 2 1.5;Hs 20 0 2 1.5;K 20 0;G 20 1.75;Hw 20 1.75 2 1.5;" +
        "Pw 18 3.5 6;G 16 3.75;G 18 3.75;G 20 3.75",
        "snsn|Hg 15.5 0 1.6 1.2;G 15.5 0;Hg 17.5 0 1.6 1.2;G 17.5 0;Hg 19.5 0 1.6 1.2;G 19.5 0;" +
        "Hg 21.5 0 1.6 1.2;G 21.5 0;Pw 16.5 1.45 2.5;Pw 20.5 1.45 2.5;G 16.5 1.7;G 20.5 1.7",
        "nhs|R 18.5 0 6 1.5;Bs 16.5 1.5 1;Bs 20.5 1.5 1;Pw 18.5 2.5 5;K 17.6 2.75;K 19.4 2.75;G 18.5 1.5",
        "nnhs|Hw 16 0 2 2;G 16 0;Hw 16 2.25 2 2;G 16 2.25;G 16 4.5;Hs 19.5 0 2.4 1.5;K 19.5 0;" +
        "Hg 19.5 1.75 2.4 1.5;G 19.5 1.75;G 19.5 3.5",
        "hnn|Bs 16 0 1;Bs 17 0 1;Bs 18 0 1;Bs 16.5 1 1;Bs 17.5 1 1;Bs 17 2 1;K 17 3;Bw 20 0 1;Bw 21 0 1;" +
        "Bw 22 0 1;Bw 20.5 1 1;Bw 21.5 1 1;Bw 21 2 1;G 21 3;G 19 0",
        "nsns|R 19 0 8 1;Vg 16.5 1 1.6;Vg 18.2 1 1.6;Vg 19.9 1 1.6;Vg 21.6 1 1.6;Pg 17.35 2.6 1.95;" +
        "Pg 20.75 2.6 1.95;G 17.35 1;G 20.75 1;G 19.05 1;G 17.35 2.85;G 20.75 2.85",
        "nhsn|Hs 16.5 0 2.4 1.5;K 16.5 0;Hs 19.5 0 2.4 1.5;K 19.5 0;Ps 18 1.75 5.4;Hw 18 2 3.4 1.5;G 17.4 2;" +
        "G 18.6 2;Hw 18 3.75 2 1.2;G 18 5.2",
        "nhn|R 16 0 3 1;R 19 0 3 2;R 22 0 3 3;Bg 16 1 1;Bg 16 2 1;G 16 3;Bw 19 2 1;Bw 19 3 1;G 19 4;Bs 22 3 1;" +
        "Bs 22 4 1;K 22 5",
        "snhn|Hw 15.5 0 2.2 1.4;G 15.5 0;Hw 18 0 2.2 1.4;G 18 0;Hw 20.5 0 2.2 1.4;G 20.5 0;Pw 16.75 1.65 2.5;" +
        "Pw 19.25 1.65 2.5;Hg 16.75 1.9 1.6 1.2;G 16.75 1.9;Hg 19.25 1.9 1.6 1.2;G 19.25 1.9",
        "hns|Hs 17 0 3 2;K 16.4 0;K 17.6 0;Hs 17 2.25 3 1.5;K 17 2.25;Bs 17 4 1;G 17 5;Hw 20.5 0 2 1.5;G 20.5 0",
        "nssn|R 19 0 10 0.75;Bg 15.5 0.75 1;Bg 15.5 1.75 1;G 15.5 2.75;Bg 17.5 0.75 1;Bg 17.5 1.75 1;" +
        "G 17.5 2.75;Bg 19.5 0.75 1;Bg 19.5 1.75 1;G 19.5 2.75;Bg 21.5 0.75 1;Bg 21.5 1.75 1;G 21.5 2.75;" +
        "G 16.5 0.75;G 20.5 0.75",
        "nhsh|Bs 16.5 0 1;Bs 17.5 0 1;Bs 18.5 0 1;Bs 17 1 1;Bs 18 1 1;Bs 17.5 2 1;Pw 17.5 3 2.4;" +
        "Hw 17.5 3.25 2 1.5;G 17.5 3.25;R 21.5 0 3 2.5;Hs 21.5 2.5 2.4 1.6;K 21.5 2.5;G 21.5 4.35",
        "nshs|Hs 15.5 0 2 1.5;K 15.5 0;Hw 15.5 1.75 2 1.5;G 15.5 1.75;G 15.5 3.5;Hs 21.5 0 2 1.5;K 21.5 0;" +
        "Hw 21.5 1.75 2 1.5;G 21.5 1.75;G 21.5 3.5;Hw 18.5 0 2.6 2;G 18.5 0;Hg 18.5 2.25 2.6 1.5;" +
        "G 18.5 2.25;G 18.5 4",
        "hshn|R 18.5 0 9 1;Hs 16 1 2.4 1.5;K 16 1;Hs 21 1 2.4 1.5;K 21 1;Hw 18.5 1 2 1.5;G 18.5 1;" +
        "Ps 18.5 2.75 7.4;Hg 17.25 3 2.2 1.4;G 17.25 3;Hg 19.75 3 2.2 1.4;G 19.75 3;Pw 18.5 4.65 4.4;" +
        "K 18.5 4.9",
    };

    private static readonly FlingLevel[] Levels = BuildLevels();

    public static FlingLevel Get(int level) => Levels[Math.Clamp(level, 1, Count) - 1];

    public static int PointsFor(FlingMaterial material) => material switch
    {
        FlingMaterial.Wood => WoodPoints,
        FlingMaterial.Glass => GlassPoints,
        FlingMaterial.Stone => StonePoints,
        _ => 0,
    };

    public static FlingLevel Parse(string source)
    {
        var halves = source.Split('|');
        if (halves.Length != 2 || halves[0].Length == 0)
        {
            throw new FormatException("A level needs birds and pieces: " + source);
        }

        var birds = new FlingBird[halves[0].Length];
        for (var index = 0; index < birds.Length; index++)
        {
            birds[index] = halves[0][index] switch
            {
                'n' => FlingBird.Normal,
                's' => FlingBird.Splitter,
                'h' => FlingBird.Heavy,
                _ => throw new FormatException("Unknown bird: " + halves[0][index]),
            };
        }

        var pieces = new List<FlingPiece>();
        var entries = halves[1].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var entryIndex = 0; entryIndex < entries.Length; entryIndex++)
        {
            AddEntry(pieces, entries[entryIndex]);
        }

        return new FlingLevel(birds, pieces.ToArray());
    }

    private static FlingLevel[] BuildLevels()
    {
        var levels = new FlingLevel[Sources.Length];
        for (var index = 0; index < Sources.Length; index++)
        {
            levels[index] = Parse(Sources[index]);
        }

        return levels;
    }

    private static void AddEntry(List<FlingPiece> pieces, string entry)
    {
        var tokens = entry.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var values = new float[tokens.Length - 1];
        for (var index = 0; index < values.Length; index++)
        {
            values[index] = float.Parse(tokens[index + 1], NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        var code = tokens[0];
        switch (code[0])
        {
            case 'G':
                Expect(values, 2, entry);
                AddGoblin(pieces, FlingPieceKind.Goblin, values[0], values[1], GoblinRadius);
                return;
            case 'K':
                Expect(values, 2, entry);
                AddGoblin(pieces, FlingPieceKind.Knight, values[0], values[1], KnightRadius);
                return;
            case 'R':
                Expect(values, 4, entry);
                AddBlock(pieces, FlingMaterial.Rock, values[0], values[1], values[2] * 0.5f, values[3] * 0.5f);
                return;
        }

        var material = MaterialOf(code, entry);
        switch (code[0])
        {
            case 'V':
                Expect(values, 3, entry);
                AddBlock(pieces, material, values[0], values[1], Plank * 0.5f, values[2] * 0.5f);
                return;
            case 'P':
                Expect(values, 3, entry);
                AddBlock(pieces, material, values[0], values[1], values[2] * 0.5f, Plank * 0.5f);
                return;
            case 'B':
                Expect(values, 3, entry);
                AddBlock(pieces, material, values[0], values[1], values[2] * 0.5f, values[2] * 0.5f);
                return;
            case 'H':
                Expect(values, 4, entry);
                AddHut(pieces, material, values[0], values[1], values[2], values[3]);
                return;
            default:
                throw new FormatException("Unknown piece: " + entry);
        }
    }

    private static FlingMaterial MaterialOf(string code, string entry)
    {
        if (code.Length != 2)
        {
            throw new FormatException("A block needs a material: " + entry);
        }

        return code[1] switch
        {
            'w' => FlingMaterial.Wood,
            'g' => FlingMaterial.Glass,
            's' => FlingMaterial.Stone,
            _ => throw new FormatException("Unknown material: " + entry),
        };
    }

    private static void Expect(float[] values, int count, string entry)
    {
        if (values.Length != count)
        {
            throw new FormatException("Wrong field count: " + entry);
        }
    }

    private static void AddHut(List<FlingPiece> pieces, FlingMaterial material, float x, float bottom, float width,
        float height)
    {
        var postOffset = width * 0.5f - Plank * 0.5f;
        AddBlock(pieces, material, x - postOffset, bottom, Plank * 0.5f, height * 0.5f);
        AddBlock(pieces, material, x + postOffset, bottom, Plank * 0.5f, height * 0.5f);
        AddBlock(pieces, material, x, bottom + height, width * 0.5f, Plank * 0.5f);
    }

    private static void AddBlock(List<FlingPiece> pieces, FlingMaterial material, float x, float bottom,
        float halfWidth, float halfHeight)
    {
        var center = new Vector2(x, -(bottom + halfHeight));
        pieces.Add(new FlingPiece(FlingPieceKind.Block, material, center, new Vector2(halfWidth, halfHeight)));
    }

    private static void AddGoblin(List<FlingPiece> pieces, FlingPieceKind kind, float x, float bottom, float radius)
    {
        var center = new Vector2(x, -(bottom + radius));
        pieces.Add(new FlingPiece(kind, FlingMaterial.Wood, center, new Vector2(radius, radius)));
    }
}
