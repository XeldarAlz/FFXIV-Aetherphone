using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Market;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Market;

internal sealed partial class MarketApp : IResumableApp
{
    private const int MaxRecents = 12;
    private const float BottomPad = 28f;

    public string Id => "market";
    public string DisplayName => Loc.T(L.Apps.Market);
    public string Glyph => "$";
    public int BadgeCount => alerts.TriggeredCount;
    public bool HasBadge => true;

    private readonly MarketboardService market;
    private readonly MarketItemIndex index;
    private readonly MarketAlertService alerts;
    private readonly MarketWatchlist watchlist;
    private readonly MarketLauncher launcher;
    private readonly GameData gameData;
    private readonly ITextureProvider textures;
    private readonly Configuration configuration;
    private readonly AppSkin ui = new(AppPalettes.Market);
    private readonly ViewRouter<MarketView> router;
    private readonly RouterDraw<MarketView> drawView;
    private readonly Action back;
    private readonly List<string> scopeLabels = new();
    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private uint pendingOpenId;

    public MarketApp(MarketboardService market, MarketItemIndex index, MarketAlertService alerts,
        MarketWatchlist watchlist, MarketLauncher launcher, GameData gameData, ITextureProvider textures,
        Configuration configuration)
    {
        this.market = market;
        this.index = index;
        this.alerts = alerts;
        this.watchlist = watchlist;
        this.launcher = launcher;
        this.gameData = gameData;
        this.textures = textures;
        this.configuration = configuration;
        router = new ViewRouter<MarketView>(MarketView.Root());
        drawView = DrawView;
        back = () => router.Pop();
    }

    public void OnOpened()
    {
        router.Reset();
        search = string.Empty;
        alertSheet.CloseImmediately();
        alerts.Acknowledge();
        index.EnsureBuilt();
        watchlist.RefreshScopes();
    }

    public void OnResumed()
    {
        alerts.Acknowledge();
        index.EnsureBuilt();
        watchlist.RefreshScopes();
    }

    public void OnClosed()
    {
        alertSheet.CloseImmediately();
    }

    private MarketScope Scope => watchlist.Scope;

    public void Draw(in PhoneContext context)
    {
        index.EnsureBuilt();
        if (watchlist.Scopes.Count == 0)
        {
            watchlist.RefreshScopes();
        }

        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = theme;
        ConsumeLaunch();
        var scale = UiScale.Current;
        var screen = SceneChrome.ScreenFrom(context.Content, theme, scale);
        ui.Backdrop(screen);
        using (InputShield.Engage(alertSheet.CapturesPointer))
        {
            router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
        }

        DrawAlertSheet(screen);
        if (router.Depth > 1)
        {
            TourHolds.Hold(Id);
        }
        else
        {
            TourHolds.Release(Id);
        }
    }

    private void ConsumeLaunch()
    {
        if (launcher.TryConsume(out var requestedItem, out var requestedSearch))
        {
            if (requestedItem != 0)
            {
                pendingOpenId = requestedItem;
            }
            else if (requestedSearch is not null)
            {
                router.Reset();
                alertSheet.CloseImmediately();
                search = requestedSearch;
                lastSearch = "\u0001";
            }
        }

        if (pendingOpenId == 0 || !index.Ready)
        {
            return;
        }

        if (index.TryGet(pendingOpenId, out var pending))
        {
            router.Reset();
            alertSheet.CloseImmediately();
            OpenItem(pending, false);
        }

        pendingOpenId = 0;
    }

    private void DrawView(MarketView view, Rect area, int depth)
    {
        ui.Body(area);
        switch (view.Kind)
        {
            case MarketViewKind.Item:
                DrawDetail(area, view);
                break;
            case MarketViewKind.Listings:
                DrawListingsPage(area, view);
                break;
            case MarketViewKind.Sales:
                DrawSalesPage(area, view);
                break;
            case MarketViewKind.Alerts:
                DrawAlertsPage(area);
                break;
            default:
                DrawRoot(area);
                break;
        }
    }

    private void OpenItem(MarketItemRef item, bool sound = true)
    {
        if (sound)
        {
            UiFeedback.Play(UiSound.Tap);
        }

        PushRecent(item.Id);
        PrimeDetail();
        router.Push(MarketView.Item(item.Id, item.Name, item.IconId));
    }

    private void OpenItem(uint itemId)
    {
        if (index.TryGet(itemId, out var item))
        {
            OpenItem(item);
        }
    }

    private void Push(MarketView view)
    {
        UiFeedback.Play(UiSound.Tap);
        router.Push(view);
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

    private float DrawScopeStrip(Vector2 origin, float width, string? anchor, float scale)
    {
        var scopes = watchlist.Scopes;
        if (scopes.Count == 0)
        {
            return origin.Y;
        }

        scopeLabels.Clear();
        for (var scopeIndex = 0; scopeIndex < scopes.Count; scopeIndex++)
        {
            scopeLabels.Add(scopes[scopeIndex].ApiName);
        }

        var row = new Rect(origin, new Vector2(origin.X + width, origin.Y + ScopeStripHeight * scale));
        if (anchor is not null)
        {
            UiAnchors.Report(anchor, row);
        }

        var current = watchlist.ScopeIndex;
        var selected = SegmentStrip.Draw(anchor ?? "market.scope.root", row, scopeLabels, current, ui.Palette);
        if (selected != current && selected >= 0)
        {
            UiFeedback.Play(UiSound.Tap);
            watchlist.SetScope(selected);
        }

        return row.Max.Y;
    }

    private static void ReserveTo(Vector2 origin, float width, float bottom)
    {
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, MathF.Max(0f, bottom - origin.Y)));
    }

    private float DrawSectionTitle(ImDrawListPtr drawList, Vector2 origin, float width, string title,
        string trailing, out bool trailingClicked, float scale)
    {
        trailingClicked = false;
        var reserve = trailing.Length > 0
            ? Typography.Measure(trailing, TextStyles.Body).X + MarketArt.ValueGap * scale
            : 0f;
        var height = MarketArt.SectionHeader(drawList, origin, width, title, ui.TitleInk, reserve, scale);
        if (trailing.Length == 0)
        {
            return height;
        }

        var size = Typography.Measure(trailing, TextStyles.Body);
        var tapHeight = MathF.Max(height, Metrics.Size.TapTarget * scale);
        var hitMin = new Vector2(origin.X + width - size.X - MarketArt.ValueGap * scale,
            origin.Y + (height - tapHeight) * 0.5f);
        var hitMax = new Vector2(origin.X + width, hitMin.Y + tapHeight);
        var hovered = UiInteract.Hover(hitMin, hitMax);
        var ink = hovered ? Palette.Lighten(ui.Accent, 0.15f) : ui.Accent;
        Typography.Draw(drawList, new Vector2(origin.X + width - size.X, origin.Y + (height - size.Y) * 0.5f),
            trailing, ink, TextStyles.Body);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        trailingClicked = UiInteract.Click(hitMin, hitMax, hovered);
        return height;
    }

    public void Dispose()
    {
    }
}
