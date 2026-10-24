using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.AppStore;

internal sealed partial class AppStoreApp
{
    private const float CategoryCardRatio = 0.78f;
    private const float CategoryCardMin = 104f;
    private const float CategoryCardMax = 150f;
    private const float CategoryGap = 12f;
    private const float CategoryPad = 13f;
    private const float CategoryArtFraction = 0.40f;
    private const float CategoryArtShrink = 0.20f;
    private const float CategoryArtSpread = 0.52f;
    private const float CategoryArtRise = 0.17f;
    private const float CategoryTopLift = 0.24f;
    private const float CategoryBottomDarken = 0.20f;
    private const float CategoryHoverLift = 0.08f;
    private const int CategoryArtCount = 3;
    private const int CategoryColumns = 2;
    private const float BrowseRowHeight = 52f;
    private const float BrowseTileSize = 32f;
    private const float BrowseGlyphScale = 1.05f;
    private const float ChevronSize = 5f;
    private const float EmptyBandHeight = 240f;
    private const int SearchMaxLength = 64;
    private static readonly Vector4 CardInkShadow = new(0f, 0f, 0f, 0.30f);

    private void DrawAppsTab(in PhoneContext context)
    {
        var navBar = AppHeader.BeginLargeTitle(context, false);
        using (UiScope.PushId("appstore.apps"))
        using (var surface = AppSurface.Begin(navBar.Body))
        {
            TakeScrollReset(surface);
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var gap = CategoryGap * scale;
            var cardWidth = (width - gap) / CategoryColumns;
            var cardHeight = Math.Clamp(cardWidth * CategoryCardRatio, CategoryCardMin * scale,
                CategoryCardMax * scale);
            var count = AppStoreCatalog.Order.Length;
            var top = origin.Y;
            for (var categoryIndex = 0; categoryIndex < count; categoryIndex++)
            {
                var column = categoryIndex % CategoryColumns;
                var row = categoryIndex / CategoryColumns;
                var lastAlone = categoryIndex == count - 1 && column == 0;
                var min = new Vector2(origin.X + column * (cardWidth + gap), top + row * (cardHeight + gap));
                var card = new Rect(min, new Vector2(lastAlone ? origin.X + width : min.X + cardWidth,
                    min.Y + cardHeight));
                if (categoryIndex == 0)
                {
                    UiAnchors.Report("appstore.category", card);
                }

                if (DrawCategoryCard(drawList, card, AppStoreCatalog.Order[categoryIndex], scale))
                {
                    OpenCategory(AppStoreCatalog.Order[categoryIndex]);
                }
            }

            var rows = (count + CategoryColumns - 1) / CategoryColumns;
            Reserve(origin, width, top + rows * cardHeight + (rows - 1) * gap);
        }

        AppHeader.EndLargeTitle(in navBar, context, "appstore.apps.nav", Loc.T(L.Store.Apps), NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty);
    }

    private bool DrawCategoryCard(ImDrawListPtr drawList, Rect card, StoreCategory category, float scale)
    {
        var hovered = UiInteract.Hover(card.Min, card.Max);
        float press;
        using (UiScope.PushId("appstore.categoryCard"))
        {
            press = PressFx.Scale(ImGui.GetID((int)category), hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left),
                PressFx.CardPressedScale);
        }

        var half = card.Size * 0.5f * press;
        var body = new Rect(card.Center - half, card.Center + half);
        var tint = AppStoreCatalog.Tint(category);
        var rounding = Metrics.Radius.Grouped * scale;
        var lift = hovered ? CategoryHoverLift : 0f;
        Squircle.FillVerticalGradient(drawList, body.Min, body.Max, rounding,
            ImGui.GetColorU32(Palette.Lighten(tint, CategoryTopLift + lift)),
            ImGui.GetColorU32(Palette.Darken(tint, CategoryBottomDarken - lift * 0.5f)));
        Material.EdgeSquircle(drawList, body.Min, body.Max, rounding, scale, 0.75f);
        DrawCategoryArt(drawList, body, category, scale);
        var pad = CategoryPad * scale;
        var label = Loc.T(AppStoreCatalog.Name(category));
        var maxLabelWidth = body.Width - pad * 2f;
        var labelLeft = body.Min.X + pad;
        var labelTop = body.Max.Y - pad - Typography.LineHeight(TextStyles.Headline);
        Marquee.DrawLeft(new MarqueeId("appstore.category.label.shadow.", (int)category), label, labelLeft,
            labelTop + 1f * scale, maxLabelWidth, TextStyles.Headline, CardInkShadow, hovered);
        Marquee.DrawLeft(new MarqueeId("appstore.category.label.", (int)category), label, labelLeft, labelTop,
            maxLabelWidth, TextStyles.Headline, White, hovered);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(card.Min, card.Max, hovered);
    }

    private void DrawCategoryArt(ImDrawListPtr drawList, Rect card, StoreCategory category, float scale)
    {
        var members = index.Category(category);
        var pad = CategoryPad * scale;
        if (members.Count == 0)
        {
            AppSkin.Icon(drawList, new Vector2(card.Max.X - pad * 3f, card.Min.Y + pad * 3f),
                IconGlyph.Of(AppStoreCatalog.Icon(category)), Palette.WithAlpha(White, 0.92f), 2f);
            return;
        }

        var frontSize = MathF.Min(card.Height, card.Width) * CategoryArtFraction;
        var frontCenter = new Vector2(card.Max.X - pad - frontSize * 0.5f, card.Min.Y + pad + frontSize * 0.62f);
        var depthCount = Math.Min(members.Count, CategoryArtCount);
        for (var depth = depthCount - 1; depth > 0; depth--)
        {
            var shrink = 1f - depth * CategoryArtShrink;
            var center = new Vector2(frontCenter.X - frontSize * CategoryArtSpread * depth,
                frontCenter.Y - frontSize * CategoryArtRise * depth);
            DrawIcon(drawList, center, frontSize * shrink, members[depth]);
        }

        DrawIcon(drawList, frontCenter, frontSize, members[0]);
    }

    private void DrawCategoryView(in PhoneContext context, in StoreView view, int depth)
    {
        var navBar = AppHeader.BeginLargeTitle(context);
        using (UiScope.PushId("appstore.category"))
        using (UiScope.PushId(view.Serial))
        using (AppSurface.Begin(navBar.Body))
        {
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var top = DrawRowCard(drawList, origin, width, index.Category(view.Category), int.MaxValue, scale);
            Reserve(origin, width, top);
        }

        AppHeader.EndLargeTitle(in navBar, context, "appstore.category.nav",
            Loc.T(AppStoreCatalog.Name(view.Category)), NavBarStyle.From(ui), ReadOnlySpan<NavBarButton>.Empty,
            BackTitle(depth), back);
    }

    private void DrawUpdatesView(in PhoneContext context, in StoreView view, int depth)
    {
        var navBar = AppHeader.BeginLargeTitle(context);
        using (UiScope.PushId("appstore.updates"))
        using (UiScope.PushId(view.Serial))
        using (AppSurface.Begin(navBar.Body))
        {
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var top = DrawUpdateCard(drawList, origin, width, index.Updates, int.MaxValue, scale);
            Reserve(origin, width, top);
        }

        AppHeader.EndLargeTitle(in navBar, context, "appstore.updates.nav", texts.UpdatedIn(index.LatestVersion),
            NavBarStyle.From(ui), ReadOnlySpan<NavBarButton>.Empty, BackTitle(depth), back);
    }

    private void DrawSearchTab(in PhoneContext context)
    {
        var navBar = AppHeader.BeginLargeTitle(context, false);
        using (UiScope.PushId("appstore.search"))
        using (var surface = AppSurface.Begin(navBar.Body))
        {
            TakeScrollReset(surface);
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var field = new Rect(origin, new Vector2(origin.X + width, origin.Y + GlassField.HeightUnits * scale));
            var ink = ui.Ink;
            SearchBar.Surface(drawList, field, ink);
            GlassField.Search(drawList, field, "##appstoreSearch", Loc.T(L.Store.SearchHint), ref search, ink.Ink,
                ink.Muted, scale, SearchMaxLength, false);
            index.Search(search);
            var top = field.Max.Y + SectionGap * scale;
            if (search.AsSpan().Trim().Length == 0)
            {
                top = DrawSectionHeader(drawList, new Vector2(origin.X, top), width, Loc.T(L.Store.BrowseCategories),
                    false, out _);
                top = DrawBrowseCard(drawList, new Vector2(origin.X, top), width, scale);
            }
            else if (index.Results.Count == 0)
            {
                var band = new Rect(new Vector2(origin.X, top), new Vector2(origin.X + width, top + EmptyBandHeight * scale));
                EmptyState.Draw(band, ui, FontAwesomeIcon.Search, Loc.T(L.Store.NoResults), Loc.T(L.Store.NoResultsHint));
                top = band.Max.Y;
            }
            else
            {
                top = DrawRowCard(drawList, new Vector2(origin.X, top), width, index.Results, int.MaxValue, scale);
            }

            Reserve(origin, width, top);
        }

        AppHeader.EndLargeTitle(in navBar, context, "appstore.search.nav", Loc.T(L.Store.Search),
            NavBarStyle.From(ui), ReadOnlySpan<NavBarButton>.Empty);
    }

    private float DrawBrowseCard(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var count = AppStoreCatalog.Order.Length;
        var rowHeight = BrowseRowHeight * scale;
        var cardMax = new Vector2(origin.X + width, origin.Y + count * rowHeight);
        ui.Card(drawList, origin, cardMax, Metrics.Radius.Grouped * scale);
        var inset = RowInset * scale;
        var tileSize = BrowseTileSize * scale;
        for (var categoryIndex = 0; categoryIndex < count; categoryIndex++)
        {
            var category = AppStoreCatalog.Order[categoryIndex];
            var rowTop = origin.Y + categoryIndex * rowHeight;
            var row = new Rect(new Vector2(origin.X + inset, rowTop), new Vector2(cardMax.X - inset, rowTop + rowHeight));
            if (categoryIndex > 0)
            {
                drawList.AddLine(new Vector2(row.Min.X + tileSize + RowTextGap * scale, rowTop),
                    new Vector2(row.Max.X, rowTop), ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, HairlineAlpha)),
                    1f);
            }

            var hovered = UiInteract.Hover(row.Min, row.Max);
            if (hovered)
            {
                Squircle.Fill(drawList, new Vector2(row.Min.X - inset * 0.5f, row.Min.Y + 2f * scale),
                    new Vector2(row.Max.X + inset * 0.5f, row.Max.Y - 2f * scale), Metrics.Radius.Md * scale,
                    ImGui.GetColorU32(ui.HoverTint));
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            var tileCenter = new Vector2(row.Min.X + tileSize * 0.5f, row.Center.Y);
            var tileHalf = new Vector2(tileSize * 0.5f);
            IconTile.FillShaded(drawList, tileCenter - tileHalf, tileCenter + tileHalf,
                tileSize * Metrics.Radius.TileFactor, IconTile.Surface(AppStoreCatalog.Tint(category)));
            AppSkin.Icon(drawList, tileCenter, IconGlyph.Of(AppStoreCatalog.Icon(category)), White, BrowseGlyphScale);
            var textLeft = row.Min.X + tileSize + RowTextGap * scale;
            var chevronX = row.Max.X - ChevronSize * scale;
            var label = Typography.FitText(Loc.T(AppStoreCatalog.Name(category)),
                MathF.Max(1f, chevronX - textLeft - RowTextGap * scale), TextStyles.Body);
            Typography.Draw(drawList,
                new Vector2(textLeft, row.Center.Y - Typography.LineHeight(TextStyles.Body) * 0.5f), label,
                ui.TitleInk, TextStyles.Body);
            DrawChevron(drawList, new Vector2(chevronX, row.Center.Y), scale);
            if (UiInteract.Click(row.Min, row.Max, hovered))
            {
                OpenCategory(category);
            }
        }

        return cardMax.Y;
    }

    private void DrawChevron(ImDrawListPtr drawList, Vector2 tip, float scale)
    {
        var arm = ChevronSize * scale;
        var color = ImGui.GetColorU32(ui.MutedInk);
        drawList.AddLine(new Vector2(tip.X - arm, tip.Y - arm), tip, color, 1.6f * scale);
        drawList.AddLine(new Vector2(tip.X - arm, tip.Y + arm), tip, color, 1.6f * scale);
    }
}
