namespace Aetherphone.Core.Video;

internal enum MediaTrackKind : byte
{
    Video,
    Audio,
    Subtitle,
}

internal readonly record struct MediaTrack(int Id, MediaTrackKind Kind, string Title, string Language, string Codec,
    bool Selected);

internal static class MediaTracks
{
    internal static MediaTrackKind? KindOf(string? type) => type switch
    {
        "video" => MediaTrackKind.Video,
        "audio" => MediaTrackKind.Audio,
        "sub" => MediaTrackKind.Subtitle,
        _ => null,
    };

    internal static string Property(MediaTrackKind kind) => kind switch
    {
        MediaTrackKind.Audio => "aid",
        MediaTrackKind.Subtitle => "sid",
        _ => "vid",
    };

    internal static int Count(MediaTrack[] tracks, MediaTrackKind kind)
    {
        var count = 0;
        for (var index = 0; index < tracks.Length; index++)
        {
            if (tracks[index].Kind == kind)
            {
                count++;
            }
        }

        return count;
    }

    internal static bool OffersChoice(MediaTrack[] tracks) =>
        Count(tracks, MediaTrackKind.Audio) > 1 || Count(tracks, MediaTrackKind.Subtitle) > 0;

    internal static string Label(in MediaTrack track, string fallback)
    {
        var title = track.Title.Trim();
        var language = track.Language.Trim();
        if (title.Length > 0 && language.Length > 0
            && !title.Contains(language, StringComparison.OrdinalIgnoreCase))
        {
            return title + " (" + language.ToUpperInvariant() + ")";
        }

        if (title.Length > 0)
        {
            return title;
        }

        return language.Length > 0 ? language.ToUpperInvariant() : fallback;
    }
}
