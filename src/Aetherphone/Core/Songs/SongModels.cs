namespace Aetherphone.Core.Songs;

internal enum SongSearchScope : byte
{
    Songs,
    LongPlays,
    All,
}

internal readonly struct Song
{
    public readonly string VideoId;
    public readonly string Title;
    public readonly string Author;
    public readonly string ThumbnailUrl;
    public readonly int DurationSeconds;

    public Song(string videoId, string title, string author, string thumbnailUrl, int durationSeconds)
    {
        VideoId = videoId;
        Title = title;
        Author = author;
        ThumbnailUrl = thumbnailUrl;
        DurationSeconds = durationSeconds;
    }
}
