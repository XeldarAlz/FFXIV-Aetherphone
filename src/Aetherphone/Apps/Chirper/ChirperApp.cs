using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Conduct;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Emoji;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Net;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Report;
using Aetherphone.Core.Sharing;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Translation;
using Aetherphone.Core.Wallpapers;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Chirper;

internal sealed partial class ChirperApp : IResumableApp
{
    private enum SheetKind
    {
        None,
        Post,
        Profile,
        Reply,
    }

    private enum PostSheetAction
    {
        Follow,
        Translate,
        Report,
        Block,
        Sensitive,
        Delete,
        Rules,
        Reactions,
        DeleteReply,
        RemoveReply,
    }

    private enum ActionGlyph
    {
        Reply,
        Rechirp,
        Share,
    }

    private enum HomeTab
    {
        Feed,
        Explore,
        Alerts,
        Profile,
    }

    private const int MaxPostLength = 300;
    private const int MaxCommentLength = 500;
    private const int PostSheetMaxItems = 5;
    private const float TopBarButtonRadius = 18f;
    private const float FeedTabRowHeight = 44f;
    private const float FeedTabUnderline = 4f;
    private const float TabBarHeight = 58f;
    private const float TabBarIconSize = 24f;
    private const float TabBarHoverRadius = 20f;
    private const float TabBarAvatarRadius = 13f;
    private const float TabBarAvatarRingGap = 2.5f;
    private const int TabCount = 4;
    private const int FilterToggleCount = 3;
    private const int TabRevalidateCooldownSeconds = 15;
    private const float FeedTopPadding = 2f;
    private const float CellPadX = 16f;
    private const float CellPadTop = 11f;
    private const float CellPadBottom = 6f;
    private const float FeedAvatarRadius = 21f;
    private const float AvatarGap = 11f;
    private const float ReplyAvatarRadius = 17f;
    private const float ReplyPadY = 10f;
    private const float MediaRounding = 15f;
    private const float SingleMediaHeight = 198f;
    private const float HeadMediaHeight = 250f;
    private const float GridMediaHeight = 148f;
    private const float MediaGridGap = 3f;
    private const float ActionRowMaxWidth = 300f;
    private const float ActionRowHeight = 34f;
    private const float ActionHitHeight = 44f;
    private const float ActionIconSize = 17f;
    private const float ReactionChipHeight = 30f;
    private const float ReactionChipGap = 6f;
    private const float ReactionChipEmoji = 14f;
    private const float SummaryPillHeight = 26f;
    private const float SummaryEmojiSize = 13f;
    private const float SummaryEmojiStep = 10f;
    private const int SummaryEmojiCount = 3;
    private const float ReactionsExpandSmoothTime = 0.12f;
    private const float MoreButtonRadius = 14f;
    private const float FeedBottomSpacer = 110f;
    private const float ControlRowHeight = 36f;
    private const float PickerEmojiMax = 21f;
    private const float PickerSlotMax = 31f;
    private const float PickerTwoRowThreshold = 24f;
    private const float RepostMenuWidth = 200f;
    private const float RepostMenuRowHeight = 42f;
    private const float FabRadius = 27f;
    private const float QuoteAvatarRadius = 9f;
    private const float QuoteThumbSize = 44f;
    private const int QuoteBodyMaxLines = 3;
    private const float SegmentSmoothTime = 0.09f;
    private const float SendRevealSmoothTime = 0.07f;
    private const float RowHeight = 44f;

    private static readonly TextStyle NameStyle = new(1f, FontWeight.SemiBold);
    private static readonly TextStyle MetaStyle = new(0.93f, FontWeight.Regular);
    private static readonly TextStyle BodyStyle = new(1.03f, FontWeight.Regular);
    private static readonly TextStyle HeadBodyStyle = new(1.17f, FontWeight.Regular);
    private static readonly TextStyle CountStyle = new(0.87f, FontWeight.SemiBold);
    private static readonly TextStyle ChipCountStyle = new(0.83f, FontWeight.SemiBold);
    private static readonly TextStyle BannerStyle = new(0.83f, FontWeight.SemiBold);
    private static readonly TextStyle QuoteNameStyle = new(0.9f, FontWeight.SemiBold);
    private static readonly TextStyle QuoteMetaStyle = new(0.87f, FontWeight.Regular);
    private static readonly TextStyle QuoteBodyStyle = new(0.93f, FontWeight.Regular);
    private static readonly TextStyle ReplyNameStyle = new(0.93f, FontWeight.SemiBold);
    private static readonly TextStyle ReplyBodyStyle = new(0.95f, FontWeight.Regular);
    private static readonly TextStyle ReplyMetaStyle = new(0.87f, FontWeight.Regular);
    private static readonly TextStyle LikeCountStyle = new(0.77f, FontWeight.SemiBold);
    private static readonly TextStyle SectionStyle = new(1f, FontWeight.Bold);
    private static readonly TextStyle DateStyle = new(0.9f, FontWeight.Regular);
    private static readonly TextStyle CapsuleStyle = new(0.87f, FontWeight.SemiBold);
    private static readonly TextStyle FeedTabStyle = new(1.07f, FontWeight.SemiBold);
    private static readonly TextStyle FeedTabIdleStyle = new(1.07f, FontWeight.Medium);
    private static readonly UnderlineTabStyle FeedTabsStyle = new(FeedTabStyle, FeedTabIdleStyle,
        ChirperInk.AccentLink, ChirperInk.SegmentIdleInk, ChirperInk.Accent, FeedTabUnderline, CellPadX,
        SegmentSmoothTime);
    private static readonly TextStyle WordmarkStyle = new(1.4f, FontWeight.Bold);
    private static readonly TextStyle BadgeStyle = new(0.67f, FontWeight.Bold);
    private static readonly TextStyle PopoverRowStyle = new(0.97f, FontWeight.SemiBold);

    private static readonly ActionSheetStyle SheetStyle = new(ChirperInk.GlassPanel, ChirperInk.GlassStroke,
        AppPalettes.Chirper.TitleInk, ChirperInk.Danger, AppPalettes.Chirper.Accent, ChirperInk.Hairline);

    private static readonly ScreenToastStyle ToastStyle = new(ChirperInk.GlassPanel, ChirperInk.GlassStroke,
        AppPalettes.Chirper.TitleInk);

    public string Id => "chirper";
    public Vector4 Accent => AppAccents.For(Id);
    public string DisplayName => Loc.T(L.Apps.Chirper);
    public string Glyph => "Ch";
    public int BadgeCount => social.UnseenCount(Id);
    public ShareKindSet AcceptedShares => store.IsSignedIn ? ShareKindSet.Photo : ShareKindSet.None;
    private readonly ChirperStore store;
    private readonly SocialLauncher launcher;
    private readonly GameData gameData;
    private readonly Configuration configuration;
    private readonly LodestoneService lodestone;
    private readonly RemoteImageCache images;
    private readonly SocialNotificationService social;
    private readonly ConductGateService conduct;
    private readonly AvatarComposer avatar;
    private readonly AvatarComposer banner;
    private readonly SocialProfilePages profile;
    private readonly AppSkin ui = new(AppPalettes.Chirper);
    private readonly ConfirmService confirm;
    private readonly TranslationService translation;
    private readonly RichTextCache bodyLayouts = new(scanHashtags: true);
    private readonly RichTextCache commentLayouts = new(scanHashtags: true);
    private readonly FeedVirtualizer feedVirtualizer = new(400f);
    private readonly FeedVirtualizer profileVirtualizer = new(400f);
    private readonly MentionPopup mentionPopup = new();
    private readonly ActionSheet sheet = new();
    private readonly FeedFilterSheet filterSheet = new();
    private readonly string[] filterLabels = new string[FilterToggleCount];
    private readonly ActionSheet.Item[] sheetItems = new ActionSheet.Item[PostSheetMaxItems];
    private readonly PostSheetAction[] sheetActions = new PostSheetAction[PostSheetMaxItems];
    private int sheetCount;
    private SheetKind sheetKind;
    private PostDto? sheetPost;
    private readonly ScreenToast toast = new();
    private readonly MentionAutocomplete composeMentions;
    private readonly MentionAutocomplete commentMentions;
    private readonly EmojiComposer composeEmoji = new() { IconPainter = PaintEmojiIcon };
    private readonly EmojiComposer commentEmoji = new() { IconPainter = PaintEmojiIcon };
    private readonly AvatarLightbox avatarLightbox = new();
    private readonly Dictionary<SocialFeedScope, PullToRefresh> pullToRefresh = new()
    {
        { SocialFeedScope.ForYou, new() },
        { SocialFeedScope.Following, new() }
    };
    private readonly ViewRouter<ChirperRoute> router;
    private readonly RouterDraw<ChirperRoute> drawView;
    private readonly Action back;
    private readonly SocialActivityFeed activityFeed;
    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private SocialFeedScope activeScope = SocialFeedScope.ForYou;
    private HomeTab homeTab;
    private Spring tabSegment;
    private Spring replySendReveal;
    private Spring reactionsExpand;
    private Rect reactionsExpandedRect;
    private string? reactionsExpandedPostId;
    private bool reactionsExpanded;
    private bool replyFocusPending;
    private Rect screenRect;
    private string draft = string.Empty;
    private bool composeFocus;
    private bool feedScrollTopPending;
    private readonly FailureSlot composeFailure = new();
    private readonly FailureSlot feedFailure = new();
    private readonly FailureSlot commentFailure = new();
    private string? commentRestore;
    private readonly CommentAttachment commentAttachment = new() { IconPainter = PaintPhotoIcon };
    private string? commentAttachmentRestore;
    private string composeStatus = string.Empty;
    private bool composeSensitive;
    private volatile int composeOutcome;
    private readonly ActionReveal<ChirperPanel> actions = new();
    private string commentDraft = string.Empty;
    private PostDto? quoteTarget;
    private string? quoteTargetId;
    private readonly HashSet<string> renderedUnderlyingIds = new(StringComparer.Ordinal);
    private readonly PhotoLibrary library;
    private readonly WallpaperImageCache wallpaperImages;
    private readonly PhotoViewerOverlay photoViewer = new();
    private readonly List<string> composeAttachments = new();
    private bool composePicking;
    private string[] composePickerPaths = Array.Empty<string>();
    private string? pendingComposePickedPath;
    private string? pendingSharedPhoto;
    private readonly FeedVirtualizer hashtagVirtualizer = new(400f);
    private string hashtagTitle = string.Empty;
    private int hashtagTodayCount;
    private string hashtagTitleTag = string.Empty;
    private UserDto? sheetUser;
    private string searchDraft = string.Empty;
    private double searchDirtyAt = -1d;
    private bool trendingRequested;
    private readonly RetryGate[] tabRevalidateGates = BuildTabRevalidateGates();
    private readonly RetryGate likedRevalidateGate = new(TimeSpan.FromSeconds(TabRevalidateCooldownSeconds));

    public ChirperApp(AethernetSession session, AethernetApi net, LodestoneService lodestone,
        RemoteImageCache images, PhotoLibrary library, SocialLauncher launcher, GameData gameData,
        Configuration configuration, SocialNotificationService social, WallpaperImageCache wallpaperImages,
        ConfirmService confirm, TranslationService translation, ReportService report, ConductGateService conduct,
        RealtimeSignalBus realtimeSignals)
    {
        this.translation = translation;
        this.confirm = confirm;
        store = new ChirperStore(session, net.Account, net.Social, net.Safety, net.Media, realtimeSignals);
        store.SetFeedRegions(SocialRegion.FilterCsv(configuration.ChirperFeedRegionMask));
        composeMentions = new MentionAutocomplete(store.NewMentionSuggestions());
        commentMentions = new MentionAutocomplete(store.NewMentionSuggestions());
        this.library = library;
        this.wallpaperImages = wallpaperImages;
        this.launcher = launcher;
        this.gameData = gameData;
        this.configuration = configuration;
        this.lodestone = lodestone;
        this.images = images;
        this.social = social;
        this.conduct = conduct;
        activityFeed = new SocialActivityFeed(SocialActivity.ChirperApp, session, net.Account);
        avatar = new AvatarComposer(() => store.AvatarBusy, store.UpdateAvatar,
            new AvatarComposerLabels(L.Chirper.ChangePhoto, L.Chirper.ImportFromPc, L.Photos.NoPhotos,
                L.Chirper.MoveAndScale, L.Chirper.Use, L.Chirper.Saving, L.Chirper.GestureHint), library,
            wallpaperImages, confirm, () => store.AvatarFailure);
        banner = new AvatarComposer(() => store.AvatarBusy, store.UpdateBanner,
            new AvatarComposerLabels(L.Chirper.ChangeBanner, L.Chirper.ImportFromPc, L.Photos.NoPhotos,
                L.Chirper.MoveAndScale, L.Chirper.Use, L.Chirper.Saving, L.Chirper.GestureHint), library,
            wallpaperImages, confirm, () => store.AvatarFailure, BannerUpload.Aspect);
        router = new ViewRouter<ChirperRoute>(ChirperRoute.Home);
        drawView = DrawView;
        back = () => router.Pop();
        profile = new SocialProfilePages(store, new SocialProfileStyle
        {
            Saving = L.Chirper.Saving,
            DeleteConfirmMessage = L.Chirper.DeleteConfirmMessage,
            DeleteConfirm = L.Chirper.DeleteConfirm,
            DeleteCancel = L.Chirper.DeleteCancel,
            DeleteFailed = L.Chirper.DeleteFailed,
            DeleteCommentConfirmMessage = L.Chirper.DeleteCommentConfirmMessage,
            DeleteCommentFailed = L.Chirper.DeleteCommentFailed,
            RemoveCommentConfirmMessage = L.Chirper.RemoveCommentConfirmMessage,
        }, confirm, report);
    }

    public void OnOpened()
    {
        router.Reset();
        avatarLightbox.Reset();
        draft = string.Empty;
        actions.Reset();
        ResetReactionsExpansion();
        sheet.Close();
        filterSheet.Close();
        homeTab = HomeTab.Feed;
        sheetKind = SheetKind.None;
        sheetPost = null;
        sheetUser = null;
        commentDraft = string.Empty;
        composeAttachments.Clear();
        composePicking = false;
        composeSensitive = false;
        store.ClearDiscover();
        trendingRequested = false;
        RefreshAndConsumeLaunch();
    }

    public void OnResumed()
    {
        RefreshAndConsumeLaunch();
    }

    private void RefreshAndConsumeLaunch()
    {
        for (var index = 0; index < tabRevalidateGates.Length; index++)
        {
            tabRevalidateGates[index].Reset();
        }

        likedRevalidateGate.Reset();

        if (store.IsSignedIn)
        {
            store.EnsureMe();
            store.RefreshFeed(SocialFeedScope.ForYou);
            store.RefreshFeed(SocialFeedScope.Following);
        }

        if (store.IsSignedIn && launcher.TryConsume(Id, out var link))
        {
            if (link.Kind == SocialLinkKind.Profile)
            {
                OpenProfile(link.Id);
            }
            else if (link.Kind == SocialLinkKind.Post)
            {
                OpenThreadFromLink(link.Id);
            }
        }
    }

    public void OnClosed()
    {
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = theme;
        sheet.Gate();
        filterSheet.Gate();
        actions.Tick(MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds));
        TickReactionsExpansion();
        if (actions.IsOpen)
        {
            UiInteract.BlockThisFrame();
        }

        var screen = SceneChrome.ScreenFrom(context.Content, theme, UiScale.Current);
        screenRect = screen;
        ui.Backdrop(screen);
        ConsumeSharedPhoto();
        if (photoViewer.Active)
        {
            photoViewer.Draw(screen, theme);
            return;
        }

        var appArea = new Rect(new Vector2(screen.Min.X, context.Content.Min.Y),
            new Vector2(screen.Max.X, context.Content.Max.Y));
        using (InputShield.Engage(avatarLightbox.Expanded))
        {
            router.Draw(appArea, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
        }

        if (avatarLightbox.Active)
        {
            avatarLightbox.Draw(screen, theme);
        }

        DrawSheet(screen);
        DrawFilterSheet(screen);
        toast.Draw(screen, ToastStyle);
    }

    private void DrawView(ChirperRoute route, Rect area, int depth)
    {
        ui.Body(area);
        switch (route.Screen)
        {
            case ChirperScreen.Compose:
                DrawCompose(area);
                break;
            case ChirperScreen.Profile:
                DrawProfile(area, route.UserId!);
                break;
            case ChirperScreen.EditProfile:
                DrawEditProfile(area);
                break;
            case ChirperScreen.Avatar:
                DrawAvatarCompose(area);
                break;
            case ChirperScreen.Banner:
                DrawBannerCompose(area);
                break;
            case ChirperScreen.Discover:
                DrawDiscover(area);
                break;
            case ChirperScreen.Thread:
                DrawThread(area, route.PostId!);
                break;
            case ChirperScreen.UserList:
                DrawUserList(area, route.UserId!, route.Kind);
                break;
            case ChirperScreen.Activity:
                DrawActivity(area);
                break;
            case ChirperScreen.Hashtag:
                DrawHashtag(area, route.Tag!);
                break;
            default:
                DrawHome(area);
                break;
        }
    }

    private void DrawHome(Rect area)
    {
        var scale = UiScale.Current;
        if (!store.IsSignedIn)
        {
            DrawHomeTopBar(area);
            TourHolds.Hold(Id);
            var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
            Typography.DrawCentered(body.Center, Loc.T(L.Chirper.SetUpAccount), ChirperInk.MutedInk);
            return;
        }

        TourHolds.Release(Id);
        var barRect = new Rect(new Vector2(area.Min.X, area.Max.Y - TabBarHeight * scale), area.Max);
        var content = new Rect(area.Min, new Vector2(area.Max.X, barRect.Min.Y));
        using (ImRaii.PushId((int)homeTab))
        {
            switch (homeTab)
            {
                case HomeTab.Explore:
                    DrawDiscover(content, true);
                    break;
                case HomeTab.Alerts:
                    DrawActivity(content, true);
                    break;
                case HomeTab.Profile:
                    DrawOwnProfileTab(content);
                    break;
                default:
                    DrawFeedTab(content);
                    break;
            }
        }

        DrawTabBar(barRect);
    }

    private void DrawOwnProfileTab(Rect area)
    {
        if (store.Me is { } me)
        {
            DrawProfile(area, me.Id, true);
            return;
        }

        store.EnsureMe();
        Typography.DrawCentered(area.Center, Loc.T(L.Common.Loading), ChirperInk.MutedInk);
    }

    private void DrawFeedTab(Rect area)
    {
        DrawHomeTopBar(area);
        var scale = UiScale.Current;
        var top = area.Min.Y + AppHeader.Height * scale;
        var rowRect = new Rect(new Vector2(area.Min.X, top), new Vector2(area.Max.X, top + FeedTabRowHeight * scale));
        DrawFeedTabs(rowRect);
        var listRect = new Rect(new Vector2(area.Min.X, rowRect.Max.Y), area.Max);
        DrawFeedList(listRect, activeScope);
        if (ComposeFab.Draw(listRect, "##chirperComposeFab", ChirperInk.Accent,
                PhoneIcons.Feather, Loc.T(L.Chirper.NewChirp), "chirper.compose",
                ChirperInk.AccentDeep, FabRadius, true))
        {
            BeginCompose();
        }
    }

    private void BeginCompose()
    {
        quoteTarget = null;
        quoteTargetId = null;
        composeAttachments.Clear();
        composePicking = false;
        composeSensitive = false;
        composeFocus = true;
        router.Push(ChirperRoute.Compose);
    }

    private void DrawFeedTabs(Rect row)
    {
        UiAnchors.Report("chirper.tabs", row);
        var picked = UnderlineTabs.Draw(row, Loc.T(L.Chirper.ForYou), Loc.T(L.Chirper.Following),
            activeScope == SocialFeedScope.Following, ref tabSegment, ChirperInk.Shared, FeedTabsStyle);
        if (picked < 0)
        {
            return;
        }

        var scope = picked == 1 ? SocialFeedScope.Following : SocialFeedScope.ForYou;
        if (scope == activeScope)
        {
            return;
        }

        activeScope = scope;
        actions.Reset();
        feedScrollTopPending = true;
        profile.EnsureLoaded(activeScope);
    }

    private void DrawTabBar(Rect bar)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        PaintBarBackdrop(drawList, bar);
        drawList.AddLine(bar.Min, new Vector2(bar.Max.X, bar.Min.Y), ImGui.GetColorU32(ChirperInk.Hairline), 1f);
        var slot = bar.Width / TabCount;
        for (var index = 0; index < TabCount; index++)
        {
            var tab = (HomeTab)index;
            var cellMin = new Vector2(bar.Min.X + slot * index, bar.Min.Y);
            var cellMax = new Vector2(cellMin.X + slot, bar.Max.Y);
            var active = homeTab == tab;
            var hovered = UiInteract.Hover(cellMin, cellMax);
            var iconCenter = new Vector2((cellMin.X + cellMax.X) * 0.5f, bar.Center.Y);
            var iconInk = active ? ChirperInk.AccentLink : hovered ? ChirperInk.TitleInk : GlassPillInk;
            var iconSize = TabBarIconSize * scale;
            if (hovered)
            {
                drawList.AddCircleFilled(iconCenter, TabBarHoverRadius * scale,
                    ImGui.GetColorU32(ChirperInk.FieldFill), 32);
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            string label;
            switch (tab)
            {
                case HomeTab.Explore:
                    PhoneIcon.Draw(drawList, iconCenter, PhoneIcons.Search, iconInk, iconSize);
                    label = Loc.T(L.Chirper.TabExplore);
                    break;
                case HomeTab.Alerts:
                    PhoneIcon.Draw(drawList, iconCenter, active ? PhoneIcons.BellFilled : PhoneIcons.Bell,
                        iconInk, iconSize);
                    DrawBellBadge(iconCenter, social.UnseenCount(Id));
                    label = Loc.T(L.Social.ActivityTitle);
                    break;
                case HomeTab.Profile:
                    DrawProfileTabIcon(drawList, iconCenter, active, iconInk, iconSize);
                    label = Loc.T(L.Chirper.TabProfile);
                    break;
                default:
                    PhoneIcon.Draw(drawList, iconCenter, active ? PhoneIcons.HomeFilled : PhoneIcons.Home,
                        iconInk, iconSize);
                    label = Loc.T(L.Chirper.TabHome);
                    break;
            }

            HoverTooltip.Show(new Rect(cellMin, cellMax), label, HoverLabelSide.Above);
            if (UiInteract.Click(cellMin, cellMax, hovered))
            {
                SelectHomeTab(tab);
            }
        }
    }

    private void DrawProfileTabIcon(ImDrawListPtr drawList, Vector2 center, bool active, Vector4 ink, float iconSize)
    {
        if (store.Me is not { } me)
        {
            store.EnsureMe();
            PhoneIcon.Draw(drawList, center, active ? PhoneIcons.UserFilled : PhoneIcons.User, ink, iconSize);
            return;
        }

        var scale = UiScale.Current;
        var radius = TabBarAvatarRadius * scale;
        DrawAvatar(drawList, center, radius, me.Name, me.World, me.AvatarUrl, 0.85f, 28, Frames.Of(me.FrameId));
        if (!active)
        {
            return;
        }

        drawList.AddCircle(center, radius + TabBarAvatarRingGap * scale, ImGui.GetColorU32(ChirperInk.AccentLink), 32,
            1.6f * scale);
    }

    private static RetryGate[] BuildTabRevalidateGates()
    {
        var cooldown = TimeSpan.FromSeconds(TabRevalidateCooldownSeconds);
        var gates = new RetryGate[TabCount];
        for (var index = 0; index < TabCount; index++)
        {
            gates[index] = new RetryGate(cooldown);
        }

        return gates;
    }

    private void SelectHomeTab(HomeTab tab)
    {
        actions.Reset();
        if (tab == HomeTab.Feed && homeTab == HomeTab.Feed)
        {
            feedScrollTopPending = true;
        }

        if (tab == HomeTab.Alerts)
        {
            social.MarkSeen(Id);
            social.RefreshNow();
        }

        if (tab == HomeTab.Profile)
        {
            store.EnsureMe();
        }

        RevalidateTab(tab);
        homeTab = tab;
    }

    private void RevalidateTab(HomeTab tab)
    {
        if (!store.IsSignedIn || !tabRevalidateGates[(int)tab].TryPass())
        {
            return;
        }

        switch (tab)
        {
            case HomeTab.Explore:
                RunDiscoverQuery();
                break;
            case HomeTab.Alerts:
                activityFeed.Invalidate();
                break;
            case HomeTab.Profile:
                if (store.Me is { } me)
                {
                    store.RevalidateProfile(me.Id);
                }

                break;
            default:
                if (!store.IsLoading(activeScope))
                {
                    store.RefreshFeed(activeScope);
                }

                break;
        }
    }

    private bool FeedFiltersActive()
    {
        return !configuration.ChirperShowPhotoPosts || !configuration.ChirperShowGifPosts
            || !configuration.ChirperShowCommentMedia || configuration.ChirperFeedRegionMask != 0;
    }

    private void OpenFilterSheet()
    {
        actions.Reset();
        filterSheet.Open();
    }

    private void DrawSheet(Rect screen)
    {
        if (!sheet.CapturesPointer)
        {
            return;
        }

        switch (sheetKind)
        {
            case SheetKind.Post:
                DrawPostSheet(screen);
                break;
            case SheetKind.Profile:
                DrawProfileSheet(screen);
                break;
            case SheetKind.Reply:
                DrawReplySheet(screen);
                break;
        }
    }

    private void DrawFilterSheet(Rect screen)
    {
        if (!filterSheet.CapturesPointer)
        {
            return;
        }

        filterLabels[0] = Loc.T(L.Settings.ChirperShowPhotos);
        filterLabels[1] = Loc.T(L.Settings.ChirperShowGifs);
        filterLabels[2] = Loc.T(L.Settings.ChirperShowReplyMedia);
        Span<bool> values = stackalloc bool[FilterToggleCount];
        values[0] = configuration.ChirperShowPhotoPosts;
        values[1] = configuration.ChirperShowGifPosts;
        values[2] = configuration.ChirperShowCommentMedia;
        var picked = filterSheet.Draw(screen, ChirperInk.Shared, Loc.T(L.Chirper.FeedFilters), filterLabels, values,
            configuration.ChirperFeedRegionMask, Loc.T(L.Chirper.Regions), Loc.T(L.Chirper.Done));
        switch (picked)
        {
            case 0:
                configuration.ChirperShowPhotoPosts = !configuration.ChirperShowPhotoPosts;
                break;
            case 1:
                configuration.ChirperShowGifPosts = !configuration.ChirperShowGifPosts;
                break;
            case 2:
                configuration.ChirperShowCommentMedia = !configuration.ChirperShowCommentMedia;
                break;
            case >= FilterToggleCount:
                configuration.ChirperFeedRegionMask = SocialRegion.ToggleMask(configuration.ChirperFeedRegionMask,
                    picked - FilterToggleCount);
                store.SetFeedRegions(SocialRegion.FilterCsv(configuration.ChirperFeedRegionMask));
                break;
            default:
                return;
        }

        configuration.Save();
    }

    private void OpenPostSheet(PostDto post)
    {
        actions.Reset();
        sheetPost = post;
        sheetKind = SheetKind.Post;
        sheetCount = 0;
        if (post.TotalReactions > 0)
        {
            AddSheetItem(PostSheetAction.Reactions, Loc.T(L.Chirper.ViewReactions), false);
        }

        var mine = store.Me is { } me && me.Id == post.AuthorId;
        if (mine)
        {
            var canVeil = !post.SensitiveLocked && PostMedia.Photos(post.MediaUrls, post.MediaUrl).Length > 0;
            if (canVeil)
            {
                AddSheetItem(PostSheetAction.Sensitive,
                    Loc.T(post.Sensitive ? L.Moderation.SensitiveOn : L.Moderation.MarkSensitive), false);
            }

            AddSheetItem(PostSheetAction.Delete, Loc.T(L.Chirper.DeleteChirp), true);
        }
        else
        {
            AddSheetItem(PostSheetAction.Follow, HandleLabel(post.IsFollowing, post.AuthorHandle), false);
            var key = new TranslationKey(TranslationSurface.Post, post.Id);
            if (translation.Peek(key).State != TranslationState.Idle || translation.ShouldOffer(post.Lang))
            {
                AddSheetItem(PostSheetAction.Translate, Loc.T(L.Chirper.TranslateChirp), false);
            }

            AddSheetItem(PostSheetAction.Report, Loc.T(L.Chirper.ReportChirp), true);
            AddSheetItem(PostSheetAction.Block,
                post.AuthorHandle.Length > 0
                    ? Loc.T(L.Chirper.BlockHandle, post.AuthorHandle)
                    : Loc.T(L.Social.BlockAction), true);
        }

        sheet.Open();
    }

    private static string HandleLabel(bool following, string handle)
    {
        if (handle.Length == 0)
        {
            return Loc.T(following ? L.Chirper.Unfollow : L.Chirper.Follow);
        }

        return Loc.T(following ? L.Chirper.UnfollowHandle : L.Chirper.FollowHandle, handle);
    }

    private void AddSheetItem(PostSheetAction action, string label, bool danger)
    {
        sheetActions[sheetCount] = action;
        sheetItems[sheetCount] = new ActionSheet.Item(label, string.Empty, danger);
        sheetCount++;
    }

    private void DrawPostSheet(Rect screen)
    {
        var picked = sheet.Draw(screen, SheetStyle, sheetItems.AsSpan(0, sheetCount), Loc.T(L.Common.Cancel),
            false);
        if (picked < 0 || sheetPost is not { } post)
        {
            return;
        }

        switch (sheetActions[picked])
        {
            case PostSheetAction.Reactions:
                OpenUserList(post.Id, UserListKind.Likers);
                break;
            case PostSheetAction.Follow:
                store.SetFollow(post.AuthorId, !post.IsFollowing);
                break;
            case PostSheetAction.Translate:
                var key = new TranslationKey(TranslationSurface.Post, post.Id);
                TranslateLink.Activate(translation, confirm, key, post.Text, translation.Peek(key));
                break;
            case PostSheetAction.Report:
                profile.OpenReport("post", post.Id, Loc.T(L.Report.PostTitle));
                break;
            case PostSheetAction.Block:
                profile.AskBlock(post.AuthorDisplayName, post.AuthorHandle, post.AuthorId);
                break;
            case PostSheetAction.Sensitive:
                store.SetSensitive(post.Id, !post.Sensitive);
                break;
            case PostSheetAction.Delete:
                profile.AskDeletePost(post.Id, () => toast.Show(Loc.T(L.Chirper.DeletedToast)));
                break;
        }
    }

    private void OpenActivity()
    {
        social.MarkSeen(Id);
        social.RefreshNow();
        RevalidateTab(HomeTab.Alerts);
        router.Push(ChirperRoute.Activity);
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
        actions.Reset();
        store.RefreshFeed(scope);
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
                store.IsLoading(scope), ChirperInk.MutedInk, () => RefreshFeed(scope));

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
                    scope == SocialFeedScope.Following ? Loc.T(L.Chirper.FollowingEmpty) :
                    Loc.T(L.Chirper.ExploreEmpty);
                Typography.DrawCentered(new Vector2(listRect.Center.X, listRect.Min.Y + 90f * UiScale.Current),
                    message, ChirperInk.MutedInk);
                if (failed)
                {
                    Typography.DrawCentered(
                        new Vector2(listRect.Center.X, listRect.Min.Y + 118f * UiScale.Current),
                        Loc.T(L.Failure.PullToRetry), ChirperInk.MutedInk, TextStyles.Footnote);
                }
            }
            else
            {
                ImGui.Dummy(new Vector2(0f, FeedTopPadding * UiScale.Current));
                feedVirtualizer.BeginFrame(store.FeedSource(scope));
                renderedUnderlyingIds.Clear();
                for (var index = 0; index < snapshot.Length; index++)
                {
                    var post = snapshot[index];
                    if (HiddenByMediaPreference(post))
                    {
                        continue;
                    }

                    if (!renderedUnderlyingIds.Add(post.RepostOfId ?? post.Id))
                    {
                        continue;
                    }

                    if (feedVirtualizer.Skip(post.Id))
                    {
                        continue;
                    }

                    DrawPost(post);
                    feedVirtualizer.Record(post.Id);
                }

                if (store.LoadingMore(scope))
                {
                    InfiniteScroll.DrawLoadingRow(listRect.Center.X, ChirperInk.MutedInk);
                }

                ImGui.Dummy(new Vector2(0f, FeedBottomSpacer * UiScale.Current));
                if (InfiniteScroll.ReachedBottom() && store.HasMoreFeed(scope) && !store.LoadingMore(scope))
                {
                    store.LoadMoreFeed(scope);
                }
            }
        }
    }

    private void DrawPost(PostDto post, bool isThreadHead = false, PostDto? repostBy = null)
    {
        if (post.RepostOfId is not null)
        {
            if (post.ReferencedPost is not null)
            {
                DrawPost(post.ReferencedPost, isThreadHead, post);
            }
            else
            {
                DrawUnavailableCell();
            }

            return;
        }

        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var padX = CellPadX * scale;
        var cellRight = origin.X + width;
        var bannerHeight = repostBy is not null ? Typography.LineHeight(BannerStyle) + 6f * scale : 0f;
        var headerTop = origin.Y + CellPadTop * scale + bannerHeight;
        var avatarRadius = FeedAvatarRadius * scale;
        var avatarCenter = new Vector2(origin.X + padX + avatarRadius, headerTop + avatarRadius);
        var contentLeft = avatarCenter.X + avatarRadius + AvatarGap * scale;
        var contentRight = cellRight - padX;
        var contentWidth = MathF.Max(1f, contentRight - contentLeft);
        var moreRadius = MoreButtonRadius * scale;
        var moreCenter = new Vector2(contentRight - moreRadius + 6f * scale, headerTop + moreRadius - 4f * scale);
        var headerRight = moreCenter.X - moreRadius - 4f * scale;
        var headerWidth = MathF.Max(1f, headerRight - contentLeft);
        var nameHeight = Typography.LineHeight(NameStyle);
        var bodyStyle = isThreadHead ? HeadBodyStyle : BodyStyle;
        var textTop = headerTop + nameHeight + 3f * scale;
        RichTextLayout? bodyLayout = null;
        var translateKey = new TranslationKey(TranslationSurface.Post, post.Id);
        var bodyView = translation.View(translateKey, post.Text, post.Lang);
        var bodyText = bodyView.Text;
        if (bodyText.Length > 0)
        {
            using (Plugin.Fonts.Push(bodyStyle.Scale))
            {
                bodyLayout = bodyLayouts.LayoutFor(bodyView.LayoutKey, bodyText, post.Mentions, contentWidth);
            }
        }

        var textHeight = bodyText.Length == 0
            ? 0f
            : bodyLayout?.Size.Y ?? Typography.MeasureWrapped(bodyText, contentWidth, bodyStyle.Scale);
        var translateHeight = TranslateLink.Height(translation, translateKey, post.Lang, scale);
        var dateHeight = isThreadHead && post.CreatedAtUnix > 0
            ? 10f * scale + Typography.LineHeight(DateStyle)
            : 0f;
        var afterText = textTop + textHeight + translateHeight + dateHeight;
        var photos = PostMedia.Photos(post.MediaUrls, post.MediaUrl);
        var mediaHeight = MediaBlockHeight(photos.Length, isThreadHead);
        var mediaTop = afterText + (photos.Length > 0 ? 9f * scale : 0f);
        var hasQuote = post.QuotedPostId is not null;
        var quoteHeight = hasQuote ? QuotedCardHeight(post.ReferencedPost, contentWidth) : 0f;
        var quoteTop = mediaTop + mediaHeight + (hasQuote ? 9f * scale : 0f);
        var contentBody = quoteTop + quoteHeight;
        var hasReactions = post.TotalReactions > 0;
        var reactionsTop = contentBody + (hasReactions ? (isThreadHead ? 12f : 9f) * scale : 0f);
        var expandProgress = isThreadHead ? 1f : ReactionsExpandProgress(post);
        var reactionsHeight = hasReactions ? ReactionsBlockHeight(post, contentWidth, expandProgress) : 0f;
        var actionsTop = MathF.Max(reactionsTop + reactionsHeight, avatarCenter.Y + avatarRadius) + 6f * scale;
        var actionsHeight = ActionRowHeight * scale;
        var cellBottom = actionsTop + actionsHeight + CellPadBottom * scale;
        var cell = FeedCell.Begin(drawList, cellBottom - origin.Y, ChirperInk.HoverTint, !isThreadHead);
        if (repostBy is not null)
        {
            DrawRepostBanner(origin, cellRight, repostBy);
        }

        DrawAvatar(drawList, avatarCenter, avatarRadius,
            SocialIdentity.Name(post.AuthorDisplayName, post.AuthorHandle), string.Empty, post.AuthorAvatarUrl,
            0.95f, 48, Frames.Of(post.AuthorFrameId));
        if (UiInteract.HoverClick(avatarCenter - new Vector2(avatarRadius, avatarRadius),
                avatarCenter + new Vector2(avatarRadius, avatarRadius)))
        {
            OpenProfile(post.AuthorId);
        }

        var rawDisplayName = SocialIdentity.Name(post.AuthorDisplayName, post.AuthorHandle);
        var drawnNameWidth = UserName.DrawAuto(drawList, "chirper.post.author." + post.Id, rawDisplayName,
            post.AuthorBadges, post.AuthorBadgeIds, contentLeft, headerTop, headerWidth * 0.45f, NameStyle,
            ChirperInk.TitleInk, theme);
        var nameMin = new Vector2(contentLeft, headerTop);
        var nameMax = new Vector2(contentLeft + drawnNameWidth, headerTop + nameHeight);
        if (UiInteract.Hover(nameMin, nameMax))
        {
            drawList.AddLine(new Vector2(nameMin.X, nameMax.Y - 1f * scale),
                new Vector2(nameMax.X, nameMax.Y - 1f * scale), ImGui.GetColorU32(ChirperInk.TitleInk), 1f);
        }

        if (UiInteract.HoverClick(nameMin, nameMax))
        {
            OpenProfile(post.AuthorId);
        }

        var meta = SocialIdentity.FeedMeta(post.AuthorHandle, TimeText.Short(post.CreatedAtUnix));
        if (ContentModeration.IsInReview(post.ScanStatus))
        {
            meta = $"{meta} · {Loc.T(L.Moderation.InReview)}";
        }

        var metaLeft = nameMax.X + 5f * scale;
        var metaFitted = Typography.FitText(meta, MathF.Max(1f, headerRight - metaLeft), MetaStyle);
        var metaSize = Typography.Measure(metaFitted, MetaStyle);
        Typography.Draw(drawList, new Vector2(metaLeft, headerTop + (nameHeight - metaSize.Y) * 0.5f), metaFitted,
            ChirperInk.MutedInk, MetaStyle);

        var moreExtent = new Vector2(moreRadius, moreRadius);
        var moreHovered = UiInteract.Hover(moreCenter - moreExtent, moreCenter + moreExtent);
        if (moreHovered)
        {
            drawList.AddCircleFilled(moreCenter, moreRadius, ImGui.GetColorU32(ChirperInk.AccentWash), 24);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        PhoneIcon.Draw(drawList, moreCenter, PhoneIcons.Dots,
            moreHovered ? ChirperInk.Accent : ChirperInk.MutedInk, 16f * scale);
        HoverTooltip.Show(new Rect(moreCenter - moreExtent, moreCenter + moreExtent), Loc.T(L.Chirper.More),
            HoverLabelSide.Above);
        if (UiInteract.Click(moreCenter - moreExtent, moreCenter + moreExtent, moreHovered))
        {
            OpenPostSheet(post);
        }

        if (bodyText.Length > 0 && bodyLayout is null)
        {
            ImGui.SetCursorScreenPos(new Vector2(contentLeft, textTop));
            using (Typography.WrapAt(contentRight))
            using (Plugin.Fonts.Push(bodyStyle.Scale))
            using (ImRaii.PushColor(ImGuiCol.Text, ChirperInk.BodyInk))
            {
                Typography.Wrapped(bodyText);
            }
        }
        else if (bodyLayout is not null)
        {
            using (Plugin.Fonts.Push(bodyStyle.Scale))
            {
                DrawRichBody(drawList, bodyLayout, new Vector2(contentLeft, textTop));
            }
        }

        if (translateHeight > 0f)
        {
            TranslateLink.Draw(translation, confirm, translateKey, post.Lang, post.Text,
                new Vector2(contentLeft, textTop + textHeight), contentWidth, ChirperInk.MutedInk,
                ChirperInk.AccentLink, scale);
        }

        if (dateHeight > 0f)
        {
            Typography.Draw(drawList,
                new Vector2(contentLeft, textTop + textHeight + translateHeight + 10f * scale),
                FullTimestamp(post.CreatedAtUnix), ChirperInk.MutedInk, DateStyle);
        }

        if (photos.Length > 0)
        {
            DrawPostMedia(post, photos, new Rect(new Vector2(contentLeft, mediaTop),
                new Vector2(contentRight, mediaTop + mediaHeight)));
        }

        if (hasQuote)
        {
            DrawQuotedCard(drawList, new Vector2(contentLeft, quoteTop), contentWidth, quoteHeight,
                post.ReferencedPost, true, post.Id);
        }

        if (hasReactions)
        {
            if (isThreadHead)
            {
                var picked = DrawReactionChips(post, contentLeft, contentWidth, reactionsTop, 1f, true);
                if (picked >= 0)
                {
                    store.ToggleReaction(post, picked);
                }
            }
            else
            {
                DrawFeedReactions(post, contentLeft, contentWidth, reactionsTop, reactionsHeight, expandProgress);
            }
        }

        DrawActionRow(post, contentLeft, contentWidth, actionsTop + actionsHeight * 0.5f, isThreadHead, cellRight);
        if (cell.Tapped)
        {
            OpenThread(post);
        }

        FeedCell.End(drawList, cell, ChirperInk.Hairline, !isThreadHead || !hasReactions);
    }

    private void PaintBarBackdrop(ImDrawListPtr drawList, Rect bar)
    {
        var target = new Rect(bar.Min, new Vector2(bar.Max.X, MathF.Max(bar.Max.Y, screenRect.Max.Y)));
        ui.PaintGradient(drawList, target, screenRect, 0f);
    }

    private static void PaintEmojiIcon(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 color)
    {
        PhoneIcon.Draw(drawList, center, PhoneIcons.MoodSmile, color, radius * 1.12f);
    }

    private static void PaintPhotoIcon(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 color)
    {
        PhoneIcon.Draw(drawList, center, PhoneIcons.Photo, color, radius * 1.12f);
    }

    private static void DrawHairline(ImDrawListPtr drawList, float left, float right, float y) =>
        FeedCell.Hairline(drawList, left, right, y, ChirperInk.Hairline);

    private static string FullTimestamp(long unixSeconds)
    {
        var local = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToLocalTime();
        return $"{TimeText.Clock(local)} · {local.ToString("d MMM yyyy", Loc.Culture)}";
    }

    private void DrawRepostBanner(Vector2 origin, float cellRight, PostDto repostBy)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var lineHeight = Typography.LineHeight(BannerStyle);
        var top = origin.Y + CellPadTop * scale;
        var iconLeft = origin.X + (CellPadX + 30f) * scale;
        var centerY = top + lineHeight * 0.5f;
        PhoneIcon.Draw(drawList, new Vector2(iconLeft + 6.5f * scale, centerY), PhoneIcons.Repeat,
            ChirperInk.MutedInk, 13f * scale);
        var mine = store.Me is { } me && me.Id == repostBy.AuthorId;
        var label = mine
            ? Loc.T(L.Chirper.YouReposted)
            : Loc.T(L.Chirper.Reposted, SocialIdentity.Name(repostBy.AuthorDisplayName, repostBy.AuthorHandle));
        var textLeft = iconLeft + 20f * scale;
        var fitted = Typography.FitText(label, MathF.Max(1f, cellRight - CellPadX * scale - textLeft), BannerStyle);
        Typography.Draw(drawList, new Vector2(textLeft, top), fitted, ChirperInk.MutedInk, BannerStyle);
    }

    private void DrawActionRow(PostDto post, float left, float width, float centerY, bool isThreadHead,
        float cellRight)
    {
        var scale = UiScale.Current;
        var rowLeft = left - 8f * scale;
        var rowWidth = MathF.Min(ActionRowMaxWidth * scale, width + 8f * scale);
        var replyCount = post.CommentCount > 0 ? post.CommentCount.ToString(Loc.Culture) : string.Empty;
        var repostCount = post.RepostCount > 0 ? post.RepostCount.ToString(Loc.Culture) : string.Empty;
        var replyWidth = ActionTargetWidth(replyCount);
        var repostWidth = ActionTargetWidth(repostCount);
        var plainWidth = ActionTargetWidth(string.Empty);
        var free = MathF.Max(0f, rowWidth - (replyWidth + repostWidth + plainWidth * 2f));
        var gap = free / 3f;
        var cursorX = rowLeft;
        if (DrawActionTarget(cursorX, centerY, replyWidth, ActionGlyph.Reply, replyCount,
                ChirperInk.MutedInk, ChirperInk.Accent, Loc.T(L.Chirper.Reply)))
        {
            if (isThreadHead)
            {
                replyFocusPending = true;
            }
            else
            {
                OpenThread(post);
            }
        }

        cursorX += replyWidth + gap;
        var repostInk = post.MyReposted ? ChirperInk.RechirpGreen : ChirperInk.MutedInk;
        if (DrawActionTarget(cursorX, centerY, repostWidth, ActionGlyph.Rechirp, repostCount, repostInk,
                ChirperInk.RechirpGreen, Loc.T(post.MyReposted ? L.Chirper.Unrepost : L.Chirper.Repost)))
        {
            actions.Open(post.Id, ChirperPanel.Repost);
        }

        cursorX += repostWidth + gap;
        if (DrawReactTarget(post, cursorX, centerY, plainWidth))
        {
            actions.Open(post.Id, ChirperPanel.Picker);
        }

        cursorX += plainWidth + gap;
        if (DrawActionTarget(cursorX, centerY, plainWidth, ActionGlyph.Share, string.Empty,
                ChirperInk.MutedInk, ChirperInk.Accent, Loc.T(L.Chirper.CopyChirp)))
        {
            CopyChirp(post);
        }

        var popoverBottom = centerY - ActionRowHeight * scale * 0.5f - 4f * scale;
        if (actions.IsShowing(post.Id, ChirperPanel.Picker))
        {
            DrawReactionPicker(post, rowLeft, cellRight - 12f * scale, popoverBottom);
        }
        else if (actions.IsShowing(post.Id, ChirperPanel.Repost))
        {
            DrawRepostMenu(post, rowLeft, popoverBottom);
        }
    }

    private static float ActionTargetWidth(string count)
    {
        var scale = UiScale.Current;
        var width = (ActionIconSize + 16f) * scale;
        if (count.Length > 0)
        {
            width += 5f * scale + Typography.Measure(count, CountStyle).X;
        }

        return width;
    }

    private static bool DrawActionTarget(float x, float centerY, float width, ActionGlyph glyph, string count,
        Vector4 ink, Vector4 hoverInk, string tooltip)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var half = ActionHitHeight * scale * 0.5f;
        var min = new Vector2(x, centerY - half);
        var max = new Vector2(x + width, centerY + half);
        var hovered = UiInteract.Hover(min, max);
        var color = hovered ? hoverInk : ink;
        var iconCenter = new Vector2(x + (8f + ActionIconSize * 0.5f) * scale, centerY);
        var iconSize = ActionIconSize * scale;
        var packed = ImGui.GetColorU32(color);
        switch (glyph)
        {
            case ActionGlyph.Reply:
                PhoneIcon.Draw(drawList, iconCenter, PhoneIcons.MessageCircle, packed, iconSize);
                break;
            case ActionGlyph.Rechirp:
                PhoneIcon.Draw(drawList, iconCenter, PhoneIcons.Repeat, packed, iconSize);
                break;
            default:
                PhoneIcon.Draw(drawList, iconCenter, PhoneIcons.Share, packed, iconSize);
                break;
        }

        if (count.Length > 0)
        {
            var size = Typography.Measure(count, CountStyle);
            Typography.Draw(drawList,
                new Vector2(iconCenter.X + (ActionIconSize * 0.5f + 5f) * scale, centerY - size.Y * 0.5f), count,
                color, CountStyle);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        HoverTooltip.Show(new Rect(min, max), tooltip, HoverLabelSide.Above);
        return UiInteract.Click(min, max, hovered);
    }

    private static bool DrawReactTarget(PostDto post, float x, float centerY, float width)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var half = ActionHitHeight * scale * 0.5f;
        var min = new Vector2(x, centerY - half);
        var max = new Vector2(x + width, centerY + half);
        var hovered = UiInteract.Hover(min, max);
        var ink = hovered ? ChirperInk.Warning : ChirperInk.MutedInk;
        var iconCenter = new Vector2(x + (8f + ActionIconSize * 0.5f) * scale, centerY);
        if (post.MyReaction >= 0)
        {
            var emojiHalf = 8f * scale * (hovered ? 1.12f : 1f);
            EmojiImages.TryDraw(drawList, ChirperReactions.EmojiFile(post.MyReaction),
                iconCenter - new Vector2(emojiHalf, emojiHalf), iconCenter + new Vector2(emojiHalf, emojiHalf),
                0xFFFFFFFFu);
        }
        else
        {
            PhoneIcon.Draw(drawList, iconCenter, PhoneIcons.MoodPlus, ink, 18f * scale);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        HoverTooltip.Show(new Rect(min, max), Loc.T(L.Chirper.React), HoverLabelSide.Above);
        return UiInteract.Click(min, max, hovered);
    }

    private void CopyChirp(PostDto post)
    {
        var text = post.Text.Length > 0
            ? post.Text
            : SocialIdentity.Name(post.AuthorDisplayName, post.AuthorHandle);
        ImGui.SetClipboardText(text);
        toast.Show(Loc.T(L.Common.Copied));
    }

    private static int CollectReactions(PostDto post, Span<int> order)
    {
        var active = 0;
        for (var kind = 0; kind < ChirperReactions.Count; kind++)
        {
            if (ReactionTally.At(post.ReactionCounts, kind) > 0)
            {
                order[active++] = kind;
            }
        }

        OrderReactions(post, order[..active]);
        return active;
    }

    private static void OrderReactions(PostDto post, Span<int> order)
    {
        for (var index = 1; index < order.Length; index++)
        {
            var kind = order[index];
            var position = index;
            while (position > 0 && ReactionComesBefore(post, kind, order[position - 1]))
            {
                order[position] = order[position - 1];
                position--;
            }

            order[position] = kind;
        }
    }

    private static bool ReactionComesBefore(PostDto post, int candidate, int existing)
    {
        var candidateMine = post.MyReaction == candidate;
        var existingMine = post.MyReaction == existing;
        if (candidateMine != existingMine)
        {
            return candidateMine;
        }

        var candidateCount = ReactionTally.At(post.ReactionCounts, candidate);
        var existingCount = ReactionTally.At(post.ReactionCounts, existing);
        if (candidateCount != existingCount)
        {
            return candidateCount > existingCount;
        }

        return candidate < existing;
    }


    private void DrawReactionSummary(PostDto post, float left, float top)
    {
        Span<int> order = stackalloc int[ChirperReactions.Count];
        var active = CollectReactions(post, order);
        if (active == 0)
        {
            return;
        }

        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var shown = Math.Min(SummaryEmojiCount, active);
        var emojiSize = SummaryEmojiSize * scale;
        var step = SummaryEmojiStep * scale;
        var countText = CountText.Compact(post.TotalReactions);
        var countSize = Typography.Measure(countText, CountStyle);
        var padLeft = 6f * scale;
        var padRight = 10f * scale;
        var emojiSpan = emojiSize + (shown - 1) * step;
        var width = padLeft + emojiSpan + 10f * scale + countSize.X + padRight;
        var height = SummaryPillHeight * scale;
        var min = new Vector2(left, top);
        var max = new Vector2(left + width, top + height);
        var hovered = UiInteract.Hover(min, max);
        var mine = post.MyReaction >= 0;
        var fill = mine ? ChirperInk.MineFill : hovered ? ChirperInk.ChipHover : ChirperInk.ChipFill;
        var stroke = mine ? ChirperInk.MineStroke : ChirperInk.ChipStroke;
        var ink = mine ? ChirperInk.MineInk : ChirperInk.BodyInk;
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(fill));
        Squircle.Stroke(drawList, min, max, height * 0.5f, ImGui.GetColorU32(stroke), 1f);
        var centerY = top + height * 0.5f;
        for (var index = shown - 1; index >= 0; index--)
        {
            var emojiMin = new Vector2(min.X + padLeft + index * step, centerY - emojiSize * 0.5f);
            EmojiImages.TryDraw(drawList, ChirperReactions.EmojiFile(order[index]), emojiMin,
                emojiMin + new Vector2(emojiSize, emojiSize), 0xFFFFFFFFu);
        }

        Typography.Draw(drawList, new Vector2(min.X + padLeft + emojiSpan + 10f * scale, centerY - countSize.Y * 0.5f),
            countText, ink, CountStyle);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var single = active == 1;
        HoverTooltip.Show(new Rect(min, max),
            single ? ChirperReactions.Label(order[0]) : Loc.T(L.Chirper.PickReaction), HoverLabelSide.Above);
        if (!UiInteract.Click(min, max, hovered))
        {
            return;
        }

        if (single)
        {
            store.ToggleReaction(post, order[0]);
            return;
        }

        ExpandReactions(post);
    }

    private void DrawFeedReactions(PostDto post, float left, float width, float top, float blockHeight,
        float expandProgress)
    {
        if (expandProgress <= 0f)
        {
            DrawReactionSummary(post, left, top);
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var blockMin = new Vector2(left, top);
        reactionsExpandedRect = new Rect(blockMin, new Vector2(left + width, top + ReactionChipRowsHeight(post, width)));
        drawList.PushClipRect(blockMin, new Vector2(left + width, top + blockHeight), true);
        var picked = DrawReactionChips(post, left, width, top, expandProgress, reactionsExpanded);
        drawList.PopClipRect();
        if (picked < 0)
        {
            return;
        }

        store.ToggleReaction(post, picked);
        CollapseReactions();
    }

    private static float ReactionsBlockHeight(PostDto post, float width, float expandProgress)
    {
        var pillHeight = SummaryPillHeight * UiScale.Current;
        if (expandProgress <= 0f)
        {
            return pillHeight;
        }

        return pillHeight + (ReactionChipRowsHeight(post, width) - pillHeight) * expandProgress;
    }

    private float ReactionsExpandProgress(PostDto post) =>
        reactionsExpandedPostId == post.Id ? Math.Clamp(reactionsExpand.Value, 0f, 1f) : 0f;

    private void ExpandReactions(PostDto post)
    {
        if (reactionsExpandedPostId != post.Id)
        {
            reactionsExpand.SnapTo(0f);
        }

        reactionsExpandedPostId = post.Id;
        reactionsExpanded = true;
    }

    private void CollapseReactions()
    {
        reactionsExpanded = false;
    }

    private void ResetReactionsExpansion()
    {
        reactionsExpandedPostId = null;
        reactionsExpanded = false;
        reactionsExpand.SnapTo(0f);
    }

    private void TickReactionsExpansion()
    {
        if (reactionsExpandedPostId is null)
        {
            return;
        }

        if (reactionsExpanded && UiInteract.ClickedOutside(reactionsExpandedRect.Min, reactionsExpandedRect.Max, false))
        {
            CollapseReactions();
        }

        var target = reactionsExpanded ? 1f : 0f;
        reactionsExpand.Step(target, ReactionsExpandSmoothTime,
            MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds));
        if (!reactionsExpanded && reactionsExpand.IsResting(0f, 0.002f, 0.01f))
        {
            ResetReactionsExpansion();
        }
    }

    private static float ReactionChipWidth(PostDto post, int kind)
    {
        var scale = UiScale.Current;
        var countText = ReactionTally.At(post.ReactionCounts, kind).ToString(Loc.Culture);
        var countSize = Typography.Measure(countText, ChipCountStyle);
        return (11f + ReactionChipEmoji + 5f + 11f) * scale + countSize.X;
    }

    private static int ReactionChipRowCount(PostDto post, float width)
    {
        Span<int> order = stackalloc int[ChirperReactions.Count];
        var active = CollectReactions(post, order);
        if (active == 0)
        {
            return 0;
        }

        var gap = ReactionChipGap * UiScale.Current;
        var rows = 1;
        var cursorX = 0f;
        for (var index = 0; index < active; index++)
        {
            var chipWidth = ReactionChipWidth(post, order[index]);
            if (cursorX > 0f && cursorX + chipWidth > width)
            {
                rows++;
                cursorX = 0f;
            }

            cursorX += chipWidth + gap;
        }

        return rows;
    }

    private static float ReactionChipRowsHeight(PostDto post, float width)
    {
        var rows = ReactionChipRowCount(post, width);
        if (rows == 0)
        {
            return 0f;
        }

        var scale = UiScale.Current;
        return rows * ReactionChipHeight * scale + (rows - 1) * ReactionChipGap * scale;
    }

    private static int DrawReactionChips(PostDto post, float left, float width, float top, float alpha,
        bool interactive)
    {
        Span<int> order = stackalloc int[ChirperReactions.Count];
        var active = CollectReactions(post, order);
        if (active == 0)
        {
            return -1;
        }

        var scale = UiScale.Current;
        var gap = ReactionChipGap * scale;
        var rowStride = (ReactionChipHeight + ReactionChipGap) * scale;
        var cursorX = 0f;
        var row = 0;
        var picked = -1;
        for (var index = 0; index < active; index++)
        {
            var kind = order[index];
            var chipWidth = ReactionChipWidth(post, kind);
            if (cursorX > 0f && cursorX + chipWidth > width)
            {
                row++;
                cursorX = 0f;
            }

            var centerY = top + row * rowStride + ReactionChipHeight * scale * 0.5f;
            if (DrawReactionChip(post, left + cursorX, centerY, kind, chipWidth, alpha, interactive))
            {
                picked = kind;
            }

            cursorX += chipWidth + gap;
        }

        return picked;
    }

    private static bool DrawReactionChip(PostDto post, float x, float centerY, int kind, float chipWidth, float alpha,
        bool interactive)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var mine = post.MyReaction == kind;
        var countText = ReactionTally.At(post.ReactionCounts, kind).ToString(Loc.Culture);
        var countSize = Typography.Measure(countText, ChipCountStyle);
        var chipHeight = ReactionChipHeight * scale;
        var min = new Vector2(x, centerY - chipHeight * 0.5f);
        var max = new Vector2(x + chipWidth, centerY + chipHeight * 0.5f);
        var hovered = interactive && UiInteract.Hover(min, max);
        var grow = hovered ? 0.03f : 0f;
        var drawMin = min - new Vector2(chipWidth, chipHeight) * grow;
        var drawMax = max + new Vector2(chipWidth, chipHeight) * grow;
        var fill = Faded(mine ? ChirperInk.MineFill : ChirperInk.ChipFill, alpha);
        var stroke = Faded(mine ? ChirperInk.MineStroke : ChirperInk.ChipStroke, alpha);
        var ink = Faded(mine ? ChirperInk.MineInk : ChirperInk.BodyInk, alpha);
        Squircle.Fill(drawList, drawMin, drawMax, (drawMax.Y - drawMin.Y) * 0.5f, ImGui.GetColorU32(fill));
        Squircle.Stroke(drawList, drawMin, drawMax, (drawMax.Y - drawMin.Y) * 0.5f, ImGui.GetColorU32(stroke), 1f);
        var emojiSize = ReactionChipEmoji * scale;
        var emojiMin = new Vector2(min.X + 11f * scale, centerY - emojiSize * 0.5f);
        EmojiImages.TryDraw(drawList, ChirperReactions.EmojiFile(kind), emojiMin,
            emojiMin + new Vector2(emojiSize, emojiSize), ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha)));
        Typography.Draw(drawList, new Vector2(emojiMin.X + emojiSize + 5f * scale, centerY - countSize.Y * 0.5f),
            countText, ink, ChipCountStyle);
        if (!interactive)
        {
            return false;
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        HoverTooltip.Show(new Rect(min, max), ChirperReactions.Label(kind), HoverLabelSide.Above);
        return UiInteract.Click(min, max, hovered);
    }

    private static Vector4 Faded(Vector4 color, float alpha) => Palette.WithAlpha(color, color.W * alpha);

    private void DrawReactionPicker(PostDto post, float left, float right, float bottom)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetForegroundDrawList();
        var progress = Easing.EaseOutQuint(Math.Clamp(actions.Progress, 0f, 1f));
        var padX = 10f * scale;
        var padY = 6f * scale;
        var innerWidth = MathF.Max(1f, right - left - padX * 2f);
        var count = ChirperReactions.Count;
        var step = MathF.Min(PickerSlotMax * scale, innerWidth / count);
        var rows = step < PickerTwoRowThreshold * scale ? 2 : 1;
        var perRow = rows == 1 ? count : (count + 1) / 2;
        step = MathF.Min(PickerSlotMax * scale, innerWidth / perRow);
        var emojiSize = MathF.Max(1f, MathF.Min(PickerEmojiMax * scale, step - 3f * scale));
        var rowHeight = emojiSize + 8f * scale;
        var rowGap = 4f * scale;
        var height = padY * 2f + rows * rowHeight + (rows - 1) * rowGap;
        var top = bottom - height;
        var grow = 0.92f + 0.08f * progress;
        var rise = (1f - progress) * 8f * scale;
        var pivot = new Vector2((left + right) * 0.5f, bottom);
        var min = pivot + (new Vector2(left, top) - pivot) * grow + new Vector2(0f, rise);
        var max = pivot + (new Vector2(right, bottom) - pivot) * grow + new Vector2(0f, rise);
        var rounding = rows == 1 ? (max.Y - min.Y) * 0.5f : 16f * scale;
        drawList.PushClipRect(screenRect.Min, screenRect.Max, false);
        PopoverSurface.DrawGlass(drawList, min, max, rounding, ChirperInk.Shared, scale, progress);
        var interactive = !actions.Closing && actions.Progress > 0.6f;
        var tint = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, progress));
        for (var kind = 0; kind < count; kind++)
        {
            var row = kind / perRow;
            var column = kind % perRow;
            var rowSlots = Math.Min(perRow, count - row * perRow);
            var rowWidth = rowSlots * step * grow;
            var rowLeft = (min.X + max.X) * 0.5f - rowWidth * 0.5f;
            var slotCenter = new Vector2(rowLeft + (column + 0.5f) * step * grow,
                min.Y + (padY + row * (rowHeight + rowGap) + rowHeight * 0.5f) * grow);
            var slotHalf = new Vector2(step * 0.5f, rowHeight * 0.5f);
            var slotMin = slotCenter - slotHalf;
            var slotMax = slotCenter + slotHalf;
            var hovered = interactive && UiInteract.HoverWindowOnly(slotMin, slotMax, false);
            if (post.MyReaction == kind)
            {
                drawList.AddCircleFilled(slotCenter, step * 0.46f,
                    ImGui.GetColorU32(Palette.WithAlpha(ChirperInk.Accent, 0.25f * progress)), 24);
            }

            var half = emojiSize * 0.5f * (hovered ? 1.35f : 1f) * grow;
            var emojiCenter = hovered ? slotCenter - new Vector2(0f, 3f * scale) : slotCenter;
            EmojiImages.TryDraw(drawList, ChirperReactions.EmojiFile(kind), emojiCenter - new Vector2(half, half),
                emojiCenter + new Vector2(half, half), tint);
            if (!hovered)
            {
                continue;
            }

            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            HoverTooltip.Enqueue(new Rect(slotMin, slotMax), ChirperReactions.Label(kind), 1f,
                HoverLabelSide.Above);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                store.ToggleReaction(post, kind);
                actions.Dismiss();
            }
        }

        drawList.PopClipRect();
        actions.DismissOnOutsideClick(min, max);
    }

    private void DrawRepostMenu(PostDto post, float left, float bottom)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetForegroundDrawList();
        var progress = Easing.EaseOutQuint(Math.Clamp(actions.Progress, 0f, 1f));
        var width = RepostMenuWidth * scale;
        var rowHeight = RepostMenuRowHeight * scale;
        var pad = 5f * scale;
        var height = pad * 2f + rowHeight * 2f;
        var maxRight = screenRect.Max.X - 12f * scale;
        if (left + width > maxRight)
        {
            left = MathF.Max(screenRect.Min.X + 12f * scale, maxRight - width);
        }

        var right = left + width;
        var top = bottom - height;
        var grow = 0.92f + 0.08f * progress;
        var rise = (1f - progress) * 8f * scale;
        var pivot = new Vector2((left + right) * 0.5f, bottom);
        var min = pivot + (new Vector2(left, top) - pivot) * grow + new Vector2(0f, rise);
        var max = pivot + (new Vector2(right, bottom) - pivot) * grow + new Vector2(0f, rise);
        drawList.PushClipRect(screenRect.Min, screenRect.Max, false);
        PopoverSurface.DrawGlass(drawList, min, max, 16f * scale, ChirperInk.Shared, scale, progress);
        var interactive = !actions.Closing && actions.Progress > 0.6f;
        var padGrown = pad * grow;
        var rowGrown = rowHeight * grow;
        var firstMin = new Vector2(min.X + padGrown, min.Y + padGrown);
        var firstMax = new Vector2(max.X - padGrown, firstMin.Y + rowGrown);
        var secondMin = new Vector2(firstMin.X, firstMax.Y);
        var secondMax = new Vector2(firstMax.X, secondMin.Y + rowGrown);
        var repostInk = post.MyReposted ? ChirperInk.RechirpGreen : ChirperInk.TitleInk;
        if (DrawPopoverRow(drawList, firstMin, firstMax, true,
                Loc.T(post.MyReposted ? L.Chirper.Unrepost : L.Chirper.Repost), repostInk, progress, interactive))
        {
            var reposting = !post.MyReposted;
            store.ToggleRepost(post);
            if (reposting)
            {
                toast.Show(Loc.T(L.Chirper.RepostedToast));
            }

            actions.Dismiss();
        }

        if (DrawPopoverRow(drawList, secondMin, secondMax, false,
                Loc.T(L.Chirper.QuoteChirp), ChirperInk.TitleInk, progress, interactive))
        {
            BeginQuote(post);
        }

        drawList.PopClipRect();
        actions.DismissOnOutsideClick(min, max);
    }

    private static bool DrawPopoverRow(ImDrawListPtr drawList, Vector2 min, Vector2 max, bool rechirpGlyph,
        string label, Vector4 ink, float alpha, bool interactive)
    {
        var scale = UiScale.Current;
        var hovered = interactive && UiInteract.HoverWindowOnly(min, max, false);
        if (hovered)
        {
            Squircle.Fill(drawList, min, max, 11f * scale, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.07f * alpha)));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var faded = Palette.WithAlpha(ink, ink.W * alpha);
        var centerY = (min.Y + max.Y) * 0.5f;
        var glyphCenter = new Vector2(min.X + 20f * scale, centerY);
        if (rechirpGlyph)
        {
            PhoneIcon.Draw(drawList, glyphCenter, PhoneIcons.Repeat, faded, 17f * scale);
        }
        else
        {
            PhoneIcon.Draw(drawList, glyphCenter, PhoneIcons.Quote, faded, 17f * scale);
        }

        var textLeft = min.X + 39f * scale;
        var fitted = Typography.FitText(label, MathF.Max(1f, max.X - textLeft - 10f * scale), PopoverRowStyle);
        var size = Typography.Measure(fitted, PopoverRowStyle);
        Typography.Draw(drawList, new Vector2(textLeft, centerY - size.Y * 0.5f), fitted, faded, PopoverRowStyle);
        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private void DrawUnavailableCell()
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var cell = FeedCell.Begin(drawList, 48f * scale, ChirperInk.HoverTint, false);
        var padX = CellPadX * scale;
        var text = Typography.FitText(Loc.T(L.Chirper.Unavailable),
            MathF.Max(1f, cell.Bounds.Width - padX * 2f), MetaStyle);
        var size = Typography.Measure(text, MetaStyle);
        Typography.Draw(drawList, new Vector2(cell.Bounds.Min.X + padX,
            cell.Bounds.Min.Y + (cell.Bounds.Height - size.Y) * 0.5f), text, ChirperInk.MutedInk, MetaStyle);
        FeedCell.End(drawList, cell, ChirperInk.Hairline);
    }

    private float QuotedCardHeight(PostDto? quoted, float width)
    {
        var scale = UiScale.Current;
        var padY = 10f * scale;
        var padX = 12f * scale;
        var headerHeight = MathF.Max(QuoteAvatarRadius * 2f * scale, Typography.LineHeight(QuoteNameStyle));
        if (quoted is null)
        {
            return padY * 2f + headerHeight;
        }

        var quotedPhotos = PostMedia.Photos(quoted.MediaUrls, quoted.MediaUrl);
        var hasMedia = quotedPhotos.Length > 0 && !HiddenByMediaPreference(quotedPhotos);
        var innerWidth = width - padX * 2f - (hasMedia ? QuoteThumbSize * scale + 8f * scale : 0f);
        var bodyLine = Typography.LineHeight(QuoteBodyStyle);
        var textHeight = quoted.Text.Length > 0
            ? MathF.Min(Typography.MeasureWrapped(quoted.Text, innerWidth, QuoteBodyStyle.Scale),
                bodyLine * QuoteBodyMaxLines)
            : 0f;
        var gap = quoted.Text.Length > 0 ? 4f * scale : 0f;
        var height = padY + headerHeight + gap + textHeight + padY;
        return hasMedia ? MathF.Max(height, padY * 2f + QuoteThumbSize * scale) : height;
    }

    private void DrawQuotedCard(ImDrawListPtr drawList, Vector2 min, float width, float height, PostDto? quoted,
        bool tappable, string hostId)
    {
        var scale = UiScale.Current;
        var max = new Vector2(min.X + width, min.Y + height);
        var padY = 10f * scale;
        var padX = 12f * scale;
        var rounding = MediaRounding * scale;
        var hovered = tappable && UiInteract.Hover(min, max);
        Squircle.Fill(drawList, min, max, rounding,
            ImGui.GetColorU32(hovered ? ChirperInk.QuoteHover : ChirperInk.QuoteFill));
        Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(ChirperInk.ChipStroke), 1f);
        var headerHeight = MathF.Max(QuoteAvatarRadius * 2f * scale, Typography.LineHeight(QuoteNameStyle));
        if (quoted is null)
        {
            var unavailable = Typography.FitText(Loc.T(L.Chirper.Unavailable), MathF.Max(1f, width - padX * 2f),
                QuoteMetaStyle);
            var unavailableSize = Typography.Measure(unavailable, QuoteMetaStyle);
            Typography.Draw(drawList,
                new Vector2(min.X + padX, min.Y + padY + (headerHeight - unavailableSize.Y) * 0.5f), unavailable,
                ChirperInk.MutedInk, QuoteMetaStyle);
            return;
        }

        var quotedPhotos = PostMedia.Photos(quoted.MediaUrls, quoted.MediaUrl);
        if (HiddenByMediaPreference(quotedPhotos))
        {
            quotedPhotos = Array.Empty<string>();
        }

        var thumbReserve = quotedPhotos.Length > 0 ? QuoteThumbSize * scale + 8f * scale : 0f;
        var innerWidth = width - padX * 2f - thumbReserve;
        var avatarRadius = QuoteAvatarRadius * scale;
        var headerCenterY = min.Y + padY + headerHeight * 0.5f;
        var avatarCenter = new Vector2(min.X + padX + avatarRadius, headerCenterY);
        var rawName = SocialIdentity.Name(quoted.AuthorDisplayName, quoted.AuthorHandle);
        DrawAvatar(drawList, avatarCenter, avatarRadius, rawName, string.Empty, quoted.AuthorAvatarUrl, 0.6f, 24,
            Frames.Of(quoted.AuthorFrameId));
        var nameLeft = avatarCenter.X + avatarRadius + 6f * scale;
        var nameHeight = Typography.LineHeight(QuoteNameStyle);
        var nameTop = headerCenterY - nameHeight * 0.5f;
        var nameMaxWidth = MathF.Max(1f, (min.X + padX + innerWidth - nameLeft) * 0.6f);
        var drawnNameWidth = UserName.DrawAuto(drawList, "chirper.quote.author." + hostId, rawName,
            quoted.AuthorBadges, quoted.AuthorBadgeIds, nameLeft, nameTop, nameMaxWidth, QuoteNameStyle,
            ChirperInk.TitleInk, theme);
        var meta = SocialIdentity.FeedMeta(quoted.AuthorHandle, TimeText.Short(quoted.CreatedAtUnix));
        var metaLeft = nameLeft + drawnNameWidth + 6f * scale;
        var clippedMeta = Typography.FitText(meta, MathF.Max(1f, min.X + padX + innerWidth - metaLeft),
            QuoteMetaStyle);
        var metaSize = Typography.Measure(clippedMeta, QuoteMetaStyle);
        Typography.Draw(drawList, new Vector2(metaLeft, headerCenterY - metaSize.Y * 0.5f), clippedMeta,
            ChirperInk.MutedInk, QuoteMetaStyle);
        if (quoted.Text.Length > 0)
        {
            var bodyTop = min.Y + padY + headerHeight + 4f * scale;
            var bodyBottom = MathF.Min(max.Y - padY, bodyTop + Typography.LineHeight(QuoteBodyStyle) * QuoteBodyMaxLines);
            ImGui.PushClipRect(new Vector2(min.X, bodyTop), new Vector2(max.X, bodyBottom), true);
            ImGui.SetCursorScreenPos(new Vector2(min.X + padX, bodyTop));
            using (Typography.WrapAt(min.X + padX + innerWidth))
            using (Plugin.Fonts.Push(QuoteBodyStyle.Scale))
            using (ImRaii.PushColor(ImGuiCol.Text, ChirperInk.QuoteBodyInk))
            {
                Typography.Wrapped(quoted.Text);
            }

            ImGui.PopClipRect();
        }

        if (quotedPhotos.Length > 0)
        {
            var thumbSize = QuoteThumbSize * scale;
            var thumbMin = new Vector2(max.X - padX - thumbSize, min.Y + (height - thumbSize) * 0.5f);
            var thumbMax = thumbMin + new Vector2(thumbSize, thumbSize);
            var thumbRounding = 8f * scale;
            var quoteVeiled = SensitiveReveals.ShouldVeil(quoted.Sensitive, quoted.Id,
                configuration.ShowSensitiveContent);
            if (quoteVeiled)
            {
                SensitiveVeil.Draw(drawList, thumbMin, thumbMax, thumbRounding);
            }
            else
            {
                var texture = MediaTexture(quotedPhotos[0]);
                if (texture is null)
                {
                    Squircle.Fill(drawList, thumbMin, thumbMax, thumbRounding, ImGui.GetColorU32(ChirperInk.ChipFill));
                }
                else
                {
                    var (uv0, uv1) = ImageFit.CoverSquare(texture.Size);
                    drawList.AddImageRounded(texture.Handle, thumbMin, thumbMax, uv0, uv1, 0xFFFFFFFFu,
                        thumbRounding, ImDrawFlags.RoundCornersAll);
                }
            }

            if (quotedPhotos.Length > 1 && !quoteVeiled)
            {
                MultiPhotoBadge.Draw(drawList, new Vector2(thumbMax.X - 4f * scale, thumbMin.Y + 4f * scale), scale);
            }
        }

        if (tappable && UiInteract.HoverClick(min, max))
        {
            OpenThread(quoted);
        }
    }

    private bool HiddenByMediaPreference(PostDto post)
    {
        var target = post.RepostOfId is not null && post.ReferencedPost is not null ? post.ReferencedPost : post;
        return HiddenByMediaPreference(PostMedia.Photos(target.MediaUrls, target.MediaUrl));
    }

    private bool HiddenByMediaPreference(string[] photos)
    {
        if (photos.Length == 0)
        {
            return false;
        }

        return GifMedia.IsGif(photos[0]) ? !configuration.ChirperShowGifPosts : !configuration.ChirperShowPhotoPosts;
    }

    private bool HiddenByMediaPreference(CommentDto comment)
    {
        return comment.Text.Length == 0 && CommentMediaHidden(comment.MediaUrl);
    }

    private bool CommentMediaHidden(string? mediaUrl)
    {
        return mediaUrl is not null && !configuration.ChirperShowCommentMedia;
    }

    private static float MediaBlockHeight(int count, bool isThreadHead)
    {
        if (count == 0)
        {
            return 0f;
        }

        var scale = UiScale.Current;
        if (count == 1)
        {
            return (isThreadHead ? HeadMediaHeight : SingleMediaHeight) * scale;
        }

        var rows = (count + 1) / 2;
        return rows * GridMediaHeight * scale + (rows - 1) * MediaGridGap * scale;
    }

    private static ImDrawFlags CornerFlags(bool topLeft, bool topRight, bool bottomLeft, bool bottomRight)
    {
        var flags = ImDrawFlags.None;
        if (topLeft)
        {
            flags |= ImDrawFlags.RoundCornersTopLeft;
        }

        if (topRight)
        {
            flags |= ImDrawFlags.RoundCornersTopRight;
        }

        if (bottomLeft)
        {
            flags |= ImDrawFlags.RoundCornersBottomLeft;
        }

        if (bottomRight)
        {
            flags |= ImDrawFlags.RoundCornersBottomRight;
        }

        return flags == ImDrawFlags.None ? ImDrawFlags.RoundCornersNone : flags;
    }

    private void DrawPostMedia(PostDto post, string[] photos, Rect rect)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var rounding = MediaRounding * scale;
        var veiled = SensitiveReveals.ShouldVeil(post.Sensitive, post.Id, configuration.ShowSensitiveContent);
        if (veiled)
        {
            SensitiveVeil.Draw(drawList, rect.Min, rect.Max, rounding);
            drawList.AddRect(rect.Min, rect.Max, ImGui.GetColorU32(ChirperInk.ChipStroke), rounding,
                ImDrawFlags.RoundCornersAll, 1f);
            if (UiInteract.HoverClick(rect.Min, rect.Max))
            {
                SensitiveReveals.Reveal(post.Id);
            }

            return;
        }

        if (photos.Length == 1)
        {
            DrawMediaTile(drawList, photos[0], rect, rounding, ImDrawFlags.RoundCornersAll, post.ScanStatus);
        }
        else
        {
            var gap = MediaGridGap * scale;
            var rowHeight = GridMediaHeight * scale;
            var rows = (photos.Length + 1) / 2;
            var columnWidth = (rect.Width - gap) * 0.5f;
            for (var index = 0; index < photos.Length; index++)
            {
                var row = index / 2;
                var column = index % 2;
                var lastRow = row == rows - 1;
                var spans = lastRow && column == 0 && index == photos.Length - 1;
                var tileMin = new Vector2(rect.Min.X + column * (columnWidth + gap),
                    rect.Min.Y + row * (rowHeight + gap));
                var tileMax = new Vector2(spans ? rect.Max.X : tileMin.X + columnWidth, tileMin.Y + rowHeight);
                var rightEdge = column == 1 || spans;
                var corners = CornerFlags(row == 0 && column == 0, row == 0 && rightEdge, lastRow && column == 0,
                    lastRow && rightEdge);
                DrawMediaTile(drawList, photos[index], new Rect(tileMin, tileMax), rounding, corners,
                    post.ScanStatus);
            }
        }

        drawList.AddRect(rect.Min, rect.Max, ImGui.GetColorU32(ChirperInk.ChipStroke), rounding,
            ImDrawFlags.RoundCornersAll, 1f);
    }

    private void DrawMediaTile(ImDrawListPtr drawList, string url, Rect rect, float rounding, ImDrawFlags corners,
        string? scanStatus)
    {
        var texture = MediaTexture(url);
        if (texture is null)
        {
            drawList.AddRectFilled(rect.Min, rect.Max, ImGui.GetColorU32(ChirperInk.ChipFill), rounding, corners);
            Typography.DrawCentered(drawList, rect.Center,
                Loc.T(images.Failed(url) ? L.Common.ImageFailed : L.Common.Loading), ChirperInk.MutedInk, MetaStyle);
        }
        else
        {
            var (uv0, uv1) = ImageFit.Cover(texture.Size.X, texture.Size.Y, rect.Width, rect.Height);
            drawList.AddImageRounded(texture.Handle, rect.Min, rect.Max, uv0, uv1, 0xFFFFFFFFu, rounding, corners);
        }

        if (GifMedia.IsGif(url))
        {
            GifBadge.Draw(drawList, rect);
        }

        ModerationOverlay.Draw(drawList, rect.Min, rect.Max, rounding, scanStatus);
        if (UiInteract.HoverClick(rect.Min, rect.Max))
        {
            photoViewer.Open(this, () => MediaTexture(url));
        }
    }

    private IDalamudTextureWrap? MediaTexture(string? url) => GifMedia.Texture(images, url, ImGui.GetTime());

    private void BeginQuote(PostDto post)
    {
        actions.Reset();
        var target = post.RepostOfId is not null && post.ReferencedPost is not null ? post.ReferencedPost : post;
        quoteTarget = target;
        quoteTargetId = target.Id;
        draft = string.Empty;
        composeStatus = string.Empty;
        composeAttachments.Clear();
        composePicking = false;
        composeSensitive = false;
        composeFocus = true;
        router.Push(ChirperRoute.Compose);
    }

    private void DrawAvatar(ImDrawListPtr drawList, Vector2 center, float radius, string name, string world,
        string? avatarUrl, float monogramScale, int segments, FrameStyle? frame = null)
    {
        AvatarView.DrawRemote(drawList, center, radius, theme, name, world, avatarUrl, images, lodestone,
            monogramScale, segments, 1f, frame);
    }

    private void OpenProfile(string userId)
    {
        actions.Reset();
        store.OpenProfile(userId);
        router.Push(ChirperRoute.Profile(userId));
    }

    private void DrawRichBody(ImDrawListPtr drawList, RichTextLayout layout, Vector2 origin)
    {
        var ink = new RichTextInk(ChirperInk.BodyInk, ChirperInk.AccentLink, ChirperInk.AccentLink);
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

    private void OpenThread(PostDto post)
    {
        actions.Reset();
        commentDraft = string.Empty;
        store.OpenDetail(post);
        router.Push(ChirperRoute.Thread(post.Id));
    }

    private void OpenThreadFromLink(string postId)
    {
        actions.Reset();
        commentDraft = string.Empty;
        store.OpenDetailById(postId);
        router.Push(ChirperRoute.Thread(postId));
    }

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
        if (!store.IsSignedIn)
        {
            return;
        }

        quoteTarget = null;
        quoteTargetId = null;
        draft = string.Empty;
        composeStatus = string.Empty;
        composeAttachments.Clear();
        composePicking = false;
        composeSensitive = false;
        AddComposeAttachment(path);
        composeFocus = true;
        router.Push(ChirperRoute.Compose);
    }

    public void Dispose()
    {
        store.Dispose();
    }
}
