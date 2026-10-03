using Aetherphone.Core;
using Aetherphone.Core.Game;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Skywatcher.Widgets;

internal sealed class WeatherWidget : IHomeWidget
{
    private const int ForecastWindows = 11;
    private const int StripColumns = 6;
    private const int ListFirst = StripColumns;
    private const int ListRows = 5;
    private const uint SampleTerritory = 132;
    private const float HeroGlyphUnits = 12f;
    private const float SmallGlyphUnits = 10f;
    private const float StripGlyphUnits = 9f;
    private const float ListGlyphUnits = 9f;
    private const float StripGapUnits = 4f;
    private const float SectionGapUnits = 10f;

    private readonly WeatherService weather;
    private readonly List<WeatherWindow> liveForecast = new(ForecastWindows);
    private readonly List<WeatherWindow> sampleForecast = new(ForecastWindows);
    private readonly CachedText[] whenLabels = new CachedText[StripColumns];
    private readonly CachedText[] untilLabels = new CachedText[ListRows];
    private WeatherPulse livePulse;
    private WeatherPulse samplePulse;
    private CachedText changeLine;
    private List<WeatherWindow> forecast;
    private string zone = string.Empty;
    private string liveZone = string.Empty;
    private string sampleZone = string.Empty;
    private bool sample;
    private bool inWorld;

    public WeatherWidget(WeatherService weather)
    {
        this.weather = weather;
        forecast = liveForecast;
    }

    public string Id => "skywatcher.forecast";
    public string DisplayName => Loc.T(L.WidgetsLife.WeatherName);
    public string Description => Loc.T(L.Widgets.WeatherDescription);
    public string AppId => "skywatcher";
    public WidgetSizeSet Sizes => WidgetSizeSet.Small | WidgetSizeSet.Medium | WidgetSizeSet.Large;

    public float Relevance(string config)
    {
        Refresh(false);
        return sample ? 0f : WeatherWidgetPaint.Relevance(forecast);
    }

    public void Draw(in WidgetContext context)
    {
        Refresh(context.Preview);
        var now = EorzeaTime.Now();
        if (forecast.Count == 0)
        {
            DrawUnavailable(context, now);
            return;
        }

        var current = forecast[0];
        var kind = WeatherSky.Classify(current.Weather.EnglishKey);
        var isDay = WeatherWidgetPaint.IsDay(current, now);
        var sky = WeatherWidgetPaint.Sky(kind, now);
        WeatherWidgetPaint.Background(context, sky);
        var ink = new WeatherInk(WidgetInk.From(context), sky);
        WeatherWidgetPaint.Ambience(context, ink, kind, isDay, sky);
        var content = WidgetMetrics.Content(context);
        switch (context.Size)
        {
            case WidgetSize.Small:
                DrawSmall(context, ink, content, kind, isDay, now);
                break;
            case WidgetSize.Medium:
                DrawHeader(context, ink, content, content.Min.Y, kind, isDay, now);
                DrawStrip(context, ink, content, content.Max.Y - StripHeight(context.Scale), now);
                break;
            default:
                var headerBottom = DrawHeader(context, ink, content, content.Min.Y, kind, isDay, now);
                var stripBottom = DrawStrip(context, ink, content, headerBottom + SectionGapUnits * context.Scale,
                    now);
                DrawList(context, ink, content, stripBottom + SectionGapUnits * 0.5f * context.Scale, now);
                break;
        }
    }

    private void Refresh(bool preview)
    {
        inWorld = weather.CurrentTerritory != 0;
        sample = preview && !inWorld;
        if (sample)
        {
            if (samplePulse.Due(SampleTerritory, 0))
            {
                sampleZone = weather.ZoneName(SampleTerritory);
                weather.Forecast(SampleTerritory, sampleForecast, ForecastWindows);
            }

            forecast = sampleForecast;
            zone = sampleZone;
            return;
        }

        if (livePulse.Due(weather.CurrentTerritory, weather.LiveWeatherId()))
        {
            liveZone = weather.ZoneName(weather.CurrentTerritory);
            weather.Forecast(liveForecast, ForecastWindows);
        }

        forecast = liveForecast;
        zone = liveZone;
    }

    private void DrawSmall(in WidgetContext context, in WeatherInk ink, Rect content, WeatherKind kind, bool isDay,
        EorzeaTime now)
    {
        var scale = context.Scale;
        var top = content.Min.Y;
        top += WidgetText.Draw(context.DrawList, new Vector2(content.Min.X, top), ZoneLabel(), ink.Primary,
            WidgetType.Headline, content.Width);
        DrawHero(context, ink, new Vector2(content.Min.X, top), content.Width, WidgetType.DisplayCompact, now);
        var captionHeight = WidgetText.LineHeight(WidgetType.Caption);
        var headlineHeight = WidgetText.LineHeight(WidgetType.Headline);
        var changeTop = content.Max.Y - captionHeight;
        var change = WeatherWidgetPaint.ChangeLine(ref changeLine, forecast, WeatherWidgetPaint.NextChange(forecast));
        WidgetText.Draw(context.DrawList, new Vector2(content.Min.X, changeTop), change, ink.Secondary,
            WidgetType.Caption, content.Width);
        var conditionTop = changeTop - WidgetMetrics.RowGap * scale - headlineHeight;
        WidgetText.Draw(context.DrawList, new Vector2(content.Min.X, conditionTop), forecast[0].Weather.Name,
            ink.Primary, WidgetType.Headline, content.Width);
        var glyphRadius = SmallGlyphUnits * scale;
        var glyphCenter = new Vector2(content.Min.X + glyphRadius, conditionTop - WidgetMetrics.RowGap * scale -
                                                                  glyphRadius);
        WeatherGlyph.Draw(context.DrawList, kind, glyphCenter, glyphRadius, ink.Glyph(kind, isDay), isDay);
    }

    private float DrawHeader(in WidgetContext context, in WeatherInk ink, Rect content, float top, WeatherKind kind,
        bool isDay, EorzeaTime now)
    {
        var scale = context.Scale;
        var leftWidth = content.Width * 0.56f;
        var rightWidth = content.Width - leftWidth - WidgetMetrics.Gutter * scale;
        var nameHeight = WidgetText.Draw(context.DrawList, new Vector2(content.Min.X, top), ZoneLabel(), ink.Primary,
            WidgetType.Headline, leftWidth);
        var heroHeight = DrawHero(context, ink, new Vector2(content.Min.X, top + nameHeight), leftWidth,
            WidgetType.Display, now);
        var glyphRadius = HeroGlyphUnits * scale;
        var glyphCenter = new Vector2(content.Max.X - glyphRadius, top + glyphRadius);
        WeatherGlyph.Draw(context.DrawList, kind, glyphCenter, glyphRadius, ink.Glyph(kind, isDay), isDay);
        var rightTop = top + glyphRadius * 2f + WidgetMetrics.RowGap * scale;
        rightTop += WeatherWidgetPaint.TextRight(context, content.Max.X, rightTop, forecast[0].Weather.Name,
            ink.Primary, WidgetType.Headline, rightWidth);
        var change = WeatherWidgetPaint.ChangeLine(ref changeLine, forecast, WeatherWidgetPaint.NextChange(forecast));
        rightTop += WeatherWidgetPaint.TextRight(context, content.Max.X, rightTop, change, ink.Secondary,
            WidgetType.Caption, rightWidth);
        return MathF.Max(top + nameHeight + heroHeight, rightTop);
    }

    private float DrawHero(in WidgetContext context, in WeatherInk ink, Vector2 position, float maxWidth,
        in TextStyle style, EorzeaTime now)
    {
        var clock = WeatherWidgetPaint.Clock(now);
        var suffix = Loc.T(L.WidgetsLife.EorzeaShort);
        var suffixWidth = Typography.Measure(suffix, WidgetType.Caption).X + WidgetMetrics.RowGap * context.Scale;
        var available = MathF.Max(1f, maxWidth - suffixWidth);
        var heroStyle = style;
        var width = WidgetText.TabularWidth(clock, heroStyle);
        if (width > available)
        {
            heroStyle = new TextStyle(MathF.Max(style.Scale * WidgetType.MinimumFit, style.Scale * available / width),
                style.Weight);
        }

        var drawn = WidgetText.Tabular(context.DrawList, position, clock, ink.Primary, heroStyle);
        var heroHeight = Typography.Measure(clock, heroStyle).Y;
        var captionHeight = WidgetText.LineHeight(WidgetType.Caption);
        Typography.Draw(context.DrawList,
            new Vector2(position.X + drawn + WidgetMetrics.RowGap * context.Scale,
                position.Y + heroHeight * 0.82f - captionHeight), suffix, ink.Secondary, WidgetType.Caption);
        return heroHeight;
    }

    private static float StripHeight(float scale) =>
        WidgetText.LineHeight(WidgetType.Caption) * 2f + (StripGlyphUnits * 2f + StripGapUnits * 2f) * scale;

    private float DrawStrip(in WidgetContext context, in WeatherInk ink, Rect content, float top, EorzeaTime now)
    {
        var scale = context.Scale;
        var columns = Math.Min(StripColumns, forecast.Count);
        var cellWidth = content.Width / StripColumns;
        var captionHeight = WidgetText.LineHeight(WidgetType.Caption);
        var glyphRadius = StripGlyphUnits * scale;
        var glyphY = top + captionHeight + StripGapUnits * scale + glyphRadius;
        var bellTop = glyphY + glyphRadius + StripGapUnits * scale;
        var labelWidth = cellWidth - WidgetMetrics.RowGap * scale;
        for (var column = 0; column < columns; column++)
        {
            var window = forecast[column];
            var centerX = content.Min.X + cellWidth * (column + 0.5f);
            var label = WeatherWidgetPaint.When(ref whenLabels[column], window);
            WeatherWidgetPaint.TextCentered(context, centerX, top, label, column == 0 ? ink.Primary : ink.Secondary,
                WidgetType.Caption, labelWidth);
            var kind = WeatherSky.Classify(window.Weather.EnglishKey);
            var isDay = WeatherWidgetPaint.IsDay(window, now);
            WeatherGlyph.Draw(context.DrawList, kind, new Vector2(centerX, glyphY), glyphRadius,
                ink.Glyph(kind, isDay), isDay);
            WeatherWidgetPaint.TextCentered(context, centerX, bellTop, WeatherWidgetPaint.Bell(window.StartBell),
                ink.Primary, WidgetType.Caption, labelWidth);
        }

        return bellTop + captionHeight;
    }

    private void DrawList(in WidgetContext context, in WeatherInk ink, Rect content, float top, EorzeaTime now)
    {
        var scale = context.Scale;
        var drawList = context.DrawList;
        drawList.AddLine(new Vector2(content.Min.X, top), new Vector2(content.Max.X, top),
            ImGui.GetColorU32(ink.Rule), MathF.Max(1f, scale));
        var rows = Math.Min(ListRows, forecast.Count - ListFirst);
        if (rows <= 0)
        {
            return;
        }

        var rowHeight = (content.Max.Y - top) / ListRows;
        var bellWidth = WidgetText.TabularWidth(WeatherWidgetPaint.Bell(20), WidgetType.Headline);
        var glyphRadius = ListGlyphUnits * scale;
        var glyphX = content.Min.X + bellWidth + WidgetMetrics.Gutter * scale + glyphRadius;
        var nameLeft = glyphX + glyphRadius + WidgetMetrics.Gutter * scale;
        var untilWidth = content.Width * 0.3f;
        var nameWidth = content.Max.X - untilWidth - WidgetMetrics.Gutter * scale - nameLeft;
        var headlineHeight = WidgetText.LineHeight(WidgetType.Headline);
        var bodyHeight = WidgetText.LineHeight(WidgetType.Body);
        var captionHeight = WidgetText.LineHeight(WidgetType.Caption);
        for (var row = 0; row < rows; row++)
        {
            var window = forecast[ListFirst + row];
            var centerY = top + rowHeight * (row + 0.5f);
            WidgetText.Tabular(drawList, new Vector2(content.Min.X, centerY - headlineHeight * 0.5f),
                WeatherWidgetPaint.Bell(window.StartBell), ink.Primary, WidgetType.Headline);
            var kind = WeatherSky.Classify(window.Weather.EnglishKey);
            var isDay = WeatherWidgetPaint.IsDay(window, now);
            WeatherGlyph.Draw(drawList, kind, new Vector2(glyphX, centerY), glyphRadius, ink.Glyph(kind, isDay),
                isDay);
            WidgetText.Draw(context.DrawList, new Vector2(nameLeft, centerY - bodyHeight * 0.5f), window.Weather.Name,
                ink.Primary, WidgetType.Body, nameWidth);
            WeatherWidgetPaint.TextRight(context, content.Max.X, centerY - captionHeight * 0.5f,
                WeatherWidgetPaint.Until(ref untilLabels[row], window), ink.Secondary, WidgetType.Caption,
                untilWidth);
        }
    }

    private void DrawUnavailable(in WidgetContext context, EorzeaTime now)
    {
        var sky = WeatherWidgetPaint.Sky(WeatherKind.Clouds, now);
        WeatherWidgetPaint.Background(context, sky);
        var weatherInk = new WeatherInk(WidgetInk.From(context), sky);
        var content = WidgetMetrics.Content(context);
        var radius = HeroGlyphUnits * context.Scale;
        var isDay = WeatherSky.Daylight(now.Hour + now.Minute / 60f) >= 0.5f;
        WeatherGlyph.Draw(context.DrawList, WeatherKind.Clouds, new Vector2(content.Min.X + radius,
            content.Min.Y + radius), radius, weatherInk.Glyph(WeatherKind.Clouds, isDay), isDay);
        var title = inWorld ? Loc.T(L.Skywatcher.NoData) : Loc.T(L.WidgetsLife.WeatherUnavailable);
        var detail = inWorld || context.Size == WidgetSize.Small
            ? string.Empty
            : Loc.T(L.WidgetsLife.WeatherUnavailableDetail);
        WidgetChrome.Message(context, WidgetMetrics.Below(context, content.Min.Y + radius * 2f + WidgetMetrics.Gutter * context.Scale), weatherInk.Primary,
            weatherInk.Secondary, title, detail);
    }

    private string ZoneLabel() => zone.Length > 0 ? zone : Loc.T(L.Home.Eorzea);

    public void Dispose()
    {
    }
}
