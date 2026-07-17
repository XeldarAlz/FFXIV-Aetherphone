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
    private const float CardPaddingX = 12f;
    private const float CardPaddingY = 8f;
    private const float CardRounding = 14f;
    private const float CardSpacing = 6f;
    private const float AccentBarWidth = 3f;
    private const float AccentBarInset = 8f;

    public static float Draw(AppSkin ui, Rect area, DateTime selectedDate,
        FrozenDictionary<long, ParsedEvent[]> events, float scale, Action<Guid> onDeleteCustom)
    {
        var drawList = ImGui.GetWindowDrawList();
        var cursorY = area.Min.Y;

        var dateLabel = selectedDate.ToString("dddd, MMMM d", Loc.Culture);
        Typography.Draw(new Vector2(area.Min.X + 4f * scale, cursorY), dateLabel, ui.TitleInk, TextStyles.Headline);
        var labelHeight = Typography.Measure(dateLabel, TextStyles.Headline).Y;
        cursorY += labelHeight + 8f * scale;

        var dayKey = selectedDate.Date.Ticks;
        var dayEvents = events.TryGetValue(dayKey, out var found) ? found : Array.Empty<ParsedEvent>();

        if (dayEvents.Length == 0)
        {
            Typography.Draw(new Vector2(area.Min.X + 4f * scale, cursorY), Loc.T(L.Calendar.NoEvents), ui.MutedInk,
                TextStyles.Subheadline);
            return cursorY + Typography.Measure(Loc.T(L.Calendar.NoEvents), TextStyles.Subheadline).Y + 8f * scale;
        }

        var contentWidth = area.Width;
        var hlScale = TextStyles.BodyEmphasized.Scale;
        var hlWeight = TextStyles.BodyEmphasized.Weight;
        var fnScale = TextStyles.Footnote.Scale;
        var fnWeight = TextStyles.Footnote.Weight;

        for (var index = 0; index < dayEvents.Length; index++)
        {
            var dayEvent = dayEvents[index];
            var cardMin = new Vector2(area.Min.X, cursorY);
            var nameSize = Typography.Measure(dayEvent.Name, hlScale, hlWeight);
            var dateSize = Typography.Measure(FormatDateRange(dayEvent), fnScale, fnWeight);
            var textHeight = nameSize.Y + 3f * scale + dateSize.Y;
            var cardHeight = Math.Max(textHeight + CardPaddingY * 2f * scale, 40f * scale);
            var cardMax = new Vector2(cardMin.X + contentWidth, cardMin.Y + cardHeight);
            var clickable = !string.IsNullOrEmpty(dayEvent.Url);
            var hovered = clickable && ImGui.IsMouseHoveringRect(cardMin, cardMax);

            ui.Card(drawList, cardMin, cardMax, CardRounding * scale);
            if (hovered)
            {
                Squircle.Fill(drawList, cardMin, cardMax, CardRounding * scale, ImGui.GetColorU32(ui.HoverTint));
            }

            var accentMin = new Vector2(cardMin.X + AccentBarInset * scale, cardMin.Y + AccentBarInset * scale);
            var accentMax = new Vector2(accentMin.X + AccentBarWidth * scale, cardMax.Y - AccentBarInset * scale);
            var accentRounding = AccentBarWidth * 0.5f * scale;
            Squircle.Fill(drawList, accentMin, accentMax, accentRounding, ImGui.GetColorU32(dayEvent.Color));

            var textStartX = accentMax.X + CardPaddingX * scale;
            var textStartY = cardMin.Y + (cardHeight - textHeight) * 0.5f;
            Typography.Draw(drawList, new Vector2(textStartX, textStartY), dayEvent.Name, ui.TitleInk, hlScale,
                hlWeight);
            Typography.Draw(drawList, new Vector2(textStartX, textStartY + nameSize.Y + 3f * scale),
                FormatDateRange(dayEvent), ui.MutedInk, fnScale, fnWeight);

            if (dayEvent.IsCustom)
            {
                DrawDeleteButton(drawList, cardMax, scale, ui, dayEvent.CustomId, onDeleteCustom);
            }
            else if (clickable && UiInteract.HoverClick(cardMin, cardMax))
            {
                Dalamud.Utility.Util.OpenLink(dayEvent.Url);
            }

            cursorY = cardMax.Y + CardSpacing * scale;
        }

        cursorY += 8f * scale;
        return cursorY;
    }

    private static void DrawDeleteButton(ImDrawListPtr drawList, Vector2 cardMax, float scale, AppSkin ui,
        Guid customId, Action<Guid> onDeleteCustom)
    {
        var radius = 12f * scale;
        var center = new Vector2(cardMax.X - AccentBarInset * scale - radius, cardMax.Y - AccentBarInset * scale - radius + 2f * scale);
        var min = center - new Vector2(radius, radius);
        var max = center + new Vector2(radius, radius);
        var hovered = ImGui.IsMouseHoveringRect(min, max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(ui.HoverTint), 24);
        }

        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var glyph = FontAwesomeIcon.Trash.ToIconString();
            var fontSize = ImGui.GetFontSize() * 0.72f;
            var size = ImGui.CalcTextSize(glyph) * 0.72f;
            drawList.AddText(UiBuilder.IconFont, fontSize, center - size * 0.5f, ImGui.GetColorU32(ui.MutedInk), glyph);
        }

        HoverTooltip.Show(new Rect(min, max), Loc.T(L.Calendar.DeleteEvent), HoverLabelSide.Above);

        if (UiInteract.HoverClick(min, max))
        {
            onDeleteCustom(customId);
        }
    }

    private static string FormatDateRange(in ParsedEvent dayEvent)
    {
        var begin = dayEvent.Begin;
        var end = dayEvent.End;

        if (dayEvent.IsCustom)
        {
            return string.Concat(begin.ToString("MMM d", Loc.Culture), ", ", begin.ToString("t", Loc.Culture));
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
