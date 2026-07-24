using Aetherphone.Apps.Skywatcher;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Game;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Widgets;

internal sealed class SkywatcherWidget : IHomeWidget
{
    private const float RefreshIntervalSeconds = 5f;
    private const int ForecastWindows = 6;

    private const float StripTopPadPref = 18f;
    private const float StripTopPadMin = 8f;
    private const float StripBottomPadPref = 6f;
    private const float StripBottomPadMin = 3f;
    private const float StripLabelGapPref = 6f;
    private const float StripLabelGapMin = 3f;
    private const float StripIconRadiusPref = 12f;
    private const float StripIconRadiusMin = 5f;

    private readonly WeatherService weather;
    private readonly List<WeatherWindow> forecast = new();
    private string zone = string.Empty;
    private float sinceRefresh = RefreshIntervalSeconds;

    public SkywatcherWidget(WeatherService weather)
    {
        this.weather = weather;
    }

    public string Id => "skywatcher.forecast";
    public string DisplayName => Loc.T(L.Apps.Skywatcher);
    public string AppId => "skywatcher";
    public WidgetSizeSet Sizes => WidgetSizeSet.Small | WidgetSizeSet.Medium | WidgetSizeSet.Large;

    public void Draw(in WidgetContext context)
    {
        Advance(context.Delta);
        var bell = EorzeaTime.Now();
        var daylight = WeatherSky.Daylight(bell.Hour + bell.Minute / 60f);
        var isDay = daylight >= 0.5f;
        var kind = forecast.Count > 0 ? WeatherSky.Classify(forecast[0].Weather.EnglishKey) : WeatherKind.Clouds;
        var palette = WeatherSky.Blend(kind, forecast.Count > 0 ? daylight : 0f);
        WidgetChrome.Tinted(context.DrawList, context.Bounds, palette.Top, palette.Bottom, context.Scale,
            context.Opacity);
        if (forecast.Count == 0)
        {
            DrawEmpty(context, palette);
            return;
        }

        WeatherAmbience.Draw(context.DrawList, context.Bounds, WidgetChrome.Radius(context.Scale), kind, isDay,
            palette, context.Scale, context.Opacity, context.Size != WidgetSize.Small);
        switch (context.Size)
        {
            case WidgetSize.Small:
                DrawSmall(context, palette, kind, isDay, bell);
                break;
            case WidgetSize.Medium:
                DrawMedium(context, palette, bell);
                break;
            default:
                DrawLarge(context, palette, bell);
                break;
        }
    }

    private void Advance(float delta)
    {
        sinceRefresh += delta;
        if (sinceRefresh < RefreshIntervalSeconds)
        {
            return;
        }

        zone = weather.CurrentZone();
        weather.Forecast(forecast, ForecastWindows);
        sinceRefresh = 0f;
    }

    private void DrawSmall(in WidgetContext context, in SkyPalette palette, WeatherKind kind, bool isDay,
        EorzeaTime bell)
    {
        var bounds = context.Bounds;
        var scale = context.Scale;
        var opacity = context.Opacity;
        var drawList = context.DrawList;
        var pad = 13f * scale;

        var eyebrowY = bounds.Min.Y + pad;
        WidgetChrome.Eyebrow(drawList, new Vector2(bounds.Min.X + pad, eyebrowY), DisplayName,
            palette.InkSoft, scale, opacity);
        var eyebrowHeight = Typography.Measure(Loc.Culture.TextInfo.ToUpper(DisplayName), 0.66f, FontWeight.SemiBold).Y;

        var glyphRadius = 16f * scale;
        var rowTop = eyebrowY + eyebrowHeight + 6f * scale;
        var glyphCenter = new Vector2(bounds.Min.X + pad + 17f * scale, rowTop + glyphRadius);
        WeatherGlyph.Draw(kind, glyphCenter, glyphRadius, palette, isDay,
            Vector4.Lerp(palette.Top, palette.Bottom, 0.5f));
        WeatherAmbience.Halo(drawList, glyphCenter, 19f * scale, palette.Glow,
            (0.55f + 0.30f * Pulse.Wave(Pulse.Breath)) * opacity);
        var time = bell.Formatted;
        var timeSize = Typography.Measure(time, TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList, new Vector2(bounds.Max.X - pad - timeSize.X, glyphCenter.Y - timeSize.Y * 0.5f),
            time, Palette.WithAlpha(palette.InkSoft, opacity), TextStyles.FootnoteEmphasized);

        var weatherTop = rowTop + glyphRadius * 2f + 6f * scale;
        var weatherText = FitScaled(forecast[0].Weather.Name, bounds.Width - pad * 2f, 1.06f, FontWeight.SemiBold,
            out var weatherScale);
        var weatherSize = Typography.Measure(weatherText, weatherScale, FontWeight.SemiBold);
        Typography.Draw(drawList, new Vector2(bounds.Min.X + pad, weatherTop), weatherText,
            Palette.WithAlpha(palette.Ink, opacity), weatherScale, FontWeight.SemiBold);

        var zoneTop = weatherTop + weatherSize.Y + 4f * scale;
        var zoneMaxWidth = bounds.Width - pad * 2f;
        var zoneSize = Typography.Measure(zone, TextStyles.Footnote);
        if (zoneTop + zoneSize.Y <= bounds.Max.Y - pad)
        {
            Marquee.DrawLeftAuto("skywatcher.small.zone", zone, bounds.Min.X + pad, zoneTop, zoneMaxWidth,
                TextStyles.Footnote, Palette.WithAlpha(palette.InkSoft, opacity));
        }
    }

    private void DrawMedium(in WidgetContext context, in SkyPalette palette, EorzeaTime bell)
    {
        var bounds = context.Bounds;
        var scale = context.Scale;
        var opacity = context.Opacity;
        var drawList = context.DrawList;
        var pad = 16f * scale;
        var topInset = 12f * scale;
        var left = bounds.Min.X + pad;
        WidgetChrome.Eyebrow(drawList, new Vector2(left, bounds.Min.Y + topInset), DisplayName, palette.InkSoft, scale,
            opacity);
        var time = bell.Formatted;
        var eyebrowHeight = Typography.Measure(Loc.Culture.TextInfo.ToUpper(DisplayName), 0.66f, FontWeight.SemiBold).Y;
        var minTopOffset = topInset + eyebrowHeight + 10f * scale;
        var contentBottom = bounds.Max.Y - 4f * scale - StripHeight(bounds, scale) - 1f * scale;
        var availableForRow = contentBottom - bounds.Min.Y - minTopOffset;

        float RowHeightAt(float candidateScale) => MathF.Max(
            Typography.Measure("0:00", candidateScale, FontWeight.Medium).Y,
            Typography.Measure("Ag", candidateScale, FontWeight.SemiBold).Y);

        var roomy = RowHeightAt(2.0f) <= availableForRow;
        var heroScale = roomy ? 2.0f : 1.62f;
        var timeStyle = new TextStyle(heroScale, FontWeight.Medium);
        var timeSize = Typography.Measure(time, timeStyle);
        var eorzeaLabel = Loc.Culture.TextInfo.ToUpper(Loc.T(L.Home.Eorzea));
        var eorzeaScale = roomy ? 0.92f : 0.72f;
        var eorzeaTracking = 1.5f * scale;
        var eorzeaWidth = Typography.Measure(eorzeaLabel, eorzeaScale, FontWeight.SemiBold).X +
                          eorzeaTracking * Math.Max(0, eorzeaLabel.Length - 1);
        var rightColumn = MathF.Max(timeSize.X, eorzeaWidth) + pad;
        var rowHeight = RowHeightAt(heroScale);
        var roomConstrainedOffset = contentBottom - bounds.Min.Y - rowHeight;
        var conditionYOffset = MathF.Max(4f * scale, MathF.Min(minTopOffset, roomConstrainedOffset));
        var conditionY = bounds.Min.Y + conditionYOffset;
        var conditionStyle = new TextStyle(heroScale, FontWeight.SemiBold);
        var conditionMaxHeight = MathF.Max(0f, contentBottom - conditionY);
        var conditionHeight = DrawConditionText(drawList, forecast[0].Weather.Name, new Vector2(left, conditionY),
            conditionStyle, Palette.WithAlpha(palette.Ink, opacity), bounds.Width - rightColumn - pad * 2f,
            conditionMaxHeight);
        Typography.Draw(drawList, new Vector2(bounds.Max.X - pad - timeSize.X, conditionY), time,
            Palette.WithAlpha(palette.Ink, opacity), timeStyle);
        var eorzeaY = conditionY + timeSize.Y + 7f * scale;
        var eorzeaHeight = Typography.Measure(eorzeaLabel, eorzeaScale, FontWeight.SemiBold).Y;
        if (eorzeaY + eorzeaHeight <= contentBottom)
        {
            WidgetChrome.Tracked(drawList, new Vector2(bounds.Max.X - pad - eorzeaWidth, eorzeaY), eorzeaLabel,
                Palette.WithAlpha(palette.InkSoft, opacity), eorzeaScale, FontWeight.SemiBold, eorzeaTracking);
        }
        if (zone.Length > 0)
        {
            var lineTwoY = conditionY + conditionHeight * 1.15f + 4f * scale;
            var zoneSize = Typography.Measure(zone, TextStyles.Subheadline);
            if (lineTwoY + zoneSize.Y <= contentBottom)
            {
                var zoneMaxWidth = bounds.Width - rightColumn - pad * 2f;
                Marquee.DrawLeftAuto("skywatcher.medium.zone", zone, left, lineTwoY, zoneMaxWidth, TextStyles.Subheadline,
                    Palette.WithAlpha(palette.InkSoft, opacity));
            }
        }

        DrawHourlyStrip(context, palette);
    }

    private void DrawHourlyStrip(in WidgetContext context, in SkyPalette palette)
    {
        var columnCount = Math.Min(5, forecast.Count);
        if (columnCount <= 0)
        {
            return;
        }

        var bounds = context.Bounds;
        var scale = context.Scale;
        var opacity = context.Opacity;
        var drawList = context.DrawList;
        var pad = 16f * scale;
        var stripHeight = StripHeight(bounds, scale);
        var stripTop = bounds.Max.Y - 4f * scale - stripHeight;
        drawList.AddLine(new Vector2(bounds.Min.X + pad, stripTop), new Vector2(bounds.Max.X - pad, stripTop),
            ImGui.GetColorU32(Palette.WithAlpha(palette.Ink, 0.14f * opacity)), 1f * scale);
        var cellWidth = (bounds.Width - pad * 2f) / columnCount;
        var labelStyle = stripHeight > 52f * scale ? TextStyles.Subheadline : TextStyles.Footnote;
        var sky = Palette.Darken(Vector4.Lerp(palette.Top, palette.Bottom, 0.8f), 0.06f);
        var labelHeight = Typography.Measure("0", labelStyle).Y;
        var minRequired = MinStripHeight(scale);
        var prefRequired = (StripTopPadPref + StripBottomPadPref + StripLabelGapPref + StripIconRadiusPref * 2f) *
                            scale + labelHeight;
        var t = prefRequired > minRequired
            ? Math.Clamp((stripHeight - minRequired) / (prefRequired - minRequired), 0f, 1f)
            : 1f;
        var topPad = (StripTopPadMin + (StripTopPadPref - StripTopPadMin) * t) * scale;
        var bottomPad = (StripBottomPadMin + (StripBottomPadPref - StripBottomPadMin) * t) * scale;
        var labelGap = (StripLabelGapMin + (StripLabelGapPref - StripLabelGapMin) * t) * scale;
        var glyphRadius = (StripIconRadiusMin + (StripIconRadiusPref - StripIconRadiusMin) * t) * scale;
        var labelTop = stripTop + topPad;
        var glyphY = labelTop + labelHeight + labelGap + glyphRadius;
        for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
        {
            var window = forecast[columnIndex];
            var centerX = bounds.Min.X + pad + cellWidth * (columnIndex + 0.5f);
            Typography.DrawCentered(drawList, new Vector2(centerX, labelTop), When(window),
                Palette.WithAlpha(palette.InkSoft, opacity), labelStyle.Scale, labelStyle.Weight);
            var columnKind = WeatherSky.Classify(window.Weather.EnglishKey);
            var columnIsDay = IsDayWindow(window);
            WeatherGlyph.Draw(columnKind, new Vector2(centerX, glyphY), glyphRadius,
                WeatherSky.Resolve(columnKind, columnIsDay), columnIsDay, sky);
        }
    }

    private void DrawHero(in WidgetContext context, Rect bounds, in SkyPalette palette, EorzeaTime bell)
    {
        var scale = context.Scale;
        var opacity = context.Opacity;
        var drawList = context.DrawList;
        var pad = 17f * scale;
        var left = bounds.Min.X + pad;
        var time = bell.Formatted;
        var timeStyle = new TextStyle(1.62f, FontWeight.Medium);
        var timeSize = Typography.Measure(time, timeStyle);
        var eorzeaLabel = Loc.T(L.Home.Eorzea);
        var eyebrowWidth = WidgetChrome.EyebrowWidth(eorzeaLabel, scale);
        var rightColumn = MathF.Max(timeSize.X, eyebrowWidth) + pad;
        WidgetChrome.Eyebrow(drawList, new Vector2(left, bounds.Min.Y + pad), DisplayName, palette.InkSoft, scale,
            opacity);
        var eyebrowHeight = Typography.Measure(Loc.Culture.TextInfo.ToUpper(DisplayName), 0.66f, FontWeight.SemiBold).Y;
        var minConditionY = bounds.Min.Y + pad + eyebrowHeight + 8f * scale;
        var conditionY = MathF.Max(minConditionY, bounds.Min.Y + bounds.Height * 0.40f);
        var conditionStyle = new TextStyle(1.62f, FontWeight.SemiBold);
        var conditionTextHeight = Typography.Measure(forecast[0].Weather.Name, conditionStyle).Y;
        var secondLineY = MathF.Max(bounds.Max.Y - pad - 16f * scale, conditionY + conditionTextHeight + 6f * scale);
        DrawConditionText(drawList, forecast[0].Weather.Name, new Vector2(left, conditionY), conditionStyle,
            Palette.WithAlpha(palette.Ink, opacity), bounds.Width - rightColumn - pad * 2f,
            MathF.Max(0f, secondLineY - conditionY - 4f * scale));
        var secondLineText = FitScaled(SecondLine(), bounds.Width - rightColumn - pad * 2f,
            TextStyles.Subheadline.Scale, TextStyles.Subheadline.Weight, out var secondLineScale);
        Typography.Draw(drawList, new Vector2(left, secondLineY), secondLineText,
            Palette.WithAlpha(palette.InkSoft, opacity), secondLineScale, TextStyles.Subheadline.Weight);
        Typography.Draw(drawList, new Vector2(bounds.Max.X - pad - timeSize.X, conditionY + 2f * scale), time,
            Palette.WithAlpha(palette.Ink, opacity), timeStyle);
        WidgetChrome.Eyebrow(drawList,
            new Vector2(bounds.Max.X - pad - eyebrowWidth, conditionY + timeSize.Y + 7f * scale), eorzeaLabel,
            palette.InkSoft, scale, opacity);
    }

    private void DrawLarge(in WidgetContext context, in SkyPalette palette, EorzeaTime bell)
    {
        var bounds = context.Bounds;
        var scale = context.Scale;
        var opacity = context.Opacity;
        var drawList = context.DrawList;
        var heroHeight = bounds.Height * 0.42f;
        DrawHero(context, new Rect(bounds.Min, new Vector2(bounds.Max.X, bounds.Min.Y + heroHeight)), palette, bell);
        var pad = 17f * scale;
        var divider = bounds.Min.Y + heroHeight;
        drawList.AddLine(new Vector2(bounds.Min.X + pad, divider), new Vector2(bounds.Max.X - pad, divider),
            ImGui.GetColorU32(Palette.WithAlpha(palette.Ink, 0.14f * opacity)), 1f * scale);
        var rowCount = Math.Min(4, forecast.Count - 1);
        if (rowCount <= 0)
        {
            return;
        }

        var rowHeight = (bounds.Max.Y - divider - pad * 0.6f) / rowCount;
        for (var index = 0; index < rowCount; index++)
        {
            var window = forecast[index + 1];
            var rowCenterY = divider + rowHeight * (index + 0.5f);
            var when = When(window);
            Typography.Draw(drawList,
                new Vector2(bounds.Min.X + pad, rowCenterY - Typography.Measure(when, TextStyles.Footnote).Y * 0.5f),
                when, Palette.WithAlpha(palette.InkSoft, opacity), TextStyles.Footnote);
            var rowKind = WeatherSky.Classify(window.Weather.EnglishKey);
            var rowIsDay = IsDayWindow(window);
            var rowPalette = WeatherSky.Resolve(rowKind, rowIsDay);
            WeatherGlyph.Draw(rowKind, new Vector2(bounds.Min.X + bounds.Width * 0.42f, rowCenterY), 10f * scale,
                rowPalette, rowIsDay, Vector4.Lerp(palette.Top, palette.Bottom, 0.5f));
            var name = FitScaled(window.Weather.Name, bounds.Width * 0.42f, TextStyles.FootnoteEmphasized.Scale,
                TextStyles.FootnoteEmphasized.Weight, out var nameScale);
            var nameSize = Typography.Measure(name, nameScale, TextStyles.FootnoteEmphasized.Weight);
            Typography.Draw(drawList, new Vector2(bounds.Max.X - pad - nameSize.X, rowCenterY - nameSize.Y * 0.5f),
                name, Palette.WithAlpha(palette.Ink, opacity), nameScale, TextStyles.FootnoteEmphasized.Weight);
        }
    }

    private void DrawEmpty(in WidgetContext context, in SkyPalette palette)
    {
        var bounds = context.Bounds;
        var scale = context.Scale;
        WeatherGlyph.Draw(WeatherKind.Clouds, bounds.Center - new Vector2(0f, 8f * scale), 15f * scale, palette, false,
            Vector4.Lerp(palette.Top, palette.Bottom, 0.5f));
        Typography.DrawCentered(context.DrawList, new Vector2(bounds.Center.X, bounds.Center.Y + 16f * scale),
            Loc.T(L.Skywatcher.NoData), Palette.WithAlpha(palette.InkSoft, context.Opacity),
            TextStyles.Caption1.Scale, TextStyles.Caption1.Weight);
    }

    private string SecondLine()
    {
        for (var index = 1; index < forecast.Count; index++)
        {
            if (forecast[index].Weather.Id != forecast[0].Weather.Id)
            {
                return zone.Length > 0
                    ? string.Concat(zone, " · ", forecast[index].Weather.Name, " ", When(forecast[index]))
                    : string.Concat(forecast[index].Weather.Name, " ", When(forecast[index]));
            }
        }

        return zone;
    }

    private static string When(WeatherWindow window)
    {
        if (window.IsCurrent || window.MinutesFromNow <= 0)
        {
            return Loc.T(L.Time.Now);
        }

        if (window.MinutesFromNow < 60)
        {
            return Loc.T(L.Time.MinutesShort, window.MinutesFromNow);
        }

        return Loc.T(L.Time.HoursShort, window.MinutesFromNow / 60);
    }

    private static bool IsDayWindow(WeatherWindow window)
    {
        var midpoint = (window.StartBell + 4) % 24;
        return midpoint >= 6 && midpoint < 19;
    }

    private static string FitScaled(string text, float maxWidth, float maxScale, FontWeight weight, out float scale)
    {
        scale = Typography.FitScale(text, maxWidth, maxScale, 0.6f, weight);
        return Typography.FitText(text, maxWidth, scale, weight);
    }

    private static float StripHeight(Rect bounds, float scale) =>
        MathF.Max(MinStripHeight(scale), MathF.Min(bounds.Height * 0.30f, 64f * scale));

    private static float MinStripHeight(float scale)
    {
        var labelHeight = Typography.Measure("0", TextStyles.Footnote).Y;
        return (StripTopPadMin + StripBottomPadMin + StripLabelGapMin + StripIconRadiusMin * 2f) * scale + labelHeight;
    }

    private static float DrawConditionText(ImDrawListPtr drawList, string text, Vector2 topLeft, in TextStyle style,
        Vector4 color, float maxWidth, float maxHeight)
    {
        var singleLineSize = Typography.Measure(text, style);
        if (singleLineSize.X <= maxWidth)
        {
            Typography.Draw(drawList, topLeft, text, color, style);
            return singleLineSize.Y;
        }

        var lineHeight = singleLineSize.Y * 1.15f;
        var lines = Typography.WrapText(text, style, maxWidth);
        var fitsWidth = true;
        for (var index = 0; index < lines.Length; index++)
        {
            if (Typography.Measure(lines[index], style).X > maxWidth)
            {
                fitsWidth = false;
                break;
            }
        }

        if (fitsWidth && lines.Length <= 2 && lines.Length * lineHeight <= maxHeight)
        {
            return Typography.DrawWrappedLeft(topLeft, text, color, style, maxWidth);
        }

        var scale = Typography.FitScale(text, maxWidth, style.Scale, 0.6f, style.Weight);
        var clipped = Typography.FitText(text, maxWidth, scale, style.Weight);
        var clippedSize = Typography.Measure(clipped, scale, style.Weight);
        Typography.Draw(drawList, topLeft, clipped, color, scale, style.Weight);
        return clippedSize.Y;
    }

    public void Dispose()
    {
    }
}
