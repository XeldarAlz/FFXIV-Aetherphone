using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
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

    public string Id => "chirper";

    public string DisplayName => Loc.T(L.Apps.Chirper);

    public string Glyph => "Ch";

    public Vector4 Accent => new(0.114f, 0.631f, 0.949f, 1f);

    public int BadgeCount => 0;

    private readonly ChirperStore store;
    private readonly LodestoneService lodestone;

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
    private string? pickerPostId;

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

    public ChirperApp(AethernetSession session, AethernetClient client, LodestoneService lodestone)
    {
        store = new ChirperStore(session, client);
        this.lodestone = lodestone;

        router = new ViewRouter<ChirperRoute>(ChirperRoute.Home);
        drawView = DrawView;
        back = () => router.Pop();
    }

    public void OnOpened()
    {
        router.Reset();
        pickerPostId = null;
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
        pickerPostId = null;
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
            case ChirperScreen.Discover:
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
            Typography.DrawCentered(body.Center, Loc.T(L.Chirper.SetUpAccount), theme.TextMuted);
            return;
        }

        var tabsRect = new Rect(new Vector2(area.Min.X, top), new Vector2(area.Max.X, top + TabsHeight * scale));
        var tabs = new[] { Loc.T(L.Chirper.ForYou), Loc.T(L.Chirper.Following) };
        var selected = ChirperTabs.Draw("chirper.tabs", tabsRect, tabs, (int)activeScope, theme);
        if (selected != (int)activeScope)
        {
            activeScope = (ChirperFeedScope)selected;
            pickerPostId = null;
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

    private void DrawPost(PostDto post)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var radius = 19f * scale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var avatarCenter = new Vector2(origin.X + radius, origin.Y + radius);
        AvatarView.Draw(drawList, avatarCenter, radius, theme.Accent, Initials.Of(post.AuthorName), 0.95f, lodestone.Avatar(post.AuthorName, post.AuthorWorld), 32);

        if (HoverClick(new Vector2(avatarCenter.X - radius, avatarCenter.Y - radius), new Vector2(avatarCenter.X + radius, avatarCenter.Y + radius)))
        {
            OpenProfile(post.AuthorId);
        }

        var contentLeft = origin.X + radius * 2f + 10f * scale;
        var contentWidth = origin.X + ImGui.GetContentRegionAvail().X - contentLeft;

        var displayName = string.IsNullOrEmpty(post.AuthorDisplayName) ? post.AuthorName : post.AuthorDisplayName;
        var nameSize = Typography.Measure(displayName, 1f, FontWeight.SemiBold);
        Typography.Draw(new Vector2(contentLeft, origin.Y), displayName, theme.TextStrong, 1f, FontWeight.SemiBold);
        var meta = post.AuthorHandle.Length > 0
            ? $"@{post.AuthorHandle} · {RelativeTime(post.CreatedAtUnix)}"
            : $"{post.AuthorWorld} · {RelativeTime(post.CreatedAtUnix)}";
        Typography.Draw(new Vector2(contentLeft + nameSize.X + 6f * scale, origin.Y + 2f * scale), meta, theme.TextMuted, 0.85f);

        ImGui.SetCursorScreenPos(new Vector2(contentLeft, origin.Y + nameSize.Y + 3f * scale));
        ImGui.PushTextWrapPos(contentLeft + contentWidth);
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            ImGui.TextWrapped(post.Text);
        }

        ImGui.PopTextWrapPos();

        ImGui.Dummy(new Vector2(0f, 6f * scale));
        if (DrawActionRow(post, contentLeft, contentWidth))
        {
            ImGui.Dummy(new Vector2(0f, 6f * scale));
            DrawReportComposer(contentLeft, contentWidth);
        }

        ImGui.Dummy(new Vector2(0f, 8f * scale));
        var separatorY = ImGui.GetCursorScreenPos().Y;
        drawList.AddLine(new Vector2(origin.X, separatorY), new Vector2(origin.X + ImGui.GetContentRegionAvail().X, separatorY), ImGui.GetColorU32(theme.Separator), 1f);
        ImGui.Dummy(new Vector2(0f, 8f * scale));
    }

    private bool DrawActionRow(PostDto post, float left, float width)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var rowY = ImGui.GetCursorScreenPos().Y;
        var centerY = rowY + 12f * scale;
        var rowHeight = 24f * scale;
        var reportShown = false;

        if (pickerPostId == post.Id)
        {
            var step = 34f * scale;
            var iconRadius = 14f * scale;
            for (var kind = 0; kind < ChirperReactions.Count; kind++)
            {
                var center = new Vector2(left + iconRadius + kind * step, centerY);
                var active = post.MyReaction == kind;
                var background = active ? Palette.WithAlpha(ChirperReactions.Color(kind), 0.22f) : theme.SurfaceMuted;
                if (DrawIconButton(center, iconRadius, ChirperReactions.Glyph(kind), ChirperReactions.Color(kind), background, 0.95f))
                {
                    store.ToggleReaction(post, kind);
                    pickerPostId = null;
                }
            }

            var closeCenter = new Vector2(left + iconRadius + ChirperReactions.Count * step, centerY);
            if (DrawIconButton(closeCenter, iconRadius, FontAwesomeIcon.Times.ToIconString(), theme.TextMuted, theme.SurfaceMuted, 0.85f))
            {
                pickerPostId = null;
            }
        }
        else
        {
            var reacted = post.MyReaction >= 0;
            var primaryGlyph = reacted ? ChirperReactions.Glyph(post.MyReaction) : FontAwesomeIcon.Heart.ToIconString();
            var primaryColor = reacted ? ChirperReactions.Color(post.MyReaction) : theme.TextMuted;
            var heartCenter = new Vector2(left + 9f * scale, centerY);
            if (DrawIconButton(heartCenter, 13f * scale, primaryGlyph, primaryColor, new Vector4(0f, 0f, 0f, 0f), 0.9f))
            {
                store.ToggleReaction(post, reacted ? post.MyReaction : ChirperReactions.DefaultKind);
            }

            var cursorX = heartCenter.X + 16f * scale;
            if (post.TotalReactions > 0)
            {
                var countText = post.TotalReactions.ToString(Loc.Culture);
                Typography.Draw(new Vector2(cursorX, centerY - 7f * scale), countText, reacted ? primaryColor : theme.TextMuted, 0.82f, FontWeight.Medium);
                cursorX += Typography.Measure(countText, 0.82f, FontWeight.Medium).X + 14f * scale;
            }

            var pickerCenter = new Vector2(cursorX + 9f * scale, centerY);
            if (DrawIconButton(pickerCenter, 12f * scale, FontAwesomeIcon.GrinBeam.ToIconString(), theme.TextMuted, new Vector4(0f, 0f, 0f, 0f), 0.85f))
            {
                pickerPostId = post.Id;
            }

            var reactionSummaryRight = left + width;
            var mine = store.Me is { } me && me.Id == post.AuthorId;
            if (!mine)
            {
                var reportCenter = new Vector2(reactionSummaryRight - 11f * scale, centerY);
                reportShown = DrawReportToggle(reportCenter, 11f * scale, "post", post.Id);
                reactionSummaryRight -= 28f * scale;
            }

            DrawReactionSummary(post, new Vector2(reactionSummaryRight, centerY));
        }

        ImGui.SetCursorScreenPos(new Vector2(left, rowY));
        ImGui.Dummy(new Vector2(width, rowHeight));
        return reportShown;
    }

    private void DrawReactionSummary(PostDto post, Vector2 rightCenter)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var cursorX = rightCenter.X;
        var drawn = 0;
        for (var kind = ChirperReactions.Count - 1; kind >= 0 && drawn < 3; kind--)
        {
            if (post.ReactionCounts[kind] <= 0)
            {
                continue;
            }

            cursorX -= 15f * scale;
            DrawIcon(new Vector2(cursorX, rightCenter.Y), ChirperReactions.Glyph(kind), ChirperReactions.Color(kind), 0.78f);
            drawn++;
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
            var radius = 20f * scale;
            var origin = ImGui.GetCursorScreenPos();
            var me = store.Me;
            if (me is not null)
            {
                AvatarView.Draw(ImGui.GetWindowDrawList(), new Vector2(origin.X + radius, origin.Y + radius), radius, theme.Accent, Initials.Of(me.Name), 0.95f, lodestone.Avatar(me.Name, me.World), 32);
            }

            var inputLeft = radius * 2f + 10f * scale;
            ImGui.SetCursorScreenPos(new Vector2(origin.X + inputLeft, origin.Y));
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - inputLeft);
            if (composeFocus)
            {
                ImGui.SetKeyboardFocusHere();
                composeFocus = false;
            }

            using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
            using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
            using (Plugin.Fonts.Push(1.1f))
            {
                ImGui.InputTextMultiline("##chirpBody", ref draft, MaxPostLength, new Vector2(ImGui.GetContentRegionAvail().X - inputLeft, 200f * scale), ImGuiInputTextFlags.None);
            }

            if (composeStatus.Length > 0)
            {
                ImGui.SetCursorScreenPos(new Vector2(origin.X + inputLeft, origin.Y + 210f * scale));
                using (ImRaii.PushColor(ImGuiCol.Text, theme.Danger))
                {
                    ImGui.TextUnformatted(composeStatus);
                }
            }

            var remaining = MaxPostLength - draft.Length;
            var counterColor = remaining < 40 ? (remaining < 0 ? theme.Danger : new Vector4(0.95f, 0.65f, 0.20f, 1f)) : theme.TextMuted;
            var counter = remaining.ToString(Loc.Culture);
            Typography.Draw(new Vector2(area.Max.X - 24f * scale - Typography.Measure(counter, 0.85f).X, area.Max.Y - 28f * scale), counter, counterColor, 0.85f);
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
        AvatarView.Draw(drawList, avatarCenter, avatarRadius, theme.Accent, Initials.Of(user.Name), 1.2f, lodestone.Avatar(user.Name, user.World), 48);

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
        AvatarView.Draw(drawList, avatarCenter, radius, theme.Accent, Initials.Of(user.Name), 0.95f, lodestone.Avatar(user.Name, user.World), 32);

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
            AvatarView.Draw(ImGui.GetWindowDrawList(), center, radius, theme.Accent, Initials.Of(me.Name), 0.85f, lodestone.Avatar(me.Name, me.World), 24);
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
        pickerPostId = null;
        store.OpenProfile(userId);
        router.Push(ChirperRoute.Profile(userId));
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

    public void Dispose() => store.Dispose();
}
