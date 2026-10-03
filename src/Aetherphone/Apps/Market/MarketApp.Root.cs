using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Market;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Market;

internal sealed partial class MarketApp
{
    private const float ScopeStripHeight = 32f;
    private const float SearchGap = 14f;
    private const float StripGap = 6f;
    private const int SearchMaxLength = 60;
    private const int MaxResults = 50;
    private const int SkeletonRows = 6;
    private const float QuickLookHeight = 82f;
    private const float QuickLookIcon = 46f;
    private const float QuickLookGlowCoverage = 0.7f;
    private const float QuickLookGlowStrength = 0.14f;
    private const float RecentTileWidth = 100f;
    private const float RecentTileHeight = 118f;
    private const float RecentTileGap = 10f;
    private const float RecentIconSize = 46f;
    private const float RecentPad = 12f;
    private const float FootnoteGap = 18f;
    private const float ValueColumnGap = 4f;

    private readonly List<MarketItemRef> results = new();
    private readonly List<MarketItemRef> sectionBuffer = new();
    private readonly List<uint> prefetchBuffer = new();
    private readonly List<MarketAlert> alertBuffer = new();
    private readonly NavBarButton[] rootButtons = new NavBarButton[1];
    private readonly PanRail recentRail = new();
    private string search = string.Empty;
    private string lastSearch = " ";
    private bool lastIndexReady;
    private MarketItemRef lastHovered;
    private bool hasHovered;
    private bool showDelta;

    private void DrawRoot(Rect area)
    {
        UpdateHovered();
        var context = new PhoneContext(area, theme, navigation);
        var navBar = AppHeader.BeginLargeTitle(context, false);
        using (AppSurface.Begin(navBar.Body))
        {
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var cursorY = DrawSearchField(drawList, origin, width, scale);
            var query = search.Trim();
            if (!string.Equals(query, lastSearch, StringComparison.Ordinal) || (index.Ready && !lastIndexReady))
            {
                index.Search(query, results, MaxResults);
                lastSearch = query;
            }

            lastIndexReady = index.Ready;
            cursorY += SearchGap * scale;
            cursorY = query.Length > 0
                ? DrawResults(drawList, new Vector2(origin.X, cursorY), width, scale)
                : DrawHome(drawList, new Vector2(origin.X, cursorY), width, scale);
            ReserveTo(origin, width, cursorY + BottomPad * scale);
        }

        rootButtons[0] = new NavBarButton(IconGlyph.Of(FontAwesomeIcon.Bell), Loc.T(L.Market.AlertsTitle));
        UiAnchors.Report("market.alerts", AppHeader.LargeTitleButtonRect(in navBar, 0, rootButtons.Length));
        var pressed = AppHeader.EndLargeTitle(in navBar, context, "market.nav", DisplayName, NavBarStyle.From(ui),
            rootButtons);
        if (pressed == 0)
        {
            Push(MarketView.Alerts());
        }
    }

    private float DrawSearchField(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var field = new Rect(origin, new Vector2(origin.X + width, origin.Y + GlassField.HeightUnits * scale));
        UiAnchors.Report("market.search", field);
        Material.ThemedGlass(drawList, field.Min, field.Max, GlassField.Radius(field), scale, theme);
        GlassField.Search(drawList, field, "##marketSearch", Loc.T(L.Market.SearchItems), ref search, theme, scale,
            SearchMaxLength, false);
        return field.Max.Y;
    }

    private float DrawHome(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        if (!index.Ready)
        {
            return DrawSkeleton(drawList, origin, width, scale);
        }

        var cursorY = origin.Y;
        var scope = Scope;
        if (!scope.IsValid)
        {
            var title = Loc.T(L.Market.SignedOutTitle);
            var body = Loc.T(L.Market.SignedOutBody);
            var height = MarketArt.PanelHeight(title, body, width, scale);
            MarketArt.Panel(drawList, ui, new Vector2(origin.X, cursorY), width, height, FontAwesomeIcon.UserLock,
                title, body, scale);
            cursorY += height;
        }
        else
        {
            cursorY = DrawScopeStrip(new Vector2(origin.X, cursorY), width, null, scale) + StripGap * scale;
        }

        PrefetchHome(scope);
        watchlist.Sync();
        if (hasHovered)
        {
            cursorY = DrawQuickLook(drawList, new Vector2(origin.X, cursorY + MarketArt.SectionGap * scale * 0.5f),
                width, scope, scale);
        }

        cursorY = DrawTriggered(drawList, new Vector2(origin.X, cursorY), width, scale);
        cursorY = DrawWatchlist(drawList, new Vector2(origin.X, cursorY), width, scale);
        cursorY = DrawRecents(drawList, new Vector2(origin.X, cursorY), width, scope, scale);
        cursorY += FootnoteGap * scale;
        cursorY += Typography.DrawWrappedLeft(new Vector2(origin.X, cursorY), Loc.T(L.Market.SourceNote),
            ui.MutedInk, TextStyles.Footnote, width);
        return cursorY;
    }

    private void PrefetchHome(MarketScope scope)
    {
        prefetchBuffer.Clear();
        if (hasHovered)
        {
            prefetchBuffer.Add(lastHovered.Id);
        }

        var recents = configuration.MarketRecents;
        for (var recentIndex = 0; recentIndex < recents.Count; recentIndex++)
        {
            prefetchBuffer.Add(recents[recentIndex]);
        }

        market.PrefetchAggregated(prefetchBuffer, scope);
    }

    private float DrawSkeleton(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var rowHeight = MarketArt.RowHeight * scale;
        var max = new Vector2(origin.X + width, origin.Y + rowHeight * SkeletonRows);
        MarketArt.Card(drawList, ui, origin, max, scale);
        var pad = Metrics.Space.Lg * scale;
        for (var rowIndex = 0; rowIndex < SkeletonRows; rowIndex++)
        {
            var top = origin.Y + rowIndex * rowHeight;
            Skeleton.Row(drawList, new Rect(new Vector2(origin.X + pad, top), new Vector2(max.X - pad, top + rowHeight)),
                scale);
        }

        return max.Y;
    }

    private float DrawQuickLook(ImDrawListPtr drawList, Vector2 origin, float width, MarketScope scope, float scale)
    {
        var max = new Vector2(origin.X + width, origin.Y + QuickLookHeight * scale);
        var rect = new Rect(origin, max);
        var radius = Metrics.Radius.Widget * scale;
        MarketArt.Card(drawList, ui, origin, max, scale);
        Material.TopGlow(drawList, origin, max, radius, ui.Accent, QuickLookGlowCoverage, QuickLookGlowStrength);
        var hovered = MarketArt.RowInteraction(drawList, ui, rect, scale);
        var pad = Metrics.Space.Lg * scale;
        var iconSize = QuickLookIcon * scale;
        MarketArt.ItemIcon(drawList, textures, ui, lastHovered.IconId,
            new Vector2(origin.X + pad, rect.Center.Y - iconSize * 0.5f), iconSize, scale);
        var price = MarketText.Price(market.AggregatedMin(lastHovered.Id, scope));
        var priceTop = rect.Center.Y - Typography.LineHeight(TextStyles.Headline) * 0.5f;
        var priceWidth = MarketArt.Price(drawList, max.X - pad, priceTop, price, ui.TitleInk, TextStyles.Headline);
        var textLeft = origin.X + pad + iconSize + MarketArt.TextGap * scale;
        var textRight = max.X - pad - priceWidth - MarketArt.ValueGap * scale;
        var eyebrowHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var top = rect.Center.Y - (eyebrowHeight + titleHeight) * 0.5f;
        var textWidth = MathF.Max(1f, textRight - textLeft);
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(Loc.T(L.Market.HoveredInGame), textWidth, TextStyles.FootnoteEmphasized), ui.Accent,
            TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, top + eyebrowHeight),
            Typography.FitText(lastHovered.Name, textWidth, TextStyles.Headline), ui.TitleInk, TextStyles.Headline);
        if (UiInteract.Click(rect.Min, rect.Max, hovered))
        {
            OpenItem(lastHovered);
        }

        return max.Y;
    }

    private float DrawTriggered(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        alerts.CopyInto(alertBuffer);
        var triggered = 0;
        for (var alertIndex = 0; alertIndex < alertBuffer.Count; alertIndex++)
        {
            if (alertBuffer[alertIndex].Triggered && alertBuffer[alertIndex].Enabled)
            {
                alertBuffer[triggered] = alertBuffer[alertIndex];
                triggered++;
            }
        }

        if (triggered == 0)
        {
            return origin.Y;
        }

        alertBuffer.RemoveRange(triggered, alertBuffer.Count - triggered);
        var cursorY = origin.Y + MarketArt.SectionGap * scale;
        cursorY += DrawSectionTitle(drawList, new Vector2(origin.X, cursorY), width, Loc.T(L.Market.AlertHitTitle),
            Loc.T(L.Market.SeeAll), out var seeAll, scale);
        cursorY += MarketArt.HeaderGap * scale;
        if (seeAll)
        {
            Push(MarketView.Alerts());
        }

        return DrawAlertRows(drawList, new Vector2(origin.X, cursorY), width, alertBuffer, false, scale);
    }

    private float DrawWatchlist(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        sectionBuffer.Clear();
        var items = watchlist.Items;
        for (var itemIndex = 0; itemIndex < items.Count; itemIndex++)
        {
            if (index.TryGet(items[itemIndex], out var item))
            {
                sectionBuffer.Add(item);
            }
        }

        var headerTop = origin.Y + MarketArt.SectionGap * scale;
        var cursorY = headerTop + DrawSectionTitle(drawList, new Vector2(origin.X, headerTop), width,
            Loc.T(L.Market.WatchlistTitle), string.Empty, out _, scale);
        cursorY += MarketArt.HeaderGap * scale;
        if (sectionBuffer.Count == 0)
        {
            var title = Loc.T(L.Market.WatchlistEmptyTitle);
            var body = Loc.T(L.Market.WatchlistEmptyBody);
            var height = MarketArt.PanelHeight(title, body, width, scale);
            MarketArt.Panel(drawList, ui, new Vector2(origin.X, cursorY), width, height, FontAwesomeIcon.Star, title,
                body, scale);
            UiAnchors.Report("market.watchlist", new Rect(new Vector2(origin.X, headerTop),
                new Vector2(origin.X + width, cursorY + height)));
            return cursorY + height;
        }

        var rowHeight = MarketArt.RowHeight * scale;
        var max = new Vector2(origin.X + width, cursorY + sectionBuffer.Count * rowHeight);
        UiAnchors.Report("market.watchlist", new Rect(new Vector2(origin.X, headerTop), max));
        if (!ImGui.IsRectVisible(new Vector2(origin.X, cursorY), max))
        {
            return max.Y;
        }

        MarketArt.Card(drawList, ui, new Vector2(origin.X, cursorY), max, scale);
        var pad = Metrics.Space.Lg * scale;
        for (var rowIndex = 0; rowIndex < sectionBuffer.Count; rowIndex++)
        {
            var top = cursorY + rowIndex * rowHeight;
            var row = new Rect(new Vector2(origin.X, top), new Vector2(max.X, top + rowHeight));
            if (rowIndex > 0)
            {
                MarketArt.Hairline(drawList, ui, origin.X + pad + (MarketArt.IconSize + MarketArt.TextGap) * scale,
                    max.X - pad, top);
            }

            if (!ImGui.IsRectVisible(row.Min, row.Max))
            {
                continue;
            }

            if (DrawWatchRow(drawList, row, sectionBuffer[rowIndex], scale))
            {
                OpenItem(sectionBuffer[rowIndex]);
            }
        }

        return max.Y;
    }

    private bool DrawWatchRow(ImDrawListPtr drawList, Rect row, MarketItemRef item, float scale)
    {
        var series = watchlist.SeriesFor(item.Id);
        var price = watchlist.CurrentPrice(item.Id, series);
        var pad = Metrics.Space.Lg * scale;
        var priceText = MarketText.Price(price);
        var hasChange = series is { Median: > 0 } && price > 0;
        var change = hasChange ? MarketTrend.Change(price, series!.Median) : 0d;
        var pillText = !hasChange ? string.Empty : showDelta ? MarketText.Delta(price - series!.Median)
            : MarketText.Change(change);
        var pillWidth = pillText.Length > 0 ? MarketArt.PillWidth(pillText, scale) : 0f;
        var priceWidth = MarketArt.PriceWidth(priceText, TextStyles.Headline);
        var columnWidth = MathF.Max(priceWidth, pillWidth);
        var priceHeight = Typography.LineHeight(TextStyles.Headline);
        var pillHeight = pillText.Length > 0 ? MarketArt.PillHeight * scale : 0f;
        var blockTop = row.Center.Y - (priceHeight + (pillHeight > 0f ? ValueColumnGap * scale + pillHeight : 0f)) * 0.5f;
        var pillMin = new Vector2(row.Max.X - pad - pillWidth, blockTop + priceHeight + ValueColumnGap * scale);
        var pillMax = pillMin + new Vector2(pillWidth, pillHeight);
        var overPill = pillWidth > 0f && UiInteract.Hover(pillMin, pillMax);
        var hovered = !overPill && MarketArt.RowInteraction(drawList, ui, row, scale);
        var iconSize = MarketArt.IconSize * scale;
        MarketArt.ItemIcon(drawList, textures, ui, item.IconId,
            new Vector2(row.Min.X + pad, row.Center.Y - iconSize * 0.5f), iconSize, scale);
        MarketArt.Price(drawList, row.Max.X - pad, blockTop, priceText, ui.TitleInk, TextStyles.Headline);
        var trendInk = MarketArt.TrendInk(theme, change, ui.MutedInk);
        if (pillWidth > 0f)
        {
            MarketArt.ChangePill(drawList, pillMin, pillWidth, pillText, trendInk, scale);
            if (overPill)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(pillMin, pillMax, overPill))
            {
                UiFeedback.Play(UiSound.Tap);
                showDelta = !showDelta;
            }
        }

        var sparkRight = row.Max.X - pad - columnWidth - MarketArt.ValueGap * scale;
        var sparkLeft = sparkRight - MarketArt.SparkWidth * scale;
        if (series is { HasLine: true })
        {
            var sparkTop = row.Center.Y - MarketArt.SparkHeight * scale * 0.5f;
            MarketArt.Sparkline(drawList, new Rect(new Vector2(sparkLeft, sparkTop),
                    new Vector2(sparkRight, sparkTop + MarketArt.SparkHeight * scale)),
                series.Points.AsSpan(0, MarketTrend.WatchPoints), series.Median, trendInk, scale);
        }

        var textLeft = row.Min.X + pad + iconSize + MarketArt.TextGap * scale;
        var subtitle = series is { Hq: true } ? MarketText.HqCategory(item.Category) : item.Category;
        MarketArt.Labels(drawList, textLeft, sparkLeft - MarketArt.ValueGap * scale, row.Center.Y, item.Name, subtitle,
            ui.TitleInk, ui.MutedInk, scale);
        return hovered && UiInteract.Click(row.Min, row.Max, hovered);
    }

    private float DrawRecents(ImDrawListPtr drawList, Vector2 origin, float width, MarketScope scope, float scale)
    {
        sectionBuffer.Clear();
        var recents = configuration.MarketRecents;
        for (var recentIndex = 0; recentIndex < recents.Count; recentIndex++)
        {
            if (index.TryGet(recents[recentIndex], out var item))
            {
                sectionBuffer.Add(item);
            }
        }

        if (sectionBuffer.Count == 0)
        {
            return origin.Y;
        }

        var cursorY = origin.Y + MarketArt.SectionGap * scale;
        cursorY += DrawSectionTitle(drawList, new Vector2(origin.X, cursorY), width, Loc.T(L.Market.Recent),
            string.Empty, out _, scale);
        cursorY += MarketArt.HeaderGap * scale;
        var tileWidth = RecentTileWidth * scale;
        var tileHeight = RecentTileHeight * scale;
        var gap = RecentTileGap * scale;
        var rail = new Rect(new Vector2(origin.X, cursorY), new Vector2(origin.X + width, cursorY + tileHeight));
        var contentWidth = sectionBuffer.Count * tileWidth + (sectionBuffer.Count - 1) * gap;
        recentRail.Begin(rail, contentWidth);
        for (var tileIndex = 0; tileIndex < sectionBuffer.Count; tileIndex++)
        {
            var left = rail.Min.X + tileIndex * (tileWidth + gap) - recentRail.Offset;
            if (left > rail.Max.X || left + tileWidth < rail.Min.X)
            {
                continue;
            }

            var tile = new Rect(new Vector2(left, rail.Min.Y), new Vector2(left + tileWidth, rail.Max.Y));
            if (DrawRecentTile(drawList, tile, sectionBuffer[tileIndex], scope, scale))
            {
                OpenItem(sectionBuffer[tileIndex]);
            }
        }

        recentRail.End();
        return rail.Max.Y;
    }

    private bool DrawRecentTile(ImDrawListPtr drawList, Rect tile, MarketItemRef item, MarketScope scope, float scale)
    {
        MarketArt.Card(drawList, ui, tile.Min, tile.Max, scale);
        var hovered = recentRail.Hover(tile.Min, tile.Max);
        if (hovered)
        {
            MarketArt.RowInteraction(drawList, ui, tile, scale);
        }

        var pad = RecentPad * scale;
        var iconSize = RecentIconSize * scale;
        MarketArt.ItemIcon(drawList, textures, ui, item.IconId, new Vector2(tile.Min.X + pad, tile.Min.Y + pad),
            iconSize, scale);
        var textWidth = tile.Width - pad * 2f;
        var priceHeight = Typography.LineHeight(TextStyles.Footnote);
        var nameHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var priceTop = tile.Max.Y - pad - priceHeight;
        var nameTop = priceTop - nameHeight;
        Typography.Draw(drawList, new Vector2(tile.Min.X + pad, nameTop),
            Typography.FitText(item.Name, textWidth, TextStyles.FootnoteEmphasized), ui.TitleInk,
            TextStyles.FootnoteEmphasized);
        var price = market.AggregatedMin(item.Id, scope);
        WidgetText.Tabular(drawList, new Vector2(tile.Min.X + pad, priceTop), MarketText.Price(price), ui.MutedInk,
            TextStyles.Footnote);
        return recentRail.Tapped(tile.Min, tile.Max, hovered);
    }

    private float DrawResults(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        if (!index.Ready)
        {
            return DrawSkeleton(drawList, origin, width, scale);
        }

        if (results.Count == 0)
        {
            var title = Loc.T(L.Market.NoMatchingItems);
            var hint = Loc.T(L.Market.NoMatchingHint);
            var height = MarketArt.PanelHeight(title, hint, width, scale);
            MarketArt.Panel(drawList, ui, origin, width, height, FontAwesomeIcon.Search, title, hint, scale);
            return origin.Y + height;
        }

        var scope = Scope;
        prefetchBuffer.Clear();
        for (var resultIndex = 0; resultIndex < results.Count; resultIndex++)
        {
            prefetchBuffer.Add(results[resultIndex].Id);
        }

        market.PrefetchAggregated(prefetchBuffer, scope);
        var rowHeight = MarketArt.CompactRowHeight * scale;
        var max = new Vector2(origin.X + width, origin.Y + results.Count * rowHeight);
        MarketArt.Card(drawList, ui, origin, max, scale);
        var pad = Metrics.Space.Lg * scale;
        var iconSize = MarketArt.IconSize * scale;
        for (var resultIndex = 0; resultIndex < results.Count; resultIndex++)
        {
            var top = origin.Y + resultIndex * rowHeight;
            var row = new Rect(new Vector2(origin.X, top), new Vector2(max.X, top + rowHeight));
            if (resultIndex == 0)
            {
                UiAnchors.Report("market.result.first", row);
            }

            if (resultIndex > 0)
            {
                MarketArt.Hairline(drawList, ui, origin.X + pad + iconSize + MarketArt.TextGap * scale, max.X - pad,
                    top);
            }

            if (!ImGui.IsRectVisible(row.Min, row.Max))
            {
                continue;
            }

            var item = results[resultIndex];
            var hovered = MarketArt.RowInteraction(drawList, ui, row, scale);
            MarketArt.ItemIcon(drawList, textures, ui, item.IconId,
                new Vector2(row.Min.X + pad, row.Center.Y - iconSize * 0.5f), iconSize, scale);
            var price = scope.IsValid ? MarketText.Price(market.AggregatedMin(item.Id, scope)) : string.Empty;
            var priceWidth = MarketArt.Price(drawList, row.Max.X - pad,
                row.Center.Y - Typography.LineHeight(TextStyles.Headline) * 0.5f, price, ui.TitleInk,
                TextStyles.Headline);
            var textLeft = row.Min.X + pad + iconSize + MarketArt.TextGap * scale;
            MarketArt.Labels(drawList, textLeft, row.Max.X - pad - priceWidth - MarketArt.ValueGap * scale,
                row.Center.Y, item.Name, item.Category, ui.TitleInk, ui.MutedInk, scale);
            if (UiInteract.Click(row.Min, row.Max, hovered))
            {
                OpenItem(item);
            }
        }

        return max.Y;
    }

    private void UpdateHovered()
    {
        var hovered = Plugin.GameGui.HoveredItem;
        if (hovered == 0)
        {
            return;
        }

        var id = (uint)(hovered % 1_000_000);
        if (id == 0 || (hasHovered && id == lastHovered.Id))
        {
            return;
        }

        if (index.TryGet(id, out var item))
        {
            lastHovered = item;
            hasHovered = true;
        }
    }
}
