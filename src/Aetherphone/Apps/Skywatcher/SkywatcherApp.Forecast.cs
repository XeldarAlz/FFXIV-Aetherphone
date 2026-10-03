using Aetherphone.Core;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Skywatcher;

internal sealed partial class SkywatcherApp
{
    private const int HourlyCount = 6;
    private const int OccurrenceHorizonWindows = 220;
    private const int BellsPerDay = 24;
    private const int BellsPerWindow = 8;
    private const int SunArcSegments = 36;
    private const float RealSecondsPerWindow = 1400f;
    private const float RealSecondsPerBell = 175f;
    private const float CardGapUnits = 12f;
    private const float RowUnits = 44f;
    private const float OddsRowUnits = 50f;
    private const float TileUnits = 158f;
    private const float DawnBell = 6f;
    private const float DuskBell = 19f;
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private readonly string[] hourTopLabels = new string[WindowCount];
    private readonly string[] hourLabels = new string[WindowCount];
    private readonly List<WeatherOdds> odds = new();
    private readonly List<string> oddsPercent = new();
    private readonly List<string> oddsNext = new();
    private string summaryText = string.Empty;
    private string sunClock = string.Empty;
    private string sunCaption = string.Empty;
    private string sunOther = string.Empty;
    private string windowLeft = string.Empty;
    private string windowThen = string.Empty;

    private uint ForecastTerritory => viewedTerritory == 0 ? weather.CurrentTerritory : viewedTerritory;

    private void RefreshForecastText()
    {
        summaryText = Summary();
        for (var index = 0; index < forecast.Count; index++)
        {
            var window = forecast[index];
            hourTopLabels[index] = ShortWhen(window);
            hourLabels[index] = window.IsCurrent ? Loc.T(L.Skywatcher.Now) : HourText(window.StartBell);
        }

        if (forecast.Count > 1)
        {
            windowLeft = Loc.T(L.Time.MinutesShort, Math.Max(1, forecast[1].MinutesFromNow));
            windowThen = Loc.T(L.Skywatcher.Then, forecast[1].Weather.Name);
        }

        RefreshSun();
    }

    private void RefreshDetails()
    {
        oddsPercent.Clear();
        oddsNext.Clear();
        var territory = ForecastTerritory;
        weather.Odds(territory, odds);
        for (var index = 0; index < odds.Count; index++)
        {
            var entry = odds[index];
            oddsPercent.Add(Loc.T(L.Skywatcher.Percent, entry.Percent));
            var minutes = weather.MinutesUntil(territory, entry.Weather.Id, OccurrenceHorizonWindows);
            oddsNext.Add(minutes < 0
                ? Loc.T(L.Skywatcher.NotSoon)
                : minutes == 0
                    ? Loc.T(L.Skywatcher.Now)
                    : LongWhenMinutes(minutes));
        }
    }

    private void RefreshSun()
    {
        var now = EorzeaTime.Now();
        var bell = now.Hour + now.Minute / 60f;
        var nextIsDawn = bell < DawnBell || bell >= DuskBell;
        var nextBell = nextIsDawn ? DawnBell : DuskBell;
        var untilBells = nextBell - bell;
        if (untilBells < 0f)
        {
            untilBells += BellsPerDay;
        }

        var realMinutes = (int)MathF.Ceiling(untilBells * RealSecondsPerBell / 60f);
        sunClock = TimeText.Clock(new DateTime(1, 1, 1, (int)nextBell, 0, 0));
        var label = Loc.T(nextIsDawn ? L.Skywatcher.Dawn : L.Skywatcher.Dusk);
        sunCaption = $"{label} · {LongWhenMinutes(realMinutes)}";
        var otherLabel = Loc.T(nextIsDawn ? L.Skywatcher.Dusk : L.Skywatcher.Dawn);
        var otherBell = (int)(nextIsDawn ? DuskBell : DawnBell);
        sunOther = $"{otherLabel} {TimeText.Clock(new DateTime(1, 1, 1, otherBell, 0, 0))}";
    }

    private void DrawForecast(in SkyPalette palette, float scale)
    {
        var width = ImGui.GetContentRegionAvail().X;
        DrawHourlyCard(width, palette, scale);
        Gap(scale);
        DrawForecastCard(width, palette, scale);
        Gap(scale);
        DrawTiles(width, palette, scale);
        if (odds.Count == 0)
        {
            return;
        }

        Gap(scale);
        DrawOddsCard(width, palette, scale);
    }

    private static void Gap(float scale) => ImGui.Dummy(new Vector2(0f, CardGapUnits * scale));

    private void DrawHourlyCard(float width, in SkyPalette palette, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var padding = WeatherCard.PaddingUnits * scale;
        var textWidth = width - padding * 2f;
        var summaryHeight = Typography.MeasureWrappedBlock(summaryText, TextStyles.Subheadline, textWidth).Y;
        var columnsTop = padding + summaryHeight + 18f * scale;
        var height = columnsTop + 84f * scale;
        var card = new Rect(origin, origin + new Vector2(width, height));
        if (UiAnchors.Recording)
        {
            UiAnchors.Report("skywatcher.forecast", card);
        }

        WeatherCard.Panel(drawList, card, palette, sky.Density, scale);
        Typography.DrawWrappedLeft(new Vector2(card.Min.X + padding, card.Min.Y + padding), summaryText, palette.Ink,
            TextStyles.Subheadline, textWidth);
        WeatherCard.Divider(drawList, card, card.Min.Y + columnsTop - 8f * scale, palette, scale);
        var count = Math.Min(forecast.Count, HourlyCount);
        var columnWidth = textWidth / count;
        var behind = WeatherCard.Behind(palette, sky.Density);
        for (var index = 0; index < count; index++)
        {
            var window = forecast[index];
            var centerX = card.Min.X + padding + columnWidth * (index + 0.5f);
            var maxWidth = MathF.Max(1f, columnWidth - 2f * scale);
            var topStyle = window.IsCurrent ? TextStyles.FootnoteEmphasized : TextStyles.Footnote;
            Typography.DrawCentered(drawList, new Vector2(centerX, card.Min.Y + columnsTop + 10f * scale),
                Typography.FitText(hourTopLabels[index], maxWidth, topStyle), palette.Ink, topStyle.Scale,
                topStyle.Weight);
            var kind = WeatherSky.Classify(window.Weather.EnglishKey);
            WeatherCard.Glyph(drawList, kind, new Vector2(centerX, card.Min.Y + columnsTop + 40f * scale),
                MathF.Min(13f * scale, columnWidth * 0.34f), IsDayWindow(window), behind);
            var bottomStyle = TextStyles.SubheadlineEmphasized;
            Typography.DrawCentered(drawList, new Vector2(centerX, card.Min.Y + columnsTop + 68f * scale),
                Typography.FitText(hourLabels[index], maxWidth, bottomStyle), palette.Ink, bottomStyle.Scale,
                bottomStyle.Weight);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawForecastCard(float width, in SkyPalette palette, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var padding = WeatherCard.PaddingUnits * scale;
        var rowHeight = RowUnits * scale;
        var headerHeight = WeatherCard.HeaderUnits * scale;
        var height = headerHeight + forecast.Count * rowHeight + 4f * scale;
        var card = new Rect(origin, origin + new Vector2(width, height));
        WeatherCard.Panel(drawList, card, palette, sky.Density, scale);
        WeatherCard.Header(drawList, card, FontAwesomeIcon.CalendarAlt, Loc.T(L.Skywatcher.Forecast), palette, scale);
        var labelWidth = 62f * scale;
        var barWidth = 80f * scale;
        var glyphX = card.Min.X + padding + labelWidth + 12f * scale;
        var nameX = glyphX + 22f * scale;
        var barMinX = card.Max.X - padding - barWidth;
        var behind = WeatherCard.Behind(palette, sky.Density);
        var now = EorzeaTime.Now();
        var nowBell = now.Hour + now.Minute / 60f;
        for (var index = 0; index < forecast.Count; index++)
        {
            var window = forecast[index];
            var rowTop = card.Min.Y + headerHeight + index * rowHeight;
            var centerY = rowTop + rowHeight * 0.5f;
            if (index > 0)
            {
                WeatherCard.Divider(drawList, card, rowTop, palette, scale);
            }

            var labelStyle = window.IsCurrent ? TextStyles.BodyEmphasized : TextStyles.Body;
            var label = Typography.FitText(hourLabels[index], labelWidth, labelStyle);
            var labelHeight = Typography.LineHeight(labelStyle);
            Typography.Draw(drawList, new Vector2(card.Min.X + padding, centerY - labelHeight * 0.5f), label,
                palette.Ink, labelStyle);
            var kind = WeatherSky.Classify(window.Weather.EnglishKey);
            var isDay = IsDayWindow(window);
            WeatherCard.Glyph(drawList, kind, new Vector2(glyphX, centerY), 12f * scale, isDay, behind);
            var name = Typography.FitText(window.Weather.Name, MathF.Max(1f, barMinX - 10f * scale - nameX),
                TextStyles.Body);
            Typography.Draw(drawList, new Vector2(nameX, centerY - Typography.LineHeight(TextStyles.Body) * 0.5f),
                name, palette.Ink, TextStyles.Body);
            DrawDayBar(drawList, new Rect(new Vector2(barMinX, centerY - 2.5f * scale),
                new Vector2(card.Max.X - padding, centerY + 2.5f * scale)), window, kind, isDay,
                window.IsCurrent ? nowBell : -1f, palette, scale);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private static void DrawDayBar(ImDrawListPtr drawList, Rect track, WeatherWindow window, WeatherKind kind,
        bool isDay, float markerBell, in SkyPalette palette, float scale)
    {
        var radius = track.Height * 0.5f;
        drawList.AddRectFilled(track.Min, track.Max, ImGui.GetColorU32(palette.Ink with { W = 0.16f }), radius);
        var start = track.Min.X + track.Width * window.StartBell / BellsPerDay;
        var end = track.Min.X + track.Width * (window.StartBell + BellsPerWindow) / BellsPerDay;
        var tint = WeatherSky.Resolve(kind, isDay).Glow;
        var left = ImGui.GetColorU32(Vector4.Lerp(tint, White, 0.25f));
        var right = ImGui.GetColorU32(tint);
        Squircle.FillHorizontalGradient(drawList, new Vector2(start, track.Min.Y), new Vector2(end, track.Max.Y),
            radius, left, right);
        if (markerBell < 0f)
        {
            return;
        }

        var markerCenter = new Vector2(track.Min.X + track.Width * markerBell / BellsPerDay, track.Center.Y);
        drawList.AddCircleFilled(markerCenter, 5.5f * scale, ImGui.GetColorU32(palette.Top with { W = 1f }), 20);
        drawList.AddCircleFilled(markerCenter, 3.5f * scale, ImGui.GetColorU32(White), 20);
    }

    private void DrawTiles(float width, in SkyPalette palette, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var gap = CardGapUnits * scale;
        var tileWidth = (width - gap) * 0.5f;
        var height = TileUnits * scale;
        var drawList = ImGui.GetWindowDrawList();
        DrawSunTile(drawList, new Rect(origin, origin + new Vector2(tileWidth, height)), palette, scale);
        if (forecast.Count > 1)
        {
            var windowMin = origin + new Vector2(tileWidth + gap, 0f);
            DrawWindowTile(drawList, new Rect(windowMin, windowMin + new Vector2(tileWidth, height)), palette, scale);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawSunTile(ImDrawListPtr drawList, Rect tile, in SkyPalette palette, float scale)
    {
        WeatherCard.Panel(drawList, tile, palette, sky.Density, scale);
        var header = WeatherCard.Header(drawList, tile, FontAwesomeIcon.Sun, Loc.T(L.Skywatcher.Sun), palette, scale);
        var padding = WeatherCard.PaddingUnits * scale;
        var innerWidth = tile.Width - padding * 2f;
        var clockStyle = TextStyles.Title1 with { Weight = FontWeight.Regular };
        var clock = Typography.FitText(sunClock, innerWidth, clockStyle);
        var top = tile.Min.Y + header + 6f * scale;
        Typography.Draw(drawList, new Vector2(tile.Min.X + padding, top), clock, palette.Ink, clockStyle);
        top += Typography.LineHeight(clockStyle);
        Typography.Draw(drawList, new Vector2(tile.Min.X + padding, top),
            Typography.FitText(sunCaption, innerWidth, TextStyles.Footnote), palette.InkSoft, TextStyles.Footnote);
        var arcTop = top + Typography.LineHeight(TextStyles.Footnote) + 4f * scale;
        var arcBottom = tile.Max.Y - padding - Typography.LineHeight(TextStyles.Caption1);
        DrawSunArc(drawList, new Rect(new Vector2(tile.Min.X + padding, arcTop),
            new Vector2(tile.Max.X - padding, arcBottom)), palette, scale);
        Typography.Draw(drawList, new Vector2(tile.Min.X + padding, arcBottom + 2f * scale),
            Typography.FitText(sunOther, innerWidth, TextStyles.Caption1), palette.InkSoft, TextStyles.Caption1);
    }

    private static void DrawSunArc(ImDrawListPtr drawList, Rect area, in SkyPalette palette, float scale)
    {
        if (area.Height < 8f * scale)
        {
            return;
        }

        var horizonY = area.Min.Y + area.Height * 0.62f;
        var amplitude = area.Height * 0.45f;
        drawList.AddLine(new Vector2(area.Min.X, horizonY), new Vector2(area.Max.X, horizonY),
            ImGui.GetColorU32(palette.Ink with { W = 0.22f }), 1f * scale);
        var previous = SunPoint(area, horizonY, amplitude, 0f);
        for (var segment = 1; segment <= SunArcSegments; segment++)
        {
            var bell = BellsPerDay * segment / (float)SunArcSegments;
            var point = SunPoint(area, horizonY, amplitude, bell);
            var above = point.Y <= horizonY && previous.Y <= horizonY;
            drawList.AddLine(previous, point, ImGui.GetColorU32(palette.Ink with { W = above ? 0.55f : 0.18f }),
                (above ? 1.6f : 1.2f) * scale);
            previous = point;
        }

        var now = EorzeaTime.Now();
        var sun = SunPoint(area, horizonY, amplitude, now.Hour + now.Minute / 60f);
        var day = sun.Y <= horizonY;
        var glow = day ? new Vector4(1f, 0.86f, 0.44f, 1f) : new Vector4(0.80f, 0.86f, 1f, 1f);
        WeatherAmbience.Glow(drawList, sun, 11f * scale, glow, 0.85f);
        drawList.AddCircleFilled(sun, 4f * scale, ImGui.GetColorU32(day ? glow : White), 20);
    }

    private static Vector2 SunPoint(Rect area, float horizonY, float amplitude, float bell)
    {
        var fraction = bell / BellsPerDay;
        var height = -MathF.Cos((bell - 0.5f) / BellsPerDay * MathF.PI * 2f);
        return new Vector2(area.Min.X + area.Width * fraction, horizonY - height * amplitude);
    }

    private void DrawWindowTile(ImDrawListPtr drawList, Rect tile, in SkyPalette palette, float scale)
    {
        WeatherCard.Panel(drawList, tile, palette, sky.Density, scale);
        var header = WeatherCard.Header(drawList, tile, FontAwesomeIcon.HourglassHalf, Loc.T(L.Skywatcher.ThisWindow),
            palette, scale);
        var padding = WeatherCard.PaddingUnits * scale;
        var captionHeight = Typography.LineHeight(TextStyles.Footnote);
        var ringArea = new Rect(new Vector2(tile.Min.X, tile.Min.Y + header),
            new Vector2(tile.Max.X, tile.Max.Y - padding - captionHeight));
        var radius = MathF.Min(ringArea.Width, ringArea.Height) * 0.36f;
        var center = ringArea.Center;
        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
        var elapsed = (float)(nowUnix % RealSecondsPerWindow / RealSecondsPerWindow);
        var thickness = 5f * scale;
        ProgressRing.Track(drawList, center, radius, thickness, palette.Ink with { W = 0.16f });
        ProgressRing.Fill(drawList, center, radius, thickness, 1f - elapsed, palette.Ink);
        Typography.DrawCentered(drawList, center, windowLeft, palette.Ink, TextStyles.Title3.Scale,
            TextStyles.Title3.Weight);
        var innerWidth = tile.Width - padding * 2f;
        Typography.DrawCentered(drawList, new Vector2(tile.Center.X, tile.Max.Y - padding - captionHeight * 0.5f),
            Typography.FitText(windowThen, innerWidth, TextStyles.Footnote), palette.InkSoft, TextStyles.Footnote.Scale,
            TextStyles.Footnote.Weight);
    }

    private void DrawOddsCard(float width, in SkyPalette palette, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var padding = WeatherCard.PaddingUnits * scale;
        var rowHeight = OddsRowUnits * scale;
        var headerHeight = WeatherCard.HeaderUnits * scale;
        var height = headerHeight + odds.Count * rowHeight + 2f * scale;
        var card = new Rect(origin, origin + new Vector2(width, height));
        WeatherCard.Panel(drawList, card, palette, sky.Density, scale);
        WeatherCard.Header(drawList, card, FontAwesomeIcon.ChartBar, Loc.T(L.Skywatcher.WeatherOdds), palette, scale);
        var behind = WeatherCard.Behind(palette, sky.Density);
        var rightColumn = 86f * scale;
        var nameX = card.Min.X + padding + 30f * scale;
        var barMaxX = card.Max.X - padding - rightColumn;
        var bodyHeight = Typography.LineHeight(TextStyles.Body);
        var isDay = WeatherSky.Daylight(EorzeaTime.Now().Hour) >= 0.5f;
        for (var index = 0; index < odds.Count; index++)
        {
            var entry = odds[index];
            var rowTop = card.Min.Y + headerHeight + index * rowHeight;
            var centerY = rowTop + rowHeight * 0.5f;
            if (index > 0)
            {
                WeatherCard.Divider(drawList, card, rowTop, palette, scale);
            }

            var kind = WeatherSky.Classify(entry.Weather.EnglishKey);
            WeatherCard.Glyph(drawList, kind, new Vector2(card.Min.X + padding + 11f * scale, centerY), 11f * scale,
                isDay, behind);
            var nameTop = centerY - (bodyHeight + 8f * scale) * 0.5f;
            Typography.Draw(drawList, new Vector2(nameX, nameTop),
                Typography.FitText(entry.Weather.Name, MathF.Max(1f, barMaxX - nameX), TextStyles.Body), palette.Ink,
                TextStyles.Body);
            var barTop = nameTop + bodyHeight + 2f * scale;
            var barMax = new Vector2(barMaxX, barTop + 4f * scale);
            drawList.AddRectFilled(new Vector2(nameX, barTop), barMax,
                ImGui.GetColorU32(palette.Ink with { W = 0.14f }), 2f * scale);
            var fill = nameX + (barMaxX - nameX) * Math.Clamp(entry.Percent / 100f, 0.02f, 1f);
            drawList.AddRectFilled(new Vector2(nameX, barTop), new Vector2(fill, barMax.Y),
                ImGui.GetColorU32(WeatherSky.Resolve(kind, isDay).Glow), 2f * scale);
            var percent = oddsPercent[index];
            var percentWidth = Typography.Measure(percent, TextStyles.BodyEmphasized).X;
            var percentOrigin = new Vector2(card.Max.X - padding - percentWidth, centerY - bodyHeight + 2f * scale);
            Typography.Draw(drawList, percentOrigin, percent, palette.Ink, TextStyles.BodyEmphasized);
            var next = Typography.FitText(oddsNext[index], rightColumn - 6f * scale, TextStyles.Footnote);
            var nextWidth = Typography.Measure(next, TextStyles.Footnote).X;
            Typography.Draw(drawList, new Vector2(card.Max.X - padding - nextWidth, centerY + 1f * scale), next,
                palette.InkSoft, TextStyles.Footnote);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private string Summary()
    {
        if (forecast.Count < 2)
        {
            return forecast.Count == 1 ? Loc.T(L.Skywatcher.Continuing, forecast[0].Weather.Name) : string.Empty;
        }

        var current = forecast[0].Weather.Id;
        for (var index = 1; index < forecast.Count; index++)
        {
            if (forecast[index].Weather.Id != current)
            {
                return $"{forecast[index].Weather.Name} {LongWhen(forecast[index])}";
            }
        }

        return Loc.T(L.Skywatcher.ForNextHours, forecast[0].Weather.Name);
    }

    private static string HourText(int bell) => TimeText.HourClock(bell);

    private static string ShortWhen(WeatherWindow window)
    {
        if (window.IsCurrent || window.MinutesFromNow <= 0)
        {
            return Loc.T(L.Skywatcher.Now);
        }

        return Loc.T(L.Time.MinutesShort, window.MinutesFromNow);
    }

    private static string LongWhen(WeatherWindow window)
    {
        if (window.IsCurrent || window.MinutesFromNow <= 0)
        {
            return Loc.T(L.Time.Now);
        }

        return LongWhenMinutes(window.MinutesFromNow);
    }

    private static string LongWhenMinutes(int totalMinutes)
    {
        if (totalMinutes < 60)
        {
            return Loc.T(L.Time.InMinutes, totalMinutes);
        }

        var hours = totalMinutes / 60;
        var minutes = totalMinutes % 60;
        return minutes == 0 ? Loc.T(L.Time.InHours, hours) : Loc.T(L.Time.InHoursMinutes, hours, minutes);
    }
}
