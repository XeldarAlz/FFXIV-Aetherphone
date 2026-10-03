using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Inventory;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Market;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Inventory;

internal sealed partial class InventoryApp : IPhoneApp
{
    private const float TextRefreshSeconds = 30f;
    private const float PricePollSeconds = 1.5f;
    private const float CompanyCheckSeconds = 2f;
    private const float RowPad = 14f;
    private const float RowHoverAlpha = 0.06f;
    private const float RowPressAlpha = 0.12f;
    private const float SectionTopGap = 22f;
    private const float SectionHeaderGap = 8f;
    private const float SectionHeaderInset = 4f;
    private const float CardGap = 14f;
    private const float BottomPad = 28f;
    private const string MarketAppId = "market";

    public string Id => "inventory";
    public Vector4 Accent => AppAccents.For(Id);
    public string DisplayName => Loc.T(L.Apps.Inventory);
    public string Glyph => "I";
    public int BadgeCount => 0;

    private readonly InventoryCaptureService capture;
    private readonly GameData gameData;
    private readonly ITextureProvider textures;
    private readonly IInventoryItemSource itemSource;
    private readonly InventoryMarketPrices prices;
    private readonly MarketLauncher marketLauncher;
    private readonly AppSkin ui = new(AppPalettes.Inventory);
    private readonly InventorySnapshot snapshot = new();
    private readonly InventoryCatalog catalog = new();
    private readonly InventoryText text = new();
    private readonly InventoryValuation valuation = new();
    private readonly List<int> results = new();
    private readonly ViewRouter<InventoryView> router;
    private readonly RouterDraw<InventoryView> drawView;
    private readonly Action back;
    private Spring[] meters = Array.Empty<Spring>();
    private (InventorySourceKind Kind, ulong OwnerId)[] meterKeys = Array.Empty<(InventorySourceKind, ulong)>();
    private Spring resultsAppear;
    private string query = string.Empty;
    private string appliedQuery = string.Empty;
    private string lowerNeedle = string.Empty;
    private int builtRevision = -1;
    private float sinceTextRefresh;
    private float sincePricePoll;
    private float sinceCompanyCheck = CompanyCheckSeconds;
    private float deltaSeconds;
    private bool inFreeCompany;
    private bool resetScroll;
    private PhoneTheme frameTheme = PhoneTheme.Default;
    private INavigator frameNavigation = null!;

    public InventoryApp(InventoryCaptureService capture, GameData gameData, ITextureProvider textures,
        IInventoryItemSource itemSource, MarketboardService market, MarketLauncher marketLauncher,
        Configuration configuration)
    {
        this.capture = capture;
        this.gameData = gameData;
        this.textures = textures;
        this.itemSource = itemSource;
        this.marketLauncher = marketLauncher;
        prices = new InventoryMarketPrices(market, gameData, configuration);
        router = new ViewRouter<InventoryView>(InventoryView.Root());
        drawView = DrawView;
        back = () => router.Pop();
    }

    public void OnOpened()
    {
        query = string.Empty;
        appliedQuery = string.Empty;
        lowerNeedle = string.Empty;
        results.Clear();
        builtRevision = -1;
        resetScroll = true;
        sinceCompanyCheck = CompanyCheckSeconds;
        router.Reset();
    }

    public void OnClosed()
    {
        query = string.Empty;
        router.Reset();
    }

    public void Draw(in PhoneContext context)
    {
        frameTheme = context.Theme;
        frameNavigation = context.Navigation;
        ui.Theme = context.Theme;
        deltaSeconds = ImGui.GetIO().DeltaTime;
        var scale = UiScale.Current;
        ui.Backdrop(SceneChrome.ScreenFrom(context.Content, context.Theme, scale));
        if (gameData.LocalPlayer is null)
        {
            TourHolds.Hold(Id);
            router.Reset();
            ui.Body(context.Content);
            DrawLoggedOut(context);
            return;
        }

        Sync();
        if (catalog.HasLocal)
        {
            TourHolds.Release(Id);
        }
        else
        {
            TourHolds.Hold(Id);
        }

        router.Draw(context.Content, AppSkin.Transparent, deltaSeconds, drawView);
    }

    private void DrawLoggedOut(in PhoneContext context)
    {
        var navBar = AppHeader.BeginLargeTitle(context, false);
        var body = navBar.Body;
        using (AppSurface.Begin(body))
        {
            InventoryArt.StateScreen(body, ui, FontAwesomeIcon.UserSlash, Loc.T(L.Inventory.LogInToView),
                Loc.T(L.Inventory.LogInHint), string.Empty);
        }

        AppHeader.EndLargeTitle(in navBar, context, "inventory.nav", DisplayName, NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty);
    }

    private void DrawView(InventoryView view, Rect area, int depth)
    {
        ui.Body(area);
        var context = new PhoneContext(area, frameTheme, frameNavigation);
        switch (view.Kind)
        {
            case InventoryViewKind.Source:
                DrawSource(context, view);
                break;
            case InventoryViewKind.Item:
                DrawItem(context, view);
                break;
            case InventoryViewKind.Tidy:
                DrawTidy(context, view);
                break;
            case InventoryViewKind.Wealth:
                DrawWealth(context, view);
                break;
            default:
                DrawRoot(context);
                break;
        }
    }

    private void Sync()
    {
        var revision = capture.Revision;
        var languageChanged = !string.Equals(text.LanguageCode, Loc.Current.Code, StringComparison.Ordinal);
        if (revision != builtRevision)
        {
            Rebuild(revision);
        }
        else if (languageChanged)
        {
            text.Build(catalog);
            text.BuildValue(catalog, valuation, prices.ScopeName, valuation.Pending);
        }
        else
        {
            sinceTextRefresh += deltaSeconds;
            if (sinceTextRefresh >= TextRefreshSeconds)
            {
                sinceTextRefresh = 0f;
                text.Build(catalog);
            }
        }

        SyncQuery();
        PollPrices();
        CheckCompany();
    }

    private void Rebuild(int revision)
    {
        builtRevision = revision;
        sinceTextRefresh = 0f;
        capture.Fill(snapshot);
        catalog.Build(snapshot, itemSource);
        text.Build(catalog);
        prices.Refresh();
        valuation.Reset(catalog.Items);
        valuation.Poll(catalog.Items, prices);
        sincePricePoll = 0f;
        text.BuildValue(catalog, valuation, prices.ScopeName, valuation.Pending);
        RemapMeters();
        appliedQuery = "\u0001";
    }

    private void SyncQuery()
    {
        if (ReferenceEquals(query, appliedQuery) || string.Equals(query, appliedQuery, StringComparison.Ordinal))
        {
            return;
        }

        var wasSearching = lowerNeedle.Length > 0;
        appliedQuery = query;
        lowerNeedle = query.Trim().ToLowerInvariant();
        catalog.Filter(lowerNeedle, results);
        if (!wasSearching && lowerNeedle.Length > 0)
        {
            resultsAppear.SnapTo(0f);
        }
    }

    private void PollPrices()
    {
        if (!valuation.Pending)
        {
            return;
        }

        sincePricePoll += deltaSeconds;
        if (sincePricePoll < PricePollSeconds)
        {
            return;
        }

        sincePricePoll = 0f;
        if (!prices.IsValid && prices.Refresh())
        {
            valuation.Reset(catalog.Items);
        }

        var resolved = valuation.Poll(catalog.Items, prices);
        if (resolved || !valuation.Pending)
        {
            text.BuildValue(catalog, valuation, prices.ScopeName, valuation.Pending);
        }
    }

    private void CheckCompany()
    {
        sinceCompanyCheck += deltaSeconds;
        if (sinceCompanyCheck < CompanyCheckSeconds)
        {
            return;
        }

        sinceCompanyCheck = 0f;
        var player = gameData.LocalPlayer;
        inFreeCompany = player is not null && player.CompanyTag.TextValue.Length > 0;
    }

    private void RemapMeters()
    {
        var sources = catalog.Sources;
        var next = new Spring[sources.Count];
        var keys = new (InventorySourceKind, ulong)[sources.Count];
        for (var index = 0; index < sources.Count; index++)
        {
            var source = sources[index];
            keys[index] = (source.Kind, source.OwnerId);
            next[index] = new Spring(0f);
            for (var previous = 0; previous < meterKeys.Length; previous++)
            {
                if (meterKeys[previous].Kind == source.Kind && meterKeys[previous].OwnerId == source.OwnerId)
                {
                    next[index] = meters[previous];
                    break;
                }
            }
        }

        meters = next;
        meterKeys = keys;
    }

    private float MeterFill(int sourceIndex)
    {
        if (sourceIndex < 0 || sourceIndex >= meters.Length)
        {
            return 0f;
        }

        return meters[sourceIndex].Step(catalog.Sources[sourceIndex].Fill, Motion.Sheet, deltaSeconds);
    }

    private void OpenItem(int itemIndex, string backTitle)
    {
        if (itemIndex < 0 || itemIndex >= catalog.Items.Count)
        {
            return;
        }

        UiFeedback.Play(UiSound.Tap);
        router.Push(InventoryView.ForItem(catalog.Items[itemIndex].ItemId, backTitle));
    }

    private void OpenSource(int sourceIndex, string backTitle)
    {
        var source = catalog.Sources[sourceIndex];
        if (!source.Browsable)
        {
            return;
        }

        UiFeedback.Play(UiSound.Tap);
        router.Push(InventoryView.ForSource(source.Kind, source.OwnerId, backTitle));
    }

    private void SectionHeader(string label, float gapUnits, float scale)
    {
        ImGui.Dummy(new Vector2(0f, gapUnits * scale));
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var fitted = Typography.FitText(label, MathF.Max(1f, width - SectionHeaderInset * scale), TextStyles.Title3);
        var size = Typography.Measure(fitted, TextStyles.Title3);
        Typography.Draw(ImGui.GetWindowDrawList(), new Vector2(origin.X + SectionHeaderInset * scale, origin.Y),
            fitted, ui.TitleInk, TextStyles.Title3);
        ImGui.Dummy(new Vector2(width, size.Y + SectionHeaderGap * scale));
    }

    private Rect BeginGroup(float height)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var rect = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        ui.Card(ImGui.GetWindowDrawList(), rect.Min, rect.Max, Metrics.Radius.Grouped * UiScale.Current, true);
        return rect;
    }

    private static void EndGroup(Rect group)
    {
        ImGui.SetCursorScreenPos(group.Min);
        ImGui.Dummy(new Vector2(group.Width, group.Height));
    }

    private bool RowInteract(ImDrawListPtr drawList, Rect row, bool first, bool last, bool interactive)
    {
        if (!interactive)
        {
            return false;
        }

        var hovered = UiInteract.Hover(row.Min, row.Max);
        if (!hovered)
        {
            return false;
        }

        var pressed = ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var corners = (first ? ImDrawFlags.RoundCornersTop : ImDrawFlags.None) |
                      (last ? ImDrawFlags.RoundCornersBottom : ImDrawFlags.None);
        if (corners == ImDrawFlags.None)
        {
            corners = ImDrawFlags.RoundCornersNone;
        }

        drawList.AddRectFilled(row.Min, row.Max,
            ImGui.GetColorU32(ui.TitleInk with { W = pressed ? RowPressAlpha : RowHoverAlpha }),
            Metrics.Radius.Grouped * UiScale.Current, corners);
        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return UiInteract.Click(row.Min, row.Max, hovered);
    }

    private void Hairline(ImDrawListPtr drawList, float left, float right, float y) =>
        drawList.AddLine(new Vector2(left, y - 0.5f), new Vector2(right, y - 0.5f), ImGui.GetColorU32(ui.Hairline),
            Metrics.Stroke.Hairline);

    private static bool RowVisible(Rect row) => ImGui.IsRectVisible(row.Min, row.Max);

    private void DrawFootnote(string message, float scale)
    {
        ImGui.Dummy(new Vector2(0f, CardGap * scale));
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var bottom = Typography.DrawWrappedCentered(ImGui.GetWindowDrawList(), message, TextStyles.Footnote,
            ui.MutedInk, new Vector2(origin.X + width * 0.5f, origin.Y), width - Metrics.Space.Xxl * scale);
        ImGui.Dummy(new Vector2(width, bottom - origin.Y + BottomPad * scale));
    }

    public void Dispose()
    {
    }
}
