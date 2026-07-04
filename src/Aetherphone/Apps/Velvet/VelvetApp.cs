using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Net;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Platform;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Wallpapers;
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
    private const int TagsMax = 200;
    private const int MessageMax = 1000;

    private const float HeartbeatSeconds = 45f;
    private const float ThreadPollSeconds = 2.5f;
    private const float TypingSendSeconds = 3f;

    private static readonly string[] VibeSuggestions = { "soft", "switch", "service", "playful", "dom", "sub", "gentle", "brat" };
    private static readonly string[] TagSuggestions = { "gpose", "romance", "cuddles", "roleplay", "teasing", "praise", "lingerie", "bondage" };


    public string Id => "velvet";

    public string DisplayName => Loc.T(L.Apps.Velvet);

    public string Glyph => "Ve";

    public Vector4 Accent => VelvetUi.Accent;

    public int BadgeCount => store.UnreadCount + store.RequestCount;

    private readonly VelvetStore store;
    private readonly LodestoneService lodestone;
    private readonly Configuration configuration;
    private readonly PhotoLibrary library;
    private readonly VelvetUi ui = new();
    private readonly VelvetAvatarComposer avatar;
    private readonly VelvetPostComposer post;
    private readonly VelvetReportControl report;
    private readonly VelvetDeleteControl delete;
    private readonly RemoteImageCache images;

    private readonly ViewRouter<VelvetRoute> router;
    private readonly RouterDraw<VelvetRoute> drawView;
    private readonly Action back;

    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;

    private VelvetTab activeTab = VelvetTab.Hub;

    private bool gateBusy;

    private int onboardStep;
    private float sinceHeartbeat = HeartbeatSeconds;

    private int lookingForFilter = VelvetLookingFor.Any;
    private float lookingForScrollX;
    private bool lookingForDragging;
    private float lookingForDragStartMouseX;
    private float lookingForDragStartScrollX;
    private bool lookingForTrackDragging;

    private string editIntro = string.Empty;
    private string editPronouns = string.Empty;
    private string editVibe = string.Empty;
    private string editTags = string.Empty;
    private string editLimits = string.Empty;
    private int editLookingFor = VelvetLookingFor.Sharing;
    private int editRelationship = VelvetRelationship.NotSaying;
    private bool editDiscoverable = true;
    private string? editLoadedFor;
    private volatile int editOutcome;
    private volatile bool editBusy;

    private string messageDraft = string.Empty;
    private bool threadFocus;
    private volatile int sendOutcome;
    private float sinceThreadPoll;
    private float sinceTypingSend = TypingSendSeconds;
    private string lastTypingDraft = string.Empty;

    private string commentsPostId = string.Empty;
    private string commentDraft = string.Empty;


    public VelvetApp(AethernetSession session, AethernetClient client, LodestoneService lodestone, Configuration configuration, PhotoLibrary library, HttpService http)
    {
        store = new VelvetStore(session, client);
        this.lodestone = lodestone;
        this.configuration = configuration;
        this.library = library;
        avatar = new VelvetAvatarComposer(store, library);
        post = new VelvetPostComposer(store, library);
        report = new VelvetReportControl(store);
        delete = new VelvetDeleteControl(store);
        images = new RemoteImageCache(http);

        router = new ViewRouter<VelvetRoute>(VelvetRoute.Root);
        drawView = DrawView;
        back = () => router.Pop();
    }

    public void OnOpened()
    {
        router.Reset();
        activeTab = VelvetTab.Hub;
        onboardStep = 0;
        store.InvalidateLists();
        if (GateAccepted && store.IsSignedIn)
        {
            store.EnsureMe();
        }
    }

    public void OnClosed()
    {
        router.Reset();
        messageDraft = string.Empty;
        report.Reset();
        delete.Reset();
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
        router.Draw(context.Content, VelvetUi.Transparent, ImGui.GetIO().DeltaTime, drawView);
    }

    private bool GateAccepted =>
        configuration.VelvetAcknowledgedGate && configuration.VelvetAcknowledgedGateVersion >= Configuration.VelvetGateVersion;

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
        Typography.DrawCentered(new Vector2(area.Center.X, area.Center.Y), title, VelvetUi.TitleInk, 1.2f, FontWeight.SemiBold);
    }

    private void DrawBottomNav(Rect nav)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddLine(nav.Min, new Vector2(nav.Max.X, nav.Min.Y), ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)), 1f);

        var width = nav.Width / 3f;
        DrawNavItem(new Rect(nav.Min, new Vector2(nav.Min.X + width, nav.Max.Y)), FontAwesomeIcon.Compass, Loc.T(L.Velvet.TabHub), VelvetTab.Hub, 0);
        DrawNavItem(new Rect(new Vector2(nav.Min.X + width, nav.Min.Y), new Vector2(nav.Min.X + width * 2f, nav.Max.Y)), FontAwesomeIcon.Comment, Loc.T(L.Velvet.Messages), VelvetTab.Messages, store.UnreadCount + store.RequestCount);
        DrawNavItem(new Rect(new Vector2(nav.Min.X + width * 2f, nav.Min.Y), nav.Max), FontAwesomeIcon.User, Loc.T(L.Velvet.TabMe), VelvetTab.Me, 0);
    }

    private void DrawNavItem(Rect rect, FontAwesomeIcon icon, string label, VelvetTab tab, int badge)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var active = activeTab == tab;
        var color = active ? new Vector4(0.99f, 0.72f, 0.82f, 1f) : VelvetUi.MutedInk;
        var iconCenter = new Vector2(rect.Center.X, rect.Min.Y + 20f * scale);
        VelvetUi.Icon(iconCenter, icon.ToIconString(), color, 1.05f);
        Typography.DrawCentered(new Vector2(rect.Center.X, rect.Min.Y + 42f * scale), label, color, 0.72f, active ? FontWeight.SemiBold : FontWeight.Regular);

        if (badge > 0)
        {
            var badgeCenter = new Vector2(iconCenter.X + 12f * scale, iconCenter.Y - 9f * scale);
            ImGui.GetWindowDrawList().AddCircleFilled(badgeCenter, 7f * scale, ImGui.GetColorU32(theme.Danger), 16);
            Typography.DrawCentered(badgeCenter, badge > 9 ? "9+" : badge.ToString(Loc.Culture), new Vector4(1f, 1f, 1f, 1f), 0.62f, FontWeight.SemiBold);
        }

        if (VelvetUi.HoverClick(rect.Min, rect.Max))
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
            store.RefreshDiscover(lookingForFilter, string.Empty);
        }

        var feed = store.Feed;
        var people = store.DiscoverResults;
        using (AppSurface.Begin(area))
        {
            ImGui.Dummy(new Vector2(0f, 6f * scale));
            VelvetUi.SectionLabel(Loc.T(L.Velvet.Fresh));
            if (feed.Length == 0)
            {
                Typography.Draw(ImGui.GetCursorScreenPos() + new Vector2(2f * scale, 4f * scale), Loc.T(store.LoadingFeed ? L.Common.Loading : L.Velvet.FeedEmpty), VelvetUi.MutedInk, 0.85f);
                ImGui.Dummy(new Vector2(0f, 30f * scale));
            }
            else
            {
                DrawFeedGrid(feed);
            }

            ImGui.Dummy(new Vector2(0f, 18f * scale));
            VelvetUi.SectionLabel(Loc.T(L.Velvet.LookingForLabel));
            DrawFilterChips();
            VelvetUi.SectionLabel(Loc.T(L.Velvet.PeopleToMeet));
            if (people.Length == 0)
            {
                Typography.Draw(ImGui.GetCursorScreenPos() + new Vector2(2f * scale, 4f * scale), Loc.T(store.LoadingDiscover ? L.Common.Loading : L.Velvet.DiscoverEmpty), VelvetUi.MutedInk, 0.85f);
            }
            else
            {
                for (var index = 0; index < people.Length; index++)
                {
                    DrawProfileRow(people[index]);
                }
            }

            ImGui.Dummy(new Vector2(0f, 72f * scale));
        }

        DrawComposeFab(area);
    }

    private void DrawFeedGrid(VelvetPostDto[] posts)
    {
        var scale = ImGuiHelpers.GlobalScale;
        const int columns = 3;
        var gap = 6f * scale;
        var cell = (ImGui.GetContentRegionAvail().X - gap * (columns - 1)) / columns;
        using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(gap, gap)))
        {
            for (var index = 0; index < posts.Length; index++)
            {
                using (ImRaii.PushId(index))
                {
                    var clicked = ImGui.InvisibleButton("post", new Vector2(cell, cell));
                    DrawPostThumbnail(posts[index], ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), scale);
                    if (clicked)
                    {
                        router.Push(VelvetRoute.PostDetail(posts[index].Id));
                    }
                }

                if (index % columns != columns - 1)
                {
                    ImGui.SameLine();
                }
            }
        }
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

        drawList.AddImageRounded(texture.Handle, min, max, Vector2.Zero, Vector2.One, 0xFFFFFFFFu, rounding, ImDrawFlags.RoundCornersAll);

        if (ImGui.IsItemHovered())
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
    }

    private void DrawPostDetail(Rect area, string postId)
    {
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Apps.Velvet), back);

        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);

        var feed = store.Feed;
        VelvetPostDto? found = null;
        for (var index = 0; index < feed.Length; index++)
        {
            if (feed[index].Id == postId)
            {
                found = feed[index];
                break;
            }
        }

        if (found is not { } post)
        {
            Typography.DrawCentered(body.Center, Loc.T(L.Common.Loading), VelvetUi.MutedInk);
            return;
        }

        if (commentsPostId != postId)
        {
            commentsPostId = postId;
            commentDraft = string.Empty;
            store.OpenComments(postId);
        }

        using (AppSurface.Begin(body))
        {
            var origin = ImGui.GetCursorScreenPos();
            var width = ImGui.GetContentRegionAvail().X;

            var headerHeight = 44f * scale;
            var avatarRadius = 16f * scale;
            var avatarCenter = new Vector2(origin.X + avatarRadius, origin.Y + headerHeight * 0.5f);
            AvatarView.Draw(ImGui.GetWindowDrawList(), avatarCenter, avatarRadius, Accent, Monogram(post.OwnerDisplayName, post.OwnerHandle), 0.95f, lodestone.Remote(post.OwnerId, ToUri(post.OwnerAvatarUrl)), 28);

            var nameLeft = avatarCenter.X + avatarRadius + 10f * scale;
            var displayName = string.IsNullOrEmpty(post.OwnerDisplayName) ? post.OwnerHandle : post.OwnerDisplayName;
            Typography.Draw(new Vector2(nameLeft, avatarCenter.Y - 8f * scale), displayName, theme.TextStrong, 0.95f, FontWeight.SemiBold);

            ImGui.SetCursorScreenPos(new Vector2(origin.X, origin.Y + headerHeight));
            var imageRect = new Rect(new Vector2(origin.X, origin.Y + headerHeight), new Vector2(origin.X + width, origin.Y + headerHeight + width));
            DrawPostThumbnail(post, imageRect.Min, imageRect.Max, scale);

            ImGui.SetCursorScreenPos(new Vector2(origin.X, imageRect.Max.Y + 12f * scale));
            var actionsY = imageRect.Max.Y + 12f * scale + 8f * scale;
            var liked = post.MyReaction >= 0;
            var heartCenter = new Vector2(origin.X + 12f * scale, actionsY);
            if (ui.IconButton(heartCenter, 14f * scale, FontAwesomeIcon.Heart.ToIconString(), liked ? theme.Danger : VelvetUi.BodyInk, new Vector4(0f, 0f, 0f, 0f), 1f))
            {
                store.ToggleReaction(post, 0);
            }

            var mine = store.Me is { } me && me.UserId == post.OwnerId;
            var reportShown = false;
            var deleteShown = false;
            if (mine)
            {
                var deleteCenter = new Vector2(origin.X + width - 14f * scale, actionsY);
                deleteShown = delete.Toggle(ui, deleteCenter, 14f * scale, post.Id);
            }
            else
            {
                var reportCenter = new Vector2(origin.X + width - 14f * scale, actionsY);
                reportShown = report.Toggle(ui, reportCenter, 14f * scale, "post", post.Id);
            }

            ImGui.SetCursorScreenPos(new Vector2(origin.X, actionsY + 18f * scale));
            if (deleteShown)
            {
                delete.Composer(ui, origin.X, width, () => router.Pop());
                ImGui.Dummy(new Vector2(0f, 6f * scale));
            }
            else if (reportShown)
            {
                report.Composer(ui, origin.X, width);
                ImGui.Dummy(new Vector2(0f, 6f * scale));
            }

            if (post.TotalReactions > 0)
            {
                Typography.Draw(ImGui.GetCursorScreenPos(), Loc.Plural(L.Velvet.Likes, post.TotalReactions), theme.TextStrong, 0.9f, FontWeight.SemiBold);
                ImGui.Dummy(new Vector2(0f, 12f * scale));
            }

            if (!string.IsNullOrWhiteSpace(post.Caption))
            {
                ImGui.PushTextWrapPos(0f);
                using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
                {
                    ImGui.TextWrapped(post.Caption);
                }

                ImGui.PopTextWrapPos();
                ImGui.Dummy(new Vector2(0f, 12f * scale));
            }

            if (post.Tags.Length > 0)
            {
                DrawTagChips(post.Tags);
            }

            DrawComments(post.Id, width, scale);

            ImGui.Dummy(new Vector2(0f, 20f * scale));
        }
    }

    private void DrawComments(string postId, float width, float scale)
    {
        ImGui.Dummy(new Vector2(0f, 6f * scale));
        VelvetUi.SectionLabel(Loc.T(L.Velvet.Comments));
        ImGui.Dummy(new Vector2(0f, 6f * scale));

        if (store.LoadingComments)
        {
            Typography.Draw(ImGui.GetCursorScreenPos(), Loc.T(L.Common.Loading), VelvetUi.MutedInk, 0.85f);
            ImGui.Dummy(new Vector2(0f, 16f * scale));
        }
        else
        {
            var comments = store.DetailComments;
            if (comments.Length == 0)
            {
                Typography.Draw(ImGui.GetCursorScreenPos(), Loc.T(L.Velvet.NoComments), VelvetUi.MutedInk, 0.85f);
                ImGui.Dummy(new Vector2(0f, 16f * scale));
            }
            else
            {
                for (var index = 0; index < comments.Length; index++)
                {
                    DrawCommentRow(comments[index], scale);
                }
            }
        }

        DrawCommentComposer(postId, width, scale);
    }

    private void DrawCommentRow(VelvetCommentDto comment, float scale)
    {
        var name = string.IsNullOrEmpty(comment.AuthorDisplayName) ? comment.AuthorHandle : comment.AuthorDisplayName;
        Typography.Draw(ImGui.GetCursorScreenPos(), name, theme.TextStrong, 0.85f, FontWeight.SemiBold);
        ImGui.Dummy(new Vector2(0f, 15f * scale));

        ImGui.PushTextWrapPos(0f);
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
        {
            ImGui.TextWrapped(comment.Text);
        }

        ImGui.PopTextWrapPos();
        ImGui.Dummy(new Vector2(0f, 12f * scale));
    }

    private void DrawCommentComposer(string postId, float width, float scale)
    {
        ImGui.Dummy(new Vector2(0f, 4f * scale));
        var drawList = ImGui.GetWindowDrawList();
        var sendRadius = 15f * scale;
        var origin = ImGui.GetCursorScreenPos();
        var pillHeight = ImGui.GetFrameHeight() + 8f * scale;
        var pillMin = origin;
        var pillMax = new Vector2(origin.X + width - sendRadius * 2f - 12f * scale, origin.Y + pillHeight);
        Squircle.Fill(drawList, pillMin, pillMax, pillHeight * 0.5f, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)));

        ImGui.SetCursorScreenPos(new Vector2(pillMin.X + 14f * scale, (pillMin.Y + pillMax.Y) * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(pillMax.X - pillMin.X - 24f * scale);

        var submitted = false;
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            if (ImGui.InputTextWithHint("##velvetComment", Loc.T(L.Velvet.AddComment), ref commentDraft, 500, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                submitted = true;
            }
        }

        var canSend = commentDraft.Trim().Length > 0 && !store.Commenting;
        var sendCenter = new Vector2(pillMax.X + 6f * scale + sendRadius, (pillMin.Y + pillMax.Y) * 0.5f);
        drawList.AddCircleFilled(sendCenter, sendRadius, ImGui.GetColorU32(canSend ? Accent : theme.SurfaceMuted), 24);
        VelvetUi.Icon(sendCenter, FontAwesomeIcon.PaperPlane.ToIconString(), new Vector4(1f, 1f, 1f, 1f), 0.8f);
        if (VelvetUi.HoverClick(sendCenter - new Vector2(sendRadius, sendRadius), sendCenter + new Vector2(sendRadius, sendRadius)) && canSend)
        {
            submitted = true;
        }

        ImGui.SetCursorScreenPos(new Vector2(origin.X, pillMax.Y));
        ImGui.Dummy(new Vector2(0f, 4f * scale));

        if (submitted && canSend)
        {
            store.AddComment(postId, commentDraft, _ => { });
            commentDraft = string.Empty;
        }
    }

    private void DrawComposeFab(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var radius = 26f * scale;
        var center = new Vector2(area.Max.X - radius - 18f * scale, area.Max.Y - radius - 18f * scale);
        var drawList = ImGui.GetWindowDrawList();
        var hovered = ImGui.IsMouseHoveringRect(center - new Vector2(radius, radius), center + new Vector2(radius, radius));
        drawList.AddCircleFilled(center + new Vector2(0f, 2f * scale), radius, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.30f)), 32);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(hovered ? Palette.Mix(VelvetUi.Accent, new Vector4(1f, 1f, 1f, 1f), 0.12f) : VelvetUi.Accent), 32);
        VelvetUi.Icon(center, FontAwesomeIcon.Plus.ToIconString(), new Vector4(1f, 1f, 1f, 1f), 1.1f);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
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
            widths[index] = Typography.Measure(VelvetLookingFor.Label(VelvetLookingFor.All[index]), 0.85f, FontWeight.Medium).X + 22f * scale;
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
                var rect = new Rect(new Vector2(cursorX, origin.Y), new Vector2(cursorX + width, origin.Y + chipHeight));
                if (ui.Chip(rect, VelvetLookingFor.Label(value), lookingForFilter == value) && !dragged)
                {
                    lookingForFilter = value;
                    store.RefreshDiscover(lookingForFilter, string.Empty);
                }
            }

            cursorX += width + gap;
        }

        drawList.PopClipRect();

        var trackY = stripRect.Max.Y + 10f * scale;
        if (maxScroll > 0f)
        {
            var trackHeight = 4f * scale;
            var track = new Rect(new Vector2(origin.X, trackY), new Vector2(origin.X + visibleWidth, trackY + trackHeight));
            drawList.AddRectFilled(track.Min, track.Max, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.14f)), trackHeight * 0.5f);

            var thumbWidth = MathF.Max(28f * scale, visibleWidth * (visibleWidth / (visibleWidth + maxScroll)));
            var thumbX = origin.X + (visibleWidth - thumbWidth) * (lookingForScrollX / maxScroll);
            var thumbHovered = ImGui.IsMouseHoveringRect(new Vector2(thumbX, trackY), new Vector2(thumbX + thumbWidth, trackY + trackHeight));
            drawList.AddRectFilled(new Vector2(thumbX, trackY), new Vector2(thumbX + thumbWidth, trackY + trackHeight), ImGui.GetColorU32(thumbHovered || lookingForTrackDragging ? Palette.Mix(VelvetUi.Accent, new Vector4(1f, 1f, 1f, 1f), 0.15f) : VelvetUi.Accent), trackHeight * 0.5f);

            var hitPadding = 10f * scale;
            var trackHitRect = new Rect(new Vector2(track.Min.X, track.Min.Y - hitPadding), new Vector2(track.Max.X, track.Max.Y + hitPadding));
            var trackHitHovered = ImGui.IsMouseHoveringRect(trackHitRect.Min, trackHitRect.Max);
            if (trackHitHovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (trackHitHovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                lookingForTrackDragging = true;
                var clickRatio = Math.Clamp((ImGui.GetMousePos().X - thumbWidth * 0.5f - origin.X) / (visibleWidth - thumbWidth), 0f, 1f);
                lookingForScrollX = clickRatio * maxScroll;
            }

            if (lookingForTrackDragging)
            {
                if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
                {
                    var ratio = Math.Clamp((ImGui.GetMousePos().X - thumbWidth * 0.5f - origin.X) / (visibleWidth - thumbWidth), 0f, 1f);
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
        var connections = store.Connections;
        var threads = store.Threads;
        using (AppSurface.Begin(area))
        {
            ImGui.Dummy(new Vector2(0f, 6f * scale));

            if (requests.Length > 0)
            {
                VelvetUi.SectionLabel($"{Loc.T(L.Velvet.Requests)} ({requests.Length})");
                for (var index = 0; index < requests.Length; index++)
                {
                    DrawRequestRow(requests[index]);
                }

                ImGui.Dummy(new Vector2(0f, 8f * scale));
            }

            if (connections.Length > 0)
            {
                VelvetUi.SectionLabel(Loc.T(L.Velvet.StartChat));
                DrawConnectionsStrip(connections);
                ImGui.Dummy(new Vector2(0f, 8f * scale));
            }

            VelvetUi.SectionLabel(Loc.T(L.Velvet.Messages));
            if (threads.Length == 0)
            {
                Typography.DrawCentered(new Vector2(area.Center.X, ImGui.GetCursorScreenPos().Y + 40f * scale), store.LoadingThreads ? Loc.T(L.Common.Loading) : Loc.T(L.Velvet.MessagesEmpty), VelvetUi.MutedInk);
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
        AvatarView.Draw(ImGui.GetWindowDrawList(), avatarCenter, radius, Accent, Monogram(request.DisplayName, request.Handle), 0.95f, lodestone.Remote(request.UserId, ToUri(request.AvatarUrl)), 32);

        var textLeft = origin.X + radius * 2f + 12f * scale;
        var displayName = string.IsNullOrEmpty(request.DisplayName) ? request.Handle : request.DisplayName;
        Typography.Draw(new Vector2(textLeft, origin.Y + 11f * scale), displayName, theme.TextStrong, 1f, FontWeight.SemiBold);
        Typography.Draw(new Vector2(textLeft, origin.Y + 31f * scale), Loc.T(L.Velvet.WantsToConnect), VelvetUi.MutedInk, 0.82f);

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
        if (ui.IconButton(declineCenter, 15f * scale, FontAwesomeIcon.Times.ToIconString(), VelvetUi.MutedInk, new Vector4(1f, 1f, 1f, 0.08f), 0.85f))
        {
            store.DeclineRequest(request.UserId);
        }

        if (VelvetUi.HoverClick(origin, new Vector2(declineCenter.X - declineWidth, origin.Y + rowHeight)))
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
            AvatarView.Draw(ImGui.GetWindowDrawList(), center, radius, Accent, Monogram(connection.DisplayName, connection.Handle), 1f, lodestone.Remote(connection.UserId, ToUri(connection.AvatarUrl)), 40);
            DrawPresenceDot(new Vector2(center.X + radius - 4f * scale, center.Y + radius - 4f * scale), connection.Presence);
            var name = string.IsNullOrEmpty(connection.DisplayName) ? connection.Handle : connection.DisplayName;
            Typography.DrawCentered(new Vector2(center.X, origin.Y + radius * 2f + 10f * scale), VelvetUi.Truncate(name, 8), VelvetUi.MutedInk, 0.72f);
            if (VelvetUi.HoverClick(center - new Vector2(radius, radius), center + new Vector2(radius, radius)))
            {
                OpenThreadWith(connection.UserId);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, radius * 2f + 24f * scale));
    }

    private void DrawMe(Rect area)
    {
        var me = store.Me;
        if (me is null)
        {
            store.EnsureMe();
            Typography.DrawCentered(area.Center, Loc.T(L.Common.Loading), VelvetUi.MutedInk);
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        using (AppSurface.Begin(area))
        {
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ImGui.GetContentRegionAvail().X;

            var avatarRadius = 40f * scale;
            var avatarCenter = new Vector2(origin.X + width * 0.5f, origin.Y + avatarRadius + 10f * scale);
            AvatarView.Draw(drawList, avatarCenter, avatarRadius, Accent, MonogramFor(me), 1.4f, AvatarFor(me), 56);

            var displayName = string.IsNullOrEmpty(me.DisplayName) ? me.Handle : me.DisplayName;
            Typography.DrawCentered(new Vector2(avatarCenter.X, avatarCenter.Y + avatarRadius + 18f * scale), displayName, theme.TextStrong, 1.35f, FontWeight.SemiBold);
            var meta = me.Handle.Length > 0 ? $"@{me.Handle}" : me.World;
            Typography.DrawCentered(new Vector2(avatarCenter.X, avatarCenter.Y + avatarRadius + 42f * scale), meta, VelvetUi.MutedInk, 0.9f);

            ImGui.SetCursorScreenPos(new Vector2(origin.X, avatarCenter.Y + avatarRadius + 60f * scale));

            var summary = $"{VelvetLookingFor.Label(me.LookingFor)}";
            if (me.RelationshipStatus != VelvetRelationship.NotSaying)
            {
                summary += $"  ·  {VelvetRelationship.Label(me.RelationshipStatus)}";
            }

            Typography.DrawCentered(new Vector2(avatarCenter.X, ImGui.GetCursorScreenPos().Y), summary, Palette.Mix(Accent, theme.TextStrong, 0.35f), 0.9f, FontWeight.Medium);
            ImGui.Dummy(new Vector2(0f, 26f * scale));

            if (me.Intro.Length > 0)
            {
                ImGui.PushTextWrapPos(0f);
                using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
                {
                    ImGui.TextWrapped(me.Intro);
                }

                ImGui.PopTextWrapPos();
                ImGui.Dummy(new Vector2(0f, 8f * scale));
            }

            if (me.Tags.Length > 0)
            {
                DrawTagChips(me.Tags);
                ImGui.Dummy(new Vector2(0f, 8f * scale));
            }

            ImGui.Dummy(new Vector2(0f, 8f * scale));
            var buttonHeight = 44f * scale;
            var editMin = ImGui.GetCursorScreenPos();
            if (ui.PillButton(new Rect(editMin, new Vector2(editMin.X + width, editMin.Y + buttonHeight)), Loc.T(L.Velvet.EditProfile), true))
            {
                router.Push(VelvetRoute.EditProfile);
            }

            ImGui.Dummy(new Vector2(0f, buttonHeight + 10f * scale));
            var settingsMin = ImGui.GetCursorScreenPos();
            if (ui.GhostButton(new Rect(settingsMin, new Vector2(settingsMin.X + width, settingsMin.Y + buttonHeight)), Loc.T(L.Velvet.Settings)))
            {
                router.Push(VelvetRoute.Settings);
            }

            ImGui.Dummy(new Vector2(0f, buttonHeight + 20f * scale));
            Typography.DrawCentered(new Vector2(avatarCenter.X, ImGui.GetCursorScreenPos().Y), Loc.T(L.Velvet.SharingSoon), VelvetUi.MutedInk, 0.8f);
            ImGui.Dummy(new Vector2(0f, 30f * scale));
        }
    }

    private void DrawSettings(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Velvet.Settings), back);

        var me = store.Me;
        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, 6f * scale));
            VelvetUi.SectionLabel(Loc.T(L.Velvet.DiscoverableLabel));
            VelvetUi.HelpText(Loc.T(L.Velvet.AppearHelp));
            ImGui.Dummy(new Vector2(0f, 6f * scale));
            if (me is not null)
            {
                var discoverable = me.Discoverable;
                ui.ToggleRow(Loc.T(L.Velvet.DiscoverableLabel), ref discoverable);
                if (discoverable != me.Discoverable && !editBusy)
                {
                    editBusy = true;
                    store.UpdateProfile(new UpdateVelvetProfileRequest(null, null, null, null, null, null, null, discoverable), _ => editBusy = false);
                }
            }

            ImGui.Dummy(new Vector2(0f, 30f * scale));
        }
    }

    private void DrawEditProfile(Rect area)
    {
        var me = store.Me;
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Velvet.EditProfile), back);

        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);

        if (me is null)
        {
            store.EnsureMe();
            Typography.DrawCentered(body.Center, Loc.T(L.Common.Loading), VelvetUi.MutedInk);
            return;
        }

        if (editOutcome == 1)
        {
            editOutcome = 0;
            router.Pop();
            return;
        }

        if (editOutcome == 2)
        {
            editOutcome = 0;
        }

        if (editLoadedFor != me.UserId)
        {
            editLoadedFor = me.UserId;
            editIntro = me.Intro;
            editPronouns = me.Pronouns;
            editVibe = me.Dynamic;
            editTags = VelvetTags.Join(me.Tags);
            editLimits = VelvetTags.Join(me.Limits);
            editLookingFor = me.LookingFor;
            editRelationship = me.RelationshipStatus;
            editDiscoverable = me.Discoverable;
        }

        if (ui.HeaderAction(area, editBusy ? Loc.T(L.Velvet.Saving) : Loc.T(L.Velvet.Save), !editBusy))
        {
            SaveProfile();
        }

        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, 8f * scale));
            DrawAvatarEditor(me);
            ImGui.Dummy(new Vector2(0f, 14f * scale));
            VelvetUi.SectionLabel(Loc.T(L.Velvet.AboutHeader));
            ui.Field(Loc.T(L.Velvet.IntroLabel), "##vIntro", ref editIntro, IntroMax, true);
            ImGui.Dummy(new Vector2(0f, 10f * scale));
            ui.Field(Loc.T(L.Velvet.PronounsLabel), "##vPronouns", ref editPronouns, ShortFieldMax, false);

            ImGui.Dummy(new Vector2(0f, 16f * scale));
            VelvetUi.SectionLabel(Loc.T(L.Velvet.DynamicLabel));
            ui.Field(Loc.T(L.Velvet.DynamicLabel), "##vVibe", ref editVibe, ShortFieldMax, false);
            DrawSuggestionRow(VibeSuggestions, ref editVibe, false);

            ImGui.Dummy(new Vector2(0f, 8f * scale));
            ui.Field(Loc.T(L.Velvet.TagsLabel), "##vTags", ref editTags, TagsMax, false);
            DrawSuggestionRow(TagSuggestions, ref editTags, true);

            ImGui.Dummy(new Vector2(0f, 16f * scale));
            VelvetUi.SectionLabel(Loc.T(L.Velvet.WantHeader));
            DrawChipPicker(Loc.T(L.Velvet.LookingForLabel), VelvetLookingFor.All, ref editLookingFor, true);
            ImGui.Dummy(new Vector2(0f, 10f * scale));
            DrawChipPicker(Loc.T(L.Velvet.RelationshipLabel), VelvetRelationship.All, ref editRelationship, false);

            ImGui.Dummy(new Vector2(0f, 12f * scale));
            ui.Field(Loc.T(L.Velvet.LimitsLabel), "##vLimits", ref editLimits, TagsMax, false);

            ImGui.Dummy(new Vector2(0f, 30f * scale));
        }
    }

    private void SaveProfile()
    {
        if (editBusy)
        {
            return;
        }

        editBusy = true;
        var request = new UpdateVelvetProfileRequest(
            editIntro.Trim(),
            editPronouns.Trim(),
            editVibe.Trim(),
            VelvetTags.Parse(editTags),
            VelvetTags.Parse(editLimits),
            editLookingFor,
            editRelationship,
            editDiscoverable);
        store.UpdateProfile(request, ok =>
        {
            editBusy = false;
            editOutcome = ok ? 1 : 2;
        });
    }

    private void DrawAvatarEditor(VelvetProfileDto me)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var radius = 42f * scale;
        var center = new Vector2(origin.X + width * 0.5f, origin.Y + radius);
        AvatarView.Draw(ImGui.GetWindowDrawList(), center, radius, Accent, MonogramFor(me), 1.4f, AvatarFor(me), 56);

        var badge = new Vector2(center.X + radius - 8f * scale, center.Y + radius - 8f * scale);
        ImGui.GetWindowDrawList().AddCircleFilled(badge, 13f * scale, ImGui.GetColorU32(Accent), 20);
        ImGui.GetWindowDrawList().AddCircle(badge, 13f * scale, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.85f)), 20, 1.5f * scale);
        VelvetUi.Icon(badge, FontAwesomeIcon.Camera.ToIconString(), new Vector4(1f, 1f, 1f, 1f), 0.72f);

        if (VelvetUi.HoverClick(center - new Vector2(radius, radius), center + new Vector2(radius, radius)))
        {
            OpenAvatarPicker();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, radius * 2f + 10f * scale));

        var buttonWidth = 170f * scale;
        var buttonHeight = 34f * scale;
        var buttonMin = new Vector2(center.X - buttonWidth * 0.5f, ImGui.GetCursorScreenPos().Y);
        if (ui.GhostButton(new Rect(buttonMin, new Vector2(buttonMin.X + buttonWidth, buttonMin.Y + buttonHeight)), Loc.T(L.Velvet.ChangePhoto)))
        {
            OpenAvatarPicker();
        }

        ImGui.Dummy(new Vector2(width, buttonHeight));
    }

    private void OpenAvatarPicker()
    {
        avatar.Open();
        router.Push(VelvetRoute.Avatar);
    }

    private void DrawAvatar(Rect area)
    {
        if (avatar.Draw(area, ui, new PhoneContext(area, theme, navigation)))
        {
            router.Pop();
        }
    }

    private void DrawChipPicker(string label, int[] values, ref int selected, bool skipFirst)
    {
        var scale = ImGuiHelpers.GlobalScale;
        using (ImRaii.PushColor(ImGuiCol.Text, VelvetUi.MutedInk))
        {
            ImGui.TextUnformatted(label);
        }

        var origin = ImGui.GetCursorScreenPos();
        var cursorX = origin.X;
        var cursorY = origin.Y + 2f * scale;
        var maxX = origin.X + ImGui.GetContentRegionAvail().X;
        var chipHeight = 30f * scale;
        for (var index = skipFirst ? 1 : 0; index < values.Length; index++)
        {
            var value = values[index];
            var text = label == Loc.T(L.Velvet.RelationshipLabel) ? VelvetRelationship.Label(value) : VelvetLookingFor.Label(value);
            var chipWidth = Typography.Measure(text, 0.85f, FontWeight.Medium).X + 22f * scale;
            if (cursorX + chipWidth > maxX)
            {
                cursorX = origin.X;
                cursorY += chipHeight + 6f * scale;
            }

            var rect = new Rect(new Vector2(cursorX, cursorY), new Vector2(cursorX + chipWidth, cursorY + chipHeight));
            if (ui.Chip(rect, text, selected == value))
            {
                selected = value;
            }

            cursorX += chipWidth + 6f * scale;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, cursorY - origin.Y + chipHeight + 4f * scale));
    }

    private void DrawSuggestionRow(string[] suggestions, ref string field, bool commaSeparated)
    {
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.Dummy(new Vector2(0f, 4f * scale));
        VelvetUi.HelpText(Loc.T(L.Velvet.Suggestions));
        var origin = ImGui.GetCursorScreenPos();
        DrawSuggestionChips(origin, ImGui.GetContentRegionAvail().X, suggestions, ref field, commaSeparated);
    }

    private void DrawSuggestionChips(Vector2 origin, float maxWidth, string[] suggestions, ref string field, bool commaSeparated = true)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var cursorX = origin.X;
        var cursorY = origin.Y + 2f * scale;
        var chipHeight = 28f * scale;
        for (var index = 0; index < suggestions.Length; index++)
        {
            var tag = suggestions[index];
            var chipWidth = Typography.Measure(tag, 0.8f).X + 20f * scale;
            if (cursorX + chipWidth > origin.X + maxWidth)
            {
                cursorX = origin.X;
                cursorY += chipHeight + 6f * scale;
            }

            var rect = new Rect(new Vector2(cursorX, cursorY), new Vector2(cursorX + chipWidth, cursorY + chipHeight));
            var present = field.Contains(tag, StringComparison.OrdinalIgnoreCase);
            if (ui.Chip(rect, tag, present) && !present)
            {
                field = AppendToken(field, tag, commaSeparated);
            }

            cursorX += chipWidth + 6f * scale;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(maxWidth, cursorY - origin.Y + chipHeight + 4f * scale));
    }

    private static string AppendToken(string field, string token, bool commaSeparated)
    {
        var trimmed = field.Trim();
        if (!commaSeparated)
        {
            return token;
        }

        if (trimmed.Length == 0)
        {
            return token;
        }

        return trimmed.TrimEnd(',') + ", " + token;
    }

    private void DrawInlineField(string id, ref string value, int maxLength, bool multiline, Rect rect, string hint)
    {
        var scale = ImGuiHelpers.GlobalScale;
        Squircle.Fill(ImGui.GetWindowDrawList(), rect.Min, new Vector2(rect.Max.X, rect.Max.Y), 9f * scale, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)));
        ImGui.SetCursorScreenPos(new Vector2(rect.Min.X + 12f * scale, rect.Min.Y + (multiline ? 8f * scale : rect.Height * 0.5f - ImGui.GetFrameHeight() * 0.5f)));
        ImGui.SetNextItemWidth(rect.Width - 24f * scale);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, VelvetUi.TitleInk))
        {
            if (multiline)
            {
                ImGui.InputTextMultiline(id, ref value, maxLength, new Vector2(rect.Width - 24f * scale, rect.Height - 16f * scale), ImGuiInputTextFlags.None);
            }
            else
            {
                ImGui.InputTextWithHint(id, hint, ref value, maxLength, ImGuiInputTextFlags.None);
            }
        }
    }

    private void DrawTagChips(string[] tags)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var cursorX = origin.X;
        var cursorY = origin.Y + 2f * scale;
        var maxX = origin.X + ImGui.GetContentRegionAvail().X;
        var chipHeight = 24f * scale;
        var drawList = ImGui.GetWindowDrawList();
        for (var index = 0; index < tags.Length; index++)
        {
            var label = tags[index];
            var width = Typography.Measure(label, 0.8f).X + 18f * scale;
            if (cursorX + width > maxX)
            {
                cursorX = origin.X;
                cursorY += chipHeight + 6f * scale;
            }

            var chipMin = new Vector2(cursorX, cursorY);
            var chipMax = new Vector2(cursorX + width, cursorY + chipHeight);
            Squircle.Fill(drawList, chipMin, chipMax, chipHeight * 0.5f, ImGui.GetColorU32(Palette.WithAlpha(Accent, 0.18f)));
            Typography.DrawCentered(new Vector2((chipMin.X + chipMax.X) * 0.5f, (chipMin.Y + chipMax.Y) * 0.5f), label, new Vector4(0.99f, 0.80f, 0.88f, 1f), 0.8f, FontWeight.Medium);
            cursorX += width + 6f * scale;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, cursorY - origin.Y + chipHeight + 4f * scale));
    }

    private void DrawTagsLine(Vector2 position, string[] tags)
    {
        if (tags.Length == 0)
        {
            return;
        }

        var text = "#" + string.Join(" #", tags);
        Typography.Draw(position, VelvetUi.Truncate(text, 40), Palette.Mix(Accent, theme.TextStrong, 0.3f), 0.78f, FontWeight.Medium);
    }

    private void DrawFullScreenMessage(Rect area, string message)
    {
        var drawList = ImGui.GetWindowDrawList();
        var screen = SceneChrome.ScreenFrom(area, theme, ImGuiHelpers.GlobalScale);
        ui.Backdrop(screen);
        Typography.DrawCentered(area.Center, message, VelvetUi.BodyInk);
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
                return string.IsNullOrEmpty(threads[index].OtherDisplayName) ? threads[index].OtherHandle : threads[index].OtherDisplayName;
            }
        }

        var connections = store.Connections;
        for (var index = 0; index < connections.Length; index++)
        {
            if (connections[index].UserId == threadId)
            {
                return string.IsNullOrEmpty(connections[index].DisplayName) ? connections[index].Handle : connections[index].DisplayName;
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
