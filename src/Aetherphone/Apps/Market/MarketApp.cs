using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Analytics;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
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
    private const float ScopeBarHeight = 38f;
    private const float SearchReportDelaySeconds = 1f;
    private const float SearchHeight = 46f;
    private const int MaxResults = 50;
    private const int MaxRecents = 12;
    private const int MaxRowsPerSection = 12;
    public string Id => "market";
    public string DisplayName => Loc.T(L.Apps.Market);
    public string Glyph => "$";
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
    private readonly List<string> scopeLabels = new();
    private readonly string[] alertDirLabels = new string[2];
    private int scopeIndex = -1;
    private bool showHq;
    private string search = string.Empty;
    private string lastSearch = " ";
    private string settledQuery = string.Empty;
    private string reportedSearch = string.Empty;
    private float searchSettleSeconds;
    private bool lastIndexReady;
    private uint pendingOpenId;
    private MarketItemRef lastHovered;
    private bool hasHovered;
    private bool showAlertEditor;
    private int alertThreshold = 1;
    private bool alertBelow = true;
    private PhoneTheme frameTheme = PhoneTheme.Default;
    private INavigator frameNavigation = null!;
    private readonly AppSkin ui = new(AppPalettes.Market);

    public MarketApp(MarketboardService market, MarketItemIndex index, MarketAlertService alerts,
        MarketLauncher launcher, GameData gameData, ITextureProvider textures, Configuration configuration)
    {
        this.market = market;
        this.index = index;
        this.alerts = alerts;
        this.launcher = launcher;
        this.gameData = gameData;
        this.textures = textures;
        this.configuration = configuration;
        router = new ViewRouter<MarketView?>(null, Id);
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

    private MarketScope CurrentScope =>
        scopeIndex >= 0 && scopeIndex < scopes.Count ? scopes[scopeIndex] : MarketScope.None;

    public void Draw(in PhoneContext context)
    {
        index.EnsureBuilt();
        if (scopes.Count == 0)
        {
            RebuildScopes();
        }

        frameTheme = context.Theme;
        frameNavigation = context.Navigation;
        ui.Theme = frameTheme;
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
                lastSearch = "\x0001";
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

        var screen = SceneChrome.ScreenFrom(context.Content, frameTheme, ImGuiHelpers.GlobalScale);
        ui.Backdrop(screen);
        router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
    }

    private void DrawView(MarketView? view, Rect area, int depth)
    {
        ui.Body(area);
        if (view is { } item)
        {
            DrawDetail(area, item);
        }
        else
        {
            DrawRoot(area);
        }
    }

    private void ReportSearch(string query)
    {
        if (!string.Equals(query, settledQuery, StringComparison.Ordinal))
        {
            settledQuery = query;
            searchSettleSeconds = 0f;
            return;
        }

        if (query.Length < 2 || string.Equals(query, reportedSearch, StringComparison.Ordinal))
        {
            return;
        }

        searchSettleSeconds += ImGui.GetIO().DeltaTime;
        if (searchSettleSeconds >= SearchReportDelaySeconds)
        {
            Plugin.Analytics.Track(AnalyticsEvents.MarketSearch());
            reportedSearch = query;
        }
    }

    private void DrawRoot(Rect area)
    {
        UpdateHovered();
        var scale = ImGuiHelpers.GlobalScale;
        DrawRootTopBar(area, scale);
        var top = area.Min.Y + AppHeader.Height * scale;
        var scopeBar = new Rect(new Vector2(area.Min.X, top), new Vector2(area.Max.X, top + ScopeBarHeight * scale));
        DrawBrandedScopeBar(scopeBar);
        var searchTop = scopeBar.Max.Y + 2f * scale;
        var searchBar = new Rect(new Vector2(area.Min.X + 16f * scale, searchTop),
            new Vector2(area.Max.X - 16f * scale, searchTop + SearchHeight * scale));
        DrawSearch(searchBar);
        var query = search.Trim();
        if (!string.Equals(query, lastSearch, StringComparison.Ordinal) || (index.Ready && !lastIndexReady))
        {
            index.Search(search, results, MaxResults);
            lastSearch = query;
        }

        lastIndexReady = index.Ready;
        ReportSearch(query);
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

    private void DrawBrandedScopeBar(Rect bar)
    {
        if (scopes.Count == 0)
        {
            return;
        }

        scopeLabels.Clear();
        for (var scopeIdx = 0; scopeIdx < scopes.Count; scopeIdx++)
        {
            scopeLabels.Add(MarketFormat.Clip(scopes[scopeIdx].ApiName, 11));
        }

        var newIndex = SegmentStrip.Draw("market.scope", bar, scopeLabels, scopeIndex, AppPalettes.Market);
        if (newIndex != scopeIndex && newIndex >= 0)
        {
            SetScope(newIndex);
        }
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

    private void DrawDetail(Rect area, MarketView view)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var context = new PhoneContext(area, frameTheme, frameNavigation);
        AppHeader.Draw(context, MarketFormat.Clip(view.Name, 18), backToList);
        DrawHeaderButtons(area, view, out var forceRefresh);
        var scope = CurrentScope;
        if (!scope.IsValid)
        {
            Typography.DrawCentered(area.Center, Loc.T(L.Market.LogInToViewPrices), frameTheme.TextMuted);
            return;
        }

        var entry = market.RequestItem(view.ItemId, scope, forceRefresh);
        var snapshot = entry.Snapshot;
        var top = area.Min.Y + AppHeader.Height * scale;
        var scopeBar = new Rect(new Vector2(area.Min.X, top), new Vector2(area.Max.X, top + ScopeBarHeight * scale));
        DrawBrandedScopeBar(scopeBar);
        var bodyTop = scopeBar.Max.Y + 4f * scale;
        if (snapshot is null)
        {
            if (entry.State == MarketState.Failed)
            {
                Typography.DrawCentered(new Vector2(area.Center.X, bodyTop + 60f * scale), Loc.T(L.Market.CouldntReach),
                    frameTheme.TextMuted);
            }
            else
            {
                LoadingPulse.Draw(new Vector2(area.Center.X, bodyTop + 46f * scale), 13f * scale,
                    AppPalettes.Market.Accent, frameTheme.TextMuted, Loc.T(L.Common.Loading));
            }

            return;
        }

        var body = new Rect(new Vector2(area.Min.X, bodyTop), area.Max);
        using (AppSurface.Begin(body))
        {
            var hasHq = snapshot.HasHq;
            var effectiveHq = hasHq && showHq;
            DrawHero(view, snapshot, effectiveHq, hasHq);
            DrawPrices(snapshot, effectiveHq);
            DrawAlertEditor(view, snapshot, effectiveHq, scope);
            DrawTrendSection(snapshot, effectiveHq);
            DrawListingsSection(snapshot, effectiveHq);
            DrawSalesSection(snapshot, effectiveHq);
        }
    }

    private void DrawHero(MarketView view, MarketSnapshot snapshot, bool hq, bool hasHq)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var iconSize = 56f * scale;
        var leftPad = 14f * scale;
        var cardHeight = iconSize + 24f * scale;
        var cardRounding = 14f * scale;
        var cardMin = new Vector2(origin.X, origin.Y);
        var cardMax = new Vector2(origin.X + width, origin.Y + cardHeight);
        Squircle.Fill(drawList, cardMin, cardMax, cardRounding, ImGui.GetColorU32(frameTheme.GroupedCard));
        Material.EdgeSquircle(drawList, cardMin, cardMax, cardRounding, scale);
        var tileRounding = 13f * scale;
        var iconMin = new Vector2(origin.X + leftPad, origin.Y + (cardHeight - iconSize) * 0.5f);
        var iconMax = iconMin + new Vector2(iconSize, iconSize);
        Elevation.Card(drawList, iconMin, iconMax, tileRounding, scale, 0.5f);
        Squircle.Fill(drawList, iconMin, iconMax, tileRounding, ImGui.GetColorU32(AppPalettes.Market.CardFill));
        if (view.IconId != 0)
        {
            var texture = textures.GetFromGameIcon(new GameIconLookup(view.IconId)).GetWrapOrEmpty();
            var inset = 4f * scale;
            drawList.AddImageRounded(texture.Handle, iconMin + new Vector2(inset, inset),
                iconMax - new Vector2(inset, inset), Vector2.Zero, Vector2.One, 0xFFFFFFFFu, tileRounding - inset);
        }

        Material.EdgeSquircle(drawList, iconMin, iconMax, tileRounding, scale);
        var textX = iconMax.X + 14f * scale;
        var textTop = iconMin.Y + 4f * scale;
        Typography.Draw(new Vector2(textX, textTop), MarketFormat.Clip(view.Name, 18), frameTheme.TextStrong,
            TextStyles.Title3);
        var min = snapshot.Min(hq);
        var priceText = min > 0 ? MarketFormat.Gil(min) : "\u2014";
        var priceSize = Typography.Measure(priceText, 1.4f, FontWeight.SemiBold);
        Typography.Draw(new Vector2(textX, textTop + 26f * scale), priceText, AppPalettes.Market.Accent, 1.4f,
            FontWeight.SemiBold);
        var cheapestLabel = hq ? Loc.T(L.Market.CheapestHq) : Loc.T(L.Market.Cheapest);
        Typography.Draw(
            new Vector2(textX + priceSize.X + 8f * scale, textTop + 28f * scale + priceSize.Y * 0.5f - 6f * scale),
            cheapestLabel, frameTheme.TextMuted, 0.78f);
        if (hasHq)
        {
            var pillGap = 6f * scale;
            var pillHeight = 26f * scale;
            var nqWidth = Typography.Measure(Loc.T(L.Common.Nq), 0.82f, FontWeight.SemiBold).X + 18f * scale;
            var hqWidth = Typography.Measure(Loc.T(L.Common.Hq), 0.82f, FontWeight.SemiBold).X + 18f * scale;
            var pillY = iconMax.Y - pillHeight - 2f * scale;
            var pillTotalWidth = nqWidth + pillGap + hqWidth;
            var pillStartX = origin.X + width - 16f * scale - pillTotalWidth;
            var nqRect = new Rect(new Vector2(pillStartX, pillY),
                new Vector2(pillStartX + nqWidth, pillY + pillHeight));
            var hqRect = new Rect(new Vector2(pillStartX + nqWidth + pillGap, pillY),
                new Vector2(pillStartX + nqWidth + pillGap + hqWidth, pillY + pillHeight));
            if (ui.PillButton(nqRect, Loc.T(L.Common.Nq), !showHq))
            {
                SetQuality(false);
            }

            if (ui.PillButton(hqRect, Loc.T(L.Common.Hq), showHq))
            {
                SetQuality(true);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cardHeight + 6f * scale));
    }

    private void DrawPrices(MarketSnapshot snapshot, bool hq)
    {
        var hasVendor = index.TryGet(snapshot.ItemId, out var itemRef) && itemRef.VendorPrice > 0;
        var rowCount = hasVendor ? 6 : 5;
        ui.SectionHeading(Loc.T(L.Market.Prices), 14f);
        var card = GroupCard.Begin(frameTheme, rowCount);
        SettingsRow.Info(card.NextRow(), Loc.T(L.Market.Average), PriceOrDash(snapshot.Average(hq)), frameTheme);
        SettingsRow.Info(card.NextRow(), Loc.T(L.Market.Highest), PriceOrDash(snapshot.Max(hq)), frameTheme);
        SettingsRow.Info(card.NextRow(), Loc.T(L.Market.SalesPerDay), MarketFormat.Velocity(snapshot.Velocity(hq)),
            frameTheme);
        SettingsRow.Info(card.NextRow(), Loc.T(L.Market.UpSold), $"{snapshot.UnitsForSale} / {snapshot.UnitsSold}",
            frameTheme);
        SettingsRow.Info(card.NextRow(), Loc.T(L.Market.Updated), TimeText.Ago(snapshot.LastUpload), frameTheme);
        if (hasVendor)
        {
            var marketMin = snapshot.Min(hq);
            var value = MarketFormat.Gil(itemRef.VendorPrice);
            if (marketMin > 0 && itemRef.VendorPrice < marketMin)
            {
                value += $"  \u00b7  {Loc.T(L.Market.Cheaper)}";
            }

            SettingsRow.Info(card.NextRow(), Loc.T(L.Market.VendorNpc), value, frameTheme);
        }

        card.End();
    }

    private void DrawAlertEditor(MarketView view, MarketSnapshot snapshot, bool hq, MarketScope scope)
    {
        ui.SectionHeading(Loc.T(L.Market.PriceAlert), 14f);
        var card = GroupCard.Begin(frameTheme, 1);
        var existing = alerts.HasAlertFor(view.ItemId);
        var label = showAlertEditor ? Loc.T(L.Common.Cancel) :
            existing ? Loc.T(L.Market.AddAnotherAlert) : Loc.T(L.Market.SetPriceAlert);
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
        ImGui.Dummy(new Vector2(0f, 8f * scale));
        DrawThresholdField();
        ImGui.Dummy(new Vector2(0f, 8f * scale));
        var dirOrigin = ImGui.GetCursorScreenPos();
        var toggleWidth = 220f * scale;
        var toggleHeight = 30f * scale;
        alertDirLabels[0] = Loc.T(L.Market.AtOrBelow);
        alertDirLabels[1] = Loc.T(L.Market.AtOrAbove);
        var dirIndex = SegmentStrip.Draw("market.alertDir",
            new Rect(dirOrigin, new Vector2(dirOrigin.X + toggleWidth, dirOrigin.Y + toggleHeight)), alertDirLabels,
            alertBelow ? 0 : 1, frameTheme);
        alertBelow = dirIndex == 0;
        ImGui.SetCursorScreenPos(dirOrigin);
        ImGui.Dummy(new Vector2(toggleWidth, toggleHeight));
        ImGui.Dummy(new Vector2(0f, 10f * scale));
        if (DrawPrimaryButton(Loc.T(L.Market.CreateAlert)))
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

    private void DrawThresholdField()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = 40f * scale;
        var pillMin = origin;
        var pillMax = new Vector2(origin.X + width, origin.Y + height);
        Squircle.Fill(drawList, pillMin, pillMax, 12f * scale, ImGui.GetColorU32(frameTheme.GroupedCard));
        Material.EdgeSquircle(drawList, pillMin, pillMax, 12f * scale, scale);
        var labelSize = Typography.Measure("Gil", TextStyles.FootnoteEmphasized);
        Typography.Draw(new Vector2(pillMin.X + 14f * scale, pillMin.Y + height * 0.5f - labelSize.Y * 0.5f), "Gil",
            frameTheme.TextMuted, TextStyles.FootnoteEmphasized);
        var inputLeft = pillMin.X + 48f * scale;
        ImGui.SetCursorScreenPos(new Vector2(inputLeft, pillMin.Y + height * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(pillMax.X - inputLeft - 14f * scale);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, frameTheme.TextStrong))
        {
            ImGui.InputInt("##marketAlertThreshold", ref alertThreshold, 0, 0);
        }

        if (alertThreshold < 1)
        {
            alertThreshold = 1;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private bool DrawPrimaryButton(string label)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = 42f * scale;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        var hovered = ImGui.IsMouseHoveringRect(min, max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var fill = pressed ? Palette.Mix(frameTheme.Accent, new Vector4(0f, 0f, 0f, 1f), 0.14f) :
            hovered ? Palette.Mix(frameTheme.Accent, frameTheme.TextStrong, 0.10f) : frameTheme.Accent;
        Elevation.Card(drawList, min, max, 12f * scale, scale, 0.6f);
        Squircle.Fill(drawList, min, max, 12f * scale, ImGui.GetColorU32(fill));
        drawList.AddLine(new Vector2(min.X + 12f * scale, min.Y + 1f * scale),
            new Vector2(max.X - 12f * scale, min.Y + 1f * scale), ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.12f)),
            1f * scale);
        Typography.DrawCentered((min + max) * 0.5f, label, new Vector4(0.99f, 0.99f, 1f, 1f), TextStyles.Headline);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private void DrawTrendSection(MarketSnapshot snapshot, bool hq)
    {
        var sales = snapshot.Sales;
        var count = CountQuality(sales, hq);
        if (count < 2)
        {
            return;
        }

        ui.SectionHeading(Loc.T(L.Market.Trend), 14f);
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

        Sparkline.Draw(graph, values, AppPalettes.Market.Accent, Palette.WithAlpha(AppPalettes.Market.Accent, 0.18f));
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawListingsSection(MarketSnapshot snapshot, bool hq)
    {
        var listings = snapshot.Listings;
        var count = CountListings(listings, hq);
        ui.SectionHeading(count > 0 ? Loc.T(L.Market.ListingsCount, count) : Loc.T(L.Market.Listings), 14f);
        if (count == 0)
        {
            DrawEmptyCard(hq ? Loc.T(L.Market.NoHqListings) : Loc.T(L.Market.NoListings));
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
        ui.SectionHeading(count > 0 ? Loc.T(L.Market.RecentSalesCount, count) : Loc.T(L.Market.RecentSales), 14f);
        if (count == 0)
        {
            DrawEmptyCard(hq ? Loc.T(L.Market.NoHqSales) : Loc.T(L.Market.NoRecentSales));
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
        Typography.Draw(new Vector2(row.Center.X - size.X * 0.5f, row.Center.Y - size.Y * 0.5f), message,
            frameTheme.TextMuted);
        card.End();
    }

    private void DrawSearch(Rect bar)
    {
        SearchField.Draw(bar, "##marketSearch", Loc.T(L.Market.SearchItems), ref search, frameTheme);
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
        Typography.DrawCentered(new Vector2(body.Center.X, body.Min.Y + 70f * scale), message, AppPalettes.Market.MutedInk);
    }

    private void CenteredLoading(Rect body, string message)
    {
        var scale = ImGuiHelpers.GlobalScale;
        LoadingPulse.Draw(new Vector2(body.Center.X, body.Min.Y + 60f * scale), 13f * scale, AppPalettes.Market.Accent,
            AppPalettes.Market.MutedInk, message);
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

    private static string PriceOrDash(double value) => value > 0 ? MarketFormat.Gil(value) : "\u2014";
    private static string PriceOrDash(long value) => value > 0 ? MarketFormat.Gil(value) : "\u2014";

    public void Dispose()
    {
    }
}
