using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Market;
using Aetherphone.Core.Onboarding;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Market;

internal sealed partial class MarketApp
{
    private void DrawRoot(Rect area)
    {
        UpdateHovered();
        var scale = ImGuiHelpers.GlobalScale;
        DrawRootTopBar(area, scale);
        var top = area.Min.Y + AppHeader.Height * scale;
        var scopeBar = new Rect(new Vector2(area.Min.X, top), new Vector2(area.Max.X, top + ScopeBarHeight * scale));
        UiAnchors.Report("market.scope", scopeBar);
        DrawBrandedScopeBar(scopeBar);
        var searchTop = scopeBar.Max.Y + 2f * scale;
        var searchBar = new Rect(new Vector2(area.Min.X + 16f * scale, searchTop),
            new Vector2(area.Max.X - 16f * scale, searchTop + SearchHeight * scale));
        UiAnchors.Report("market.search", searchBar);
        DrawSearch(searchBar);
        var query = search.Trim();
        if (!string.Equals(query, lastSearch, StringComparison.Ordinal) || (index.Ready && !lastIndexReady))
        {
            index.Search(search, results, MaxResults);
            lastSearch = query;
        }

        lastIndexReady = index.Ready;
        var body = new Rect(new Vector2(area.Min.X, searchBar.Max.Y), area.Max);
        using (AppSurface.Begin(body))
        {
            if (query.Length > 0)
            {
                DrawResults(body);
            }
            else
            {
                DrawDefault(body);
            }
        }
    }

    private void DrawRootTopBar(Rect area, float scale)
    {
        var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        var displaySize = Typography.Measure(DisplayName, 1.3f, FontWeight.Bold);
        Typography.Draw(new Vector2(area.Min.X + 16f * scale, rowCenterY - displaySize.Y * 0.5f), DisplayName,
            AppPalettes.Market.TitleInk, 1.3f, FontWeight.Bold);
    }

    private void DrawResults(Rect body)
    {
        var scope = CurrentScope;
        if (!index.Ready)
        {
            CenteredLoading(body, Loc.T(L.Market.LoadingItemList));
            return;
        }

        if (results.Count == 0)
        {
            CenteredHint(body, Loc.T(L.Market.NoMatchingItems));
            return;
        }

        prefetchBuffer.Clear();
        for (var resultIndex = 0; resultIndex < results.Count; resultIndex++)
        {
            prefetchBuffer.Add(results[resultIndex].Id);
        }

        market.PrefetchAggregated(prefetchBuffer, scope);
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.Dummy(new Vector2(0f, 4f * scale));
        var card = GroupCard.Begin(frameTheme, results.Count, MarketRowViews.ItemRowHeight);
        for (var resultIndex = 0; resultIndex < results.Count; resultIndex++)
        {
            var price = market.AggregatedMin(results[resultIndex].Id, scope);
            if (MarketRowViews.ItemRow(card.NextRow(), results[resultIndex], price, textures, frameTheme))
            {
                OpenItem(results[resultIndex]);
            }
        }

        card.End();
    }

    private void DrawDefault(Rect body)
    {
        if (!index.Ready)
        {
            CenteredLoading(body, Loc.T(L.Market.LoadingItemList));
            return;
        }

        var favorites = configuration.MarketFavorites;
        var recents = configuration.MarketRecents;
        var scope = CurrentScope;
        alerts.CopyInto(alertBuffer);
        if (!hasHovered && alertBuffer.Count == 0 && favorites.Count == 0 && recents.Count == 0)
        {
            CenteredHint(body, Loc.T(L.Market.SearchHint));
            return;
        }

        prefetchBuffer.Clear();
        if (hasHovered)
        {
            prefetchBuffer.Add(lastHovered.Id);
        }

        for (var favoriteIndex = 0; favoriteIndex < favorites.Count; favoriteIndex++)
        {
            prefetchBuffer.Add(favorites[favoriteIndex]);
        }

        for (var recentIndex = 0; recentIndex < recents.Count; recentIndex++)
        {
            prefetchBuffer.Add(recents[recentIndex]);
        }

        market.PrefetchAggregated(prefetchBuffer, scope);
        if (hasHovered)
        {
            DrawHoveredSection(scope);
        }

        if (alertBuffer.Count > 0)
        {
            DrawAlertsSection();
        }

        DrawItemIdSection(Loc.T(L.Market.Favorites), favorites, scope);
        DrawItemIdSection(Loc.T(L.Market.Recent), recents, scope);
    }

    private void DrawHoveredSection(MarketScope scope)
    {
        ui.SectionHeading(Loc.T(L.Market.HoveredInGame), 14f);
        var card = GroupCard.Begin(frameTheme, 1, MarketRowViews.ItemRowHeight);
        var price = market.AggregatedMin(lastHovered.Id, scope);
        if (MarketRowViews.ItemRow(card.NextRow(), lastHovered, price, textures, frameTheme))
        {
            OpenItem(lastHovered);
        }

        card.End();
    }

    private void DrawAlertsSection()
    {
        ui.SectionHeading(Loc.T(L.Common.Alerts), 14f);
        var card = GroupCard.Begin(frameTheme, alertBuffer.Count, MarketRowViews.DataRowHeight);
        for (var alertIndex = 0; alertIndex < alertBuffer.Count; alertIndex++)
        {
            var action = MarketRowViews.AlertRow(card.NextRow(), alertBuffer[alertIndex], alertIndex, textures,
                frameTheme);
            if (action == MarketRowAction.Open)
            {
                if (index.TryGet(alertBuffer[alertIndex].ItemId, out var item))
                {
                    OpenItem(item);
                }
            }
            else if (action == MarketRowAction.Delete)
            {
                alerts.Remove(alertBuffer[alertIndex]);
            }
        }

        card.End();
    }

    private void DrawItemIdSection(string title, List<uint> ids, MarketScope scope)
    {
        sectionBuffer.Clear();
        for (var idIndex = 0; idIndex < ids.Count; idIndex++)
        {
            if (index.TryGet(ids[idIndex], out var item))
            {
                sectionBuffer.Add(item);
            }
        }

        if (sectionBuffer.Count == 0)
        {
            return;
        }

        ui.SectionHeading(title, 14f);
        var card = GroupCard.Begin(frameTheme, sectionBuffer.Count, MarketRowViews.ItemRowHeight);
        for (var bufferIndex = 0; bufferIndex < sectionBuffer.Count; bufferIndex++)
        {
            var price = market.AggregatedMin(sectionBuffer[bufferIndex].Id, scope);
            if (MarketRowViews.ItemRow(card.NextRow(), sectionBuffer[bufferIndex], price, textures, frameTheme))
            {
                OpenItem(sectionBuffer[bufferIndex]);
            }
        }

        card.End();
    }

    private void DrawSearch(Rect bar)
    {
        SearchField.Draw(bar, "##marketSearch", Loc.T(L.Market.SearchItems), ref search, frameTheme);
    }

    private void UpdateHovered()
    {
        var hovered = Plugin.GameGui.HoveredItem;
        if (hovered == 0)
        {
            return;
        }

        var id = (uint)(hovered % 1_000_000);
        if (id == 0 || id == lastHovered.Id)
        {
            return;
        }

        if (index.TryGet(id, out var item))
        {
            lastHovered = item;
            hasHovered = true;
        }
    }

    private void CenteredHint(Rect body, string message)
    {
        var scale = ImGuiHelpers.GlobalScale;
        Typography.DrawCentered(new Vector2(body.Center.X, body.Min.Y + 70f * scale), message, AppPalettes.Market.MutedInk);
    }

    private void CenteredLoading(Rect body, string message)
    {
        var scale = ImGuiHelpers.GlobalScale;
        LoadingPulse.Draw(new Vector2(body.Center.X, body.Min.Y + 60f * scale), 13f * scale, AppPalettes.Market.Accent,
            AppPalettes.Market.MutedInk, message);
    }
}
