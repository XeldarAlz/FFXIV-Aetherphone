using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Conduct;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Game;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Net;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Report;
using Aetherphone.Core.Runtime;
using Aetherphone.Core.Sharing;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Translation;
using Aetherphone.Core.Wallpapers;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Aethergram;

internal sealed partial class AethergramApp : IResumableApp
{
    private enum PostSheetAction
    {
        View,
        Delete,
        Follow,
        Report,
        Block,
    }

    private const int MaxCaptionLength = 500;
    private const int MaxPhotoTags = 20;
    private const int MaxCommentLength = 500;
    private const float BottomNavHeight = 52f;
    private const int NavSlotCount = 4;
    private const float NavIconSize = 26f;
    private const float NavHoverRadius = 20f;
    private const float NavAvatarRadius = 13f;
    private const float NavAvatarRingGap = 2.5f;
    private const float NavAnchorHalf = 20f;
    private const float TopBarIconSize = 26f;
    private const float TitleChevronSize = 18f;
    private const float TitleChevronGap = 4f;
    private const float TitleHitPad = 8f;
    private const float ScopePopoverWidth = 236f;
    private const float ScopePopoverRowHeight = 42f;
    private const float ScopePopoverPad = 6f;
    private const float ScopePopoverGap = 8f;
    private const float ScopePopoverRounding = 16f;
    private const float CardPadTop = 10f;
    private const float CardPadBottom = 12f;
    private const float CardAvatarRadius = 16f;
    private const float CardRingGap = 3f;
    private const float CardHeaderBlock = 36f;
    private const float CardNameGap = 10f;
    private const float CardMediaGap = 8f;
    private const float CardMoreRadius = 16f;
    private const float CardActionsHeight = 44f;
    private const float CardActionIconSize = 24f;
    private const float CardActionInset = 12f;
    private const float CardActionGap = 18f;
    private const float CardCountGap = 6f;
    private const float CardTextGap = 2f;
    private const float CardLineGap = 4f;
    private const float CardCaptionScale = 0.95f;
    private const int GridColumns = 3;
    private const int PostSheetMaxItems = 4;
    private const float LikeBurstDuration = 0.9f;
    private const float LikeBurstSize = 84f;
    private const float TagModeBarHeight = 28f;

    public string Id => "aethergram";
    public Vector4 Accent => AppAccents.For(Id);
    public string DisplayName => Loc.T(L.Apps.Aethergram);
    public string Glyph => "Ag";
    public int BadgeCount => dmStore.UnreadCount + social.UnseenCount(Id);
    public ShareKindSet AcceptedShares => store.IsSignedIn ? ShareKindSet.Photo : ShareKindSet.None;
    private static readonly TextStyle CardNameStyle = new(0.97f, FontWeight.SemiBold);
    private static readonly TextStyle CardMetaStyle = new(0.85f, FontWeight.Regular);
    private static readonly TextStyle CardCountStyle = TextStyles.SubheadlineEmphasized;
    private static readonly TextStyle CardLinkStyle = new(0.88f, FontWeight.Regular);
    private static readonly TextStyle CardTimeStyle = TextStyles.Footnote;
    private static readonly TextStyle FeedTitleStyle = new(1.2f, FontWeight.SemiBold);
    private static readonly TextStyle PopoverRowStyle = new(0.97f, FontWeight.Medium);

    private enum HomePanel
    {
        None,
        Scope,
    }

    private readonly Dictionary<SocialFeedScope, PullToRefresh> pullToRefresh = new()
    {
        { SocialFeedScope.ForYou, new() },
        { SocialFeedScope.Following, new() }
    };
    private readonly AethergramStore store;
    private readonly GramDmStore dmStore;
    private readonly AccountClient account;
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
    private readonly TranslationService translation;
    private readonly ReportService report;
    private readonly WallpaperImageCache wallpaperImages;
    internal readonly EncryptionHelpService encryptionHelp;
    private readonly ActionSheet postSheet = new();
    private readonly ActionReveal<HomePanel> homeReveal = new();
    private Rect scopeAnchor;
    private string cardTimestampPostId = string.Empty;
    private string cardTimestamp = string.Empty;
    private readonly ActionSheet.Item[] postSheetItems = new ActionSheet.Item[PostSheetMaxItems];
    private readonly PostSheetAction[] postSheetActions = new PostSheetAction[PostSheetMaxItems];
    private int postSheetCount;
    private string postSheetTitle = string.Empty;
    private readonly Action<NotificationDto> openActivityActor;
    private readonly Action<NotificationDto> openActivityPost;
    private readonly SocialActivityFeed activityFeed;
    private readonly Action loadOlderActivity;
    private PostDto? sheetPost;
    private readonly StoryPresenter stories;
    private readonly PhotoViewerOverlay photoViewer = new();
    private readonly AvatarLightbox avatarLightbox = new();
    private readonly PhotoCarousel carousel = new();
    private string? pendingViewUrl;
    private double pendingViewAt;
    private readonly AppSkin ui = new(AppPalettes.Aethergram);
    private readonly SocialProfilePages profile;
    private readonly RichTextCache bodyLayouts = new(scanHashtags: true);
    private readonly FeedVirtualizer feedVirtualizer = new(400f);
    private readonly RichTextCache detailBodyLayouts = new(scanHashtags: true);
    private readonly RichTextCache commentLayouts = new(scanHashtags: true);
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
    private SocialFeedScope activeScope = SocialFeedScope.ForYou;
    private bool feedScrollTopPending;
    private bool commentFocusPending;
    private readonly PhotoComposeSession composeSession;
    private bool composeAvatarMode;
    private bool composeStoryMode;
    private readonly string[] aspectLabels = new string[PostAspects.All.Length];
    private string? pendingSharedPhoto;
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
    private bool composeSensitive;
    private bool captionFocus;
    private readonly FailureSlot feedFailure = new();
    private readonly FailureSlot commentFailure = new();
    private string? commentRestore;
    private readonly CommentAttachment commentAttachment = new();
    private string? commentAttachmentRestore;
    private string composeStatus = string.Empty;
    private volatile int composeOutcome;
    private string commentDraft = string.Empty;
    private string likeBurstPostId = string.Empty;
    private double likeBurstStart;
    private string hashtagTitle = string.Empty;
    private string hashtagTitleTag = string.Empty;
    private readonly ActionSheet inboxRowSheet = new();
    private readonly CancellationTokenSource settingsCancellation = new();
    private readonly HashSet<string> shareSentUserIds = new(StringComparer.Ordinal);
    private string shareSearchDraft = string.Empty;
    private readonly ThreadView threadView;

    public AethergramApp(AethernetSession session, AethernetApi net, LodestoneService lodestone,
        RemoteImageCache images, PhotoLibrary library, SocialLauncher launcher, GramDmLauncher dmLauncher,
        GameData gameData, Configuration configuration, SocialNotificationService social,
        NotificationService notifications, HttpService http, KeyVault keyVault,
        ConversationKeyStore conversationKeys, DecryptedHistoryStore chatHistory,
        PhoneVisibility visibility, RealtimeSignalBus realtimeSignals,
        WallpaperImageCache wallpaperImages, ConfirmService confirm, TranslationService translation,
        ReportService report, ConductGateService conduct,
        AppInstaller installer, EncryptionHelpService encryptionHelp)
    {
        this.translation = translation;
        store = new AethergramStore(session, net.Account, net.Social, net.Grams, net.Safety, net.Media, realtimeSignals);
        store.SetFeedRegions(SocialRegion.FilterCsv(configuration.AethergramFeedRegionMask));
        account = net.Account;
        dmStore = new GramDmStore(session, net.GramDm, net.Social, net.Safety, net.Media, notifications, keyVault,
            conversationKeys, chatHistory, visibility, realtimeSignals, installer);
        composeMentions = new MentionAutocomplete(store.NewMentionSuggestions());
        commentMentions = new MentionAutocomplete(store.NewMentionSuggestions());
        personPicker = new PersonPicker(store.NewMentionSuggestions());
        stories = new StoryPresenter(session, net.Grams, net.Media, images, lodestone, AethergramArt.StoryRing,
            AppPalettes.Aethergram, new StoryConfirmLabels(L.Aethergram.DeleteConfirm, L.Aethergram.DeleteCancel,
                L.Aethergram.Saving), confirm, translation, realtimeSignals, "Aethergram stories", StartStoryCompose,
            new StoryReplyHooks(L.Aethergram.ReplyToStory, dmStore.SendStoryReply, OpenThread), OpenProfile);
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
        this.encryptionHelp = encryptionHelp;
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
            RemoveCommentConfirmMessage = L.Aethergram.RemoveCommentConfirmMessage,
            MessageLabel = L.Aethergram.MessageButton,
            SettingsLabel = L.Aethergram.Settings,
            SavedLabel = L.Aethergram.SavedTitle,
        }, images, lodestone, avatarLightbox, configuration, gameData, confirm, report, translation,
            () => router.Push(AethergramRoute.EditProfile), () => StartCompose(true), OpenProfile, OpenUserList, back,
            null, OpenThread, () => router.Push(AethergramRoute.Settings), OpenSaved);
        threadView = new ThreadView(this);
    }

    public void OnOpened()
    {
        router.Reset();
        activeTab = AethergramTab.Home;
        avatarLightbox.Reset();
        caption = string.Empty;
        composeSensitive = false;
        profile.SearchDraft = string.Empty;
        commentDraft = string.Empty;
        shareSearchDraft = string.Empty;
        shareSentUserIds.Clear();
        store.ClearDiscover();
        RefreshAndConsumeLaunch();
    }

    public void OnResumed()
    {
        RefreshAndConsumeLaunch();
    }

    private void RefreshAndConsumeLaunch()
    {
        if (store.IsSignedIn)
        {
            store.RefreshFeed(SocialFeedScope.ForYou);
            store.RefreshFeed(SocialFeedScope.Following);
            stories.RefreshTray();
        }

        if (store.IsSignedIn && dmLauncher.TryConsume(out var threadUserId, out var threadDraft))
        {
            router.Push(AethergramRoute.Thread(threadUserId), false);
            if (!string.IsNullOrEmpty(threadDraft))
            {
                threadView.PrefillDraft(threadDraft);
            }

            return;
        }

        if (store.IsSignedIn && launcher.TryConsume(Id, out var link))
        {
            if (link.Kind == SocialLinkKind.Profile)
            {
                OpenProfile(link.Id);
            }
            else if (link.Kind == SocialLinkKind.Requests)
            {
                social.MarkSeen(Id);
                OpenFollowRequests();
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
        stories.Close();
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = theme;
        postSheet.Gate();
        commentSheet.Gate();
        inboxRowSheet.Gate();
        homeReveal.Tick(MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds));
        if (homeReveal.IsOpen)
        {
            UiInteract.BlockThisFrame();
        }

        threadView.GateMenus();
        var screen = SceneChrome.ScreenFrom(context.Content, theme, UiScale.Current);
        screenRect = screen;
        ui.Backdrop(screen);
        ConsumeSharedPhoto();
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

        DrawScopePopover(screen);
        DrawPostSheet(screen);
        DrawCommentSheet(screen);
        DrawInboxRowSheet(screen);
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
            case AethergramScreen.Settings:
                DrawSettings(area);
                break;
            case AethergramScreen.Share:
                DrawShare(area, route.Id!);
                break;
            case AethergramScreen.FollowRequests:
                DrawFollowRequests(area);
                break;
            case AethergramScreen.Saved:
                DrawSaved(area);
                break;
            case AethergramScreen.Encryption:
                threadView.DrawEncryptionScreen(area);
                break;
            case AethergramScreen.Hashtag:
                DrawHashtag(area, route.Id!);
                break;
            case AethergramScreen.Activity:
                DrawActivity(area);
                break;
            default:
                DrawRoot(area);
                break;
        }
    }

    private void DrawRoot(Rect area)
    {
        var scale = UiScale.Current;
        if (!store.IsSignedIn)
        {
            DrawHomeTopBar(area);
            TourHolds.Hold(Id);
            var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
            Typography.DrawCentered(body.Center, Loc.T(L.Aethergram.SetUpAccount), AethergramInk.MutedInk);
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
        var tabArea = new Rect(area.Min, new Vector2(area.Max.X, navRect.Min.Y));
        using (ImRaii.PushId((int)activeTab))
        {
            switch (activeTab)
            {
                case AethergramTab.Search:
                    DrawSearchTab(tabArea);
                    break;
                case AethergramTab.Profile:
                    DrawProfileTab(tabArea);
                    break;
                default:
                    DrawFeedTab(tabArea);
                    break;
            }
        }

        DrawBottomNav(navRect);
    }

    private void DrawTabTitle(Rect area, string title)
    {
        var scale = UiScale.Current;
        var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        Typography.DrawCentered(new Vector2(area.Center.X, rowCenterY), title, Ink.TitleInk, 1.2f, FontWeight.SemiBold);
    }

    private void DrawFeedTab(Rect area)
    {
        var scale = UiScale.Current;
        DrawHomeTopBar(area);
        var listRect = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        DrawFeedList(listRect, activeScope);
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

    private void SelectTab(AethergramTab tab)
    {
        if (tab == AethergramTab.Home && activeTab == AethergramTab.Home)
        {
            RefreshActiveFeed();
            return;
        }

        activeTab = tab;
        postSheet.Close();
        homeReveal.Reset();
        switch (tab)
        {
            case AethergramTab.Home:
                profile.EnsureLoaded(activeScope);
                break;
            case AethergramTab.Search:
                ResetExplore();
                break;
            case AethergramTab.Profile:
                store.EnsureMe();
                break;
        }
    }

    private bool HiddenByMediaPreference(PostDto post)
    {
        if (configuration.AethergramShowGifPosts)
        {
            return false;
        }

        var photos = PostMedia.Photos(post.MediaUrls, post.MediaUrl);
        return photos.Length > 0 && GifMedia.IsGif(photos[0]);
    }

    private bool HiddenByMediaPreference(CommentDto comment)
    {
        return comment.Text.Length == 0 && CommentMediaHidden(comment.MediaUrl);
    }

    private bool CommentMediaHidden(string? mediaUrl)
    {
        return mediaUrl is not null && !configuration.AethergramShowCommentMedia;
    }

    private void OpenPostSheet(PostDto post, bool includeView)
    {
        sheetPost = post;
        postSheetTitle = SocialIdentity.Name(post.AuthorDisplayName, post.AuthorHandle);
        postSheetCount = 0;
        if (includeView)
        {
            AddPostSheetItem(PostSheetAction.View, Loc.T(L.Aethergram.ViewPost), false);
        }

        if (store.Me is { } me && me.Id == post.AuthorId)
        {
            AddPostSheetItem(PostSheetAction.Delete, Loc.T(L.Aethergram.DeleteConfirm), true);
        }
        else
        {
            AddPostSheetItem(PostSheetAction.Follow,
                Loc.T(post.IsFollowing ? L.Aethergram.Unfollow : L.Aethergram.Follow), false);
            AddPostSheetItem(PostSheetAction.Report, Loc.T(L.Report.Action), true);
            AddPostSheetItem(PostSheetAction.Block, Loc.T(L.Social.BlockAction), true);
        }

        postSheet.Open();
    }

    private void AddPostSheetItem(PostSheetAction action, string label, bool danger)
    {
        postSheetActions[postSheetCount] = action;
        postSheetItems[postSheetCount] = new ActionSheet.Item(label, string.Empty, danger);
        postSheetCount++;
    }

    private void DrawPostSheet(Rect screen)
    {
        if (!postSheet.CapturesPointer)
        {
            return;
        }

        var picked = postSheet.Draw(screen, ActionSheetStyle.From(ui), postSheetItems.AsSpan(0, postSheetCount),
            Loc.T(L.Common.Cancel), false, postSheetTitle);
        if (picked < 0 || sheetPost is not { } post)
        {
            return;
        }

        switch (postSheetActions[picked])
        {
            case PostSheetAction.View:
                OpenDetail(post);
                break;
            case PostSheetAction.Delete:
                profile.AskDeletePost(post.Id, back);
                break;
            case PostSheetAction.Follow:
                store.SetFollow(post.AuthorId, !post.IsFollowing);
                break;
            case PostSheetAction.Report:
                profile.OpenReport("post", post.Id, Loc.T(L.Report.PostTitle));
                break;
            case PostSheetAction.Block:
                profile.AskBlock(post.AuthorDisplayName, post.AuthorHandle, post.AuthorId);
                break;
        }
    }

    private void DrawFeedList(Rect listRect, SocialFeedScope scope)
    {
        var snapshot = store.Feed(scope);
        using (var surface = AppSurface.BeginEdgeToEdge(listRect))
        {
            if (feedScrollTopPending)
            {
                surface.JumpToTop();
                feedScrollTopPending = false;
            }

            pullToRefresh[scope].Draw(listRect, surface.Pull, surface.Dragging,
                store.IsLoading(scope), Ink.MutedInk, () => RefreshFeed(scope));
            stories.DrawTray(theme, store.Me?.AvatarUrl, store.Me is { } me ? me.Name : string.Empty,
                store.Me?.FrameId);
            if (snapshot.Length == 0)
            {
                var failed = !store.IsLoading(scope) && store.FeedFailed(scope);
                if (failed)
                {
                    feedFailure.Set(store.FeedFailure(scope));
                }

                if (store.IsLoading(scope))
                {
                    var skeletonTop = ImGui.GetCursorScreenPos().Y + 12f * UiScale.Current;
                    Skeleton.Feed(ImGui.GetWindowDrawList(),
                        new Rect(new Vector2(listRect.Min.X, skeletonTop), listRect.Max), UiScale.Current);
                    return;
                }

                var message = failed ? feedFailure.Text() :
                    scope == SocialFeedScope.Following ? Loc.T(L.Aethergram.FollowingEmpty) :
                    Loc.T(L.Aethergram.ExploreEmpty);
                var messageY = ImGui.GetCursorScreenPos().Y + 60f * UiScale.Current;
                Typography.DrawCentered(new Vector2(listRect.Center.X, messageY), message, Ink.MutedInk);
                if (failed)
                {
                    Typography.DrawCentered(new Vector2(listRect.Center.X, messageY + 28f * UiScale.Current),
                        Loc.T(L.Failure.PullToRetry), Ink.MutedInk, TextStyles.Footnote);
                }
            }
            else
            {
                ImGui.Dummy(new Vector2(0f, 4f * UiScale.Current));
                feedVirtualizer.BeginFrame(store.FeedSource(scope));
                for (var index = 0; index < snapshot.Length; index++)
                {
                    var post = snapshot[index];
                    if (HiddenByMediaPreference(post))
                    {
                        continue;
                    }

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
                    InfiniteScroll.DrawLoadingRow(listRect.Center.X, Ink.MutedInk);
                }

                ImGui.Dummy(new Vector2(0f, 16f * UiScale.Current));
                if (InfiniteScroll.ReachedBottom() && store.HasMoreFeed(scope) && !store.LoadingMore(scope))
                {
                    store.LoadMoreFeed(scope);
                }
            }
        }
    }

    private void DrawGramCard(PostDto post, bool detail = false)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var width = ScrollLayout.StableContentWidth();
        var inset = CellPadX * scale;
        var innerWidth = width - inset * 2f;
        var displayName = SocialIdentity.Name(post.AuthorDisplayName, post.AuthorHandle);
        var headerBlock = CardHeaderBlock * scale;
        var avatarRadius = CardAvatarRadius * scale;
        var mediaHeight = PostAspects.DisplayHeight(width, post.MediaWidth, post.MediaHeight);
        var actionsHeight = CardActionsHeight * scale;
        RichTextLayout? captionLayout = null;
        var translateKey = new TranslationKey(TranslationSurface.Post, post.Id);
        var captionView = translation.View(translateKey, post.Text, post.Lang);
        var captionText = captionView.Text;
        if (captionText.Length > 0)
        {
            using (Plugin.Fonts.Push(CardCaptionScale))
            {
                captionLayout = bodyLayouts.LayoutFor(captionView.LayoutKey, captionText, post.Mentions, innerWidth);
            }
        }

        var captionTextHeight = captionText.Length == 0
            ? 0f
            : captionLayout?.Size.Y ?? Typography.MeasureWrapped(captionText, innerWidth, CardCaptionScale);
        var translateHeight = TranslateLink.Height(translation, translateKey, post.Lang, scale);
        var lineGap = CardLineGap * scale;
        var captionHeight = captionText.Length == 0 ? 0f : captionTextHeight + translateHeight + lineGap;
        var showCommentsLink = !detail && post.CommentCount > 0;
        var commentsHeight = showCommentsLink ? Typography.LineHeight(CardLinkStyle) + lineGap : 0f;
        var timeHeight = Typography.LineHeight(CardTimeStyle);
        var cellHeight = CardPadTop * scale + headerBlock + CardMediaGap * scale + mediaHeight + actionsHeight
            + CardTextGap * scale + captionHeight + commentsHeight + timeHeight + CardPadBottom * scale;
        var cell = FeedCell.Begin(drawList, cellHeight, Ink.HoverTint, false);
        var origin = cell.Bounds.Min;
        var innerX = origin.X + inset;
        var headerTop = origin.Y + CardPadTop * scale;
        var imageTop = headerTop + headerBlock + CardMediaGap * scale;
        var imageBottom = imageTop + mediaHeight;
        var actionsTop = imageBottom;
        var textTop = actionsTop + actionsHeight + CardTextGap * scale;
        var avatarCenter = new Vector2(innerX + avatarRadius, headerTop + headerBlock * 0.5f);
        var ringRadius = avatarRadius + CardRingGap * scale;
        var hasStory = stories.TryRing(post.AuthorId, out var authorRing);
        if (hasStory)
        {
            AethergramArt.StoryRing(drawList, avatarCenter, ringRadius, scale, authorRing.HasUnseen);
        }

        DrawAvatar(avatarCenter, avatarRadius - 1f * scale, displayName, string.Empty, post.AuthorAvatarUrl, 0.85f, 32,
            Frames.Of(post.AuthorFrameId));
        var moreRadius = CardMoreRadius * scale;
        var moreCenter = new Vector2(origin.X + width - inset - moreRadius + 6f * scale, avatarCenter.Y);
        var nameLeft = avatarCenter.X + avatarRadius + CardNameGap * scale;
        var headerTextRight = moreCenter.X - moreRadius - 4f * scale;
        var headerTextMaxWidth = MathF.Max(1f, headerTextRight - nameLeft);
        var nameHeight = Typography.LineHeight(CardNameStyle);
        var metaHeight = Typography.LineHeight(CardMetaStyle);
        var nameTop = avatarCenter.Y - (nameHeight + metaHeight + 1f * scale) * 0.5f;
        var drawnNameWidth = UserName.DrawAuto(drawList, "aethergram.card." + post.Id, displayName, post.AuthorBadges,
            post.AuthorBadgeIds, nameLeft, nameTop, headerTextMaxWidth, CardNameStyle, Ink.TitleInk, theme);
        var nameMin = new Vector2(nameLeft, nameTop);
        var nameMax = new Vector2(nameLeft + drawnNameWidth, nameTop + nameHeight);
        if (UiInteract.Hover(nameMin, nameMax))
        {
            drawList.AddLine(new Vector2(nameMin.X, nameMax.Y - 1f * scale),
                new Vector2(nameMax.X, nameMax.Y - 1f * scale), ImGui.GetColorU32(Ink.TitleInk), 1f);
        }

        var regionCode = SocialRegion.Resolve(null, post.AuthorWorld, gameData);
        var meta = SocialIdentity.ProfileMeta(post.AuthorHandle, regionCode);
        if (ContentModeration.IsInReview(post.ScanStatus))
        {
            meta = $"{meta} · {Loc.T(L.Moderation.InReview)}";
        }

        Typography.Draw(drawList, new Vector2(nameLeft, nameTop + nameHeight + 1f * scale),
            Typography.FitText(meta, headerTextMaxWidth, CardMetaStyle), Ink.MutedInk, CardMetaStyle);
        var ringExtent = new Vector2(ringRadius, ringRadius);
        var overRing = hasStory && UiInteract.Hover(avatarCenter - ringExtent, avatarCenter + ringExtent);
        if (hasStory && UiInteract.HoverClickCircle(avatarCenter, ringRadius))
        {
            stories.OpenRing(authorRing);
        }
        else if (!overRing && UiInteract.HoverClick(new Vector2(innerX, headerTop),
                     new Vector2(headerTextRight, headerTop + headerBlock)))
        {
            OpenProfile(post.AuthorId);
        }

        var moreExtent = new Vector2(moreRadius, moreRadius);
        var moreHovered = UiInteract.Hover(moreCenter - moreExtent, moreCenter + moreExtent);
        if (moreHovered)
        {
            drawList.AddCircleFilled(moreCenter, moreRadius, ImGui.GetColorU32(Ink.FieldFill), 24);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        PhoneIcon.Draw(drawList, moreCenter, PhoneIcons.Dots, Ink.TitleInk, 20f * scale);
        HoverTooltip.Show(new Rect(moreCenter - moreExtent, moreCenter + moreExtent), Loc.T(L.Aethergram.More),
            HoverLabelSide.Above);
        if (UiInteract.Click(moreCenter - moreExtent, moreCenter + moreExtent, moreHovered))
        {
            OpenPostSheet(post, !detail);
        }

        var imageRect = new Rect(new Vector2(origin.X, imageTop), new Vector2(origin.X + width, imageBottom));
        var photos = PostMedia.Photos(post.MediaUrls, post.MediaUrl);
        var page = DrawGramCarousel(imageRect, post, photos, 0f);
        var actionCenterY = actionsTop + actionsHeight * 0.5f;
        var liked = post.MyReaction >= 0;
        var actionX = innerX + CardActionInset * scale - CardActionIconSize * scale * 0.5f;
        if (DrawCardAction(drawList, ref actionX, actionCenterY, liked ? PhoneIcons.HeartFilled : PhoneIcons.Heart,
                liked ? Ink.LikeRed : Ink.TitleInk, post.TotalReactions, Loc.T(L.Aethergram.Like)))
        {
            store.ToggleLike(post);
        }

        if (DrawCardAction(drawList, ref actionX, actionCenterY, PhoneIcons.MessageCircle, Ink.TitleInk,
                post.CommentCount, Loc.T(L.Aethergram.Comment)))
        {
            OpenDetail(post, true);
        }

        if (DrawCardAction(drawList, ref actionX, actionCenterY, PhoneIcons.Send, Ink.TitleInk, 0,
                Loc.T(L.Aethergram.SendTo)))
        {
            OpenShare(post.Id);
        }

        var iconSize = CardActionIconSize * scale;
        var bookmarkCenter = new Vector2(origin.X + width - inset - iconSize * 0.5f + 2f * scale, actionCenterY);
        var bookmarkMin = new Vector2(bookmarkCenter.X - iconSize, actionsTop);
        var bookmarkMax = new Vector2(bookmarkCenter.X + iconSize * 0.5f + 6f * scale, actionsTop + actionsHeight);
        var bookmarkHovered = UiInteract.Hover(bookmarkMin, bookmarkMax);
        if (bookmarkHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        PhoneIcon.Draw(drawList, bookmarkCenter, post.Saved ? PhoneIcons.BookmarkFilled : PhoneIcons.Bookmark,
            Ink.TitleInk, iconSize);
        HoverTooltip.Show(new Rect(bookmarkMin, bookmarkMax), Loc.T(L.Aethergram.Save), HoverLabelSide.Above);
        if (UiInteract.Click(bookmarkMin, bookmarkMax, bookmarkHovered))
        {
            store.SetSaved(post.Id, !post.Saved);
        }

        if (photos.Length > 1)
        {
            var dotsLeft = actionX;
            var dotsRight = bookmarkMin.X - 6f * scale;
            var dotsCenter = new Vector2((dotsLeft + dotsRight) * 0.5f, actionCenterY);
            PhotoCarousel.DrawDots(drawList, dotsCenter, photos.Length, page, MathF.Max(0f, dotsRight - dotsLeft),
                Ink.BodyInk);
        }

        var y = textTop;
        if (captionText.Length > 0)
        {
            if (captionLayout is null)
            {
                ImGui.SetCursorScreenPos(new Vector2(innerX, y));
                using (Typography.WrapAt(innerX + innerWidth))
                using (ImRaii.PushColor(ImGuiCol.Text, Ink.BodyInk))
                using (Plugin.Fonts.Push(CardCaptionScale))
                {
                    Typography.Wrapped(captionText);
                }
            }
            else
            {
                using (Plugin.Fonts.Push(CardCaptionScale))
                {
                    DrawRichBody(drawList, captionLayout, new Vector2(innerX, y));
                }
            }

            if (translateHeight > 0f)
            {
                TranslateLink.Draw(translation, confirm, translateKey, post.Lang, post.Text,
                    new Vector2(innerX, y + captionTextHeight), innerWidth, Ink.MutedInk, Ink.AccentLink, scale);
            }

            y += captionHeight;
        }

        if (showCommentsLink)
        {
            var commentsLabel = Loc.T(L.Aethergram.ViewComments, post.CommentCount);
            var labelPos = new Vector2(innerX, y);
            var labelSize = Typography.Measure(commentsLabel, CardLinkStyle);
            Typography.Draw(drawList, labelPos, commentsLabel, Ink.MutedInk, CardLinkStyle);
            if (UiInteract.HoverClick(labelPos, labelPos + labelSize))
            {
                OpenDetail(post, false);
            }

            y += commentsHeight;
        }

        var time = detail ? CardTimestamp(post) : TimeText.Short(post.CreatedAtUnix);
        Typography.Draw(drawList, new Vector2(innerX, y), Typography.FitText(time, innerWidth, CardTimeStyle),
            Ink.MutedInk, CardTimeStyle);
        FeedCell.End(drawList, cell, Ink.Hairline);
    }

    private static bool DrawCardAction(ImDrawListPtr drawList, ref float x, float centerY, string glyph, Vector4 ink,
        int count, string tooltip)
    {
        var scale = UiScale.Current;
        var iconSize = CardActionIconSize * scale;
        var halfHeight = CardActionsHeight * scale * 0.5f;
        var label = count > 0 ? CountText.Compact(count) : string.Empty;
        var labelWidth = label.Length > 0 ? Typography.Measure(label, CardCountStyle).X : 0f;
        var contentWidth = iconSize + (label.Length > 0 ? CardCountGap * scale + labelWidth : 0f);
        var min = new Vector2(x - 6f * scale, centerY - halfHeight);
        var max = new Vector2(x + contentWidth + 6f * scale, centerY + halfHeight);
        var hovered = UiInteract.Hover(min, max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        PhoneIcon.Draw(drawList, new Vector2(x + iconSize * 0.5f, centerY), glyph, ink, iconSize);
        if (label.Length > 0)
        {
            var labelSize = Typography.Measure(label, CardCountStyle);
            Typography.Draw(drawList, new Vector2(x + iconSize + CardCountGap * scale, centerY - labelSize.Y * 0.5f),
                label, Ink.TitleInk, CardCountStyle);
        }

        HoverTooltip.Show(new Rect(min, max), tooltip, HoverLabelSide.Above);
        x += contentWidth + CardActionGap * scale;
        return UiInteract.Click(min, max, hovered);
    }

    private string CardTimestamp(PostDto post)
    {
        if (!string.Equals(cardTimestampPostId, post.Id, StringComparison.Ordinal))
        {
            var local = DateTimeOffset.FromUnixTimeSeconds(post.CreatedAtUnix).ToLocalTime();
            cardTimestampPostId = post.Id;
            cardTimestamp = $"{TimeText.Clock(local)} · {local.ToString("d MMM yyyy", Loc.Culture)}";
        }

        return cardTimestamp;
    }

    private int DrawGramCarousel(Rect imageRect, PostDto post, string[] photos, float rounding)
    {
        var scanStatus = post.ScanStatus;
        var veiled = SensitiveReveals.ShouldVeil(post.Sensitive, post.Id, configuration.ShowSensitiveContent);
        var result = carousel.Draw(ImGui.GetWindowDrawList(), imageRect, post.Id, photos, rounding,
            (list, min, max, radius, url) => DrawGramImage(list, new Rect(min, max), url, radius, scanStatus, veiled));
        if (result.InputConsumed)
        {
            pendingViewUrl = null;
        }
        else if (veiled)
        {
            if (result.Tapped)
            {
                SensitiveReveals.Reveal(post.Id);
            }

            return result.Index;
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
        if (!UiInteract.Hover(imageRect.Min, imageRect.Max))
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
        photoViewer.Open(this, () => GifMedia.Texture(images, url, ImGui.GetTime()));
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

        var scale = UiScale.Current;
        var appear = Math.Clamp(elapsed / 0.22f, 0f, 1f);
        var back = appear - 1f;
        var pop = MathF.Max(1f + back * back * (2.70158f * back + 1.70158f), 0.05f);
        var alpha = elapsed < 0.55f ? 1f : 1f - (elapsed - 0.55f) / (LikeBurstDuration - 0.55f);
        var rise = elapsed < 0.55f ? 0f : (elapsed - 0.55f) * 46f * scale;
        var center = new Vector2(imageRect.Center.X, imageRect.Center.Y - rise);
        var drawList = ImGui.GetWindowDrawList();
        var size = LikeBurstSize * scale * pop;
        PhoneIcon.Draw(drawList, center + new Vector2(0f, 2f * scale), PhoneIcons.HeartFilled,
            new Vector4(0f, 0f, 0f, 0.35f * alpha), size);
        PhoneIcon.Draw(drawList, center, PhoneIcons.HeartFilled, new Vector4(1f, 1f, 1f, alpha), size);
    }

    private void DrawGramImage(Rect rect, string? url, float rounding, string? scanStatus = null) =>
        DrawGramImage(ImGui.GetWindowDrawList(), rect, url, rounding, scanStatus);

    private void DrawGramImage(ImDrawListPtr drawList, Rect rect, string? url, float rounding,
        string? scanStatus = null, bool veiled = false)
    {
        if (veiled)
        {
            SensitiveVeil.Draw(drawList, rect.Min, rect.Max, rounding);
            return;
        }

        var scale = UiScale.Current;
        var texture = GifMedia.Texture(images, url, ImGui.GetTime());
        if (texture is null)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(Ink.FieldFill));
            Typography.DrawCentered(rect.Center,
                Loc.T(images.Failed(url) ? L.Common.ImageFailed : L.Common.Loading), Ink.MutedInk, 0.85f);
        }
        else
        {
            ImageFit.DrawLetterboxed(drawList, texture, rect, Vector2.Zero, Vector2.One, rounding);
        }

        if (GifMedia.IsGif(url))
        {
            GifBadge.Draw(drawList, rect);
        }

        ModerationOverlay.Draw(drawList, rect.Min, rect.Max, rounding, scanStatus);
    }

    private void DrawBottomNav(Rect bar)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        PaintBarBackdrop(drawList, bar);
        DrawHairline(drawList, bar.Min.X, bar.Max.X, bar.Min.Y + 1f);
        var slot = bar.Width / NavSlotCount;
        var anchorHalf = new Vector2(NavAnchorHalf * scale, NavAnchorHalf * scale);
        for (var index = 0; index < NavSlotCount; index++)
        {
            var cell = new Rect(new Vector2(bar.Min.X + slot * index, bar.Min.Y),
                new Vector2(bar.Min.X + slot * (index + 1), bar.Max.Y));
            var center = new Vector2(cell.Center.X, bar.Center.Y);
            switch (index)
            {
                case 0:
                    if (DrawNavSlot(drawList, cell, center, activeTab == AethergramTab.Home ? PhoneIcons.HomeFilled
                            : PhoneIcons.Home, activeTab == AethergramTab.Home, Loc.T(L.Aethergram.Home), 0))
                    {
                        SelectTab(AethergramTab.Home);
                    }

                    break;
                case 1:
                    UiAnchors.Report("aethergram.tab.search", new Rect(center - anchorHalf, center + anchorHalf));
                    if (DrawNavSlot(drawList, cell, center, PhoneIcons.Search, activeTab == AethergramTab.Search,
                            Loc.T(L.Aethergram.Search), 0))
                    {
                        SelectTab(AethergramTab.Search);
                    }

                    break;
                case 2:
                    if (DrawNavSlot(drawList, cell, center, PhoneIcons.Send, false, Loc.T(L.Aethergram.InboxTitle),
                            dmStore.UnreadCount))
                    {
                        OpenInbox();
                    }

                    break;
                default:
                    UiAnchors.Report("aethergram.tab.profile", new Rect(center - anchorHalf, center + anchorHalf));
                    if (DrawNavProfile(drawList, cell, center))
                    {
                        SelectTab(AethergramTab.Profile);
                    }

                    break;
            }
        }
    }

    private static bool DrawNavSlot(ImDrawListPtr drawList, Rect cell, Vector2 center, string glyph, bool active,
        string label, int badge)
    {
        var scale = UiScale.Current;
        var hovered = DrawNavHover(drawList, cell, center);
        var ink = active ? Ink.TitleInk : hovered ? Ink.BodyInk : Ink.MutedInk;
        PhoneIcon.Draw(drawList, center, glyph, ink, NavIconSize * scale);
        SocialChrome.DrawCountBadge(drawList, center + new Vector2(11f * scale, -10f * scale), badge, Ink);
        HoverTooltip.Show(cell, label, HoverLabelSide.Above);
        return UiInteract.Click(cell.Min, cell.Max, hovered);
    }

    private static bool DrawNavHover(ImDrawListPtr drawList, Rect cell, Vector2 center)
    {
        var hovered = UiInteract.Hover(cell.Min, cell.Max);
        if (!hovered)
        {
            return false;
        }

        drawList.AddCircleFilled(center, NavHoverRadius * UiScale.Current, ImGui.GetColorU32(Ink.FieldFill), 32);
        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return true;
    }

    private bool DrawNavProfile(ImDrawListPtr drawList, Rect cell, Vector2 center)
    {
        var scale = UiScale.Current;
        var active = activeTab == AethergramTab.Profile;
        var label = Loc.T(L.Aethergram.Profile);
        if (store.Me is not { } me)
        {
            store.EnsureMe();
            return DrawNavSlot(drawList, cell, center, active ? PhoneIcons.UserFilled : PhoneIcons.User, active, label,
                0);
        }

        var hovered = DrawNavHover(drawList, cell, center);
        var radius = NavAvatarRadius * scale;
        DrawAvatar(center, radius, me.Name, me.World, me.AvatarUrl, 0.85f, 28, Frames.Of(me.FrameId));
        if (active)
        {
            drawList.AddCircle(center, radius + NavAvatarRingGap * scale, ImGui.GetColorU32(Ink.TitleInk), 32,
                1.6f * scale);
        }

        HoverTooltip.Show(cell, label, HoverLabelSide.Above);
        return UiInteract.Click(cell.Min, cell.Max, hovered);
    }

    private void DrawAvatar(Vector2 center, float radius, string name, string world, string? avatarUrl,
        float monogramScale, int segments, FrameStyle? frame = null)
    {
        AvatarView.DrawRemote(ImGui.GetWindowDrawList(), center, radius, theme, name, world, avatarUrl, images,
            lodestone, monogramScale, segments, 1f, frame);
    }

    private void OpenProfile(string userId)
    {
        profileTab = 0;
        store.OpenProfile(userId);
        router.Push(AethergramRoute.Profile(userId));
    }

    private void DrawRichBody(ImDrawListPtr drawList, RichTextLayout layout, Vector2 origin)
    {
        var ink = new RichTextInk(Ink.BodyInk, Ink.AccentLink, Ink.AccentLink);
        RichText.Draw(drawList, layout, origin, ink, out var hit);
        if (hit.Kind == RichTextRunKind.Mention && hit.Clicked)
        {
            OpenProfile(layout.Mentions[hit.TargetIndex].UserId);
        }

        if (hit.Kind == RichTextRunKind.Hashtag && hit.Clicked)
        {
            OpenHashtag(layout.Tags[hit.TargetIndex]);
        }

        if (hit.Kind == RichTextRunKind.Link && hit.Clicked)
        {
            UrlActions.AskThenOpen(layout.Urls[hit.TargetIndex]);
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
        settingsCancellation.Cancel();
        settingsCancellation.Dispose();
        threadView.Dispose();
        dmStore.Dispose();
        store.Dispose();
        stories.Dispose();
    }

    private void DrawHomeTopBar(Rect area)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        var signedIn = store.IsSignedIn;
        var title = signedIn
            ? Loc.T(activeScope == SocialFeedScope.Following ? L.Aethergram.Following : L.Aethergram.ForYou)
            : DisplayName;
        var titleSize = Typography.Measure(title, FeedTitleStyle);
        var chevron = signedIn ? TitleChevronSize * scale : 0f;
        var chevronGap = signedIn ? TitleChevronGap * scale : 0f;
        var blockWidth = titleSize.X + chevronGap + chevron;
        var titleLeft = area.Center.X - blockWidth * 0.5f;
        var titleTop = rowCenterY - titleSize.Y * 0.5f;
        var hitPad = TitleHitPad * scale;
        var titleMin = new Vector2(titleLeft - hitPad, titleTop - 6f * scale);
        var titleMax = new Vector2(titleLeft + blockWidth + hitPad, titleTop + titleSize.Y + 6f * scale);
        if (signedIn)
        {
            UiInteract.HoverHighlight(drawList, titleMin, titleMax, 8f * scale);
        }

        Typography.Draw(drawList, new Vector2(titleLeft, titleTop), title, Ink.TitleInk, FeedTitleStyle);
        if (!signedIn)
        {
            return;
        }

        PhoneIcon.Draw(drawList, new Vector2(titleLeft + titleSize.X + chevronGap + chevron * 0.5f, rowCenterY + 1f * scale),
            PhoneIcons.ChevronDown, Ink.TitleInk, chevron);
        scopeAnchor = new Rect(titleMin, titleMax);
        if (UiInteract.HoverClick(titleMin, titleMax))
        {
            if (homeReveal.IsOpen)
            {
                homeReveal.Dismiss();
            }
            else
            {
                homeReveal.Open(Id, HomePanel.Scope);
            }
        }

        if (store.IsLoading(activeScope))
        {
            LoadingPulse.Spinner(new Vector2(titleMax.X + 12f * scale, rowCenterY), 7f * scale, Ink.AccentLink);
        }

        var hitRadius = SocialChrome.HeaderIconRadius * scale;
        var hitExtent = new Vector2(hitRadius, hitRadius);
        var composeCenter = new Vector2(area.Min.X + CellPadX * scale + hitRadius, rowCenterY);
        UiAnchors.Report("aethergram.compose", new Rect(composeCenter - hitExtent, composeCenter + hitExtent));
        if (DrawHeaderIcon(drawList, composeCenter, PhoneIcons.SquareRoundedPlus, Loc.T(L.Aethergram.NewPost),
                iconSize: TopBarIconSize))
        {
            StartCompose(false);
        }

        var activityCenter = SocialChrome.HeaderSlot(area, 0);
        UiAnchors.Report("aethergram.activity", new Rect(activityCenter - hitExtent, activityCenter + hitExtent));
        if (DrawHeaderIcon(drawList, activityCenter, PhoneIcons.Heart, Loc.T(L.Social.ActivityTitle),
                badge: social.UnseenCount(Id), iconSize: TopBarIconSize))
        {
            OpenActivity();
        }
    }

    private void DrawScopePopover(Rect screen)
    {
        if (!homeReveal.IsOpen)
        {
            return;
        }

        if (activeTab != AethergramTab.Home || router.Current.Screen != AethergramScreen.Home)
        {
            homeReveal.Reset();
            return;
        }

        var scale = UiScale.Current;
        var drawList = ImGui.GetForegroundDrawList();
        var progress = Easing.EaseOutQuint(Math.Clamp(homeReveal.Progress, 0f, 1f));
        var width = ScopePopoverWidth * scale;
        var rowHeight = ScopePopoverRowHeight * scale;
        var pad = ScopePopoverPad * scale;
        var gap = ScopePopoverGap * scale;
        var rowCount = 4 + SocialRegion.Codes.Length;
        var height = pad * 2f + rowHeight * rowCount + gap;
        var left = Math.Clamp(scopeAnchor.Center.X - width * 0.5f, screen.Min.X + 12f * scale,
            screen.Max.X - 12f * scale - width);
        var top = scopeAnchor.Max.Y + 2f * scale;
        var grow = 0.92f + 0.08f * progress;
        var lift = (1f - progress) * -6f * scale;
        var pivot = new Vector2(scopeAnchor.Center.X, top);
        var min = pivot + (new Vector2(left, top) - pivot) * grow + new Vector2(0f, lift);
        var max = pivot + (new Vector2(left + width, top + height) - pivot) * grow + new Vector2(0f, lift);
        drawList.PushClipRect(screen.Min, screen.Max, false);
        PopoverSurface.DrawGlass(drawList, min, max, ScopePopoverRounding * scale, Ink, scale, progress);
        var interactive = !homeReveal.Closing && homeReveal.Progress > 0.6f;
        var rowGrown = rowHeight * grow;
        var rowLeft = min.X + pad * grow;
        var rowRight = max.X - pad * grow;
        var y = min.Y + pad * grow;
        if (DrawScopeRow(drawList, rowLeft, rowRight, ref y, rowGrown, Loc.T(L.Aethergram.ForYou),
                activeScope == SocialFeedScope.ForYou, progress, interactive))
        {
            SelectScope(SocialFeedScope.ForYou);
        }

        if (DrawScopeRow(drawList, rowLeft, rowRight, ref y, rowGrown, Loc.T(L.Aethergram.Following),
                activeScope == SocialFeedScope.Following, progress, interactive))
        {
            SelectScope(SocialFeedScope.Following);
        }

        DrawHairline(drawList, rowLeft + 8f * scale, rowRight - 8f * scale, y + gap * grow * 0.5f);
        y += gap * grow;
        if (DrawScopeRow(drawList, rowLeft, rowRight, ref y, rowGrown, Loc.T(L.Settings.AethergramShowGifs),
                configuration.AethergramShowGifPosts, progress, interactive))
        {
            configuration.AethergramShowGifPosts = !configuration.AethergramShowGifPosts;
            configuration.Save();
        }

        if (DrawScopeRow(drawList, rowLeft, rowRight, ref y, rowGrown, Loc.T(L.Settings.AethergramShowCommentMedia),
                configuration.AethergramShowCommentMedia, progress, interactive))
        {
            configuration.AethergramShowCommentMedia = !configuration.AethergramShowCommentMedia;
            configuration.Save();
        }

        for (var regionIndex = 0; regionIndex < SocialRegion.Codes.Length; regionIndex++)
        {
            if (!DrawScopeRow(drawList, rowLeft, rowRight, ref y, rowGrown, SocialRegion.Codes[regionIndex],
                    SocialRegion.MaskShows(configuration.AethergramFeedRegionMask, regionIndex), progress, interactive))
            {
                continue;
            }

            configuration.AethergramFeedRegionMask =
                SocialRegion.ToggleMask(configuration.AethergramFeedRegionMask, regionIndex);
            store.SetFeedRegions(SocialRegion.FilterCsv(configuration.AethergramFeedRegionMask));
            configuration.Save();
        }

        drawList.PopClipRect();
        homeReveal.DismissOnOutsideClick(min, max);
    }

    private static bool DrawScopeRow(ImDrawListPtr drawList, float left, float right, ref float y, float height,
        string label, bool selected, float alpha, bool interactive)
    {
        var scale = UiScale.Current;
        var min = new Vector2(left, y);
        var max = new Vector2(right, y + height);
        y += height;
        var hovered = interactive && UiInteract.HoverWindowOnly(min, max, false);
        if (hovered)
        {
            Squircle.Fill(drawList, min, max, 11f * scale, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.07f * alpha)));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var centerY = (min.Y + max.Y) * 0.5f;
        var textLeft = min.X + 14f * scale;
        var checkCenter = new Vector2(max.X - 20f * scale, centerY);
        var fitted = Typography.FitText(label, MathF.Max(1f, checkCenter.X - 14f * scale - textLeft), PopoverRowStyle);
        var size = Typography.Measure(fitted, PopoverRowStyle);
        Typography.Draw(drawList, new Vector2(textLeft, centerY - size.Y * 0.5f), fitted,
            Palette.WithAlpha(Ink.TitleInk, Ink.TitleInk.W * alpha), PopoverRowStyle);
        if (selected)
        {
            PhoneIcon.Draw(drawList, checkCenter, PhoneIcons.Check,
                Palette.WithAlpha(Ink.AccentLink, Ink.AccentLink.W * alpha), 18f * scale);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private void SelectScope(SocialFeedScope scope)
    {
        homeReveal.Dismiss();
        if (scope == activeScope)
        {
            return;
        }

        activeScope = scope;
        feedScrollTopPending = true;
        profile.EnsureLoaded(activeScope);
    }

}
