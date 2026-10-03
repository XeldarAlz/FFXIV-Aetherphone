using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Inventory;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Inventory;

internal sealed partial class InventoryApp
{
    private const float DetailIcon = 64f;
    private const float DetailIconGap = 16f;
    private const float PlaceTile = 32f;
    private const float ActionGap = 10f;
    private const float SourceIcon = 36f;
    private const float PageHeaderGap = 18f;
    private const float PageHeaderBottom = 6f;
    private const float SummaryTile = 44f;

    private int detailItemIndex = -1;
    private int detailTextVersion = -1;
    private string detailLowest = string.Empty;
    private string detailUnit = string.Empty;
    private string detailTotal = string.Empty;
    private string detailVendor = string.Empty;
    private string detailHighQuality = string.Empty;

    private void DrawItem(in PhoneContext context, InventoryView view)
    {
        var scale = UiScale.Current;
        var itemIndex = catalog.IndexOfItem(view.ItemId);
        var title = itemIndex >= 0 ? catalog.Items[itemIndex].Info.Name : DisplayName;
        var navBar = AppHeader.BeginLargeTitle(context);
        var body = navBar.Body;
        using (AppSurface.Begin(body))
        {
            if (itemIndex < 0)
            {
                InventoryArt.StateScreen(body, ui, FontAwesomeIcon.BoxOpen, Loc.T(L.Inventory.ItemGone),
                    Loc.T(L.Inventory.ItemGoneHint), string.Empty);
            }
            else
            {
                SyncDetailText(itemIndex);
                var item = catalog.Items[itemIndex];
                var showName = Typography.Measure(item.Info.Name, TextStyles.LargeTitle).X > context.Content.Width;
                DrawItemHero(item, itemIndex, showName, scale);
                SectionHeader(Loc.T(L.Inventory.Where), SectionTopGap, scale);
                DrawPlacements(item, scale);
                SectionHeader(Loc.T(L.Inventory.Value), SectionTopGap, scale);
                DrawValueCard(item, itemIndex, scale);
                DrawItemActions(item, scale);
                ImGui.Dummy(new Vector2(0f, BottomPad * scale));
            }
        }

        AppHeader.EndLargeTitle(in navBar, context, "inventory.item.nav", title, NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty, view.BackTitle, back);
    }

    private void SyncDetailText(int itemIndex)
    {
        if (itemIndex == detailItemIndex && detailTextVersion == text.Version)
        {
            return;
        }

        detailItemIndex = itemIndex;
        detailTextVersion = text.Version;
        var item = catalog.Items[itemIndex];
        var unit = valuation.UnitPrice(itemIndex);
        detailLowest = prices.ScopeName.Length > 0 ? Loc.T(L.Inventory.MarketLowest, prices.ScopeName) : string.Empty;
        detailUnit = unit > 0
            ? Loc.T(L.Inventory.MarketEach, NumberText.Group(unit))
            : valuation.Pending ? Loc.T(L.Inventory.Pricing) : Loc.T(L.Inventory.NoPrices);
        detailTotal = unit > 0 ? NumberText.Group(valuation.Value(itemIndex)) : string.Empty;
        detailVendor = item.Info.VendorPrice > 0
            ? Loc.T(L.Inventory.VendorSells, NumberText.Group(item.Info.VendorPrice))
            : string.Empty;
        detailHighQuality = item.HighQualityQuantity > 0 && item.HighQualityQuantity < item.Quantity
            ? InventoryText.Quantity(item.HighQualityQuantity)
            : string.Empty;
    }

    private void DrawItemHero(InventoryItemEntry item, int itemIndex, bool showName, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = HeroPad * scale;
        var iconSize = DetailIcon * scale;
        var textLeft = origin.X + pad + iconSize + DetailIconGap * scale;
        var textWidth = MathF.Max(1f, origin.X + width - pad - textLeft);
        var nameLines = showName ? Typography.WrapText(item.Info.Name, TextStyles.Headline, textWidth) : null;
        var headlineHeight = Typography.LineHeight(TextStyles.Headline);
        var nameHeight = nameLines is null ? 0f : nameLines.Length * headlineHeight + HeroLineGap * scale;
        var valueHeight = Typography.LineHeight(TextStyles.Title1);
        var placesHeight = Typography.LineHeight(TextStyles.Subheadline);
        var hqHeight = detailHighQuality.Length > 0 ? Typography.LineHeight(TextStyles.Footnote) + HeroLineGap * scale : 0f;
        var block = nameHeight + valueHeight + HeroLineGap * scale + placesHeight + hqHeight;
        var height = pad * 2f + MathF.Max(iconSize, block);
        var max = new Vector2(origin.X + width, origin.Y + height);
        var radius = Metrics.Radius.Widget * scale;
        ui.Card(drawList, origin, max, radius, true);
        Material.TopGlow(drawList, origin, max, radius, ui.Accent, HeroGlowCoverage, HeroGlowStrength);
        var iconMin = new Vector2(origin.X + pad, origin.Y + (height - iconSize) * 0.5f);
        InventoryArt.ItemIcon(drawList, textures, item.Info.IconId, item.HighQualityQuantity == item.Quantity,
            iconMin, iconMin + new Vector2(iconSize, iconSize), scale, ui.TitleInk with { W = ItemWellAlpha });
        var lineY = origin.Y + (height - block) * 0.5f;
        if (nameLines is not null)
        {
            for (var lineIndex = 0; lineIndex < nameLines.Length; lineIndex++)
            {
                Typography.Draw(drawList, new Vector2(textLeft, lineY), nameLines[lineIndex], ui.TitleInk,
                    TextStyles.Headline);
                lineY += headlineHeight;
            }

            lineY += HeroLineGap * scale;
        }

        Typography.Draw(drawList, new Vector2(textLeft, lineY),
            Typography.FitText(text.ItemQuantity[itemIndex], textWidth, TextStyles.Title1), ui.TitleInk,
            TextStyles.Title1);
        lineY += valueHeight + HeroLineGap * scale;
        Typography.Draw(drawList, new Vector2(textLeft, lineY),
            Typography.FitText(text.ItemPlaces[itemIndex], textWidth, TextStyles.Subheadline), ui.MutedInk,
            TextStyles.Subheadline);
        lineY += placesHeight;
        if (detailHighQuality.Length > 0)
        {
            lineY += HeroLineGap * scale;
            var footHeight = Typography.LineHeight(TextStyles.Footnote);
            var hqLabel = Loc.T(L.Common.Hq);
            var badgeWidth = InventoryArt.HqBadge(drawList, new Vector2(textLeft, lineY + footHeight * 0.5f), hqLabel,
                ui.Accent, scale);
            Typography.Draw(drawList, new Vector2(textLeft + badgeWidth + BadgeGap * scale, lineY), detailHighQuality,
                ui.BodyInk, TextStyles.Footnote);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private float PlacementRowHeight(float scale) =>
        RowPad * 2f * scale + MathF.Max(PlaceTile * scale,
            Typography.LineHeight(TextStyles.Headline) + ResultLineGap * scale +
            Typography.LineHeight(TextStyles.Footnote));

    private void DrawPlacements(InventoryItemEntry item, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rowHeight = PlacementRowHeight(scale);
        var placementIndices = item.Placements;
        var group = BeginGroup(rowHeight * placementIndices.Length);
        var hqLabel = Loc.T(L.Common.Hq);
        for (var position = 0; position < placementIndices.Length; position++)
        {
            var row = RowRect(group, position, rowHeight);
            if (!RowVisible(row))
            {
                continue;
            }

            var placementIndex = placementIndices[position];
            var placement = catalog.Placements[placementIndex];
            var source = catalog.Sources[placement.SourceIndex];
            var last = position == placementIndices.Length - 1;
            if (RowInteract(drawList, row, position == 0, last, source.Browsable))
            {
                OpenSource(placement.SourceIndex, item.Info.Name);
            }

            var pad = RowPad * scale;
            var tileSize = PlaceTile * scale;
            var tileCenter = new Vector2(row.Min.X + pad + tileSize * 0.5f, row.Center.Y);
            InventoryArt.Tile(drawList, tileCenter, tileSize, InventoryArt.AccentFor(source.Kind),
                InventoryArt.IconFor(source.Kind), scale, true);
            var textLeft = tileCenter.X + tileSize * 0.5f + StorageTileGap * scale;
            var right = row.Max.X - pad;
            var quantity = text.PlacementQuantity[placementIndex];
            var quantitySize = Typography.Measure(quantity, TextStyles.BodyEmphasized);
            Typography.Draw(drawList, new Vector2(right - quantitySize.X, row.Center.Y - quantitySize.Y * 0.5f),
                quantity, ui.TitleInk, TextStyles.BodyEmphasized);
            var textRight = right - quantitySize.X - TrailingGap * scale;
            if (placement.HighQuality)
            {
                var badgeWidth = InventoryArt.HqBadgeWidth(hqLabel, scale);
                InventoryArt.HqBadge(drawList, new Vector2(textRight - badgeWidth, row.Center.Y), hqLabel, ui.Accent,
                    scale);
                textRight -= badgeWidth + BadgeGap * scale;
            }

            var detail = text.PlacementDetail[placementIndex];
            var headlineHeight = Typography.LineHeight(TextStyles.Headline);
            var block = detail.Length > 0
                ? headlineHeight + ResultLineGap * scale + Typography.LineHeight(TextStyles.Footnote)
                : headlineHeight;
            var top = row.Center.Y - block * 0.5f;
            var textWidth = MathF.Max(1f, textRight - textLeft);
            Typography.Draw(drawList, new Vector2(textLeft, top),
                Typography.FitText(text.SourceTitles[placement.SourceIndex], textWidth, TextStyles.Headline),
                ui.TitleInk, TextStyles.Headline);
            if (detail.Length > 0)
            {
                Typography.Draw(drawList, new Vector2(textLeft, top + headlineHeight + ResultLineGap * scale),
                    Typography.FitText(detail, textWidth, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
            }

            if (!last)
            {
                Hairline(drawList, textLeft, row.Max.X, row.Max.Y);
            }
        }

        EndGroup(group);
    }

    private void DrawValueCard(InventoryItemEntry item, int itemIndex, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = RowPad * scale;
        var innerWidth = MathF.Max(1f, width - pad * 2f);
        var footHeight = Typography.LineHeight(TextStyles.Footnote);
        var headlineHeight = Typography.LineHeight(TextStyles.Headline);
        var subHeight = Typography.LineHeight(TextStyles.Subheadline);
        var marketable = item.Info.Marketable;
        var marketBlock = marketable
            ? (detailLowest.Length > 0 ? footHeight + HeroLineGap * scale : 0f) + headlineHeight
            : subHeight;
        var vendorBlock = detailVendor.Length > 0 ? StorageLineGap * 2f * scale + subHeight : 0f;
        var height = pad * 2f + marketBlock + vendorBlock;
        var max = new Vector2(origin.X + width, origin.Y + height);
        ui.Card(drawList, origin, max, Metrics.Radius.Grouped * scale, true);
        var left = origin.X + pad;
        var right = max.X - pad;
        var lineY = origin.Y + pad;
        if (marketable)
        {
            if (detailLowest.Length > 0)
            {
                Typography.Draw(drawList, new Vector2(left, lineY),
                    Typography.FitText(detailLowest, innerWidth, TextStyles.Footnote), ui.MutedInk,
                    TextStyles.Footnote);
                lineY += footHeight + HeroLineGap * scale;
            }

            var totalWidth = 0f;
            if (detailTotal.Length > 0)
            {
                var totalSize = Typography.Measure(detailTotal, TextStyles.BodyEmphasized);
                totalWidth = totalSize.X + TrailingGap * scale;
                Typography.Draw(drawList, new Vector2(right - totalSize.X, lineY + (headlineHeight - totalSize.Y) * 0.5f),
                    detailTotal, ui.Accent, TextStyles.BodyEmphasized);
            }

            Typography.Draw(drawList, new Vector2(left, lineY),
                Typography.FitText(detailUnit, MathF.Max(1f, innerWidth - totalWidth), TextStyles.Headline),
                valuation.UnitPrice(itemIndex) > 0 ? ui.TitleInk : ui.MutedInk, TextStyles.Headline);
            lineY += headlineHeight;
        }
        else
        {
            Typography.Draw(drawList, new Vector2(left, lineY),
                Typography.FitText(Loc.T(L.Inventory.NotMarketable), innerWidth, TextStyles.Subheadline), ui.MutedInk,
                TextStyles.Subheadline);
            lineY += subHeight;
        }

        if (detailVendor.Length > 0)
        {
            lineY += StorageLineGap * scale;
            Hairline(drawList, left, right, lineY);
            lineY += StorageLineGap * scale;
            Typography.Draw(drawList, new Vector2(left, lineY),
                Typography.FitText(detailVendor, innerWidth, TextStyles.Subheadline), ui.BodyInk,
                TextStyles.Subheadline);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawItemActions(InventoryItemEntry item, float scale)
    {
        ImGui.Dummy(new Vector2(0f, CardGap * scale));
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var height = Metrics.Size.Pill * scale;
        var showMarket = item.Info.Marketable && frameNavigation.IsAvailable(MarketAppId);
        var gap = ActionGap * scale;
        var copyWidth = showMarket ? (width - gap) * 0.5f : width;
        if (showMarket)
        {
            var marketRect = new Rect(origin, new Vector2(origin.X + copyWidth, origin.Y + height));
            if (ui.AccentPill(marketRect, Loc.T(L.Inventory.CheckMarket), true, TextStyles.Headline))
            {
                UiFeedback.Play(UiSound.Tap);
                marketLauncher.RequestItem(item.ItemId);
                frameNavigation.Open(MarketAppId);
            }
        }

        var copyLeft = showMarket ? origin.X + copyWidth + gap : origin.X;
        var copyRect = new Rect(new Vector2(copyLeft, origin.Y), new Vector2(copyLeft + copyWidth, origin.Y + height));
        if (ui.ActionPill(copyRect, Loc.T(L.Inventory.CopyName), true, TextStyles.Headline))
        {
            ImGui.SetClipboardText(item.Info.Name);
            UiFeedback.Play(UiSound.Success);
            ShellToast.Show(Loc.T(L.Common.Copied));
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawSource(in PhoneContext context, InventoryView view)
    {
        var scale = UiScale.Current;
        var sourceIndex = catalog.IndexOfSource(view.Source, view.OwnerId);
        var title = sourceIndex >= 0 ? text.SourceTitles[sourceIndex] : DisplayName;
        var navBar = AppHeader.BeginLargeTitle(context);
        var body = navBar.Body;
        UiAnchors.Report("inventory.source", body);
        using (AppSurface.Begin(body))
        {
            if (sourceIndex < 0)
            {
                InventoryArt.StateScreen(body, ui, FontAwesomeIcon.BoxOpen, Loc.T(L.Inventory.EmptySource),
                    string.Empty, string.Empty);
            }
            else
            {
                DrawSourceSummary(sourceIndex, scale);
                DrawSourcePages(body, sourceIndex, title, scale);
            }
        }

        AppHeader.EndLargeTitle(in navBar, context, "inventory.source.nav", title, NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty, view.BackTitle, back);
    }

    private void DrawSourceSummary(int sourceIndex, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var source = catalog.Sources[sourceIndex];
        var pad = HeroPad * scale;
        var headlineHeight = Typography.LineHeight(TextStyles.Headline);
        var footHeight = Typography.LineHeight(TextStyles.Footnote);
        var status = text.SourceStatus[sourceIndex];
        var block = headlineHeight;
        if (source.HasMeter)
        {
            block += StorageLineGap * scale + InventoryArt.MeterHeight * scale;
        }

        if (status.Length > 0)
        {
            block += StorageLineGap * scale + footHeight;
        }

        var tileSize = SummaryTile * scale;
        var height = pad * 2f + MathF.Max(tileSize, block);
        var max = new Vector2(origin.X + width, origin.Y + height);
        var radius = Metrics.Radius.Widget * scale;
        ui.Card(drawList, origin, max, radius, true);
        Material.TopGlow(drawList, origin, max, radius, InventoryArt.AccentFor(source.Kind), HeroGlowCoverage,
            HeroGlowStrength);
        var tileCenter = new Vector2(origin.X + pad + tileSize * 0.5f, origin.Y + height * 0.5f);
        InventoryArt.Tile(drawList, tileCenter, tileSize, InventoryArt.AccentFor(source.Kind),
            InventoryArt.IconFor(source.Kind), scale, true);
        var textLeft = tileCenter.X + tileSize * 0.5f + DetailIconGap * scale;
        var textWidth = MathF.Max(1f, max.X - pad - textLeft);
        var lineY = origin.Y + (height - block) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, lineY),
            Typography.FitText(text.SourceSlots[sourceIndex], textWidth, TextStyles.Headline), ui.TitleInk,
            TextStyles.Headline);
        lineY += headlineHeight;
        if (source.HasMeter)
        {
            lineY += StorageLineGap * scale;
            InventoryArt.Meter(drawList, new Vector2(textLeft, lineY), textWidth, MeterFill(sourceIndex),
                InventoryArt.AccentFor(source.Kind), ui.TitleInk, frameTheme, scale);
            lineY += InventoryArt.MeterHeight * scale;
        }

        if (status.Length > 0)
        {
            lineY += StorageLineGap * scale;
            Typography.Draw(drawList, new Vector2(textLeft, lineY),
                Typography.FitText(status, textWidth, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawSourcePages(Rect body, int sourceIndex, string title, float scale)
    {
        var placementIndices = catalog.Sources[sourceIndex].Placements;
        if (placementIndices.Length == 0)
        {
            var top = ImGui.GetCursorScreenPos().Y;
            InventoryArt.StateScreen(new Rect(new Vector2(body.Min.X, top), body.Max), ui, FontAwesomeIcon.BoxOpen,
                Loc.T(L.Inventory.EmptySource), string.Empty, string.Empty);
            return;
        }

        var rowHeight = RowPad * 2f * scale + MathF.Max(SourceIcon * scale, Typography.LineHeight(TextStyles.Body));
        var start = 0;
        while (start < placementIndices.Length)
        {
            var page = catalog.Placements[placementIndices[start]].Page;
            var end = start + 1;
            while (end < placementIndices.Length && catalog.Placements[placementIndices[end]].Page == page)
            {
                end++;
            }

            DrawPageRun(placementIndices, start, end, rowHeight, title, scale);
            start = end;
        }

        ImGui.Dummy(new Vector2(0f, BottomPad * scale));
    }

    private void DrawPageRun(int[] placementIndices, int start, int end, float rowHeight, string title, float scale)
    {
        var label = text.PlacementPage[placementIndices[start]];
        ImGui.Dummy(new Vector2(0f, (label.Length > 0 ? PageHeaderGap : CardGap) * scale));
        var drawList = ImGui.GetWindowDrawList();
        if (label.Length > 0)
        {
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var labelHeight = Typography.LineHeight(TextStyles.SubheadlineEmphasized);
            Typography.Draw(drawList, new Vector2(origin.X + SectionHeaderInset * scale, origin.Y),
                Typography.FitText(label, width, TextStyles.SubheadlineEmphasized), ui.MutedInk,
                TextStyles.SubheadlineEmphasized);
            ImGui.Dummy(new Vector2(width, labelHeight + PageHeaderBottom * scale));
        }

        var count = end - start;
        var group = BeginGroup(rowHeight * count);
        var hqLabel = Loc.T(L.Common.Hq);
        for (var position = 0; position < count; position++)
        {
            var row = RowRect(group, position, rowHeight);
            if (!RowVisible(row))
            {
                continue;
            }

            var placementIndex = placementIndices[start + position];
            var placement = catalog.Placements[placementIndex];
            var item = catalog.Items[placement.ItemIndex];
            var last = position == count - 1;
            if (RowInteract(drawList, row, position == 0, last, true))
            {
                OpenItem(placement.ItemIndex, title);
            }

            var pad = RowPad * scale;
            var iconSize = SourceIcon * scale;
            var iconMin = new Vector2(row.Min.X + pad, row.Center.Y - iconSize * 0.5f);
            InventoryArt.ItemIcon(drawList, textures, item.Info.IconId, placement.HighQuality, iconMin,
                iconMin + new Vector2(iconSize, iconSize), scale, ui.TitleInk with { W = ItemWellAlpha });
            var textLeft = iconMin.X + iconSize + ResultIconGap * scale;
            var right = row.Max.X - pad;
            var quantity = text.PlacementQuantity[placementIndex];
            var quantitySize = Typography.Measure(quantity, TextStyles.BodyEmphasized);
            Typography.Draw(drawList, new Vector2(right - quantitySize.X, row.Center.Y - quantitySize.Y * 0.5f),
                quantity, ui.Accent, TextStyles.BodyEmphasized);
            var textRight = right - quantitySize.X - TrailingGap * scale;
            var badgeWidth = placement.HighQuality ? InventoryArt.HqBadgeWidth(hqLabel, scale) + BadgeGap * scale : 0f;
            var name = Typography.FitText(item.Info.Name, MathF.Max(1f, textRight - badgeWidth - textLeft),
                TextStyles.Body);
            var nameSize = Typography.Measure(name, TextStyles.Body);
            Typography.Draw(drawList, new Vector2(textLeft, row.Center.Y - nameSize.Y * 0.5f), name, ui.TitleInk,
                TextStyles.Body);
            if (placement.HighQuality)
            {
                InventoryArt.HqBadge(drawList, new Vector2(textLeft + nameSize.X + BadgeGap * scale, row.Center.Y),
                    hqLabel, ui.Accent, scale);
            }

            if (!last)
            {
                Hairline(drawList, textLeft, row.Max.X, row.Max.Y);
            }
        }

        EndGroup(group);
    }
}
