using Aetherphone.Apps.Clock.Widgets;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Clock;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Clock;

internal sealed partial class ClockApp
{
    private const float CityRowHeight = 58f;
    private const float CitySearchGap = 12f;
    private const float CityMarkRadius = 11f;
    private const float CityTextGap = 10f;
    private const int CityQueryMaxLength = 40;
    private const float EmptyAreaUnits = 320f;

    private readonly CachedText[] cityDetails = new CachedText[WorldClockCatalog.All.Count];
    private string cityQuery = string.Empty;

    private void DrawCityPicker(in PhoneContext context)
    {
        var scale = UiScale.Current;
        var navBar = AppHeader.BeginLargeTitle(context);
        using (UiScope.PushId("clock.cities"))
        using (AppSurface.Begin(navBar.Body))
        {
            var width = ImGui.GetContentRegionAvail().X;
            DrawCitySearch(width, scale);
            DrawCityList(width, scale);
            ImGui.Dummy(new Vector2(0f, ClockArt.BottomPad * scale));
        }

        AppHeader.EndLargeTitle(in navBar, context, "clock.cities.nav", Loc.T(L.Clock.ChooseCity),
            NavBarStyle.From(ui), ReadOnlySpan<NavBarButton>.Empty, Loc.T(L.Clock.TabWorld), back);
    }

    private void DrawCitySearch(float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var height = GlassField.HeightUnits * scale;
        var field = new Rect(origin, origin + new Vector2(width, height));
        var drawList = ImGui.GetWindowDrawList();
        SearchBar.Surface(drawList, field, ControlInk.From(theme));
        GlassField.Search(drawList, field, "##clockCitySearch", Loc.T(L.Clock.SearchCities), ref cityQuery, theme,
            scale, CityQueryMaxLength, false);
        ClockArt.Advance(origin, width, height, CitySearchGap, scale);
    }

    private void DrawCityList(float width, float scale)
    {
        var catalog = WorldClockCatalog.All;
        var query = cityQuery.AsSpan().Trim();
        var matches = 0;
        for (var index = 0; index < catalog.Count; index++)
        {
            if (Matches(catalog[index], query))
            {
                matches++;
            }
        }

        var origin = ImGui.GetCursorScreenPos();
        if (matches == 0)
        {
            var area = new Rect(origin, origin + new Vector2(width, EmptyAreaUnits * scale));
            EmptyState.Draw(area, ui, FontAwesomeIcon.Globe, Loc.T(L.Clock.NoCityMatches),
                Loc.T(L.Clock.NoCityMatchesHint));
            ClockArt.Advance(origin, width, area.Height, 0f, scale);
            return;
        }

        var rowHeight = CityRowHeight * scale;
        var max = new Vector2(origin.X + width, origin.Y + matches * rowHeight);
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, origin, max, Metrics.Radius.Grouped * scale);
        var utcNow = DateTime.UtcNow;
        var row = 0;
        for (var index = 0; index < catalog.Count; index++)
        {
            var city = catalog[index];
            if (!Matches(city, query))
            {
                continue;
            }

            var rect = RowRect(origin, width, row * rowHeight, rowHeight);
            if (row > 0)
            {
                FeedCell.Hairline(drawList, origin.X + Metrics.Space.Lg * scale, max.X, rect.Min.Y, ui.Hairline);
            }

            DrawCityOption(drawList, rect, index, city, utcNow, scale);
            row++;
        }

        ClockArt.Advance(origin, width, max.Y - origin.Y, 0f, scale);
    }

    private static bool Matches(in WorldCity city, ReadOnlySpan<char> query) =>
        query.Length == 0 || city.City.AsSpan().Contains(query, StringComparison.OrdinalIgnoreCase);

    private void DrawCityOption(ImDrawListPtr drawList, Rect row, int index, in WorldCity city, DateTime utcNow,
        float scale)
    {
        if (!ImGui.IsRectVisible(row.Min, row.Max))
        {
            return;
        }

        var added = WorldClockCatalog.Contains(configuration.WorldClocks, city);
        var hovered = UiInteract.Hover(row.Min, row.Max);
        if (hovered)
        {
            drawList.AddRectFilled(row.Min, row.Max, ImGui.GetColorU32(ui.HoverWash));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var inset = Metrics.Space.Lg * scale;
        var left = row.Min.X + inset;
        var right = row.Max.X - inset;
        var markRadius = CityMarkRadius * scale;
        var markCenter = new Vector2(right - markRadius, row.Center.Y);
        var textWidth = MathF.Max(1f, markCenter.X - markRadius - CityTextGap * scale - left);
        var slot = new ClockSlot(ClockSlotKind.City, city.City, ClockZones.Resolve(city.TimeZoneId));
        var reading = ClockZones.Read(slot, utcNow);
        var detail = ClockZones.Detail(ref cityDetails[index], slot, reading);
        var nameHeight = Typography.LineHeight(TextStyles.Headline);
        var detailHeight = Typography.LineHeight(TextStyles.Footnote);
        var top = row.Center.Y - (nameHeight + detailHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(left, top), Typography.FitText(city.City, textWidth, TextStyles.Headline),
            ui.TitleInk, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(left, top + nameHeight),
            Typography.FitText(detail, textWidth, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
        if (added)
        {
            drawList.AddCircleFilled(markCenter, markRadius, ImGui.GetColorU32(ui.Accent), 24);
            PhoneIcon.Draw(drawList, markCenter, PhoneIcons.Check, ClockArt.White, markRadius * 1.3f);
        }
        else
        {
            drawList.AddCircle(markCenter, markRadius, ImGui.GetColorU32(ui.MutedInk), 24, 1.6f * scale);
        }

        if (!UiInteract.Click(row.Min, row.Max, hovered))
        {
            return;
        }

        if (added)
        {
            RemoveCity(city);
            return;
        }

        configuration.WorldClocks.Add(new WorldClockEntry { TimeZoneId = city.TimeZoneId, City = city.City });
        configuration.Save();
        UiFeedback.Play(UiSound.Success);
        router.Pop();
    }

    private void RemoveCity(in WorldCity city)
    {
        var cities = configuration.WorldClocks;
        for (var index = cities.Count - 1; index >= 0; index--)
        {
            if (string.Equals(cities[index].City, city.City, StringComparison.Ordinal) &&
                string.Equals(cities[index].TimeZoneId, city.TimeZoneId, StringComparison.Ordinal))
            {
                cities.RemoveAt(index);
            }
        }

        configuration.Save();
    }
}
