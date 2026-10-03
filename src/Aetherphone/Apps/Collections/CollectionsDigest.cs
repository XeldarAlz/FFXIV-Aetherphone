using Aetherphone.Core.Collections;

namespace Aetherphone.Apps.Collections;

internal enum TileMode : byte
{
    Plain,
    Loading,
    Ring,
    Private,
    NotLinked,
}

internal enum RowBadge : byte
{
    None,
    Owned,
    Missing,
}

internal readonly record struct DigestRow(CollectionItem Item, string Trailing, RowBadge Badge);

internal sealed class TileState
{
    public TileMode Mode;
    public float Fraction;
    public string Percent = string.Empty;
    public string Count = string.Empty;
}

internal sealed class CollectionsDigest
{
    public const int RecentLimit = 5;
    public const int UpNextLimit = 5;

    public readonly TileState[] Tiles = CreateTiles();
    public readonly List<DigestRow> Recent = new();
    public readonly List<DigestRow> Wishlist = new();
    public readonly List<DigestRow> UpNext = new();
    public readonly List<CollectionItem> Candidates = new();
    public bool HasOverall;
    public float OverallFraction;
    public string OverallPercent = string.Empty;
    public string OverallCount = string.Empty;
    public string NewThisWeek = string.Empty;
    public string CharacterName = string.Empty;

    private bool valid;
    private int catalogRevision;
    private int journalRevision;
    private long minute;
    private string language = string.Empty;

    public void Invalidate() => valid = false;

    public bool IsCurrent(int catalogStamp, int journalStamp, long minuteStamp, string languageCode) =>
        valid && catalogRevision == catalogStamp && journalRevision == journalStamp && minute == minuteStamp &&
        string.Equals(language, languageCode, StringComparison.Ordinal);

    public void Stamp(int catalogStamp, int journalStamp, long minuteStamp, string languageCode)
    {
        valid = true;
        catalogRevision = catalogStamp;
        journalRevision = journalStamp;
        minute = minuteStamp;
        language = languageCode;
    }

    public void Clear()
    {
        Recent.Clear();
        Wishlist.Clear();
        UpNext.Clear();
        Candidates.Clear();
        HasOverall = false;
        OverallFraction = 0f;
        OverallPercent = string.Empty;
        OverallCount = string.Empty;
        NewThisWeek = string.Empty;
        CharacterName = string.Empty;
    }

    private static TileState[] CreateTiles()
    {
        var tiles = new TileState[CollectionCategories.All.Length];
        for (var index = 0; index < tiles.Length; index++)
        {
            tiles[index] = new TileState();
        }

        return tiles;
    }
}
