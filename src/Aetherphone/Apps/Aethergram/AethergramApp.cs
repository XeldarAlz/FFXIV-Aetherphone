using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Conduct;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Net;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Report;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Wallpapers;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Aethergram;

internal sealed partial class AethergramApp : IPhoneApp
{
    private const int MaxCaptionLength = 500;
    private const int MaxPhotoTags = 20;
    private const int MaxCommentLength = 500;
    private const float BottomNavHeight = 52f;
    private const int NavTabCount = 4;
    private const float NavHitRadius = 17f;
    private const float NavHoverSmoothTime = 0.12f;
    private const float NavHoverMaxFrameSeconds = 0.1f;
    private const float NavPillWidth = 40f;
    private const float NavPillHeight = 34f;
    private const float NavPillAlpha = 0.10f;
    private const int GridColumns = 3;
    private const float LikeBurstDuration = 0.9f;
    private const float TagModeBarHeight = 28f;

    public string Id => "aethergram";
    public Vector4 Accent => AppAccents.For(Id);
    public string DisplayName => Loc.T(L.Apps.Aethergram);
    public string Glyph => "Ag";
    public int BadgeCount => dmStore.UnreadCount;
    private const string ScopeMenuId = "scope";
    private readonly Dictionary<SocialFeedScope, PullToRefresh> pullToRefresh = new()
    {
        { SocialFeedScope.ForYou, new() },
        { SocialFeedScope.Following, new() }
    };
    private readonly AethergramStore store;
    private readonly GramDmStore dmStore;
    private readonly SocialLauncher launcher;
    private readonly GramDmLauncher dmLauncher;
    private readonly GameData gameData;
    private readonly Configuration configuration;
    private readonly LodestoneService lodestone;
    private readonly PhotoLibrary library;
    private readonly RemoteImageCache images;
    private readonly HttpService http;
    private readonly SocialNotificationService social;
    private readonly ConductGateService conduct;
    private readonly ConfirmService confirm;
    private readonly ReportService report;
    private readonly WallpaperImageCache wallpaperImages;
    private readonly DropdownMenu scopeMenu = new();
    private readonly DropdownMenu postMenu = new();
    private readonly DropdownMenu.Item[] scopeItems = new DropdownMenu.Item[2];
    private readonly DropdownMenu.Item[] postItems = new DropdownMenu.Item[4];
    private readonly Action<NotificationDto> openActivityActor;
    private readonly Action<NotificationDto> openActivityPost;
    private readonly SocialActivityFeed activityFeed;
    private readonly Action loadOlderActivity;
    private PostDto? menuPost;
    private readonly StoryPresenter stories;
    private readonly PhotoViewerOverlay photoViewer = new();
    private readonly AvatarLightbox avatarLightbox = new();
    private readonly PhotoCarousel carousel = new();
    private string? pendingViewUrl;
    private double pendingViewAt;
    private readonly AppSkin ui = new(AppPalettes.Aethergram);
    private readonly SocialProfilePages profile;
    private readonly RichTextCache bodyLayouts = new();
    private readonly FeedVirtualizer feedVirtualizer = new(400f);
    private readonly RichTextCache detailBodyLayouts = new();
    private readonly RichTextCache commentLayouts = new();
    private readonly MentionPopup mentionPopup = new();
    private readonly EmojiComposer commentEmoji = new();
    private readonly EmojiComposer captionEmoji = new();
    private readonly MentionAutocomplete composeMentions;
    private readonly MentionAutocomplete commentMentions;
    private readonly ViewRouter<AethergramRoute> router;
    private readonly RouterDraw<AethergramRoute> drawView;
    private readonly Action back;
    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private AethergramTab activeTab = AethergramTab.Home;
    private readonly Spring[] navHover = new Spring[NavTabCount];
    private SocialFeedScope activeScope = SocialFeedScope.ForYou;
    private bool feedScrollTopPending;
    private bool commentFocusPending;
    private readonly PhotoComposeSession composeSession;
    private bool composeAvatarMode;
    private bool composeStoryMode;
    private static readonly LocString[] ProfileTabs = { L.PhotoTag.PostsTab, L.PhotoTag.TaggedTab };
    private readonly string[] profileTabLabels = new string[ProfileTabs.Length];
    private int profileTab;
    private bool composeTagMode;
    private int composeTagPhotoIndex;
    private Vector2 composeTagPoint;
    private readonly List<PhotoTagDto> composeTags = new();
    private readonly PhotoTagOverlay tagOverlay = new();
    private readonly PersonPicker personPicker;
    private string caption = string.Empty;
    private bool captionFocus;
    private string composeStatus = string.Empty;
    private volatile int composeOutcome;
    private string commentDraft = string.Empty;
    private string likeBurstPostId = string.Empty;
    private double likeBurstStart;

    public AethergramApp(AethernetSession session, AethernetApi net, LodestoneService lodestone,
        RemoteImageCache images, PhotoLibrary library, SocialLauncher launcher, GramDmLauncher dmLauncher,
        GameData gameData, Configuration configuration, SocialNotificationService social,
        NotificationService notifications, HttpService http, KeyVault keyVault,
        ConversationKeyStore conversationKeys, PhoneVisibility visibility, RealtimeSignalBus realtimeSignals,
        WallpaperImageCache wallpaperImages, ConfirmService confirm, ReportService report, ConductGateService conduct)
    {
        store = new AethergramStore(session, net.Account, net.Social, net.Grams, net.Safety, net.Media);
        dmStore = new GramDmStore(session, net.GramDm, net.Safety, net.Media, notifications, keyVault,
            conversationKeys, visibility, realtimeSignals);
        composeMentions = new MentionAutocomplete(store.NewMentionSuggestions());
        commentMentions = new MentionAutocomplete(store.NewMentionSuggestions());
        personPicker = new PersonPicker(store.NewMentionSuggestions());
        stories = new StoryPresenter(session, net.Grams, net.Media, images, lodestone, AethergramArt.StoryRing,
            AppPalettes.Aethergram, new StoryConfirmLabels(L.Aethergram.DeleteConfirm, L.Aethergram.DeleteCancel,
                L.Aethergram.Saving), confirm, "Aethergram stories", StartStoryCompose);
        this.launcher = launcher;
        this.dmLauncher = dmLauncher;
        this.gameData = gameData;
        this.configuration = configuration;
        this.lodestone = lodestone;
        this.library = library;
        composeSession = new PhotoComposeSession(library, wallpaperImages);
        this.images = images;
        this.http = http;
        this.social = social;
        this.conduct = conduct;
        this.confirm = confirm;
        this.report = report;
        this.wallpaperImages = wallpaperImages;
        activityFeed = new SocialActivityFeed(SocialActivity.AethergramApp, session, net.Account);
        loadOlderActivity = activityFeed.LoadOlder;
        router = new ViewRouter<AethergramRoute>(AethergramRoute.Home);
        drawView = DrawView;
        back = () => router.Pop();
        openActivityActor = item => OpenProfile(item.ActorId);
        openActivityPost = item => OpenDetailFromLink(item.PostId!);
        profile = new SocialProfilePages(store, ui, new SocialProfileStyle
        {
            Palette = AppPalettes.Aethergram,
            SearchInputId = "##aethergramSearch",
            StatsPostsFirst = true,
            CountGrams = true,
            CardUserRows = false,
            EditProfile = L.Aethergram.EditProfile,
            Follow = L.Aethergram.Follow,
            Following = L.Aethergram.Following,
            Posts = L.Aethergram.Posts,
            Save = L.Aethergram.Save,
            Saving = L.Aethergram.Saving,
            HandleTaken = L.Aethergram.HandleTaken,
            HandleRules = L.Aethergram.HandleRules,
            HandleLabel = L.Aethergram.HandleLabel,
            DisplayNameLabel = L.Aethergram.DisplayNameLabel,
            BioLabel = L.Aethergram.BioLabel,
            ChangePhoto = L.Aethergram.ChangePhoto,
            ProfileError = L.Aethergram.ProfileError,
            NameOrWorld = L.Aethergram.NameOrWorld,
            SearchByName = L.Aethergram.SearchByName,
            DeleteConfirmMessage = L.Aethergram.DeleteConfirmMessage,
            DeleteConfirm = L.Aethergram.DeleteConfirm,
            DeleteCancel = L.Aethergram.DeleteCancel,
            DeleteFailed = L.Aethergram.DeleteFailed,
            DeleteCommentConfirmMessage = L.Aethergram.DeleteCommentConfirmMessage,
            DeleteCommentFailed = L.Aethergram.DeleteCommentFailed,
            MessageLabel = L.Aethergram.MessageButton,
        }, images, lodestone, avatarLightbox, configuration, gameData, confirm, report,
            () => router.Push(AethergramRoute.EditProfile), () => StartCompose(true), OpenProfile, OpenUserList, back,
            null, OpenThread);
        threadView = new ThreadView(this);
    }

    public void OnOpened()
    {
        router.Reset();
        activeTab = AethergramTab.Home;
        if (store.IsSignedIn)
        {
            store.RefreshFeed(SocialFeedScope.ForYou);
            store.RefreshFeed(SocialFeedScope.Following);
            stories.RefreshTray();
        }

        if (store.IsSignedIn && dmLauncher.TryConsume(out var threadUserId))
        {
            router.Push(AethergramRoute.Thread(threadUserId), false);
            return;
        }

        if (store.IsSignedIn && launcher.TryConsume(Id, out var link))
        {
            if (link.Kind == SocialLinkKind.Profile)
            {
                OpenProfile(link.Id);
            }
            else
            {
                OpenDetailFromLink(link.Id);
            }
        }
    }

    public void OnClosed()
    {
        threadView.OnAppClosed();
        router.Reset();
        avatarLightbox.Reset();
        caption = string.Empty;
        profile.SearchDraft = string.Empty;
        commentDraft = string.Empty;
        store.ClearDiscover();
        stories.Close();
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = theme;
        scopeMenu.Gate();
        postMenu.Gate();
        threadView.GateMenus();
        var screen = SceneChrome.ScreenFrom(context.Content, theme, ImGuiHelpers.GlobalScale);
        ui.Backdrop(screen);
        AdvancePendingPhotoView();
        stories.Advance();
        if (photoViewer.Active)
        {
            photoViewer.Draw(screen, theme);
            return;
        }

        if (stories.Active)
        {
            stories.DrawViewer(screen, theme);
            return;
        }

        using (InputShield.Engage(avatarLightbox.Expanded))
        {
            router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
        }

        if (avatarLightbox.Active)
        {
            avatarLightbox.Draw(screen, theme);
        }
    }

    private void DrawView(AethergramRoute route, Rect area, int depth)
    {
        ui.Body(area);
        switch (route.Screen)
        {
            case AethergramScreen.Compose:
                DrawCompose(area);
                break;
            case AethergramScreen.Detail:
                DrawDetail(area, route.Id!);
                break;
            case AethergramScreen.Profile:
                DrawProfile(area, route.Id!);
                break;
            case AethergramScreen.EditProfile:
                profile.DrawEditProfile(area, theme, navigation);
                break;
            case AethergramScreen.UserList:
                profile.DrawUserList(area, theme, navigation, route.Id!, route.Kind);
                break;
            case AethergramScreen.Inbox:
                DrawInbox(area);
                break;
            case AethergramScreen.Thread:
                threadView.Draw(area, route.Id!);
                break;
            case AethergramScreen.ChatImage:
                threadView.DrawImagePicker(area, route.Id!);
                break;
            case AethergramScreen.ImageView:
                threadView.DrawImageViewer(area, route.Id!);
                break;
            case AethergramScreen.Reactions:
                threadView.DrawReactions(area, route.Id!);
                break;
            default:
                DrawRoot(area);
                break;
        }
    }

    private void DrawRoot(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var headerRect = new Rect(area.Min, new Vector2(area.Max.X, area.Min.Y + AppHeader.Height * scale));
        DrawRootHeader(headerRect);
        var contentArea = new Rect(new Vector2(area.Min.X, headerRect.Max.Y), area.Max);
        if (!store.IsSignedIn)
        {
            TourHolds.Hold(Id);
            Typography.DrawCentered(contentArea.Center, Loc.T(L.Aethergram.SetUpAccount), AppPalettes.Aethergram.MutedInk);
            return;
        }

        TourHolds.Release(Id);
        if (GuideIntents.Consume("aethergram.tab.search"))
        {
            SelectTab(AethergramTab.Search);
        }

        if (GuideIntents.Consume("aethergram.tab.profile"))
        {
            SelectTab(AethergramTab.Profile);
        }

        var navRect = new Rect(new Vector2(area.Min.X, area.Max.Y - BottomNavHeight * scale), area.Max);
        var tabArea = new Rect(contentArea.Min, new Vector2(area.Max.X, navRect.Min.Y));
        switch (activeTab)
        {
            case AethergramTab.Search:
                DrawSearchTab(tabArea);
                break;
            case AethergramTab.Activity:
                DrawActivityTab(tabArea);
                break;
            case AethergramTab.Profile:
                DrawProfileTab(tabArea);
                break;
            default:
                DrawFeedTab(tabArea);
                break;
        }

        DrawBottomNav(navRect);
        DrawScopeMenu(area);
        DrawPostMenu(area, true);
    }

    private void DrawRootHeader(Rect area)
    {
        switch (activeTab)
        {
            case AethergramTab.Search:
                DrawTabTitle(area, Loc.T(L.Aethergram.FindPeople));
                break;
            case AethergramTab.Activity:
                DrawTabTitle(area, Loc.T(L.Social.ActivityTitle));
                break;
            case AethergramTab.Profile:
                DrawTabTitle(area, store.Me is { } me ? SocialIdentity.Name(me.DisplayName, me.Handle)
                    : Loc.T(L.Aethergram.Profile));
                break;
            default:
                DrawHomeTopBar(area);
                break;
        }
    }

    private void DrawTabTitle(Rect area, string title)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        Typography.DrawCentered(new Vector2(area.Center.X, rowCenterY), title, AppPalettes.Aethergram.TitleInk, 1.2f,
            FontWeight.SemiBold);
    }

    private void DrawFeedTab(Rect area)
    {
        DrawFeedList(area, activeScope);
    }

    private void RefreshActiveFeed()
    {
        if (!store.IsSignedIn || store.IsLoading(activeScope))
        {
            return;
        }

        feedScrollTopPending = true;
        RefreshFeed(activeScope);
    }

    private void RefreshFeed(SocialFeedScope scope)
    {
        store.RefreshFeed(scope);
        stories.RefreshTray();
    }

    private void DrawActivityTab(Rect area)
    {
        activityFeed.EnsureFresh(social.Latest);
        SocialActivityList.Draw(area, ui, AppPalettes.Aethergram, theme, activityFeed.Items, Id, images, lodestone,
            openActivityActor, openActivityPost, loadOlderActivity);
    }

    private void DrawProfileTab(Rect area)
    {
        if (store.Me is not { } me)
        {
            store.EnsureMe();
            Typography.DrawCentered(area.Center, Loc.T(L.Common.Loading), AppPalettes.Aethergram.MutedInk);
            return;
        }

        DrawProfileBody(area, me.Id);
    }

    private void SelectTab(AethergramTab tab)
    {
        if (tab == AethergramTab.Home && activeTab == AethergramTab.Home)
        {
            RefreshActiveFeed();
            return;
        }

        activeTab = tab;
        scopeMenu.Close();
        postMenu.Close();
        switch (tab)
        {
            case AethergramTab.Home:
                profile.EnsureLoaded(activeScope);
                break;
            case AethergramTab.Search:
                store.ClearDiscover();
                profile.SearchDraft = string.Empty;
                break;
            case AethergramTab.Activity:
                social.RefreshNow();
                social.MarkSeen(Id);
                activityFeed.Invalidate();
                break;
            case AethergramTab.Profile:
                store.EnsureMe();
                break;
        }
    }

    private void DrawScopeMenu(Rect area)
    {
        if (!scopeMenu.IsOpenFor(ScopeMenuId))
        {
            return;
        }

        scopeItems[0] = new DropdownMenu.Item(Loc.T(L.Aethergram.ForYou),
            Selected: activeScope == SocialFeedScope.ForYou);
        scopeItems[1] = new DropdownMenu.Item(Loc.T(L.Aethergram.Following),
            Selected: activeScope == SocialFeedScope.Following);
        var picked = scopeMenu.Draw(area, theme, scopeItems);
        if (picked < 0)
        {
            return;
        }

        var scope = picked == 1 ? SocialFeedScope.Following : SocialFeedScope.ForYou;
        if (scope != activeScope)
        {
            activeScope = scope;
            profile.EnsureLoaded(activeScope);
        }
    }

    private void DrawPostMenu(Rect area, bool includeView)
    {
        if (menuPost is not { } post || !postMenu.IsOpenFor(post.Id))
        {
            return;
        }

        var mine = store.Me is { } me && me.Id == post.AuthorId;
        var count = 0;
        if (includeView)
        {
            postItems[count++] = new DropdownMenu.Item(Loc.T(L.Aethergram.ViewPost),
                FontAwesomeIcon.Expand.ToIconString());
        }

        if (mine)
        {
            postItems[count++] = new DropdownMenu.Item(Loc.T(L.Aethergram.DeleteConfirm),
                FontAwesomeIcon.Trash.ToIconString(), true);
        }
        else
        {
            postItems[count++] = new DropdownMenu.Item(
                Loc.T(post.IsFollowing ? L.Aethergram.Unfollow : L.Aethergram.Follow),
                (post.IsFollowing ? FontAwesomeIcon.UserCheck : FontAwesomeIcon.UserPlus).ToIconString());
            postItems[count++] = new DropdownMenu.Item(Loc.T(L.Report.Action),
                FontAwesomeIcon.Flag.ToIconString(), true);
            postItems[count++] = new DropdownMenu.Item(Loc.T(L.Social.BlockAction),
                FontAwesomeIcon.Ban.ToIconString(), true);
        }

        var picked = postMenu.Draw(area, theme, postItems.AsSpan(0, count));
        if (picked < 0)
        {
            return;
        }

        var viewOffset = includeView ? 1 : 0;
        if (includeView && picked == 0)
        {
            OpenDetail(post);
            return;
        }

        if (mine)
        {
            profile.AskDeletePost(post.Id, back);
            return;
        }

        if (picked == viewOffset)
        {
            store.SetFollow(post.AuthorId, !post.IsFollowing);
            return;
        }

        if (picked == viewOffset + 1)
        {
            profile.OpenReport("post", post.Id, Loc.T(L.Report.PostTitle));
            return;
        }

        profile.AskBlock(post.AuthorDisplayName, post.AuthorHandle, post.AuthorId);
    }

    private void DrawFeedList(Rect listRect, SocialFeedScope scope)
    {
        var snapshot = store.Feed(scope);
        using (var surface = AppSurface.Begin(listRect))
        {
            if (feedScrollTopPending)
            {
                surface.JumpToTop();
                feedScrollTopPending = false;
            }

            pullToRefresh[scope].Draw(listRect, surface.Pull, surface.Dragging,
                store.IsLoading(scope), AppPalettes.Aethergram.MutedInk, () => RefreshFeed(scope));
            stories.DrawTray(theme);
            if (snapshot.Length == 0)
            {
                var message = store.IsLoading(scope) ? Loc.T(L.Common.Loading) :
                    scope == SocialFeedScope.Following ? Loc.T(L.Aethergram.FollowingEmpty) :
                    Loc.T(L.Aethergram.ExploreEmpty);
                Typography.DrawCentered(
                    new Vector2(listRect.Center.X, ImGui.GetCursorScreenPos().Y + 60f * ImGuiHelpers.GlobalScale),
                    message, AppPalettes.Aethergram.MutedInk);
            }
            else
            {
                ImGui.Dummy(new Vector2(0f, 4f * ImGuiHelpers.GlobalScale));
                feedVirtualizer.BeginFrame();
                for (var index = 0; index < snapshot.Length; index++)
                {
                    var post = snapshot[index];
                    var revision = post.CommentCount > 0 ? 1 : 0;
                    if (feedVirtualizer.Skip(post.Id, revision))
                    {
                        continue;
                    }

                    DrawGramCard(post);
                    feedVirtualizer.Record(post.Id, revision);
                }

                if (store.LoadingMore(scope))
                {
                    InfiniteScroll.DrawLoadingRow(listRect.Center.X, AppPalettes.Aethergram.MutedInk);
                }

                ImGui.Dummy(new Vector2(0f, 16f * ImGuiHelpers.GlobalScale));
                if (InfiniteScroll.ReachedBottom() && store.HasMoreFeed(scope) && !store.LoadingMore(scope))
                {
                    store.LoadMoreFeed(scope);
                }
            }
        }
    }

    private void DrawGramCard(PostDto post)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = 14f * scale;
        var innerX = origin.X + pad;
        var innerWidth = width - pad * 2f;
        var displayName = SocialIdentity.Name(post.AuthorDisplayName, post.AuthorHandle);
        var headerBlock = 40f * scale;
        var avatarRadius = 18f * scale;
        var imageTop = origin.Y + pad + headerBlock + 12f * scale;
        var imageBottom = imageTop + innerWidth;
        var actionsTop = imageBottom + 12f * scale;
        var actionsHeight = 24f * scale;
        var textTop = actionsTop + actionsHeight + 10f * scale;
        RichTextLayout? captionLayout = null;
        if (post.Text.Length > 0)
        {
            using (Plugin.Fonts.Push(0.95f))
            {
                captionLayout = bodyLayouts.LayoutFor(post.Id, post.Text, post.Mentions, innerWidth);
            }
        }

        var captionHeight = post.Text.Length == 0
            ? 0f
            : (captionLayout?.Size.Y ?? Typography.MeasureWrapped(post.Text, innerWidth, 0.95f)) + 6f * scale;
        var commentsHeight = post.CommentCount > 0 ? 20f * scale : 0f;
        var cardBottom = textTop + captionHeight + commentsHeight + pad;
        ui.Card(drawList, origin, new Vector2(origin.X + width, cardBottom), 18f * scale);
        var avatarCenter = new Vector2(innerX + avatarRadius, origin.Y + pad + avatarRadius);
        var ringRadius = avatarRadius + 3f * scale;
        var hasStory = stories.TryRing(post.AuthorId, out var authorRing);
        if (hasStory)
        {
            AethergramArt.StoryRing(drawList, avatarCenter, ringRadius, scale, authorRing.HasUnseen);
        }

        DrawAvatar(avatarCenter, avatarRadius - 1f * scale, post.AuthorName, post.AuthorWorld, post.AuthorAvatarUrl,
            0.85f, 32);
        var nameLeft = avatarCenter.X + avatarRadius + 12f * scale;
        var headerTextRight = origin.X + width - pad - 34f * scale;
        var headerTextMaxWidth = MathF.Max(1f, headerTextRight - nameLeft);
        var cardNameStyle = new TextStyle(1f, FontWeight.SemiBold);
        var cardNameHeight = Typography.Measure(displayName, cardNameStyle).Y;
        var cardNameHovering = ImGui.IsMouseHoveringRect(new Vector2(nameLeft, origin.Y + pad),
            new Vector2(nameLeft + headerTextMaxWidth, origin.Y + pad + cardNameHeight));
        Marquee.DrawLeft("aethergram.card." + post.Id, displayName, nameLeft, origin.Y + pad, headerTextMaxWidth,
            cardNameStyle, theme.TextStrong, cardNameHovering);
        var subline = SocialIdentity.FeedMeta(post.AuthorHandle, TimeText.Short(post.CreatedAtUnix));
        var sublineTop = origin.Y + pad + 21f * scale;
        var sublineSize = Typography.Measure(Typography.FitText(subline, headerTextMaxWidth, 0.85f, FontWeight.Regular), 0.85f);
        var sublineHovering = ImGui.IsMouseHoveringRect(new Vector2(nameLeft, sublineTop),
            new Vector2(nameLeft + headerTextMaxWidth, sublineTop + sublineSize.Y));
        Marquee.DrawLeft("aethergram.card.sub." + post.Id, subline, nameLeft, sublineTop, headerTextMaxWidth,
            new TextStyle(0.85f, FontWeight.Regular), AppPalettes.Aethergram.MutedInk, sublineHovering);
        if (hasStory && UiInteract.HoverClickCircle(avatarCenter, ringRadius))
        {
            stories.OpenRing(authorRing);
        }
        else if (UiInteract.HoverClick(new Vector2(innerX, origin.Y + pad),
                new Vector2(origin.X + width - pad - 30f * scale, origin.Y + pad + headerBlock)))
        {
            OpenProfile(post.AuthorId);
        }

        var moreCenter = new Vector2(origin.X + width - pad - 6f * scale, avatarCenter.Y);
        var moreRadius = 14f * scale;
        if (ui.IconButton(moreCenter, moreRadius, FontAwesomeIcon.EllipsisH.ToIconString(), AppPalettes.Aethergram.BodyInk,
                AppSkin.Transparent, 1f, Loc.T(L.Aethergram.More)))
        {
            menuPost = post;
            postMenu.Toggle(post.Id, new Rect(moreCenter - new Vector2(moreRadius, moreRadius),
                moreCenter + new Vector2(moreRadius, moreRadius)));
        }

        var imageRect = new Rect(new Vector2(innerX, imageTop), new Vector2(innerX + innerWidth, imageBottom));
        var photos = PostMedia.Photos(post.MediaUrls, post.MediaUrl);
        var page = DrawGramCarousel(imageRect, post, photos, 14f * scale);
        var liked = post.MyReaction >= 0;
        var actionCenterY = actionsTop + actionsHeight * 0.5f;
        var heartCenter = new Vector2(innerX + 13f * scale, actionCenterY);
        if (ui.IconButton(heartCenter, 15f * scale, FontAwesomeIcon.Heart.ToIconString(),
                liked ? CommentHeart.LikeRed : AppPalettes.Aethergram.BodyInk, AppSkin.Transparent, 1.25f, Loc.T(L.Aethergram.Like)))
        {
            store.ToggleLike(post);
        }

        var cursorX = heartCenter.X + 20f * scale;
        if (post.TotalReactions > 0)
        {
            var likeText = post.TotalReactions.ToString(Loc.Culture);
            Typography.Draw(new Vector2(cursorX, actionCenterY - 8f * scale), likeText, AppPalettes.Aethergram.BodyInk, 0.9f,
                FontWeight.Medium);
            cursorX += Typography.Measure(likeText, 0.9f, FontWeight.Medium).X + 14f * scale;
        }
        else
        {
            cursorX += 6f * scale;
        }

        var commentCenter = new Vector2(cursorX + 6f * scale, actionCenterY);
        if (ui.IconButton(commentCenter, 15f * scale, FontAwesomeIcon.Comment.ToIconString(), AppPalettes.Aethergram.BodyInk,
                AppSkin.Transparent, 1.2f, Loc.T(L.Aethergram.Comment)))
        {
            OpenDetail(post, true);
        }

        var actionsRight = commentCenter.X + 20f * scale;
        if (post.CommentCount > 0)
        {
            var commentText = post.CommentCount.ToString(Loc.Culture);
            Typography.Draw(new Vector2(actionsRight, actionCenterY - 8f * scale), commentText,
                AppPalettes.Aethergram.BodyInk, 0.9f, FontWeight.Medium);
            actionsRight += Typography.Measure(commentText, 0.9f, FontWeight.Medium).X;
        }

        if (photos.Length > 1)
        {
            var dotsCenter = new Vector2(origin.X + width * 0.5f, actionCenterY);
            var dotsRoom = (origin.X + width - pad - dotsCenter.X) * 2f;
            var available = MathF.Min(dotsRoom, (dotsCenter.X - actionsRight - 10f * scale) * 2f);
            PhotoCarousel.DrawDots(drawList, dotsCenter, photos.Length, page, available,
                AppPalettes.Aethergram.BodyInk);
        }

        var y = textTop;
        if (post.Text.Length > 0)
        {
            if (captionLayout is null)
            {
                ImGui.SetCursorScreenPos(new Vector2(innerX, y));
                ImGui.PushTextWrapPos(innerX + innerWidth - ImGui.GetWindowPos().X);
                using (ImRaii.PushColor(ImGuiCol.Text, AppPalettes.Aethergram.BodyInk))
                using (Plugin.Fonts.Push(0.95f))
                {
                    Typography.Wrapped(post.Text);
                }

                ImGui.PopTextWrapPos();
            }
            else
            {
                using (Plugin.Fonts.Push(0.95f))
                {
                    DrawRichBody(drawList, captionLayout, new Vector2(innerX, y));
                }
            }

            y += captionHeight;
        }

        if (post.CommentCount > 0)
        {
            var commentsLabel = Loc.T(L.Aethergram.ViewComments, post.CommentCount);
            var labelPos = new Vector2(innerX, y + 2f * scale);
            Typography.Draw(labelPos, commentsLabel, AppPalettes.Aethergram.MutedInk, 0.85f);
            var labelSize = Typography.Measure(commentsLabel, 0.85f);
            if (UiInteract.HoverClick(labelPos, labelPos + labelSize))
            {
                OpenDetail(post, false);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cardBottom - origin.Y + 12f * scale));
    }

    private int DrawGramCarousel(Rect imageRect, PostDto post, string[] photos, float rounding)
    {
        var scanStatus = post.ScanStatus;
        var result = carousel.Draw(ImGui.GetWindowDrawList(), imageRect, post.Id, photos, rounding,
            (list, min, max, radius, url) => DrawGramImage(list, new Rect(min, max), url, radius, scanStatus));
        if (result.InputConsumed)
        {
            pendingViewUrl = null;
        }
        else
        {
            HandleLikeGesture(imageRect, post, photos, result.Index);
        }

        var tags = tagOverlay.Draw(ImGui.GetWindowDrawList(), imageRect, post.Id, result.Index, post.PhotoTags,
            theme, ImGui.GetIO().DeltaTime);
        if (tags.InputConsumed)
        {
            pendingViewUrl = null;
        }

        if (tags.OpenUserId is { } taggedUserId)
        {
            OpenProfile(taggedUserId);
        }

        DrawLikeBurst(imageRect, post.Id);
        return result.Index;
    }

    private void HandleLikeGesture(Rect imageRect, PostDto post, string[] photos, int page)
    {
        if (!ImGui.IsMouseHoveringRect(imageRect.Min, imageRect.Max))
        {
            return;
        }

        if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
        {
            pendingViewUrl = null;
            if (post.MyReaction < 0)
            {
                store.ToggleLike(post);
            }

            likeBurstPostId = post.Id;
            likeBurstStart = ImGui.GetTime();
            return;
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && page < photos.Length)
        {
            pendingViewUrl = photos[page];
            pendingViewAt = ImGui.GetTime();
        }
    }

    private void AdvancePendingPhotoView()
    {
        if (pendingViewUrl is not { } url)
        {
            return;
        }

        if (DragScrollHost.AnyDragging)
        {
            pendingViewUrl = null;
            return;
        }

        if (ImGui.GetTime() - pendingViewAt < 0.30)
        {
            return;
        }

        pendingViewUrl = null;
        photoViewer.Open(() => images.Get(url));
    }

    private void DrawLikeBurst(Rect imageRect, string postId)
    {
        if (likeBurstPostId != postId)
        {
            return;
        }

        var elapsed = (float)(ImGui.GetTime() - likeBurstStart);
        if (elapsed >= LikeBurstDuration)
        {
            likeBurstPostId = string.Empty;
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var appear = Math.Clamp(elapsed / 0.22f, 0f, 1f);
        var back = appear - 1f;
        var pop = MathF.Max(1f + back * back * (2.70158f * back + 1.70158f), 0.05f);
        var alpha = elapsed < 0.55f ? 1f : 1f - (elapsed - 0.55f) / (LikeBurstDuration - 0.55f);
        var rise = elapsed < 0.55f ? 0f : (elapsed - 0.55f) * 46f * scale;
        var center = new Vector2(imageRect.Center.X, imageRect.Center.Y - rise);
        AppSkin.Icon(center + new Vector2(0f, 2f * scale), FontAwesomeIcon.Heart.ToIconString(),
            new Vector4(0f, 0f, 0f, 0.35f * alpha), 4.5f * pop);
        AppSkin.Icon(center, FontAwesomeIcon.Heart.ToIconString(), new Vector4(1f, 1f, 1f, alpha), 4.4f * pop);
    }

    private void DrawGramImage(Rect rect, string? url, float rounding, string? scanStatus = null) =>
        DrawGramImage(ImGui.GetWindowDrawList(), rect, url, rounding, scanStatus);

    private void DrawGramImage(ImDrawListPtr drawList, Rect rect, string? url, float rounding,
        string? scanStatus = null)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var texture = images.Get(url);
        if (texture is null)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(AppPalettes.Aethergram.FieldSurface));
            Typography.DrawCentered(rect.Center,
                Loc.T(images.Failed(url) ? L.Common.ImageFailed : L.Common.Loading), AppPalettes.Aethergram.MutedInk, 0.85f);
        }
        else
        {
            var (uv0, uv1) = ImageFit.CoverSquare(texture.Size);
            drawList.AddImageRounded(texture.Handle, rect.Min, rect.Max, uv0, uv1, 0xFFFFFFFFu, rounding,
                ImDrawFlags.RoundCornersAll);
        }

        ModerationOverlay.Draw(drawList, rect.Min, rect.Max, rounding, scanStatus);
    }

    private void DrawBottomNav(Rect bar)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddLine(bar.Min, new Vector2(bar.Max.X, bar.Min.Y), ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)),
            1f);
        var slot = bar.Width / 4f;
        var centerY = bar.Center.Y;
        var searchCenter = new Vector2(bar.Min.X + slot * 1.5f, centerY);
        var activityCenter = new Vector2(bar.Min.X + slot * 2.5f, centerY);
        var profileCenter = new Vector2(bar.Min.X + slot * 3.5f, centerY);
        var anchorHalf = new Vector2(20f * scale, 20f * scale);
        UiAnchors.Report("aethergram.tab.search", new Rect(searchCenter - anchorHalf, searchCenter + anchorHalf));
        UiAnchors.Report("aethergram.tab.activity", new Rect(activityCenter - anchorHalf, activityCenter + anchorHalf));
        UiAnchors.Report("aethergram.tab.profile", new Rect(profileCenter - anchorHalf, profileCenter + anchorHalf));
        DrawNavIcon(new Vector2(bar.Min.X + slot * 0.5f, centerY), FontAwesomeIcon.Home, AethergramTab.Home,
            Loc.T(L.Aethergram.Home));
        DrawNavIcon(searchCenter, FontAwesomeIcon.Search, AethergramTab.Search, Loc.T(L.Aethergram.Search));
        DrawNavIcon(activityCenter, FontAwesomeIcon.Heart, AethergramTab.Activity, Loc.T(L.Social.ActivityTab));
        ActivityBadge.Draw(activityCenter + new Vector2(11f * scale, -10f * scale), social.UnseenCount(Id), theme,
            scale);
        DrawNavProfile(profileCenter);
    }

    private float StepNavHover(AethergramTab tab, Vector2 center)
    {
        var hit = new Vector2(NavHitRadius * ImGuiHelpers.GlobalScale, NavHitRadius * ImGuiHelpers.GlobalScale);
        var hovered = UiInteract.Hover(center - hit, center + hit);
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, NavHoverMaxFrameSeconds);
        navHover[(int)tab].Step(hovered ? 1f : 0f, NavHoverSmoothTime, delta);
        return Math.Clamp(navHover[(int)tab].Value, 0f, 1f);
    }

    private void DrawNavHoverPill(Vector2 center, float hover)
    {
        if (hover <= 0.001f)
        {
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var grow = 0.86f + 0.14f * hover;
        var half = new Vector2(NavPillWidth * 0.5f * scale * grow, NavPillHeight * 0.5f * scale * grow);
        var tint = Palette.WithAlpha(ui.HoverTint, NavPillAlpha * hover);
        Squircle.Fill(ImGui.GetWindowDrawList(), center - half, center + half, half.Y, ImGui.GetColorU32(tint));
    }

    private void DrawNavIcon(Vector2 center, FontAwesomeIcon icon, AethergramTab tab, string label)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var active = activeTab == tab;
        DrawNavHoverPill(center, StepNavHover(tab, center));
        var color = active ? AppPalettes.Aethergram.TitleInk : AppPalettes.Aethergram.MutedInk;
        if (ui.IconButton(center, NavHitRadius * scale, icon.ToIconString(), color, AppSkin.Transparent,
                active ? 1.3f : 1.2f, label))
        {
            SelectTab(tab);
        }
    }

    private void DrawNavProfile(Vector2 center)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var active = activeTab == AethergramTab.Profile;
        var label = Loc.T(L.Aethergram.Profile);
        DrawNavHoverPill(center, StepNavHover(AethergramTab.Profile, center));
        if (store.Me is not { } me)
        {
            store.EnsureMe();
            var color = active ? AppPalettes.Aethergram.TitleInk : AppPalettes.Aethergram.MutedInk;
            if (ui.IconButton(center, NavHitRadius * scale, FontAwesomeIcon.User.ToIconString(), color,
                    AppSkin.Transparent, 1.15f, label))
            {
                SelectTab(AethergramTab.Profile);
            }

            return;
        }

        var radius = 14f * scale;
        if (active)
        {
            ImGui.GetWindowDrawList().AddCircle(center, radius + 3f * scale,
                ImGui.GetColorU32(AppPalettes.Aethergram.TitleInk), 32, 1.6f * scale);
        }

        DrawAvatar(center, radius, me.Name, me.World, me.AvatarUrl, 0.85f, 28);
        var hit = new Vector2(NavHitRadius * scale, NavHitRadius * scale);
        HoverTooltip.Show(new Rect(center - hit, center + hit), label, HoverLabelSide.Above);
        if (UiInteract.HoverClick(center - hit, center + hit))
        {
            SelectTab(AethergramTab.Profile);
        }
    }

    private void DrawAvatar(Vector2 center, float radius, string name, string world, string? avatarUrl,
        float monogramScale, int segments)
    {
        AvatarView.DrawRemote(ImGui.GetWindowDrawList(), center, radius, theme, name, world, avatarUrl, images,
            lodestone, monogramScale, segments);
    }

    private void OpenProfile(string userId)
    {
        profileTab = 0;
        store.OpenProfile(userId);
        router.Push(AethergramRoute.Profile(userId));
    }

    private void DrawRichBody(ImDrawListPtr drawList, RichTextLayout layout, Vector2 origin)
    {
        var ink = new RichTextInk(AppPalettes.Aethergram.BodyInk, AppPalettes.Aethergram.Accent,
            AppPalettes.Aethergram.Accent);
        RichText.Draw(drawList, layout, origin, ink, out var hit);
        if (hit.Kind == RichTextRunKind.Mention && hit.Clicked)
        {
            OpenProfile(layout.Mentions[hit.TargetIndex].UserId);
        }
    }

    private void OpenDetail(PostDto post, bool focusComment = false)
    {
        store.OpenDetail(post);
        commentDraft = string.Empty;
        commentFocusPending = focusComment;
        router.Push(AethergramRoute.Detail(post.Id));
    }

    private void OpenDetailFromLink(string postId)
    {
        store.OpenDetailById(postId);
        commentDraft = string.Empty;
        commentFocusPending = false;
        router.Push(AethergramRoute.Detail(postId));
    }

    private void OpenUserList(string sourceId, UserListKind kind)
    {
        store.OpenUserList(sourceId, kind);
        router.Push(AethergramRoute.UserList(sourceId, kind));
    }

    public void Dispose()
    {
        threadView.Dispose();
        dmStore.Dispose();
        store.Dispose();
        stories.Dispose();
    }
}
