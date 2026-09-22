using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using SharpDX.Direct3D11;

namespace Aetherphone.Core.Video;

internal enum MpvEndReason : byte
{
    Finished,
    Stopped,
    Quit,
    Failed,
    Redirect,
}

internal enum PlaybackFailureKind : byte
{
    Unknown,
    BotCheck,
}

internal readonly record struct PlaybackEnding(MpvEndReason Reason, string? Detail, PlaybackFailureKind FailureKind);

internal sealed class MpvRenderer : IDisposable
{
    private const string Library = "libmpv-2";
    private const string RenderKey = "mpv";

    private const int FormatFlag = 3;
    private const int FormatInt64 = 4;
    private const int FormatDouble = 5;

    private const int EventShutdown = 1;
    private const int EventLogMessage = 2;
    private const int EventStartFile = 6;
    private const int EventEndFile = 7;
    private const int EventFileLoaded = 8;
    private const int EventPlaybackRestart = 21;

    private const int ParamInvalid = 0;
    private const int ParamApiType = 1;
    private const int ParamSoftwareSize = 17;
    private const int ParamSoftwareFormat = 18;
    private const int ParamSoftwareStride = 19;
    private const int ParamSoftwarePointer = 20;

    private const int ParamStride = 16;
    private const int MaxConsecutiveRenderFailures = 30;
    private const int RenderWaitMilliseconds = 250;
    private const double EventWaitSeconds = 0.1;
    private const int MaxErrorDetailLength = 200;
    private const double MinSpeed = 0.5;
    private const double MaxSpeed = 2.0;
    private const string ResolverLogPrefix = "ytdl_hook";
    private const string ResolverErrorPrefix = "ERROR: ";
    private const string ResolverFailedPrefix = "youtube-dl failed: ";
    private const string BotCheckMarker = "not a bot";

    private static readonly Regex ResolverSitePrefix = new(@"^\[[^\]]+\]\s+\S+:\s+", RegexOptions.Compiled);

    private static readonly string[] ResolverAdviceMarkers =
    [
        " Use --",
        " See https://",
        "; please report",
        ". Please report",
        " (caused by",
    ];

    private const string StreamReconnectOptions =
        "reconnect=1,reconnect_streamed=1,reconnect_on_network_error=1,reconnect_on_http_error=5xx,reconnect_delay_max=30";

    private static readonly char[] LineBreaks = ['\r', '\n'];

    private static readonly string[] StreamErrorMarkers =
    [
        "failed to load segment",
        "Connection reset",
        "timed out",
        "Server returned 5",
        "I/O error",
        "Input/output error",
        "End of file",
        "Failed to open",
    ];

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr mpv_create();

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int mpv_initialize(IntPtr ctx);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int mpv_set_option_string(IntPtr ctx,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name, [MarshalAs(UnmanagedType.LPUTF8Str)] string data);

    [DllImport(Library, EntryPoint = "mpv_command", CallingConvention = CallingConvention.Cdecl)]
    private static extern int mpv_command_native(IntPtr ctx, IntPtr[] args);

    private static int MpvCommand(IntPtr ctx, string?[] args)
    {
        var pointers = new IntPtr[args.Length];
        try
        {
            for (var index = 0; index < args.Length; index++)
            {
                if (args[index] is { } value)
                {
                    pointers[index] = Marshal.StringToCoTaskMemUTF8(value);
                }
            }

            return mpv_command_native(ctx, pointers);
        }
        finally
        {
            for (var index = 0; index < pointers.Length; index++)
            {
                if (pointers[index] != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(pointers[index]);
                }
            }
        }
    }

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int mpv_render_context_create(ref IntPtr result, IntPtr ctx, IntPtr parameters);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int mpv_render_context_render(IntPtr ctx, IntPtr parameters);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void mpv_render_context_free(IntPtr ctx);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void mpv_render_context_set_update_callback(IntPtr ctx, MpvRenderUpdateFn callback,
        IntPtr callbackContext);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern ulong mpv_render_context_update(IntPtr ctx);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr mpv_wait_event(IntPtr ctx, double timeout);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int mpv_request_log_messages(IntPtr ctx,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string minimumLevel);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void mpv_terminate_destroy(IntPtr ctx);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int mpv_get_property(IntPtr ctx,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name, int format, out double data);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int mpv_get_property(IntPtr ctx,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name, int format, out int data);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int mpv_get_property(IntPtr ctx,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name, int format, out long data);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr mpv_get_property_string(IntPtr ctx,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void mpv_free(IntPtr data);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr mpv_error_string(int error);

    [StructLayout(LayoutKind.Sequential)]
    private struct MpvRenderParam
    {
        public int Type;
        public IntPtr Data;
    }

    internal delegate void MpvRenderUpdateFn(IntPtr callbackContext);

    private readonly Lock commandLock = new();
    private readonly Lock snapshotLock = new();
    private readonly ManualResetEventSlim frameReady = new(false);

    private IntPtr mpvContext;
    private IntPtr renderContext;
    private IntPtr frameBuffer;
    private IntPtr snapshotA;
    private IntPtr snapshotB;
    private IntPtr latestSnapshot;
    private bool useSnapshotA = true;

    private IntPtr renderParameters;
    private IntPtr sizeParameter;
    private IntPtr strideParameter;
    private IntPtr formatParameter;

    private int frameBytes;
    private int width;
    private int height;

    private Texture2D? targetTexture;
    private MpvRenderUpdateFn? updateCallback;
    private GCHandle updateCallbackHandle;

    private Thread? eventThread;
    private Thread? renderThread;

    private volatile bool disposed;
    private volatile bool started;
    private volatile string? currentUrl;
    private volatile string? lastErrorDetail;
    private volatile bool lastErrorFromResolver;
    private int consecutiveRenderFailures;
    private int httpForbiddenHits;
    private int streamErrorHits;
    private int botCheckHits;

    internal event Action? FileLoaded;
    internal event Action<PlaybackEnding>? FileEnded;

    internal bool IsRunning => started && !disposed;

    internal bool SawHttpForbidden => Volatile.Read(ref httpForbiddenHits) > 0;

    internal bool SawStreamError => Volatile.Read(ref streamErrorHits) > 0;

    internal bool SawBotCheck => Volatile.Read(ref botCheckHits) > 0;

    internal string? LastErrorDetail => lastErrorDetail;

    internal string? LinkResolverPath { get; private set; }

    internal void Initialize(int frameWidth, int frameHeight, Texture2D? texture, string? linkResolverPath,
        bool hardwareDecoding, int maxQualityHeight, bool allowInsecureDirectUrls, int initialVolume)
    {
        width = frameWidth;
        height = frameHeight;
        targetTexture = texture;
        frameBytes = frameWidth * frameHeight * 4;

        frameBuffer = Marshal.AllocHGlobal(frameBytes);
        snapshotA = Marshal.AllocHGlobal(frameBytes);
        snapshotB = Marshal.AllocHGlobal(frameBytes);

        mpvContext = mpv_create();
        if (mpvContext == IntPtr.Zero)
        {
            throw new InvalidOperationException("mpv could not be created.");
        }

        SetOption("vo", "libmpv");
        SetOption("hwdec", hardwareDecoding ? "auto-safe" : "no");
        SetOption("profile", "sw-fast");
        SetOption("ytdl", "yes");
        LinkResolverPath = linkResolverPath;
        if (linkResolverPath is { Length: > 0 })
        {
            SetOption("script-opts", $"ytdl_hook-ytdl_path={linkResolverPath}");
        }

        SetOption("ytdl-format",
            $"bestvideo[height<={maxQualityHeight}][vcodec^=avc1]+bestaudio/"
            + $"bestvideo[height<={maxQualityHeight}][ext=mp4]+bestaudio/best[height<={maxQualityHeight}]");
        SetOption("ytdl-raw-options", "force-ipv4=,hls-use-mpegts=");
        SetOption("stream-lavf-o", StreamReconnectOptions);
        SetOption("volume", initialVolume.ToString(CultureInfo.InvariantCulture));
        SetOption("msg-level", "all=warn,ffmpeg=error");
        SetOption("terminal", "no");
        SetOption("idle", "yes");
        SetOption("keep-open", "no");
        SetOption("reset-on-next-file", "pause");
        if (WineEnvironment.IsWine && allowInsecureDirectUrls)
        {
            SetOption("tls-verify", "no");
        }

        _ = mpv_request_log_messages(mpvContext, "warn");

        var initialized = mpv_initialize(mpvContext);
        if (initialized < 0)
        {
            throw new InvalidOperationException($"mpv could not start: {ErrorText(initialized)}");
        }

        CreateRenderContext();
        BuildRenderParameters();

        updateCallback = _ => frameReady.Set();
        updateCallbackHandle = GCHandle.Alloc(updateCallback);
        mpv_render_context_set_update_callback(renderContext, updateCallback, IntPtr.Zero);

        started = true;

        eventThread = new Thread(EventLoop) { IsBackground = true, Name = "aetherstream-mpv-events" };
        eventThread.Start();

        renderThread = new Thread(RenderLoop) { IsBackground = true, Name = "aetherstream-mpv-render" };
        renderThread.Start();
    }

    private void SetOption(string name, string value)
    {
        var result = mpv_set_option_string(mpvContext, name, value);
        if (result < 0)
        {
            AepLog.Warning($"[MPV] Option {name}={value} was refused: {ErrorText(result)}");
        }
    }

    private void CreateRenderContext()
    {
        var apiType = Marshal.StringToHGlobalAnsi("sw");
        var parameters = Marshal.AllocHGlobal(ParamStride * 2);
        try
        {
            Marshal.StructureToPtr(new MpvRenderParam { Type = ParamApiType, Data = apiType }, parameters, false);
            Marshal.StructureToPtr(new MpvRenderParam { Type = ParamInvalid, Data = IntPtr.Zero },
                parameters + ParamStride, false);

            var created = mpv_render_context_create(ref renderContext, mpvContext, parameters);
            if (created < 0)
            {
                throw new InvalidOperationException($"mpv could not start rendering: {ErrorText(created)}");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(apiType);
            Marshal.FreeHGlobal(parameters);
        }
    }

    private void BuildRenderParameters()
    {
        sizeParameter = Marshal.AllocHGlobal(8);
        Marshal.WriteInt32(sizeParameter, width);
        Marshal.WriteInt32(sizeParameter + 4, height);

        strideParameter = Marshal.AllocHGlobal(IntPtr.Size);
        Marshal.WriteIntPtr(strideParameter, new IntPtr(width * 4));

        formatParameter = Marshal.StringToHGlobalAnsi("bgra");

        renderParameters = Marshal.AllocHGlobal(ParamStride * 5);
        Marshal.StructureToPtr(new MpvRenderParam { Type = ParamSoftwareSize, Data = sizeParameter },
            renderParameters, false);
        Marshal.StructureToPtr(new MpvRenderParam { Type = ParamSoftwareFormat, Data = formatParameter },
            renderParameters + ParamStride, false);
        Marshal.StructureToPtr(new MpvRenderParam { Type = ParamSoftwareStride, Data = strideParameter },
            renderParameters + ParamStride * 2, false);
        Marshal.StructureToPtr(new MpvRenderParam { Type = ParamSoftwarePointer, Data = frameBuffer },
            renderParameters + ParamStride * 3, false);
        Marshal.StructureToPtr(new MpvRenderParam { Type = ParamInvalid, Data = IntPtr.Zero },
            renderParameters + ParamStride * 4, false);
    }

    internal bool Play(string url, double startSeconds, bool playing)
    {
        if (!IsRunning || url.Length == 0)
        {
            return false;
        }

        var start = ((int)Math.Max(0d, startSeconds)).ToString(CultureInfo.InvariantCulture);
        var options = playing ? $"start={start},pause=no" : $"start={start},pause=yes";

        lock (commandLock)
        {
            if (mpvContext == IntPtr.Zero)
            {
                return false;
            }

            lastErrorDetail = null;
            lastErrorFromResolver = false;
            Interlocked.Exchange(ref httpForbiddenHits, 0);
            Interlocked.Exchange(ref streamErrorHits, 0);
            Interlocked.Exchange(ref botCheckHits, 0);
            _ = MpvCommand(mpvContext, ["set", "speed", "1", null]);
            var result = MpvCommand(mpvContext, ["loadfile", url, "replace", "0", options, null]);
            if (result < 0)
            {
                AepLog.Warning($"[MPV] Could not load {url}: {ErrorText(result)}");
                return false;
            }
        }

        currentUrl = url;
        Interlocked.Exchange(ref consecutiveRenderFailures, 0);
        return true;
    }

    internal void Stop()
    {
        if (!IsRunning)
        {
            return;
        }

        currentUrl = null;
        lock (commandLock)
        {
            if (mpvContext != IntPtr.Zero)
            {
                _ = MpvCommand(mpvContext, ["stop", null]);
            }
        }

        lock (snapshotLock)
        {
            latestSnapshot = IntPtr.Zero;
        }
    }

    internal void Pause(bool paused) => SetProperty("pause", paused ? "yes" : "no");

    internal void SetVolume(int volume) =>
        SetProperty("volume", Math.Clamp(volume, 0, 100).ToString(CultureInfo.InvariantCulture));

    internal void SetSpeed(double speed) =>
        SetProperty("speed", Math.Clamp(speed, MinSpeed, MaxSpeed).ToString("F3", CultureInfo.InvariantCulture));

    internal void Seek(double seconds)
    {
        if (!IsRunning)
        {
            return;
        }

        var target = Math.Max(0d, seconds).ToString("F3", CultureInfo.InvariantCulture);
        lock (commandLock)
        {
            if (mpvContext == IntPtr.Zero)
            {
                return;
            }

            var result = MpvCommand(mpvContext, ["seek", target, "absolute", null]);
            if (result < 0)
            {
                AepLog.Warning($"[MPV] Could not seek to {target}s: {ErrorText(result)}");
            }
        }
    }

    private void SetProperty(string name, string value)
    {
        if (!IsRunning)
        {
            return;
        }

        lock (commandLock)
        {
            if (mpvContext != IntPtr.Zero)
            {
                _ = MpvCommand(mpvContext, ["set", name, value, null]);
            }
        }
    }

    internal MpvPlaybackInfo ReadPlaybackInfo()
    {
        if (!IsRunning)
        {
            return default;
        }

        lock (commandLock)
        {
            if (mpvContext == IntPtr.Zero)
            {
                return default;
            }

            _ = mpv_get_property(mpvContext, "time-pos", FormatDouble, out double position);
            _ = mpv_get_property(mpvContext, "duration", FormatDouble, out double duration);
            _ = mpv_get_property(mpvContext, "pause", FormatFlag, out int paused);
            _ = mpv_get_property(mpvContext, "core-idle", FormatFlag, out int coreIdle);
            _ = mpv_get_property(mpvContext, "seeking", FormatFlag, out int seeking);
            var hasAudioPosition = mpv_get_property(mpvContext, "audio-pts", FormatDouble, out double audioPosition) >= 0;
            var hasVideo = mpv_get_property(mpvContext, "vid", FormatInt64, out long _) >= 0;
            var videoIsStill = hasVideo
                && ((mpv_get_property(mpvContext, "current-tracks/video/albumart", FormatFlag, out int albumArt) >= 0 && albumArt == 1)
                    || (mpv_get_property(mpvContext, "current-tracks/video/image", FormatFlag, out int image) >= 0 && image == 1));
            return new MpvPlaybackInfo(position, duration, paused == 1, coreIdle == 1, seeking == 1,
                hasVideo && !videoIsStill, hasAudioPosition, hasAudioPosition ? audioPosition : 0d);
        }
    }

    internal string? ReadMediaTitle() => ReadStringProperty("media-title");

    internal string? CurrentUrl => currentUrl;

    private string? ReadStringProperty(string name)
    {
        if (!IsRunning)
        {
            return null;
        }

        lock (commandLock)
        {
            if (mpvContext == IntPtr.Zero)
            {
                return null;
            }

            var pointer = mpv_get_property_string(mpvContext, name);
            if (pointer == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                return Marshal.PtrToStringUTF8(pointer);
            }
            finally
            {
                mpv_free(pointer);
            }
        }
    }

    internal byte[]? CopyLatestFrame(out int frameWidth, out int frameHeight)
    {
        frameWidth = width;
        frameHeight = height;
        lock (snapshotLock)
        {
            if (latestSnapshot == IntPtr.Zero || frameBytes == 0)
            {
                return null;
            }

            var frame = new byte[frameBytes];
            Marshal.Copy(latestSnapshot, frame, 0, frameBytes);
            return frame;
        }
    }

    internal bool TryCopyLatestFrame(byte[] destination)
    {
        lock (snapshotLock)
        {
            if (latestSnapshot == IntPtr.Zero || frameBytes == 0 || destination.Length < frameBytes)
            {
                return false;
            }

            Marshal.Copy(latestSnapshot, destination, 0, frameBytes);
            return true;
        }
    }

    internal int FrameVersion => Volatile.Read(ref frameVersion);

    private int frameVersion;

    private void RenderLoop()
    {
        try
        {
            while (!disposed)
            {
                if (!frameReady.Wait(RenderWaitMilliseconds))
                {
                    continue;
                }

                frameReady.Reset();
                if (disposed)
                {
                    return;
                }

                RenderPendingFrame();
            }
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[MPV] Render loop stopped: {exception.Message}");
        }
    }

    private void RenderPendingFrame()
    {
        var flags = mpv_render_context_update(renderContext);
        if ((flags & 1) == 0)
        {
            return;
        }

        try
        {
            var result = mpv_render_context_render(renderContext, renderParameters);
            if (disposed)
            {
                return;
            }

            if (result < 0)
            {
                NoteRenderFailure($"render failed: {ErrorText(result)}");
                return;
            }

            var texture = targetTexture;
            if (texture is null)
            {
                return;
            }

            var snapshot = useSnapshotA ? snapshotA : snapshotB;
            useSnapshotA = !useSnapshotA;

            unsafe
            {
                System.Buffer.MemoryCopy((void*)frameBuffer, (void*)snapshot, frameBytes, frameBytes);
            }

            lock (snapshotLock)
            {
                latestSnapshot = snapshot;
            }

            Interlocked.Increment(ref frameVersion);
            Interlocked.Exchange(ref consecutiveRenderFailures, 0);

            var rowPitch = width * 4;
            DxHandler.RunOnRenderThread(RenderKey, () =>
            {
                DxHandler.Device?.ImmediateContext.UpdateSubresource(texture, 0, null, snapshot, rowPitch, 0);
            });
        }
        catch (Exception exception)
        {
            NoteRenderFailure(exception.Message);
        }
    }

    private void NoteRenderFailure(string reason)
    {
        var failures = Interlocked.Increment(ref consecutiveRenderFailures);
        if (failures == 1 || failures % MaxConsecutiveRenderFailures == 0)
        {
            AepLog.Warning($"[MPV] Frame {reason} ({failures} in a row)");
        }
    }

    private void EventLoop()
    {
        try
        {
            while (!disposed)
            {
                var handle = mpv_wait_event(mpvContext, EventWaitSeconds);
                if (handle == IntPtr.Zero)
                {
                    continue;
                }

                var eventId = Marshal.ReadInt32(handle);
                switch (eventId)
                {
                    case EventShutdown:
                        return;
                    case EventLogMessage:
                        HandleLogMessage(handle);
                        break;
                    case EventStartFile:
                        AepLog.Verbose("[MPV] Loading a file");
                        break;
                    case EventFileLoaded:
                        FileLoaded?.Invoke();
                        break;
                    case EventPlaybackRestart:
                        AepLog.Verbose("[MPV] Playback running");
                        break;
                    case EventEndFile:
                        HandleEndFile(handle);
                        break;
                    default:
                        break;
                }
            }
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[MPV] Event loop stopped: {exception.Message}");
        }
    }

    private void HandleLogMessage(IntPtr handle)
    {
        var data = Marshal.ReadIntPtr(handle + 16);
        if (data == IntPtr.Zero)
        {
            return;
        }

        var prefix = Marshal.PtrToStringUTF8(Marshal.ReadIntPtr(data));
        var level = Marshal.PtrToStringUTF8(Marshal.ReadIntPtr(data + 8));
        var text = Marshal.PtrToStringUTF8(Marshal.ReadIntPtr(data + 16))?.Trim();
        if (text is null || text.Length == 0)
        {
            return;
        }

        if (level is "error" or "fatal")
        {
            if (IsHttpForbiddenText(text))
            {
                Interlocked.Increment(ref httpForbiddenHits);
            }

            if (IsStreamErrorText(text))
            {
                Interlocked.Increment(ref streamErrorHits);
            }

            if (IsBotCheckText(text))
            {
                Interlocked.Increment(ref botCheckHits);
            }

            RememberErrorDetail(prefix, text);
            AepLog.Warning($"[MPV/{prefix}] {text}");
            return;
        }

        AepLog.Verbose($"[MPV/{prefix}/{level}] {text}");
    }

    private static bool IsHttpForbiddenText(string text) =>
        text.Contains("403", StringComparison.Ordinal)
        && (text.Contains("http", StringComparison.OrdinalIgnoreCase)
            || text.Contains("forbidden", StringComparison.OrdinalIgnoreCase));

    internal static bool IsBotCheckText(string text) =>
        text.Contains(BotCheckMarker, StringComparison.OrdinalIgnoreCase);

    private static bool IsStreamErrorText(string text)
    {
        for (var index = 0; index < StreamErrorMarkers.Length; index++)
        {
            if (text.Contains(StreamErrorMarkers[index], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void RememberErrorDetail(string? prefix, string text)
    {
        var fromResolver = prefix == ResolverLogPrefix;
        var hasDetail = lastErrorDetail is { Length: > 0 };
        if (fromResolver && hasDetail && text.StartsWith(ResolverFailedPrefix, StringComparison.Ordinal))
        {
            return;
        }

        if (!fromResolver && hasDetail && lastErrorFromResolver)
        {
            return;
        }

        var detail = text;
        var lineBreak = detail.IndexOfAny(LineBreaks);
        if (lineBreak >= 0)
        {
            detail = detail[..lineBreak];
        }

        if (fromResolver)
        {
            detail = CleanResolverError(detail);
        }

        if (detail.Length == 0)
        {
            return;
        }

        if (detail.Length > MaxErrorDetailLength)
        {
            detail = detail[..MaxErrorDetailLength].TrimEnd();
        }

        lastErrorDetail = detail;
        lastErrorFromResolver = fromResolver;
    }

    internal static string CleanResolverError(string line)
    {
        if (line.StartsWith(ResolverErrorPrefix, StringComparison.Ordinal))
        {
            line = line[ResolverErrorPrefix.Length..];
        }
        else if (line.StartsWith(ResolverFailedPrefix, StringComparison.Ordinal))
        {
            line = line[ResolverFailedPrefix.Length..];
        }

        line = ResolverSitePrefix.Replace(line, string.Empty, 1);
        for (var index = 0; index < ResolverAdviceMarkers.Length; index++)
        {
            var cut = line.IndexOf(ResolverAdviceMarkers[index], StringComparison.Ordinal);
            if (cut > 0)
            {
                line = line[..cut];
            }
        }

        line = line.Trim().TrimEnd('.');
        if (line.Length > 0 && char.IsLower(line[0]))
        {
            line = char.ToUpperInvariant(line[0]) + line[1..];
        }

        return line;
    }

    private void HandleEndFile(IntPtr handle)
    {
        var data = Marshal.ReadIntPtr(handle + 16);
        var reasonCode = 0;
        var errorCode = 0;
        if (data != IntPtr.Zero)
        {
            reasonCode = Marshal.ReadInt32(data);
            errorCode = Marshal.ReadInt32(data + 4);
        }

        var reason = reasonCode switch
        {
            2 => MpvEndReason.Stopped,
            3 => MpvEndReason.Quit,
            4 => MpvEndReason.Failed,
            5 => MpvEndReason.Redirect,
            _ => MpvEndReason.Finished,
        };

        string? detail = null;
        var failureKind = PlaybackFailureKind.Unknown;
        if (reason == MpvEndReason.Failed)
        {
            var errorText = ErrorText(errorCode);
            detail = lastErrorDetail is { Length: > 0 } logged ? logged : errorText;
            failureKind = SawBotCheck ? PlaybackFailureKind.BotCheck : PlaybackFailureKind.Unknown;
            AepLog.Warning($"[MPV] Playback failed ({errorText}): {detail}");
        }

        FileEnded?.Invoke(new PlaybackEnding(reason, detail, failureKind));
    }

    private static string ErrorText(int error)
    {
        try
        {
            var pointer = mpv_error_string(error);
            return pointer == IntPtr.Zero
                ? error.ToString(CultureInfo.InvariantCulture)
                : Marshal.PtrToStringUTF8(pointer) ?? error.ToString(CultureInfo.InvariantCulture);
        }
        catch (Exception)
        {
            return error.ToString(CultureInfo.InvariantCulture);
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        started = false;
        currentUrl = null;
        frameReady.Set();

        renderThread?.Join(2000);
        eventThread?.Join(2000);

        DxHandler.CancelRenderThreadWork(RenderKey);

        lock (snapshotLock)
        {
            latestSnapshot = IntPtr.Zero;
        }

        if (renderContext != IntPtr.Zero)
        {
            mpv_render_context_free(renderContext);
            renderContext = IntPtr.Zero;
        }

        if (updateCallbackHandle.IsAllocated)
        {
            updateCallbackHandle.Free();
        }

        lock (commandLock)
        {
            if (mpvContext != IntPtr.Zero)
            {
                mpv_terminate_destroy(mpvContext);
                mpvContext = IntPtr.Zero;
            }
        }

        FreeUnmanaged(ref frameBuffer);
        FreeUnmanaged(ref snapshotA);
        FreeUnmanaged(ref snapshotB);
        FreeUnmanaged(ref sizeParameter);
        FreeUnmanaged(ref strideParameter);
        FreeUnmanaged(ref formatParameter);
        FreeUnmanaged(ref renderParameters);

        targetTexture = null;
        frameReady.Dispose();
        GC.SuppressFinalize(this);
    }

    private static void FreeUnmanaged(ref IntPtr pointer)
    {
        if (pointer == IntPtr.Zero)
        {
            return;
        }

        Marshal.FreeHGlobal(pointer);
        pointer = IntPtr.Zero;
    }
}

internal readonly record struct MpvPlaybackInfo(
    double PositionSeconds,
    double DurationSeconds,
    bool Paused,
    bool CoreIdle,
    bool Seeking,
    bool HasMovingVideo,
    bool HasAudioPosition,
    double AudioPositionSeconds);
