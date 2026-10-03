using System.Collections.Frozen;
using Aetherphone.Core;
using Aetherphone.Core.Calendar;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Calendar;

internal sealed partial class CalendarApp : IPhoneApp
{
    private enum CalendarScreen : byte
    {
        Month,
        EditEvent,
        Groups,
        EditGroup,
    }

    private const float EditorFieldHeight = 46f;

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
    private readonly NavBarButton[] headerButtons = new NavBarButton[2];
    private readonly Action<Guid> deleteCustomEvent;
    private readonly Action<Guid> editCustomEvent;
    private readonly Action stepDateBack;
    private readonly Action stepDateForward;
    private readonly Action stepHourBack;
    private readonly Action stepHourForward;
    private readonly Action stepMinuteBack;
    private readonly Action stepMinuteForward;
    private readonly Action stepLeadBack;
    private readonly Action stepLeadForward;
    private readonly Action stepGroupBack;
    private readonly Action stepGroupForward;
    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private int monthOffset;
    private DateTime selectedDate;
    private FrozenDictionary<long, ParsedEvent[]> merged = FrozenDictionary<long, ParsedEvent[]>.Empty;
    private FrozenDictionary<long, ParsedEvent[]> mergedRemote = FrozenDictionary<long, ParsedEvent[]>.Empty;
    private Vector4 mergedAccent;
    private int mergedRevision = -1;

    public CalendarApp(Configuration configuration, CalendarEvents events, ConfirmService confirm)
    {
        this.configuration = configuration;
        this.events = events;
        this.confirm = confirm;
        selectedDate = DateTime.Today;
        router = new ViewRouter<CalendarScreen>(CalendarScreen.Month);
        drawView = DrawView;
        back = () => router.Pop();
        deleteCustomEvent = AskDeleteCustomEvent;
        editCustomEvent = StartEditEvent;
        stepDateBack = () => editDate = editDate.AddDays(-1);
        stepDateForward = () => editDate = editDate.AddDays(1);
        stepHourBack = () => editHour = (editHour + HoursPerDay - 1) % HoursPerDay;
        stepHourForward = () => editHour = (editHour + 1) % HoursPerDay;
        stepMinuteBack = () => editMinute = (editMinute + MinutesPerHour - MinuteStep) % MinutesPerHour;
        stepMinuteForward = () => editMinute = (editMinute + MinuteStep) % MinutesPerHour;
        stepLeadBack = () => editLeadIndex = (editLeadIndex + LeadOptionCount - 1) % LeadOptionCount;
        stepLeadForward = () => editLeadIndex = (editLeadIndex + 1) % LeadOptionCount;
        stepGroupBack = () => editGroupIndex = (editGroupIndex + GroupChoiceCount - 1) % GroupChoiceCount;
        stepGroupForward = () => editGroupIndex = (editGroupIndex + 1) % GroupChoiceCount;
    }

    public void OnOpened()
    {
        router.Reset();
        monthOffset = 0;
        selectedDate = DateTime.Today;
        events.Initialize();
    }

    public void OnClosed()
    {
        router.Reset();
    }

    public void Draw(in PhoneContext context)
    {
        var scale = UiScale.Current;
        var content = context.Content;
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = context.Theme;
        ui.Palette = AppPalettes.Calendar(context.Theme);

        var screen = SceneChrome.ScreenFrom(content, context.Theme, scale);
        ui.Backdrop(screen);
        router.Draw(content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
    }

    private void DrawView(CalendarScreen screen, Rect area, int depth)
    {
        var scale = UiScale.Current;
        ui.Body(area);
        switch (screen)
        {
            case CalendarScreen.EditEvent:
                DrawEventEditor(area, scale);
                return;
            case CalendarScreen.Groups:
                DrawGroups(area, scale);
                return;
            case CalendarScreen.EditGroup:
                DrawGroupEditor(area, scale);
                return;
            default:
                DrawMonth(area, scale);
                return;
        }
    }

    private void DrawMonth(Rect content, float scale)
    {
        var context = new PhoneContext(content, theme, navigation);
        var navBar = AppHeader.BeginLargeTitle(context, false);
        var body = navBar.Body;
        using (AppSurface.Begin(body))
        {
            if (events.IsLoaded)
            {
                TourHolds.Release(Id);
                DrawMonthBody(body, scale);
            }
            else
            {
                TourHolds.Hold(Id);
                DrawMonthStatus(body, scale);
            }
        }

        headerButtons[0] = new NavBarButton(IconGlyph.Of(FontAwesomeIcon.LayerGroup), Loc.T(L.Calendar.Groups));
        headerButtons[1] = new NavBarButton(IconGlyph.Of(FontAwesomeIcon.Plus), Loc.T(L.Calendar.NewEvent));
        UiAnchors.Report("calendar.groups", AppHeader.LargeTitleButtonRect(in navBar, 0, headerButtons.Length));
        UiAnchors.Report("calendar.new",AppHeader.LargeTitleButtonRect(in navBar, 1, headerButtons.Length));
        var pressed = AppHeader.EndLargeTitle(in navBar, context, "calendar.nav", DisplayName, NavBarStyle.From(ui),
            headerButtons);
        if (pressed == 0)
        {
            router.Push(CalendarScreen.Groups);
        }
        else if (pressed == 1)
        {
            StartNewEvent();
        }
    }

    private void DrawMonthStatus(Rect body, float scale)
    {
        if (events.IsLoading)
        {
            Typography.DrawCentered(new Vector2(body.Center.X, body.Min.Y + 60f * scale), Loc.T(L.Common.Loading),
                ui.MutedInk);
        }
        else if (events.HasFailed)
        {
            Typography.DrawCentered(new Vector2(body.Center.X, body.Min.Y + 60f * scale),
                Loc.T(L.Calendar.FailedToLoad), ui.MutedInk);
        }
    }

    private void DrawMonthBody(Rect body, float scale)
    {
        var visible = MergedEvents();
        var detailReserved = Math.Clamp(body.Height * 0.30f, 130f * scale, 220f * scale);
        var monthTarget = body.Height - detailReserved;
        var monthBottom = CalendarMonthView.Draw(ui, body, monthTarget, ref monthOffset, ref selectedDate, visible);
        ImGui.Dummy(new Vector2(0f, 8f * scale));

        var detailArea = new Rect(new Vector2(body.Min.X, monthBottom), new Vector2(body.Max.X, body.Max.Y));
        UiAnchors.Report("calendar.agenda", detailArea);
        CalendarDayList.Draw(ui, detailArea, selectedDate, visible, scale, deleteCustomEvent, editCustomEvent);
    }

    private FrozenDictionary<long, ParsedEvent[]> MergedEvents()
    {
        var remote = events.Events;
        var accent = ui.Accent;
        if (mergedRevision == events.CustomRevision && ReferenceEquals(remote, mergedRemote) && accent == mergedAccent)
        {
            return merged;
        }

        merged = CalendarEventMerger.Merge(remote, configuration.CalendarCustomEvents, configuration.CalendarGroups,
            configuration.CalendarGameEventsInApp, CalendarSurface.App, accent);
        mergedRemote = remote;
        mergedAccent = accent;
        mergedRevision = events.CustomRevision;
        return merged;
    }

    private void DrawTextField(Rect rect, float scale, string id, string hint, ref string value, int maxLength)
    {
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, rect.Min, rect.Max, Metrics.Radius.Md * scale);
        ImGui.SetCursorScreenPos(new Vector2(rect.Min.X + Metrics.Space.Md * scale,
            rect.Min.Y + rect.Height * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(rect.Width - Metrics.Space.Md * 2f * scale);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, ui.TitleInk))
        {
            ImGui.InputTextWithHint(id, hint, ref value, maxLength, ImGuiInputTextFlags.None);
        }
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

    private void AskDeleteCustomEvent(Guid id)
    {
        confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Calendar.DeleteConfirmMessage),
            ConfirmLabel = Loc.T(L.Calendar.DeleteConfirm),
            CancelLabel = Loc.T(L.Calendar.DeleteCancel),
            Sheet = true,
            Confirm = () => DeleteCustomEvent(id),
        });
    }

    private void DeleteCustomEvent(Guid id)
    {
        configuration.CalendarCustomEvents.RemoveAll(entry => entry.Id == id);
        SaveCalendar();
    }

    public void Dispose()
    {
        events.Dispose();
    }
}
