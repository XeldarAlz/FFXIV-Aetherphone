using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Clock;

internal sealed partial class ClockApp : IResumableApp, ITabRouteTarget
{
    private const string WorldRoute = "clock.tab.world";
    private const string AlarmsRoute = "clock.tab.alarms";
    private const string StopwatchRoute = "clock.tab.stopwatch";
    private const string TimerRoute = "clock.tab.timer";
    private const int TabCount = 4;

    private enum ClockScreen : byte
    {
        Root,
        EditAlarm,
        AddCity,
    }

    private enum ClockTab : byte
    {
        World,
        Alarms,
        Stopwatch,
        Timer,
    }

    public string Id => "clock";
    public string DisplayName => Loc.T(L.Apps.Clock);
    public string Glyph => "T";
    public Vector4 Accent => AppAccents.For(Id);
    public int BadgeCount => 0;

    private readonly Configuration configuration;
    private readonly ConfirmService confirm;
    private readonly AppSkin ui = new(AppPalettes.Clock);
    private readonly ViewRouter<ClockScreen> router;
    private readonly RouterDraw<ClockScreen> drawView;
    private readonly Action back;
    private readonly TabBar tabBar = new();
    private readonly TabItem[] tabItems = new TabItem[TabCount];
    private readonly NavBarButton[] navButtons = new NavBarButton[2];
    private PendingTab pendingTab;
    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private ClockTab activeTab;

    public ClockApp(Configuration configuration, ConfirmService confirm)
    {
        this.configuration = configuration;
        this.confirm = confirm;
        router = new ViewRouter<ClockScreen>(ClockScreen.Root);
        drawView = DrawView;
        back = () => router.Pop();
        swLaps = new List<double>();
    }

    public void OpenTab(string tab) => pendingTab.Request(tab);

    public void OnOpened()
    {
        router.Reset();
        editingWorld = false;
        editingAlarms = false;
        alarmsDirty = true;
    }

    public void OnResumed() => alarmsDirty = true;

    public void OnClosed() => EndCityDrag(false);

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = context.Theme;
        ConsumePendingTab();
        var scale = UiScale.Current;
        ui.Backdrop(SceneChrome.ScreenFrom(context.Content, context.Theme, scale));
        router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
    }

    private void ConsumePendingTab()
    {
        if (pendingTab.Take(AlarmsRoute))
        {
            OpenRootTab(ClockTab.Alarms);
        }
        else if (pendingTab.Take(TimerRoute))
        {
            OpenRootTab(ClockTab.Timer);
        }
        else if (pendingTab.Take(StopwatchRoute))
        {
            OpenRootTab(ClockTab.Stopwatch);
        }
        else if (pendingTab.Take(WorldRoute))
        {
            OpenRootTab(ClockTab.World);
        }
    }

    private void OpenRootTab(ClockTab tab)
    {
        router.Reset();
        SelectTab(tab);
    }

    private void DrawView(ClockScreen screen, Rect area, int depth)
    {
        ui.Body(area);
        var context = new PhoneContext(area, theme, navigation);
        switch (screen)
        {
            case ClockScreen.EditAlarm:
                DrawAlarmEditor(context);
                return;
            case ClockScreen.AddCity:
                DrawCityPicker(context);
                return;
            default:
                DrawRoot(context, area);
                return;
        }
    }

    private void DrawRoot(in PhoneContext context, Rect area)
    {
        var scale = UiScale.Current;
        using (TabBar.ReserveContent(scale))
        {
            switch (activeTab)
            {
                case ClockTab.Alarms:
                    DrawAlarms(context);
                    break;
                case ClockTab.Stopwatch:
                    DrawStopwatch(context);
                    break;
                case ClockTab.Timer:
                    DrawTimer(context);
                    break;
                default:
                    DrawWorld(context);
                    break;
            }
        }

        DrawTabBar(area);
    }

    private void DrawTabBar(Rect area)
    {
        tabItems[(int)ClockTab.World] = new TabItem(Loc.T(L.Clock.TabWorld), IconGlyph.Of(FontAwesomeIcon.Globe),
            AnchorKey: WorldRoute);
        tabItems[(int)ClockTab.Alarms] = new TabItem(Loc.T(L.Clock.TabAlarms), PhoneIcons.Bell, PhoneIcons.BellFilled,
            AnchorKey: AlarmsRoute);
        tabItems[(int)ClockTab.Stopwatch] = new TabItem(Loc.T(L.Clock.TabStopwatch),
            IconGlyph.Of(FontAwesomeIcon.Stopwatch), AnchorKey: StopwatchRoute);
        tabItems[(int)ClockTab.Timer] = new TabItem(Loc.T(L.Clock.TabTimer),
            IconGlyph.Of(FontAwesomeIcon.HourglassHalf), AnchorKey: TimerRoute);
        var result = tabBar.Draw(area, ui, tabItems, (int)activeTab);
        if (result.Tapped < 0 || result.Tapped == (int)activeTab)
        {
            return;
        }

        SelectTab((ClockTab)result.Tapped);
    }

    private void SelectTab(ClockTab tab)
    {
        if (tab == activeTab)
        {
            return;
        }

        activeTab = tab;
        editingWorld = false;
        editingAlarms = false;
        EndCityDrag(false);
        if (tab == ClockTab.Timer)
        {
            primeTimerPicker = true;
        }
    }

    private int NavButton(int count, string glyph, string tooltip)
    {
        navButtons[count] = new NavBarButton(glyph, tooltip);
        return count + 1;
    }

    public void Dispose()
    {
    }
}
