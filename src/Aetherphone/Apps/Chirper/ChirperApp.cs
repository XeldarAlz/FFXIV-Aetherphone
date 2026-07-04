using System.Numerics;
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
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Chirper;

internal sealed class ChirperApp : IPhoneApp
{
    private const float FeedRefreshSeconds = 25f;
    private const int MaxPostLength = 500;
    private const int DisplayNameMax = 40;
    private const int HandleMax = 15;
    private const int BioMax = 200;
    private const float FeedTopPadding = 8f;
    private const int MaxReportReasonLength = 200;
    private const int MaxCommentLength = 500;

    public string Id => "chirper";

    public string DisplayName => Loc.T(L.Apps.Chirper);

    public string Glyph => "Ch";

    public Vector4 Accent => new(0.16f, 0.52f, 0.94f, 1f);

    public int BadgeCount => 0;

    private readonly ChirperStore store;
    private readonly LodestoneService lodestone;
    private readonly RemoteImageCache images;
    private readonly ChirperAvatarComposer avatar;
    private readonly ChirperUi ui = new();

    private readonly ViewRouter<ChirperRoute> router;
    private readonly RouterDraw<ChirperRoute> drawView;
    private readonly Action back;

    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;

    private ChirperFeedScope activeScope = ChirperFeedScope.ForYou;
    private float tabSegmentAnim;
    private float sinceForYou;
    private float sinceFollowing;

    private string draft = string.Empty;
    private bool composeFocus;
    private string composeStatus = string.Empty;
    private volatile int composeOutcome;
    private string searchDraft = string.Empty;
    private readonly ChirperActionReveal actions = new();
    private string commentDraft = string.Empty;

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

    private string? deleteCommentPostId;
    private string? deleteCommentId;
    private string deleteCommentStatus = string.Empty;
    private volatile bool deleteCommentSubmitting;

    private Spring confirmSpring;
    private bool confirmWasPostDelete;

    public ChirperApp(AethernetSession session, AethernetClient client, LodestoneService lodestone, HttpService http, PhotoLibrary library)
    {
        store = new ChirperStore(session, client);
        this.lodestone = lodestone;
        images = new RemoteImageCache(http);
        avatar = new ChirperAvatarComposer(store, library);

        router = new ViewRouter<ChirperRoute>(ChirperRoute.Home);
        drawView = DrawView;
        back = () => router.Pop();
    }

    public void OnOpened()
    {
        router.Reset();
        actions.Reset();
        if (store.IsSignedIn)
        {
            store.EnsureMe();
            store.RefreshFeed(ChirperFeedScope.ForYou);
            store.RefreshFeed(ChirperFeedScope.Following);
        }
    }

    public void OnClosed()
    {
        router.Reset();
        draft = string.Empty;
        searchDraft = string.Empty;
        actions.Reset();
        commentDraft = string.Empty;
        reportTargetType = null;
        reportTargetId = null;
        reportReasonDraft = string.Empty;
        reportStatus = string.Empty;
        deleteTargetId = null;
        deleteStatus = string.Empty;
        confirmSpring.SnapTo(0f);
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
        router.Draw(context.Content, ChirperUi.Transparent, ImGui.GetIO().DeltaTime, drawView);
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
            case ChirperScreen.Discover:
                DrawDiscover(area);
                break;
            case ChirperScreen.Thread:
                DrawThread(area, route.PostId!);
                break;
            default:
                DrawHome(area);
                break;
        }

        var showPostDelete = deleteTargetId is not null;
        var showCommentDelete = deleteCommentPostId is not null && deleteCommentId is not null && route.Screen == ChirperScreen.Thread;
        var showConfirmation = showPostDelete || showCommentDelete;

        if (showConfirmation)
        {
            confirmWasPostDelete = showPostDelete;
        }

        var target = showConfirmation ? 1f : 0f;
        confirmSpring.Step(target, 0.18f, ImGui.GetIO().DeltaTime);
        var opacity = confirmSpring.Value;

        if (showConfirmation || !confirmSpring.IsResting(target, 0.001f, 0.005f))
        {
            var screen = SceneChrome.ScreenFrom(area, theme, ImGuiHelpers.GlobalScale);

            ImGui.SetCursorScreenPos(area.Min);
            using (ImRaii.Child("##confirmOverlay", area.Size, false,
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoBackground))
            {
                if (showPostDelete || (confirmWasPostDelete && !showConfirmation && opacity > 0.005f))
                {
                    ConfirmDialog.Draw(
                        screen,
                        theme,
                        Loc.T(L.Chirper.DeleteConfirmMessage),
                        Loc.T(L.Chirper.DeleteConfirm),
                        Loc.T(L.Chirper.DeleteCancel),
                        Loc.T(L.Chirper.Saving),
                        deleteSubmitting,
                        deleteStatus.Length > 0 ? deleteStatus : null,
                        out var postCanceled,
                        out var postConfirmed,
                        opacity);

                    if (postCanceled)
                    {
                        deleteTargetId = null;
                    }

                    if (postConfirmed)
                    {
                        SubmitDelete(deleteTargetId!);
                    }
                }
                else if (showCommentDelete || (!confirmWasPostDelete && !showConfirmation && opacity > 0.005f))
                {
                    ConfirmDialog.Draw(
                        screen,
                        theme,
                        Loc.T(L.Chirper.DeleteCommentConfirmMessage),
                        Loc.T(L.Chirper.DeleteConfirm),
                        Loc.T(L.Chirper.DeleteCancel),
                        Loc.T(L.Chirper.Saving),
                        deleteCommentSubmitting,
                        deleteCommentStatus.Length > 0 ? deleteCommentStatus : null,
                        out var commentCanceled,
                        out var commentConfirmed,
                        opacity);

                    if (commentCanceled)
                    {
                        deleteCommentPostId = null;
                        deleteCommentId = null;
                    }

                    if (commentConfirmed)
                    {
                        SubmitDeleteComment(deleteCommentPostId!, deleteCommentId!);
                    }
                }
            }
        }

        if (!showConfirmation && confirmSpring.IsResting(0f, 0.001f, 0.005f))
        {
            confirmSpring.SnapTo(0f);
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
            Typography.DrawCentered(body.Center, Loc.T(L.Chirper.SetUpAccount), ChirperUi.MutedInk);
            return;
        }

        var segmentHeight = 38f * scale;
        var tabsRect = new Rect(
            new Vector2(area.Min.X + 16f * scale, top + 2f * scale),
            new Vector2(area.Max.X - 16f * scale, top + 2f * scale + segmentHeight));
        var selected = DrawScopeSegments(tabsRect);
        if (selected != (int)activeScope)
        {
            activeScope = (ChirperFeedScope)selected;
            actions.Reset();
            EnsureLoaded(activeScope);
        }

        sinceForYou += ImGui.GetIO().DeltaTime;
        sinceFollowing += ImGui.GetIO().DeltaTime;
        TickRefresh(activeScope);

        var listRect = new Rect(new Vector2(area.Min.X, tabsRect.Max.Y + 6f * scale), area.Max);
        DrawFeedList(listRect, activeScope);
        DrawComposeFab(listRect);
    }

    private int DrawScopeSegments(Rect rect)
    {
        var drawList = ImGui.GetWindowDrawList();
        var radius = rect.Height * 0.5f;
        Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.08f)));

        var target = (int)activeScope == 1 ? 1f : 0f;
        tabSegmentAnim += (target - tabSegmentAnim) * MathF.Min(1f, ImGui.GetIO().DeltaTime * 14f);

        var scale = ImGuiHelpers.GlobalScale;
        var half = rect.Width * 0.5f;
        var pad = 3f * scale;
        var thumbMinX = rect.Min.X + pad + tabSegmentAnim * half;
        var thumbMin = new Vector2(thumbMinX, rect.Min.Y + pad);
        var thumbMax = new Vector2(thumbMinX + half - pad * 2f, rect.Max.Y - pad);
        Squircle.Fill(drawList, thumbMin, thumbMax, (thumbMax.Y - thumbMin.Y) * 0.5f, ImGui.GetColorU32(Accent));

        var forYouRect = new Rect(rect.Min, new Vector2(rect.Min.X + half, rect.Max.Y));
        var followingRect = new Rect(new Vector2(rect.Min.X + half, rect.Min.Y), rect.Max);
        DrawSegmentLabel(forYouRect, Loc.T(L.Chirper.ForYou), activeScope == ChirperFeedScope.ForYou);
        DrawSegmentLabel(followingRect, Loc.T(L.Chirper.Following), activeScope == ChirperFeedScope.Following);

        var selected = (int)activeScope;
        if (HoverClick(forYouRect.Min, forYouRect.Max))
        {
            selected = 0;
        }

        if (HoverClick(followingRect.Min, followingRect.Max))
        {
            selected = 1;
        }

        return selected;
    }

    private static void DrawSegmentLabel(Rect rect, string label, bool active)
    {
        var ink = active ? new Vector4(1f, 1f, 1f, 1f) : ChirperUi.MutedInk;
        Typography.DrawCentered(rect.Center, label, ink, 0.9f, active ? FontWeight.SemiBold : FontWeight.Medium);
    }

    private void DrawFeedList(Rect listRect, ChirperFeedScope scope)
    {
        var snapshot = store.Feed(scope);
        using (AppSurface.Begin(listRect))
        {
            if (snapshot.Length == 0)
            {
                var message = store.IsLoading(scope)
                    ? Loc.T(L.Common.Loading)
                    : scope == ChirperFeedScope.Following ? Loc.T(L.Chirper.FollowingEmpty) : Loc.T(L.Chirper.ExploreEmpty);
                Typography.DrawCentered(new Vector2(listRect.Center.X, listRect.Min.Y + 90f * ImGuiHelpers.GlobalScale), message, ChirperUi.MutedInk);
            }
            else
            {
                ImGui.Dummy(new Vector2(0f, FeedTopPadding * ImGuiHelpers.GlobalScale));
                for (var index = 0; index < snapshot.Length; index++)
                {
                    DrawPost(snapshot[index]);
                }

                ImGui.Dummy(new Vector2(0f, 72f * ImGuiHelpers.GlobalScale));
            }
        }
    }

    private void DrawPost(PostDto post, bool isThreadHead = false)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = 14f * scale;
        var radius = 20f * scale;

        var avatarCenter = new Vector2(origin.X + pad + radius, origin.Y + pad + radius);
        var contentLeft = avatarCenter.X + radius + 12f * scale;
        var contentRight = origin.X + width - pad;
        var contentWidth = contentRight - contentLeft;

        var displayName = string.IsNullOrEmpty(post.AuthorDisplayName) ? post.AuthorName : post.AuthorDisplayName;
        var nameSize = Typography.Measure(displayName, 1.05f, FontWeight.SemiBold);

        var textTop = origin.Y + pad + nameSize.Y + 6f * scale;
        var textHeight = post.Text.Length > 0 ? MeasureWrapped(post.Text, contentWidth, 1.05f) : 0f;
        var contentBottom = MathF.Max(avatarCenter.Y + radius, textTop + textHeight);
        var actionsTop = contentBottom + 8f * scale;
        var actionsHeight = 30f * scale;
        var cardBottom = actionsTop + actionsHeight + pad * 0.5f;

        ui.Card(drawList, origin, new Vector2(origin.X + width, cardBottom), 18f * scale);

        DrawAvatar(drawList, avatarCenter, radius, post.AuthorName, post.AuthorWorld, post.AuthorAvatarUrl, 0.95f, 48);
        if (HoverClick(avatarCenter - new Vector2(radius, radius), avatarCenter + new Vector2(radius, radius)))
        {
            OpenProfile(post.AuthorId);
        }

        Typography.Draw(new Vector2(contentLeft, origin.Y + pad), displayName, theme.TextStrong, 1.05f, FontWeight.SemiBold);
        var meta = post.AuthorHandle.Length > 0
            ? $"@{post.AuthorHandle} · {RelativeTime(post.CreatedAtUnix)}"
            : $"{post.AuthorWorld} · {RelativeTime(post.CreatedAtUnix)}";
        var metaSize = Typography.Measure(meta, 0.9f);
        Typography.Draw(new Vector2(contentLeft + nameSize.X + 7f * scale, origin.Y + pad + (nameSize.Y - metaSize.Y) * 0.5f), meta, ChirperUi.MutedInk, 0.9f);
        if (HoverClick(new Vector2(contentLeft, origin.Y + pad), new Vector2(contentRight - 24f * scale, origin.Y + pad + nameSize.Y)))
        {
            OpenProfile(post.AuthorId);
        }

        if (post.Text.Length > 0)
        {
            ImGui.SetCursorScreenPos(new Vector2(contentLeft, textTop));
            var wrapPos = contentRight - ImGui.GetWindowPos().X;
            ImGui.PushTextWrapPos(wrapPos);
            using (Plugin.Fonts.Push(1.05f))
            using (ImRaii.PushColor(ImGuiCol.Text, ChirperUi.BodyInk))
            {
                ImGui.TextWrapped(post.Text);
            }

            ImGui.PopTextWrapPos();
        }

        DrawPostActions(post, contentLeft, contentWidth, actionsTop + actionsHeight * 0.5f, isThreadHead);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cardBottom - origin.Y));

        var reportActive = reportTargetType == "post" && reportTargetId == post.Id;
        if (reportActive)
        {
            ImGui.Dummy(new Vector2(0f, 8f * scale));
            var composerLeft = origin.X + pad;
            var composerWidth = width - pad * 2f;
            DrawReportComposer(composerLeft, composerWidth);
        }

        ImGui.Dummy(new Vector2(0f, 10f * scale));
    }

    private void DrawPostActions(PostDto post, float left, float width, float centerY, bool isThreadHead)
    {
        if (actions.IsShowing(post.Id, ChirperActionReveal.Panel.Picker))
        {
            DrawReactionPicker(post, left, centerY);
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

    private static float MeasureWrapped(string text, float wrapWidth, float fontScale)
    {
        using (Plugin.Fonts.Push(fontScale))
        {
            return ImGui.CalcTextSize(text, false, wrapWidth).Y;
        }
    }

    private void DrawDefaultActions(PostDto post, float left, float width, float centerY, bool isThreadHead)
    {
        var scale = ImGuiHelpers.GlobalScale;

        var commentCenter = new Vector2(left + 11f * scale, centerY);
        if (DrawIconButton(commentCenter, 14f * scale, FontAwesomeIcon.Comment.ToIconString(), ChirperUi.MutedInk, new Vector4(0f, 0f, 0f, 0f), 1f, Loc.T(L.Chirper.Reply)) && !isThreadHead)
        {
            OpenThread(post);
        }

        var cursorX = commentCenter.X + 20f * scale;
        if (post.CommentCount > 0)
        {
            var countText = post.CommentCount.ToString(Loc.Culture);
            var countSize = Typography.Measure(countText, 0.9f, FontWeight.Medium);
            Typography.Draw(new Vector2(cursorX, centerY - countSize.Y * 0.5f), countText, ChirperUi.MutedInk, 0.9f, FontWeight.Medium);
            cursorX += countSize.X + 6f * scale;
        }

        cursorX += 12f * scale;
        var triggerCenter = new Vector2(cursorX + 11f * scale, centerY);
        if (DrawIconButton(triggerCenter, 14f * scale, FontAwesomeIcon.GrinBeam.ToIconString(), ChirperUi.MutedInk, new Vector4(0f, 0f, 0f, 0f), 1f, Loc.T(L.Chirper.React)))
        {
            actions.Open(post.Id, ChirperActionReveal.Panel.Picker);
        }

        cursorX = triggerCenter.X + 20f * scale;
        var chipLimit = left + width - 34f * scale;
        var shown = 0;
        for (var kind = 0; kind < ChirperReactions.Count && shown < 3; kind++)
        {
            if (post.ReactionCounts[kind] <= 0 || cursorX >= chipLimit)
            {
                continue;
            }

            cursorX += DrawReactionChip(post, cursorX, centerY, kind);
            shown++;
        }

        var ellipsisCenter = new Vector2(left + width - 12f * scale, centerY);
        if (DrawIconButton(ellipsisCenter, 13f * scale, FontAwesomeIcon.EllipsisH.ToIconString(), ChirperUi.BodyInk, new Vector4(0f, 0f, 0f, 0f), 0.9f, Loc.T(L.Chirper.More)))
        {
            actions.Open(post.Id, ChirperActionReveal.Panel.Menu);
        }
    }

    private float DrawReactionChip(PostDto post, float x, float centerY, int kind)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var color = ChirperReactions.Color(kind);
        var active = post.MyReaction == kind;
        var countText = post.ReactionCounts[kind].ToString(Loc.Culture);
        var countSize = Typography.Measure(countText, 0.82f, FontWeight.Medium);
        var glyphWidth = 11f * scale;
        var padX = 7f * scale;
        var gap = 4f * scale;
        var chipWidth = padX + glyphWidth + gap + countSize.X + padX;
        var chipHeight = 22f * scale;
        var min = new Vector2(x, centerY - chipHeight * 0.5f);
        var max = new Vector2(x + chipWidth, centerY + chipHeight * 0.5f);
        var drawList = ImGui.GetWindowDrawList();
        var hovered = ImGui.IsMouseHoveringRect(min, max);
        var background = active
            ? Palette.WithAlpha(color, 0.24f)
            : (hovered ? new Vector4(1f, 1f, 1f, 0.14f) : ChirperUi.FieldSurface);
        Squircle.Fill(drawList, min, max, chipHeight * 0.5f, ImGui.GetColorU32(background));
        DrawIcon(new Vector2(min.X + padX + glyphWidth * 0.5f, centerY), ChirperReactions.Glyph(kind), color, 0.82f);
        Typography.Draw(new Vector2(min.X + padX + glyphWidth + gap, centerY - countSize.Y * 0.5f), countText, active ? color : ChirperUi.MutedInk, 0.82f, FontWeight.Medium);

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
            var background = active ? Palette.WithAlpha(color, 0.22f) : ChirperUi.FieldSurface;
            var reveal = ChirperActionReveal.Stagger(actions.Progress, kind, count);
            if (DrawRevealIcon(center, iconRadius, ChirperReactions.Glyph(kind), color, background, 1f, reveal, ChirperReactions.Label(kind), interactive))
            {
                store.ToggleReaction(post, kind);
                actions.Dismiss();
            }
        }

        var closeCenter = new Vector2(left + iconRadius + ChirperReactions.Count * step, centerY);
        var closeReveal = ChirperActionReveal.Stagger(actions.Progress, ChirperReactions.Count, count);
        if (DrawRevealIcon(closeCenter, iconRadius, FontAwesomeIcon.Times.ToIconString(), ChirperUi.MutedInk, ChirperUi.FieldSurface, 0.9f, closeReveal, Loc.T(L.Common.Close), interactive))
        {
            actions.Dismiss();
        }
    }

    private void DrawOverflowMenuRow(PostDto post, float left, float width, float centerY)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var step = 34f * scale;
        var iconRadius = 15f * scale;
        var anchorX = left + width - 12f * scale;
        var interactive = !actions.Closing;
        var mine = store.Me is { } me && me.Id == post.AuthorId;
        var count = mine ? 2 : 3;
        var slot = 0;

        var closeCenter = new Vector2(anchorX - slot * step, centerY);
        if (DrawRevealIcon(closeCenter, iconRadius, FontAwesomeIcon.Times.ToIconString(), ChirperUi.MutedInk, ChirperUi.FieldSurface, 0.9f, ChirperActionReveal.Stagger(actions.Progress, slot, count), Loc.T(L.Common.Close), interactive))
        {
            actions.Dismiss();
        }

        slot++;

        if (mine)
        {
            var trashCenter = new Vector2(anchorX - slot * step, centerY);
            if (DrawRevealIcon(trashCenter, iconRadius, FontAwesomeIcon.Trash.ToIconString(), theme.Danger, Palette.WithAlpha(theme.Danger, 0.16f), 0.85f, ChirperActionReveal.Stagger(actions.Progress, slot, count), Loc.T(L.Chirper.DeleteConfirm), interactive))
            {
                deleteTargetId = post.Id;
                deleteStatus = string.Empty;
                reportTargetType = null;
                reportTargetId = null;
                actions.Dismiss();
            }

            return;
        }

        var reportCenter = new Vector2(anchorX - slot * step, centerY);
        if (DrawRevealIcon(reportCenter, iconRadius, FontAwesomeIcon.Flag.ToIconString(), theme.Danger, Palette.WithAlpha(theme.Danger, 0.16f), 0.85f, ChirperActionReveal.Stagger(actions.Progress, slot, count), Loc.T(L.Chirper.ReportSubmit), interactive))
        {
            reportTargetType = "post";
            reportTargetId = post.Id;
            reportReasonDraft = string.Empty;
            reportStatus = string.Empty;
            deleteTargetId = null;
            actions.Dismiss();
        }

        slot++;

        var followGlyph = post.IsFollowing ? FontAwesomeIcon.UserCheck.ToIconString() : FontAwesomeIcon.UserPlus.ToIconString();
        var followColor = post.IsFollowing ? theme.Accent : theme.TextStrong;
        var followTip = Loc.T(post.IsFollowing ? L.Chirper.Unfollow : L.Chirper.Follow);
        var followCenter = new Vector2(anchorX - slot * step, centerY);
        if (DrawRevealIcon(followCenter, iconRadius, followGlyph, followColor, ChirperUi.FieldSurface, 0.9f, ChirperActionReveal.Stagger(actions.Progress, slot, count), followTip, interactive))
        {
            store.SetFollow(post.AuthorId, !post.IsFollowing);
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

            Typography.DrawCentered(new Vector2(area.Center.X, top + 60f * scale), Loc.T(L.Common.Loading), ChirperUi.MutedInk);
            return;
        }

        var composerHeight = 50f * scale;
        var body = new Rect(new Vector2(area.Min.X, top), new Vector2(area.Max.X, area.Max.Y - composerHeight));

        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, FeedTopPadding * scale));
            DrawPost(post, true);

            var comments = store.DetailComments;
            ImGui.Dummy(new Vector2(0f, 2f * scale));
            ui.SectionHeading(comments.Length > 0 ? $"{Loc.T(L.Chirper.RepliesTitle)} · {comments.Length}" : Loc.T(L.Chirper.RepliesTitle));

            if (comments.Length == 0)
            {
                if (!store.DetailLoading)
                {
                    Typography.Draw(new Vector2(ImGui.GetCursorScreenPos().X + 2f * scale, ImGui.GetCursorScreenPos().Y), Loc.T(L.Chirper.NoComments), ChirperUi.MutedInk, 0.85f);
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

        DrawCommentComposer(new Rect(new Vector2(area.Min.X, area.Max.Y - composerHeight), area.Max), postId);
    }

    private void DrawComment(CommentDto comment)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var drawList = ImGui.GetWindowDrawList();
        var radius = 15f * scale;
        var avatarCenter = new Vector2(origin.X + radius, origin.Y + radius);
        DrawAvatar(drawList, avatarCenter, radius, comment.AuthorName, string.Empty, comment.AuthorAvatarUrl, 0.85f, 32);
        if (HoverClick(avatarCenter - new Vector2(radius, radius), avatarCenter + new Vector2(radius, radius)))
        {
            OpenProfile(comment.AuthorId);
        }

        var textLeft = origin.X + radius * 2f + 10f * scale;
        var displayName = string.IsNullOrEmpty(comment.AuthorDisplayName) ? comment.AuthorName : comment.AuthorDisplayName;
        var nameSize = Typography.Measure(displayName, 0.95f, FontWeight.SemiBold);
        Typography.Draw(new Vector2(textLeft, origin.Y), displayName, theme.TextStrong, 0.95f, FontWeight.SemiBold);

        var meta = comment.AuthorHandle.Length > 0
            ? $"@{comment.AuthorHandle} · {RelativeTime(comment.CreatedAtUnix)}"
            : RelativeTime(comment.CreatedAtUnix);
        var metaSize = Typography.Measure(meta, 0.82f);
        Typography.Draw(new Vector2(textLeft + nameSize.X + 7f * scale, origin.Y + (nameSize.Y - metaSize.Y) * 0.5f), meta, ChirperUi.MutedInk, 0.82f);

        ImGui.SetCursorScreenPos(new Vector2(textLeft, origin.Y + nameSize.Y + 6f * scale));
        var commentWrapPos = (origin.X + width - 4f * scale) - ImGui.GetWindowPos().X;
        ImGui.PushTextWrapPos(commentWrapPos);
        using (ImRaii.PushColor(ImGuiCol.Text, ChirperUi.BodyInk))
        {
            ImGui.TextWrapped(comment.Text);
        }

        ImGui.PopTextWrapPos();

        if (store.Me is { } me && me.Id == comment.AuthorId && store.DetailPost is { } post)
        {
            var trashCenter = new Vector2(origin.X + width - 10f * scale, origin.Y + 9f * scale);
            var trashHitRadius = 11f * scale;
            if (DrawIconButton(trashCenter, trashHitRadius, FontAwesomeIcon.Times.ToIconString(), ChirperUi.MutedInk, new Vector4(0f, 0f, 0f, 0f), 0.75f))
            {
                deleteCommentPostId = post.Id;
                deleteCommentId = comment.Id;
                deleteCommentStatus = string.Empty;
            }

            var trashHovered = ImGui.IsMouseHoveringRect(trashCenter - new Vector2(trashHitRadius, trashHitRadius), trashCenter + new Vector2(trashHitRadius, trashHitRadius));
            if (trashHovered)
            {
                DrawDeleteCommentTooltip(trashCenter, trashHitRadius, scale);
            }
        }

        var bottom = MathF.Max(ImGui.GetCursorScreenPos().Y, origin.Y + radius * 2f);
        ImGui.SetCursorScreenPos(new Vector2(origin.X, bottom));
        ImGui.Dummy(new Vector2(width, 12f * scale));
    }

    private void DrawCommentComposer(Rect bar, string postId)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddLine(bar.Min, new Vector2(bar.Max.X, bar.Min.Y), ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)), 1f);

        var pillMin = new Vector2(bar.Min.X + 12f * scale, bar.Min.Y + 8f * scale);
        var pillMax = new Vector2(bar.Max.X - 56f * scale, bar.Max.Y - 8f * scale);
        Squircle.Fill(drawList, pillMin, pillMax, (pillMax.Y - pillMin.Y) * 0.5f, ImGui.GetColorU32(ChirperUi.FieldSurface));

        ImGui.SetCursorScreenPos(new Vector2(pillMin.X + 14f * scale, (pillMin.Y + pillMax.Y) * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(pillMax.X - pillMin.X - 24f * scale);
        var submitted = false;
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, ChirperUi.TitleInk))
        {
            submitted = ImGui.InputTextWithHint("##chirperComment", Loc.T(L.Chirper.AddComment), ref commentDraft, MaxCommentLength, ImGuiInputTextFlags.EnterReturnsTrue);
        }

        var canSend = commentDraft.Trim().Length > 0 && !store.Commenting;
        var sendCenter = new Vector2(bar.Max.X - 28f * scale, bar.Center.Y);
        if ((DrawIconButton(sendCenter, 16f * scale, FontAwesomeIcon.PaperPlane.ToIconString(), canSend ? Accent : ChirperUi.MutedInk, new Vector4(0f, 0f, 0f, 0f), 0.95f) || submitted) && canSend)
        {
            var text = commentDraft;
            commentDraft = string.Empty;
            store.AddComment(postId, text, _ => { });
        }
    }

    private void DrawComposeFab(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var radius = 26f * scale;
        var center = new Vector2(area.Max.X - radius - 16f * scale, area.Max.Y - radius - 18f * scale);
        var drawList = ImGui.GetWindowDrawList();
        var hovered = ImGui.IsMouseHoveringRect(center - new Vector2(radius, radius), center + new Vector2(radius, radius));

        drawList.AddCircleFilled(center + new Vector2(0f, 2f * scale), radius, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.30f)), 32);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(hovered ? Palette.Mix(Accent, theme.TextStrong, 0.12f) : Accent), 32);

        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var glyph = FontAwesomeIcon.Feather.ToIconString();
            var size = ImGui.CalcTextSize(glyph);
            ImGui.SetCursorScreenPos(center - size * 0.5f);
            using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(1f, 1f, 1f, 1f)))
            {
                ImGui.TextUnformatted(glyph);
            }
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                composeFocus = true;
                router.Push(ChirperRoute.Compose);
            }
        }
    }

    private void DrawCompose(Rect area)
    {
        if (composeOutcome == 1)
        {
            composeOutcome = 0;
            draft = string.Empty;
            composeStatus = string.Empty;
            sinceForYou = FeedRefreshSeconds;
            sinceFollowing = FeedRefreshSeconds;
            router.Pop();
            return;
        }

        if (composeOutcome == 2)
        {
            composeOutcome = 0;
            composeStatus = Loc.T(L.Account.CannotReach);
        }

        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Chirper.NewChirp), back);

        var canPost = !string.IsNullOrWhiteSpace(draft) && !store.Posting;
        if (DrawHeaderAction(area, store.Posting ? Loc.T(L.Chirper.Saving) : Loc.T(L.Chirper.Post), canPost))
        {
            Submit();
        }

        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);

        using (AppSurface.Begin(body))
        {
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ImGui.GetContentRegionAvail().X;

            var footerHeight = 40f * scale;
            var cardMin = origin;
            var cardMax = new Vector2(origin.X + width, area.Max.Y - footerHeight);
            ui.Card(drawList, cardMin, cardMax, 18f * scale);

            var pad = 14f * scale;
            var radius = 20f * scale;
            var me = store.Me;
            var displayName = me is null ? string.Empty : (string.IsNullOrEmpty(me.DisplayName) ? me.Name : me.DisplayName);
            if (me is not null)
            {
                DrawAvatar(drawList, new Vector2(cardMin.X + pad + radius, cardMin.Y + pad + radius), radius, me.Name, me.World, me.AvatarUrl, 0.95f, 48);
            }

            var inputLeft = pad + radius * 2f + 12f * scale;
            var inputX = cardMin.X + inputLeft;
            var nameSize = displayName.Length > 0 ? Typography.Measure(displayName, 1.05f, FontWeight.SemiBold) : Vector2.Zero;
            if (displayName.Length > 0)
            {
                Typography.Draw(new Vector2(inputX, cardMin.Y + pad), displayName, theme.TextStrong, 1.05f, FontWeight.SemiBold);
            }

            var inputTop = cardMin.Y + pad + nameSize.Y + 6f * scale;
            var inputWidth = width - inputLeft - pad;
            var inputHeight = cardMax.Y - inputTop - pad;

            ImGui.SetCursorScreenPos(new Vector2(inputX, inputTop));
            ImGui.SetNextItemWidth(inputWidth);
            if (composeFocus)
            {
                ImGui.SetKeyboardFocusHere();
                composeFocus = false;
            }

            using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
            using (ImRaii.PushColor(ImGuiCol.Text, ChirperUi.TitleInk))
            using (Plugin.Fonts.Push(1.15f))
            {
                ImGui.InputTextMultiline("##chirpBody", ref draft, MaxPostLength, new Vector2(inputWidth, inputHeight), ImGuiInputTextFlags.None);
            }

            if (draft.Length == 0)
            {
                Typography.Draw(new Vector2(inputX + 4f * scale, inputTop + 2f * scale), Loc.T(L.Chirper.Compose), ChirperUi.MutedInk, 1.15f);
            }

            var footerY = area.Max.Y - footerHeight * 0.5f;
            if (composeStatus.Length > 0)
            {
                Typography.Draw(new Vector2(origin.X + 2f * scale, footerY - Typography.Measure(composeStatus, 0.85f).Y * 0.5f), composeStatus, theme.Danger, 0.85f);
            }

            var remaining = MaxPostLength - draft.Length;
            var counterColor = remaining < 40 ? (remaining < 0 ? theme.Danger : new Vector4(0.95f, 0.65f, 0.20f, 1f)) : ChirperUi.MutedInk;
            var counter = remaining.ToString(Loc.Culture);
            var counterSize = Typography.Measure(counter, 0.9f, FontWeight.Medium);
            Typography.Draw(new Vector2(area.Max.X - 4f * scale - counterSize.X, footerY - counterSize.Y * 0.5f), counter, counterColor, 0.9f, FontWeight.Medium);
        }
    }

    private void Submit()
    {
        if (string.IsNullOrWhiteSpace(draft) || store.Posting)
        {
            return;
        }

        composeStatus = string.Empty;
        store.Compose(draft, ok => composeOutcome = ok ? 1 : 2);
    }

    private void DrawProfile(Rect area, string userId)
    {
        if (store.ProfileUserId != userId)
        {
            store.OpenProfile(userId);
        }

        var user = store.ProfileUser;
        var title = user is null ? Loc.T(L.Apps.Chirper) : (string.IsNullOrEmpty(user.DisplayName) ? user.Name : user.DisplayName);
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, title, back);

        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);

        if (store.ProfileFailed)
        {
            Typography.DrawCentered(body.Center, Loc.T(L.Chirper.ProfileError), ChirperUi.MutedInk);
            return;
        }

        if (user is null)
        {
            Typography.DrawCentered(body.Center, Loc.T(L.Common.Loading), ChirperUi.MutedInk);
            return;
        }

        using (AppSurface.Begin(body))
        {
            DrawProfileHeader(user);

            var posts = store.ProfilePosts;
            ui.SectionHeading(Loc.T(L.Chirper.ChirpsTitle));
            if (posts.Length == 0)
            {
                Typography.DrawCentered(new Vector2(body.Center.X, ImGui.GetCursorScreenPos().Y + 40f * scale), Loc.T(L.Chirper.Empty), ChirperUi.MutedInk);
            }
            else
            {
                for (var index = 0; index < posts.Length; index++)
                {
                    DrawPost(posts[index]);
                }

                ImGui.Dummy(new Vector2(0f, 24f * scale));
            }
        }
    }

    private void DrawProfileHeader(UserDto user)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = 16f * scale;
        var innerLeft = origin.X + pad;
        var innerWidth = width - pad * 2f;

        var displayName = string.IsNullOrEmpty(user.DisplayName) ? user.Name : user.DisplayName;
        var avatarRadius = 40f * scale;
        var handleLine = user.Handle.Length > 0 ? $"@{user.Handle}" : string.Empty;
        var worldLine = $"{user.Name} · {user.World}";

        var lineGap = 3f * scale;
        var nameH = Typography.Measure(displayName, 1.4f, FontWeight.Bold).Y;
        var handleH = handleLine.Length > 0 ? Typography.Measure(handleLine, 0.95f).Y + lineGap : 0f;
        var worldH = Typography.Measure(worldLine, 0.95f).Y;
        var bioH = user.Bio.Length > 0 ? 8f * scale + MeasureWrapped(user.Bio, innerWidth, 1f) : 0f;

        var textTop = origin.Y + pad + avatarRadius * 2f + 14f * scale;
        var cardBottom = textTop + nameH + lineGap + handleH + worldH + bioH + pad;
        ui.Card(drawList, origin, new Vector2(origin.X + width, cardBottom), 20f * scale);

        var avatarCenter = new Vector2(innerLeft + avatarRadius, origin.Y + pad + avatarRadius);
        drawList.AddCircleFilled(avatarCenter, avatarRadius + 2.5f * scale, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.14f)), 64);
        DrawAvatar(drawList, avatarCenter, avatarRadius, user.Name, user.World, user.AvatarUrl, 1.5f, 64);

        var buttonHeight = 34f * scale;
        var buttonWidth = 122f * scale;
        var buttonMax = new Vector2(origin.X + width - pad, avatarCenter.Y + buttonHeight * 0.5f);
        var buttonRect = new Rect(new Vector2(buttonMax.X - buttonWidth, buttonMax.Y - buttonHeight), buttonMax);
        var reportShown = false;
        if (user.IsMe)
        {
            if (DrawPillButton(buttonRect, Loc.T(L.Chirper.EditProfile), false))
            {
                editLoadedFor = null;
                router.Push(ChirperRoute.EditProfile);
            }
        }
        else
        {
            var reportCenter = new Vector2(buttonRect.Min.X - buttonHeight * 0.5f - 10f * scale, avatarCenter.Y);
            reportShown = DrawReportToggle(reportCenter, buttonHeight * 0.5f, "user", user.Id);

            if (DrawPillButton(buttonRect, user.IsFollowing ? Loc.T(L.Chirper.Following) : Loc.T(L.Chirper.Follow), !user.IsFollowing))
            {
                store.SetFollow(user.Id, !user.IsFollowing);
            }
        }

        Typography.Draw(new Vector2(innerLeft, textTop), displayName, theme.TextStrong, 1.4f, FontWeight.Bold);
        var textY = textTop + nameH + lineGap;
        if (handleLine.Length > 0)
        {
            Typography.Draw(new Vector2(innerLeft, textY), handleLine, ChirperUi.MutedInk, 0.95f);
            textY += handleH;
        }

        Typography.Draw(new Vector2(innerLeft, textY), worldLine, ChirperUi.MutedInk, 0.95f);
        textY += worldH;

        if (user.Bio.Length > 0)
        {
            ImGui.SetCursorScreenPos(new Vector2(innerLeft, textY + 8f * scale));
            var bioWrapPos = (innerLeft + innerWidth) - ImGui.GetWindowPos().X;
            ImGui.PushTextWrapPos(bioWrapPos);
            using (ImRaii.PushColor(ImGuiCol.Text, ChirperUi.BodyInk))
            {
                ImGui.TextWrapped(user.Bio);
            }

            ImGui.PopTextWrapPos();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cardBottom - origin.Y + 10f * scale));

        if (reportShown)
        {
            DrawReportComposer(innerLeft, innerWidth);
            ImGui.Dummy(new Vector2(0f, 10f * scale));
        }

        DrawProfileStats(user);
        ImGui.Dummy(new Vector2(0f, 14f * scale));
    }

    private void DrawProfileStats(UserDto user)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = 64f * scale;
        ui.Card(drawList, origin, new Vector2(origin.X + width, origin.Y + height), 18f * scale);

        var third = width / 3f;
        var centerY = origin.Y + height * 0.5f;
        var dividerColor = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.08f));
        for (var index = 1; index < 3; index++)
        {
            var x = origin.X + third * index;
            drawList.AddLine(new Vector2(x, origin.Y + 14f * scale), new Vector2(x, origin.Y + height - 14f * scale), dividerColor, 1f);
        }

        var followersLabel = Loc.Plural(L.Account.Followers, user.Followers).Split(' ', 2)[^1];
        DrawStatColumn(origin.X + third * 0f, third, centerY, user.Following.ToString(Loc.Culture), Loc.T(L.Chirper.Following));
        DrawStatColumn(origin.X + third * 1f, third, centerY, user.Followers.ToString(Loc.Culture), followersLabel);
        DrawStatColumn(origin.X + third * 2f, third, centerY, user.Posts.ToString(Loc.Culture), PostsLabel(user.Posts));

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawStatColumn(float left, float columnWidth, float centerY, string value, string label)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var center = left + columnWidth * 0.5f;
        Typography.DrawCentered(new Vector2(center, centerY - 10f * scale), value, theme.TextStrong, 1.25f, FontWeight.Bold);
        Typography.DrawCentered(new Vector2(center, centerY + 13f * scale), label, ChirperUi.MutedInk, 0.8f);
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
        using (ImRaii.PushColor(ImGuiCol.FrameBg, ChirperUi.FieldSurface))
        using (ImRaii.PushColor(ImGuiCol.Text, ChirperUi.TitleInk))
        {
            ImGui.InputTextWithHint("##reportReason", Loc.T(L.Chirper.ReportReasonHint), ref reportReasonDraft, MaxReportReasonLength);
        }

        var buttonRect = new Rect(new Vector2(left + width - buttonWidth, origin.Y - 2f * scale), new Vector2(left + width, origin.Y - 2f * scale + buttonHeight));
        var canSubmit = !reportSubmitting;
        if (DrawPillButton(buttonRect, reportSubmitting ? Loc.T(L.Chirper.Saving) : Loc.T(L.Chirper.ReportSubmit), canSubmit) && canSubmit)
        {
            SubmitReport();
        }

        ImGui.SetCursorScreenPos(new Vector2(left, origin.Y + buttonHeight + 2f * scale));
        if (reportStatus.Length > 0)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, ChirperUi.MutedInk))
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
            reportStatus = Loc.T(ok ? L.Chirper.ReportSent : L.Chirper.ReportFailed);
            if (ok)
            {
                reportTargetType = null;
                reportTargetId = null;
            }
        });
    }

    private void SubmitDeleteComment(string postId, string commentId)
    {
        if (deleteCommentSubmitting || deleteCommentPostId != postId || deleteCommentId != commentId)
        {
            return;
        }

        deleteCommentSubmitting = true;
        store.DeleteComment(postId, commentId, ok =>
        {
            deleteCommentSubmitting = false;
            if (ok)
            {
                deleteCommentPostId = null;
                deleteCommentId = null;
                deleteCommentStatus = string.Empty;
            }
            else
            {
                deleteCommentStatus = Loc.T(L.Chirper.DeleteCommentFailed);
            }
        });
    }

    private void DrawDeleteCommentTooltip(Vector2 iconCenter, float hitRadius, float scale)
    {
        var dl = ImGui.GetWindowDrawList();
        var tooltipText = Loc.T(L.Chirper.DeleteComment);
        var textSize = Typography.Measure(tooltipText, 0.78f, FontWeight.Medium);
        var padX = 9f * scale;
        var padY = 5f * scale;
        var bubbleSize = new Vector2(textSize.X + padX * 2f, textSize.Y + padY * 2f);
        var gap = 9f * scale;

        var windowMin = ImGui.GetWindowPos();
        var windowMax = windowMin + ImGui.GetWindowSize();
        var minX = Math.Clamp(iconCenter.X - bubbleSize.X * 0.5f, windowMin.X + 4f * scale, windowMax.X - bubbleSize.X - 4f * scale);
        var minY = iconCenter.Y - hitRadius - gap - bubbleSize.Y;
        if (minY < windowMin.Y + 4f * scale)
        {
            minY = iconCenter.Y + hitRadius + gap;
        }

        var min = new Vector2(minX, minY);
        var max = min + bubbleSize;
        var bubble = Palette.WithAlpha(Palette.Mix(theme.AppBackground, theme.TextStrong, 0.9f), 0.97f);
        Squircle.Fill(dl, min, max, bubbleSize.Y * 0.5f, ImGui.GetColorU32(bubble));
        Typography.Draw(dl, new Vector2(min.X + padX, min.Y + padY), tooltipText, theme.AppBackground, 0.78f, FontWeight.Medium);
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
            }
            else
            {
                deleteStatus = Loc.T(L.Chirper.DeleteFailed);
            }
        });
    }

    private void DrawEditProfile(Rect area)
    {
        var me = store.Me ?? (store.ProfileUser is { IsMe: true } self ? self : null);
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Chirper.EditProfile), back);

        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);

        if (me is null)
        {
            store.EnsureMe();
            Typography.DrawCentered(body.Center, Loc.T(L.Common.Loading), ChirperUi.MutedInk);
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
            editStatus = Loc.T(L.Chirper.HandleTaken);
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
        if (DrawHeaderAction(area, editBusy ? Loc.T(L.Chirper.Saving) : Loc.T(L.Chirper.Save), canSave))
        {
            SaveProfile();
        }

        using (AppSurface.Begin(body))
        {
            var avatarRadius = 34f * scale;
            var avatarOrigin = ImGui.GetCursorScreenPos();
            var avatarCenter = new Vector2(avatarOrigin.X + ImGui.GetContentRegionAvail().X * 0.5f, avatarOrigin.Y + avatarRadius);
            DrawAvatar(ImGui.GetWindowDrawList(), avatarCenter, avatarRadius, me.Name, me.World, me.AvatarUrl, 1.3f, 48);

            ImGui.SetCursorScreenPos(new Vector2(avatarOrigin.X, avatarCenter.Y + avatarRadius + 8f * scale));
            var changeWidth = 150f * scale;
            var changeTop = ImGui.GetCursorScreenPos().Y;
            var changeRect = new Rect(new Vector2(avatarCenter.X - changeWidth * 0.5f, changeTop), new Vector2(avatarCenter.X + changeWidth * 0.5f, changeTop + 30f * scale));
            if (DrawPillButton(changeRect, Loc.T(L.Chirper.ChangePhoto), false))
            {
                OpenAvatarComposer();
            }

            ImGui.SetCursorScreenPos(new Vector2(avatarOrigin.X, changeRect.Max.Y + 16f * scale));

            DrawField(Loc.T(L.Chirper.DisplayNameLabel), "##editDisplay", ref editDisplay, DisplayNameMax, false);

            ImGui.Dummy(new Vector2(0f, 10f * scale));
            DrawHandleField();

            ImGui.Dummy(new Vector2(0f, 10f * scale));
            DrawField(Loc.T(L.Chirper.BioLabel), "##editBio", ref editBio, BioMax, true);

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

    private void OpenAvatarComposer()
    {
        avatar.Open();
        router.Push(ChirperRoute.Avatar);
    }

    private void DrawAvatarCompose(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        if (avatar.Draw(area, context, Accent))
        {
            store.ReloadProfile();
            router.Pop();
        }
    }

    private void DrawHandleField()
    {
        var scale = ImGuiHelpers.GlobalScale;
        using (ImRaii.PushColor(ImGuiCol.Text, ChirperUi.MutedInk))
        {
            ImGui.TextUnformatted(Loc.T(L.Chirper.HandleLabel));
        }

        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = 34f * scale;
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, origin, new Vector2(origin.X + width, origin.Y + height), 9f * scale, ImGui.GetColorU32(ChirperUi.FieldSurface));

        Typography.Draw(new Vector2(origin.X + 12f * scale, origin.Y + height * 0.5f - 8f * scale), "@", ChirperUi.MutedInk, 1f);

        ImGui.SetCursorScreenPos(new Vector2(origin.X + 26f * scale, origin.Y + height * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(width - 38f * scale);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, IsHandleValid(editHandle) ? ChirperUi.TitleInk : theme.Danger))
        {
            if (ImGui.InputText("##editHandle", ref editHandle, HandleMax, ImGuiInputTextFlags.CharsNoBlank))
            {
                editHandle = editHandle.ToLowerInvariant();
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
        Typography.Draw(new Vector2(origin.X + 2f * scale, origin.Y + height + 3f * scale), Loc.T(L.Chirper.HandleRules), ChirperUi.MutedInk, 0.78f);
        ImGui.Dummy(new Vector2(width, 16f * scale));
    }

    private void DrawField(string label, string id, ref string value, int maxLength, bool multiline)
    {
        ui.Field(label, id, ref value, maxLength, multiline);
    }

    private void SaveProfile()
    {
        if (!store.IsSignedIn || editBusy)
        {
            return;
        }

        if (!IsHandleValid(editHandle) || editDisplay.Trim().Length == 0)
        {
            editStatus = Loc.T(L.Chirper.HandleRules);
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
        AppHeader.Draw(context, Loc.T(L.Chirper.FindPeople), back);

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
                Typography.DrawCentered(new Vector2(listRect.Center.X, listRect.Min.Y + 60f * scale), store.Searching ? Loc.T(L.Common.Searching) : Loc.T(L.Chirper.SearchByName), ChirperUi.MutedInk);
            }
            else
            {
                ImGui.Dummy(new Vector2(0f, 4f * scale));
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
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, origin, new Vector2(origin.X + width, origin.Y + rowHeight), 16f * scale);

        var pad = 12f * scale;
        var radius = 20f * scale;
        var avatarCenter = new Vector2(origin.X + pad + radius, origin.Y + rowHeight * 0.5f);
        DrawAvatar(drawList, avatarCenter, radius, user.Name, user.World, user.AvatarUrl, 0.95f, 32);

        var textLeft = avatarCenter.X + radius + 12f * scale;
        var displayName = string.IsNullOrEmpty(user.DisplayName) ? user.Name : user.DisplayName;
        Typography.Draw(new Vector2(textLeft, origin.Y + 12f * scale), displayName, theme.TextStrong, 1f, FontWeight.SemiBold);
        var sub = user.Handle.Length > 0 ? $"@{user.Handle} · {user.World}" : $"{user.Name} · {user.World}";
        Typography.Draw(new Vector2(textLeft, origin.Y + 33f * scale), sub, ChirperUi.MutedInk, 0.85f);

        var buttonWidth = 96f * scale;
        var buttonHeight = 30f * scale;
        var buttonRect = new Rect(new Vector2(origin.X + width - pad - buttonWidth, origin.Y + rowHeight * 0.5f - buttonHeight * 0.5f), new Vector2(origin.X + width - pad, origin.Y + rowHeight * 0.5f + buttonHeight * 0.5f));
        if (DrawPillButton(buttonRect, user.IsFollowing ? Loc.T(L.Chirper.Following) : Loc.T(L.Chirper.Follow), !user.IsFollowing))
        {
            store.SetFollow(user.Id, !user.IsFollowing);
        }

        var rowMin = origin;
        var rowMax = new Vector2(origin.X + width - buttonWidth - pad - 6f * scale, origin.Y + rowHeight);
        if (HoverClick(rowMin, rowMax))
        {
            OpenProfile(user.Id);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight + 8f * scale));
    }

    private void DrawSearchBar(Rect bar)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var pillMin = new Vector2(bar.Min.X + 12f * scale, bar.Min.Y + 9f * scale);
        var pillMax = new Vector2(bar.Max.X - 12f * scale, bar.Max.Y - 9f * scale);
        Squircle.Fill(drawList, pillMin, pillMax, (pillMax.Y - pillMin.Y) * 0.5f, ImGui.GetColorU32(ChirperUi.FieldSurface));

        DrawIcon(new Vector2(pillMin.X + 16f * scale, (pillMin.Y + pillMax.Y) * 0.5f), FontAwesomeIcon.Search.ToIconString(), ChirperUi.MutedInk, 0.85f);

        ImGui.SetCursorScreenPos(new Vector2(pillMin.X + 32f * scale, (pillMin.Y + pillMax.Y) * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(pillMax.X - pillMin.X - 44f * scale);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, ChirperUi.TitleInk))
        {
            if (ImGui.InputTextWithHint("##chirperSearch", Loc.T(L.Chirper.NameOrWorld), ref searchDraft, 64, ImGuiInputTextFlags.EnterReturnsTrue))
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
        Typography.Draw(new Vector2(area.Min.X + 16f * scale, rowCenterY - logoSize.Y * 0.5f), DisplayName, ChirperUi.TitleInk, 1.3f, FontWeight.Bold);

        var me = store.Me;
        var searchCenter = new Vector2(area.Max.X - 22f * scale, rowCenterY);
        if (me is not null)
        {
            var radius = 14f * scale;
            var center = new Vector2(area.Max.X - 52f * scale, rowCenterY);
            DrawAvatar(ImGui.GetWindowDrawList(), center, radius, me.Name, me.World, me.AvatarUrl, 0.85f, 24);
            if (HoverClick(center - new Vector2(radius, radius), center + new Vector2(radius, radius)))
            {
                OpenProfile(me.Id);
            }
        }

        if (DrawIconButton(searchCenter, 14f * scale, FontAwesomeIcon.Search.ToIconString(), ChirperUi.BodyInk, new Vector4(0f, 0f, 0f, 0f), 0.95f) && store.IsSignedIn)
        {
            store.ClearDiscover();
            searchDraft = string.Empty;
            router.Push(ChirperRoute.Discover);
        }
    }

    private bool DrawHeaderAction(Rect area, string label, bool enabled)
    {
        return ui.HeaderAction(area, label, enabled);
    }

    private bool DrawPillButton(Rect rect, string label, bool filled)
    {
        return ui.PillButton(rect, label, filled);
    }

    private bool DrawIconButton(Vector2 center, float hitRadius, string glyph, Vector4 color, Vector4 background, float glyphScale, string tooltip = "")
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
            if (tooltip.Length > 0)
            {
                DrawActionTooltip(center, hitRadius, tooltip);
            }
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private bool DrawRevealIcon(Vector2 center, float hitRadius, string glyph, Vector4 color, Vector4 background, float glyphScale, float reveal, string tooltip, bool interactive)
    {
        var drawList = ImGui.GetWindowDrawList();
        var eased = Easing.EaseOutBack(Math.Clamp(reveal, 0f, 1f));
        var alpha = Easing.SmoothStep(Math.Clamp(reveal / 0.6f, 0f, 1f));
        var hovered = interactive && ImGui.IsMouseHoveringRect(center - new Vector2(hitRadius, hitRadius), center + new Vector2(hitRadius, hitRadius));

        if (background.W > 0f)
        {
            var fill = hovered ? Palette.Mix(background, theme.TextStrong, 0.08f) : background;
            drawList.AddCircleFilled(center, hitRadius * eased, ImGui.GetColorU32(Palette.WithAlpha(fill, fill.W * alpha)), 24);
        }

        var ink = hovered ? Palette.Mix(color, theme.TextStrong, 0.2f) : color;
        DrawIcon(center, glyph, Palette.WithAlpha(ink, ink.W * alpha), glyphScale * eased);

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (tooltip.Length > 0 && reveal > 0.6f)
            {
                DrawActionTooltip(center, hitRadius, tooltip);
            }
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private void DrawActionTooltip(Vector2 iconCenter, float hitRadius, string text)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var textSize = Typography.Measure(text, 0.78f, FontWeight.Medium);
        var padX = 9f * scale;
        var padY = 5f * scale;
        var bubbleSize = new Vector2(textSize.X + padX * 2f, textSize.Y + padY * 2f);
        var gap = 9f * scale;

        var windowMin = ImGui.GetWindowPos();
        var windowMax = windowMin + ImGui.GetWindowSize();
        var minX = Math.Clamp(iconCenter.X - bubbleSize.X * 0.5f, windowMin.X + 4f * scale, windowMax.X - bubbleSize.X - 4f * scale);
        var minY = iconCenter.Y - hitRadius - gap - bubbleSize.Y;
        if (minY < windowMin.Y + 4f * scale)
        {
            minY = iconCenter.Y + hitRadius + gap;
        }

        var min = new Vector2(minX, minY);
        var max = min + bubbleSize;
        var bubble = Palette.WithAlpha(Palette.Mix(theme.AppBackground, theme.TextStrong, 0.9f), 0.97f);
        Squircle.Fill(drawList, min, max, bubbleSize.Y * 0.5f, ImGui.GetColorU32(bubble));
        Typography.Draw(drawList, new Vector2(min.X + padX, min.Y + padY), text, theme.AppBackground, 0.78f, FontWeight.Medium);
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

    private void DrawAvatar(ImDrawListPtr drawList, Vector2 center, float radius, string name, string world, string? avatarUrl, float monogramScale, int segments)
    {
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
        actions.Reset();
        store.OpenProfile(userId);
        router.Push(ChirperRoute.Profile(userId));
    }

    private void OpenThread(PostDto post)
    {
        actions.Reset();
        commentDraft = string.Empty;
        store.OpenDetail(post);
        router.Push(ChirperRoute.Thread(post.Id));
    }

    private void EnsureLoaded(ChirperFeedScope scope)
    {
        if (store.Feed(scope).Length == 0 && !store.IsLoading(scope))
        {
            store.RefreshFeed(scope);
        }
    }

    private void TickRefresh(ChirperFeedScope scope)
    {
        if (store.IsLoading(scope))
        {
            return;
        }

        if (scope == ChirperFeedScope.ForYou && sinceForYou >= FeedRefreshSeconds)
        {
            sinceForYou = 0f;
            store.RefreshFeed(scope);
        }
        else if (scope == ChirperFeedScope.Following && sinceFollowing >= FeedRefreshSeconds)
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
        var plural = Loc.Plural(L.Chirper.Posts, count);
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
