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
    private const float HeroIconSize = 56f;
    private const float HeroGap = 14f;
    private const float HeroGlowCoverage = 0.7f;
    private const float HeroGlowStrength = 0.14f;
    private const float QualityStripHeight = 30f;
    private const float StatCellHeight = 58f;
    private const int StatRows = 3;
    private const float InsightRowHeight = 58f;
    private const float InsightTileSize = 30f;
    private const float InsightGlyphSize = 15f;
    private const float WorldRowHeight = 46f;
    private const float WorldNameWidth = 0.34f;
    private const float WorldBarHeight = 6f;
    private const float WorldBarMinimum = 0.08f;
    private const float WorldBarTrackAlpha = 0.10f;
    private const float WorldGlyphSize = 11f;
    private const float WorldBarRestAlpha = 0.45f;
    private const float WorldPriceColumnWidth = 72f;
    private const int MaxWorlds = 8;
    private const int PreviewRows = 5;

    private readonly NavBarButton[] detailButtons = new NavBarButton[2];
    private readonly List<uint> detailPrefetch = new(1);
    private readonly string[] qualityLabels = new string[2];
    private bool preferHq;
    private bool autoHq;
    private uint autoHqItemId;
    private CachedText heroFooter;
    private CachedText worldsNote;

    private void PrimeDetail()
    {
        preferHq = configuration.MarketHqOnly;
        autoHqItemId = 0;
        chartSource = null;
    }

    private void DrawDetail(Rect area, MarketView view)
    {
        var context = new PhoneContext(area, theme, navigation);
        var navBar = AppHeader.BeginLargeTitle(context);
        var scope = Scope;
        if (!scope.IsValid)
        {
            MarketArt.StateScreen(ImGui.GetWindowDrawList(), ui, navBar.Body, FontAwesomeIcon.UserLock,
                Loc.T(L.Market.SignedOutTitle), Loc.T(L.Market.SignedOutBody), UiScale.Current);
        }
        else
        {
            DrawDetailBody(navBar.Body, view, scope);
        }

        var watched = watchlist.Contains(view.ItemId);
        detailButtons[0] = watched
            ? new NavBarButton(IconGlyph.Of(FontAwesomeIcon.Check), Loc.T(L.Market.Unwatch))
            : new NavBarButton(IconGlyph.Of(FontAwesomeIcon.Plus), Loc.T(L.Market.Watch));
        detailButtons[1] = new NavBarButton(IconGlyph.Of(FontAwesomeIcon.Bell), Loc.T(L.Market.PriceAlert));
        UiAnchors.Report("market.favorite", AppHeader.LargeTitleButtonRect(in navBar, 0, detailButtons.Length));
        UiAnchors.Report("market.alert", AppHeader.LargeTitleButtonRect(in navBar, 1, detailButtons.Length));
        var pressed = AppHeader.EndLargeTitle(in navBar, context, "market.detail.nav", view.Name, NavBarStyle.From(ui),
            detailButtons, DisplayName, back);
        if (pressed == 0)
        {
            var added = watchlist.Toggle(view.ItemId);
            UiFeedback.Play(added ? UiSound.ToggleOn : UiSound.ToggleOff);
        }
        else if (pressed == 1 && scope.IsValid)
        {
            OpenAlertSheet(view, scope);
        }
    }

    private void DrawDetailBody(Rect body, MarketView view, MarketScope scope)
    {
        var entry = market.RequestItem(view.ItemId, scope, false);
        var historyEntry = market.RequestHistory(view.ItemId, scope);
        detailPrefetch.Clear();
        detailPrefetch.Add(view.ItemId);
        market.PrefetchAggregated(detailPrefetch, scope);
        var snapshot = entry.Snapshot;
        using (AppSurface.Begin(body))
        {
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var cursorY = DrawScopeStrip(origin, width, "market.scope", scale) + StripGap * scale;
            if (snapshot is null)
            {
                cursorY = DrawDetailPending(drawList, new Vector2(origin.X, cursorY + Metrics.Space.Md * scale), width,
                    entry.State == MarketState.Failed, scale);
                ReserveTo(origin, width, cursorY + BottomPad * scale);
                return;
            }

            var hq = ResolveQuality(snapshot);
            SyncChart(historyEntry.History, hq);
            cursorY = DrawHero(drawList, new Vector2(origin.X, cursorY + Metrics.Space.Md * scale), width, view,
                snapshot, hq, scope, scale);
            cursorY = DrawChart(drawList, new Vector2(origin.X, cursorY + Metrics.Space.Md * scale), width,
                historyEntry, scale);
            cursorY = DrawStats(drawList, new Vector2(origin.X, cursorY + Metrics.Space.Md * scale), width, snapshot,
                hq, scale);
            cursorY = DrawInsights(drawList, new Vector2(origin.X, cursorY), width, view, snapshot, hq, scope, scale);
            cursorY = DrawWorlds(drawList, new Vector2(origin.X, cursorY), width, snapshot, hq, scope, scale);
            cursorY = DrawListingsPreview(drawList, new Vector2(origin.X, cursorY), width, view, snapshot, hq, scope,
                scale);
            cursorY = DrawSalesPreview(drawList, new Vector2(origin.X, cursorY), width, view, snapshot, hq, scale);
            ReserveTo(origin, width, cursorY + BottomPad * scale);
        }
    }

    private float DrawDetailPending(ImDrawListPtr drawList, Vector2 origin, float width, bool failed, float scale)
    {
        if (failed)
        {
            var title = Loc.T(L.Market.CouldntReach);
            var body = Loc.T(L.Market.CouldntReachBody);
            var height = MarketArt.PanelHeight(title, body, width, scale);
            MarketArt.Panel(drawList, ui, origin, width, height, FontAwesomeIcon.Plug, title, body, scale);
            return origin.Y + height;
        }

        var heroHeight = (HeroIconSize + Metrics.Space.Lg * 2f) * scale;
        var heroMax = new Vector2(origin.X + width, origin.Y + heroHeight);
        MarketArt.Card(drawList, ui, origin, heroMax, scale);
        var pad = Metrics.Space.Lg * scale;
        Skeleton.Row(drawList, new Rect(origin + new Vector2(pad, pad), heroMax - new Vector2(pad, pad)), scale);
        var chartTop = heroMax.Y + Metrics.Space.Md * scale;
        var chartMax = new Vector2(origin.X + width, chartTop + (ChartHeight + ChartHeaderHeight) * scale);
        MarketArt.Card(drawList, ui, new Vector2(origin.X, chartTop), chartMax, scale);
        Skeleton.Bar(drawList, new Vector2(origin.X + pad, chartTop + pad), chartMax - new Vector2(pad, pad),
            Metrics.Radius.Md * scale);
        return chartMax.Y;
    }

    private float DrawHero(ImDrawListPtr drawList, Vector2 origin, float width, MarketView view,
        MarketSnapshot snapshot, bool hq, MarketScope scope, float scale)
    {
        var pad = Metrics.Space.Lg * scale;
        var iconSize = HeroIconSize * scale;
        var eyebrowHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var priceStyle = TextStyles.WidgetDisplayCompact;
        var priceHeight = Typography.LineHeight(priceStyle);
        var changeHeight = MathF.Max(MarketArt.PillHeight * scale, Typography.LineHeight(TextStyles.Footnote));
        var footerHeight = Typography.LineHeight(TextStyles.Footnote);
        var textBlock = eyebrowHeight + priceHeight + MarketArt.LineGap * scale + changeHeight;
        var topBlock = MathF.Max(iconSize, textBlock);
        var hasQuality = snapshot.HasHq;
        var height = pad * 2f + topBlock + HeroGap * scale + footerHeight +
                     (hasQuality ? HeroGap * scale + QualityStripHeight * scale : 0f);
        var max = new Vector2(origin.X + width, origin.Y + height);
        var radius = Metrics.Radius.Widget * scale;
        UiAnchors.Report("market.detail.hero", new Rect(origin, max));
        ui.Card(drawList, origin, max, radius, true);
        Material.TopGlow(drawList, origin, max, radius, ui.Accent, HeroGlowCoverage, HeroGlowStrength);
        var left = origin.X + pad;
        var right = max.X - pad;
        MarketArt.ItemIcon(drawList, textures, ui, view.IconId, new Vector2(left, origin.Y + pad), iconSize, scale);
        var textLeft = left + iconSize + HeroGap * scale;
        var textWidth = MathF.Max(1f, right - textLeft);
        var top = origin.Y + pad + (topBlock - textBlock) * 0.5f;
        var eyebrow = MarketText.Format(hq ? L.Market.CheapestHqIn : L.Market.CheapestIn, scope.ApiName);
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(eyebrow, textWidth, TextStyles.FootnoteEmphasized), ui.MutedInk,
            TextStyles.FootnoteEmphasized);
        top += eyebrowHeight;
        var cheapest = snapshot.Min(hq);
        var priceText = MarketText.Price(cheapest);
        var fitted = WidgetText.FitStyle(priceText, priceStyle, textWidth, true);
        WidgetText.Tabular(drawList, new Vector2(textLeft, top + (priceHeight - Typography.LineHeight(fitted))),
            priceText, ui.TitleInk, fitted);
        top += priceHeight + MarketArt.LineGap * scale;
        DrawHeroChange(drawList, new Vector2(textLeft, top), textWidth, changeHeight, cheapest, scale);

        var footerTop = origin.Y + pad + topBlock + HeroGap * scale;
        Typography.Draw(drawList, new Vector2(left, footerTop),
            Typography.FitText(HeroFooter(snapshot, hq), right - left, TextStyles.Footnote), ui.MutedInk,
            TextStyles.Footnote);
        if (hasQuality)
        {
            var stripTop = footerTop + footerHeight + HeroGap * scale;
            var strip = new Rect(new Vector2(left, stripTop), new Vector2(right, stripTop + QualityStripHeight * scale));
            qualityLabels[0] = Loc.T(L.Common.Nq);
            qualityLabels[1] = Loc.T(L.Common.Hq);
            var selected = SegmentStrip.Draw("market.quality", strip, qualityLabels, hq ? 1 : 0, ui.Palette);
            if (selected != (hq ? 1 : 0))
            {
                UiFeedback.Play(UiSound.Tap);
                SetQuality(selected == 1);
            }
        }

        return max.Y;
    }

    private void DrawHeroChange(ImDrawListPtr drawList, Vector2 origin, float width, float height, long cheapest,
        float scale)
    {
        var lineTop = origin.Y + (height - Typography.LineHeight(TextStyles.Footnote)) * 0.5f;
        if (chartMedianWeek <= 0 || cheapest <= 0)
        {
            var waiting = chartLoading ? Loc.T(L.Common.Loading) : Loc.T(L.Market.NoRecentSales);
            Typography.Draw(drawList, new Vector2(origin.X, lineTop),
                Typography.FitText(waiting, width, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
            return;
        }

        var change = MarketTrend.Change(cheapest, chartMedianWeek);
        var pillText = MarketText.Change(change);
        var pillWidth = MarketArt.PillWidth(pillText, scale);
        var pillTop = origin.Y + (height - MarketArt.PillHeight * scale) * 0.5f;
        MarketArt.ChangePill(drawList, new Vector2(origin.X, pillTop), pillWidth, pillText,
            MarketArt.TrendInk(theme, change, ui.MutedInk), scale);
        var labelLeft = origin.X + pillWidth + Metrics.Space.Sm * scale;
        var label = MarketText.Format(L.Market.VersusMedian, chartMedianWeek);
        Typography.Draw(drawList, new Vector2(labelLeft, lineTop),
            Typography.FitText(label, MathF.Max(1f, origin.X + width - labelLeft), TextStyles.Footnote), ui.MutedInk,
            TextStyles.Footnote);
    }

    private string HeroFooter(MarketSnapshot snapshot, bool hq)
    {
        var world = string.Empty;
        var listings = snapshot.Listings;
        if (snapshot.MultiWorld)
        {
            for (var listingIndex = 0; listingIndex < listings.Length; listingIndex++)
            {
                if (listings[listingIndex].Hq == hq)
                {
                    world = listings[listingIndex].World;
                    break;
                }
            }
        }

        var minutes = snapshot.LastUpload == default
            ? -1L
            : (long)(DateTime.UtcNow - snapshot.LastUpload).TotalMinutes;
        var key = (minutes << 20) ^ world.GetHashCode() ^ snapshot.ItemId;
        if (heroFooter.IsCurrent(key))
        {
            return heroFooter.Value;
        }

        var updated = Loc.T(L.Market.UpdatedAgo, TimeText.Ago(snapshot.LastUpload));
        return heroFooter.Store(key, world.Length > 0 ? Loc.T(L.Market.OnWorld, world, updated) : updated);
    }

    private float DrawStats(ImDrawListPtr drawList, Vector2 origin, float width, MarketSnapshot snapshot, bool hq,
        float scale)
    {
        var cellHeight = StatCellHeight * scale;
        var max = new Vector2(origin.X + width, origin.Y + cellHeight * StatRows);
        if (!ImGui.IsRectVisible(origin, max))
        {
            return max.Y;
        }

        MarketArt.Card(drawList, ui, origin, max, scale);
        var pad = Metrics.Space.Lg * scale;
        var columnWidth = width * 0.5f;
        var range = RangeLabel(chartRange);
        DrawStatCell(drawList, origin, columnWidth, cellHeight, MarketText.Format(L.Market.StatMedian, range),
            MarketText.Price(chartMedianRange), scale);
        DrawStatCell(drawList, new Vector2(origin.X + columnWidth, origin.Y), columnWidth, cellHeight,
            Loc.T(L.Market.Average), MarketText.Price(snapshot.Average(hq)), scale);
        DrawStatCell(drawList, new Vector2(origin.X, origin.Y + cellHeight), columnWidth, cellHeight,
            Loc.T(L.Market.SalesPerDay), MarketText.Velocity(snapshot.Velocity(hq)), scale);
        DrawStatCell(drawList, new Vector2(origin.X + columnWidth, origin.Y + cellHeight), columnWidth, cellHeight,
            MarketText.Format(L.Market.StatSold, range), chartUnits > 0 ? MarketFormat.Gil(chartUnits) : "-", scale);
        DrawStatCell(drawList, new Vector2(origin.X, origin.Y + cellHeight * 2f), columnWidth, cellHeight,
            Loc.T(L.Market.Highest), MarketText.Price(snapshot.Max(hq)), scale);
        DrawStatCell(drawList, new Vector2(origin.X + columnWidth, origin.Y + cellHeight * 2f), columnWidth,
            cellHeight, Loc.T(L.Market.UnitsListed),
            snapshot.UnitsForSale > 0 ? MarketFormat.Gil(snapshot.UnitsForSale) : "-", scale);
        for (var rowIndex = 1; rowIndex < StatRows; rowIndex++)
        {
            MarketArt.Hairline(drawList, ui, origin.X + pad, max.X - pad, origin.Y + rowIndex * cellHeight);
        }

        drawList.AddLine(new Vector2(origin.X + columnWidth, origin.Y + pad),
            new Vector2(origin.X + columnWidth, max.Y - pad), ImGui.GetColorU32(ui.Hairline),
            Metrics.Stroke.Hairline);
        return max.Y;
    }

    private void DrawStatCell(ImDrawListPtr drawList, Vector2 origin, float width, float height, string label,
        string value, float scale)
    {
        var pad = Metrics.Space.Lg * scale;
        var textWidth = MathF.Max(1f, width - pad * 2f);
        var labelHeight = Typography.LineHeight(TextStyles.Footnote);
        var valueHeight = Typography.LineHeight(TextStyles.Headline);
        var top = origin.Y + (height - labelHeight - valueHeight - MarketArt.LineGap * scale) * 0.5f;
        Typography.Draw(drawList, new Vector2(origin.X + pad, top),
            Typography.FitText(label, textWidth, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
        var fitted = WidgetText.FitStyle(value, TextStyles.Headline, textWidth, true);
        WidgetText.Tabular(drawList, new Vector2(origin.X + pad, top + labelHeight + MarketArt.LineGap * scale), value,
            ui.TitleInk, fitted);
    }

    private float DrawInsights(ImDrawListPtr drawList, Vector2 origin, float width, MarketView view,
        MarketSnapshot snapshot, bool hq, MarketScope scope, float scale)
    {
        var cheapest = snapshot.Min(hq);
        var hasVendor = index.TryGet(view.ItemId, out var item) && item.VendorPrice > 0;
        var hasCheaper = market.TryFindCheaperScope(view.ItemId, scope, hq, cheapest, out var cheaperPrice,
            out var cheaperWorld);
        var hasTax = market.TryGetLowestTax(gameData.WorldName(gameData.LocalCurrentWorldId), out var taxRate,
            out var taxCity) && cheapest > 0;
        var rows = (hasVendor ? 1 : 0) + (hasCheaper ? 1 : 0) + (hasTax ? 1 : 0);
        if (rows == 0)
        {
            return origin.Y;
        }

        var top = origin.Y + Metrics.Space.Md * scale;
        var rowHeight = InsightRowHeight * scale;
        var max = new Vector2(origin.X + width, top + rows * rowHeight);
        if (!ImGui.IsRectVisible(new Vector2(origin.X, top), max))
        {
            return max.Y;
        }

        MarketArt.Card(drawList, ui, new Vector2(origin.X, top), max, scale);
        var drawn = 0;
        if (hasVendor)
        {
            var subtitle = cheapest <= 0 || item.VendorPrice < cheapest ? Loc.T(L.Market.VendorCheaper) : string.Empty;
            DrawInsightRow(drawList, origin.X, width, top, drawn++, FontAwesomeIcon.Store, Loc.T(L.Market.VendorNpc),
                subtitle, MarketText.Price((long)item.VendorPrice), scale);
        }

        if (hasCheaper)
        {
            DrawInsightRow(drawList, origin.X, width, top, drawn++, FontAwesomeIcon.Globe,
                MarketText.Format(L.Market.CheaperOn, gameData.WorldName(cheaperWorld)),
                MarketText.Change(MarketTrend.Change(cheaperPrice, cheapest)), MarketText.Price(cheaperPrice), scale);
        }

        if (hasTax)
        {
            var net = cheapest - cheapest * taxRate / 100L;
            DrawInsightRow(drawList, origin.X, width, top, drawn, FontAwesomeIcon.BalanceScale,
                MarketText.Format(L.Market.AfterTaxTitle, taxRate), MarketText.Format(L.Market.TaxCity, taxCity),
                MarketText.Price(net), scale);
        }

        return max.Y;
    }

    private void DrawInsightRow(ImDrawListPtr drawList, float left, float width, float cardTop, int rowIndex,
        FontAwesomeIcon icon, string title, string subtitle, string value, float scale)
    {
        var rowHeight = InsightRowHeight * scale;
        var top = cardTop + rowIndex * rowHeight;
        var pad = Metrics.Space.Lg * scale;
        var tileSize = InsightTileSize * scale;
        if (rowIndex > 0)
        {
            MarketArt.Hairline(drawList, ui, left + pad + tileSize + MarketArt.TextGap * scale, left + width - pad, top);
        }

        var centerY = top + rowHeight * 0.5f;
        var tileMin = new Vector2(left + pad, centerY - tileSize * 0.5f);
        var tileMax = tileMin + new Vector2(tileSize, tileSize);
        IconTile.FillShaded(drawList, tileMin, tileMax, tileSize * Metrics.Radius.TileFactor,
            IconTile.Surface(ui.Accent));
        ProgressRing.CenterIcon(drawList, (tileMin + tileMax) * 0.5f, icon, AccentRing.Ink, InsightGlyphSize * scale);
        var valueTop = centerY - Typography.LineHeight(TextStyles.Headline) * 0.5f;
        var valueWidth = MarketArt.Price(drawList, left + width - pad, valueTop, value, ui.TitleInk,
            TextStyles.Headline);
        MarketArt.Labels(drawList, tileMax.X + MarketArt.TextGap * scale,
            left + width - pad - valueWidth - MarketArt.ValueGap * scale, centerY, title, subtitle, ui.TitleInk,
            ui.MutedInk, scale);
    }

    private float DrawWorlds(ImDrawListPtr drawList, Vector2 origin, float width, MarketSnapshot snapshot, bool hq,
        MarketScope scope, float scale)
    {
        var offers = snapshot.Worlds(hq);
        if (offers.Length < 2)
        {
            return origin.Y;
        }

        var cursorY = origin.Y + MarketArt.SectionGap * scale;
        cursorY += DrawSectionTitle(drawList, new Vector2(origin.X, cursorY), width,
            MarketText.Format(L.Market.AcrossScope, scope.ApiName), string.Empty, out _, scale);
        cursorY += MarketArt.HeaderGap * scale;
        var shown = Math.Min(offers.Length, MaxWorlds);
        var rowHeight = WorldRowHeight * scale;
        var cardTop = cursorY;
        var max = new Vector2(origin.X + width, cardTop + shown * rowHeight + Metrics.Space.Sm * scale * 2f);
        UiAnchors.Report("market.worlds", new Rect(new Vector2(origin.X, cardTop), max));
        if (ImGui.IsRectVisible(new Vector2(origin.X, cardTop), max))
        {
            MarketArt.Card(drawList, ui, new Vector2(origin.X, cardTop), max, scale);
            var highest = offers[shown - 1].Cheapest;
            var here = gameData.WorldName(gameData.LocalCurrentWorldId);
            for (var offerIndex = 0; offerIndex < shown; offerIndex++)
            {
                var top = cardTop + Metrics.Space.Sm * scale + offerIndex * rowHeight;
                DrawWorldRow(drawList, new Rect(new Vector2(origin.X, top), new Vector2(max.X, top + rowHeight)),
                    offers[offerIndex], highest, offerIndex == 0,
                    string.Equals(offers[offerIndex].World, here, StringComparison.Ordinal), scale);
            }
        }

        cursorY = max.Y + Metrics.Space.Sm * scale;
        cursorY += Typography.DrawWrappedLeft(new Vector2(origin.X, cursorY), WorldsNote(offers.Length, scope),
            ui.MutedInk, TextStyles.Footnote, width);
        return cursorY;
    }

    private void DrawWorldRow(ImDrawListPtr drawList, Rect row, in MarketWorldOffer offer, long highest,
        bool cheapest, bool here, float scale)
    {
        var pad = Metrics.Space.Lg * scale;
        var nameWidth = (row.Width - pad * 2f) * WorldNameWidth;
        var nameLeft = row.Min.X + pad;
        var nameInk = here ? ui.Accent : ui.TitleInk;
        if (here)
        {
            ProgressRing.CenterIcon(drawList, new Vector2(nameLeft + WorldGlyphSize * scale * 0.5f, row.Center.Y),
                FontAwesomeIcon.LocationArrow, ui.Accent, WorldGlyphSize * scale);
            nameLeft += (WorldGlyphSize + Metrics.Space.Xs) * scale;
        }

        var nameHeight = Typography.LineHeight(TextStyles.BodyEmphasized);
        Typography.Draw(drawList, new Vector2(nameLeft, row.Center.Y - nameHeight * 0.5f),
            Typography.FitText(offer.World, MathF.Max(1f, row.Min.X + pad + nameWidth - nameLeft),
                TextStyles.BodyEmphasized), nameInk, TextStyles.BodyEmphasized);
        var price = MarketText.Price(offer.Cheapest);
        var priceWidth = MarketArt.Price(drawList, row.Max.X - pad,
            row.Center.Y - Typography.LineHeight(TextStyles.Headline) * 0.5f, price,
            cheapest ? MarketArt.UpInk(theme) : ui.TitleInk, TextStyles.Headline);
        var barLeft = row.Min.X + pad + nameWidth + MarketArt.ValueGap * scale;
        var barRight = row.Max.X - pad - MathF.Max(priceWidth, WorldPriceColumnWidth * scale) - MarketArt.ValueGap * scale;
        if (barRight - barLeft < WorldBarHeight * scale * 2f)
        {
            return;
        }

        var barHeight = WorldBarHeight * scale;
        var barMin = new Vector2(barLeft, row.Center.Y - barHeight * 0.5f);
        var barMax = new Vector2(barRight, row.Center.Y + barHeight * 0.5f);
        drawList.AddRectFilled(barMin, barMax, ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, WorldBarTrackAlpha)),
            barHeight * 0.5f);
        var fraction = highest > 0 ? Math.Clamp((float)offer.Cheapest / highest, WorldBarMinimum, 1f) : 1f;
        var fill = cheapest ? MarketArt.UpInk(theme) : here ? ui.Accent : Palette.WithAlpha(ui.TitleInk, WorldBarRestAlpha);
        drawList.AddRectFilled(barMin, new Vector2(barLeft + (barRight - barLeft) * fraction, barMax.Y),
            ImGui.GetColorU32(fill), barHeight * 0.5f);
    }

    private string WorldsNote(int count, MarketScope scope)
    {
        var key = ((long)count << 32) ^ scope.ApiName.GetHashCode();
        if (worldsNote.IsCurrent(key))
        {
            return worldsNote.Value;
        }

        return worldsNote.Store(key, Loc.T(L.Market.WorldsNote, scope.ApiName));
    }

    private void SetQuality(bool hq)
    {
        preferHq = hq;
        autoHq = false;
        configuration.MarketHqOnly = hq;
        configuration.Save();
    }

    private bool ResolveQuality(MarketSnapshot snapshot)
    {
        var hasHq = snapshot.HasHq;
        if (autoHqItemId != snapshot.ItemId)
        {
            autoHqItemId = snapshot.ItemId;
            autoHq = hasHq && !preferHq && snapshot.Min(false) <= 0 && snapshot.Min(true) > 0;
        }

        return hasHq && (preferHq || autoHq);
    }
}
