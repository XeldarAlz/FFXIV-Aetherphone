using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Translation;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Chirper;

internal sealed partial class ChirperApp
{
    private enum ProfileTab
    {
        Chirps,
        Media,
        Likes,
    }

    private const float ProfileBannerHeight = 120f;
    private const float ProfileAvatarRadius = 40f;
    private const float ProfileAvatarRing = 3f;
    private const float ProfileTabHeight = 44f;
    private const float ProfileTabUnderline = 3f;
    private const float ProfileActionHeight = 36f;
    private const float ProfileTabSmoothTime = 0.08f;
    private const int MediaGridColumns = 3;
    private const float MediaGridCellGap = 2f;

    private static readonly TextStyle ProfileNameStyle = new(1.4f, FontWeight.Bold);
    private static readonly TextStyle ProfileHandleStyle = new(0.93f, FontWeight.Regular);
    private static readonly TextStyle ProfileBioStyle = new(1f, FontWeight.Regular);
    private static readonly TextStyle ProfileMetaStyle = new(0.9f, FontWeight.Regular);
    private static readonly TextStyle StatValueStyle = new(1f, FontWeight.Bold);
    private static readonly TextStyle StatLabelStyle = new(0.93f, FontWeight.Regular);
    private static readonly TextStyle FollowedByStyle = new(0.83f, FontWeight.Regular);
    private static readonly TextStyle TabStyle = new(0.97f, FontWeight.SemiBold);
    private static readonly TextStyle TabIdleStyle = new(0.97f, FontWeight.Medium);
    private static readonly TextStyle FollowPillStyle = new(0.97f, FontWeight.Bold);
    private static readonly Vector4 GlassPillFill = new(1f, 1f, 1f, 0.08f);
    private static readonly Vector4 OutlinePillFill = new(1f, 1f, 1f, 0.04f);
    private static readonly Vector4 BannerScrim = new(0f, 0f, 0f, 0.4f);
    private static readonly Vector4 BannerVeil = new(0.031f, 0.067f, 0.122f, 0.55f);

    private ProfileTab profileTab;
    private Spring profileTabSlide;

    private void DrawProfile(Rect area, string userId, bool root = false)
    {
        if (store.ProfileUserId != userId)
        {
            store.OpenProfile(userId);
            profileTab = ProfileTab.Chirps;
        }

        var user = store.ProfileUser;
        var scale = UiScale.Current;
        if (store.ProfileFailed)
        {
            DrawScreenHeader(area, Loc.T(L.Apps.Chirper), 0f, string.Empty, !root);
            Typography.DrawCentered(area.Center, Loc.T(L.Chirper.ProfileError), ChirperInk.MutedInk);
            return;
        }

        if (user is null)
        {
            DrawScreenHeader(area, Loc.T(L.Apps.Chirper), 0f, string.Empty, !root);
            Typography.DrawCentered(area.Center, Loc.T(L.Common.Loading), ChirperInk.MutedInk);
            return;
        }

        using (AppSurface.BeginEdgeToEdge(area))
        {
            DrawProfileBanner(user, root);
            DrawProfileIdentity(user);
            DrawProfileTabs(user.IsMe);
            switch (profileTab)
            {
                case ProfileTab.Media:
                    DrawProfileMediaGrid(store.ProfilePosts, area);
                    break;
                case ProfileTab.Likes:
                    DrawLikedPosts(area);
                    break;
                default:
                    DrawProfilePosts(store.ProfilePosts, area);
                    break;
            }

            if (profileTab != ProfileTab.Likes && store.ProfileLoadingMore)
            {
                InfiniteScroll.DrawLoadingRow(area.Center.X, ChirperInk.MutedInk);
            }

            ImGui.Dummy(new Vector2(0f, 24f * scale));
            if (profileTab != ProfileTab.Likes && InfiniteScroll.ReachedBottom() && store.HasMoreProfilePosts
                && !store.ProfileLoadingMore)
            {
                store.LoadMoreProfilePosts();
            }
        }
    }

    private void DrawProfileBanner(UserDto user, bool root)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = ProfileBannerHeight * scale;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        var texture = MediaTexture(user.BannerUrl) ?? MediaTexture(user.AvatarUrl);
        if (texture is null)
        {
            Squircle.FillVerticalGradient(drawList, min, max, 0f,
                ImGui.GetColorU32(Palette.Mix(ChirperInk.AccentDeep, ChirperInk.BackdropTop, 0.35f)),
                ImGui.GetColorU32(ChirperInk.BackdropTop));
        }
        else
        {
            var (uv0, uv1) = ImageFit.Cover(texture.Size.X, texture.Size.Y, width, height);
            drawList.AddImage(texture.Handle, min, max, uv0, uv1, 0xFFFFFFFFu);
            if (user.BannerUrl is null)
            {
                drawList.AddRectFilled(min, max, ImGui.GetColorU32(BannerVeil));
            }
        }

        drawList.AddRectFilledMultiColor(min, new Vector2(max.X, min.Y + 44f * scale), ImGui.GetColorU32(BannerScrim),
            ImGui.GetColorU32(BannerScrim), 0u, 0u);
        drawList.AddRectFilledMultiColor(new Vector2(min.X, max.Y - 56f * scale), max, 0u, 0u,
            ImGui.GetColorU32(ChirperInk.BackdropTop), ImGui.GetColorU32(ChirperInk.BackdropTop));
        if (user.BannerUrl is { Length: > 0 } bannerUrl && UiInteract.HoverClick(min, max))
        {
            photoViewer.Open(this, () => MediaTexture(bannerUrl));
        }

        if (user.IsMe)
        {
            var badgeRadius = 15f * scale;
            var badgeCenter = new Vector2(max.X - 14f * scale - badgeRadius, min.Y + 10f * scale + badgeRadius);
            var badgeExtent = new Vector2(badgeRadius + 6f * scale, badgeRadius + 6f * scale);
            var badgeHovered = UiInteract.Hover(badgeCenter - badgeExtent, badgeCenter + badgeExtent);
            drawList.AddCircleFilled(badgeCenter, badgeRadius,
                ImGui.GetColorU32(badgeHovered ? new Vector4(0f, 0f, 0f, 0.65f) : new Vector4(0f, 0f, 0f, 0.45f)), 32);
            PhoneIcon.Draw(drawList, badgeCenter, PhoneIcons.Camera, ChirperInk.White, 15f * scale);
            if (badgeHovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            HoverTooltip.Show(new Rect(badgeCenter - badgeExtent, badgeCenter + badgeExtent),
                Loc.T(L.Chirper.ChangeBanner), HoverLabelSide.Below);
            if (UiInteract.Click(badgeCenter - badgeExtent, badgeCenter + badgeExtent, badgeHovered))
            {
                OpenBannerComposer();
            }
        }

        if (!root)
        {
            var chipRadius = BackChipRadius * scale;
            var chipCenter = new Vector2(min.X + 12f * scale + chipRadius, min.Y + 10f * scale + chipRadius);
            var hitHalf = 22f * scale;
            var hitMin = chipCenter - new Vector2(hitHalf, hitHalf);
            var hitMax = chipCenter + new Vector2(hitHalf, hitHalf);
            var hovered = UiInteract.Hover(hitMin, hitMax);
            drawList.AddCircleFilled(chipCenter, chipRadius,
                ImGui.GetColorU32(hovered ? new Vector4(0f, 0f, 0f, 0.6f) : new Vector4(0f, 0f, 0f, 0.4f)), 32);
            PhoneIcon.Draw(drawList, chipCenter, PhoneIcons.ChevronLeft,
                ChirperInk.White, 17f * scale);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(hitMin, hitMax, hovered))
            {
                back();
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawProfileIdentity(UserDto user)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var padX = CellPadX * scale;
        var innerLeft = origin.X + padX;
        var innerRight = origin.X + width - padX;
        var innerWidth = MathF.Max(1f, innerRight - innerLeft);
        var avatarRadius = ProfileAvatarRadius * scale;
        var avatarCenter = new Vector2(innerLeft + avatarRadius, origin.Y);
        var displayName = SocialIdentity.Name(user.DisplayName, user.Handle);
        var portraitName = user.IsMe ? user.Name : displayName;
        var portraitWorld = user.IsMe ? user.World : string.Empty;
        drawList.AddCircleFilled(avatarCenter, avatarRadius + ProfileAvatarRing * scale,
            ImGui.GetColorU32(ChirperInk.BackdropTop), 64);
        var frame = Frames.Of(user.FrameId);
        DrawAvatar(drawList, avatarCenter, avatarRadius, portraitName, portraitWorld, user.AvatarUrl, 1.5f, 64, frame);
        if (frame is null)
        {
            drawList.AddCircle(avatarCenter, avatarRadius + 1f * scale, ImGui.GetColorU32(ChirperInk.AccentLink), 64,
                2f * scale);
        }

        avatarLightbox.TryOpen(avatarCenter, avatarRadius, user.AvatarUrl, images);

        var actionTop = origin.Y + 10f * scale;
        DrawProfileActions(drawList, user, innerLeft, innerRight, actionTop);

        var cursorY = MathF.Max(avatarCenter.Y + avatarRadius, actionTop + ProfileActionHeight * scale) + 10f * scale;
        var nameHeight = Typography.LineHeight(ProfileNameStyle);
        UserName.DrawAuto(drawList, "chirper.profile.name." + user.Id, displayName, user.Badges, user.ProfileBadges,
            innerLeft, cursorY, innerWidth, ProfileNameStyle, ChirperInk.TitleInk, theme, 2);
        cursorY += nameHeight + 2f * scale;
        if (user.Handle.Length > 0)
        {
            var handleHeight = Typography.LineHeight(ProfileHandleStyle);
            var handleText = Typography.FitText("@" + user.Handle, innerWidth, ProfileHandleStyle);
            Typography.Draw(drawList, new Vector2(innerLeft, cursorY), handleText, ChirperInk.MutedInk, ProfileHandleStyle);
            cursorY += handleHeight;
        }

        var bioKey = new TranslationKey(TranslationSurface.Bio, user.Id);
        var bioText = translation.View(bioKey, user.Bio).Text;
        if (bioText.Length > 0)
        {
            cursorY += 8f * scale;
            var bioHeight = Typography.MeasureWrappedBlock(bioText, ProfileBioStyle, innerWidth).Y;
            Typography.DrawWrappedLeft(new Vector2(innerLeft, cursorY), bioText, ChirperInk.BodyInk, ProfileBioStyle,
                innerWidth);
            cursorY += bioHeight;
            var bioLinkHeight = TranslateLink.Height(translation, bioKey, user.BioLang, scale);
            if (bioLinkHeight > 0f)
            {
                TranslateLink.Draw(translation, confirm, bioKey, user.BioLang, user.Bio, new Vector2(innerLeft, cursorY),
                    innerWidth, ChirperInk.MutedInk, ChirperInk.AccentLink, scale);
                cursorY += bioLinkHeight;
            }
        }

        cursorY += 8f * scale;
        cursorY = DrawProfileMetaRow(drawList, user, innerLeft, innerRight, cursorY) + 10f * scale;
        cursorY = DrawProfileStats(drawList, user, innerLeft, innerRight, cursorY) + 8f * scale;
        var followedBy = SocialProfilePages.FollowedByLine(user);
        if (followedBy.Length > 0)
        {
            var followedByHeight = Typography.MeasureWrappedBlock(followedBy, FollowedByStyle, innerWidth).Y;
            var lineTop = new Vector2(innerLeft, cursorY);
            Typography.DrawWrappedLeft(lineTop, followedBy, ChirperInk.FaintInk, FollowedByStyle, innerWidth);
            if (UiInteract.HoverClick(lineTop, new Vector2(innerRight, cursorY + followedByHeight)))
            {
                OpenUserList(user.Id, UserListKind.Mutuals);
            }

            cursorY += followedByHeight + 8f * scale;
        }

        cursorY += 6f * scale;
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cursorY - origin.Y));
    }

    private float DrawProfileMetaRow(ImDrawListPtr drawList, UserDto user, float left, float right, float top)
    {
        var scale = UiScale.Current;
        var height = SocialChrome.MetaChipHeight * scale;
        var centerY = top + height * 0.5f;
        var cursorX = left;
        var regionCode = user.IsMe
            ? SocialRegion.EffectiveCode(configuration, gameData)
            : SocialRegion.Resolve(user.Region, user.World, gameData);
        if (regionCode.Length > 0)
        {
            SocialChrome.DrawMetaChip(drawList, ref cursorX, right, centerY, PhoneIcons.Pin, regionCode,
                ChirperInk.Shared, ProfileMetaStyle);
        }

        if (user.UtcOffsetMinutes is { } offsetMinutes)
        {
            SocialChrome.DrawMetaChip(drawList, ref cursorX, right, centerY, PhoneIcons.Clock,
                SocialTimeZone.ClockLabel(offsetMinutes), ChirperInk.Shared, ProfileMetaStyle);
        }

        if (!user.IsMe && user.FollowsYou)
        {
            SocialChrome.DrawMetaChip(drawList, ref cursorX, right, centerY, string.Empty, Loc.T(L.Social.FollowsYou),
                ChirperInk.Shared, ProfileMetaStyle);
        }

        return top + height;
    }

    private float DrawProfileStats(ImDrawListPtr drawList, UserDto user, float left, float right, float top)
    {
        var scale = UiScale.Current;
        var listsOpen = user.IsMe || user.IsFollowing || !user.IsPrivate;
        var lineHeight = Typography.LineHeight(StatValueStyle);
        var cursorX = left;
        cursorX = DrawStat(drawList, cursorX, top, lineHeight, user.Following.ToString(Loc.Culture),
            Loc.T(L.Chirper.Following), listsOpen, right, out var followingClicked);
        cursorX += 18f * scale;
        DrawStat(drawList, cursorX, top, lineHeight, user.Followers.ToString(Loc.Culture),
            SocialProfilePages.FollowersLabel(user.Followers), listsOpen, right, out var followersClicked);
        if (followingClicked)
        {
            OpenUserList(user.Id, UserListKind.Following);
        }

        if (followersClicked)
        {
            OpenUserList(user.Id, UserListKind.Followers);
        }

        return top + lineHeight;
    }

    private static float DrawStat(ImDrawListPtr drawList, float left, float top, float lineHeight, string value,
        string label, bool tappable, float limit, out bool clicked) =>
        SocialChrome.DrawStat(drawList, left, top, lineHeight, value, label, tappable, limit, ChirperInk.Shared,
            StatValueStyle, StatLabelStyle, out clicked);

    private void DrawProfileActions(ImDrawListPtr drawList, UserDto user, float left, float right, float top)
    {
        var scale = UiScale.Current;
        var height = ProfileActionHeight * scale;
        var rounding = height * 0.5f;
        var moreRadius = height * 0.5f;
        var moreCenter = new Vector2(right - moreRadius, top + moreRadius);
        var moreExtent = new Vector2(moreRadius, moreRadius);
        var moreHovered = UiInteract.Hover(moreCenter - moreExtent, moreCenter + moreExtent);
        drawList.AddCircleFilled(moreCenter, moreRadius,
            ImGui.GetColorU32(moreHovered ? ChirperInk.ChipHover : GlassPillFill), 32);
        drawList.AddCircle(moreCenter, moreRadius, ImGui.GetColorU32(ChirperInk.ChipStroke), 32, 1f);
        PhoneIcon.Draw(drawList, moreCenter, PhoneIcons.Dots, GlassPillInk, 16f * scale);
        if (moreHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        HoverTooltip.Show(new Rect(moreCenter - moreExtent, moreCenter + moreExtent), Loc.T(L.Chirper.More),
            HoverLabelSide.Above);
        if (UiInteract.Click(moreCenter - moreExtent, moreCenter + moreExtent, moreHovered))
        {
            OpenProfileSheet(user);
        }

        var pillRight = moreCenter.X - moreRadius - 8f * scale;
        if (user.IsMe)
        {
            var label = Loc.T(L.Chirper.EditProfile);
            var pillWidth = Typography.Measure(label, FollowPillStyle).X + 34f * scale;
            var editRect = new Rect(new Vector2(pillRight - pillWidth, top), new Vector2(pillRight, top + height));
            if (DrawOutlinePill(drawList, editRect, label, rounding))
            {
                editLoadedFor = null;
                router.Push(ChirperRoute.EditProfile);
            }

            return;
        }

        var state = SocialFeedStore.FollowStateOf(user);
        var followLabel = state switch
        {
            FollowState.Following => Loc.T(L.Chirper.Following),
            FollowState.Requested => Loc.T(L.Social.Requested),
            _ => Loc.T(L.Chirper.Follow),
        };
        var followWidth = MathF.Max(110f * scale, Typography.Measure(followLabel, FollowPillStyle).X + 34f * scale);
        var pillRect = new Rect(new Vector2(pillRight - followWidth, top), new Vector2(pillRight, top + height));
        var clicked = state == FollowState.None
            ? DrawGradientPill(drawList, pillRect, followLabel, rounding)
            : DrawOutlinePill(drawList, pillRect, followLabel, rounding);
        if (clicked)
        {
            store.ToggleFollow(user);
        }
    }

    private static bool DrawGradientPill(ImDrawListPtr drawList, Rect rect, string label, float rounding) =>
        SocialPill.Accent(drawList, rect, label, ChirperInk.Shared, FollowPillStyle, rounding);

    private static bool DrawOutlinePill(ImDrawListPtr drawList, Rect rect, string label, float rounding) =>
        SocialPill.Outline(drawList, rect, label, ChirperInk.Shared, FollowPillStyle, rounding, OutlinePillFill);

    private void DrawProfileTabs(bool showLikes)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = ProfileTabHeight * scale;
        var tabCount = showLikes ? 3 : 2;
        var tabWidth = width / tabCount;
        if (!showLikes && profileTab == ProfileTab.Likes)
        {
            profileTab = ProfileTab.Chirps;
        }

        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        profileTabSlide.Step((int)profileTab, ProfileTabSmoothTime, delta);
        var centerY = origin.Y + height * 0.5f;
        var activeLabel = profileTab switch
        {
            ProfileTab.Media => Loc.T(L.Chirper.MediaTab),
            ProfileTab.Likes => Loc.T(L.Chirper.LikesTab),
            _ => Loc.T(L.Chirper.ChirpsTitle),
        };
        DrawProfileTab(drawList, origin.X, tabWidth, centerY, height, Loc.T(L.Chirper.ChirpsTitle), ProfileTab.Chirps);
        DrawProfileTab(drawList, origin.X + tabWidth, tabWidth, centerY, height, Loc.T(L.Chirper.MediaTab),
            ProfileTab.Media);
        if (showLikes)
        {
            DrawProfileTab(drawList, origin.X + tabWidth * 2f, tabWidth, centerY, height, Loc.T(L.Chirper.LikesTab),
                ProfileTab.Likes);
        }

        var underlineWidth = Typography.Measure(activeLabel, TabStyle).X + 24f * scale;
        var underlineCenterX = origin.X + tabWidth * (0.5f + profileTabSlide.Value);
        var underlineTop = origin.Y + height - ProfileTabUnderline * scale;
        Squircle.Fill(drawList, new Vector2(underlineCenterX - underlineWidth * 0.5f, underlineTop),
            new Vector2(underlineCenterX + underlineWidth * 0.5f, origin.Y + height), 1.5f * scale,
            ImGui.GetColorU32(ChirperInk.Accent));
        DrawHairline(drawList, origin.X, origin.X + width, origin.Y + height);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawProfileTab(ImDrawListPtr drawList, float left, float width, float centerY, float height,
        string label, ProfileTab tab)
    {
        var active = profileTab == tab;
        var style = active ? TabStyle : TabIdleStyle;
        var min = new Vector2(left, centerY - height * 0.5f);
        var max = new Vector2(left + width, centerY + height * 0.5f);
        var hovered = UiInteract.Hover(min, max);
        var ink = active ? ChirperInk.AccentLink : hovered ? ChirperInk.BodyInk : ChirperInk.MutedInk;
        var fitted = Typography.FitText(label, MathF.Max(1f, width - 12f * UiScale.Current), style);
        Typography.DrawCentered(drawList, new Vector2(left + width * 0.5f, centerY - 1.5f * UiScale.Current), fitted,
            ink, style);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(min, max, hovered))
        {
            SelectProfileTab(tab);
        }
    }

    private void SelectProfileTab(ProfileTab tab)
    {
        profileTab = tab;
        if (tab == ProfileTab.Likes && likedRevalidateGate.TryPass())
        {
            store.RefreshLiked();
        }
    }

    private void DrawProfilePosts(PostDto[] posts, Rect body)
    {
        var scale = UiScale.Current;
        if (posts.Length == 0)
        {
            Typography.DrawCentered(new Vector2(body.Center.X, ImGui.GetCursorScreenPos().Y + 40f * scale),
                Loc.T(L.Chirper.Empty), ChirperInk.MutedInk);
            return;
        }

        profileVirtualizer.BeginFrame();
        renderedUnderlyingIds.Clear();
        for (var index = 0; index < posts.Length; index++)
        {
            var post = posts[index];
            if (HiddenByMediaPreference(post))
            {
                continue;
            }

            if (!renderedUnderlyingIds.Add(post.RepostOfId ?? post.Id))
            {
                continue;
            }

            if (profileVirtualizer.Skip(post.Id))
            {
                continue;
            }

            DrawPost(post);
            profileVirtualizer.Record(post.Id);
        }
    }

    private void DrawLikedPosts(Rect body)
    {
        var scale = UiScale.Current;
        var posts = store.LikedPosts;
        if (posts.Length == 0)
        {
            Typography.DrawWrappedCentered(new Vector2(body.Center.X, ImGui.GetCursorScreenPos().Y + 40f * scale),
                store.LikedLoading ? Loc.T(L.Common.Loading) : Loc.T(L.Chirper.LikesEmpty), ChirperInk.MutedInk,
                MetaStyle, body.Width - 64f * scale);
            return;
        }

        DrawProfilePosts(posts, body);
        if (store.LikedLoadingMore)
        {
            InfiniteScroll.DrawLoadingRow(body.Center.X, ChirperInk.MutedInk);
        }
        else if (store.HasMoreLiked && InfiniteScroll.ReachedBottom())
        {
            store.LoadMoreLiked();
        }
    }

    private void DrawProfileMediaGrid(PostDto[] posts, Rect body)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var gap = MediaGridCellGap * scale;
        var cell = (width - gap * (MediaGridColumns - 1)) / MediaGridColumns;
        var viewTop = ImGui.GetWindowPos().Y;
        var viewHeight = ImGui.GetWindowSize().Y;
        var cullMargin = cell + 60f * scale;
        var cellIndex = 0;
        for (var postIndex = 0; postIndex < posts.Length; postIndex++)
        {
            var post = posts[postIndex];
            if (post.RepostOfId is not null)
            {
                continue;
            }

            var photos = PostMedia.Photos(post.MediaUrls, post.MediaUrl);
            if (photos.Length == 0 || HiddenByMediaPreference(photos))
            {
                continue;
            }

            var veiled = SensitiveReveals.ShouldVeil(post.Sensitive, post.Id, configuration.ShowSensitiveContent);
            for (var photoIndex = 0; photoIndex < photos.Length; photoIndex++)
            {
                var column = cellIndex % MediaGridColumns;
                var row = cellIndex / MediaGridColumns;
                cellIndex++;
                var rowTop = row * (cell + gap);
                var localTop = origin.Y + rowTop - viewTop;
                if (localTop + cell < -cullMargin || localTop > viewHeight + cullMargin)
                {
                    continue;
                }

                var min = new Vector2(origin.X + column * (cell + gap), origin.Y + rowTop);
                var max = new Vector2(min.X + cell, min.Y + cell);
                DrawProfileMediaCell(drawList, post, photos[photoIndex], min, max, veiled, scale);
            }
        }

        if (cellIndex == 0)
        {
            Typography.DrawCentered(new Vector2(body.Center.X, origin.Y + 40f * scale), Loc.T(L.Common.NoPhotos),
                ChirperInk.MutedInk);
            return;
        }

        var rows = (cellIndex + MediaGridColumns - 1) / MediaGridColumns;
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rows * (cell + gap)));
    }

    private void DrawProfileMediaCell(ImDrawListPtr drawList, PostDto post, string url, Vector2 min, Vector2 max,
        bool veiled, float scale)
    {
        if (veiled)
        {
            SensitiveVeil.Draw(drawList, min, max, 0f);
        }
        else
        {
            var texture = MediaTexture(url);
            if (texture is null)
            {
                drawList.AddRectFilled(min, max, ImGui.GetColorU32(ChirperInk.ChipFill));
            }
            else
            {
                var (uv0, uv1) = ImageFit.CoverSquare(texture.Size);
                drawList.AddImage(texture.Handle, min, max, uv0, uv1, 0xFFFFFFFFu);
            }

            if (GifMedia.IsGif(url))
            {
                GifBadge.Draw(drawList, new Rect(min, max));
            }
        }

        var hovered = UiInteract.Hover(min, max);
        if (hovered)
        {
            drawList.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.08f)));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(min, max, hovered))
        {
            OpenThread(post);
        }
    }

    private void OpenProfileSheet(UserDto user)
    {
        actions.Reset();
        sheetUser = user;
        sheetKind = SheetKind.Profile;
        sheetCount = 0;
        if (user.IsMe)
        {
            AddSheetItem(PostSheetAction.Rules, Loc.T(L.Conduct.Eyebrow), false);
        }
        else
        {
            AddSheetItem(PostSheetAction.Report, Loc.T(L.Report.UserTitle), true);
            AddSheetItem(PostSheetAction.Block,
                user.Handle.Length > 0 ? Loc.T(L.Chirper.BlockHandle, user.Handle) : Loc.T(L.Social.BlockAction),
                true);
        }

        sheet.Open();
    }

    private void DrawProfileSheet(Rect screen)
    {
        var picked = sheet.Draw(screen, SheetStyle, sheetItems.AsSpan(0, sheetCount), Loc.T(L.Common.Cancel),
            false);
        if (picked < 0 || sheetUser is not { } user)
        {
            return;
        }

        switch (sheetActions[picked])
        {
            case PostSheetAction.Report:
                profile.OpenReport("user", user.Id, Loc.T(L.Report.UserTitle));
                break;
            case PostSheetAction.Block:
                profile.AskBlock(user.DisplayName, user.Handle, user.Id);
                break;
            case PostSheetAction.Rules:
                conduct.ShowRules(Id);
                break;
        }
    }

    private void OpenUserList(string sourceId, UserListKind kind)
    {
        actions.Reset();
        store.OpenUserList(sourceId, kind);
        router.Push(ChirperRoute.UserList(sourceId, kind));
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

    private void OpenBannerComposer()
    {
        banner.Open();
        router.Push(ChirperRoute.Banner);
    }

    private void DrawBannerCompose(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        if (banner.Draw(area, context, Accent))
        {
            store.ReloadProfile();
            router.Pop();
        }
    }

    private void OpenHashtag(string tag) => OpenHashtag(tag, 0);

    private void OpenHashtag(string tag, int postsToday)
    {
        actions.Reset();
        hashtagTodayCount = postsToday;
        store.OpenHashtagPosts(tag);
        router.Push(ChirperRoute.Hashtag(tag));
    }

    private string HashtagTitle(string tag)
    {
        if (!string.Equals(hashtagTitleTag, tag, StringComparison.Ordinal))
        {
            hashtagTitleTag = tag;
            hashtagTitle = "#" + tag;
        }

        return hashtagTitle;
    }

    private void DrawHashtag(Rect area, string tag)
    {
        store.EnsureHashtagPosts(tag);
        DrawScreenHeader(area, HashtagTitle(tag), 0f,
            hashtagTodayCount > 0 ? Loc.Plural(L.Chirper.ChirpsToday, hashtagTodayCount) : string.Empty);
        var scale = UiScale.Current;
        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        using (AppSurface.BeginEdgeToEdge(body))
        {
            var posts = store.HashtagPosts;
            if (posts.Length == 0)
            {
                Typography.DrawCentered(new Vector2(body.Center.X, top + 60f * scale),
                    store.HashtagLoading ? Loc.T(L.Common.Loading) : Loc.T(L.Social.HashtagEmpty),
                    ChirperInk.MutedInk);
                return;
            }

            ImGui.Dummy(new Vector2(0f, FeedTopPadding * scale));
            hashtagVirtualizer.BeginFrame();
            renderedUnderlyingIds.Clear();
            for (var index = 0; index < posts.Length; index++)
            {
                var post = posts[index];
                if (HiddenByMediaPreference(post))
                {
                    continue;
                }

                if (!renderedUnderlyingIds.Add(post.RepostOfId ?? post.Id))
                {
                    continue;
                }

                if (hashtagVirtualizer.Skip(post.Id))
                {
                    continue;
                }

                DrawPost(post);
                hashtagVirtualizer.Record(post.Id);
            }

            if (store.HashtagLoadingMore)
            {
                InfiniteScroll.DrawLoadingRow(body.Center.X, ChirperInk.MutedInk);
            }

            ImGui.Dummy(new Vector2(0f, 24f * scale));
            if (InfiniteScroll.ReachedBottom() && store.HasMoreHashtagPosts && !store.HashtagLoadingMore)
            {
                store.LoadMoreHashtagPosts();
            }
        }
    }

    private void DrawHomeTopBar(Rect area)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        var featherSize = 26f * scale;
        var featherCenter = new Vector2(area.Min.X + CellPadX * scale + featherSize * 0.5f, rowCenterY);
        PhoneIcon.Draw(drawList, featherCenter, PhoneIcons.Feather,
            ChirperInk.AccentLink, featherSize);
        var titleLeft = featherCenter.X + featherSize * 0.5f + 12f * scale;
        var buttonRadius = TopBarButtonRadius * scale;
        var refreshCenter = new Vector2(area.Max.X - CellPadX * scale - buttonRadius, rowCenterY);
        var filterCenter = new Vector2(refreshCenter.X - buttonRadius * 2f - 2f * scale, rowCenterY);
        var titleHeight = Typography.LineHeight(WordmarkStyle);
        var titleMaxWidth = MathF.Max(1f, filterCenter.X - buttonRadius - 8f * scale - titleLeft);
        var title = Typography.FitText(DisplayName, titleMaxWidth, WordmarkStyle);
        var titleSize = Typography.Measure(title, WordmarkStyle);
        var titleMin = new Vector2(titleLeft - 6f * scale, rowCenterY - titleHeight * 0.5f - 4f * scale);
        var titleMax = new Vector2(titleLeft + titleSize.X + 6f * scale, rowCenterY + titleHeight * 0.5f + 4f * scale);
        UiInteract.HoverHighlight(drawList, titleMin, titleMax, 8f * scale);
        Typography.Draw(drawList, new Vector2(titleLeft, rowCenterY - titleHeight * 0.5f), title, ChirperInk.TitleInk,
            WordmarkStyle);
        if (UiInteract.HoverClick(titleMin, titleMax))
        {
            RefreshActiveFeed();
        }

        if (!store.IsSignedIn)
        {
            return;
        }

        var filtersActive = FeedFiltersActive();
        if (DrawTopBarButton(drawList, filterCenter, buttonRadius, Loc.T(L.Chirper.FeedFilters), true, filtersActive))
        {
            OpenFilterSheet();
        }

        if (store.IsLoading(activeScope))
        {
            LoadingPulse.Spinner(refreshCenter, 8f * scale, ChirperInk.AccentLink);
        }
        else if (DrawTopBarButton(drawList, refreshCenter, buttonRadius, Loc.T(L.Common.Refresh), false, false))
        {
            RefreshActiveFeed();
        }
    }

    private static bool DrawTopBarButton(ImDrawListPtr drawList, Vector2 center, float radius, string tooltip,
        bool filter, bool highlighted) =>
        SocialChrome.DrawHeaderIcon(drawList, center, radius,
            filter ? PhoneIcons.AdjustmentsHorizontal : PhoneIcons.Refresh, 18f, tooltip, ChirperInk.Shared,
            GlassPillInk, highlighted);

    private static void DrawBellBadge(Vector2 bellCenter, int count)
    {
        var scale = UiScale.Current;
        SocialChrome.DrawCountBadge(ImGui.GetWindowDrawList(), bellCenter + new Vector2(10f * scale, -10f * scale),
            count, ChirperInk.Shared);
    }
}
