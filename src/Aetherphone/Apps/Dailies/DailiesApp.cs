using Aetherphone.Apps.Timers;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Dailies;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Dailies;

internal sealed partial class DailiesApp : IPhoneApp, ITabRouteTarget
{
    private const float BottomPad = 28f;
    private const int AddButton = 0;
    private const int MenuButton = 1;
    private const int MenuEdit = 0;
    private const int MenuRemindDaily = 1;
    private const int MenuRemindWeekly = 2;
    private const string WeeklyTab = "dailies.tab.weekly";

    private readonly DailiesTracker tracker;
    private readonly AppSkin ui = new(AppPalettes.Dailies);
    private readonly NavBarButton[] navButtons = new NavBarButton[2];
    private readonly DropdownMenu menu = new();
    private readonly DropdownMenu.Item[] menuItems = new DropdownMenu.Item[3];
    private PendingTab pendingTab;
    private DailyCadence cadence;
    private bool editing;
    private Rect frameScreen;
    private PhoneTheme frameTheme = PhoneTheme.Default;

    public DailiesApp(DailiesTracker tracker)
    {
        this.tracker = tracker;
    }

    public string Id => DailiesTracker.AppId;

    public string DisplayName => Loc.T(L.Apps.Dailies);

    public string Glyph => "D";

    public int BadgeCount => tracker.Outstanding;

    public bool HasBadge => true;

    public void OpenTab(string tab) => pendingTab.Request(tab);

    public void OnOpened()
    {
        cadence = DailyCadence.Daily;
        editing = false;
        EndCompose();
        menu.Close();
        PrimeChecks();
        PrimeTiles();
    }

    public void OnClosed()
    {
        EndCompose();
        menu.Close();
    }

    public void Draw(in PhoneContext context)
    {
        if (pendingTab.Take(WeeklyTab))
        {
            SelectCadence(DailyCadence.Weekly, false);
        }

        frameTheme = context.Theme;
        ui.Theme = frameTheme;
        var area = context.Content;
        var scale = UiScale.Current;
        frameScreen = SceneChrome.ScreenFrom(area, frameTheme, scale);
        ui.Backdrop(frameScreen);
        ui.Body(area);
        menu.Gate();

        var loggedIn = tracker.LoggedIn;
        if (loggedIn)
        {
            TourHolds.Release(Id);
        }
        else
        {
            TourHolds.Hold(Id);
        }

        var navBar = AppHeader.BeginLargeTitle(context, false);
        using (AppSurface.Begin(navBar.Body))
        {
            var width = ImGui.GetContentRegionAvail().X;
            var utcNow = DateTime.UtcNow;
            if (loggedIn)
            {
                DrawTiles(utcNow, width, scale);
                DrawSections(utcNow, width, scale);
            }
            else
            {
                DrawState(FontAwesomeIcon.UserCircle, Loc.T(L.Dailies.SignedOutTitle),
                    Loc.T(L.Dailies.SignedOutBody), width, scale);
            }

            ImGui.Dummy(new Vector2(0f, BottomPad * scale));
        }

        DrawNavBar(in navBar, context, loggedIn);
        DrawMenu();
    }

    private void DrawNavBar(in NavBarFrame navBar, in PhoneContext context, bool loggedIn)
    {
        var count = loggedIn ? navButtons.Length : 0;
        navButtons[AddButton] = new NavBarButton(IconGlyph.Of(FontAwesomeIcon.Plus), Loc.T(L.Dailies.NewTask));
        navButtons[MenuButton] = editing
            ? new NavBarButton(IconGlyph.Of(FontAwesomeIcon.Check), Loc.T(L.Dailies.Done))
            : new NavBarButton(IconGlyph.Of(FontAwesomeIcon.EllipsisH), Loc.T(L.Dailies.More));
        var addRect = AppHeader.LargeTitleButtonRect(in navBar, AddButton, navButtons.Length);
        var menuRect = AppHeader.LargeTitleButtonRect(in navBar, MenuButton, navButtons.Length);
        if (loggedIn)
        {
            UiAnchors.Report("dailies.add", addRect);
        }

        var pressed = AppHeader.EndLargeTitle(in navBar, context, "dailies.nav", DisplayName, NavBarStyle.From(ui),
            navButtons.AsSpan(0, count));
        if (pressed == AddButton)
        {
            editing = false;
            StartCompose();
            return;
        }

        if (pressed != MenuButton)
        {
            return;
        }

        if (editing)
        {
            editing = false;
            return;
        }

        menu.Toggle("dailies.menu", menuRect);
    }

    private void DrawMenu()
    {
        if (!menu.Open)
        {
            return;
        }

        menuItems[MenuEdit] = new DropdownMenu.Item(Loc.T(L.Dailies.EditList), IconGlyph.Of(FontAwesomeIcon.Pen));
        menuItems[MenuRemindDaily] = new DropdownMenu.Item(Loc.T(L.Dailies.RemindDaily),
            IconGlyph.Of(FontAwesomeIcon.Bell), false, tracker.RemindDaily);
        menuItems[MenuRemindWeekly] = new DropdownMenu.Item(Loc.T(L.Dailies.RemindWeekly),
            IconGlyph.Of(FontAwesomeIcon.CalendarAlt), false, tracker.RemindWeekly);
        var picked = menu.Draw(frameScreen, frameTheme, menuItems);
        switch (picked)
        {
            case MenuEdit:
                EndCompose();
                editing = true;
                return;
            case MenuRemindDaily:
                ToggleReminder(DailyCadence.Daily, !tracker.RemindDaily);
                return;
            case MenuRemindWeekly:
                ToggleReminder(DailyCadence.Weekly, !tracker.RemindWeekly);
                return;
        }
    }

    private void ToggleReminder(DailyCadence target, bool value)
    {
        tracker.SetReminder(target, value);
        UiFeedback.Play(value ? UiSound.ToggleOn : UiSound.ToggleOff);
    }

    private void SelectCadence(DailyCadence next, bool withSound)
    {
        if (cadence == next)
        {
            return;
        }

        cadence = next;
        EndCompose();
        if (withSound)
        {
            UiFeedback.Play(UiSound.Tap);
        }
    }

    private Rect DrawState(FontAwesomeIcon icon, string title, string body, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var height = TimersArt.StateHeight(title, body, width, scale);
        var rect = new Rect(origin, origin + new Vector2(width, height));
        if (ImGui.IsRectVisible(rect.Min, rect.Max))
        {
            TimersArt.State(ImGui.GetWindowDrawList(), ui, origin, width, height, icon, title, body, scale);
        }

        Advance(origin, width, height, TimersArt.SectionGap, scale);
        return rect;
    }

    private static void Advance(Vector2 origin, float width, float height, float gap, float scale)
    {
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + gap * scale - ImGui.GetStyle().ItemSpacing.Y));
    }

    public void Dispose()
    {
    }
}
