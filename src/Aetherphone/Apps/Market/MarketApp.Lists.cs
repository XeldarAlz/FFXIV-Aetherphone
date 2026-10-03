using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Market;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Market;

internal sealed partial class MarketApp
{
    private const float ToggleGap = 10f;
    private const float RemoveRadius = 15f;
    private const float RemoveGlyphRadius = 9f;
    private const int MaxSaleRows = 64;

    private static readonly Dictionary<(int Quantity, string Detail, string World), string> SubCache = new();

    private readonly List<MarketAlert> pageAlerts = new();
    private readonly List<string> alertIds = new();
    private readonly CachedText[] saleAgo = new CachedText[MaxSaleRows];

    private float DrawListingsPreview(ImDrawListPtr drawList, Vector2 origin, float width, MarketView view,
        MarketSnapshot snapshot, bool hq, MarketScope scope, float scale)
    {
        var count = CountListings(snapshot.Listings, hq);
        var headerTop = origin.Y + MarketArt.SectionGap * scale;
        var cursorY = headerTop + DrawSectionTitle(drawList, new Vector2(origin.X, headerTop), width,
            Loc.T(L.Market.Listings), count > PreviewRows ? Loc.T(L.Market.SeeAll) : string.Empty, out var seeAll,
            scale);
        cursorY += MarketArt.HeaderGap * scale;
        if (seeAll)
        {
            Push(view.With(MarketViewKind.Listings));
        }

        if (count == 0)
        {
            var title = hq ? Loc.T(L.Market.NoHqListings) : Loc.T(L.Market.NoListings);
            var body = MarketText.Format(L.Market.NoListingsBody, scope.ApiName);
            var height = MarketArt.PanelHeight(title, body, width, scale);
            MarketArt.Panel(drawList, ui, new Vector2(origin.X, cursorY), width, height, FontAwesomeIcon.ShoppingBag, title,
                body, scale);
            return cursorY + height;
        }

        return DrawListingRows(drawList, new Vector2(origin.X, cursorY), width, snapshot, hq,
            Math.Min(count, PreviewRows), scale);
    }

    private float DrawSalesPreview(ImDrawListPtr drawList, Vector2 origin, float width, MarketView view,
        MarketSnapshot snapshot, bool hq, float scale)
    {
        var count = CountSales(snapshot.Sales, hq);
        var headerTop = origin.Y + MarketArt.SectionGap * scale;
        var cursorY = headerTop + DrawSectionTitle(drawList, new Vector2(origin.X, headerTop), width,
            Loc.T(L.Market.RecentSales), count > PreviewRows ? Loc.T(L.Market.SeeAll) : string.Empty,
            out var seeAll, scale);
        cursorY += MarketArt.HeaderGap * scale;
        if (seeAll)
        {
            Push(view.With(MarketViewKind.Sales));
        }

        if (count == 0)
        {
            var title = hq ? Loc.T(L.Market.NoHqSales) : Loc.T(L.Market.NoRecentSales);
            var body = Loc.T(L.Market.NoSalesBody);
            var height = MarketArt.PanelHeight(title, body, width, scale);
            MarketArt.Panel(drawList, ui, new Vector2(origin.X, cursorY), width, height, FontAwesomeIcon.Receipt,
                title, body, scale);
            return cursorY + height;
        }

        return DrawSaleRows(drawList, new Vector2(origin.X, cursorY), width, snapshot, hq,
            Math.Min(count, PreviewRows), scale);
    }

    private void DrawListingsPage(Rect area, MarketView view)
    {
        DrawRowsPage(area, view, Loc.T(L.Market.Listings), "market.listings.nav", true);
    }

    private void DrawSalesPage(Rect area, MarketView view)
    {
        DrawRowsPage(area, view, Loc.T(L.Market.RecentSales), "market.sales.nav", false);
    }

    private void DrawRowsPage(Rect area, MarketView view, string title, string id, bool listings)
    {
        var context = new PhoneContext(area, theme, navigation);
        var navBar = AppHeader.BeginLargeTitle(context);
        var scope = Scope;
        var snapshot = scope.IsValid ? market.RequestItem(view.ItemId, scope, false).Snapshot : null;
        using (AppSurface.Begin(navBar.Body))
        {
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var cursorY = origin.Y;
            if (snapshot is not null)
            {
                var hq = ResolveQuality(snapshot);
                cursorY = listings
                    ? DrawListingRows(drawList, origin, width, snapshot, hq, CountListings(snapshot.Listings, hq),
                        scale)
                    : DrawSaleRows(drawList, origin, width, snapshot, hq,
                        Math.Min(MaxSaleRows, CountSales(snapshot.Sales, hq)), scale);
            }

            ReserveTo(origin, width, cursorY + BottomPad * scale);
        }

        AppHeader.EndLargeTitle(in navBar, context, id, title, NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty, view.Name, back);
    }

    private float DrawListingRows(ImDrawListPtr drawList, Vector2 origin, float width, MarketSnapshot snapshot,
        bool hq, int count, float scale)
    {
        if (count <= 0)
        {
            return origin.Y;
        }

        var rowHeight = MarketArt.CompactRowHeight * scale;
        var max = new Vector2(origin.X + width, origin.Y + count * rowHeight);
        if (!ImGui.IsRectVisible(origin, max))
        {
            return max.Y;
        }

        MarketArt.Card(drawList, ui, origin, max, scale);
        var pad = Metrics.Space.Lg * scale;
        var listings = snapshot.Listings;
        var drawn = 0;
        for (var listingIndex = 0; listingIndex < listings.Length && drawn < count; listingIndex++)
        {
            var listing = listings[listingIndex];
            if (listing.Hq != hq)
            {
                continue;
            }

            var top = origin.Y + drawn * rowHeight;
            if (drawn > 0)
            {
                MarketArt.Hairline(drawList, ui, origin.X + pad, max.X - pad, top);
            }

            drawn++;
            var row = new Rect(new Vector2(origin.X + pad, top), new Vector2(max.X - pad, top + rowHeight));
            if (!ImGui.IsRectVisible(row.Min, row.Max))
            {
                continue;
            }

            var total = MarketText.Price(listing.Total);
            var totalWidth = MarketArt.Price(drawList, row.Max.X,
                row.Center.Y - Typography.LineHeight(TextStyles.Body) * 0.5f, total, ui.MutedInk, TextStyles.Body);
            DrawTradeLabels(drawList, row, totalWidth, listing.PricePerUnit, listing.Hq,
                Sub(listing.Quantity, listing.Retainer, snapshot.MultiWorld ? listing.World : string.Empty), scale);
        }

        return max.Y;
    }

    private float DrawSaleRows(ImDrawListPtr drawList, Vector2 origin, float width, MarketSnapshot snapshot, bool hq,
        int count, float scale)
    {
        if (count <= 0)
        {
            return origin.Y;
        }

        var rowHeight = MarketArt.CompactRowHeight * scale;
        var max = new Vector2(origin.X + width, origin.Y + count * rowHeight);
        if (!ImGui.IsRectVisible(origin, max))
        {
            return max.Y;
        }

        MarketArt.Card(drawList, ui, origin, max, scale);
        var pad = Metrics.Space.Lg * scale;
        var sales = snapshot.Sales;
        var drawn = 0;
        for (var saleIndex = 0; saleIndex < sales.Length && drawn < count; saleIndex++)
        {
            var sale = sales[saleIndex];
            if (sale.Hq != hq)
            {
                continue;
            }

            var top = origin.Y + drawn * rowHeight;
            if (drawn > 0)
            {
                MarketArt.Hairline(drawList, ui, origin.X + pad, max.X - pad, top);
            }

            var slot = drawn;
            drawn++;
            var row = new Rect(new Vector2(origin.X + pad, top), new Vector2(max.X - pad, top + rowHeight));
            if (!ImGui.IsRectVisible(row.Min, row.Max))
            {
                continue;
            }

            var ago = SaleAgo(slot, sale.Time);
            var agoWidth = Typography.Measure(ago, TextStyles.Footnote).X;
            Typography.Draw(drawList,
                new Vector2(row.Max.X - agoWidth, row.Center.Y - Typography.LineHeight(TextStyles.Footnote) * 0.5f),
                ago, ui.MutedInk, TextStyles.Footnote);
            DrawTradeLabels(drawList, row, agoWidth, sale.PricePerUnit, sale.Hq,
                Sub(sale.Quantity, sale.Buyer, snapshot.MultiWorld ? sale.World : string.Empty), scale);
        }

        return max.Y;
    }

    private void DrawTradeLabels(ImDrawListPtr drawList, Rect row, float trailingWidth, long price, bool hq,
        string subtitle, float scale)
    {
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var subtitleHeight = Typography.LineHeight(TextStyles.Footnote);
        var top = row.Center.Y - (titleHeight + MarketArt.LineGap * scale + subtitleHeight) * 0.5f;
        var priceText = MarketText.Price(price);
        var priceWidth = WidgetText.Tabular(drawList, new Vector2(row.Min.X, top), priceText, ui.TitleInk,
            TextStyles.Headline);
        if (hq)
        {
            MarketArt.HqBadge(drawList, new Vector2(row.Min.X + priceWidth + Metrics.Space.Xs * scale,
                top + titleHeight * 0.5f), scale);
        }

        var subtitleWidth = MathF.Max(1f, row.Width - trailingWidth - MarketArt.ValueGap * scale);
        Typography.Draw(drawList, new Vector2(row.Min.X, top + titleHeight + MarketArt.LineGap * scale),
            Typography.FitText(subtitle, subtitleWidth, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
    }

    private string SaleAgo(int slot, DateTime time)
    {
        var minutes = time == default ? -1L : (long)(DateTime.UtcNow - time).TotalMinutes;
        var key = (minutes << 24) ^ time.Ticks;
        ref var cache = ref saleAgo[slot];
        if (cache.IsCurrent(key))
        {
            return cache.Value;
        }

        return cache.Store(key, TimeText.Ago(time));
    }

    private static string Sub(int quantity, string detail, string world)
    {
        var key = (quantity, detail, world);
        if (SubCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        if (SubCache.Count > 512)
        {
            SubCache.Clear();
        }

        var result = MarketText.Format(L.Market.Quantity, quantity);
        if (detail.Length > 0)
        {
            result = string.Concat(result, " · ", detail);
        }

        if (world.Length > 0)
        {
            result = string.Concat(result, " · ", world);
        }

        SubCache[key] = result;
        return result;
    }

    private void DrawAlertsPage(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        var navBar = AppHeader.BeginLargeTitle(context);
        alerts.CopyInto(pageAlerts);
        if (pageAlerts.Count == 0)
        {
            MarketArt.StateScreen(ImGui.GetWindowDrawList(), ui, navBar.Body, FontAwesomeIcon.Bell,
                Loc.T(L.Market.AlertsEmptyTitle), Loc.T(L.Market.AlertsEmptyBody), UiScale.Current);
        }
        else
        {
            using (AppSurface.Begin(navBar.Body))
            {
                var scale = UiScale.Current;
                var drawList = ImGui.GetWindowDrawList();
                var origin = ImGui.GetCursorScreenPos();
                var width = ScrollLayout.StableContentWidth();
                var cursorY = DrawAlertRows(drawList, origin, width, pageAlerts, true, scale);
                cursorY += FootnoteGap * scale;
                cursorY += Typography.DrawWrappedLeft(new Vector2(origin.X, cursorY), Loc.T(L.Market.AlertsNote),
                    ui.MutedInk, TextStyles.Footnote, width);
                ReserveTo(origin, width, cursorY + BottomPad * scale);
            }
        }

        AppHeader.EndLargeTitle(in navBar, context, "market.alerts.nav", Loc.T(L.Market.AlertsTitle),
            NavBarStyle.From(ui), ReadOnlySpan<NavBarButton>.Empty, DisplayName, back);
    }

    private float DrawAlertRows(ImDrawListPtr drawList, Vector2 origin, float width, List<MarketAlert> list,
        bool manage, float scale)
    {
        var rowHeight = MarketArt.RowHeight * scale;
        var max = new Vector2(origin.X + width, origin.Y + list.Count * rowHeight);
        if (!ImGui.IsRectVisible(origin, max))
        {
            return max.Y;
        }

        MarketArt.Card(drawList, ui, origin, max, scale);
        var pad = Metrics.Space.Lg * scale;
        var iconSize = MarketArt.IconSize * scale;
        MarketAlert? removed = null;
        for (var alertIndex = 0; alertIndex < list.Count; alertIndex++)
        {
            var alert = list[alertIndex];
            var top = origin.Y + alertIndex * rowHeight;
            var row = new Rect(new Vector2(origin.X, top), new Vector2(max.X, top + rowHeight));
            if (alertIndex > 0)
            {
                MarketArt.Hairline(drawList, ui, origin.X + pad + iconSize + MarketArt.TextGap * scale, max.X - pad,
                    top);
            }

            if (!ImGui.IsRectVisible(row.Min, row.Max))
            {
                continue;
            }

            var right = row.Max.X - pad;
            var overControl = false;
            if (manage)
            {
                var removeCenter = new Vector2(right - RemoveRadius * scale, row.Center.Y);
                var removeRadius = RemoveRadius * scale;
                overControl |= UiInteract.Hover(removeCenter - new Vector2(removeRadius, removeRadius),
                    removeCenter + new Vector2(removeRadius, removeRadius));
                if (HoverButton.Circle(drawList, AlertId(alertIndex), removeCenter, RemoveGlyphRadius * scale,
                        FontAwesomeIcon.Trash, Palette.WithAlpha(ui.TitleInk, 0f), ui.MutedInk,
                        ImGui.GetIO().DeltaTime, 1f, true, Loc.T(L.Market.RemoveAlert), HoverLabelSide.Above))
                {
                    removed = alert;
                }

                right = removeCenter.X - removeRadius - ToggleGap * scale;
                var toggleSize = new Vector2(Metrics.Size.ToggleWidth, Metrics.Size.ToggleHeight) * scale;
                var toggle = new Rect(new Vector2(right - toggleSize.X, row.Center.Y - toggleSize.Y * 0.5f),
                    new Vector2(right, row.Center.Y + toggleSize.Y * 0.5f));
                overControl |= UiInteract.Hover(toggle.Min, toggle.Max);
                var enabled = Toggle.Draw(AlertId(alertIndex), toggle, alert.Enabled, theme);
                if (enabled != alert.Enabled)
                {
                    alerts.SetEnabled(alert, enabled);
                }

                right = toggle.Min.X - MarketArt.ValueGap * scale;
            }
            else
            {
                var price = MarketText.Price(alert.LastSeenPrice);
                var priceWidth = MarketArt.Price(drawList, right,
                    row.Center.Y - Typography.LineHeight(TextStyles.Headline) * 0.5f, price,
                    ui.Accent, TextStyles.Headline);
                right -= priceWidth + MarketArt.ValueGap * scale;
            }

            var hovered = !overControl && MarketArt.RowInteraction(drawList, ui, row, scale);
            MarketArt.ItemIcon(drawList, textures, ui, alert.IconId,
                new Vector2(row.Min.X + pad, row.Center.Y - iconSize * 0.5f), iconSize, scale);
            var subtitle = alert.Triggered && alert.Enabled && alert.LastSeenPrice > 0
                ? MarketText.Format(L.Market.AlertHitNow, MarketText.Rule(alert), alert.LastSeenPrice)
                : MarketText.Rule(alert);
            var textLeft = row.Min.X + pad + iconSize + MarketArt.TextGap * scale;
            MarketArt.Labels(drawList, textLeft, right, row.Center.Y, alert.ItemName, subtitle,
                alert.Enabled ? ui.TitleInk : ui.MutedInk, alert.Triggered && alert.Enabled ? ui.Accent : ui.MutedInk,
                scale);
            if (UiInteract.Click(row.Min, row.Max, hovered))
            {
                OpenItem(alert.ItemId);
            }
        }

        if (removed is not null)
        {
            UiFeedback.Play(UiSound.ToggleOff);
            alerts.Remove(removed);
        }

        return max.Y;
    }

    private string AlertId(int alertIndex)
    {
        while (alertIds.Count <= alertIndex)
        {
            alertIds.Add("market.alert." + alertIds.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        return alertIds[alertIndex];
    }

    private static int CountListings(MarketListing[] listings, bool hq)
    {
        var count = 0;
        for (var listingIndex = 0; listingIndex < listings.Length; listingIndex++)
        {
            if (listings[listingIndex].Hq == hq)
            {
                count++;
            }
        }

        return count;
    }

    private static int CountSales(MarketSale[] sales, bool hq)
    {
        var count = 0;
        for (var saleIndex = 0; saleIndex < sales.Length; saleIndex++)
        {
            if (sales[saleIndex].Hq == hq)
            {
                count++;
            }
        }

        return count;
    }
}
