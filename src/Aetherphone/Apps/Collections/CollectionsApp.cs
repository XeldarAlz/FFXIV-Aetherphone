using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Collections;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Market;
using Aetherphone.Core.Media;
using Aetherphone.Core.Net;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Collections;

internal sealed partial class CollectionsApp : IPhoneApp
{
    private const float IconFadeSeconds = 0.22f;
    private const float SpinnerRadius = 9f;
    private const float SpinnerThickness = 2f;
    private const int SearchMaxLength = 60;

    public string Id => CollectionsJournal.AppId;
    public Vector4 Accent => AppAccents.For(Id);
    public string DisplayName => Loc.T(L.Apps.Collections);
    public string Glyph => "Co";
    public int BadgeCount => 0;

    private readonly CollectionsCatalogService catalog;
    private readonly CollectionsJournal journal;
    private readonly LodestoneService lodestone;
    private readonly MediaCache media;
    private readonly HttpService http;
    private readonly GameData gameData;
    private readonly MarketLauncher marketLauncher;
    private readonly ViewRouter<CollectionView> router;
    private readonly RouterDraw<CollectionView> drawView;
    private readonly AppSkin ui = new(AppPalettes.Collections);
    private readonly Action back;
    private readonly Dictionary<string, float> iconFade = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Func<CancellationToken, Task<byte[]?>>> iconSources =
        new(StringComparer.Ordinal);

    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private Rect screen;
    private string? lodestoneId;
    private bool tracking;

    public CollectionsApp(CollectionsCatalogService catalog, CollectionsJournal journal, LodestoneService lodestone,
        MediaCache media, HttpService http, GameData gameData, MarketLauncher marketLauncher)
    {
        this.catalog = catalog;
        this.journal = journal;
        this.lodestone = lodestone;
        this.media = media;
        this.http = http;
        this.gameData = gameData;
        this.marketLauncher = marketLauncher;
        router = new ViewRouter<CollectionView>(CollectionView.Root());
        drawView = DrawView;
        back = () => router.Pop();
    }

    public void OnOpened()
    {
        router.Reset();
        ResetCategoryState();
        rootQuery = string.Empty;
        sortSheet.Close();
        tracking = IsPresent();
        lodestoneId = ResolveLocalId();
        catalog.ResetOwned();
        catalog.ResetSummaries();
        if (tracking)
        {
            catalog.RequestSummary(lodestoneId);
        }

        for (var index = 0; index < CollectionCategories.All.Length; index++)
        {
            catalog.RequestCatalog(CollectionCategories.All[index]);
        }

        digest.Invalidate();
        heroFill.SnapTo(0f);
        for (var index = 0; index < tileFills.Length; index++)
        {
            tileFills[index].SnapTo(0f);
        }
    }

    public void OnClosed()
    {
        router.Reset();
        sortSheet.Close();
        iconFade.Clear();
        ResetCategoryState();
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = theme;
        sortSheet.Gate();
        SyncTracking();
        if (tracking)
        {
            TourHolds.Release(Id);
        }
        else
        {
            TourHolds.Hold(Id);
        }

        var scale = UiScale.Current;
        screen = SceneChrome.ScreenFrom(context.Content, theme, scale);
        ui.Backdrop(screen);
        router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
        DrawSortSheet();
    }

    private void DrawView(CollectionView view, Rect area, int depth)
    {
        ui.Body(area);
        switch (view.Kind)
        {
            case CollectionViewKind.Category:
                DrawCategory(area, view.Category);
                break;
            case CollectionViewKind.Detail when view.Item is { } item:
                DrawDetail(area, item);
                break;
            default:
                DrawRoot(area);
                break;
        }
    }

    private void SyncTracking()
    {
        var present = IsPresent();
        if (present == tracking)
        {
            return;
        }

        tracking = present;
        lodestoneId = present ? ResolveLocalId() : null;
        catalog.ResetOwned();
        catalog.ResetSummaries();
        if (present)
        {
            catalog.RequestSummary(lodestoneId);
        }

        digest.Invalidate();
    }

    private bool IsPresent() => journal.IsTracking || gameData.LocalPlayer is not null;

    private string? ResolveLocalId()
    {
        var player = gameData.LocalPlayer;
        if (player is null)
        {
            return null;
        }

        var name = player.Name.TextValue;
        var world = gameData.WorldName(player.HomeWorld.RowId);
        return lodestone.TryGetCachedId(name, world);
    }

    private HashSet<int>? OwnedIds(CollectionCategory category)
    {
        if (!tracking)
        {
            return null;
        }

        var owned = catalog.RequestOwned(lodestoneId, category);
        return owned.State == OwnedState.Ready ? owned.Ids : null;
    }

    private CategoryProgress? Progress(CollectionCategory category)
    {
        if (!tracking)
        {
            return null;
        }

        var summary = catalog.RequestSummary(lodestoneId);
        var usable = summary.State == SummaryState.Ready ||
                     (summary.State == SummaryState.Loading && CollectionsCatalogService.HasLocalUnlocks(category));
        return usable ? summary.For(category) : null;
    }

    private void OpenCategory(CollectionCategory category, string query)
    {
        ResetCategoryState();
        categorySearch = query;
        catalog.RequestCatalog(category);
        router.Push(CollectionView.ForCategory(category));
    }

    private void OpenItem(CollectionItem item) => router.Push(CollectionView.ForItem(item));

    private void DrawIcon(ImDrawListPtr drawList, CollectionItem item, Vector2 min, Vector2 max, float rounding)
    {
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(ui.FieldSurface));
        if (item.IconUrl.Length == 0)
        {
            ProgressRing.CenterIcon(drawList, (min + max) * 0.5f, CollectionsArt.Icon(item.Category), ui.MutedInk,
                (max.Y - min.Y) * 0.42f);
            return;
        }

        var result = media.GetOrRequest(item.IconUrl, SourceFor(item.IconUrl));
        if (result.Texture is { } texture)
        {
            var fade = StepFade(item.IconUrl, true);
            var tint = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, fade));
            drawList.AddImageRounded(texture.Handle, min, max, Vector2.Zero, Vector2.One, tint, rounding,
                ImDrawFlags.RoundCornersAll);
            return;
        }

        StepFade(item.IconUrl, false);
        if (result.Loading)
        {
            var scale = UiScale.Current;
            ProgressRing.Sweep((min + max) * 0.5f, SpinnerRadius * scale, SpinnerThickness * scale, ui.MutedInk,
                900.0, 1.8f, 0.9f);
        }
    }

    private Func<CancellationToken, Task<byte[]?>> SourceFor(string url)
    {
        if (!iconSources.TryGetValue(url, out var source))
        {
            var uri = new Uri(url);
            source = token => http.GetBytesAsync(uri, token);
            iconSources[url] = source;
        }

        return source;
    }

    private float StepFade(string url, bool ready)
    {
        iconFade.TryGetValue(url, out var fade);
        var target = ready ? 1f : 0f;
        if (fade < target)
        {
            fade = Math.Min(target, fade + ImGui.GetIO().DeltaTime / IconFadeSeconds);
        }

        iconFade[url] = fade;
        return fade;
    }

    private static void ReserveTo(Vector2 origin, float width, float bottom)
    {
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, MathF.Max(0f, bottom - origin.Y)));
    }

    public void Dispose()
    {
    }
}
