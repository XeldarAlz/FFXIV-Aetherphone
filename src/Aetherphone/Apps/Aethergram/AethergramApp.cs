using System.Numerics;
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
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Aethergram;

internal sealed class AethergramApp : IPhoneApp
{
    private const float FeedRefreshSeconds = 25f;
    private const int MaxCaptionLength = 500;
    private const int MaxCommentLength = 500;
    private const int DisplayNameMax = 40;
    private const int HandleMax = 15;
    private const int BioMax = 200;
    private const float TabsHeight = 40f;
    private const float BottomNavHeight = 52f;
    private const float CropSmoothTime = 0.10f;
    private const int GridColumns = 3;
    private const float LikeBurstDuration = 0.9f;
    private const int MaxReportReasonLength = 200;

    private static readonly Vector4 LikeRed = new(0.95f, 0.27f, 0.36f, 1f);

    private static readonly Vector4[] RingStops =
    [
        new(1f, 0.863f, 0.502f, 1f),
        new(0.969f, 0.435f, 0.216f, 1f),
        new(0.882f, 0.188f, 0.424f, 1f),
        new(0.514f, 0.227f, 0.706f, 1f),
    ];

    public string Id => "aethergram";

    public string DisplayName => Loc.T(L.Apps.Aethergram);

    public string Glyph => "Ag";

    public Vector4 Accent => new(0.78f, 0.23f, 0.58f, 1f);

    public int BadgeCount => 0;

    private readonly AethergramStore store;
    private readonly LodestoneService lodestone;
    private readonly PhotoLibrary library;
    private readonly RemoteImageCache images;

    private readonly ViewRouter<AethergramRoute> router;
    private readonly RouterDraw<AethergramRoute> drawView;
    private readonly Action back;

    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;

    private AethergramFeedScope activeScope = AethergramFeedScope.ForYou;
    private float sinceForYou;
    private float sinceFollowing;

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

    private string? deleteTargetId;
    private string deleteStatus = string.Empty;
    private volatile bool deleteSubmitting;

    public AethergramApp(AethernetSession session, AethernetClient client, LodestoneService lodestone, HttpService http, PhotoLibrary library)
    {
        store = new AethergramStore(session, client);
        this.lodestone = lodestone;
        this.library = library;
        images = new RemoteImageCache(http);

        router = new ViewRouter<AethergramRoute>(AethergramRoute.Home);
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
            store.RefreshFeed(AethergramFeedScope.ForYou);
            store.RefreshFeed(AethergramFeedScope.Following);
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
        deleteTargetId = null;
        deleteStatus = string.Empty;
        store.ClearDiscover();
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        router.Draw(context.Content, context.Theme.AppBackground, ImGui.GetIO().DeltaTime, drawView);
    }

    private void DrawView(AethergramRoute route, Rect area, int depth)
    {
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
            Typography.DrawCentered(body.Center, Loc.T(L.Aethergram.SetUpAccount), theme.TextMuted);
            return;
        }

        var tabsRect = new Rect(new Vector2(area.Min.X, top), new Vector2(area.Max.X, top + TabsHeight * scale));
        var tabs = new[] { Loc.T(L.Aethergram.ForYou), Loc.T(L.Aethergram.Following) };
        var selected = ChirperTabsProxy(tabsRect, tabs);
        if (selected != (int)activeScope)
        {
            activeScope = (AethergramFeedScope)selected;
            EnsureLoaded(activeScope);
        }

        sinceForYou += ImGui.GetIO().DeltaTime;
        sinceFollowing += ImGui.GetIO().DeltaTime;
        TickRefresh(activeScope);

        var navRect = new Rect(new Vector2(area.Min.X, area.Max.Y - BottomNavHeight * scale), area.Max);
        var listRect = new Rect(new Vector2(area.Min.X, tabsRect.Max.Y), new Vector2(area.Max.X, navRect.Min.Y));
        DrawFeedList(listRect, activeScope);
        DrawBottomNav(navRect);
    }

    private int ChirperTabsProxy(Rect tabsRect, string[] tabs)
    {
        return Aetherphone.Apps.Chirper.ChirperTabs.Draw("aethergram.tabs", tabsRect, tabs, (int)activeScope, theme, Accent);
    }

    private void DrawFeedList(Rect listRect, AethergramFeedScope scope)
    {
        var snapshot = store.Feed(scope);
        using (AppSurface.Begin(listRect))
        {
            if (snapshot.Length == 0)
            {
                var message = store.IsLoading(scope)
                    ? Loc.T(L.Common.Loading)
                    : scope == AethergramFeedScope.Following ? Loc.T(L.Aethergram.FollowingEmpty) : Loc.T(L.Aethergram.ExploreEmpty);
                Typography.DrawCentered(new Vector2(listRect.Center.X, listRect.Min.Y + 90f * ImGuiHelpers.GlobalScale), message, theme.TextMuted);
            }
            else
            {
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
        var bleed = 16f * scale;

        var headerHeight = 56f * scale;
        var avatarRadius = 17f * scale;
        var avatarCenter = new Vector2(origin.X + avatarRadius + 4f * scale, origin.Y + headerHeight * 0.5f);
        DrawStoryRing(drawList, avatarCenter, avatarRadius + 4f * scale, scale);
        DrawAvatar(avatarCenter, avatarRadius, post.AuthorName, post.AuthorWorld, post.AuthorAvatarUrl, 0.85f, 28);
        if (HoverClick(avatarCenter - new Vector2(avatarRadius, avatarRadius), avatarCenter + new Vector2(avatarRadius, avatarRadius)))
        {
            OpenProfile(post.AuthorId);
        }

        var nameLeft = avatarCenter.X + avatarRadius + 12f * scale;
        var displayName = string.IsNullOrEmpty(post.AuthorDisplayName) ? post.AuthorName : post.AuthorDisplayName;
        Typography.Draw(new Vector2(nameLeft, avatarCenter.Y - 16f * scale), displayName, theme.TextStrong, 0.92f, FontWeight.SemiBold);
        Typography.Draw(new Vector2(nameLeft, avatarCenter.Y + 1f * scale), post.AuthorWorld, theme.TextMuted, 0.78f);
        if (HoverClick(new Vector2(nameLeft, origin.Y), new Vector2(origin.X + width - 44f * scale, origin.Y + headerHeight)))
        {
            OpenProfile(post.AuthorId);
        }

        var moreCenter = new Vector2(origin.X + width - 10f * scale, avatarCenter.Y);
        if (DrawIconButton(moreCenter, 12f * scale, FontAwesomeIcon.EllipsisH.ToIconString(), theme.TextStrong, new Vector4(0f, 0f, 0f, 0f), 0.85f))
        {
            OpenDetail(post);
        }

        var imageTop = origin.Y + headerHeight;
        var imageSide = width + bleed * 2f;
        var imageRect = new Rect(new Vector2(origin.X - bleed, imageTop), new Vector2(origin.X + width + bleed, imageTop + imageSide));
        DrawGramImage(imageRect, post.MediaUrl, 0f);
        HandleLikeGesture(imageRect, post);
        DrawLikeBurst(imageRect, post.Id);

        var liked = post.MyReaction >= 0;
        var actionsY = imageRect.Max.Y + 24f * scale;
        var heartCenter = new Vector2(origin.X + 12f * scale, actionsY);
        if (DrawIconButton(heartCenter, 16f * scale, FontAwesomeIcon.Heart.ToIconString(), liked ? LikeRed : theme.TextStrong, new Vector4(0f, 0f, 0f, 0f), 1.3f))
        {
            store.ToggleLike(post);
        }

        var commentCenter = new Vector2(heartCenter.X + 44f * scale, actionsY);
        if (DrawIconButton(commentCenter, 16f * scale, FontAwesomeIcon.Comment.ToIconString(), theme.TextStrong, new Vector4(0f, 0f, 0f, 0f), 1.22f))
        {
            OpenDetail(post);
        }

        var textTop = actionsY + 24f * scale;
        ImGui.SetCursorScreenPos(new Vector2(origin.X, textTop));

        if (post.TotalReactions > 0)
        {
            Typography.Draw(new Vector2(origin.X, textTop), Loc.Plural(L.Aethergram.Likes, post.TotalReactions), theme.TextStrong, 0.92f, FontWeight.SemiBold);
            ImGui.SetCursorScreenPos(new Vector2(origin.X, textTop + 20f * scale));
        }

        if (post.Text.Length > 0)
        {
            var captionPos = ImGui.GetCursorScreenPos();
            Typography.Draw(captionPos, displayName, theme.TextStrong, 0.92f, FontWeight.SemiBold);
            var nameWidth = Typography.Measure(displayName, 0.92f, FontWeight.SemiBold).X;
            ImGui.SetCursorScreenPos(new Vector2(captionPos.X + nameWidth + 6f * scale, captionPos.Y));
            ImGui.PushTextWrapPos(origin.X + width);
            using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
            using (Plugin.Fonts.Push(0.92f))
            {
                ImGui.TextWrapped(post.Text);
            }

            ImGui.PopTextWrapPos();
        }

        if (post.CommentCount > 0)
        {
            ImGui.Dummy(new Vector2(0f, 3f * scale));
            var commentsLabel = Loc.T(L.Aethergram.ViewComments, post.CommentCount);
            var labelPos = ImGui.GetCursorScreenPos();
            Typography.Draw(labelPos, commentsLabel, theme.TextMuted, 0.85f);
            var labelSize = Typography.Measure(commentsLabel, 0.85f);
            if (HoverClick(labelPos, labelPos + labelSize))
            {
                OpenDetail(post);
            }

            ImGui.Dummy(labelSize);
        }

        ImGui.Dummy(new Vector2(0f, 1f * scale));
        Typography.Draw(ImGui.GetCursorScreenPos(), RelativeTime(post.CreatedAtUnix), theme.TextMuted, 0.72f);
        ImGui.Dummy(new Vector2(width, 26f * scale));
    }

    private static void DrawStoryRing(ImDrawListPtr drawList, Vector2 center, float radius, float scale)
    {
        const int Segments = 40;
        var direction = Vector2.Normalize(new Vector2(1f, -1f));
        var step = MathF.Tau / Segments;
        var previous = center + new Vector2(radius, 0f);
        for (var index = 1; index <= Segments; index++)
        {
            var angle = step * index;
            var point = center + radius * new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var normal = Vector2.Normalize((previous + point) * 0.5f - center);
            var position = Vector2.Dot(normal, direction) * 0.5f + 0.5f;
            drawList.AddLine(previous, point, ImGui.GetColorU32(RingColor(position)), 2.4f * scale);
            previous = point;
        }
    }

    private static Vector4 RingColor(float t)
    {
        var position = Math.Clamp(t, 0f, 1f) * (RingStops.Length - 1);
        var index = Math.Min((int)position, RingStops.Length - 2);
        return Vector4.Lerp(RingStops[index], RingStops[index + 1], position - index);
    }

    private void HandleLikeGesture(Rect imageRect, PostDto post)
    {
        if (!ImGui.IsMouseHoveringRect(imageRect.Min, imageRect.Max) || !ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
        {
            return;
        }

        if (post.MyReaction < 0)
        {
            store.ToggleLike(post);
        }

        likeBurstPostId = post.Id;
        likeBurstStart = ImGui.GetTime();
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
        DrawIcon(center + new Vector2(0f, 2f * scale), FontAwesomeIcon.Heart.ToIconString(), new Vector4(0f, 0f, 0f, 0.35f * alpha), 4.5f * pop);
        DrawIcon(center, FontAwesomeIcon.Heart.ToIconString(), new Vector4(1f, 1f, 1f, alpha), 4.4f * pop);
    }

    private void DrawGramImage(Rect rect, string? url, float rounding)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var texture = images.Get(url);
        if (texture is null)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(theme.SurfaceMuted));
            Typography.DrawCentered(rect.Center, Loc.T(images.Failed(url) ? L.Aethergram.ImageFailed : L.Common.Loading), theme.TextMuted, 0.85f);
            return;
        }

        var (uv0, uv1) = CenterCropSquare(texture.Size);
        drawList.AddImageRounded(texture.Handle, rect.Min, rect.Max, uv0, uv1, 0xFFFFFFFFu, rounding, ImDrawFlags.RoundCornersAll);
    }

    private void DrawBottomNav(Rect bar)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(bar.Min, bar.Max, ImGui.GetColorU32(theme.AppBackground));
        drawList.AddLine(bar.Min, new Vector2(bar.Max.X, bar.Min.Y), ImGui.GetColorU32(theme.Separator), 1f);

        var slot = bar.Width / 4f;
        var centerY = bar.Center.Y;
        var none = new Vector4(0f, 0f, 0f, 0f);

        if (DrawIconButton(new Vector2(bar.Min.X + slot * 0.5f, centerY), 17f * scale, FontAwesomeIcon.Home.ToIconString(), theme.TextStrong, none, 1.15f))
        {
            store.RefreshFeed(activeScope);
        }

        if (DrawIconButton(new Vector2(bar.Min.X + slot * 1.5f, centerY), 17f * scale, FontAwesomeIcon.Search.ToIconString(), theme.TextMuted, none, 1.1f))
        {
            store.ClearDiscover();
            searchDraft = string.Empty;
            router.Push(AethergramRoute.Discover);
        }

        if (DrawIconButton(new Vector2(bar.Min.X + slot * 2.5f, centerY), 17f * scale, FontAwesomeIcon.PlusSquare.ToIconString(), theme.TextMuted, none, 1.2f))
        {
            StartCompose(false);
        }

        var profileCenter = new Vector2(bar.Min.X + slot * 3.5f, centerY);
        if (store.Me is { } me)
        {
            var radius = 13f * scale;
            DrawAvatar(profileCenter, radius, me.Name, me.World, me.AvatarUrl, 0.8f, 24);
            if (HoverClick(profileCenter - new Vector2(radius, radius), profileCenter + new Vector2(radius, radius)))
            {
                OpenProfile(me.Id);
            }
        }
        else
        {
            store.EnsureMe();
            DrawIcon(profileCenter, FontAwesomeIcon.User.ToIconString(), theme.TextMuted, 1.05f);
        }
    }

    private void StartCompose(bool avatarMode)
    {
        composeAvatarMode = avatarMode;
        composeStage = ComposeStage.Pick;
        composeSourcePath = string.Empty;
        caption = string.Empty;
        composeStatus = string.Empty;
        pendingPickedPath = null;
        pickerPaths = library.List();
        router.Push(AethergramRoute.Compose);
    }

    private void DrawCompose(Rect area)
    {
        if (composeOutcome == 1)
        {
            composeOutcome = 0;
            composeStatus = string.Empty;
            if (!composeAvatarMode)
            {
                caption = string.Empty;
                sinceForYou = FeedRefreshSeconds;
                sinceFollowing = FeedRefreshSeconds;
            }

            router.Pop();
            return;
        }

        if (composeOutcome == 2)
        {
            composeOutcome = 0;
            composeStatus = Loc.T(L.Account.CannotReach);
        }

        var picked = Interlocked.Exchange(ref pendingPickedPath, null);
        if (!string.IsNullOrEmpty(picked))
        {
            BeginCrop(picked);
        }

        switch (composeStage)
        {
            case ComposeStage.Crop:
                DrawComposeCrop(area);
                break;
            case ComposeStage.Caption:
                DrawComposeCaption(area);
                break;
            default:
                DrawComposePick(area);
                break;
        }
    }

    private void DrawComposePick(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, composeAvatarMode ? Loc.T(L.Aethergram.NewAvatar) : Loc.T(L.Aethergram.NewPost), back);

        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var importHeight = 46f * scale;
        var importRect = new Rect(new Vector2(area.Min.X + 16f * scale, top + 8f * scale), new Vector2(area.Max.X - 16f * scale, top + 8f * scale + importHeight));
        if (DrawPillButton(importRect, Loc.T(L.Aethergram.ImportFromPc), true))
        {
            LaunchFileDialog();
        }

        var gridTop = importRect.Max.Y + 12f * scale;
        var gridRect = new Rect(new Vector2(area.Min.X, gridTop), area.Max);

        using (AppSurface.Begin(gridRect))
        {
            if (pickerPaths.Length == 0)
            {
                Typography.DrawCentered(new Vector2(gridRect.Center.X, gridRect.Min.Y + 60f * scale), Loc.T(L.Photos.NoPhotos), theme.TextMuted);
                return;
            }

            var gap = 6f * scale;
            var cell = (ImGui.GetContentRegionAvail().X - gap * (GridColumns - 1)) / GridColumns;
            using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(gap, gap)))
            {
                for (var index = 0; index < pickerPaths.Length; index++)
                {
                    using (ImRaii.PushId(index))
                    {
                        var clicked = ImGui.InvisibleButton("pick", new Vector2(cell, cell));
                        DrawLocalThumbnail(pickerPaths[index], ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), scale);
                        if (clicked)
                        {
                            BeginCrop(pickerPaths[index]);
                        }
                    }

                    if (index % GridColumns != GridColumns - 1)
                    {
                        ImGui.SameLine();
                    }
                }
            }
        }
    }

    private void DrawLocalThumbnail(string path, Vector2 min, Vector2 max, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rounding = 10f * scale;
        var texture = Plugin.WallpaperImages.Get(path);
        if (texture is null)
        {
            Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(theme.SurfaceMuted));
            return;
        }

        var (uv0, uv1) = CenterCropSquare(texture.Size);
        drawList.AddImageRounded(texture.Handle, min, max, uv0, uv1, 0xFFFFFFFFu, rounding, ImDrawFlags.RoundCornersAll);
        if (ImGui.IsItemHovered())
        {
            drawList.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.1f)), rounding);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
    }

    private void BeginCrop(string path)
    {
        composeSourcePath = path;
        targetZoom = 1f;
        targetCenterX = 0.5f;
        targetCenterY = 0.5f;
        zoomSpring.SnapTo(1f);
        centerXSpring.SnapTo(0.5f);
        centerYSpring.SnapTo(0.5f);
        cropDragging = false;
        composeStage = ComposeStage.Crop;
    }

    private void DrawComposeCrop(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Aethergram.MoveAndScale), () => composeStage = ComposeStage.Pick);

        var canAdvance = !store.Posting;
        var actionLabel = composeAvatarMode ? (store.Posting ? Loc.T(L.Aethergram.Saving) : Loc.T(L.Aethergram.Use)) : Loc.T(L.Aethergram.Next);
        if (DrawHeaderAction(area, actionLabel, canAdvance))
        {
            if (composeAvatarMode)
            {
                CommitAvatar();
            }
            else
            {
                composeStage = ComposeStage.Caption;
                captionFocus = true;
            }
        }

        var scale = ImGuiHelpers.GlobalScale;
        var deltaSeconds = MathF.Min(ImGui.GetIO().DeltaTime, 0.1f);
        var drawList = ImGui.GetWindowDrawList();
        var top = area.Min.Y + AppHeader.Height * scale;

        var stage = new Rect(new Vector2(area.Min.X + 16f * scale, top + 12f * scale), new Vector2(area.Max.X - 16f * scale, area.Max.Y - 96f * scale));
        var side = MathF.Min(stage.Width, stage.Height);
        var preview = new Rect(new Vector2(stage.Center.X - side * 0.5f, stage.Center.Y - side * 0.5f), new Vector2(stage.Center.X + side * 0.5f, stage.Center.Y + side * 0.5f));
        var rounding = 18f * scale;

        var texture = Plugin.WallpaperImages.Get(composeSourcePath);
        if (texture is null)
        {
            Squircle.Fill(drawList, preview.Min, preview.Max, rounding, ImGui.GetColorU32(theme.SurfaceMuted));
            Typography.DrawCentered(preview.Center, Loc.T(L.Common.Loading), theme.TextMuted);
            return;
        }

        var size = texture.Size;
        var zoom = zoomSpring.Step(targetZoom, CropSmoothTime, deltaSeconds);
        var centerX = centerXSpring.Step(targetCenterX, CropSmoothTime, deltaSeconds);
        var centerY = centerYSpring.Step(targetCenterY, CropSmoothTime, deltaSeconds);
        var crop = new WallpaperCrop(zoom, centerX, centerY).Clamped(size, 1f);
        var (uv0, uv1) = crop.ComputeUv(size, 1f);

        drawList.AddImageRounded(texture.Handle, preview.Min, preview.Max, uv0, uv1, 0xFFFFFFFFu, rounding, ImDrawFlags.RoundCornersAll);
        Material.EdgeSquircle(drawList, preview.Min, preview.Max, rounding, scale);
        HandleCropGestures(preview, size, uv1 - uv0);

        Typography.DrawCentered(new Vector2(area.Center.X, area.Max.Y - 70f * scale), Loc.T(L.Aethergram.GestureHint), theme.TextMuted, 0.78f);
        var trackWidth = area.Width * 0.62f;
        var track = new Rect(new Vector2(area.Center.X - trackWidth * 0.5f, area.Max.Y - 48f * scale), new Vector2(area.Center.X + trackWidth * 0.5f, area.Max.Y - 44f * scale));
        var zoomNormalized = (targetZoom - WallpaperCrop.MinZoom) / (WallpaperCrop.MaxZoom - WallpaperCrop.MinZoom);
        var updated = Scrubber.Draw(track, zoomNormalized, theme.Accent, theme.SurfaceMuted, 1f);
        targetZoom = WallpaperCrop.MinZoom + updated * (WallpaperCrop.MaxZoom - WallpaperCrop.MinZoom);
    }

    private void HandleCropGestures(Rect preview, Vector2 size, Vector2 visible)
    {
        var hovering = ImGui.IsMouseHoveringRect(preview.Min, preview.Max);
        if (hovering)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            var wheel = ImGui.GetIO().MouseWheel;
            if (wheel != 0f)
            {
                targetZoom = Math.Clamp(targetZoom * (1f + wheel * 0.12f), WallpaperCrop.MinZoom, WallpaperCrop.MaxZoom);
            }
        }

        if (hovering && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            cropDragging = true;
            cropLastDrag = ImGui.GetMousePos();
        }

        if (cropDragging)
        {
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                var position = ImGui.GetMousePos();
                var delta = position - cropLastDrag;
                cropLastDrag = position;
                if (preview.Width > 0f && preview.Height > 0f)
                {
                    targetCenterX -= delta.X * visible.X / preview.Width;
                    targetCenterY -= delta.Y * visible.Y / preview.Height;
                }
            }
            else
            {
                cropDragging = false;
            }
        }

        var clamped = new WallpaperCrop(targetZoom, targetCenterX, targetCenterY).Clamped(size, 1f);
        targetZoom = clamped.Zoom;
        targetCenterX = clamped.CenterX;
        targetCenterY = clamped.CenterY;
    }

    private void DrawComposeCaption(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Aethergram.NewPost), () => composeStage = ComposeStage.Crop);

        var scale = ImGuiHelpers.GlobalScale;
        var margin = 16f * scale;
        var top = area.Min.Y + AppHeader.Height * scale;

        var shareHeight = 46f * scale;
        var shareRect = new Rect(
            new Vector2(area.Min.X + margin, area.Max.Y - margin - shareHeight),
            new Vector2(area.Max.X - margin, area.Max.Y - margin));

        var statusHeight = composeStatus.Length > 0 ? 24f * scale : 0f;
        var cardHeight = 124f * scale;
        var cardBottom = shareRect.Min.Y - 14f * scale - statusHeight;
        var cardRect = new Rect(
            new Vector2(area.Min.X + margin, cardBottom - cardHeight),
            new Vector2(area.Max.X - margin, cardBottom));

        var hintY = cardRect.Min.Y - 20f * scale;
        var previewRegion = new Rect(
            new Vector2(area.Min.X + margin, top + 14f * scale),
            new Vector2(area.Max.X - margin, hintY - 12f * scale));
        DrawCaptionPreview(previewRegion, scale);
        Typography.DrawCentered(new Vector2(area.Center.X, hintY), Loc.T(L.Aethergram.TapToAdjust), theme.TextMuted, 0.75f);

        DrawCaptionCard(cardRect, scale);

        if (composeStatus.Length > 0)
        {
            Typography.DrawCentered(new Vector2(area.Center.X, cardRect.Max.Y + 10f * scale), composeStatus, theme.Danger, 0.82f);
        }

        var busy = store.Posting;
        if (DrawShareBar(shareRect, busy ? Loc.T(L.Aethergram.Sharing) : Loc.T(L.Aethergram.Share), !busy))
        {
            CommitGram();
        }
    }

    private void DrawCaptionPreview(Rect region, float scale)
    {
        var side = MathF.Min(region.Width, region.Height);
        if (side <= 0f)
        {
            return;
        }

        var half = side * 0.5f;
        var preview = new Rect(region.Center - new Vector2(half, half), region.Center + new Vector2(half, half));
        var rounding = 18f * scale;
        var drawList = ImGui.GetWindowDrawList();

        Squircle.Fill(
            drawList,
            new Vector2(preview.Min.X - 2f * scale, preview.Min.Y + 4f * scale),
            new Vector2(preview.Max.X + 2f * scale, preview.Max.Y + 8f * scale),
            rounding + 2f * scale,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.32f)));

        var texture = Plugin.WallpaperImages.Get(composeSourcePath);
        if (texture is null)
        {
            Squircle.Fill(drawList, preview.Min, preview.Max, rounding, ImGui.GetColorU32(theme.SurfaceMuted));
            Typography.DrawCentered(preview.Center, Loc.T(L.Common.Loading), theme.TextMuted);
            return;
        }

        var crop = new WallpaperCrop(targetZoom, targetCenterX, targetCenterY).Clamped(texture.Size, 1f);
        var (uv0, uv1) = crop.ComputeUv(texture.Size, 1f);
        drawList.AddImageRounded(texture.Handle, preview.Min, preview.Max, uv0, uv1, 0xFFFFFFFFu, rounding, ImDrawFlags.RoundCornersAll);
        Material.EdgeSquircle(drawList, preview.Min, preview.Max, rounding, scale);

        if (HoverClick(preview.Min, preview.Max))
        {
            composeStage = ComposeStage.Crop;
        }
    }

    private void DrawCaptionCard(Rect card, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rounding = 14f * scale;
        Squircle.Fill(drawList, card.Min, card.Max, rounding, ImGui.GetColorU32(theme.GroupedCard));
        Material.EdgeSquircle(drawList, card.Min, card.Max, rounding, scale);

        var padding = 12f * scale;
        var inputTop = card.Min.Y + padding;
        if (store.Me is { } me)
        {
            var radius = 11f * scale;
            var avatarCenter = new Vector2(card.Min.X + padding + radius, card.Min.Y + padding + radius);
            DrawAvatar(avatarCenter, radius, me.Name, me.World, me.AvatarUrl, 0.7f, 24);
            var displayName = string.IsNullOrEmpty(me.DisplayName) ? me.Name : me.DisplayName;
            Typography.Draw(new Vector2(avatarCenter.X + radius + 8f * scale, avatarCenter.Y - 8f * scale), displayName, theme.TextStrong, 0.88f, FontWeight.SemiBold);
            inputTop = avatarCenter.Y + radius + 6f * scale;
        }

        var counter = $"{caption.Length}/{MaxCaptionLength}";
        var counterSize = Typography.Measure(counter, 0.72f);
        var counterPos = new Vector2(card.Max.X - padding - counterSize.X, card.Max.Y - padding * 0.75f - counterSize.Y);

        var inputPos = new Vector2(card.Min.X + padding, inputTop);
        var inputSize = new Vector2(card.Width - padding * 2f, counterPos.Y - 4f * scale - inputTop);

        ImGui.SetCursorScreenPos(inputPos);
        if (captionFocus)
        {
            ImGui.SetKeyboardFocusHere();
            captionFocus = false;
        }

        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            ImGui.InputTextMultiline("##gramCaption", ref caption, MaxCaptionLength, inputSize, ImGuiInputTextFlags.None);
        }

        if (caption.Length == 0)
        {
            Typography.Draw(inputPos + ImGui.GetStyle().FramePadding, Loc.T(L.Aethergram.CaptionHint), theme.TextMuted, 1f);
        }

        var counterInk = caption.Length >= MaxCaptionLength - 50 ? theme.Danger : theme.TextMuted;
        Typography.Draw(counterPos, counter, counterInk, 0.72f);
    }

    private bool DrawShareBar(Rect rect, string label, bool enabled)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var hovered = enabled && ImGui.IsMouseHoveringRect(rect.Min, rect.Max);
        var radius = rect.Height * 0.5f;

        var fill = enabled
            ? (hovered ? Palette.Mix(Accent, theme.TextStrong, 0.12f) : Accent)
            : Palette.Mix(Accent, theme.AppBackground, 0.55f);
        Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(fill));
        Material.EdgeSquircle(drawList, rect.Min, rect.Max, radius, scale, enabled ? 1f : 0.5f);

        var ink = new Vector4(1f, 1f, 1f, enabled ? 1f : 0.75f);
        var textSize = Typography.Measure(label, 1f, FontWeight.SemiBold);
        var iconWidth = 14f * scale;
        var iconGap = 8f * scale;
        var left = rect.Center.X - (iconWidth + iconGap + textSize.X) * 0.5f;
        DrawIcon(new Vector2(left + iconWidth * 0.5f, rect.Center.Y), FontAwesomeIcon.PaperPlane.ToIconString(), ink, 0.9f);
        Typography.Draw(new Vector2(left + iconWidth + iconGap, rect.Center.Y - textSize.Y * 0.5f), label, ink, 1f, FontWeight.SemiBold);

        if (!hovered)
        {
            return false;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private void CommitGram()
    {
        if (composeSourcePath.Length == 0 || store.Posting)
        {
            return;
        }

        composeStatus = string.Empty;
        var crop = new WallpaperCrop(targetZoom, targetCenterX, targetCenterY);
        store.CreateGram(composeSourcePath, crop, caption, ok => composeOutcome = ok ? 1 : 2);
    }

    private void CommitAvatar()
    {
        if (composeSourcePath.Length == 0 || store.Posting)
        {
            return;
        }

        composeStatus = string.Empty;
        var crop = new WallpaperCrop(targetZoom, targetCenterX, targetCenterY);
        store.UpdateAvatar(composeSourcePath, crop, ok => composeOutcome = ok ? 1 : 2);
    }

    private void LaunchFileDialog()
    {
        _ = NativeFileDialog.OpenImageAsync(Loc.T(L.Aethergram.NewPost)).ContinueWith(task =>
        {
            if (task.Status == TaskStatus.RanToCompletion && !string.IsNullOrEmpty(task.Result))
            {
                Interlocked.Exchange(ref pendingPickedPath, task.Result);
            }
        });
    }

    private void DrawDetail(Rect area, string postId)
    {
        var post = store.DetailPost;
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Aethergram.PostTitle), back);

        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;

        if (post is null || post.Id != postId)
        {
            Typography.DrawCentered(new Vector2(area.Center.X, top + 60f * scale), Loc.T(L.Common.Loading), theme.TextMuted);
            return;
        }

        var composerHeight = 50f * scale;
        var body = new Rect(new Vector2(area.Min.X, top), new Vector2(area.Max.X, area.Max.Y - composerHeight));

        using (AppSurface.Begin(body))
        {
            var origin = ImGui.GetCursorScreenPos();
            var width = ImGui.GetContentRegionAvail().X;

            var headerHeight = 44f * scale;
            var avatarRadius = 16f * scale;
            var avatarCenter = new Vector2(origin.X + avatarRadius, origin.Y + headerHeight * 0.5f);
            DrawAvatar(avatarCenter, avatarRadius, post.AuthorName, post.AuthorWorld, post.AuthorAvatarUrl, 0.85f, 28);
            if (HoverClick(avatarCenter - new Vector2(avatarRadius, avatarRadius), avatarCenter + new Vector2(avatarRadius, avatarRadius)))
            {
                OpenProfile(post.AuthorId);
            }

            var nameLeft = avatarCenter.X + avatarRadius + 10f * scale;
            var displayName = string.IsNullOrEmpty(post.AuthorDisplayName) ? post.AuthorName : post.AuthorDisplayName;
            Typography.Draw(new Vector2(nameLeft, avatarCenter.Y - 8f * scale), displayName, theme.TextStrong, 0.95f, FontWeight.SemiBold);

            ImGui.SetCursorScreenPos(new Vector2(origin.X, origin.Y + headerHeight));
            var imageRect = new Rect(new Vector2(origin.X, origin.Y + headerHeight), new Vector2(origin.X + width, origin.Y + headerHeight + width));
            DrawGramImage(imageRect, post.MediaUrl, 0f);

            ImGui.SetCursorScreenPos(new Vector2(origin.X, imageRect.Max.Y + 12f * scale));
            var liked = post.MyReaction >= 0;
            var actionsY = imageRect.Max.Y + 12f * scale + 8f * scale;
            var heartCenter = new Vector2(origin.X + 12f * scale, actionsY);
            if (DrawIconButton(heartCenter, 14f * scale, FontAwesomeIcon.Heart.ToIconString(), liked ? LikeRed : theme.TextStrong, new Vector4(0f, 0f, 0f, 0f), 1f))
            {
                store.ToggleLike(post);
            }

            var mine = store.Me is { } me && me.Id == post.AuthorId;
            var reportShown = false;
            var deleteShown = false;
            if (mine)
            {
                var deleteCenter = new Vector2(origin.X + width - 14f * scale, actionsY);
                deleteShown = DrawDeleteToggle(deleteCenter, 14f * scale, post.Id);
            }
            else
            {
                var reportCenter = new Vector2(origin.X + width - 14f * scale, actionsY);
                reportShown = DrawReportToggle(reportCenter, 14f * scale, "post", post.Id);
            }

            ImGui.SetCursorScreenPos(new Vector2(origin.X, actionsY + 18f * scale));
            if (deleteShown)
            {
                DrawDeleteComposer(post.Id, origin.X, width);
                ImGui.Dummy(new Vector2(0f, 6f * scale));
            }
            else if (reportShown)
            {
                DrawReportComposer(origin.X, width);
                ImGui.Dummy(new Vector2(0f, 6f * scale));
            }

            if (post.TotalReactions > 0)
            {
                Typography.Draw(ImGui.GetCursorScreenPos(), Loc.Plural(L.Aethergram.Likes, post.TotalReactions), theme.TextStrong, 0.9f, FontWeight.SemiBold);
                ImGui.Dummy(new Vector2(0f, 18f * scale));
            }

            if (post.Text.Length > 0)
            {
                ImGui.PushTextWrapPos(origin.X + width);
                using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
                {
                    ImGui.TextWrapped($"{displayName}  {post.Text}");
                }

                ImGui.PopTextWrapPos();
            }

            ImGui.Dummy(new Vector2(0f, 8f * scale));
            var separatorY = ImGui.GetCursorScreenPos().Y;
            ImGui.GetWindowDrawList().AddLine(new Vector2(origin.X, separatorY), new Vector2(origin.X + width, separatorY), ImGui.GetColorU32(theme.Separator), 1f);
            ImGui.Dummy(new Vector2(0f, 10f * scale));

            var comments = store.DetailComments;
            if (comments.Length == 0 && !store.DetailLoading)
            {
                Typography.Draw(ImGui.GetCursorScreenPos(), Loc.T(L.Aethergram.NoComments), theme.TextMuted, 0.85f);
            }
            else
            {
                for (var index = 0; index < comments.Length; index++)
                {
                    DrawComment(comments[index]);
                }
            }

            ImGui.Dummy(new Vector2(0f, 16f * scale));
        }

        DrawCommentComposer(new Rect(new Vector2(area.Min.X, area.Max.Y - composerHeight), area.Max), postId);
    }

    private void DrawComment(CommentDto comment)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var radius = 14f * scale;
        var avatarCenter = new Vector2(origin.X + radius, origin.Y + radius);
        DrawAvatar(avatarCenter, radius, comment.AuthorName, string.Empty, comment.AuthorAvatarUrl, 0.8f, 24);
        if (HoverClick(avatarCenter - new Vector2(radius, radius), avatarCenter + new Vector2(radius, radius)))
        {
            OpenProfile(comment.AuthorId);
        }

        var textLeft = origin.X + radius * 2f + 10f * scale;
        var displayName = string.IsNullOrEmpty(comment.AuthorDisplayName) ? comment.AuthorName : comment.AuthorDisplayName;
        ImGui.SetCursorScreenPos(new Vector2(textLeft, origin.Y));
        ImGui.PushTextWrapPos(origin.X + width);
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            ImGui.TextWrapped($"{displayName}  {comment.Text}");
        }

        ImGui.PopTextWrapPos();

        var mine = store.Me is { } me && me.Id == comment.AuthorId;
        if (mine)
        {
            var trashCenter = new Vector2(origin.X + width - 8f * scale, origin.Y + 8f * scale);
            if (DrawIconButton(trashCenter, 10f * scale, FontAwesomeIcon.Times.ToIconString(), theme.TextMuted, new Vector4(0f, 0f, 0f, 0f), 0.7f) && store.DetailPost is { } post)
            {
                store.DeleteComment(post.Id, comment.Id);
            }
        }

        var bottom = MathF.Max(ImGui.GetCursorScreenPos().Y, origin.Y + radius * 2f);
        ImGui.SetCursorScreenPos(new Vector2(origin.X, bottom));
        ImGui.Dummy(new Vector2(width, 8f * scale));
    }

    private void DrawCommentComposer(Rect bar, string postId)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddLine(bar.Min, new Vector2(bar.Max.X, bar.Min.Y), ImGui.GetColorU32(theme.Separator), 1f);

        var pillMin = new Vector2(bar.Min.X + 12f * scale, bar.Min.Y + 8f * scale);
        var pillMax = new Vector2(bar.Max.X - 56f * scale, bar.Max.Y - 8f * scale);
        Squircle.Fill(drawList, pillMin, pillMax, (pillMax.Y - pillMin.Y) * 0.5f, ImGui.GetColorU32(theme.GroupedCard));

        ImGui.SetCursorScreenPos(new Vector2(pillMin.X + 14f * scale, (pillMin.Y + pillMax.Y) * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(pillMax.X - pillMin.X - 24f * scale);
        var submitted = false;
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            submitted = ImGui.InputTextWithHint("##gramComment", Loc.T(L.Aethergram.AddComment), ref commentDraft, MaxCommentLength, ImGuiInputTextFlags.EnterReturnsTrue);
        }

        var canSend = commentDraft.Trim().Length > 0 && !store.Commenting;
        var sendCenter = new Vector2(bar.Max.X - 28f * scale, bar.Center.Y);
        if ((DrawIconButton(sendCenter, 16f * scale, FontAwesomeIcon.PaperPlane.ToIconString(), canSend ? Accent : theme.TextMuted, new Vector4(0f, 0f, 0f, 0f), 0.95f) || submitted) && canSend)
        {
            var text = commentDraft;
            commentDraft = string.Empty;
            store.AddComment(postId, text, _ => { });
        }
    }

    private void DrawProfile(Rect area, string userId)
    {
        if (store.ProfileUserId != userId)
        {
            store.OpenProfile(userId);
        }

        var user = store.ProfileUser;
        var title = user is null ? DisplayName : (string.IsNullOrEmpty(user.DisplayName) ? user.Name : user.DisplayName);
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, title, back);

        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);

        if (store.ProfileFailed)
        {
            Typography.DrawCentered(body.Center, Loc.T(L.Aethergram.ProfileError), theme.TextMuted);
            return;
        }

        if (user is null)
        {
            Typography.DrawCentered(body.Center, Loc.T(L.Common.Loading), theme.TextMuted);
            return;
        }

        using (AppSurface.Begin(body))
        {
            DrawProfileHeader(user);
            DrawProfileGrid();
        }
    }

    private bool DrawReportToggle(Vector2 center, float radius, string targetType, string targetId)
    {
        var active = reportTargetType == targetType && reportTargetId == targetId;
        var background = Palette.WithAlpha(theme.Danger, active ? 0.32f : 0.16f);
        if (DrawIconButton(center, radius, FontAwesomeIcon.Flag.ToIconString(), theme.Danger, background, 0.9f))
        {
            if (active)
            {
                reportTargetType = null;
                reportTargetId = null;
                active = false;
            }
            else
            {
                reportTargetType = targetType;
                reportTargetId = targetId;
                reportReasonDraft = string.Empty;
                reportStatus = string.Empty;
                active = true;
            }
        }

        return active;
    }

    private void DrawReportComposer(float left, float width)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var buttonWidth = 84f * scale;
        var buttonHeight = 28f * scale;

        ImGui.SetCursorScreenPos(new Vector2(left, origin.Y));
        ImGui.SetNextItemWidth(width - buttonWidth - 8f * scale);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, theme.GroupedCard))
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            ImGui.InputTextWithHint("##reportReason", Loc.T(L.Aethergram.ReportReasonHint), ref reportReasonDraft, MaxReportReasonLength);
        }

        var buttonRect = new Rect(new Vector2(left + width - buttonWidth, origin.Y - 2f * scale), new Vector2(left + width, origin.Y - 2f * scale + buttonHeight));
        var canSubmit = !reportSubmitting;
        if (DrawPillButton(buttonRect, reportSubmitting ? Loc.T(L.Aethergram.Saving) : Loc.T(L.Aethergram.ReportSubmit), canSubmit) && canSubmit)
        {
            SubmitReport();
        }

        ImGui.SetCursorScreenPos(new Vector2(left, origin.Y + buttonHeight + 2f * scale));
        if (reportStatus.Length > 0)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
            {
                ImGui.TextUnformatted(reportStatus);
            }

            ImGui.Dummy(new Vector2(0f, 4f * scale));
        }
    }

    private void SubmitReport()
    {
        if (reportSubmitting || reportTargetType is not { } targetType || reportTargetId is not { } targetId)
        {
            return;
        }

        reportSubmitting = true;
        var reason = reportReasonDraft.Trim();
        store.Report(targetType, targetId, reason.Length > 0 ? reason : null, ok =>
        {
            reportSubmitting = false;
            reportStatus = Loc.T(ok ? L.Aethergram.ReportSent : L.Aethergram.ReportFailed);
            if (ok)
            {
                reportTargetType = null;
                reportTargetId = null;
            }
        });
    }

    private bool DrawDeleteToggle(Vector2 center, float radius, string postId)
    {
        var active = deleteTargetId == postId;
        var background = Palette.WithAlpha(theme.Danger, active ? 0.32f : 0.16f);
        if (DrawIconButton(center, radius, FontAwesomeIcon.Trash.ToIconString(), theme.Danger, background, 0.9f))
        {
            if (active)
            {
                deleteTargetId = null;
                active = false;
            }
            else
            {
                deleteTargetId = postId;
                deleteStatus = string.Empty;
                active = true;
            }
        }

        return active;
    }

    private void DrawDeleteComposer(string postId, float left, float width)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();

        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            ImGui.SetCursorScreenPos(new Vector2(left, origin.Y));
            ImGui.PushTextWrapPos(left + width);
            ImGui.TextWrapped(Loc.T(L.Aethergram.DeleteConfirmMessage));
            ImGui.PopTextWrapPos();
        }

        var rowY = ImGui.GetCursorScreenPos().Y + 6f * scale;
        var buttonWidth = 84f * scale;
        var buttonHeight = 28f * scale;

        var cancelRect = new Rect(new Vector2(left + width - buttonWidth * 2f - 8f * scale, rowY), new Vector2(left + width - buttonWidth - 8f * scale, rowY + buttonHeight));
        if (DrawPillButton(cancelRect, Loc.T(L.Aethergram.DeleteCancel), false) && !deleteSubmitting)
        {
            deleteTargetId = null;
        }

        var deleteRect = new Rect(new Vector2(left + width - buttonWidth, rowY), new Vector2(left + width, rowY + buttonHeight));
        var canSubmit = !deleteSubmitting;
        if (DrawDangerPillButton(deleteRect, deleteSubmitting ? Loc.T(L.Aethergram.Saving) : Loc.T(L.Aethergram.DeleteConfirm)) && canSubmit)
        {
            SubmitDelete(postId);
        }

        ImGui.SetCursorScreenPos(new Vector2(left, rowY + buttonHeight + 2f * scale));
        if (deleteStatus.Length > 0)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
            {
                ImGui.TextUnformatted(deleteStatus);
            }

            ImGui.Dummy(new Vector2(0f, 4f * scale));
        }
    }

    private bool DrawDangerPillButton(Rect rect, string label)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = ImGui.IsMouseHoveringRect(rect.Min, rect.Max);
        var radius = rect.Height * 0.5f;
        var fill = hovered ? Palette.Mix(theme.Danger, theme.TextStrong, 0.12f) : theme.Danger;
        Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(fill));

        var textSize = Typography.Measure(label, 0.9f, FontWeight.SemiBold);
        Typography.Draw(rect.Center - textSize * 0.5f, label, new Vector4(1f, 1f, 1f, 1f), 0.9f, FontWeight.SemiBold);

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private void SubmitDelete(string postId)
    {
        if (deleteSubmitting || deleteTargetId != postId)
        {
            return;
        }

        deleteSubmitting = true;
        store.DeletePost(postId, ok =>
        {
            deleteSubmitting = false;
            if (ok)
            {
                deleteTargetId = null;
                deleteStatus = string.Empty;
                back();
            }
            else
            {
                deleteStatus = Loc.T(L.Aethergram.DeleteFailed);
            }
        });
    }

    private void DrawProfileHeader(UserDto user)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var drawList = ImGui.GetWindowDrawList();

        var avatarRadius = 36f * scale;
        var avatarCenter = new Vector2(origin.X + avatarRadius, origin.Y + avatarRadius);
        DrawAvatar(avatarCenter, avatarRadius, user.Name, user.World, user.AvatarUrl, 1.3f, 48);

        var statsLeft = avatarCenter.X + avatarRadius + 18f * scale;
        var statsY = origin.Y + 6f * scale;
        var third = (origin.X + width - statsLeft) / 3f;
        DrawCountStat(statsLeft + third * 0f, statsY, third, user.Grams.ToString(Loc.Culture), PostsLabel(user.Grams));
        DrawCountStat(statsLeft + third * 1f, statsY, third, user.Followers.ToString(Loc.Culture), FollowersLabel(user.Followers));
        DrawCountStat(statsLeft + third * 2f, statsY, third, user.Following.ToString(Loc.Culture), Loc.T(L.Aethergram.Following));

        ImGui.SetCursorScreenPos(new Vector2(origin.X, avatarCenter.Y + avatarRadius + 10f * scale));

        var displayName = string.IsNullOrEmpty(user.DisplayName) ? user.Name : user.DisplayName;
        using (Plugin.Fonts.Push(1.1f, FontWeight.SemiBold))
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            ImGui.TextUnformatted(displayName);
        }

        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
        {
            if (user.Handle.Length > 0)
            {
                ImGui.TextUnformatted($"@{user.Handle}");
            }

            ImGui.TextUnformatted($"{user.Name} · {user.World}");
        }

        if (user.Bio.Length > 0)
        {
            ImGui.Dummy(new Vector2(0f, 4f * scale));
            ImGui.PushTextWrapPos(0f);
            using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
            {
                ImGui.TextWrapped(user.Bio);
            }

            ImGui.PopTextWrapPos();
        }

        ImGui.Dummy(new Vector2(0f, 10f * scale));
        var buttonHeight = 32f * scale;
        var buttonTop = ImGui.GetCursorScreenPos().Y;
        var reportShown = false;
        if (user.IsMe)
        {
            var buttonRect = new Rect(new Vector2(origin.X, buttonTop), new Vector2(origin.X + width, buttonTop + buttonHeight));
            if (DrawPillButton(buttonRect, Loc.T(L.Aethergram.EditProfile), false))
            {
                editLoadedFor = null;
                router.Push(AethergramRoute.EditProfile);
            }
        }
        else
        {
            var reportCenter = new Vector2(origin.X + width - buttonHeight * 0.5f, buttonTop + buttonHeight * 0.5f);
            reportShown = DrawReportToggle(reportCenter, buttonHeight * 0.5f, "user", user.Id);

            var followRect = new Rect(new Vector2(origin.X, buttonTop), new Vector2(origin.X + width - buttonHeight - 10f * scale, buttonTop + buttonHeight));
            if (DrawPillButton(followRect, user.IsFollowing ? Loc.T(L.Aethergram.Following) : Loc.T(L.Aethergram.Follow), !user.IsFollowing))
            {
                store.SetFollow(user.Id, !user.IsFollowing);
            }
        }

        ImGui.SetCursorScreenPos(new Vector2(origin.X, buttonTop + buttonHeight + 10f * scale));
        if (reportShown)
        {
            DrawReportComposer(origin.X, width);
        }

        var separatorY = ImGui.GetCursorScreenPos().Y;
        drawList.AddLine(new Vector2(origin.X, separatorY), new Vector2(origin.X + width, separatorY), ImGui.GetColorU32(theme.Separator), 1f);
        ImGui.Dummy(new Vector2(0f, 8f * scale));
    }

    private void DrawProfileGrid()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var posts = store.ProfilePosts;
        if (posts.Length == 0)
        {
            Typography.DrawCentered(new Vector2(ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X * 0.5f, ImGui.GetCursorScreenPos().Y + 40f * scale), Loc.T(L.Aethergram.Empty), theme.TextMuted);
            return;
        }

        var gap = 3f * scale;
        var cell = (ImGui.GetContentRegionAvail().X - gap * (GridColumns - 1)) / GridColumns;
        using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(gap, gap)))
        {
            for (var index = 0; index < posts.Length; index++)
            {
                using (ImRaii.PushId(index))
                {
                    var clicked = ImGui.InvisibleButton("gram", new Vector2(cell, cell));
                    DrawGridThumbnail(posts[index], ImGui.GetItemRectMin(), ImGui.GetItemRectMax());
                    if (clicked)
                    {
                        OpenDetail(posts[index]);
                    }
                }

                if (index % GridColumns != GridColumns - 1)
                {
                    ImGui.SameLine();
                }
            }
        }

        ImGui.Dummy(new Vector2(0f, 24f * scale));
    }

    private void DrawGridThumbnail(PostDto post, Vector2 min, Vector2 max)
    {
        var drawList = ImGui.GetWindowDrawList();
        var texture = images.Get(post.MediaUrl);
        if (texture is null)
        {
            drawList.AddRectFilled(min, max, ImGui.GetColorU32(theme.SurfaceMuted));
            return;
        }

        var (uv0, uv1) = CenterCropSquare(texture.Size);
        drawList.AddImage(texture.Handle, min, max, uv0, uv1);
        if (ImGui.IsItemHovered())
        {
            drawList.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.1f)));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
    }

    private void DrawEditProfile(Rect area)
    {
        var me = store.Me ?? (store.ProfileUser is { IsMe: true } self ? self : null);
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Aethergram.EditProfile), back);

        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);

        if (me is null)
        {
            store.EnsureMe();
            Typography.DrawCentered(body.Center, Loc.T(L.Common.Loading), theme.TextMuted);
            return;
        }

        if (editOutcome == 1)
        {
            editOutcome = 0;
            store.ReloadProfile();
            router.Pop();
            return;
        }

        if (editOutcome == 2)
        {
            editOutcome = 0;
            editStatus = Loc.T(L.Aethergram.HandleTaken);
        }

        if (editLoadedFor != me.Id)
        {
            editLoadedFor = me.Id;
            editDisplay = me.DisplayName;
            editHandle = me.Handle;
            editBio = me.Bio;
            editStatus = string.Empty;
        }

        var handleValid = IsHandleValid(editHandle);
        var canSave = !editBusy && editDisplay.Trim().Length > 0 && handleValid;
        if (DrawHeaderAction(area, editBusy ? Loc.T(L.Aethergram.Saving) : Loc.T(L.Aethergram.Save), canSave))
        {
            SaveProfile();
        }

        using (AppSurface.Begin(body))
        {
            var origin = ImGui.GetCursorScreenPos();
            var avatarRadius = 34f * scale;
            var avatarCenter = new Vector2(origin.X + ImGui.GetContentRegionAvail().X * 0.5f, origin.Y + avatarRadius);
            DrawAvatar(avatarCenter, avatarRadius, me.Name, me.World, me.AvatarUrl, 1.3f, 48);

            ImGui.SetCursorScreenPos(new Vector2(origin.X, avatarCenter.Y + avatarRadius + 8f * scale));
            var changeWidth = 150f * scale;
            var changeRect = new Rect(new Vector2(avatarCenter.X - changeWidth * 0.5f, ImGui.GetCursorScreenPos().Y), new Vector2(avatarCenter.X + changeWidth * 0.5f, ImGui.GetCursorScreenPos().Y + 30f * scale));
            if (DrawPillButton(changeRect, Loc.T(L.Aethergram.ChangePhoto), false))
            {
                StartCompose(true);
            }

            ImGui.SetCursorScreenPos(new Vector2(origin.X, changeRect.Max.Y + 16f * scale));

            DrawField(Loc.T(L.Aethergram.DisplayNameLabel), "##editDisplay", ref editDisplay, DisplayNameMax, false);
            ImGui.Dummy(new Vector2(0f, 10f * scale));
            DrawHandleField();
            ImGui.Dummy(new Vector2(0f, 10f * scale));
            DrawField(Loc.T(L.Aethergram.BioLabel), "##editBio", ref editBio, BioMax, true);

            if (editStatus.Length > 0)
            {
                ImGui.Dummy(new Vector2(0f, 10f * scale));
                using (ImRaii.PushColor(ImGuiCol.Text, theme.Danger))
                {
                    ImGui.TextWrapped(editStatus);
                }
            }
        }
    }

    private void SaveProfile()
    {
        var me = store.Me;
        if (me is null || editBusy)
        {
            return;
        }

        if (!IsHandleValid(editHandle) || editDisplay.Trim().Length == 0)
        {
            editStatus = Loc.T(L.Aethergram.HandleRules);
            return;
        }

        editBusy = true;
        editStatus = string.Empty;
        store.UpdateProfile(editDisplay.Trim(), editHandle.Trim(), editBio.Trim(), (ok, _) =>
        {
            editBusy = false;
            editOutcome = ok ? 1 : 2;
        });
    }

    private void DrawDiscover(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Aethergram.FindPeople), back);

        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var searchHeight = 52f * scale;
        DrawSearchBar(new Rect(new Vector2(area.Min.X, top), new Vector2(area.Max.X, top + searchHeight)));

        var listRect = new Rect(new Vector2(area.Min.X, top + searchHeight), area.Max);
        var snapshot = store.DiscoverResults;
        using (AppSurface.Begin(listRect))
        {
            if (snapshot.Length == 0)
            {
                Typography.DrawCentered(new Vector2(listRect.Center.X, listRect.Min.Y + 60f * scale), store.Searching ? Loc.T(L.Common.Searching) : Loc.T(L.Aethergram.SearchByName), theme.TextMuted);
            }
            else
            {
                for (var index = 0; index < snapshot.Length; index++)
                {
                    DrawUserRow(snapshot[index]);
                }
            }
        }
    }

    private void DrawUserRow(UserDto user)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var rowHeight = 58f * scale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var radius = 20f * scale;
        var avatarCenter = new Vector2(origin.X + radius, origin.Y + rowHeight * 0.5f);
        DrawAvatar(avatarCenter, radius, user.Name, user.World, user.AvatarUrl, 0.95f, 32);

        var textLeft = origin.X + radius * 2f + 12f * scale;
        var displayName = string.IsNullOrEmpty(user.DisplayName) ? user.Name : user.DisplayName;
        Typography.Draw(new Vector2(textLeft, origin.Y + 9f * scale), displayName, theme.TextStrong, 1f, FontWeight.SemiBold);
        var sub = user.Handle.Length > 0 ? $"@{user.Handle} · {user.World}" : $"{user.Name} · {user.World}";
        Typography.Draw(new Vector2(textLeft, origin.Y + 31f * scale), sub, theme.TextMuted, 0.85f);

        var buttonWidth = 96f * scale;
        var buttonHeight = 30f * scale;
        var buttonRect = new Rect(new Vector2(origin.X + width - buttonWidth, origin.Y + rowHeight * 0.5f - buttonHeight * 0.5f), new Vector2(origin.X + width, origin.Y + rowHeight * 0.5f + buttonHeight * 0.5f));
        if (DrawPillButton(buttonRect, user.IsFollowing ? Loc.T(L.Aethergram.Following) : Loc.T(L.Aethergram.Follow), !user.IsFollowing))
        {
            store.SetFollow(user.Id, !user.IsFollowing);
        }

        if (HoverClick(origin, new Vector2(origin.X + width - buttonWidth - 6f * scale, origin.Y + rowHeight)))
        {
            OpenProfile(user.Id);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight));
    }

    private void DrawSearchBar(Rect bar)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var pillMin = new Vector2(bar.Min.X + 12f * scale, bar.Min.Y + 9f * scale);
        var pillMax = new Vector2(bar.Max.X - 12f * scale, bar.Max.Y - 9f * scale);
        Squircle.Fill(drawList, pillMin, pillMax, (pillMax.Y - pillMin.Y) * 0.5f, ImGui.GetColorU32(theme.GroupedCard));

        DrawIcon(new Vector2(pillMin.X + 16f * scale, (pillMin.Y + pillMax.Y) * 0.5f), FontAwesomeIcon.Search.ToIconString(), theme.TextMuted, 0.85f);

        ImGui.SetCursorScreenPos(new Vector2(pillMin.X + 32f * scale, (pillMin.Y + pillMax.Y) * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(pillMax.X - pillMin.X - 44f * scale);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            if (ImGui.InputTextWithHint("##aethergramSearch", Loc.T(L.Aethergram.NameOrWorld), ref searchDraft, 64, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                store.Search(searchDraft);
            }
        }
    }

    private void DrawHomeTopBar(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        var logoSize = Typography.Measure(DisplayName, 1.3f, FontWeight.Bold);
        Typography.Draw(new Vector2(area.Min.X + 16f * scale, rowCenterY - logoSize.Y * 0.5f), DisplayName, theme.TextStrong, 1.3f, FontWeight.Bold);
    }

    private void DrawAvatar(Vector2 center, float radius, string name, string world, string? avatarUrl, float monogramScale, int segments)
    {
        var drawList = ImGui.GetWindowDrawList();
        if (!string.IsNullOrEmpty(avatarUrl))
        {
            var texture = images.Get(avatarUrl);
            if (texture is not null)
            {
                drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(theme.SurfaceMuted), segments);
                var (uv0, uv1) = CenterCropSquare(texture.Size);
                var corner = new Vector2(radius, radius);
                drawList.AddImageRounded(texture.Handle, center - corner, center + corner, uv0, uv1, 0xFFFFFFFFu, radius, ImDrawFlags.RoundCornersAll);
                return;
            }
        }

        AvatarView.Draw(drawList, center, radius, theme.Accent, Initials.Of(name), monogramScale, lodestone.Avatar(name, world), segments);
    }

    private void DrawCountStat(float left, float y, float columnWidth, string value, string label)
    {
        var center = left + columnWidth * 0.5f;
        Typography.DrawCentered(new Vector2(center, y + 8f * ImGuiHelpers.GlobalScale), value, theme.TextStrong, 1f, FontWeight.SemiBold);
        Typography.DrawCentered(new Vector2(center, y + 26f * ImGuiHelpers.GlobalScale), label, theme.TextMuted, 0.78f);
    }

    private void DrawField(string label, string id, ref string value, int maxLength, bool multiline)
    {
        var scale = ImGuiHelpers.GlobalScale;
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
        {
            ImGui.TextUnformatted(label);
        }

        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = (multiline ? 88f : 34f) * scale;
        Squircle.Fill(ImGui.GetWindowDrawList(), origin, new Vector2(origin.X + width, origin.Y + height), 9f * scale, ImGui.GetColorU32(theme.GroupedCard));

        ImGui.SetCursorScreenPos(new Vector2(origin.X + 12f * scale, origin.Y + (multiline ? 8f * scale : height * 0.5f - ImGui.GetFrameHeight() * 0.5f)));
        ImGui.SetNextItemWidth(width - 24f * scale);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            if (multiline)
            {
                ImGui.InputTextMultiline(id, ref value, maxLength, new Vector2(width - 24f * scale, height - 16f * scale), ImGuiInputTextFlags.None);
            }
            else
            {
                ImGui.InputText(id, ref value, maxLength, ImGuiInputTextFlags.None);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawHandleField()
    {
        var scale = ImGuiHelpers.GlobalScale;
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
        {
            ImGui.TextUnformatted(Loc.T(L.Aethergram.HandleLabel));
        }

        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = 34f * scale;
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, origin, new Vector2(origin.X + width, origin.Y + height), 9f * scale, ImGui.GetColorU32(theme.GroupedCard));

        Typography.Draw(new Vector2(origin.X + 12f * scale, origin.Y + height * 0.5f - 8f * scale), "@", theme.TextMuted, 1f);

        ImGui.SetCursorScreenPos(new Vector2(origin.X + 26f * scale, origin.Y + height * 0.5f - ImGui.GetFrameHeight() * 0.5f));
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
        Typography.Draw(new Vector2(origin.X + 2f * scale, origin.Y + height + 3f * scale), Loc.T(L.Aethergram.HandleRules), theme.TextMuted, 0.78f);
        ImGui.Dummy(new Vector2(width, 16f * scale));
    }

    private bool DrawHeaderAction(Rect area, string label, bool enabled)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var height = 28f * scale;
        var width = Typography.Measure(label, 0.9f, FontWeight.SemiBold).X + 26f * scale;
        var max = new Vector2(area.Max.X - 12f * scale, area.Min.Y + AppHeader.Height * scale * 0.5f + height * 0.5f);
        var min = new Vector2(max.X - width, max.Y - height);
        var rect = new Rect(min, max);
        return DrawPillButton(rect, label, enabled) && enabled;
    }

    private bool DrawPillButton(Rect rect, string label, bool filled)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = ImGui.IsMouseHoveringRect(rect.Min, rect.Max);
        var radius = rect.Height * 0.5f;

        var fill = filled
            ? (hovered ? Palette.Mix(Accent, theme.TextStrong, 0.12f) : Accent)
            : (hovered ? Palette.Mix(theme.GroupedCard, theme.TextStrong, 0.08f) : theme.GroupedCard);
        var ink = filled ? new Vector4(1f, 1f, 1f, 1f) : theme.TextStrong;
        Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(fill));
        if (!filled)
        {
            Squircle.Stroke(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(theme.Separator), 1f);
        }

        var textSize = Typography.Measure(label, 0.9f, FontWeight.SemiBold);
        Typography.Draw(rect.Center - textSize * 0.5f, label, ink, 0.9f, FontWeight.SemiBold);

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private bool DrawIconButton(Vector2 center, float hitRadius, string glyph, Vector4 color, Vector4 background, float glyphScale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = ImGui.IsMouseHoveringRect(center - new Vector2(hitRadius, hitRadius), center + new Vector2(hitRadius, hitRadius));
        if (background.W > 0f)
        {
            drawList.AddCircleFilled(center, hitRadius, ImGui.GetColorU32(hovered ? Palette.Mix(background, theme.TextStrong, 0.08f) : background), 24);
        }

        DrawIcon(center, glyph, hovered ? Palette.Mix(color, theme.TextStrong, 0.2f) : color, glyphScale);

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private static void DrawIcon(Vector2 center, string glyph, Vector4 color, float scale)
    {
        float fontSize;
        Vector2 size;
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            fontSize = ImGui.GetFontSize() * scale;
            size = ImGui.CalcTextSize(glyph) * scale;
        }

        ImGui.GetWindowDrawList().AddText(UiBuilder.IconFont, fontSize, center - size * 0.5f, ImGui.GetColorU32(color), glyph, 0f);
    }

    private static (Vector2 Uv0, Vector2 Uv1) CenterCropSquare(Vector2 size)
    {
        if (size.X <= 0f || size.Y <= 0f)
        {
            return (Vector2.Zero, Vector2.One);
        }

        var aspect = size.X / size.Y;
        if (aspect > 1f)
        {
            var inset = (1f - 1f / aspect) * 0.5f;
            return (new Vector2(inset, 0f), new Vector2(1f - inset, 1f));
        }

        if (aspect < 1f)
        {
            var inset = (1f - aspect) * 0.5f;
            return (new Vector2(0f, inset), new Vector2(1f, 1f - inset));
        }

        return (Vector2.Zero, Vector2.One);
    }

    private static bool HoverClick(Vector2 min, Vector2 max)
    {
        if (!ImGui.IsMouseHoveringRect(min, max))
        {
            return false;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private void OpenProfile(string userId)
    {
        store.OpenProfile(userId);
        router.Push(AethergramRoute.Profile(userId));
    }

    private void OpenDetail(PostDto post)
    {
        store.OpenDetail(post);
        commentDraft = string.Empty;
        router.Push(AethergramRoute.Detail(post.Id));
    }

    private void EnsureLoaded(AethergramFeedScope scope)
    {
        if (store.Feed(scope).Length == 0 && !store.IsLoading(scope))
        {
            store.RefreshFeed(scope);
        }
    }

    private void TickRefresh(AethergramFeedScope scope)
    {
        if (store.IsLoading(scope))
        {
            return;
        }

        if (scope == AethergramFeedScope.ForYou && sinceForYou >= FeedRefreshSeconds)
        {
            sinceForYou = 0f;
            store.RefreshFeed(scope);
        }
        else if (scope == AethergramFeedScope.Following && sinceFollowing >= FeedRefreshSeconds)
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

    private static string RelativeTime(long unixSeconds)
    {
        var moment = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
        var span = DateTime.UtcNow - moment;
        if (span.TotalSeconds < 60)
        {
            return Loc.T(L.Time.Now);
        }

        if (span.TotalMinutes < 60)
        {
            return Loc.T(L.Time.MinutesShort, (int)span.TotalMinutes);
        }

        if (span.TotalHours < 24)
        {
            return Loc.T(L.Time.HoursShort, (int)span.TotalHours);
        }

        if (span.TotalDays < 7)
        {
            return Loc.T(L.Time.DaysShort, (int)span.TotalDays);
        }

        return moment.ToString("MMM d", Loc.Culture);
    }

    public void Dispose()
    {
        store.Dispose();
        images.Dispose();
    }
}
