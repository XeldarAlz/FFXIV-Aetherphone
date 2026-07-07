using System.Numerics;
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
using Aetherphone.Core.Messaging;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Platform;
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
    private const float FeedRefreshSeconds = 25f;
    private const int MaxCaptionLength = 500;
    private const int MaxCommentLength = 500;
    private const int DisplayNameMax = 40;
    private const int HandleMax = 15;
    private const int BioMax = 200;
    private const float BottomNavHeight = 52f;
    private const float CropSmoothTime = 0.10f;
    private const int GridColumns = 3;
    private const float LikeBurstDuration = 0.9f;
    private const int MaxReportReasonLength = 200;
    private static readonly Vector4 LikeRed = new(0.95f, 0.27f, 0.36f, 1f);

    public string Id => "aethergram";
    public Vector4 Accent => AppAccents.For(Id);
    public string DisplayName => Loc.T(L.Apps.Aethergram);
    public string Glyph => "Ag";
    public int BadgeCount => 0;
    private readonly AethergramStore store;
    private readonly SocialLauncher launcher;
    private readonly GameData gameData;
    private readonly Configuration configuration;
    private readonly LodestoneService lodestone;
    private readonly PhotoLibrary library;
    private readonly RemoteImageCache images;
    private readonly PhotoViewerOverlay photoViewer = new();
    private string? pendingViewUrl;
    private double pendingViewAt;
    private readonly AppSkin ui = new(AppPalettes.Aethergram);
    private readonly ViewRouter<AethergramRoute> router;
    private readonly RouterDraw<AethergramRoute> drawView;
    private readonly Action back;
    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private SocialFeedScope activeScope = SocialFeedScope.ForYou;
    private float tabSegmentAnim;
    private float sinceForYou;
    private float sinceFollowing;
    private bool commentFocusPending;
    private ComposeStage composeStage = ComposeStage.Pick;
    private bool composeAvatarMode;
    private string composeSourcePath = string.Empty;
    private string caption = string.Empty;
    private bool captionFocus;
    private string composeStatus = string.Empty;
    private volatile int composeOutcome;
    private string[] pickerPaths = Array.Empty<string>();
    private string? pendingPickedPath;
    private Spring zoomSpring = new(1f);
    private Spring centerXSpring = new(0.5f);
    private Spring centerYSpring = new(0.5f);
    private float targetZoom = 1f;
    private float targetCenterX = 0.5f;
    private float targetCenterY = 0.5f;
    private bool cropDragging;
    private Vector2 cropLastDrag;
    private string commentDraft = string.Empty;
    private string searchDraft = string.Empty;
    private string likeBurstPostId = string.Empty;
    private double likeBurstStart;
    private string editDisplay = string.Empty;
    private string editHandle = string.Empty;
    private string editBio = string.Empty;
    private string editStatus = string.Empty;
    private string? editLoadedFor;
    private volatile bool editBusy;
    private volatile int editOutcome;
    private string? reportTargetType;
    private string? reportTargetId;
    private string reportReasonDraft = string.Empty;
    private string reportStatus = string.Empty;
    private volatile bool reportSubmitting;
    private bool deferredTooltipActive;
    private Vector2 deferredTooltipCenter;
    private float deferredTooltipRadius;
    private string deferredTooltipText = string.Empty;

    public AethergramApp(AethernetSession session, AethernetClient client, LodestoneService lodestone,
        RemoteImageCache images, PhotoLibrary library, SocialLauncher launcher, GameData gameData,
        Configuration configuration)
    {
        store = new AethergramStore(session, client);
        this.launcher = launcher;
        this.gameData = gameData;
        this.configuration = configuration;
        this.lodestone = lodestone;
        this.library = library;
        this.images = images;
        router = new ViewRouter<AethergramRoute>(AethergramRoute.Home, Id);
        drawView = DrawView;
        back = () => router.Pop();
    }

    private enum ComposeStage
    {
        Pick,
        Crop,
        Caption,
    }

    public void OnOpened()
    {
        router.Reset();
        if (store.IsSignedIn)
        {
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
                OpenDetailFromLink(link.Id);
            }
        }
    }

    public void OnClosed()
    {
        router.Reset();
        caption = string.Empty;
        searchDraft = string.Empty;
        commentDraft = string.Empty;
        reportTargetType = null;
        reportTargetId = null;
        reportReasonDraft = string.Empty;
        reportStatus = string.Empty;
        store.ClearDiscover();
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = theme;
        var screen = SceneChrome.ScreenFrom(context.Content, theme, ImGuiHelpers.GlobalScale);
        ui.Backdrop(screen);
        AdvancePendingPhotoView();
        if (photoViewer.Active)
        {
            photoViewer.Draw(screen, theme);
            return;
        }

        router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
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
                DrawEditProfile(area);
                break;
            case AethergramScreen.Discover:
                DrawDiscover(area);
                break;
            case AethergramScreen.UserList:
                DrawUserList(area, route.Id!, route.Kind);
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
            var body = new Rect(new Vector2(area.Min.X, top), area.Max);
            Typography.DrawCentered(body.Center, Loc.T(L.Aethergram.SetUpAccount), AppPalettes.Aethergram.MutedInk);
            return;
        }

        var segmentHeight = 38f * scale;
        var tabsRect = new Rect(new Vector2(area.Min.X + 16f * scale, top + 2f * scale),
            new Vector2(area.Max.X - 16f * scale, top + 2f * scale + segmentHeight));
        var selected = SegmentSlider.Draw(tabsRect, Loc.T(L.Aethergram.ForYou), Loc.T(L.Aethergram.Following),
            (int)activeScope, ref tabSegmentAnim, Accent, AppPalettes.Aethergram.MutedInk);
        if (selected != (int)activeScope)
        {
            activeScope = (SocialFeedScope)selected;
            EnsureLoaded(activeScope);
        }

        sinceForYou += ImGui.GetIO().DeltaTime;
        sinceFollowing += ImGui.GetIO().DeltaTime;
        TickRefresh(activeScope);
        var navRect = new Rect(new Vector2(area.Min.X, area.Max.Y - BottomNavHeight * scale), area.Max);
        var listRect = new Rect(new Vector2(area.Min.X, tabsRect.Max.Y + 6f * scale),
            new Vector2(area.Max.X, navRect.Min.Y));
        DrawBottomNav(navRect);
        DrawFeedList(listRect, activeScope);
        FlushDeferredTooltip();
    }

    private void DrawFeedList(Rect listRect, SocialFeedScope scope)
    {
        var snapshot = store.Feed(scope);
        using (AppSurface.Begin(listRect))
        {
            if (snapshot.Length == 0)
            {
                var message = store.IsLoading(scope) ? Loc.T(L.Common.Loading) :
                    scope == SocialFeedScope.Following ? Loc.T(L.Aethergram.FollowingEmpty) :
                    Loc.T(L.Aethergram.ExploreEmpty);
                Typography.DrawCentered(new Vector2(listRect.Center.X, listRect.Min.Y + 90f * ImGuiHelpers.GlobalScale),
                    message, AppPalettes.Aethergram.MutedInk);
            }
            else
            {
                ImGui.Dummy(new Vector2(0f, 4f * ImGuiHelpers.GlobalScale));
                for (var index = 0; index < snapshot.Length; index++)
                {
                    DrawGramCard(snapshot[index]);
                }

                ImGui.Dummy(new Vector2(0f, 16f * ImGuiHelpers.GlobalScale));
            }
        }
    }

    private void DrawGramCard(PostDto post)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = 14f * scale;
        var innerX = origin.X + pad;
        var innerWidth = width - pad * 2f;
        var displayName = SocialIdentity.Name(post.AuthorDisplayName, post.AuthorHandle);
        var headerBlock = 40f * scale;
        var avatarRadius = 18f * scale;
        var imageTop = origin.Y + pad + headerBlock + 12f * scale;
        var imageBottom = imageTop + innerWidth;
        var actionsTop = imageBottom + 12f * scale;
        var actionsHeight = 22f * scale;
        var textTop = actionsTop + actionsHeight + 10f * scale;
        var captionHeight = post.Text.Length > 0 ? Typography.MeasureWrapped(post.Text, innerWidth, 0.9f) + 6f * scale : 0f;
        var commentsHeight = post.CommentCount > 0 ? 20f * scale : 0f;
        var cardBottom = textTop + captionHeight + commentsHeight + pad;
        ui.Card(drawList, origin, new Vector2(origin.X + width, cardBottom), 18f * scale);
        var avatarCenter = new Vector2(innerX + avatarRadius, origin.Y + pad + avatarRadius);
        AethergramArt.StoryRing(drawList, avatarCenter, avatarRadius + 3f * scale, scale);
        DrawAvatar(avatarCenter, avatarRadius - 1f * scale, post.AuthorName, post.AuthorWorld, post.AuthorAvatarUrl,
            0.85f, 32);
        var nameLeft = avatarCenter.X + avatarRadius + 12f * scale;
        Typography.Draw(new Vector2(nameLeft, origin.Y + pad + 2f * scale), displayName, theme.TextStrong, 0.92f,
            FontWeight.SemiBold);
        var subline = SocialIdentity.FeedMeta(post.AuthorHandle, TimeText.Short(post.CreatedAtUnix));
        Typography.Draw(new Vector2(nameLeft, origin.Y + pad + 21f * scale), subline, AppPalettes.Aethergram.MutedInk, 0.76f);
        if (UiInteract.HoverClick(new Vector2(innerX, origin.Y + pad),
                new Vector2(origin.X + width - pad - 30f * scale, origin.Y + pad + headerBlock)))
        {
            OpenProfile(post.AuthorId);
        }

        var moreCenter = new Vector2(origin.X + width - pad - 4f * scale, avatarCenter.Y);
        if (ui.IconButton(moreCenter, 12f * scale, FontAwesomeIcon.EllipsisH.ToIconString(), AppPalettes.Aethergram.BodyInk,
                AppSkin.Transparent, 0.85f, Loc.T(L.Aethergram.More)))
        {
            OpenDetail(post, false);
        }

        var imageRect = new Rect(new Vector2(innerX, imageTop), new Vector2(innerX + innerWidth, imageBottom));
        DrawGramImage(imageRect, post.MediaUrl, 14f * scale);
        HandleLikeGesture(imageRect, post);
        DrawLikeBurst(imageRect, post.Id);
        var liked = post.MyReaction >= 0;
        var actionCenterY = actionsTop + actionsHeight * 0.5f;
        var heartCenter = new Vector2(innerX + 12f * scale, actionCenterY);
        if (ui.IconButton(heartCenter, 14f * scale, FontAwesomeIcon.Heart.ToIconString(),
                liked ? LikeRed : AppPalettes.Aethergram.BodyInk, AppSkin.Transparent, 1.15f, Loc.T(L.Aethergram.Like)))
        {
            store.ToggleLike(post);
        }

        var cursorX = heartCenter.X + 18f * scale;
        if (post.TotalReactions > 0)
        {
            var likeText = post.TotalReactions.ToString(Loc.Culture);
            Typography.Draw(new Vector2(cursorX, actionCenterY - 7f * scale), likeText, AppPalettes.Aethergram.BodyInk, 0.82f,
                FontWeight.Medium);
            cursorX += Typography.Measure(likeText, 0.82f, FontWeight.Medium).X + 14f * scale;
        }
        else
        {
            cursorX += 6f * scale;
        }

        var commentCenter = new Vector2(cursorX + 12f * scale, actionCenterY);
        if (ui.IconButton(commentCenter, 14f * scale, FontAwesomeIcon.Comment.ToIconString(), AppPalettes.Aethergram.BodyInk,
                AppSkin.Transparent, 1.05f, Loc.T(L.Aethergram.Comment)))
        {
            OpenDetail(post, true);
        }

        if (post.CommentCount > 0)
        {
            Typography.Draw(new Vector2(commentCenter.X + 18f * scale, actionCenterY - 7f * scale),
                post.CommentCount.ToString(Loc.Culture), AppPalettes.Aethergram.BodyInk, 0.82f, FontWeight.Medium);
        }

        var y = textTop;
        if (post.Text.Length > 0)
        {
            ImGui.SetCursorScreenPos(new Vector2(innerX, y));
            ImGui.PushTextWrapPos(innerX + innerWidth - ImGui.GetWindowPos().X);
            using (ImRaii.PushColor(ImGuiCol.Text, AppPalettes.Aethergram.BodyInk))
            using (Plugin.Fonts.Push(0.9f))
            {
                ImGui.TextWrapped(post.Text);
            }

            ImGui.PopTextWrapPos();
            y += captionHeight;
        }

        if (post.CommentCount > 0)
        {
            var commentsLabel = Loc.T(L.Aethergram.ViewComments, post.CommentCount);
            var labelPos = new Vector2(innerX, y + 2f * scale);
            Typography.Draw(labelPos, commentsLabel, AppPalettes.Aethergram.MutedInk, 0.82f);
            var labelSize = Typography.Measure(commentsLabel, 0.82f);
            if (UiInteract.HoverClick(labelPos, labelPos + labelSize))
            {
                OpenDetail(post, false);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cardBottom - origin.Y + 12f * scale));
    }

    private void HandleLikeGesture(Rect imageRect, PostDto post)
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

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !string.IsNullOrEmpty(post.MediaUrl))
        {
            pendingViewUrl = post.MediaUrl;
            pendingViewAt = ImGui.GetTime();
        }
    }

    private void AdvancePendingPhotoView()
    {
        if (pendingViewUrl is not { } url || ImGui.GetTime() - pendingViewAt < 0.30)
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

    private void DrawGramImage(Rect rect, string? url, float rounding)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var texture = images.Get(url);
        if (texture is null)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(AppPalettes.Aethergram.FieldSurface));
            Typography.DrawCentered(rect.Center,
                Loc.T(images.Failed(url) ? L.Aethergram.ImageFailed : L.Common.Loading), AppPalettes.Aethergram.MutedInk, 0.85f);
            return;
        }

        var (uv0, uv1) = ImageFit.CoverSquare(texture.Size);
        drawList.AddImageRounded(texture.Handle, rect.Min, rect.Max, uv0, uv1, 0xFFFFFFFFu, rounding,
            ImDrawFlags.RoundCornersAll);
    }

    private void DrawBottomNav(Rect bar)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddLine(bar.Min, new Vector2(bar.Max.X, bar.Min.Y), ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)),
            1f);
        deferredTooltipActive = false;
        deferredTooltipText = string.Empty;
        var slot = bar.Width / 4f;
        var centerY = bar.Center.Y;
        var none = AppSkin.Transparent;
        var hitRadius = 17f * scale;
        var homeCenter = new Vector2(bar.Min.X + slot * 0.5f, centerY);
        if (ui.IconButton(homeCenter, hitRadius, FontAwesomeIcon.Home.ToIconString(), AppPalettes.Aethergram.TitleInk, none,
                1.15f))
        {
            store.RefreshFeed(activeScope);
        }

        var searchCenter = new Vector2(bar.Min.X + slot * 1.5f, centerY);
        if (ui.IconButton(searchCenter, hitRadius, FontAwesomeIcon.Search.ToIconString(), AppPalettes.Aethergram.MutedInk, none,
                1.1f))
        {
            store.ClearDiscover();
            searchDraft = string.Empty;
            router.Push(AethergramRoute.Discover);
        }

        var postCenter = new Vector2(bar.Min.X + slot * 2.5f, centerY);
        if (ui.IconButton(postCenter, hitRadius, FontAwesomeIcon.PlusSquare.ToIconString(), AppPalettes.Aethergram.MutedInk,
                none, 1.2f))
        {
            StartCompose(false);
        }

        var profileCenter = new Vector2(bar.Min.X + slot * 3.5f, centerY);
        if (store.Me is { } me)
        {
            var radius = 13f * scale;
            var profileMin = profileCenter - new Vector2(radius, radius);
            var profileMax = profileCenter + new Vector2(radius, radius);
            var hovered = ImGui.IsMouseHoveringRect(profileMin, profileMax);
            DrawAvatar(profileCenter, radius, me.Name, me.World, me.AvatarUrl, 0.8f, 24);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                DeferTooltip(profileCenter, radius, Loc.T(L.Aethergram.Profile));
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    OpenProfile(me.Id);
                }
            }
        }
        else
        {
            store.EnsureMe();
            ui.IconButton(profileCenter, hitRadius, FontAwesomeIcon.User.ToIconString(), AppPalettes.Aethergram.MutedInk,
                AppSkin.Transparent, 1.05f);
        }
    }

    private void DrawAvatar(Vector2 center, float radius, string name, string world, string? avatarUrl,
        float monogramScale, int segments)
    {
        AvatarView.DrawRemote(ImGui.GetWindowDrawList(), center, radius, theme, name, world, avatarUrl, images,
            lodestone, monogramScale, segments);
    }

    private void DrawField(string label, string id, ref string value, int maxLength, bool multiline)
    {
        ui.Field(label, id, ref value, maxLength, multiline);
    }

    private void DrawHandleField()
    {
        var scale = ImGuiHelpers.GlobalScale;
        using (ImRaii.PushColor(ImGuiCol.Text, AppPalettes.Aethergram.MutedInk))
        {
            ImGui.TextUnformatted(Loc.T(L.Aethergram.HandleLabel));
        }

        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = 34f * scale;
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, origin, new Vector2(origin.X + width, origin.Y + height), 9f * scale,
            ImGui.GetColorU32(AppPalettes.Aethergram.FieldSurface));
        Typography.Draw(new Vector2(origin.X + 12f * scale, origin.Y + height * 0.5f - 8f * scale), "@",
            AppPalettes.Aethergram.MutedInk, 1f);
        ImGui.SetCursorScreenPos(new Vector2(origin.X + 26f * scale,
            origin.Y + height * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(width - 38f * scale);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, IsHandleValid(editHandle) ? theme.TextStrong : theme.Danger))
        {
            if (ImGui.InputText("##editHandle", ref editHandle, HandleMax, ImGuiInputTextFlags.CharsNoBlank))
            {
                editHandle = editHandle.ToLowerInvariant();
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
        Typography.Draw(new Vector2(origin.X + 2f * scale, origin.Y + height + 3f * scale),
            Loc.T(L.Aethergram.HandleRules), AppPalettes.Aethergram.MutedInk, 0.78f);
        ImGui.Dummy(new Vector2(width, 16f * scale));
    }

    private void OpenProfile(string userId)
    {
        store.OpenProfile(userId);
        router.Push(AethergramRoute.Profile(userId));
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

    private void EnsureLoaded(SocialFeedScope scope)
    {
        if (store.Feed(scope).Length == 0 && !store.IsLoading(scope))
        {
            store.RefreshFeed(scope);
        }
    }

    private void TickRefresh(SocialFeedScope scope)
    {
        if (store.IsLoading(scope))
        {
            return;
        }

        if (scope == SocialFeedScope.ForYou && sinceForYou >= FeedRefreshSeconds)
        {
            sinceForYou = 0f;
            store.RefreshFeed(scope);
        }
        else if (scope == SocialFeedScope.Following && sinceFollowing >= FeedRefreshSeconds)
        {
            sinceFollowing = 0f;
            store.RefreshFeed(scope);
        }
    }

    private static bool IsHandleValid(string handle)
    {
        var value = handle.Trim();
        if (value.Length < 3 || value.Length > HandleMax)
        {
            return false;
        }

        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            var ok = character is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_';
            if (!ok)
            {
                return false;
            }
        }

        return true;
    }

    private static string PostsLabel(int count)
    {
        var plural = Loc.Plural(L.Aethergram.Posts, count);
        var parts = plural.Split(' ', 2);
        return parts.Length > 1 ? parts[1] : plural;
    }

    private static string FollowersLabel(int count)
    {
        var plural = Loc.Plural(L.Account.Followers, count);
        var parts = plural.Split(' ', 2);
        return parts.Length > 1 ? parts[1] : plural;
    }

    public void Dispose()
    {
        store.Dispose();
    }
}
