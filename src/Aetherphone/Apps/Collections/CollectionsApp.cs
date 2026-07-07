using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Collections;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Net;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Collections;

internal sealed partial class CollectionsApp : IPhoneApp
{
    private const float TilePadding = 16f;
    private const float TileGap = 12f;
    private const float TileHeight = 92f;
    private const float SearchHeight = 46f;
    private const float SegmentHeight = 34f;
    private const float ChipRowHeight = 36f;
    private const float RowHeight = 60f;
    private const float IconSize = 40f;
    private const float DropdownHeight = 30f;
    private const float MenuRowHeight = 32f;
    private const float PagerHeight = 52f;
    private const int PageSize = 50;
    public string Id => "collections";
    public Vector4 Accent => AppAccents.For(Id);
    public string DisplayName => Loc.T(L.Apps.Collections);
    public string Glyph => "Co";
    public int BadgeCount => 0;
    private readonly CollectionsCatalogService catalog;
    private readonly LodestoneService lodestone;
    private readonly MediaCache media;
    private readonly HttpService http;
    private readonly GameData gameData;
    private readonly ViewRouter<CollectionView> router;
    private readonly RouterDraw<CollectionView> drawView;
    private readonly Action back;
    private readonly List<CollectionItem> filtered = new();
    private readonly SortedSet<string> sourceSet = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> sourceList = new();
    private readonly Dictionary<string, float> iconFade = new();
    private readonly string[] ownershipLabels = new string[3];
    private string search = string.Empty;
    private OwnershipFilter ownership = OwnershipFilter.All;
    private int sourceIndex;
    private bool resetScroll;
    private string? lodestoneId;
    private bool sourceMenuOpen;
    private int page;
    private string lastSearch = string.Empty;
    private Rect sourceMenuAnchor;
    private float contentBottom;
    private PhoneTheme frameTheme = PhoneTheme.Default;
    private INavigator frameNavigation = null!;

    public CollectionsApp(CollectionsCatalogService catalog, LodestoneService lodestone, MediaCache media,
        HttpService http, GameData gameData)
    {
        this.catalog = catalog;
        this.lodestone = lodestone;
        this.media = media;
        this.http = http;
        this.gameData = gameData;
        router = new ViewRouter<CollectionView>(CollectionView.Root(), Id);
        drawView = DrawView;
        back = () => router.Pop();
    }

    public void OnOpened()
    {
        router.Reset();
        ResetFilters();
        ownershipLabels[0] = Loc.T(L.Collections.FilterAll);
        ownershipLabels[1] = Loc.T(L.Collections.FilterOwned);
        ownershipLabels[2] = Loc.T(L.Collections.FilterMissing);
        lodestoneId = ResolveLocalId();
        catalog.ResetOwned();
        for (var index = 0; index < CollectionCategories.All.Length; index++)
        {
            catalog.RequestCatalog(CollectionCategories.All[index]);
        }
    }

    public void OnClosed()
    {
        router.Reset();
        iconFade.Clear();
        ResetFilters();
    }

    public void Draw(in PhoneContext context)
    {
        frameTheme = context.Theme;
        frameNavigation = context.Navigation;
        router.Draw(context.Content, context.Theme.AppBackground, ImGui.GetIO().DeltaTime, drawView);
    }

    private void DrawView(CollectionView view, Rect area, int depth)
    {
        switch (view.Kind)
        {
            case CollectionViewKind.Category:
                DrawCategory(area, view.Category);
                break;
            case CollectionViewKind.Detail when view.Item is { } item:
                DrawDetail(area, view.Category, item);
                break;
            default:
                DrawRoot(area);
                break;
        }
    }

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

    private void OpenCategory(CollectionCategory category)
    {
        ResetFilters();
        resetScroll = true;
        catalog.RequestCatalog(category);
        if (lodestoneId is not null)
        {
            catalog.RequestOwned(lodestoneId, category);
        }

        router.Push(CollectionView.ForCategory(category));
    }

    private void DrawIcon(ImDrawListPtr drawList, CollectionItem item, Vector2 min, Vector2 max, float rounding)
    {
        var scale = ImGuiHelpers.GlobalScale;
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(frameTheme.SurfaceMuted), rounding);
        if (item.IconUrl.Length == 0)
        {
            return;
        }

        var result = Thumb(item.IconUrl);
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
            ProgressRing.Sweep((min + max) * 0.5f, 9f * scale, 2f * scale, frameTheme.TextMuted, 900.0, 1.8f, 0.9f);
        }
    }

    private void OpenItem(CollectionCategory category, CollectionItem item) =>
        router.Push(CollectionView.ForItem(category, item));

    private string SubtitleOf(CollectionItem item)
    {
        if (item.SourceType.Length > 0 && item.SourceText.Length > 0)
        {
            return $"{item.SourceType} · {item.SourceText}";
        }

        if (item.SourceText.Length > 0)
        {
            return item.SourceText;
        }

        return item.SourceType;
    }

    private MediaResult Thumb(string url) => media.GetOrRequest(url, token => http.GetBytesAsync(new Uri(url), token));

    private float StepFade(string url, bool ready)
    {
        iconFade.TryGetValue(url, out var fade);
        var target = ready ? 1f : 0f;
        if (fade < target)
        {
            fade = Math.Min(target, fade + ImGui.GetIO().DeltaTime / 0.22f);
        }

        iconFade[url] = fade;
        return fade;
    }

    private void ResetFilters()
    {
        search = string.Empty;
        lastSearch = string.Empty;
        ownership = OwnershipFilter.All;
        sourceIndex = 0;
        sourceMenuOpen = false;
        page = 0;
    }

    private static string CategoryLabel(CollectionCategory category) =>
        category switch
        {
            CollectionCategory.Mounts => Loc.T(L.Collections.Mounts),
            CollectionCategory.Minions => Loc.T(L.Collections.Minions),
            CollectionCategory.Emotes => Loc.T(L.Collections.Emotes),
            CollectionCategory.Orchestrions => Loc.T(L.Collections.Orchestrions),
            CollectionCategory.Hairstyles => Loc.T(L.Collections.Hairstyles),
            CollectionCategory.Facewear => Loc.T(L.Collections.Facewear),
            CollectionCategory.Achievements => Loc.T(L.Collections.Achievements),
            _ => Loc.T(L.Collections.TriadCards),
        };

    public void Dispose()
    {
    }
}
