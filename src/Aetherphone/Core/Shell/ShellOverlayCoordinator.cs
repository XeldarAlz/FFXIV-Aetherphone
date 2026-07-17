using Aetherphone.Core.Apps;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;

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
    private readonly IncomingCallOverlay incomingOverlay;
    private readonly ConfirmOverlay confirmOverlay;
    private readonly ReportOverlay reportOverlay;
    private readonly OnboardingDirector director;
    private readonly SetupOverlay setup;

    public ShellOverlayCoordinator(Configuration configuration, LoadingScreen loading, NavigationStack navigation,
        ControlCenter controlCenter, NotificationBanner banner, DynamicIsland island,
        IncomingCallOverlay incomingOverlay, ConfirmOverlay confirmOverlay, ReportOverlay reportOverlay,
        OnboardingDirector director, SetupOverlay setup)
    {
        this.configuration = configuration;
        this.loading = loading;
        this.navigation = navigation;
        this.controlCenter = controlCenter;
        this.banner = banner;
        this.island = island;
        this.incomingOverlay = incomingOverlay;
        this.confirmOverlay = confirmOverlay;
        this.reportOverlay = reportOverlay;
        this.director = director;
        this.setup = setup;
    }

    public ShellOverlayState Assess(Rect screen)
    {
        var setupActive = setup.IsActive;
        var confirming = !loading.IsActive && (confirmOverlay.CapturesPointer || reportOverlay.CapturesPointer);
        var controlCenterCaptures = !loading.IsActive && controlCenter.CapturesPointer;
        var overlaysCapture = controlCenterCaptures && !director.WantsControlCenter;
        var ringing = !loading.IsActive && incomingOverlay.IsRinging;
        var islandCaptures = !loading.IsActive && !controlCenterCaptures && !ringing && !confirming &&
                             !setupActive &&
                             (island.CapturesPointer(screen) ||
                              (!director.CapturesPointer && banner.CapturesPointer(screen)));
        var busy = loading.IsActive || overlaysCapture || ringing || confirming || navigation.IsTransitioning ||
                   setupActive;
        var shieldBase = loading.IsActive || islandCaptures || controlCenterCaptures || ringing || confirming ||
                         setupActive;
        return new ShellOverlayState(setupActive, confirming, islandCaptures, busy, shieldBase);
    }

    public void DrawOverlays(Rect screen, PhoneTheme theme, float delta, in ShellOverlayState state)
    {
        if (state.SetupActive)
        {
            setup.Draw(screen, theme, delta, !loading.IsActive && !state.Confirming);
        }

        if (loading.IsActive)
        {
            loading.Draw(screen, theme);
            return;
        }

        if (state.SetupActive)
        {
            HoverTooltip.Flush();
            confirmOverlay.Draw(screen, theme);
            DeviceChrome.DrawBrightnessVeil(screen, theme, configuration.ScreenBrightness);
            return;
        }

        if (!director.CapturesPointer)
        {
            banner.Draw(screen, theme);
            if (!controlCenter.IsActive)
            {
                island.Draw(screen, theme, navigation, navigation.Current?.Id);
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

        controlCenter.Draw(screen, theme, delta,
            !navigation.IsTransitioning && !director.CapturesPointer && !state.IslandCaptures,
            !director.CapturesPointer);
        HoverTooltip.Flush();
        reportOverlay.Draw(screen, theme);
        confirmOverlay.Draw(screen, theme);
        director.Draw(screen, theme);
        DeviceChrome.DrawBrightnessVeil(screen, theme, configuration.ScreenBrightness);
    }
}
