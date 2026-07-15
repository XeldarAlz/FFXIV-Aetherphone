using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Market;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Market;

internal sealed partial class MarketApp
{
    private void DrawDetail(Rect area, MarketView view)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var context = new PhoneContext(area, frameTheme, frameNavigation);
        AppHeader.Draw(context, MarketFormat.Clip(view.Name, 18), backToList);
        DrawHeaderButtons(area, view, out var forceRefresh);
        var scope = CurrentScope;
        if (!scope.IsValid)
        {
            Typography.DrawCentered(area.Center, Loc.T(L.Market.LogInToViewPrices), frameTheme.TextMuted);
            return;
        }

        var entry = market.RequestItem(view.ItemId, scope, forceRefresh);
        var snapshot = entry.Snapshot;
        var top = area.Min.Y + AppHeader.Height * scale;
        var scopeBar = new Rect(new Vector2(area.Min.X, top), new Vector2(area.Max.X, top + ScopeBarHeight * scale));
        DrawBrandedScopeBar(scopeBar);
        var bodyTop = scopeBar.Max.Y + 4f * scale;
        if (snapshot is null)
        {
            if (entry.State == MarketState.Failed)
            {
                Typography.DrawCentered(new Vector2(area.Center.X, bodyTop + 60f * scale), Loc.T(L.Market.CouldntReach),
                    frameTheme.TextMuted);
            }
            else
            {
                LoadingPulse.Draw(new Vector2(area.Center.X, bodyTop + 46f * scale), 13f * scale,
                    AppPalettes.Market.Accent, frameTheme.TextMuted, Loc.T(L.Common.Loading));
            }

            return;
        }

        var body = new Rect(new Vector2(area.Min.X, bodyTop), area.Max);
        using (AppSurface.Begin(body))
        {
            var hasHq = snapshot.HasHq;
            var effectiveHq = hasHq && showHq;
            DrawHero(view, snapshot, effectiveHq, hasHq);
            DrawPrices(snapshot, effectiveHq);
            DrawAlertEditor(view, snapshot, effectiveHq, scope);
            DrawTrendSection(snapshot, effectiveHq);
            DrawListingsSection(snapshot, effectiveHq);
            DrawSalesSection(snapshot, effectiveHq);
        }
    }

    private void DrawHero(MarketView view, MarketSnapshot snapshot, bool hq, bool hasHq)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var iconSize = 56f * scale;
        var leftPad = 14f * scale;
        var cardHeight = iconSize + 24f * scale;
        var cardRounding = 14f * scale;
        var cardMin = new Vector2(origin.X, origin.Y);
        var cardMax = new Vector2(origin.X + width, origin.Y + cardHeight);
        Squircle.Fill(drawList, cardMin, cardMax, cardRounding, ImGui.GetColorU32(frameTheme.GroupedCard));
        Material.EdgeSquircle(drawList, cardMin, cardMax, cardRounding, scale);
        var tileRounding = 13f * scale;
        var iconMin = new Vector2(origin.X + leftPad, origin.Y + (cardHeight - iconSize) * 0.5f);
        var iconMax = iconMin + new Vector2(iconSize, iconSize);
        Elevation.Card(drawList, iconMin, iconMax, tileRounding, scale, 0.5f);
        Squircle.Fill(drawList, iconMin, iconMax, tileRounding, ImGui.GetColorU32(AppPalettes.Market.CardFill));
        if (view.IconId != 0)
        {
            var texture = textures.GetFromGameIcon(new GameIconLookup(view.IconId)).GetWrapOrEmpty();
            var inset = 4f * scale;
            drawList.AddImageRounded(texture.Handle, iconMin + new Vector2(inset, inset),
                iconMax - new Vector2(inset, inset), Vector2.Zero, Vector2.One, 0xFFFFFFFFu, tileRounding - inset);
        }

        Material.EdgeSquircle(drawList, iconMin, iconMax, tileRounding, scale);
        var textX = iconMax.X + 14f * scale;
        var textTop = iconMin.Y + 4f * scale;
        Typography.Draw(new Vector2(textX, textTop), MarketFormat.Clip(view.Name, 18), frameTheme.TextStrong,
            TextStyles.Title3);
        var min = snapshot.Min(hq);
        var priceText = PriceOrDash(min);
        var priceSize = Typography.Measure(priceText, 1.4f, FontWeight.SemiBold);
        Typography.Draw(new Vector2(textX, textTop + 26f * scale), priceText, AppPalettes.Market.Accent, 1.4f,
            FontWeight.SemiBold);
        var cheapestLabel = hq ? Loc.T(L.Market.CheapestHq) : Loc.T(L.Market.Cheapest);
        Typography.Draw(
            new Vector2(textX + priceSize.X + 8f * scale, textTop + 28f * scale + priceSize.Y * 0.5f - 6f * scale),
            cheapestLabel, frameTheme.TextMuted, 0.78f);
        if (hasHq)
        {
            var pillGap = 6f * scale;
            var pillHeight = 26f * scale;
            var nqWidth = Typography.Measure(Loc.T(L.Common.Nq), 0.82f, FontWeight.SemiBold).X + 18f * scale;
            var hqWidth = Typography.Measure(Loc.T(L.Common.Hq), 0.82f, FontWeight.SemiBold).X + 18f * scale;
            var pillY = iconMax.Y - pillHeight - 2f * scale;
            var pillTotalWidth = nqWidth + pillGap + hqWidth;
            var pillStartX = origin.X + width - 16f * scale - pillTotalWidth;
            var nqRect = new Rect(new Vector2(pillStartX, pillY),
                new Vector2(pillStartX + nqWidth, pillY + pillHeight));
            var hqRect = new Rect(new Vector2(pillStartX + nqWidth + pillGap, pillY),
                new Vector2(pillStartX + nqWidth + pillGap + hqWidth, pillY + pillHeight));
            if (ui.PillButton(nqRect, Loc.T(L.Common.Nq), !showHq))
            {
                SetQuality(false);
            }

            if (ui.PillButton(hqRect, Loc.T(L.Common.Hq), showHq))
            {
                SetQuality(true);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cardHeight + 6f * scale));
    }

    private void DrawPrices(MarketSnapshot snapshot, bool hq)
    {
        var hasVendor = index.TryGet(snapshot.ItemId, out var itemRef) && itemRef.VendorPrice > 0;
        var rowCount = hasVendor ? 6 : 5;
        ui.SectionHeading(Loc.T(L.Market.Prices), 14f);
        var card = GroupCard.Begin(frameTheme, rowCount);
        SettingsRow.Info(card.NextRow(), Loc.T(L.Market.Average), PriceOrDash(snapshot.Average(hq)), frameTheme);
        SettingsRow.Info(card.NextRow(), Loc.T(L.Market.Highest), PriceOrDash(snapshot.Max(hq)), frameTheme);
        SettingsRow.Info(card.NextRow(), Loc.T(L.Market.SalesPerDay), MarketFormat.Velocity(snapshot.Velocity(hq)),
            frameTheme);
        SettingsRow.Info(card.NextRow(), Loc.T(L.Market.UpSold), $"{snapshot.UnitsForSale} / {snapshot.UnitsSold}",
            frameTheme);
        SettingsRow.Info(card.NextRow(), Loc.T(L.Market.Updated), TimeText.Ago(snapshot.LastUpload), frameTheme);
        if (hasVendor)
        {
            var marketMin = snapshot.Min(hq);
            var value = MarketFormat.Gil(itemRef.VendorPrice);
            if (marketMin > 0 && itemRef.VendorPrice < marketMin)
            {
                value += $"  \u00b7  {Loc.T(L.Market.Cheaper)}";
            }

            SettingsRow.Info(card.NextRow(), Loc.T(L.Market.VendorNpc), value, frameTheme);
        }

        card.End();
    }

    private void DrawAlertEditor(MarketView view, MarketSnapshot snapshot, bool hq, MarketScope scope)
    {
        ui.SectionHeading(Loc.T(L.Market.PriceAlert), 14f);
        var card = GroupCard.Begin(frameTheme, 1);
        var existing = alerts.HasAlertFor(view.ItemId);
        var label = showAlertEditor ? Loc.T(L.Common.Cancel) :
            existing ? Loc.T(L.Market.AddAnotherAlert) : Loc.T(L.Market.SetPriceAlert);
        if (SettingsRow.Link(card.NextRow(), FontAwesomeIcon.Bell, frameTheme.Accent, label, string.Empty, frameTheme))
        {
            showAlertEditor = !showAlertEditor;
            if (showAlertEditor)
            {
                var min = snapshot.Min(hq);
                alertThreshold = (int)Math.Clamp(min > 0 ? min : 1, 1, int.MaxValue);
                alertBelow = true;
            }
        }

        card.End();
        if (!showAlertEditor)
        {
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        ImGui.Dummy(new Vector2(0f, 8f * scale));
        DrawThresholdField();
        ImGui.Dummy(new Vector2(0f, 8f * scale));
        var dirOrigin = ImGui.GetCursorScreenPos();
        var toggleWidth = 220f * scale;
        var toggleHeight = 30f * scale;
        alertDirLabels[0] = Loc.T(L.Market.AtOrBelow);
        alertDirLabels[1] = Loc.T(L.Market.AtOrAbove);
        var dirIndex = SegmentStrip.Draw("market.alertDir",
            new Rect(dirOrigin, new Vector2(dirOrigin.X + toggleWidth, dirOrigin.Y + toggleHeight)), alertDirLabels,
            alertBelow ? 0 : 1, frameTheme);
        alertBelow = dirIndex == 0;
        ImGui.SetCursorScreenPos(dirOrigin);
        ImGui.Dummy(new Vector2(toggleWidth, toggleHeight));
        ImGui.Dummy(new Vector2(0f, 10f * scale));
        if (DrawPrimaryButton(Loc.T(L.Market.CreateAlert)))
        {
            alerts.Add(new MarketAlert
            {
                ItemId = view.ItemId,
                ItemName = view.Name,
                IconId = view.IconId,
                ScopeKind = scope.Kind,
                ScopeName = scope.ApiName,
                HqOnly = hq,
                Threshold = alertThreshold,
                Below = alertBelow,
                Enabled = true,
            });
            showAlertEditor = false;
        }
    }

    private void DrawThresholdField()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = 40f * scale;
        var pillMin = origin;
        var pillMax = new Vector2(origin.X + width, origin.Y + height);
        Squircle.Fill(drawList, pillMin, pillMax, 12f * scale, ImGui.GetColorU32(frameTheme.GroupedCard));
        Material.EdgeSquircle(drawList, pillMin, pillMax, 12f * scale, scale);
        var labelSize = Typography.Measure("Gil", TextStyles.FootnoteEmphasized);
        Typography.Draw(new Vector2(pillMin.X + 14f * scale, pillMin.Y + height * 0.5f - labelSize.Y * 0.5f), "Gil",
            frameTheme.TextMuted, TextStyles.FootnoteEmphasized);
        var inputLeft = pillMin.X + 48f * scale;
        ImGui.SetCursorScreenPos(new Vector2(inputLeft, pillMin.Y + height * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(pillMax.X - inputLeft - 14f * scale);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, frameTheme.TextStrong))
        {
            ImGui.InputInt("##marketAlertThreshold", ref alertThreshold, 0, 0);
        }

        if (alertThreshold < 1)
        {
            alertThreshold = 1;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private bool DrawPrimaryButton(string label)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = 42f * scale;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        var hovered = ImGui.IsMouseHoveringRect(min, max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var fill = pressed ? Palette.Mix(frameTheme.Accent, new Vector4(0f, 0f, 0f, 1f), 0.14f) :
            hovered ? Palette.Mix(frameTheme.Accent, frameTheme.TextStrong, 0.10f) : frameTheme.Accent;
        Elevation.Card(drawList, min, max, 12f * scale, scale, 0.6f);
        Squircle.Fill(drawList, min, max, 12f * scale, ImGui.GetColorU32(fill));
        drawList.AddLine(new Vector2(min.X + 12f * scale, min.Y + 1f * scale),
            new Vector2(max.X - 12f * scale, min.Y + 1f * scale), ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.12f)),
            1f * scale);
        Typography.DrawCentered((min + max) * 0.5f, label, new Vector4(0.99f, 0.99f, 1f, 1f), TextStyles.Headline);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private void DrawTrendSection(MarketSnapshot snapshot, bool hq)
    {
        var sales = snapshot.Sales;
        var count = CountQuality(sales, hq);
        if (count < 2)
        {
            return;
        }

        ui.SectionHeading(Loc.T(L.Market.Trend), 14f);
        var scale = ImGuiHelpers.GlobalScale;
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var height = 60f * scale;
        var graph = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        Span<float> values = count <= 64 ? stackalloc float[count] : new float[count];
        var cursor = 0;
        for (var saleIndex = sales.Length - 1; saleIndex >= 0; saleIndex--)
        {
            if (sales[saleIndex].Hq == hq)
            {
                values[cursor++] = sales[saleIndex].PricePerUnit;
            }
        }

        Sparkline.Draw(graph, values, AppPalettes.Market.Accent, Palette.WithAlpha(AppPalettes.Market.Accent, 0.18f));
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawListingsSection(MarketSnapshot snapshot, bool hq)
    {
        var listings = snapshot.Listings;
        var count = CountListings(listings, hq);
        ui.SectionHeading(count > 0 ? Loc.T(L.Market.ListingsCount, count) : Loc.T(L.Market.Listings), 14f);
        if (count == 0)
        {
            DrawEmptyCard(hq ? Loc.T(L.Market.NoHqListings) : Loc.T(L.Market.NoListings));
            return;
        }

        var shown = Math.Min(count, MaxRowsPerSection);
        var card = GroupCard.Begin(frameTheme, shown, MarketRowViews.DataRowHeight);
        var drawn = 0;
        for (var listingIndex = 0; listingIndex < listings.Length && drawn < shown; listingIndex++)
        {
            if (listings[listingIndex].Hq != hq)
            {
                continue;
            }

            MarketRowViews.ListingRow(card.NextRow(), listings[listingIndex], snapshot.MultiWorld, frameTheme);
            drawn++;
        }

        card.End();
    }

    private void DrawSalesSection(MarketSnapshot snapshot, bool hq)
    {
        var sales = snapshot.Sales;
        var count = CountQuality(sales, hq);
        ui.SectionHeading(count > 0 ? Loc.T(L.Market.RecentSalesCount, count) : Loc.T(L.Market.RecentSales), 14f);
        if (count == 0)
        {
            DrawEmptyCard(hq ? Loc.T(L.Market.NoHqSales) : Loc.T(L.Market.NoRecentSales));
            return;
        }

        var shown = Math.Min(count, MaxRowsPerSection);
        var card = GroupCard.Begin(frameTheme, shown, MarketRowViews.DataRowHeight);
        var drawn = 0;
        for (var saleIndex = 0; saleIndex < sales.Length && drawn < shown; saleIndex++)
        {
            if (sales[saleIndex].Hq != hq)
            {
                continue;
            }

            MarketRowViews.SaleRow(card.NextRow(), sales[saleIndex], snapshot.MultiWorld, frameTheme);
            drawn++;
        }

        card.End();
    }

    private void DrawEmptyCard(string message)
    {
        var card = GroupCard.Begin(frameTheme, 1);
        var row = card.NextRow();
        var size = Typography.Measure(message);
        Typography.Draw(new Vector2(row.Center.X - size.X * 0.5f, row.Center.Y - size.Y * 0.5f), message,
            frameTheme.TextMuted);
        card.End();
    }

    private void DrawHeaderButtons(Rect area, MarketView view, out bool forceRefresh)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var midY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        var starCenter = new Vector2(area.Max.X - 18f * scale, midY);
        var refreshCenter = new Vector2(area.Max.X - 46f * scale, midY);
        var favorite = IsFavorite(view.ItemId);
        if (IconButton(starCenter, FontAwesomeIcon.Star, favorite ? frameTheme.Accent : frameTheme.TextMuted))
        {
            ToggleFavorite(view.ItemId);
        }

        forceRefresh = IconButton(refreshCenter, FontAwesomeIcon.Sync, frameTheme.TextMuted);
    }

    private bool IconButton(Vector2 center, FontAwesomeIcon icon, Vector4 color)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var box = 14f * scale;
        var hovered = ImGui.IsMouseHoveringRect(center - new Vector2(box, box), center + new Vector2(box, box));
        var glyph = icon.ToIconString();
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var size = ImGui.CalcTextSize(glyph);
            ImGui.SetCursorScreenPos(center - size * 0.5f);
            using (ImRaii.PushColor(ImGuiCol.Text, hovered ? frameTheme.TextStrong : color))
            {
                Typography.Plain(glyph);
            }
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private bool IsFavorite(uint id) => configuration.MarketFavorites.Contains(id);

    private void ToggleFavorite(uint id)
    {
        var favorites = configuration.MarketFavorites;
        if (!favorites.Remove(id))
        {
            favorites.Add(id);
        }

        configuration.Save();
    }

    private void SetQuality(bool hq)
    {
        showHq = hq;
        configuration.MarketHqOnly = hq;
        configuration.Save();
    }

    private static int CountListings(MarketListing[] listings, bool hq)
    {
        var count = 0;
        for (var index = 0; index < listings.Length; index++)
        {
            if (listings[index].Hq == hq)
            {
                count++;
            }
        }

        return count;
    }

    private static int CountQuality(MarketSale[] sales, bool hq)
    {
        var count = 0;
        for (var index = 0; index < sales.Length; index++)
        {
            if (sales[index].Hq == hq)
            {
                count++;
            }
        }

        return count;
    }

    private static string PriceOrDash(double value) => value > 0 ? MarketFormat.Gil(value) : "-";
    private static string PriceOrDash(long value) => value > 0 ? MarketFormat.Gil(value) : "-";
}
