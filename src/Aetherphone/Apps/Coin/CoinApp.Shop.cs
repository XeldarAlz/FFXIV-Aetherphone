using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Coins;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Coin;

internal sealed partial class CoinApp
{
    private const string FlairKind = "flair";
    private const string FrameKind = "frame";

    private const float BalanceStripHeight = 52f;
    private const float FeaturedAspect = 0.52f;
    private const float FeaturedIconFraction = 0.42f;
    private const float FeaturedScrimAlpha = 0.62f;
    private const float FeaturedTagPadX = 10f;
    private const float FeaturedTagPadY = 4f;
    private const float FeaturedTagAlpha = 0.88f;
    private const float TileHeight = 168f;
    private const float TileArtFraction = 0.56f;
    private const float TileIconFraction = 0.38f;
    private const float TileGap = 12f;
    private const float TileBarHeight = 3f;
    private const float TileLeavingDot = 4f;
    private const float ItemStageFraction = 0.86f;
    private const float ItemCellGap = 12f;
    private const float ItemPad = 10f;
    private const float StagePadding = 14f;
    private const float StageRounding = 16f;
    private const float GlyphFraction = 0.72f;
    private const float GlyphGapFraction = 0.34f;
    private const float PreviewMaxScale = 1.90f;
    private const float PreviewMinScale = 0.80f;
    private const float BloomAlpha = 0.16f;
    private const float PlainGlyphFraction = 0.36f;
    private const float TagAlpha = 0.22f;
    private const long FallbackCategoryIcon = 0xF290;

    private CachedText balanceStripText;

    private void DrawShop(in PhoneContext context)
    {
        var navBar = AppHeader.BeginLargeTitle(context, false);
        using (UiScope.PushId("coin.shop"))
        using (var surface = AppSurface.Begin(navBar.Body))
        {
            catalog.EnsureFresh();
            shopRefresh.Draw(navBar.Body, surface.Pull, surface.Dragging, catalog.Fetching, ui.MutedInk, RefreshShop);
            DrawShopBody(navBar.Body);
        }

        AppHeader.EndLargeTitle(in navBar, context, "coin.shop.nav", TabTitle(CoinTab.Shop), NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty);
    }

    private void DrawShopBody(Rect body)
    {
        var scale = UiScale.Current;
        var categories = catalog.Categories;
        if (categories.Length == 0)
        {
            if (!catalog.LoadedOnce)
            {
                LoadingPulse.Draw(body.Center, 16f * scale, ui.Palette.Accent, ui.MutedInk, LoadingPulse.SafeLabel());
            }
            else
            {
                CoinArt.StateScreen(ImGui.GetWindowDrawList(), ui, body, FontAwesomeIcon.Store,
                    Loc.T(L.Coin.ShopEmpty), Loc.T(L.Coin.ShopEmptyHint), string.Empty, 0u, scale);
            }

            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var cursorY = DrawBalanceStrip(drawList, origin, width, scale);
        cursorY = DrawFeatured(drawList, new Vector2(origin.X, cursorY), width, scale);
        cursorY += CoinArt.SectionGap * scale;
        cursorY += CoinArt.SectionHeader(drawList, new Vector2(origin.X, cursorY), width, Loc.T(L.Coin.Categories),
            ui.TitleInk, 0f, scale) + CoinArt.HeaderGap * scale;
        var tilesTop = cursorY;
        cursorY = DrawCategoryTiles(new Vector2(origin.X, cursorY), string.Empty, width, scale);
        var tilesBottom = MathF.Min(cursorY, body.Max.Y);
        if (tilesBottom > tilesTop)
        {
            UiAnchors.Report("coin.shop", new Rect(new Vector2(origin.X, tilesTop),
                new Vector2(origin.X + width, tilesBottom)));
        }

        CoinArt.Reserve(origin, width, cursorY + CoinArt.BottomPad * scale);
    }

    private float DrawBalanceStrip(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var wallet = store.Wallet;
        if (wallet is null)
        {
            return origin.Y;
        }

        var max = new Vector2(origin.X + width, origin.Y + BalanceStripHeight * scale);
        CoinArt.Card(drawList, ui, origin, max, scale);
        var pad = Metrics.Space.Lg * scale;
        var centerY = (origin.Y + max.Y) * 0.5f;
        var amount = BalanceText(wallet.Balance);
        var amountWidth = CoinArt.PriceWidth(amount, TextStyles.Headline);
        var lineHeight = Typography.LineHeight(TextStyles.Headline);
        CoinArt.Price(drawList, new Vector2(max.X - pad - amountWidth, centerY - lineHeight * 0.5f), amount,
            ui.TitleInk, TextStyles.Headline);
        var labelHeight = Typography.LineHeight(TextStyles.Body);
        Typography.Draw(drawList, new Vector2(origin.X + pad, centerY - labelHeight * 0.5f),
            Typography.FitText(Loc.T(L.Coin.YourBalance), MathF.Max(1f, width - pad * 3f - amountWidth),
                TextStyles.Body), ui.MutedInk, TextStyles.Body);
        return max.Y;
    }

    private string BalanceText(long balance) =>
        balanceStripText.IsCurrent(balance)
            ? balanceStripText.Value
            : balanceStripText.Store(balance, NumberText.Group(balance));

    private CoinShopCategoryStyle? FeaturedCategory()
    {
        CoinShopCategoryStyle? best = null;
        var categories = catalog.Categories;
        for (var index = 0; index < categories.Length; index++)
        {
            var category = categories[index];
            if (category.SoonestLeavingUnix is not { } leaving || HasChildren(category.Id))
            {
                continue;
            }

            if (best is null || leaving < best.SoonestLeavingUnix)
            {
                best = category;
            }
        }

        return best;
    }

    private float DrawFeatured(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var category = FeaturedCategory();
        if (category is null)
        {
            return origin.Y;
        }

        var top = origin.Y + Metrics.Space.Md * scale;
        var height = width * FeaturedAspect;
        var restMin = new Vector2(origin.X, top);
        var restMax = new Vector2(origin.X + width, top + height);
        var hovered = UiInteract.Hover(restMin, restMax);
        var press = PressFx.Scale("coin.featured", hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left),
            Core.Animation.Motion.PressScaleCard);
        var center = (restMin + restMax) * 0.5f;
        var half = (restMax - restMin) * 0.5f * press;
        var min = center - half;
        var max = center + half;
        var radius = Metrics.Radius.Grouped * scale;
        var texture = category.ImageUrl.Length == 0 ? null : images.Get(category.ImageUrl);
        if (texture is null)
        {
            IconTile.FillShaded(drawList, min, max, radius, IconTile.Surface(ui.Accent));
            var icon = category.Icon == 0 ? FallbackCategoryIcon : category.Icon;
            var iconSize = height * FeaturedIconFraction;
            ProgressRing.CenterIcon(drawList, new Vector2(max.X - iconSize * 0.9f, min.Y + height * 0.42f),
                (FontAwesomeIcon)icon, Palette.WithAlpha(CoinArt.White, 0.85f), iconSize);
        }
        else
        {
            var (uv0, uv1) = ImageFit.Cover(texture.Size.X, texture.Size.Y, max.X - min.X, max.Y - min.Y);
            Squircle.FillImage(drawList, min, max, radius, texture.Handle, 0xFFFFFFFFu, uv0, uv1);
        }

        Squircle.FillVerticalGradient(drawList, min, max, radius, 0u,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, FeaturedScrimAlpha)));
        Material.EdgeSquircle(drawList, min, max, radius, scale);

        var pad = Metrics.Space.Lg * scale;
        DrawTag(drawList, new Vector2(min.X + pad, min.Y + pad), LeavingText(category.SoonestLeavingUnix ?? 0),
            (max.X - min.X) - pad * 2f, ui.Accent, scale);
        var counter = CategoryCounter(category);
        var counterHeight = Typography.LineHeight(TextStyles.Subheadline);
        var titleHeight = Typography.LineHeight(TextStyles.Title2);
        var textWidth = (max.X - min.X) - pad * 2f;
        Typography.Draw(drawList, new Vector2(min.X + pad, max.Y - pad - counterHeight),
            Typography.FitText(counter, textWidth, TextStyles.Subheadline), Palette.WithAlpha(CoinArt.White, 0.82f),
            TextStyles.Subheadline);
        Typography.Draw(drawList, new Vector2(min.X + pad, max.Y - pad - counterHeight - titleHeight),
            Typography.FitText(CategoryTitle(category), textWidth, TextStyles.Title2), CoinArt.White,
            TextStyles.Title2);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(restMin, restMax, hovered))
        {
            EnterCategory(category);
        }

        return restMax.Y;
    }

    private static void DrawTag(ImDrawListPtr drawList, Vector2 topLeft, string label, float maxWidth, Vector4 tint,
        float scale)
    {
        var padX = FeaturedTagPadX * scale;
        var padY = FeaturedTagPadY * scale;
        var fitted = Typography.FitText(label, MathF.Max(1f, maxWidth - padX * 2f), TextStyles.FootnoteEmphasized);
        var size = Typography.Measure(fitted, TextStyles.FootnoteEmphasized);
        var max = topLeft + new Vector2(size.X + padX * 2f, size.Y + padY * 2f);
        Squircle.Fill(drawList, topLeft, max, (max.Y - topLeft.Y) * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(Palette.Darken(tint, 0.25f), FeaturedTagAlpha)));
        Typography.Draw(drawList, topLeft + new Vector2(padX, padY), fitted, CoinArt.White,
            TextStyles.FootnoteEmphasized);
    }

    private void DrawShopBrowse(in PhoneContext context, CoinRoute route, int depth)
    {
        var category = catalog.Category(route.CategoryId);
        var navBar = AppHeader.BeginLargeTitle(context);
        using (UiScope.PushId("coin.browse"))
        using (var surface = AppSurface.Begin(navBar.Body))
        {
            browseRefresh.Draw(navBar.Body, surface.Pull, surface.Dragging, catalog.Fetching, ui.MutedInk,
                RefreshShop);
            DrawBrowseBody(navBar.Body, route);
        }

        AppHeader.EndLargeTitle(in navBar, context, "coin.browse.nav", CategoryTitle(category), NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty, BackTitle(depth), back);
    }

    private void DrawBrowseBody(Rect body, CoinRoute route)
    {
        var scale = UiScale.Current;
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        if (route.Screen == CoinScreen.ShopFolder)
        {
            var bottom = DrawCategoryTiles(origin, route.CategoryId, width, scale);
            CoinArt.Reserve(origin, width, bottom + CoinArt.BottomPad * scale);
            return;
        }

        catalog.EnsureShelf(route.CategoryId);
        var items = catalog.Shelf(route.CategoryId);
        if (items.Length == 0)
        {
            if (catalog.ShelfLoaded(route.CategoryId) || catalog.ItemsComplete)
            {
                CoinArt.StateScreen(ImGui.GetWindowDrawList(), ui, body, FontAwesomeIcon.Store,
                    Loc.T(L.Coin.ShopShelfEmpty), Loc.T(L.Coin.ShopEmptyHint), string.Empty, 0u, scale);
            }
            else
            {
                LoadingPulse.Draw(body.Center, 16f * scale, ui.Palette.Accent, ui.MutedInk, LoadingPulse.SafeLabel());
            }

            return;
        }

        var itemsBottom = DrawItemGrid(origin, items, route.CategoryId, width, scale);
        CoinArt.Reserve(origin, width, itemsBottom + CoinArt.BottomPad * scale);
    }

    private string CategoryTitle(CoinShopCategoryStyle? category)
    {
        if (category is null)
        {
            return Loc.T(L.Coin.TabShop);
        }

        return category.IsUnfiled ? Loc.T(L.Coin.ShopUnfiled) : category.Name;
    }

    private float DrawCategoryTiles(Vector2 origin, string parentId, float width, float scale)
    {
        var categories = catalog.Categories;
        var gap = TileGap * scale;
        var cellWidth = (width - gap) * 0.5f;
        var cellHeight = TileHeight * scale;
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
            if (ImGui.IsRectVisible(min, min + new Vector2(cellWidth, cellHeight)))
            {
                DrawCategoryTile(category, min, cellWidth, cellHeight, scale);
            }

            cell++;
        }

        var rows = (cell + 1) / 2;
        return origin.Y + MathF.Max(0f, rows * (cellHeight + gap) - gap);
    }

    private void DrawCategoryTile(CoinShopCategoryStyle category, Vector2 origin, float cellWidth, float cellHeight,
        float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var restMin = origin;
        var restMax = origin + new Vector2(cellWidth, cellHeight);
        var hovered = UiInteract.Hover(restMin, restMax);
        var press = PressFx.Scale(ImGui.GetID(category.Id.Length == 0 ? "coin.tile.unfiled" : category.Id),
            hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left), Core.Animation.Motion.PressScaleCard);
        var center = (restMin + restMax) * 0.5f;
        var half = (restMax - restMin) * 0.5f * press;
        var min = center - half;
        var max = center + half;
        var radius = Metrics.Radius.Grouped * scale;
        CoinArt.Card(drawList, ui, min, max, scale);

        var artMax = new Vector2(max.X, min.Y + (max.Y - min.Y) * TileArtFraction);
        var texture = category.ImageUrl.Length == 0 ? null : images.Get(category.ImageUrl);
        if (texture is null)
        {
            var inset = Metrics.Space.Sm * scale;
            var artMin = min + new Vector2(inset, inset);
            var artInnerMax = new Vector2(max.X - inset, artMax.Y);
            IconTile.FillShaded(drawList, artMin, artInnerMax, radius - inset, IconTile.Surface(ui.Accent));
            var icon = category.Icon == 0 ? FallbackCategoryIcon : category.Icon;
            ProgressRing.CenterIcon(drawList, (artMin + artInnerMax) * 0.5f, (FontAwesomeIcon)icon, AccentRing.Ink,
                (artInnerMax.Y - artMin.Y) * TileIconFraction);
        }
        else
        {
            var (uv0, uv1) = ImageFit.Cover(texture.Size.X, texture.Size.Y, max.X - min.X, artMax.Y - min.Y);
            drawList.AddImageRounded(texture.Handle, min, artMax, uv0, uv1, 0xFFFFFFFFu, radius,
                ImDrawFlags.RoundCornersTop);
        }

        var pad = Metrics.Space.Md * scale;
        var textWidth = (max.X - min.X) - pad * 2f;
        var titleTop = artMax.Y + Metrics.Space.Sm * scale;
        Typography.Draw(drawList, new Vector2(min.X + pad, titleTop),
            Typography.FitText(CategoryTitle(category), textWidth, TextStyles.BodyEmphasized), ui.TitleInk,
            TextStyles.BodyEmphasized);
        var counterTop = titleTop + Typography.LineHeight(TextStyles.BodyEmphasized) + CoinArt.LineGap * scale;
        Typography.Draw(drawList, new Vector2(min.X + pad, counterTop),
            Typography.FitText(CategoryCounter(category), textWidth, TextStyles.Footnote), ui.MutedInk,
            TextStyles.Footnote);
        if (category.OwnedCount is { } owned && category.ItemCount > 0)
        {
            var barTop = max.Y - pad - TileBarHeight * scale;
            CoinArt.Bar(drawList, new Vector2(min.X + pad, barTop), new Vector2(max.X - pad, barTop + TileBarHeight * scale),
                owned / (float)category.ItemCount, Palette.WithAlpha(ui.TitleInk, 0.12f), ui.Accent);
        }

        if (category.SoonestLeavingUnix is not null)
        {
            var dot = TileLeavingDot * scale;
            drawList.AddCircleFilled(new Vector2(max.X - pad - dot, artMax.Y + pad + dot), dot,
                ImGui.GetColorU32(ui.Accent), 16);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(restMin, restMax, hovered))
        {
            EnterCategory(category);
        }
    }

    private string CategoryCounter(CoinShopCategoryStyle category) =>
        texts.Counter(category.OwnedCount, category.ItemCount);

    private void EnterCategory(CoinShopCategoryStyle category)
    {
        UiFeedback.Play(UiSound.Tap);
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

    private float DrawItemGrid(Vector2 origin, CoinSkuStyle[] items, string categoryId, float width, float scale)
    {
        var gap = ItemCellGap * scale;
        var cellWidth = (width - gap) * 0.5f;
        var cellHeight = ItemCellHeight(cellWidth, scale);
        for (var index = 0; index < items.Length; index++)
        {
            var column = index % 2;
            var row = index / 2;
            var min = new Vector2(origin.X + column * (cellWidth + gap), origin.Y + row * (cellHeight + gap));
            if (ImGui.IsRectVisible(min, min + new Vector2(cellWidth, cellHeight)))
            {
                DrawItemTile(items[index], categoryId, min, cellWidth, cellHeight, scale);
            }
        }

        var rows = (items.Length + 1) / 2;
        return origin.Y + MathF.Max(0f, rows * (cellHeight + gap) - gap);
    }

    private static float ItemCellHeight(float cellWidth, float scale)
    {
        var pad = ItemPad * scale;
        var stage = (cellWidth - pad * 2f) * ItemStageFraction;
        return pad + stage + Metrics.Space.Sm * scale + Typography.LineHeight(TextStyles.BodyEmphasized) +
               Metrics.Space.Sm * scale + CoinArt.CapsuleHeight * scale + pad;
    }

    private void DrawItemTile(CoinSkuStyle sku, string categoryId, Vector2 origin, float cellWidth, float cellHeight,
        float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var min = origin;
        var max = origin + new Vector2(cellWidth, cellHeight);
        var pad = ItemPad * scale;
        var buttonRect = new Rect(new Vector2(min.X + pad, max.Y - pad - CoinArt.CapsuleHeight * scale),
            new Vector2(max.X - pad, max.Y - pad));
        var overButton = UiInteract.Hover(buttonRect.Min, buttonRect.Max);
        var hovered = !overButton && UiInteract.Hover(min, max);
        CoinArt.Card(drawList, ui, min, max, scale);
        if (hovered)
        {
            Squircle.Fill(drawList, min, max, Metrics.Radius.Grouped * scale, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var stageHeight = (cellWidth - pad * 2f) * ItemStageFraction;
        var stage = new Rect(new Vector2(min.X + pad, min.Y + pad), new Vector2(max.X - pad, min.Y + pad + stageHeight));
        DrawItemStage(drawList, stage, sku.Kind, sku.Payload, scale);
        if (sku.AvailableUntilUnix is { } leaving && !sku.Owned)
        {
            DrawTag(drawList, stage.Min + new Vector2(Metrics.Space.Xs * scale, Metrics.Space.Xs * scale),
                LeavingText(leaving), stage.Width - Metrics.Space.Xs * scale * 2f, ui.Accent, scale);
        }

        var nameTop = stage.Max.Y + Metrics.Space.Sm * scale;
        Typography.Draw(drawList, new Vector2(min.X + pad, nameTop),
            Typography.FitText(sku.Name, cellWidth - pad * 2f, TextStyles.BodyEmphasized), ui.TitleInk,
            TextStyles.BodyEmphasized);
        DrawBuyControl(drawList, sku, buttonRect);
        if (UiInteract.Click(min, max, hovered))
        {
            OpenProduct(categoryId, sku.Id);
        }
    }

    private void DrawBuyControl(ImDrawListPtr drawList, CoinSkuStyle sku, Rect buttonRect)
    {
        if (sku.Owned)
        {
            CoinArt.Capsule(drawList, ui, ImGui.GetID(sku.Id), buttonRect, Loc.T(L.Coin.Owned), CapsuleTone.Quiet,
                false);
            return;
        }

        var enabled = !store.Purchasing && store.Wallet?.FrozenUntilUnix is null;
        if (CoinArt.PriceCapsule(drawList, ui, ImGui.GetID(sku.Id), buttonRect, NumberText.Group(sku.Price), enabled))
        {
            AskPurchase(sku);
        }
    }

    private void OpenProduct(string categoryId, string skuId)
    {
        UiFeedback.Play(UiSound.Tap);
        router.Push(CoinRoute.Product(categoryId, skuId));
    }

    private string LeavingText(long leavingUnix) => texts.Leaving(leavingUnix);

    private void DrawItemStage(ImDrawListPtr drawList, Rect stage, string kind, string payload, float scale)
    {
        var rounding = StageRounding * scale;
        Squircle.Fill(drawList, stage.Min, stage.Max, rounding, ImGui.GetColorU32(ui.Palette.FieldSurface));
        drawList.PushClipRect(stage.Min, stage.Max, true);
        if (string.Equals(kind, FrameKind, StringComparison.Ordinal))
        {
            DrawFramePreview(drawList, stage, frameCatalog.Find(payload));
        }
        else if (string.Equals(kind, FlairKind, StringComparison.Ordinal) && badgeCatalog.Find(payload) is { } badge)
        {
            DrawFlairPreview(drawList, stage, badge, scale);
        }
        else
        {
            DrawBloom(drawList, stage, stage.Center, stage.Width * 0.42f, stage.Height * 0.55f, ui.Accent);
            ProgressRing.CenterIcon(drawList, stage.Center, KindIcon(kind), ui.Accent,
                MathF.Min(stage.Width, stage.Height) * PlainGlyphFraction);
        }

        drawList.PopClipRect();
    }

    private static FontAwesomeIcon KindIcon(string kind)
    {
        if (string.Equals(kind, FrameKind, StringComparison.Ordinal))
        {
            return FontAwesomeIcon.UserCircle;
        }

        return string.Equals(kind, FlairKind, StringComparison.Ordinal) ? FontAwesomeIcon.Star : FontAwesomeIcon.Gift;
    }

    private void DrawFramePreview(ImDrawListPtr drawList, Rect stage, Core.Social.FrameStyle? frame)
    {
        DrawBloom(drawList, stage, stage.Center, stage.Width * 0.42f, stage.Height * 0.60f, ui.Accent);
        var outerRadius = MathF.Min(stage.Height * 0.44f, stage.Width * 0.44f);
        var avatarRadius = outerRadius / (frame?.Scale ?? 1f);
        var user = session.CurrentUser;
        AvatarView.DrawRemote(drawList, stage.Center, avatarRadius, theme, user?.Name ?? string.Empty,
            user?.World ?? string.Empty, user?.AvatarUrl, images, lodestone, 1.2f, 48, 1f, frame);
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
        DrawBloom(drawList, stage, new Vector2(stage.Center.X, rowCenterY), blockWidth * 0.72f, nameSize.Y * 1.35f,
            RoleInk.Highlight(badge.Colors[0], light));
        BadgeStrip.DrawOne(drawList, new Vector2(blockLeft + glyphSize * 0.5f, rowCenterY), badge, images, light,
            glyphSize);
        var ink = RoleInk.For(badge.Colors[0], light);
        Typography.Draw(drawList, new Vector2(blockLeft + glyphSize + gap, rowCenterY - nameSize.Y * 0.5f), name, ink,
            nameStyle, NameEffects.For(badge, light));
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
        drawList.AddRectFilledMultiColor(new Vector2(center.X, top), new Vector2(right, center.Y), edge, edge, edge,
            core);
        drawList.AddRectFilledMultiColor(new Vector2(left, center.Y), new Vector2(center.X, bottom), edge, core, edge,
            edge);
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

    private CoinSkuStyle? FindSku(string categoryId, string skuId)
    {
        var items = catalog.Shelf(categoryId);
        for (var index = 0; index < items.Length; index++)
        {
            if (string.Equals(items[index].Id, skuId, StringComparison.Ordinal))
            {
                return items[index];
            }
        }

        return null;
    }

    private void AskPurchase(CoinSkuStyle sku)
    {
        UiFeedback.Play(UiSound.Tap);
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
            ClearGoalFor(result.SkuId);
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
        if (string.Equals(result.Reason, "price_changed", StringComparison.Ordinal))
        {
            catalog.RefreshNow();
        }
    }
}
