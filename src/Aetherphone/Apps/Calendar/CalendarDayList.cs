using System.Collections.Frozen;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Calendar;

internal static class CalendarDayList
{
    private const float TextGapX = 12f;
    private const float CellPaddingY = 8f;
    private const float AccentBarWidth = 3f;
    private const float AccentBarInset = 8f;
    private const float HeaderGapY = 8f;
    private const float ListTailPadding = 8f;

    public static void Draw(AppSkin ui, Rect area, DateTime selectedDate,
        FrozenDictionary<long, ParsedEvent[]> events, float scale, Action<Guid> onDeleteCustom,
        Action<Guid> onEditCustom)
    {
        var dateLabel = selectedDate.ToString("dddd, MMMM d", Loc.Culture);
        Typography.Draw(new Vector2(area.Min.X + FeedCell.PadX * scale, area.Min.Y), dateLabel, ui.TitleInk,
            TextStyles.Headline);
        var listTop = area.Min.Y + Typography.Measure(dateLabel, TextStyles.Headline).Y + HeaderGapY * scale;

        var dayKey = selectedDate.Date.Ticks;
        var dayEvents = events.TryGetValue(dayKey, out var found) ? found : Array.Empty<ParsedEvent>();

        if (dayEvents.Length == 0)
        {
            Typography.Draw(new Vector2(area.Min.X + FeedCell.PadX * scale, listTop), Loc.T(L.Calendar.NoEvents),
                ui.MutedInk, TextStyles.Subheadline);
            return;
        }

        var listKey = ImGui.GetID("##calendarAgenda");
        ImGui.SetCursorScreenPos(new Vector2(area.Min.X, listTop));
        var listSize = new Vector2(area.Width, MathF.Max(1f, ImGui.GetContentRegionAvail().Y));
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, Vector2.Zero))
        using (var list = ImRaii.Child("##calendarAgenda", listSize, false,
                   DragScrollHost.ScrollFlags(ImGuiWindowFlags.NoBackground)))
        {
            if (!list)
            {
                return;
            }

            AppSurface.ResetScrollOnNewVisit();
            var surface = DragScrollHost.Begin(listKey);
            if (ConsumeDayChange(dayKey))
            {
                surface.JumpToTop();
            }

            DrawCells(ui, dayEvents, scale, onDeleteCustom, onEditCustom);
        }
    }

    private static void DrawCells(AppSkin ui, ParsedEvent[] dayEvents, float scale, Action<Guid> onDeleteCustom,
        Action<Guid> onEditCustom)
    {
        var drawList = ImGui.GetWindowDrawList();
        var contentWidth = ScrollLayout.StableContentWidth();
        var hlScale = TextStyles.BodyEmphasized.Scale;
        var hlWeight = TextStyles.BodyEmphasized.Weight;
        var fnScale = TextStyles.Footnote.Scale;
        var fnWeight = TextStyles.Footnote.Weight;

        for (var index = 0; index < dayEvents.Length; index++)
        {
            var dayEvent = dayEvents[index];
            var cellMin = ImGui.GetCursorScreenPos();
            var nameSize = Typography.Measure(dayEvent.Name, hlScale, hlWeight);
            var dateRange = FormatDateRange(dayEvent);
            var dateSize = Typography.Measure(dateRange, fnScale, fnWeight);
            var textHeight = nameSize.Y + 3f * scale + dateSize.Y;
            var cellHeight = Math.Max(textHeight + CellPaddingY * 2f * scale, 40f * scale);
            var cellMax = new Vector2(cellMin.X + contentWidth, cellMin.Y + cellHeight);
            var clickable = dayEvent.IsCustom || !string.IsNullOrEmpty(dayEvent.Url);
            var cellHovered = UiInteract.Hover(cellMin, cellMax);
            var hovered = clickable && cellHovered;
            if (hovered)
            {
                drawList.AddRectFilled(cellMin, cellMax, ImGui.GetColorU32(ui.HoverWash));
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            var accentMin = new Vector2(cellMin.X + FeedCell.PadX * scale, cellMin.Y + AccentBarInset * scale);
            var accentMax = new Vector2(accentMin.X + AccentBarWidth * scale, cellMax.Y - AccentBarInset * scale);
            var accentRounding = AccentBarWidth * 0.5f * scale;
            Squircle.Fill(drawList, accentMin, accentMax, accentRounding, ImGui.GetColorU32(dayEvent.Color));

            var textStartX = accentMax.X + TextGapX * scale;
            var textStartY = cellMin.Y + (cellHeight - textHeight) * 0.5f;
            var trailing = (dayEvent.IsCustom ? 32f * scale : 0f) + FeedCell.PadX * scale;
            var textMaxWidth = MathF.Max(1f, cellMax.X - textStartX - trailing);
            Marquee.DrawLeft(new MarqueeId("calendar.day.", dayEvent.Name + "." + index), dayEvent.Name, textStartX, textStartY,
                textMaxWidth, new TextStyle(hlScale, hlWeight), ui.TitleInk, cellHovered);
            var dateFitted = Typography.FitText(dateRange, textMaxWidth, fnScale, fnWeight);
            Typography.Draw(drawList, new Vector2(textStartX, textStartY + nameSize.Y + 3f * scale),
                dateFitted, ui.MutedInk, fnScale, fnWeight);

            if (dayEvent.IsCustom)
            {
                var overDelete = DrawDeleteButton(drawList, cellMax, scale, ui, dayEvent.CustomId, onDeleteCustom);
                if (!overDelete && UiInteract.Click(cellMin, cellMax, cellHovered))
                {
                    onEditCustom(dayEvent.CustomId);
                }
            }
            else if (clickable && UiInteract.HoverClick(cellMin, cellMax))
            {
                Dalamud.Utility.Util.OpenLink(dayEvent.Url);
            }

            FeedCell.Hairline(drawList, cellMin.X, cellMax.X, cellMax.Y, ui.Hairline);
            ImGui.SetCursorScreenPos(cellMin);
            ImGui.Dummy(new Vector2(contentWidth, cellHeight));
        }

        ImGui.Dummy(new Vector2(0f, ListTailPadding * scale));
    }

    private static bool ConsumeDayChange(long dayKey)
    {
        var storage = ImGui.GetStateStorage();
        var stampKey = ImGui.GetID("##calendarAgendaDay");
        var stamp = (int)(dayKey / TimeSpan.TicksPerDay);
        if (storage.GetInt(stampKey, 0) == stamp)
        {
            return false;
        }

        storage.SetInt(stampKey, stamp);
        return true;
    }

    private static bool DrawDeleteButton(ImDrawListPtr drawList, Vector2 cellMax, float scale, AppSkin ui,
        Guid customId, Action<Guid> onDeleteCustom)
    {
        var radius = 12f * scale;
        var center = new Vector2(cellMax.X - FeedCell.PadX * scale - radius,
            cellMax.Y - AccentBarInset * scale - radius + 2f * scale);
        var min = center - new Vector2(radius, radius);
        var max = center + new Vector2(radius, radius);
        var hovered = UiInteract.Hover(min, max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(ui.HoverTint), 24);
        }

        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var glyph = IconGlyph.Of(FontAwesomeIcon.Trash);
            var fontSize = ImGui.GetFontSize() * 0.72f;
            var size = ImGui.CalcTextSize(glyph) * 0.72f;
            drawList.AddText(UiBuilder.IconFont, fontSize, center - size * 0.5f, ImGui.GetColorU32(ui.MutedInk), glyph);
        }

        HoverTooltip.Show(new Rect(min, max), Loc.T(L.Calendar.DeleteEvent), HoverLabelSide.Above);

        if (UiInteract.HoverClick(min, max))
        {
            onDeleteCustom(customId);
        }

        return hovered;
    }

    private static string FormatDateRange(in ParsedEvent dayEvent)
    {
        var begin = dayEvent.Begin;
        var end = dayEvent.End;

        if (dayEvent.IsCustom)
        {
            var when = string.Concat(begin.ToString("MMM d", Loc.Culture), ", ", TimeText.Clock(begin));
            return string.IsNullOrEmpty(dayEvent.GroupName) ? when : string.Concat(when, " · ", dayEvent.GroupName);
        }

        if (begin.Date == end.Date)
        {
            return begin.ToString("MMM d", Loc.Culture);
        }

        if (begin.Year == end.Year && begin.Month == end.Month)
        {
            return string.Concat(begin.ToString("MMM d", Loc.Culture), " \u2013 ", end.ToString("d", Loc.Culture));
        }

        return string.Concat(begin.ToString("MMM d", Loc.Culture), " \u2013 ", end.ToString("MMM d, yyyy", Loc.Culture));
    }
}
