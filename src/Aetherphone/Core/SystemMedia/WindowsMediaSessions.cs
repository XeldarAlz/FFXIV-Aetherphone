using System.Collections.Concurrent;
using Aetherphone.Core.Platform;

namespace Aetherphone.Core.SystemMedia;

internal sealed class WindowsMediaSessions : IDisposable
{
    private const string ManagerClass = "Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager";

    private const int RequestManagerSlot = 6;
    private const int CurrentSessionSlot = 6;
    private const int SessionsSlot = 7;
    private const int VectorGetAtSlot = 6;
    private const int VectorSizeSlot = 7;
    private const int SessionAppIdSlot = 6;
    private const int SessionMediaPropertiesSlot = 7;
    private const int SessionTimelineSlot = 8;
    private const int SessionPlaybackInfoSlot = 9;
    private const int SessionPlaySlot = 10;
    private const int SessionPauseSlot = 11;
    private const int SessionNextSlot = 16;
    private const int SessionPreviousSlot = 17;
    private const int SessionTogglePlayPauseSlot = 20;
    private const int SessionSeekSlot = 24;
    private const int MediaTitleSlot = 6;
    private const int MediaAlbumArtistSlot = 8;
    private const int MediaArtistSlot = 9;
    private const int MediaAlbumSlot = 10;
    private const int MediaThumbnailSlot = 15;
    private const int TimelineStartSlot = 6;
    private const int TimelineEndSlot = 7;
    private const int TimelinePositionSlot = 10;
    private const int TimelineUpdatedSlot = 11;
    private const int PlaybackControlsSlot = 6;
    private const int PlaybackStatusSlot = 7;
    private const int ControlsPlaySlot = 6;
    private const int ControlsPauseSlot = 7;
    private const int ControlsNextSlot = 12;
    private const int ControlsPreviousSlot = 13;
    private const int ControlsToggleSlot = 16;
    private const int ControlsSeekSlot = 20;

    private const int PollMilliseconds = 1000;
    private const int CommandSettleMilliseconds = 150;
    private const int DisabledRecheckMilliseconds = 2000;
    private const long IdleAfterMilliseconds = 5000;
    private const int RequestTimeoutMilliseconds = 5000;
    private const int PropertiesTimeoutMilliseconds = 2000;
    private const int ArtworkTimeoutMilliseconds = 3000;
    private const int MaximumArtworkBytes = 4 * 1024 * 1024;
    private const int MaximumSessions = 16;
    private const long FileTimeEpochTicks = 504911232000000000L;

    private const int SupportUnknown = 0;
    private const int SupportAvailable = 1;
    private const int SupportUnavailable = 2;

    private static readonly Guid ManagerStaticsId = new("2050c4ee-11a0-57de-aed7-c97c70338245");

    private readonly Configuration configuration;
    private readonly string processFileName;
    private readonly ConcurrentQueue<MediaSessionCommand> commands = new();
    private readonly MtaWorker worker;
    private readonly nint[] sessions = new nint[MaximumSessions];
    private readonly string[] sessionAppIds = new string[MaximumSessions];
    private readonly MediaSessionCandidate[] candidates = new MediaSessionCandidate[MaximumSessions];

    private SnapshotBox current = new(MediaSessionSnapshot.Empty);
    private volatile int support = SupportUnknown;
    private long demandedAt;

    private nint manager;
    private nint selected;
    private string selectedAppId = string.Empty;
    private long timelineStartTicks;
    private ArtworkRefresh artworkRefresh;
    private byte[]? artwork;
    private ulong artworkFingerprint;
    private int artworkTrackKey;
    private int artworkRevision;

    public WindowsMediaSessions(Configuration configuration)
    {
        this.configuration = configuration;
        processFileName = Path.GetFileName(Environment.ProcessPath ?? string.Empty);
        Array.Fill(sessionAppIds, string.Empty);
        worker = new MtaWorker("Aetherphone media sessions", Tick, Teardown);
    }

    public bool IsSupported => support == SupportAvailable;

    public ref readonly MediaSessionSnapshot Current
    {
        get
        {
            MarkDemand();
            return ref Latest;
        }
    }

    private ref readonly MediaSessionSnapshot Latest => ref Volatile.Read(ref current).Value;

    public void Play() => Enqueue(MediaSessionCommandKind.Play, 0);

    public void Pause() => Enqueue(MediaSessionCommandKind.Pause, 0);

    public void TogglePlayPause() => Enqueue(MediaSessionCommandKind.TogglePlayPause, 0);

    public void Next() => Enqueue(MediaSessionCommandKind.Next, 0);

    public void Previous() => Enqueue(MediaSessionCommandKind.Previous, 0);

    public void Seek(TimeSpan position) => Enqueue(MediaSessionCommandKind.Seek, Math.Max(0, position.Ticks));

    public void Dispose() => worker.Dispose();

    private void Enqueue(MediaSessionCommandKind kind, long positionTicks)
    {
        if (support == SupportUnavailable)
        {
            return;
        }

        commands.Enqueue(new MediaSessionCommand(kind, positionTicks));
        Volatile.Write(ref demandedAt, Environment.TickCount64);
        worker.Wake();
    }

    private void MarkDemand()
    {
        var now = Environment.TickCount64;
        var previous = Volatile.Read(ref demandedAt);
        Volatile.Write(ref demandedAt, now);
        if (now - previous >= IdleAfterMilliseconds)
        {
            worker.Wake();
        }
    }

    private int Tick()
    {
        if (Environment.TickCount64 - Volatile.Read(ref demandedAt) >= IdleAfterMilliseconds)
        {
            return Timeout.Infinite;
        }

        if (!configuration.ShowWindowsMedia)
        {
            commands.Clear();
            ReleaseSelected();
            Publish(MediaSessionSnapshot.Empty);
            return DisabledRecheckMilliseconds;
        }

        if (manager == 0 && !TryInitialize())
        {
            return MtaWorker.Exit;
        }

        var commanded = ExecuteCommands();
        Poll();
        return commanded ? CommandSettleMilliseconds : PollMilliseconds;
    }

    private bool TryInitialize()
    {
        if (NativeFileDialog.RunsUnderWine)
        {
            MarkUnavailable("not available under Wine", 0);
            return false;
        }

        var status = WinRt.GetActivationFactory(ManagerClass, ManagerStaticsId, out var statics);
        if (!ComCall.Succeeded(status))
        {
            MarkUnavailable("session manager activation failed", status);
            return false;
        }

        nint operation = 0;
        try
        {
            status = ComCall.GetPointer(statics, RequestManagerSlot, out operation);
            if (!ComCall.Succeeded(status)
                || !WinRt.WaitForPointer(operation, RequestTimeoutMilliseconds, out manager)
                || manager == 0)
            {
                MarkUnavailable("session manager request failed", status);
                return false;
            }
        }
        finally
        {
            ComCall.Release(operation);
            ComCall.Release(statics);
        }

        support = SupportAvailable;
        AepLog.Info("[SystemMedia] reading Windows media sessions");
        return true;
    }

    private void MarkUnavailable(string reason, int status)
    {
        support = SupportUnavailable;
        commands.Clear();
        AepLog.Info($"[SystemMedia] media sessions unavailable: {reason} (0x{status:X8})");
    }

    private bool ExecuteCommands()
    {
        var executed = false;
        while (commands.TryDequeue(out var command))
        {
            if (selected == 0)
            {
                continue;
            }

            nint operation;
            var status = command.Kind switch
            {
                MediaSessionCommandKind.Play => ComCall.GetPointer(selected, SessionPlaySlot, out operation),
                MediaSessionCommandKind.Pause => ComCall.GetPointer(selected, SessionPauseSlot, out operation),
                MediaSessionCommandKind.TogglePlayPause => ComCall.GetPointer(selected, SessionTogglePlayPauseSlot,
                    out operation),
                MediaSessionCommandKind.Next => ComCall.GetPointer(selected, SessionNextSlot, out operation),
                MediaSessionCommandKind.Previous => ComCall.GetPointer(selected, SessionPreviousSlot, out operation),
                _ => ComCall.GetPointerWithInt64(selected, SessionSeekSlot, command.PositionTicks + timelineStartTicks,
                    out operation),
            };
            ComCall.Release(operation);
            executed |= ComCall.Succeeded(status);
        }

        return executed;
    }

    private void Poll()
    {
        var count = CollectSessions();
        try
        {
            var picked = MediaSessionPicker.Pick(candidates.AsSpan(0, count));
            if (picked == MediaSessionPicker.None)
            {
                ReleaseSelected();
                Publish(MediaSessionSnapshot.Empty);
                return;
            }

            ComCall.Release(selected);
            selected = sessions[picked];
            sessions[picked] = 0;
            selectedAppId = sessionAppIds[picked];
            ReadSelected();
        }
        finally
        {
            for (var index = 0; index < count; index++)
            {
                ComCall.Release(ref sessions[index]);
            }
        }
    }

    private int CollectSessions()
    {
        nint list = 0;
        nint currentIdentity = 0;
        try
        {
            if (!ComCall.Succeeded(ComCall.GetPointer(manager, SessionsSlot, out list)) || list == 0
                || !ComCall.Succeeded(ComCall.GetInt32(list, VectorSizeSlot, out var size)))
            {
                return 0;
            }

            currentIdentity = CurrentSessionIdentity();
            var count = Math.Min(size, MaximumSessions);
            var filled = 0;
            for (var index = 0; index < count; index++)
            {
                if (!ComCall.Succeeded(ComCall.GetPointerWithUInt32(list, VectorGetAtSlot, (uint)index,
                        out var session)) || session == 0)
                {
                    continue;
                }

                sessions[filled] = session;
                candidates[filled] = Describe(session, filled, currentIdentity);
                filled++;
            }

            return filled;
        }
        finally
        {
            ComCall.Release(currentIdentity);
            ComCall.Release(list);
        }
    }

    private nint CurrentSessionIdentity()
    {
        if (!ComCall.Succeeded(ComCall.GetPointer(manager, CurrentSessionSlot, out var session)) || session == 0)
        {
            return 0;
        }

        try
        {
            return ComCall.Succeeded(ComCall.QueryInterface(session, WinRt.UnknownId, out var identity)) ? identity : 0;
        }
        finally
        {
            ComCall.Release(session);
        }
    }

    private MediaSessionCandidate Describe(nint session, int sessionIndex, nint currentIdentity)
    {
        var appId = ReadString(session, SessionAppIdSlot, sessionAppIds[sessionIndex]);
        sessionAppIds[sessionIndex] = appId;
        var isCurrent = false;
        if (currentIdentity != 0 && ComCall.Succeeded(ComCall.QueryInterface(session, WinRt.UnknownId, out var identity)))
        {
            isCurrent = identity == currentIdentity;
            ComCall.Release(identity);
        }

        var playback = ReadPlayback(session, out _);
        return new MediaSessionCandidate(MediaAppNames.IsOwnProcess(appId, processFileName), isCurrent,
            playback == MediaSessionPlayback.Playing, appId.Length > 0 && appId == selectedAppId);
    }

    private static MediaSessionPlayback ReadPlayback(nint session, out MediaSessionControls controls)
    {
        controls = MediaSessionControls.None;
        if (!ComCall.Succeeded(ComCall.GetPointer(session, SessionPlaybackInfoSlot, out var info)) || info == 0)
        {
            return MediaSessionPlayback.Closed;
        }

        try
        {
            controls = ReadControls(info);
            return ComCall.Succeeded(ComCall.GetInt32(info, PlaybackStatusSlot, out var status))
                ? (MediaSessionPlayback)status
                : MediaSessionPlayback.Closed;
        }
        finally
        {
            ComCall.Release(info);
        }
    }

    private static MediaSessionControls ReadControls(nint playbackInfo)
    {
        if (!ComCall.Succeeded(ComCall.GetPointer(playbackInfo, PlaybackControlsSlot, out var controls))
            || controls == 0)
        {
            return MediaSessionControls.None;
        }

        try
        {
            var flags = MediaSessionControls.None;
            flags |= Flag(controls, ControlsPlaySlot, MediaSessionControls.Play);
            flags |= Flag(controls, ControlsPauseSlot, MediaSessionControls.Pause);
            flags |= Flag(controls, ControlsToggleSlot, MediaSessionControls.PlayPauseToggle);
            flags |= Flag(controls, ControlsNextSlot, MediaSessionControls.Next);
            flags |= Flag(controls, ControlsPreviousSlot, MediaSessionControls.Previous);
            flags |= Flag(controls, ControlsSeekSlot, MediaSessionControls.Seek);
            return flags;
        }
        finally
        {
            ComCall.Release(controls);
        }
    }

    private static MediaSessionControls Flag(nint controls, int slot, MediaSessionControls flag) =>
        ComCall.Succeeded(ComCall.GetBoolean(controls, slot, out var enabled)) && enabled
            ? flag
            : MediaSessionControls.None;

    private void ReadSelected()
    {
        var previous = Latest;
        var playback = ReadPlayback(selected, out var controls);
        var timeline = ReadTimeline(selected, previous);
        var title = previous.Title;
        var artist = previous.Artist;
        var album = previous.Album;
        nint properties = 0;
        nint operation = 0;
        try
        {
            if (ComCall.Succeeded(ComCall.GetPointer(selected, SessionMediaPropertiesSlot, out operation))
                && WinRt.WaitForPointer(operation, PropertiesTimeoutMilliseconds, out properties)
                && properties != 0)
            {
                title = ReadString(properties, MediaTitleSlot, previous.Title);
                artist = ReadString(properties, MediaArtistSlot, previous.Artist);
                if (artist.Length == 0)
                {
                    artist = ReadString(properties, MediaAlbumArtistSlot, previous.Artist);
                }

                album = ReadString(properties, MediaAlbumSlot, previous.Album);
                RefreshArtwork(properties, HashCode.Combine(selectedAppId, title, artist, album));
            }
        }
        finally
        {
            ComCall.Release(properties);
            ComCall.Release(operation);
        }

        var appName = ReferenceEquals(selectedAppId, previous.AppId)
            ? previous.AppName
            : ResolveAppName(selectedAppId);
        Publish(new MediaSessionSnapshot(selectedAppId, appName, title, artist, album, playback, controls,
            timeline.Position, timeline.Duration, timeline.UpdatedUtcTicks, artwork, artworkRevision));
    }

    private static string ResolveAppName(string appId)
    {
        var displayName = ShellAppNames.DisplayName(appId);
        return displayName.Length > 0 ? displayName : MediaAppNames.FromAppUserModelId(appId);
    }

    private (TimeSpan Position, TimeSpan Duration, long UpdatedUtcTicks) ReadTimeline(nint session,
        in MediaSessionSnapshot previous)
    {
        if (!ComCall.Succeeded(ComCall.GetPointer(session, SessionTimelineSlot, out var timeline)) || timeline == 0)
        {
            return (TimeSpan.Zero, TimeSpan.Zero, 0);
        }

        try
        {
            _ = ComCall.GetInt64(timeline, TimelineStartSlot, out var start);
            _ = ComCall.GetInt64(timeline, TimelineEndSlot, out var end);
            _ = ComCall.GetInt64(timeline, TimelinePositionSlot, out var position);
            _ = ComCall.GetInt64(timeline, TimelineUpdatedSlot, out var updated);
            timelineStartTicks = start;
            var relative = TimeSpan.FromTicks(Math.Max(0, position - start));
            var duration = TimeSpan.FromTicks(Math.Max(0, end - start));
            long updatedUtcTicks;
            if (updated > 0)
            {
                updatedUtcTicks = updated + FileTimeEpochTicks;
            }
            else
            {
                updatedUtcTicks = relative == previous.Position && previous.PositionUpdatedUtcTicks != 0
                    ? previous.PositionUpdatedUtcTicks
                    : DateTime.UtcNow.Ticks;
            }

            return (relative, duration, updatedUtcTicks);
        }
        finally
        {
            ComCall.Release(timeline);
        }
    }

    private void RefreshArtwork(nint properties, int trackKey)
    {
        var now = Environment.TickCount64;
        if (!artworkRefresh.ShouldFetch(trackKey, now))
        {
            return;
        }

        var bytes = FetchArtwork(properties);
        artworkRefresh.Fetched(bytes != null, now);
        if (bytes == null)
        {
            if (artwork != null && artworkTrackKey != trackKey)
            {
                artwork = null;
                artworkFingerprint = 0;
                artworkRevision++;
            }

            return;
        }

        artworkTrackKey = trackKey;
        var fingerprint = ArtworkFingerprint.Of(bytes);
        if (fingerprint == artworkFingerprint)
        {
            return;
        }

        artwork = bytes;
        artworkFingerprint = fingerprint;
        artworkRevision++;
    }

    private static byte[]? FetchArtwork(nint properties)
    {
        if (!ComCall.Succeeded(ComCall.GetPointer(properties, MediaThumbnailSlot, out var thumbnail)) || thumbnail == 0)
        {
            return null;
        }

        try
        {
            return WinRtStreams.ReadReference(thumbnail, ArtworkTimeoutMilliseconds, MaximumArtworkBytes);
        }
        finally
        {
            ComCall.Release(thumbnail);
        }
    }

    private static string ReadString(nint instance, int slot, string previous) =>
        ComCall.Succeeded(ComCall.GetPointer(instance, slot, out var handle))
            ? WinRt.TakeString(handle, previous)
            : string.Empty;

    private void Publish(in MediaSessionSnapshot snapshot)
    {
        if (snapshot.SameContent(Latest))
        {
            return;
        }

        Volatile.Write(ref current, new SnapshotBox(snapshot));
    }

    private void ReleaseSelected()
    {
        ComCall.Release(ref selected);
        selectedAppId = string.Empty;
        timelineStartTicks = 0;
        artworkRefresh.Reset();
        artworkFingerprint = 0;
        artworkTrackKey = 0;
        if (artwork == null)
        {
            return;
        }

        artwork = null;
        artworkRevision++;
    }

    private void Teardown()
    {
        ReleaseSelected();
        ComCall.Release(ref manager);
        Publish(MediaSessionSnapshot.Empty);
    }

    private sealed class SnapshotBox
    {
        public readonly MediaSessionSnapshot Value;

        public SnapshotBox(in MediaSessionSnapshot value)
        {
            Value = value;
        }
    }
}
