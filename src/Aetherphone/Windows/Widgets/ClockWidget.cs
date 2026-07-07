using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Game;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;

namespace Aetherphone.Windows.Widgets;

internal sealed class ClockWidget : IHomeWidget
{
    public string Id => "clock.faces";
    public string DisplayName => Loc.T(L.Apps.Clock);
    public string AppId => "clock";
    public WidgetSizeSet Sizes => WidgetSizeSet.Small | WidgetSizeSet.Medium;

    public void Draw(in WidgetContext context)
    {
        WidgetChrome.Card(context.DrawList, context.Bounds, context.Scale, context.Opacity);
        if (context.Size == WidgetSize.Small)
        {
            DrawSmall(context);
            return;
        }

        DrawMedium(context);
    }

    private static void DrawSmall(in WidgetContext context)
    {
        var bounds = context.Bounds;
        var scale = context.Scale;
        var radius = MathF.Min(bounds.Width, bounds.Height) * 0.5f - 16f * scale;
        var now = DateTime.Now;
        AnalogClock.Draw(bounds.Center, radius, now.Hour, now.Minute, now.Second + now.Millisecond / 1000f,
            context.Theme);
    }

    private static void DrawMedium(in WidgetContext context)
    {
        var bounds = context.Bounds;
        var scale = context.Scale;
        var radius = MathF.Min(bounds.Height * 0.30f, bounds.Width * 0.17f);
        var faceCenterY = bounds.Min.Y + bounds.Height * 0.42f;
        var now = DateTime.Now;
        var bell = EorzeaTime.Now();
        var bellSeconds = EorzeaTime.CurrentSeconds() % 60;
        DrawFace(context, new Vector2(bounds.Min.X + bounds.Width * 0.27f, faceCenterY), radius, now.Hour, now.Minute,
            now.Second + now.Millisecond / 1000f, Loc.T(L.Home.Local), now.ToString("HH:mm"), context.Theme, scale);
        DrawFace(context, new Vector2(bounds.Min.X + bounds.Width * 0.73f, faceCenterY), radius, bell.Hour,
            bell.Minute, bellSeconds, Loc.T(L.Home.Eorzea), bell.Formatted, context.Theme, scale);
    }

    private static void DrawFace(in WidgetContext context, Vector2 center, float radius, float hours, float minutes,
        float seconds, string label, string digital, PhoneTheme theme, float scale)
    {
        AnalogClock.Draw(center, radius, hours, minutes, seconds, theme);
        var labelWidth = WidgetChrome.EyebrowWidth(label, scale);
        var labelTop = center.Y + radius + 9f * scale;
        WidgetChrome.Eyebrow(context.DrawList, new Vector2(center.X - labelWidth * 0.5f, labelTop), label,
            theme.TextMuted, scale, context.Opacity);
        Typography.DrawCentered(context.DrawList, new Vector2(center.X, labelTop + 19f * scale), digital,
            Palette.WithAlpha(theme.TextStrong, context.Opacity), TextStyles.SubheadlineEmphasized);
    }

    public void Dispose()
    {
    }
}
