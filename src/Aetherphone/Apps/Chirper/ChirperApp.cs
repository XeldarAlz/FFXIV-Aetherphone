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
using Aetherphone.Core.Net;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Chirper;

internal sealed partial class ChirperApp : IPhoneApp
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
    private readonly ChirperAvatarComposer avatar;
    private readonly AppSkin ui = new(AppPalettes.Chirper);
    private readonly ViewRouter<ChirperRoute> router;
    private readonly RouterDraw<ChirperRoute> drawView;
    private readonly Action back;
    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private SocialFeedScope activeScope = SocialFeedScope.ForYou;
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

    public ChirperApp(AethernetSession session, AethernetClient client, LodestoneService lodestone, HttpService http,
        PhotoLibrary library, SocialLauncher launcher, GameData gameData, Configuration configuration)
    {
        store = new ChirperStore(session, client);
        this.launcher = launcher;
        this.gameData = gameData;
        this.configuration = configuration;
        this.lodestone = lodestone;
        images = new RemoteImageCache(http);
        avatar = new ChirperAvatarComposer(store, library);
        router = new ViewRouter<ChirperRoute>(ChirperRoute.Home, Id);
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
        draft = string.Empty;
        searchDraft = string.Empty;
        actions.Reset();
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
        actions.Tick(MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds));
        var screen = SceneChrome.ScreenFrom(context.Content, theme, ImGuiHelpers.GlobalScale);
        ui.Backdrop(screen);
        router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
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
            case ChirperScreen.UserList:
                DrawUserList(area, route.UserId!, route.Kind);
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
            Typography.DrawCentered(body.Center, Loc.T(L.Chirper.SetUpAccount), AppPalettes.Chirper.MutedInk);
            return;
        }

        var segmentHeight = 38f * scale;
        var tabsRect = new Rect(new Vector2(area.Min.X + 16f * scale, top + 2f * scale),
            new Vector2(area.Max.X - 16f * scale, top + 2f * scale + segmentHeight));
        var selected = DrawScopeSegments(tabsRect);
        if (selected != (int)activeScope)
        {
            activeScope = (SocialFeedScope)selected;
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
        DrawSegmentLabel(forYouRect, Loc.T(L.Chirper.ForYou), activeScope == SocialFeedScope.ForYou);
        DrawSegmentLabel(followingRect, Loc.T(L.Chirper.Following), activeScope == SocialFeedScope.Following);
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
        var ink = active ? new Vector4(1f, 1f, 1f, 1f) : AppPalettes.Chirper.MutedInk;
        Typography.DrawCentered(rect.Center, label, ink, 0.9f, active ? FontWeight.SemiBold : FontWeight.Medium);
    }

    private void DrawFeedList(Rect listRect, SocialFeedScope scope)
    {
        var snapshot = store.Feed(scope);
        using (AppSurface.Begin(listRect))
        {
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
        var displayName = SocialIdentity.Name(post.AuthorDisplayName, post.AuthorHandle);
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

        Typography.Draw(new Vector2(contentLeft, origin.Y + pad), displayName, theme.TextStrong, 1.05f,
            FontWeight.SemiBold);
        var meta = SocialIdentity.FeedMeta(post.AuthorHandle, RelativeTime(post.CreatedAtUnix));
        var metaSize = Typography.Measure(meta, 0.9f);
        Typography.Draw(
            new Vector2(contentLeft + nameSize.X + 7f * scale, origin.Y + pad + (nameSize.Y - metaSize.Y) * 0.5f), meta,
            AppPalettes.Chirper.MutedInk, 0.9f);
        if (HoverClick(new Vector2(contentLeft, origin.Y + pad),
                new Vector2(contentRight - 24f * scale, origin.Y + pad + nameSize.Y)))
        {
            OpenProfile(post.AuthorId);
        }

        if (post.Text.Length > 0)
        {
            ImGui.SetCursorScreenPos(new Vector2(contentLeft, textTop));
            var wrapPos = contentRight - ImGui.GetWindowPos().X;
            ImGui.PushTextWrapPos(wrapPos);
            using (Plugin.Fonts.Push(1.05f))
            using (ImRaii.PushColor(ImGuiCol.Text, AppPalettes.Chirper.BodyInk))
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
        if (DrawIconButton(commentCenter, 14f * scale, FontAwesomeIcon.Comment.ToIconString(), AppPalettes.Chirper.MutedInk,
                new Vector4(0f, 0f, 0f, 0f), 1f, Loc.T(L.Chirper.Reply)) && !isThreadHead)
        {
            OpenThread(post);
        }

        var cursorX = commentCenter.X + 20f * scale;
        if (post.CommentCount > 0)
        {
            var countText = post.CommentCount.ToString(Loc.Culture);
            var countSize = Typography.Measure(countText, 0.9f, FontWeight.Medium);
            Typography.Draw(new Vector2(cursorX, centerY - countSize.Y * 0.5f), countText, AppPalettes.Chirper.MutedInk, 0.9f,
                FontWeight.Medium);
            cursorX += countSize.X + 6f * scale;
        }

        cursorX += 12f * scale;
        var triggerCenter = new Vector2(cursorX + 11f * scale, centerY);
        if (DrawIconButton(triggerCenter, 14f * scale, FontAwesomeIcon.GrinBeam.ToIconString(), AppPalettes.Chirper.MutedInk,
                new Vector4(0f, 0f, 0f, 0f), 1f, Loc.T(L.Chirper.React)))
        {
            actions.Open(post.Id, ChirperActionReveal.Panel.Picker);
        }

        var chipLimit = left + width - 34f * scale;
        DrawReactionSummary(post, triggerCenter.X + 20f * scale, centerY, chipLimit);

        var ellipsisCenter = new Vector2(left + width - 12f * scale, centerY);
        if (DrawIconButton(ellipsisCenter, 13f * scale, FontAwesomeIcon.EllipsisH.ToIconString(), AppPalettes.Chirper.BodyInk,
                new Vector4(0f, 0f, 0f, 0f), 0.9f, Loc.T(L.Chirper.More)))
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
        var countSize = Typography.Measure(countText, 0.82f, FontWeight.Medium);
        var glyphWidth = 11f * scale;
        var padX = 7f * scale;
        var gap = 4f * scale;
        return padX + glyphWidth + gap + countSize.X + padX;
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
        var chipWidth = ReactionChipWidth(post, kind);
        var chipHeight = 22f * scale;
        var min = new Vector2(x, centerY - chipHeight * 0.5f);
        var max = new Vector2(x + chipWidth, centerY + chipHeight * 0.5f);
        var drawList = ImGui.GetWindowDrawList();
        var hovered = ImGui.IsMouseHoveringRect(min, max);
        var background = active
            ? Palette.WithAlpha(color, 0.24f)
            : (hovered ? new Vector4(1f, 1f, 1f, 0.14f) : AppPalettes.Chirper.FieldSurface);
        Squircle.Fill(drawList, min, max, chipHeight * 0.5f, ImGui.GetColorU32(background));
        AppSkin.Icon(new Vector2(min.X + padX + glyphWidth * 0.5f, centerY), ChirperReactions.Glyph(kind), color, 0.82f);
        Typography.Draw(new Vector2(min.X + padX + glyphWidth + gap, centerY - countSize.Y * 0.5f), countText,
            active ? color : AppPalettes.Chirper.MutedInk, 0.82f, FontWeight.Medium);
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
            if (DrawRevealIcon(center, iconRadius, ChirperReactions.Glyph(kind), color, background, 1f, reveal,
                    ChirperReactions.Label(kind), interactive))
            {
                store.ToggleReaction(post, kind);
                actions.Dismiss();
            }
        }

        var closeCenter = new Vector2(left + iconRadius + ChirperReactions.Count * step, centerY);
        var closeReveal = ChirperActionReveal.Stagger(actions.Progress, ChirperReactions.Count, count);
        if (DrawRevealIcon(closeCenter, iconRadius, FontAwesomeIcon.Times.ToIconString(), AppPalettes.Chirper.MutedInk,
                AppPalettes.Chirper.FieldSurface, 0.9f, closeReveal, Loc.T(L.Common.Close), interactive))
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
        if (DrawRevealIcon(closeCenter, iconRadius, FontAwesomeIcon.Times.ToIconString(), AppPalettes.Chirper.MutedInk,
                AppPalettes.Chirper.FieldSurface, 0.9f, ChirperActionReveal.Stagger(actions.Progress, slot, count),
                Loc.T(L.Common.Close), interactive))
        {
            actions.Dismiss();
        }

        slot++;
        if (mine)
        {
            var trashCenter = new Vector2(anchorX - slot * step, centerY);
            if (DrawRevealIcon(trashCenter, iconRadius, FontAwesomeIcon.Trash.ToIconString(), theme.Danger,
                    Palette.WithAlpha(theme.Danger, 0.16f), 0.85f,
                    ChirperActionReveal.Stagger(actions.Progress, slot, count), Loc.T(L.Chirper.DeleteConfirm),
                    interactive))
            {
                reportTargetType = null;
                reportTargetId = null;
                AskDeletePost(post.Id);
                actions.Dismiss();
            }

            return;
        }

        var reportCenter = new Vector2(anchorX - slot * step, centerY);
        if (DrawRevealIcon(reportCenter, iconRadius, FontAwesomeIcon.Flag.ToIconString(), theme.Danger,
                Palette.WithAlpha(theme.Danger, 0.16f), 0.85f,
                ChirperActionReveal.Stagger(actions.Progress, slot, count), Loc.T(L.Chirper.ReportSubmit), interactive))
        {
            reportTargetType = "post";
            reportTargetId = post.Id;
            reportReasonDraft = string.Empty;
            reportStatus = string.Empty;
            actions.Dismiss();
        }

        slot++;
        var followGlyph = post.IsFollowing
            ? FontAwesomeIcon.UserCheck.ToIconString()
            : FontAwesomeIcon.UserPlus.ToIconString();
        var followColor = post.IsFollowing ? theme.Accent : theme.TextStrong;
        var followTip = Loc.T(post.IsFollowing ? L.Chirper.Unfollow : L.Chirper.Follow);
        var followCenter = new Vector2(anchorX - slot * step, centerY);
        if (DrawRevealIcon(followCenter, iconRadius, followGlyph, followColor, AppPalettes.Chirper.FieldSurface, 0.9f,
                ChirperActionReveal.Stagger(actions.Progress, slot, count), followTip, interactive))
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
        DrawAvatar(drawList, avatarCenter, radius, comment.AuthorName, string.Empty, comment.AuthorAvatarUrl, 0.85f,
            32);
        if (HoverClick(avatarCenter - new Vector2(radius, radius), avatarCenter + new Vector2(radius, radius)))
        {
            OpenProfile(comment.AuthorId);
        }

        var textLeft = origin.X + radius * 2f + 10f * scale;
        var displayName = SocialIdentity.Name(comment.AuthorDisplayName, comment.AuthorHandle);
        var nameSize = Typography.Measure(displayName, 0.95f, FontWeight.SemiBold);
        Typography.Draw(new Vector2(textLeft, origin.Y), displayName, theme.TextStrong, 0.95f, FontWeight.SemiBold);
        var meta = comment.AuthorHandle.Length > 0
            ? $"@{comment.AuthorHandle} · {RelativeTime(comment.CreatedAtUnix)}"
            : RelativeTime(comment.CreatedAtUnix);
        var metaSize = Typography.Measure(meta, 0.82f);
        Typography.Draw(new Vector2(textLeft + nameSize.X + 7f * scale, origin.Y + (nameSize.Y - metaSize.Y) * 0.5f),
            meta, AppPalettes.Chirper.MutedInk, 0.82f);
        ImGui.SetCursorScreenPos(new Vector2(textLeft, origin.Y + nameSize.Y + 6f * scale));
        var commentWrapPos = (origin.X + width - 4f * scale) - ImGui.GetWindowPos().X;
        ImGui.PushTextWrapPos(commentWrapPos);
        using (ImRaii.PushColor(ImGuiCol.Text, AppPalettes.Chirper.BodyInk))
        {
            ImGui.TextWrapped(comment.Text);
        }

        ImGui.PopTextWrapPos();
        if (store.Me is { } me && me.Id == comment.AuthorId && store.DetailPost is { } post)
        {
            var trashCenter = new Vector2(origin.X + width - 10f * scale, origin.Y + 9f * scale);
            var trashHitRadius = 11f * scale;
            if (DrawIconButton(trashCenter, trashHitRadius, FontAwesomeIcon.Times.ToIconString(), AppPalettes.Chirper.MutedInk,
                    new Vector4(0f, 0f, 0f, 0f), 0.75f))
            {
                AskDeleteComment(post.Id, comment.Id);
            }

            var trashHovered = ImGui.IsMouseHoveringRect(trashCenter - new Vector2(trashHitRadius, trashHitRadius),
                trashCenter + new Vector2(trashHitRadius, trashHitRadius));
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
        drawList.AddLine(bar.Min, new Vector2(bar.Max.X, bar.Min.Y), ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)),
            1f);
        var pillMin = new Vector2(bar.Min.X + 12f * scale, bar.Min.Y + 8f * scale);
        var pillMax = new Vector2(bar.Max.X - 56f * scale, bar.Max.Y - 8f * scale);
        Squircle.Fill(drawList, pillMin, pillMax, (pillMax.Y - pillMin.Y) * 0.5f,
            ImGui.GetColorU32(AppPalettes.Chirper.FieldSurface));
        ImGui.SetCursorScreenPos(new Vector2(pillMin.X + 14f * scale,
            (pillMin.Y + pillMax.Y) * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(pillMax.X - pillMin.X - 24f * scale);
        var submitted = false;
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, AppPalettes.Chirper.TitleInk))
        {
            submitted = ImGui.InputTextWithHint("##chirperComment", Loc.T(L.Chirper.AddComment), ref commentDraft,
                MaxCommentLength, ImGuiInputTextFlags.EnterReturnsTrue);
        }

        var canSend = commentDraft.Trim().Length > 0 && !store.Commenting;
        var sendCenter = new Vector2(bar.Max.X - 28f * scale, bar.Center.Y);
        if ((DrawIconButton(sendCenter, 16f * scale, FontAwesomeIcon.PaperPlane.ToIconString(),
                canSend ? Accent : AppPalettes.Chirper.MutedInk, new Vector4(0f, 0f, 0f, 0f), 0.95f) || submitted) && canSend)
        {
            var text = commentDraft;
            commentDraft = string.Empty;
            store.AddComment(postId, text, _ => { });
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

    private bool DrawIconButton(Vector2 center, float hitRadius, string glyph, Vector4 color, Vector4 background,
        float glyphScale, string tooltip = "")
    {
        return ui.IconButton(center, hitRadius, glyph, color, background, glyphScale, tooltip);
    }

    private bool DrawRevealIcon(Vector2 center, float hitRadius, string glyph, Vector4 color, Vector4 background,
        float glyphScale, float reveal, string tooltip, bool interactive)
    {
        var drawList = ImGui.GetWindowDrawList();
        var eased = Easing.EaseOutBack(Math.Clamp(reveal, 0f, 1f));
        var alpha = Easing.SmoothStep(Math.Clamp(reveal / 0.6f, 0f, 1f));
        var hovered = interactive && ImGui.IsMouseHoveringRect(center - new Vector2(hitRadius, hitRadius),
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
            if (tooltip.Length > 0 && reveal > 0.6f)
            {
                ui.DrawActionTooltip(center, hitRadius, tooltip);
            }
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }


    private void DrawAvatar(ImDrawListPtr drawList, Vector2 center, float radius, string name, string world,
        string? avatarUrl, float monogramScale, int segments)
    {
        AvatarView.DrawRemote(drawList, center, radius, theme, name, world, avatarUrl, images, lodestone,
            monogramScale, segments);
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

    private void DrawLikersLink(PostDto post)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var label = Loc.Plural(L.Chirper.Likes, post.TotalReactions);
        var origin = ImGui.GetCursorScreenPos();
        var pad = 16f * scale;
        var pos = new Vector2(origin.X + pad, origin.Y);
        var size = Typography.Measure(label, 0.9f, FontWeight.Medium);
        var hovered = ImGui.IsMouseHoveringRect(pos, pos + size);
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
