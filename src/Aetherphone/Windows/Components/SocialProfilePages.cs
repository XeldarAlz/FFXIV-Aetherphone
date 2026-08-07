using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Report;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal sealed class SocialProfileStyle
{
    public required AppPalette Palette { get; init; }
    public required string SearchInputId { get; init; }
    public required bool StatsPostsFirst { get; init; }
    public required bool CountGrams { get; init; }
    public required bool CardUserRows { get; init; }
    public Vector4? HandleValidInk { get; init; }
    public required LocString EditProfile { get; init; }
    public required LocString Follow { get; init; }
    public required LocString Following { get; init; }
    public required LocPlural Posts { get; init; }
    public required LocString Save { get; init; }
    public required LocString Saving { get; init; }
    public required LocString HandleTaken { get; init; }
    public required LocString HandleRules { get; init; }
    public required LocString HandleLabel { get; init; }
    public required LocString DisplayNameLabel { get; init; }
    public required LocString BioLabel { get; init; }
    public required LocString ChangePhoto { get; init; }
    public required LocString ProfileError { get; init; }
    public required LocString NameOrWorld { get; init; }
    public required LocString SearchByName { get; init; }
    public required LocString DeleteConfirmMessage { get; init; }
    public required LocString DeleteConfirm { get; init; }
    public required LocString DeleteCancel { get; init; }
    public required LocString DeleteFailed { get; init; }
    public required LocString DeleteCommentConfirmMessage { get; init; }
    public required LocString DeleteCommentFailed { get; init; }
    public LocString? MessageLabel { get; init; }
    public LocString? SettingsLabel { get; init; }
    public LocString? SavedLabel { get; init; }
}

internal sealed class SocialProfilePages
{
    public const int DisplayNameMax = 40;
    public const int HandleMax = 15;
    public const int BioMax = 200;

    private const float ProfileAvatarRadius = 40f;
    private const float ActionRowHeight = 34f;

    private readonly SocialFeedStore store;
    private readonly AppSkin ui;
    private readonly SocialProfileStyle style;
    private readonly RemoteImageCache images;
    private readonly LodestoneService lodestone;
    private readonly AvatarLightbox avatarLightbox;
    private readonly Configuration configuration;
    private readonly GameData gameData;
    private readonly ConfirmService confirm;
    private readonly ReportService report;
    private readonly Action openEditProfile;
    private readonly Action openAvatarComposer;
    private readonly Action<string> openProfile;
    private readonly Action<string, UserListKind> openUserList;
    private readonly Action back;
    private readonly Action? openConductRules;
    private readonly Action<string>? openMessage;
    private readonly Action? openSettings;
    private readonly Action? openSaved;

    private string editDisplay = string.Empty;
    private string editHandle = string.Empty;
    private string editBio = string.Empty;
    private string editStatus = string.Empty;
    private string? editLoadedFor;
    private volatile bool editBusy;
    private volatile int editOutcome;

    public SocialProfilePages(SocialFeedStore store, AppSkin ui, SocialProfileStyle style, RemoteImageCache images,
        LodestoneService lodestone, AvatarLightbox avatarLightbox, Configuration configuration, GameData gameData,
        ConfirmService confirm, ReportService report, Action openEditProfile, Action openAvatarComposer,
        Action<string> openProfile, Action<string, UserListKind> openUserList, Action back,
        Action? openConductRules, Action<string>? openMessage = null, Action? openSettings = null,
        Action? openSaved = null)
    {
        this.store = store;
        this.ui = ui;
        this.style = style;
        this.images = images;
        this.lodestone = lodestone;
        this.avatarLightbox = avatarLightbox;
        this.configuration = configuration;
        this.gameData = gameData;
        this.confirm = confirm;
        this.report = report;
        this.openEditProfile = openEditProfile;
        this.openAvatarComposer = openAvatarComposer;
        this.openProfile = openProfile;
        this.openUserList = openUserList;
        this.back = back;
        this.openConductRules = openConductRules;
        this.openMessage = openMessage;
        this.openSettings = openSettings;
        this.openSaved = openSaved;
    }

    public string SearchDraft = string.Empty;

    public void ResetEdit()
    {
        editLoadedFor = null;
    }

    public void EnsureLoaded(SocialFeedScope scope)
    {
        if (store.Feed(scope).Length == 0 && !store.IsLoading(scope))
        {
            store.RefreshFeed(scope);
        }
    }

    public static bool IsHandleValid(string handle)
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

    public string PostsLabel(int count) => TrailingWord(Loc.Plural(style.Posts, count));

    public static string FollowersLabel(int count) => TrailingWord(Loc.Plural(L.Account.Followers, count));

    private static string TrailingWord(string plural)
    {
        var parts = plural.Split(' ', 2);
        return parts.Length > 1 ? parts[1] : plural;
    }

    public void DrawProfileHeader(UserDto user, PhoneTheme theme)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = Metrics.Space.Lg * scale;
        var innerLeft = origin.X + pad;
        var innerRight = origin.X + width - pad;
        var innerWidth = MathF.Max(1f, innerRight - innerLeft);
        var displayName = SocialIdentity.Name(user.DisplayName, user.Handle);
        var avatarRadius = ProfileAvatarRadius * scale;
        var regionCode = user.IsMe
            ? SocialRegion.EffectiveCode(configuration, gameData)
            : SocialRegion.Resolve(user.Region, user.World, gameData);
        var metaLine = SocialIdentity.ProfileMeta(user.Handle, regionCode);
        var timeLine = user.UtcOffsetMinutes is { } offsetMinutes ? SocialTimeZone.Describe(offsetMinutes) : string.Empty;
        var lineGap = 3f * scale;
        var nameHeight = Typography.Measure(displayName, TextStyles.Title2).Y;
        var metaHeight = metaLine.Length > 0 ? Typography.Measure(metaLine, TextStyles.Callout).Y : 0f;
        var timeHeight = timeLine.Length > 0 ? Typography.Measure(timeLine, TextStyles.Footnote).Y : 0f;
        var identityHeight = nameHeight
            + (metaHeight > 0f ? lineGap + metaHeight : 0f)
            + (timeHeight > 0f ? lineGap + timeHeight : 0f);
        var identityLeft = innerLeft + avatarRadius * 2f + Metrics.Space.Md * scale;
        var identityWidth = MathF.Max(1f, innerRight - identityLeft);
        var headTop = origin.Y + pad;
        var headHeight = MathF.Max(avatarRadius * 2f, identityHeight);
        var bioHeight = user.Bio.Length > 0
            ? Typography.MeasureWrappedBlock(user.Bio, TextStyles.Body, innerWidth).Y
            : 0f;
        var followedByLine = FollowedByLine(user);
        var followedByHeight = followedByLine.Length > 0
            ? Typography.MeasureWrappedBlock(followedByLine, TextStyles.Subheadline, innerWidth).Y
            : 0f;
        var contentBottom = headTop + headHeight;
        var bioTop = contentBottom + Metrics.Space.Md * scale;
        if (bioHeight > 0f)
        {
            contentBottom = bioTop + bioHeight;
        }

        var followedByTop = contentBottom + (bioHeight > 0f ? Metrics.Space.Xs : Metrics.Space.Md) * scale;
        if (followedByHeight > 0f)
        {
            contentBottom = followedByTop + followedByHeight;
        }

        var actionHeight = ActionRowHeight * scale;
        var actionTop = contentBottom + Metrics.Space.Lg * scale;
        var cardBottom = actionTop + actionHeight + pad;
        ui.Card(drawList, origin, new Vector2(origin.X + width, cardBottom), 20f * scale);
        var avatarCenter = new Vector2(innerLeft + avatarRadius, headTop + headHeight * 0.5f);
        drawList.AddCircleFilled(avatarCenter, avatarRadius + 2.5f * scale,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.14f)), 64);
        var portraitName = user.IsMe ? user.Name : displayName;
        var portraitWorld = user.IsMe ? user.World : string.Empty;
        DrawAvatar(drawList, avatarCenter, avatarRadius, theme, portraitName, portraitWorld, user.AvatarUrl, 1.5f, 64);
        avatarLightbox.TryOpen(avatarCenter, avatarRadius, user.AvatarUrl, images);
        var textY = headTop + (headHeight - identityHeight) * 0.5f;
        UserName.DrawAuto(drawList, "socialprofile.name." + user.Id, displayName, user.Badges, user.ProfileBadges,
            identityLeft, textY, identityWidth, TextStyles.Title2, theme.TextStrong, theme, 2);
        textY += nameHeight;
        if (metaHeight > 0f)
        {
            textY += lineGap;
            var showFollowsYouChip = !user.IsMe && user.FollowsYou;
            var chipReserve = showFollowsYouChip ? FollowsYouChipWidth(scale) + Metrics.Space.Sm * scale : 0f;
            var metaWidth = Marquee.DrawLeftAuto("socialprofile.meta." + user.Id, metaLine, identityLeft, textY,
                MathF.Max(1f, identityWidth - chipReserve), TextStyles.Callout, style.Palette.MutedInk);
            if (showFollowsYouChip)
            {
                var chipAnchor = new Vector2(identityLeft + metaWidth + Metrics.Space.Sm * scale, textY);
                DrawFollowsYouChip(drawList, chipAnchor, metaHeight, scale);
            }

            textY += metaHeight;
        }

        if (timeHeight > 0f)
        {
            textY += lineGap;
            Marquee.DrawLeftAuto("socialprofile.time." + user.Id, timeLine, identityLeft, textY, identityWidth,
                TextStyles.Footnote, style.Palette.MutedInk);
        }

        if (bioHeight > 0f)
        {
            Typography.DrawWrappedLeft(new Vector2(innerLeft, bioTop), user.Bio, style.Palette.BodyInk,
                TextStyles.Body, innerWidth);
        }

        if (followedByHeight > 0f)
        {
            var lineTop = new Vector2(innerLeft, followedByTop);
            Typography.DrawWrappedLeft(lineTop, followedByLine, style.Palette.MutedInk, TextStyles.Subheadline,
                innerWidth);
            if (UiInteract.HoverClick(lineTop, new Vector2(innerRight, lineTop.Y + followedByHeight)))
            {
                openUserList(user.Id, UserListKind.Mutuals);
            }
        }

        DrawProfileActions(user, theme, innerLeft, innerRight, actionTop, actionHeight);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cardBottom - origin.Y + 10f * scale));
        DrawProfileStats(user, theme);
        ImGui.Dummy(new Vector2(0f, 14f * scale));
    }

    private void DrawProfileActions(UserDto user, PhoneTheme theme, float left, float right, float top, float height)
    {
        var scale = UiScale.Current;
        var gap = Metrics.Space.Sm * scale;
        var centerY = top + height * 0.5f;
        var trailing = right;
        var secondaryLabel = string.Empty;
        if (user.IsMe)
        {
            if (openConductRules is not null)
            {
                trailing = DrawTrailingIcon(trailing, centerY, height, FontAwesomeIcon.QuestionCircle.ToIconString(),
                    style.Palette.MutedInk, Loc.T(L.Conduct.Eyebrow), openConductRules);
            }

            if (openSettings is not null && style.SettingsLabel is { } settingsLabel)
            {
                trailing = DrawTrailingIcon(trailing, centerY, height, FontAwesomeIcon.Cog.ToIconString(),
                    style.Palette.MutedInk, Loc.T(settingsLabel), openSettings);
            }

            if (openSaved is not null && style.SavedLabel is { } savedLabel)
            {
                trailing = DrawTrailingIcon(trailing, centerY, height, FontAwesomeIcon.Bookmark.ToIconString(),
                    style.Palette.MutedInk, Loc.T(savedLabel), openSaved);
            }
        }
        else
        {
            trailing = DrawTrailingIcon(trailing, centerY, height, FontAwesomeIcon.Flag.ToIconString(), theme.Danger,
                Loc.T(L.Report.Action), () => OpenReport("user", user.Id, Loc.T(L.Report.UserTitle)));
            if (openMessage is not null && style.MessageLabel is { } messageLabel && user.CanMessage)
            {
                secondaryLabel = Loc.T(messageLabel);
            }
        }

        var primaryRight = trailing;
        if (secondaryLabel.Length > 0)
        {
            var natural = Typography.Measure(secondaryLabel, 0.9f, FontWeight.SemiBold).X + height;
            var secondaryWidth = MathF.Min(natural, MathF.Max(1f, (trailing - left - gap) * 0.5f));
            primaryRight = trailing - secondaryWidth - gap;
            var secondaryRect = new Rect(new Vector2(trailing - secondaryWidth, top),
                new Vector2(trailing, top + height));
            if (ui.PillButton(secondaryRect, secondaryLabel, false, "socialprofile.pill.message." + user.Id))
            {
                openMessage!(user.Id);
            }
        }

        var primaryRect = new Rect(new Vector2(left, top), new Vector2(MathF.Max(left + height, primaryRight),
            top + height));
        if (!user.IsMe)
        {
            if (ui.PillButton(primaryRect, FollowPillLabel(user), FollowPillFilled(user),
                    "socialprofile.pill.follow." + user.Id))
            {
                store.ToggleFollow(user);
            }

            return;
        }

        if (ui.PillButton(primaryRect, Loc.T(style.EditProfile), false, "socialprofile.pill.editprofile." + user.Id))
        {
            editLoadedFor = null;
            openEditProfile();
        }
    }

    private float DrawTrailingIcon(float rightEdge, float centerY, float diameter, string glyph, Vector4 ink,
        string tooltip, Action activate)
    {
        var radius = diameter * 0.5f;
        if (ui.IconButton(new Vector2(rightEdge - radius, centerY), radius, glyph, ink,
                Palette.WithAlpha(ink, 0.16f), 0.9f, tooltip))
        {
            activate();
        }

        return rightEdge - diameter - Metrics.Space.Sm * UiScale.Current;
    }

    private string FollowPillLabel(UserDto user) => SocialFeedStore.FollowStateOf(user) switch
    {
        FollowState.Following => Loc.T(style.Following),
        FollowState.Requested => Loc.T(L.Social.Requested),
        _ => Loc.T(style.Follow),
    };

    private static bool FollowPillFilled(UserDto user) => SocialFeedStore.FollowStateOf(user) == FollowState.None;

    private static bool CanViewFollowLists(UserDto user) => user.IsMe || user.IsFollowing || !user.IsPrivate;

    private void DrawProfileStats(UserDto user, PhoneTheme theme)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = 64f * scale;
        ui.Card(drawList, origin, new Vector2(origin.X + width, origin.Y + height), 18f * scale);
        var third = width / 3f;
        var centerY = origin.Y + height * 0.5f;
        var postsColumn = style.StatsPostsFirst ? 0 : 2;
        var listsOpen = CanViewFollowLists(user);
        if (listsOpen)
        {
            for (var column = 0; column < 3; column++)
            {
                if (column != postsColumn)
                {
                    DrawStatHover(drawList, origin, third * column, third, height, scale);
                }
            }
        }

        var dividerColor = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.08f));
        for (var index = 1; index < 3; index++)
        {
            var x = origin.X + third * index;
            drawList.AddLine(new Vector2(x, origin.Y + 14f * scale), new Vector2(x, origin.Y + height - 14f * scale),
                dividerColor, 1f);
        }

        var postCount = style.CountGrams ? user.Grams : user.Posts;
        var followingColumn = style.StatsPostsFirst ? 2 : 0;
        DrawStatColumn(origin.X + third * postsColumn, third, centerY, theme, postCount.ToString(Loc.Culture),
            PostsLabel(postCount));
        DrawStatColumn(origin.X + third * 1f, third, centerY, theme, user.Followers.ToString(Loc.Culture),
            FollowersLabel(user.Followers));
        DrawStatColumn(origin.X + third * followingColumn, third, centerY, theme,
            user.Following.ToString(Loc.Culture), Loc.T(style.Following));
        if (listsOpen && UiInteract.HoverClick(new Vector2(origin.X + third * followingColumn, origin.Y),
                new Vector2(origin.X + third * (followingColumn + 1), origin.Y + height)))
        {
            openUserList(user.Id, UserListKind.Following);
        }

        if (listsOpen && UiInteract.HoverClick(new Vector2(origin.X + third, origin.Y),
                new Vector2(origin.X + third * 2f, origin.Y + height)))
        {
            openUserList(user.Id, UserListKind.Followers);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawStatColumn(float left, float columnWidth, float centerY, PhoneTheme theme, string value,
        string label)
    {
        var scale = UiScale.Current;
        var center = left + columnWidth * 0.5f;
        Typography.DrawCentered(new Vector2(center, centerY - 10f * scale), value, theme.TextStrong, 1.25f,
            FontWeight.Bold);
        Typography.DrawCentered(new Vector2(center, centerY + 13f * scale), label, style.Palette.MutedInk, 0.8f);
    }

    private static void DrawStatHover(ImDrawListPtr drawList, Vector2 origin, float columnOffset, float columnWidth,
        float height, float scale)
    {
        var padX = 6f * scale;
        var padY = 8f * scale;
        var min = new Vector2(origin.X + columnOffset + padX, origin.Y + padY);
        var max = new Vector2(origin.X + columnOffset + columnWidth - padX, origin.Y + height - padY);
        UiInteract.HoverHighlight(drawList, min, max, 12f * scale);
    }

    public void DrawEditProfile(Rect area, PhoneTheme theme, INavigator navigation)
    {
        var me = store.Me ?? (store.ProfileUser is { IsMe: true } self ? self : null);
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(style.EditProfile), back);
        var scale = UiScale.Current;
        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        if (me is null)
        {
            store.EnsureMe();
            Typography.DrawCentered(body.Center, Loc.T(L.Common.Loading), style.Palette.MutedInk);
            return;
        }

        if (editOutcome == 1)
        {
            editOutcome = 0;
            store.ReloadProfile();
            back();
            return;
        }

        if (editOutcome == 2)
        {
            editOutcome = 0;
            editStatus = Loc.T(style.HandleTaken);
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
        if (ui.HeaderAction(area, editBusy ? Loc.T(style.Saving) : Loc.T(style.Save), canSave))
        {
            SaveProfile();
        }

        using (AppSurface.Begin(body))
        {
            var avatarRadius = 34f * scale;
            var avatarOrigin = ImGui.GetCursorScreenPos();
            var avatarCenter = new Vector2(avatarOrigin.X + ImGui.GetContentRegionAvail().X * 0.5f,
                avatarOrigin.Y + avatarRadius);
            DrawAvatar(ImGui.GetWindowDrawList(), avatarCenter, avatarRadius, theme, me.Name, me.World, me.AvatarUrl,
                1.3f, 48);
            ImGui.SetCursorScreenPos(new Vector2(avatarOrigin.X, avatarCenter.Y + avatarRadius + 8f * scale));
            var changeWidth = 150f * scale;
            var changeTop = ImGui.GetCursorScreenPos().Y;
            var changeRect = new Rect(new Vector2(avatarCenter.X - changeWidth * 0.5f, changeTop),
                new Vector2(avatarCenter.X + changeWidth * 0.5f, changeTop + 30f * scale));
            if (ui.PillButton(changeRect, Loc.T(style.ChangePhoto), false, "socialprofile.pill.changephoto"))
            {
                openAvatarComposer();
            }

            ImGui.SetCursorScreenPos(new Vector2(avatarOrigin.X, changeRect.Max.Y + 16f * scale));
            ui.Field(Loc.T(style.DisplayNameLabel), "##editDisplay", ref editDisplay, DisplayNameMax, false);
            ImGui.Dummy(new Vector2(0f, 10f * scale));
            DrawHandleField(theme);
            ImGui.Dummy(new Vector2(0f, 10f * scale));
            ui.Field(Loc.T(style.BioLabel), "##editBio", ref editBio, BioMax, true);
            if (editStatus.Length > 0)
            {
                ImGui.Dummy(new Vector2(0f, 10f * scale));
                using (ImRaii.PushColor(ImGuiCol.Text, theme.Danger))
                {
                    Typography.Wrapped(editStatus);
                }
            }
        }
    }

    private void DrawHandleField(PhoneTheme theme)
    {
        var scale = UiScale.Current;
        using (ImRaii.PushColor(ImGuiCol.Text, style.Palette.MutedInk))
        {
            Typography.Plain(Loc.T(style.HandleLabel));
        }

        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = 34f * scale;
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, origin, new Vector2(origin.X + width, origin.Y + height), 9f * scale,
            ImGui.GetColorU32(style.Palette.FieldSurface));
        Typography.Draw(new Vector2(origin.X + 12f * scale, origin.Y + height * 0.5f - 8f * scale), "@",
            style.Palette.MutedInk, 1f);
        ImGui.SetCursorScreenPos(new Vector2(origin.X + 26f * scale,
            origin.Y + height * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(width - 38f * scale);
        var validInk = style.HandleValidInk ?? theme.TextStrong;
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, IsHandleValid(editHandle) ? validInk : theme.Danger))
        {
            if (ImGui.InputText("##editHandle", ref editHandle, HandleMax, ImGuiInputTextFlags.CharsNoBlank))
            {
                editHandle = editHandle.ToLowerInvariant();
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
        Typography.Draw(new Vector2(origin.X + 2f * scale, origin.Y + height + 3f * scale),
            Typography.FitText(Loc.T(style.HandleRules), width - 4f * scale, 0.78f, FontWeight.Regular),
            style.Palette.MutedInk, 0.78f);
        ImGui.Dummy(new Vector2(width, 16f * scale));
    }

    private void SaveProfile()
    {
        if (!store.IsSignedIn || editBusy)
        {
            return;
        }

        if (!IsHandleValid(editHandle) || editDisplay.Trim().Length == 0)
        {
            editStatus = Loc.T(style.HandleRules);
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

    public void DrawUserList(Rect area, PhoneTheme theme, INavigator navigation, string sourceId, UserListKind kind)
    {
        store.OpenUserList(sourceId, kind);
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, UserListTitle(kind), back);
        var scale = UiScale.Current;
        var top = area.Min.Y + AppHeader.Height * scale;
        var listRect = new Rect(new Vector2(area.Min.X, top), area.Max);
        var snapshot = store.UserListResults;
        using (AppSurface.Begin(listRect))
        {
            if (snapshot.Length == 0)
            {
                var message = store.UserListLoading ? Loc.T(L.Common.Loading)
                    : store.UserListFailed ? Loc.T(style.ProfileError)
                    : Loc.T(L.Social.ListEmpty);
                Typography.DrawCentered(new Vector2(listRect.Center.X, listRect.Min.Y + 60f * scale), message,
                    style.Palette.MutedInk);
            }
            else
            {
                ImGui.Dummy(new Vector2(0f, 4f * scale));
                for (var index = 0; index < snapshot.Length; index++)
                {
                    DrawUserRow(snapshot[index], theme);
                }

                if (store.UserListLoadingMore)
                {
                    InfiniteScroll.DrawLoadingRow(listRect.Center.X, style.Palette.MutedInk);
                }
                else if (store.HasMoreUserList && InfiniteScroll.ReachedBottom())
                {
                    store.LoadMoreUserList();
                }

                ImGui.Dummy(new Vector2(0f, 12f * scale));
            }
        }
    }

    public static string UserListTitle(UserListKind kind) => kind switch
    {
        UserListKind.Followers => Loc.T(L.Social.FollowersTitle),
        UserListKind.Following => Loc.T(L.Social.FollowingTitle),
        UserListKind.Mutuals => Loc.T(L.Social.MutualsTitle),
        _ => Loc.T(L.Social.LikedByTitle),
    };

    private static float FollowsYouChipWidth(float scale)
    {
        var label = Loc.T(L.Social.FollowsYou);
        var size = Typography.Measure(label, TextStyles.Footnote.Scale, TextStyles.Footnote.Weight);
        return size.X + 7f * scale * 2f;
    }

    private void DrawFollowsYouChip(ImDrawListPtr drawList, Vector2 anchor, float lineHeight, float scale)
    {
        var label = Loc.T(L.Social.FollowsYou);
        var size = Typography.Measure(label, TextStyles.Footnote.Scale, TextStyles.Footnote.Weight);
        var padX = 7f * scale;
        var padY = 2.5f * scale;
        var centerY = anchor.Y + lineHeight * 0.5f;
        var min = new Vector2(anchor.X, centerY - size.Y * 0.5f - padY);
        var max = new Vector2(anchor.X + size.X + padX * 2f, centerY + size.Y * 0.5f + padY);
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(Palette.WithAlpha(style.Palette.MutedInk, 0.14f)),
            (max.Y - min.Y) * 0.5f);
        Typography.Draw(drawList, new Vector2(min.X + padX, centerY - size.Y * 0.5f), label, style.Palette.MutedInk,
            TextStyles.Footnote);
    }

    private static string FollowedByLine(UserDto user)
    {
        if (user.IsMe || user.FollowedByCount <= 0 || user.FollowedByPreview is not { Length: > 0 } preview)
        {
            return string.Empty;
        }

        var others = user.FollowedByCount - preview.Length;
        if (others <= 0)
        {
            return preview.Length == 1
                ? string.Format(Loc.Culture, Loc.T(L.Social.FollowedByOne), preview[0])
                : string.Format(Loc.Culture, Loc.T(L.Social.FollowedByTwo), preview[0], preview[1]);
        }

        if (preview.Length == 1)
        {
            return others == 1
                ? string.Format(Loc.Culture, Loc.T(L.Social.FollowedByOneMoreOne), preview[0])
                : string.Format(Loc.Culture, Loc.T(L.Social.FollowedByOneMoreMany), preview[0], others);
        }

        return others == 1
            ? string.Format(Loc.Culture, Loc.T(L.Social.FollowedByTwoMoreOne), preview[0], preview[1])
            : string.Format(Loc.Culture, Loc.T(L.Social.FollowedByTwoMoreMany), preview[0], preview[1], others);
    }

    public void DrawUserRow(UserDto user, PhoneTheme theme)
    {
        var scale = UiScale.Current;
        var rowHeight = 58f * scale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var drawList = ImGui.GetWindowDrawList();
        var pad = style.CardUserRows ? 12f * scale : 0f;
        if (style.CardUserRows)
        {
            ui.Card(drawList, origin, new Vector2(origin.X + width, origin.Y + rowHeight), 16f * scale);
        }

        var radius = 20f * scale;
        var avatarCenter = new Vector2(origin.X + pad + radius, origin.Y + rowHeight * 0.5f);
        var displayName = SocialIdentity.Name(user.DisplayName, user.Handle);
        var portraitName = user.IsMe ? user.Name : displayName;
        var portraitWorld = user.IsMe ? user.World : string.Empty;
        DrawAvatar(drawList, avatarCenter, radius, theme, portraitName, portraitWorld, user.AvatarUrl, 0.95f, 32);
        var textLeft = avatarCenter.X + radius + 12f * scale;
        var nameTop = style.CardUserRows ? 12f : 9f;
        var subTop = style.CardUserRows ? 33f : 31f;
        var buttonWidth = 96f * scale;
        var buttonHeight = 30f * scale;
        var textMaxWidth = origin.X + width - pad - buttonWidth - 10f * scale - textLeft;
        var nameY = origin.Y + nameTop * scale;
        var nameSize = Typography.Measure(displayName, 1f, FontWeight.SemiBold);
        var nameHovering = UiInteract.Hover(new Vector2(textLeft, nameY),
            new Vector2(textLeft + textMaxWidth, nameY + nameSize.Y));
        UserName.Draw(drawList, "socialprofile.row.name." + user.Id, displayName, user.Badges, user.ProfileBadges, textLeft, nameY,
            textMaxWidth, new TextStyle(1f, FontWeight.SemiBold), theme.TextStrong, nameHovering, theme);
        var regionCode = user.IsMe
            ? SocialRegion.EffectiveCode(configuration, gameData)
            : SocialRegion.Resolve(user.Region, user.World, gameData);
        var sub = SocialIdentity.ProfileMeta(user.Handle, regionCode);
        var subY = origin.Y + subTop * scale;
        var subSize = Typography.Measure(sub, 0.85f);
        var subHovering = UiInteract.Hover(new Vector2(textLeft, subY),
            new Vector2(textLeft + textMaxWidth, subY + subSize.Y));
        Marquee.DrawLeft("socialprofile.row.sub." + user.Id, sub, textLeft, subY,
            textMaxWidth, new TextStyle(0.85f, FontWeight.Regular), style.Palette.MutedInk, subHovering);
        var buttonRect =
            new Rect(
                new Vector2(origin.X + width - pad - buttonWidth, origin.Y + rowHeight * 0.5f - buttonHeight * 0.5f),
                new Vector2(origin.X + width - pad, origin.Y + rowHeight * 0.5f + buttonHeight * 0.5f));
        if (ui.PillButton(buttonRect, FollowPillLabel(user), FollowPillFilled(user), "socialprofile.row.follow." + user.Id))
        {
            store.ToggleFollow(user);
        }

        var rowMax = new Vector2(origin.X + width - buttonWidth - pad - 6f * scale, origin.Y + rowHeight);
        if (UiInteract.HoverClick(origin, rowMax))
        {
            openProfile(user.Id);
        }

        ImGui.SetCursorScreenPos(origin);
        var rowSpacing = style.CardUserRows ? 8f * scale : 0f;
        ImGui.Dummy(new Vector2(width, rowHeight + rowSpacing));
    }

    public void DrawSearchBar(Rect bar)
    {
        if (SearchField.DrawSubmit(bar, style.SearchInputId, Loc.T(style.NameOrWorld), ref SearchDraft,
                style.Palette))
        {
            store.Search(SearchDraft);
        }
    }

    public void DrawSearchResults(Rect listRect, PhoneTheme theme, bool topPadding)
    {
        var scale = UiScale.Current;
        var snapshot = store.DiscoverResults;
        using (AppSurface.Begin(listRect))
        {
            if (snapshot.Length == 0)
            {
                Typography.DrawCentered(new Vector2(listRect.Center.X, listRect.Min.Y + 60f * scale),
                    store.Searching ? Loc.T(L.Common.Searching) : Loc.T(style.SearchByName), style.Palette.MutedInk);
            }
            else
            {
                if (topPadding)
                {
                    ImGui.Dummy(new Vector2(0f, 4f * scale));
                }

                for (var index = 0; index < snapshot.Length; index++)
                {
                    DrawUserRow(snapshot[index], theme);
                }
            }
        }
    }

    public void OpenReport(string targetType, string targetId, string title)
    {
        report.Open(new ReportPrompt
        {
            Title = title,
            Submit = (reason, done) => store.Report(targetType, targetId, reason, done),
        });
    }

    public void AskBlock(string authorDisplayName, string authorHandle, string authorId)
    {
        var name = SocialIdentity.Name(authorDisplayName, authorHandle);
        confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Social.BlockConfirm, name),
            ConfirmLabel = Loc.T(L.Social.BlockAction),
            CancelLabel = Loc.T(L.Common.Cancel),
            Danger = true,
            Confirm = () => store.Block(authorId, _ => { }),
        });
    }

    public void AskDeletePost(string postId, Action? deleted = null)
    {
        confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(style.DeleteConfirmMessage),
            ConfirmLabel = Loc.T(style.DeleteConfirm),
            CancelLabel = Loc.T(style.DeleteCancel),
            BusyLabel = Loc.T(style.Saving),
            FailedMessage = Loc.T(style.DeleteFailed),
            ConfirmAsync = done => store.DeletePost(postId, ok =>
            {
                if (ok)
                {
                    deleted?.Invoke();
                }

                done(ok);
            }),
        });
    }

    public void AskDeleteComment(string postId, string commentId)
    {
        confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(style.DeleteCommentConfirmMessage),
            ConfirmLabel = Loc.T(style.DeleteConfirm),
            CancelLabel = Loc.T(style.DeleteCancel),
            BusyLabel = Loc.T(style.Saving),
            FailedMessage = Loc.T(style.DeleteCommentFailed),
            ConfirmAsync = done => store.DeleteComment(postId, commentId, done),
        });
    }

    private void DrawAvatar(ImDrawListPtr drawList, Vector2 center, float radius, PhoneTheme theme, string name,
        string world, string? avatarUrl, float monogramScale, int segments)
    {
        AvatarView.DrawRemote(drawList, center, radius, theme, name, world, avatarUrl, images, lodestone,
            monogramScale, segments);
    }
}
