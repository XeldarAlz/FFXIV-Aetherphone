using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

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

        AppHeader.Draw(context, DisplayName);

        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        var content = context.Content;
        var body = new Rect(new Vector2(content.Min.X, content.Min.Y + AppHeader.Height * scale), content.Max);

        if (forecast.Count == 0)
        {
            Typography.DrawCentered(body.Center, "Weather unavailable here", theme.TextMuted);
            return;
        }

        using (AppSurface.Begin(body))
        {
            DrawCurrent(theme);

            SettingsSection.Header("Forecast", theme);
            var card = GroupCard.Begin(theme, forecast.Count);
            for (var index = 0; index < forecast.Count; index++)
            {
                SettingsRow.Info(card.NextRow(), forecast[index].Weather, WhenLabel(forecast[index]), theme);
            }

            card.End();
        }
    }

    private void DrawCurrent(PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var centerX = origin.X + width * 0.5f;

        if (zone.Length > 0)
        {
            Typography.DrawCentered(new Vector2(centerX, origin.Y + 8f * scale), zone, theme.TextMuted, 0.9f);
        }

        Typography.DrawCentered(new Vector2(centerX, origin.Y + 36f * scale), forecast[0].Weather, theme.TextStrong, 2.0f);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, 66f * scale));
    }

    private static string WhenLabel(WeatherWindow window)
    {
        if (window.IsCurrent || window.MinutesFromNow <= 0)
        {
            return "Now";
        }

        if (window.MinutesFromNow < 60)
        {
            return $"in {window.MinutesFromNow}m";
        }

        return $"in {window.MinutesFromNow / 60}h {window.MinutesFromNow % 60}m";
    }

    public void Dispose()
    {
    }
}
