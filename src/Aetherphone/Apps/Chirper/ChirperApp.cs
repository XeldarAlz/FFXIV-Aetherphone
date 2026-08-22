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
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Report;
using Aetherphone.Core.Sharing;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Wallpapers;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Chirper;

internal sealed partial class ChirperApp : IPhoneApp
{
    private const int MaxPostLength = 300;
    private const float FeedTopPadding = 8f;
    private const int MaxCommentLength = 500;
    private const string MediaFilterMenuId = "chirper.mediaFilterMenu";
    private const string OverflowMenuId = "chirper.overflowMenu";
    private const int HomeActionSlots = 3;
    private const float ReactionEmojiWidth = 18f;
    private const float ReactionEmojiFill = 0.62f;
    private const float ReactionSlotMin = 30f;
    private const float ReactionSlotMax = 34f;
    private const float ReactionChipHeight = 26f;
    private const float ReactionChipGap = 6f;
    private const float ReactionRowGap = 8f;
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
    private readonly ConfirmService confirmService;
    private readonly AvatarComposer avatar;
    private readonly SocialProfilePages profile;
    private readonly AppSkin ui = new(AppPalettes.Chirper);
    private readonly RichTextCache bodyLayouts = new(scanHashtags: true);
    private readonly RichTextCache commentLayouts = new(scanHashtags: true);
    private readonly FeedVirtualizer feedVirtualizer = new(400f);
    private readonly FeedVirtualizer profileVirtualizer = new(400f);
    private readonly MentionPopup mentionPopup = new();
    private readonly DropdownMenu mediaFilterMenu = new();
    private readonly DropdownMenu.Item[] mediaFilterItems = new DropdownMenu.Item[3 + SocialRegion.Codes.Length];
    private readonly DropdownMenu overflowMenu = new();
    private readonly DropdownMenu.Item[] overflowItems = new DropdownMenu.Item[1];
    private readonly MentionAutocomplete composeMentions;
    private readonly MentionAutocomplete commentMentions;
    private readonly EmojiComposer composeEmoji = new();
    private readonly EmojiComposer commentEmoji = new();
    private readonly AvatarLightbox avatarLightbox = new();
    private readonly Dictionary<SocialFeedScope, PullToRefresh> pullToRefresh = new()
    {
        { SocialFeedScope.ForYou, new() },
        { SocialFeedScope.Following, new() }
    };
    private readonly ViewRouter<ChirperRoute> router;
    private readonly RouterDraw<ChirperRoute> drawView;
    private readonly Action back;
    private readonly Action<NotificationDto> openActivityActor;
    private readonly Action<NotificationDto> openActivityPost;
    private readonly SocialActivityFeed activityFeed;
    private readonly Action loadOlderActivity;
    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private SocialFeedScope activeScope = SocialFeedScope.ForYou;
    private float tabSegmentAnim;
    private string draft = string.Empty;
    private bool composeFocus;
    private bool feedScrollTopPending;
    private readonly FailureSlot composeFailure = new();
    private readonly FailureSlot feedFailure = new();
    private readonly FailureSlot commentFailure = new();
    private string? commentRestore;
    private readonly CommentAttachment commentAttachment = new();
    private string? commentAttachmentRestore;
    private string composeStatus = string.Empty;
    private bool composeSensitive;
    private volatile int composeOutcome;
    private readonly ChirperActionReveal actions = new();
    private string commentDraft = string.Empty;
    private PostDto? quoteTarget;
    private string? quoteTargetId;
    private readonly HashSet<string> renderedUnderlyingIds = new(StringComparer.Ordinal);
    private readonly PhotoLibrary library;
    private readonly WallpaperImageCache wallpaperImages;
    private readonly PhotoCarousel carousel = new();
    private readonly PhotoViewerOverlay photoViewer = new();
    private readonly List<string> composeAttachments = new();
    private bool composePicking;
    private string[] composePickerPaths = Array.Empty<string>();
    private string? pendingComposePickedPath;
    private string? pendingSharedPhoto;
    private readonly FeedVirtualizer hashtagVirtualizer = new(400f);
    private string hashtagTitle = string.Empty;
    private string hashtagTitleTag = string.Empty;

    public ChirperApp(AethernetSession session, AethernetApi net, LodestoneService lodestone,
        RemoteImageCache images, PhotoLibrary library, SocialLauncher launcher, GameData gameData,
        Configuration configuration, SocialNotificationService social, WallpaperImageCache wallpaperImages,
        ConfirmService confirm, ReportService report, ConductGateService conduct, RealtimeSignalBus realtimeSignals)
    {
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
        loadOlderActivity = activityFeed.LoadOlder;
        avatar = new AvatarComposer(() => store.AvatarBusy, store.UpdateAvatar,
            new AvatarComposerLabels(L.Chirper.ChangePhoto, L.Chirper.ImportFromPc, L.Photos.NoPhotos,
                L.Chirper.MoveAndScale, L.Chirper.Use, L.Chirper.Saving, L.Chirper.GestureHint), library,
            wallpaperImages, confirm, () => store.AvatarFailure);
        router = new ViewRouter<ChirperRoute>(ChirperRoute.Home);
        drawView = DrawView;
        confirmService = confirm;
        back = () => router.Pop();
        openActivityActor = item => OpenProfile(item.ActorId);
        openActivityPost = item => OpenThreadFromLink(item.PostId!);
        profile = new SocialProfilePages(store, ui, new SocialProfileStyle
        {
            Palette = AppPalettes.Chirper,
            SearchInputId = "##chirperSearch",
            StatsPostsFirst = false,
            CountGrams = false,
            CardUserRows = true,
            HandleValidInk = AppPalettes.Chirper.TitleInk,
            EditProfile = L.Chirper.EditProfile,
            Follow = L.Chirper.Follow,
            Following = L.Chirper.Following,
            Posts = L.Chirper.Posts,
            Save = L.Chirper.Save,
            Saving = L.Chirper.Saving,
            HandleTaken = L.Chirper.HandleTaken,
            HandleRules = L.Chirper.HandleRules,
            HandleLabel = L.Chirper.HandleLabel,
            DisplayNameLabel = L.Chirper.DisplayNameLabel,
            BioLabel = L.Chirper.BioLabel,
            ChangePhoto = L.Chirper.ChangePhoto,
            ProfileError = L.Chirper.ProfileError,
            NameOrWorld = L.Chirper.NameOrWorld,
            SearchByName = L.Chirper.SearchByName,
            DeleteConfirmMessage = L.Chirper.DeleteConfirmMessage,
            DeleteConfirm = L.Chirper.DeleteConfirm,
            DeleteCancel = L.Chirper.DeleteCancel,
            DeleteFailed = L.Chirper.DeleteFailed,
            DeleteCommentConfirmMessage = L.Chirper.DeleteCommentConfirmMessage,
            DeleteCommentFailed = L.Chirper.DeleteCommentFailed,
            RemoveCommentConfirmMessage = L.Chirper.RemoveCommentConfirmMessage,
        }, images, lodestone, avatarLightbox, configuration, gameData, confirm, report,
            () => router.Push(ChirperRoute.EditProfile), OpenAvatarComposer, OpenProfile, OpenUserList, back,
            null);
    }

    public void OnOpened()
    {
        router.Reset();
        actions.Reset();
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
        router.Reset();
        avatarLightbox.Reset();
        draft = string.Empty;
        profile.SearchDraft = string.Empty;
        actions.Reset();
        commentDraft = string.Empty;
        composeAttachments.Clear();
        composePicking = false;
        composeSensitive = false;
        store.ClearDiscover();
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = theme;
        mediaFilterMenu.Gate();
        overflowMenu.Gate();
        actions.Tick(MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds));
        var screen = SceneChrome.ScreenFrom(context.Content, theme, UiScale.Current);
        ui.Backdrop(screen);
        ConsumeSharedPhoto();
        if (photoViewer.Active)
        {
            photoViewer.Draw(screen, theme);
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

        DrawMediaFilterMenu(screen);
        DrawOverflowMenu(screen);
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
                profile.DrawEditProfile(area, theme, navigation);
                break;
            case ChirperScreen.Avatar:
                DrawAvatarCompose(area);
                break;
            case ChirperScreen.Discover:
                DrawDiscover(area);
                break;
            case ChirperScreen.Thread:
                DrawThread(area, route.PostId!);
                break;
            case ChirperScreen.UserList:
                profile.DrawUserList(area, theme, navigation, route.UserId!, route.Kind);
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
        DrawHomeTopBar(area);
        var scale = UiScale.Current;
        var top = area.Min.Y + AppHeader.Height * scale;
        if (!store.IsSignedIn)
        {
            TourHolds.Hold(Id);
            var body = new Rect(new Vector2(area.Min.X, top), area.Max);
            Typography.DrawCentered(body.Center, Loc.T(L.Chirper.SetUpAccount), AppPalettes.Chirper.MutedInk);
            return;
        }

        TourHolds.Release(Id);
        var rowTop = top + 2f * scale;
        var rowRect = new Rect(new Vector2(area.Min.X, rowTop),
            new Vector2(area.Max.X, rowTop + FeedControlRow.Height * scale));
        var mediaOn = configuration.ChirperShowPhotoPosts && configuration.ChirperShowGifPosts
            && configuration.ChirperShowCommentMedia && configuration.ChirperFeedRegionMask == 0;
        var controls = FeedControlRow.Draw(rowRect, ui, Accent, Loc.T(L.Chirper.ForYou), Loc.T(L.Chirper.Following),
            (int)activeScope, ref tabSegmentAnim, store.IsLoading(activeScope), mediaOn, Loc.T(L.Common.Refresh),
            Loc.T(L.Chirper.FeedFilters), "chirper.tabs");
        if (controls.MediaToggled)
        {
            mediaFilterMenu.Toggle(MediaFilterMenuId, controls.MediaBounds);
        }

        if (controls.Refreshed)
        {
            RefreshActiveFeed();
        }

        if (controls.Selected != (int)activeScope)
        {
            activeScope = (SocialFeedScope)controls.Selected;
            actions.Reset();
            feedScrollTopPending = true;
            profile.EnsureLoaded(activeScope);
        }

        var listRect = new Rect(new Vector2(area.Min.X, rowRect.Max.Y + 6f * scale), area.Max);
        DrawFeedList(listRect, activeScope);
        if (ComposeFab.Draw(listRect, "##chirperComposeFab", Accent, FontAwesomeIcon.Feather.ToIconString(),
                Loc.T(L.Chirper.NewChirp), "chirper.compose"))
        {
            quoteTarget = null;
            quoteTargetId = null;
            composeAttachments.Clear();
            composePicking = false;
            composeSensitive = false;
            composeFocus = true;
            router.Push(ChirperRoute.Compose);
        }
    }

    private void DrawOverflowMenu(Rect screen)
    {
        if (!overflowMenu.IsOpenFor(OverflowMenuId))
        {
            return;
        }

        overflowItems[0] = new DropdownMenu.Item(Loc.T(L.Conduct.Eyebrow),
            FontAwesomeIcon.QuestionCircle.ToIconString());
        if (overflowMenu.Draw(screen, theme, overflowItems) == 0)
        {
            conduct.ShowRules(Id);
        }
    }

    private void DrawMediaFilterMenu(Rect screen)
    {
        if (!mediaFilterMenu.IsOpenFor(MediaFilterMenuId))
        {
            return;
        }

        mediaFilterItems[0] = new DropdownMenu.Item(Loc.T(L.Settings.ChirperShowPhotos),
            FontAwesomeIcon.Image.ToIconString(), Selected: configuration.ChirperShowPhotoPosts);
        mediaFilterItems[1] = new DropdownMenu.Item(Loc.T(L.Settings.ChirperShowGifs),
            FontAwesomeIcon.Film.ToIconString(), Selected: configuration.ChirperShowGifPosts);
        mediaFilterItems[2] = new DropdownMenu.Item(Loc.T(L.Settings.ChirperShowReplyMedia),
            FontAwesomeIcon.Comment.ToIconString(), Selected: configuration.ChirperShowCommentMedia);
        for (var regionIndex = 0; regionIndex < SocialRegion.Codes.Length; regionIndex++)
        {
            mediaFilterItems[3 + regionIndex] = new DropdownMenu.Item(SocialRegion.Codes[regionIndex],
                FontAwesomeIcon.Globe.ToIconString(),
                Selected: SocialRegion.MaskShows(configuration.ChirperFeedRegionMask, regionIndex));
        }

        mediaFilterMenu.Header = Loc.T(L.Chirper.FeedFilters);
        mediaFilterMenu.KeepOpen = true;
        var picked = mediaFilterMenu.Draw(screen, theme, mediaFilterItems);
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
            case >= 3:
                configuration.ChirperFeedRegionMask =
                    SocialRegion.ToggleMask(configuration.ChirperFeedRegionMask, picked - 3);
                store.SetFeedRegions(SocialRegion.FilterCsv(configuration.ChirperFeedRegionMask));
                break;
            default:
                return;
        }

        configuration.Save();
    }

    private void DrawActivity(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Social.ActivityTitle), back);
        var top = area.Min.Y + AppHeader.Height * UiScale.Current;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        activityFeed.EnsureFresh(social.Latest);
        SocialActivityList.Draw(body, ui, AppPalettes.Chirper, theme, activityFeed.Items, Id, images, lodestone,
            openActivityActor, openActivityPost, loadOlderActivity);
    }

    private void OpenActivity()
    {
        social.MarkSeen(Id);
        social.RefreshNow();
        activityFeed.Invalidate();
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
        using (var surface = AppSurface.Begin(listRect))
        {
            if (feedScrollTopPending)
            {
                surface.JumpToTop();
                feedScrollTopPending = false;
            }

            pullToRefresh[scope].Draw(listRect, surface.Pull, surface.Dragging,
                store.IsLoading(scope), AppPalettes.Chirper.MutedInk, () => RefreshFeed(scope));

            if (snapshot.Length == 0)
            {
                var failed = !store.IsLoading(scope) && store.FeedFailed(scope);
                if (failed)
                {
                    feedFailure.Set(store.FeedFailure(scope));
                }

                var message = store.IsLoading(scope) ? Loc.T(L.Common.Loading) :
                    failed ? feedFailure.Text() :
                    scope == SocialFeedScope.Following ? Loc.T(L.Chirper.FollowingEmpty) :
                    Loc.T(L.Chirper.ExploreEmpty);
                Typography.DrawCentered(new Vector2(listRect.Center.X, listRect.Min.Y + 90f * UiScale.Current),
                    message, AppPalettes.Chirper.MutedInk);
                if (failed)
                {
                    Typography.DrawCentered(
                        new Vector2(listRect.Center.X, listRect.Min.Y + 118f * UiScale.Current),
                        Loc.T(L.Failure.PullToRetry), AppPalettes.Chirper.MutedInk, TextStyles.Footnote);
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
                    InfiniteScroll.DrawLoadingRow(listRect.Center.X, AppPalettes.Chirper.MutedInk);
                }

                ImGui.Dummy(new Vector2(0f, 72f * UiScale.Current));
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
                DrawUnavailableCard();
            }

            return;
        }

        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = 14f * scale;
        var headerHeight = repostBy is not null ? 22f * scale : 0f;
        var contentTop = origin.Y + headerHeight;
        var radius = 20f * scale;
        var avatarCenter = new Vector2(origin.X + pad + radius, contentTop + pad + radius);
        var contentLeft = avatarCenter.X + radius + 12f * scale;
        var contentRight = origin.X + width - pad;
        var contentWidth = contentRight - contentLeft;
        var headerRight = contentRight - 24f * scale;
        var headerWidth = MathF.Max(1f, headerRight - contentLeft);
        var rawDisplayName = SocialIdentity.Name(post.AuthorDisplayName, post.AuthorHandle);
        var nameMaxWidth = headerWidth * 0.55f;
        var displayName = Typography.FitText(rawDisplayName, nameMaxWidth, 1.05f, FontWeight.SemiBold);
        var nameSize = Typography.Measure(displayName, 1.05f, FontWeight.SemiBold);
        var textTop = contentTop + pad + nameSize.Y + 6f * scale;
        RichTextLayout? bodyLayout = null;
        if (post.Text.Length > 0)
        {
            using (Plugin.Fonts.Push(1.05f))
            {
                bodyLayout = bodyLayouts.LayoutFor(post.Id, post.Text, post.Mentions, contentWidth);
            }
        }

        var textHeight = post.Text.Length == 0
            ? 0f
            : bodyLayout?.Size.Y ?? Typography.MeasureWrapped(post.Text, contentWidth, 1.05f);
        var photos = PostMedia.Photos(post.MediaUrls, post.MediaUrl);
        var mediaGap = photos.Length > 0 ? 8f * scale : 0f;
        var mediaHeight = photos.Length > 0
            ? PostAspects.DisplayHeight(contentWidth, post.MediaWidth, post.MediaHeight)
            : 0f;
        var mediaTop = textTop + textHeight + mediaGap;
        var hasQuote = post.QuotedPostId is not null;
        var quoteGap = hasQuote ? 8f * scale : 0f;
        var quoteHeight = hasQuote ? QuotedCardHeight(post.ReferencedPost, contentWidth) : 0f;
        var quoteTop = mediaTop + mediaHeight + quoteGap;
        var contentBody = hasQuote ? quoteTop + quoteHeight : mediaTop + mediaHeight;
        var contentBottom = MathF.Max(avatarCenter.Y + radius, contentBody);
        var reactionsTop = contentBottom + 8f * scale;
        var reactionsHeight = ReactionRowsHeight(post, contentWidth);
        var actionsTop = reactionsTop + reactionsHeight;
        var actionsHeight = 30f * scale;
        var pickerLeft = origin.X + pad;
        var pickerExtra = ReactionPickerExtraHeight(post, contentRight - pickerLeft);
        var cardBottom = actionsTop + actionsHeight + pickerExtra + pad * 0.5f;
        ui.Card(drawList, origin, new Vector2(origin.X + width, cardBottom), 18f * scale);
        if (repostBy is not null)
        {
            DrawRepostHeader(origin, contentLeft, headerHeight, width, repostBy);
        }

        DrawAvatar(drawList, avatarCenter, radius, SocialIdentity.Name(post.AuthorDisplayName, post.AuthorHandle),
            string.Empty, post.AuthorAvatarUrl, 0.95f, 48, Frames.Of(post.AuthorFrameId));
        if (UiInteract.HoverClick(avatarCenter - new Vector2(radius, radius), avatarCenter + new Vector2(radius, radius)))
        {
            OpenProfile(post.AuthorId);
        }

        var nameHovering = UiInteract.Hover(new Vector2(contentLeft, contentTop + pad),
            new Vector2(contentLeft + nameMaxWidth, contentTop + pad + nameSize.Y));
        var drawnNameWidth = UserName.Draw("chirper.post.author." + post.Id, rawDisplayName, post.AuthorBadges, post.AuthorBadgeIds,
            contentLeft, contentTop + pad, nameMaxWidth, new TextStyle(1.05f, FontWeight.SemiBold), theme.TextStrong,
            nameHovering, theme);
        var meta = SocialIdentity.FeedMeta(post.AuthorHandle, TimeText.Short(post.CreatedAtUnix));
        if (ContentModeration.IsInReview(post.ScanStatus))
        {
            meta = $"{meta} · {Loc.T(L.Moderation.InReview)}";
        }

        var metaLeft = contentLeft + drawnNameWidth + 7f * scale;
        var metaMaxWidth = MathF.Max(1f, headerRight - metaLeft);
        var metaSize = Typography.Measure(Typography.FitText(meta, metaMaxWidth, 0.95f, FontWeight.Regular), 0.95f);
        var metaTop = contentTop + pad + (nameSize.Y - metaSize.Y) * 0.5f;
        var metaHovering = UiInteract.Hover(new Vector2(metaLeft, metaTop),
            new Vector2(metaLeft + metaMaxWidth, metaTop + metaSize.Y));
        Marquee.DrawLeft("chirper.post.meta." + post.Id, meta, metaLeft, metaTop, metaMaxWidth,
            new TextStyle(0.95f, FontWeight.Regular), AppPalettes.Chirper.MutedInk, metaHovering);
        if (UiInteract.HoverClick(new Vector2(contentLeft, contentTop + pad),
                new Vector2(contentRight - 24f * scale, contentTop + pad + nameSize.Y)))
        {
            OpenProfile(post.AuthorId);
        }

        if (post.Text.Length > 0 && bodyLayout is null)
        {
            ImGui.SetCursorScreenPos(new Vector2(contentLeft, textTop));
            using (Typography.WrapAt(contentRight))
            using (Plugin.Fonts.Push(1.05f))
            using (ImRaii.PushColor(ImGuiCol.Text, AppPalettes.Chirper.BodyInk))
            {
                Typography.Wrapped(post.Text);
            }
        }
        else if (bodyLayout is not null)
        {
            using (Plugin.Fonts.Push(1.05f))
            {
                DrawRichBody(drawList, bodyLayout, new Vector2(contentLeft, textTop));
            }
        }

        if (photos.Length > 0)
        {
            DrawPostMedia(post, photos, new Rect(new Vector2(contentLeft, mediaTop),
                new Vector2(contentLeft + contentWidth, mediaTop + mediaHeight)));
        }

        if (hasQuote)
        {
            DrawQuotedCard(drawList, new Vector2(contentLeft, quoteTop), contentWidth, quoteHeight, post.ReferencedPost,
                true, post.Id);
        }

        DrawReactionRows(post, contentLeft, contentWidth, reactionsTop);
        DrawPostActions(post, contentLeft, contentWidth, pickerLeft, actionsTop + actionsHeight * 0.5f, isThreadHead);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cardBottom - origin.Y));
        ImGui.Dummy(new Vector2(0f, 10f * scale));
    }

    private void DrawPostActions(PostDto post, float left, float width, float pickerLeft, float centerY,
        bool isThreadHead)
    {
        if (actions.IsShowing(post.Id, ChirperActionReveal.Panel.Picker))
        {
            DrawReactionPicker(post, pickerLeft, left + width - pickerLeft, centerY);
        }
        else if (actions.IsShowing(post.Id, ChirperActionReveal.Panel.Repost))
        {
            DrawRepostMenu(post, left, centerY);
        }
        else if (actions.IsShowing(post.Id, ChirperActionReveal.Panel.Menu))
        {
            DrawOverflowMenuRow(post, left, width, centerY);
        }
        else
        {
            DrawDefaultActions(post, left, width, centerY, isThreadHead);
        }
    }

    private void DrawDefaultActions(PostDto post, float left, float width, float centerY, bool isThreadHead)
    {
        var scale = UiScale.Current;
        var hasCommentCount = post.CommentCount > 0;
        var commentCountText = hasCommentCount ? post.CommentCount.ToString(Loc.Culture) : string.Empty;
        var commentCountSize = hasCommentCount ? Typography.Measure(commentCountText, 0.95f, FontWeight.Medium) : Vector2.Zero;
        var hasRepostCount = post.RepostCount > 0;
        var repostCountText = hasRepostCount ? post.RepostCount.ToString(Loc.Culture) : string.Empty;
        var repostCountSize = hasRepostCount ? Typography.Measure(repostCountText, 0.95f, FontWeight.Medium) : Vector2.Zero;

        var commentCenterX = left + 8f * scale;
        var ellipsisCenterX = left + width - 8f * scale;
        var ceiling = ellipsisCenterX - 28f * scale;

        var gapUnits = 18f + 10f + 10f + 18f + 10f + 10f + (hasCommentCount ? 6f : 0f) + (hasRepostCount ? 6f : 0f);
        var fixedWidth = commentCountSize.X + repostCountSize.X;
        var available = MathF.Max(1f, ceiling - commentCenterX - fixedWidth);
        var t = MathF.Max(0.79f, MathF.Min(1f, available / (gapUnits * scale)));

        var commentCenter = new Vector2(commentCenterX, centerY);
        if (ui.IconButton(commentCenter, 15f * scale, FontAwesomeIcon.Comment.ToIconString(), AppPalettes.Chirper.MutedInk,
                new Vector4(0f, 0f, 0f, 0f), 1.15f, Loc.T(L.Chirper.Reply)) && !isThreadHead)
        {
            OpenThread(post);
        }

        var cursorX = commentCenter.X + 18f * scale * t;
        if (hasCommentCount)
        {
            Typography.Draw(new Vector2(cursorX, centerY - commentCountSize.Y * 0.5f), commentCountText,
                AppPalettes.Chirper.MutedInk, 0.95f, FontWeight.Medium);
            cursorX += commentCountSize.X + 6f * scale * t;
        }

        cursorX += 10f * scale * t;
        var repostColor = post.MyReposted ? theme.Accent : AppPalettes.Chirper.MutedInk;
        var repostCenter = new Vector2(cursorX + 10f * scale * t, centerY);
        if (ui.IconButton(repostCenter, 15f * scale, FontAwesomeIcon.Retweet.ToIconString(), repostColor,
                new Vector4(0f, 0f, 0f, 0f), 1.1f, Loc.T(post.MyReposted ? L.Chirper.Unrepost : L.Chirper.Repost)))
        {
            actions.Open(post.Id, ChirperActionReveal.Panel.Repost);
        }

        cursorX = repostCenter.X + 18f * scale * t;
        if (hasRepostCount)
        {
            Typography.Draw(new Vector2(cursorX, centerY - repostCountSize.Y * 0.5f), repostCountText, repostColor,
                0.95f, FontWeight.Medium);
            cursorX += repostCountSize.X + 6f * scale * t;
        }

        cursorX += 10f * scale * t;
        var ellipsisCenter = new Vector2(ellipsisCenterX, centerY);
        var triggerX = MathF.Min(cursorX + 10f * scale * t, ceiling);
        var triggerCenter = new Vector2(triggerX, centerY);
        if (ui.IconButton(triggerCenter, 15f * scale, FontAwesomeIcon.GrinBeam.ToIconString(), AppPalettes.Chirper.MutedInk,
                new Vector4(0f, 0f, 0f, 0f), 1.15f, Loc.T(L.Chirper.React)))
        {
            actions.Open(post.Id, ChirperActionReveal.Panel.Picker);
        }

        if (ui.IconButton(ellipsisCenter, 14f * scale, FontAwesomeIcon.EllipsisH.ToIconString(), AppPalettes.Chirper.BodyInk,
                new Vector4(0f, 0f, 0f, 0f), 1.05f, Loc.T(L.Chirper.More)))
        {
            actions.Open(post.Id, ChirperActionReveal.Panel.Menu);
        }
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

    private static int ReactionRowCount(PostDto post, float width)
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

    private static float ReactionRowsHeight(PostDto post, float width)
    {
        var rows = ReactionRowCount(post, width);
        if (rows == 0)
        {
            return 0f;
        }

        var scale = UiScale.Current;
        return rows * ReactionChipHeight * scale + (rows - 1) * ReactionChipGap * scale + ReactionRowGap * scale;
    }

    private void DrawReactionRows(PostDto post, float left, float width, float top)
    {
        Span<int> order = stackalloc int[ChirperReactions.Count];
        var active = CollectReactions(post, order);
        if (active == 0)
        {
            return;
        }

        var scale = UiScale.Current;
        var gap = ReactionChipGap * scale;
        var rowStride = (ReactionChipHeight + ReactionChipGap) * scale;
        var cursorX = 0f;
        var row = 0;
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
            DrawReactionChip(post, left + cursorX, centerY, kind);
            cursorX += chipWidth + gap;
        }
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

    private static float ReactionChipWidth(PostDto post, int kind)
    {
        var scale = UiScale.Current;
        var countText = ReactionTally.At(post.ReactionCounts, kind).ToString(Loc.Culture);
        var countSize = Typography.Measure(countText, 0.88f, FontWeight.Medium);
        var glyphWidth = ReactionEmojiWidth * scale;
        var padX = 8f * scale;
        var gap = 4f * scale;
        return padX + glyphWidth + gap + countSize.X + padX;
    }

    private void DrawReactionChip(PostDto post, float x, float centerY, int kind)
    {
        var scale = UiScale.Current;
        var color = ChirperReactions.Color(kind);
        var active = post.MyReaction == kind;
        var countText = ReactionTally.At(post.ReactionCounts, kind).ToString(Loc.Culture);
        var countSize = Typography.Measure(countText, 0.88f, FontWeight.Medium);
        var glyphWidth = ReactionEmojiWidth * scale;
        var padX = 8f * scale;
        var gap = 4f * scale;
        var chipWidth = ReactionChipWidth(post, kind);
        var chipHeight = ReactionChipHeight * scale;
        var min = new Vector2(x, centerY - chipHeight * 0.5f);
        var max = new Vector2(x + chipWidth, centerY + chipHeight * 0.5f);
        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(min, max);
        var background = active
            ? Palette.WithAlpha(color, 0.24f)
            : (hovered ? new Vector4(1f, 1f, 1f, 0.14f) : AppPalettes.Chirper.FieldSurface);
        Squircle.Fill(drawList, min, max, chipHeight * 0.5f, ImGui.GetColorU32(background));
        var emojiMin = new Vector2(min.X + padX, centerY - glyphWidth * 0.5f);
        EmojiImages.TryDraw(drawList, ChirperReactions.EmojiFile(kind), emojiMin,
            emojiMin + new Vector2(glyphWidth, glyphWidth), 0xFFFFFFFF);
        Typography.Draw(new Vector2(min.X + padX + glyphWidth + gap, centerY - countSize.Y * 0.5f), countText,
            active ? color : AppPalettes.Chirper.MutedInk, 0.88f, FontWeight.Medium);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(min, max, hovered))
        {
            store.ToggleReaction(post, kind);
        }
    }

    private static ReactionPickerLayout MeasureReactionPicker(float width)
    {
        var scale = UiScale.Current;
        var slots = ChirperReactions.Count + 1;
        var fitting = Math.Clamp((int)MathF.Floor(width / (ReactionSlotMin * scale)), 1, slots);
        var rows = (slots + fitting - 1) / fitting;
        var columns = (slots + rows - 1) / rows;
        var step = MathF.Min(ReactionSlotMax * scale, width / columns);
        return new ReactionPickerLayout(columns, rows, step, MathF.Min(15f * scale, step * 0.46f));
    }

    private float ReactionPickerExtraHeight(PostDto post, float width)
    {
        if (!actions.IsShowing(post.Id, ChirperActionReveal.Panel.Picker))
        {
            return 0f;
        }

        var layout = MeasureReactionPicker(width);
        return (layout.Rows - 1) * layout.Step * Math.Clamp(actions.Progress, 0f, 1f);
    }

    private void DrawReactionPicker(PostDto post, float left, float width, float centerY)
    {
        var scale = UiScale.Current;
        var layout = MeasureReactionPicker(width);
        var slots = ChirperReactions.Count + 1;
        var slide = Math.Clamp(actions.Progress, 0f, 1f);
        var interactive = !actions.Closing;
        for (var kind = 0; kind < ChirperReactions.Count; kind++)
        {
            var center = ReactionSlotCenter(layout, left, centerY, kind, slide);
            var color = ChirperReactions.Color(kind);
            var active = post.MyReaction == kind;
            var background = active ? Palette.WithAlpha(color, 0.22f) : AppPalettes.Chirper.FieldSurface;
            var reveal = ChirperActionReveal.Stagger(actions.Progress, kind, slots);
            if (DrawRevealEmoji(center, layout.IconRadius, ChirperReactions.EmojiFile(kind), background, reveal,
                    ChirperReactions.Label(kind), interactive))
            {
                store.ToggleReaction(post, kind);
                actions.Dismiss();
            }
        }

        var closeCenter = ReactionSlotCenter(layout, left, centerY, ChirperReactions.Count, slide);
        var closeReveal = ChirperActionReveal.Stagger(actions.Progress, ChirperReactions.Count, slots);
        if (DrawRevealIcon(closeCenter, layout.IconRadius, FontAwesomeIcon.Times.ToIconString(),
                AppPalettes.Chirper.MutedInk, AppPalettes.Chirper.FieldSurface, layout.IconRadius / (15f * scale),
                closeReveal, Loc.T(L.Common.Close), interactive))
        {
            actions.Dismiss();
        }
    }

    private static Vector2 ReactionSlotCenter(in ReactionPickerLayout layout, float left, float centerY, int slot,
        float slide)
    {
        var row = slot / layout.Columns;
        var column = slot % layout.Columns;
        return new Vector2(left + layout.IconRadius + column * layout.Step,
            centerY + row * layout.Step * slide);
    }

    private void DrawRepostMenu(PostDto post, float left, float centerY)
    {
        var scale = UiScale.Current;
        var step = 34f * scale;
        var iconRadius = 15f * scale;
        var interactive = !actions.Closing;
        const int count = 3;

        var reposted = post.MyReposted;
        var repostColor = reposted ? theme.Accent : AppPalettes.Chirper.MutedInk;
        var repostCenter = new Vector2(left + iconRadius, centerY);
        if (DrawRevealIcon(repostCenter, iconRadius, FontAwesomeIcon.Retweet.ToIconString(), repostColor,
                reposted ? Palette.WithAlpha(theme.Accent, 0.20f) : AppPalettes.Chirper.FieldSurface, 1.1f,
                ChirperActionReveal.Stagger(actions.Progress, 0, count),
                Loc.T(reposted ? L.Chirper.Unrepost : L.Chirper.Repost), interactive))
        {
            store.ToggleRepost(post);
            actions.Dismiss();
        }

        var quoteCenter = new Vector2(left + iconRadius + step, centerY);
        if (DrawRevealIcon(quoteCenter, iconRadius, FontAwesomeIcon.QuoteRight.ToIconString(), theme.TextStrong,
                AppPalettes.Chirper.FieldSurface, 0.95f, ChirperActionReveal.Stagger(actions.Progress, 1, count),
                Loc.T(L.Chirper.Quote), interactive))
        {
            BeginQuote(post);
            actions.Dismiss();
        }

        var closeCenter = new Vector2(left + iconRadius + step * 2f, centerY);
        if (DrawRevealIcon(closeCenter, iconRadius, FontAwesomeIcon.Times.ToIconString(), AppPalettes.Chirper.MutedInk,
                AppPalettes.Chirper.FieldSurface, 1f, ChirperActionReveal.Stagger(actions.Progress, 2, count),
                Loc.T(L.Common.Close), interactive))
        {
            actions.Dismiss();
        }
    }

    private void DrawRepostHeader(Vector2 origin, float contentLeft, float headerHeight, float width, PostDto repostBy)
    {
        var scale = UiScale.Current;
        var centerY = origin.Y + headerHeight * 0.5f + 2f * scale;
        AppSkin.Icon(new Vector2(contentLeft - 16f * scale, centerY), FontAwesomeIcon.Retweet.ToIconString(),
            AppPalettes.Chirper.MutedInk, 0.72f);
        var who = SocialIdentity.Name(repostBy.AuthorDisplayName, repostBy.AuthorHandle);
        var label = string.Format(Loc.Culture, Loc.T(L.Chirper.Reposted), who);
        var labelMaxWidth = MathF.Max(1f, origin.X + width - 14f * scale - contentLeft);
        label = Typography.FitText(label, labelMaxWidth, 0.82f, FontWeight.Medium);
        var labelSize = Typography.Measure(label, 0.82f, FontWeight.Medium);
        Typography.Draw(new Vector2(contentLeft, centerY - labelSize.Y * 0.5f), label, AppPalettes.Chirper.MutedInk,
            0.82f, FontWeight.Medium);
    }

    private void DrawUnavailableCard()
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = 14f * scale;
        var height = 44f * scale;
        ui.Card(drawList, origin, new Vector2(origin.X + width, origin.Y + height), 18f * scale);
        var unavailableText = Loc.T(L.Chirper.Unavailable);
        var unavailableMaxWidth = MathF.Max(1f, width - pad * 2f);
        var unavailableSize = Typography.Measure(unavailableText, 0.9f);
        var unavailableHovering = UiInteract.Hover(new Vector2(origin.X + pad, origin.Y + pad),
            new Vector2(origin.X + pad + unavailableMaxWidth, origin.Y + pad + unavailableSize.Y));
        Marquee.DrawLeft("chirper.card.unavailable", unavailableText, origin.X + pad, origin.Y + pad,
            unavailableMaxWidth, new TextStyle(0.9f, FontWeight.Regular), AppPalettes.Chirper.MutedInk,
            unavailableHovering);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
        ImGui.Dummy(new Vector2(0f, 10f * scale));
    }

    private const float QuoteThumbSize = 44f;

    private float QuotedCardHeight(PostDto? quoted, float width)
    {
        var scale = UiScale.Current;
        var innerPad = 10f * scale;
        var nameHeight = Typography.Measure("Ag", 0.85f, FontWeight.SemiBold).Y;
        if (quoted is null)
        {
            return innerPad + nameHeight + innerPad;
        }

        var quotedPhotos = PostMedia.Photos(quoted.MediaUrls, quoted.MediaUrl);
        var hasMedia = quotedPhotos.Length > 0 && !HiddenByMediaPreference(quotedPhotos);
        var innerWidth = width - innerPad * 2f - (hasMedia ? QuoteThumbSize * scale + 8f * scale : 0f);
        var textHeight = quoted.Text.Length > 0
            ? MathF.Min(Typography.MeasureWrapped(quoted.Text, innerWidth, 0.9f), nameHeight * 4f)
            : 0f;
        var gap = quoted.Text.Length > 0 ? 4f * scale : 0f;
        var height = innerPad + nameHeight + gap + textHeight + innerPad;
        return hasMedia ? MathF.Max(height, innerPad * 2f + QuoteThumbSize * scale) : height;
    }

    private void DrawQuotedCard(ImDrawListPtr drawList, Vector2 min, float width, float height, PostDto? quoted,
        bool tappable, string hostId)
    {
        var scale = UiScale.Current;
        var max = new Vector2(min.X + width, min.Y + height);
        var innerPad = 10f * scale;
        Squircle.Fill(drawList, min, max, 12f * scale, ImGui.GetColorU32(AppPalettes.Chirper.FieldSurface));
        if (quoted is null)
        {
            var unavailableText = Loc.T(L.Chirper.Unavailable);
            var unavailableMaxWidth = MathF.Max(1f, width - innerPad * 2f);
            var unavailableSize = Typography.Measure(unavailableText, 0.85f);
            var unavailableHovering = UiInteract.Hover(new Vector2(min.X + innerPad, min.Y + innerPad),
                new Vector2(min.X + innerPad + unavailableMaxWidth, min.Y + innerPad + unavailableSize.Y));
            Marquee.DrawLeft("chirper.quoted.unavailable", unavailableText, min.X + innerPad, min.Y + innerPad,
                unavailableMaxWidth, new TextStyle(0.85f, FontWeight.Regular), AppPalettes.Chirper.MutedInk,
                unavailableHovering);
            return;
        }

        var quotedPhotos = PostMedia.Photos(quoted.MediaUrls, quoted.MediaUrl);
        if (HiddenByMediaPreference(quotedPhotos))
        {
            quotedPhotos = Array.Empty<string>();
        }
        var thumbReserve = quotedPhotos.Length > 0 ? QuoteThumbSize * scale + 8f * scale : 0f;
        var innerWidth = width - innerPad * 2f - thumbReserve;
        var rawName = SocialIdentity.Name(quoted.AuthorDisplayName, quoted.AuthorHandle);
        var nameMaxWidth = innerWidth * 0.55f;
        var name = Typography.FitText(rawName, nameMaxWidth, 0.85f, FontWeight.SemiBold);
        var nameSize = Typography.Measure(name, 0.85f, FontWeight.SemiBold);
        var nameHovering = UiInteract.Hover(new Vector2(min.X + innerPad, min.Y + innerPad),
            new Vector2(min.X + innerPad + nameMaxWidth, min.Y + innerPad + nameSize.Y));
        var drawnNameWidth = UserName.Draw("chirper.quote.author." + hostId, rawName, quoted.AuthorBadges, quoted.AuthorBadgeIds,
            min.X + innerPad, min.Y + innerPad, nameMaxWidth, new TextStyle(0.85f, FontWeight.SemiBold),
            theme.TextStrong, nameHovering, theme);
        var meta = SocialIdentity.FeedMeta(quoted.AuthorHandle, TimeText.Short(quoted.CreatedAtUnix));
        var metaMaxWidth = MathF.Max(1f, innerWidth - drawnNameWidth - 6f * scale);
        var clippedMeta = Typography.FitText(meta, metaMaxWidth, 0.8f, FontWeight.Regular);
        var metaSize = Typography.Measure(clippedMeta, 0.8f);
        Typography.Draw(new Vector2(min.X + innerPad + drawnNameWidth + 6f * scale,
            min.Y + innerPad + (nameSize.Y - metaSize.Y) * 0.5f), clippedMeta, AppPalettes.Chirper.MutedInk, 0.8f);
        if (quoted.Text.Length > 0)
        {
            ImGui.PushClipRect(min, max, true);
            ImGui.SetCursorScreenPos(new Vector2(min.X + innerPad, min.Y + innerPad + nameSize.Y + 4f * scale));
            using (Typography.WrapAt(min.X + innerPad + innerWidth))
            using (Plugin.Fonts.Push(0.9f))
            using (ImRaii.PushColor(ImGuiCol.Text, AppPalettes.Chirper.BodyInk))
            {
                Typography.Wrapped(quoted.Text);
            }

            ImGui.PopClipRect();
        }

        if (quotedPhotos.Length > 0)
        {
            var thumbSize = QuoteThumbSize * scale;
            var thumbMin = new Vector2(max.X - innerPad - thumbSize, min.Y + (height - thumbSize) * 0.5f);
            var thumbMax = thumbMin + new Vector2(thumbSize, thumbSize);
            var thumbRounding = 8f * scale;
            var quoteVeiled = SensitiveReveals.ShouldVeil(quoted.Sensitive, quoted.Id, configuration.ShowSensitiveContent);
            if (quoteVeiled)
            {
                SensitiveVeil.Draw(drawList, thumbMin, thumbMax, thumbRounding);
            }
            else
            {
                var texture = MediaTexture(quotedPhotos[0]);
                if (texture is null)
                {
                    Squircle.Fill(drawList, thumbMin, thumbMax, thumbRounding, ImGui.GetColorU32(theme.SurfaceMuted));
                }
                else
                {
                    var (uv0, uv1) = ImageFit.CoverSquare(texture.Size);
                    drawList.AddImageRounded(texture.Handle, thumbMin, thumbMax, uv0, uv1, 0xFFFFFFFFu, thumbRounding,
                        ImDrawFlags.RoundCornersAll);
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

    private void DrawPostMedia(PostDto post, string[] photos, Rect rect)
    {
        var rounding = 12f * UiScale.Current;
        var scanStatus = post.ScanStatus;
        var veiled = SensitiveReveals.ShouldVeil(post.Sensitive, post.Id, configuration.ShowSensitiveContent);
        var result = carousel.Draw(ImGui.GetWindowDrawList(), rect, post.Id, photos, rounding,
            (pageList, pageMin, pageMax, pageRounding, pageUrl) =>
                DrawPostImage(pageList, new Rect(pageMin, pageMax), pageUrl, pageRounding, scanStatus, veiled));
        if (!result.Tapped || result.Index >= photos.Length)
        {
            return;
        }

        if (veiled)
        {
            SensitiveReveals.Reveal(post.Id);
            return;
        }

        var url = photos[result.Index];
        photoViewer.Open(this, () => MediaTexture(url));
    }

    private IDalamudTextureWrap? MediaTexture(string? url) => GifMedia.Texture(images, url, ImGui.GetTime());

    private void DrawPostImage(ImDrawListPtr drawList, Rect rect, string? url, float rounding, string? scanStatus,
        bool veiled = false)
    {
        if (veiled)
        {
            SensitiveVeil.Draw(drawList, rect.Min, rect.Max, rounding);
            return;
        }

        var texture = MediaTexture(url);
        if (texture is null)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(AppPalettes.Chirper.FieldSurface));
            Typography.DrawCentered(rect.Center,
                Loc.T(images.Failed(url) ? L.Common.ImageFailed : L.Common.Loading), AppPalettes.Chirper.MutedInk,
                0.85f);
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

    private void DrawOverflowMenuRow(PostDto post, float left, float width, float centerY)
    {
        var scale = UiScale.Current;
        var step = 34f * scale;
        var iconRadius = 15f * scale;
        var anchorX = left + width - 12f * scale;
        var interactive = !actions.Closing;
        var mine = store.Me is { } me && me.Id == post.AuthorId;
        var canVeil = mine && !post.SensitiveLocked
            && PostMedia.Photos(post.MediaUrls, post.MediaUrl).Length > 0;
        var count = mine ? (canVeil ? 3 : 2) : 4;
        var slot = 0;
        var closeCenter = new Vector2(anchorX - slot * step, centerY);
        if (DrawRevealIcon(closeCenter, iconRadius, FontAwesomeIcon.Times.ToIconString(), AppPalettes.Chirper.MutedInk,
                AppPalettes.Chirper.FieldSurface, 1f, ChirperActionReveal.Stagger(actions.Progress, slot, count),
                Loc.T(L.Common.Close), interactive))
        {
            actions.Dismiss();
        }

        slot++;
        if (mine)
        {
            if (canVeil)
            {
                var veilCenter = new Vector2(anchorX - slot * step, centerY);
                if (DrawRevealIcon(veilCenter, iconRadius, FontAwesomeIcon.EyeSlash.ToIconString(),
                        post.Sensitive ? Accent : AppPalettes.Chirper.MutedInk, AppPalettes.Chirper.FieldSurface, 0.95f,
                        ChirperActionReveal.Stagger(actions.Progress, slot, count),
                        Loc.T(post.Sensitive ? L.Moderation.SensitiveOn : L.Moderation.MarkSensitive), interactive))
                {
                    store.SetSensitive(post.Id, !post.Sensitive);
                    actions.Dismiss();
                }

                slot++;
            }

            var trashCenter = new Vector2(anchorX - slot * step, centerY);
            if (DrawRevealIcon(trashCenter, iconRadius, FontAwesomeIcon.Trash.ToIconString(), theme.Danger,
                    Palette.WithAlpha(theme.Danger, 0.16f), 0.95f,
                    ChirperActionReveal.Stagger(actions.Progress, slot, count), Loc.T(L.Chirper.DeleteConfirm),
                    interactive))
            {
                profile.AskDeletePost(post.Id);
                actions.Dismiss();
            }

            return;
        }

        var reportCenter = new Vector2(anchorX - slot * step, centerY);
        if (DrawRevealIcon(reportCenter, iconRadius, FontAwesomeIcon.Flag.ToIconString(), theme.Danger,
                Palette.WithAlpha(theme.Danger, 0.16f), 0.95f,
                ChirperActionReveal.Stagger(actions.Progress, slot, count), Loc.T(L.Report.Action), interactive))
        {
            profile.OpenReport("post", post.Id, Loc.T(L.Report.PostTitle));
            actions.Dismiss();
        }

        slot++;
        var followGlyph = post.IsFollowing
            ? FontAwesomeIcon.UserCheck.ToIconString()
            : FontAwesomeIcon.UserPlus.ToIconString();
        var followColor = post.IsFollowing ? theme.Accent : theme.TextStrong;
        var followTip = Loc.T(post.IsFollowing ? L.Chirper.Unfollow : L.Chirper.Follow);
        var followCenter = new Vector2(anchorX - slot * step, centerY);
        if (DrawRevealIcon(followCenter, iconRadius, followGlyph, followColor, AppPalettes.Chirper.FieldSurface, 1f,
                ChirperActionReveal.Stagger(actions.Progress, slot, count), followTip, interactive))
        {
            store.SetFollow(post.AuthorId, !post.IsFollowing);
            actions.Dismiss();
        }

        slot++;
        var blockCenter = new Vector2(anchorX - slot * step, centerY);
        if (DrawRevealIcon(blockCenter, iconRadius, FontAwesomeIcon.Ban.ToIconString(), theme.Danger,
                Palette.WithAlpha(theme.Danger, 0.16f), 0.95f,
                ChirperActionReveal.Stagger(actions.Progress, slot, count), Loc.T(L.Social.BlockAction), interactive))
        {
            profile.AskBlock(post.AuthorDisplayName, post.AuthorHandle, post.AuthorId);
            actions.Dismiss();
        }
    }

    private void DrawThread(Rect area, string postId)
    {
        var post = store.DetailPost;
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Chirper.PostTitle), back);
        var scale = UiScale.Current;
        var top = area.Min.Y + AppHeader.Height * scale;
        if (post is null || post.Id != postId)
        {
            if (post is null && !store.DetailLoading)
            {
                back();
                return;
            }

            Typography.DrawCentered(new Vector2(area.Center.X, top + 60f * scale), Loc.T(L.Common.Loading),
                AppPalettes.Chirper.MutedInk);
            return;
        }

        var composerHeight = 50f * scale;
        var body = new Rect(new Vector2(area.Min.X, top), new Vector2(area.Max.X, area.Max.Y - composerHeight));
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, FeedTopPadding * scale));
            DrawPost(post, true);
            if (post.TotalReactions > 0)
            {
                DrawLikersLink(post);
            }

            var comments = store.DetailComments;
            var commentTotal = store.HasMoreComments ? Math.Max(post.CommentCount, comments.Length) : comments.Length;
            ImGui.Dummy(new Vector2(0f, 2f * scale));
            ui.SectionHeading(commentTotal > 0
                ? $"{Loc.T(L.Chirper.RepliesTitle)} · {commentTotal}"
                : Loc.T(L.Chirper.RepliesTitle));
            if (comments.Length == 0)
            {
                if (!store.DetailLoading)
                {
                    Typography.Draw(
                        new Vector2(ImGui.GetCursorScreenPos().X + 2f * scale, ImGui.GetCursorScreenPos().Y),
                        Loc.T(L.Chirper.NoComments), AppPalettes.Chirper.MutedInk, 0.85f);
                }
            }
            else
            {
                DrawEarlierCommentsRow();
                for (var index = 0; index < comments.Length; index++)
                {
                    if (HiddenByMediaPreference(comments[index]))
                    {
                        continue;
                    }

                    DrawComment(comments[index]);
                }
            }

            ImGui.Dummy(new Vector2(0f, 24f * scale));
        }

        DrawCommentComposer(new Rect(new Vector2(area.Min.X, area.Max.Y - composerHeight), area.Max), area, postId);
    }

    private void DrawComment(CommentDto comment)
    {
        var scale = UiScale.Current;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var drawList = ImGui.GetWindowDrawList();
        var radius = 15f * scale;
        var avatarCenter = new Vector2(origin.X + radius, origin.Y + radius);
        DrawAvatar(drawList, avatarCenter, radius, SocialIdentity.Name(comment.AuthorDisplayName, comment.AuthorHandle),
            string.Empty, comment.AuthorAvatarUrl, 0.85f, 32, Frames.Of(comment.AuthorFrameId));
        if (UiInteract.HoverClick(avatarCenter - new Vector2(radius, radius), avatarCenter + new Vector2(radius, radius)))
        {
            OpenProfile(comment.AuthorId);
        }

        var textLeft = origin.X + radius * 2f + 10f * scale;
        var commentRight = origin.X + width - 30f * scale;
        var headerWidth = MathF.Max(1f, commentRight - textLeft);
        var rawDisplayName = SocialIdentity.Name(comment.AuthorDisplayName, comment.AuthorHandle);
        var nameMaxWidth = headerWidth * 0.55f;
        var displayName = Typography.FitText(rawDisplayName, nameMaxWidth, 0.95f, FontWeight.SemiBold);
        var nameSize = Typography.Measure(displayName, 0.95f, FontWeight.SemiBold);
        var nameHovering = UiInteract.Hover(new Vector2(textLeft, origin.Y),
            new Vector2(textLeft + nameMaxWidth, origin.Y + nameSize.Y));
        var drawnNameWidth = UserName.Draw("chirper.comment.author." + comment.Id, rawDisplayName,
            comment.AuthorBadges, comment.AuthorBadgeIds, textLeft, origin.Y, nameMaxWidth,
            new TextStyle(0.95f, FontWeight.SemiBold), theme.TextStrong, nameHovering, theme);
        var meta = comment.AuthorHandle.Length > 0
            ? $"@{comment.AuthorHandle} · {TimeText.Short(comment.CreatedAtUnix)}"
            : TimeText.Short(comment.CreatedAtUnix);
        var metaLeft = textLeft + drawnNameWidth + 7f * scale;
        var metaMaxWidth = MathF.Max(1f, commentRight - metaLeft - 34f * scale);
        var metaFullSize = Typography.Measure(meta, 0.85f);
        var metaY = origin.Y + (nameSize.Y - metaFullSize.Y) * 0.5f;
        var metaHovering = UiInteract.Hover(new Vector2(metaLeft, metaY),
            new Vector2(metaLeft + metaMaxWidth, metaY + metaFullSize.Y));
        var metaWidth = Marquee.DrawLeft("chirper.comment.meta." + comment.Id, meta, metaLeft, metaY, metaMaxWidth,
            new TextStyle(0.85f, FontWeight.Regular), AppPalettes.Chirper.MutedInk, metaHovering);
        CommentReviewTag.Draw(
            new Vector2(metaLeft + metaWidth + 7f * scale, metaY),
            commentRight, comment.ScanStatus, 0.85f);
        var bodyOrigin = new Vector2(textLeft, origin.Y + nameSize.Y + 6f * scale);
        ImGui.SetCursorScreenPos(bodyOrigin);
        var commentLayout = comment.Text.Length > 0
            ? commentLayouts.LayoutFor(comment.Id, comment.Text, comment.Mentions, commentRight - textLeft)
            : null;
        if (comment.Text.Length > 0)
        {
            if (commentLayout is null)
            {
                using (Typography.WrapAt(commentRight))
                using (ImRaii.PushColor(ImGuiCol.Text, AppPalettes.Chirper.BodyInk))
                {
                    Typography.Wrapped(comment.Text);
                }
            }
            else
            {
                DrawRichBody(drawList, commentLayout, bodyOrigin);
                ImGui.SetCursorScreenPos(bodyOrigin);
                ImGui.Dummy(commentLayout.Size);
            }
        }

        var textBottom = ImGui.GetCursorScreenPos().Y;
        if (comment.MediaUrl is { } commentMediaUrl && !CommentMediaHidden(commentMediaUrl))
        {
            var mediaTop = textBottom + (comment.Text.Length > 0 ? 6f * scale : 0f);
            var mediaRect = CommentMedia.Draw(drawList, images, comment, new Vector2(textLeft, mediaTop),
                commentRight - textLeft, scale, AppPalettes.Chirper.FieldSurface, AppPalettes.Chirper.MutedInk);
            if (UiInteract.HoverClick(mediaRect.Min, mediaRect.Max))
            {
                photoViewer.Open(this, () => MediaTexture(commentMediaUrl));
            }

            textBottom = mediaRect.Max.Y;
        }
        if (store.Me is { } me && store.DetailPost is { } post
            && (me.Id == comment.AuthorId || me.Id == post.AuthorId))
        {
            var mine = me.Id == comment.AuthorId;
            var trashCenter = new Vector2(origin.X + width - 10f * scale, origin.Y + 9f * scale);
            if (ui.IconButton(trashCenter, 12f * scale, FontAwesomeIcon.Times.ToIconString(), AppPalettes.Chirper.MutedInk,
                    new Vector4(0f, 0f, 0f, 0f), 0.85f, Loc.T(mine ? L.Chirper.DeleteComment : L.Chirper.RemoveComment)))
            {
                if (mine)
                {
                    profile.AskDeleteComment(post.Id, comment.Id);
                }
                else
                {
                    profile.AskRemoveComment(post.Id, comment.Id);
                }
            }
        }

        var heartCenter = new Vector2(origin.X + width - 12f * scale, origin.Y + nameSize.Y + 14f * scale);
        if (CommentHeart.Draw(ui, heartCenter, comment.Liked, comment.LikeCount, AppPalettes.Chirper.MutedInk,
                AppPalettes.Chirper.MutedInk, Loc.T(L.Chirper.ReactLike), out var heartBottom))
        {
            store.ToggleCommentLike(comment);
        }

        var bottom = MathF.Max(MathF.Max(textBottom, origin.Y + radius * 2f), heartBottom);
        ImGui.SetCursorScreenPos(new Vector2(origin.X, bottom));
        ImGui.Dummy(new Vector2(width, 12f * scale));
    }

    private void DrawCommentComposer(Rect bar, Rect screen, string postId)
    {
        var returned = Interlocked.Exchange(ref commentRestore, null);
        if (returned is not null)
        {
            commentDraft = returned;
        }

        var returnedAttachment = Interlocked.Exchange(ref commentAttachmentRestore, null);
        if (returnedAttachment is not null)
        {
            commentAttachment.Restore(returnedAttachment);
        }

        if (commentFailure.Failed)
        {
            Typography.DrawWrappedCentered(new Vector2(bar.Center.X,
                    bar.Min.Y - 22f * UiScale.Current - commentAttachment.StripHeight(UiScale.Current)),
                commentFailure.Text(), AppPalettes.Chirper.MutedInk, TextStyles.Footnote,
                bar.Width - 28f * UiScale.Current);
        }

        var style = new CommentComposerStyle(new Vector4(1f, 1f, 1f, 0.10f), AppPalettes.Chirper.FieldSurface,
            AppPalettes.Chirper.TitleInk, Accent, AppPalettes.Chirper.MutedInk, default, false, 8f, 56f, 0.95f);
        var focusPending = false;
        if (CommentComposerBar.Draw(bar, screen, ui, theme, style, "##chirperComment", Loc.T(L.Chirper.AddComment),
                ref commentDraft, MaxCommentLength, commentMentions, mentionPopup, images, lodestone, store.Commenting,
                ref focusPending, commentEmoji, commentAttachment, library, wallpaperImages))
        {
            var text = commentDraft;
            var attachmentPath = commentAttachment.Path;
            commentDraft = string.Empty;
            commentAttachment.Clear();
            commentFailure.Clear();
            store.AddComment(postId, text, attachmentPath, accepted =>
            {
                if (accepted)
                {
                    return;
                }

                commentRestore = text;
                commentAttachmentRestore = attachmentPath;
            }, commentFailure.Set);
        }
    }

    private bool DrawRevealIcon(Vector2 center, float hitRadius, string glyph, Vector4 color, Vector4 background,
        float glyphScale, float reveal, string tooltip, bool interactive)
    {
        var drawList = ImGui.GetWindowDrawList();
        var eased = Easing.EaseOutQuint(Math.Clamp(reveal, 0f, 1f));
        var alpha = Easing.SmoothStep(Math.Clamp(reveal / 0.6f, 0f, 1f));
        var hitMin = center - new Vector2(hitRadius, hitRadius);
        var hitMax = center + new Vector2(hitRadius, hitRadius);
        var hovered = interactive && UiInteract.Hover(hitMin, hitMax);
        if (background.W > 0f)
        {
            var fill = hovered ? Palette.Mix(background, theme.TextStrong, 0.08f) : background;
            drawList.AddCircleFilled(center, hitRadius * eased,
                ImGui.GetColorU32(Palette.WithAlpha(fill, fill.W * alpha)), 24);
        }

        var ink = hovered ? Palette.Mix(color, theme.TextStrong, 0.2f) : color;
        AppSkin.Icon(center, glyph, Palette.WithAlpha(ink, ink.W * alpha), glyphScale * eased);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (reveal > 0.6f)
        {
            HoverTooltip.Show(new Rect(hitMin, hitMax), tooltip, HoverLabelSide.Above);
        }

        return UiInteract.Click(hitMin, hitMax, hovered);
    }

    private bool DrawRevealEmoji(Vector2 center, float hitRadius, string emojiFile, Vector4 background, float reveal,
        string tooltip, bool interactive)
    {
        var drawList = ImGui.GetWindowDrawList();
        var eased = Easing.EaseOutQuint(Math.Clamp(reveal, 0f, 1f));
        var alpha = Easing.SmoothStep(Math.Clamp(reveal / 0.6f, 0f, 1f));
        var hitMin = center - new Vector2(hitRadius, hitRadius);
        var hitMax = center + new Vector2(hitRadius, hitRadius);
        var hovered = interactive && UiInteract.Hover(hitMin, hitMax);
        if (background.W > 0f)
        {
            var fill = hovered ? Palette.Mix(background, theme.TextStrong, 0.08f) : background;
            drawList.AddCircleFilled(center, hitRadius * eased,
                ImGui.GetColorU32(Palette.WithAlpha(fill, fill.W * alpha)), 24);
        }

        var half = hitRadius * ReactionEmojiFill * (hovered ? 1.08f : 1f) * eased;
        EmojiImages.TryDraw(drawList, emojiFile, center - new Vector2(half, half), center + new Vector2(half, half),
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha)));
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (reveal > 0.6f)
        {
            HoverTooltip.Show(new Rect(hitMin, hitMax), tooltip, HoverLabelSide.Above);
        }

        return UiInteract.Click(hitMin, hitMax, hovered);
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
        var ink = new RichTextInk(AppPalettes.Chirper.BodyInk, AppPalettes.Chirper.Accent, AppPalettes.Chirper.Accent);
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
            UrlActions.AskThenOpen(confirmService, layout.Urls[hit.TargetIndex]);
        }
    }

    private void DrawEarlierCommentsRow()
    {
        var scale = UiScale.Current;
        if (store.CommentsLoadingMore)
        {
            InfiniteScroll.DrawLoadingRow(
                ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X * 0.5f,
                AppPalettes.Chirper.MutedInk);
            return;
        }

        if (!store.HasMoreComments)
        {
            return;
        }

        var label = Loc.T(L.Chirper.EarlierComments);
        var origin = ImGui.GetCursorScreenPos();
        var pos = new Vector2(origin.X + 2f * scale, origin.Y);
        var size = Typography.Measure(label, 0.85f, FontWeight.Medium);
        var hovered = UiInteract.Hover(pos, pos + size);
        Typography.Draw(pos, label, hovered ? theme.Accent : AppPalettes.Chirper.MutedInk, 0.85f, FontWeight.Medium);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(pos, pos + size, hovered))
        {
            store.LoadMoreComments();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(size.X, size.Y + 12f * scale));
    }

    private void DrawLikersLink(PostDto post)
    {
        var scale = UiScale.Current;
        var label = Loc.Plural(L.Chirper.Likes, post.TotalReactions);
        var origin = ImGui.GetCursorScreenPos();
        var pad = 16f * scale;
        var pos = new Vector2(origin.X + pad, origin.Y);
        var size = Typography.Measure(label, 0.9f, FontWeight.Medium);
        var hovered = UiInteract.Hover(pos, pos + size);
        Typography.Draw(pos, label, hovered ? theme.Accent : AppPalettes.Chirper.MutedInk, 0.9f, FontWeight.Medium);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(pos, pos + size, hovered))
        {
            OpenUserList(post.Id, UserListKind.Likers);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(size.X + pad, size.Y + 6f * scale));
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
