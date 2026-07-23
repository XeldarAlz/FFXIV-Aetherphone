using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.AppStore;

internal sealed partial class AppStoreApp
{
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
            var section = new Rect(new Vector2(origin.X - Metrics.Space.Lg * scale, origin.Y),
                new Vector2(origin.X + width + Metrics.Space.Lg * scale, body.Max.Y));
            var top = origin.Y;
            var pending = Collect(app => !installer.IsInstalled(app.Id));
            if (pending.Count > 0)
            {
                top = DrawSection(section, top, Loc.T(L.Store.NotInstalled), pending, scale);
            }

            for (var index = 0; index < AppStoreCatalog.Order.Length; index++)
            {
                var category = AppStoreCatalog.Order[index];
                var entries = Collect(app => AppStoreCatalog.For(app.Id).Category == category);
                top = DrawSection(section, top, Loc.T(AppStoreCatalog.Name(category)), entries, scale);
            }

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
