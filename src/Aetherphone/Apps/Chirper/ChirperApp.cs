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
    private const float TabsHeight = 40f;
    private const float FeedTopPadding = 12f;
    private const int MaxReportReasonLength = 200;
    private const int MaxCommentLength = 500;

    public string Id => "chirper";

    public string DisplayName => Loc.T(L.Apps.Chirper);

    public string Glyph => "Ch";

    public Vector4 Accent => new(0.114f, 0.631f, 0.949f, 1f);

    public int BadgeCount => 0;

    private readonly ChirperStore store;
    private readonly LodestoneService lodestone;
    private readonly RemoteImageCache images;
    private readonly ChirperAvatarComposer avatar;

    private readonly ViewRouter<ChirperRoute> router;
    private readonly RouterDraw<ChirperRoute> drawView;
    private readonly Action back;

    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;

    private ChirperFeedScope activeScope = ChirperFeedScope.ForYou;
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
        store.ClearDiscover();
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        actions.Tick(MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds));
        router.Draw(context.Content, context.Theme.AppBackground, ImGui.GetIO().DeltaTime, drawView);
    }

    private void DrawView(ChirperRoute route, Rect area, int depth)
    {
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
    }

    private void DrawHome(Rect area)
    {
        DrawHomeTopBar(area);

        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;

        if (!store.IsSignedIn)
        {
            var body = new Rect(new Vector2(area.Min.X, top), area.Max);
            Typography.DrawCentered(body.Center, Loc.T(L.Chirper.SetUpAccount), theme.TextMuted);
            return;
        }

        var tabsRect = new Rect(new Vector2(area.Min.X, top), new Vector2(area.Max.X, top + TabsHeight * scale));
        var tabs = new[] { Loc.T(L.Chirper.ForYou), Loc.T(L.Chirper.Following) };
        var selected = ChirperTabs.Draw("chirper.tabs", tabsRect, tabs, (int)activeScope, theme);
        if (selected != (int)activeScope)
        {
            activeScope = (ChirperFeedScope)selected;
            actions.Reset();
            EnsureLoaded(activeScope);
        }

        sinceForYou += ImGui.GetIO().DeltaTime;
        sinceFollowing += ImGui.GetIO().DeltaTime;
        TickRefresh(activeScope);

        var listRect = new Rect(new Vector2(area.Min.X, tabsRect.Max.Y), area.Max);
        DrawFeedList(listRect, activeScope);
        DrawComposeFab(listRect);
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
                Typography.DrawCentered(new Vector2(listRect.Center.X, listRect.Min.Y + 90f * ImGuiHelpers.GlobalScale), message, theme.TextMuted);
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
        var radius = 21f * scale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var avatarCenter = new Vector2(origin.X + radius, origin.Y + radius);
        DrawAvatar(drawList, avatarCenter, radius, post.AuthorName, post.AuthorWorld, post.AuthorAvatarUrl, 0.95f, 48);

        if (HoverClick(new Vector2(avatarCenter.X - radius, avatarCenter.Y - radius), new Vector2(avatarCenter.X + radius, avatarCenter.Y + radius)))
        {
            OpenProfile(post.AuthorId);
        }

        var contentLeft = origin.X + radius * 2f + 12f * scale;
        var contentWidth = origin.X + ImGui.GetContentRegionAvail().X - contentLeft;

        var displayName = string.IsNullOrEmpty(post.AuthorDisplayName) ? post.AuthorName : post.AuthorDisplayName;
        var nameSize = Typography.Measure(displayName, 1.05f, FontWeight.SemiBold);
        Typography.Draw(new Vector2(contentLeft, origin.Y), displayName, theme.TextStrong, 1.05f, FontWeight.SemiBold);
        var meta = post.AuthorHandle.Length > 0
            ? $"@{post.AuthorHandle} · {RelativeTime(post.CreatedAtUnix)}"
            : $"{post.AuthorWorld} · {RelativeTime(post.CreatedAtUnix)}";
        var metaSize = Typography.Measure(meta, 0.9f);
        Typography.Draw(new Vector2(contentLeft + nameSize.X + 7f * scale, origin.Y + nameSize.Y - metaSize.Y - 1f * scale), meta, theme.TextMuted, 0.9f);

        ImGui.SetCursorScreenPos(new Vector2(contentLeft, origin.Y + nameSize.Y + 4f * scale));
        ImGui.PushTextWrapPos(0f);
        using (Plugin.Fonts.Push(1.05f))
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            ImGui.TextWrapped(post.Text);
        }

        ImGui.PopTextWrapPos();

        ImGui.Dummy(new Vector2(0f, 8f * scale));
        DrawActionRow(post, contentLeft, contentWidth, isThreadHead);

        var deleteActive = deleteTargetId == post.Id;
        var reportActive = reportTargetType == "post" && reportTargetId == post.Id;
        if (deleteActive || reportActive)
        {
            ImGui.Dummy(new Vector2(0f, 6f * scale));
            if (deleteActive)
            {
                DrawDeleteComposer(post.Id, contentLeft, contentWidth);
            }
            else
            {
                DrawReportComposer(contentLeft, contentWidth);
            }
        }

        ImGui.Dummy(new Vector2(0f, 10f * scale));
        var separatorY = ImGui.GetCursorScreenPos().Y;
        drawList.AddLine(new Vector2(origin.X, separatorY), new Vector2(origin.X + ImGui.GetContentRegionAvail().X, separatorY), ImGui.GetColorU32(theme.Separator), 1f);
        ImGui.Dummy(new Vector2(0f, 10f * scale));
    }

    private void DrawActionRow(PostDto post, float left, float width, bool isThreadHead)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var rowY = ImGui.GetCursorScreenPos().Y;
        var rowHeight = 30f * scale;
        var centerY = rowY + rowHeight * 0.5f;

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

        ImGui.SetCursorScreenPos(new Vector2(left, rowY));
        ImGui.Dummy(new Vector2(width, rowHeight));
    }

    private void DrawDefaultActions(PostDto post, float left, float width, float centerY, bool isThreadHead)
    {
        var scale = ImGuiHelpers.GlobalScale;

        var commentCenter = new Vector2(left + 11f * scale, centerY);
        if (DrawIconButton(commentCenter, 14f * scale, FontAwesomeIcon.Comment.ToIconString(), theme.TextMuted, new Vector4(0f, 0f, 0f, 0f), 1f) && !isThreadHead)
        {
            OpenThread(post);
        }

        var cursorX = commentCenter.X + 20f * scale;
        if (post.CommentCount > 0)
        {
            var countText = post.CommentCount.ToString(Loc.Culture);
            var countSize = Typography.Measure(countText, 0.9f, FontWeight.Medium);
            Typography.Draw(new Vector2(cursorX, centerY - countSize.Y * 0.5f), countText, theme.TextMuted, 0.9f, FontWeight.Medium);
            cursorX += countSize.X + 6f * scale;
        }

        cursorX += 12f * scale;
        var triggerCenter = new Vector2(cursorX + 11f * scale, centerY);
        if (DrawIconButton(triggerCenter, 14f * scale, FontAwesomeIcon.GrinBeam.ToIconString(), theme.TextMuted, new Vector4(0f, 0f, 0f, 0f), 1f))
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
        if (DrawIconButton(ellipsisCenter, 13f * scale, FontAwesomeIcon.EllipsisH.ToIconString(), theme.TextStrong, new Vector4(0f, 0f, 0f, 0f), 0.9f))
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
            : (hovered ? Palette.Mix(theme.SurfaceMuted, theme.TextStrong, 0.06f) : theme.SurfaceMuted);
        Squircle.Fill(drawList, min, max, chipHeight * 0.5f, ImGui.GetColorU32(background));
        DrawIcon(new Vector2(min.X + padX + glyphWidth * 0.5f, centerY), ChirperReactions.Glyph(kind), color, 0.82f);
        Typography.Draw(new Vector2(min.X + padX + glyphWidth + gap, centerY - countSize.Y * 0.5f), countText, active ? color : theme.TextMuted, 0.82f, FontWeight.Medium);

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
            var background = active ? Palette.WithAlpha(color, 0.22f) : theme.SurfaceMuted;
            var reveal = ChirperActionReveal.Stagger(actions.Progress, kind, count);
            if (DrawRevealIcon(center, iconRadius, ChirperReactions.Glyph(kind), color, background, 1f, reveal, ChirperReactions.Label(kind), interactive))
            {
                store.ToggleReaction(post, kind);
                actions.Dismiss();
            }
        }

        var closeCenter = new Vector2(left + iconRadius + ChirperReactions.Count * step, centerY);
        var closeReveal = ChirperActionReveal.Stagger(actions.Progress, ChirperReactions.Count, count);
        if (DrawRevealIcon(closeCenter, iconRadius, FontAwesomeIcon.Times.ToIconString(), theme.TextMuted, theme.SurfaceMuted, 0.9f, closeReveal, Loc.T(L.Common.Close), interactive))
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
        if (DrawRevealIcon(closeCenter, iconRadius, FontAwesomeIcon.Times.ToIconString(), theme.TextMuted, theme.SurfaceMuted, 0.9f, ChirperActionReveal.Stagger(actions.Progress, slot, count), Loc.T(L.Common.Close), interactive))
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
        if (DrawRevealIcon(followCenter, iconRadius, followGlyph, followColor, theme.SurfaceMuted, 0.9f, ChirperActionReveal.Stagger(actions.Progress, slot, count), followTip, interactive))
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

            Typography.DrawCentered(new Vector2(area.Center.X, top + 60f * scale), Loc.T(L.Common.Loading), theme.TextMuted);
            return;
        }

        var composerHeight = 50f * scale;
        var body = new Rect(new Vector2(area.Min.X, top), new Vector2(area.Max.X, area.Max.Y - composerHeight));

        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, FeedTopPadding * scale));
            DrawPost(post, true);

            var comments = store.DetailComments;
            if (comments.Length == 0)
            {
                if (!store.DetailLoading)
                {
                    Typography.Draw(new Vector2(ImGui.GetCursorScreenPos().X + 2f * scale, ImGui.GetCursorScreenPos().Y), Loc.T(L.Chirper.NoComments), theme.TextMuted, 0.85f);
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
        Typography.Draw(new Vector2(textLeft + nameSize.X + 7f * scale, origin.Y + nameSize.Y - metaSize.Y - 1f * scale), meta, theme.TextMuted, 0.82f);

        ImGui.SetCursorScreenPos(new Vector2(textLeft, origin.Y + nameSize.Y + 3f * scale));
        ImGui.PushTextWrapPos(origin.X + width - 4f * scale);
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            ImGui.TextWrapped(comment.Text);
        }

        ImGui.PopTextWrapPos();

        if (store.Me is { } me && me.Id == comment.AuthorId && store.DetailPost is { } post)
        {
            var trashCenter = new Vector2(origin.X + width - 10f * scale, origin.Y + 9f * scale);
            if (DrawIconButton(trashCenter, 11f * scale, FontAwesomeIcon.Times.ToIconString(), theme.TextMuted, new Vector4(0f, 0f, 0f, 0f), 0.75f))
            {
                store.DeleteComment(post.Id, comment.Id);
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
            submitted = ImGui.InputTextWithHint("##chirperComment", Loc.T(L.Chirper.AddComment), ref commentDraft, MaxCommentLength, ImGuiInputTextFlags.EnterReturnsTrue);
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
            Squircle.Fill(drawList, cardMin, cardMax, 18f * scale, ImGui.GetColorU32(theme.GroupedCard));

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
            using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
            using (Plugin.Fonts.Push(1.15f))
            {
                ImGui.InputTextMultiline("##chirpBody", ref draft, MaxPostLength, new Vector2(inputWidth, inputHeight), ImGuiInputTextFlags.None);
            }

            if (draft.Length == 0)
            {
                Typography.Draw(new Vector2(inputX + 4f * scale, inputTop + 2f * scale), Loc.T(L.Chirper.Compose), theme.TextMuted, 1.15f);
            }

            var footerY = area.Max.Y - footerHeight * 0.5f;
            if (composeStatus.Length > 0)
            {
                Typography.Draw(new Vector2(origin.X + 2f * scale, footerY - Typography.Measure(composeStatus, 0.85f).Y * 0.5f), composeStatus, theme.Danger, 0.85f);
            }

            var remaining = MaxPostLength - draft.Length;
            var counterColor = remaining < 40 ? (remaining < 0 ? theme.Danger : new Vector4(0.95f, 0.65f, 0.20f, 1f)) : theme.TextMuted;
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
            Typography.DrawCentered(body.Center, Loc.T(L.Chirper.ProfileError), theme.TextMuted);
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

            var posts = store.ProfilePosts;
            if (posts.Length == 0)
            {
                Typography.DrawCentered(new Vector2(body.Center.X, ImGui.GetCursorScreenPos().Y + 50f * scale), Loc.T(L.Chirper.Empty), theme.TextMuted);
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

        var bannerHeight = 78f * scale;
        var bannerMin = new Vector2(origin.X - 16f * scale, origin.Y - 8f * scale);
        var bannerMax = new Vector2(bannerMin.X + width + 32f * scale, bannerMin.Y + bannerHeight);
        Squircle.FillVerticalGradient(drawList, bannerMin, bannerMax, 0f,
            ImGui.GetColorU32(Palette.Mix(theme.Accent, theme.TextStrong, 0.12f)),
            ImGui.GetColorU32(Palette.Mix(theme.Accent, new Vector4(0f, 0f, 0f, 1f), 0.30f)));

        var avatarRadius = 32f * scale;
        var avatarCenter = new Vector2(origin.X + avatarRadius, bannerMax.Y);
        drawList.AddCircleFilled(avatarCenter, avatarRadius + 3f * scale, ImGui.GetColorU32(theme.AppBackground), 40);
        DrawAvatar(drawList, avatarCenter, avatarRadius, user.Name, user.World, user.AvatarUrl, 1.2f, 48);

        var buttonWidth = 110f * scale;
        var buttonHeight = 32f * scale;
        var buttonMin = new Vector2(origin.X + width - buttonWidth, bannerMax.Y + 8f * scale);
        var buttonRect = new Rect(buttonMin, new Vector2(buttonMin.X + buttonWidth, buttonMin.Y + buttonHeight));
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
            var reportCenter = new Vector2(buttonMin.X - buttonHeight * 0.5f - 8f * scale, buttonMin.Y + buttonHeight * 0.5f);
            reportShown = DrawReportToggle(reportCenter, buttonHeight * 0.5f, "user", user.Id);

            if (DrawPillButton(buttonRect, user.IsFollowing ? Loc.T(L.Chirper.Following) : Loc.T(L.Chirper.Follow), !user.IsFollowing))
            {
                store.SetFollow(user.Id, !user.IsFollowing);
            }
        }

        ImGui.SetCursorScreenPos(new Vector2(origin.X, avatarCenter.Y + avatarRadius + 8f * scale));
        if (reportShown)
        {
            DrawReportComposer(origin.X, width);
            ImGui.Dummy(new Vector2(0f, 8f * scale));
        }

        var displayName = string.IsNullOrEmpty(user.DisplayName) ? user.Name : user.DisplayName;
        using (Plugin.Fonts.Push(1.35f, FontWeight.SemiBold))
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

        ImGui.Dummy(new Vector2(0f, 6f * scale));
        var statsOrigin = ImGui.GetCursorScreenPos();
        var cursorX = statsOrigin.X;
        DrawStat(ref cursorX, statsOrigin.Y, user.Following.ToString(Loc.Culture), Loc.T(L.Chirper.Following));
        DrawStat(ref cursorX, statsOrigin.Y, user.Followers.ToString(Loc.Culture), Loc.Plural(L.Account.Followers, user.Followers).Split(' ', 2)[^1]);
        DrawStat(ref cursorX, statsOrigin.Y, user.Posts.ToString(Loc.Culture), PostsLabel(user.Posts));
        ImGui.Dummy(new Vector2(0f, 24f * scale));

        var separatorY = ImGui.GetCursorScreenPos().Y;
        drawList.AddLine(new Vector2(origin.X, separatorY), new Vector2(origin.X + width, separatorY), ImGui.GetColorU32(theme.Separator), 1f);
        ImGui.Dummy(new Vector2(0f, 8f * scale));
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
            reportStatus = Loc.T(ok ? L.Chirper.ReportSent : L.Chirper.ReportFailed);
            if (ok)
            {
                reportTargetType = null;
                reportTargetId = null;
            }
        });
    }

    private void DrawDeleteComposer(string postId, float left, float width)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();

        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            ImGui.SetCursorScreenPos(new Vector2(left, origin.Y));
            ImGui.PushTextWrapPos(left + width);
            ImGui.TextWrapped(Loc.T(L.Chirper.DeleteConfirmMessage));
            ImGui.PopTextWrapPos();
        }

        var rowY = ImGui.GetCursorScreenPos().Y + 6f * scale;
        var buttonWidth = 84f * scale;
        var buttonHeight = 28f * scale;

        var cancelRect = new Rect(new Vector2(left + width - buttonWidth * 2f - 8f * scale, rowY), new Vector2(left + width - buttonWidth - 8f * scale, rowY + buttonHeight));
        if (DrawPillButton(cancelRect, Loc.T(L.Chirper.DeleteCancel), false) && !deleteSubmitting)
        {
            deleteTargetId = null;
        }

        var deleteRect = new Rect(new Vector2(left + width - buttonWidth, rowY), new Vector2(left + width, rowY + buttonHeight));
        var canSubmit = !deleteSubmitting;
        if (DrawDangerPillButton(deleteRect, deleteSubmitting ? Loc.T(L.Chirper.Saving) : Loc.T(L.Chirper.DeleteConfirm)) && canSubmit)
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
            }
            else
            {
                deleteStatus = Loc.T(L.Chirper.DeleteFailed);
            }
        });
    }

    private void DrawStat(ref float cursorX, float y, string value, string label)
    {
        var scale = ImGuiHelpers.GlobalScale;
        Typography.Draw(new Vector2(cursorX, y), value, theme.TextStrong, 0.92f, FontWeight.SemiBold);
        var valueWidth = Typography.Measure(value, 0.92f, FontWeight.SemiBold).X;
        Typography.Draw(new Vector2(cursorX + valueWidth + 4f * scale, y + 1f * scale), label, theme.TextMuted, 0.85f);
        cursorX += valueWidth + 4f * scale + Typography.Measure(label, 0.85f).X + 16f * scale;
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
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
        {
            ImGui.TextUnformatted(Loc.T(L.Chirper.HandleLabel));
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
        Typography.Draw(new Vector2(origin.X + 2f * scale, origin.Y + height + 3f * scale), Loc.T(L.Chirper.HandleRules), theme.TextMuted, 0.78f);
        ImGui.Dummy(new Vector2(width, 16f * scale));
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
                Typography.DrawCentered(new Vector2(listRect.Center.X, listRect.Min.Y + 60f * scale), store.Searching ? Loc.T(L.Common.Searching) : Loc.T(L.Chirper.SearchByName), theme.TextMuted);
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
        var drawList = ImGui.GetWindowDrawList();
        var radius = 20f * scale;
        var avatarCenter = new Vector2(origin.X + radius, origin.Y + rowHeight * 0.5f);
        DrawAvatar(drawList, avatarCenter, radius, user.Name, user.World, user.AvatarUrl, 0.95f, 32);

        var textLeft = origin.X + radius * 2f + 12f * scale;
        var displayName = string.IsNullOrEmpty(user.DisplayName) ? user.Name : user.DisplayName;
        Typography.Draw(new Vector2(textLeft, origin.Y + 9f * scale), displayName, theme.TextStrong, 1f, FontWeight.SemiBold);
        var sub = user.Handle.Length > 0 ? $"@{user.Handle} · {user.World}" : $"{user.Name} · {user.World}";
        Typography.Draw(new Vector2(textLeft, origin.Y + 31f * scale), sub, theme.TextMuted, 0.85f);

        var buttonWidth = 96f * scale;
        var buttonHeight = 30f * scale;
        var buttonRect = new Rect(new Vector2(origin.X + width - buttonWidth, origin.Y + rowHeight * 0.5f - buttonHeight * 0.5f), new Vector2(origin.X + width, origin.Y + rowHeight * 0.5f + buttonHeight * 0.5f));
        if (DrawPillButton(buttonRect, user.IsFollowing ? Loc.T(L.Chirper.Following) : Loc.T(L.Chirper.Follow), !user.IsFollowing))
        {
            store.SetFollow(user.Id, !user.IsFollowing);
        }

        var rowMin = origin;
        var rowMax = new Vector2(origin.X + width - buttonWidth - 6f * scale, origin.Y + rowHeight);
        if (HoverClick(rowMin, rowMax))
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
        Typography.DrawCentered(new Vector2(area.Center.X, rowCenterY), DisplayName, theme.TextStrong, 1.15f, FontWeight.SemiBold);

        var me = store.Me;
        if (me is not null)
        {
            var radius = 14f * scale;
            var center = new Vector2(area.Min.X + 22f * scale, rowCenterY);
            DrawAvatar(ImGui.GetWindowDrawList(), center, radius, me.Name, me.World, me.AvatarUrl, 0.85f, 24);
            if (HoverClick(center - new Vector2(radius, radius), center + new Vector2(radius, radius)))
            {
                OpenProfile(me.Id);
            }
        }

        var searchCenter = new Vector2(area.Max.X - 22f * scale, rowCenterY);
        if (DrawIconButton(searchCenter, 14f * scale, FontAwesomeIcon.Search.ToIconString(), theme.TextStrong, new Vector4(0f, 0f, 0f, 0f), 0.95f) && store.IsSignedIn)
        {
            store.ClearDiscover();
            searchDraft = string.Empty;
            router.Push(ChirperRoute.Discover);
        }
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
        var scale = ImGuiHelpers.GlobalScale;
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
