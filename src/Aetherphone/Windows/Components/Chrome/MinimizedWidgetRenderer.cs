using Aetherphone.Core;
using Aetherphone.Core.Activity;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Shell;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;

namespace Aetherphone.Windows.Components;

internal static class MinimizedWidgetRenderer
{
    private const float CaptionScale = 0.58f;
    private const float ValueScale = 0.82f;
    private const float CaptionGap = 1f;
    private const float WeatherGlyphRadius = 11f;
    private const float WeatherGap = 3f;
    private const float RingRadius = 19f;
    private const float RingThicknessFactor = 0.26f;
    private const float RingGapFactor = 0.09f;
    private const float AmountIconSize = 13f;
    private const float AmountGap = 4f;
    private const string Placeholder = "-";
    private const string HeightSample = "0";

    private static int lineHeightFrame = -1;
    private static float captionHeight;
    private static float valueHeight;

    public static float Height(MinimizedPart part, float scale)
    {
        switch (part)
        {
            case MinimizedPart.Weather:
                return WeatherGlyphRadius * 2f * scale + WeatherGap * scale + CaptionHeight();
            case MinimizedPart.Gil:
            case MinimizedPart.Coin:
                return MathF.Max(AmountIconSize * scale, ValueHeight());
            case MinimizedPart.Rings:
                return RingRadius * 2f * scale;
            default:
                return CaptionHeight() + CaptionGap * scale + ValueHeight();
        }
    }

    public static void Draw(ImDrawListPtr drawList, Rect rect, MinimizedPart part, MinimizedFeed feed,
        Configuration configuration, PhoneTheme theme, float alpha, float scale)
    {
        switch (part)
        {
            case MinimizedPart.EorzeaClock:
                DrawEorzeaClock(drawList, rect, theme, alpha, scale);
                break;
            case MinimizedPart.Weather:
                DrawWeather(drawList, rect, feed, theme, alpha, scale);
                break;
            case MinimizedPart.Resets:
                DrawResets(drawList, rect, theme, alpha, scale);
                break;
            case MinimizedPart.Gil:
                DrawGil(drawList, rect, feed, theme, alpha, scale);
                break;
            case MinimizedPart.Coin:
                DrawCoin(drawList, rect, feed, theme, alpha);
                break;
            case MinimizedPart.Ventures:
                DrawVentures(drawList, rect, feed, theme, alpha, scale);
                break;
            case MinimizedPart.Rings:
                DrawRings(drawList, rect, feed, configuration, theme, alpha, scale);
                break;
        }
    }

    private static void DrawEorzeaClock(ImDrawListPtr drawList, Rect rect, PhoneTheme theme, float alpha, float scale)
    {
        var bell = EorzeaTime.Now();
        DrawCaptionAndValue(drawList, rect, "minimized.eorzea", Loc.T(L.Home.Eorzea), bell.Formatted, theme.Accent,
            theme, alpha, scale);
    }

    private static void DrawWeather(ImDrawListPtr drawList, Rect rect, MinimizedFeed feed, PhoneTheme theme,
        float alpha, float scale)
    {
        feed.EnsureWeather();
        var bell = EorzeaTime.Now();
        var daylight = WeatherSky.Daylight(bell.Hour + bell.Minute / 60f);
        var isDay = daylight >= 0.5f;
        var kind = feed.HasWeather ? WeatherSky.Classify(feed.WeatherKey) : WeatherKind.Clouds;
        var palette = WeatherSky.Resolve(kind, isDay);
        var radius = WeatherGlyphRadius * scale;
        var glyphCenter = new Vector2(rect.Center.X, rect.Min.Y + radius);
        WeatherGlyph.Draw(drawList, kind, glyphCenter, radius, palette, isDay, theme.ScreenBase, alpha);
        var name = feed.HasWeather ? feed.WeatherName : Loc.T(L.Skywatcher.NoData);
        Marquee.DrawCenteredAuto(drawList, "minimized.weather.name", name, rect.Center.X,
            glyphCenter.Y + radius + WeatherGap * scale, rect.Width, CaptionStyle(),
            Palette.WithAlpha(theme.TextMuted, alpha));
    }

    private static void DrawResets(ImDrawListPtr drawList, Rect rect, PhoneTheme theme, float alpha, float scale)
    {
        var utcNow = DateTime.UtcNow;
        var next = GameSchedule.NextDailyReset(utcNow);
        var label = L.Timers.DailyReset;
        var grandCompany = GameSchedule.NextGrandCompanyReset(utcNow);
        if (grandCompany < next)
        {
            next = grandCompany;
            label = L.Timers.GrandCompanyReset;
        }

        var weekly = GameSchedule.NextWeeklyReset(utcNow);
        if (weekly < next)
        {
            next = weekly;
            label = L.Timers.WeeklyReset;
        }

        DrawCaptionAndValue(drawList, rect, "minimized.resets", Loc.T(label), Countdown(next - utcNow),
            theme.TextStrong, theme, alpha, scale);
    }

    private static void DrawGil(ImDrawListPtr drawList, Rect rect, MinimizedFeed feed, PhoneTheme theme, float alpha,
        float scale)
    {
        var style = ValueStyle();
        var text = feed.GilText();
        var textSize = Typography.Measure(text, style);
        var iconId = feed.GilIconId();
        var iconSize = iconId == 0 ? 0f : AmountIconSize * scale;
        var gap = iconSize > 0f ? AmountGap * scale : 0f;
        var left = rect.Center.X - (iconSize + gap + textSize.X) * 0.5f;
        if (iconSize > 0f)
        {
            var iconMin = new Vector2(left, rect.Center.Y - iconSize * 0.5f);
            var texture = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(iconId)).GetWrapOrEmpty();
            drawList.AddImage(texture.Handle, iconMin, iconMin + new Vector2(iconSize, iconSize), Vector2.Zero,
                Vector2.One, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha)));
        }

        Typography.Draw(drawList, new Vector2(left + iconSize + gap, rect.Center.Y - textSize.Y * 0.5f), text,
            Palette.WithAlpha(theme.TextStrong, alpha), style);
    }

    private static void DrawCoin(ImDrawListPtr drawList, Rect rect, MinimizedFeed feed, PhoneTheme theme, float alpha)
    {
        var style = ValueStyle();
        var text = feed.CoinText();
        var size = CurrencyGlyph.MeasureAmount(text, style);
        CurrencyGlyph.DrawAmount(drawList, new Vector2(rect.Center.X - size.X * 0.5f, rect.Center.Y - size.Y * 0.5f),
            text, CurrencyKind.Coins, Palette.WithAlpha(theme.TextStrong, alpha), style, alpha);
    }

    private static void DrawVentures(ImDrawListPtr drawList, Rect rect, MinimizedFeed feed, PhoneTheme theme,
        float alpha, float scale)
    {
        feed.EnsureVentures();
        var value = Placeholder;
        var tint = theme.TextStrong;
        if (feed.VenturesReady > 0)
        {
            value = Loc.T(L.Timers.Ready);
            tint = theme.ToggleOn;
        }
        else if (feed.HasRunningVenture)
        {
            value = Countdown(feed.NextVentureUtc - DateTime.UtcNow);
        }
        else if (feed.RetainersKnown)
        {
            value = Loc.T(L.Timers.NoVenture);
        }

        DrawCaptionAndValue(drawList, rect, "minimized.ventures", Loc.T(L.Timers.Retainers), value, tint, theme,
            alpha, scale);
    }

    private static void DrawRings(ImDrawListPtr drawList, Rect rect, MinimizedFeed feed, Configuration configuration,
        PhoneTheme theme, float alpha, float scale)
    {
        var day = feed.Today;
        var radius = RingRadius * scale;
        var center = new Vector2(rect.Center.X, rect.Min.Y + radius);
        var thickness = radius * RingThicknessFactor;
        var gap = radius * RingGapFactor;
        var middle = radius - thickness - gap;
        var inner = middle - thickness - gap;
        var track = Palette.WithAlpha(theme.TextStrong, 0.12f * alpha);
        DrawRing(drawList, center, radius, thickness,
            day is null ? 0f : ActivityGoals.ProgressFraction(configuration, day), ActivityRings.RingOneTint, track,
            alpha);
        DrawRing(drawList, center, middle, thickness,
            day is null ? 0f : ActivityGoals.AdventureFraction(configuration, day), ActivityRings.RingTwoTint, track,
            alpha);
        DrawRing(drawList, center, inner, thickness,
            day is null ? 0f : ActivityGoals.FortuneFraction(configuration, day), ActivityRings.RingThreeTint, track,
            alpha);
    }

    private static void DrawRing(ImDrawListPtr drawList, Vector2 center, float radius, float thickness,
        float fraction, Vector4 tint, Vector4 track, float alpha)
    {
        ProgressRing.Track(drawList, center, radius, thickness, track);
        ProgressRing.Fill(drawList, center, radius, thickness, fraction, Palette.WithAlpha(tint, alpha));
    }

    private static void DrawCaptionAndValue(ImDrawListPtr drawList, Rect rect, string id, string caption, string value,
        Vector4 valueTint, PhoneTheme theme, float alpha, float scale)
    {
        Marquee.DrawCenteredAuto(drawList, id, caption, rect.Center.X, rect.Min.Y, rect.Width, CaptionStyle(),
            Palette.WithAlpha(theme.TextMuted, alpha));
        Marquee.DrawCenteredAuto(drawList, new MarqueeId(id, ".value"), value, rect.Center.X,
            rect.Min.Y + CaptionHeight() + CaptionGap * scale, rect.Width, ValueStyle(),
            Palette.WithAlpha(valueTint, alpha));
    }

    private static string Countdown(TimeSpan remaining)
    {
        if (remaining <= TimeSpan.Zero)
        {
            return Loc.T(L.Time.Now);
        }

        var totalMinutes = (int)remaining.TotalMinutes;
        if (totalMinutes < 60)
        {
            return Loc.T(L.Time.MinutesShort, Math.Max(1, totalMinutes));
        }

        var totalHours = totalMinutes / 60;
        return totalHours < 24 ? Loc.T(L.Time.HoursShort, totalHours) : Loc.T(L.Time.DaysShort, totalHours / 24);
    }

    private static TextStyle CaptionStyle() => new(Text(CaptionScale), FontWeight.Medium);

    private static TextStyle ValueStyle() => new(Text(ValueScale), FontWeight.SemiBold);

    private static float CaptionHeight()
    {
        RefreshLineHeights();
        return captionHeight;
    }

    private static float ValueHeight()
    {
        RefreshLineHeights();
        return valueHeight;
    }

    private static void RefreshLineHeights()
    {
        var frame = ImGui.GetFrameCount();
        if (frame == lineHeightFrame)
        {
            return;
        }

        lineHeightFrame = frame;
        captionHeight = Typography.Measure(HeightSample, CaptionStyle()).Y;
        valueHeight = Typography.Measure(HeightSample, ValueStyle()).Y;
    }

    private static float Text(float scale) => UiScale.MinimizedText(scale);
}
