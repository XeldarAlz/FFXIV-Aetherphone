using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Market;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Market.Widgets;

internal sealed class MarketWatchWidget : IHomeWidget
{
    private const string AppKey = "market";
    private const int RefreshMilliseconds = 2000;
    private const int MaxRows = 7;
    private const float IconUnits = 28f;
    private const float RowUnits = 40f;
    private const float DotUnits = 3f;
    private const float SparkUnits = 40f;
    private const float SparkHeightUnits = 18f;
    private const float SparkMinimumRowUnits = 300f;

    private static readonly float[][] SamplePoints =
    {
        new[] { 1900f, 1880f, 1920f, 1870f, 1860f, 1840f, 1850f },
        new[] { 39000f, 40500f, 40100f, 41200f, 41800f, 41500f, 42000f },
        new[] { 4200f, 4100f, 4150f, 4000f, 3950f, 3980f, 3900f },
        new[] { 740f, 760f, 750f, 770f, 755f, 765f, 760f },
    };

    private static readonly double[] SampleChanges = { -0.026, 0.031, -0.048, 0.007 };

    private readonly MarketAlertService alerts;
    private readonly MarketWatchlist watchlist;
    private readonly MarketItemIndex index;
    private readonly ITextureProvider textures;
    private readonly List<MarketAlert> triggered = new();
    private readonly List<MarketItemRef> watched = new();
    private readonly CachedText[] samplePrices = new CachedText[4];
    private WidgetRefresh refresh;
    private CachedText trailingText;

    public MarketWatchWidget(MarketAlertService alerts, MarketWatchlist watchlist, MarketItemIndex index,
        ITextureProvider textures)
    {
        this.alerts = alerts;
        this.watchlist = watchlist;
        this.index = index;
        this.textures = textures;
    }

    public string Id => "market.alerts";
    public string DisplayName => Loc.T(L.WidgetsUtility.MarketName);
    public string Description => Loc.T(L.Market.WidgetDescription);
    public string AppId => AppKey;
    public WidgetSizeSet Sizes => WidgetSizeSet.Medium | WidgetSizeSet.Large;

    public float Relevance(string config) => alerts.TriggeredCount > 0 ? 0.85f : 0f;

    public void Draw(in WidgetContext context)
    {
        Refresh();
        WidgetChrome.Container(context);
        var ink = WidgetInk.From(context);
        var scale = context.Scale;
        var content = WidgetMetrics.Content(context);
        var accentRaw = AppAccents.For(AppKey);
        var accent = ink.Accent(accentRaw);
        var sample = context.Preview && triggered.Count == 0 && watched.Count == 0;
        var shownTriggered = sample ? 1 : triggered.Count;
        var trailing = shownTriggered > 0 ? Trailing(shownTriggered) : string.Empty;
        var top = WidgetChrome.Header(context, ink, AppKey, Loc.T(L.WidgetsUtility.MarketName), accentRaw,
            trailing, accent);
        var body = new Rect(new Vector2(content.Min.X, top + WidgetMetrics.RowGap * scale), content.Max);
        var total = sample ? WidgetSamples.MarketItems.Length : triggered.Count + watched.Count;
        if (total == 0)
        {
            WidgetChrome.Message(context, ink, body, FontAwesomeIcon.ChartBar, accentRaw,
                Loc.T(L.Market.WidgetEmptyTitle), Loc.T(L.Market.WidgetEmptyHint));
            return;
        }

        var capacity = Math.Clamp((int)(body.Height / (RowUnits * scale)), 1, MaxRows);
        var rowHeight = body.Height / capacity;
        var rows = Math.Min(capacity, total);
        var showSpark = body.Width >= SparkMinimumRowUnits * scale;
        for (var rowIndex = 0; rowIndex < rows; rowIndex++)
        {
            var rowRect = new Rect(new Vector2(body.Min.X, body.Min.Y + rowIndex * rowHeight),
                new Vector2(body.Max.X, body.Min.Y + (rowIndex + 1) * rowHeight));
            if (sample)
            {
                DrawSample(context, ink, rowIndex, rowRect, accent, showSpark);
            }
            else if (rowIndex < triggered.Count)
            {
                DrawAlert(context, ink, rowIndex, rowRect, triggered[rowIndex], accent);
            }
            else
            {
                DrawWatched(context, ink, rowIndex, rowRect, watched[rowIndex - triggered.Count], showSpark);
            }

            if (rowIndex < rows - 1)
            {
                var left = rowRect.Min.X + (IconUnits + WidgetMetrics.Gutter) * scale;
                WidgetChrome.Separator(context, ink, left, rowRect.Max.X, rowRect.Max.Y);
            }
        }
    }

    private void DrawAlert(in WidgetContext context, in WidgetInk ink, int rowIndex, Rect rowRect, MarketAlert alert,
        Vector4 accent)
    {
        WidgetControls.Link(context, ink, rowIndex, rowRect, WidgetRoute.MarketItem(alert.ItemId));
        var icon = Icon(rowRect, context.Scale);
        DrawItemIcon(context, ink, icon, alert.IconId, accent);
        var price = alert.LastSeenPrice > 0 ? MarketFormat.Gil(alert.LastSeenPrice) : Loc.T(L.WidgetsUtility.Checking);
        var right = DrawPrice(context, rowRect, price, accent, alert.LastSeenPrice > 0);
        var dot = DotUnits * context.Scale;
        context.DrawList.AddCircleFilled(new Vector2(right - dot * 2.5f, rowRect.Center.Y), dot,
            ImGui.GetColorU32(accent), 16);
        DrawLabels(context, ink, rowRect, icon, right - dot * 4f, alert.ItemName, MarketText.Rule(alert),
            ink.Secondary);
    }

    private void DrawWatched(in WidgetContext context, in WidgetInk ink, int rowIndex, Rect rowRect,
        MarketItemRef item, bool showSpark)
    {
        WidgetControls.Link(context, ink, rowIndex, rowRect, WidgetRoute.MarketItem(item.Id));
        var scale = context.Scale;
        var icon = Icon(rowRect, scale);
        DrawItemIcon(context, ink, icon, item.IconId, ink.Accent(AppAccents.For(AppKey)));
        var series = watchlist.SeriesFor(item.Id);
        var price = watchlist.CurrentPrice(item.Id, series);
        var hasChange = series is { Median: > 0 } && price > 0;
        var change = hasChange ? MarketTrend.Change(price, series!.Median) : 0d;
        var trendInk = ink.Accent(change > 0d ? context.Theme.ToggleOn :
            change < 0d ? context.Theme.Danger : ink.Secondary);
        var right = DrawPrice(context, rowRect, MarketText.Price(price), ink.Primary, price > 0);
        if (showSpark && series is { HasLine: true })
        {
            var sparkWidth = SparkUnits * scale;
            var sparkHeight = SparkHeightUnits * scale;
            var sparkRight = right - WidgetMetrics.Gutter * scale;
            MarketArt.Sparkline(context.DrawList,
                new Rect(new Vector2(sparkRight - sparkWidth, rowRect.Center.Y - sparkHeight * 0.5f),
                    new Vector2(sparkRight, rowRect.Center.Y + sparkHeight * 0.5f)),
                series.Points.AsSpan(0, MarketTrend.WatchPoints), series.Median, trendInk, scale);
            right = sparkRight - sparkWidth;
        }

        var caption = hasChange ? MarketText.Change(change) : item.Category;
        DrawLabels(context, ink, rowRect, icon, right, item.Name, caption, hasChange ? trendInk : ink.Secondary);
    }

    private void DrawSample(in WidgetContext context, in WidgetInk ink, int rowIndex, Rect rowRect, Vector4 accent,
        bool showSpark)
    {
        var scale = context.Scale;
        var icon = Icon(rowRect, scale);
        DrawItemIcon(context, ink, icon, 0, accent);
        var price = WidgetText.Number(ref samplePrices[rowIndex], WidgetSamples.MarketPrices[rowIndex]);
        var right = DrawPrice(context, rowRect, price, ink.Primary, true);
        var change = SampleChanges[rowIndex];
        var trendInk = ink.Accent(change > 0d ? context.Theme.ToggleOn : context.Theme.Danger);
        if (showSpark)
        {
            var sparkWidth = SparkUnits * scale;
            var sparkHeight = SparkHeightUnits * scale;
            var sparkRight = right - WidgetMetrics.Gutter * scale;
            MarketArt.Sparkline(context.DrawList,
                new Rect(new Vector2(sparkRight - sparkWidth, rowRect.Center.Y - sparkHeight * 0.5f),
                    new Vector2(sparkRight, rowRect.Center.Y + sparkHeight * 0.5f)),
                SamplePoints[rowIndex], 0f, trendInk, scale);
            right = sparkRight - sparkWidth;
        }

        DrawLabels(context, ink, rowRect, icon, right, WidgetSamples.MarketItems[rowIndex],
            MarketText.Change(change), trendInk);
    }

    private static float DrawPrice(in WidgetContext context, Rect rowRect, string price, Vector4 color, bool tabular)
    {
        var style = tabular ? WidgetType.Headline : WidgetType.Caption;
        var width = tabular ? WidgetText.TabularWidth(price, style) : Typography.Measure(price, style).X;
        var height = Typography.Measure(price, style).Y;
        var position = new Vector2(rowRect.Max.X - width, rowRect.Center.Y - height * 0.5f);
        if (tabular)
        {
            WidgetText.Tabular(context.DrawList, position, price, color, style);
        }
        else
        {
            Typography.Draw(context.DrawList, position, price, color, style);
        }

        return position.X;
    }

    private static void DrawLabels(in WidgetContext context, in WidgetInk ink, Rect rowRect, Rect icon, float right,
        string name, string caption, Vector4 captionInk)
    {
        var scale = context.Scale;
        var left = icon.Max.X + WidgetMetrics.Gutter * scale;
        var width = right - WidgetMetrics.Gutter * scale - left;
        var titleHeight = WidgetText.SpacedLineHeight(WidgetType.Headline);
        var captionHeight = WidgetText.SpacedLineHeight(WidgetType.Caption);
        var top = rowRect.Center.Y - (titleHeight + captionHeight) * 0.5f;
        WidgetText.Draw(context.DrawList, new Vector2(left, top), name, ink.Primary, WidgetType.Headline, width);
        WidgetText.Draw(context.DrawList, new Vector2(left, top + titleHeight), caption, captionInk,
            WidgetType.Caption, width);
    }

    private void DrawItemIcon(in WidgetContext context, in WidgetInk ink, Rect icon, uint iconId, Vector4 accent)
    {
        var drawList = context.DrawList;
        if (iconId != 0)
        {
            var texture = textures.GetFromGameIcon(new GameIconLookup(iconId)).GetWrapOrEmpty();
            Squircle.FillImage(drawList, icon.Min, icon.Max, icon.Width * Metrics.Radius.TileFactor, texture.Handle,
                ImGui.GetColorU32(ink.ImageTint));
            return;
        }

        Squircle.Fill(drawList, icon.Min, icon.Max, icon.Width * Metrics.Radius.TileFactor,
            ImGui.GetColorU32(ink.Fill));
        ProgressRing.CenterIcon(drawList, icon.Center, FontAwesomeIcon.Coins, accent, icon.Height * 0.5f);
    }

    private static Rect Icon(Rect rowRect, float scale)
    {
        var side = MathF.Min(IconUnits * scale, rowRect.Height - 4f * scale);
        var min = new Vector2(rowRect.Min.X, rowRect.Center.Y - side * 0.5f);
        return new Rect(min, min + new Vector2(side, side));
    }

    private void Refresh()
    {
        if (!refresh.Due(RefreshMilliseconds))
        {
            return;
        }

        alerts.CopyInto(triggered);
        for (var alertIndex = triggered.Count - 1; alertIndex >= 0; alertIndex--)
        {
            if (!triggered[alertIndex].Triggered || !triggered[alertIndex].Enabled)
            {
                triggered.RemoveAt(alertIndex);
            }
        }

        watched.Clear();
        if (!index.Ready)
        {
            index.EnsureBuilt();
            return;
        }

        watchlist.RefreshScopes();
        watchlist.Sync();
        var items = watchlist.Items;
        for (var itemIndex = 0; itemIndex < items.Count && watched.Count < MaxRows; itemIndex++)
        {
            if (index.TryGet(items[itemIndex], out var item))
            {
                watched.Add(item);
            }
        }
    }

    private string Trailing(int count) =>
        trailingText.IsCurrent(count)
            ? trailingText.Value
            : trailingText.Store(count, Loc.T(L.WidgetsUtility.Triggered, count));

    public void Dispose()
    {
    }
}
