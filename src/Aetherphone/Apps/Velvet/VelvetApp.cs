using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Messaging;
using Aetherphone.Core.Net;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetApp : IPhoneApp
{
    private const int IntroMax = 400;
    private const int ShortFieldMax = 40;
    private const int VibeMax = 140;
    private const int TagsMax = 200;
    private const int MessageMax = 1000;
    private const int HandleMax = 15;
    private const float HeartbeatSeconds = 45f;
    private const float ThreadPollSeconds = 2.5f;
    private const float TypingSendSeconds = 3f;

    private static readonly string[] VibeSuggestions =
    {
        "soft", "dom", "sub", "switch", "service", "brat", "gentle", "playful",
        "sensual", "primal", "romantic", "nurturing", "experimental", "curious"
    };

    private static readonly string[] TagSuggestions =
    {
        "gpose", "romance", "cuddles", "roleplay", "teasing", "praise", "lingerie", "bondage",
        "aftercare", "worship", "sensory", "slowburn", "fluff", "flirty", "latenight", "storytelling"
    };

    private static readonly string[] LimitSuggestions =
    {
        "no irl details", "no pain", "no degradation", "no humiliation",
        "no gore", "no scat", "no permanent marks", "no public scenes"
    };

    public string Id => "velvet";
    public Vector4 Accent => AppAccents.For(Id);
    public string DisplayName => Loc.T(L.Apps.Velvet);
    public string Glyph => "Ve";
    public int BadgeCount => store.UnreadCount + store.RequestCount;
    private readonly VelvetStore store;
    private readonly VelvetLauncher launcher;
    private readonly SocialLauncher socialLauncher;
    private readonly LodestoneService lodestone;
    private readonly Configuration configuration;
    private readonly GameData gameData;
    private readonly PhotoLibrary library;
    private readonly AppSkin ui = new(AppPalettes.Velvet);
    private readonly VelvetAvatarComposer avatar;
    private readonly VelvetPostComposer post;
    private readonly VelvetReportControl report;
    private readonly RemoteImageCache images;
    private readonly HttpService http;
    private readonly ViewRouter<VelvetRoute> router;
    private readonly RouterDraw<VelvetRoute> drawView;
    private readonly Action back;
    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private VelvetTab activeTab = VelvetTab.Hub;
    private VelvetHubView hubView = VelvetHubView.Timeline;
    private float hubSegmentAnim;
    private string discoverQuery = string.Empty;
    private string discoverApplied = string.Empty;
    private float discoverDebounce;
    private bool gateBusy;
    private int onboardStep;
    private float sinceHeartbeat = HeartbeatSeconds;
    private int lookingForFilter = VelvetLookingFor.Any;
    private float lookingForScrollX;
    private bool lookingForDragging;
    private float lookingForDragStartMouseX;
    private float lookingForDragStartScrollX;
    private bool lookingForTrackDragging;
    private string editDisplayName = string.Empty;
    private string editHandle = string.Empty;
    private string editIntro = string.Empty;
    private string editPronouns = string.Empty;
    private string editVibe = string.Empty;
    private string editVibeAdd = string.Empty;
    private string editTags = string.Empty;
    private string editTagsAdd = string.Empty;
    private string editLimits = string.Empty;
    private string editLimitsAdd = string.Empty;
    private string? pendingTokenFocus;
    private int editLookingFor = VelvetLookingFor.Sharing;
    private int editRelationship = VelvetRelationship.NotSaying;
    private bool editDiscoverable = true;
    private string? editLoadedFor;
    private volatile int editOutcome;
    private volatile bool editBusy;
    private string messageDraft = string.Empty;
    private bool threadFocus;
    private string[] chatPickerPaths = Array.Empty<string>();
    private string? chatPickerThreadId;
    private string? chatPendingPickedPath;
    private float sinceThreadPoll;
    private float sinceTypingSend = TypingSendSeconds;
    private string lastTypingDraft = string.Empty;
    private readonly List<BubbleEntrance> threadEntrances = new();
    private string? entranceThreadId;
    private int entranceSettled;
    private bool entrancePrimed;
    private string? followThreadId;
    private bool followThreadBottom;
    private bool snapThreadToBottom;
    private float typingReveal;
    private float typingPhase;
    private string? imageViewId;
    private volatile int imageSaveOutcome;
    private volatile bool imageSaveBusy;
    private string commentsPostId = string.Empty;
    private string commentDraft = string.Empty;
    private bool sentExpanded;

    public VelvetApp(AethernetSession session, AethernetClient client, LodestoneService lodestone,
        Configuration configuration, PhotoLibrary library, HttpService http, NotificationService notifications,
        VelvetLauncher launcher, SocialLauncher socialLauncher, GameData gameData)
    {
        store = new VelvetStore(session, client, notifications, configuration);
        this.launcher = launcher;
        this.socialLauncher = socialLauncher;
        this.lodestone = lodestone;
        this.configuration = configuration;
        this.gameData = gameData;
        this.library = library;
        avatar = new VelvetAvatarComposer(store, library);
        post = new VelvetPostComposer(store, library);
        report = new VelvetReportControl(store);
        images = new RemoteImageCache(http);
        this.http = http;
        router = new ViewRouter<VelvetRoute>(VelvetRoute.Root, Id);
        drawView = DrawView;
        back = () => router.Pop();
    }

    public void OnOpened()
    {
        router.Reset();
        activeTab = VelvetTab.Hub;
        hubView = VelvetHubView.Timeline;
        hubSegmentAnim = 0f;
        discoverQuery = string.Empty;
        discoverApplied = string.Empty;
        discoverDebounce = 0f;
        onboardStep = 0;
        store.InvalidateLists();
        if (GateAccepted && store.IsSignedIn)
        {
            store.EnsureMe();
        }

        if (launcher.TryConsume(out var targetUserId) && GateAccepted && configuration.VelvetOnboarded &&
            store.IsSignedIn)
        {
            activeTab = VelvetTab.Messages;
            OpenThreadWith(targetUserId);
        }

        if (socialLauncher.TryConsume(Id, out var link) && GateAccepted && configuration.VelvetOnboarded &&
            store.IsSignedIn)
        {
            if (link.Kind == SocialLinkKind.Profile)
            {
                OpenProfile(link.Id);
            }
            else
            {
                store.EnsurePost(link.Id);
                router.Push(VelvetRoute.PostDetail(link.Id));
            }
        }
    }

    public void OnClosed()
    {
        router.Reset();
        messageDraft = string.Empty;
        discoverQuery = string.Empty;
        discoverApplied = string.Empty;
        report.Reset();
        store.ClearDiscover();
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = theme;
        if (!store.IsSignedIn)
        {
            DrawFullScreenMessage(context.Content, Loc.T(L.Velvet.SetUpAccount));
            return;
        }

        if (!GateAccepted)
        {
            DrawGate(context.Content);
            return;
        }

        if (!configuration.VelvetOnboarded)
        {
            DrawOnboarding(context.Content);
            return;
        }

        store.EnsureMe();
        TickHeartbeat();
        var screen = SceneChrome.ScreenFrom(context.Content, theme, ImGuiHelpers.GlobalScale);
        ui.Backdrop(screen);
        router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
    }

    private bool GateAccepted =>
        configuration.VelvetAcknowledgedGate &&
        configuration.VelvetAcknowledgedGateVersion >= Configuration.VelvetGateVersion;

    private void DrawView(VelvetRoute route, Rect area, int depth)
    {
        ui.Body(area);
        switch (route.Screen)
        {
            case VelvetScreen.Profile:
                DrawProfile(area, route.Id!);
                break;
            case VelvetScreen.EditProfile:
                DrawEditProfile(area);
                break;
            case VelvetScreen.Settings:
                DrawSettings(area);
                break;
            case VelvetScreen.Thread:
                DrawThread(area, route.Id!);
                break;
            case VelvetScreen.ChatImage:
                DrawChatImagePicker(area, route.Id!);
                break;
            case VelvetScreen.ImageView:
                DrawImageViewer(area, route.Id!);
                break;
            case VelvetScreen.Avatar:
                DrawAvatar(area);
                break;
            case VelvetScreen.Compose:
                if (post.Draw(area, ui, new PhoneContext(area, theme, navigation)))
                {
                    router.Pop();
                    store.RefreshFeed();
                }

                break;
            case VelvetScreen.PostDetail:
                DrawPostDetail(area, route.Id!);
                break;
            case VelvetScreen.Likers:
                DrawLikers(area, route.Id!);
                break;
            default:
                DrawRoot(area);
                break;
        }
    }

    private void DrawRoot(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var headerHeight = 42f * scale;
        var navHeight = 60f * scale;
        var headerRect = new Rect(area.Min, new Vector2(area.Max.X, area.Min.Y + headerHeight));
        var navRect = new Rect(new Vector2(area.Min.X, area.Max.Y - navHeight), area.Max);
        var contentArea = new Rect(new Vector2(area.Min.X, headerRect.Max.Y), new Vector2(area.Max.X, navRect.Min.Y));
        DrawRootHeader(headerRect);
        switch (activeTab)
        {
            case VelvetTab.Messages:
                DrawMessages(contentArea);
                break;
            case VelvetTab.Me:
                DrawMe(contentArea);
                break;
            default:
                DrawHub(contentArea);
                break;
        }

        DrawBottomNav(navRect);
    }

    private void DrawRootHeader(Rect area)
    {
        var title = activeTab switch
        {
            VelvetTab.Messages => Loc.T(L.Velvet.Messages),
            _ => Loc.T(L.Apps.Velvet),
        };
        Typography.DrawCentered(new Vector2(area.Center.X, area.Center.Y), title, AppPalettes.Velvet.TitleInk, 1.2f,
            FontWeight.SemiBold);
    }

    private void DrawBottomNav(Rect nav)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddLine(nav.Min, new Vector2(nav.Max.X, nav.Min.Y), ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)),
            1f);
        var width = nav.Width / 3f;
        DrawNavItem(new Rect(nav.Min, new Vector2(nav.Min.X + width, nav.Max.Y)), FontAwesomeIcon.Compass,
            Loc.T(L.Velvet.TabHub), VelvetTab.Hub, 0);
        DrawNavItem(new Rect(new Vector2(nav.Min.X + width, nav.Min.Y), new Vector2(nav.Min.X + width * 2f, nav.Max.Y)),
            FontAwesomeIcon.Comment, Loc.T(L.Velvet.Messages), VelvetTab.Messages,
            store.UnreadCount + store.RequestCount);
        DrawNavItem(new Rect(new Vector2(nav.Min.X + width * 2f, nav.Min.Y), nav.Max), FontAwesomeIcon.User,
            Loc.T(L.Velvet.TabMe), VelvetTab.Me, 0);
    }

    private void DrawNavItem(Rect rect, FontAwesomeIcon icon, string label, VelvetTab tab, int badge)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var active = activeTab == tab;
        var color = active ? new Vector4(0.99f, 0.72f, 0.82f, 1f) : AppPalettes.Velvet.MutedInk;
        var iconCenter = new Vector2(rect.Center.X, rect.Min.Y + 20f * scale);
        AppSkin.Icon(iconCenter, icon.ToIconString(), color, 1.05f);
        Typography.DrawCentered(new Vector2(rect.Center.X, rect.Min.Y + 42f * scale), label, color, 0.72f,
            active ? FontWeight.SemiBold : FontWeight.Regular);
        if (badge > 0)
        {
            var badgeCenter = new Vector2(iconCenter.X + 12f * scale, iconCenter.Y - 9f * scale);
            ImGui.GetWindowDrawList().AddCircleFilled(badgeCenter, 7f * scale, ImGui.GetColorU32(theme.Danger), 16);
            Typography.DrawCentered(badgeCenter, badge > 9 ? "9+" : badge.ToString(Loc.Culture),
                new Vector4(1f, 1f, 1f, 1f), 0.62f, FontWeight.SemiBold);
        }

        if (UiInteract.HoverClick(rect.Min, rect.Max))
        {
            activeTab = tab;
        }
    }

    private void DrawHub(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        if (!store.FeedLoaded && !store.LoadingFeed)
        {
            store.RefreshFeed();
        }

        if (!store.DiscoverLoaded && !store.LoadingDiscover)
        {
            store.RefreshDiscover(lookingForFilter, discoverApplied);
        }

        var segmentHeight = 38f * scale;
        var segmentRect = new Rect(new Vector2(area.Min.X + 16f * scale, area.Min.Y + 8f * scale),
            new Vector2(area.Max.X - 16f * scale, area.Min.Y + 8f * scale + segmentHeight));
        DrawHubSegments(segmentRect);
        var bodyRect = new Rect(new Vector2(area.Min.X, segmentRect.Max.Y + 8f * scale), area.Max);
        if (hubView == VelvetHubView.Discover)
        {
            DrawDiscover(bodyRect);
        }
        else
        {
            DrawTimeline(bodyRect);
            DrawComposeFab(area);
        }
    }

    private void DrawHubSegments(Rect rect)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var radius = rect.Height * 0.5f;
        Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.08f)));
        var target = hubView == VelvetHubView.Discover ? 1f : 0f;
        hubSegmentAnim += (target - hubSegmentAnim) * MathF.Min(1f, ImGui.GetIO().DeltaTime * 14f);
        var half = rect.Width * 0.5f;
        var pad = 3f * scale;
        var thumbMinX = rect.Min.X + pad + hubSegmentAnim * half;
        var thumbMin = new Vector2(thumbMinX, rect.Min.Y + pad);
        var thumbMax = new Vector2(thumbMinX + half - pad * 2f, rect.Max.Y - pad);
        Squircle.Fill(drawList, thumbMin, thumbMax, (thumbMax.Y - thumbMin.Y) * 0.5f, ImGui.GetColorU32(Accent));
        var timelineRect = new Rect(rect.Min, new Vector2(rect.Min.X + half, rect.Max.Y));
        var discoverRect = new Rect(new Vector2(rect.Min.X + half, rect.Min.Y), rect.Max);
        DrawSegmentLabel(timelineRect, Loc.T(L.Velvet.TabTimeline), hubView == VelvetHubView.Timeline);
        DrawSegmentLabel(discoverRect, Loc.T(L.Velvet.TabDiscover), hubView == VelvetHubView.Discover);
        if (UiInteract.HoverClick(timelineRect.Min, timelineRect.Max))
        {
            hubView = VelvetHubView.Timeline;
        }

        if (UiInteract.HoverClick(discoverRect.Min, discoverRect.Max))
        {
            hubView = VelvetHubView.Discover;
        }
    }

    private static void DrawSegmentLabel(Rect rect, string label, bool active)
    {
        var ink = active ? new Vector4(1f, 1f, 1f, 1f) : AppPalettes.Velvet.MutedInk;
        Typography.DrawCentered(rect.Center, label, ink, 0.9f, active ? FontWeight.SemiBold : FontWeight.Medium);
    }

    private void DrawTimeline(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var feed = store.Feed;
        if (feed.Length == 0)
        {
            DrawHubEmpty(area, FontAwesomeIcon.Camera,
                Loc.T(store.LoadingFeed ? L.Common.Loading : L.Velvet.FeedEmpty));
            return;
        }

        using (AppSurface.Begin(area))
        {
            ImGui.Dummy(new Vector2(0f, 4f * scale));
            for (var index = 0; index < feed.Length; index++)
            {
                DrawPostCard(feed[index]);
            }

            ImGui.Dummy(new Vector2(0f, 96f * scale));
        }
    }

    private void DrawPostCard(VelvetPostDto post)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = 14f * scale;
        var innerX = origin.X + pad;
        var innerWidth = width - pad * 2f;
        var headerBlock = 40f * scale;
        var avatarRadius = 18f * scale;
        var imageTop = origin.Y + pad + headerBlock + 10f * scale;
        var imageSize = innerWidth;
        var imageBottom = imageTop + imageSize;
        var actionsTop = imageBottom + 12f * scale;
        var actionsHeight = 20f * scale;
        var textTop = actionsTop + actionsHeight + 12f * scale;
        var captionHeight = post.Caption.Length > 0 ? MeasureWrapped(post.Caption, innerWidth, 0.9f) + 8f * scale : 0f;
        var tagsHeight = post.Tags.Length > 0 ? 24f * scale : 0f;
        var cardBottom = textTop + captionHeight + tagsHeight + pad;
        var cardHeight = cardBottom - origin.Y;
        Squircle.Fill(drawList, origin, new Vector2(origin.X + width, cardBottom), 18f * scale,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.05f)));
        Squircle.Stroke(drawList, origin, new Vector2(origin.X + width, cardBottom), 18f * scale,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.06f)), 1f);
        var avatarCenter = new Vector2(innerX + avatarRadius, origin.Y + pad + avatarRadius);
        AvatarView.Draw(drawList, avatarCenter, avatarRadius, Accent, Monogram(post.OwnerDisplayName, post.OwnerHandle),
            1f, lodestone.Remote(post.OwnerId, ToUri(post.OwnerAvatarUrl)), 40);
        var nameLeft = avatarCenter.X + avatarRadius + 10f * scale;
        var displayName = string.IsNullOrEmpty(post.OwnerDisplayName) ? post.OwnerHandle : post.OwnerDisplayName;
        Typography.Draw(new Vector2(nameLeft, origin.Y + pad + 2f * scale), displayName, theme.TextStrong, 0.92f,
            FontWeight.SemiBold);
        var handleText = post.OwnerHandle.Length > 0 ? $"@{post.OwnerHandle}" : string.Empty;
        var timeText = TimeText.Short(post.CreatedAtUnix);
        var sub = handleText.Length > 0 ? $"{handleText} · {timeText}" : timeText;
        Typography.Draw(new Vector2(nameLeft, origin.Y + pad + 21f * scale), sub, AppPalettes.Velvet.MutedInk, 0.76f);
        if (UiInteract.HoverClick(new Vector2(innerX, origin.Y + pad),
                new Vector2(origin.X + width - pad, origin.Y + pad + headerBlock)))
        {
            OpenProfile(post.OwnerId);
        }

        DrawPostThumbnail(post, new Vector2(innerX, imageTop), new Vector2(innerX + imageSize, imageBottom), scale);
        if (UiInteract.HoverClick(new Vector2(innerX, imageTop), new Vector2(innerX + imageSize, imageBottom)))
        {
            router.Push(VelvetRoute.PostDetail(post.Id));
        }

        var liked = post.MyReaction >= 0;
        var actionCenterY = actionsTop + actionsHeight * 0.5f;
        var heartCenter = new Vector2(innerX + 11f * scale, actionCenterY);
        if (ui.IconButton(heartCenter, 12f * scale, FontAwesomeIcon.Heart.ToIconString(),
                liked ? theme.Danger : AppPalettes.Velvet.BodyInk, AppSkin.Transparent, 1f, Loc.T(L.Velvet.Like)))
        {
            store.ToggleReaction(post, 0);
        }

        var cursorX = heartCenter.X + 16f * scale;
        if (post.TotalReactions > 0)
        {
            var likeText = post.TotalReactions.ToString(Loc.Culture);
            Typography.Draw(new Vector2(cursorX, actionCenterY - 7f * scale), likeText, AppPalettes.Velvet.BodyInk, 0.82f,
                FontWeight.Medium);
            cursorX += Typography.Measure(likeText, 0.82f, FontWeight.Medium).X + 14f * scale;
        }
        else
        {
            cursorX += 6f * scale;
        }

        var commentCenter = new Vector2(cursorX + 11f * scale, actionCenterY);
        if (ui.IconButton(commentCenter, 12f * scale, FontAwesomeIcon.Comment.ToIconString(), AppPalettes.Velvet.BodyInk,
                AppSkin.Transparent, 0.95f, Loc.T(L.Velvet.Comment)))
        {
            router.Push(VelvetRoute.PostDetail(post.Id));
        }

        if (post.CommentCount > 0)
        {
            Typography.Draw(new Vector2(commentCenter.X + 16f * scale, actionCenterY - 7f * scale),
                post.CommentCount.ToString(Loc.Culture), AppPalettes.Velvet.BodyInk, 0.82f, FontWeight.Medium);
        }

        var mine = store.Me is { } me && me.UserId == post.OwnerId;
        if (mine)
        {
            var deleteCenter = new Vector2(origin.X + width - pad - 12f * scale, actionCenterY);
            var deleteBackground = Palette.WithAlpha(ui.Theme.Danger, 0.16f);
            if (ui.IconButton(deleteCenter, 12f * scale, FontAwesomeIcon.Trash.ToIconString(), ui.Theme.Danger,
                    deleteBackground, 0.85f, Loc.T(L.Velvet.DeleteConfirm)))
            {
                AskDeletePost(post.Id);
            }
        }

        var y = textTop;
        if (post.Caption.Length > 0)
        {
            ImGui.SetCursorScreenPos(new Vector2(innerX, y));
            ImGui.PushTextWrapPos(innerX + innerWidth);
            using (ImRaii.PushColor(ImGuiCol.Text, AppPalettes.Velvet.BodyInk))
            using (Plugin.Fonts.Push(0.9f))
            {
                ImGui.TextWrapped(post.Caption);
            }

            ImGui.PopTextWrapPos();
            y += captionHeight;
        }

        if (post.Tags.Length > 0)
        {
            DrawTagsLine(new Vector2(innerX, y + 2f * scale), post.Tags);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cardHeight + 12f * scale));
    }

    private static float MeasureWrapped(string text, float wrapWidth, float fontScale)
    {
        using (Plugin.Fonts.Push(fontScale))
        {
            return ImGui.CalcTextSize(text, false, wrapWidth).Y;
        }
    }

    private void DrawDiscover(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var people = store.DiscoverResults;
        using (AppSurface.Begin(area))
        {
            ImGui.Dummy(new Vector2(0f, 4f * scale));
            var submitted = DrawDiscoverSearch();
            var trimmed = discoverQuery.Trim();
            if (submitted)
            {
                ApplyDiscoverQuery(trimmed);
            }
            else if (trimmed != discoverApplied)
            {
                discoverDebounce += ImGui.GetIO().DeltaTime;
                if (discoverDebounce >= 0.45f)
                {
                    ApplyDiscoverQuery(trimmed);
                }
            }
            else
            {
                discoverDebounce = 0f;
            }

            ImGui.Dummy(new Vector2(0f, 14f * scale));
            DrawSectionHeading(Loc.T(L.Velvet.LookingForLabel));
            DrawFilterChips();
            ImGui.Dummy(new Vector2(0f, 12f * scale));
            DrawSectionHeading(Loc.T(L.Velvet.PeopleToMeet));
            if (people.Length == 0)
            {
                Typography.Draw(ImGui.GetCursorScreenPos() + new Vector2(2f * scale, 4f * scale),
                    Loc.T(store.LoadingDiscover ? L.Common.Loading : L.Velvet.DiscoverEmpty), AppPalettes.Velvet.MutedInk, 0.85f);
                ImGui.Dummy(new Vector2(0f, 30f * scale));
            }
            else
            {
                for (var index = 0; index < people.Length; index++)
                {
                    DrawProfileRow(people[index]);
                }
            }

            ImGui.Dummy(new Vector2(0f, 40f * scale));
        }
    }

    private bool DrawDiscoverSearch()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = 40f * scale;
        Squircle.Fill(drawList, origin, new Vector2(origin.X + width, origin.Y + height), 12f * scale,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)));
        var iconCenter = new Vector2(origin.X + 20f * scale, origin.Y + height * 0.5f);
        AppSkin.Icon(iconCenter, FontAwesomeIcon.Search.ToIconString(), AppPalettes.Velvet.MutedInk, 0.82f);
        var hasQuery = discoverQuery.Length > 0;
        var clearReserve = hasQuery ? 34f * scale : 14f * scale;
        ImGui.SetCursorScreenPos(new Vector2(origin.X + 38f * scale,
            origin.Y + height * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(width - 38f * scale - clearReserve);
        var submitted = false;
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, AppPalettes.Velvet.TitleInk))
        {
            if (ImGui.InputTextWithHint("##velvetDiscoverSearch", Loc.T(L.Velvet.SearchPeopleHint), ref discoverQuery,
                    60, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                submitted = true;
            }
        }

        if (hasQuery)
        {
            var clearCenter = new Vector2(origin.X + width - 20f * scale, origin.Y + height * 0.5f);
            if (ui.IconButton(clearCenter, 12f * scale, FontAwesomeIcon.Times.ToIconString(), AppPalettes.Velvet.MutedInk,
                    new Vector4(1f, 1f, 1f, 0.08f), 0.72f))
            {
                discoverQuery = string.Empty;
                submitted = true;
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
        return submitted;
    }

    private void ApplyDiscoverQuery(string query)
    {
        discoverApplied = query;
        discoverDebounce = 0f;
        store.RefreshDiscover(lookingForFilter, query);
    }

    private void DrawSectionHeading(string label)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var barWidth = 3f * scale;
        var barHeight = 14f * scale;
        Squircle.Fill(drawList, new Vector2(origin.X, origin.Y + 2f * scale),
            new Vector2(origin.X + barWidth, origin.Y + 2f * scale + barHeight), barWidth * 0.5f,
            ImGui.GetColorU32(Accent));
        Typography.Draw(new Vector2(origin.X + barWidth + 9f * scale, origin.Y), label,
            new Vector4(0.99f, 0.90f, 0.94f, 1f), 0.95f, FontWeight.SemiBold);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, barHeight + 10f * scale));
    }

    private void DrawHubEmpty(Rect area, FontAwesomeIcon icon, string text)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var center = new Vector2(area.Center.X, area.Center.Y - 20f * scale);
        AppSkin.Icon(center, icon.ToIconString(), Palette.WithAlpha(AppPalettes.Velvet.MutedInk, 0.7f), 2.4f);
        Typography.DrawCentered(new Vector2(area.Center.X, center.Y + 42f * scale), text, AppPalettes.Velvet.MutedInk, 0.9f);
    }

    private void DrawPostThumbnail(VelvetPostDto post, Vector2 min, Vector2 max, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rounding = 12f * scale;
        var texture = images.Get(post.MediaUrl);
        if (texture is null)
        {
            Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)));
            return;
        }

        drawList.AddImageRounded(texture.Handle, min, max, Vector2.Zero, Vector2.One, 0xFFFFFFFFu, rounding,
            ImDrawFlags.RoundCornersAll);
        if (ImGui.IsItemHovered())
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
    }

    private void DrawComposeFab(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var radius = 26f * scale;
        var margin = 18f * scale;
        var boxSize = radius * 2f + margin;
        var boxMin = new Vector2(area.Max.X - boxSize, area.Max.Y - boxSize);
        ImGui.SetCursorScreenPos(boxMin);
        using var overlay = ImRaii.Child("##velvetComposeFab", new Vector2(boxSize, boxSize), false,
            ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        var center = new Vector2(area.Max.X - radius - margin, area.Max.Y - radius - margin);
        var drawList = ImGui.GetWindowDrawList();
        var hovered =
            ImGui.IsMouseHoveringRect(center - new Vector2(radius, radius), center + new Vector2(radius, radius));
        drawList.AddCircleFilled(center + new Vector2(0f, 2f * scale), radius,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.30f)), 32);
        drawList.AddCircleFilled(center, radius,
            ImGui.GetColorU32(hovered
                ? Palette.Mix(AppPalettes.Velvet.Accent, new Vector4(1f, 1f, 1f, 1f), 0.12f)
                : AppPalettes.Velvet.Accent), 32);
        AppSkin.Icon(center, FontAwesomeIcon.Plus.ToIconString(), new Vector4(1f, 1f, 1f, 1f), 1.1f);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            ui.DrawActionTooltip(center, radius, Loc.T(L.Velvet.NewPost));
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                post.Open();
                router.Push(VelvetRoute.Compose);
            }
        }
    }

    private void DrawFilterChips()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var chipHeight = 30f * scale;
        var gap = 6f * scale;
        var widths = new float[VelvetLookingFor.All.Length];
        var totalWidth = 0f;
        for (var index = 0; index < VelvetLookingFor.All.Length; index++)
        {
            widths[index] =
                Typography.Measure(VelvetLookingFor.Label(VelvetLookingFor.All[index]), 0.85f, FontWeight.Medium).X +
                22f * scale;
            totalWidth += widths[index] + (index > 0 ? gap : 0f);
        }

        var origin = ImGui.GetCursorScreenPos();
        var visibleWidth = ImGui.GetContentRegionAvail().X;
        var stripRect = new Rect(origin, new Vector2(origin.X + visibleWidth, origin.Y + chipHeight));
        var maxScroll = MathF.Max(0f, totalWidth - visibleWidth);
        lookingForScrollX = Math.Clamp(lookingForScrollX, 0f, maxScroll);
        var hovering = ImGui.IsMouseHoveringRect(stripRect.Min, stripRect.Max);
        if (hovering)
        {
            var wheel = ImGui.GetIO().MouseWheel;
            if (wheel != 0f)
            {
                lookingForScrollX = Math.Clamp(lookingForScrollX - wheel * 40f * scale, 0f, maxScroll);
            }
        }

        if (hovering && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            lookingForDragging = true;
            lookingForDragStartMouseX = ImGui.GetMousePos().X;
            lookingForDragStartScrollX = lookingForScrollX;
        }

        var dragged = false;
        if (lookingForDragging)
        {
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                var deltaX = ImGui.GetMousePos().X - lookingForDragStartMouseX;
                if (MathF.Abs(deltaX) > 3f * scale)
                {
                    dragged = true;
                }

                lookingForScrollX = Math.Clamp(lookingForDragStartScrollX - deltaX, 0f, maxScroll);
            }
            else
            {
                lookingForDragging = false;
            }
        }

        var drawList = ImGui.GetWindowDrawList();
        drawList.PushClipRect(stripRect.Min, stripRect.Max, true);
        var cursorX = origin.X - lookingForScrollX;
        for (var index = 0; index < VelvetLookingFor.All.Length; index++)
        {
            var width = widths[index];
            if (cursorX + width >= stripRect.Min.X && cursorX <= stripRect.Max.X)
            {
                var value = VelvetLookingFor.All[index];
                var rect = new Rect(new Vector2(cursorX, origin.Y),
                    new Vector2(cursorX + width, origin.Y + chipHeight));
                if (ui.Chip(rect, VelvetLookingFor.Label(value), lookingForFilter == value) && !dragged)
                {
                    lookingForFilter = value;
                    store.RefreshDiscover(lookingForFilter, discoverApplied);
                }
            }

            cursorX += width + gap;
        }

        drawList.PopClipRect();
        var trackY = stripRect.Max.Y + 10f * scale;
        if (maxScroll > 0f)
        {
            var trackHeight = 4f * scale;
            var track = new Rect(new Vector2(origin.X, trackY),
                new Vector2(origin.X + visibleWidth, trackY + trackHeight));
            drawList.AddRectFilled(track.Min, track.Max, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.14f)),
                trackHeight * 0.5f);
            var thumbWidth = MathF.Max(28f * scale, visibleWidth * (visibleWidth / (visibleWidth + maxScroll)));
            var thumbX = origin.X + (visibleWidth - thumbWidth) * (lookingForScrollX / maxScroll);
            var thumbHovered = ImGui.IsMouseHoveringRect(new Vector2(thumbX, trackY),
                new Vector2(thumbX + thumbWidth, trackY + trackHeight));
            drawList.AddRectFilled(new Vector2(thumbX, trackY), new Vector2(thumbX + thumbWidth, trackY + trackHeight),
                ImGui.GetColorU32(thumbHovered || lookingForTrackDragging
                    ? Palette.Mix(AppPalettes.Velvet.Accent, new Vector4(1f, 1f, 1f, 1f), 0.15f)
                    : AppPalettes.Velvet.Accent), trackHeight * 0.5f);
            var hitPadding = 10f * scale;
            var trackHitRect = new Rect(new Vector2(track.Min.X, track.Min.Y - hitPadding),
                new Vector2(track.Max.X, track.Max.Y + hitPadding));
            var trackHitHovered = ImGui.IsMouseHoveringRect(trackHitRect.Min, trackHitRect.Max);
            if (trackHitHovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (trackHitHovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                lookingForTrackDragging = true;
                var clickRatio =
                    Math.Clamp((ImGui.GetMousePos().X - thumbWidth * 0.5f - origin.X) / (visibleWidth - thumbWidth), 0f,
                        1f);
                lookingForScrollX = clickRatio * maxScroll;
            }

            if (lookingForTrackDragging)
            {
                if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
                {
                    var ratio = Math.Clamp(
                        (ImGui.GetMousePos().X - thumbWidth * 0.5f - origin.X) / (visibleWidth - thumbWidth), 0f, 1f);
                    lookingForScrollX = ratio * maxScroll;
                }
                else
                {
                    lookingForTrackDragging = false;
                }
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(visibleWidth, trackY + 8f * scale - origin.Y));
    }

    private void DrawMessages(Rect area)
    {
        if (!store.RequestsLoaded && !store.LoadingRequests)
        {
            store.RefreshRequests();
        }

        if (!store.SentRequestsLoaded && !store.LoadingSentRequests)
        {
            store.RefreshSentRequests();
        }

        if (!store.ThreadsLoaded && !store.LoadingThreads)
        {
            store.RefreshThreads();
        }

        if (!store.ConnectionsLoaded && !store.LoadingConnections)
        {
            store.RefreshConnections();
        }

        var scale = ImGuiHelpers.GlobalScale;
        var requests = store.Requests;
        var sentRequests = store.SentRequests;
        var connections = store.Connections;
        var threads = store.Threads;
        using (AppSurface.Begin(area))
        {
            ImGui.Dummy(new Vector2(0f, 6f * scale));
            if (requests.Length > 0)
            {
                ui.SectionLabel($"{Loc.T(L.Velvet.Requests)} ({requests.Length})");
                for (var index = 0; index < requests.Length; index++)
                {
                    DrawRequestRow(requests[index]);
                }

                ImGui.Dummy(new Vector2(0f, 8f * scale));
            }

            if (sentRequests.Length > 0)
            {
                DrawSentRequestsSection(sentRequests);
                ImGui.Dummy(new Vector2(0f, 8f * scale));
            }

            if (connections.Length > 0)
            {
                ui.SectionLabel(Loc.T(L.Velvet.StartChat));
                DrawConnectionsStrip(connections);
                ImGui.Dummy(new Vector2(0f, 8f * scale));
            }

            ui.SectionLabel(Loc.T(L.Velvet.Messages));
            if (threads.Length == 0)
            {
                Typography.DrawCentered(new Vector2(area.Center.X, ImGui.GetCursorScreenPos().Y + 40f * scale),
                    store.LoadingThreads ? Loc.T(L.Common.Loading) : Loc.T(L.Velvet.MessagesEmpty), AppPalettes.Velvet.MutedInk);
            }
            else
            {
                for (var index = 0; index < threads.Length; index++)
                {
                    DrawThreadRow(threads[index]);
                }
            }

            ImGui.Dummy(new Vector2(0f, 16f * scale));
        }
    }

    private void DrawRequestRow(VelvetConnectionDto request)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var rowHeight = 60f * scale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var radius = 20f * scale;
        var avatarCenter = new Vector2(origin.X + radius, origin.Y + rowHeight * 0.5f);
        AvatarView.Draw(ImGui.GetWindowDrawList(), avatarCenter, radius, Accent,
            Monogram(request.DisplayName, request.Handle), 0.95f,
            lodestone.Remote(request.UserId, ToUri(request.AvatarUrl)), 32);
        var textLeft = origin.X + radius * 2f + 12f * scale;
        var displayName = string.IsNullOrEmpty(request.DisplayName) ? request.Handle : request.DisplayName;
        Typography.Draw(new Vector2(textLeft, origin.Y + 11f * scale), displayName, theme.TextStrong, 1f,
            FontWeight.SemiBold);
        Typography.Draw(new Vector2(textLeft, origin.Y + 31f * scale), Loc.T(L.Velvet.WantsToConnect),
            AppPalettes.Velvet.MutedInk, 0.82f);
        var buttonHeight = 30f * scale;
        var acceptWidth = 78f * scale;
        var declineWidth = 34f * scale;
        var acceptMin = new Vector2(origin.X + width - acceptWidth, origin.Y + rowHeight * 0.5f - buttonHeight * 0.5f);
        var acceptRect = new Rect(acceptMin, new Vector2(acceptMin.X + acceptWidth, acceptMin.Y + buttonHeight));
        if (ui.PillButton(acceptRect, Loc.T(L.Velvet.Accept), true))
        {
            store.AcceptRequest(request.UserId);
        }

        var declineCenter = new Vector2(acceptMin.X - declineWidth * 0.5f - 6f * scale, origin.Y + rowHeight * 0.5f);
        if (ui.IconButton(declineCenter, 15f * scale, FontAwesomeIcon.Times.ToIconString(), AppPalettes.Velvet.MutedInk,
                new Vector4(1f, 1f, 1f, 0.08f), 0.85f))
        {
            store.DeclineRequest(request.UserId);
        }

        if (UiInteract.HoverClick(origin, new Vector2(declineCenter.X - declineWidth, origin.Y + rowHeight)))
        {
            OpenProfile(request.UserId);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight));
    }

    private void DrawSentRequestsSection(VelvetConnectionDto[] requests)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var rowHeight = 44f * scale;
        var centerY = origin.Y + rowHeight * 0.5f;
        var hovered = ImGui.IsMouseHoveringRect(origin, new Vector2(origin.X + width, origin.Y + rowHeight));
        if (hovered)
        {
            Squircle.Fill(drawList, origin, new Vector2(origin.X + width, origin.Y + rowHeight), 12f * scale,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.04f)));
        }

        var label = $"{Loc.T(L.Velvet.SentRequests)} ({requests.Length})".ToUpperInvariant();
        var labelSize = Typography.Measure(label, 0.78f, FontWeight.SemiBold);
        Typography.Draw(new Vector2(origin.X + 2f * scale, centerY - labelSize.Y * 0.5f), label,
            AppPalettes.Velvet.HeaderInk, 0.78f, FontWeight.SemiBold);

        var chevronCenter = new Vector2(origin.X + width - 10f * scale, centerY);
        var chevron = sentExpanded ? FontAwesomeIcon.ChevronUp : FontAwesomeIcon.ChevronDown;
        AppSkin.Icon(chevronCenter, chevron.ToIconString(), AppPalettes.Velvet.MutedInk, 0.72f);

        if (!sentExpanded)
        {
            var stackRadius = 13f * scale;
            var stackStep = stackRadius * 1.15f;
            var stackCount = Math.Min(requests.Length, 3);
            var rightmostX = chevronCenter.X - 22f * scale - stackRadius;
            for (var index = stackCount - 1; index >= 0; index--)
            {
                var request = requests[index];
                var center = new Vector2(rightmostX - (stackCount - 1 - index) * stackStep, centerY);
                drawList.AddCircleFilled(center, stackRadius + 2f * scale, ImGui.GetColorU32(theme.AppBackground), 28);
                AvatarView.Draw(drawList, center, stackRadius, Accent, Monogram(request.DisplayName, request.Handle),
                    0.78f, lodestone.Remote(request.UserId, ToUri(request.AvatarUrl)), 28);
            }
        }

        if (UiInteract.HoverClick(origin, new Vector2(origin.X + width, origin.Y + rowHeight)))
        {
            sentExpanded = !sentExpanded;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight));
        if (!sentExpanded)
        {
            return;
        }

        for (var index = 0; index < requests.Length; index++)
        {
            DrawSentRequestRow(requests[index]);
        }
    }

    private void DrawSentRequestRow(VelvetConnectionDto request)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var rowHeight = 60f * scale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var radius = 20f * scale;
        var avatarCenter = new Vector2(origin.X + radius, origin.Y + rowHeight * 0.5f);
        AvatarView.Draw(ImGui.GetWindowDrawList(), avatarCenter, radius, Accent,
            Monogram(request.DisplayName, request.Handle), 0.95f,
            lodestone.Remote(request.UserId, ToUri(request.AvatarUrl)), 32);
        var textLeft = origin.X + radius * 2f + 12f * scale;
        var displayName = string.IsNullOrEmpty(request.DisplayName) ? request.Handle : request.DisplayName;
        Typography.Draw(new Vector2(textLeft, origin.Y + 11f * scale), displayName, theme.TextStrong, 1f,
            FontWeight.SemiBold);
        var sub = request.Handle.Length > 0 ? $"@{request.Handle}" : TimeText.Short(request.ConnectedAtUnix);
        Typography.Draw(new Vector2(textLeft, origin.Y + 31f * scale), sub, AppPalettes.Velvet.MutedInk, 0.82f);
        var label = Loc.T(L.Velvet.Requested);
        var buttonHeight = 30f * scale;
        var buttonWidth = MathF.Max(92f * scale, Typography.Measure(label, 0.9f, FontWeight.SemiBold).X + 24f * scale);
        var buttonMin = new Vector2(origin.X + width - buttonWidth, origin.Y + rowHeight * 0.5f - buttonHeight * 0.5f);
        var buttonRect = new Rect(buttonMin, new Vector2(buttonMin.X + buttonWidth, buttonMin.Y + buttonHeight));
        if (ui.PillButton(buttonRect, label, false))
        {
            store.CancelRequest(request.UserId);
        }

        if (UiInteract.HoverClick(origin, new Vector2(buttonMin.X - 6f * scale, origin.Y + rowHeight)))
        {
            OpenProfile(request.UserId);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight));
    }

    private void DrawConnectionsStrip(VelvetConnectionDto[] connections)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var radius = 26f * scale;
        var origin = ImGui.GetCursorScreenPos();
        var step = radius * 2f + 14f * scale;
        var maxAcross = Math.Max(1, (int)(ImGui.GetContentRegionAvail().X / step));
        var count = Math.Min(connections.Length, maxAcross);
        for (var index = 0; index < count; index++)
        {
            var connection = connections[index];
            var center = new Vector2(origin.X + radius + index * step, origin.Y + radius);
            AvatarView.Draw(ImGui.GetWindowDrawList(), center, radius, Accent,
                Monogram(connection.DisplayName, connection.Handle), 1f,
                lodestone.Remote(connection.UserId, ToUri(connection.AvatarUrl)), 40);
            DrawPresenceDot(new Vector2(center.X + radius - 4f * scale, center.Y + radius - 4f * scale),
                connection.Presence);
            var name = string.IsNullOrEmpty(connection.DisplayName) ? connection.Handle : connection.DisplayName;
            Typography.DrawCentered(new Vector2(center.X, origin.Y + radius * 2f + 10f * scale),
                UiText.Truncate(name, 8), AppPalettes.Velvet.MutedInk, 0.72f);
            if (UiInteract.HoverClick(center - new Vector2(radius, radius), center + new Vector2(radius, radius)))
            {
                OpenThreadWith(connection.UserId);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, radius * 2f + 24f * scale));
    }

    private void DrawFullScreenMessage(Rect area, string message)
    {
        var drawList = ImGui.GetWindowDrawList();
        var screen = SceneChrome.ScreenFrom(area, theme, ImGuiHelpers.GlobalScale);
        ui.Backdrop(screen);
        Typography.DrawCentered(area.Center, message, AppPalettes.Velvet.BodyInk);
    }

    private void TickHeartbeat()
    {
        sinceHeartbeat += ImGui.GetIO().DeltaTime;
        if (sinceHeartbeat < HeartbeatSeconds)
        {
            return;
        }

        sinceHeartbeat = 0f;
        store.Heartbeat();
    }

    private void DrawPresenceDot(Vector2 center, int presence)
    {
        if (presence == VelvetPresence.Offline)
        {
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddCircleFilled(center, 6f * scale, ImGui.GetColorU32(theme.AppBackground), 16);
        drawList.AddCircleFilled(center, 4f * scale, ImGui.GetColorU32(VelvetPresence.Color(presence)), 16);
    }

    private void OpenProfile(string userId)
    {
        store.OpenProfile(userId);
        router.Push(VelvetRoute.Profile(userId));
    }

    private void OpenLikers(string postId)
    {
        store.OpenLikers(postId);
        router.Push(VelvetRoute.Likers(postId));
    }

    private void DrawLikers(Rect area, string postId)
    {
        store.OpenLikers(postId);
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Social.LikedByTitle), back);
        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var listRect = new Rect(new Vector2(area.Min.X, top), area.Max);
        var snapshot = store.Likers;
        using (AppSurface.Begin(listRect))
        {
            if (snapshot.Length == 0)
            {
                var message = store.LikersLoading ? Loc.T(L.Common.Loading) : Loc.T(L.Social.ListEmpty);
                Typography.DrawCentered(new Vector2(listRect.Center.X, listRect.Min.Y + 60f * scale), message,
                    AppPalettes.Velvet.MutedInk);
            }
            else
            {
                ImGui.Dummy(new Vector2(0f, 6f * scale));
                for (var index = 0; index < snapshot.Length; index++)
                {
                    DrawLikerRow(snapshot[index]);
                }

                ImGui.Dummy(new Vector2(0f, 16f * scale));
            }
        }
    }

    private void DrawLikerRow(UserDto user)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var rowHeight = 58f * scale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var radius = 20f * scale;
        var avatarCenter = new Vector2(origin.X + radius, origin.Y + rowHeight * 0.5f);
        AvatarView.Draw(ImGui.GetWindowDrawList(), avatarCenter, radius, Accent, Monogram(user.DisplayName, user.Handle),
            0.95f, lodestone.Remote(user.Id, ToUri(user.AvatarUrl)), 32);
        var textLeft = origin.X + radius * 2f + 12f * scale;
        var displayName = string.IsNullOrEmpty(user.DisplayName) ? user.Handle : user.DisplayName;
        Typography.Draw(new Vector2(textLeft, origin.Y + 11f * scale), displayName, theme.TextStrong, 1f,
            FontWeight.SemiBold);
        if (user.Handle.Length > 0)
        {
            Typography.Draw(new Vector2(textLeft, origin.Y + 31f * scale), $"@{user.Handle}", AppPalettes.Velvet.MutedInk,
                0.82f);
        }

        if (UiInteract.HoverClick(origin, new Vector2(origin.X + width, origin.Y + rowHeight)))
        {
            OpenProfile(user.Id);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight));
    }

    private void OpenThreadWith(string userId)
    {
        store.OpenThread(userId);
        router.Push(VelvetRoute.Thread(userId));
    }

    private string ThreadTitle(string threadId)
    {
        var threads = store.Threads;
        for (var index = 0; index < threads.Length; index++)
        {
            if (threads[index].OtherUserId == threadId)
            {
                return string.IsNullOrEmpty(threads[index].OtherDisplayName)
                    ? threads[index].OtherHandle
                    : threads[index].OtherDisplayName;
            }
        }

        var connections = store.Connections;
        for (var index = 0; index < connections.Length; index++)
        {
            if (connections[index].UserId == threadId)
            {
                return string.IsNullOrEmpty(connections[index].DisplayName)
                    ? connections[index].Handle
                    : connections[index].DisplayName;
            }
        }

        return Loc.T(L.Velvet.Messages);
    }

    private AvatarHandle AvatarFor(VelvetProfileDto? profile)
    {
        if (profile is null)
        {
            return AvatarHandle.Disabled;
        }

        return lodestone.Remote(profile.UserId, ToUri(profile.AvatarUrl));
    }

    private static string MonogramFor(VelvetProfileDto? profile) =>
        profile is null ? "?" : Monogram(profile.DisplayName, profile.Handle);

    private static string Monogram(string displayName, string handle)
    {
        var source = string.IsNullOrEmpty(displayName) ? handle : displayName;
        return source.Length > 0 ? source[..1].ToUpperInvariant() : "?";
    }

    private static Uri? ToUri(string? url) =>
        string.IsNullOrEmpty(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri) ? null : uri;

    public void Dispose()
    {
        images.Dispose();
        store.Dispose();
    }
}
