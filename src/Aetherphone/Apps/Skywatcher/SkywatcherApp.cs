using Aetherphone.Core.Animation;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Skywatcher;

internal enum SkywatcherTab : byte
{
    Forecast,
    Control,
}

internal sealed partial class SkywatcherApp : IPhoneApp
{
    private const int WindowCount = 8;
    private const int HourlyStripCount = 5;
    private const float NavHeight = 60f;
    public string Id => "skywatcher";
    public string DisplayName => Loc.T(L.Apps.Skywatcher);
    public string Glyph => "W";
    public int BadgeCount => 0;
    private readonly WeatherService weather;
    private readonly WeatherControl control;
    private readonly Configuration configuration;
    private readonly ViewRouter<SkywatcherRoute> router;
    private readonly RouterDraw<SkywatcherRoute> drawView;
    private readonly Action closeDetail;
    private readonly List<WeatherWindow> previewForecast = new();
    private readonly List<string> previewForecastWhenLabels = new();
    private readonly List<WeatherWindow> detailForecast = new();
    private readonly List<string> detailForecastWhenLabels = new();
    private string previewZone = string.Empty;
    private string detailZone = string.Empty;
    private uint lastPreviewTerritoryId;
    private long lastWindowStartUnix = -1;
    private long lastMinuteUnix = -1;
    private SkywatcherTab activeTab;
    private bool scrubbing;
    private bool pendingScrollReset;
    private DragScrollHost.Surface scrollSurface;
    private string search = string.Empty;

    public SkywatcherApp(WeatherService weather, WeatherControl control, Configuration configuration)
    {
        this.weather = weather;
        this.control = control;
        this.configuration = configuration;
        router = new ViewRouter<SkywatcherRoute>(SkywatcherRoute.Browse);
        drawView = DrawView;
        closeDetail = CloseDetail;
    }

    public void OnOpened()
    {
        activeTab = SkywatcherTab.Forecast;
        scrubbing = false;
        router.Reset();
        search = string.Empty;
        Refresh();
    }

    public void OnClosed()
    {
    }

    private void Refresh() => Refresh(WeatherService.CurrentWindowStartUnix());

    private void Refresh(long windowStart)
    {
        var route = router.Current;
        if (route.Screen == SkywatcherScreen.Browse)
        {
            lastPreviewTerritoryId = weather.CurrentTerritoryId;
            RefreshRowWeather();
            RefreshFavoriteForecasts();
            previewZone = weather.CurrentZone();
            weather.Forecast(weather.CurrentTerritoryId, previewForecast, WindowCount);
            BuildShortWhenLabels(previewForecastWhenLabels, previewForecast);
        }
        else
        {
            detailZone = weather.ZoneName(route.TerritoryId);
            weather.Forecast(route.TerritoryId, detailForecast, WindowCount);
            BuildShortWhenLabels(detailForecastWhenLabels, detailForecast);
        }

        lastWindowStartUnix = windowStart;
    }

    private void OpenDetail(uint territoryId)
    {
        detailZone = weather.ZoneName(territoryId);
        weather.Forecast(territoryId, detailForecast, WindowCount);
        BuildShortWhenLabels(detailForecastWhenLabels, detailForecast);
        router.Push(SkywatcherRoute.Detail(territoryId));
        pendingScrollReset = true;
    }

    private void CloseDetail()
    {
        router.Pop();
        Refresh();
        pendingScrollReset = true;
    }

    private void RefreshMinuteLabels(long windowStart, long nowUnix)
    {
        WeatherService.RefreshMinutesFromNow(previewForecast, windowStart, nowUnix);
        BuildShortWhenLabels(previewForecastWhenLabels, previewForecast);
        WeatherService.RefreshMinutesFromNow(detailForecast, windowStart, nowUnix);
        BuildShortWhenLabels(detailForecastWhenLabels, detailForecast);
        RefreshFavoriteMinuteLabels(windowStart, nowUnix);
    }

    public void Draw(in PhoneContext context)
    {
        var windowStart = WeatherService.CurrentWindowStartUnix();
        var route = router.Current;
        var onBrowse = route.Screen == SkywatcherScreen.Browse;
        var activeForecast = onBrowse ? previewForecast : detailForecast;
        var activeTerritoryId = onBrowse ? weather.CurrentTerritoryId : route.TerritoryId;
        var zoneChanged = onBrowse && weather.CurrentTerritoryId != lastPreviewTerritoryId;
        var liveDiverged = activeTerritoryId == weather.CurrentTerritoryId && activeForecast.Count > 0 &&
            weather.LiveRenderedWeather() is { } live && live.Id != activeForecast[0].Weather.Id;
        if (zoneChanged || liveDiverged || windowStart != lastWindowStartUnix)
        {
            Refresh(windowStart);
        }

        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var currentMinute = nowUnix / 60;
        if (currentMinute != lastMinuteUnix)
        {
            RefreshMinuteLabels(windowStart, nowUnix);
            lastMinuteUnix = currentMinute;
        }

        var scale = UiScale.Current;
        var theme = context.Theme;
        var content = context.Content;
        var screen = SceneChrome.ScreenFrom(content, theme, scale);
        var (kind, isDay, palette, _) = SkyDataFor(activeForecast);
        WeatherSky.Paint(screen, theme.ScreenRounding * scale, palette, kind, isDay);
        WeatherAmbience.Draw(ImGui.GetWindowDrawList(), screen, theme.ScreenRounding * scale, kind, isDay, palette,
            scale, 1f, false);
        if (activeTab == SkywatcherTab.Forecast && !onBrowse)
        {
            SceneChrome.BackChevron(content, closeDetail, palette.Ink, scale);
        }
        else
        {
            SceneChrome.BackChevron(content, context.Navigation, palette.Ink, scale);
        }

        var navRect = new Rect(new Vector2(content.Min.X, content.Max.Y - NavHeight * scale), content.Max);
        var body = new Rect(new Vector2(screen.Min.X, content.Min.Y + 40f * scale),
            new Vector2(screen.Max.X, navRect.Min.Y));
        if (activeTab == SkywatcherTab.Control)
        {
            using var scroll = SkyScroll.Begin(this, body, scale);
            if (scroll.Active)
            {
                DrawControl(palette, scale);
            }
        }
        else
        {
            router.Draw(body, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
        }

        DrawBottomNav(navRect, palette, scale);
    }

    private void DrawView(SkywatcherRoute route, Rect area, int depth)
    {
        var scale = UiScale.Current;
        if (route.Screen == SkywatcherScreen.Browse)
        {
            var (browseKind, browseIsDay, browsePalette, _) = SkyDataFor(previewForecast);
            PaintSky(area, browseKind, browseIsDay, browsePalette, scale);
            using var scroll = SkyScroll.Begin(this, area, scale);
            if (scroll.Active)
            {
                DrawBrowse(browsePalette, scale);
            }

            return;
        }

        var (kind, isDay, palette, hasData) = SkyDataFor(detailForecast);
        PaintSky(area, kind, isDay, palette, scale);
        using var detailScroll = SkyScroll.Begin(this, area, scale);
        if (!detailScroll.Active)
        {
            return;
        }

        if (!hasData)
        {
            DrawEmpty(area, palette, scale);
            return;
        }

        var width = ImGui.GetContentRegionAvail().X;
        DrawHero(width, area, palette, kind, isDay, scale);
        SectionLabel(Loc.T(L.Skywatcher.NextFewHours), palette, scale);
        DrawHourly(palette, scale);
        SectionLabel(Loc.T(L.Skywatcher.Forecast), palette, scale);
        DrawForecastList(palette, scale);
        ImGui.Dummy(new Vector2(0f, 8f * scale));
    }

    private static (WeatherKind Kind, bool IsDay, SkyPalette Palette, bool HasData) SkyDataFor(
        IReadOnlyList<WeatherWindow> windows)
    {
        var bell = EorzeaTime.Now();
        var daylight = WeatherSky.Daylight(bell.Hour + bell.Minute / 60f);
        var isDay = daylight >= 0.5f;
        var hasData = windows.Count > 0;
        var kind = hasData ? WeatherSky.Classify(windows[0].Weather.EnglishKey) : WeatherKind.Clouds;
        var palette = WeatherSky.Blend(kind, hasData ? daylight : 0f);
        return (kind, isDay, palette, hasData);
    }

    private static void PaintSky(Rect area, WeatherKind kind, bool isDay, in SkyPalette palette, float scale)
    {
        WeatherSky.Paint(area, 0f, palette, kind, isDay);
        WeatherAmbience.Draw(ImGui.GetWindowDrawList(), area, 0f, kind, isDay, palette, scale, 1f, false);
    }

    private void DrawBottomNav(Rect nav, in SkyPalette palette, float scale)
    {
        var margin = 12f * scale;
        var bar = new Rect(new Vector2(nav.Min.X + margin, nav.Min.Y + 3f * scale),
            new Vector2(nav.Max.X - margin, nav.Max.Y - 9f * scale));
        WeatherCard.Panel(ImGui.GetWindowDrawList(), bar, palette, scale, bar.Height * 0.5f);
        var half = bar.Width * 0.5f;
        DrawNavItem(new Rect(bar.Min, new Vector2(bar.Min.X + half, bar.Max.Y)), FontAwesomeIcon.CloudSun,
            Loc.T(L.Skywatcher.Forecast), SkywatcherTab.Forecast, palette, scale);
        DrawNavItem(new Rect(new Vector2(bar.Min.X + half, bar.Min.Y), bar.Max), FontAwesomeIcon.SlidersH,
            Loc.T(L.Skywatcher.Control), SkywatcherTab.Control, palette, scale);
    }

    private void DrawNavItem(Rect cell, FontAwesomeIcon icon, string label, SkywatcherTab tab, in SkyPalette palette,
        float scale)
    {
        var active = activeTab == tab;
        var ink = active ? palette.Ink : palette.InkSoft;
        if (active)
        {
            var pill = cell.Inset(5f * scale);
            Squircle.Fill(ImGui.GetWindowDrawList(), pill.Min, pill.Max, pill.Height * 0.5f,
                ImGui.GetColorU32(palette.Ink with { W = 0.14f }));
        }

        ProgressRing.CenterIcon(new Vector2(cell.Center.X, cell.Min.Y + 15f * scale), icon, ink, 15f * scale);
        Typography.DrawCentered(new Vector2(cell.Center.X, cell.Min.Y + 33f * scale), label, ink,
            TextStyles.Caption1.Scale, active ? FontWeight.SemiBold : FontWeight.Regular);
        if (UiInteract.HoverClick(cell.Min, cell.Max))
        {
            activeTab = tab;
        }
    }

    private void DrawHero(float width, Rect area, in SkyPalette palette, WeatherKind kind, bool isDay, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var centerX = origin.X + width * 0.5f;
        var titleHeight = 0f;
        if (detailZone.Length > 0)
        {
            titleHeight = ShadowWrappedCentered(new Vector2(centerX, origin.Y + 16f * scale), detailZone, palette.Ink,
                TextStyles.Title2, width - 32f * scale, palette, scale);
        }

        var glyphBaseline = origin.Y + 100f * scale;
        var glyphCenter = new Vector2(centerX, MathF.Max(glyphBaseline, origin.Y + 16f * scale + titleHeight + 44f * scale));
        var glyphOverflow = MathF.Max(0f, glyphCenter.Y - glyphBaseline);
        var radius = 50f * scale;
        WeatherGlyph.Draw(kind, glyphCenter, radius, palette, isDay, SampleSky(palette, area, glyphCenter.Y));
        WeatherAmbience.Halo(ImGui.GetWindowDrawList(), glyphCenter, radius * 1.05f, palette.Glow,
            0.65f + 0.40f * Pulse.Wave(Pulse.Breath));
        ShadowCentered(new Vector2(centerX, origin.Y + 176f * scale + glyphOverflow), detailForecast[0].Weather.Name,
            palette.Ink, TextStyles.LargeTitle.Scale, FontWeight.Regular, palette, scale);
        ShadowCentered(new Vector2(centerX, origin.Y + 210f * scale + glyphOverflow), Summary(), palette.InkSoft,
            TextStyles.Subheadline.Scale, TextStyles.Subheadline.Weight, palette, scale);
        var heroHeight = 234f * scale + glyphOverflow;
        if (UiAnchors.Recording)
        {
            UiAnchors.Report("skywatcher.current", new Rect(origin, origin + new Vector2(width, heroHeight)));
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, heroHeight));
    }

    private void DrawHourly(in SkyPalette palette, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = 96f * scale;
        var card = new Rect(origin, origin + new Vector2(width, height));
        if (UiAnchors.Recording)
        {
            UiAnchors.Report("skywatcher.forecast", card);
        }

        DrawGlass(card, palette, scale);
        var inner = card.Inset(12f * scale);
        var count = Math.Min(detailForecast.Count, HourlyStripCount);
        var columnWidth = inner.Width / count;
        var drawList = ImGui.GetWindowDrawList();
        for (var index = 0; index < count; index++)
        {
            var window = detailForecast[index];
            var columnCenterX = inner.Min.X + columnWidth * (index + 0.5f);
            if (window.IsCurrent)
            {
                var pillMin = new Vector2(columnCenterX - columnWidth * 0.42f, inner.Min.Y - 2f * scale);
                var pillMax = new Vector2(columnCenterX + columnWidth * 0.42f, inner.Max.Y + 2f * scale);
                drawList.AddRectFilled(pillMin, pillMax, ImGui.GetColorU32(palette.Ink with { W = 0.12f }),
                    columnWidth * 0.30f);
            }

            var columnMaxWidth = MathF.Max(1f, columnWidth - 4f * scale);
            Marquee.DrawCentered(new MarqueeId("skywatcher.hourly.", index), ShortWhen(window), columnCenterX,
                inner.Min.Y + 10f * scale, columnMaxWidth, TextStyles.Footnote, palette.InkSoft, false);
            var glyphCenter = new Vector2(columnCenterX, inner.Min.Y + inner.Height * 0.62f);
            var glyphRadius = MathF.Min(columnWidth * 0.30f, inner.Height * 0.24f);
            DrawMini(window, glyphCenter, glyphRadius);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawForecastList(in SkyPalette palette, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var rowHeight = 42f * scale;
        var count = detailForecast.Count;
        var card = new Rect(origin, origin + new Vector2(width, count * rowHeight + 10f * scale));
        DrawGlass(card, palette, scale);
        var inner = card.Inset(5f * scale);
        var drawList = ImGui.GetWindowDrawList();
        var glyphX = inner.Min.X + 98f * scale;
        for (var index = 0; index < count; index++)
        {
            var window = detailForecast[index];
            var rowTop = inner.Min.Y + index * rowHeight;
            var rowCenterY = rowTop + rowHeight * 0.5f;
            if (index > 0)
            {
                drawList.AddLine(new Vector2(inner.Min.X + 12f * scale, rowTop),
                    new Vector2(inner.Max.X - 10f * scale, rowTop), ImGui.GetColorU32(palette.Ink with { W = 0.10f }),
                    1f);
            }

            var label = window.IsCurrent ? Loc.T(L.Skywatcher.Now) : BellLabel(window);
            var labelMaxWidth = MathF.Max(1f, glyphX - 8f * scale - (inner.Min.X + 12f * scale));
            var fittedLabel = Typography.FitText(label, labelMaxWidth, TextStyles.Body);
            var labelSize = Typography.Measure(fittedLabel);
            Typography.Draw(new Vector2(inner.Min.X + 12f * scale, rowCenterY - labelSize.Y * 0.5f), fittedLabel,
                window.IsCurrent ? palette.Ink : palette.InkSoft);
            DrawMini(window, new Vector2(glyphX, rowCenterY), 13f * scale);
            var nameMaxWidth = MathF.Max(1f, inner.Max.X - 10f * scale - (glyphX + 23f * scale));
            var name = Typography.FitText(window.Weather.Name, nameMaxWidth, 1f, FontWeight.Regular);
            var nameSize = Typography.Measure(name);
            Typography.Draw(new Vector2(inner.Max.X - 10f * scale - nameSize.X, rowCenterY - nameSize.Y * 0.5f), name,
                palette.Ink);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, card.Height));
    }

    private static void DrawMini(WeatherWindow window, Vector2 center, float radius)
    {
        var kind = WeatherSky.Classify(window.Weather.EnglishKey);
        var isDay = IsDayWindow(window);
        var scale = UiScale.Current;
        var half = radius + 3f * scale;
        var chip = new Rect(new Vector2(center.X - half, center.Y - half),
            new Vector2(center.X + half, center.Y + half));
        WeatherCard.Chip(ImGui.GetWindowDrawList(), chip, kind, isDay, scale);
    }

    private static void ShadowCentered(Vector2 center, string text, Vector4 color, float fontScale, FontWeight weight,
        in SkyPalette palette, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var shadow = new Vector4(0f, 0f, 0f, palette.LightSky ? 0.20f : 0.42f);
        Typography.DrawCentered(drawList, center + new Vector2(0f, 1.4f * scale), text, shadow, fontScale, weight);
        Typography.DrawCentered(drawList, center, text, color, fontScale, weight);
    }

    private static float ShadowWrappedCentered(Vector2 topCenter, string text, Vector4 color, in TextStyle style,
        float maxWidth, in SkyPalette palette, float scale)
    {
        var shadow = new Vector4(0f, 0f, 0f, palette.LightSky ? 0.20f : 0.42f);
        Typography.DrawWrappedCentered(topCenter + new Vector2(0f, 1.4f * scale), text, shadow, style, maxWidth);
        return Typography.DrawWrappedCentered(topCenter, text, color, style, maxWidth);
    }

    private static void DrawEmpty(Rect area, in SkyPalette palette, float scale)
    {
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var center = new Vector2(origin.X + width * 0.5f, origin.Y + ImGui.GetContentRegionAvail().Y * 0.4f);
        WeatherGlyph.Draw(WeatherKind.Clouds, center - new Vector2(0f, 28f * scale), 46f * scale, palette, false,
            SampleSky(palette, area, center.Y - 28f * scale));
        Typography.DrawCentered(new Vector2(center.X, center.Y + 48f * scale), Loc.T(L.Skywatcher.NoData),
            palette.InkSoft, 1.0f);
    }

    private static void SectionLabel(string title, in SkyPalette palette, float scale)
    {
        SectionLabelUpper(Loc.Culture.TextInfo.ToUpper(title), palette, scale);
    }

    private static void SectionLabelUpper(string text, in SkyPalette palette, float scale)
    {
        ImGui.Dummy(new Vector2(0f, 12f * scale));
        var style = TextStyles.FootnoteEmphasized;
        var origin = ImGui.GetCursorScreenPos() + new Vector2(4f * scale, 0f);
        var drawList = ImGui.GetWindowDrawList();
        var shadow = new Vector4(0f, 0f, 0f, palette.LightSky ? 0.14f : 0.34f);
        Typography.Draw(drawList, origin + new Vector2(0f, 1f * scale), text, shadow, style);
        Typography.Draw(drawList, origin, text, palette.InkSoft, style);
        var size = Typography.Measure(text, style);
        ImGui.Dummy(new Vector2(size.X + 8f * scale, size.Y + 6f * scale));
    }

    private static void DrawGlass(Rect card, in SkyPalette palette, float scale)
    {
        WeatherCard.Panel(ImGui.GetWindowDrawList(), card, palette, scale);
    }

    private string Summary()
    {
        if (detailForecast.Count < 2)
        {
            return detailForecast.Count == 1
                ? Loc.T(L.Skywatcher.Continuing, detailForecast[0].Weather.Name)
                : string.Empty;
        }

        var current = detailForecast[0].Weather.Id;
        for (var index = 1; index < detailForecast.Count; index++)
        {
            if (detailForecast[index].Weather.Id != current)
            {
                return $"{detailForecast[index].Weather.Name} {LongWhen(detailForecast[index])}";
            }
        }

        return Loc.T(L.Skywatcher.ForNextHours, detailForecast[0].Weather.Name);
    }

    private static Vector4 SampleSky(in SkyPalette palette, Rect screen, float y)
    {
        var fraction = screen.Height <= 0f ? 0f : Math.Clamp((y - screen.Min.Y) / screen.Height, 0f, 1f);
        return Vector4.Lerp(palette.Top, palette.Bottom, fraction);
    }

    private static bool IsDayWindow(WeatherWindow window)
    {
        float bell;
        if (window.IsCurrent)
        {
            var now = EorzeaTime.Now();
            bell = now.Hour + now.Minute / 60f;
        }
        else
        {
            bell = window.StartBell;
        }

        return WeatherSky.Daylight(bell) >= 0.5f;
    }

    private static string BellLabel(WeatherWindow window) => $"{window.StartBell:D2}:00";

    private static string ShortWhen(WeatherWindow window)
    {
        if (window.IsCurrent || window.MinutesFromNow <= 0)
        {
            return Loc.T(L.Skywatcher.Now);
        }

        return Loc.T(L.Time.MinutesShort, window.MinutesFromNow);
    }

    private static void BuildShortWhenLabels(List<string> into, IReadOnlyList<WeatherWindow> windows)
    {
        into.Clear();
        for (var index = 0; index < windows.Count; index++)
        {
            into.Add(ShortWhen(windows[index]));
        }
    }

    private static string LongWhen(WeatherWindow window)
    {
        if (window.IsCurrent || window.MinutesFromNow <= 0)
        {
            return Loc.T(L.Time.Now);
        }

        if (window.MinutesFromNow < 60)
        {
            return Loc.T(L.Time.InMinutes, window.MinutesFromNow);
        }

        var hours = window.MinutesFromNow / 60;
        var minutes = window.MinutesFromNow % 60;
        return minutes == 0 ? Loc.T(L.Time.InHours, hours) : Loc.T(L.Time.InHoursMinutes, hours, minutes);
    }

    public void Dispose()
    {
    }

    private readonly ref struct SkyScroll
    {
        private readonly SkywatcherApp owner;
        private readonly ImRaii.ChildDisposable child;
        private readonly IDisposable? padding;
        public readonly bool Active;

        private SkyScroll(SkywatcherApp owner, ImRaii.ChildDisposable child, IDisposable? padding, bool active)
        {
            this.owner = owner;
            this.child = child;
            this.padding = padding;
            Active = active;
        }

        public static SkyScroll Begin(SkywatcherApp owner, Rect area, float scale)
        {
            ImGui.SetCursorScreenPos(area.Min);
            var padding = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(14f * scale, 4f * scale));
            var child = ImRaii.Child("##sky", area.Size, false,
                DragScrollHost.ScrollFlags(ImGuiWindowFlags.NoBackground));
            if (!child)
            {
                return new SkyScroll(owner, child, padding, false);
            }

            AppSurface.ResetScrollOnNewVisit();
            var surface = DragScrollHost.Begin(ImGui.GetID("##sky"));
            owner.scrollSurface = surface;
            if (owner.pendingScrollReset)
            {
                surface.JumpToTop();
                owner.pendingScrollReset = false;
            }

            return new SkyScroll(owner, child, padding, true);
        }

        public void Dispose()
        {
            if (Active && owner.scrubbing)
            {
                owner.scrollSurface.CancelDrag();
            }

            padding?.Dispose();
            child.Dispose();
        }
    }
}
