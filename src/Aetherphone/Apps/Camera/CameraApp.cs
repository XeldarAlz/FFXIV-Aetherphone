using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
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
    private const float FlashDuration = 0.42f;
    private const float ReticleDuration = 1.1f;
    private const float PressDuration = 0.18f;
    private const int SquareModeIndex = 0;
    private const int CaptureDelayFrames = 3;
    private const int CaptureWatchdogTicks = 30;
    private static readonly LocString[] Modes = { L.Camera.ModeSquare, L.Camera.ModePhoto };

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
    private int modeIndex = 1;


    private float shutterPress;
    private float flashAge = FlashDuration + 1f;
    private float reticleAge = ReticleDuration + 1f;
    private Vector2 reticlePos;
    private IDalamudTextureWrap? lastShot;
    private int captureCountdown;
    private int captureWatchdogTicks;
    private Rect pendingCaptureRect;
    private bool captureHooksAttached;

    public CameraApp(PhotoCaptureService capture, PhotoLibrary library, Configuration configuration,
        GameUiVisibility gameUiVisibility)
    {
        this.capture = capture;
        this.library = library;
        this.configuration = configuration;
        this.gameUiVisibility = gameUiVisibility;
    }

    public void OnOpened()
    {
        flashAge = FlashDuration + 1f;
        reticleAge = ReticleDuration + 1f;
        shutterPress = 0f;
        SyncLandscape();
        DetachCaptureHooks();
    }

    public void OnClosed()
    {
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
        AdvanceTimers(ImGui.GetIO().DeltaTime);
        var screen = ScreenFrom(context.Content, theme, scale);
        var landscape = screen.IsLandscape();
        var viewfinder = ViewfinderRect(screen, scale);
        var captureRect = CaptureRect(viewfinder);

        var barAction = landscape
            ? CameraChrome.SideBar(screen, SideBarWidth, configuration.CameraFlash, configuration.CameraShowUi,
                configuration.CameraLandscape, scale, rounding)
            : CameraChrome.TopBar(screen, TopBarHeight, configuration.CameraFlash, configuration.CameraShowUi,
                configuration.CameraLandscape, scale, rounding);
        var consumed = barAction != CameraBarAction.None;
        ApplyBarAction(barAction);

        CameraChrome.Viewfinder(viewfinder, captureRect, configuration.CameraGrid, reticleAge, ReticleDuration, reticlePos, scale);
        consumed |= landscape
            ? DrawSideTray(screen, captureRect, context.Navigation, scale, rounding)
            : DrawTray(screen, captureRect, context.Navigation, scale, rounding);
        HandleFocusTap(viewfinder, consumed);
        CameraChrome.Flash(screen, flashAge, FlashDuration, rounding);
    }

    private void ApplyBarAction(CameraBarAction action)
    {
        switch (action)
        {
            case CameraBarAction.ToggleFlash:
                configuration.CameraFlash = !configuration.CameraFlash;
                configuration.Save();
                break;
            case CameraBarAction.ToggleShowUi:
                configuration.CameraShowUi = !configuration.CameraShowUi;
                configuration.Save();
                break;
            case CameraBarAction.ToggleLandscape:
                configuration.CameraLandscape = !configuration.CameraLandscape;
                configuration.Save();
                SyncLandscape();
                break;
        }
    }

    private void AdvanceTimers(float delta)
    {
        if (shutterPress > 0f)
        {
            shutterPress = MathF.Max(0f, shutterPress - delta / PressDuration);
        }

        if (flashAge <= FlashDuration)
        {
            flashAge += delta;
        }

        if (reticleAge <= ReticleDuration)
        {
            reticleAge += delta;
        }

        if (captureCountdown > 0)
        {
            captureCountdown--;
            if (captureCountdown == 0)
            {
                CompleteCapture();
            }
        }
    }

    private bool DrawTray(Rect screen, Rect captureRect, INavigator navigation, float scale, float rounding)
    {
        var trayTop = screen.Max.Y - TrayHeight * scale;
        CameraChrome.TrayBackground(screen, trayTop, rounding);

        var newMode = CameraChrome.ModeCarousel(screen, trayTop + 22f * scale, Modes, modeIndex, scale);
        var consumed = newMode != modeIndex;
        modeIndex = newMode;

        var shutterCenter = new Vector2(screen.Center.X, trayTop + 92f * scale);
        if (CameraChrome.Shutter(shutterCenter, shutterPress, scale))
        {
            Shoot(captureRect);
            consumed = true;
        }

        if (CameraChrome.ThumbnailWell(new Vector2(screen.Min.X + 44f * scale, shutterCenter.Y), lastShot, scale))
        {
            navigation.Open("photos");
            consumed = true;
        }

        if (CameraChrome.GridToggle(new Vector2(screen.Max.X - 44f * scale, shutterCenter.Y), configuration.CameraGrid, scale))
        {
            configuration.CameraGrid = !configuration.CameraGrid;
            configuration.Save();
            consumed = true;
        }

        return consumed;
    }

    private bool DrawSideTray(Rect screen, Rect captureRect, INavigator navigation, float scale, float rounding)
    {
        var trayLeft = screen.Max.X - SideTrayWidth * scale;
        CameraChrome.SideTrayBackground(screen, trayLeft, rounding);

        var trayCenterX = trayLeft + SideTrayWidth * 0.5f * scale;
        var shutterCenter = new Vector2(trayCenterX, screen.Center.Y);
        var shutterRadius = CameraChrome.ShutterRadius * scale;
        var newMode = CameraChrome.ModeColumn(trayCenterX, screen.Min.Y + 44f * scale,
            shutterCenter.Y - shutterRadius - 10f * scale, Modes, modeIndex, scale);
        var consumed = newMode != modeIndex;
        modeIndex = newMode;

        if (CameraChrome.Shutter(shutterCenter, shutterPress, scale))
        {
            Shoot(captureRect);
            consumed = true;
        }

        var wellCenterY = (shutterCenter.Y + shutterRadius + screen.Max.Y - 30f * scale) * 0.5f;
        if (CameraChrome.ThumbnailWell(new Vector2(trayLeft + 52f * scale, wellCenterY), lastShot, scale))
        {
            navigation.Open("photos");
            consumed = true;
        }

        if (CameraChrome.GridToggle(new Vector2(screen.Max.X - 52f * scale, wellCenterY), configuration.CameraGrid, scale))
        {
            configuration.CameraGrid = !configuration.CameraGrid;
            configuration.Save();
            consumed = true;
        }

        return consumed;
    }

    private void Shoot(Rect captureRect)
    {
        if (captureCountdown > 0)
        {
            return;
        }

        shutterPress = 1f;
        if (configuration.CameraFlash)
        {
            flashAge = 0f;
        }

        pendingCaptureRect = captureRect;
        AttachCaptureHooks();
        captureCountdown = CaptureDelayFrames;
    }

    private void CompleteCapture()
    {
        try
        {
            if (!capture.TryCapture(pendingCaptureRect, out var pixels, out var width, out var height))
            {
                return;
            }

            lastShot?.Dispose();
            lastShot = Plugin.TextureProvider.CreateFromRaw(RawImageSpecification.Rgba32(width, height), pixels,
                "Aetherphone.Photo.Last");
            library.Save(pixels, width, height);
            UiFeedback.Play(UiSound.Shutter);
        }
        finally
        {
            DetachCaptureHooks();
        }
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

    private void HandleFocusTap(Rect viewfinder, bool consumed)
    {
        if (consumed || !ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            return;
        }

        var mouse = ImGui.GetMousePos();
        if (!UiInteract.Hover(viewfinder.Min, viewfinder.Max))
        {
            return;
        }

        reticlePos = mouse;
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

    private static Rect ScreenFrom(Rect content, PhoneTheme theme, float scale)
    {
        var min = new Vector2(content.Min.X - theme.SidePadding * scale, content.Min.Y - theme.TopZoneHeight * scale);
        var max = new Vector2(content.Max.X + theme.SidePadding * scale,
            content.Max.Y + theme.BottomZoneHeight * scale);
        return new Rect(min, max);
    }

    public void Dispose()
    {
        DetachCaptureHooks();
        lastShot?.Dispose();
        lastShot = null;
    }
}
