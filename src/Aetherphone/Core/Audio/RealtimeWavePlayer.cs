using NAudio.Wave;

namespace Aetherphone.Core.Audio;

internal sealed class RealtimeWavePlayer : IWavePlayer
{
    private readonly IWavePlayer inner;
    private readonly int latencyMilliseconds;

    public RealtimeWavePlayer(IWavePlayer inner, int latencyMilliseconds)
    {
        this.inner = inner;
        this.latencyMilliseconds = latencyMilliseconds;
        inner.PlaybackStopped += OnInnerStopped;
    }

    public event EventHandler<StoppedEventArgs>? PlaybackStopped;

    public float Volume
    {
        get => inner.Volume;
        set => inner.Volume = value;
    }

    public PlaybackState PlaybackState => inner.PlaybackState;

    public WaveFormat OutputWaveFormat => inner.OutputWaveFormat;

    public void Init(IWaveProvider waveProvider) =>
        inner.Init(new RealtimeWaveProvider(waveProvider, latencyMilliseconds));

    public void Play() => inner.Play();

    public void Stop() => inner.Stop();

    public void Pause() => inner.Pause();

    public void Dispose() => inner.Dispose();

    private void OnInnerStopped(object? sender, StoppedEventArgs arguments) => PlaybackStopped?.Invoke(this, arguments);
}
