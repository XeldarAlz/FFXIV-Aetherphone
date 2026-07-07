using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Market;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;

namespace Aetherphone.Windows.Components;

internal enum MarketRowAction
{
    None,
    Open,
    Delete,
}

internal static class MarketRowViews
{
    public const float ItemRowHeight = 52f;
    public const float DataRowHeight = 52f;

    public static MarketRowAction AlertRow(Rect row, MarketAlert alert, ITextureProvider textures, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var openRight = row.Max.X - 56f * scale;
        if (ImGui.IsMouseHoveringRect(row.Min, new Vector2(openRight, row.Max.Y)))
        {
            DrawRowHighlight(drawList, new Rect(row.Min, new Vector2(openRight, row.Max.Y)),
                ImGui.IsMouseDown(ImGuiMouseButton.Left), scale);
        }

        var iconSize = 30f * scale;
        var iconMin = new Vector2(row.Min.X, row.Center.Y - iconSize * 0.5f);
        var iconMax = iconMin + new Vector2(iconSize, iconSize);
        if (alert.IconId != 0)
        {
            var texture = textures.GetFromGameIcon(new GameIconLookup(alert.IconId)).GetWrapOrEmpty();
            drawList.AddImageRounded(texture.Handle, iconMin, iconMax, Vector2.Zero, Vector2.One, 0xFFFFFFFFu,
                6f * scale);
        }

        var deleteSize = 22f * scale;
        var deleteCenter = new Vector2(row.Max.X - deleteSize * 0.5f, row.Center.Y);
        var deleteHovered = ImGui.IsMouseHoveringRect(deleteCenter - new Vector2(deleteSize * 0.5f, deleteSize * 0.5f),
            deleteCenter + new Vector2(deleteSize * 0.5f, deleteSize * 0.5f));
        var crossColor = ImGui.GetColorU32(deleteHovered ? theme.Danger : theme.TextMuted);
        var arm = 5f * scale;
        drawList.AddLine(deleteCenter - new Vector2(arm, arm), deleteCenter + new Vector2(arm, arm), crossColor,
            2f * scale);
        drawList.AddLine(deleteCenter + new Vector2(-arm, arm), deleteCenter + new Vector2(arm, -arm), crossColor,
            2f * scale);
        var dotCenter = new Vector2(deleteCenter.X - deleteSize - 4f * scale, row.Center.Y);
        var dotColor = !alert.Enabled ? theme.TextMuted :
            alert.Triggered ? theme.Accent : new Vector4(0.30f, 0.78f, 0.42f, 1f);
        drawList.AddCircleFilled(dotCenter, 4f * scale, ImGui.GetColorU32(dotColor), 16);
        var textX = iconMax.X + 12f * scale;
        var topY = row.Min.Y + 9f * scale;
        Typography.Draw(new Vector2(textX, topY), MarketFormat.Clip(alert.ItemName, 18), theme.TextStrong);
        var arrow = alert.Below ? "≤" : "≥";
        var sub =
            $"{arrow} {MarketFormat.Gil(alert.Threshold)} · {alert.ScopeName}{(alert.HqOnly ? $" · {Loc.T(L.Common.Hq)}" : string.Empty)}";
        var subSize = Typography.Measure(sub, 0.82f);
        Typography.Draw(new Vector2(textX, row.Max.Y - 9f * scale - subSize.Y), sub, theme.TextMuted, 0.82f);
        if (deleteHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            return ImGui.IsMouseClicked(ImGuiMouseButton.Left) ? MarketRowAction.Delete : MarketRowAction.None;
        }

        var rowHovered = ImGui.IsMouseHoveringRect(row.Min, new Vector2(dotCenter.X - 8f * scale, row.Max.Y));
        if (rowHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                return MarketRowAction.Open;
            }
        }

        return MarketRowAction.None;
    }

    public static bool ItemRow(Rect row, MarketItemRef item, long minPrice, ITextureProvider textures, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var hovered = ImGui.IsMouseHoveringRect(row.Min, row.Max);
        var drawList = ImGui.GetWindowDrawList();
        if (hovered)
        {
            DrawRowHighlight(drawList, row, ImGui.IsMouseDown(ImGuiMouseButton.Left), scale);
        }

        var iconSize = 30f * scale;
        var iconMin = new Vector2(row.Min.X, row.Center.Y - iconSize * 0.5f);
        var iconMax = iconMin + new Vector2(iconSize, iconSize);
        if (item.IconId != 0)
        {
            var texture = textures.GetFromGameIcon(new GameIconLookup(item.IconId)).GetWrapOrEmpty();
            drawList.AddImageRounded(texture.Handle, iconMin, iconMax, Vector2.Zero, Vector2.One, 0xFFFFFFFFu,
                6f * scale);
        }

        var nameMaxLength = minPrice > 0 ? 20 : 28;
        var name = MarketFormat.Clip(item.Name, nameMaxLength);
        var nameSize = Typography.Measure(name);
        Typography.Draw(new Vector2(iconMax.X + 12f * scale, row.Center.Y - nameSize.Y * 0.5f), name, theme.TextStrong);
        var chevronTip = new Vector2(row.Max.X, row.Center.Y);
        DrawChevronRight(chevronTip, 6f * scale, 2.2f * scale, theme.TextMuted);
        if (minPrice > 0)
        {
            var priceText = MarketFormat.Gil(minPrice);
            var priceSize = Typography.Measure(priceText, 0.92f);
            Typography.Draw(new Vector2(chevronTip.X - 14f * scale - priceSize.X, row.Center.Y - priceSize.Y * 0.5f),
                priceText, theme.Accent, 0.92f);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    public static void ListingRow(Rect row, in MarketListing listing, bool multiWorld, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var topY = row.Min.Y + 9f * scale;
        var unit = MarketFormat.Gil(listing.PricePerUnit);
        Typography.Draw(new Vector2(row.Min.X, topY), unit, theme.TextStrong, 1.05f);
        var unitWidth = Typography.Measure(unit, 1.05f).X;
        if (listing.Hq)
        {
            DrawHqBadge(new Vector2(row.Min.X + unitWidth + 8f * scale, topY), scale);
        }

        var total = MarketFormat.Gil(listing.Total);
        var totalSize = Typography.Measure(total);
        Typography.Draw(new Vector2(row.Max.X - totalSize.X, topY + 1f * scale), total, theme.Accent);
        var sub = BuildSub(listing.Quantity, multiWorld ? listing.World : string.Empty, listing.Retainer);
        var subSize = Typography.Measure(sub, 0.82f);
        Typography.Draw(new Vector2(row.Min.X, row.Max.Y - 9f * scale - subSize.Y), sub, theme.TextMuted, 0.82f);
    }

    public static void SaleRow(Rect row, in MarketSale sale, bool multiWorld, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var topY = row.Min.Y + 9f * scale;
        var price = MarketFormat.Gil(sale.PricePerUnit);
        Typography.Draw(new Vector2(row.Min.X, topY), price, theme.TextStrong, 1.05f);
        var priceWidth = Typography.Measure(price, 1.05f).X;
        if (sale.Hq)
        {
            DrawHqBadge(new Vector2(row.Min.X + priceWidth + 8f * scale, topY), scale);
        }

        var ago = TimeText.Ago(sale.Time);
        var agoSize = Typography.Measure(ago, 0.9f);
        Typography.Draw(new Vector2(row.Max.X - agoSize.X, topY + 1f * scale), ago, theme.TextMuted, 0.9f);
        var sub = BuildSub(sale.Quantity, multiWorld ? sale.World : string.Empty, sale.Buyer);
        var subSize = Typography.Measure(sub, 0.82f);
        Typography.Draw(new Vector2(row.Min.X, row.Max.Y - 9f * scale - subSize.Y), sub, theme.TextMuted, 0.82f);
    }

    private static readonly Dictionary<(string, int, string, string), string> SubCache = new();

    private static string BuildSub(int quantity, string world, string detail)
    {
        var key = (Loc.Current.Code, quantity, world, detail);
        if (SubCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var result = Loc.T(L.Market.Quantity, quantity);
        if (world.Length > 0)
        {
            result += $" · {world}";
        }

        if (detail.Length > 0)
        {
            result += $" · {MarketFormat.Clip(detail, 16)}";
        }

        SubCache[key] = result;
        return result;
    }

    private static void DrawHqBadge(Vector2 position, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hq = Loc.T(L.Common.Hq);
        var size = Typography.Measure(hq, 0.72f);
        var padX = 5f * scale;
        var padY = 2f * scale;
        var min = new Vector2(position.X, position.Y + 2f * scale);
        var max = new Vector2(min.X + size.X + padX * 2f, min.Y + size.Y + padY * 2f);
        var tint = new Vector4(0.96f, 0.78f, 0.32f, 1f);
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(tint), 4f * scale);
        Typography.Draw(new Vector2(min.X + padX, min.Y + padY), hq, new Vector4(0.1f, 0.08f, 0.02f, 1f), 0.72f);
    }

    private static void DrawChevronRight(Vector2 tip, float size, float thickness, Vector4 color)
    {
        var drawList = ImGui.GetWindowDrawList();
        var packed = ImGui.GetColorU32(color);
        drawList.AddLine(new Vector2(tip.X - size, tip.Y - size), tip, packed, thickness);
        drawList.AddLine(tip, new Vector2(tip.X - size, tip.Y + size), packed, thickness);
    }

    private static void DrawRowHighlight(ImDrawListPtr drawList, Rect row, bool pressed, float scale)
    {
        var min = new Vector2(row.Min.X - 8f * scale, row.Min.Y + 2f * scale);
        var max = new Vector2(row.Max.X + 8f * scale, row.Max.Y - 2f * scale);
        var alpha = pressed ? 0.10f : 0.05f;
        Squircle.Fill(drawList, min, max, 9f * scale, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha)));
    }
}
