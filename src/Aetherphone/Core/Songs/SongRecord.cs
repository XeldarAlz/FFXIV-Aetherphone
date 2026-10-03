namespace Aetherphone.Core.Songs;

[Serializable]
internal sealed class SongRecord
{
    public string VideoId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public int DurationSeconds { get; set; }
    public string ChannelId { get; set; } = string.Empty;
    public long AddedUnix { get; set; }

    public Song ToSong()
    {
        return new Song(VideoId, Title, Author, ThumbnailUrl, DurationSeconds, ChannelId);
    }

    public static SongRecord From(in Song song)
    {
        return new SongRecord
        {
            VideoId = song.VideoId,
            Title = song.Title,
            Author = song.Author,
            ThumbnailUrl = song.ThumbnailUrl,
            DurationSeconds = song.DurationSeconds,
            ChannelId = song.ChannelId,
            AddedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        };
    }
}
