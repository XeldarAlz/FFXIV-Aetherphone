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
    private const float CategoryCardHeight = 106f;
    private const float CategoryGap = 12f;

    private void DrawCatalogTab(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        DrawLargeTitle(area, Loc.T(L.Store.Apps), null);
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + (HeaderHeight - 18f) * scale), area.Max);
        using (AppSurface.Begin(body))
        {
            if (resetScroll)
            {
                ImGui.SetScrollY(0f);
                resetScroll = false;
            }

            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            Typography.Draw(new Vector2(origin.X, origin.Y), Loc.T(L.Store.BrowseCategories), ui.TitleInk,
                TextStyles.Title3);
            var top = origin.Y + 30f * scale;
            var cardWidth = (width - CategoryGap * scale) * 0.5f;
            for (var index = 0; index < AppStoreCatalog.Order.Length; index++)
            {
                var column = index % 2;
                var row = index / 2;
                var min = new Vector2(origin.X + column * (cardWidth + CategoryGap * scale),
                    top + row * (CategoryCardHeight + CategoryGap) * scale);
                var card = new Rect(min, new Vector2(min.X + cardWidth, min.Y + CategoryCardHeight * scale));
                if (DrawCategoryCard(card, AppStoreCatalog.Order[index], scale))
                {
                    router.Push(StoreView.ForCategory(AppStoreCatalog.Order[index]));
                }
            }

            var rows = (AppStoreCatalog.Order.Length + 1) / 2;
            top += rows * (CategoryCardHeight + CategoryGap) * scale;
            ImGui.SetCursorScreenPos(new Vector2(origin.X, top));
            ImGui.Dummy(new Vector2(width, Metrics.Space.Lg * scale));
        }
    }

    private bool DrawCategoryCard(Rect card, StoreCategory category, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(card.Min, card.Max);
        var tint = AppStoreCatalog.Tint(category);
        var rounding = Metrics.Radius.Card * scale;
        var lift = hovered ? 0.06f : 0f;
        Elevation.Floating(drawList, card.Min, card.Max, rounding, scale, hovered ? 0.7f : 0.45f);
        Squircle.FillVerticalGradient(drawList, card.Min, card.Max, rounding,
            ImGui.GetColorU32(Palette.Lighten(tint, 0.18f + lift)),
            ImGui.GetColorU32(Palette.Darken(tint, 0.22f)));
        Squircle.Stroke(drawList, card.Min, card.Max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(Palette.Lighten(tint, 0.5f), 0.28f)), 1f * scale);
        var glyphCenter = new Vector2(card.Max.X - 34f * scale, card.Min.Y + 40f * scale);
        AppSkin.Icon(drawList, glyphCenter, AppStoreCatalog.Icon(category).ToIconString(),
            new Vector4(1f, 1f, 1f, 0.92f), 1.9f);
        var label = Typography.FitText(Loc.T(AppStoreCatalog.Name(category)), card.Width - 20f * scale,
            TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(card.Min.X + 12f * scale, card.Max.Y - 26f * scale), label,
            new Vector4(1f, 1f, 1f, 1f), TextStyles.Headline);
        if (!hovered)
        {
            return false;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return ImGui.IsMouseClicked(ImGuiMouseButton.Left);
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
        SearchField.Draw(bar, "appstore.search", Loc.T(L.Store.SearchHint), ref search, ui.Palette);
        if (!string.Equals(search, lastSearch, StringComparison.Ordinal))
        {
            lastSearch = search;
            resetScroll = true;
        }

        var body = new Rect(new Vector2(area.Min.X, bar.Max.Y + Metrics.Space.Sm * scale), area.Max);
        using (AppSurface.Begin(body))
        {
            if (resetScroll)
            {
                ImGui.SetScrollY(0f);
                resetScroll = false;
            }

            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var query = search.Trim();
            var matches = Collect(app => Matches(app.Id, app.DisplayName, query));
            if (matches.Count == 0)
            {
                Typography.DrawCentered(new Vector2(origin.X + width * 0.5f, origin.Y + 40f * scale),
                    Loc.T(query.Length == 0 ? L.Store.SearchHint : L.Store.NoResults), ui.MutedInk, TextStyles.Body);
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
