using Aetherphone.Core.Platform;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.SystemMedia;

internal sealed class WindowsMediaPublisher : IDisposable
{
    private const string ControlsClass = "Windows.Media.SystemMediaTransportControls";
    private const string TimelineClass = "Windows.Media.SystemMediaTransportControlsTimelineProperties";

    private const int GetForWindowSlot = 6;
    private const int PlaybackStatusSlot = 7;
    private const int DisplayUpdaterSlot = 8;
    private const int IsEnabledSlot = 11;
    private const int IsPlayEnabledSlot = 13;
    private const int IsStopEnabledSlot = 15;
    private const int IsPauseEnabledSlot = 17;
    private const int IsPreviousEnabledSlot = 25;
    private const int IsNextEnabledSlot = 27;
    private const int AddButtonPressedSlot = 32;
    private const int RemoveButtonPressedSlot = 33;
    private const int UpdateTimelineSlot = 12;
    private const int UpdaterTypeSlot = 7;
    private const int UpdaterThumbnailSlot = 11;
    private const int UpdaterMusicSlot = 12;
    private const int UpdaterUpdateSlot = 17;
    private const int MusicTitleSlot = 7;
    private const int MusicArtistSlot = 11;
    private const int MusicAlbumSlot = 7;
    private const int TimelineStartSlot = 7;
    private const int TimelineEndSlot = 9;
    private const int TimelineMinimumSeekSlot = 11;
    private const int TimelineMaximumSeekSlot = 13;
    private const int TimelinePositionSlot = 15;
    private const int MusicPlaybackType = 1;

    private const int ConfigurationRecheckMilliseconds = 2000;
    private const int AttachRetryMilliseconds = 2000;
    private const int SupportUnknown = 0;
    private const int SupportAvailable = 1;
    private const int SupportUnavailable = 2;

    private static readonly Guid InteropId = new("ddb0472d-c911-4a1f-86d9-dc3d71a95f5a");
    private static readonly Guid ControlsId = new("99fa3ff4-1742-42a6-902e-087d41f965ec");
    private static readonly Guid Controls2Id = new("ea98d2f6-7f3c-4af2-a586-72889808efb1");
    private static readonly Guid Music2Id = new("00368462-97d3-44b9-b00f-008afcefaf18");
    private static readonly Guid TimelineId = new("5125316a-c3a2-475b-8507-93534dc88f15");

    private readonly Configuration configuration;
    private readonly IFramework framework;
    private readonly Func<nint> windowHandle;
    private readonly object gate = new();
    private readonly MtaWorker worker;

    private PublisherState desired = PublisherState.Initial;
    private volatile int support = SupportUnknown;

    private nint controls;
    private long buttonToken;
    private bool buttonRegistered;
    private bool appliedActive;
    private PublisherState applied = PublisherState.Unapplied;

    public WindowsMediaPublisher(Configuration configuration, IFramework framework, Func<nint> windowHandle)
    {
        this.configuration = configuration;
        this.framework = framework;
        this.windowHandle = windowHandle;
        worker = new MtaWorker("Aetherphone media publisher", Tick, Teardown);
    }

    public event Action<MediaTransportButton>? ButtonPressed;

    public bool IsSupported => support == SupportAvailable;

    public void Enable() => SetEnabled(true);

    public void Disable() => SetEnabled(false);

    public void SetPlaybackStatus(MediaTransportStatus status)
    {
        lock (gate)
        {
            if (desired.Status == status)
            {
                return;
            }

            desired.Status = status;
        }

        worker.Wake();
    }

    public void SetNavigation(bool canNext, bool canPrevious)
    {
        lock (gate)
        {
            if (desired.CanNext == canNext && desired.CanPrevious == canPrevious)
            {
                return;
            }

            desired.CanNext = canNext;
            desired.CanPrevious = canPrevious;
        }

        worker.Wake();
    }

    public void SetMetadata(string title, string artist, string album, byte[]? thumbnail) =>
        StoreMetadata(title, artist, album, thumbnail, null);

    public void SetMetadataWithThumbnailFile(string title, string artist, string album, string? thumbnailPath) =>
        StoreMetadata(title, artist, album, null, thumbnailPath);

    public void SetTimeline(TimeSpan position, TimeSpan duration)
    {
        lock (gate)
        {
            desired.PositionTicks = Math.Max(0, position.Ticks);
            desired.DurationTicks = Math.Max(0, duration.Ticks);
            desired.TimelineRevision++;
        }

        worker.Wake();
    }

    public void Dispose()
    {
        ButtonPressed = null;
        worker.Dispose();
    }

    private void StoreMetadata(string title, string artist, string album, byte[]? thumbnail, string? thumbnailPath)
    {
        lock (gate)
        {
            desired.Title = title;
            desired.Artist = artist;
            desired.Album = album;
            desired.Thumbnail = thumbnail;
            desired.ThumbnailPath = thumbnailPath;
            desired.MetadataRevision++;
        }

        worker.Wake();
    }

    private void SetEnabled(bool enabled)
    {
        lock (gate)
        {
            if (desired.Enabled == enabled)
            {
                return;
            }

            desired.Enabled = enabled;
        }

        worker.Wake();
    }

    private int Tick()
    {
        PublisherState wanted;
        lock (gate)
        {
            wanted = desired;
        }

        var allowed = configuration.PublishToWindowsMedia;
        if (!wanted.Enabled || !allowed || !IsLive(wanted.Status))
        {
            Deactivate();
            return wanted.Enabled && !allowed ? ConfigurationRecheckMilliseconds : Timeout.Infinite;
        }

        if (controls == 0)
        {
            if (support == SupportUnavailable || !TryAttach())
            {
                return support == SupportUnavailable ? MtaWorker.Exit : AttachRetryMilliseconds;
            }
        }

        Apply(wanted);
        return ConfigurationRecheckMilliseconds;
    }

    private static bool IsLive(MediaTransportStatus status) =>
        status is MediaTransportStatus.Playing or MediaTransportStatus.Paused or MediaTransportStatus.Changing;

    private bool TryAttach()
    {
        if (NativeFileDialog.RunsUnderWine)
        {
            MarkUnavailable("not available under Wine", 0);
            return false;
        }

        var window = windowHandle();
        if (window == 0)
        {
            return false;
        }

        var status = WinRt.GetActivationFactory(ControlsClass, InteropId, out var interop);
        if (!ComCall.Succeeded(status))
        {
            MarkUnavailable("transport controls activation failed", status);
            return false;
        }

        try
        {
            status = ComCall.GetInterfaceWithPointer(interop, GetForWindowSlot, window, ControlsId, out controls);
            if (!ComCall.Succeeded(status) || controls == 0)
            {
                controls = 0;
                MarkUnavailable("transport controls for the game window failed", status);
                return false;
            }
        }
        finally
        {
            ComCall.Release(interop);
        }

        RegisterButtons();
        support = SupportAvailable;
        AepLog.Info("[SystemMedia] publishing phone playback to Windows media controls");
        return true;
    }

    private void RegisterButtons()
    {
        var handler = TransportButtonHandler.Create(OnButton);
        try
        {
            buttonRegistered = ComCall.Succeeded(ComCall.AddHandler(controls, AddButtonPressedSlot, handler,
                out buttonToken));
        }
        finally
        {
            ComCall.Release(handler);
        }
    }

    private void MarkUnavailable(string reason, int status)
    {
        support = SupportUnavailable;
        AepLog.Info($"[SystemMedia] media publishing unavailable: {reason} (0x{status:X8})");
    }

    private void OnButton(int button)
    {
        if (button is not ((int)MediaTransportButton.Play or (int)MediaTransportButton.Pause
            or (int)MediaTransportButton.Stop or (int)MediaTransportButton.Next
            or (int)MediaTransportButton.Previous))
        {
            return;
        }

        var pressed = (MediaTransportButton)button;
        _ = framework.RunOnFrameworkThread(() => ButtonPressed?.Invoke(pressed));
    }

    private void Apply(in PublisherState wanted)
    {
        if (!appliedActive)
        {
            _ = ComCall.SetBoolean(controls, IsPlayEnabledSlot, true);
            _ = ComCall.SetBoolean(controls, IsPauseEnabledSlot, true);
            _ = ComCall.SetBoolean(controls, IsStopEnabledSlot, true);
            _ = ComCall.SetBoolean(controls, IsEnabledSlot, true);
            ApplyNavigation(wanted);
            appliedActive = true;
        }
        else if (applied.CanNext != wanted.CanNext || applied.CanPrevious != wanted.CanPrevious)
        {
            ApplyNavigation(wanted);
        }

        if (applied.Status != wanted.Status)
        {
            _ = ComCall.SetInt32(controls, PlaybackStatusSlot, (int)wanted.Status);
            applied.Status = wanted.Status;
        }

        if (applied.MetadataRevision != wanted.MetadataRevision)
        {
            ApplyMetadata(wanted);
            applied.MetadataRevision = wanted.MetadataRevision;
        }

        if (applied.TimelineRevision != wanted.TimelineRevision)
        {
            ApplyTimeline(wanted);
            applied.TimelineRevision = wanted.TimelineRevision;
        }
    }

    private void ApplyNavigation(in PublisherState wanted)
    {
        _ = ComCall.SetBoolean(controls, IsNextEnabledSlot, wanted.CanNext);
        _ = ComCall.SetBoolean(controls, IsPreviousEnabledSlot, wanted.CanPrevious);
        applied.CanNext = wanted.CanNext;
        applied.CanPrevious = wanted.CanPrevious;
    }

    private void ApplyMetadata(in PublisherState wanted)
    {
        nint updater = 0;
        nint music = 0;
        nint music2 = 0;
        nint thumbnail = 0;
        try
        {
            if (!ComCall.Succeeded(ComCall.GetPointer(controls, DisplayUpdaterSlot, out updater)) || updater == 0)
            {
                return;
            }

            _ = ComCall.SetInt32(updater, UpdaterTypeSlot, MusicPlaybackType);
            if (ComCall.Succeeded(ComCall.GetPointer(updater, UpdaterMusicSlot, out music)) && music != 0)
            {
                SetString(music, MusicTitleSlot, wanted.Title);
                SetString(music, MusicArtistSlot, wanted.Artist);
                if (ComCall.Succeeded(ComCall.QueryInterface(music, Music2Id, out music2)))
                {
                    SetString(music2, MusicAlbumSlot, wanted.Album);
                }
            }

            var bytes = wanted.Thumbnail ?? ReadThumbnailFile(wanted.ThumbnailPath);
            thumbnail = bytes == null ? 0 : WinRtStreams.CreateReference(bytes);
            _ = ComCall.SetPointer(updater, UpdaterThumbnailSlot, thumbnail);
            _ = ComCall.Invoke(updater, UpdaterUpdateSlot);
        }
        finally
        {
            ComCall.Release(thumbnail);
            ComCall.Release(music2);
            ComCall.Release(music);
            ComCall.Release(updater);
        }
    }

    private static byte[]? ReadThumbnailFile(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        try
        {
            return File.ReadAllBytes(path);
        }
        catch (Exception exception)
        {
            AepLog.Debug(exception, $"[SystemMedia] reading thumbnail {path} failed");
            return null;
        }
    }

    private void ApplyTimeline(in PublisherState wanted)
    {
        nint timeline = 0;
        nint controls2 = 0;
        try
        {
            if (!ComCall.Succeeded(WinRt.ActivateInstance(TimelineClass, TimelineId, out timeline))
                || !ComCall.Succeeded(ComCall.QueryInterface(controls, Controls2Id, out controls2)))
            {
                return;
            }

            var position = Math.Min(wanted.PositionTicks, Math.Max(wanted.DurationTicks, wanted.PositionTicks));
            _ = ComCall.SetInt64(timeline, TimelineStartSlot, 0);
            _ = ComCall.SetInt64(timeline, TimelineEndSlot, wanted.DurationTicks);
            _ = ComCall.SetInt64(timeline, TimelineMinimumSeekSlot, 0);
            _ = ComCall.SetInt64(timeline, TimelineMaximumSeekSlot, wanted.DurationTicks);
            _ = ComCall.SetInt64(timeline, TimelinePositionSlot, position);
            _ = ComCall.SetPointer(controls2, UpdateTimelineSlot, timeline);
        }
        finally
        {
            ComCall.Release(controls2);
            ComCall.Release(timeline);
        }
    }

    private static void SetString(nint instance, int slot, string value)
    {
        using var text = new HString(value);
        _ = ComCall.SetPointer(instance, slot, text.Handle);
    }

    private void Deactivate()
    {
        if (!appliedActive || controls == 0)
        {
            return;
        }

        _ = ComCall.SetInt32(controls, PlaybackStatusSlot, (int)MediaTransportStatus.Closed);
        _ = ComCall.SetBoolean(controls, IsEnabledSlot, false);
        appliedActive = false;
        applied = PublisherState.Unapplied;
    }

    private void Teardown()
    {
        if (controls == 0)
        {
            return;
        }

        Deactivate();
        if (buttonRegistered)
        {
            _ = ComCall.SetInt64(controls, RemoveButtonPressedSlot, buttonToken);
            buttonRegistered = false;
        }

        ComCall.Release(ref controls);
    }

    private struct PublisherState
    {
        public static readonly PublisherState Initial = new()
        {
            Title = string.Empty,
            Artist = string.Empty,
            Album = string.Empty,
            Status = MediaTransportStatus.Closed,
        };

        public static readonly PublisherState Unapplied = new()
        {
            Title = string.Empty,
            Artist = string.Empty,
            Album = string.Empty,
            Status = (MediaTransportStatus)byte.MaxValue,
            MetadataRevision = -1,
            TimelineRevision = -1,
        };

        public bool Enabled;
        public MediaTransportStatus Status;
        public bool CanNext;
        public bool CanPrevious;
        public string Title;
        public string Artist;
        public string Album;
        public byte[]? Thumbnail;
        public string? ThumbnailPath;
        public int MetadataRevision;
        public long PositionTicks;
        public long DurationTicks;
        public int TimelineRevision;
    }
}
