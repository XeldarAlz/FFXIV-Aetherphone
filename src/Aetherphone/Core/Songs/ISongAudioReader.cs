using NAudio.Wave;

namespace Aetherphone.Core.Songs;

/// <summary>
/// Common surface over the two song decode backends (Media Foundation for mp4/AAC,
/// OpusWebmSampleProvider for webm/Opus) so SongPlayer.PlayOnce doesn't need to branch
/// on which one it's holding.
/// </summary>
internal interface ISongAudioReader : IDisposable
{
    ISampleProvider ToSampleProvider();
    TimeSpan TotalTime { get; }
    TimeSpan CurrentTime { get; set; }
}

internal sealed class MediaFoundationSongReader : ISongAudioReader
{
    private readonly MediaFoundationReader inner;

    public MediaFoundationSongReader(MediaFoundationReader inner) => this.inner = inner;

    public ISampleProvider ToSampleProvider() => inner.ToSampleProvider();
    public TimeSpan TotalTime => inner.TotalTime;

    public TimeSpan CurrentTime
    {
        get => inner.CurrentTime;
        set => inner.CurrentTime = value;
    }

    public void Dispose() => inner.Dispose();
}
