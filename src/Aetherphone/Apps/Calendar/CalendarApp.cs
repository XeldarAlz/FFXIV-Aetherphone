using System.Collections.Frozen;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Calendar;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Calendar;

internal enum CalendarScreen : byte
{
    Root,
    Event,
    EditEvent,
    Groups,
    EditGroup,
}

internal sealed partial class CalendarApp : IResumableApp
{
    private const int MonthsPerYear = 12;

    public string Id => "calendar";
    public string DisplayName => Loc.T(L.Calendar.Title);
    public string Glyph => "C";
    public int BadgeCount => 0;
    public bool WantsSystemTheme => true;

    private readonly CalendarEvents events;
    private readonly Configuration configuration;
    private readonly ConfirmService confirm;
    private readonly AppSkin ui = new(AppPalettes.Calendar(PhoneTheme.Default));
    private readonly ViewRouter<CalendarScreen> router;
    private readonly RouterDraw<CalendarScreen> drawView;
    private readonly Action back;
    private readonly CalendarAgenda agenda = new();
    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private Rect frameScreen;
    private FrozenDictionary<long, ParsedEvent[]> merged = FrozenDictionary<long, ParsedEvent[]>.Empty;
    private FrozenDictionary<long, ParsedEvent[]> mergedRemote = FrozenDictionary<long, ParsedEvent[]>.Empty;
    private Vector4 mergedAccent;
    private int mergedRevision = -1;
    private int mergedWindowYear;
    private int mergedWindowEndYear;
    private bool mergedShowsGame;

    public CalendarApp(Configuration configuration, CalendarEvents events, ConfirmService confirm)
    {
        this.configuration = configuration;
        this.events = events;
        this.confirm = confirm;
        selectedDate = DateTime.Today;
        router = new ViewRouter<CalendarScreen>(CalendarScreen.Root);
        drawView = DrawView;
        back = () => router.Pop();
    }

    public void OnOpened()
    {
        router.Reset();
        menu.Close();
        mode = CalendarMode.Month;
        monthOffset = 0;
        selectedDate = DateTime.Today;
        monthSlide.SnapTo(0f);
        gridHeightPrimed = false;
        events.Initialize();
    }

    public void OnResumed() => events.Initialize();

    public void OnClosed() => menu.Close();

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = context.Theme;
        ui.Palette = AppPalettes.Calendar(context.Theme) with { Accent = AppAccents.For("calendar") };
        var scale = UiScale.Current;
        frameScreen = SceneChrome.ScreenFrom(context.Content, context.Theme, scale);
        ui.Backdrop(frameScreen);
        menu.Gate();
        TourHolds.Release(Id);
        router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
        DrawMenu();
    }

    private void DrawView(CalendarScreen screen, Rect area, int depth)
    {
        ui.Body(area);
        switch (screen)
        {
            case CalendarScreen.Event:
                DrawDetail(area);
                return;
            case CalendarScreen.EditEvent:
                DrawEventEditor(area);
                return;
            case CalendarScreen.Groups:
                DrawGroups(area);
                return;
            case CalendarScreen.EditGroup:
                DrawGroupEditor(area);
                return;
            default:
                DrawRoot(area);
                return;
        }
    }

    private void Push(CalendarScreen screen)
    {
        UiFeedback.Play(UiSound.Tap);
        menu.Close();
        router.Push(screen);
    }

    private FrozenDictionary<long, ParsedEvent[]> MergedEvents()
    {
        var remote = events.Events;
        var accent = ui.Accent;
        var showGame = configuration.CalendarGameEventsInApp;
        var windowYear = Math.Min(VisibleMonth.Year, DateTime.Today.Year);
        var windowEndYear = Math.Max(VisibleMonth.Year, DateTime.Today.Year);
        if (mergedRevision == events.CustomRevision && ReferenceEquals(remote, mergedRemote) &&
            accent == mergedAccent && windowYear == mergedWindowYear && windowEndYear == mergedWindowEndYear &&
            showGame == mergedShowsGame)
        {
            return merged;
        }

        var windowStart = new DateTime(windowYear - 1, 1, 1);
        var windowEnd = new DateTime(windowEndYear + 2, 1, 1);
        merged = CalendarEventMerger.Merge(remote, configuration.CalendarCustomEvents, configuration.CalendarGroups,
            showGame, CalendarSurface.App, accent, windowStart, windowEnd);
        mergedRemote = remote;
        mergedAccent = accent;
        mergedRevision = events.CustomRevision;
        mergedWindowYear = windowYear;
        mergedWindowEndYear = windowEndYear;
        mergedShowsGame = showGame;
        agenda.Invalidate();
        return merged;
    }

    private static bool HasText(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (!char.IsWhiteSpace(value[index]))
            {
                return true;
            }
        }

        return false;
    }

    private void SaveCalendar()
    {
        configuration.Save();
        events.MarkCustomChanged();
    }

    private CalendarCustomEvent? FindCustomEvent(Guid id)
    {
        var customEvents = configuration.CalendarCustomEvents;
        for (var index = 0; index < customEvents.Count; index++)
        {
            if (customEvents[index].Id == id)
            {
                return customEvents[index];
            }
        }

        return null;
    }

    private CalendarEventGroup? FindGroup(Guid groupId)
    {
        if (groupId == Guid.Empty)
        {
            return null;
        }

        var groups = configuration.CalendarGroups;
        for (var index = 0; index < groups.Count; index++)
        {
            if (groups[index].Id == groupId)
            {
                return groups[index];
            }
        }

        return null;
    }

    private static int MonthOffsetOf(DateTime date)
    {
        var today = DateTime.Today;
        return (date.Year - today.Year) * MonthsPerYear + date.Month - today.Month;
    }

    public void Dispose()
    {
        events.Dispose();
    }
}
