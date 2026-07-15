using System.Numerics;
using Aetherphone.Core.Animation;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Skywatcher;

internal sealed class SkywatcherApp : IPhoneApp
{
    private const int WindowCount = 8;
    private const float RefreshIntervalSeconds = 5f;
    public string Id => "skywatcher";
    public string DisplayName => Loc.T(L.Apps.Skywatcher);
    public string Glyph => "W";
    public int BadgeCount => 0;
    private readonly WeatherService weather;
    private readonly List<WeatherWindow> forecast = new();
    private string zone = string.Empty;
    private float sinceRefresh;

    public SkywatcherApp(WeatherService weather)
    {
        this.weather = weather;
    }

    public void OnOpened() => Refresh();

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
        if (forecast.Count == 0)
        {
            DrawEmpty(screen, scale);
            SceneChrome.BackChevron(content, context.Navigation, WeatherSky.Resolve(WeatherKind.Clouds, false).Ink,
                scale);
            return;
        }

        var kind = WeatherSky.Classify(forecast[0].Weather);
        var palette = WeatherSky.Blend(kind, daylight);
        WeatherSky.Paint(screen, theme.ScreenRounding * scale, palette, kind, isDay);
        WeatherAmbience.Draw(ImGui.GetWindowDrawList(), screen, theme.ScreenRounding * scale, kind, isDay, palette,
            scale, 1f, false);
        SceneChrome.BackChevron(content, context.Navigation, palette.Ink, scale);
        var body = new Rect(new Vector2(content.Min.X, content.Min.Y + 40f * scale), content.Max);
        ImGui.SetCursorScreenPos(body.Min);
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(14f * scale, 4f * scale)))
        using (var child = ImRaii.Child("##sky", body.Size, false,
                   ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoScrollbar))
        {
            if (!child)
            {
                return;
            }

            var width = ImGui.GetContentRegionAvail().X;
            DrawHero(width, screen, palette, kind, isDay, scale);
            SectionLabel(Loc.T(L.Skywatcher.NextFewHours), palette.InkSoft, scale);
            DrawHourly(screen, palette, scale);
            SectionLabel(Loc.T(L.Skywatcher.Forecast), palette.InkSoft, scale);
            DrawForecastList(screen, palette, scale);
            ImGui.Dummy(new Vector2(0f, 8f * scale));
        }
    }

    private void DrawHero(float width, Rect screen, in SkyPalette palette, WeatherKind kind, bool isDay, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var centerX = origin.X + width * 0.5f;
        if (zone.Length > 0)
        {
            Typography.DrawCentered(new Vector2(centerX, origin.Y + 16f * scale), zone, palette.Ink,
                TextStyles.Title2.Scale, TextStyles.Title2.Weight);
        }

        var glyphCenter = new Vector2(centerX, origin.Y + 100f * scale);
        var radius = 50f * scale;
        WeatherGlyph.Draw(kind, glyphCenter, radius, palette, isDay, SampleSky(palette, screen, glyphCenter.Y));
        WeatherAmbience.Halo(ImGui.GetWindowDrawList(), glyphCenter, radius * 1.05f, palette.Glow,
            0.65f + 0.40f * Pulse.Wave(Pulse.Breath));
        Typography.DrawCentered(new Vector2(centerX, origin.Y + 176f * scale), forecast[0].Weather, palette.Ink,
            TextStyles.LargeTitle.Scale, FontWeight.Regular);
        Typography.DrawCentered(new Vector2(centerX, origin.Y + 210f * scale), Summary(), palette.InkSoft,
            TextStyles.Subheadline);
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
        var count = forecast.Count;
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
            var glyphRadius = MathF.Min(columnWidth * 0.34f, inner.Height * 0.28f);
            DrawMini(window, glyphCenter, glyphRadius, screen);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawForecastList(Rect screen, in SkyPalette palette, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var rowHeight = 38f * scale;
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
            DrawMini(window, new Vector2(glyphX, rowCenterY), 13f * scale, screen);
            var name = window.Weather;
            var nameSize = Typography.Measure(name);
            Typography.Draw(new Vector2(inner.Max.X - 10f * scale - nameSize.X, rowCenterY - nameSize.Y * 0.5f), name,
                palette.Ink);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, card.Height));
    }

    private void DrawMini(WeatherWindow window, Vector2 center, float radius, Rect screen)
    {
        var kind = WeatherSky.Classify(window.Weather);
        var isDay = IsDayWindow(window);
        var palette = WeatherSky.Resolve(kind, isDay);
        WeatherGlyph.Draw(kind, center, radius, palette, isDay, SampleSky(palette, screen, center.Y));
    }

    private void DrawEmpty(Rect screen, float scale)
    {
        var kind = WeatherKind.Clouds;
        var palette = WeatherSky.Resolve(kind, false);
        WeatherSky.Paint(screen, PhoneTheme.Default.ScreenRounding * scale, palette, kind, false);
        var center = screen.Center;
        WeatherGlyph.Draw(WeatherKind.Clouds, center - new Vector2(0f, 28f * scale), 46f * scale, palette, false,
            SampleSky(palette, screen, center.Y - 28f * scale));
        Typography.DrawCentered(new Vector2(center.X, center.Y + 48f * scale), Loc.T(L.Skywatcher.NoData),
            palette.InkSoft, 1.0f);
    }

    private static void SectionLabel(string title, Vector4 ink, float scale)
    {
        ImGui.Dummy(new Vector2(0f, 12f * scale));
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 4f * scale);
        using (Plugin.Fonts.Push(TextStyles.FootnoteEmphasized.Scale, TextStyles.FootnoteEmphasized.Weight))
        using (ImRaii.PushColor(ImGuiCol.Text, ink))
        {
            Typography.Plain(Loc.Culture.TextInfo.ToUpper(title));
        }

        ImGui.Dummy(new Vector2(0f, 6f * scale));
    }

    private static void DrawGlass(Rect card, in SkyPalette palette, float scale)
    {
        Material.Glass(ImGui.GetWindowDrawList(), card.Min, card.Max, Metrics.Radius.Lg * scale, palette.Ink, scale);
    }

    private string Summary()
    {
        if (forecast.Count < 2)
        {
            return forecast.Count == 1 ? Loc.T(L.Skywatcher.Continuing, forecast[0].Weather) : string.Empty;
        }

        var current = forecast[0].Weather;
        for (var index = 1; index < forecast.Count; index++)
        {
            if (!string.Equals(forecast[index].Weather, current, StringComparison.Ordinal))
            {
                return $"{forecast[index].Weather} {LongWhen(forecast[index])}";
            }
        }

        return Loc.T(L.Skywatcher.ForNextHours, current);
    }

    private static Vector4 SampleSky(in SkyPalette palette, Rect screen, float y)
    {
        var fraction = screen.Height <= 0f ? 0f : Math.Clamp((y - screen.Min.Y) / screen.Height, 0f, 1f);
        return Vector4.Lerp(palette.Top, palette.Bottom, fraction);
    }

    private static bool IsDayWindow(WeatherWindow window)
    {
        var midpoint = (window.StartBell + 4) % 24;
        return midpoint >= 6 && midpoint < 19;
    }

    private static string BellLabel(WeatherWindow window) => $"{window.StartBell:D2}:00";

    private static string ShortWhen(WeatherWindow window)
    {
        if (window.IsCurrent || window.MinutesFromNow <= 0)
        {
            return Loc.T(L.Skywatcher.Now);
        }

        if (window.MinutesFromNow < 60)
        {
            return Loc.T(L.Time.MinutesShort, window.MinutesFromNow);
        }

        return Loc.T(L.Time.HoursShort, window.MinutesFromNow / 60);
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
