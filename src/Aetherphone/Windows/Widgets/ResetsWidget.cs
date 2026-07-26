using Aetherphone.Core;
using Aetherphone.Core.Game;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;

namespace Aetherphone.Windows.Widgets;

internal sealed class ResetsWidget : IHomeWidget
{
    private static readonly Vector4 WeeklyColor = new(0.36f, 0.65f, 1f, 1f);
    private static readonly Vector4 GrandCompanyColor = new(1f, 0.63f, 0.32f, 1f);

    public string Id => "timers.resets";
    public string DisplayName => Loc.T(L.Apps.Timers);
    public string AppId => "timers";
    public WidgetSizeSet Sizes => WidgetSizeSet.Small | WidgetSizeSet.Medium;

    public void Draw(in WidgetContext context)
    {
        WidgetChrome.Card(context.DrawList, context.Bounds, context.Scale, context.Opacity);
        var utcNow = DateTime.UtcNow;
        if (context.Size == WidgetSize.Small)
        {
            DrawSmall(context, utcNow);
            return;
        }

        DrawMedium(context, utcNow);
    }

    private static void DrawSmall(in WidgetContext context, DateTime utcNow)
    {
        var bounds = context.Bounds;
        var scale = context.Scale;
        var pad = 13f * scale;
        var label = Loc.T(L.Timers.DailyReset);
        WidgetChrome.Eyebrow(context.DrawList, new Vector2(bounds.Min.X + pad, bounds.Min.Y + pad), label,
            context.Theme.TextMuted, scale, context.Opacity);
        var center = new Vector2(bounds.Center.X, bounds.Center.Y + 7f * scale);
        var radius = MathF.Min(bounds.Width, bounds.Height) * 0.5f - 27f * scale;
        DrawRing(context, center, radius, GameSchedule.NextDailyReset(utcNow), utcNow, TimeSpan.FromDays(1),
            context.Theme.Accent);
    }

    private static void DrawMedium(in WidgetContext context, DateTime utcNow)
    {
        var bounds = context.Bounds;
        var scale = context.Scale;
        var radius = MathF.Min(bounds.Height * 0.24f, bounds.Width * 0.11f);
        var centerY = bounds.Min.Y + bounds.Height * 0.42f;
        var columnMaxWidth = bounds.Width * 0.30f - 6f * scale;
        DrawColumn(context, new Vector2(bounds.Min.X + bounds.Width * 0.20f, centerY), radius, columnMaxWidth,
            Loc.T(L.Timers.DailyReset), GameSchedule.NextDailyReset(utcNow), utcNow, TimeSpan.FromDays(1),
            context.Theme.Accent);
        DrawColumn(context, new Vector2(bounds.Min.X + bounds.Width * 0.50f, centerY), radius, columnMaxWidth,
            Loc.T(L.Timers.WeeklyReset), GameSchedule.NextWeeklyReset(utcNow), utcNow, TimeSpan.FromDays(7),
            WeeklyColor);
        DrawColumn(context, new Vector2(bounds.Min.X + bounds.Width * 0.80f, centerY), radius, columnMaxWidth,
            Loc.T(L.Timers.GrandCompanyReset), GameSchedule.NextGrandCompanyReset(utcNow), utcNow,
            TimeSpan.FromDays(1), GrandCompanyColor);
    }

    private static void DrawColumn(in WidgetContext context, Vector2 center, float radius, float maxLabelWidth,
        string label, DateTime next, DateTime utcNow, TimeSpan period, Vector4 color)
    {
        DrawRing(context, center, radius, next, utcNow, period, color);
        var upperLabel = Loc.Culture.TextInfo.ToUpper(label);
        var trackingBudget = MathF.Max(1f, maxLabelWidth - 1.6f * context.Scale * MathF.Max(0, upperLabel.Length - 1));
        var clippedLabel = Typography.FitText(upperLabel, trackingBudget, 0.66f, FontWeight.SemiBold);
        var labelWidth = WidgetChrome.EyebrowWidth(clippedLabel, context.Scale);
        WidgetChrome.Eyebrow(context.DrawList, new Vector2(center.X - labelWidth * 0.5f,
            center.Y + radius + 11f * context.Scale), clippedLabel, context.Theme.TextMuted, context.Scale,
            context.Opacity);
    }

    private static void DrawRing(in WidgetContext context, Vector2 center, float radius, DateTime next,
        DateTime utcNow, TimeSpan period, Vector4 color)
    {
        var scale = context.Scale;
        var opacity = context.Opacity;
        var remaining = next - utcNow;
        if (remaining < TimeSpan.Zero)
        {
            remaining = TimeSpan.Zero;
        }

        var fraction = 1f - (float)(remaining.TotalSeconds / period.TotalSeconds);
        var thickness = MathF.Max(3f * scale, radius * 0.14f);
        ProgressRing.Track(center, radius, thickness, Palette.WithAlpha(color, 0.18f * opacity));
        ProgressRing.Fill(center, radius, thickness, Math.Clamp(fraction, 0f, 1f),
            Palette.WithAlpha(color, opacity));
        ProgressRing.CenterValue(center, Countdown(remaining), null,
            Palette.WithAlpha(context.Theme.TextStrong, opacity), Palette.WithAlpha(context.Theme.TextMuted, opacity),
            new TextStyle(MathF.Max(0.62f, radius / (34f * scale)), FontWeight.SemiBold));
    }

    private static string Countdown(TimeSpan remaining)
    {
        if (remaining.TotalDays >= 1)
        {
            return string.Concat(((int)remaining.TotalDays).ToString(), "d ", remaining.Hours.ToString(), "h");
        }

        return string.Concat(((int)remaining.TotalHours).ToString(), ":", remaining.Minutes.ToString("D2"));
    }

    public void Dispose()
    {
    }
}
