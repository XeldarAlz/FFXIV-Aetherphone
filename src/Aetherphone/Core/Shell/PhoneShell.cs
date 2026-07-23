using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Shell.Home;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Wallpapers;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Core.Shell;

internal sealed class PhoneShell : IDisposable
{
    private const ImGuiWindowFlags ChromeFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                                                 ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoInputs;

    private const float IndicatorSwipeDistance = 26f;
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
    private readonly NotificationBanner banner;
    private readonly MinimizedPhone minimizedView;
    private readonly MinimizeTransition minimize = new();
    private readonly SideButton sideButton = new();
    private readonly CallHub calls;
    private readonly OnboardingDirector director;
    private readonly SetupOverlay setup;
    private readonly ShellScreenPainter painter;
    private readonly ShellTransitionRenderer transition;
    private readonly MinimizeMorphView morph;
    private readonly ShellOverlayCoordinator overlays;
    private readonly HomeScreen home;
    private NotificationShake shake = new(ShakeDuration, ShakeFrequency, ShakeAmplitude);
    private bool closeRequested;
    private bool indicatorPressActive;
    private Vector2 indicatorPressPos;
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
        var notifications = services.Notifications;
        navigation = new NavigationStack(apps);
        notifications.AppAvailability = navigation.IsAvailable;
        director = new OnboardingDirector(navigation);
        navigation.AppOpened += director.OnAppOpened;
        navigation.AppOpened += services.Conduct.NotifyAppOpened;
        var router = new NotificationRouter(navigation, notifications, services.LinkpearlLauncher,
            services.VelvetLauncher, services.DmLauncher, services.SocialLauncher);
        banner = new NotificationBanner(notifications, VisibleAppId, router);
        banner.Shown += OnBannerShown;
        var island = new DynamicIsland(services.Playback, calls);
        var controlCenter = new ControlCenter(configuration, themes, services.Playback, calls, navigation,
            notifications, router);
        minimizedView = new MinimizedPhone(notifications, configuration);
        home = new HomeScreen(apps, bundle.Widgets, configuration);
        navigation.ReturningHome += home.PrepareReveal;
        var incomingOverlay = new IncomingCallOverlay(calls);
        var banOverlay = new BanOverlay(services.AethernetSession);
        var confirmOverlay = new ConfirmOverlay(services.Confirm);
        var reportOverlay = new ReportOverlay(services.Report);
        var conductOverlay = new ConductGateOverlay(services.Conduct);
        setup = new SetupOverlay(services.AethernetSession, services.Aethernet, services.GameData,
            services.RemoteImages, services.Lodestone, bundle.Photos, services.WallpaperImages, navigation,
            configuration, services.Confirm);
        painter = new ShellScreenPainter(themes, navigation, home);
        transition = new ShellTransitionRenderer(themes, navigation, home, painter);
        morph = new MinimizeMorphView(themes, minimize, minimizedView, notifications, painter);
        overlays = new ShellOverlayCoordinator(configuration, loading, navigation, controlCenter, banner, island,
            incomingOverlay, banOverlay, confirmOverlay, reportOverlay, conductOverlay, director, setup);
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

    public bool HomeEditing => home.Editing;

    public MinimizePhase MinimizePhase => minimize.Phase;

    public float MinimizeEased => minimize.EasedProgress;

    public void ForceMaximize() => minimize.SnapFull();

    public void ForceMinimized() => minimize.SnapMinimized();

    private void OnBannerShown()
    {
        if (minimize.Phase == MinimizePhase.None && configuration.Vibration)
        {
            shake.Trigger();
        }
    }

    private string? VisibleAppId()
    {
        if (DateTime.UtcNow - lastVisibleDrawUtc >= ScreenVisibleGrace)
        {
            return null;
        }

        return navigation.Current?.Id;
    }

    public void Draw(Rect device)
    {
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        minimize.Advance(delta);
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
            return;
        }

        minimizedView.IsShowing = false;
        lastVisibleDrawUtc = DateTime.UtcNow;
        device = device.Translate(new Vector2(shake.Advance(delta), 0f));
        wallpapers.StepDayNight(delta);
        var theme = themes.Chrome;
        var screen = DeviceChrome.ScreenRect(device, theme);
        DeviceChrome.DrawBody(device, theme, TransparentBand(screen));
        loading.Advance(delta);
        navigation.Advance(delta);
        if (!navigation.IsTransitioning)
        {
            transition.ResetPrepared();
        }

        banner.Advance(delta);
        calls.Advance(delta);
        if (!loading.IsActive)
        {
            switch (sideButton.Update(DeviceChrome.SideButtonRect(device), theme, delta))
            {
                case SideButtonAction.Minimize:
                    minimize.BeginCollapse();
                    break;
                case SideButtonAction.Close:
                    closeRequested = true;
                    break;
            }

            if (SideToggle.Update(DeviceChrome.MuteButtonRect(device), theme, configuration.DoNotDisturb,
                    Loc.T(configuration.DoNotDisturb ? L.Plugin.DndDisableHint : L.Plugin.DndEnableHint)))
            {
                configuration.DoNotDisturb = !configuration.DoNotDisturb;
                configuration.Save();
            }

            if (SideToggle.Update(DeviceChrome.LockButtonRect(device), theme, configuration.LockPosition,
                    Loc.T(configuration.LockPosition ? L.Plugin.UnlockPositionHint : L.Plugin.LockPositionHint)))
            {
                configuration.LockPosition = !configuration.LockPosition;
                configuration.Save();
            }
        }

        SyncCallNavigation();
        var state = overlays.Assess(screen);
        director.Advance(delta, state.Busy, navigation.AtHome, navigation.Current?.Id);
        UiAnchors.BeginFrame(director.WantsAnchors);
        UiAnchors.Report("chrome.lock", DeviceChrome.LockButtonRect(device));
        UiAnchors.Report("chrome.minimize", DeviceChrome.SideButtonRect(device));
        UiAnchors.Report("chrome.controlcenter",
            new Rect(screen.Min, new Vector2(screen.Max.X, screen.Min.Y + 44f * ImGuiHelpers.GlobalScale)));
        using (InputShield.Engage(state.ShieldBase || director.CapturesPointer))
        {
            DrawContent(screen, theme);
            if (!navigation.AtHome || navigation.IsTransitioning)
            {
                DeviceChrome.MaskScreenCorners(screen, theme);
            }

            DrawChrome(screen, theme);
        }

        overlays.DrawOverlays(screen, theme, delta, state);
    }

    private Rect? TransparentBand(Rect screen)
    {
        var scale = ImGuiHelpers.GlobalScale;
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

    private void DrawContent(Rect screen, PhoneTheme theme)
    {
        if (navigation.IsTransitioning)
        {
            transition.Draw(screen, theme);
            return;
        }

        painter.PaintCurrent(screen, theme, HomeMotion.Rest);
    }

    private void DrawChrome(Rect screen, PhoneTheme theme)
    {
        ImGui.SetCursorScreenPos(screen.Min);
        using (ImRaii.Child("chrome", screen.Size, false, ChromeFlags))
        {
            StatusBar.Draw(screen, theme);
            DrawHomeIndicator(screen, theme);
        }
    }

    private void DrawHomeIndicator(Rect screen, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var width = 112f * scale;
        var height = 5f * scale;
        var center = new Vector2(screen.Center.X, screen.Max.Y - 14f * scale);
        var min = new Vector2(center.X - width * 0.5f, center.Y - height * 0.5f);
        var max = new Vector2(center.X + width * 0.5f, center.Y + height * 0.5f);
        UiAnchors.Report("chrome.home", new Rect(min, max));
        var hitMin = new Vector2(min.X - 24f * scale, min.Y - 16f * scale);
        var hitMax = new Vector2(max.X + 24f * scale, max.Y + 16f * scale);
        var hovered = ImGui.IsMouseHoveringRect(hitMin, hitMax);
        var usable = !navigation.AtHome && !navigation.IsTransitioning;
        var actionable = usable && (hovered || indicatorPressActive);
        var color = actionable ? theme.TextStrong : Palette.WithAlpha(theme.TextStrong, 0.55f);
        ImGui.GetWindowDrawList().AddRectFilled(min, max, ImGui.GetColorU32(color), height * 0.5f);
        if (!usable)
        {
            indicatorPressActive = false;
            return;
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var mouse = ImGui.GetMousePos();
        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            indicatorPressActive = true;
            indicatorPressPos = mouse;
        }

        if (!indicatorPressActive)
        {
            return;
        }

        if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            if (indicatorPressPos.Y - mouse.Y > IndicatorSwipeDistance * scale)
            {
                indicatorPressActive = false;
                navigation.GoHome();
            }

            return;
        }

        indicatorPressActive = false;
        if (hovered && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            navigation.GoHome();
        }
    }

    public void Dispose()
    {
        banner.Shown -= OnBannerShown;
        banner.Dispose();
        minimizedView.Dispose();
        setup.Dispose();
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
