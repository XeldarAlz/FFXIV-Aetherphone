using System.Numerics;
using Aetherphone.Apps.Skywatcher;
using Aetherphone.Core;
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
        var isDay = bell.Hour >= 6 && bell.Hour < 19;
        var kind = forecast.Count > 0 ? WeatherSky.Classify(forecast[0].Weather) : WeatherKind.Clouds;
        var palette = WeatherSky.Resolve(kind, forecast.Count > 0 && isDay);
        WidgetChrome.Tinted(context.DrawList, context.Bounds, palette.Top, palette.Bottom, context.Scale,
            context.Opacity);
        if (forecast.Count == 0)
        {
            DrawEmpty(context, palette);
            return;
        }

        switch (context.Size)
        {
            case WidgetSize.Small:
                DrawSmall(context, palette, kind, isDay, bell);
                break;
            case WidgetSize.Medium:
                DrawHero(context, context.Bounds, palette, bell);
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
        WidgetChrome.Eyebrow(drawList, new Vector2(bounds.Min.X + pad, bounds.Min.Y + pad), DisplayName,
            palette.InkSoft, scale, opacity);
        var glyphCenter = new Vector2(bounds.Min.X + pad + 17f * scale, bounds.Center.Y + 2f * scale);
        WeatherGlyph.Draw(kind, glyphCenter, 16f * scale, palette, isDay,
            Vector4.Lerp(palette.Top, palette.Bottom, 0.5f));
        var time = bell.Formatted;
        var timeSize = Typography.Measure(time, TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList, new Vector2(bounds.Max.X - pad - timeSize.X, glyphCenter.Y - timeSize.Y * 0.5f),
            time, Palette.WithAlpha(palette.InkSoft, opacity), TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList, new Vector2(bounds.Min.X + pad, bounds.Max.Y - pad - 40f * scale),
            Fit(forecast[0].Weather, bounds.Width - pad * 2f, 1.06f, FontWeight.SemiBold),
            Palette.WithAlpha(palette.Ink, opacity), 1.06f, FontWeight.SemiBold);
        Typography.Draw(drawList, new Vector2(bounds.Min.X + pad, bounds.Max.Y - pad - 17f * scale),
            Fit(zone, bounds.Width - pad * 2f, TextStyles.Caption1.Scale, TextStyles.Caption1.Weight),
            Palette.WithAlpha(palette.InkSoft, opacity), TextStyles.Caption1);
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
        var conditionY = bounds.Min.Y + bounds.Height * 0.40f;
        Typography.Draw(drawList, new Vector2(left, conditionY),
            Fit(forecast[0].Weather, bounds.Width - rightColumn - pad * 2f, 1.62f, FontWeight.SemiBold),
            Palette.WithAlpha(palette.Ink, opacity), 1.62f, FontWeight.SemiBold);
        Typography.Draw(drawList, new Vector2(left, bounds.Max.Y - pad - 16f * scale),
            Fit(SecondLine(), bounds.Width - rightColumn - pad * 2f, TextStyles.Subheadline.Scale,
                TextStyles.Subheadline.Weight), Palette.WithAlpha(palette.InkSoft, opacity), TextStyles.Subheadline);
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
            var rowKind = WeatherSky.Classify(window.Weather);
            var rowIsDay = IsDayWindow(window);
            var rowPalette = WeatherSky.Resolve(rowKind, rowIsDay);
            WeatherGlyph.Draw(rowKind, new Vector2(bounds.Min.X + bounds.Width * 0.42f, rowCenterY), 10f * scale,
                rowPalette, rowIsDay, Vector4.Lerp(palette.Top, palette.Bottom, 0.5f));
            var name = Fit(window.Weather, bounds.Width * 0.42f, TextStyles.FootnoteEmphasized.Scale,
                TextStyles.FootnoteEmphasized.Weight);
            var nameSize = Typography.Measure(name, TextStyles.FootnoteEmphasized);
            Typography.Draw(drawList, new Vector2(bounds.Max.X - pad - nameSize.X, rowCenterY - nameSize.Y * 0.5f),
                name, Palette.WithAlpha(palette.Ink, opacity), TextStyles.FootnoteEmphasized);
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
            if (!string.Equals(forecast[index].Weather, forecast[0].Weather, StringComparison.Ordinal))
            {
                return zone.Length > 0
                    ? string.Concat(zone, " · ", forecast[index].Weather, " ", When(forecast[index]))
                    : string.Concat(forecast[index].Weather, " ", When(forecast[index]));
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

    private static string Fit(string text, float maxWidth, float fontScale, FontWeight weight) =>
        Typography.FitText(text, maxWidth, fontScale, weight);

    public void Dispose()
    {
    }
}
