using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
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

    public string DisplayName => "Skywatcher";

    public string Glyph => "W";

    public Vector4 Accent => new(0.28f, 0.68f, 0.92f, 1f);

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
        var screen = ScreenFrom(content, theme, scale);

        var bell = EorzeaTime.Now().Hour;
        var isDay = bell >= 6 && bell < 19;

        if (forecast.Count == 0)
        {
            DrawEmpty(screen, scale);
            DrawBack(content, context.Navigation, WeatherSky.Resolve(WeatherKind.Clouds, false).Ink, scale);
            return;
        }

        var kind = WeatherSky.Classify(forecast[0].Weather);
        var palette = WeatherSky.Resolve(kind, isDay);

        WeatherSky.Paint(screen, theme.ScreenRounding * scale, palette, kind, isDay);
        DrawBack(content, context.Navigation, palette.Ink, scale);

        var body = new Rect(new Vector2(content.Min.X, content.Min.Y + 40f * scale), content.Max);
        ImGui.SetCursorScreenPos(body.Min);
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(14f * scale, 4f * scale)))
        using (var child = ImRaii.Child("##sky", body.Size, false, ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoScrollbar))
        {
            if (!child)
            {
                return;
            }

            var width = ImGui.GetContentRegionAvail().X;
            DrawHero(width, screen, palette, kind, isDay, scale);
            SectionLabel("Next Few Hours", palette.InkSoft, scale);
            DrawHourly(screen, palette, scale);
            SectionLabel("Forecast", palette.InkSoft, scale);
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
            Typography.DrawCentered(new Vector2(centerX, origin.Y + 16f * scale), zone, palette.Ink, 1.3f, FontWeight.SemiBold);
        }

        var glyphCenter = new Vector2(centerX, origin.Y + 100f * scale);
        var radius = 50f * scale;
        ProgressRing.Glow(glyphCenter, radius * 1.05f, palette.Glow, 0.45f + 0.35f * Styling.Pulse(Styling.PulseBreath));
        WeatherGlyph.Draw(kind, glyphCenter, radius, palette, isDay, SampleSky(palette, screen, glyphCenter.Y));

        Typography.DrawCentered(new Vector2(centerX, origin.Y + 176f * scale), forecast[0].Weather, palette.Ink, 1.9f);
        Typography.DrawCentered(new Vector2(centerX, origin.Y + 210f * scale), Summary(), palette.InkSoft, 0.9f);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, 234f * scale));
    }

    private void DrawHourly(Rect screen, in SkyPalette palette, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = 96f * scale;
        var card = new Rect(origin, origin + new Vector2(width, height));
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
                drawList.AddRectFilled(pillMin, pillMax, ImGui.GetColorU32(palette.Ink with { W = 0.12f }), columnWidth * 0.30f);
            }

            Typography.DrawCentered(new Vector2(columnCenterX, inner.Min.Y + 10f * scale), ShortWhen(window), palette.InkSoft, 0.8f);

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
                drawList.AddLine(new Vector2(inner.Min.X + 12f * scale, rowTop), new Vector2(inner.Max.X - 10f * scale, rowTop), ImGui.GetColorU32(palette.Ink with { W = 0.10f }), 1f);
            }

            var label = window.IsCurrent ? "Now" : BellLabel(window);
            var labelSize = Typography.Measure(label);
            Typography.Draw(new Vector2(inner.Min.X + 12f * scale, rowCenterY - labelSize.Y * 0.5f), label, window.IsCurrent ? palette.Ink : palette.InkSoft);

            DrawMini(window, new Vector2(glyphX, rowCenterY), 13f * scale, screen);

            var name = window.Weather;
            var nameSize = Typography.Measure(name);
            Typography.Draw(new Vector2(inner.Max.X - 10f * scale - nameSize.X, rowCenterY - nameSize.Y * 0.5f), name, palette.Ink);
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
        WeatherGlyph.Draw(WeatherKind.Clouds, center - new Vector2(0f, 28f * scale), 46f * scale, palette, false, SampleSky(palette, screen, center.Y - 28f * scale));
        Typography.DrawCentered(new Vector2(center.X, center.Y + 48f * scale), "No weather data here", palette.InkSoft, 1.0f);
    }

    private static void SectionLabel(string title, Vector4 ink, float scale)
    {
        ImGui.Dummy(new Vector2(0f, 12f * scale));
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 4f * scale);
        using (Plugin.Fonts.Push(0.8f))
        using (ImRaii.PushColor(ImGuiCol.Text, ink))
        {
            ImGui.TextUnformatted(title.ToUpperInvariant());
        }

        ImGui.Dummy(new Vector2(0f, 6f * scale));
    }

    private static void DrawGlass(Rect card, in SkyPalette palette, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var lightSky = palette.Ink.X < 0.5f;
        var fill = lightSky ? new Vector4(0.10f, 0.12f, 0.16f, 0.10f) : new Vector4(1f, 1f, 1f, 0.10f);
        var rounding = 18f * scale;
        Squircle.Fill(drawList, card.Min, card.Max, rounding, ImGui.GetColorU32(fill));
        Squircle.Stroke(drawList, card.Min, card.Max, rounding, ImGui.GetColorU32(palette.Ink with { W = 0.14f }), 1f * scale);
        drawList.AddLine(new Vector2(card.Min.X + rounding, card.Min.Y + 1f), new Vector2(card.Max.X - rounding, card.Min.Y + 1f), ImGui.GetColorU32(palette.Ink with { W = lightSky ? 0.05f : 0.18f }), 1f);
    }

    private void DrawBack(Rect content, INavigator navigation, Vector4 ink, float scale)
    {
        var rowCenterY = content.Min.Y + 20f * scale;
        var hitMin = new Vector2(content.Min.X, content.Min.Y);
        var hitMax = new Vector2(content.Min.X + 46f * scale, content.Min.Y + 40f * scale);
        var hovered = ImGui.IsMouseHoveringRect(hitMin, hitMax);

        var tip = new Vector2(content.Min.X + 8f * scale, rowCenterY);
        var size = 7f * scale;
        var color = ImGui.GetColorU32(hovered ? ink : ink with { W = 0.82f });
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddLine(new Vector2(tip.X + size, tip.Y - size), tip, color, 2.4f * scale);
        drawList.AddLine(tip, new Vector2(tip.X + size, tip.Y + size), color, 2.4f * scale);

        if (!hovered)
        {
            return;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            navigation.Back();
        }
    }

    private string Summary()
    {
        if (forecast.Count < 2)
        {
            return forecast.Count == 1 ? $"{forecast[0].Weather} continuing" : string.Empty;
        }

        var current = forecast[0].Weather;
        for (var index = 1; index < forecast.Count; index++)
        {
            if (!string.Equals(forecast[index].Weather, current, StringComparison.Ordinal))
            {
                return $"{forecast[index].Weather} {LongWhen(forecast[index])}";
            }
        }

        return $"{current} for the next few hours";
    }

    private static Vector4 SampleSky(in SkyPalette palette, Rect screen, float y)
    {
        var fraction = screen.Height <= 0f ? 0f : Math.Clamp((y - screen.Min.Y) / screen.Height, 0f, 1f);
        return Vector4.Lerp(palette.Top, palette.Bottom, fraction);
    }

    private static Rect ScreenFrom(Rect content, PhoneTheme theme, float scale)
    {
        var min = new Vector2(content.Min.X - theme.SidePadding * scale, content.Min.Y - theme.TopZoneHeight * scale);
        var max = new Vector2(content.Max.X + theme.SidePadding * scale, content.Max.Y + theme.BottomZoneHeight * scale);
        return new Rect(min, max);
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
            return "Now";
        }

        if (window.MinutesFromNow < 60)
        {
            return $"{window.MinutesFromNow}m";
        }

        return $"{window.MinutesFromNow / 60}h";
    }

    private static string LongWhen(WeatherWindow window)
    {
        if (window.IsCurrent || window.MinutesFromNow <= 0)
        {
            return "now";
        }

        if (window.MinutesFromNow < 60)
        {
            return $"in {window.MinutesFromNow}m";
        }

        var hours = window.MinutesFromNow / 60;
        var minutes = window.MinutesFromNow % 60;
        return minutes == 0 ? $"in {hours}h" : $"in {hours}h {minutes}m";
    }

    public void Dispose()
    {
    }
}
