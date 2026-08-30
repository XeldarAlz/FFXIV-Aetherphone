using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Emoji;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Chirper;

internal sealed partial class ChirperApp
{
    private const float BackChipRadius = SocialChrome.BackChipRadius;
    private const float UserRowHeight = 62f;
    private const float FollowPillHeight = 31f;
    private const float ActivityIconRadius = 19f;
    private const float ActivityBadgeRadius = 9f;
    private const float ActivityBadgeRimFraction = 0.70711f;
    private const float ReactionBadgeRadius = 9f;
    private const float ReactionRailHeight = 46f;
    private const float SearchDebounceSeconds = 0.35f;
    private const float TagRowHeight = 60f;
    private const float TagGlyphSize = 38f;
    private const float EditAvatarRadius = 46f;
    private const float EditRowHeight = 46f;
    private const float EditBioMinHeight = 64f;

    private static readonly TextStyle ScreenTitleStyle = new(1.13f, FontWeight.Bold);
    private static readonly TextStyle ScreenSubtitleStyle = new(0.8f, FontWeight.Regular);
    private static readonly TextStyle UserNameStyle = new(0.97f, FontWeight.SemiBold);
    private static readonly TextStyle UserSubStyle = new(0.87f, FontWeight.Regular);
    private static readonly TextStyle SmallPillStyle = new(0.83f, FontWeight.Bold);
    private static readonly SocialUserRowStyle UserRowStyle = new(UserRowHeight, FeedAvatarRadius, CellPadX, AvatarGap,
        FollowPillHeight, UserNameStyle, UserSubStyle, RowHover);
    private static readonly TextStyle SectionLabelStyle = new(0.87f, FontWeight.Bold);
    private static readonly TextStyle TagNameStyle = new(1f, FontWeight.SemiBold);
    private static readonly TextStyle TagCountStyle = new(0.87f, FontWeight.Regular);
    private static readonly TextStyle TagGlyphStyle = new(1.13f, FontWeight.Bold);
    private static readonly TextStyle ActivityActorStyle = new(0.95f, FontWeight.Bold);
    private static readonly TextStyle ActivityBodyStyle = new(0.93f, FontWeight.Regular);
    private static readonly TextStyle ActivityTimeStyle = new(0.8f, FontWeight.Regular);
    private static readonly TextStyle EditLabelStyle = new(0.93f, FontWeight.Regular);
    private static readonly TextStyle EditValueStyle = new(1f, FontWeight.Regular);
    private static readonly TextStyle EditHintStyle = new(0.8f, FontWeight.SemiBold);
    private static readonly TextStyle EditFootStyle = new(0.83f, FontWeight.Regular);
    private static readonly TextStyle SaveWordStyle = new(1.03f, FontWeight.Bold);
    private static readonly Vector4 SolidPillFill = new(0.949f, 0.961f, 0.980f, 1f);
    private static readonly Vector4 SolidPillInk = new(0.043f, 0.078f, 0.125f, 1f);
    private static readonly Vector4 GlassPillInk = new(0.875f, 0.902f, 0.941f, 1f);
    private static readonly Vector4 GlassPillStroke = new(1f, 1f, 1f, 0.12f);
    private static readonly Vector4 RowHover = new(1f, 1f, 1f, 0.03f);
    private static readonly Vector4 MentionInk = new(0.718f, 0.612f, 1f, 1f);
    private static readonly Vector4 ActivityBadgeRing = new(0f, 0f, 0f, 0.55f);
    private static readonly Vector4 UnreadTint = Palette.WithAlpha(ChirperInk.Accent, 0.045f);
    private static readonly Vector4 EditCardFill = new(1f, 1f, 1f, 0.045f);
    private static readonly Vector4 EditCardStroke = new(1f, 1f, 1f, 0.07f);
    private static readonly Vector4 EditRowHairline = new(1f, 1f, 1f, 0.06f);
    private static readonly Vector4 SearchFieldFill = new(1f, 1f, 1f, 0.08f);
    private static readonly Vector4 SearchFieldStroke = new(1f, 1f, 1f, 0.07f);

    private bool mentionsOnly;
    private Spring activitySegment;
    private string editDisplay = string.Empty;
    private string editHandle = string.Empty;
    private string editBio = string.Empty;
    private string editStatus = string.Empty;
    private string? editLoadedFor;
    private volatile bool editBusy;
    private volatile int editOutcome;

    private float DrawScreenHeader(Rect area, string title, float trailingReserve = 0f, string subtitle = "",
        bool showBack = true, TextStyle? titleStyle = null) =>
        SocialChrome.DrawScreenHeader(area, title, ChirperInk.Shared, back, titleStyle ?? ScreenTitleStyle,
            trailingReserve / UiScale.Current, subtitle, showBack);

    private void DrawDiscover(Rect area, bool root = false)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        var fieldLeft = area.Min.X + 14f * scale;
        if (!root)
        {
            var chipRadius = BackChipRadius * scale;
            var chipCenter = new Vector2(area.Min.X + 14f * scale + chipRadius, rowCenterY);
            if (SocialChrome.DrawBackChip(drawList, chipCenter, chipRadius, ChirperInk.Shared))
            {
                back();
            }

            fieldLeft = chipCenter.X + chipRadius + 10f * scale;
        }

        var fieldHeight = 36f * scale;
        var fieldMin = new Vector2(fieldLeft, rowCenterY - fieldHeight * 0.5f);
        var fieldMax = new Vector2(area.Max.X - 14f * scale, rowCenterY + fieldHeight * 0.5f);
        Squircle.Fill(drawList, fieldMin, fieldMax, 12f * scale, ImGui.GetColorU32(SearchFieldFill));
        Squircle.Stroke(drawList, fieldMin, fieldMax, 12f * scale, ImGui.GetColorU32(SearchFieldStroke), 1f);
        PhoneIcon.Draw(drawList, new Vector2(fieldMin.X + 19f * scale, rowCenterY), PhoneIcons.Search,
            ChirperInk.MutedInk, 15f * scale);
        ImGui.SetCursorScreenPos(new Vector2(fieldMin.X + 32f * scale, rowCenterY - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(fieldMax.X - fieldMin.X - 40f * scale);
        var hint = Loc.T(L.Chirper.SearchHint);
        Plugin.Fonts.NoticeText(hint);
        Plugin.Fonts.NoticeText(searchDraft);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, ChirperInk.TitleInk))
        {
            if (ImGui.InputTextWithHint("##chirperSearch", hint, ref searchDraft, 64))
            {
                searchDirtyAt = ImGui.GetTime();
            }
        }

        var searchingTags = searchDraft.TrimStart().StartsWith('#');
        if (!trendingRequested)
        {
            RunDiscoverQuery();
        }

        if (searchDirtyAt >= 0d && ImGui.GetTime() - searchDirtyAt >= SearchDebounceSeconds)
        {
            if (string.IsNullOrWhiteSpace(searchDraft))
            {
                store.ClearDiscover();
            }

            RunDiscoverQuery();
        }

        var top = area.Min.Y + AppHeader.Height * scale + 6f * scale;
        var listRect = new Rect(new Vector2(area.Min.X, top), area.Max);
        var results = searchingTags ? Array.Empty<UserDto>() : store.DiscoverResults;
        var tags = store.DiscoverTags;
        using (AppSurface.BeginEdgeToEdge(listRect))
        {
            if (results.Length == 0 && tags.Length == 0)
            {
                var message = store.Searching || store.TagsLoading
                    ? Loc.T(L.Common.Searching)
                    : Loc.T(L.Chirper.SearchByName);
                Typography.DrawCentered(new Vector2(listRect.Center.X, listRect.Min.Y + 60f * scale), message,
                    ChirperInk.MutedInk, MetaStyle);
                return;
            }

            if (tags.Length > 0)
            {
                DrawSectionLabel(string.IsNullOrWhiteSpace(searchDraft)
                    ? Loc.T(L.Chirper.Trending)
                    : Loc.T(L.Chirper.HashtagsTitle));
                for (var index = 0; index < tags.Length; index++)
                {
                    DrawTagRow(tags[index]);
                }
            }

            if (results.Length > 0)
            {
                DrawSectionLabel(Loc.T(L.Chirper.SuggestedPeople));
                for (var index = 0; index < results.Length; index++)
                {
                    DrawUserRow(results[index]);
                }
            }

            ImGui.Dummy(new Vector2(0f, 40f * scale));
        }
    }

    private void RunDiscoverQuery()
    {
        searchDirtyAt = -1d;
        trendingRequested = true;
        if (string.IsNullOrWhiteSpace(searchDraft))
        {
            store.SearchTags(string.Empty);
            return;
        }

        store.SearchTags(searchDraft);
        if (!searchDraft.TrimStart().StartsWith('#'))
        {
            store.Search(searchDraft);
        }
    }

    private void DrawTagRow(TagSummaryDto summary)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = TagRowHeight * scale;
        var padX = CellPadX * scale;
        var rowMax = new Vector2(origin.X + width, origin.Y + height);
        var hovered = UiInteract.Hover(origin, rowMax);
        if (hovered)
        {
            drawList.AddRectFilled(origin, rowMax, ImGui.GetColorU32(RowHover));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var glyphSize = TagGlyphSize * scale;
        var glyphMin = new Vector2(origin.X + padX, origin.Y + (height - glyphSize) * 0.5f);
        var glyphMax = glyphMin + new Vector2(glyphSize, glyphSize);
        Squircle.Fill(drawList, glyphMin, glyphMax, 12f * scale, ImGui.GetColorU32(ChirperInk.AccentWash));
        Typography.DrawCentered(drawList, (glyphMin + glyphMax) * 0.5f, "#", ChirperInk.AccentLink, TagGlyphStyle);
        var textLeft = glyphMax.X + 12f * scale;
        var chevronLeft = rowMax.X - padX - 14f * scale;
        var textWidth = MathF.Max(1f, chevronLeft - textLeft - 8f * scale);
        var nameHeight = Typography.LineHeight(TagNameStyle);
        var countHeight = Typography.LineHeight(TagCountStyle);
        var textTop = origin.Y + (height - nameHeight - countHeight - 2f * scale) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, textTop),
            Typography.FitText("#" + summary.Tag, textWidth, TagNameStyle), ChirperInk.TitleInk, TagNameStyle);
        var count = summary.PostsToday > 0
            ? Loc.Plural(L.Chirper.ChirpsToday, summary.PostsToday)
            : Loc.Plural(L.Chirper.Posts, summary.Posts);
        Typography.Draw(drawList, new Vector2(textLeft, textTop + nameHeight + 2f * scale),
            Typography.FitText(count, textWidth, TagCountStyle), ChirperInk.MutedInk, TagCountStyle);
        PhoneIcon.Draw(drawList, new Vector2(chevronLeft, origin.Y + height * 0.5f), PhoneIcons.ChevronRight,
            ChirperInk.FaintInk, 14f * scale);
        if (UiInteract.Click(origin, rowMax, hovered))
        {
            OpenHashtag(summary.Tag, summary.PostsToday);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private static void DrawSectionLabel(string label) =>
        SocialChrome.DrawSectionLabel(label, ChirperInk.Shared, SectionLabelStyle);

    private void DrawUserRow(UserDto user)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var state = SocialFeedStore.FollowStateOf(user);
        var pillLabel = user.IsMe ? string.Empty : state switch
        {
            FollowState.Following => Loc.T(L.Chirper.Following),
            FollowState.Requested => Loc.T(L.Social.Requested),
            _ => Loc.T(L.Chirper.Follow),
        };
        var pillWidth = pillLabel.Length > 0 ? Typography.Measure(pillLabel, SmallPillStyle).X / scale + 30f : 0f;
        var regionCode = user.IsMe
            ? SocialRegion.EffectiveCode(configuration, gameData)
            : SocialRegion.Resolve(user.Region, user.World, gameData);
        var sub = user.Bio.Length > 0 && user.Handle.Length > 0
            ? $"@{user.Handle} · {user.Bio}"
            : SocialIdentity.ProfileMeta(user.Handle, regionCode);
        var row = SocialUserRow.Draw("chirper.row.name.", user, sub, pillWidth, UserRowStyle, ChirperInk.Shared, theme,
            images, lodestone);
        DrawReactionBadge(drawList, row.AvatarCenter, row.AvatarRadius, store.ReactionKindOf(user.Id));
        if (pillLabel.Length > 0)
        {
            var solid = state == FollowState.None;
            var rounding = row.Trailing.Height * 0.5f;
            var clicked = solid
                ? SocialPill.Flat(drawList, row.Trailing, pillLabel, SolidPillFill, ChirperInk.White, default,
                    SolidPillInk, SmallPillStyle, rounding)
                : SocialPill.Flat(drawList, row.Trailing, pillLabel, GlassPillFill, ChirperInk.ChipHover,
                    GlassPillStroke, GlassPillInk, SmallPillStyle, rounding);
            if (clicked)
            {
                store.ToggleFollow(user);
            }
        }

        if (row.Tapped)
        {
            OpenProfile(user.Id);
        }
    }

    private void DrawUserList(Rect area, string sourceId, UserListKind kind)
    {
        store.EnsureUserList(sourceId, kind);
        DrawScreenHeader(area, SocialProfilePages.UserListTitle(kind), 0f, SocialProfilePages.UserListCount(store));
        var scale = UiScale.Current;
        var top = area.Min.Y + AppHeader.Height * scale;
        var counts = kind == UserListKind.Likers ? store.UserListReactionCounts : null;
        if (counts is not null && ActiveReactionKinds(counts) > 1)
        {
            var rail = new Rect(new Vector2(area.Min.X, top),
                new Vector2(area.Max.X, top + ReactionRailHeight * scale));
            DrawReactionFilterRail(rail, counts);
            top = rail.Max.Y;
        }

        var listRect = new Rect(new Vector2(area.Min.X, top), area.Max);
        var snapshot = store.UserListResults;
        using (AppSurface.BeginEdgeToEdge(listRect))
        {
            if (snapshot.Length == 0)
            {
                var message = store.UserListLoading ? Loc.T(L.Common.Loading)
                    : store.UserListFailed ? Loc.T(L.Chirper.ProfileError)
                    : Loc.T(L.Social.ListEmpty);
                Typography.DrawCentered(new Vector2(listRect.Center.X, listRect.Min.Y + 60f * scale), message,
                    ChirperInk.MutedInk, MetaStyle);
                return;
            }

            ImGui.Dummy(new Vector2(0f, 4f * scale));
            for (var index = 0; index < snapshot.Length; index++)
            {
                DrawUserRow(snapshot[index]);
            }

            if (store.UserListLoadingMore)
            {
                InfiniteScroll.DrawLoadingRow(listRect.Center.X, ChirperInk.MutedInk);
            }
            else if (store.HasMoreUserList && InfiniteScroll.ReachedBottom())
            {
                store.LoadMoreUserList();
            }

            ImGui.Dummy(new Vector2(0f, 12f * scale));
        }
    }

    private static int ActiveReactionKinds(int[] counts)
    {
        var active = 0;
        for (var kind = 0; kind < counts.Length; kind++)
        {
            if (counts[kind] > 0)
            {
                active++;
            }
        }

        return active;
    }

    private static void DrawReactionBadge(ImDrawListPtr drawList, Vector2 avatarCenter, float avatarRadius, int kind)
    {
        if (kind < 0)
        {
            return;
        }

        var scale = UiScale.Current;
        var offset = avatarRadius * ActivityBadgeRimFraction;
        var center = avatarCenter + new Vector2(offset, offset);
        var radius = ReactionBadgeRadius * scale;
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(ActivityBadgeRing), 20);
        var emoji = radius * 1.35f;
        var half = new Vector2(emoji * 0.5f, emoji * 0.5f);
        EmojiImages.TryDraw(drawList, ChirperReactions.EmojiFile(kind), center - half, center + half, 0xFFFFFFFFu);
    }

    private void DrawReactionFilterRail(Rect rail, int[] counts)
    {
        var scale = UiScale.Current;
        var selected = store.UserListReactionFilter;
        var total = 0;
        for (var kind = 0; kind < counts.Length; kind++)
        {
            total += counts[kind];
        }

        ImGui.SetCursorScreenPos(rail.Min);
        using (var child = ImRaii.Child("##chirperReactionRail", new Vector2(rail.Width, rail.Height), false,
                   ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoScrollbar
                   | ImGuiWindowFlags.HorizontalScrollbar))
        {
            if (child)
            {
                var drawList = ImGui.GetWindowDrawList();
                var origin = ImGui.GetCursorScreenPos();
                var centerY = origin.Y + rail.Height * 0.5f;
                var gap = ReactionChipGap * scale;
                var cursorX = origin.X + CellPadX * scale;
                cursorX += DrawReactionFilterChip(drawList, cursorX, centerY, -1, total, selected < 0) + gap;
                for (var kind = 0; kind < counts.Length; kind++)
                {
                    if (counts[kind] == 0)
                    {
                        continue;
                    }

                    cursorX += DrawReactionFilterChip(drawList, cursorX, centerY, kind, counts[kind],
                        selected == kind) + gap;
                }

                ImGui.Dummy(new Vector2(cursorX - origin.X + CellPadX * scale - gap, rail.Height));
            }
        }

        DrawHairline(ImGui.GetWindowDrawList(), rail.Min.X, rail.Max.X, rail.Max.Y);
    }

    private float DrawReactionFilterChip(ImDrawListPtr drawList, float x, float centerY, int kind, int count,
        bool selected)
    {
        var scale = UiScale.Current;
        var label = kind < 0 ? Loc.T(L.Chirper.ReactionsAll) : string.Empty;
        var countText = count.ToString(Loc.Culture);
        var countSize = Typography.Measure(countText, ChipCountStyle);
        var labelSize = label.Length > 0 ? Typography.Measure(label, ChipCountStyle) : Vector2.Zero;
        var emojiSize = ReactionChipEmoji * scale;
        var lead = kind < 0 ? labelSize.X : emojiSize;
        var chipWidth = (11f + 5f + 11f) * scale + lead + countSize.X;
        var chipHeight = ReactionChipHeight * scale;
        var min = new Vector2(x, centerY - chipHeight * 0.5f);
        var max = new Vector2(x + chipWidth, centerY + chipHeight * 0.5f);
        var hovered = UiInteract.Hover(min, max);
        var fill = selected ? ChirperInk.MineFill : ChirperInk.ChipFill;
        var stroke = selected ? ChirperInk.MineStroke : ChirperInk.ChipStroke;
        var ink = selected ? ChirperInk.MineInk : ChirperInk.BodyInk;
        Squircle.Fill(drawList, min, max, chipHeight * 0.5f, ImGui.GetColorU32(fill));
        Squircle.Stroke(drawList, min, max, chipHeight * 0.5f, ImGui.GetColorU32(stroke), 1f);
        var leadLeft = min.X + 11f * scale;
        if (kind < 0)
        {
            Typography.Draw(drawList, new Vector2(leadLeft, centerY - labelSize.Y * 0.5f), label, ink, ChipCountStyle);
        }
        else
        {
            var emojiMin = new Vector2(leadLeft, centerY - emojiSize * 0.5f);
            EmojiImages.TryDraw(drawList, ChirperReactions.EmojiFile(kind), emojiMin,
                emojiMin + new Vector2(emojiSize, emojiSize), 0xFFFFFFFFu);
        }

        Typography.Draw(drawList, new Vector2(leadLeft + lead + 5f * scale, centerY - countSize.Y * 0.5f), countText,
            ink, ChipCountStyle);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (kind >= 0)
        {
            HoverTooltip.Show(new Rect(min, max), ChirperReactions.Label(kind), HoverLabelSide.Below);
        }

        if (UiInteract.Click(min, max, hovered))
        {
            store.FilterUserListByReaction(kind);
        }

        return chipWidth;
    }

    private void DrawActivity(Rect area, bool root = false)
    {
        var scale = UiScale.Current;
        DrawScreenHeader(area, Loc.T(L.Social.ActivityTitle), 0f, string.Empty, !root,
            root ? WordmarkStyle : ScreenTitleStyle);
        var rowTop = area.Min.Y + AppHeader.Height * scale;
        var row = new Rect(new Vector2(area.Min.X, rowTop), new Vector2(area.Max.X, rowTop + FeedTabRowHeight * scale));
        var picked = UnderlineTabs.Draw(row, Loc.T(L.Chirper.ActivityAll), Loc.T(L.Chirper.ActivityMentions),
            mentionsOnly, ref activitySegment, ChirperInk.Shared, FeedTabsStyle);
        if (picked >= 0)
        {
            mentionsOnly = picked == 1;
        }

        var body = new Rect(new Vector2(area.Min.X, row.Max.Y), area.Max);
        activityFeed.EnsureFresh(social.Latest);
        var items = activityFeed.Items;
        var shown = 0;
        using (AppSurface.BeginEdgeToEdge(body))
        {
            for (var index = 0; index < items.Length; index++)
            {
                if (!ShowsActivity(items[index]))
                {
                    continue;
                }

                DrawActivityRow(items[index]);
                shown++;
            }

            if (shown == 0)
            {
                Typography.DrawWrappedCentered(new Vector2(body.Center.X, body.Min.Y + 90f * scale),
                    Loc.T(L.Social.ActivityEmpty), ChirperInk.MutedInk, MetaStyle, body.Width - 64f * scale);
                return;
            }

            ImGui.Dummy(new Vector2(0f, 16f * scale));
            if (ImGui.GetScrollY() >= ImGui.GetScrollMaxY() - 300f * scale)
            {
                activityFeed.LoadOlder();
            }
        }
    }

    private bool ShowsActivity(NotificationDto item)
    {
        if (item.App != Id || SocialActivity.IsModerationNotice(item.Type))
        {
            return false;
        }

        return !mentionsOnly
            || item.Type == SocialActivity.TypeMention
            || item.Type == SocialActivity.TypeCommentMention;
    }

    private void DrawActivityRow(NotificationDto item)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var padX = CellPadX * scale;
        var padY = 12f * scale;
        var iconRadius = ActivityIconRadius * scale;
        var timeText = TimeText.Short(item.CreatedAtUnix);
        var timeSize = Typography.Measure(timeText, ActivityTimeStyle);
        var textLeft = origin.X + padX + iconRadius * 2f + 12f * scale;
        var textRight = origin.X + width - padX - timeSize.X - 12f * scale;
        var textWidth = MathF.Max(1f, textRight - textLeft);
        var actor = SocialActivity.ActorLabel(item);
        var body = SocialActivity.Body(item);
        var actorHeight = Typography.LineHeight(ActivityActorStyle);
        var bodyHeight = body.Length > 0 ? EmojiText.BlockHeight(body, ActivityBodyStyle, textWidth) : 0f;
        var contentHeight = actorHeight + (bodyHeight > 0f ? 2f * scale + bodyHeight : 0f);
        var rowHeight = MathF.Max(iconRadius * 2f, contentHeight) + padY * 2f;
        var rowMax = new Vector2(origin.X + width, origin.Y + rowHeight);
        var hovered = UiInteract.Hover(origin, rowMax);
        if (!item.Read)
        {
            drawList.AddRectFilled(origin, rowMax, ImGui.GetColorU32(UnreadTint));
        }

        if (hovered)
        {
            drawList.AddRectFilled(origin, rowMax, ImGui.GetColorU32(RowHover));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var rowTapped = UiInteract.Click(origin, rowMax, hovered);
        var avatarCenter = new Vector2(origin.X + padX + iconRadius, origin.Y + padY + iconRadius);
        DrawAvatar(drawList, avatarCenter, iconRadius, actor, string.Empty, item.ActorAvatarUrl, 0.95f, 32,
            Frames.Of(item.ActorFrameId));
        var badgeOffset = iconRadius * ActivityBadgeRimFraction;
        DrawActivityBadge(drawList, avatarCenter + new Vector2(badgeOffset, badgeOffset), item.Type, scale);
        var textTop = origin.Y + padY;
        var actorWidth = UserName.DrawAuto(drawList, "chirper.activity.actor." + item.Id, actor, item.ActorBadges,
            item.ActorBadgeIds, textLeft, textTop, textWidth, ActivityActorStyle, ChirperInk.TitleInk, theme);
        var actorMin = new Vector2(textLeft, textTop);
        var actorMax = new Vector2(textLeft + actorWidth, textTop + actorHeight);
        if (UiInteract.Hover(actorMin, actorMax))
        {
            drawList.AddLine(new Vector2(actorMin.X, actorMax.Y - 1f * scale),
                new Vector2(actorMax.X, actorMax.Y - 1f * scale), ImGui.GetColorU32(ChirperInk.TitleInk), 1f);
        }

        if (bodyHeight > 0f)
        {
            EmojiText.DrawBlock(new Vector2(textLeft, textTop + actorHeight + 2f * scale), body, ChirperInk.BodyInk,
                ActivityBodyStyle, textWidth);
        }

        Typography.Draw(drawList, new Vector2(origin.X + width - padX - timeSize.X, textTop + 1f * scale), timeText,
            ChirperInk.FaintInk, ActivityTimeStyle);
        if (!item.Read)
        {
            drawList.AddCircleFilled(new Vector2(origin.X + width - padX - 3.5f * scale, textTop + timeSize.Y + 10f * scale),
                3.5f * scale, ImGui.GetColorU32(ChirperInk.Accent), 12);
        }

        DrawHairline(drawList, origin.X, rowMax.X, rowMax.Y);
        var avatarExtent = new Vector2(iconRadius, iconRadius);
        var avatarTapped = UiInteract.HoverClick(avatarCenter - avatarExtent, avatarCenter + avatarExtent);
        var actorTapped = UiInteract.HoverClick(actorMin, actorMax);
        if (avatarTapped || actorTapped)
        {
            OpenProfile(item.ActorId);
        }
        else if (rowTapped)
        {
            if (SocialActivity.OpensPost(item))
            {
                OpenThreadFromLink(item.PostId!);
            }
            else
            {
                OpenProfile(item.ActorId);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight));
    }

    private static void DrawActivityBadge(ImDrawListPtr drawList, Vector2 center, int type, float scale)
    {
        var radius = ActivityBadgeRadius * scale;
        var iconSize = radius * 1.15f;
        var ink = ImGui.GetColorU32(ChirperInk.White);
        drawList.AddCircleFilled(center, radius + 1.6f * scale, ImGui.GetColorU32(ActivityBadgeRing), 20);
        switch (type)
        {
            case SocialActivity.TypeLike:
            case SocialActivity.TypeCommentLike:
                drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(ChirperInk.LikeRed), 20);
                PhoneIcon.Draw(drawList, center, PhoneIcons.HeartFilled, ChirperInk.White, iconSize);
                break;
            case SocialActivity.TypeRepost:
            case SocialActivity.TypeQuote:
                drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(ChirperInk.RechirpGreen), 20);
                PhoneIcon.Draw(drawList, center, PhoneIcons.Repeat, ink, iconSize);
                break;
            case SocialActivity.TypeComment:
                drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(ChirperInk.AccentLink), 20);
                PhoneIcon.Draw(drawList, center, PhoneIcons.MessageCircle, ink, iconSize);
                break;
            case SocialActivity.TypeMention:
            case SocialActivity.TypeCommentMention:
                drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(MentionInk), 20);
                Typography.DrawCentered(drawList, center, "@", ChirperInk.White, TextStyles.Caption2);
                break;
            case SocialActivity.TypeFollow:
            case SocialActivity.TypeFollowRequest:
            case SocialActivity.TypeFollowAccept:
            case SocialActivity.TypeConnectRequest:
            case SocialActivity.TypeConnectAccept:
                drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(ChirperInk.AccentLink), 20);
                PhoneIcon.Draw(drawList, center, PhoneIcons.Plus, ink, iconSize);
                break;
            default:
                drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(ChirperInk.MutedInk), 20);
                PhoneIcon.Draw(drawList, center, PhoneIcons.Bell, ink, iconSize);
                break;
        }
    }

    private void DrawEditProfile(Rect area)
    {
        var me = store.Me ?? (store.ProfileUser is { IsMe: true } self ? self : null);
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        var cancelLabel = Loc.T(L.Common.Cancel);
        var cancelSize = Typography.Measure(cancelLabel, ComposeCancelStyle);
        var cancelMin = area.Min;
        var cancelMax = new Vector2(area.Min.X + CellPadX * scale + cancelSize.X + 12f * scale, area.Min.Y + AppHeader.Height * scale);
        var cancelHovered = UiInteract.Hover(cancelMin, cancelMax);
        Typography.Draw(drawList, new Vector2(area.Min.X + CellPadX * scale, rowCenterY - cancelSize.Y * 0.5f), cancelLabel,
            cancelHovered ? ChirperInk.TitleInk : ChirperInk.BodyInk, ComposeCancelStyle);
        if (cancelHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(cancelMin, cancelMax, cancelHovered))
        {
            back();
        }

        if (me is null)
        {
            store.EnsureMe();
            AppHeader.DrawTitleWithReserve(area, "chirper.edit.title", Loc.T(L.Chirper.EditProfile), 0f, ChirperInk.TitleInk,
                scale, ComposeTitleStyle);
            Typography.DrawCentered(new Vector2(area.Center.X, area.Min.Y + 120f * scale), Loc.T(L.Common.Loading),
                ChirperInk.MutedInk);
            return;
        }

        if (editOutcome == 1)
        {
            editOutcome = 0;
            store.ReloadProfile();
            toast.Show(Loc.T(L.Chirper.Save));
            back();
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

        var handleValid = SocialProfilePages.IsHandleValid(editHandle);
        var canSave = !editBusy && !string.IsNullOrWhiteSpace(editDisplay) && handleValid;
        var saveLabel = editBusy ? Loc.T(L.Chirper.Saving) : Loc.T(L.Chirper.Save);
        var saveSize = Typography.Measure(saveLabel, SaveWordStyle);
        var saveMax = new Vector2(area.Max.X, area.Min.Y + AppHeader.Height * scale);
        var saveMin = new Vector2(area.Max.X - CellPadX * scale - saveSize.X - 12f * scale, area.Min.Y);
        var saveHovered = canSave && UiInteract.Hover(saveMin, saveMax);
        Typography.Draw(drawList, new Vector2(area.Max.X - CellPadX * scale - saveSize.X, rowCenterY - saveSize.Y * 0.5f),
            saveLabel, !canSave ? ChirperInk.FaintInk : saveHovered ? ChirperInk.MineInk : ChirperInk.AccentLink, SaveWordStyle);
        if (saveHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(saveMin, saveMax, saveHovered))
        {
            SaveProfile();
        }

        AppHeader.DrawTitleWithReserve(area, "chirper.edit.title", Loc.T(L.Chirper.EditProfile), saveSize.X + 28f * scale,
            ChirperInk.TitleInk, scale, ComposeTitleStyle, (cancelMax.X - area.Min.X) / scale + 8f);

        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        using (AppSurface.Begin(body))
        {
            var listDrawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ImGui.GetContentRegionAvail().X;
            var padX = CellPadX * scale;
            var avatarRadius = EditAvatarRadius * scale;
            var avatarCenter = new Vector2(origin.X + width * 0.5f, origin.Y + 14f * scale + avatarRadius);
            DrawAvatar(listDrawList, avatarCenter, avatarRadius, me.Name, me.World, me.AvatarUrl, 1.4f, 64, Frames.Of(me.FrameId));
            var badgeRadius = 15f * scale;
            var badgeCenter = avatarCenter + new Vector2(avatarRadius - badgeRadius + 2f * scale, avatarRadius - badgeRadius + 2f * scale);
            listDrawList.AddCircleFilled(badgeCenter, badgeRadius + 3f * scale, ImGui.GetColorU32(ChirperInk.BackdropTop), 32);
            Squircle.FillCircleVerticalGradient(listDrawList, badgeCenter, badgeRadius, ImGui.GetColorU32(ChirperInk.Accent),
                ImGui.GetColorU32(ChirperInk.AccentDeep));
            PhoneIcon.Draw(listDrawList, badgeCenter, PhoneIcons.Camera, ChirperInk.White, 13f * scale);
            var avatarExtent = new Vector2(avatarRadius + 4f * scale, avatarRadius + 4f * scale);
            if (UiInteract.HoverClick(avatarCenter - avatarExtent, avatarCenter + avatarExtent))
            {
                OpenAvatarComposer();
            }

            var cardTop = avatarCenter.Y + avatarRadius + 18f * scale;
            var cardMin = new Vector2(origin.X + padX, cardTop);
            var cardRight = origin.X + width - padX;
            var rowHeight = EditRowHeight * scale;
            var labelWidth = 104f * scale;
            var innerPad = 14f * scale;
            var bioLabelHeight = Typography.LineHeight(EditLabelStyle);
            var bioFieldHeight = EditBioMinHeight * scale;
            var bioRowHeight = 12f * scale + bioLabelHeight + 5f * scale + bioFieldHeight + 12f * scale;
            var cardMax = new Vector2(cardRight, cardTop + rowHeight * 2f + bioRowHeight);
            Squircle.Fill(listDrawList, cardMin, cardMax, 16f * scale, ImGui.GetColorU32(EditCardFill));
            Squircle.Stroke(listDrawList, cardMin, cardMax, 16f * scale, ImGui.GetColorU32(EditCardStroke), 1f);

            var nameRowTop = cardTop;
            DrawEditLabel(listDrawList, cardMin.X + innerPad, nameRowTop, rowHeight, Loc.T(L.Chirper.NameLabel));
            DrawEditInput("##chirperEditName", cardMin.X + innerPad + labelWidth, cardRight - innerPad, nameRowTop, rowHeight,
                ref editDisplay, SocialProfilePages.DisplayNameMax, ImGuiInputTextFlags.None, ChirperInk.TitleInk);
            listDrawList.AddLine(new Vector2(cardMin.X, nameRowTop + rowHeight), new Vector2(cardRight, nameRowTop + rowHeight),
                ImGui.GetColorU32(EditRowHairline), 1f);

            var handleRowTop = nameRowTop + rowHeight;
            DrawEditLabel(listDrawList, cardMin.X + innerPad, handleRowTop, rowHeight, Loc.T(L.Chirper.HandleShort));
            var atSize = Typography.Measure("@", EditValueStyle);
            Typography.Draw(listDrawList, new Vector2(cardMin.X + innerPad + labelWidth, handleRowTop + (rowHeight - atSize.Y) * 0.5f),
                "@", ChirperInk.FaintInk, EditValueStyle);
            var availableLabel = Loc.T(L.Chirper.HandleAvailable);
            var availableSize = Typography.Measure(availableLabel, EditHintStyle);
            var showAvailable = handleValid && string.Equals(editHandle, me.Handle, StringComparison.Ordinal);
            var handleRight = cardRight - innerPad - (showAvailable ? availableSize.X + 22f * scale : 0f);
            if (DrawEditInput("##chirperEditHandle", cardMin.X + innerPad + labelWidth + atSize.X + 2f * scale, handleRight,
                    handleRowTop, rowHeight, ref editHandle, SocialProfilePages.HandleMax, ImGuiInputTextFlags.CharsNoBlank,
                    handleValid ? ChirperInk.TitleInk : ChirperInk.Danger))
            {
                editHandle = editHandle.ToLowerInvariant();
            }

            if (showAvailable)
            {
                var checkCenter = new Vector2(cardRight - innerPad - availableSize.X - 9f * scale, handleRowTop + rowHeight * 0.5f);
                PhoneIcon.Draw(listDrawList, checkCenter, PhoneIcons.Check,
                    ChirperInk.RechirpGreen, 13f * scale);
                Typography.Draw(listDrawList, new Vector2(cardRight - innerPad - availableSize.X, handleRowTop + (rowHeight - availableSize.Y) * 0.5f),
                    availableLabel, ChirperInk.RechirpGreen, EditHintStyle);
            }

            listDrawList.AddLine(new Vector2(cardMin.X, handleRowTop + rowHeight), new Vector2(cardRight, handleRowTop + rowHeight),
                ImGui.GetColorU32(EditRowHairline), 1f);

            var bioRowTop = handleRowTop + rowHeight;
            var bioLabelTop = bioRowTop + 12f * scale;
            Typography.Draw(listDrawList, new Vector2(cardMin.X + innerPad, bioLabelTop), Loc.T(L.Chirper.BioLabel), ChirperInk.MutedInk,
                EditLabelStyle);
            var counter = $"{editBio.Length.ToString(Loc.Culture)}/{SocialProfilePages.BioMax.ToString(Loc.Culture)}";
            var counterSize = Typography.Measure(counter, EditFootStyle);
            Typography.Draw(listDrawList, new Vector2(cardRight - innerPad - counterSize.X, bioLabelTop + (bioLabelHeight - counterSize.Y) * 0.5f),
                counter, ChirperInk.FaintInk, EditFootStyle);
            var bioFieldTop = bioLabelTop + bioLabelHeight + 5f * scale;
            var bioFieldWidth = cardRight - innerPad - (cardMin.X + innerPad);
            ImGui.SetCursorScreenPos(new Vector2(cardMin.X + innerPad, bioFieldTop));
            using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
            using (ImRaii.PushColor(ImGuiCol.Text, ChirperInk.TitleInk))
            {
                var wrapWidth = bioFieldWidth - ImGui.GetStyle().FramePadding.X * 2f - 4f * scale;
                SoftWrapField.Multiline("##chirperEditBio", ref editBio, SocialProfilePages.BioMax,
                    new Vector2(bioFieldWidth, bioFieldHeight), wrapWidth);
            }

            var footTop = cardMax.Y + 12f * scale;
            var footText = editStatus.Length > 0 ? editStatus : Loc.T(L.Chirper.HandleRules);
            Typography.DrawWrappedLeft(new Vector2(cardMin.X + 4f * scale, footTop), footText,
                editStatus.Length > 0 ? ChirperInk.Danger : ChirperInk.FaintInk, EditFootStyle, cardRight - cardMin.X - 8f * scale);
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, footTop + 60f * scale - origin.Y));
        }
    }

    private static void DrawEditLabel(ImDrawListPtr drawList, float left, float rowTop, float rowHeight, string label)
    {
        var size = Typography.Measure(label, EditLabelStyle);
        Typography.Draw(drawList, new Vector2(left, rowTop + (rowHeight - size.Y) * 0.5f), label, ChirperInk.MutedInk,
            EditLabelStyle);
    }

    private static bool DrawEditInput(string id, float left, float right, float rowTop, float rowHeight, ref string value,
        int maxLength, ImGuiInputTextFlags flags, Vector4 ink)
    {
        ImGui.SetCursorScreenPos(new Vector2(left, rowTop + rowHeight * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(MathF.Max(1f, right - left));
        Plugin.Fonts.NoticeText(value);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, ink))
        {
            return ImGui.InputText(id, ref value, maxLength, flags);
        }
    }

    private void SaveProfile()
    {
        if (!store.IsSignedIn || editBusy)
        {
            return;
        }

        if (!SocialProfilePages.IsHandleValid(editHandle) || string.IsNullOrWhiteSpace(editDisplay))
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
}
