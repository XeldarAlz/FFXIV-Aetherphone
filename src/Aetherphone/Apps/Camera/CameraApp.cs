using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Camera;

internal sealed class CameraApp : IPhoneApp
{
    private const float TopBarHeight = 88f;
    private const float TrayHeight = 172f;
    private const float SideBarWidth = 88f;
    private const float SideTrayWidth = 172f;
    private const float ControlRowLift = 26f;
    private const float ControlEdgeInset = 34f;
    private const float ControlPitch = 48f;
    private const float ColumnTop = 64f;
    private const float DialDrop = 22f;
    private const float ShutterDrop = 92f;
    private const float TrayControlInset = 46f;
    private const float SideTrayControlInset = 52f;
    private const float SideDialTop = 44f;
    private const float SideDialGap = 10f;
    private const float SideWellBottom = 30f;
    private const float FlashDuration = 0.42f;
    private const float ReticleDuration = 1.1f;
    private const float FlightSmoothTime = 0.2f;
    private const float FlightLanded = 0.995f;
    private const float MaxFrameSeconds = 0.1f;
    private const int SquareModeIndex = 0;
    private const int PhotoModeIndex = 1;
    private const int CaptureDelayFrames = 3;
    private const int CaptureWatchdogTicks = 30;
    private const int ShortTimerSeconds = 3;
    private const int LongTimerSeconds = 10;
    private const float RequestedShotSettleSeconds = 0.6f;
    private static readonly LocString[] Modes = { L.Camera.ModeSquare, L.Camera.ModePhoto };
    private static readonly string[] ModeAnchors = { "camera.mode.square", "camera.mode.photo" };
    private static readonly string[] CountdownDigits = new string[LongTimerSeconds + 1];

    public string Id => "camera";
    public string DisplayName => Loc.T(L.Apps.Camera);
    public string Glyph => "O";
    public int BadgeCount => 0;
    public bool WantsTransparentScreen => true;

    public Rect? TransparentViewport(Rect screen, float scale) => ViewfinderRect(screen, scale);

    private static Rect ViewfinderRect(Rect screen, float scale)
    {
        if (screen.IsLandscape())
        {
            return new Rect(new Vector2(screen.Min.X + SideBarWidth * scale, screen.Min.Y),
                new Vector2(screen.Max.X - SideTrayWidth * scale, screen.Max.Y));
        }

        return new Rect(new Vector2(screen.Min.X, screen.Min.Y + TopBarHeight * scale),
            new Vector2(screen.Max.X, screen.Max.Y - TrayHeight * scale));
    }

    private readonly PhotoCaptureService capture;
    private readonly PhotoLibrary library;
    private readonly Configuration configuration;
    private readonly GameUiVisibility gameUiVisibility;
    private readonly CameraShutter shutter;
    private readonly CameraModeDial dial = new(Modes, ModeAnchors);
    private readonly CancellationTokenSource cancellation = new();
    private CachedText timerLabel;
    private int modeIndex = PhotoModeIndex;
    private float flashAge = FlashDuration + 1f;
    private float reticleAge = ReticleDuration + 1f;
    private Vector2 reticlePosition;
    private IDalamudTextureWrap? wellTexture;
    private IDalamudTextureWrap? pendingWellTexture;
    private IDalamudTextureWrap? flightTexture;
    private string lastShotPath = string.Empty;
    private int wellGeneration;
    private Spring flight;
    private bool flying;
    private Rect flightFrom;
    private Rect wellRect;
    private double timerEndsAt;
    private int timerShown;
    private int captureCountdown;
    private int captureWatchdogTicks;
    private Rect pendingCaptureRect;
    private bool captureHooksAttached;
    private float requestedShotDelay;

    public CameraApp(PhotoCaptureService capture, PhotoLibrary library, Configuration configuration,
        GameUiVisibility gameUiVisibility, CameraShutter shutter)
    {
        this.capture = capture;
        this.library = library;
        this.configuration = configuration;
        this.gameUiVisibility = gameUiVisibility;
        this.shutter = shutter;
    }

    private bool TimerRunning => timerEndsAt > 0d;

    public void OnOpened()
    {
        flashAge = FlashDuration + 1f;
        reticleAge = ReticleDuration + 1f;
        timerEndsAt = 0d;
        flying = false;
        modeIndex = PhotoModeIndex;
        dial.Snap(modeIndex);
        SyncLandscape();
        DetachCaptureHooks();
        _ = LoadNewestAsync(++wellGeneration);
    }

    public void OnClosed()
    {
        timerEndsAt = 0d;
        requestedShotDelay = 0f;
        AppLandscape.Release(Id);
        DetachCaptureHooks();
    }

    private void SyncLandscape()
    {
        if (configuration.CameraLandscape)
        {
            AppLandscape.Request(Id);
            return;
        }

        AppLandscape.Release(Id);
    }

    public void Draw(in PhoneContext context)
    {
        var scale = UiScale.Current;
        var theme = context.Theme;
        var rounding = theme.ScreenRounding * scale;
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, MaxFrameSeconds);
        WallpaperBackdrop.RecordFlat(CameraChrome.Dark);
        AdvanceTimers(delta);
        var screen = SceneChrome.ScreenFrom(context.Content, theme, scale);
        var landscape = screen.IsLandscape();
        var viewfinder = ViewfinderRect(screen, scale);
        var captureRect = CaptureRect(viewfinder);
        var drawList = ImGui.GetWindowDrawList();
        UiAnchors.Report("camera.viewfinder", viewfinder);
        HandleFocusTap(viewfinder);
        CameraChrome.Viewfinder(drawList, viewfinder, captureRect, configuration.CameraGrid, scale);
        CameraChrome.Reticle(drawList, reticleAge, ReticleDuration, reticlePosition, scale);
        DrawCountdown(drawList, captureRect, scale);
        if (landscape)
        {
            DrawSideBar(drawList, screen, rounding, scale);
            DrawSideTray(drawList, screen, captureRect, context.Navigation, rounding, scale);
        }
        else
        {
            DrawTopBar(drawList, screen, rounding, scale);
            DrawTray(drawList, screen, captureRect, context.Navigation, rounding, scale);
        }

        DrawFlight(drawList, delta, scale);
        CameraChrome.Flash(drawList, screen, flashAge, FlashDuration, rounding);
        TakeRequestedShot(captureRect, delta);
    }

    private void TakeRequestedShot(Rect captureRect, float delta)
    {
        if (shutter.TryConsume())
        {
            requestedShotDelay = RequestedShotSettleSeconds;
        }

        if (requestedShotDelay <= 0f)
        {
            return;
        }

        requestedShotDelay -= delta;
        if (requestedShotDelay > 0f || TimerRunning)
        {
            return;
        }

        requestedShotDelay = 0f;
        Shoot(captureRect);
    }

    private void DrawTopBar(ImDrawListPtr drawList, Rect screen, float rounding, float scale)
    {
        var barMax = new Vector2(screen.Max.X, screen.Min.Y + TopBarHeight * scale);
        CameraChrome.Band(drawList, screen.Min, barMax, rounding, ImDrawFlags.RoundCornersTop, false);
        var rowY = barMax.Y - ControlRowLift * scale;
        var left = screen.Min.X + ControlEdgeInset * scale;
        var right = screen.Max.X - ControlEdgeInset * scale;
        var pitch = ControlPitch * scale;
        FlashControl(drawList, new Vector2(left, rowY), scale);
        TimerControl(drawList, new Vector2(left + pitch, rowY), scale);
        GameUiControl(drawList, new Vector2(right - pitch, rowY), scale);
        RotateControl(drawList, new Vector2(right, rowY), scale);
    }

    private void DrawSideBar(ImDrawListPtr drawList, Rect screen, float rounding, float scale)
    {
        var barMax = new Vector2(screen.Min.X + SideBarWidth * scale, screen.Max.Y);
        CameraChrome.Band(drawList, screen.Min, barMax, rounding, ImDrawFlags.RoundCornersLeft, false);
        var columnX = screen.Min.X + SideBarWidth * 0.5f * scale;
        var top = screen.Min.Y + ColumnTop * scale;
        var pitch = ControlPitch * scale;
        FlashControl(drawList, new Vector2(columnX, top), scale);
        TimerControl(drawList, new Vector2(columnX, top + pitch), scale);
        GameUiControl(drawList, new Vector2(columnX, top + pitch * 2f), scale);
        RotateControl(drawList, new Vector2(columnX, top + pitch * 3f), scale);
    }

    private void DrawTray(ImDrawListPtr drawList, Rect screen, Rect captureRect, INavigator navigation,
        float rounding, float scale)
    {
        var trayTop = screen.Max.Y - TrayHeight * scale;
        CameraChrome.Band(drawList, new Vector2(screen.Min.X, trayTop), screen.Max, rounding,
            ImDrawFlags.RoundCornersBottom, true);
        SetMode(dial.DrawRow(drawList, screen, trayTop + DialDrop * scale, modeIndex, scale));
        var shutterCenter = new Vector2(screen.Center.X, trayTop + ShutterDrop * scale);
        DrawShutter(drawList, shutterCenter, captureRect, scale);
        DrawWell(drawList, new Vector2(screen.Min.X + TrayControlInset * scale, shutterCenter.Y), navigation, scale);
        GridControl(drawList, new Vector2(screen.Max.X - TrayControlInset * scale, shutterCenter.Y), scale);
    }

    private void DrawSideTray(ImDrawListPtr drawList, Rect screen, Rect captureRect, INavigator navigation,
        float rounding, float scale)
    {
        var trayLeft = screen.Max.X - SideTrayWidth * scale;
        CameraChrome.Band(drawList, new Vector2(trayLeft, screen.Min.Y), screen.Max, rounding,
            ImDrawFlags.RoundCornersRight, true);
        var centerX = trayLeft + SideTrayWidth * 0.5f * scale;
        var shutterCenter = new Vector2(centerX, screen.Center.Y);
        var shutterRadius = CameraChrome.ShutterRadius * scale;
        SetMode(dial.DrawColumn(drawList, centerX, screen.Min.Y + SideDialTop * scale,
            shutterCenter.Y - shutterRadius - SideDialGap * scale, modeIndex, scale));
        DrawShutter(drawList, shutterCenter, captureRect, scale);
        var lowerY = (shutterCenter.Y + shutterRadius + screen.Max.Y - SideWellBottom * scale) * 0.5f;
        DrawWell(drawList, new Vector2(trayLeft + SideTrayControlInset * scale, lowerY), navigation, scale);
        GridControl(drawList, new Vector2(screen.Max.X - SideTrayControlInset * scale, lowerY), scale);
    }

    private void SetMode(int mode)
    {
        if (TimerRunning || mode == modeIndex)
        {
            return;
        }

        modeIndex = mode;
        UiFeedback.Play(UiSound.Tap);
    }

    private void DrawShutter(ImDrawListPtr drawList, Vector2 center, Rect captureRect, float scale)
    {
        if (!CameraChrome.Shutter(drawList, center, TimerRunning, scale))
        {
            return;
        }

        if (TimerRunning)
        {
            timerEndsAt = 0d;
            UiFeedback.Play(UiSound.ToggleOff);
            return;
        }

        Shoot(captureRect);
    }

    private void DrawWell(ImDrawListPtr drawList, Vector2 center, INavigator navigation, float scale)
    {
        wellRect = CameraChrome.WellRect(center, scale);
        var shown = flying ? wellTexture : wellTexture ?? flightTexture;
        if (!CameraChrome.Well(drawList, wellRect, shown, Loc.T(L.Apps.Photos), scale))
        {
            return;
        }

        if (lastShotPath.Length > 0)
        {
            library.RequestOpen(lastShotPath);
        }

        navigation.Open("photos");
    }

    private void FlashControl(ImDrawListPtr drawList, Vector2 center, float scale)
    {
        var on = configuration.CameraFlash;
        var tooltip = Loc.T(on ? L.Camera.FlashOn : L.Camera.FlashOff);
        if (Control(drawList, "camera.flash", center, tooltip, false, scale, out var grow))
        {
            configuration.CameraFlash = !on;
            configuration.Save();
            UiFeedback.Play(on ? UiSound.ToggleOff : UiSound.ToggleOn);
        }

        CameraChrome.BoltGlyph(drawList, center, grow, configuration.CameraFlash, false, scale);
    }

    private void TimerControl(ImDrawListPtr drawList, Vector2 center, float scale)
    {
        var seconds = configuration.CameraTimerSeconds;
        var active = seconds > 0;
        var label = active ? TimerLabel(seconds) : string.Empty;
        var tooltip = Loc.T(L.Camera.SelfTimer);
        UiAnchors.Report("camera.timer", ControlRect(center, scale));
        if (Control(drawList, "camera.timer", center, tooltip, active, scale, out var grow) && !TimerRunning)
        {
            configuration.CameraTimerSeconds = NextTimer(seconds);
            configuration.Save();
            UiFeedback.Play(configuration.CameraTimerSeconds > 0 ? UiSound.ToggleOn : UiSound.ToggleOff);
        }

        CameraChrome.TimerGlyph(drawList, center, grow, label, scale);
    }

    private void GameUiControl(ImDrawListPtr drawList, Vector2 center, float scale)
    {
        var shown = configuration.CameraShowUi;
        UiAnchors.Report("camera.showUi", ControlRect(center, scale));
        var tooltip = Loc.T(shown ? L.Camera.HideGameUi : L.Camera.ShowGameUi);
        if (Control(drawList, "camera.showUi", center, tooltip, false, scale, out var grow))
        {
            configuration.CameraShowUi = !shown;
            configuration.Save();
            UiFeedback.Play(shown ? UiSound.ToggleOff : UiSound.ToggleOn);
        }

        CameraChrome.HudGlyph(drawList, center, grow, configuration.CameraShowUi, false, scale);
    }

    private void RotateControl(ImDrawListPtr drawList, Vector2 center, float scale)
    {
        var landscape = configuration.CameraLandscape;
        UiAnchors.Report("camera.rotate", ControlRect(center, scale));
        if (Control(drawList, "camera.rotate", center, Loc.T(L.Camera.Landscape), landscape, scale, out var grow))
        {
            configuration.CameraLandscape = !landscape;
            configuration.Save();
            SyncLandscape();
        }

        CameraChrome.RotateGlyph(drawList, center, grow, configuration.CameraLandscape, configuration.CameraLandscape,
            scale);
    }

    private void GridControl(ImDrawListPtr drawList, Vector2 center, float scale)
    {
        var on = configuration.CameraGrid;
        if (Control(drawList, "camera.grid", center, Loc.T(L.Camera.Grid), on, scale, out var grow))
        {
            configuration.CameraGrid = !on;
            configuration.Save();
            UiFeedback.Play(on ? UiSound.ToggleOff : UiSound.ToggleOn);
        }

        CameraChrome.GridGlyph(drawList, center, grow, configuration.CameraGrid, scale);
    }

    private static bool Control(ImDrawListPtr drawList, string id, Vector2 center, string tooltip, bool active,
        float scale, out float grow) =>
        GlassCircle.Draw(drawList, ImGui.GetID(id), center, CameraChrome.ControlRadius * scale, scale, GlassTone.Dark,
            tooltip, HoverLabelSide.Below, out grow, active ? CameraChrome.Yellow : null, tapSound: false);

    private static Rect ControlRect(Vector2 center, float scale)
    {
        var extent = new Vector2(CameraChrome.ControlRadius * scale, CameraChrome.ControlRadius * scale);
        return new Rect(center - extent, center + extent);
    }

    public static int NextTimer(int seconds) => seconds switch
    {
        0 => ShortTimerSeconds,
        ShortTimerSeconds => LongTimerSeconds,
        _ => 0,
    };

    private string TimerLabel(int seconds) =>
        timerLabel.IsCurrent(seconds)
            ? timerLabel.Value
            : timerLabel.Store(seconds, Loc.T(L.Camera.TimerSeconds, seconds.ToString(Loc.Culture)));

    private static string Digits(int value)
    {
        var clamped = Math.Clamp(value, 0, LongTimerSeconds);
        return CountdownDigits[clamped] ??= clamped.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private void DrawCountdown(ImDrawListPtr drawList, Rect captureRect, float scale)
    {
        if (!TimerRunning)
        {
            return;
        }

        var remaining = timerEndsAt - ImGui.GetTime();
        if (remaining <= 0d)
        {
            timerEndsAt = 0d;
            pendingCaptureRect = captureRect;
            StartCapture();
            return;
        }

        var shown = (int)Math.Ceiling(remaining);
        if (shown != timerShown)
        {
            timerShown = shown;
            UiFeedback.Play(UiSound.Tap);
        }

        CameraChrome.Countdown(drawList, captureRect.Center, Digits(shown), scale);
    }

    private void DrawFlight(ImDrawListPtr drawList, float delta, float scale)
    {
        if (!flying || flightTexture is not { } texture)
        {
            PromotePendingWell();
            return;
        }

        var progress = flight.Step(1f, FlightSmoothTime, delta);
        if (progress >= FlightLanded)
        {
            flying = false;
            PromotePendingWell();
            return;
        }

        CameraChrome.Flight(drawList, texture, flightFrom, wellRect, progress, scale);
    }

    private void PromotePendingWell()
    {
        if (flying || pendingWellTexture is not { } ready)
        {
            return;
        }

        pendingWellTexture = null;
        if (wellTexture is { } old)
        {
            DeferredDispose.Later(old);
        }

        wellTexture = ready;
        if (flightTexture is { } spent)
        {
            DeferredDispose.Later(spent);
            flightTexture = null;
        }
    }

    private void AdvanceTimers(float delta)
    {
        if (flashAge <= FlashDuration)
        {
            flashAge += delta;
        }

        if (reticleAge <= ReticleDuration)
        {
            reticleAge += delta;
        }

        if (captureCountdown <= 0)
        {
            return;
        }

        captureCountdown--;
        if (captureCountdown == 0)
        {
            CompleteCapture();
        }
    }

    private void Shoot(Rect captureRect)
    {
        if (captureCountdown > 0)
        {
            return;
        }

        pendingCaptureRect = captureRect;
        var seconds = configuration.CameraTimerSeconds;
        if (seconds > 0)
        {
            timerShown = 0;
            timerEndsAt = ImGui.GetTime() + seconds;
            return;
        }

        StartCapture();
    }

    private void StartCapture()
    {
        if (configuration.CameraFlash)
        {
            flashAge = 0f;
        }

        AttachCaptureHooks();
        captureCountdown = CaptureDelayFrames;
    }

    private void CompleteCapture()
    {
        try
        {
            if (!capture.TryCapture(pendingCaptureRect, out var pixels, out var width, out var height))
            {
                UiFeedback.Play(UiSound.Caution);
                ShellToast.Show(Loc.T(L.Camera.CaptureFailed));
                return;
            }

            var path = library.Save(pixels, width, height);
            if (path is null)
            {
                UiFeedback.Play(UiSound.Caution);
                ShellToast.Show(Loc.T(L.Camera.CaptureFailed));
                return;
            }

            if (PhotoPlaces.Stamp(configuration.PhotoPlaces, path, Plugin.ClientState.TerritoryType))
            {
                configuration.Save();
            }

            UiFeedback.Play(UiSound.Shutter);
            BeginFlight(pixels, width, height, path);
        }
        finally
        {
            DetachCaptureHooks();
        }
    }

    private void BeginFlight(byte[] pixels, int width, int height, string path)
    {
        if (flightTexture is { } previous)
        {
            DeferredDispose.Later(previous);
        }

        flightTexture = Plugin.TextureProvider.CreateFromRaw(RawImageSpecification.Rgba32(width, height), pixels,
            "Aetherphone.Camera.Flight");
        flightFrom = pendingCaptureRect;
        flight.SnapTo(0f);
        flying = true;
        lastShotPath = path;
        _ = BuildWellAsync(pixels, width, height, ++wellGeneration);
    }

    private async Task BuildWellAsync(byte[] pixels, int width, int height, int generation)
    {
        try
        {
            var small = await Task.Run(
                () => PhotoPixels.Downsample(pixels, width, height, PhotoLibrary.ThumbnailMaxDimension),
                cancellation.Token).ConfigureAwait(false);
            if (small.Width == 0)
            {
                return;
            }

            var wrap = Plugin.TextureProvider.CreateFromRaw(RawImageSpecification.Rgba32(small.Width, small.Height),
                small.Pixels, "Aetherphone.Camera.Well");
            await Plugin.Framework.RunOnFrameworkThread(() => AcceptWell(wrap, generation, string.Empty))
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[Camera] could not build the last shot preview");
        }
    }

    private async Task LoadNewestAsync(int generation)
    {
        try
        {
            var token = cancellation.Token;
            var newest = await Task.Run(NewestPath, token).ConfigureAwait(false);
            if (newest.Length == 0)
            {
                await Plugin.Framework.RunOnFrameworkThread(() => ClearWell(generation)).ConfigureAwait(false);
                return;
            }

            var bytes = await library.ThumbnailBytesAsync(newest, token).ConfigureAwait(false);
            var wrap = await ImageProcessor.DecodeToTextureAsync(Plugin.TextureProvider, bytes, "camera:" + newest,
                ImageProcessor.MaxDecodePixels, token).ConfigureAwait(false);
            await Plugin.Framework.RunOnFrameworkThread(() => AcceptWell(wrap, generation, newest))
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[Camera] could not load the newest photo");
        }
    }

    private string NewestPath()
    {
        var paths = library.List();
        return paths.Length > 0 ? paths[0] : string.Empty;
    }

    private void ClearWell(int generation)
    {
        if (generation != wellGeneration || cancellation.IsCancellationRequested)
        {
            return;
        }

        lastShotPath = string.Empty;
        if (pendingWellTexture is { } pending)
        {
            pending.Dispose();
            pendingWellTexture = null;
        }

        if (wellTexture is { } old)
        {
            DeferredDispose.Later(old);
            wellTexture = null;
        }
    }

    private void AcceptWell(IDalamudTextureWrap wrap, int generation, string path)
    {
        if (generation != wellGeneration || cancellation.IsCancellationRequested)
        {
            wrap.Dispose();
            return;
        }

        if (path.Length > 0)
        {
            lastShotPath = path;
        }

        if (pendingWellTexture is { } stale)
        {
            stale.Dispose();
        }

        pendingWellTexture = wrap;
    }

    private void AttachCaptureHooks()
    {
        captureWatchdogTicks = CaptureWatchdogTicks;
        if (captureHooksAttached)
        {
            return;
        }

        Plugin.Framework.Update += ReleaseStalledCapture;
        captureHooksAttached = true;
        if (!configuration.CameraShowUi)
        {
            gameUiVisibility.Hide();
        }
    }

    private void ReleaseStalledCapture(IFramework framework)
    {
        captureWatchdogTicks--;
        if (captureWatchdogTicks > 0)
        {
            return;
        }

        DetachCaptureHooks();
    }

    private void DetachCaptureHooks()
    {
        captureCountdown = 0;
        if (!captureHooksAttached)
        {
            return;
        }

        Plugin.Framework.Update -= ReleaseStalledCapture;
        captureHooksAttached = false;
        gameUiVisibility.Restore();
    }

    private void HandleFocusTap(Rect viewfinder)
    {
        var hovered = UiInteract.Hover(viewfinder.Min, viewfinder.Max);
        if (!UiInteract.Click(viewfinder.Min, viewfinder.Max, hovered))
        {
            return;
        }

        reticlePosition = ImGui.GetMousePos();
        reticleAge = 0f;
    }

    private Rect CaptureRect(Rect viewfinder)
    {
        if (modeIndex != SquareModeIndex)
        {
            return viewfinder;
        }

        var side = MathF.Min(viewfinder.Width, viewfinder.Height);
        var center = viewfinder.Center;
        var half = new Vector2(side * 0.5f, side * 0.5f);
        return new Rect(center - half, center + half);
    }

    public void Dispose()
    {
        cancellation.Cancel();
        DetachCaptureHooks();
        wellTexture?.Dispose();
        pendingWellTexture?.Dispose();
        flightTexture?.Dispose();
        wellTexture = null;
        pendingWellTexture = null;
        flightTexture = null;
        cancellation.Dispose();
    }
}
