using Aetherphone.Core;
using Aetherphone.Core.Game;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;

namespace Aetherphone.Apps.Skywatcher.Widgets;

internal readonly struct WeatherInk
{
    private const float DarkAmbience = 0.6f;
    private const float RuleAlpha = 0.16f;

    public readonly WidgetInk Base;
    public readonly Vector4 Primary;
    public readonly Vector4 Secondary;
    public readonly Vector4 Rule;

    public WeatherInk(in WidgetInk ink, in SkyPalette sky)
    {
        Base = ink;
        if (ink.KeepsOwnColors)
        {
            Primary = ink.Fade(sky.Ink);
            Secondary = ink.Fade(sky.InkSoft);
            Rule = ink.Fade(sky.Ink with { W = RuleAlpha });
            return;
        }

        Primary = ink.Primary;
        Secondary = ink.Secondary;
        Rule = ink.Separator;
    }

    public float AmbienceStrength => Base.Mode == WidgetMode.Dark ? DarkAmbience : 1f;

    public GlyphInk Glyph(WeatherKind kind, bool isDay)
    {
        var natural = GlyphInk.Natural(WeatherSky.Resolve(kind, isDay), isDay);
        return new GlyphInk(Map(natural.Glow), Map(natural.Cloud), Map(natural.Drop), Map(natural.Flake), default);
    }

    private Vector4 Map(Vector4 color) => Base.KeepsOwnColors ? Base.Fade(color) : Base.Accent(color);
}

internal static class WeatherWidgetPaint
{
    private const float DaylightThreshold = 0.5f;
    private const int MinutesPerHour = 60;

    public static bool IsDay(in WeatherWindow window, EorzeaTime now)
    {
        var bell = window.IsCurrent ? now.Hour + now.Minute / 60f : window.StartBell;
        return WeatherSky.Daylight(bell) >= DaylightThreshold;
    }

    public static SkyPalette Sky(WeatherKind kind, EorzeaTime now) =>
        WeatherSky.Blend(kind, WeatherSky.Daylight(now.Hour + now.Minute / 60f));

    public static void Background(in WidgetContext context, in SkyPalette sky) =>
        WidgetChrome.Container(context, sky.Top, sky.Bottom);

    public static void Ambience(in WidgetContext context, in WeatherInk ink, WeatherKind kind, bool isDay,
        in SkyPalette sky)
    {
        if (context.Opacity <= 0f)
        {
            return;
        }

        var radius = WidgetChrome.Radius(context.Scale);
        if (ink.Base.Mode is WidgetMode.Tinted or WidgetMode.Clear)
        {
            WeatherAmbience.DrawMono(context.DrawList, context.Bounds, radius, kind, isDay, ink.Base.Primary,
                context.Scale, context.Opacity);
            return;
        }

        WeatherAmbience.Draw(context.DrawList, context.Bounds, radius, kind, isDay, sky, context.Scale,
            context.Opacity * ink.AmbienceStrength);
    }

    public static string Bell(int bell) => TimeText.Clock(new DateTime(1, 1, 1, bell % 24, 0, 0));

    public static string Clock(EorzeaTime time) => TimeText.Clock(new DateTime(1, 1, 1, time.Hour, time.Minute, 0));

    public static string When(ref CachedText cache, in WeatherWindow window)
    {
        var minutes = window.IsCurrent ? -1 : Math.Max(0, window.MinutesFromNow);
        if (cache.IsCurrent(minutes))
        {
            return cache.Value;
        }

        if (minutes <= 0)
        {
            return cache.Store(minutes, Loc.T(L.Skywatcher.Now));
        }

        return cache.Store(minutes, minutes < MinutesPerHour
            ? Loc.T(L.Time.MinutesShort, minutes)
            : Loc.T(L.Time.HoursShort, minutes / MinutesPerHour));
    }

    public static string Until(ref CachedText cache, in WeatherWindow window)
    {
        var minutes = Math.Max(0, window.MinutesFromNow);
        if (cache.IsCurrent(minutes))
        {
            return cache.Value;
        }

        if (minutes < MinutesPerHour)
        {
            return cache.Store(minutes, Loc.T(L.Time.InMinutes, Math.Max(1, minutes)));
        }

        var remainder = minutes % MinutesPerHour;
        return cache.Store(minutes, remainder == 0
            ? Loc.T(L.Time.InHours, minutes / MinutesPerHour)
            : Loc.T(L.Time.InHoursMinutes, minutes / MinutesPerHour, remainder));
    }

    public static int NextChange(List<WeatherWindow> forecast)
    {
        if (forecast.Count == 0)
        {
            return -1;
        }

        var current = forecast[0].Weather.Id;
        for (var index = 1; index < forecast.Count; index++)
        {
            if (forecast[index].Weather.Id != current)
            {
                return index;
            }
        }

        return -1;
    }

    public static string ChangeLine(ref CachedText cache, List<WeatherWindow> forecast, int changeIndex)
    {
        if (changeIndex < 0)
        {
            return cache.IsCurrent(-1) ? cache.Value : cache.Store(-1, Loc.T(L.WidgetsLife.SteadyForNow));
        }

        var window = forecast[changeIndex];
        var minutes = Math.Max(1, window.MinutesFromNow);
        var key = window.Weather.Id * 100_000L + minutes;
        if (cache.IsCurrent(key))
        {
            return cache.Value;
        }

        var span = minutes < MinutesPerHour
            ? Loc.T(L.Time.MinutesShort, minutes)
            : Loc.T(L.Time.HoursShort, (minutes + MinutesPerHour / 2) / MinutesPerHour);
        return cache.Store(key, Loc.T(L.WidgetsLife.ChangeIn, window.Weather.Name, span));
    }

    public static float Relevance(List<WeatherWindow> forecast)
    {
        var change = NextChange(forecast);
        if (change < 0)
        {
            return 0f;
        }

        var window = forecast[change];
        if (window.MinutesFromNow > 15)
        {
            return 0.1f;
        }

        var kind = WeatherSky.Classify(window.Weather.EnglishKey);
        return kind is WeatherKind.Rain or WeatherKind.Thunder or WeatherKind.Snow or WeatherKind.Sand or
            WeatherKind.Wind or WeatherKind.Gloom
            ? 0.8f
            : 0.5f;
    }

    public static float TextRight(in WidgetContext context, float right, float top, string text, Vector4 color,
        in TextStyle style, float maxWidth)
    {
        var fitted = WidgetText.Fit(text, maxWidth, style, out var scale);
        var size = Typography.Measure(fitted, scale, style.Weight);
        Typography.Draw(context.DrawList, new Vector2(right - size.X, top), fitted, color, scale, style.Weight);
        return size.Y;
    }

    public static float TextCentered(in WidgetContext context, float centerX, float top, string text, Vector4 color,
        in TextStyle style, float maxWidth)
    {
        var fitted = WidgetText.Fit(text, maxWidth, style, out var scale);
        var size = Typography.Measure(fitted, scale, style.Weight);
        Typography.Draw(context.DrawList, new Vector2(centerX - size.X * 0.5f, top), fitted, color, scale,
            style.Weight);
        return size.Y;
    }
}
