namespace Aetherphone.Core.SystemMedia;

internal readonly struct MediaSessionSnapshot
{
    public static readonly MediaSessionSnapshot Empty = new(string.Empty, string.Empty, string.Empty, string.Empty,
        string.Empty, MediaSessionPlayback.Closed, MediaSessionControls.None, TimeSpan.Zero, TimeSpan.Zero, 0, null,
        0);

    public readonly string AppId;
    public readonly string AppName;
    public readonly string Title;
    public readonly string Artist;
    public readonly string Album;
    public readonly MediaSessionPlayback Playback;
    public readonly MediaSessionControls Controls;
    public readonly TimeSpan Position;
    public readonly TimeSpan Duration;
    public readonly long PositionUpdatedUtcTicks;
    public readonly byte[]? Artwork;
    public readonly int ArtworkRevision;

    public MediaSessionSnapshot(string appId, string appName, string title, string artist, string album,
        MediaSessionPlayback playback, MediaSessionControls controls, TimeSpan position, TimeSpan duration,
        long positionUpdatedUtcTicks, byte[]? artwork, int artworkRevision)
    {
        AppId = appId;
        AppName = appName;
        Title = title;
        Artist = artist;
        Album = album;
        Playback = playback;
        Controls = controls;
        Position = position;
        Duration = duration;
        PositionUpdatedUtcTicks = positionUpdatedUtcTicks;
        Artwork = artwork;
        ArtworkRevision = artworkRevision;
    }

    public bool HasSession => AppId.Length > 0;

    public bool IsPlaying => Playback == MediaSessionPlayback.Playing;

    public bool CanPlayPause =>
        (Controls & (MediaSessionControls.Play | MediaSessionControls.Pause | MediaSessionControls.PlayPauseToggle))
        != 0;

    public bool CanNext => (Controls & MediaSessionControls.Next) != 0;

    public bool CanPrevious => (Controls & MediaSessionControls.Previous) != 0;

    public bool CanSeek => (Controls & MediaSessionControls.Seek) != 0 && Duration > TimeSpan.Zero;

    public TimeSpan PositionAt(long utcTicks)
    {
        var ticks = Position.Ticks;
        if (IsPlaying)
        {
            ticks += Math.Max(0, utcTicks - PositionUpdatedUtcTicks);
        }

        if (Duration > TimeSpan.Zero)
        {
            ticks = Math.Min(ticks, Duration.Ticks);
        }

        return TimeSpan.FromTicks(Math.Max(0, ticks));
    }

    public bool SameContent(in MediaSessionSnapshot other) =>
        ReferenceEquals(AppId, other.AppId)
        && ReferenceEquals(Title, other.Title)
        && ReferenceEquals(Artist, other.Artist)
        && ReferenceEquals(Album, other.Album)
        && Playback == other.Playback
        && Controls == other.Controls
        && Position == other.Position
        && Duration == other.Duration
        && PositionUpdatedUtcTicks == other.PositionUpdatedUtcTicks
        && ArtworkRevision == other.ArtworkRevision;
}
