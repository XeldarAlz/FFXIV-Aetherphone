namespace Aetherphone.Apps.Games.Mahjong;

internal enum TileKind : byte
{
    Crystal,
    Role,
    Gil,
    Wind,
    Dragon,
    Season,
    Flower,
}

internal static class MahjongTiles
{
    public const int SuitSize = 9;
    public const int FirstCrystal = 0;
    public const int FirstRole = 9;
    public const int FirstGil = 18;
    public const int FirstWind = 27;
    public const int FirstDragon = 31;
    public const int FirstSeason = 34;
    public const int FirstFlower = 38;
    public const int FaceCount = 42;
    public const int GroupCount = 36;
    public const int SeasonGroup = 34;
    public const int FlowerGroup = 35;
    public const int CopiesPerFace = 4;
    public const int PairCount = 72;

    private static readonly byte[] PairFaces = BuildPairFaces();

    public static int Group(int face)
    {
        if (face < FirstSeason)
        {
            return face;
        }

        return face < FirstFlower ? SeasonGroup : FlowerGroup;
    }

    public static bool Matches(int first, int second) => Group(first) == Group(second);

    public static TileKind KindOf(int face)
    {
        if (face < FirstRole)
        {
            return TileKind.Crystal;
        }

        if (face < FirstGil)
        {
            return TileKind.Role;
        }

        if (face < FirstWind)
        {
            return TileKind.Gil;
        }

        if (face < FirstDragon)
        {
            return TileKind.Wind;
        }

        if (face < FirstSeason)
        {
            return TileKind.Dragon;
        }

        return face < FirstFlower ? TileKind.Season : TileKind.Flower;
    }

    public static int Rank(int face) => face < FirstWind ? face % SuitSize + 1 : 0;

    public static int Variant(int face) => KindOf(face) switch
    {
        TileKind.Wind => face - FirstWind,
        TileKind.Dragon => face - FirstDragon,
        TileKind.Season => face - FirstSeason,
        TileKind.Flower => face - FirstFlower,
        _ => face % SuitSize,
    };

    public static int PairFirst(int pair) => PairFaces[pair * 2];

    public static int PairSecond(int pair) => PairFaces[pair * 2 + 1];

    private static byte[] BuildPairFaces()
    {
        var faces = new byte[PairCount * 2];
        var cursor = 0;
        for (var face = 0; face < FirstSeason; face++)
        {
            for (var copy = 0; copy < CopiesPerFace; copy++)
            {
                faces[cursor++] = (byte)face;
            }
        }

        for (var face = FirstSeason; face < FaceCount; face++)
        {
            faces[cursor++] = (byte)face;
        }

        return faces;
    }
}
