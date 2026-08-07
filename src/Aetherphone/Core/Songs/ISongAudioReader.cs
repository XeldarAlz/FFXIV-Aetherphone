using NAudio.Wave;

namespace Aetherphone.Core.Songs;

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
