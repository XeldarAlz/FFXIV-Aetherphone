using Aetherphone.Core;
using Aetherphone.Core.Clock;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Clock;

internal sealed partial class ClockApp
{
    private const double EorzeaRate = 144.0 / 7.0;
    private const float WorldRowHeight = 74f;
    private const float CardRounding = 18f;
    private const float RowPadding = 14f;

    private void DrawWorld(Rect body, float scale)
    {
        using (AppSurface.Begin(body))
        {
            var available = ImGui.GetContentRegionAvail().Y;
            var spacer = 14f * scale;
            var fixedRows = 2f * WorldRowHeight * scale;
            var heroHeight = Math.Clamp(available - fixedRows - spacer, 132f * scale, 200f * scale);
            DrawHero(heroHeight, scale);
            ImGui.Dummy(new Vector2(0f, spacer));

            var cities = configuration.WorldClocks;
            var rowCount = 2 + cities.Count;
            var card = BeginRowCard(rowCount, WorldRowHeight, scale);

            var eorzea = EorzeaTime.Now();
            var eorzeaSeconds = (float)(EorzeaSeconds() % 60.0);
            DrawWorldRow(RowAt(card, 0, WorldRowHeight, scale), "Eorzea", Loc.T(L.Clock.InGame), eorzea.Formatted,
                eorzea.Hour, eorzea.Minute, eorzeaSeconds);

            var utc = DateTime.UtcNow;
            var utcSeconds = utc.Second + utc.Millisecond / 1000f;
            DrawWorldRow(RowAt(card, 1, WorldRowHeight, scale), Loc.T(L.Clock.Server), "UTC", TimeText.Clock(utc),
                utc.Hour, utc.Minute, utcSeconds);

            for (var index = 0; index < cities.Count; index++)
            {
                DrawCityRow(RowAt(card, index + 2, WorldRowHeight, scale), cities[index]);
            }

            EndRowCard(card, 10f, scale);
        }
    }

    private void DrawHero(float heroHeight, float scale)
    {
        var local = DateTime.Now;
        var localSeconds = local.Second + local.Millisecond / 1000f;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var heroMin = origin;
        var heroMax = new Vector2(origin.X + width, origin.Y + heroHeight);
        var rounding = 24f * scale;
        ui.Card(drawList, heroMin, heroMax, rounding, elevated: true);

        var pad = 20f * scale;
        var minTextColumn = 100f * scale;
        var radiusFromHeight = (heroHeight - pad * 2f) * 0.5f;
        var radiusFromWidth = MathF.Max(30f * scale, width - pad * 2f - 22f * scale - minTextColumn);
        var clockRadius = MathF.Min(radiusFromHeight, radiusFromWidth);
        var clockCenter = new Vector2(heroMin.X + pad + clockRadius, heroMin.Y + heroHeight * 0.5f);
        ProgressRing.Glow(clockCenter, clockRadius * 0.92f, theme.Accent, 0.45f);
        AnalogClock.Draw(clockCenter, clockRadius, local.Hour, local.Minute, localSeconds, theme);

        var textX = clockCenter.X + clockRadius + 22f * scale;
        var textMaxWidth = MathF.Max(1f, heroMax.X - pad - textX);
        var digital = Typography.FitText(TimeText.Clock(local), textMaxWidth, TextStyles.LargeTitle);
        var date = Typography.FitText(local.ToString("ddd d MMM", Loc.Culture), textMaxWidth, TextStyles.Subheadline);
        var zone = Typography.FitText($"{Loc.T(L.Clock.Local)} · {LocalOffsetLabel()}", textMaxWidth,
            TextStyles.FootnoteEmphasized);
        var digitalSize = Typography.Measure(digital, TextStyles.LargeTitle);
        var dateSize = Typography.Measure(date, TextStyles.Subheadline);
        var zoneSize = Typography.Measure(zone, TextStyles.FootnoteEmphasized);
        var stackHeight = digitalSize.Y + 6f * scale + dateSize.Y + 4f * scale + zoneSize.Y;
        var startY = clockCenter.Y - stackHeight * 0.5f;
        Typography.Draw(new Vector2(textX, startY), digital, ui.TitleInk, TextStyles.LargeTitle);
        Typography.Draw(new Vector2(textX, startY + digitalSize.Y + 6f * scale), date, ui.MutedInk,
            TextStyles.Subheadline);
        Typography.Draw(new Vector2(textX, startY + digitalSize.Y + dateSize.Y + 10f * scale), zone, ui.Accent,
            TextStyles.FootnoteEmphasized);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, heroHeight));
    }

    private void DrawWorldRow(Rect row, string name, string sublabel, string digital, float hours, float minutes,
        float seconds)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dialRadius = (row.Height - 22f * scale) * 0.5f;
        var dialCenter = new Vector2(row.Min.X + dialRadius, row.Center.Y);
        AnalogClock.Draw(dialCenter, dialRadius, hours, minutes, seconds, theme);
        var textLeft = dialCenter.X + dialRadius + 16f * scale;
        var digitalSize = Typography.Measure(digital, TextStyles.Title1);
        var textMaxWidth = MathF.Max(1f, row.Max.X - 10f * scale - digitalSize.X - textLeft);
        var clippedName = Typography.FitText(name, textMaxWidth, TextStyles.Headline);
        var clippedSublabel = Typography.FitText(sublabel, textMaxWidth, TextStyles.Footnote);
        Typography.Draw(new Vector2(textLeft, row.Center.Y - 17f * scale), clippedName, ui.TitleInk,
            TextStyles.Headline);
        Typography.Draw(new Vector2(textLeft, row.Center.Y + 4f * scale), clippedSublabel, ui.MutedInk,
            TextStyles.Footnote);
        Typography.Draw(new Vector2(row.Max.X - digitalSize.X, row.Center.Y - digitalSize.Y * 0.5f), digital,
            ui.TitleInk, TextStyles.Title1);
    }

    private void DrawCityRow(Rect row, WorldClockEntry entry)
    {
        if (!WorldClockCatalog.TryResolve(entry.TimeZoneId, out var zone))
        {
            DrawWorldRow(row, entry.City, entry.TimeZoneId, "--:--", 0f, 0f, 0f);
            return;
        }

        var cityNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone);
        var citySeconds = cityNow.Second + cityNow.Millisecond / 1000f;
        DrawWorldRow(row, entry.City, CityOffsetLabel(zone, cityNow), TimeText.Clock(cityNow), cityNow.Hour,
            cityNow.Minute, citySeconds);
    }

    private void DrawCityPicker(Rect content, float scale)
    {
        var context = new PhoneContext(content, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Clock.AddCity), back);
        var body = new Rect(new Vector2(content.Min.X, content.Min.Y + AppHeader.Height * scale), content.Max);
        using (AppSurface.Begin(body))
        {
            var catalog = WorldClockCatalog.All;
            var card = BeginRowCard(catalog.Count, Metrics.Size.Row + 12f, scale);
            for (var index = 0; index < catalog.Count; index++)
            {
                DrawCityOption(RowAt(card, index, Metrics.Size.Row + 12f, scale), catalog[index]);
            }

            EndRowCard(card, 10f, scale);
        }
    }

    private void DrawCityOption(Rect row, WorldCity city)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var added = configuration.WorldClocks.Exists(entry => entry.TimeZoneId == city.TimeZoneId &&
                                                              entry.City == city.City);
        var hovering = ImGui.IsMouseHoveringRect(row.Min, row.Max);
        var textMaxWidth = MathF.Max(1f, row.Max.X - 34f * scale - row.Min.X);
        Marquee.DrawLeft("clock.cityPicker.name." + city.City, city.City, row.Min.X, row.Center.Y - 16f * scale,
            textMaxWidth, TextStyles.Headline, ui.TitleInk, hovering);
        if (WorldClockCatalog.TryResolve(city.TimeZoneId, out var zone))
        {
            var cityNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone);
            var offsetLabel = Typography.FitText(CityOffsetLabel(zone, cityNow), textMaxWidth, TextStyles.Footnote);
            Typography.Draw(new Vector2(row.Min.X, row.Center.Y + 4f * scale), offsetLabel,
                ui.MutedInk, TextStyles.Footnote);
        }

        var drawList = ImGui.GetWindowDrawList();
        var iconCenter = new Vector2(row.Max.X - 12f * scale, row.Center.Y);
        if (added)
        {
            drawList.AddCircleFilled(iconCenter, 11f * scale, ImGui.GetColorU32(ui.Accent), 24);
            var check = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f));
            drawList.AddLine(iconCenter + new Vector2(-4.6f * scale, 0f), iconCenter + new Vector2(-1f * scale, 4f * scale),
                check, 2f * scale);
            drawList.AddLine(iconCenter + new Vector2(-1f * scale, 4f * scale), iconCenter + new Vector2(5f * scale, -4f * scale),
                check, 2f * scale);
        }
        else
        {
            drawList.AddCircle(iconCenter, 11f * scale, ImGui.GetColorU32(ui.MutedInk), 24, 1.6f * scale);
        }

        if (UiInteract.HoverClick(row.Min, row.Max))
        {
            ToggleCity(city, added);
        }
    }

    private void ToggleCity(WorldCity city, bool added)
    {
        if (added)
        {
            configuration.WorldClocks.RemoveAll(entry => entry.TimeZoneId == city.TimeZoneId && entry.City == city.City);
        }
        else
        {
            configuration.WorldClocks.Add(new WorldClockEntry { TimeZoneId = city.TimeZoneId, City = city.City });
        }

        configuration.Save();
    }

    private Rect BeginRowCard(int rowCount, float rowHeight, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var card = new Rect(origin, origin + new Vector2(width, rowCount * rowHeight * scale));
        ui.Card(ImGui.GetWindowDrawList(), card.Min, card.Max, CardRounding * scale, elevated: true);
        return card;
    }

    private Rect RowAt(Rect card, int index, float rowHeight, float scale)
    {
        var top = card.Min.Y + index * rowHeight * scale;
        if (index > 0)
        {
            var separatorX = card.Min.X + RowPadding * 2f * scale;
            ImGui.GetWindowDrawList().AddLine(new Vector2(separatorX, top), new Vector2(card.Max.X - RowPadding * scale, top),
                ImGui.GetColorU32(ui.Palette.CardStroke), 1f);
        }

        return new Rect(new Vector2(card.Min.X + RowPadding * scale, top),
            new Vector2(card.Max.X - RowPadding * scale, top + rowHeight * scale));
    }

    private void EndRowCard(Rect card, float extraGap, float scale)
    {
        ImGui.SetCursorScreenPos(card.Min);
        ImGui.Dummy(new Vector2(card.Width, card.Height + extraGap * scale));
    }

    private static string CityOffsetLabel(TimeZoneInfo zone, DateTime cityNow)
    {
        var day = RelativeDayLabel(cityNow.Date);
        var diff = zone.GetUtcOffset(DateTime.UtcNow) - TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow);
        var sign = diff < TimeSpan.Zero ? "-" : "+";
        var magnitude = diff < TimeSpan.Zero ? diff.Negate() : diff;
        var offset = magnitude.Minutes == 0
            ? $"{sign}{magnitude.Hours}HR"
            : $"{sign}{magnitude.Hours}:{magnitude.Minutes:D2}";
        return diff == TimeSpan.Zero ? day : $"{day}, {offset}";
    }

    private static string RelativeDayLabel(DateTime day)
    {
        var today = DateTime.Today;
        if (day == today)
        {
            return Loc.T(L.Clock.DayToday);
        }

        if (day == today.AddDays(1))
        {
            return Loc.T(L.Clock.DayTomorrow);
        }

        if (day == today.AddDays(-1))
        {
            return Loc.T(L.Clock.DayYesterday);
        }

        return day.ToString("ddd", Loc.Culture);
    }

    private static double EorzeaSeconds() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0 * EorzeaRate;

    private static string LocalOffsetLabel()
    {
        var offset = DateTimeOffset.Now.Offset;
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        return offset.Minutes == 0
            ? $"UTC{sign}{Math.Abs(offset.Hours)}"
            : $"UTC{sign}{Math.Abs(offset.Hours)}:{Math.Abs(offset.Minutes):D2}";
    }
}
