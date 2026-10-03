using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Clock.Widgets;

internal sealed class ClockWidget : IHomeWidget
{
    private const string CityKey = "city";
    private const string WorldRoute = "clock.tab.world";
    private const int FaceCount = 4;
    private const int ListCapacity = 16;
    private const float SmallFaceInset = 7f;
    private const float SmallLabelLift = 0.42f;
    private const float SmallLabelWidth = 1.1f;
    private const float FaceLabelGap = 6f;
    private const float HeroFaceUnits = 46f;
    private const float RowUnits = 44f;
    private const float RowGlyphUnits = 15f;

    private readonly Configuration configuration;
    private readonly WidgetOption[] options;
    private readonly ClockSlot[] faces = new ClockSlot[FaceCount];
    private readonly ClockSlot[] rows = new ClockSlot[ListCapacity];
    private readonly CachedText[] faceDetails = new CachedText[FaceCount];
    private readonly CachedText[] rowDetails = new CachedText[ListCapacity];
    private CachedText heroDate;
    private CachedText heroZone;

    public ClockWidget(Configuration configuration)
    {
        this.configuration = configuration;
        options = new[]
        {
            new WidgetOption(CityKey, L.WidgetsTime.City,
                target => ClockZones.Choices(target, configuration.WorldClocks), ClockZones.LocalValue),
        };
    }

    public string Id => "clock.faces";
    public string DisplayName => Loc.T(L.Apps.Clock);
    public string Description => Loc.T(L.WidgetsTime.ClockDescription);
    public string AppId => "clock";
    public WidgetSizeSet Sizes => WidgetSizeSet.Small | WidgetSizeSet.Medium | WidgetSizeSet.Large;
    public IReadOnlyList<WidgetOption> Options => options;

    public WidgetRoute Target(in WidgetContext context) => WidgetRoute.Tab(AppId, WorldRoute);

    public void Draw(in WidgetContext context)
    {
        if (context.Opacity <= 0f)
        {
            return;
        }

        WidgetChrome.Container(context);
        var ink = WidgetInk.From(context);
        var utcNow = DateTime.UtcNow;
        switch (context.Size)
        {
            case WidgetSize.Small:
                DrawSmall(context, ink, utcNow);
                return;
            case WidgetSize.Medium:
                DrawMedium(context, ink, utcNow);
                return;
            default:
                DrawLarge(context, ink, utcNow);
                return;
        }
    }

    private void DrawSmall(in WidgetContext context, in WidgetInk ink, DateTime utcNow)
    {
        var bounds = context.Bounds;
        var scale = context.Scale;
        var slot = ClockZones.FromChoice(WidgetConfig.Get(context.Config, CityKey), configuration.WorldClocks);
        var reading = ClockZones.Read(slot, utcNow);
        var radius = MathF.Min(bounds.Width, bounds.Height) * 0.5f - SmallFaceInset * scale;
        var center = bounds.Center;
        var paint = TimeWidgetParts.ClockPaint(ink, reading.Moment.Hour, AppAccents.For(AppId));
        DrawFace(context, center, radius, reading, paint);
        var label = WidgetText.Upper(slot.Name);
        var maxWidth = radius * SmallLabelWidth;
        var width = MathF.Min(maxWidth, WidgetText.EyebrowWidth(label, scale));
        var height = WidgetText.EyebrowHeight();
        WidgetText.EyebrowFit(context.DrawList,
            new Vector2(center.X - width * 0.5f, center.Y - radius * SmallLabelLift - height * 0.5f), label, maxWidth,
            paint.Ink with { W = paint.Ink.W * 0.75f }, scale);
    }

    private void DrawMedium(in WidgetContext context, in WidgetInk ink, DateTime utcNow)
    {
        var content = WidgetMetrics.Content(context);
        var scale = context.Scale;
        var drawList = context.DrawList;
        var count = ClockZones.FillFaces(faces, configuration.WorldClocks);
        var columnWidth = content.Width / count;
        var nameHeight = Typography.Measure("A", WidgetType.Headline).Y;
        var detailHeight = Typography.Measure("A", WidgetType.Caption).Y;
        var labelBlock = FaceLabelGap * scale + nameHeight + WidgetMetrics.RowGap * scale + detailHeight;
        var radius = MathF.Max(8f * scale,
            MathF.Min(columnWidth * 0.5f - WidgetMetrics.Gutter * 0.5f * scale, (content.Height - labelBlock) * 0.5f));
        var blockTop = content.Min.Y + (content.Height - radius * 2f - labelBlock) * 0.5f;
        var accent = AppAccents.For(AppId);
        for (var index = 0; index < count; index++)
        {
            var slot = faces[index];
            var reading = ClockZones.Read(slot, utcNow);
            var columnCenter = content.Min.X + columnWidth * (index + 0.5f);
            var center = new Vector2(columnCenter, blockTop + radius);
            DrawFace(context, center, radius, reading, TimeWidgetParts.ClockPaint(ink, reading.Moment.Hour, accent));
            var nameTop = center.Y + radius + FaceLabelGap * scale;
            var labelWidth = columnWidth - WidgetMetrics.RowGap * 2f * scale;
            DrawCenteredFit(drawList, slot.Name, columnCenter, nameTop, labelWidth, ink.Primary, WidgetType.Headline);
            DrawCenteredFit(drawList, ClockZones.Detail(ref faceDetails[index], slot, reading), columnCenter,
                nameTop + nameHeight + WidgetMetrics.RowGap * scale, labelWidth, ink.Secondary, WidgetType.Caption);
        }
    }

    private void DrawLarge(in WidgetContext context, in WidgetInk ink, DateTime utcNow)
    {
        var content = WidgetMetrics.Content(context);
        var scale = context.Scale;
        var drawList = context.DrawList;
        var accent = AppAccents.For(AppId);
        var headerBottom = WidgetChrome.Header(context, ink, AppId, L.Clock.TabWorld, accent);

        var local = ClockZones.Local;
        var reading = ClockZones.Read(local, utcNow);
        var faceRadius = HeroFaceUnits * scale;
        var heroTop = headerBottom + WidgetMetrics.Gutter * 1.5f * scale;
        var faceCenter = new Vector2(content.Min.X + faceRadius, heroTop + faceRadius);
        DrawFace(context, faceCenter, faceRadius, reading,
            TimeWidgetParts.ClockPaint(ink, reading.Moment.Hour, accent));

        var textLeft = faceCenter.X + faceRadius + WidgetMetrics.Gutter * 2f * scale;
        var textWidth = MathF.Max(1f, content.Max.X - textLeft);
        var clock = TimeText.Clock(reading.Moment);
        var hero = WidgetText.FitStyle(clock, WidgetType.DisplayCompact, textWidth, true);
        var heroHeight = Typography.Measure(clock, hero).Y;
        var dateText = HeroDate(reading.Moment);
        var dateHeight = Typography.Measure(dateText, WidgetType.Body).Y;
        var zoneText = HeroZone();
        var zoneHeight = Typography.Measure(zoneText, WidgetType.Caption).Y;
        var stack = heroHeight + WidgetMetrics.RowGap * scale + dateHeight + WidgetMetrics.RowGap * scale + zoneHeight;
        var stackTop = faceCenter.Y - stack * 0.5f;
        TimeWidgetParts.Clock(drawList, new Vector2(textLeft, stackTop), clock, ink.Primary, hero, WidgetType.Title,
            ink.Secondary);
        var dateTop = stackTop + heroHeight + WidgetMetrics.RowGap * scale;
        WidgetText.Draw(drawList, new Vector2(textLeft, dateTop), dateText, ink.Secondary, WidgetType.Body, textWidth);
        WidgetText.Draw(drawList, new Vector2(textLeft, dateTop + dateHeight + WidgetMetrics.RowGap * scale), zoneText,
            ink.Accent(accent), WidgetType.Caption, textWidth);

        var listTop = faceCenter.Y + faceRadius + WidgetMetrics.Gutter * 1.5f * scale;
        var rowHeight = RowUnits * scale;
        var count = ClockZones.Fill(rows, configuration.WorldClocks, false);
        var visible = Math.Min(count, (int)((content.Max.Y - listTop) / rowHeight));
        for (var index = 0; index < visible; index++)
        {
            var top = listTop + index * rowHeight;
            WidgetChrome.Separator(context, ink, content.Min.X, content.Max.X, top);
            DrawRow(context, ink, new Rect(new Vector2(content.Min.X, top), new Vector2(content.Max.X, top + rowHeight)),
                index, utcNow);
        }
    }

    private void DrawRow(in WidgetContext context, in WidgetInk ink, Rect row, int index, DateTime utcNow)
    {
        var scale = context.Scale;
        var drawList = context.DrawList;
        var slot = rows[index];
        var reading = ClockZones.Read(slot, utcNow);
        var glyph = RowGlyphUnits * scale;
        var glyphColor = ink.Accent(reading.IsDaytime ? Accent.Amber : Accent.Indigo);
        ProgressRing.CenterIcon(drawList, new Vector2(row.Min.X + glyph * 0.5f, row.Center.Y),
            reading.IsDaytime ? FontAwesomeIcon.Sun : FontAwesomeIcon.Moon, glyphColor, glyph);

        var clock = TimeText.Clock(reading.Moment);
        var clockWidth = TimeWidgetParts.ClockWidth(clock, WidgetType.Title, WidgetType.Caption);
        var clockHeight = Typography.Measure(clock, WidgetType.Title).Y;
        TimeWidgetParts.Clock(drawList, new Vector2(row.Max.X - clockWidth, row.Center.Y - clockHeight * 0.5f), clock,
            ink.Primary, WidgetType.Title, WidgetType.Caption, ink.Secondary);

        var textLeft = row.Min.X + glyph + WidgetMetrics.Gutter * 1.5f * scale;
        var textWidth = MathF.Max(1f, row.Max.X - clockWidth - WidgetMetrics.Gutter * scale - textLeft);
        var nameHeight = Typography.Measure("A", WidgetType.Headline).Y;
        var detailHeight = Typography.Measure("A", WidgetType.Caption).Y;
        var top = row.Center.Y - (nameHeight + WidgetMetrics.RowGap * scale + detailHeight) * 0.5f;
        WidgetText.Draw(drawList, new Vector2(textLeft, top), slot.Name, ink.Primary, WidgetType.Headline, textWidth);
        WidgetText.Draw(drawList, new Vector2(textLeft, top + nameHeight + WidgetMetrics.RowGap * scale),
            ClockZones.Detail(ref rowDetails[index], slot, reading), ink.Secondary, WidgetType.Caption, textWidth);
    }

    private static void DrawFace(in WidgetContext context, Vector2 center, float radius, in ClockReading reading,
        in ClockFacePaint paint) =>
        AnalogClock.Draw(context.DrawList, center, radius, reading.Moment.Hour, reading.Moment.Minute,
            reading.Seconds, paint, context.Scale);

    private static void DrawCenteredFit(ImDrawListPtr drawList, string text, float centerX, float top, float maxWidth,
        Vector4 color, in TextStyle style)
    {
        var fitted = WidgetText.Fit(text, maxWidth, style, out var fittedScale);
        var width = Typography.Measure(fitted, fittedScale, style.Weight).X;
        Typography.Draw(drawList, new Vector2(centerX - width * 0.5f, top), fitted, color, fittedScale, style.Weight);
    }

    private string HeroDate(DateTime local)
    {
        var key = local.Date.Ticks;
        if (heroDate.IsCurrent(key))
        {
            return heroDate.Value;
        }

        var format = Loc.Culture.DateTimeFormat;
        return heroDate.Store(key, string.Concat(local.ToString("dddd", Loc.Culture), ", ",
            local.ToString(format.MonthDayPattern, Loc.Culture)));
    }

    private string HeroZone()
    {
        var offset = (int)TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow).TotalMinutes;
        return heroZone.IsCurrent(offset)
            ? heroZone.Value
            : heroZone.Store(offset,
                Loc.T(L.WidgetsTime.DayOffset, Loc.T(L.Clock.Local), ClockZones.UtcLabel(offset)));
    }

    public void Dispose()
    {
    }
}
