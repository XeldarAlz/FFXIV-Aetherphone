using Aetherphone.Core.Songs;
using Aetherphone.Core.SystemMedia;

namespace Aetherphone.Core.Shell;

[Flags]
internal enum IslandMediaControls : ushort
{
    None = 0,
    Playing = 1 << 0,
    PlayPause = 1 << 1,
    Previous = 1 << 2,
    Next = 1 << 3,
    Seek = 1 << 4,
    Shuffle = 1 << 5,
    ShuffleActive = 1 << 6,
    Repeat = 1 << 7,
    RepeatActive = 1 << 8,
    RepeatOne = 1 << 9,
    Stop = 1 << 10,
}

internal enum IslandMediaSource : byte
{
    Phone,
    Pc,
    Video,
}

internal readonly record struct IslandMedia(string Title, string Subtitle, float Position, float Duration,
    IslandMediaControls Controls)
{
    public bool Has(IslandMediaControls control) => (Controls & control) == control;

    public bool HasTimeline => Duration > 0f;

    public static IslandMedia ForPhone(string title, string subtitle, float position, float duration, bool playing,
        bool song, bool shuffleActive, SongRepeatMode repeat)
    {
        var controls = IslandMediaControls.PlayPause | IslandMediaControls.Previous | IslandMediaControls.Next |
                       When(playing, IslandMediaControls.Playing) |
                       When(song && duration > 0f, IslandMediaControls.Seek);
        if (song)
        {
            controls |= IslandMediaControls.Shuffle | IslandMediaControls.Repeat |
                        When(shuffleActive, IslandMediaControls.ShuffleActive) |
                        When(repeat != SongRepeatMode.Off, IslandMediaControls.RepeatActive) |
                        When(repeat == SongRepeatMode.One, IslandMediaControls.RepeatOne);
        }
        else
        {
            controls |= IslandMediaControls.Stop;
        }

        return song
            ? new IslandMedia(title, subtitle, MathF.Max(0f, position), MathF.Max(0f, duration), controls)
            : new IslandMedia(title, subtitle, 0f, 0f, controls);
    }

    public static IslandMedia ForSession(in MediaSessionSnapshot snapshot, string title, string subtitle,
        long utcTicks)
    {
        var controls = When(snapshot.IsPlaying, IslandMediaControls.Playing) |
                       When(snapshot.CanPlayPause, IslandMediaControls.PlayPause) |
                       When(snapshot.CanPrevious, IslandMediaControls.Previous) |
                       When(snapshot.CanNext, IslandMediaControls.Next) |
                       When(snapshot.CanSeek, IslandMediaControls.Seek);
        if (snapshot.CanShuffle)
        {
            controls |= IslandMediaControls.Shuffle | When(snapshot.ShuffleActive, IslandMediaControls.ShuffleActive);
        }

        if (snapshot.CanRepeat)
        {
            controls |= IslandMediaControls.Repeat |
                        When(snapshot.Repeat != MediaSessionRepeat.None, IslandMediaControls.RepeatActive) |
                        When(snapshot.Repeat == MediaSessionRepeat.Track, IslandMediaControls.RepeatOne);
        }

        if (snapshot.Duration <= TimeSpan.Zero)
        {
            return new IslandMedia(title, subtitle, 0f, 0f, controls);
        }

        return new IslandMedia(title, subtitle, (float)snapshot.PositionAt(utcTicks).TotalSeconds,
            (float)snapshot.Duration.TotalSeconds, controls);
    }

    public static IslandMedia ForVideo(string title, string subtitle, float position, float duration, bool loaded,
        bool playing, bool canDrive, bool hasNext)
    {
        var drivable = loaded && canDrive;
        var controls = When(loaded && playing, IslandMediaControls.Playing) |
                       When(drivable, IslandMediaControls.PlayPause) |
                       When(drivable && hasNext, IslandMediaControls.Next) |
                       When(drivable && duration > 0f, IslandMediaControls.Seek);
        return loaded && duration > 0f
            ? new IslandMedia(title, subtitle, Math.Clamp(position, 0f, duration), duration, controls)
            : new IslandMedia(title, subtitle, 0f, 0f, controls);
    }

    private static IslandMediaControls When(bool condition, IslandMediaControls control) =>
        condition ? control : IslandMediaControls.None;
}
