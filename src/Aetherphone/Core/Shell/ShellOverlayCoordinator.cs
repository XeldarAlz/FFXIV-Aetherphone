using Aetherphone.Core.Apps;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Core.Shell;

internal readonly struct ShellOverlayState
{
    public readonly bool SetupActive;
    public readonly bool Confirming;
    public readonly bool IslandCaptures;
    public readonly bool Busy;
    public readonly bool ShieldBase;

    public ShellOverlayState(bool setupActive, bool confirming, bool islandCaptures, bool busy, bool shieldBase)
    {
        SetupActive = setupActive;
        Confirming = confirming;
        IslandCaptures = islandCaptures;
        Busy = busy;
        ShieldBase = shieldBase;
    }
}

internal sealed class ShellOverlayCoordinator
{
    private readonly Configuration configuration;
    private readonly LoadingScreen loading;
    private readonly NavigationStack navigation;
    private readonly ControlCenter controlCenter;
    private readonly NotificationBanner banner;
    private readonly DynamicIsland island;
    private readonly RateLimitPill rateLimitPill;
    private readonly ShortcutRunPill shortcutPill;
    private readonly IncomingCallOverlay incomingOverlay;
    private readonly BanOverlay banOverlay;
    private readonly ConfirmOverlay confirmOverlay;
    private readonly ReportOverlay reportOverlay;
    private readonly ShareSheet shareSheet;
    private readonly ConductGateOverlay conductOverlay;
    private readonly OnboardingDirector director;
    private readonly SetupOverlay setup;

    public ShellOverlayCoordinator(Configuration configuration, LoadingScreen loading, NavigationStack navigation,
        ControlCenter controlCenter, NotificationBanner banner, DynamicIsland island, RateLimitPill rateLimitPill,
        ShortcutRunPill shortcutPill, IncomingCallOverlay incomingOverlay, BanOverlay banOverlay,
        ConfirmOverlay confirmOverlay, ReportOverlay reportOverlay, ShareSheet shareSheet,
        ConductGateOverlay conductOverlay, OnboardingDirector director, SetupOverlay setup)
    {
        this.configuration = configuration;
        this.loading = loading;
        this.navigation = navigation;
        this.controlCenter = controlCenter;
        this.banner = banner;
        this.island = island;
        this.rateLimitPill = rateLimitPill;
        this.shortcutPill = shortcutPill;
        this.incomingOverlay = incomingOverlay;
        this.banOverlay = banOverlay;
        this.confirmOverlay = confirmOverlay;
        this.reportOverlay = reportOverlay;
        this.shareSheet = shareSheet;
        this.conductOverlay = conductOverlay;
        this.director = director;
        this.setup = setup;
    }

    public ShellOverlayState Assess(Rect screen)
    {
        var banNotice = !loading.IsActive && banOverlay.IsActive;
        var conductActive = !loading.IsActive && !banNotice && conductOverlay.Captures;
        var setupActive = setup.IsActive;
        var confirming = !loading.IsActive &&
                         (confirmOverlay.CapturesPointer || reportOverlay.CapturesPointer ||
                          shareSheet.CapturesPointer);
        var controlCenterCaptures = !loading.IsActive && controlCenter.CapturesPointer;
        var overlaysCapture = controlCenterCaptures && !director.WantsControlCenter;
        var ringing = !loading.IsActive && incomingOverlay.IsRinging;
        var islandCaptures = !loading.IsActive && !controlCenterCaptures && !ringing && !confirming &&
                             !setupActive && !conductActive && !banNotice &&
                             (island.CapturesPointer(screen) ||
                              (!director.CapturesPointer &&
                               (banner.CapturesPointer(screen) || shortcutPill.CapturesPointer())));
        var busy = loading.IsActive || overlaysCapture || ringing || confirming || navigation.IsTransitioning ||
                   setupActive || banNotice || conductActive;
        var shieldBase = loading.IsActive || islandCaptures || controlCenterCaptures || ringing || confirming ||
                         setupActive || banNotice || conductActive;
        return new ShellOverlayState(setupActive, confirming, islandCaptures, busy, shieldBase);
    }

    private void HandleEscape()
    {
        if (banOverlay.IsActive || conductOverlay.Captures || setup.IsActive || director.CapturesPointer)
        {
            return;
        }

        if (!confirmOverlay.CapturesPointer && !reportOverlay.CapturesPointer && !shareSheet.CapturesPointer &&
            !controlCenter.IsActive)
        {
            return;
        }

        if (UiInteract.WindowFocused)
        {
            ImGui.SetNextFrameWantCaptureKeyboard(true);
        }

        if (!ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            return;
        }

        if (confirmOverlay.CapturesPointer)
        {
            confirmOverlay.CancelActive();
            return;
        }

        if (reportOverlay.CapturesPointer)
        {
            reportOverlay.Dismiss();
            return;
        }

        if (shareSheet.CapturesPointer)
        {
            shareSheet.Dismiss();
            return;
        }

        controlCenter.Dismiss();
    }

    public void DrawOverlays(in ChassisGeometry chassis, PhoneTheme theme, float delta, in ShellOverlayState state)
    {
        var screen = chassis.Screen;
        if (state.SetupActive)
        {
            setup.Draw(screen, theme, delta, !loading.IsActive && !state.Confirming && !banOverlay.IsActive);
        }

        if (loading.IsActive)
        {
            loading.Draw(screen, theme);
            DeviceChrome.SealScreen(chassis, theme, configuration.ScreenBrightness);
            return;
        }

        if (state.SetupActive)
        {
            HoverTooltip.Flush();
            CopyToast.Flush();
            banOverlay.Draw(screen, theme);
            confirmOverlay.Draw(screen, theme);
            DeviceChrome.SealScreen(chassis, theme, configuration.ScreenBrightness);
            return;
        }

        if (!director.CapturesPointer)
        {
            if (!controlCenter.IsActive)
            {
                banner.Draw(screen, theme);
                island.Draw(screen, theme, navigation, navigation.Current?.Id);
                shortcutPill.Draw(screen, theme, delta, banner.IsVisible);
                if (!banner.IsVisible && !shortcutPill.IsVisible)
                {
                    rateLimitPill.Draw(screen, theme, delta);
                }
            }

            incomingOverlay.Draw(screen, theme);
        }

        if (GuideIntents.Consume(TourRegistry.ControlCenterOpenIntent))
        {
            controlCenter.Open();
        }

        if (GuideIntents.Consume(TourRegistry.ControlCenterCloseIntent))
        {
            controlCenter.Dismiss();
        }

        HandleEscape();
        controlCenter.Draw(screen, theme, delta,
            !navigation.IsTransitioning && !director.CapturesPointer && !state.IslandCaptures &&
            !banOverlay.IsActive && navigation.Current?.Id != "camera",
            !director.CapturesPointer);
        HoverTooltip.Flush();
        CopyToast.Flush();
        shareSheet.Draw(screen, theme);
        reportOverlay.Draw(screen, theme);
        confirmOverlay.Draw(screen, theme);
        director.Draw(screen, theme);
        conductOverlay.Draw(screen, theme);
        banOverlay.Draw(screen, theme);
        DeviceChrome.SealScreen(chassis, theme, configuration.ScreenBrightness);
    }
}
