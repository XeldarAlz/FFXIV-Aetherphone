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
using Dalamud.Interface.Utility;
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
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = 16f * scale;
        var innerLeft = origin.X + pad;
        var innerWidth = width - pad * 2f;
        var displayName = SocialIdentity.Name(user.DisplayName, user.Handle);
        var avatarRadius = 40f * scale;
        var regionCode = user.IsMe ? SocialRegion.EffectiveCode(configuration, gameData) : gameData.RegionCodeForWorld(user.World);
        var metaLine = SocialIdentity.ProfileMeta(user.Handle, regionCode);
        var timeLine = user.UtcOffsetMinutes is { } offsetMinutes ? SocialTimeZone.Describe(offsetMinutes) : string.Empty;
        var lineGap = 3f * scale;
        var timeGap = 2f * scale;
        var nameH = Typography.Measure(displayName, 1.4f, FontWeight.Bold).Y;
        var metaH = metaLine.Length > 0 ? Typography.Measure(metaLine, 0.95f).Y : 0f;
        var timeTextH = timeLine.Length > 0 ? Typography.Measure(timeLine, 0.85f).Y : 0f;
        var timeH = timeLine.Length > 0 ? timeGap + timeTextH : 0f;
        var bioH = user.Bio.Length > 0 ? 8f * scale + Typography.MeasureWrapped(user.Bio, innerWidth, 1f) : 0f;
        var followedByLine = FollowedByLine(user);
        var followedByH = followedByLine.Length > 0
            ? 8f * scale + Typography.MeasureWrapped(followedByLine, innerWidth, TextStyles.Subheadline.Scale)
            : 0f;
        var buttonHeight = 34f * scale;
        var hasIconRow = user.IsMe && (openConductRules is not null ||
            (openSettings is not null && style.SettingsLabel is not null) ||
            (openSaved is not null && style.SavedLabel is not null));
        var iconRowHeight = hasIconRow ? buttonHeight + 8f * scale : 0f;
        var textTop = origin.Y + pad + avatarRadius * 2f + 14f * scale + iconRowHeight;
        var cardBottom = textTop + nameH + lineGap + metaH + timeH + bioH + followedByH + pad;
        ui.Card(drawList, origin, new Vector2(origin.X + width, cardBottom), 20f * scale);
        var avatarCenter = new Vector2(innerLeft + avatarRadius, origin.Y + pad + avatarRadius);
        drawList.AddCircleFilled(avatarCenter, avatarRadius + 2.5f * scale,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.14f)), 64);
        DrawAvatar(drawList, avatarCenter, avatarRadius, theme, user.Name, user.World, user.AvatarUrl, 1.5f, 64);
        avatarLightbox.TryOpen(avatarCenter, avatarRadius, user.AvatarUrl, images);
        var avatarRight = avatarCenter.X + avatarRadius;
        var rightEdge = origin.X + width - pad;
        var reportReserve = user.IsMe ? 0f : buttonHeight + 10f * scale;
        var maxButtonAreaWidth = MathF.Max(60f * scale, rightEdge - avatarRight - 14f * scale - reportReserve);
        var buttonWidth = MathF.Min(122f * scale, maxButtonAreaWidth);
        var buttonMax = new Vector2(rightEdge, avatarCenter.Y + buttonHeight * 0.5f);
        var buttonRect = new Rect(new Vector2(buttonMax.X - buttonWidth, buttonMax.Y - buttonHeight), buttonMax);
        if (user.IsMe)
        {
            var iconRowY = buttonMax.Y + 8f * scale + buttonHeight * 0.5f;
            var iconCenterX = rightEdge - buttonHeight * 0.5f;
            if (openConductRules is not null)
            {
                if (ui.IconButton(new Vector2(iconCenterX, iconRowY), buttonHeight * 0.5f,
                        FontAwesomeIcon.QuestionCircle.ToIconString(), style.Palette.MutedInk,
                        Palette.WithAlpha(style.Palette.MutedInk, 0.14f), 0.9f, Loc.T(L.Conduct.Eyebrow)))
                {
                    openConductRules();
                }

                iconCenterX -= buttonHeight + 8f * scale;
            }

            if (openSettings is not null && style.SettingsLabel is { } settingsLabel)
            {
                if (ui.IconButton(new Vector2(iconCenterX, iconRowY), buttonHeight * 0.5f,
                        FontAwesomeIcon.Cog.ToIconString(), style.Palette.MutedInk,
                        Palette.WithAlpha(style.Palette.MutedInk, 0.14f), 0.9f, Loc.T(settingsLabel)))
                {
                    openSettings();
                }

                iconCenterX -= buttonHeight + 8f * scale;
            }

            if (openSaved is not null && style.SavedLabel is { } savedLabel
                && ui.IconButton(new Vector2(iconCenterX, iconRowY), buttonHeight * 0.5f,
                    FontAwesomeIcon.Bookmark.ToIconString(), style.Palette.MutedInk,
                    Palette.WithAlpha(style.Palette.MutedInk, 0.14f), 0.9f, Loc.T(savedLabel)))
            {
                openSaved();
            }

            if (ui.PillButton(buttonRect, Loc.T(style.EditProfile), false))
            {
                editLoadedFor = null;
                openEditProfile();
            }
        }
        else
        {
            var followRect = buttonRect;
            var hasMessage = openMessage is not null && style.MessageLabel is not null && user.CanMessage;
            var messageRect = default(Rect);
            if (hasMessage)
            {
                var innerRight = origin.X + width - pad;
                var pillGap = 8f * scale;
                var iconRoom = buttonHeight + 10f * scale;
                var available = innerRight - (avatarCenter.X + avatarRadius) - 12f * scale - iconRoom;
                var pillWidth = MathF.Min(buttonWidth, (available - pillGap) * 0.5f);
                followRect = new Rect(new Vector2(innerRight - pillWidth, buttonMax.Y - buttonHeight),
                    new Vector2(innerRight, buttonMax.Y));
                messageRect = new Rect(new Vector2(followRect.Min.X - pillGap - pillWidth, followRect.Min.Y),
                    new Vector2(followRect.Min.X - pillGap, followRect.Max.Y));
            }

            var iconAnchorX = hasMessage ? messageRect.Min.X : followRect.Min.X;
            var reportCenter = new Vector2(iconAnchorX - buttonHeight * 0.5f - 2f * scale, avatarCenter.Y);
            if (ui.IconButton(reportCenter, buttonHeight * 0.5f, FontAwesomeIcon.Flag.ToIconString(), theme.Danger,
                    Palette.WithAlpha(theme.Danger, 0.16f), 0.9f, Loc.T(L.Report.Action)))
            {
                OpenReport("user", user.Id, Loc.T(L.Report.UserTitle));
            }

            if (hasMessage && ui.PillButton(messageRect, Loc.T(style.MessageLabel!.Value), false))
            {
                openMessage!(user.Id);
            }

            if (ui.PillButton(followRect, FollowPillLabel(user), FollowPillFilled(user)))
            {
                store.ToggleFollow(user);
            }
        }

        Marquee.DrawLeftAuto("socialprofile.name." + user.Id, displayName, innerLeft, textTop, innerWidth,
            new TextStyle(1.4f, FontWeight.Bold), theme.TextStrong);
        var textY = textTop + nameH + lineGap;
        if (metaLine.Length > 0)
        {
            var showFollowsYouChip = !user.IsMe && user.FollowsYou;
            var chipReserve = showFollowsYouChip ? FollowsYouChipWidth(scale) + 8f * scale : 0f;
            var metaWidth = Marquee.DrawLeftAuto("socialprofile.meta." + user.Id, metaLine, innerLeft, textY,
                MathF.Max(1f, innerWidth - chipReserve), new TextStyle(0.95f, FontWeight.Regular),
                style.Palette.MutedInk);
            if (showFollowsYouChip)
            {
                var chipAnchor = new Vector2(innerLeft + metaWidth + 8f * scale, textY);
                DrawFollowsYouChip(drawList, chipAnchor, metaH, scale);
            }

            textY += metaH;
        }

        if (timeLine.Length > 0)
        {
            textY += timeGap;
            Marquee.DrawLeftAuto("socialprofile.time." + user.Id, timeLine, innerLeft, textY, innerWidth,
                new TextStyle(0.85f, FontWeight.Regular), style.Palette.MutedInk);
            textY += timeTextH;
        }

        if (user.Bio.Length > 0)
        {
            ImGui.SetCursorScreenPos(new Vector2(innerLeft, textY + 8f * scale));
            var bioWrapPos = (innerLeft + innerWidth) - ImGui.GetWindowPos().X;
            ImGui.PushTextWrapPos(bioWrapPos);
            using (ImRaii.PushColor(ImGuiCol.Text, style.Palette.BodyInk))
            {
                Typography.Wrapped(user.Bio);
            }

            ImGui.PopTextWrapPos();
        }

        if (followedByLine.Length > 0)
        {
            var lineTop = new Vector2(innerLeft, textY + bioH + 8f * scale);
            var drawnHeight = Typography.DrawWrappedLeft(lineTop, followedByLine, style.Palette.MutedInk,
                TextStyles.Subheadline, innerWidth);
            if (UiInteract.HoverClick(lineTop, new Vector2(innerLeft + innerWidth, lineTop.Y + drawnHeight)))
            {
                openUserList(user.Id, UserListKind.Mutuals);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cardBottom - origin.Y + 10f * scale));
        DrawProfileStats(user, theme);
        ImGui.Dummy(new Vector2(0f, 14f * scale));
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
        var scale = ImGuiHelpers.GlobalScale;
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
        var scale = ImGuiHelpers.GlobalScale;
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
        var scale = ImGuiHelpers.GlobalScale;
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
            if (ui.PillButton(changeRect, Loc.T(style.ChangePhoto), false))
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
        var scale = ImGuiHelpers.GlobalScale;
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
        var scale = ImGuiHelpers.GlobalScale;
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
        var scale = ImGuiHelpers.GlobalScale;
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
        DrawAvatar(drawList, avatarCenter, radius, theme, user.Name, user.World, user.AvatarUrl, 0.95f, 32);
        var textLeft = avatarCenter.X + radius + 12f * scale;
        var displayName = SocialIdentity.Name(user.DisplayName, user.Handle);
        var nameTop = style.CardUserRows ? 12f : 9f;
        var subTop = style.CardUserRows ? 33f : 31f;
        var buttonWidth = 96f * scale;
        var buttonHeight = 30f * scale;
        var textMaxWidth = origin.X + width - pad - buttonWidth - 10f * scale - textLeft;
        var nameY = origin.Y + nameTop * scale;
        var nameSize = Typography.Measure(displayName, 1f, FontWeight.SemiBold);
        var nameHovering = ImGui.IsMouseHoveringRect(new Vector2(textLeft, nameY),
            new Vector2(textLeft + textMaxWidth, nameY + nameSize.Y));
        Marquee.DrawLeft("socialprofile.row.name." + user.Id, displayName, textLeft, nameY,
            textMaxWidth, new TextStyle(1f, FontWeight.SemiBold), theme.TextStrong, nameHovering);
        var regionCode = user.IsMe ? SocialRegion.EffectiveCode(configuration, gameData) : gameData.RegionCodeForWorld(user.World);
        var sub = SocialIdentity.ProfileMeta(user.Handle, regionCode);
        var subY = origin.Y + subTop * scale;
        var subSize = Typography.Measure(sub, 0.85f);
        var subHovering = ImGui.IsMouseHoveringRect(new Vector2(textLeft, subY),
            new Vector2(textLeft + textMaxWidth, subY + subSize.Y));
        Marquee.DrawLeft("socialprofile.row.sub." + user.Id, sub, textLeft, subY,
            textMaxWidth, new TextStyle(0.85f, FontWeight.Regular), style.Palette.MutedInk, subHovering);
        var buttonRect =
            new Rect(
                new Vector2(origin.X + width - pad - buttonWidth, origin.Y + rowHeight * 0.5f - buttonHeight * 0.5f),
                new Vector2(origin.X + width - pad, origin.Y + rowHeight * 0.5f + buttonHeight * 0.5f));
        if (ui.PillButton(buttonRect, FollowPillLabel(user), FollowPillFilled(user)))
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
        var scale = ImGuiHelpers.GlobalScale;
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
