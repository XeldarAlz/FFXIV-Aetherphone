using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Conduct;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Game;
using Aetherphone.Core.Home;
using Aetherphone.Core.Inventory;
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
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell : IResumableApp
{
    private const float HeartbeatSeconds = 45f;
    private const byte LalafellRaceId = 3;

    private readonly VelvetStore store;
    private readonly FailureSlot discoverFailure = new();
    private readonly FailureSlot commentFailure = new();
    private string? commentRestore;
    private readonly HashSet<string> reportedTargets = new(StringComparer.Ordinal);
    private readonly StoryPresenter stories;
    private readonly VelvetLauncher launcher;
    private readonly SocialLauncher socialLauncher;
    private readonly LodestoneService lodestone;
    private readonly Configuration configuration;
    private readonly GameData gameData;
    private readonly PhotoLibrary library;
    private readonly HttpService http;
    private readonly RemoteImageCache images;
    private readonly SocialNotificationService social;
    private readonly SocialActivityFeed activityFeed;
    private readonly Action loadOlderActivity;
    private readonly ConfirmService confirm;
    private readonly TranslationService translation;
    private readonly ReportService report;
    private readonly ConductGateService conduct;
    private readonly WallpaperImageCache wallpaperImages;
    internal readonly EncryptionHelpService encryptionHelp;
    private readonly AppSkin ui = new(VelvetTheme.Palette);
    private readonly RichTextCache feedCaptionLayouts = new();
    private readonly RichTextCache detailBodyLayouts = new();
    private readonly RichTextCache commentLayouts = new();
    private readonly MentionPopup mentionPopup = new();
    private readonly MentionAutocomplete commentMentions;
    private readonly EmojiComposer commentEmoji = new();
    private readonly PhotoViewerOverlay photoViewer = new();
    private readonly AvatarLightbox avatarLightbox = new();
    private readonly PhotoCarousel carousel = new();
    private readonly PullToRefresh pullToRefresh = new();
    private readonly AvatarComposer avatar;
    private readonly AvatarComposer cardPhotos;
    private readonly Func<IDalamudTextureWrap?> viewerSource;
    private readonly VelvetPostComposer post;
    private string? pendingSharedPhoto;
    private readonly ViewRouter<VelvetView> router;
    private readonly RouterDraw<VelvetView> drawView;
    private readonly Action back;

    private PhoneTheme theme = PhoneTheme.Default;
    private Rect screenRect;
    private INavigator navigation = null!;
    private VelvetPage activeTab = VelvetPage.Discover;
    private float sinceHeartbeat = HeartbeatSeconds;
    private bool cachedLalafell;
    private int localRaceId;
    private bool raceKnown;
    private ulong raceContentId;
    private readonly VelvetFilterSelection discoverInclude = new();
    private readonly VelvetFilterSelection feedInclude = new();
    private readonly string[] whoLabels = new string[3];
    private readonly ActionSheet postSheet = new();
    private readonly ActionSheet threadSheet = new();
    private VelvetMessagesTab messagesTab = VelvetMessagesTab.Chats;
    private readonly ThreadView threadView;

    public VelvetShell(AethernetSession session, AethernetApi net, LodestoneService lodestone,
        Configuration configuration, PhotoLibrary library, HttpService http, RemoteImageCache images,
        NotificationService notifications, VelvetLauncher launcher, SocialLauncher socialLauncher, GameData gameData,
        SocialNotificationService social, KeyVault keyVault, ConversationKeyStore conversationKeys,
        DecryptedHistoryStore chatHistory,
        PhoneVisibility visibility, RealtimeSignalBus realtimeSignals, WallpaperImageCache wallpaperImages,
        ConfirmService confirm, TranslationService translation, ReportService report, ConductGateService conduct,
        AppInstaller installer, EncryptionHelpService encryptionHelp)
    {
        this.translation = translation;
        var velvetArchiveDir = new DirectoryInfo(Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, "Velvet"));
        var notInterestedArchive = new VelvetNotInterestedArchive(velvetArchiveDir);
        store = new VelvetStore(session, net.Velvet, net.Account, net.Safety, net.Media, notifications, configuration,
            keyVault, conversationKeys, chatHistory, visibility, realtimeSignals, installer, notInterestedArchive);
        commentMentions = new MentionAutocomplete(store.NewMentionSuggestions());
        editCaptionMentions = new MentionAutocomplete(store.NewMentionSuggestions());
        stories = new StoryPresenter(session, net.Grams, net.Media, images, lodestone, VelvetArt.StoryRing, VelvetTheme.Palette,
            new StoryConfirmLabels(L.Velvet.DeleteConfirm, L.Velvet.DeleteCancel, L.Velvet.Saving), confirm,
            translation, realtimeSignals, "Velvet stories", StartStoryCompose, openProfile: OpenProfile);
        this.launcher = launcher;
        this.socialLauncher = socialLauncher;
        this.lodestone = lodestone;
        this.configuration = configuration;
        this.gameData = gameData;
        this.library = library;
        this.http = http;
        this.images = images;
        this.social = social;
        activityFeed = new SocialActivityFeed(SocialActivity.VelvetApp, session, net.Account);
        loadOlderActivity = activityFeed.LoadOlder;
        this.confirm = confirm;
        this.report = report;
        this.conduct = conduct;
        this.wallpaperImages = wallpaperImages;
        this.encryptionHelp = encryptionHelp;
        avatar = new AvatarComposer(() => store.AvatarBusy, store.UpdateAvatar,
            new AvatarComposerLabels(L.Velvet.ChangePhoto, L.Velvet.ImportFromPc, L.Velvet.NoPhotos,
                L.Velvet.MoveAndScale, L.Velvet.Use, L.Velvet.Saving, L.Velvet.GestureHint), library,
            wallpaperImages, confirm, () => store.AvatarFailure);
        cardPhotos = new AvatarComposer(() => store.CardPhotoBusy, store.AddCardPhoto,
            new AvatarComposerLabels(L.Velvet.AddPhoto, L.Velvet.ImportFromPc, L.Velvet.NoPhotos,
                L.Velvet.MoveAndScale, L.Velvet.Use, L.Velvet.Saving, L.Velvet.GestureHint), library,
            wallpaperImages, confirm, () => store.CardPhotoFailure, CardPhotoAspect);
        viewerSource = () => images.Get(viewerUrl);
        post = new VelvetPostComposer(store, stories, library, images, lodestone, wallpaperImages, OpenPostTags);
        router = new ViewRouter<VelvetView>(VelvetView.Root);
        drawView = DrawView;
        back = () => router.Pop();
        threadView = new ThreadView(this);
        LoadMutes();
    }

    public string Id => "velvet";

    public Vector4 Accent => AppAccents.For(Id);

    public string DisplayName => Loc.T(L.Apps.Velvet);

    public string Glyph => "Ve";

    public int BadgeCount => store.UnreadCount + store.RequestCount;
    public bool HasBadge => true;

    public ShareKindSet AcceptedShares =>
        GateAccepted && store.IsSignedIn && configuration.IsVelvetOnboarded()
            ? ShareKindSet.Photo
            : ShareKindSet.None;

    public void OnShare(in ShareItem item)
    {
        if (item.Kind != ShareKind.Photo)
        {
            return;
        }

        pendingSharedPhoto = item.LocalPath;
    }

    private void ConsumeSharedPhoto()
    {
        var path = pendingSharedPhoto;
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        pendingSharedPhoto = null;
        post.OpenWith(path);
        router.Push(VelvetView.Compose);
    }

    public void OnOpened()
    {
        router.Reset();
        activeTab = VelvetPage.Discover;
        messagesTab = VelvetMessagesTab.Chats;
        ResetChatsSearch();
        profileTab = VelvetProfileTab.About;
        avatarLightbox.Reset();
        store.ClearDiscover();
        ResetDeck();
        discoverInclude.Clear();
        feedInclude.Clear();
        RefreshAndConsumeLaunch();
    }

    public void OnResumed()
    {
        RefreshAndConsumeLaunch();
    }

    private void RefreshAndConsumeLaunch()
    {
        store.InvalidateLists();
        if (GateAccepted && store.IsSignedIn)
        {
            store.EnsureMe();
            store.RefreshRequests();
            stories.RefreshTray();
            ApplyFeedFilters();
        }

        if (GateAccepted && configuration.IsVelvetOnboarded() && store.IsSignedIn &&
            launcher.TryConsume(out var targetUserId))
        {
            OpenThread(targetUserId);
        }

        if (GateAccepted && configuration.IsVelvetOnboarded() && store.IsSignedIn &&
            socialLauncher.TryConsume(Id, out var link))
        {
            if (link.Kind == SocialLinkKind.Profile)
            {
                OpenProfile(link.Id);
            }
            else
            {
                store.EnsurePost(link.Id);
                router.Push(VelvetView.PostDetail(link.Id));
            }
        }
    }

    public void OnClosed()
    {
        store.FlushFeedSignals();
        postSheet.Close();
        threadSheet.Close();
        profileMenu.Close();
        photoSheet.Close();
        stories.Close();
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = theme;
        SyncLocalRace();

        if (!store.IsSignedIn)
        {
            TourHolds.Hold(Id);
            EmptyState.Draw(context.Content, ui, PhoneIcons.Moon, Loc.T(L.Velvet.SignedOutTitle),
                Loc.T(L.Velvet.SignedOutHint));
            return;
        }

        if (LocalRaceIsLalafell is true || store.AccessBlocked)
        {
            TourHolds.Hold(Id);
            store.EnsureMe();
            TickHeartbeat();
            var reason = store.RegionBlocked ? L.Velvet.UnavailableRegionBody : L.Velvet.UnavailableBody;
            EmptyState.Draw(context.Content, ui, PhoneIcons.Ban, Loc.T(L.Velvet.UnavailableTitle),
                Loc.T(reason));
            return;
        }

        if (!GateAccepted)
        {
            TourHolds.Hold(Id);
            DrawGate(context.Content);
            return;
        }

        if (!configuration.IsVelvetOnboarded())
        {
            TourHolds.Hold(Id);
            DrawOnboarding(context.Content);
            return;
        }

        TourHolds.Release(Id);
        store.EnsureMe();
        TickHeartbeat();
        GateMenus();
        var screen = SceneChrome.ScreenFrom(context.Content, theme, UiScale.Current);
        screenRect = screen;
        ui.Backdrop(screen);
        ConsumeSharedPhoto();
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

        var appArea = SceneChrome.AppAreaFrom(context.Content, theme, UiScale.Current);
        using (InputShield.Engage(avatarLightbox.Expanded))
        {
            router.Draw(appArea, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
        }

        if (avatarLightbox.Active)
        {
            avatarLightbox.Draw(screen, theme);
        }

        DrawPostSheet(screen);
        DrawThreadSheet(screen);
        DrawProfileMenu(screen);
        DrawPhotoSheet(screen);
    }

    public void Dispose()
    {
        threadView.Dispose();
        stories.Dispose();
        store.Dispose();
    }

    private bool GateAccepted =>
        configuration.VelvetAcknowledgedGate &&
        configuration.VelvetAcknowledgedGateVersion >= Configuration.VelvetGateVersion;

    private void TickHeartbeat()
    {
        sinceHeartbeat += ImGui.GetIO().DeltaTime;
        if (sinceHeartbeat >= HeartbeatSeconds)
        {
            sinceHeartbeat = 0f;
            store.Heartbeat(LocalRaceIsLalafell, localRaceId);
        }
    }

    private void SyncLocalRace()
    {
        if (!Plugin.Framework.IsInFrameworkUpdateThread)
        {
            return;
        }

        var contentId = InventoryReader.ReadLocalContentId();
        if (contentId != raceContentId)
        {
            raceContentId = contentId;
            raceKnown = false;
            cachedLalafell = false;
            localRaceId = 0;
        }

        if (raceKnown || contentId == 0)
        {
            return;
        }

        var local = gameData.LocalPlayer;
        if (local is null)
        {
            return;
        }

        var customize = local.Customize;
        var raceIndex = (int)CustomizeIndex.Race;
        if (customize.Length <= raceIndex)
        {
            return;
        }

        localRaceId = customize[raceIndex];
        cachedLalafell = localRaceId == LalafellRaceId;
        raceKnown = true;
    }

    private bool? LocalRaceIsLalafell => raceKnown ? cachedLalafell : null;

    private void DrawView(VelvetView view, Rect area, int depth)
    {
        ui.Body(area);
        switch (view.Screen)
        {
            case VelvetScreenId.Root:
                DrawRoot(area);
                break;
            case VelvetScreenId.Profile:
                DrawProfile(area, view.Arg ?? string.Empty);
                break;
            case VelvetScreenId.Thread:
                threadView.Draw(area, view.Arg ?? string.Empty);
                break;
            case VelvetScreenId.PostDetail:
                DrawPostDetail(area, view.Arg ?? string.Empty);
                break;
            case VelvetScreenId.Compose:
                DrawCompose(area);
                break;
            case VelvetScreenId.EditProfile:
                DrawEditProfile(area);
                break;
            case VelvetScreenId.Settings:
                DrawSettings(area);
                break;
            case VelvetScreenId.Activity:
                DrawActivity(area);
                break;
            case VelvetScreenId.Likers:
                DrawLikers(area, view.Arg ?? string.Empty);
                break;
            case VelvetScreenId.Blocked:
                DrawBlocked(area);
                break;
            case VelvetScreenId.NotInterested:
                DrawNotInterested(area);
                break;
            case VelvetScreenId.ChatImage:
                threadView.DrawImagePicker(area, view.Arg ?? string.Empty);
                break;
            case VelvetScreenId.ImageView:
                threadView.DrawImageViewer(area, view.Arg ?? string.Empty);
                break;
            case VelvetScreenId.Intro:
                DrawIntro(area, view.Arg ?? string.Empty);
                break;
            case VelvetScreenId.RequestDetail:
                DrawRequestDetail(area, view.Arg ?? string.Empty);
                break;
            case VelvetScreenId.Reactions:
                threadView.DrawReactions(area, view.Arg ?? string.Empty);
                break;
            case VelvetScreenId.Filters:
                DrawFilters(area);
                break;
            case VelvetScreenId.Search:
                DrawSearch(area);
                break;
            case VelvetScreenId.CardPreview:
                DrawCardPreview(area);
                break;
            case VelvetScreenId.PostTags:
                DrawPostTags(area);
                break;
            case VelvetScreenId.TagPosts:
                DrawTagPosts(area, view.Arg ?? string.Empty);
                break;
            case VelvetScreenId.EditCaption:
                DrawEditCaption(area);
                break;
            case VelvetScreenId.Encryption:
                threadView.DrawEncryptionScreen(area);
                break;
            case VelvetScreenId.UserPosts:
                DrawUserPosts(area, view.Arg ?? string.Empty);
                break;
            default:
                DrawRoot(area);
                break;
        }
    }

    private void DrawRoot(Rect area)
    {
        var scale = UiScale.Current;
        var headerHeight = VHeader.Height * scale;
        var tabHeight = TabBarHeight * scale;
        var headerRect = new Rect(area.Min, new Vector2(area.Max.X, area.Min.Y + headerHeight));
        var tabRect = new Rect(new Vector2(area.Min.X, area.Max.Y - tabHeight), area.Max);
        var bodyRect = new Rect(new Vector2(area.Min.X, headerRect.Max.Y),
            new Vector2(area.Max.X, tabRect.Min.Y));

        if (GuideIntents.Consume("velvet.tab.feed"))
        {
            activeTab = VelvetPage.Feed;
        }
        else if (GuideIntents.Consume("velvet.tab.messages"))
        {
            activeTab = VelvetPage.Messages;
        }
        else if (GuideIntents.Consume("velvet.tab.me"))
        {
            activeTab = VelvetPage.Me;
        }
        else if (GuideIntents.Consume("velvet.tab.discover"))
        {
            activeTab = VelvetPage.Discover;
        }

        DrawRootTopBar(headerRect);

        if (activeTab == VelvetPage.Feed)
        {
            bodyRect = DrawFeedScopeTabs(bodyRect);
        }

        switch (activeTab)
        {
            case VelvetPage.Feed:
                DrawFeed(bodyRect);
                break;
            case VelvetPage.Messages:
                DrawMessages(bodyRect);
                break;
            case VelvetPage.Me:
                DrawMe(bodyRect);
                break;
            default:
                DrawDiscover(bodyRect);
                break;
        }

        DrawTabBar(tabRect);
    }

    private void DrawRichBody(ImDrawListPtr drawList, RichTextLayout layout, Vector2 origin)
    {
        var ink = new RichTextInk(VelvetTheme.BodyInk, VelvetTheme.RoseGlow, VelvetTheme.RoseGlow);
        RichText.Draw(drawList, layout, origin, ink, out var hit);
        if (hit.Kind == RichTextRunKind.Mention && hit.Clicked)
        {
            OpenProfile(layout.Mentions[hit.TargetIndex].UserId);
        }

        if (hit.Kind == RichTextRunKind.Link && hit.Clicked)
        {
            UrlActions.AskThenOpen(layout.Urls[hit.TargetIndex]);
        }
    }

    private void OpenProfileFromPost(string userId, string postId)
    {
        store.ReportFeedSignal(postId, FeedSignalKinds.ProfileOpen);
        OpenProfile(userId);
    }

    private void OpenProfile(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        profileTab = VelvetProfileTab.About;
        store.OpenProfile(userId);
        router.Push(VelvetView.Profile(userId));
    }

    private void OpenThread(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        activeTab = VelvetPage.Messages;
        router.Push(VelvetView.Thread(userId));
    }
}
