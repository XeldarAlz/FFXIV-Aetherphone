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
using Dalamud.Interface.Utility;
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
    private const float RefreshIntervalSeconds = 5f;
    private const float NavHeight = 60f;
    public string Id => "skywatcher";
    public string DisplayName => Loc.T(L.Apps.Skywatcher);
    public string Glyph => "W";
    public int BadgeCount => 0;
    private readonly WeatherService weather;
    private readonly WeatherControl control;
    private readonly List<WeatherWindow> forecast = new();
    private string zone = string.Empty;
    private float sinceRefresh;
    private SkywatcherTab activeTab;

    public SkywatcherApp(WeatherService weather, WeatherControl control)
    {
        this.weather = weather;
        this.control = control;
    }

    public void OnOpened()
    {
        activeTab = SkywatcherTab.Forecast;
        scrubbing = false;
        Refresh();
    }

    public void OnClosed()
    {
    }

    private void Refresh()
    {
        zone = weather.CurrentZone();
        weather.Forecast(forecast, WindowCount);
        sinceRefresh = 0f;
    }

    public void Draw(in PhoneContext context)
    {
        sinceRefresh += ImGui.GetIO().DeltaTime;
        if (sinceRefresh >= RefreshIntervalSeconds)
        {
            Refresh();
        }

        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        var content = context.Content;
        var screen = SceneChrome.ScreenFrom(content, theme, scale);
        var bell = EorzeaTime.Now();
        var daylight = WeatherSky.Daylight(bell.Hour + bell.Minute / 60f);
        var isDay = daylight >= 0.5f;
        var hasData = forecast.Count > 0;
        var kind = hasData ? WeatherSky.Classify(forecast[0].Weather.EnglishKey) : WeatherKind.Clouds;
        var palette = WeatherSky.Blend(kind, hasData ? daylight : 0f);
        WeatherSky.Paint(screen, theme.ScreenRounding * scale, palette, kind, isDay);
        WeatherAmbience.Draw(ImGui.GetWindowDrawList(), screen, theme.ScreenRounding * scale, kind, isDay, palette,
            scale, 1f, false);
        SceneChrome.BackChevron(content, context.Navigation, palette.Ink, scale);
        var navRect = new Rect(new Vector2(content.Min.X, content.Max.Y - NavHeight * scale), content.Max);
        var body = new Rect(new Vector2(content.Min.X, content.Min.Y + 40f * scale),
            new Vector2(content.Max.X, navRect.Min.Y));
        var skyKey = ImGui.GetID("##sky");
        ImGui.SetCursorScreenPos(body.Min);
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(14f * scale, 4f * scale)))
        using (var child = ImRaii.Child("##sky", body.Size, false,
                   DragScrollHost.ScrollFlags(ImGuiWindowFlags.NoBackground)))
        {
            if (child)
            {
                var surface = DragScrollHost.Begin(skyKey);
                DrawTab(screen, palette, kind, isDay, hasData, scale);
                if (scrubbing)
                {
                    surface.CancelDrag();
                }
            }
        }

        DrawBottomNav(navRect, palette, scale);
    }

    private void DrawTab(Rect screen, in SkyPalette palette, WeatherKind kind, bool isDay, bool hasData, float scale)
    {
        if (activeTab == SkywatcherTab.Control)
        {
            DrawControl(palette, scale);
            return;
        }

        if (!hasData)
        {
            DrawEmpty(screen, palette, scale);
            return;
        }

        var width = ImGui.GetContentRegionAvail().X;
        DrawHero(width, screen, palette, kind, isDay, scale);
        SectionLabel(Loc.T(L.Skywatcher.NextFewHours), palette, scale);
        DrawHourly(screen, palette, scale);
        SectionLabel(Loc.T(L.Skywatcher.Forecast), palette, scale);
        DrawForecastList(screen, palette, scale);
        ImGui.Dummy(new Vector2(0f, 8f * scale));
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

    private void DrawHero(float width, Rect screen, in SkyPalette palette, WeatherKind kind, bool isDay, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var centerX = origin.X + width * 0.5f;
        if (zone.Length > 0)
        {
            ShadowCentered(new Vector2(centerX, origin.Y + 16f * scale), zone, palette.Ink,
                TextStyles.Title2.Scale, TextStyles.Title2.Weight, palette, scale);
        }

        var glyphCenter = new Vector2(centerX, origin.Y + 100f * scale);
        var radius = 50f * scale;
        WeatherGlyph.Draw(kind, glyphCenter, radius, palette, isDay, SampleSky(palette, screen, glyphCenter.Y));
        WeatherAmbience.Halo(ImGui.GetWindowDrawList(), glyphCenter, radius * 1.05f, palette.Glow,
            0.65f + 0.40f * Pulse.Wave(Pulse.Breath));
        ShadowCentered(new Vector2(centerX, origin.Y + 176f * scale), forecast[0].Weather.Name, palette.Ink,
            TextStyles.LargeTitle.Scale, FontWeight.Regular, palette, scale);
        ShadowCentered(new Vector2(centerX, origin.Y + 210f * scale), Summary(), palette.InkSoft,
            TextStyles.Subheadline.Scale, TextStyles.Subheadline.Weight, palette, scale);
        if (UiAnchors.Recording)
        {
            UiAnchors.Report("skywatcher.current", new Rect(origin, origin + new Vector2(width, 234f * scale)));
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, 234f * scale));
    }

    private void DrawHourly(Rect screen, in SkyPalette palette, float scale)
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
        var count = Math.Min(forecast.Count, HourlyStripCount);
        var columnWidth = inner.Width / count;
        var drawList = ImGui.GetWindowDrawList();
        for (var index = 0; index < count; index++)
        {
            var window = forecast[index];
            var columnCenterX = inner.Min.X + columnWidth * (index + 0.5f);
            if (window.IsCurrent)
            {
                var pillMin = new Vector2(columnCenterX - columnWidth * 0.42f, inner.Min.Y - 2f * scale);
                var pillMax = new Vector2(columnCenterX + columnWidth * 0.42f, inner.Max.Y + 2f * scale);
                drawList.AddRectFilled(pillMin, pillMax, ImGui.GetColorU32(palette.Ink with { W = 0.12f }),
                    columnWidth * 0.30f);
            }

            Typography.DrawCentered(new Vector2(columnCenterX, inner.Min.Y + 10f * scale), ShortWhen(window),
                palette.InkSoft, TextStyles.Footnote);
            var glyphCenter = new Vector2(columnCenterX, inner.Min.Y + inner.Height * 0.62f);
            var glyphRadius = MathF.Min(columnWidth * 0.30f, inner.Height * 0.24f);
            DrawMini(window, glyphCenter, glyphRadius);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawForecastList(Rect screen, in SkyPalette palette, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var rowHeight = 42f * scale;
        var count = forecast.Count;
        var card = new Rect(origin, origin + new Vector2(width, count * rowHeight + 10f * scale));
        DrawGlass(card, palette, scale);
        var inner = card.Inset(5f * scale);
        var drawList = ImGui.GetWindowDrawList();
        var glyphX = inner.Min.X + 98f * scale;
        for (var index = 0; index < count; index++)
        {
            var window = forecast[index];
            var rowTop = inner.Min.Y + index * rowHeight;
            var rowCenterY = rowTop + rowHeight * 0.5f;
            if (index > 0)
            {
                drawList.AddLine(new Vector2(inner.Min.X + 12f * scale, rowTop),
                    new Vector2(inner.Max.X - 10f * scale, rowTop), ImGui.GetColorU32(palette.Ink with { W = 0.10f }),
                    1f);
            }

            var label = window.IsCurrent ? Loc.T(L.Skywatcher.Now) : BellLabel(window);
            var labelSize = Typography.Measure(label);
            Typography.Draw(new Vector2(inner.Min.X + 12f * scale, rowCenterY - labelSize.Y * 0.5f), label,
                window.IsCurrent ? palette.Ink : palette.InkSoft);
            DrawMini(window, new Vector2(glyphX, rowCenterY), 13f * scale);
            var name = window.Weather.Name;
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
        var scale = ImGuiHelpers.GlobalScale;
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

    private static void DrawEmpty(Rect screen, in SkyPalette palette, float scale)
    {
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var center = new Vector2(origin.X + width * 0.5f, origin.Y + ImGui.GetContentRegionAvail().Y * 0.4f);
        WeatherGlyph.Draw(WeatherKind.Clouds, center - new Vector2(0f, 28f * scale), 46f * scale, palette, false,
            SampleSky(palette, screen, center.Y - 28f * scale));
        Typography.DrawCentered(new Vector2(center.X, center.Y + 48f * scale), Loc.T(L.Skywatcher.NoData),
            palette.InkSoft, 1.0f);
    }

    private static void SectionLabel(string title, in SkyPalette palette, float scale)
    {
        ImGui.Dummy(new Vector2(0f, 12f * scale));
        var text = Loc.Culture.TextInfo.ToUpper(title);
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
}
