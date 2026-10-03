using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Inventory;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Inventory;

internal sealed partial class InventoryApp
{
    private const int SearchMaxLength = 64;
    private const float SearchBottomGap = 14f;
    private const float HeroPad = 18f;
    private const float HeroTile = 44f;
    private const float HeroTileGap = 14f;
    private const float HeroLineGap = 2f;
    private const float HeroDividerGap = 14f;
    private const float HeroValueTile = 28f;
    private const float HeroGlowCoverage = 0.78f;
    private const float HeroGlowStrength = 0.10f;
    private const float ValueShare = 0.6f;
    private const float StorageTile = 40f;
    private const float StorageTileGap = 12f;
    private const float StorageLineGap = 6f;
    private const float ChevronSlot = 18f;
    private const float TrailingGap = 10f;
    private const float TidyTile = 40f;
    private const float ResultIcon = 40f;
    private const float ResultIconGap = 12f;
    private const float ResultLineGap = 2f;
    private const float BadgeGap = 6f;
    private const float ItemWellAlpha = 0.06f;
    private const int SkeletonRows = 4;
    private const string HeroId = "inventory.hero";
    private const string TidyId = "inventory.tidy";

    private void DrawRoot(in PhoneContext context)
    {
        var scale = UiScale.Current;
        var navBar = AppHeader.BeginLargeTitle(context, false);
        var body = navBar.Body;
        using (var surface = AppSurface.Begin(body))
        {
            if (resetScroll)
            {
                surface.JumpToTop();
                resetScroll = false;
            }

            DrawSearchField(scale);
            if (lowerNeedle.Length > 0)
            {
                DrawResults(body, scale);
            }
            else if (!catalog.HasLocal)
            {
                DrawHomeSkeleton(scale);
            }
            else
            {
                DrawHome(scale);
            }
        }

        AppHeader.EndLargeTitle(in navBar, context, "inventory.nav", DisplayName, NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty);
    }

    private void DrawSearchField(float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var field = new Rect(origin, new Vector2(origin.X + width, origin.Y + GlassField.HeightUnits * scale));
        UiAnchors.Report("inventory.search", field);
        Material.ThemedGlass(drawList, field.Min, field.Max, GlassField.Radius(field), scale, frameTheme);
        GlassField.Search(drawList, field, "##inventorySearch", Loc.T(L.Inventory.Search), ref query, frameTheme,
            scale, SearchMaxLength, false);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, field.Height + SearchBottomGap * scale));
    }

    private void DrawHome(float scale)
    {
        DrawWealthCard(scale);
        SectionHeader(Loc.T(L.Inventory.Storage), SectionTopGap, scale);
        DrawStorage(scale);
        if (catalog.TidySlotsSaved > 0)
        {
            ImGui.Dummy(new Vector2(0f, CardGap * scale));
            DrawTidyCard(scale);
        }

        DrawFootnote(Loc.T(L.Inventory.StaleNote), scale);
    }

    private void DrawWealthCard(float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = HeroPad * scale;
        var eyebrowHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var valueHeight = Typography.LineHeight(TextStyles.Title1);
        var footHeight = Typography.LineHeight(TextStyles.Footnote);
        var topBlock = MathF.Max(HeroTile * scale, eyebrowHeight + valueHeight + footHeight + HeroLineGap * 2f * scale);
        var bottomRow = HeroValueTile * scale;
        var height = pad + topBlock + HeroDividerGap * 2f * scale + bottomRow + pad;
        var rest = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        UiAnchors.Report("inventory.wealth", rest);
        var hovered = UiInteract.Hover(rest.Min, rest.Max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = PressFx.Scale(HeroId, pressed, Motion.PressScaleCard);
        var center = rest.Center;
        var half = new Vector2(width, height) * 0.5f * press;
        var radius = Metrics.Radius.Widget * scale;
        ui.Card(drawList, center - half, center + half, radius * press, true);
        Material.TopGlow(drawList, center - half, center + half, radius, ui.Accent, HeroGlowCoverage, HeroGlowStrength);

        var left = rest.Min.X + pad;
        var right = rest.Max.X - pad;
        var tileCenter = new Vector2(left + HeroTile * 0.5f * scale, rest.Min.Y + pad + topBlock * 0.5f);
        InventoryArt.Tile(drawList, tileCenter, HeroTile * scale, InventoryArt.GoldTint, FontAwesomeIcon.Coins, scale,
            true);
        var textLeft = left + (HeroTile + HeroTileGap) * scale;
        var textWidth = MathF.Max(1f, right - textLeft);
        var lineY = rest.Min.Y + pad + (topBlock - (eyebrowHeight + valueHeight + footHeight +
                                                    HeroLineGap * 2f * scale)) * 0.5f;
        var eyebrow = Typography.FitText(text.GilEyebrow, textWidth, TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, lineY), eyebrow, ui.MutedInk, TextStyles.FootnoteEmphasized);
        lineY += eyebrowHeight + HeroLineGap * scale;
        Typography.Draw(drawList, new Vector2(textLeft, lineY),
            Typography.FitText(text.GilTotal, textWidth, TextStyles.Title1), ui.TitleInk, TextStyles.Title1);
        lineY += valueHeight + HeroLineGap * scale;
        Typography.Draw(drawList, new Vector2(textLeft, lineY),
            Typography.FitText(text.GilBreakdown, textWidth, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);

        var dividerY = rest.Min.Y + pad + topBlock + HeroDividerGap * scale;
        Hairline(drawList, left, right, dividerY);
        var rowCenterY = dividerY + HeroDividerGap * scale + bottomRow * 0.5f;
        InventoryArt.Tile(drawList, new Vector2(left + HeroValueTile * 0.5f * scale, rowCenterY),
            HeroValueTile * scale, ui.Accent, FontAwesomeIcon.Store, scale, true);
        var chevronTip = new Vector2(right, rowCenterY);
        InventoryArt.Chevron(drawList, chevronTip, hovered ? ui.Accent : ui.MutedInk, scale);
        var valueRight = right - ChevronSlot * scale;
        var labelLeft = left + (HeroValueTile + StorageTileGap) * scale;
        var value = Typography.FitText(text.ValueTotal, MathF.Max(1f, (valueRight - labelLeft) * ValueShare),
            TextStyles.BodyEmphasized);
        var valueSize = Typography.Measure(value, TextStyles.BodyEmphasized);
        Typography.Draw(drawList, new Vector2(valueRight - valueSize.X, rowCenterY - valueSize.Y * 0.5f),
            value, valuation.PricedCount > 0 ? ui.TitleInk : ui.MutedInk, TextStyles.BodyEmphasized);
        var label = Typography.FitText(Loc.T(L.Inventory.MarketValue),
            MathF.Max(1f, valueRight - valueSize.X - TrailingGap * scale - labelLeft), TextStyles.Subheadline);
        var labelHeight = Typography.Measure(label, TextStyles.Subheadline).Y;
        Typography.Draw(drawList, new Vector2(labelLeft, rowCenterY - labelHeight * 0.5f), label, ui.BodyInk,
            TextStyles.Subheadline);

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(rest.Min, rest.Max, hovered))
        {
            UiFeedback.Play(UiSound.Tap);
            router.Push(InventoryView.ForWealth(DisplayName));
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private float StorageRowHeight(float scale) =>
        RowPad * 2f * scale + Typography.LineHeight(TextStyles.Headline) + StorageLineGap * scale +
        InventoryArt.MeterHeight * scale + StorageLineGap * scale + Typography.LineHeight(TextStyles.Footnote);

    private void DrawStorage(float scale)
    {
        var sources = catalog.Sources;
        var hasRetainer = false;
        var hasFreeCompany = false;
        for (var index = 0; index < sources.Count; index++)
        {
            hasRetainer |= sources[index].Kind == InventorySourceKind.Retainer;
            hasFreeCompany |= sources[index].Kind == InventorySourceKind.FreeCompany;
        }

        var showRetainerHint = !hasRetainer;
        var showCompanyHint = !hasFreeCompany && inFreeCompany;
        var rowCount = sources.Count + (showRetainerHint ? 1 : 0) + (showCompanyHint ? 1 : 0);
        if (rowCount == 0)
        {
            return;
        }

        var rowHeight = StorageRowHeight(scale);
        var drawList = ImGui.GetWindowDrawList();
        var group = BeginGroup(rowHeight * rowCount);
        var rowIndex = 0;
        for (var sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
        {
            var row = RowRect(group, rowIndex, rowHeight);
            if (sourceIndex == 0)
            {
                UiAnchors.Report("inventory.storage", row);
            }

            var fill = MeterFill(sourceIndex);
            if (RowVisible(row))
            {
                DrawStorageRow(drawList, sourceIndex, row, fill, rowIndex == 0, rowIndex == rowCount - 1, scale);
            }

            rowIndex++;
        }

        if (showRetainerHint)
        {
            DrawPlaceholderRow(drawList, RowRect(group, rowIndex, rowHeight), InventorySourceKind.Retainer,
                Loc.T(L.Inventory.SourceRetainer), rowIndex == rowCount - 1, scale);
            rowIndex++;
        }

        if (showCompanyHint)
        {
            DrawPlaceholderRow(drawList, RowRect(group, rowIndex, rowHeight), InventorySourceKind.FreeCompany,
                Loc.T(L.Inventory.SourceFreeCompany), true, scale);
        }

        EndGroup(group);
    }

    private static Rect RowRect(Rect group, int rowIndex, float rowHeight)
    {
        var top = group.Min.Y + rowIndex * rowHeight;
        return new Rect(new Vector2(group.Min.X, top), new Vector2(group.Max.X, top + rowHeight));
    }

    private void DrawStorageRow(ImDrawListPtr drawList, int sourceIndex, Rect row, float fill, bool first, bool last,
        float scale)
    {
        var source = catalog.Sources[sourceIndex];
        if (RowInteract(drawList, row, first, last, source.Browsable))
        {
            OpenSource(sourceIndex, DisplayName);
        }

        var pad = RowPad * scale;
        var tileSize = StorageTile * scale;
        var tileCenter = new Vector2(row.Min.X + pad + tileSize * 0.5f, row.Center.Y);
        InventoryArt.Tile(drawList, tileCenter, tileSize, InventoryArt.AccentFor(source.Kind),
            InventoryArt.IconFor(source.Kind), scale, source.Browsable);
        var textLeft = tileCenter.X + tileSize * 0.5f + StorageTileGap * scale;
        var right = row.Max.X - pad;
        if (source.Browsable)
        {
            InventoryArt.Chevron(drawList, new Vector2(right, row.Center.Y), ui.MutedInk, scale);
        }

        var contentRight = right - ChevronSlot * scale;
        var titleY = row.Min.Y + pad;
        var usage = text.SourceUsage[sourceIndex];
        var usageSize = Typography.Measure(usage, TextStyles.Footnote);
        var headlineHeight = Typography.LineHeight(TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(contentRight - usageSize.X, titleY + (headlineHeight - usageSize.Y) * 0.5f),
            usage, ui.MutedInk, TextStyles.Footnote);
        var title = Typography.FitText(text.SourceTitles[sourceIndex],
            MathF.Max(1f, contentRight - usageSize.X - TrailingGap * scale - textLeft), TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(textLeft, titleY), title, ui.TitleInk, TextStyles.Headline);

        var lineY = titleY + headlineHeight + StorageLineGap * scale;
        var lineWidth = MathF.Max(1f, contentRight - textLeft);
        if (source.HasMeter)
        {
            InventoryArt.Meter(drawList, new Vector2(textLeft, lineY), lineWidth, fill,
                InventoryArt.AccentFor(source.Kind), ui.TitleInk, frameTheme, scale);
        }

        var status = text.SourceStatus[sourceIndex];
        if (status.Length == 0 && !source.HasMeter)
        {
            status = text.SourceSlots[sourceIndex];
        }

        if (status.Length > 0)
        {
            var statusY = lineY + InventoryArt.MeterHeight * scale + StorageLineGap * scale;
            Typography.Draw(drawList, new Vector2(textLeft, statusY),
                Typography.FitText(status, lineWidth, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
        }

        if (!last)
        {
            Hairline(drawList, textLeft, row.Max.X, row.Max.Y);
        }
    }

    private void DrawPlaceholderRow(ImDrawListPtr drawList, Rect row, InventorySourceKind kind, string title,
        bool last, float scale)
    {
        var pad = RowPad * scale;
        var tileSize = StorageTile * scale;
        var tileCenter = new Vector2(row.Min.X + pad + tileSize * 0.5f, row.Center.Y);
        InventoryArt.Tile(drawList, tileCenter, tileSize, InventoryArt.AccentFor(kind), InventoryArt.IconFor(kind),
            scale, false);
        var textLeft = tileCenter.X + tileSize * 0.5f + StorageTileGap * scale;
        var width = MathF.Max(1f, row.Max.X - pad - textLeft);
        var headlineHeight = Typography.LineHeight(TextStyles.Headline);
        var footHeight = Typography.LineHeight(TextStyles.Footnote);
        var top = row.Center.Y - (headlineHeight + StorageLineGap * scale + footHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, top), Typography.FitText(title, width, TextStyles.Headline),
            ui.BodyInk, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(textLeft, top + headlineHeight + StorageLineGap * scale),
            Typography.FitText(Loc.T(L.Inventory.NotOpened), width, TextStyles.Footnote), ui.MutedInk,
            TextStyles.Footnote);
        if (!last)
        {
            Hairline(drawList, textLeft, row.Max.X, row.Max.Y);
        }
    }

    private void DrawTidyCard(float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = RowPad * scale;
        var headlineHeight = Typography.LineHeight(TextStyles.Headline);
        var footHeight = Typography.LineHeight(TextStyles.Footnote);
        var height = MathF.Max(TidyTile * scale, headlineHeight + footHeight + HeroLineGap * scale) + pad * 2f;
        var rest = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        UiAnchors.Report("inventory.tidy", rest);
        var hovered = UiInteract.Hover(rest.Min, rest.Max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = PressFx.Scale(TidyId, pressed, Motion.PressScaleCard);
        var half = new Vector2(width, height) * 0.5f * press;
        var radius = Metrics.Radius.Widget * scale;
        ui.Card(drawList, rest.Center - half, rest.Center + half, radius * press, true);
        var tileCenter = new Vector2(rest.Min.X + pad + TidyTile * 0.5f * scale, rest.Center.Y);
        InventoryArt.Tile(drawList, tileCenter, TidyTile * scale, ui.Accent, FontAwesomeIcon.LayerGroup, scale, true);
        var textLeft = tileCenter.X + TidyTile * 0.5f * scale + StorageTileGap * scale;
        var right = rest.Max.X - pad;
        InventoryArt.Chevron(drawList, new Vector2(right, rest.Center.Y), hovered ? ui.Accent : ui.MutedInk, scale);
        var textWidth = MathF.Max(1f, right - ChevronSlot * scale - textLeft);
        var top = rest.Center.Y - (headlineHeight + footHeight + HeroLineGap * scale) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(Loc.T(L.Inventory.TidyTitle), textWidth, TextStyles.Headline), ui.TitleInk,
            TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(textLeft, top + headlineHeight + HeroLineGap * scale),
            Typography.FitText(text.TidySummary, textWidth, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(rest.Min, rest.Max, hovered))
        {
            UiFeedback.Play(UiSound.Tap);
            router.Push(InventoryView.ForTidy(DisplayName));
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawHomeSkeleton(float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var width = ScrollLayout.StableContentWidth();
        var ink = InventoryArt.SkeletonInk((float)ImGui.GetTime());
        var origin = ImGui.GetCursorScreenPos();
        var heroHeight = (HeroPad * 2f + HeroTile + HeroDividerGap * 2f + HeroValueTile) * scale +
                         Typography.LineHeight(TextStyles.Title1);
        ui.Card(drawList, origin, new Vector2(origin.X + width, origin.Y + heroHeight), Metrics.Radius.Widget * scale,
            true);
        ImGui.Dummy(new Vector2(width, heroHeight));
        ImGui.Dummy(new Vector2(0f, SectionTopGap * scale));
        var rowHeight = StorageRowHeight(scale);
        var group = BeginGroup(rowHeight * SkeletonRows);
        var pad = RowPad * scale;
        var tileSize = StorageTile * scale;
        for (var rowIndex = 0; rowIndex < SkeletonRows; rowIndex++)
        {
            var row = RowRect(group, rowIndex, rowHeight);
            var tileMin = new Vector2(row.Min.X + pad, row.Center.Y - tileSize * 0.5f);
            Squircle.Fill(drawList, tileMin, tileMin + new Vector2(tileSize, tileSize),
                tileSize * Metrics.Radius.TileFactor, ink);
            var textLeft = tileMin.X + tileSize + StorageTileGap * scale;
            var barHeight = Typography.LineHeight(TextStyles.Footnote) * 0.6f;
            drawList.AddRectFilled(new Vector2(textLeft, row.Min.Y + pad),
                new Vector2(textLeft + (row.Max.X - textLeft) * 0.4f, row.Min.Y + pad + barHeight), ink,
                barHeight * 0.5f);
            var meterY = row.Center.Y;
            drawList.AddRectFilled(new Vector2(textLeft, meterY),
                new Vector2(row.Max.X - pad, meterY + InventoryArt.MeterHeight * scale), ink,
                InventoryArt.MeterHeight * 0.5f * scale);
        }

        EndGroup(group);
    }

    private void DrawResults(Rect body, float scale)
    {
        if (results.Count == 0)
        {
            var top = ImGui.GetCursorScreenPos().Y;
            InventoryArt.StateScreen(new Rect(new Vector2(body.Min.X, top), body.Max), ui, FontAwesomeIcon.Search,
                Loc.T(L.Inventory.NoMatches), Loc.T(L.Inventory.NoMatchesHint), string.Empty);
            return;
        }

        var appear = resultsAppear.Step(1f, Motion.Appear, deltaSeconds);
        using (ImRaii.PushStyle(ImGuiStyleVar.Alpha, MathF.Max(0.05f, appear)))
        {
            DrawItemList(results, ResultRowHeight(scale), scale);
        }

        ImGui.Dummy(new Vector2(0f, BottomPad * scale));
    }

    private float ResultRowHeight(float scale) =>
        MathF.Max(ResultIcon * scale, Typography.LineHeight(TextStyles.Headline) + ResultLineGap * scale +
                                      Typography.LineHeight(TextStyles.Subheadline)) + RowPad * 2f * scale;

    private void DrawItemList(List<int> itemIndices, float rowHeight, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var group = BeginGroup(rowHeight * itemIndices.Count);
        for (var position = 0; position < itemIndices.Count; position++)
        {
            var row = RowRect(group, position, rowHeight);
            if (!RowVisible(row))
            {
                continue;
            }

            var itemIndex = itemIndices[position];
            var first = position == 0;
            var last = position == itemIndices.Count - 1;
            if (RowInteract(drawList, row, first, last, true))
            {
                OpenItem(itemIndex, DisplayName);
            }

            var item = catalog.Items[itemIndex];
            DrawItemRow(drawList, row, item, text.ItemBreadcrumb[itemIndex], text.ItemQuantity[itemIndex],
                item.HighQualityQuantity > 0, item.HighQualityQuantity == item.Quantity, scale);
            if (!last)
            {
                Hairline(drawList, row.Min.X + (RowPad + ResultIcon + ResultIconGap) * scale, row.Max.X, row.Max.Y);
            }
        }

        EndGroup(group);
    }

    private void DrawItemRow(ImDrawListPtr drawList, Rect row, InventoryItemEntry item, string detail,
        string quantity, bool showHq, bool allHq, float scale)
    {
        var pad = RowPad * scale;
        var iconSize = ResultIcon * scale;
        var iconMin = new Vector2(row.Min.X + pad, row.Center.Y - iconSize * 0.5f);
        InventoryArt.ItemIcon(drawList, textures, item.Info.IconId, allHq, iconMin,
            iconMin + new Vector2(iconSize, iconSize), scale, ui.TitleInk with { W = ItemWellAlpha });
        var textLeft = iconMin.X + iconSize + ResultIconGap * scale;
        var right = row.Max.X - pad;
        var quantitySize = Typography.Measure(quantity, TextStyles.BodyEmphasized);
        Typography.Draw(drawList, new Vector2(right - quantitySize.X, row.Center.Y - quantitySize.Y * 0.5f), quantity,
            ui.Accent, TextStyles.BodyEmphasized);
        var textRight = right - quantitySize.X - TrailingGap * scale;
        var headlineHeight = Typography.LineHeight(TextStyles.Headline);
        var hasDetail = detail.Length > 0;
        var blockHeight = hasDetail
            ? headlineHeight + ResultLineGap * scale + Typography.LineHeight(TextStyles.Subheadline)
            : headlineHeight;
        var top = row.Center.Y - blockHeight * 0.5f;
        var nameRight = textRight;
        var hqLabel = Loc.T(L.Common.Hq);
        var badgeWidth = showHq
            ? InventoryArt.HqBadgeWidth(hqLabel, scale) + BadgeGap * scale
            : 0f;
        var name = Typography.FitText(item.Info.Name, MathF.Max(1f, nameRight - badgeWidth - textLeft),
            TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(textLeft, top), name, ui.TitleInk, TextStyles.Headline);
        if (showHq)
        {
            var nameWidth = Typography.Measure(name, TextStyles.Headline).X;
            InventoryArt.HqBadge(drawList, new Vector2(textLeft + nameWidth + BadgeGap * scale,
                top + headlineHeight * 0.5f), hqLabel, ui.Accent, scale);
        }

        if (hasDetail)
        {
            Typography.Draw(drawList, new Vector2(textLeft, top + headlineHeight + ResultLineGap * scale),
                Typography.FitText(detail, MathF.Max(1f, textRight - textLeft), TextStyles.Subheadline), ui.MutedInk,
                TextStyles.Subheadline);
        }
    }
}
