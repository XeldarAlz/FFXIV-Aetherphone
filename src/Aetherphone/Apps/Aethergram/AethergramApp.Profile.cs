using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Translation;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Aethergram;

internal sealed partial class AethergramApp
{
    private enum ProfileMenuAction
    {
        Settings,
        Saved,
        FollowRequests,
        Encryption,
        Rules,
        Report,
        Block,
    }

    private const float ProfileAvatarRadius = 44f;
    private const float ProfileRingGap = 4f;
    private const float ProfileStoryBadgeRadius = 10f;
    private const float ProfileHeadTop = 12f;
    private const float ProfileStatsGap = 18f;
    private const float ProfileStatColumnPad = 10f;
    private const float ProfileBlockGap = 10f;
    private const float ProfileChipHeight = SocialChrome.MetaChipHeight;
    private const float ProfileButtonGap = 6f;
    private const float ProfileSquareButton = 38f;
    private const float ProfileBottomPad = 14f;
    private const float ProfileGridGlyphCell = 4.4f;
    private const float ProfileGridGlyphGap = 2.4f;
    private const float ProfileEmptyHeight = 250f;
    private const float ProfileCreateWidth = 132f;
    private const float PrivateLockRadius = 26f;
    private const int ProfileMenuMaxItems = 5;
    private const int ProfileTabCount = 2;

    private static readonly TextStyle OwnProfileTitleStyle = new(1.15f, FontWeight.SemiBold);
    private static readonly TextStyle ProfileNameStyle = new(1.4f, FontWeight.Bold);
    private static readonly TextStyle ProfileStatValueStyle = new(1.15f, FontWeight.Bold);
    private static readonly TextStyle ProfileStatLabelStyle = TextStyles.Subheadline;
    private static readonly TextStyle ProfileChipStyle = TextStyles.Footnote;

    private readonly ActionSheet profileMenu = new();
    private readonly ActionSheet profileActionSheet = new();
    private readonly ActionSheet.Item[] profileMenuItems = new ActionSheet.Item[ProfileMenuMaxItems];
    private readonly ProfileMenuAction[] profileMenuActions = new ProfileMenuAction[ProfileMenuMaxItems];
    private int profileMenuCount;
    private UserDto? profileSheetUser;
    private Spring profileTabSlide;

    private void DrawProfileTab(Rect area)
    {
        var scale = UiScale.Current;
        DrawOwnProfileTopBar(area);
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        if (store.Me is not { } me)
        {
            store.EnsureMe();
            Typography.DrawCentered(body.Center, Loc.T(L.Common.Loading), Ink.MutedInk);
            return;
        }

        DrawProfileBody(body, me.Id);
    }

    private void DrawOwnProfileTopBar(Rect area)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        var leading = new Vector2(area.Min.X + (CellPadX + SocialChrome.HeaderIconRadius) * scale, rowCenterY);
        if (DrawHeaderIcon(drawList, leading, PhoneIcons.SquareRoundedPlus, Loc.T(L.Aethergram.NewPost)))
        {
            StartCompose(false);
        }

        if (DrawHeaderIcon(drawList, SocialChrome.HeaderSlot(area, 0), PhoneIcons.Menu, Loc.T(L.Aethergram.More)))
        {
            OpenProfileMenu();
        }

        var me = store.Me;
        var title = me is null ? Loc.T(L.Aethergram.Profile) : me.Handle.Length > 0 ? me.Handle : me.DisplayName;
        var isPrivate = me is { IsPrivate: true };
        var lockSize = isPrivate ? 16f * scale : 0f;
        var lockGap = isPrivate ? 5f * scale : 0f;
        var reserve = (CellPadX + SocialChrome.HeaderIconRadius * 2f + 10f) * scale;
        var maxWidth = MathF.Max(1f, area.Width - reserve * 2f - lockSize - lockGap);
        var fitted = Typography.FitText(title, maxWidth, OwnProfileTitleStyle);
        var titleSize = Typography.Measure(fitted, OwnProfileTitleStyle);
        var blockWidth = lockSize + lockGap + titleSize.X;
        var blockLeft = area.Center.X - blockWidth * 0.5f;
        if (isPrivate)
        {
            PhoneIcon.Draw(drawList, new Vector2(blockLeft + lockSize * 0.5f, rowCenterY), PhoneIcons.Lock,
                Ink.TitleInk, lockSize);
        }

        Typography.Draw(drawList, new Vector2(blockLeft + lockSize + lockGap, rowCenterY - titleSize.Y * 0.5f), fitted,
            Ink.TitleInk, OwnProfileTitleStyle);
    }

    private void DrawProfile(Rect area, string userId)
    {
        if (store.ProfileUserId != userId)
        {
            store.OpenProfile(userId);
        }

        var user = store.ProfileUser;
        var title = user is null ? DisplayName : user.Handle.Length > 0 ? user.Handle : user.DisplayName;
        DrawScreenHeader(area, title, 1);
        if (user is not null)
        {
            var drawList = ImGui.GetWindowDrawList();
            var slot = SocialChrome.HeaderSlot(area, 0);
            if (user.IsMe)
            {
                if (DrawHeaderIcon(drawList, slot, PhoneIcons.Menu, Loc.T(L.Aethergram.More)))
                {
                    OpenProfileMenu();
                }
            }
            else if (DrawHeaderIcon(drawList, slot, PhoneIcons.Dots, Loc.T(L.Aethergram.More)))
            {
                OpenProfileActionSheet(user);
            }
        }

        var scale = UiScale.Current;
        var top = area.Min.Y + AppHeader.Height * scale;
        DrawProfileBody(new Rect(new Vector2(area.Min.X, top), area.Max), userId);
    }

    private void DrawProfileBody(Rect body, string userId)
    {
        if (store.ProfileUserId != userId)
        {
            store.OpenProfile(userId);
        }

        if (store.ProfileFailed)
        {
            Typography.DrawCentered(body.Center, Loc.T(L.Aethergram.ProfileError), Ink.MutedInk);
            return;
        }

        var user = store.ProfileUser;
        if (user is null)
        {
            Typography.DrawCentered(body.Center, Loc.T(L.Common.Loading), Ink.MutedInk);
            return;
        }

        if (user.IsMe)
        {
            store.EnsureFollowRequests();
        }

        using (AppSurface.BeginEdgeToEdge(body))
        {
            DrawProfileHead(user);
            if (user.IsPrivate && !user.IsFollowing && !user.IsMe)
            {
                DrawPrivateProfileNotice();
                return;
            }

            DrawProfileTabs();
            if (profileTab == 0)
            {
                DrawProfilePosts(user);
                return;
            }

            store.EnsureTaggedPosts(userId);
            DrawPostGrid(store.TaggedPosts, L.PhotoTag.NoTagged, store.HasMoreTagged, store.TaggedLoadingMore,
                store.LoadMoreTaggedPosts, SquareGrid, PostSource.Tagged);
        }
    }

    private void DrawProfileHead(UserDto user)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = CellPadX * scale;
        var innerLeft = origin.X + pad;
        var innerRight = origin.X + width - pad;
        var innerWidth = MathF.Max(1f, innerRight - innerLeft);
        var radius = ProfileAvatarRadius * scale;
        var frame = Frames.Of(user.FrameId);
        var frameReach = AvatarView.Reserve(frame, radius);
        var avatarCenter = new Vector2(innerLeft + frameReach + radius,
            origin.Y + ProfileHeadTop * scale + frameReach + radius);
        var displayName = SocialIdentity.Name(user.DisplayName, user.Handle);
        var nameHeight = Typography.LineHeight(ProfileNameStyle);
        var bioKey = new TranslationKey(TranslationSurface.Bio, user.Id);
        var bioText = translation.View(bioKey, user.Bio).Text;
        var bioHeight = bioText.Length > 0 ? Typography.MeasureWrappedBlock(bioText, TextStyles.Body, innerWidth).Y : 0f;
        var bioLinkHeight = bioHeight > 0f ? TranslateLink.Height(translation, bioKey, user.BioLang, scale) : 0f;
        var followedBy = SocialProfilePages.FollowedByLine(user);
        var followedByHeight = followedBy.Length > 0
            ? Typography.MeasureWrappedBlock(followedBy, TextStyles.Subheadline, innerWidth).Y
            : 0f;
        var regionCode = ProfileRegionCode(user);
        var hasChips = HasProfileChips(user, regionCode);
        var statsLeft = avatarCenter.X + radius + frameReach + ProfileStatsGap * scale;
        var statsHeight = Typography.LineHeight(ProfileStatValueStyle) + Typography.LineHeight(ProfileStatLabelStyle);
        var statsInline = StatsFitInline(innerRight - statsLeft, scale);
        var statsRowTop = avatarCenter.Y + radius + frameReach + ProfileBlockGap * scale;
        var nameTop = statsInline ? statsRowTop : statsRowTop + statsHeight + ProfileBlockGap * scale;
        var bioTop = nameTop + nameHeight + (bioHeight > 0f ? 4f * scale : 0f);
        var followedByTop = bioTop + bioHeight + bioLinkHeight + (followedByHeight > 0f ? 6f * scale : 0f);
        var chipsTop = followedByTop + followedByHeight + (hasChips ? ProfileBlockGap * scale : 0f);
        var buttonsTop = chipsTop + (hasChips ? ProfileChipHeight * scale : 0f) + 14f * scale;
        var buttonsBottom = buttonsTop + PillHeight * scale;
        var blockBottom = buttonsBottom + ProfileBottomPad * scale;

        DrawProfileAvatar(drawList, user, avatarCenter, radius, displayName, frame);
        if (statsInline)
        {
            DrawProfileStats(drawList, user, statsLeft, innerRight, avatarCenter.Y);
        }
        else
        {
            DrawProfileStats(drawList, user, innerLeft, innerRight, statsRowTop + statsHeight * 0.5f);
        }
        UserName.DrawAuto(drawList, "aethergram.profile.name." + user.Id, displayName, user.Badges, user.ProfileBadges,
            innerLeft, nameTop, innerWidth, ProfileNameStyle, Ink.TitleInk, theme, 2);
        if (bioHeight > 0f)
        {
            Typography.DrawWrappedLeft(new Vector2(innerLeft, bioTop), bioText, Ink.BodyInk, TextStyles.Body, innerWidth);
            if (bioLinkHeight > 0f)
            {
                TranslateLink.Draw(translation, confirm, bioKey, user.BioLang, user.Bio,
                    new Vector2(innerLeft, bioTop + bioHeight), innerWidth, Ink.MutedInk, Ink.AccentLink, scale);
            }
        }

        if (followedByHeight > 0f)
        {
            var lineTop = new Vector2(innerLeft, followedByTop);
            Typography.DrawWrappedLeft(lineTop, followedBy, Ink.MutedInk, TextStyles.Subheadline, innerWidth);
            if (UiInteract.HoverClick(lineTop, new Vector2(innerRight, followedByTop + followedByHeight)))
            {
                OpenUserList(user.Id, UserListKind.Mutuals);
            }
        }

        if (hasChips)
        {
            DrawProfileChips(drawList, user, regionCode, innerLeft, innerRight,
                chipsTop + ProfileChipHeight * scale * 0.5f);
        }

        DrawProfileButtons(user, innerLeft, innerRight, buttonsTop);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, blockBottom - origin.Y));
    }

    private void DrawProfileAvatar(ImDrawListPtr drawList, UserDto user, Vector2 center, float radius,
        string displayName, FrameStyle? frame)
    {
        var scale = UiScale.Current;
        var hasRing = stories.TryRing(user.Id, out var ring);
        if (hasRing)
        {
            AethergramArt.StoryRing(drawList, center, radius + ProfileRingGap * scale, scale, ring.HasUnseen);
        }

        DrawAvatar(center, radius, user.IsMe ? user.Name : displayName, user.IsMe ? user.World : string.Empty,
            user.AvatarUrl, 1.5f, 64, frame);
        if (hasRing)
        {
            if (UiInteract.HoverClickCircle(center, radius))
            {
                stories.OpenRing(ring);
            }
        }
        else
        {
            avatarLightbox.TryOpen(center, radius, user.AvatarUrl, images);
        }

        if (!user.IsMe)
        {
            return;
        }

        var badgeRadius = ProfileStoryBadgeRadius * scale;
        var badgeCenter = center + new Vector2(radius - badgeRadius + 2f * scale, radius - badgeRadius + 2f * scale);
        var badgeHovered = UiInteract.HoverClickCircle(badgeCenter, badgeRadius);
        drawList.AddCircleFilled(badgeCenter, badgeRadius + 2f * scale, ImGui.GetColorU32(Ink.BackdropTop), 32);
        drawList.AddCircleFilled(badgeCenter, badgeRadius, ImGui.GetColorU32(Ink.Accent), 32);
        PhoneIcon.Draw(drawList, badgeCenter, PhoneIcons.Plus, Ink.White, 13f * scale);
        HoverTooltip.Show(new Rect(badgeCenter - new Vector2(badgeRadius, badgeRadius),
            badgeCenter + new Vector2(badgeRadius, badgeRadius)), Loc.T(L.Story.NewStory), HoverLabelSide.Below);
        if (badgeHovered)
        {
            StartStoryCompose();
        }
    }

    private static bool StatsFitInline(float available, float scale)
    {
        var widest = MathF.Max(Typography.Measure(Loc.T(L.Aethergram.StatPosts), ProfileStatLabelStyle).X,
            MathF.Max(Typography.Measure(Loc.T(L.Aethergram.StatFollowers), ProfileStatLabelStyle).X,
                Typography.Measure(Loc.T(L.Aethergram.StatFollowing), ProfileStatLabelStyle).X));
        return (widest + ProfileStatColumnPad * scale) * 3f <= available;
    }

    private void DrawProfileStats(ImDrawListPtr drawList, UserDto user, float left, float right, float centerY)
    {
        var scale = UiScale.Current;
        var column = MathF.Max(1f, (right - left) / 3f);
        var valueHeight = Typography.LineHeight(ProfileStatValueStyle);
        var labelHeight = Typography.LineHeight(ProfileStatLabelStyle);
        var top = centerY - (valueHeight + labelHeight) * 0.5f;
        var listsOpen = user.IsMe || user.IsFollowing || !user.IsPrivate;
        DrawProfileStat(drawList, left, top, column, valueHeight, user.Grams, Loc.T(L.Aethergram.StatPosts), false);
        if (DrawProfileStat(drawList, left + column, top, column, valueHeight, user.Followers,
                Loc.T(L.Aethergram.StatFollowers), listsOpen))
        {
            OpenUserList(user.Id, UserListKind.Followers);
        }

        if (DrawProfileStat(drawList, left + column * 2f, top, column, valueHeight, user.Following,
                Loc.T(L.Aethergram.StatFollowing), listsOpen))
        {
            OpenUserList(user.Id, UserListKind.Following);
        }
    }

    private static bool DrawProfileStat(ImDrawListPtr drawList, float left, float top, float width, float valueHeight,
        int count, string label, bool tappable)
    {
        var scale = UiScale.Current;
        var maxWidth = MathF.Max(1f, width - 4f * scale);
        var value = CountText.Compact(count);
        var fittedLabel = Typography.FitText(label, maxWidth, ProfileStatLabelStyle);
        var labelHeight = Typography.LineHeight(ProfileStatLabelStyle);
        var min = new Vector2(left, top);
        var max = new Vector2(left + maxWidth, top + valueHeight + labelHeight);
        var hovered = tappable && UiInteract.Hover(min, max);
        Typography.Draw(drawList, min, value, Ink.TitleInk, ProfileStatValueStyle);
        Typography.Draw(drawList, new Vector2(left, top + valueHeight), fittedLabel,
            hovered ? Ink.BodyInk : Ink.MutedInk, ProfileStatLabelStyle);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return tappable && UiInteract.Click(min, max, hovered);
    }

    private string ProfileRegionCode(UserDto user) => user.IsMe
        ? SocialRegion.EffectiveCode(configuration, gameData)
        : SocialRegion.Resolve(user.Region, user.World, gameData);

    private static bool HasProfileChips(UserDto user, string regionCode) =>
        regionCode.Length > 0 || user.UtcOffsetMinutes is not null || (!user.IsMe && user.FollowsYou);

    private void DrawProfileChips(ImDrawListPtr drawList, UserDto user, string regionCode, float left, float right,
        float centerY)
    {
        var cursorX = left;
        if (regionCode.Length > 0)
        {
            DrawProfileChip(drawList, ref cursorX, right, centerY, PhoneIcons.World, regionCode);
        }

        if (user.UtcOffsetMinutes is { } offsetMinutes)
        {
            DrawProfileChip(drawList, ref cursorX, right, centerY, PhoneIcons.Clock,
                SocialTimeZone.ClockLabel(offsetMinutes));
        }

        if (!user.IsMe && user.FollowsYou)
        {
            DrawProfileChip(drawList, ref cursorX, right, centerY, string.Empty, Loc.T(L.Social.FollowsYou));
        }
    }

    private static void DrawProfileChip(ImDrawListPtr drawList, ref float cursorX, float right, float centerY,
        string glyph, string label) =>
        SocialChrome.DrawMetaChip(drawList, ref cursorX, right, centerY, glyph, label, Ink, ProfileChipStyle);

    private void DrawProfileButtons(UserDto user, float left, float right, float top)
    {
        var scale = UiScale.Current;
        var gap = ProfileButtonGap * scale;
        var square = ProfileSquareButton * scale;
        var bottom = top + PillHeight * scale;
        var showSquare = user.IsMe || user.FollowedByCount > 0;
        var squareRect = new Rect(new Vector2(right - square, top), new Vector2(right, bottom));
        var pillsRight = showSquare ? squareRect.Min.X - gap : right;
        if (user.IsMe)
        {
            var half = (pillsRight - left - gap) * 0.5f;
            if (DrawGrayPill(new Rect(new Vector2(left, top), new Vector2(left + half, bottom)),
                    Loc.T(L.Aethergram.EditProfile)))
            {
                editLoadedFor = null;
                router.Push(AethergramRoute.EditProfile);
            }

            if (DrawGrayPill(new Rect(new Vector2(left + half + gap, top), new Vector2(pillsRight, bottom)),
                    Loc.T(L.Aethergram.SavedTitle)))
            {
                OpenSaved();
            }

            if (DrawGrayIconButton(squareRect, PhoneIcons.UserPlus, Loc.T(L.Social.FollowRequests)))
            {
                OpenFollowRequests();
            }

            SocialChrome.DrawCountBadge(ImGui.GetWindowDrawList(),
                new Vector2(squareRect.Max.X - 4f * scale, squareRect.Min.Y + 2f * scale),
                store.PendingFollowRequestCount, Ink);
            return;
        }

        var showMessage = user.CanMessage;
        var followRight = showMessage ? left + (pillsRight - left - gap) * 0.5f : pillsRight;
        DrawFollowPill(new Rect(new Vector2(left, top), new Vector2(followRight, bottom)), user);
        if (showMessage && DrawGrayPill(new Rect(new Vector2(followRight + gap, top), new Vector2(pillsRight, bottom)),
                Loc.T(L.Aethergram.MessageButton)))
        {
            OpenThread(user.Id);
        }

        if (showSquare && DrawGrayIconButton(squareRect, PhoneIcons.UserPlus, Loc.T(L.Social.MutualsTitle)))
        {
            OpenUserList(user.Id, UserListKind.Mutuals);
        }
    }

    private void DrawProfileTabs()
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var row = new Rect(origin, new Vector2(origin.X + width, origin.Y + IconTabHeight * scale));
        var slot = row.Width / ProfileTabCount;
        for (var index = 0; index < ProfileTabCount; index++)
        {
            var cellMin = new Vector2(row.Min.X + slot * index, row.Min.Y);
            var cellMax = new Vector2(cellMin.X + slot, row.Max.Y);
            var hovered = UiInteract.Hover(cellMin, cellMax);
            var glyphInk = index == profileTab ? Ink.TitleInk : hovered ? Ink.BodyInk : Ink.MutedInk;
            var center = new Vector2((cellMin.X + cellMax.X) * 0.5f, row.Center.Y);
            if (index == 0)
            {
                DrawGridGlyph(drawList, center, glyphInk);
            }
            else
            {
                PhoneIcon.Draw(drawList, center, PhoneIcons.UserSquareRounded, glyphInk, IconTabIconSize * scale);
            }

            HoverTooltip.Show(new Rect(cellMin, cellMax),
                Loc.T(index == 0 ? L.PhotoTag.PostsTab : L.PhotoTag.TaggedTab), HoverLabelSide.Below);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(cellMin, cellMax, hovered))
            {
                profileTab = index;
            }
        }

        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        profileTabSlide.Step(profileTab, TabSmoothTime, delta);
        var underlineLeft = row.Min.X + profileTabSlide.Value * slot;
        drawList.AddRectFilled(new Vector2(underlineLeft, row.Max.Y - IconTabUnderline * scale),
            new Vector2(underlineLeft + slot, row.Max.Y), ImGui.GetColorU32(Ink.TitleInk));
        DrawHairline(drawList, row.Min.X, row.Max.X, row.Max.Y);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, row.Height + GridGap * scale));
    }

    private static void DrawGridGlyph(ImDrawListPtr drawList, Vector2 center, Vector4 ink)
    {
        var scale = UiScale.Current;
        var cell = ProfileGridGlyphCell * scale;
        var step = cell + ProfileGridGlyphGap * scale;
        var start = center - new Vector2(step, step) - new Vector2(cell * 0.5f, cell * 0.5f);
        var color = ImGui.GetColorU32(ink);
        for (var row = 0; row < 3; row++)
        {
            for (var column = 0; column < 3; column++)
            {
                var min = start + new Vector2(column * step, row * step);
                drawList.AddRectFilled(min, min + new Vector2(cell, cell), color, 1f * scale);
            }
        }
    }

    private void DrawProfilePosts(UserDto user)
    {
        var posts = store.ProfilePosts;
        if (posts.Length > 0 || !user.IsMe || store.ProfileLoading)
        {
            DrawPostGrid(posts, L.Aethergram.Empty, store.HasMoreProfilePosts, store.ProfileLoadingMore,
                store.LoadMoreProfilePosts, SquareGrid, PostSource.Profile);
            return;
        }

        var scale = UiScale.Current;
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var area = new Rect(origin, new Vector2(origin.X + width, origin.Y + ProfileEmptyHeight * scale));
        DrawEmptyState(area, Loc.T(L.Aethergram.CreateFirstPost), Loc.T(L.Aethergram.CreateFirstPostHint));
        var pillWidth = ProfileCreateWidth * scale;
        var pillTop = area.Max.Y - PillHeight * scale - 24f * scale;
        var pillRect = new Rect(new Vector2(area.Center.X - pillWidth * 0.5f, pillTop),
            new Vector2(area.Center.X + pillWidth * 0.5f, pillTop + PillHeight * scale));
        if (DrawAccentPill(pillRect, Loc.T(L.Aethergram.Create)))
        {
            StartCompose(false);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, area.Height));
    }

    private void DrawPrivateProfileNotice()
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var centerX = origin.X + width * 0.5f;
        var lockCenter = new Vector2(centerX, origin.Y + 52f * scale);
        drawList.AddCircle(lockCenter, PrivateLockRadius * scale,
            ImGui.GetColorU32(Palette.WithAlpha(Ink.MutedInk, 0.5f)), 48, 1.6f * scale);
        PhoneIcon.Draw(drawList, lockCenter, PhoneIcons.Lock, Ink.TitleInk, 24f * scale);
        var maxWidth = MathF.Max(1f, width - 48f * scale);
        var titleTop = lockCenter.Y + 40f * scale;
        var titleHeight = Typography.DrawWrappedCentered(drawList, Loc.T(L.Aethergram.PrivateTitle),
            TextStyles.BodyEmphasized, Ink.TitleInk, new Vector2(centerX, titleTop), maxWidth);
        var subtitleTop = titleTop + titleHeight + 6f * scale;
        var subtitleHeight = Typography.DrawWrappedCentered(drawList, Loc.T(L.Aethergram.PrivateSubtitle),
            TextStyles.Subheadline, Ink.MutedInk, new Vector2(centerX, subtitleTop), maxWidth);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, subtitleTop + subtitleHeight + 24f * scale - origin.Y));
    }

    private void OpenProfileMenu()
    {
        profileMenuCount = 0;
        AddProfileMenuItem(Loc.T(L.Aethergram.Settings), ProfileMenuAction.Settings);
        AddProfileMenuItem(Loc.T(L.Aethergram.SavedTitle), ProfileMenuAction.Saved);
        var pending = store.PendingFollowRequestCount;
        AddProfileMenuItem(pending > 0 ? Loc.T(L.Social.FollowRequestsCount, pending) : Loc.T(L.Social.FollowRequests),
            ProfileMenuAction.FollowRequests);
        AddProfileMenuItem(Loc.T(L.Encryption.Title), ProfileMenuAction.Encryption);
        AddProfileMenuItem(Loc.T(L.Conduct.Eyebrow), ProfileMenuAction.Rules);
        profileMenu.Open();
    }

    private void AddProfileMenuItem(string label, ProfileMenuAction action, bool danger = false)
    {
        profileMenuItems[profileMenuCount] = new ActionSheet.Item(label, Danger: danger);
        profileMenuActions[profileMenuCount] = action;
        profileMenuCount++;
    }

    private void DrawProfileMenu(Rect screen)
    {
        if (!profileMenu.CapturesPointer)
        {
            return;
        }

        var picked = profileMenu.Draw(screen, ActionSheetStyle.From(ui), profileMenuItems.AsSpan(0, profileMenuCount),
            Loc.T(L.Common.Cancel), false);
        if (picked < 0)
        {
            return;
        }

        switch (profileMenuActions[picked])
        {
            case ProfileMenuAction.Settings:
                router.Push(AethergramRoute.Settings);
                break;
            case ProfileMenuAction.Saved:
                OpenSaved();
                break;
            case ProfileMenuAction.FollowRequests:
                OpenFollowRequests();
                break;
            case ProfileMenuAction.Encryption:
                router.Push(AethergramRoute.Encryption);
                break;
            case ProfileMenuAction.Rules:
                conduct.ShowRules(Id);
                break;
        }
    }

    private void OpenProfileActionSheet(UserDto user)
    {
        profileSheetUser = user;
        profileMenuCount = 0;
        AddProfileMenuItem(Loc.T(L.Report.Action), ProfileMenuAction.Report);
        AddProfileMenuItem(Loc.T(L.Social.BlockAction), ProfileMenuAction.Block, true);
        profileActionSheet.Open();
    }

    private void DrawProfileActionSheet(Rect screen)
    {
        if (!profileActionSheet.CapturesPointer)
        {
            return;
        }

        var picked = profileActionSheet.Draw(screen, ActionSheetStyle.From(ui),
            profileMenuItems.AsSpan(0, profileMenuCount), Loc.T(L.Common.Cancel), false);
        if (picked < 0 || profileSheetUser is not { } user)
        {
            return;
        }

        switch (profileMenuActions[picked])
        {
            case ProfileMenuAction.Report:
                profile.OpenReport("user", user.Id, Loc.T(L.Report.UserTitle));
                break;
            case ProfileMenuAction.Block:
                profile.AskBlock(user.DisplayName, user.Handle, user.Id);
                break;
        }
    }
}
