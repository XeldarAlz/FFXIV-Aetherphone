using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Net;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Core.Shell;

internal sealed class RateLimitPill
{
    private const float PresenceSmoothTime = 0.16f;
    private const float TopGap = 12f;
    private const float PadX = 13f;
    private const float PadY = 6f;
    private const float IconGap = 7f;
    private const float SlideDistance = 10f;
    private const float TextScale = 0.82f;

    private static readonly Vector4 CooldownTone = new(0.98f, 0.72f, 0.25f, 1f);

    private readonly HttpService http;
    private readonly AethernetSession session;
    private Spring presence;
    private string cachedBaseUrl = string.Empty;
    private string cachedHost = string.Empty;
    private int lastSeconds = 1;

    public RateLimitPill(HttpService http, AethernetSession session)
    {
        this.http = http;
        this.session = session;
    }

    public void Draw(Rect screen, PhoneTheme theme, float delta)
    {
        var remaining = http.PauseRemaining(ApiHost());
        presence.Step(remaining > TimeSpan.Zero ? 1f : 0f, PresenceSmoothTime, delta);
        var alpha = Math.Clamp(presence.Value, 0f, 1f);
        if (alpha < 0.02f)
        {
            return;
        }

        if (remaining > TimeSpan.Zero)
        {
            lastSeconds = Math.Max(1, (int)Math.Ceiling(remaining.TotalSeconds));
        }

        var scale = UiScale.Current;
        var label = Loc.T(L.Common.RateLimited, lastSeconds);
        var textSize = Typography.Measure(label, TextScale);
        var iconSize = textSize.Y * 0.86f;
        var width = PadX * 2f * scale + iconSize + IconGap * scale + textSize.X;
        var height = textSize.Y + PadY * 2f * scale;
        var island = StatusBar.BaseIsland(screen);
        var top = island.Max.Y + TopGap * scale - SlideDistance * scale * (1f - alpha);
        var bounds = new Rect(new Vector2(screen.Center.X - width * 0.5f, top),
            new Vector2(screen.Center.X + width * 0.5f, top + height));
        var drawList = ImGui.GetForegroundDrawList();
        drawList.PushClipRect(screen.Min, screen.Max, true);
        var rounding = height * 0.5f;
        Elevation.Draw(drawList, bounds.Min, bounds.Max, rounding, scale, 4f, 2f, 0.18f * alpha);
        drawList.AddRectFilled(bounds.Min, bounds.Max,
            ImGui.GetColorU32(Palette.WithAlpha(theme.Glass, alpha * theme.Glass.W)), rounding);
        drawList.AddRect(bounds.Min, bounds.Max,
            ImGui.GetColorU32(Palette.WithAlpha(CooldownTone, 0.42f * alpha)), rounding,
            ImDrawFlags.RoundCornersAll, 1.4f * scale);
        var iconCenter = new Vector2(bounds.Min.X + PadX * scale + iconSize * 0.5f, bounds.Center.Y);
        ProgressRing.CenterIcon(drawList, iconCenter, FontAwesomeIcon.HourglassHalf,
            Palette.WithAlpha(CooldownTone, alpha), iconSize);
        Typography.Draw(drawList,
            new Vector2(iconCenter.X + iconSize * 0.5f + IconGap * scale, bounds.Center.Y - textSize.Y * 0.5f),
            label, Palette.WithAlpha(ChromeInk.TextStrong, alpha), TextScale);
        drawList.PopClipRect();
    }

    private string ApiHost()
    {
        var baseUrl = session.BaseUrl;
        if (string.Equals(baseUrl, cachedBaseUrl, StringComparison.Ordinal))
        {
            return cachedHost;
        }

        cachedBaseUrl = baseUrl;
        cachedHost = Uri.TryCreate(baseUrl, UriKind.Absolute, out var parsed) ? parsed.Host : string.Empty;
        return cachedHost;
    }
}
