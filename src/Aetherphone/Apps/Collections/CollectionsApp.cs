using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Collections;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Net;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Collections;

internal sealed partial class CollectionsApp : IPhoneApp
{
    private const float TileGap = 12f;
    private const float TileHeight = 104f;
    private const float MaxTileHeight = 118f;
    private const float SearchHeight = 50f;
    private const float SegmentHeight = 34f;
    private const float ChipRowHeight = 38f;
    private const float RowHeight = 64f;
    private const float IconSize = 46f;
    private const float DropdownHeight = 32f;
    private const float MenuRowHeight = 34f;
    private const float PagerHeight = 54f;
    private const int PageSize = 50;
    public string Id => "collections";
    public Vector4 Accent => AppAccents.For(Id);
    public string DisplayName => Loc.T(L.Apps.Collections);
    public string Glyph => "Co";
    public int BadgeCount => 0;

    private static readonly Vector4[] CategoryTints =
    {
        new(0.95f, 0.55f, 0.25f, 1f),
        new(0.30f, 0.78f, 0.48f, 1f),
        new(0.98f, 0.76f, 0.30f, 1f),
        new(0.62f, 0.46f, 0.96f, 1f),
        new(0.93f, 0.38f, 0.62f, 1f),
        new(0.26f, 0.74f, 0.86f, 1f),
        new(0.98f, 0.64f, 0.22f, 1f),
        new(0.36f, 0.62f, 0.96f, 1f),
    };

    private readonly CollectionsCatalogService catalog;
    private readonly LodestoneService lodestone;
    private readonly MediaCache media;
    private readonly HttpService http;
    private readonly GameData gameData;
    private readonly ViewRouter<CollectionView> router;
    private readonly RouterDraw<CollectionView> drawView;
    private readonly AppSkin ui = new(AppPalettes.Collections);
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
        router = new ViewRouter<CollectionView>(CollectionView.Root());
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
        catalog.ResetSummaries();
        if (lodestoneId is not null)
        {
            catalog.RequestSummary(lodestoneId);
        }

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
        ui.Theme = context.Theme;
        if (GuideIntents.Consume("collections.category.mounts"))
        {
            OpenCategory(CollectionCategory.Mounts);
        }

        var scale = ImGuiHelpers.GlobalScale;
        var screen = SceneChrome.ScreenFrom(context.Content, context.Theme, scale);
        ui.Backdrop(screen);
        router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
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
                DrawDetail(area, view.Category, item);
                break;
            default:
                DrawRoot(area);
                break;
        }
    }

    private void DrawNavBar(Rect area, string title, Action? onBack)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        var fitted = Typography.FitText(title, area.Width - 96f * scale, TextStyles.Title3);
        Typography.DrawCentered(new Vector2(area.Center.X, rowCenterY), fitted, ui.TitleInk, TextStyles.Title3);
        if (onBack is null)
        {
            return;
        }

        var hitMin = new Vector2(area.Min.X, area.Min.Y);
        var hitMax = new Vector2(area.Min.X + 46f * scale, area.Min.Y + AppHeader.Height * scale);
        var hovered = UiInteract.Hover(hitMin, hitMax);
        var center = new Vector2(area.Min.X + 17f * scale, rowCenterY);
        if (BackButton.Draw("collections.back", center, 15f * scale, ui.TitleInk, hovered, scale))
        {
            onBack();
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
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(ui.FieldSurface));
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
            ProgressRing.Sweep((min + max) * 0.5f, 9f * scale, 2f * scale, ui.MutedInk, 900.0, 1.8f, 0.9f);
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

    private static FontAwesomeIcon CategoryIcon(CollectionCategory category) =>
        category switch
        {
            CollectionCategory.Mounts => FontAwesomeIcon.Horse,
            CollectionCategory.Minions => FontAwesomeIcon.Paw,
            CollectionCategory.Emotes => FontAwesomeIcon.Smile,
            CollectionCategory.Orchestrions => FontAwesomeIcon.Music,
            CollectionCategory.Hairstyles => FontAwesomeIcon.Cut,
            CollectionCategory.Facewear => FontAwesomeIcon.Glasses,
            CollectionCategory.Achievements => FontAwesomeIcon.Trophy,
            _ => FontAwesomeIcon.Clone,
        };

    private static Vector4 CategoryTint(CollectionCategory category) => CategoryTints[(int)category];

    public void Dispose()
    {
    }
}
