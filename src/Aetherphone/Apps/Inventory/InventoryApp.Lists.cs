using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Inventory;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Inventory;

internal sealed partial class InventoryApp
{
    private const float IntroGap = 4f;
    private const float GilTile = 32f;

    private void DrawTidy(in PhoneContext context, InventoryView view)
    {
        var scale = UiScale.Current;
        var title = Loc.T(L.Inventory.TidyTitle);
        var navBar = AppHeader.BeginLargeTitle(context);
        var body = navBar.Body;
        using (AppSurface.Begin(body))
        {
            if (catalog.Tidy.Count == 0)
            {
                InventoryArt.StateScreen(body, ui, FontAwesomeIcon.LayerGroup, Loc.T(L.Inventory.TidyEmptyTitle),
                    Loc.T(L.Inventory.TidyEmptyHint), string.Empty);
            }
            else
            {
                DrawIntro(Loc.T(L.Inventory.TidyIntro), scale);
                DrawTidyRows(title, scale);
                ImGui.Dummy(new Vector2(0f, BottomPad * scale));
            }
        }

        AppHeader.EndLargeTitle(in navBar, context, "inventory.tidy.nav", title, NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty, view.BackTitle, back);
    }

    private void DrawIntro(string message, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var inset = SectionHeaderInset * scale;
        var height = Typography.DrawWrappedLeft(new Vector2(origin.X + inset, origin.Y + IntroGap * scale), message,
            ui.MutedInk, TextStyles.Subheadline, MathF.Max(1f, width - inset * 2f));
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + CardGap * scale));
    }

    private void DrawTidyRows(string title, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var tidy = catalog.Tidy;
        var rowHeight = ResultRowHeight(scale);
        var group = BeginGroup(rowHeight * tidy.Count);
        for (var position = 0; position < tidy.Count; position++)
        {
            var row = RowRect(group, position, rowHeight);
            if (!RowVisible(row))
            {
                continue;
            }

            var entry = tidy[position];
            var last = position == tidy.Count - 1;
            if (RowInteract(drawList, row, position == 0, last, true))
            {
                OpenItem(entry.ItemIndex, title);
            }

            DrawItemRow(drawList, row, catalog.Items[entry.ItemIndex], text.TidyDetail[position],
                text.ItemQuantity[entry.ItemIndex], entry.HighQuality, entry.HighQuality, scale);
            if (!last)
            {
                Hairline(drawList, row.Min.X + (RowPad + ResultIcon + ResultIconGap) * scale, row.Max.X, row.Max.Y);
            }
        }

        EndGroup(group);
    }

    private void DrawWealth(in PhoneContext context, InventoryView view)
    {
        var scale = UiScale.Current;
        var title = Loc.T(L.Inventory.WealthTitle);
        var navBar = AppHeader.BeginLargeTitle(context);
        var body = navBar.Body;
        using (AppSurface.Begin(body))
        {
            SectionHeader(Loc.T(L.Inventory.Gil), 0f, scale);
            DrawGilRows(scale);
            DrawGilFootnote(scale);
            SectionHeader(Loc.T(L.Inventory.MostValuable), SectionTopGap, scale);
            DrawValueRows(title, scale);
            ImGui.Dummy(new Vector2(0f, BottomPad * scale));
        }

        AppHeader.EndLargeTitle(in navBar, context, "inventory.wealth.nav", title, NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty, view.BackTitle, back);
    }

    private void DrawGilRows(float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var sources = catalog.Sources;
        var retainerRows = 0;
        for (var index = 0; index < sources.Count; index++)
        {
            if (sources[index].Kind == InventorySourceKind.Retainer && sources[index].Gil >= 0)
            {
                retainerRows++;
            }
        }

        var rowCount = 1 + retainerRows + (retainerRows > 0 ? 1 : 0);
        var rowHeight = RowPad * 2f * scale + MathF.Max(GilTile * scale, Typography.LineHeight(TextStyles.Headline));
        var group = BeginGroup(rowHeight * rowCount);
        var rowIndex = 0;
        DrawGilRow(drawList, RowRect(group, rowIndex, rowHeight), InventoryArt.GoldTint, FontAwesomeIcon.Coins,
            Loc.T(L.Inventory.OnYou), text.GilOnYou, false, rowIndex == rowCount - 1, scale);
        rowIndex++;
        for (var index = 0; index < sources.Count; index++)
        {
            var source = sources[index];
            if (source.Kind != InventorySourceKind.Retainer || source.Gil < 0)
            {
                continue;
            }

            DrawGilRow(drawList, RowRect(group, rowIndex, rowHeight), InventoryArt.AccentFor(source.Kind),
                InventoryArt.IconFor(source.Kind), text.SourceTitles[index], text.RetainerGil[index], false,
                rowIndex == rowCount - 1, scale);
            rowIndex++;
        }

        if (retainerRows > 0)
        {
            DrawGilRow(drawList, RowRect(group, rowIndex, rowHeight), ui.Accent, FontAwesomeIcon.BalanceScale,
                Loc.T(L.Inventory.TotalGil), text.GilTotal, true, true, scale);
        }

        EndGroup(group);
    }

    private void DrawGilRow(ImDrawListPtr drawList, Rect row, Vector4 tint, FontAwesomeIcon icon, string label,
        string value, bool emphasized, bool last, float scale)
    {
        var pad = RowPad * scale;
        var tileSize = GilTile * scale;
        var tileCenter = new Vector2(row.Min.X + pad + tileSize * 0.5f, row.Center.Y);
        InventoryArt.Tile(drawList, tileCenter, tileSize, tint, icon, scale, true);
        var textLeft = tileCenter.X + tileSize * 0.5f + StorageTileGap * scale;
        var right = row.Max.X - pad;
        var valueStyle = emphasized ? TextStyles.Headline : TextStyles.Body;
        var valueSize = Typography.Measure(value, valueStyle);
        Typography.Draw(drawList, new Vector2(right - valueSize.X, row.Center.Y - valueSize.Y * 0.5f), value,
            ui.TitleInk, valueStyle);
        var labelStyle = emphasized ? TextStyles.Headline : TextStyles.Body;
        var fitted = Typography.FitText(label, MathF.Max(1f, right - valueSize.X - TrailingGap * scale - textLeft),
            labelStyle);
        var labelHeight = Typography.Measure(fitted, labelStyle).Y;
        Typography.Draw(drawList, new Vector2(textLeft, row.Center.Y - labelHeight * 0.5f), fitted,
            emphasized ? ui.TitleInk : ui.BodyInk, labelStyle);
        if (!last)
        {
            Hairline(drawList, textLeft, row.Max.X, row.Max.Y);
        }
    }

    private void DrawGilFootnote(float scale)
    {
        var message = catalog.RetainersWithGil > 0 ? text.RetainersUpdated : Loc.T(L.Inventory.RetainerGilUnknown);
        if (message.Length == 0)
        {
            return;
        }

        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var inset = SectionHeaderInset * scale;
        var height = Typography.DrawWrappedLeft(new Vector2(origin.X + inset, origin.Y + StorageLineGap * scale),
            message, ui.MutedInk, TextStyles.Footnote, MathF.Max(1f, width - inset * 2f));
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + StorageLineGap * scale));
    }

    private void DrawValueRows(string title, float scale)
    {
        if (text.ValueCoverage.Length > 0)
        {
            DrawIntro(text.ValueCoverage, scale);
        }

        var ranked = valuation.Ranked;
        if (ranked.Length == 0)
        {
            var message = valuation.MarketableCount == 0
                ? Loc.T(L.Inventory.ValueEmpty)
                : valuation.Pending ? Loc.T(L.Inventory.Pricing) : Loc.T(L.Inventory.NoPrices);
            DrawIntro(message, scale);
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var rowHeight = ResultRowHeight(scale);
        var count = Math.Min(ranked.Length, text.RankedValue.Length);
        var group = BeginGroup(rowHeight * count);
        for (var position = 0; position < count; position++)
        {
            var row = RowRect(group, position, rowHeight);
            if (!RowVisible(row))
            {
                continue;
            }

            var itemIndex = ranked[position];
            var item = catalog.Items[itemIndex];
            var last = position == count - 1;
            if (RowInteract(drawList, row, position == 0, last, true))
            {
                OpenItem(itemIndex, title);
            }

            DrawItemRow(drawList, row, item, text.RankedDetail[position], text.RankedValue[position],
                item.HighQualityQuantity > 0, item.HighQualityQuantity == item.Quantity, scale);
            if (!last)
            {
                Hairline(drawList, row.Min.X + (RowPad + ResultIcon + ResultIconGap) * scale, row.Max.X, row.Max.Y);
            }
        }

        EndGroup(group);
    }
}
