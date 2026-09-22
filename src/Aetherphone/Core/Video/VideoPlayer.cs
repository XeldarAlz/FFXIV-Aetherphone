using System.Collections.Concurrent;
using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Video;

internal enum VideoPlaybackState : byte
{
    Idle,
    Loading,
    Playing,
    Paused,
    Failed,
}

internal readonly record struct PlaybackProgress(float Position, float Duration, bool Paused, bool Seeking)
{
    internal float Fraction => Duration > 0f ? Math.Clamp(Position / Duration, 0f, 1f) : 0f;
}

internal sealed class VideoPlayer : IDisposable
{
    private readonly VideoEngine engine;
    private readonly ConcurrentQueue<PlaybackEnding> pendingEndings = new();
    private volatile bool pendingLoaded;
    private int startGeneration;

    internal VideoPlayer(VideoEngine engine)
    {
        this.engine = engine;
        engine.PlaybackEnded += OnEngineEnded;
        engine.PlaybackLoaded += OnEngineLoaded;
    }

    internal event Action? Finished;
    internal event Action<string?>? Failed;

    internal VideoPlaybackState State { get; private set; } = VideoPlaybackState.Idle;
    internal PlaybackProgress Progress { get; private set; }
    internal string? LastError { get; private set; }
    internal PlaybackFailureKind FailureKind { get; private set; }
    internal string? RecoveryNotice => engine.RecoveryNotice;
    internal bool RecoveryExhausted => HasMedia && engine.RecoveryExhausted;

    internal void ResetRecoveryBudget() => engine.ResetRecoveryBudget();

    internal bool HasMedia => State is VideoPlaybackState.Loading or VideoPlaybackState.Playing
        or VideoPlaybackState.Paused;

    internal bool HardwareDecoding
    {
        get => engine.HardwareDecoding;
        set => engine.HardwareDecoding = value;
    }

    internal bool AllowInsecureDirectUrls
    {
        get => engine.AllowInsecureDirectUrls;
        set => engine.AllowInsecureDirectUrls = value;
    }

    internal int MaxQualityHeight
    {
        get => engine.MaxQualityHeight;
        set => engine.MaxQualityHeight = value;
    }

    internal void SetVolume(int volumePercent) => engine.SetVolume(volumePercent);

    internal void Play(string url, double startSeconds = 0d, bool playing = true)
    {
        LastError = null;
        FailureKind = PlaybackFailureKind.Unknown;
        State = VideoPlaybackState.Loading;
        engine.ClearError();
        var generation = Interlocked.Increment(ref startGeneration);
        _ = ObserveStartAsync(engine.Play(url, startSeconds, playing), generation);
    }

    private async Task ObserveStartAsync(Task<PlayStart> start, int generation)
    {
        var started = await start.ConfigureAwait(false);
        if (started == PlayStart.Failed && generation == Volatile.Read(ref startGeneration))
        {
            pendingEndings.Enqueue(new PlaybackEnding(MpvEndReason.Failed, engine.LastError,
                PlaybackFailureKind.Unknown));
        }
    }

    internal void Pause(bool paused)
    {
        engine.Pause(paused);
        if (State is VideoPlaybackState.Playing or VideoPlaybackState.Paused)
        {
            State = paused ? VideoPlaybackState.Paused : VideoPlaybackState.Playing;
        }
    }

    internal void Seek(double seconds) => engine.Seek(seconds);

    internal void SetSpeed(double speed) => engine.SetSpeed(speed);

    internal void Stop()
    {
        engine.StopVideo();
        State = VideoPlaybackState.Idle;
        FailureKind = PlaybackFailureKind.Unknown;
        Progress = default;
        while (pendingEndings.TryDequeue(out _))
        {
        }
    }

    internal byte[]? TryGetFrame(out int width, out int height) => engine.TryGetFrame(out width, out height);

    internal bool TryCopyLatestFrame(byte[] destination) => engine.TryCopyLatestFrame(destination);

    internal int FrameVersion => engine.FrameVersion;

    internal void OnFrameworkUpdate()
    {
        while (pendingEndings.TryDequeue(out var ending))
        {
            ApplyEnding(ending);
        }

        if (pendingLoaded)
        {
            pendingLoaded = false;
            if (State == VideoPlaybackState.Loading)
            {
                State = VideoPlaybackState.Playing;
            }
        }

        if (!HasMedia)
        {
            Progress = default;
            return;
        }

        var info = engine.ReadPlaybackInfo();
        Progress = new PlaybackProgress((float)info.PositionSeconds, (float)info.DurationSeconds, info.Paused,
            info.Seeking);
        engine.ObserveForStall(info);

        if (State == VideoPlaybackState.Playing && info.Paused)
        {
            State = VideoPlaybackState.Paused;
            return;
        }

        if (State == VideoPlaybackState.Paused && !info.Paused)
        {
            State = VideoPlaybackState.Playing;
        }
    }

    private void ApplyEnding(PlaybackEnding ending)
    {
        switch (ending.Reason)
        {
            case MpvEndReason.Finished:
                if (State == VideoPlaybackState.Loading)
                {
                    return;
                }

                State = VideoPlaybackState.Idle;
                Progress = default;
                Finished?.Invoke();
                return;
            case MpvEndReason.Failed:
                State = VideoPlaybackState.Failed;
                FailureKind = ending.FailureKind;
                LastError = ending.Detail ?? engine.LastError ?? Loc.T(L.AetherStream.PlaybackFailed);
                Progress = default;
                Failed?.Invoke(LastError);
                return;
            case MpvEndReason.Stopped:
            case MpvEndReason.Redirect:
                return;
            default:
                State = VideoPlaybackState.Idle;
                Progress = default;
                return;
        }
    }

    private void OnEngineLoaded() => pendingLoaded = true;

    private void OnEngineEnded(PlaybackEnding ending) => pendingEndings.Enqueue(ending);

    public void Dispose()
    {
        engine.PlaybackEnded -= OnEngineEnded;
        engine.PlaybackLoaded -= OnEngineLoaded;
        engine.Shutdown();
        State = VideoPlaybackState.Idle;
    }
}
