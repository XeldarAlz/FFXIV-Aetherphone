namespace Aetherphone.Core.Songs;

internal sealed class SongHistory
{
    private const int Capacity = 12;

    private readonly Configuration configuration;

    public SongHistory(Configuration configuration)
    {
        this.configuration = configuration;
    }

    public Song[] Recent(int max)
    {
        var source = configuration.SongRecents;
        var count = Math.Min(max, source.Count);
        if (count <= 0)
        {
            return Array.Empty<Song>();
        }

        var songs = new Song[count];
        for (var index = 0; index < count; index++)
        {
            songs[index] = source[index].ToSong();
        }

        return songs;
    }

    public void Record(in Song song)
    {
        if (string.IsNullOrEmpty(song.VideoId))
        {
            return;
        }

        var list = configuration.SongRecents;
        for (var index = 0; index < list.Count; index++)
        {
            if (string.Equals(list[index].VideoId, song.VideoId, StringComparison.Ordinal))
            {
                list.RemoveAt(index);
                break;
            }
        }

        list.Insert(0, SongRecord.From(song));
        while (list.Count > Capacity)
        {
            list.RemoveAt(list.Count - 1);
        }

        configuration.Save();
    }
}
