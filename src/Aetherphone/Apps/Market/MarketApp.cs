using Aetherphone.Core;
using Aetherphone.Core.Analytics;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Market;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Market;

internal sealed partial class MarketApp : IPhoneApp
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
    private readonly IAnalyticsService analytics;
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
        MarketLauncher launcher, GameData gameData, ITextureProvider textures, Configuration configuration,
        IAnalyticsService analytics)
    {
        this.market = market;
        this.index = index;
        this.alerts = alerts;
        this.launcher = launcher;
        this.gameData = gameData;
        this.textures = textures;
        this.configuration = configuration;
        this.analytics = analytics;
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
            analytics.Track(AnalyticsEvents.MarketSearch());
            reportedSearch = query;
        }
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

    private void SetScope(int newIndex)
    {
        scopeIndex = newIndex;
        configuration.MarketScope = scopes[newIndex].Kind;
        configuration.Save();
    }

    public void Dispose()
    {
    }
}
