using Aetherphone.Core;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;

namespace Aetherphone.Apps.Calendar.Widgets;

internal sealed class HalloweenWidget : IHomeWidget
{
    private const int HalloweenMonth = 10;
    private const int HalloweenDay = 31;
    private const float PumpkinUnits = 22f;
    private const float GlowReach = 2.4f;
    private const float GlowAlpha = 0.28f;
    private const int GlowCells = 8;

    private CachedText eyebrow;
    private CachedText number;
    private CachedText caption;
    private CachedText treats;
    private LanguageInfo? language;

    public string Id => "calendar.halloween";
    public string DisplayName => Loc.T(L.Seasonal.CountdownName);
    public string Description => Loc.T(L.Seasonal.CountdownDescription);
    public string AppId => "calendar";
    public WidgetSizeSet Sizes => WidgetSizeSet.Small;

    public void Draw(in WidgetContext context)
    {
        if (context.Opacity <= 0f)
        {
            return;
        }

        SyncLanguage();
        WidgetChrome.Container(context);
        var ink = WidgetInk.From(context);
        var content = WidgetMetrics.Content(context);
        var scale = context.Scale;
        var accent = ink.Accent(Spooks.Pumpkin);
        var pumpkinSize = PumpkinUnits * scale;
        var pumpkinCenter = new Vector2(content.Max.X - pumpkinSize * 0.5f, content.Min.Y + pumpkinSize * 0.5f);
        if (SeasonalTheme.Halloween)
        {
            NightScene.Glow(context.DrawList, pumpkinCenter, pumpkinSize * GlowReach,
                accent with { W = accent.W * GlowAlpha }, GlowCells);
        }

        PhoneIcon.Draw(context.DrawList, pumpkinCenter, PhoneIcons.Pumpkin, accent, pumpkinSize);
        WidgetText.EyebrowFit(context.DrawList, content.Min, Eyebrow(), content.Width - pumpkinSize, accent, scale);

        var days = DaysUntilHalloween(DateTime.Today);
        var top = content.Min.Y + pumpkinSize + WidgetMetrics.Gutter * 0.5f * scale;
        var headline = days == 0 ? Loc.T(L.Seasonal.Tonight) : Number(days);
        var style = days == 0 ? WidgetType.DisplayCompact : WidgetType.Display;
        top += WidgetText.Draw(context.DrawList, new Vector2(content.Min.X, top), headline, ink.Primary, style,
            content.Width);
        WidgetText.Draw(context.DrawList, new Vector2(content.Min.X, top), Caption(days), ink.Secondary,
            WidgetType.Caption, content.Width);
        if (!SeasonalTheme.Halloween)
        {
            return;
        }

        var treatsTop = content.Max.Y - WidgetText.LineHeight(WidgetType.Caption);
        WidgetText.Draw(context.DrawList, new Vector2(content.Min.X, treatsTop), TreatsLine(Treats.Found), accent,
            WidgetType.Caption, content.Width);
    }

    public static int DaysUntilHalloween(DateTime today)
    {
        var target = new DateTime(today.Year, HalloweenMonth, HalloweenDay);
        if (today > target)
        {
            target = target.AddYears(1);
        }

        return (target - today).Days;
    }

    private void SyncLanguage()
    {
        if (ReferenceEquals(language, Loc.Current))
        {
            return;
        }

        language = Loc.Current;
        eyebrow = default;
        caption = default;
        treats = default;
    }

    private string Eyebrow() => eyebrow.IsCurrent(0)
        ? eyebrow.Value
        : eyebrow.Store(0, WidgetText.Upper(L.Seasonal.CountdownName));

    private string Number(int days) => WidgetText.Integer(ref number, days);

    private string Caption(int days) => caption.IsCurrent(days)
        ? caption.Value
        : caption.Store(days, days == 0 ? Loc.T(L.Seasonal.HalloweenNight) : Loc.Plural(L.Seasonal.DaysToGo, days));

    private string TreatsLine(int found) => treats.IsCurrent(found)
        ? treats.Value
        : treats.Store(found, Loc.T(L.Seasonal.TreatsProgress, found, Treats.Total));

    public void Dispose()
    {
    }
}
