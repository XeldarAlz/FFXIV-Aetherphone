using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Calendar;

internal sealed class CalendarApp : IPhoneApp
{
    private enum CalendarScreen : byte
    {
        Month,
        AddEvent,
    }

    private const int TitleMaxLength = 60;

    public string Id => "calendar";
    public string DisplayName => Loc.T(L.Calendar.Title);
    public string Glyph => "C";
    public int BadgeCount => 0;

    private readonly CalendarEvents events;
    private readonly Configuration configuration;
    private readonly AppSkin ui = new(AppPalettes.Calendar(PhoneTheme.Default));
    private readonly ViewRouter<CalendarScreen> router;
    private readonly RouterDraw<CalendarScreen> drawView;
    private readonly Action back;
    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private int monthOffset;
    private DateTime selectedDate;
    private string newEventTitle = string.Empty;
    private DateTime newEventDate;
    private int newEventHour;
    private int newEventMinute;

    public CalendarApp(Configuration configuration, CalendarEvents events)
    {
        this.configuration = configuration;
        this.events = events;
        selectedDate = DateTime.Today;
        router = new ViewRouter<CalendarScreen>(CalendarScreen.Month, Id);
        drawView = DrawView;
        back = () => router.Pop();
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
        var scale = ImGuiHelpers.GlobalScale;
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
        var scale = ImGuiHelpers.GlobalScale;
        ui.Body(area);
        if (screen == CalendarScreen.AddEvent)
        {
            DrawAddEvent(area, scale);
            return;
        }

        DrawMonth(area, scale);
    }

    private void DrawMonth(Rect content, float scale)
    {
        DrawTopBar(content, scale);

        var bodyMin = new Vector2(content.Min.X, content.Min.Y + AppHeader.Height * scale);
        var body = new Rect(bodyMin, content.Max);

        using (AppSurface.Begin(body))
        {
            if (!events.IsLoaded)
            {
                if (events.IsLoading)
                {
                    Typography.DrawCentered(new Vector2(body.Center.X, body.Min.Y + 60f * scale),
                        Loc.T(L.Common.Loading), ui.MutedInk);
                }
                else if (events.HasFailed)
                {
                    Typography.DrawCentered(new Vector2(body.Center.X, body.Min.Y + 60f * scale),
                        Loc.T(L.Calendar.FailedToLoad), ui.MutedInk);
                }

                return;
            }

            var merged = CalendarEventMerger.Merge(events.Events, configuration.CalendarCustomEvents, ui.Accent);
            var detailReserved = Math.Clamp(body.Height * 0.30f, 130f * scale, 220f * scale);
            var monthTarget = body.Height - detailReserved;
            var monthBottom = CalendarMonthView.Draw(ui, body, monthTarget, ref monthOffset, ref selectedDate, merged);
            ImGui.Dummy(new Vector2(0f, 8f * scale));

            var detailArea = new Rect(new Vector2(body.Min.X, monthBottom), new Vector2(body.Max.X, body.Max.Y));
            CalendarDayList.Draw(ui, detailArea, selectedDate, merged, scale, AskDeleteCustomEvent);
        }
    }

    private void DrawTopBar(Rect content, float scale)
    {
        var centerY = content.Min.Y + AppHeader.Height * scale * 0.5f;
        var textSize = Typography.Measure(DisplayName, 1.15f, FontWeight.SemiBold);
        Typography.Draw(new Vector2(content.Center.X - textSize.X * 0.5f, centerY - textSize.Y * 0.5f), DisplayName,
            ui.TitleInk, 1.15f, FontWeight.SemiBold);

        var buttonRadius = 15f * scale;
        var buttonCenter = new Vector2(content.Max.X - 16f * scale - buttonRadius, centerY);
        DrawAddButton(buttonCenter, buttonRadius);
    }

    private void DrawAddButton(Vector2 center, float radius)
    {
        var drawList = ImGui.GetWindowDrawList();
        var iconColor = ui.Theme.TextStrong;
        var hovered = ImGui.IsMouseHoveringRect(center - new Vector2(radius, radius), center + new Vector2(radius, radius));
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(iconColor, hovered ? 0.20f : 0.12f)), 32);
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var glyph = FontAwesomeIcon.Plus.ToIconString();
            var fontSize = ImGui.GetFontSize() * 0.62f;
            var size = ImGui.CalcTextSize(glyph) * 0.62f;
            drawList.AddText(UiBuilder.IconFont, fontSize, center - size * 0.5f, ImGui.GetColorU32(iconColor), glyph);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            ui.DrawActionTooltip(center, radius, Loc.T(L.Calendar.NewEvent));
        }

        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            StartAddEvent();
        }
    }

    private void StartAddEvent()
    {
        newEventTitle = string.Empty;
        newEventDate = selectedDate;
        var now = DateTime.Now;
        newEventHour = now.Hour;
        newEventMinute = now.Minute / 5 * 5;
        router.Push(CalendarScreen.AddEvent);
    }

    private void DrawAddEvent(Rect content, float scale)
    {
        var context = new PhoneContext(content, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Calendar.NewEvent), back);
        var margin = 16f * scale;
        var top = content.Min.Y + AppHeader.Height * scale + 16f * scale;
        var fieldHeight = 46f * scale;
        var gap = 12f * scale;

        var titleRect = new Rect(new Vector2(content.Min.X + margin, top),
            new Vector2(content.Max.X - margin, top + fieldHeight));
        DrawTitleField(titleRect, scale);

        var labelHeight = Typography.Measure("A", TextStyles.Footnote).Y;
        var dateLabelY = titleRect.Max.Y + gap;
        Typography.Draw(new Vector2(content.Min.X + margin, dateLabelY), Loc.T(L.Calendar.EventDate), ui.MutedInk,
            TextStyles.Footnote);
        var dateTop = dateLabelY + labelHeight + 4f * scale;
        var dateRect = new Rect(new Vector2(content.Min.X + margin, dateTop),
            new Vector2(content.Max.X - margin, dateTop + fieldHeight));
        DrawChevronField(dateRect, newEventDate.ToString("dddd, MMM d", Loc.Culture), scale, () => newEventDate = newEventDate.AddDays(-1),
            () => newEventDate = newEventDate.AddDays(1));

        var timeLabelY = dateRect.Max.Y + gap;
        Typography.Draw(new Vector2(content.Min.X + margin, timeLabelY), Loc.T(L.Calendar.EventTime), ui.MutedInk,
            TextStyles.Footnote);
        var timeRowTop = timeLabelY + labelHeight + 4f * scale;
        var timeRowRect = new Rect(new Vector2(content.Min.X + margin, timeRowTop),
            new Vector2(content.Max.X - margin, timeRowTop + fieldHeight));
        var half = timeRowRect.Width * 0.5f - gap * 0.5f;
        var hourRect = new Rect(timeRowRect.Min, new Vector2(timeRowRect.Min.X + half, timeRowRect.Max.Y));
        var minuteRect = new Rect(new Vector2(timeRowRect.Max.X - half, timeRowRect.Min.Y), timeRowRect.Max);
        DrawChevronField(hourRect, newEventHour.ToString("D2"), scale, () => newEventHour = (newEventHour + 23) % 24,
            () => newEventHour = (newEventHour + 1) % 24);
        DrawChevronField(minuteRect, newEventMinute.ToString("D2"), scale,
            () => newEventMinute = (newEventMinute + 55) % 60, () => newEventMinute = (newEventMinute + 5) % 60);

        var saveHeight = 46f * scale;
        var saveRect = new Rect(new Vector2(content.Min.X + margin, content.Max.Y - margin - saveHeight),
            new Vector2(content.Max.X - margin, content.Max.Y - margin));
        var enabled = newEventTitle.Trim().Length > 0;
        if (DrawSaveButton(saveRect, scale, enabled) && enabled)
        {
            CommitNewEvent();
        }
    }

    private void DrawTitleField(Rect rect, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, rect.Min, rect.Max, 12f * scale);
        ImGui.SetCursorScreenPos(new Vector2(rect.Min.X + 12f * scale,
            rect.Min.Y + rect.Height * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(rect.Width - 24f * scale);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, ui.TitleInk))
        {
            ImGui.InputTextWithHint("##calNewEventTitle", Loc.T(L.Calendar.TitlePlaceholder), ref newEventTitle,
                TitleMaxLength, ImGuiInputTextFlags.None);
        }
    }

    private void DrawChevronField(Rect rect, string valueText, float scale, Action onDecrement, Action onIncrement)
    {
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, rect.Min, rect.Max, 12f * scale);
        var chevronWidth = 34f * scale;
        var leftRect = new Rect(rect.Min, new Vector2(rect.Min.X + chevronWidth, rect.Max.Y));
        var rightRect = new Rect(new Vector2(rect.Max.X - chevronWidth, rect.Min.Y), rect.Max);
        if (DrawChevronHit(drawList, leftRect, "<", scale))
        {
            onDecrement();
        }

        if (DrawChevronHit(drawList, rightRect, ">", scale))
        {
            onIncrement();
        }

        Typography.DrawCentered(drawList, rect.Center, valueText, ui.TitleInk, TextStyles.Headline.Scale,
            TextStyles.Headline.Weight);
    }

    private bool DrawChevronHit(ImDrawListPtr drawList, Rect rect, string chevron, float scale)
    {
        var hovered = ImGui.IsMouseHoveringRect(rect.Min, rect.Max);
        if (hovered)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, 10f * scale, ImGui.GetColorU32(ui.HoverTint));
        }

        Typography.DrawCentered(drawList, rect.Center, chevron, ui.MutedInk, TextStyles.Headline.Scale,
            TextStyles.Headline.Weight);
        return UiInteract.HoverClick(rect.Min, rect.Max);
    }

    private bool DrawSaveButton(Rect rect, float scale, bool enabled)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = enabled && ImGui.IsMouseHoveringRect(rect.Min, rect.Max);
        var fill = !enabled ? Palette.WithAlpha(ui.Accent, 0.35f) :
            hovered ? Palette.Mix(ui.Accent, new Vector4(0f, 0f, 0f, 1f), 0.12f) : ui.Accent;
        Squircle.Fill(drawList, rect.Min, rect.Max, rect.Height * 0.5f, ImGui.GetColorU32(fill));
        Typography.DrawCentered(drawList, rect.Center, Loc.T(L.Calendar.Save), new Vector4(1f, 1f, 1f, 1f),
            TextStyles.Headline.Scale, TextStyles.Headline.Weight);
        if (!hovered)
        {
            return false;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private void CommitNewEvent()
    {
        var title = newEventTitle.Trim();
        if (title.Length == 0)
        {
            return;
        }

        var when = new DateTime(newEventDate.Year, newEventDate.Month, newEventDate.Day, newEventHour,
            newEventMinute, 0);
        configuration.CalendarCustomEvents.Add(new CalendarCustomEvent { Title = title, When = when });
        configuration.Save();

        selectedDate = when.Date;
        var today = DateTime.Today;
        monthOffset = ((when.Year - today.Year) * 12) + when.Month - today.Month;
        router.Pop();
    }

    private void AskDeleteCustomEvent(Guid id)
    {
        Plugin.Confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Calendar.DeleteConfirmMessage),
            ConfirmLabel = Loc.T(L.Calendar.DeleteConfirm),
            CancelLabel = Loc.T(L.Calendar.DeleteCancel),
            Confirm = () => DeleteCustomEvent(id),
        });
    }

    private void DeleteCustomEvent(Guid id)
    {
        configuration.CalendarCustomEvents.RemoveAll(entry => entry.Id == id);
        configuration.Save();
    }

    public void Dispose()
    {
        events.Dispose();
    }
}
