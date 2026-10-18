using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.News;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Apps.News;

internal sealed partial class NewsApp : IResumableApp, ITabRouteTarget
{
    private const string AppId = "news";

    public string Id => AppId;
    public Vector4 Accent => AppAccents.For(Id);
    public string DisplayName => Loc.T(L.Apps.News);
    public string Glyph => "Ne";
    public int BadgeCount => 0;

    private static readonly string[] NavIds =
    {
        "news.nav.topics", "news.nav.notices", "news.nav.maintenance", "news.nav.updates", "news.nav.status",
    };

    private static readonly string[] TabAnchors =
    {
        "news.tab.topics", "news.tab.notices", "news.tab.maintenance", "news.tab.updates", "news.tab.status",
    };

    private readonly NewsService news;
    private readonly RemoteImageCache images;
    private readonly GameData gameData;
    private readonly AppSkin ui = new(AppPalettes.News);
    private readonly Dictionary<string, NewsStory> storyCache = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Spring> imageReveal = new(StringComparer.Ordinal);
    private readonly NewsFeed[] feeds = new NewsFeed[NewsCategories.All.Length];
    private readonly ViewRouter<NewsView> router;
    private readonly RouterDraw<NewsView> drawView;
    private readonly Action back;
    private readonly Action refresh;
    private readonly TabBar tabBar = new();
    private readonly TabItem[] tabItems = new TabItem[NewsCategories.All.Length];
    private readonly NewsCategory[] tabCategories = new NewsCategory[NewsCategories.All.Length];
    private readonly ScreenToast toast = new();
    private PendingTab pendingTab;
    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private string locale = "na";
    private NewsCategory activeCategory = NewsCategory.Topics;
    private int tabCount;
    private bool resetScroll;
    private float deltaSeconds;
    private float fontKey;

    public NewsApp(NewsService news, RemoteImageCache images, GameData gameData)
    {
        this.news = news;
        this.images = images;
        this.gameData = gameData;
        for (var index = 0; index < feeds.Length; index++)
        {
            feeds[index] = new NewsFeed(NewsCategories.All[index], storyCache);
        }

        router = new ViewRouter<NewsView>(NewsView.Feed(NewsCategory.Topics));
        drawView = DrawView;
        back = () => router.Pop();
        refresh = Refresh;
    }

    public void OpenTab(string tab) => pendingTab.Request(tab);

    public void OnOpened()
    {
        locale = gameData.LodestoneLocale();
        router.Reset();
        imageReveal.Clear();
        activeCategory = NewsCategory.Topics;
        resetScroll = true;
        articleTopDepth = NoArticleTop;
    }

    public void OnResumed()
    {
        locale = gameData.LodestoneLocale();
        if (NewsCategories.AvailableFor(activeCategory, locale))
        {
            return;
        }

        router.Reset();
        activeCategory = NewsCategory.Topics;
        resetScroll = true;
        articleTopDepth = NoArticleTop;
    }

    public void OnClosed()
    {
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = theme;
        deltaSeconds = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        fontKey = Typography.LineHeight(TextStyles.Body);
        ConsumePendingTab();
        var scale = UiScale.Current;
        var screen = SceneChrome.ScreenFrom(context.Content, theme, scale);
        ui.Backdrop(screen);
        router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
        toast.Draw(screen, ScreenToastStyle.From(ui));
        UpdateTourHold();
    }

    private void DrawView(NewsView view, Rect area, int depth)
    {
        ui.Body(area);
        if (view.Screen == NewsScreen.Article)
        {
            DrawArticle(area, view, depth);
            return;
        }

        DrawRoot(area);
    }

    private void ConsumePendingTab()
    {
        for (var index = 0; index < NewsCategories.All.Length; index++)
        {
            var category = NewsCategories.All[index];
            if (!pendingTab.Take(NewsCategories.Path(category)))
            {
                continue;
            }

            router.Reset();
            locale = gameData.LodestoneLocale();
            activeCategory = NewsCategories.AvailableFor(category, locale) ? category : NewsCategory.Topics;
            resetScroll = true;
        }
    }

    private NewsFeed Feed(NewsCategory category)
    {
        var feed = feeds[(int)category];
        feed.Sync(news.Request(category, locale, false));
        return feed;
    }

    private void Refresh()
    {
        feeds[(int)activeCategory].Sync(news.Request(activeCategory, locale, true));
        if (activeCategory == NewsCategory.Topics)
        {
            feeds[(int)NewsCategory.Maintenance].Sync(news.Request(NewsCategory.Maintenance, locale, true));
        }
    }

    private void SelectCategory(NewsCategory category)
    {
        if (category == activeCategory)
        {
            return;
        }

        UiFeedback.Play(UiSound.Tap);
        activeCategory = category;
        resetScroll = true;
    }

    private void OpenStory(NewsCategory category, NewsStory story)
    {
        UiFeedback.Play(UiSound.Tap);
        router.Push(NewsView.Article(category, story.Id));
        articleTopDepth = router.Depth;
    }

    private void UpdateTourHold()
    {
        if (router.Depth == 1 && feeds[(int)NewsCategory.Maintenance].HasStories)
        {
            TourHolds.Release(Id);
            return;
        }

        TourHolds.Hold(Id);
    }

    private IDalamudTextureWrap? Banner(string? url, float drawnPixels, out float reveal, out bool failed)
    {
        reveal = 0f;
        failed = false;
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        var texture = images.Sized(url, drawnPixels);
        imageReveal.TryGetValue(url, out var spring);
        spring.Step(texture is null ? 0f : 1f, Motion.Appear, deltaSeconds);
        imageReveal[url] = spring;
        reveal = Math.Clamp(spring.Value, 0f, 1f);
        failed = texture is null && images.Failed(url);
        return texture;
    }

    private string PageTitle(NewsCategory category) =>
        category == NewsCategory.Topics ? DisplayName : Loc.T(NewsCategories.Label(category));

    public void Dispose()
    {
    }
}
