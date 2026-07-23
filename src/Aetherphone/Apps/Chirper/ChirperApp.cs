using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
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

namespace Aetherphone.Apps.Chirper;

internal sealed partial class ChirperApp : IPhoneApp
{
    private const int MaxPostLength = 300;
    private const float FeedTopPadding = 8f;
    private const int MaxCommentLength = 500;
    public string Id => "chirper";
    public Vector4 Accent => AppAccents.For(Id);
    public string DisplayName => Loc.T(L.Apps.Chirper);
    public string Glyph => "Ch";
    public int BadgeCount => 0;
    private readonly ChirperStore store;
    private readonly SocialLauncher launcher;
    private readonly GameData gameData;
    private readonly Configuration configuration;
    private readonly LodestoneService lodestone;
    private readonly RemoteImageCache images;
    private readonly SocialNotificationService social;
    private readonly AvatarComposer avatar;
    private readonly SocialProfilePages profile;
    private readonly AppSkin ui = new(AppPalettes.Chirper);
    private readonly RichTextCache bodyLayouts = new();
    private readonly RichTextCache commentLayouts = new();
    private readonly FeedVirtualizer feedVirtualizer = new(300f);
    private readonly MentionPopup mentionPopup = new();
    private readonly MentionAutocomplete composeMentions;
    private readonly MentionAutocomplete commentMentions;
    private readonly EmojiComposer composeEmoji = new();
    private readonly EmojiComposer commentEmoji = new();
    private readonly AvatarLightbox avatarLightbox = new();
    private readonly ViewRouter<ChirperRoute> router;
    private readonly RouterDraw<ChirperRoute> drawView;
    private readonly Action back;
    private readonly Action<NotificationDto> openActivityActor;
    private readonly Action<NotificationDto> openActivityPost;
    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private SocialFeedScope activeScope = SocialFeedScope.ForYou;
    private float tabSegmentAnim;
    private string draft = string.Empty;
    private bool composeFocus;
    private bool feedScrollTopPending;
    private string composeStatus = string.Empty;
    private volatile int composeOutcome;
    private readonly ChirperActionReveal actions = new();
    private string commentDraft = string.Empty;
    private PostDto? quoteTarget;
    private string? quoteTargetId;
    private readonly HashSet<string> renderedUnderlyingIds = new(StringComparer.Ordinal);

    public ChirperApp(AethernetSession session, AethernetApi net, LodestoneService lodestone,
        RemoteImageCache images, PhotoLibrary library, SocialLauncher launcher, GameData gameData,
        Configuration configuration, SocialNotificationService social, WallpaperImageCache wallpaperImages,
        ConfirmService confirm, ReportService report)
    {
        store = new ChirperStore(session, net.Account, net.Social, net.Safety, net.Media);
        composeMentions = new MentionAutocomplete(store.NewMentionSuggestions());
        commentMentions = new MentionAutocomplete(store.NewMentionSuggestions());
        this.launcher = launcher;
        this.gameData = gameData;
        this.configuration = configuration;
        this.lodestone = lodestone;
        this.images = images;
        this.social = social;
        avatar = new AvatarComposer(() => store.AvatarBusy, store.UpdateAvatar,
            new AvatarComposerLabels(L.Chirper.ChangePhoto, L.Chirper.ImportFromPc, L.Photos.NoPhotos,
                L.Chirper.MoveAndScale, L.Chirper.Use, L.Chirper.Saving, L.Chirper.GestureHint), library,
            wallpaperImages);
        router = new ViewRouter<ChirperRoute>(ChirperRoute.Home);
        drawView = DrawView;
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
        }, images, lodestone, avatarLightbox, configuration, gameData, confirm, report,
            () => router.Push(ChirperRoute.EditProfile), OpenAvatarComposer, OpenProfile, OpenUserList, back);
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
            else
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
        store.ClearDiscover();
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = theme;
        actions.Tick(MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds));
        var screen = SceneChrome.ScreenFrom(context.Content, theme, ImGuiHelpers.GlobalScale);
        ui.Backdrop(screen);
        using (InputShield.Engage(avatarLightbox.Expanded))
        {
            router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
        }

        if (avatarLightbox.Active)
        {
            avatarLightbox.Draw(screen, theme);
        }
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
            default:
                DrawHome(area);
                break;
        }
    }

    private void DrawHome(Rect area)
    {
        DrawHomeTopBar(area);
        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        if (!store.IsSignedIn)
        {
            TourHolds.Hold(Id);
            var body = new Rect(new Vector2(area.Min.X, top), area.Max);
            Typography.DrawCentered(body.Center, Loc.T(L.Chirper.SetUpAccount), AppPalettes.Chirper.MutedInk);
            return;
        }

        TourHolds.Release(Id);
        var segmentHeight = 38f * scale;
        var tabsRect = new Rect(new Vector2(area.Min.X + 16f * scale, top + 2f * scale),
            new Vector2(area.Max.X - 16f * scale, top + 2f * scale + segmentHeight));
        UiAnchors.Report("chirper.tabs", tabsRect);
        var selected = SegmentSlider.Draw(tabsRect, Loc.T(L.Chirper.ForYou), Loc.T(L.Chirper.Following),
            (int)activeScope, ref tabSegmentAnim, Accent, AppPalettes.Chirper.MutedInk);
        if (selected != (int)activeScope)
        {
            activeScope = (SocialFeedScope)selected;
            actions.Reset();
            profile.EnsureLoaded(activeScope);
        }

        profile.Tick(ImGui.GetIO().DeltaTime);
        profile.TickRefresh(activeScope);
        var listRect = new Rect(new Vector2(area.Min.X, tabsRect.Max.Y + 6f * scale), area.Max);
        DrawFeedList(listRect, activeScope);
        if (ComposeFab.Draw(listRect, "##chirperComposeFab", Accent, FontAwesomeIcon.Feather.ToIconString(),
                Loc.T(L.Chirper.NewChirp), "chirper.compose"))
        {
            quoteTarget = null;
            quoteTargetId = null;
            composeFocus = true;
            router.Push(ChirperRoute.Compose);
        }
    }

    private void DrawActivity(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Social.ActivityTitle), back);
        var top = area.Min.Y + AppHeader.Height * ImGuiHelpers.GlobalScale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        SocialActivityList.Draw(body, ui, AppPalettes.Chirper, theme, social.Latest, Id, images, lodestone,
            openActivityActor, openActivityPost);
    }

    private void OpenActivity()
    {
        social.RefreshNow();
        social.MarkSeen(Id);
        router.Push(ChirperRoute.Activity);
    }

    private void DrawFeedList(Rect listRect, SocialFeedScope scope)
    {
        var snapshot = store.Feed(scope);
        using (AppSurface.Begin(listRect))
        {
            if (feedScrollTopPending)
            {
                ImGui.SetScrollY(0f);
                feedScrollTopPending = false;
            }

            if (snapshot.Length == 0)
            {
                var message = store.IsLoading(scope) ? Loc.T(L.Common.Loading) :
    scope == SocialFeedScope.Following ? Loc.T(L.Chirper.FollowingEmpty) :
                Loc.T(L.Chirper.ExploreEmpty);
                Typography.DrawCentered(new Vector2(listRect.Center.X, listRect.Min.Y + 90f * ImGuiHelpers.GlobalScale),
                    message, AppPalettes.Chirper.MutedInk);
            }
            else
            {
                ImGui.Dummy(new Vector2(0f, FeedTopPadding * ImGuiHelpers.GlobalScale));
                feedVirtualizer.BeginFrame();
                renderedUnderlyingIds.Clear();
                for (var index = 0; index < snapshot.Length; index++)
                {
                    var post = snapshot[index];
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

                ImGui.Dummy(new Vector2(0f, 72f * ImGuiHelpers.GlobalScale));
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

        var scale = ImGuiHelpers.GlobalScale;
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
        var displayName = SocialIdentity.Name(post.AuthorDisplayName, post.AuthorHandle);
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
        var hasQuote = post.QuotedPostId is not null;
        var quoteGap = hasQuote ? 8f * scale : 0f;
        var quoteHeight = hasQuote ? QuotedCardHeight(post.ReferencedPost, contentWidth) : 0f;
        var quoteTop = textTop + textHeight + quoteGap;
        var contentBody = hasQuote ? quoteTop + quoteHeight : textTop + textHeight;
        var contentBottom = MathF.Max(avatarCenter.Y + radius, contentBody);
        var actionsTop = contentBottom + 8f * scale;
        var actionsHeight = 30f * scale;
        var cardBottom = actionsTop + actionsHeight + pad * 0.5f;
        ui.Card(drawList, origin, new Vector2(origin.X + width, cardBottom), 18f * scale);
        if (repostBy is not null)
        {
            DrawRepostHeader(origin, contentLeft, headerHeight, repostBy);
        }

        DrawAvatar(drawList, avatarCenter, radius, post.AuthorName, post.AuthorWorld, post.AuthorAvatarUrl, 0.95f, 48);
        if (UiInteract.HoverClick(avatarCenter - new Vector2(radius, radius), avatarCenter + new Vector2(radius, radius)))
        {
            OpenProfile(post.AuthorId);
        }

        Typography.Draw(new Vector2(contentLeft, contentTop + pad), displayName, theme.TextStrong, 1.05f,
            FontWeight.SemiBold);
        var meta = SocialIdentity.FeedMeta(post.AuthorHandle, TimeText.Short(post.CreatedAtUnix));
        if (ContentModeration.IsInReview(post.ScanStatus))
        {
            meta = $"{meta} · {Loc.T(L.Moderation.InReview)}";
        }

        var metaSize = Typography.Measure(meta, 0.95f);
        Typography.Draw(
            new Vector2(contentLeft + nameSize.X + 7f * scale, contentTop + pad + (nameSize.Y - metaSize.Y) * 0.5f), meta,
            AppPalettes.Chirper.MutedInk, 0.95f);
        if (UiInteract.HoverClick(new Vector2(contentLeft, contentTop + pad),
                new Vector2(contentRight - 24f * scale, contentTop + pad + nameSize.Y)))
        {
            OpenProfile(post.AuthorId);
        }

        if (post.Text.Length > 0 && bodyLayout is null)
        {
            ImGui.SetCursorScreenPos(new Vector2(contentLeft, textTop));
            var wrapPos = contentRight - ImGui.GetWindowPos().X;
            ImGui.PushTextWrapPos(wrapPos);
            using (Plugin.Fonts.Push(1.05f))
            using (ImRaii.PushColor(ImGuiCol.Text, AppPalettes.Chirper.BodyInk))
            {
                Typography.Wrapped(post.Text);
            }

            ImGui.PopTextWrapPos();
        }
        else if (bodyLayout is not null)
        {
            using (Plugin.Fonts.Push(1.05f))
            {
                DrawRichBody(drawList, bodyLayout, new Vector2(contentLeft, textTop));
            }
        }

        if (hasQuote)
        {
            DrawQuotedCard(drawList, new Vector2(contentLeft, quoteTop), contentWidth, quoteHeight, post.ReferencedPost, true);
        }

        DrawPostActions(post, contentLeft, contentWidth, actionsTop + actionsHeight * 0.5f, isThreadHead);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cardBottom - origin.Y));
        ImGui.Dummy(new Vector2(0f, 10f * scale));
    }

    private void DrawPostActions(PostDto post, float left, float width, float centerY, bool isThreadHead)
    {
        if (actions.IsShowing(post.Id, ChirperActionReveal.Panel.Picker))
        {
            DrawReactionPicker(post, left, centerY);
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
        var scale = ImGuiHelpers.GlobalScale;
        var commentCenter = new Vector2(left + 12f * scale, centerY);
        if (ui.IconButton(commentCenter, 15f * scale, FontAwesomeIcon.Comment.ToIconString(), AppPalettes.Chirper.MutedInk,
                new Vector4(0f, 0f, 0f, 0f), 1.15f, Loc.T(L.Chirper.Reply)) && !isThreadHead)
        {
            OpenThread(post);
        }

        var cursorX = commentCenter.X + 22f * scale;
        if (post.CommentCount > 0)
        {
            var countText = post.CommentCount.ToString(Loc.Culture);
            var countSize = Typography.Measure(countText, 0.95f, FontWeight.Medium);
            Typography.Draw(new Vector2(cursorX, centerY - countSize.Y * 0.5f), countText, AppPalettes.Chirper.MutedInk,
                0.95f, FontWeight.Medium);
            cursorX += countSize.X + 6f * scale;
        }

        cursorX += 12f * scale;
        var repostColor = post.MyReposted ? theme.Accent : AppPalettes.Chirper.MutedInk;
        var repostCenter = new Vector2(cursorX + 12f * scale, centerY);
        if (ui.IconButton(repostCenter, 15f * scale, FontAwesomeIcon.Retweet.ToIconString(), repostColor,
                new Vector4(0f, 0f, 0f, 0f), 1.1f, Loc.T(post.MyReposted ? L.Chirper.Unrepost : L.Chirper.Repost)))
        {
            actions.Open(post.Id, ChirperActionReveal.Panel.Repost);
        }

        cursorX = repostCenter.X + 22f * scale;
        if (post.RepostCount > 0)
        {
            var repostText = post.RepostCount.ToString(Loc.Culture);
            var repostSize = Typography.Measure(repostText, 0.95f, FontWeight.Medium);
            Typography.Draw(new Vector2(cursorX, centerY - repostSize.Y * 0.5f), repostText, repostColor,
                0.95f, FontWeight.Medium);
            cursorX += repostSize.X + 6f * scale;
        }

        cursorX += 12f * scale;
        var triggerCenter = new Vector2(cursorX + 12f * scale, centerY);
        if (ui.IconButton(triggerCenter, 15f * scale, FontAwesomeIcon.GrinBeam.ToIconString(), AppPalettes.Chirper.MutedInk,
                new Vector4(0f, 0f, 0f, 0f), 1.15f, Loc.T(L.Chirper.React)))
        {
            actions.Open(post.Id, ChirperActionReveal.Panel.Picker);
        }

        var chipLimit = left + width - 36f * scale;
        DrawReactionSummary(post, triggerCenter.X + 22f * scale, centerY, chipLimit);

        var ellipsisCenter = new Vector2(left + width - 12f * scale, centerY);
        if (ui.IconButton(ellipsisCenter, 14f * scale, FontAwesomeIcon.EllipsisH.ToIconString(), AppPalettes.Chirper.BodyInk,
                new Vector4(0f, 0f, 0f, 0f), 1.05f, Loc.T(L.Chirper.More)))
        {
            actions.Open(post.Id, ChirperActionReveal.Panel.Menu);
        }
    }

    private void DrawReactionSummary(PostDto post, float startX, float centerY, float chipLimit)
    {
        Span<int> order = stackalloc int[ChirperReactions.Count];
        var active = 0;
        for (var kind = 0; kind < ChirperReactions.Count; kind++)
        {
            if (post.ReactionCounts[kind] > 0)
            {
                order[active++] = kind;
            }
        }

        OrderReactions(post, order[..active]);
        var cursorX = startX;
        for (var index = 0; index < active; index++)
        {
            var kind = order[index];
            if (cursorX + ReactionChipWidth(post, kind) > chipLimit)
            {
                break;
            }

            cursorX += DrawReactionChip(post, cursorX, centerY, kind);
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

        var candidateCount = post.ReactionCounts[candidate];
        var existingCount = post.ReactionCounts[existing];
        if (candidateCount != existingCount)
        {
            return candidateCount > existingCount;
        }

        return candidate < existing;
    }

    private static float ReactionChipWidth(PostDto post, int kind)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var countText = post.ReactionCounts[kind].ToString(Loc.Culture);
        var countSize = Typography.Measure(countText, 0.88f, FontWeight.Medium);
        var glyphWidth = 12f * scale;
        var padX = 8f * scale;
        var gap = 4f * scale;
        return padX + glyphWidth + gap + countSize.X + padX;
    }

    private float DrawReactionChip(PostDto post, float x, float centerY, int kind)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var color = ChirperReactions.Color(kind);
        var active = post.MyReaction == kind;
        var countText = post.ReactionCounts[kind].ToString(Loc.Culture);
        var countSize = Typography.Measure(countText, 0.88f, FontWeight.Medium);
        var glyphWidth = 12f * scale;
        var padX = 8f * scale;
        var gap = 4f * scale;
        var chipWidth = ReactionChipWidth(post, kind);
        var chipHeight = 24f * scale;
        var min = new Vector2(x, centerY - chipHeight * 0.5f);
        var max = new Vector2(x + chipWidth, centerY + chipHeight * 0.5f);
        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(min, max);
        var background = active
            ? Palette.WithAlpha(color, 0.24f)
            : (hovered ? new Vector4(1f, 1f, 1f, 0.14f) : AppPalettes.Chirper.FieldSurface);
        Squircle.Fill(drawList, min, max, chipHeight * 0.5f, ImGui.GetColorU32(background));
        AppSkin.Icon(new Vector2(min.X + padX + glyphWidth * 0.5f, centerY), ChirperReactions.Glyph(kind), color, 0.9f);
        Typography.Draw(new Vector2(min.X + padX + glyphWidth + gap, centerY - countSize.Y * 0.5f), countText,
            active ? color : AppPalettes.Chirper.MutedInk, 0.88f, FontWeight.Medium);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                store.ToggleReaction(post, kind);
            }
        }

        return chipWidth + 6f * scale;
    }

    private void DrawReactionPicker(PostDto post, float left, float centerY)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var step = 34f * scale;
        var iconRadius = 15f * scale;
        var count = ChirperReactions.Count + 1;
        var interactive = !actions.Closing;
        for (var kind = 0; kind < ChirperReactions.Count; kind++)
        {
            var center = new Vector2(left + iconRadius + kind * step, centerY);
            var color = ChirperReactions.Color(kind);
            var active = post.MyReaction == kind;
            var background = active ? Palette.WithAlpha(color, 0.22f) : AppPalettes.Chirper.FieldSurface;
            var reveal = ChirperActionReveal.Stagger(actions.Progress, kind, count);
            if (DrawRevealIcon(center, iconRadius, ChirperReactions.Glyph(kind), color, background, 1.1f, reveal,
                    ChirperReactions.Label(kind), interactive))
            {
                store.ToggleReaction(post, kind);
                actions.Dismiss();
            }
        }

        var closeCenter = new Vector2(left + iconRadius + ChirperReactions.Count * step, centerY);
        var closeReveal = ChirperActionReveal.Stagger(actions.Progress, ChirperReactions.Count, count);
        if (DrawRevealIcon(closeCenter, iconRadius, FontAwesomeIcon.Times.ToIconString(), AppPalettes.Chirper.MutedInk,
                AppPalettes.Chirper.FieldSurface, 1f, closeReveal, Loc.T(L.Common.Close), interactive))
        {
            actions.Dismiss();
        }
    }

    private void DrawRepostMenu(PostDto post, float left, float centerY)
    {
        var scale = ImGuiHelpers.GlobalScale;
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

    private void DrawRepostHeader(Vector2 origin, float contentLeft, float headerHeight, PostDto repostBy)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var centerY = origin.Y + headerHeight * 0.5f + 2f * scale;
        AppSkin.Icon(new Vector2(contentLeft - 16f * scale, centerY), FontAwesomeIcon.Retweet.ToIconString(),
            AppPalettes.Chirper.MutedInk, 0.72f);
        var who = SocialIdentity.Name(repostBy.AuthorDisplayName, repostBy.AuthorHandle);
        var label = string.Format(Loc.Culture, Loc.T(L.Chirper.Reposted), who);
        var labelSize = Typography.Measure(label, 0.82f, FontWeight.Medium);
        Typography.Draw(new Vector2(contentLeft, centerY - labelSize.Y * 0.5f), label, AppPalettes.Chirper.MutedInk,
            0.82f, FontWeight.Medium);
    }

    private void DrawUnavailableCard()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = 14f * scale;
        var height = 44f * scale;
        ui.Card(drawList, origin, new Vector2(origin.X + width, origin.Y + height), 18f * scale);
        Typography.Draw(new Vector2(origin.X + pad, origin.Y + pad), Loc.T(L.Chirper.Unavailable),
            AppPalettes.Chirper.MutedInk, 0.9f);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
        ImGui.Dummy(new Vector2(0f, 10f * scale));
    }

    private static float QuotedCardHeight(PostDto? quoted, float width)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var innerPad = 10f * scale;
        var nameHeight = Typography.Measure("Ag", 0.85f, FontWeight.SemiBold).Y;
        if (quoted is null)
        {
            return innerPad + nameHeight + innerPad;
        }

        var innerWidth = width - innerPad * 2f;
        var textHeight = quoted.Text.Length > 0
            ? MathF.Min(Typography.MeasureWrapped(quoted.Text, innerWidth, 0.9f), nameHeight * 4f)
            : 0f;
        var gap = quoted.Text.Length > 0 ? 4f * scale : 0f;
        return innerPad + nameHeight + gap + textHeight + innerPad;
    }

    private void DrawQuotedCard(ImDrawListPtr drawList, Vector2 min, float width, float height, PostDto? quoted,
        bool tappable)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var max = new Vector2(min.X + width, min.Y + height);
        var innerPad = 10f * scale;
        Squircle.Fill(drawList, min, max, 12f * scale, ImGui.GetColorU32(AppPalettes.Chirper.FieldSurface));
        if (quoted is null)
        {
            Typography.Draw(new Vector2(min.X + innerPad, min.Y + innerPad), Loc.T(L.Chirper.Unavailable),
                AppPalettes.Chirper.MutedInk, 0.85f);
            return;
        }

        var name = SocialIdentity.Name(quoted.AuthorDisplayName, quoted.AuthorHandle);
        var nameSize = Typography.Measure(name, 0.85f, FontWeight.SemiBold);
        Typography.Draw(new Vector2(min.X + innerPad, min.Y + innerPad), name, theme.TextStrong, 0.85f,
            FontWeight.SemiBold);
        var meta = SocialIdentity.FeedMeta(quoted.AuthorHandle, TimeText.Short(quoted.CreatedAtUnix));
        var metaSize = Typography.Measure(meta, 0.8f);
        Typography.Draw(new Vector2(min.X + innerPad + nameSize.X + 6f * scale,
            min.Y + innerPad + (nameSize.Y - metaSize.Y) * 0.5f), meta, AppPalettes.Chirper.MutedInk, 0.8f);
        if (quoted.Text.Length > 0)
        {
            var innerWidth = width - innerPad * 2f;
            ImGui.PushClipRect(min, max, true);
            ImGui.SetCursorScreenPos(new Vector2(min.X + innerPad, min.Y + innerPad + nameSize.Y + 4f * scale));
            ImGui.PushTextWrapPos(min.X + innerPad + innerWidth - ImGui.GetWindowPos().X);
            using (Plugin.Fonts.Push(0.9f))
            using (ImRaii.PushColor(ImGuiCol.Text, AppPalettes.Chirper.BodyInk))
            {
                Typography.Wrapped(quoted.Text);
            }

            ImGui.PopTextWrapPos();
            ImGui.PopClipRect();
        }

        if (tappable && UiInteract.HoverClick(min, max))
        {
            OpenThread(quoted);
        }
    }

    private void BeginQuote(PostDto post)
    {
        actions.Reset();
        var target = post.RepostOfId is not null && post.ReferencedPost is not null ? post.ReferencedPost : post;
        quoteTarget = target;
        quoteTargetId = target.Id;
        draft = string.Empty;
        composeStatus = string.Empty;
        composeFocus = true;
        router.Push(ChirperRoute.Compose);
    }

    private void DrawOverflowMenuRow(PostDto post, float left, float width, float centerY)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var step = 34f * scale;
        var iconRadius = 15f * scale;
        var anchorX = left + width - 12f * scale;
        var interactive = !actions.Closing;
        var mine = store.Me is { } me && me.Id == post.AuthorId;
        var count = mine ? 2 : 4;
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
        var scale = ImGuiHelpers.GlobalScale;
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
            ImGui.Dummy(new Vector2(0f, 2f * scale));
            ui.SectionHeading(comments.Length > 0
                ? $"{Loc.T(L.Chirper.RepliesTitle)} · {comments.Length}"
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
                for (var index = 0; index < comments.Length; index++)
                {
                    DrawComment(comments[index]);
                }
            }

            ImGui.Dummy(new Vector2(0f, 24f * scale));
        }

        DrawCommentComposer(new Rect(new Vector2(area.Min.X, area.Max.Y - composerHeight), area.Max), area, postId);
    }

    private void DrawComment(CommentDto comment)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var drawList = ImGui.GetWindowDrawList();
        var radius = 15f * scale;
        var avatarCenter = new Vector2(origin.X + radius, origin.Y + radius);
        DrawAvatar(drawList, avatarCenter, radius, comment.AuthorName, string.Empty, comment.AuthorAvatarUrl, 0.85f,
            32);
        if (UiInteract.HoverClick(avatarCenter - new Vector2(radius, radius), avatarCenter + new Vector2(radius, radius)))
        {
            OpenProfile(comment.AuthorId);
        }

        var textLeft = origin.X + radius * 2f + 10f * scale;
        var displayName = SocialIdentity.Name(comment.AuthorDisplayName, comment.AuthorHandle);
        var nameSize = Typography.Measure(displayName, 0.95f, FontWeight.SemiBold);
        Typography.Draw(new Vector2(textLeft, origin.Y), displayName, theme.TextStrong, 0.95f, FontWeight.SemiBold);
        var meta = comment.AuthorHandle.Length > 0
            ? $"@{comment.AuthorHandle} · {TimeText.Short(comment.CreatedAtUnix)}"
            : TimeText.Short(comment.CreatedAtUnix);
        var metaSize = Typography.Measure(meta, 0.85f);
        Typography.Draw(new Vector2(textLeft + nameSize.X + 7f * scale, origin.Y + (nameSize.Y - metaSize.Y) * 0.5f),
            meta, AppPalettes.Chirper.MutedInk, 0.85f);
        CommentReviewTag.Draw(
            new Vector2(textLeft + nameSize.X + 7f * scale + metaSize.X + 7f * scale,
                origin.Y + (nameSize.Y - metaSize.Y) * 0.5f),
            origin.X + width - 30f * scale, comment.ScanStatus, 0.85f);
        var bodyOrigin = new Vector2(textLeft, origin.Y + nameSize.Y + 6f * scale);
        ImGui.SetCursorScreenPos(bodyOrigin);
        var commentRight = origin.X + width - 30f * scale;
        var commentLayout = commentLayouts.LayoutFor(comment.Id, comment.Text, comment.Mentions, commentRight - textLeft);
        if (commentLayout is null)
        {
            ImGui.PushTextWrapPos(commentRight - ImGui.GetWindowPos().X);
            using (ImRaii.PushColor(ImGuiCol.Text, AppPalettes.Chirper.BodyInk))
            {
                Typography.Wrapped(comment.Text);
            }

            ImGui.PopTextWrapPos();
        }
        else
        {
            DrawRichBody(drawList, commentLayout, bodyOrigin);
            ImGui.SetCursorScreenPos(bodyOrigin);
            ImGui.Dummy(commentLayout.Size);
        }

        var textBottom = ImGui.GetCursorScreenPos().Y;
        if (store.Me is { } me && me.Id == comment.AuthorId && store.DetailPost is { } post)
        {
            var trashCenter = new Vector2(origin.X + width - 10f * scale, origin.Y + 9f * scale);
            if (ui.IconButton(trashCenter, 12f * scale, FontAwesomeIcon.Times.ToIconString(), AppPalettes.Chirper.MutedInk,
                    new Vector4(0f, 0f, 0f, 0f), 0.85f, Loc.T(L.Chirper.DeleteComment)))
            {
                profile.AskDeleteComment(post.Id, comment.Id);
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
        var style = new CommentComposerStyle(new Vector4(1f, 1f, 1f, 0.10f), AppPalettes.Chirper.FieldSurface,
            AppPalettes.Chirper.TitleInk, Accent, AppPalettes.Chirper.MutedInk, default, false, 8f, 56f, 0.95f);
        var focusPending = false;
        if (CommentComposerBar.Draw(bar, screen, ui, theme, style, "##chirperComment", Loc.T(L.Chirper.AddComment),
                ref commentDraft, MaxCommentLength, commentMentions, mentionPopup, images, lodestone, store.Commenting,
                ref focusPending, commentEmoji))
        {
            var text = commentDraft;
            commentDraft = string.Empty;
            store.AddComment(postId, text, _ => { });
        }
    }

    private bool DrawRevealIcon(Vector2 center, float hitRadius, string glyph, Vector4 color, Vector4 background,
        float glyphScale, float reveal, string tooltip, bool interactive)
    {
        var drawList = ImGui.GetWindowDrawList();
        var eased = Easing.EaseOutQuint(Math.Clamp(reveal, 0f, 1f));
        var alpha = Easing.SmoothStep(Math.Clamp(reveal / 0.6f, 0f, 1f));
        var hovered = interactive && UiInteract.Hover(center - new Vector2(hitRadius, hitRadius),
            center + new Vector2(hitRadius, hitRadius));
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
            HoverTooltip.Show(new Rect(center - new Vector2(hitRadius, hitRadius),
                center + new Vector2(hitRadius, hitRadius)), tooltip, HoverLabelSide.Above);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }


    private void DrawAvatar(ImDrawListPtr drawList, Vector2 center, float radius, string name, string world,
        string? avatarUrl, float monogramScale, int segments)
    {
        AvatarView.DrawRemote(drawList, center, radius, theme, name, world, avatarUrl, images, lodestone,
            monogramScale, segments);
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
    }

    private void DrawLikersLink(PostDto post)
    {
        var scale = ImGuiHelpers.GlobalScale;
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
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                OpenUserList(post.Id, UserListKind.Likers);
            }
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

    public void Dispose()
    {
        store.Dispose();
    }
}
