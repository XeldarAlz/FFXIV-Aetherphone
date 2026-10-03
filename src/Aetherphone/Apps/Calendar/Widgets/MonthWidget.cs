using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Widgets;

namespace Aetherphone.Apps.Calendar.Widgets;

internal sealed class MonthWidget : IHomeWidget
{
    private CachedText monthName;

    public string Id => "calendar.month";
    public string DisplayName => Loc.T(L.Calendar.Title);
    public string Description => Loc.T(L.WidgetsTime.MonthDescription);
    public string AppId => "calendar";
    public WidgetSizeSet Sizes => WidgetSizeSet.Small;

    public void Draw(in WidgetContext context)
    {
        if (context.Opacity <= 0f)
        {
            return;
        }

        var accent = AppAccents.For(AppId);
        WidgetChrome.Container(context);
        var ink = WidgetInk.From(context);
        var content = WidgetMetrics.Content(context);
        var scale = context.Scale;
        var today = DateTime.Today;
        WidgetText.EyebrowFit(context.DrawList, content.Min, MonthName(today), content.Width, ink.Accent(accent),
            scale);
        var gridTop = content.Min.Y + WidgetText.EyebrowHeight() + WidgetMetrics.Gutter * 0.75f * scale;
        MonthGrid.Draw(context.DrawList, ink, new Rect(new Vector2(content.Min.X, gridTop), content.Max), today,
            accent, 0u, null, false, scale);
    }

    private string MonthName(DateTime today)
    {
        var key = today.Ticks;
        return monthName.IsCurrent(key) ? monthName.Value : monthName.Store(key, today.ToString("MMMM", Loc.Culture));
    }

    public void Dispose()
    {
    }
}
