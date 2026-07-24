using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.AppStore;

internal sealed partial class AppStoreApp
{
    private const float CategoryCardRatio = 0.80f;
    private const float CategoryCardMin = 100f;
    private const float CategoryCardMax = 148f;
    private const float CategoryGap = 12f;
    private const float CategoryPad = 13f;
    private static readonly Vector4 CardInk = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 CardInkShadow = new(0f, 0f, 0f, 0.30f);

    private void DrawCatalogTab(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        DrawLargeTitle(area, Loc.T(L.Store.Apps), null);
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + (HeaderHeight - 18f) * scale), area.Max);
        using (var surface = AppSurface.Begin(body))
        {
            if (resetScroll)
            {
                surface.JumpToTop();
                resetScroll = false;
            }

            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            Typography.Draw(new Vector2(origin.X, origin.Y), Loc.T(L.Store.BrowseCategories), ui.TitleInk,
                TextStyles.Title3);
            var top = origin.Y + 30f * scale;
            var gap = CategoryGap * scale;
            var cardWidth = (width - gap) * 0.5f;
            var cardHeight = Math.Clamp(cardWidth * CategoryCardRatio, CategoryCardMin * scale,
                CategoryCardMax * scale);
            for (var index = 0; index < AppStoreCatalog.Order.Length; index++)
            {
                var column = index % 2;
                var row = index / 2;
                var min = new Vector2(origin.X + column * (cardWidth + gap), top + row * (cardHeight + gap));
                var card = new Rect(min, new Vector2(min.X + cardWidth, min.Y + cardHeight));
                if (DrawCategoryCard(card, index, scale))
                {
                    router.Push(StoreView.ForCategory(AppStoreCatalog.Order[index]));
                }
            }

            var rows = (AppStoreCatalog.Order.Length + 1) / 2;
            top += rows * (cardHeight + gap);
            ImGui.SetCursorScreenPos(new Vector2(origin.X, top));
            ImGui.Dummy(new Vector2(width, Metrics.Space.Lg * scale));
        }
    }

    private bool DrawCategoryCard(Rect card, int categoryIndex, float scale)
    {
        var category = AppStoreCatalog.Order[categoryIndex];
        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(card.Min, card.Max);
        var tint = AppStoreCatalog.Tint(category);
        var rounding = Metrics.Radius.Card * scale * 1.15f;
        var lift = hovered ? 0.08f : 0f;
        Elevation.Floating(drawList, card.Min, card.Max, rounding, scale, hovered ? 0.6f : 0.34f);
        Squircle.FillVerticalGradient(drawList, card.Min, card.Max, rounding,
            ImGui.GetColorU32(Palette.Lighten(tint, 0.24f + lift)),
            ImGui.GetColorU32(Palette.Darken(tint, 0.20f - lift * 0.5f)));
        Material.EdgeSquircle(drawList, card.Min, card.Max, rounding, scale, 0.75f);
        DrawCategoryArt(drawList, card, categoryIndex, category, scale);
        var pad = CategoryPad * scale;
        var label = Typography.FitText(Loc.T(AppStoreCatalog.Name(category)), card.Width - pad * 2f,
            TextStyles.Headline);
        var labelTop = card.Max.Y - 28f * scale;
        Typography.Draw(drawList, new Vector2(card.Min.X + pad, labelTop + 1f * scale), label, CardInkShadow,
            TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(card.Min.X + pad, labelTop), label, CardInk, TextStyles.Headline);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(card.Min, card.Max, hovered);
    }

    private void DrawCategoryArt(ImDrawListPtr drawList, Rect card, int categoryIndex, StoreCategory category,
        float scale)
    {
        var slot = categoryIndex * CategoryArtCount;
        var front = categoryArt[slot];
        var pad = CategoryPad * scale;
        if (front is null)
        {
            AppSkin.Icon(drawList, new Vector2(card.Max.X - 36f * scale, card.Min.Y + 42f * scale),
                AppStoreCatalog.Icon(category).ToIconString(), Palette.WithAlpha(CardInk, 0.92f), 2f);
            return;
        }

        var frontSize = card.Height * 0.40f;
        var frontCenter = new Vector2(card.Max.X - pad - frontSize * 0.5f, card.Min.Y + pad + frontSize * 0.62f);
        for (var depth = CategoryArtCount - 1; depth > 0; depth--)
        {
            var behind = categoryArt[slot + depth];
            if (behind is null)
            {
                continue;
            }

            var shrink = 1f - depth * 0.20f;
            var center = new Vector2(frontCenter.X - frontSize * 0.52f * depth,
                frontCenter.Y - frontSize * 0.17f * depth);
            DrawIcon(drawList, center, frontSize * shrink, behind);
        }

        DrawIcon(drawList, frontCenter, frontSize, front);
    }

    private void DrawCategoryView(Rect area, StoreCategory category)
    {
        var scale = ImGuiHelpers.GlobalScale;
        DrawNavBar(area, Loc.T(AppStoreCatalog.Name(category)), scale);
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        using (AppSurface.Begin(body))
        {
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var section = new Rect(new Vector2(origin.X - Metrics.Space.Lg * scale, origin.Y),
                new Vector2(origin.X + width + Metrics.Space.Lg * scale, body.Max.Y));
            var entries = Collect(app => AppStoreCatalog.For(app.Id).Category == category);
            var top = entries.Count > 0
                ? DrawRowCard(section, origin.Y, entries, scale)
                : origin.Y;
            ImGui.SetCursorScreenPos(new Vector2(origin.X, top));
            ImGui.Dummy(new Vector2(width, Metrics.Space.Lg * scale));
        }
    }

    private void DrawSearchTab(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        DrawLargeTitle(area, Loc.T(L.Store.Search), null);
        var barTop = area.Min.Y + (HeaderHeight - 12f) * scale;
        var bar = new Rect(new Vector2(area.Min.X + Metrics.Space.Lg * scale, barTop),
            new Vector2(area.Max.X - Metrics.Space.Lg * scale, barTop + SearchHeight * scale));
        SearchField.Draw(bar, "##appstoreSearch", Loc.T(L.Store.SearchHint), ref search, ui.Palette);
        if (!string.Equals(search, lastSearch, StringComparison.Ordinal))
        {
            lastSearch = search;
            resetScroll = true;
        }

        var body = new Rect(new Vector2(area.Min.X, bar.Max.Y + Metrics.Space.Sm * scale), area.Max);
        using (var surface = AppSurface.Begin(body))
        {
            if (resetScroll)
            {
                surface.JumpToTop();
                resetScroll = false;
            }

            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var query = search.Trim();
            if (query.Length == 0)
            {
                return;
            }

            var matches = Collect(app => Matches(app.Id, app.DisplayName, query));
            if (matches.Count == 0)
            {
                Typography.DrawCentered(new Vector2(origin.X + width * 0.5f, origin.Y + 40f * scale),
                    Loc.T(L.Store.NoResults), ui.MutedInk, TextStyles.Body);
                return;
            }

            var section = new Rect(new Vector2(origin.X - Metrics.Space.Lg * scale, origin.Y),
                new Vector2(origin.X + width + Metrics.Space.Lg * scale, body.Max.Y));
            var top = DrawSection(section, origin.Y, Loc.T(L.Store.Apps), matches, scale);
            ImGui.SetCursorScreenPos(new Vector2(origin.X, top));
            ImGui.Dummy(new Vector2(width, Metrics.Space.Lg * scale));
        }
    }

    private static bool Matches(string appId, string displayName, string query)
    {
        if (query.Length == 0)
        {
            return false;
        }

        if (displayName.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
            appId.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var entry = AppStoreCatalog.For(appId);
        return Loc.T(entry.Subtitle).Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
               Loc.T(AppStoreCatalog.Name(entry.Category)).Contains(query, StringComparison.CurrentCultureIgnoreCase);
    }
}
