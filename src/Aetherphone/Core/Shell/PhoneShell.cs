using System.Numerics;
using Aetherphone.Core.Analytics;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Messaging;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Playback;
using Aetherphone.Core.Shell.Home;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Theme;
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

    private readonly ThemeProvider themes;
    private readonly IReadOnlyList<IPhoneApp> apps;
    private readonly WidgetRegistry widgets;
    private readonly NavigationStack navigation;
    private readonly NotificationBanner banner;
    private readonly DynamicIsland island;
    private readonly ControlCenter controlCenter;
    private readonly MinimizedPhone minimizedView;
    private readonly MinimizeTransition minimize = new();
    private readonly NotificationService notifications;
    private readonly HomeScreen home;
    private readonly SideButton sideButton = new();
    private readonly CallHub calls;
    private readonly IncomingCallOverlay incomingOverlay;
    private readonly ConfirmOverlay confirmOverlay;
    private readonly OnboardingDirector director;
    private NotificationShake shake = new(ShakeDuration, ShakeFrequency, ShakeAmplitude);
    private bool closeRequested;
    private bool analyticsConsentRequested;
    private bool indicatorPressActive;
    private Vector2 indicatorPressPos;
    private string? zoomPreparedFor;
    private CallState lastCallState;

    public PhoneShell(ThemeProvider themes, AppBundle bundle, NotificationService notifications,
        PlaybackHub playback, CallHub calls, MessageLauncher messageLauncher, VelvetLauncher velvetLauncher,
        DmLauncher dmLauncher, SocialLauncher socialLauncher, ConfirmService confirm)
    {
        this.themes = themes;
        apps = bundle.Apps;
        widgets = bundle.Widgets;
        this.calls = calls;
        this.notifications = notifications;
        navigation = new NavigationStack(apps);
        director = new OnboardingDirector(navigation);
        navigation.AppOpened += director.OnAppOpened;
        var router = new NotificationRouter(navigation, notifications, messageLauncher, velvetLauncher, dmLauncher, socialLauncher);
        banner = new NotificationBanner(notifications, () => navigation.Current?.Id, router);
        banner.Shown += OnBannerShown;
        island = new DynamicIsland(playback, calls);
        controlCenter = new ControlCenter(themes, playback, calls, navigation, notifications, router);
        minimizedView = new MinimizedPhone(notifications);
        home = new HomeScreen(apps, bundle.Widgets);
        navigation.ReturningHome += home.PrepareReveal;
        incomingOverlay = new IncomingCallOverlay(calls);
        confirmOverlay = new ConfirmOverlay(confirm);
    }

    public void OnOpened()
    {
        if (minimize.Phase == MinimizePhase.None)
        {
            Plugin.Loading.BeginSession();
        }

        director.OnPhoneOpened();
    }

    private void MaybeAskAnalyticsConsent(bool busy)
    {
        if (analyticsConsentRequested || Plugin.Cfg.AnalyticsConsentPrompted)
        {
            return;
        }

        if (busy || director.CapturesPointer || !WelcomeOnboardingSettled())
        {
            return;
        }

        analyticsConsentRequested = true;
        Plugin.Confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Settings.ConsentTitle),
            Message = Loc.T(L.Settings.ConsentMessage),
            ConfirmLabel = Loc.T(L.Settings.ConsentAccept),
            CancelLabel = Loc.T(L.Settings.ConsentDecline),
            Danger = false,
            Confirm = () => SetAnalyticsConsent(true),
            Cancel = () => SetAnalyticsConsent(false),
        });
    }

    private static bool WelcomeOnboardingSettled()
    {
        if (!OnboardingState.Enabled)
        {
            return true;
        }

        var welcome = TourRegistry.GetWelcome();
        return OnboardingState.HasCompleted(welcome.Id, welcome.ContentVersion);
    }

    private static void SetAnalyticsConsent(bool enabled)
    {
        Plugin.Cfg.AnalyticsEnabled = enabled;
        Plugin.Cfg.AnalyticsConsentPrompted = true;
        Plugin.Cfg.Save();
    }

    public void OnClosed()
    {
        Plugin.Loading.Cancel();
        director.Suspend();
    }

    public void OpenApp(string appId, string source)
    {
        if (navigation.Current?.Id == appId)
        {
            return;
        }

        navigation.Open(appId, source);
    }

    public bool ConsumeCloseRequest()
    {
        var requested = closeRequested;
        closeRequested = false;
        return requested;
    }

    public bool MinimizedResting => minimize.MinimizedResting;

    public void ForceMaximize() => minimize.SnapFull();

    public void ForceMinimized() => minimize.SnapMinimized();

    private void OnBannerShown()
    {
        if (minimize.Phase == MinimizePhase.None && Plugin.Cfg.Vibration)
        {
            shake.Trigger();
        }
    }

    public void Draw(Rect device)
    {
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        minimize.Advance(delta);
        if (minimize.Phase != MinimizePhase.None)
        {
            if (Plugin.Loading.IsActive)
            {
                Plugin.Loading.Cancel();
            }

            if (minimize.MorphActive)
            {
                DrawMinimizeMorph(device, delta);
            }
            else
            {
                DrawMinimizedFace(device, delta);
            }

            HoverTooltip.Flush();
            return;
        }

        minimizedView.IsShowing = false;
        device = device.Translate(new Vector2(shake.Advance(delta), 0f));
        Plugin.Wallpapers.StepDayNight(delta);
        var theme = themes.Chrome;
        var screen = DeviceChrome.ScreenRect(device, theme);
        DeviceChrome.DrawBody(device, theme, TransparentBand(screen));
        var loading = Plugin.Loading;
        loading.Advance(delta);
        navigation.Advance(delta);
        if (!navigation.IsTransitioning)
        {
            zoomPreparedFor = null;
        }

        banner.Advance(delta);
        calls.Advance(delta);
        if (!loading.IsActive)
        {
            switch (sideButton.Update(DeviceChrome.SideButtonRect(device), theme, delta))
            {
                case SideButtonAction.Minimize:
                    minimize.BeginCollapse();
                    if (Plugin.Cfg.LockPosition)
                    {
                        Plugin.Cfg.LockPosition = false;
                        Plugin.Cfg.Save();
                    }

                    break;
                case SideButtonAction.Close:
                    closeRequested = true;
                    break;
            }

            if (SideToggle.Update(DeviceChrome.MuteButtonRect(device), theme, Plugin.Cfg.DoNotDisturb,
                    Loc.T(Plugin.Cfg.DoNotDisturb ? L.Plugin.DndDisableHint : L.Plugin.DndEnableHint)))
            {
                Plugin.Cfg.DoNotDisturb = !Plugin.Cfg.DoNotDisturb;
                Plugin.Cfg.Save();
            }

            if (SideToggle.Update(DeviceChrome.LockButtonRect(device), theme, Plugin.Cfg.LockPosition,
                    Loc.T(Plugin.Cfg.LockPosition ? L.Plugin.UnlockPositionHint : L.Plugin.LockPositionHint)))
            {
                Plugin.Cfg.LockPosition = !Plugin.Cfg.LockPosition;
                Plugin.Cfg.Save();
            }
        }

        SyncCallNavigation();
        var confirming = !loading.IsActive && confirmOverlay.CapturesPointer;
        var overlaysCapture = !loading.IsActive && controlCenter.CapturesPointer;
        var ringing = !loading.IsActive && incomingOverlay.IsRinging;
        var islandCaptures = !loading.IsActive && !overlaysCapture && !ringing && !confirming &&
                             (island.CapturesPointer(screen) ||
                              (!director.CapturesPointer && banner.CapturesPointer(screen)));
        var busy = loading.IsActive || overlaysCapture || ringing || confirming || navigation.IsTransitioning;
        director.Advance(delta, busy, navigation.AtHome, navigation.Current?.Id);
        MaybeAskAnalyticsConsent(busy);
        UiAnchors.BeginFrame(director.WantsAnchors);
        UiAnchors.Report("chrome.lock", DeviceChrome.LockButtonRect(device));
        UiAnchors.Report("chrome.minimize", DeviceChrome.SideButtonRect(device));
        UiAnchors.Report("chrome.controlcenter",
            new Rect(screen.Min, new Vector2(screen.Max.X, screen.Min.Y + 44f * ImGuiHelpers.GlobalScale)));
        using (InputShield.Engage(loading.IsActive || islandCaptures || overlaysCapture || ringing || confirming ||
                                  director.CapturesPointer))
        {
            DrawContent(screen, theme);
            if (!navigation.AtHome || navigation.IsTransitioning)
            {
                DeviceChrome.MaskScreenCorners(screen, theme);
            }

            DrawChrome(screen, theme);
        }

        if (loading.IsActive)
        {
            loading.Draw(screen, theme);
            return;
        }

        if (!director.CapturesPointer)
        {
            banner.Draw(screen, theme);
            if (!controlCenter.IsActive)
            {
                island.Draw(screen, theme, navigation);
            }

            incomingOverlay.Draw(screen, theme);
        }

        controlCenter.Draw(screen, theme, delta,
            !navigation.IsTransitioning && !director.CapturesPointer && !islandCaptures);
        HoverTooltip.Flush();
        confirmOverlay.Draw(screen, theme);
        director.Draw(screen, theme);
        DeviceChrome.DrawBrightnessVeil(screen, theme, Plugin.Cfg.ScreenBrightness);
    }

    private bool TransparencyActive()
    {
        if (navigation.IsTransitioning)
        {
            return navigation.MotionOver.WantsTransparentScreen ||
                   (navigation.MotionUnder?.WantsTransparentScreen ?? false);
        }

        return !navigation.AtHome && (navigation.Current?.WantsTransparentScreen ?? false);
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
        if (state == CallState.Active && lastCallState != CallState.Active && navigation.Current?.Id != "phone")
        {
            navigation.Open("phone", AppOpenSource.System);
        }

        lastCallState = state;
    }

    private void DrawMinimizeMorph(Rect device, float delta)
    {
        minimizedView.IsShowing = false;
        var scale = ImGuiHelpers.GlobalScale;
        var theme = themes.Chrome;
        var startBody = DeviceChrome.BodyRect(device);
        var endBody = MinimizedRect(device, scale).Inset(scale);
        var eased = minimize.EasedProgress;
        var body = new Rect(Vector2.Lerp(startBody.Min, endBody.Min, eased),
            Vector2.Lerp(startBody.Max, endBody.Max, eased));
        var bezel = Easing.Lerp(theme.BezelThickness * scale, endBody.Width * 0.09f, eased);
        var rounding = Easing.Lerp(theme.DeviceRounding * scale, endBody.Width * 0.30f, eased);
        var geometry = MinimizedPhone.Geometry.Lerp(body, bezel, rounding);

        var shell = ImGui.GetWindowDrawList();
        Elevation.Floating(shell, geometry.Body.Min, geometry.Body.Max, geometry.Rounding, scale, eased);
        MinimizedPhone.DrawShell(shell, geometry, theme);
        RevealMorphContent(device, theme, geometry, eased);

        var raw = Math.Clamp((eased - 0.5f) / 0.4f, 0f, 1f);
        var glyphAlpha = raw * raw * (3f - 2f * raw);
        MinimizedPhone.DrawFace(ImGui.GetWindowDrawList(), geometry, theme, scale, glyphAlpha,
            notifications.UnreadCount);
    }

    private void RevealMorphContent(Rect device, PhoneTheme theme, in MinimizedPhone.Geometry geometry, float eased)
    {
        if (geometry.Screen.Height <= 0.5f)
        {
            return;
        }

        var fullScreen = DeviceChrome.ScreenRect(device, theme);
        SceneCompositor.DrawClipped(geometry.Screen, fullScreen, 0f, target => PaintCurrentScreen(target, theme));
        var veil = ImGui.GetColorU32(Palette.WithAlpha(theme.ScreenBase, eased));
        Squircle.Fill(ImGui.GetWindowDrawList(), geometry.Screen.Min, geometry.Screen.Max, geometry.ScreenRounding,
            veil);
    }

    private void PaintCurrentScreen(Rect screen, PhoneTheme theme)
    {
        if (navigation.AtHome)
        {
            PaintHome(screen, theme, HomeMotion.Rest);
            return;
        }

        using (ImRaii.PushId(navigation.Current!.Id))
        {
            PaintApp(screen, theme, navigation.Current!);
        }
    }

    private void DrawMinimizedFace(Rect device, float delta)
    {
        minimizedView.IsShowing = true;
        var mini = MinimizedRect(device, ImGuiHelpers.GlobalScale);
        switch (minimizedView.Draw(mini, themes.Chrome, delta))
        {
            case MinimizedAction.Expand:
                minimize.BeginExpand();
                break;
            case MinimizedAction.Close:
                closeRequested = true;
                break;
        }
    }

    private static Rect MinimizedRect(Rect device, float scale) =>
        new(device.Min, device.Min + MinimizeTransition.MinimizedSize * scale);

    private void DrawContent(Rect screen, PhoneTheme theme)
    {
        if (navigation.IsTransitioning)
        {
            DrawTransition(screen, theme);
        }
        else if (navigation.AtHome)
        {
            PaintHome(screen, theme, HomeMotion.Rest);
        }
        else
        {
            using (ImRaii.PushId(navigation.Current!.Id))
            {
                PaintApp(screen, theme, navigation.Current!);
            }
        }
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

    private void DrawTransition(Rect screen, PhoneTheme theme)
    {
        var cover = navigation.MotionProgress;
        var height = screen.Height;
        var over = navigation.MotionOver;
        var under = navigation.MotionUnder;
        if (under is null && !over.WantsTransparentScreen)
        {
            DrawZoomTransition(screen, theme, over);
            return;
        }

        var overOffset = new Vector2(0f, (1f - cover) * height);
        var underDim = cover * TransitionTiming.ShellDimMax;
        LayerPainter underPaint = under is null
            ? target => PaintHome(target, theme, new HomeMotion(1f, default, 0f, false))
            : target => PaintApp(target, theme, under);
        LayerPainter overPaint = target => PaintApp(target, theme, over);
        if (over.WantsTransparentScreen || (under?.WantsTransparentScreen ?? false))
        {
            var band = new Rect(screen.Min, new Vector2(screen.Max.X, screen.Min.Y + overOffset.Y));
            SceneCompositor.DrawClipped(band, screen, underDim, underPaint);
            SceneCompositor.DrawLayer(screen,
                new SceneCompositor.Layer(over.Id, overOffset, 0f, overPaint, default, true));
            return;
        }

        var underLayer =
            new SceneCompositor.Layer(under?.Id ?? "home", Vector2.Zero, underDim, underPaint, default, true);
        var overLayer = new SceneCompositor.Layer(over.Id, overOffset, 0f, overPaint, default, true);
        SceneCompositor.Composite(screen, underLayer, overLayer);
    }

    private void DrawZoomTransition(Rect screen, PhoneTheme theme, IPhoneApp over)
    {
        var raw = Math.Clamp(navigation.MotionProgress, 0f, 1f);
        var content = ContentRect(screen, theme);
        if (navigation.Motion == ShellMotion.Present && navigation.MotionOrigin is null &&
            !string.Equals(zoomPreparedFor, over.Id, StringComparison.Ordinal))
        {
            zoomPreparedFor = over.Id;
            home.PrepareReveal(over.Id);
        }

        var recede = Easing.SmoothStep(raw);
        var rest = navigation.MotionOrigin ?? home.RevealRect(over.Id, content) ?? CenterOrigin(content);
        var motion = new HomeMotion(1f + TransitionTiming.HomeZoomDepth * recede, rest.Center, recede, false);
        SceneCompositor.DrawLayer(screen, new SceneCompositor.Layer("home", Vector2.Zero,
            TransitionTiming.ShellDimMax * recede, target => PaintHome(target, theme, motion), default, true));
        var warped = motion.Warp(rest);
        var card = new Rect(Vector2.Lerp(warped.Min, screen.Min, raw), Vector2.Lerp(warped.Max, screen.Max, raw));
        if (card.Width < 4f || card.Height < 4f)
        {
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var iconRadius = MathF.Min(MathF.Min(rest.Width, rest.Height) * 0.26f, 24f * scale);
        var rounding = iconRadius + (theme.ScreenRounding * scale - iconRadius) * raw;
        var shellDrawList = ImGui.GetWindowDrawList();
        var elevation = Easing.Clamp01(raw * 2.4f);
        Elevation.Floating(shellDrawList, card.Min, card.Max, rounding, scale, elevation);
        DrawZoomCard(screen, theme, over, rest, card, rounding, raw);
        Material.EdgeSquircle(ImGui.GetWindowDrawList(), card.Min, card.Max, rounding, scale, elevation);
    }

    private void DrawZoomCard(Rect screen, PhoneTheme theme, IPhoneApp over, Rect rest, Rect card, float rounding,
        float raw)
    {
        const ImGuiWindowFlags cardFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                                           ImGuiWindowFlags.NoBackground;
        ImGui.SetCursorScreenPos(card.Min);
        using (ImRaii.PushId("zoomcard"))
        using (ImRaii.Child("card", card.Size, false, cardFlags))
        using (InputShield.Engage(true))
        {
            var reveal = Easing.SmootherStep(Easing.Segment(raw, 0.45f, 0.95f));
            if (raw > 0.35f)
            {
                var offset = (card.Center - screen.Center) * 0.9f;
                var rise = (1f - reveal) * 10f * ImGuiHelpers.GlobalScale;
                var target = new Rect(screen.Min + offset + new Vector2(0f, rise),
                    screen.Max + offset + new Vector2(0f, rise));
                using (ImRaii.PushId(over.Id))
                {
                    PaintApp(target, theme, over);
                }
            }

            var cardDrawList = ImGui.GetWindowDrawList();
            var veilAlpha = 1f - reveal;
            if (veilAlpha > 0.001f)
            {
                var surface = IconTile.Surface(over.Accent);
                var background = themes.Current.AppBackground;
                var settle = Easing.SmootherStep(Easing.Segment(raw, 0f, 0.5f));
                var veil = Vector4.Lerp(surface, background, settle);
                Squircle.Fill(cardDrawList, card.Min, card.Max, rounding,
                    ImGui.GetColorU32(veil with { W = veil.W * veilAlpha }));
            }

            var glyphAlpha = 1f - Easing.Segment(raw, 0.08f, 0.42f);
            if (glyphAlpha > 0.001f)
            {
                DrawZoomGlyph(cardDrawList, over, card, rest, raw, Easing.SmootherStep(glyphAlpha));
            }
        }
    }

    private static void DrawZoomGlyph(ImDrawListPtr drawList, IPhoneApp over, Rect card, Rect rest, float raw,
        float alpha)
    {
        var size = rest.Width * (1f + 0.4f * raw);
        var center = card.Center;
        var surface = IconTile.Surface(over.Accent);
        var ink = new Vector4(1f, 1f, 1f, alpha);
        if (!AppIconArt.TryDraw(drawList, over.Id, center, size, ink,
                Palette.WithAlpha(Palette.Darken(surface, 0.25f), alpha)))
        {
            var glyphHeight = Typography.Measure(over.Glyph).Y;
            var glyphScale = glyphHeight > 0f ? size * 0.5f / glyphHeight : 1f;
            Typography.DrawCentered(drawList, center, over.Glyph, ink, glyphScale, FontWeight.Regular);
        }
    }

    private static Rect CenterOrigin(Rect content)
    {
        var half = 30f * ImGuiHelpers.GlobalScale;
        return new Rect(content.Center - new Vector2(half, half), content.Center + new Vector2(half, half));
    }

    private void PaintHome(Rect screen, PhoneTheme theme, in HomeMotion motion)
    {
        DeviceChrome.DrawWallpaper(screen, theme, motion);
        DeviceChrome.DrawHomeScrim(screen, theme);
        home.Draw(screen, ContentRect(screen, theme), theme, navigation, motion);
    }

    private void PaintApp(Rect screen, PhoneTheme theme, IPhoneApp app)
    {
        var content = themes.Current;
        if (!app.WantsTransparentScreen)
        {
            DeviceChrome.FillScreen(screen, theme, content.AppBackground);
        }

        app.Draw(new PhoneContext(ContentRect(screen, theme), content, navigation));
    }

    private static Rect ContentRect(Rect screen, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var min = new Vector2(screen.Min.X + theme.SidePadding * scale, screen.Min.Y + theme.TopZoneHeight * scale);
        var max = new Vector2(screen.Max.X - theme.SidePadding * scale, screen.Max.Y - theme.BottomZoneHeight * scale);
        return new Rect(min, max);
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
