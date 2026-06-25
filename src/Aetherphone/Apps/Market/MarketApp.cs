using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Market;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Market;

internal sealed class MarketApp : IPhoneApp
{
    private const float ScopeBarHeight = 34f;
    private const float SearchHeight = 46f;
    private const float QualityRowHeight = 34f;
    private const int MaxResults = 50;
    private const int MaxRecents = 12;
    private const int MaxRowsPerSection = 12;

    public string Id => "market";

    public string DisplayName => "Market";

    public string Glyph => "$";

    public Vector4 Accent => new(0.95f, 0.74f, 0.26f, 1f);

    public int BadgeCount => alerts.TriggeredCount;

    private readonly MarketboardService market;
    private readonly MarketItemIndex index;
    private readonly MarketAlertService alerts;
    private readonly MarketLauncher launcher;
    private readonly GameData gameData;
    private readonly ITextureProvider textures;
    private readonly Configuration configuration;

    private readonly ViewRouter<MarketView?> router;
    private readonly RouterDraw<MarketView?> drawView;
    private readonly Action backToList;

    private readonly List<MarketScope> scopes = new();
    private readonly List<MarketItemRef> results = new();
    private readonly List<MarketItemRef> sectionBuffer = new();
    private readonly List<uint> prefetchBuffer = new();
    private readonly List<MarketAlert> alertBuffer = new();

    private int scopeIndex = -1;
    private bool showHq;
    private string search = string.Empty;
    private string lastSearch = " ";
    private bool lastIndexReady;

    private uint pendingOpenId;
    private MarketItemRef lastHovered;
    private bool hasHovered;
    private bool showAlertEditor;
    private int alertThreshold = 1;
    private bool alertBelow = true;

    private PhoneTheme frameTheme = PhoneTheme.Default;
    private INavigator frameNavigation = null!;

    public MarketApp(MarketboardService market, MarketItemIndex index, MarketAlertService alerts, MarketLauncher launcher, GameData gameData, ITextureProvider textures, Configuration configuration)
    {
        this.market = market;
        this.index = index;
        this.alerts = alerts;
        this.launcher = launcher;
        this.gameData = gameData;
        this.textures = textures;
        this.configuration = configuration;

        router = new ViewRouter<MarketView?>(null);
        drawView = DrawView;
        backToList = () => router.Pop();
    }

    public void OnOpened()
    {
        router.Reset();
        search = string.Empty;
        lastSearch = " ";
        showHq = configuration.MarketHqOnly;
        showAlertEditor = false;
        alerts.Acknowledge();
        index.EnsureBuilt();
        RebuildScopes();
    }

    public void OnClosed()
    {
        router.Reset();
        search = string.Empty;
    }

    private void RebuildScopes()
    {
        MarketScopes.Build(scopes, gameData);
        scopeIndex = MarketScopes.IndexOfKind(scopes, configuration.MarketScope);
    }

    private MarketScope CurrentScope => scopeIndex >= 0 && scopeIndex < scopes.Count ? scopes[scopeIndex] : MarketScope.None;

    public void Draw(in PhoneContext context)
    {
        index.EnsureBuilt();
        if (scopes.Count == 0)
        {
            RebuildScopes();
        }

        frameTheme = context.Theme;
        frameNavigation = context.Navigation;

        if (launcher.TryConsume(out var requestedItem, out var requestedSearch))
        {
            if (requestedItem != 0)
            {
                pendingOpenId = requestedItem;
            }
            else if (requestedSearch is not null)
            {
                router.Reset();
                search = requestedSearch;
                lastSearch = "";
            }
        }

        if (pendingOpenId != 0 && index.Ready)
        {
            if (index.TryGet(pendingOpenId, out var pending))
            {
                router.Reset();
                OpenItem(pending);
            }

            pendingOpenId = 0;
        }

        router.Draw(context.Content, context.Theme.AppBackground, ImGui.GetIO().DeltaTime, drawView);
    }

    private void DrawView(MarketView? view, Rect area, int depth)
    {
        if (view is { } item)
        {
            DrawDetail(area, item);
        }
        else
        {
            DrawRoot(area);
        }
    }

    private void DrawRoot(Rect area)
    {
        UpdateHovered();

        var context = new PhoneContext(area, frameTheme, frameNavigation);
        AppHeader.Draw(context, DisplayName);

        var scale = ImGuiHelpers.GlobalScale;
        var pad = 16f * scale;
        var top = area.Min.Y + AppHeader.Height * scale;

        var scopeBar = new Rect(new Vector2(area.Min.X + pad, top), new Vector2(area.Max.X - pad, top + ScopeBarHeight * scale));
        DrawScopeBar(scopeBar);

        var searchTop = scopeBar.Max.Y + 2f * scale;
        var searchBar = new Rect(new Vector2(area.Min.X + pad, searchTop), new Vector2(area.Max.X - pad, searchTop + SearchHeight * scale));
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

    private void DrawResults(Rect body)
    {
        var scope = CurrentScope;
        if (!index.Ready)
        {
            CenteredHint(body, "Loading item list…");
            return;
        }

        if (results.Count == 0)
        {
            CenteredHint(body, "No matching items");
            return;
        }

        prefetchBuffer.Clear();
        for (var resultIndex = 0; resultIndex < results.Count; resultIndex++)
        {
            prefetchBuffer.Add(results[resultIndex].Id);
        }

        market.PrefetchAggregated(prefetchBuffer, scope);

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
            CenteredHint(body, "Loading item list…");
            return;
        }

        var favorites = configuration.MarketFavorites;
        var recents = configuration.MarketRecents;
        var scope = CurrentScope;

        alerts.CopyInto(alertBuffer);

        if (!hasHovered && alertBuffer.Count == 0 && favorites.Count == 0 && recents.Count == 0)
        {
            CenteredHint(body, "Search for an item, or right-click any item in-game.");
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

        DrawItemIdSection("Favorites", favorites, scope);
        DrawItemIdSection("Recent", recents, scope);
    }

    private void DrawHoveredSection(MarketScope scope)
    {
        SettingsSection.Header("Hovered in-game", frameTheme);
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
        SettingsSection.Header("Alerts", frameTheme);
        var card = GroupCard.Begin(frameTheme, alertBuffer.Count, MarketRowViews.DataRowHeight);
        for (var alertIndex = 0; alertIndex < alertBuffer.Count; alertIndex++)
        {
            var action = MarketRowViews.AlertRow(card.NextRow(), alertBuffer[alertIndex], textures, frameTheme);
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

        SettingsSection.Header(title, frameTheme);
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

    private void DrawDetail(Rect area, MarketView view)
    {
        var context = new PhoneContext(area, frameTheme, frameNavigation);
        AppHeader.Draw(context, MarketFormat.Clip(view.Name, 18), backToList);
        DrawHeaderButtons(area, view, out var forceRefresh);

        var scale = ImGuiHelpers.GlobalScale;
        var pad = 16f * scale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var scope = CurrentScope;

        if (!scope.IsValid)
        {
            Typography.DrawCentered(area.Center, "Log in to view market prices", frameTheme.TextMuted);
            return;
        }

        var entry = market.RequestItem(view.ItemId, scope, forceRefresh);
        var snapshot = entry.Snapshot;

        var scopeBar = new Rect(new Vector2(area.Min.X + pad, top), new Vector2(area.Max.X - pad, top + ScopeBarHeight * scale));
        DrawScopeBar(scopeBar);

        var hasHq = snapshot is { HasHq: true };
        var bodyTop = scopeBar.Max.Y;
        if (hasHq)
        {
            var qualityRow = new Rect(new Vector2(area.Min.X + pad, bodyTop), new Vector2(area.Max.X - pad, bodyTop + QualityRowHeight * scale));
            DrawQualityToggle(qualityRow);
            bodyTop = qualityRow.Max.Y;
        }

        var effectiveHq = hasHq && showHq;
        var body = new Rect(new Vector2(area.Min.X, bodyTop), area.Max);

        if (snapshot is null)
        {
            var message = entry.State == MarketState.Failed ? "Couldn't reach Universalis" : "Loading…";
            Typography.DrawCentered(new Vector2(area.Center.X, body.Center.Y), message, frameTheme.TextMuted);
            return;
        }

        using (AppSurface.Begin(body))
        {
            DrawHero(view, snapshot, effectiveHq);
            DrawPrices(snapshot, effectiveHq);
            DrawAlertEditor(view, snapshot, effectiveHq, scope);
            DrawTrendSection(snapshot, effectiveHq);
            DrawListingsSection(snapshot, effectiveHq);
            DrawSalesSection(snapshot, effectiveHq);
        }
    }

    private void DrawHero(MarketView view, MarketSnapshot snapshot, bool hq)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();

        var iconSize = 44f * scale;
        var iconMin = new Vector2(origin.X, origin.Y + 4f * scale);
        var iconMax = iconMin + new Vector2(iconSize, iconSize);
        if (view.IconId != 0)
        {
            var texture = textures.GetFromGameIcon(new GameIconLookup(view.IconId)).GetWrapOrEmpty();
            drawList.AddImageRounded(texture.Handle, iconMin, iconMax, Vector2.Zero, Vector2.One, 0xFFFFFFFFu, 8f * scale);
        }

        var textX = iconMax.X + 12f * scale;
        Typography.Draw(new Vector2(textX, iconMin.Y + 2f * scale), MarketFormat.Clip(view.Name, 16), frameTheme.TextStrong, 1.0f);
        Typography.Draw(new Vector2(textX, iconMin.Y + 24f * scale), hq ? "Cheapest HQ" : "Cheapest", frameTheme.TextMuted, 0.82f);

        var min = snapshot.Min(hq);
        var priceText = min > 0 ? MarketFormat.Gil(min) : "—";
        var priceSize = Typography.Measure(priceText, 1.5f, FontWeight.SemiBold);
        Typography.Draw(new Vector2(origin.X + width - priceSize.X, iconMin.Y + 4f * scale), priceText, frameTheme.Accent, 1.5f, FontWeight.SemiBold);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, iconSize + 12f * scale));
    }

    private void DrawPrices(MarketSnapshot snapshot, bool hq)
    {
        var hasVendor = index.TryGet(snapshot.ItemId, out var itemRef) && itemRef.VendorPrice > 0;
        var rowCount = hasVendor ? 6 : 5;

        SettingsSection.Header("Prices", frameTheme);
        var card = GroupCard.Begin(frameTheme, rowCount);
        SettingsRow.Info(card.NextRow(), "Average", PriceOrDash(snapshot.Average(hq)), frameTheme);
        SettingsRow.Info(card.NextRow(), "Highest", PriceOrDash(snapshot.Max(hq)), frameTheme);
        SettingsRow.Info(card.NextRow(), "Sales / day", MarketFormat.Velocity(snapshot.Velocity(hq)), frameTheme);
        SettingsRow.Info(card.NextRow(), "Up / sold", $"{snapshot.UnitsForSale} / {snapshot.UnitsSold}", frameTheme);
        SettingsRow.Info(card.NextRow(), "Updated", MarketFormat.Ago(snapshot.LastUpload), frameTheme);
        if (hasVendor)
        {
            var marketMin = snapshot.Min(hq);
            var value = MarketFormat.Gil(itemRef.VendorPrice);
            if (marketMin > 0 && itemRef.VendorPrice < marketMin)
            {
                value += "  ·  cheaper";
            }

            SettingsRow.Info(card.NextRow(), "Vendor (NPC)", value, frameTheme);
        }

        card.End();
    }

    private void DrawAlertEditor(MarketView view, MarketSnapshot snapshot, bool hq, MarketScope scope)
    {
        SettingsSection.Header("Price alert", frameTheme);
        var card = GroupCard.Begin(frameTheme, 1);
        var existing = alerts.HasAlertFor(view.ItemId);
        var label = showAlertEditor ? "Cancel" : existing ? "Add another alert" : "Set a price alert";
        if (SettingsRow.Link(card.NextRow(), "!", frameTheme.Accent, label, string.Empty, frameTheme))
        {
            showAlertEditor = !showAlertEditor;
            if (showAlertEditor)
            {
                var min = snapshot.Min(hq);
                alertThreshold = (int)Math.Clamp(min > 0 ? min : 1, 1, int.MaxValue);
                alertBelow = true;
            }
        }

        card.End();

        if (!showAlertEditor)
        {
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        ImGui.Dummy(new Vector2(0f, 6f * scale));

        ImGui.SetNextItemWidth(170f * scale);
        ImGui.InputInt("gil##marketAlertThreshold", ref alertThreshold);
        if (alertThreshold < 1)
        {
            alertThreshold = 1;
        }

        ImGui.Dummy(new Vector2(0f, 4f * scale));

        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var toggleWidth = 190f * scale;
        var trackMin = origin;
        var trackMax = new Vector2(origin.X + toggleWidth, origin.Y + 26f * scale);
        ImGui.GetWindowDrawList().AddRectFilled(trackMin, trackMax, ImGui.GetColorU32(frameTheme.GroupedCard), (trackMax.Y - trackMin.Y) * 0.5f);
        var middle = (trackMin.X + trackMax.X) * 0.5f;
        if (DrawSegment(new Rect(trackMin, new Vector2(middle, trackMax.Y)), "At or below", alertBelow))
        {
            alertBelow = true;
        }

        if (DrawSegment(new Rect(new Vector2(middle, trackMin.Y), trackMax), "At or above", !alertBelow))
        {
            alertBelow = false;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, 26f * scale));

        ImGui.Dummy(new Vector2(0f, 4f * scale));
        if (ImGui.Button("Create alert"))
        {
            alerts.Add(new MarketAlert
            {
                ItemId = view.ItemId,
                ItemName = view.Name,
                IconId = view.IconId,
                ScopeKind = scope.Kind,
                ScopeName = scope.ApiName,
                HqOnly = hq,
                Threshold = alertThreshold,
                Below = alertBelow,
                Enabled = true,
            });
            showAlertEditor = false;
        }
    }

    private void DrawTrendSection(MarketSnapshot snapshot, bool hq)
    {
        var sales = snapshot.Sales;
        var count = CountQuality(sales, hq);
        if (count < 2)
        {
            return;
        }

        SettingsSection.Header("Trend", frameTheme);
        var scale = ImGuiHelpers.GlobalScale;
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var height = 60f * scale;
        var graph = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));

        Span<float> values = count <= 64 ? stackalloc float[count] : new float[count];
        var cursor = 0;
        for (var saleIndex = sales.Length - 1; saleIndex >= 0; saleIndex--)
        {
            if (sales[saleIndex].Hq == hq)
            {
                values[cursor++] = sales[saleIndex].PricePerUnit;
            }
        }

        Sparkline.Draw(graph, values, frameTheme.Accent, Palette.WithAlpha(frameTheme.Accent, 0.18f));

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawListingsSection(MarketSnapshot snapshot, bool hq)
    {
        var listings = snapshot.Listings;
        var count = CountListings(listings, hq);
        SettingsSection.Header(count > 0 ? $"Listings · {count}" : "Listings", frameTheme);

        if (count == 0)
        {
            DrawEmptyCard(hq ? "No HQ listings" : "No listings");
            return;
        }

        var shown = Math.Min(count, MaxRowsPerSection);
        var card = GroupCard.Begin(frameTheme, shown, MarketRowViews.DataRowHeight);
        var drawn = 0;
        for (var listingIndex = 0; listingIndex < listings.Length && drawn < shown; listingIndex++)
        {
            if (listings[listingIndex].Hq != hq)
            {
                continue;
            }

            MarketRowViews.ListingRow(card.NextRow(), listings[listingIndex], snapshot.MultiWorld, frameTheme);
            drawn++;
        }

        card.End();
    }

    private void DrawSalesSection(MarketSnapshot snapshot, bool hq)
    {
        var sales = snapshot.Sales;
        var count = CountQuality(sales, hq);
        SettingsSection.Header(count > 0 ? $"Recent sales · {count}" : "Recent sales", frameTheme);

        if (count == 0)
        {
            DrawEmptyCard(hq ? "No HQ sales" : "No recent sales");
            return;
        }

        var shown = Math.Min(count, MaxRowsPerSection);
        var card = GroupCard.Begin(frameTheme, shown, MarketRowViews.DataRowHeight);
        var drawn = 0;
        for (var saleIndex = 0; saleIndex < sales.Length && drawn < shown; saleIndex++)
        {
            if (sales[saleIndex].Hq != hq)
            {
                continue;
            }

            MarketRowViews.SaleRow(card.NextRow(), sales[saleIndex], snapshot.MultiWorld, frameTheme);
            drawn++;
        }

        card.End();
    }

    private void DrawEmptyCard(string message)
    {
        var card = GroupCard.Begin(frameTheme, 1);
        var row = card.NextRow();
        var size = Typography.Measure(message);
        Typography.Draw(new Vector2(row.Center.X - size.X * 0.5f, row.Center.Y - size.Y * 0.5f), message, frameTheme.TextMuted);
        card.End();
    }

    private void DrawSearch(Rect bar)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var pillMin = new Vector2(bar.Min.X, bar.Min.Y + 6f * scale);
        var pillMax = new Vector2(bar.Max.X, bar.Max.Y - 6f * scale);
        drawList.AddRectFilled(pillMin, pillMax, ImGui.GetColorU32(frameTheme.GroupedCard), (pillMax.Y - pillMin.Y) * 0.5f);

        ImGui.SetCursorScreenPos(new Vector2(pillMin.X + 14f * scale, (pillMin.Y + pillMax.Y) * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(pillMax.X - pillMin.X - 28f * scale);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, frameTheme.TextStrong))
        {
            ImGui.InputTextWithHint("##marketSearch", "Search items", ref search, 100, ImGuiInputTextFlags.None);
        }
    }

    private void DrawScopeBar(Rect bar)
    {
        if (scopes.Count == 0)
        {
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var trackMin = new Vector2(bar.Min.X, bar.Center.Y - 13f * scale);
        var trackMax = new Vector2(bar.Max.X, bar.Center.Y + 13f * scale);
        drawList.AddRectFilled(trackMin, trackMax, ImGui.GetColorU32(frameTheme.GroupedCard), (trackMax.Y - trackMin.Y) * 0.5f);

        var segmentWidth = (trackMax.X - trackMin.X) / scopes.Count;
        for (var scopeIdx = 0; scopeIdx < scopes.Count; scopeIdx++)
        {
            var segMin = new Vector2(trackMin.X + segmentWidth * scopeIdx, trackMin.Y);
            var segMax = new Vector2(segMin.X + segmentWidth, trackMax.Y);
            var label = MarketFormat.Clip(scopes[scopeIdx].ApiName, 11);
            if (DrawSegment(new Rect(segMin, segMax), label, scopeIdx == scopeIndex))
            {
                SetScope(scopeIdx);
            }
        }
    }

    private void DrawQualityToggle(Rect bar)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var width = 120f * scale;
        var trackMin = new Vector2(bar.Min.X, bar.Center.Y - 12f * scale);
        var trackMax = new Vector2(bar.Min.X + width, bar.Center.Y + 12f * scale);
        drawList.AddRectFilled(trackMin, trackMax, ImGui.GetColorU32(frameTheme.GroupedCard), (trackMax.Y - trackMin.Y) * 0.5f);

        var middle = (trackMin.X + trackMax.X) * 0.5f;
        if (DrawSegment(new Rect(trackMin, new Vector2(middle, trackMax.Y)), "NQ", !showHq))
        {
            SetQuality(false);
        }

        if (DrawSegment(new Rect(new Vector2(middle, trackMin.Y), trackMax), "HQ", showHq))
        {
            SetQuality(true);
        }
    }

    private bool DrawSegment(Rect segment, string label, bool selected)
    {
        var scale = ImGuiHelpers.GlobalScale;
        if (selected)
        {
            var drawList = ImGui.GetWindowDrawList();
            var inset = 2f * scale;
            var min = new Vector2(segment.Min.X + inset, segment.Min.Y + inset);
            var max = new Vector2(segment.Max.X - inset, segment.Max.Y - inset);
            drawList.AddRectFilled(min, max, ImGui.GetColorU32(frameTheme.Accent), (max.Y - min.Y) * 0.5f);
        }

        Typography.DrawCentered(segment.Center, label, selected ? frameTheme.TextStrong : frameTheme.TextMuted, 0.82f);

        var hovered = ImGui.IsMouseHoveringRect(segment.Min, segment.Max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && !selected && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private void DrawHeaderButtons(Rect area, MarketView view, out bool forceRefresh)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var midY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        var starCenter = new Vector2(area.Max.X - 18f * scale, midY);
        var refreshCenter = new Vector2(area.Max.X - 46f * scale, midY);

        var favorite = IsFavorite(view.ItemId);
        if (IconButton(starCenter, FontAwesomeIcon.Star, favorite ? frameTheme.Accent : frameTheme.TextMuted))
        {
            ToggleFavorite(view.ItemId);
        }

        forceRefresh = IconButton(refreshCenter, FontAwesomeIcon.Sync, frameTheme.TextMuted);
    }

    private bool IconButton(Vector2 center, FontAwesomeIcon icon, Vector4 color)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var box = 14f * scale;
        var hovered = ImGui.IsMouseHoveringRect(center - new Vector2(box, box), center + new Vector2(box, box));

        var glyph = icon.ToIconString();
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var size = ImGui.CalcTextSize(glyph);
            ImGui.SetCursorScreenPos(center - size * 0.5f);
            using (ImRaii.PushColor(ImGuiCol.Text, hovered ? frameTheme.TextStrong : color))
            {
                ImGui.TextUnformatted(glyph);
            }
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
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
        Typography.DrawCentered(new Vector2(body.Center.X, body.Min.Y + 70f * scale), message, frameTheme.TextMuted);
    }

    private void OpenItem(MarketItemRef item)
    {
        PushRecent(item.Id);
        router.Push(new MarketView(item.Id, item.Name, item.IconId));
    }

    private void PushRecent(uint id)
    {
        var recents = configuration.MarketRecents;
        recents.Remove(id);
        recents.Insert(0, id);
        while (recents.Count > MaxRecents)
        {
            recents.RemoveAt(recents.Count - 1);
        }

        configuration.Save();
    }

    private bool IsFavorite(uint id) => configuration.MarketFavorites.Contains(id);

    private void ToggleFavorite(uint id)
    {
        var favorites = configuration.MarketFavorites;
        if (!favorites.Remove(id))
        {
            favorites.Add(id);
        }

        configuration.Save();
    }

    private void SetScope(int newIndex)
    {
        scopeIndex = newIndex;
        configuration.MarketScope = scopes[newIndex].Kind;
        configuration.Save();
    }

    private void SetQuality(bool hq)
    {
        showHq = hq;
        configuration.MarketHqOnly = hq;
        configuration.Save();
    }

    private static int CountListings(MarketListing[] listings, bool hq)
    {
        var count = 0;
        for (var index = 0; index < listings.Length; index++)
        {
            if (listings[index].Hq == hq)
            {
                count++;
            }
        }

        return count;
    }

    private static int CountQuality(MarketSale[] sales, bool hq)
    {
        var count = 0;
        for (var index = 0; index < sales.Length; index++)
        {
            if (sales[index].Hq == hq)
            {
                count++;
            }
        }

        return count;
    }

    private static string PriceOrDash(double value) => value > 0 ? MarketFormat.Gil(value) : "—";

    private static string PriceOrDash(long value) => value > 0 ? MarketFormat.Gil(value) : "—";

    public void Dispose()
    {
    }
}
