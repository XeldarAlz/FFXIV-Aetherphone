using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Coins;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Coin;

internal sealed partial class CoinApp
{
    private const string FlairKind = "flair";
    private const string FrameKind = "frame";
    private const string PlainKind = "";

    private const float TileHeight = 136f;
    private const float TileGap = 12f;
    private const float TileIconFraction = 0.34f;
    private const float TileImageFraction = 0.52f;
    private const float SkuCardGap = 12f;
    private const float FlairCardHeight = 200f;
    private const float PlainCardHeight = 112f;
    private const float FrameCellHeight = 260f;
    private const float FrameCellStageHeight = 150f;
    private const float FrameCellButtonHeight = 34f;
    private const float StageHeight = 92f;
    private const float StagePadding = 20f;
    private const float GlyphFraction = 0.72f;
    private const float GlyphGapFraction = 0.34f;
    private const float PreviewMaxScale = 1.90f;
    private const float PreviewMinScale = 1.00f;
    private const float BloomAlpha = 0.14f;
    private const float ButtonHeight = 44f;
    private const long FallbackCategoryIcon = 0xF07A;

    private void DrawShop(Rect body)
    {
        var scale = UiScale.Current;
        catalog.EnsureFresh();
        using var surface = AppSurface.Begin(body);
        shopRefresh.Draw(body, surface.Pull, surface.Dragging, catalog.Fetching, ui.MutedInk, RefreshShop);
        ConsumePurchaseResult();

        var categories = catalog.Categories;
        if (categories.Length == 0)
        {
            if (!catalog.LoadedOnce)
            {
                LoadingPulse.Draw(body.Center, 16f * scale, ui.Palette.Accent, ui.MutedInk, LoadingPulse.SafeLabel());
            }
            else
            {
                EmptyState.Draw(body, ui, FontAwesomeIcon.Store, Loc.T(L.Coin.ShopEmpty),
                    Loc.T(L.Coin.ShopEmptyHint));
            }

            return;
        }

        var width = ScrollLayout.StableContentWidth();
        var tilesOrigin = ImGui.GetCursorScreenPos();
        DrawCategoryTiles(string.Empty, width, scale);
        var tilesBottom = MathF.Min(ImGui.GetCursorScreenPos().Y, body.Max.Y);
        if (tilesBottom > tilesOrigin.Y)
        {
            UiAnchors.Report("coin.shop",
                new Rect(tilesOrigin, new Vector2(tilesOrigin.X + width, tilesBottom)));
        }

        ImGui.Dummy(new Vector2(0f, 16f * scale));
    }

    private void DrawShopBrowse(CoinRoute route, Rect area)
    {
        var scale = UiScale.Current;
        var category = catalog.Category(route.CategoryId);
        AppHeader.Draw(new PhoneContext(area, theme, navigation), CategoryTitle(category), LeaveBrowse);

        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale + 6f * scale), area.Max);
        using var surface = AppSurface.Begin(body);
        browseRefresh.Draw(body, surface.Pull, surface.Dragging, catalog.Fetching, ui.MutedInk, RefreshShop);
        ConsumePurchaseResult();

        var width = ScrollLayout.StableContentWidth();
        if (route.Screen == CoinScreen.ShopFolder)
        {
            DrawCategoryTiles(route.CategoryId, width, scale);
            ImGui.Dummy(new Vector2(0f, 16f * scale));
            return;
        }

        catalog.EnsureShelf(route.CategoryId);
        var items = catalog.Shelf(route.CategoryId);
        if (items.Length == 0)
        {
            if (catalog.ShelfLoaded(route.CategoryId) || catalog.ItemsComplete)
            {
                EmptyState.Draw(body, ui, FontAwesomeIcon.Store, Loc.T(L.Coin.ShopShelfEmpty),
                    Loc.T(L.Coin.ShopEmptyHint));
            }
            else
            {
                LoadingPulse.Draw(body.Center, 16f * scale, ui.Palette.Accent, ui.MutedInk, LoadingPulse.SafeLabel());
            }

            return;
        }

        DrawShelfItems(items, width, scale);
        ImGui.Dummy(new Vector2(0f, 16f * scale));
    }

    private void LeaveBrowse()
    {
        router.Pop();
    }

    private string CategoryTitle(CoinShopCategoryStyle? category)
    {
        if (category is null)
        {
            return Loc.T(L.Coin.TabShop);
        }

        return category.IsUnfiled ? Loc.T(L.Coin.ShopUnfiled) : category.Name;
    }

    private void DrawCategoryTiles(string parentId, float width, float scale)
    {
        var categories = catalog.Categories;
        var gap = TileGap * scale;
        var cellWidth = (width - gap) * 0.5f;
        var cellHeight = TileHeight * scale;
        var origin = ImGui.GetCursorScreenPos();
        var cell = 0;
        for (var index = 0; index < categories.Length; index++)
        {
            var category = categories[index];
            if (!string.Equals(category.ParentId, parentId, StringComparison.Ordinal))
            {
                continue;
            }

            var column = cell % 2;
            var row = cell / 2;
            var min = new Vector2(origin.X + column * (cellWidth + gap), origin.Y + row * (cellHeight + gap));
            DrawCategoryTile(category, min, cellWidth, cellHeight, scale);
            cell++;
        }

        var rows = (cell + 1) / 2;
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rows * (cellHeight + gap)));
    }

    private void DrawCategoryTile(CoinShopCategoryStyle category, Vector2 origin, float cellWidth, float cellHeight,
        float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var min = origin;
        var max = origin + new Vector2(cellWidth, cellHeight);
        var hovered = UiInteract.Hover(min, max);
        ui.Card(drawList, min, max, 18f * scale, hovered);

        var titleY = DrawTileArt(drawList, category, min, cellWidth, cellHeight, scale);

        var title = category.IsUnfiled ? Loc.T(L.Coin.ShopUnfiled) : category.Name;
        var inset = 12f * scale;
        var titleFit = Typography.FitText(title, cellWidth - inset * 2f, TextStyles.BodyEmphasized);
        var titleSize = Typography.Measure(titleFit, TextStyles.BodyEmphasized);
        Typography.Draw(drawList, new Vector2(min.X + (cellWidth - titleSize.X) * 0.5f, titleY), titleFit,
            ui.Palette.TitleInk, TextStyles.BodyEmphasized);

        var counter = category.OwnedCount is { } owned
            ? Loc.T(L.Coin.SectionOwned, owned, category.ItemCount)
            : Loc.Plural(L.Coin.ShopItemCount, category.ItemCount);
        var counterFit = Typography.FitText(counter, cellWidth - inset * 2f, TextStyles.Caption1);
        var counterSize = Typography.Measure(counterFit, TextStyles.Caption1);
        Typography.Draw(drawList, new Vector2(min.X + (cellWidth - counterSize.X) * 0.5f,
                titleY + titleSize.Y + 4f * scale), counterFit, ui.MutedInk, TextStyles.Caption1);

        if (category.SoonestLeavingUnix is not null)
        {
            var dotRadius = 4f * scale;
            drawList.AddCircleFilled(new Vector2(max.X - inset - dotRadius, min.Y + inset + dotRadius), dotRadius,
                ImGui.GetColorU32(ui.Palette.Accent), 16);
        }

        if (UiInteract.HoverClick(min, max))
        {
            EnterCategory(category);
        }
    }

    private float DrawTileArt(ImDrawListPtr drawList, CoinShopCategoryStyle category, Vector2 min, float cellWidth,
        float cellHeight, float scale)
    {
        var texture = category.ImageUrl.Length == 0 ? null : images.Get(category.ImageUrl);
        if (texture is null)
        {
            var iconSize = cellHeight * TileIconFraction;
            var iconCenter = new Vector2(min.X + cellWidth * 0.5f, min.Y + iconSize * 0.5f + 18f * scale);
            var icon = category.Icon == 0 ? FallbackCategoryIcon : category.Icon;
            ProgressRing.CenterIcon(drawList, iconCenter, (FontAwesomeIcon)icon, ui.Palette.Accent, iconSize);
            return iconCenter.Y + iconSize * 0.5f + 12f * scale;
        }

        var heroMax = new Vector2(min.X + cellWidth, min.Y + cellHeight * TileImageFraction);
        var (uv0, uv1) = ImageFit.Cover(texture.Size.X, texture.Size.Y, cellWidth, heroMax.Y - min.Y);
        drawList.AddImageRounded(texture.Handle, min, heroMax, uv0, uv1, 0xFFFFFFFFu, 18f * scale,
            ImDrawFlags.RoundCornersTop);
        return heroMax.Y + 10f * scale;
    }

    private void EnterCategory(CoinShopCategoryStyle category)
    {
        router.Push(HasChildren(category.Id) ? CoinRoute.Folder(category.Id) : CoinRoute.Shelf(category.Id));
    }

    private bool HasChildren(string categoryId)
    {
        if (categoryId.Length == 0)
        {
            return false;
        }

        var categories = catalog.Categories;
        for (var index = 0; index < categories.Length; index++)
        {
            if (string.Equals(categories[index].ParentId, categoryId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private void DrawShelfItems(CoinSkuStyle[] items, float width, float scale)
    {
        var start = 0;
        while (start < items.Length)
        {
            var kind = EffectiveKind(items[start]);
            var end = start + 1;
            while (end < items.Length && string.Equals(EffectiveKind(items[end]), kind, StringComparison.Ordinal))
            {
                end++;
            }

            DrawItemRun(items, start, end, SpecFor(kind), width, scale);
            start = end;
        }
    }

    private void DrawItemRun(CoinSkuStyle[] items, int start, int end, in ShopCardSpec spec, float width, float scale)
    {
        var gap = SkuCardGap * scale;
        var cellWidth = spec.Columns == 1 ? width : (width - gap) * (1f / spec.Columns);
        var cellHeight = spec.Height * scale;
        var origin = ImGui.GetCursorScreenPos();
        for (var index = start; index < end; index++)
        {
            var cell = index - start;
            var column = cell % spec.Columns;
            var row = cell / spec.Columns;
            var min = new Vector2(origin.X + column * (cellWidth + gap), origin.Y + row * (cellHeight + gap));
            DrawShopItem(items[index], spec, min, cellWidth, scale);
        }

        var rows = (end - start + spec.Columns - 1) / spec.Columns;
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rows * (cellHeight + gap)));
    }

    private readonly record struct ShopCardSpec(int Columns, float Height, float StageHeight, float ButtonHeight,
        bool Centered);

    private static ShopCardSpec SpecFor(string kind)
    {
        if (string.Equals(kind, FrameKind, StringComparison.Ordinal))
        {
            return new ShopCardSpec(2, FrameCellHeight, FrameCellStageHeight, FrameCellButtonHeight, true);
        }

        if (string.Equals(kind, FlairKind, StringComparison.Ordinal))
        {
            return new ShopCardSpec(1, FlairCardHeight, StageHeight, ButtonHeight, false);
        }

        return new ShopCardSpec(1, PlainCardHeight, 0f, ButtonHeight, false);
    }

    private string EffectiveKind(CoinSkuStyle sku)
    {
        if (string.Equals(sku.Kind, FlairKind, StringComparison.Ordinal))
        {
            return badgeCatalog.Find(sku.Payload) is null ? PlainKind : FlairKind;
        }

        if (string.Equals(sku.Kind, FrameKind, StringComparison.Ordinal))
        {
            return FrameKind;
        }

        return PlainKind;
    }

    private void DrawShopItem(CoinSkuStyle sku, in ShopCardSpec spec, Vector2 origin, float cardWidth, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var min = origin;
        var max = origin + new Vector2(cardWidth, spec.Height * scale);
        ui.Card(drawList, min, max, (spec.Centered ? 18f : 20f) * scale, false);

        var inset = (spec.Centered ? 10f : 14f) * scale;
        var left = min.X + inset;
        var right = max.X - inset;
        var cursorY = min.Y + inset;
        var stageWidth = right - left;

        if (spec.StageHeight > 0f)
        {
            var stage = new Rect(new Vector2(left, cursorY),
                new Vector2(right, cursorY + spec.StageHeight * scale));
            DrawItemStage(drawList, stage, sku, scale);
            cursorY = stage.Max.Y + (spec.Centered ? 8f : 12f) * scale;
            if (spec.Centered)
            {
                DrawLeavingTag(drawList, stage, sku, scale);
            }
        }

        if (spec.Centered)
        {
            DrawCenteredLabels(drawList, sku, min, cardWidth, stageWidth, cursorY, scale);
        }
        else
        {
            DrawRowLabels(drawList, sku, left, right, cursorY, scale);
        }

        var buttonRect = new Rect(
            new Vector2(left, max.Y - inset - spec.ButtonHeight * scale),
            new Vector2(right, max.Y - inset));
        DrawBuyControl(sku, buttonRect);
    }

    private void DrawItemStage(ImDrawListPtr drawList, Rect stage, CoinSkuStyle sku, float scale)
    {
        var rounding = 15f * scale;
        Squircle.Fill(drawList, stage.Min, stage.Max, rounding, ImGui.GetColorU32(ui.Palette.FieldSurface));
        Material.EdgeSquircle(drawList, stage.Min, stage.Max, rounding, scale);

        if (string.Equals(sku.Kind, FrameKind, StringComparison.Ordinal))
        {
            DrawFramePreview(drawList, stage, frameCatalog.Find(sku.Payload), scale);
            return;
        }

        var badge = badgeCatalog.Find(sku.Payload);
        if (badge is not null)
        {
            DrawFlairPreview(drawList, stage, badge, scale);
        }
    }

    private void DrawFramePreview(ImDrawListPtr drawList, Rect stage, Core.Social.FrameStyle? frame, float scale)
    {
        DrawBloom(drawList, stage, stage.Center, stage.Width * 0.42f, stage.Height * 0.60f, ui.Palette.Accent);

        var outerRadius = MathF.Min(stage.Height * 0.48f, stage.Width * 0.45f);
        var avatarRadius = outerRadius / (frame?.Scale ?? 1f);
        var user = session.CurrentUser;
        AvatarView.DrawRemote(drawList, stage.Center, avatarRadius, theme, user?.Name ?? string.Empty,
            user?.World ?? string.Empty, user?.AvatarUrl, images, lodestone, 1.2f, 48, 1f, frame);
    }

    private void DrawCenteredLabels(ImDrawListPtr drawList, CoinSkuStyle sku, Vector2 min, float cardWidth,
        float stageWidth, float cursorY, float scale)
    {
        var name = Typography.FitText(sku.Name, stageWidth, TextStyles.BodyEmphasized);
        var nameSize = Typography.Measure(name, TextStyles.BodyEmphasized);
        Typography.Draw(drawList, new Vector2(min.X + (cardWidth - nameSize.X) * 0.5f, cursorY), name,
            ui.Palette.TitleInk, TextStyles.BodyEmphasized);

        var priceText = Loc.Plural(L.Coin.Price, (int)sku.Price);
        var lineHeight = Typography.Measure(priceText, TextStyles.SubheadlineEmphasized).Y;
        var coinGlyph = lineHeight * GlyphFraction;
        var coinGap = lineHeight * GlyphGapFraction;
        var priceFit = Typography.FitText(priceText, stageWidth - coinGlyph - coinGap,
            TextStyles.SubheadlineEmphasized);
        var priceSize = Typography.Measure(priceFit, TextStyles.SubheadlineEmphasized);
        var blockLeft = min.X + (cardWidth - coinGlyph - coinGap - priceSize.X) * 0.5f;
        var priceY = cursorY + nameSize.Y + 4f * scale;
        CurrencyGlyph.Draw(drawList, CurrencyKind.Coins,
            new Vector2(blockLeft + coinGlyph * 0.5f, priceY + priceSize.Y * 0.5f), coinGlyph);
        Typography.Draw(drawList, new Vector2(blockLeft + coinGlyph + coinGap, priceY), priceFit,
            ui.Palette.Accent, TextStyles.SubheadlineEmphasized);
    }

    private void DrawRowLabels(ImDrawListPtr drawList, CoinSkuStyle sku, float left, float right, float cursorY,
        float scale)
    {
        var priceText = Loc.Plural(L.Coin.Price, (int)sku.Price);
        var priceSize = Typography.Measure(priceText, TextStyles.Title3);
        var coinGlyph = priceSize.Y * GlyphFraction;
        var coinGap = priceSize.Y * GlyphGapFraction;
        var priceLeft = right - priceSize.X - coinGlyph - coinGap;

        var name = Typography.FitText(sku.Name, priceLeft - left - 8f * scale, TextStyles.Title3);
        Typography.Draw(drawList, new Vector2(left, cursorY), name, ui.Palette.TitleInk, TextStyles.Title3);

        CurrencyGlyph.Draw(drawList, CurrencyKind.Coins,
            new Vector2(priceLeft + coinGlyph * 0.5f, cursorY + priceSize.Y * 0.5f), coinGlyph);
        Typography.Draw(drawList, new Vector2(priceLeft + coinGlyph + coinGap, cursorY), priceText,
            ui.Palette.Accent, TextStyles.Title3);

        if (sku.AvailableUntilUnix is not { } leavingUnix)
        {
            return;
        }

        var leaving = Loc.T(L.Coin.LeavingSoon, TimeText.FutureDayLabel(leavingUnix));
        var fitted = Typography.FitText(leaving, right - left, TextStyles.Caption1);
        Typography.Draw(drawList, new Vector2(left, cursorY + 26f * scale), fitted, ui.MutedInk, TextStyles.Caption1);
    }

    private void DrawLeavingTag(ImDrawListPtr drawList, Rect stage, CoinSkuStyle sku, float scale)
    {
        if (sku.AvailableUntilUnix is not { } leavingUnix)
        {
            return;
        }

        var label = Loc.T(L.Coin.LeavingSoon, TimeText.FutureDayLabel(leavingUnix));
        var paddingX = 8f * scale;
        var paddingY = 3f * scale;
        var margin = 6f * scale;
        var fitted = Typography.FitText(label, stage.Width - margin * 2f - paddingX * 2f, TextStyles.Caption2);
        var size = Typography.Measure(fitted, TextStyles.Caption2);
        var min = new Vector2(stage.Min.X + margin, stage.Min.Y + margin);
        var max = min + new Vector2(size.X + paddingX * 2f, size.Y + paddingY * 2f);
        Squircle.Fill(drawList, min, max, (max.Y - min.Y) * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Palette.Accent, 0.22f)));
        Typography.Draw(drawList, min + new Vector2(paddingX, paddingY), fitted, ui.Palette.TitleInk,
            TextStyles.Caption2);
    }

    private void DrawBuyControl(CoinSkuStyle sku, Rect buttonRect)
    {
        if (sku.Owned)
        {
            AppSkin.Chip(buttonRect, Loc.T(L.Coin.Owned), true, theme);
            return;
        }

        if (store.Purchasing)
        {
            AppSkin.Chip(buttonRect, Loc.T(L.Coin.Buy), false, theme);
            return;
        }

        if (ui.PillButton(buttonRect, Loc.T(L.Coin.Buy), true, "coin.buy." + sku.Id))
        {
            AskPurchase(sku);
        }
    }

    private void DrawFlairPreview(ImDrawListPtr drawList, Rect stage, Core.Social.BadgeStyle badge, float scale)
    {
        var light = RoleInk.IsLight(theme);
        var padding = StagePadding * scale;
        var maxWidth = stage.Width - padding * 2f;
        var tallest = Typography.Measure("M", new TextStyle(PreviewMaxScale, FontWeight.Bold)).Y;
        var reserve = tallest * (GlyphFraction + GlyphGapFraction);
        var available = MathF.Max(1f, maxWidth - reserve);

        var source = PreviewName();
        var nameScale = Typography.FitScale(source, available, PreviewMaxScale, PreviewMinScale, FontWeight.Bold);
        var nameStyle = new TextStyle(nameScale, FontWeight.Bold);
        var name = Typography.FitText(source, available, nameStyle);
        var nameSize = Typography.Measure(name, nameStyle);

        var glyphSize = nameSize.Y * GlyphFraction;
        var gap = nameSize.Y * GlyphGapFraction;
        var blockWidth = glyphSize + gap + nameSize.X;
        var blockLeft = stage.Center.X - blockWidth * 0.5f;
        var rowCenterY = stage.Center.Y;

        DrawBloom(drawList, stage, new Vector2(stage.Center.X, rowCenterY),
            blockWidth * 0.72f, nameSize.Y * 1.35f, RoleInk.Highlight(badge.Colors[0], light));

        BadgeStrip.DrawOne(drawList, new Vector2(blockLeft + glyphSize * 0.5f, rowCenterY), badge, images, light,
            glyphSize);

        var ink = RoleInk.For(badge.Colors[0], light);
        var namePos = new Vector2(blockLeft + glyphSize + gap, rowCenterY - nameSize.Y * 0.5f);
        Typography.Draw(drawList, namePos, name, ink, nameStyle, NameEffects.For(badge, light));
    }

    private static void DrawBloom(ImDrawListPtr drawList, Rect stage, Vector2 center, float radiusX, float radiusY,
        Vector4 color)
    {
        var spanX = MathF.Min(radiusX, MathF.Min(center.X - stage.Min.X, stage.Max.X - center.X));
        var spanY = MathF.Min(radiusY, MathF.Min(center.Y - stage.Min.Y, stage.Max.Y - center.Y));
        if (spanX <= 1f || spanY <= 1f)
        {
            return;
        }

        var core = ImGui.GetColorU32(color with { W = BloomAlpha });
        var edge = ImGui.GetColorU32(color with { W = 0f });
        var left = center.X - spanX;
        var right = center.X + spanX;
        var top = center.Y - spanY;
        var bottom = center.Y + spanY;

        drawList.AddRectFilledMultiColor(new Vector2(left, top), center, edge, edge, core, edge);
        drawList.AddRectFilledMultiColor(new Vector2(center.X, top), new Vector2(right, center.Y),
            edge, edge, edge, core);
        drawList.AddRectFilledMultiColor(new Vector2(left, center.Y), new Vector2(center.X, bottom),
            edge, core, edge, edge);
        drawList.AddRectFilledMultiColor(center, new Vector2(right, bottom), core, edge, edge, edge);
    }

    private string PreviewName()
    {
        var user = session.CurrentUser;
        if (user is null)
        {
            return Loc.T(L.Coin.TabShop);
        }

        return user.DisplayName.Length > 0 ? user.DisplayName : user.Name;
    }

    private void AskPurchase(CoinSkuStyle sku)
    {
        var price = sku.Price;
        var skuId = sku.Id;
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Coin.BuyConfirmTitle, sku.Name),
            Message = Loc.Plural(L.Coin.BuyConfirmBody, (int)price),
            ConfirmLabel = Loc.T(L.Coin.Buy),
            CancelLabel = Loc.T(L.Common.Cancel),
            Danger = false,
            Confirm = () => store.Purchase(skuId, price),
        });
    }

    private void ConsumePurchaseResult()
    {
        var result = store.TakePurchaseResult();
        if (result is null)
        {
            return;
        }

        if (result.Purchased)
        {
            UiFeedback.Play(UiSound.Success);
            RefreshShop();
            RefreshInventory();
            return;
        }

        if (string.Equals(result.Reason, "frozen", StringComparison.Ordinal))
        {
            confirm.Alert(Loc.T(L.Coin.FrozenAlertTitle), Loc.T(L.Coin.FrozenAlertBody), Loc.T(L.Common.Close));
            return;
        }

        var message = result.Reason switch
        {
            "insufficient" => Loc.T(L.Coin.Insufficient),
            "price_changed" => Loc.T(L.Coin.PriceChanged),
            _ => Loc.T(L.Coin.Unavailable),
        };
        confirm.Alert(null, message, Loc.T(L.Common.Close));
        if (result.Reason == "price_changed")
        {
            catalog.RefreshNow();
        }
    }
}
