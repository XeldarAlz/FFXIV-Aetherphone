using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Moderation;
using Aetherphone.Core.Muster;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Shell.Home;
using Aetherphone.Core.Social;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Wallpapers;
using Aetherphone.Core.YellowPages;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Core.Shell;

internal sealed class PhoneShell : IDisposable
{
    private const ImGuiWindowFlags ChromeFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                                                 ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoInputs;

    private const float ShakeDuration = 0.4f;
    private const float ShakeFrequency = 48f;
    private const float ShakeAmplitude = 3f;
    private static readonly TimeSpan ScreenVisibleGrace = TimeSpan.FromSeconds(0.5);

    private readonly Configuration configuration;
    private readonly LoadingScreen loading;
    private readonly WallpaperLibrary wallpapers;
    private readonly ThemeProvider themes;
    private readonly IReadOnlyList<IPhoneApp> apps;
    private readonly WidgetRegistry widgets;
    private readonly NavigationStack navigation;
    private readonly NotificationService notifications;
    private readonly NotificationBanner banner;
    private readonly ShortcutRunPill shortcutPill;
    private readonly CoinEarnPill coinPill;
    private readonly CoinEarnFloats coinFloats;
    private readonly MinimizedPhone minimizedPhone;
    private readonly MinimizeTransition minimize = new();
    private readonly OrientationTurn turn = new();
    private readonly SideButton sideButton = new();
    private readonly ResizeGrip resizeGrip = new();
    private readonly CallHub calls;
    private readonly OnboardingDirector director;
    private readonly SetupOverlay setup;
    private readonly ShellScreenPainter painter;
    private readonly ShellTransitionRenderer transition;
    private readonly MinimizeMorphView morph;
    private readonly ShellOverlayCoordinator overlays;
    private readonly HomeScreen home;
    private readonly SuspensionGate suspensions;
    private readonly AethernetSession session;
    private readonly ConfirmService confirm;
    private readonly AppSwitcher appSwitcher;
    private NotificationShake shake = new(ShakeDuration, ShakeFrequency, ShakeAmplitude);
    private bool closeRequested;
    private readonly HomeIndicatorGesture indicatorGesture = new();
    private CallState lastCallState;
    private DateTime lastVisibleDrawUtc = DateTime.MinValue;

    public PhoneShell(PhoneServices services, AppBundle bundle)
    {
        configuration = services.Configuration;
        loading = services.Loading;
        wallpapers = services.Wallpapers;
        themes = services.Themes;
        apps = bundle.Apps;
        widgets = bundle.Widgets;
        calls = services.Calls;
        notifications = services.Notifications;
        session = services.AethernetSession;
        confirm = services.Confirm;
        suspensions = new SuspensionGate(services.AethernetSession);
        navigation = new NavigationStack(apps, services.Installer, suspensions);
        notifications.AppAvailability = navigation.IsAvailable;
        director = new OnboardingDirector(navigation);
        navigation.AppOpened += director.OnAppOpened;
        navigation.AppOpened += services.Conduct.NotifyAppOpened;
        var router = new NotificationRouter(navigation, notifications, services.SocialNotifications,
            services.LinkpearlLauncher, services.VelvetLauncher, services.DmLauncher, services.GramDmLauncher,
            services.SocialLauncher, services.MusterLauncher, services.YellowPagesLauncher,
            services.AnnouncementsLauncher, services.SafetyLauncher, services.EncryptionSetup, services.RadioLauncher,
            services.CasinoLauncher, services.AetherStreamLauncher, services.HuntsLauncher);
        MusterChatBridge.Bind(services.Musters, services.MusterLauncher, navigation);
        AdChatBridge.Bind(services.YellowPages, services.YellowPagesLauncher, navigation);
        banner = new NotificationBanner(notifications, VisibleAppId, PhoneVisible, router);
        notifications.Vibration += OnVibration;
        var island = new DynamicIsland(services.Playback, calls, configuration);
        var rateLimitPill = new RateLimitPill(services.Http, services.AethernetSession);
        shortcutPill = new ShortcutRunPill(services.ShortcutRunner);
        coinPill = new CoinEarnPill(services.Coins, configuration);
        coinFloats = new CoinEarnFloats(services.Coins);
        var controlCenter = new ControlCenter(configuration, themes, services.Playback, calls, navigation,
            notifications, router, services.Coins, services.AethernetSession);
        minimizedPhone = new MinimizedPhone(services, router, navigation, services.MinimizedLayout);
        var spotlightIndex = new Spotlight.SpotlightIndex(apps, services.Installer, bundle.Contacts,
            services.DmLauncher, services.ChatInbox, services.ChatLog, services.LinkpearlLauncher,
            services.MarketIndex, services.MarketLauncher, services.Shortcuts, services.ShortcutRunner,
            services.Maps, services.StratsManifest, services.Venues, themes, calls, configuration);
        home = new HomeScreen(apps, bundle.Widgets, services.Shortcuts, services.ShortcutRunner, configuration,
            services.Confirm, spotlightIndex);
        services.Installer.Bind(home.Layout);
        services.Shortcuts.Bind(home.Layout);
        navigation.ReturningHome += home.PrepareReveal;
        var incomingOverlay = new IncomingCallOverlay(calls);
        var banOverlay = new BanOverlay(services.AethernetSession);
        suspensions.Blocked += banOverlay.Present;
        var confirmOverlay = new ConfirmOverlay(services.Confirm);
        var reportOverlay = new ReportOverlay(services.Report);
        services.Share.Bind(apps, navigation);
        var shareSheet = new ShareSheet(services.Share);
        var conductOverlay = new ConductGateOverlay(services.Conduct);
        var encryptionHelpOverlay = new EncryptionHelpOverlay(services.EncryptionHelp);
        setup = new SetupOverlay(services.AethernetSession, services.Aethernet, services.GameData,
            services.RemoteImages, services.Lodestone, bundle.Photos, services.WallpaperImages, navigation,
            configuration, services.Confirm, themes);
        painter = new ShellScreenPainter(themes, navigation, home);
        appSwitcher = new AppSwitcher(navigation, painter);
        transition = new ShellTransitionRenderer(themes, navigation, home, painter);
        morph = new MinimizeMorphView(themes, minimize, minimizedPhone, painter, configuration);
        overlays = new ShellOverlayCoordinator(configuration, loading, navigation, controlCenter, appSwitcher, banner,
            island, rateLimitPill, shortcutPill, coinPill, coinFloats, incomingOverlay, banOverlay, confirmOverlay,
            reportOverlay, shareSheet, conductOverlay, encryptionHelpOverlay, director, setup);
    }

    public void OnOpened()
    {
        if (minimize.Phase == MinimizePhase.None)
        {
            loading.BeginSession();
        }

        director.OnPhoneOpened();
    }

    public void OnClosed()
    {
        loading.Cancel();
        director.Suspend();
        appSwitcher.CloseImmediate();
        SensitiveReveals.Clear();
        UiFeedback.Play(UiSound.Sleep);
    }

    public void OpenApp(string appId)
    {
        if (navigation.Current?.Id == appId)
        {
            return;
        }

        navigation.Open(appId);
    }

    public bool ConsumeCloseRequest()
    {
        var requested = closeRequested;
        closeRequested = false;
        return requested;
    }

    public bool MinimizedResting => minimize.MinimizedResting;

    private static bool BezelDoubleClicked(Rect device, in ChassisGeometry chassis)
    {
        if (!ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left) || ImGui.IsAnyItemHovered())
        {
            return false;
        }

        var mouse = ImGui.GetMousePos();
        var onDevice = mouse.X >= device.Min.X && mouse.X <= device.Max.X &&
                       mouse.Y >= device.Min.Y && mouse.Y <= device.Max.Y;
        var onScreen = mouse.X >= chassis.Screen.Min.X && mouse.X <= chassis.Screen.Max.X &&
                       mouse.Y >= chassis.Screen.Min.Y && mouse.Y <= chassis.Screen.Max.Y;
        return onDevice && !onScreen;
    }

    public bool HomeEditing => home.Editing && navigation.Current is null;

    public bool LandscapeActive => minimize.Phase == MinimizePhase.None && !navigation.IsTransitioning &&
                                   navigation.Current is { } landscapeApp && AppLandscape.Held(landscapeApp.Id);

    public MinimizePhase MinimizePhase => minimize.Phase;

    public float MinimizeEased => minimize.EasedProgress;

    public Vector2 MinimizedSize => minimizedPhone.Measure(UiScale.Global);

    public Vector2 MinimizedIdleSize => MinimizedPhone.IdleSize(UiScale.Global);

    public MinimizedDrag ConsumeMinimizedDrag() => minimizedPhone.ConsumeDrag();

    public void ForceMaximize() => minimize.SnapFull();

    public void ForceMinimized() => minimize.SnapMinimized();

    private void ApplyResize(float width, bool landscape)
    {
        if (landscape)
        {
            if (MathF.Abs(width - configuration.LandscapePhoneWidth) > 0.01f)
            {
                configuration.LandscapePhoneWidth = width;
            }

            return;
        }

        if (MathF.Abs(width - configuration.PhoneWidth) > 0.01f)
        {
            configuration.PhoneWidth = width;
        }
    }

    private void OnVibration(PhoneNotification notification)
    {
        if (minimize.Phase != MinimizePhase.None)
        {
            return;
        }

        if (!PhoneVisible())
        {
            return;
        }

        if (VisibleAppId() == notification.AppId)
        {
            return;
        }

        shake.Trigger();
    }

    private bool PhoneVisible() => DateTime.UtcNow - lastVisibleDrawUtc < ScreenVisibleGrace;

    private string? VisibleAppId()
    {
        if (!PhoneVisible())
        {
            return null;
        }

        return navigation.Current?.Id;
    }

    public void PrepareFrame(float delta)
    {
        minimize.Advance(delta);
        turn.Advance(delta, LandscapeActive, minimize.Phase == MinimizePhase.None);
    }

    public OrientationTurn Turn => turn;

    public void Draw(Rect device)
    {
        var delta = FrameClock.Delta;
        if (minimize.Phase != MinimizePhase.None)
        {
            if (loading.IsActive)
            {
                loading.Cancel();
            }

            if (morph.Draw(device, delta))
            {
                closeRequested = true;
            }

            HoverTooltip.Flush();
            ShellToast.Draw(device, themes.Chrome);
            return;
        }

        lastVisibleDrawUtc = DateTime.UtcNow;
        device = device.Translate(new Vector2(shake.Advance(delta), 0f));
        wallpapers.StepDayNight(delta);
        var theme = themes.Chrome;
        var chassis = DeviceChrome.Chassis(device, theme);
        var screen = chassis.Screen;
        var sideButtonRect = DeviceChrome.SideButtonRect(device, chassis, out var sideButtonSide);
        var muteButtonRect = DeviceChrome.MuteButtonRect(device, chassis, out var muteButtonSide);
        var lockButtonRect = DeviceChrome.LockButtonRect(device, chassis, out var lockButtonSide);
        DeviceChrome.DrawBody(chassis, theme, turn.Turning ? null : TransparentBand(screen));
        loading.Advance(delta);
        navigation.Advance(delta);
        appSwitcher.Advance(screen, delta);
        if (!navigation.IsTransitioning)
        {
            transition.ResetPrepared();
            if (navigation.Current is { } blockedApp && suspensions.Blocks(blockedApp.Id))
            {
                navigation.GoHome();
            }
        }

        banner.Advance(delta);
        InstallSourceNotice.Poll(session, confirm);
        calls.Advance(delta);
        if (!loading.IsActive)
        {
            switch (sideButton.Update(sideButtonRect, sideButtonSide, theme, delta))
            {
                case SideButtonAction.Minimize:
                    minimize.BeginCollapse();
                    break;
                case SideButtonAction.Close:
                    closeRequested = true;
                    break;
            }

            if (BezelDoubleClicked(device, chassis))
            {
                minimize.BeginCollapse();
            }

            if (SideToggle.Update(muteButtonRect, muteButtonSide, theme, configuration.DoNotDisturb,
                    Loc.T(configuration.DoNotDisturb ? L.Plugin.DndDisableHint : L.Plugin.DndEnableHint)))
            {
                configuration.DoNotDisturb = !configuration.DoNotDisturb;
                configuration.Save();
            }

            if (SideToggle.Update(lockButtonRect, lockButtonSide, theme, configuration.LockPosition,
                    Loc.T(configuration.LockPosition ? L.Plugin.UnlockPositionHint : L.Plugin.LockPositionHint)))
            {
                configuration.LockPosition = !configuration.LockPosition;
                configuration.Save();
            }

            var landscape = chassis.Body.IsLandscape();
            var gripWidth = landscape
                ? PhoneBounds.LandscapeWidth(configuration)
                : PhoneBounds.ClampWidth(configuration.PhoneWidth);
            var resized = resizeGrip.Update(chassis, gripWidth, landscape, delta);
            if (resized.Adjusting)
            {
                ApplyResize(resized.Width, landscape);
            }

            if (resized.Committed)
            {
                configuration.Save();
            }
        }

        SyncCallNavigation();
        var state = overlays.Assess(screen);
        director.Advance(delta, state.Busy, navigation.AtHome, navigation.Current?.Id);
        UiAnchors.BeginFrame(director.WantsAnchors);
        UiAnchors.Report("chrome.lock", lockButtonRect);
        UiAnchors.Report("chrome.minimize", sideButtonRect);
        UiAnchors.Report("chrome.controlcenter",
            new Rect(screen.Min, new Vector2(screen.Max.X, screen.Min.Y + 44f * UiScale.Current)));
        using (InputShield.Engage(state.ShieldBase || director.CapturesPointer))
        {
            DrawContent(chassis, theme);
            DrawChrome(chassis, theme);
        }

        overlays.DrawOverlays(chassis, theme, delta, state, !turn.Turning);
    }

    private Rect? TransparentBand(Rect screen)
    {
        if (appSwitcher.Overtakes)
        {
            return null;
        }

        var scale = UiScale.Current;
        if (navigation.IsTransitioning)
        {
            return navigation.MotionOver.TransparentViewport(screen, scale) ??
                   navigation.MotionUnder?.TransparentViewport(screen, scale);
        }

        if (navigation.AtHome)
        {
            return null;
        }

        return navigation.Current?.TransparentViewport(screen, scale);
    }

    private void SyncCallNavigation()
    {
        var state = calls.Snapshot().State;
        var engaged = state is CallState.Connecting or CallState.Active;
        var wasEngaged = lastCallState is CallState.Connecting or CallState.Active;
        if (engaged && !wasEngaged && navigation.Current?.Id != "message")
        {
            calls.RequestCallScreen();
            navigation.Open("message");
        }

        lastCallState = state;
    }

    private void DrawContent(in ChassisGeometry chassis, PhoneTheme theme)
    {
        if (appSwitcher.Overtakes)
        {
            appSwitcher.DrawStage(chassis.Screen, chassis.ScreenRadius, theme);
            return;
        }

        if (navigation.IsTransitioning)
        {
            transition.Draw(chassis.Screen, chassis.ScreenRadius, theme);
            return;
        }

        painter.PaintCurrent(chassis.Screen, chassis.ScreenRadius, theme, HomeMotion.Rest);
    }

    private void DrawChrome(in ChassisGeometry chassis, PhoneTheme theme)
    {
        var screen = chassis.Screen;
        ImGui.SetCursorScreenPos(screen.Min);
        using (ImRaii.Child("chrome", screen.Size, false, ChromeFlags))
        {
            DeviceChrome.MaskScreenCorners(ImGui.GetWindowDrawList(), chassis, theme, UiScale.Current);
            var ink = painter.SurfaceTheme(theme);
            StatusBar.Draw(screen, ink, screen.IsLandscape());
            DrawHomeIndicator(screen, ink);
            if (turn.Turning)
            {
                DeviceChrome.DrawBrightnessVeil(ImGui.GetWindowDrawList(), chassis, configuration.ScreenBrightness);
            }
        }
    }

    private void DrawHomeIndicator(Rect screen, PhoneTheme theme)
    {
        var scale = UiScale.Current;
        var bounds = HomeIndicator.Bounds(screen, scale);
        var min = bounds.Min;
        var max = bounds.Max;
        UiAnchors.Report("chrome.home", bounds);
        var hitMin = new Vector2(min.X - 24f * scale, min.Y - 16f * scale);
        var hitMax = new Vector2(max.X + 24f * scale, max.Y + 16f * scale);
        var hovered = UiInteract.Hover(hitMin, hitMax);
        var scrubUsable = navigation.CanGoHome;
        var holdUsable = !navigation.IsTransitioning && !LandscapeActive;
        var actionable = (scrubUsable || holdUsable) && (hovered || indicatorGesture.Pressed);
        HomeIndicator.Draw(ImGui.GetWindowDrawList(), bounds, theme, actionable);
        if (!scrubUsable && !holdUsable)
        {
            indicatorGesture.Cancel();
            return;
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var mouse = ImGui.GetMousePos();
        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            indicatorGesture.Press(mouse);
        }

        if (!indicatorGesture.Pressed)
        {
            return;
        }

        if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            if (scrubUsable)
            {
                TrackIndicatorDrag(mouse, screen, scale);
            }

            if (holdUsable && indicatorGesture.TrackHold(mouse, FrameClock.Delta, scale))
            {
                indicatorGesture.Cancel();
                appSwitcher.Open();
            }

            return;
        }

        var release = indicatorGesture.Release(screen.Height, scale);
        if (release.WasScrubbing)
        {
            navigation.ReleaseScrub(release.ToHome, release.CoverVelocity);
            return;
        }

        if (hovered && scrubUsable)
        {
            navigation.GoHome();
        }
    }

    private void TrackIndicatorDrag(Vector2 mouse, Rect screen, float scale)
    {
        var frame = indicatorGesture.Track(mouse, FrameClock.Delta, screen.Height, scale);
        if (!frame.Scrubbing || navigation.Scrub(frame.Cover, frame.Drift))
        {
            return;
        }

        indicatorGesture.Cancel();
        navigation.GoHome();
    }

    public void Dispose()
    {
        MusterChatBridge.Clear();
        AdChatBridge.Clear();
        notifications.Vibration -= OnVibration;
        banner.Dispose();
        shortcutPill.Dispose();
        coinPill.Dispose();
        coinFloats.Dispose();
        minimizedPhone.Dispose();
        setup.Dispose();
        navigation.Current?.OnClosed();
        for (var index = 0; index < apps.Count; index++)
        {
            apps[index].Dispose();
        }

        for (var index = 0; index < widgets.All.Count; index++)
        {
            widgets.All[index].Dispose();
        }
    }
}
