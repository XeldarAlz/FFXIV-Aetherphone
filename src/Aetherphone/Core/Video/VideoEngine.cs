using System.Text.RegularExpressions;
using Aetherphone.Core.Localization;
using SharpDX.Direct3D11;
using SharpDX.DXGI;

namespace Aetherphone.Core.Video;

internal enum PlayStart : byte
{
    Started,
    Superseded,
    Failed,
}

internal sealed class VideoEngine : IDisposable
{
    internal const int ScreenWidth = 1920;
    internal const int ScreenHeight = 1080;

    private const float DefaultScreenSpawnDistance = 2.0f;
    private const float DefaultScreenHeightOffset = 1.0f;

    internal const float MinScreenScale = 0.1f;
    internal const float MaxScreenScale = 8.0f;

    internal const float ScreenPositionSliderRange = 10f;

    private static readonly TimeSpan YouTubeLoadSpacing = TimeSpan.FromSeconds(3);

    private const long ResolverUpdateCheckCooldownMilliseconds = 10 * 60 * 1000;
    private const long RecoveryRestartBackoffMilliseconds = 15 * 1000;
    private const int MaxRecoveryRestartsPerUrl = 2;
    private const long StallWatchdogMilliseconds = 10 * 1000;
    private const long BufferingWatchdogMilliseconds = 30 * 1000;
    private const double StallPositionTolerance = 0.05;
    private const double StallEndGuardSeconds = 5.0;

    private static readonly Regex YouTubeHost =
        new(@"^\w+://[^/]*youtube\.\w+/|^\w+://youtu\.be/", RegexOptions.Compiled);

    private readonly ScreenPainter screenPainter;
    private readonly List<ScreenPositionPreset> screenPresets = [];
    private readonly SemaphoreSlim playGate = new(1, 1);
    private readonly Texture2D screenTexture;

    private static readonly Texture2DDescription ScreenTextureDescription = new()
    {
        Width = ScreenWidth,
        Height = ScreenHeight,
        MipLevels = 1,
        ArraySize = 1,
        Format = Format.B8G8R8A8_UNorm,
        BindFlags = BindFlags.ShaderResource | BindFlags.RenderTarget,
        CpuAccessFlags = CpuAccessFlags.None,
        SampleDescription = new SampleDescription(1, 0),
        Usage = ResourceUsage.Default,
        OptionFlags = ResourceOptionFlags.None,
    };

    private MpvRenderer? renderer;
    private ShaderResourceView? screenView;
    private readonly CancellationTokenSource lifetime = new();
    private DateTime lastYouTubeLoad = DateTime.MinValue;
    private int pendingVolume = 60;
    private volatile bool active;
    private int activeLoads;
    private int loadGeneration;
    private long lastResolverUpdateCheckAtTicks = long.MinValue;
    private long lastRecoveryRestartAtTicks = long.MinValue;
    private string? recoveryRestartUrl;
    private int recoveryRestartsForUrl;
    private string? recoveryExhaustedUrl;
    private volatile bool recovering;
    private volatile string? recoveryNotice;
    private ResolverUpdateOutcome lastResolverOutcome = ResolverUpdateOutcome.Failed;
    private long stallProgressAtTicks;
    private long framesProgressAtTicks;
    private long audioProgressAtTicks;
    private int lastObservedFrameVersion;
    private double lastObservedPosition;
    private double lastObservedAudioPosition;
    private bool audioPositionSeen;

    internal VideoEngine()
    {
        Dependencies = new MediaDependencies();
        DxHandler.Initialise(Plugin.PluginInterface);

        screenTexture = new Texture2D(DxHandler.Device, ScreenTextureDescription);
        screenPainter = new ScreenPainter();

        screenPresets.AddRange(Plugin.Cfg.ScreenPresets);
    }

    internal MediaDependencies Dependencies { get; }

    internal Vector3 ScreenPosition { get; private set; }
    internal float ScreenYaw { get; private set; }
    internal float ScreenScale { get; private set; } = 1.0f;
    internal Vector3 ScreenSpawnAnchor { get; private set; }

    internal bool HardwareDecoding { get; set; }
    internal int MaxQualityHeight { get; set; } = 720;
    internal bool AllowInsecureDirectUrls { get; set; }

    internal bool IsActive => active;
    internal bool IsLoading => Volatile.Read(ref activeLoads) > 0;
    internal string? LastError { get; private set; }
    internal string? RecoveryNotice => recoveryNotice;

    internal bool RecoveryExhausted =>
        recoveryExhaustedUrl is { } exhausted && string.Equals(renderer?.CurrentUrl, exhausted, StringComparison.Ordinal);

    internal void ResetRecoveryBudget()
    {
        recoveryRestartUrl = null;
        recoveryRestartsForUrl = 0;
        recoveryExhaustedUrl = null;
        lastRecoveryRestartAtTicks = long.MinValue;
    }

    internal event Action<MpvEndReason, string?>? PlaybackEnded;
    internal event Action? PlaybackLoaded;

    internal void ClearError()
    {
        LastError = null;
        recoveryNotice = null;
    }

    internal Task<PlayStart> Play(string url, double startSeconds = 0d, bool playing = true)
    {
        if (url.Length == 0)
        {
            return Task.FromResult(PlayStart.Failed);
        }

        PrepareScreenForSession();
        return PlayAsync(url, startSeconds, playing);
    }

    internal async Task<PlayStart> PlayAsync(string url, double startSeconds, bool playing)
    {
        if (url.Length == 0)
        {
            return PlayStart.Failed;
        }

        var generation = Interlocked.Increment(ref loadGeneration);
        var token = lifetime.Token;
        Interlocked.Increment(ref activeLoads);
        LastError = null;

        try
        {
            if (!await Dependencies.EnsureReadyAsync(token).ConfigureAwait(false))
            {
                LastError = DependencyFailureText();
                return PlayStart.Failed;
            }

            await SpaceOutYouTubeLoads(url, token).ConfigureAwait(false);
            await playGate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if (token.IsCancellationRequested || generation != Volatile.Read(ref loadGeneration))
                {
                    return PlayStart.Superseded;
                }

                var player = EnsureRenderer();
                if (player is null)
                {
                    return PlayStart.Failed;
                }

                if (!player.Play(url, startSeconds, playing))
                {
                    LastError = Loc.T(L.AetherStream.PlaybackFailed);
                    return PlayStart.Failed;
                }

                lastObservedPosition = startSeconds;
                stallProgressAtTicks = Environment.TickCount64;
                framesProgressAtTicks = stallProgressAtTicks;
                lastObservedFrameVersion = player.FrameVersion;
                active = true;
                screenPainter.SetTransform(ScreenPosition, ScreenYaw, ScreenScale);
                return PlayStart.Started;
            }
            finally
            {
                playGate.Release();
            }
        }
        catch (OperationCanceledException)
        {
            return PlayStart.Superseded;
        }
        catch (Exception exception)
        {
            AepLog.Error($"[Video] Could not start playback: {exception.Message}");
            LastError = exception.Message;
            return PlayStart.Failed;
        }
        finally
        {
            Interlocked.Decrement(ref activeLoads);
        }
    }

    private string DependencyFailureText()
    {
        var library = Dependencies.VideoLibrary.Snapshot();
        if (library.FailureReason is { Length: > 0 } libraryReason)
        {
            return libraryReason;
        }

        var resolver = Dependencies.LinkResolver.Snapshot();
        if (resolver.FailureReason is { Length: > 0 } resolverReason)
        {
            return resolverReason;
        }

        return Loc.T(L.AetherStream.ComponentsMissing);
    }

    private async Task SpaceOutYouTubeLoads(string url, CancellationToken token)
    {
        if (!YouTubeHost.IsMatch(url))
        {
            return;
        }

        var elapsed = DateTime.UtcNow - lastYouTubeLoad;
        if (elapsed < YouTubeLoadSpacing)
        {
            await Task.Delay(YouTubeLoadSpacing - elapsed, token).ConfigureAwait(false);
        }

        lastYouTubeLoad = DateTime.UtcNow;
    }

    private MpvRenderer? EnsureRenderer()
    {
        if (renderer is { IsRunning: true })
        {
            return renderer;
        }

        DetachRenderer();

        var created = new MpvRenderer();
        try
        {
            created.Initialize(ScreenWidth, ScreenHeight, screenTexture, Dependencies.LinkResolverPath,
                HardwareDecoding, MaxQualityHeight, AllowInsecureDirectUrls, pendingVolume);
        }
        catch (Exception exception)
        {
            AepLog.Error($"[Video] The video engine could not start: {exception.Message}");
            LastError = exception.Message;
            created.Dispose();
            return null;
        }

        created.FileEnded += OnFileEnded;
        created.FileLoaded += OnFileLoaded;
        renderer = created;
        return created;
    }

    private void DetachRenderer()
    {
        if (renderer is not { } existing)
        {
            return;
        }

        renderer = null;
        existing.FileEnded -= OnFileEnded;
        existing.FileLoaded -= OnFileLoaded;
        existing.Dispose();
    }

    private void OnFileLoaded()
    {
        recoveryNotice = null;
        PlaybackLoaded?.Invoke();
    }

    private void OnFileEnded(MpvEndReason reason, string? detail)
    {
        if (reason == MpvEndReason.Failed)
        {
            if (RefusedStreamUrl() is { } refusedUrl)
            {
                if (TryBeginRecovery(refusedUrl))
                {
                    recoveryNotice = Loc.T(L.AetherStream.ResolverRecovering);
                    _ = RecoverResolverAsync(refusedUrl, lastObservedPosition);
                    return;
                }

                detail = RefusedStreamText();
            }
            else
            {
                detail = FailureText(detail);
            }

            LastError = detail;
        }

        PlaybackEnded?.Invoke(reason, detail);
    }

    private static string FailureText(string? detail) => detail is { Length: > 0 }
        ? string.Format(Loc.T(L.AetherStream.PlaybackFailedReason), detail)
        : Loc.T(L.AetherStream.PlaybackFailed);

    internal void ObserveForStall(in MpvPlaybackInfo info)
    {
        var now = Environment.TickCount64;
        var frameVersion = renderer?.FrameVersion ?? 0;
        if (!active || IsLoading || recovering || info.Paused || info.Seeking || renderer is not { } player)
        {
            ResetStallTrackers(info, frameVersion, now);
            return;
        }

        if (Math.Abs(info.PositionSeconds - lastObservedPosition) > StallPositionTolerance)
        {
            lastObservedPosition = info.PositionSeconds;
            stallProgressAtTicks = now;
        }

        if (info.CoreIdle || frameVersion != lastObservedFrameVersion)
        {
            lastObservedFrameVersion = frameVersion;
            framesProgressAtTicks = now;
        }

        if (info.CoreIdle)
        {
            lastObservedAudioPosition = info.AudioPositionSeconds;
            audioProgressAtTicks = now;
        }
        else if (info.HasAudioPosition)
        {
            if (!audioPositionSeen
                 || Math.Abs(info.AudioPositionSeconds - lastObservedAudioPosition) > StallPositionTolerance)
            {
                audioPositionSeen = true;
                lastObservedAudioPosition = info.AudioPositionSeconds;
                audioProgressAtTicks = now;
            }
        }

        var nearEnd = info.DurationSeconds > 0d
            && info.PositionSeconds >= info.DurationSeconds - StallEndGuardSeconds;
        var positionWindow = info.CoreIdle ? BufferingWatchdogMilliseconds : StallWatchdogMilliseconds;
        var positionStalled = (player.SawHttpForbidden || player.SawStreamError)
            && now - stallProgressAtTicks >= positionWindow;
        var framesStalled = info.HasMovingVideo && now - framesProgressAtTicks >= StallWatchdogMilliseconds;
        var audioStalled = audioPositionSeen && info.HasAudioPosition && !nearEnd
            && now - audioProgressAtTicks >= StallWatchdogMilliseconds;
        if (!positionStalled && !framesStalled && !audioStalled)
        {
            return;
        }

        stallProgressAtTicks = now;
        framesProgressAtTicks = now;
        audioProgressAtTicks = now;
        var stall = positionStalled ? "position" : framesStalled ? "video frames" : "audio";

        if (RefusedStreamUrl() is { } refusedUrl)
        {
            if (TryBeginRecovery(refusedUrl))
            {
                AepLog.Warning(
                    $"[Video] Playback {stall} stalled after a refused stream; restarting the player at {lastObservedPosition:F1}s.");
                recoveryNotice = Loc.T(L.AetherStream.ResolverRecovering);
                _ = RecoverResolverAsync(refusedUrl, lastObservedPosition);
                return;
            }

            if (RecoveryBudgetSpent(refusedUrl))
            {
                StopVideo();
                AbandonRecovery(RefusedStreamText());
            }

            return;
        }

        if (player.CurrentUrl is not { Length: > 0 } stalledUrl)
        {
            return;
        }

        if (!TryBeginRecovery(stalledUrl))
        {
            if (recoveryExhaustedUrl != stalledUrl && RecoveryBudgetSpent(stalledUrl))
            {
                recoveryExhaustedUrl = stalledUrl;
                AepLog.Warning(
                    $"[Video] Playback {stall} stalled again but the restart budget for this link is spent; leaving it to play.");
            }

            return;
        }

        AepLog.Warning($"[Video] Playback {stall} stalled; restarting the player at {lastObservedPosition:F1}s.");
        recoveryNotice = Loc.T(L.AetherStream.StreamStalledRecovering);
        _ = RestartStalledAsync(stalledUrl, lastObservedPosition);
    }

    private void ResetStallTrackers(in MpvPlaybackInfo info, int frameVersion, long now)
    {
        lastObservedPosition = info.PositionSeconds;
        lastObservedFrameVersion = frameVersion;
        lastObservedAudioPosition = info.AudioPositionSeconds;
        audioPositionSeen = false;
        stallProgressAtTicks = now;
        framesProgressAtTicks = now;
        audioProgressAtTicks = now;
    }

    private async Task RestartStalledAsync(string stalledUrl, double resumeSeconds)
    {
        var token = lifetime.Token;
        try
        {
            await playGate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if (renderer is not { } current || !string.Equals(current.CurrentUrl, stalledUrl, StringComparison.Ordinal))
                {
                    recoveryNotice = null;
                    return;
                }

                DetachRenderer();
            }
            finally
            {
                playGate.Release();
            }

            var restart = await PlayAsync(stalledUrl, resumeSeconds, playing: true).ConfigureAwait(false);
            if (restart == PlayStart.Failed)
            {
                AbandonRecovery(LastError ?? Loc.T(L.AetherStream.PlaybackFailed));
            }
            else if (restart == PlayStart.Superseded)
            {
                recoveryNotice = null;
            }
        }
        catch (OperationCanceledException)
        {
            recoveryNotice = null;
        }
        catch (Exception exception)
        {
            AepLog.Error($"[Video] Restarting a stalled stream failed: {exception.Message}");
            AbandonRecovery(exception.Message);
        }
        finally
        {
            recovering = false;
        }
    }

    private string? RefusedStreamUrl()
    {
        if (renderer is not { SawHttpForbidden: true } refusedRenderer)
        {
            return null;
        }

        var url = refusedRenderer.CurrentUrl;
        return url is { Length: > 0 } && url.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? url : null;
    }

    private bool RecoveryBudgetSpent(string url) =>
        !recovering && recoveryRestartUrl == url && recoveryRestartsForUrl >= MaxRecoveryRestartsPerUrl;

    private bool TryBeginRecovery(string url)
    {
        if (recovering)
        {
            return false;
        }

        var now = Environment.TickCount64;
        if (lastRecoveryRestartAtTicks != long.MinValue
            && now - lastRecoveryRestartAtTicks < RecoveryRestartBackoffMilliseconds)
        {
            return false;
        }

        if (recoveryRestartUrl != url)
        {
            recoveryRestartUrl = url;
            recoveryRestartsForUrl = 0;
            recoveryExhaustedUrl = null;
        }

        if (recoveryRestartsForUrl >= MaxRecoveryRestartsPerUrl)
        {
            return false;
        }

        recoveryRestartsForUrl++;
        lastRecoveryRestartAtTicks = now;
        recovering = true;
        return true;
    }

    private async Task RecoverResolverAsync(string refusedUrl, double resumeSeconds)
    {
        var token = lifetime.Token;
        try
        {
            var now = Environment.TickCount64;
            if (lastResolverUpdateCheckAtTicks == long.MinValue
                || now - lastResolverUpdateCheckAtTicks >= ResolverUpdateCheckCooldownMilliseconds)
            {
                lastResolverUpdateCheckAtTicks = now;
                var outcome = await Dependencies.UpdateIfNewerAsync(Dependencies.LinkResolver, token)
                    .ConfigureAwait(false);
                lastResolverOutcome = outcome;
                if (outcome == ResolverUpdateOutcome.Updated)
                {
                    AepLog.Warning("[Video] The stream was refused with the old link resolver; updated to "
                        + $"{Dependencies.LinkResolver.RemoteVersion} and retrying.");
                }
                else
                {
                    AepLog.Warning(
                        "[Video] The stream was refused with a current link resolver; restarting the player to reset its connections.");
                }
            }
            else
            {
                AepLog.Warning(
                    $"[Video] The stream was refused again; restarting the player at {resumeSeconds:F1}s without another resolver check.");
            }

            if (renderer is { } current && !string.Equals(current.CurrentUrl, refusedUrl, StringComparison.Ordinal))
            {
                recoveryNotice = null;
                return;
            }

            await playGate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                DetachRenderer();
            }
            finally
            {
                playGate.Release();
            }

            var restart = await PlayAsync(refusedUrl, resumeSeconds, playing: true).ConfigureAwait(false);
            if (restart == PlayStart.Failed)
            {
                AbandonRecovery(RefusedStreamText());
            }
            else if (restart == PlayStart.Superseded)
            {
                recoveryNotice = null;
            }
        }
        catch (OperationCanceledException)
        {
            recoveryNotice = null;
        }
        catch (Exception exception)
        {
            AepLog.Error($"[Video] Link resolver recovery failed: {exception.Message}");
            lastResolverOutcome = ResolverUpdateOutcome.Failed;
            AbandonRecovery(RefusedStreamText());
        }
        finally
        {
            recovering = false;
        }
    }

    private void AbandonRecovery(string message)
    {
        recoveryNotice = null;
        LastError = message;
        PlaybackEnded?.Invoke(MpvEndReason.Failed, message);
    }

    private string RefusedStreamText() => lastResolverOutcome switch
    {
        ResolverUpdateOutcome.Updated => Loc.T(L.AetherStream.ResolverStillRefused),
        ResolverUpdateOutcome.AlreadyCurrent => Loc.T(L.AetherStream.ResolverAlreadyCurrent),
        _ => Loc.T(L.AetherStream.ResolverUpdateFailed),
    };

    internal void StopVideo()
    {
        Interlocked.Increment(ref loadGeneration);
        active = false;
        recoveryNotice = null;
        renderer?.Stop();
        screenPainter.SetTarget(null);
    }

    internal void Shutdown()
    {
        Interlocked.Increment(ref loadGeneration);
        active = false;
        DetachRenderer();
        screenPainter.SetTarget(null);
    }

    internal void Pause(bool paused) => renderer?.Pause(paused);

    internal void Seek(double seconds) => renderer?.Seek(seconds);

    internal void SetSpeed(double speed) => renderer?.SetSpeed(speed);

    internal void SetVolume(int volume)
    {
        pendingVolume = Math.Clamp(volume, 0, 100);
        renderer?.SetVolume(pendingVolume);
    }

    internal MpvPlaybackInfo ReadPlaybackInfo() => renderer?.ReadPlaybackInfo() ?? default;

    internal string? GetMediaTitle() => renderer?.ReadMediaTitle();

    internal string? GetCurrentUrl() => renderer?.CurrentUrl;

    internal int FrameVersion => renderer?.FrameVersion ?? 0;

    internal nint ScreenViewHandle
    {
        get
        {
            if (screenView is null && DxHandler.Device is { } device)
            {
                screenView = new ShaderResourceView(device, screenTexture);
            }

            return screenView?.NativePointer ?? nint.Zero;
        }
    }

    internal bool TryCopyLatestFrame(byte[] destination) => renderer?.TryCopyLatestFrame(destination) ?? false;

    internal byte[]? TryGetFrame(out int width, out int height)
    {
        if (renderer is null)
        {
            width = ScreenWidth;
            height = ScreenHeight;
            return null;
        }

        return renderer.CopyLatestFrame(out width, out height);
    }

    internal static bool ValidateURL(string inputUrl, out Uri? url)
    {
        var formattedUrl = inputUrl;

        if (!formattedUrl.StartsWith("http://", StringComparison.Ordinal)
            && !formattedUrl.StartsWith("https://", StringComparison.Ordinal))
        {
            formattedUrl = "https://" + formattedUrl;
        }

        return Uri.TryCreate(formattedUrl, UriKind.Absolute, out url)
            && (url?.Scheme == Uri.UriSchemeHttp || url?.Scheme == Uri.UriSchemeHttps)
            && url.Host.Contains('.') && !url.Host.EndsWith('.')
            && Uri.CheckHostName(url.Host) == UriHostNameType.Dns;
    }

    private void SpawnScreenInFrontOfLocalPlayer()
    {
        var localPlayer = Plugin.ObjectTable.LocalPlayer;
        if (localPlayer is null)
        {
            return;
        }

        var yaw = localPlayer.Rotation;
        var forward = Vector3.Transform(Vector3.UnitZ, Quaternion.CreateFromAxisAngle(Vector3.UnitY, yaw));

        var position = localPlayer.Position + forward * DefaultScreenSpawnDistance
            + new Vector3(0, DefaultScreenHeightOffset, 0);
        ScreenSpawnAnchor = position;
        SetScreenTransform(position, yaw + MathF.PI, 1.0f);
    }

    internal void RecenterScreen() => SpawnScreenInFrontOfLocalPlayer();

    internal void SetScreenTransform(Vector3 position, float yaw, float scale)
    {
        ScreenPosition = position;
        ScreenYaw = yaw;
        ScreenScale = Math.Clamp(scale, MinScreenScale, MaxScreenScale);

        if (active)
        {
            screenPainter.SetTransform(ScreenPosition, ScreenYaw, ScreenScale);
        }
    }

    internal List<ScreenPositionPreset> GetScreenPresets() => [.. screenPresets];

    internal void SaveScreenPreset(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        screenPresets.RemoveAll(preset => preset.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        screenPresets.Add(new ScreenPositionPreset
        {
            Name = name, X = ScreenPosition.X, Y = ScreenPosition.Y, Z = ScreenPosition.Z, Yaw = ScreenYaw,
            Scale = ScreenScale,
        });

        Plugin.Cfg.ScreenPresets = screenPresets;
        Plugin.Cfg.Save();
    }

    internal void RemoveScreenPreset(string name)
    {
        screenPresets.RemoveAll(preset => preset.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        Plugin.Cfg.ScreenPresets = screenPresets;
        Plugin.Cfg.Save();
    }

    internal void ApplyScreenPreset(ScreenPositionPreset preset)
    {
        var position = new Vector3(preset.X, preset.Y, preset.Z);
        ScreenSpawnAnchor = position;
        SetScreenTransform(position, preset.Yaw, preset.Scale);
    }

    internal void ApplyRemoteScreenTransform(Vector3 position, float yaw, float scale)
    {
        ScreenSpawnAnchor = position;
        SetScreenTransform(position, yaw, scale);
    }

    private void PrepareScreenForSession()
    {
        var isNewSession = !active;
        screenPainter.SetTarget(screenTexture);

        if (isNewSession)
        {
            SpawnScreenInFrontOfLocalPlayer();
        }
    }

    internal bool ScreenVisible
    {
        get => screenPainter.Visible;
        set => screenPainter.Visible = value;
    }

    public void Dispose()
    {
        lifetime.Cancel();
        DetachRenderer();
        screenPainter.Dispose();
        screenView?.Dispose();
        screenTexture.Dispose();
        Dependencies.Dispose();
        playGate.Dispose();
        lifetime.Dispose();
    }
}
