using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Aethergram;

internal readonly record struct PostGridStyle(float Aspect, bool ShowLikes);

internal sealed partial class AethergramApp
{
    private const float CellPadX = SocialChrome.CellPadX;
    private const float HeaderIconSize = 24f;
    private const float PillHeight = 32f;
    private const float PillRounding = 8f;
    private const float IconTabHeight = 44f;
    private const float IconTabUnderline = 1.5f;
    private const float IconTabIconSize = 22f;
    private const float TabSmoothTime = 0.09f;
    private const float GridGap = 1.5f;
    private const float UserRowHeight = 64f;
    private const float UserRowAvatarRadius = 22f;
    private const float FollowPillWidth = 96f;
    private const float EmptyStateTop = 72f;

    private static readonly SocialInk Ink = AethergramInk.Shared;
    private static readonly TextStyle ScreenTitleStyle = new(1.05f, FontWeight.SemiBold);
    private static readonly TextStyle PillStyle = TextStyles.SubheadlineEmphasized;
    private static readonly TextStyle GridOverlayStyle = TextStyles.FootnoteEmphasized;
    private static readonly TextStyle EmptyTitleStyle = TextStyles.Title2;
    private static readonly TextStyle EmptyBodyStyle = TextStyles.Callout;
    private static readonly PostGridStyle SquareGrid = new(1f, false);
    private static readonly PostGridStyle ExploreGrid = new(1.3333f, true);
    private static readonly SocialUserRowStyle UserRowStyle = new(UserRowHeight, UserRowAvatarRadius, CellPadX, 12f,
        PillHeight, TextStyles.Headline, TextStyles.Subheadline, AethergramInk.Shared.HoverTint);

    private Rect screenRect;

    private void PaintBarBackdrop(ImDrawListPtr drawList, Rect bar) =>
        SocialChrome.PaintBarBackdrop(ui, drawList, bar, screenRect);

    private static void DrawHairline(ImDrawListPtr drawList, float left, float right, float y) =>
        FeedCell.Hairline(drawList, left, right, y, Ink.Hairline);

    private void DrawScreenHeader(Rect area, string title, int trailingSlots = 0, bool showBack = true,
        bool centered = true, string subtitle = "") =>
        SocialChrome.DrawScreenHeader(area, title, Ink, back, ScreenTitleStyle,
            SocialChrome.HeaderReserve(trailingSlots), subtitle, showBack, centered);

    private static bool DrawHeaderIcon(ImDrawListPtr drawList, Vector2 center, string glyph, string tooltip,
        bool highlighted = false, int badge = 0, float iconSize = HeaderIconSize) =>
        SocialChrome.DrawHeaderIcon(drawList, center, SocialChrome.HeaderIconRadius * UiScale.Current, glyph,
            iconSize, tooltip, Ink, Ink.TitleInk, highlighted, badge);

    private static int DrawIconTabs(Rect row, ReadOnlySpan<string> glyphs, ReadOnlySpan<string> labels, int active,
        ref Spring slide) =>
        UnderlineTabs.DrawIcons(row, glyphs, labels, active, ref slide, Ink, IconTabIconSize, IconTabUnderline,
            TabSmoothTime);

    private static bool DrawAccentPill(Rect rect, string label, bool enabled = true) =>
        SocialPill.Accent(ImGui.GetWindowDrawList(), rect, label, Ink, PillStyle, PillRounding * UiScale.Current,
            enabled);

    private static bool DrawGrayPill(Rect rect, string label) =>
        SocialPill.Flat(ImGui.GetWindowDrawList(), rect, label, Ink.ButtonFill, Ink.ButtonHover, default,
            Ink.TitleInk, PillStyle, PillRounding * UiScale.Current);

    private static bool DrawGrayIconButton(Rect rect, string glyph, string tooltip, float iconSize = 20f) =>
        SocialPill.Icon(ImGui.GetWindowDrawList(), rect, glyph, tooltip, Ink.ButtonFill, Ink.ButtonHover,
            Ink.TitleInk, iconSize, PillRounding * UiScale.Current);

    private void DrawFollowPill(Rect rect, UserDto user)
    {
        var state = SocialFeedStore.FollowStateOf(user);
        var clicked = state switch
        {
            FollowState.Following => DrawGrayPill(rect, Loc.T(L.Aethergram.Following)),
            FollowState.Requested => DrawGrayPill(rect, Loc.T(L.Social.Requested)),
            _ => DrawAccentPill(rect, Loc.T(L.Aethergram.Follow)),
        };
        if (clicked)
        {
            store.ToggleFollow(user);
        }
    }

    private void DrawPostGrid(PostDto[] posts, LocString emptyMessage, bool hasMore, bool loadingMore,
        Action loadMore, in PostGridStyle style, PostSource source)
    {
        var scale = UiScale.Current;
        if (posts.Length == 0)
        {
            Typography.DrawCentered(
                new Vector2(ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X * 0.5f,
                    ImGui.GetCursorScreenPos().Y + 40f * scale), Loc.T(emptyMessage), Ink.MutedInk);
            return;
        }

        var width = ScrollLayout.StableContentWidth();
        var gridCenterX = ImGui.GetCursorScreenPos().X + width * 0.5f;
        var gap = GridGap * scale;
        var cellWidth = (width - gap * (GridColumns - 1)) / GridColumns;
        var cellHeight = cellWidth * style.Aspect;
        using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(gap, gap)))
        {
            for (var index = 0; index < posts.Length; index++)
            {
                ImGui.Dummy(new Vector2(cellWidth, cellHeight));
                var min = ImGui.GetItemRectMin();
                var max = ImGui.GetItemRectMax();
                DrawGridTile(posts[index], min, max, style);
                if (UiInteract.Click(min, max, UiInteract.Hover(min, max)))
                {
                    OpenPosts(posts[index].Id, source);
                }

                if (index % GridColumns != GridColumns - 1)
                {
                    ImGui.SameLine();
                }
            }
        }

        ImGui.NewLine();
        if (loadingMore)
        {
            InfiniteScroll.DrawLoadingRow(gridCenterX, Ink.MutedInk);
        }
        else if (hasMore && InfiniteScroll.ReachedBottom())
        {
            loadMore();
        }

        ImGui.Dummy(new Vector2(0f, 24f * scale));
    }

    private void DrawGridTile(PostDto post, Vector2 min, Vector2 max, in PostGridStyle style)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var photos = PostMedia.Photos(post.MediaUrls, post.MediaUrl);
        if (SensitiveReveals.ShouldVeil(post.Sensitive, post.Id, configuration.ShowSensitiveContent))
        {
            SensitiveVeil.Draw(drawList, min, max, 0f);
        }
        else
        {
            var texture = images.Get(photos.Length > 0 ? photos[0] : null);
            if (texture is null)
            {
                drawList.AddRectFilled(min, max, ImGui.GetColorU32(Ink.ThumbFill));
                return;
            }

            var (uv0, uv1) = ImageFit.Cover(texture.Size.X, texture.Size.Y, max.X - min.X, max.Y - min.Y);
            drawList.AddImage(texture.Handle, min, max, uv0, uv1);
            if (photos.Length > 1)
            {
                PhoneIcon.Draw(drawList, new Vector2(max.X - 12f * scale, min.Y + 12f * scale), PhoneIcons.Copy,
                    Ink.White, 16f * scale);
            }
        }

        if (style.ShowLikes)
        {
            DrawGridOverlayCount(drawList, min, max, post.TotalReactions);
        }

        if (UiInteract.Hover(min, max))
        {
            drawList.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.08f)));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
    }

    private static void DrawGridOverlayCount(ImDrawListPtr drawList, Vector2 min, Vector2 max, int likes)
    {
        var scale = UiScale.Current;
        var scrimTop = max.Y - 34f * scale;
        drawList.AddRectFilledMultiColor(new Vector2(min.X, scrimTop), max, 0u, 0u, ImGui.GetColorU32(Ink.Scrim),
            ImGui.GetColorU32(Ink.Scrim));
        var iconCenter = new Vector2(min.X + 14f * scale, max.Y - 12f * scale);
        PhoneIcon.Draw(drawList, iconCenter, PhoneIcons.HeartFilled, Ink.White, 13f * scale);
        var label = CountText.Compact(likes);
        var size = Typography.Measure(label, GridOverlayStyle);
        Typography.Draw(drawList, new Vector2(iconCenter.X + 10f * scale, iconCenter.Y - size.Y * 0.5f), label,
            Ink.White, GridOverlayStyle);
    }

    private SocialUserRowResult DrawUserRow(UserDto user, float trailingWidth)
    {
        var regionCode = SocialRegion.Resolve(user.Region, user.World, gameData);
        return SocialUserRow.Draw("aethergram.row.", user, SocialIdentity.ProfileMeta(user.Handle, regionCode),
            trailingWidth, UserRowStyle, Ink, theme, images, lodestone);
    }

    private void DrawUserRowWithFollow(UserDto user)
    {
        var isMe = store.Me is { } me && me.Id == user.Id;
        var row = DrawUserRow(user, isMe ? 0f : FollowPillWidth);
        if (!isMe)
        {
            DrawFollowPill(row.Trailing, user);
        }

        if (row.Tapped)
        {
            OpenProfile(user.Id);
        }
    }

    private static void DrawEmptyState(Rect area, string title, string body)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var maxWidth = MathF.Max(1f, area.Width - CellPadX * 2f * scale);
        var top = area.Min.Y + EmptyStateTop * scale;
        var titleHeight = Typography.DrawWrappedCentered(drawList, title, EmptyTitleStyle, Ink.TitleInk,
            new Vector2(area.Center.X, top), maxWidth);
        if (body.Length == 0)
        {
            return;
        }

        Typography.DrawWrappedCentered(drawList, body, EmptyBodyStyle, Ink.MutedInk,
            new Vector2(area.Center.X, top + titleHeight + 8f * scale), maxWidth);
    }
}
