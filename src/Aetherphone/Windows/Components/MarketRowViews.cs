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
        var deleteMin = deleteCenter - new Vector2(deleteSize * 0.5f, deleteSize * 0.5f);
        var deleteMax = deleteCenter + new Vector2(deleteSize * 0.5f, deleteSize * 0.5f);
        var deleteHovered = UiInteract.Hover(deleteMin, deleteMax);
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
        var textMaxWidth = MathF.Max(1f, dotCenter.X - 8f * scale - textX);
        var nameSize = Typography.Measure(alert.ItemName, TextStyles.Body);
        var nameHovered = ImGui.IsMouseHoveringRect(new Vector2(textX, topY),
            new Vector2(textX + textMaxWidth, topY + nameSize.Y));
        Marquee.DrawLeft("marketrow.alert.name." + alert.ItemName, alert.ItemName, textX, topY, textMaxWidth,
            TextStyles.Body, theme.TextStrong, nameHovered);
        var arrow = alert.Below ? "≤" : "≥";
        var sub =
            $"{arrow} {MarketFormat.Gil(alert.Threshold)} · {alert.ScopeName}{(alert.HqOnly ? $" · {Loc.T(L.Common.Hq)}" : string.Empty)}";
        var subSize = Typography.Measure(sub, 0.82f);
        var subTop = row.Max.Y - 9f * scale - subSize.Y;
        var subHovered = ImGui.IsMouseHoveringRect(new Vector2(textX, subTop),
            new Vector2(textX + textMaxWidth, subTop + subSize.Y));
        Marquee.DrawLeft("marketrow.alert.sub." + alert.ItemName, sub, textX, subTop,
            textMaxWidth, new TextStyle(0.82f, FontWeight.Regular), theme.TextMuted, subHovered);
        if (deleteHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(deleteMin, deleteMax, deleteHovered))
        {
            return MarketRowAction.Delete;
        }

        var rowHitMax = new Vector2(dotCenter.X - 8f * scale, row.Max.Y);
        var rowHovered = UiInteract.Hover(row.Min, rowHitMax);
        if (rowHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(row.Min, rowHitMax, rowHovered))
        {
            return MarketRowAction.Open;
        }

        return MarketRowAction.None;
    }

    public static bool ItemRow(Rect row, MarketItemRef item, long minPrice, ITextureProvider textures, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var hovered = UiInteract.Hover(row.Min, row.Max);
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

        var chevronTip = new Vector2(row.Max.X, row.Center.Y);
        var rightReserve = 6f * scale + 14f * scale;
        if (minPrice > 0)
        {
            var priceText = MarketFormat.Gil(minPrice);
            var priceSize = Typography.Measure(priceText, 0.92f);
            Typography.Draw(new Vector2(chevronTip.X - 14f * scale - priceSize.X, row.Center.Y - priceSize.Y * 0.5f),
                priceText, theme.Accent, 0.92f);
            rightReserve += priceSize.X + 14f * scale;
        }

        var nameLeft = iconMax.X + 12f * scale;
        var nameMaxWidth = MathF.Max(1f, chevronTip.X - rightReserve - nameLeft);
        var nameSize = Typography.Measure(item.Name);
        Marquee.DrawLeft("marketrow.item.name." + item.Name, item.Name, nameLeft, row.Center.Y - nameSize.Y * 0.5f,
            nameMaxWidth, TextStyles.Body, theme.TextStrong, hovered);
        DrawChevronRight(chevronTip, 6f * scale, 2.2f * scale, theme.TextMuted);

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(row.Min, row.Max, hovered);
    }

    public static void ListingRow(Rect row, in MarketListing listing, bool multiWorld, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var topY = row.Min.Y + 9f * scale;
        var unit = MarketFormat.Gil(listing.PricePerUnit);
        Typography.Draw(new Vector2(row.Min.X, topY), unit, theme.TextStrong, 1.05f);
        var unitWidth = Typography.Measure(unit, 1.05f).X;
        var leftReserve = unitWidth;
        if (listing.Hq)
        {
            DrawHqBadge(new Vector2(row.Min.X + unitWidth + 8f * scale, topY), scale);
            leftReserve += 8f * scale + HqBadgeWidth(scale);
        }

        var totalMaxWidth = MathF.Max(1f, row.Width - leftReserve - 10f * scale);
        var total = Typography.FitText(MarketFormat.Gil(listing.Total), totalMaxWidth, 1f, FontWeight.Regular);
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
        var leftReserve = priceWidth;
        if (sale.Hq)
        {
            DrawHqBadge(new Vector2(row.Min.X + priceWidth + 8f * scale, topY), scale);
            leftReserve += 8f * scale + HqBadgeWidth(scale);
        }

        var agoMaxWidth = MathF.Max(1f, row.Width - leftReserve - 10f * scale);
        var ago = Typography.FitText(TimeText.Ago(sale.Time), agoMaxWidth, 0.9f, FontWeight.Regular);
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

    private static float HqBadgeWidth(float scale) =>
        Typography.Measure(Loc.T(L.Common.Hq), 0.72f).X + 10f * scale;

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
