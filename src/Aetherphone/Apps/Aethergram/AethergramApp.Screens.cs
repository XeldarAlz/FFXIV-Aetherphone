using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Aethergram;

internal sealed partial class AethergramApp
{
    private enum ActivityBucket
    {
        None,
        Today,
        Week,
        Month,
        Earlier,
    }

    private const float ActivityAvatarRadius = 22f;
    private const float ActivityBadgeRadius = 9f;
    private const float ActivityBadgeRimFraction = 0.70711f;
    private const float ActivityRowPadY = 12f;
    private const float ActivityRequestRowHeight = 64f;
    private const float ActivityRequestChipSize = 40f;
    private const float ActivityLoadOlderMargin = 300f;
    private const float RequestPillWidth = 76f;
    private const float RequestPillGap = 8f;
    private const long SecondsPerDay = 86400L;
    private const long WeekSeconds = 7L * SecondsPerDay;
    private const long MonthSeconds = 30L * SecondsPerDay;

    private static readonly TextStyle ActivityActorStyle = TextStyles.Headline;
    private static readonly TextStyle ActivityBodyStyle = TextStyles.Subheadline;
    private static readonly TextStyle ActivityTimeStyle = TextStyles.Footnote;
    private static readonly TextStyle ActivitySectionStyle = TextStyles.FootnoteEmphasized;
    private static readonly Vector4 ActivityUnreadWash = Palette.WithAlpha(AethergramInk.Shared.Accent, 0.06f);
    private static readonly Vector4 ActivityBadgeRing = new(0f, 0f, 0f, 0.55f);

    private void DrawActivity(Rect area)
    {
        var scale = UiScale.Current;
        DrawScreenHeader(area, Loc.T(L.Social.ActivityTitle));
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        activityFeed.EnsureFresh(social.Latest);
        store.EnsureMe();
        store.EnsureFollowRequests();
        var items = activityFeed.Items;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var shown = 0;
        var bucket = ActivityBucket.None;
        using (AppSurface.BeginEdgeToEdge(body))
        {
            var requestCount = store.PendingFollowRequestCount;
            if (requestCount > 0)
            {
                DrawFollowRequestsRow(requestCount);
            }

            for (var index = 0; index < items.Length; index++)
            {
                if (!ShowsActivity(items[index]))
                {
                    continue;
                }

                var itemBucket = BucketFor(items[index].CreatedAtUnix, now);
                if (itemBucket != bucket)
                {
                    bucket = itemBucket;
                    SocialChrome.DrawSectionLabel(Loc.T(BucketLabel(bucket)), Ink, ActivitySectionStyle);
                }

                DrawActivityRow(items[index]);
                shown++;
            }

            if (shown == 0)
            {
                Typography.DrawWrappedCentered(new Vector2(body.Center.X, body.Min.Y + 90f * scale),
                    Loc.T(L.Social.ActivityEmpty), Ink.MutedInk, TextStyles.Callout, body.Width - 64f * scale);
                return;
            }

            ImGui.Dummy(new Vector2(0f, 16f * scale));
            if (ImGui.GetScrollY() >= ImGui.GetScrollMaxY() - ActivityLoadOlderMargin * scale)
            {
                loadOlderActivity();
            }
        }
    }

    private bool ShowsActivity(NotificationDto item) =>
        item.App == Id && !SocialActivity.IsModerationNotice(item.Type);

    private static ActivityBucket BucketFor(long createdAtUnix, long now)
    {
        if (TimeText.SameLocalDay(createdAtUnix, now))
        {
            return ActivityBucket.Today;
        }

        var age = now - createdAtUnix;
        if (age < WeekSeconds)
        {
            return ActivityBucket.Week;
        }

        return age < MonthSeconds ? ActivityBucket.Month : ActivityBucket.Earlier;
    }

    private static LocString BucketLabel(ActivityBucket bucket) => bucket switch
    {
        ActivityBucket.Today => L.Aethergram.ActivityToday,
        ActivityBucket.Week => L.Aethergram.ActivityThisWeek,
        ActivityBucket.Month => L.Aethergram.ActivityThisMonth,
        _ => L.Aethergram.ActivityEarlier,
    };

    private void OpenActivity()
    {
        social.MarkSeen(Id);
        social.RefreshNow();
        activityFeed.Invalidate();
        store.RefreshFollowRequests();
        router.Push(AethergramRoute.Activity);
    }

    private void DrawFollowRequestsRow(int count)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var cell = FeedCell.Begin(drawList, ActivityRequestRowHeight * scale, Ink.HoverTint);
        var chipHalf = ActivityRequestChipSize * 0.5f * scale;
        var chipCenter = new Vector2(cell.Bounds.Min.X + CellPadX * scale + chipHalf, cell.Bounds.Center.Y);
        Squircle.Fill(drawList, chipCenter - new Vector2(chipHalf, chipHalf), chipCenter + new Vector2(chipHalf, chipHalf),
            chipHalf * 0.5f, ImGui.GetColorU32(Ink.Accent));
        PhoneIcon.Draw(drawList, chipCenter, PhoneIcons.UserPlus, Ink.White, 22f * scale);
        var chevronCenter = new Vector2(cell.Bounds.Max.X - CellPadX * scale - 8f * scale, cell.Bounds.Center.Y);
        PhoneIcon.Draw(drawList, chevronCenter, PhoneIcons.ChevronRight, Ink.MutedInk, 18f * scale);
        var countText = count.ToString(Loc.Culture);
        var countSize = Typography.Measure(countText, TextStyles.Subheadline);
        var countLeft = chevronCenter.X - 14f * scale - countSize.X;
        Typography.Draw(drawList, new Vector2(countLeft, cell.Bounds.Center.Y - countSize.Y * 0.5f), countText,
            Ink.MutedInk, TextStyles.Subheadline);
        var labelLeft = chipCenter.X + chipHalf + 12f * scale;
        var label = Typography.FitText(Loc.T(L.Social.FollowRequests), MathF.Max(1f, countLeft - 10f * scale - labelLeft),
            ActivityActorStyle);
        var labelSize = Typography.Measure(label, ActivityActorStyle);
        Typography.Draw(drawList, new Vector2(labelLeft, cell.Bounds.Center.Y - labelSize.Y * 0.5f), label,
            Ink.TitleInk, ActivityActorStyle);
        if (cell.Tapped)
        {
            OpenFollowRequests();
        }

        FeedCell.End(drawList, cell, Ink.Hairline);
    }

    private void DrawActivityRow(NotificationDto item)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var padX = CellPadX * scale;
        var padY = ActivityRowPadY * scale;
        var radius = ActivityAvatarRadius * scale;
        var timeText = TimeText.Short(item.CreatedAtUnix);
        var timeSize = Typography.Measure(timeText, ActivityTimeStyle);
        var textLeft = origin.X + padX + radius * 2f + 12f * scale;
        var textRight = origin.X + width - padX - timeSize.X - 12f * scale;
        var textWidth = MathF.Max(1f, textRight - textLeft);
        var actor = SocialActivity.ActorLabel(item);
        var body = SocialActivity.Body(item);
        var actorHeight = Typography.LineHeight(ActivityActorStyle);
        var bodyHeight = body.Length > 0 ? EmojiText.BlockHeight(body, ActivityBodyStyle, textWidth) : 0f;
        var contentHeight = actorHeight + (bodyHeight > 0f ? 2f * scale + bodyHeight : 0f);
        var rowHeight = MathF.Max(radius * 2f, contentHeight) + padY * 2f;
        var cell = FeedCell.Begin(drawList, rowHeight, Ink.HoverTint);
        var rowMax = cell.Bounds.Max;
        if (!item.Read)
        {
            drawList.AddRectFilled(origin, rowMax, ImGui.GetColorU32(ActivityUnreadWash));
        }

        var avatarCenter = new Vector2(origin.X + padX + radius, origin.Y + rowHeight * 0.5f);
        DrawAvatar(avatarCenter, radius, actor, string.Empty, item.ActorAvatarUrl, 0.95f, 32,
            Frames.Of(item.ActorFrameId));
        var badgeOffset = radius * ActivityBadgeRimFraction;
        DrawActivityBadge(drawList, avatarCenter + new Vector2(badgeOffset, badgeOffset), item.Type, scale);
        var textTop = origin.Y + (rowHeight - contentHeight) * 0.5f;
        var actorWidth = UserName.DrawAuto(drawList, "aethergram.activity.actor." + item.Id, actor, item.ActorBadges,
            item.ActorBadgeIds, textLeft, textTop, textWidth, ActivityActorStyle, Ink.TitleInk, theme);
        var actorMin = new Vector2(textLeft, textTop);
        var actorMax = new Vector2(textLeft + actorWidth, textTop + actorHeight);
        if (UiInteract.Hover(actorMin, actorMax))
        {
            drawList.AddLine(new Vector2(actorMin.X, actorMax.Y - 1f * scale),
                new Vector2(actorMax.X, actorMax.Y - 1f * scale), ImGui.GetColorU32(Ink.TitleInk), 1f);
        }

        if (bodyHeight > 0f)
        {
            EmojiText.DrawBlock(new Vector2(textLeft, textTop + actorHeight + 2f * scale), body, Ink.BodyInk,
                ActivityBodyStyle, textWidth);
        }

        Typography.Draw(drawList, new Vector2(origin.X + width - padX - timeSize.X, textTop + 1f * scale), timeText,
            Ink.FaintInk, ActivityTimeStyle);
        if (!item.Read)
        {
            SocialChrome.DrawUnreadDot(drawList,
                new Vector2(origin.X + width - padX - 4f * scale, textTop + timeSize.Y + 12f * scale), Ink);
        }

        var avatarExtent = new Vector2(radius, radius);
        var avatarTapped = UiInteract.HoverClick(avatarCenter - avatarExtent, avatarCenter + avatarExtent);
        var actorTapped = UiInteract.HoverClick(actorMin, actorMax);
        if (avatarTapped || actorTapped)
        {
            openActivityActor(item);
        }
        else if (cell.Tapped)
        {
            if (SocialActivity.OpensPost(item))
            {
                openActivityPost(item);
            }
            else
            {
                openActivityActor(item);
            }
        }

        FeedCell.End(drawList, cell, Ink.Hairline);
    }

    private static void DrawActivityBadge(ImDrawListPtr drawList, Vector2 center, int type, float scale)
    {
        var radius = ActivityBadgeRadius * scale;
        var iconSize = radius * 1.15f;
        drawList.AddCircleFilled(center, radius + 1.6f * scale, ImGui.GetColorU32(ActivityBadgeRing), 20);
        switch (type)
        {
            case SocialActivity.TypeLike:
            case SocialActivity.TypeCommentLike:
                drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Ink.LikeRed), 20);
                PhoneIcon.Draw(drawList, center, PhoneIcons.HeartFilled, Ink.White, iconSize);
                break;
            case SocialActivity.TypeComment:
                drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Ink.AccentLink), 20);
                PhoneIcon.Draw(drawList, center, PhoneIcons.MessageCircleFilled, Ink.White, iconSize);
                break;
            case SocialActivity.TypeMention:
            case SocialActivity.TypeCommentMention:
                drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Ink.AccentLink), 20);
                Typography.DrawCentered(drawList, center, "@", Ink.White, TextStyles.Caption2);
                break;
            case SocialActivity.TypePhotoTag:
                drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Ink.AccentLink), 20);
                PhoneIcon.Draw(drawList, center, PhoneIcons.UserSquareRounded, Ink.White, iconSize);
                break;
            case SocialActivity.TypeFollow:
            case SocialActivity.TypeFollowRequest:
            case SocialActivity.TypeFollowAccept:
            case SocialActivity.TypeConnectRequest:
            case SocialActivity.TypeConnectAccept:
                drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Ink.AccentLink), 20);
                PhoneIcon.Draw(drawList, center, PhoneIcons.Plus, Ink.White, iconSize);
                break;
            default:
                drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Ink.MutedInk), 20);
                PhoneIcon.Draw(drawList, center, PhoneIcons.Bell, Ink.White, iconSize);
                break;
        }
    }

    private void OpenFollowRequests()
    {
        store.RefreshFollowRequests();
        router.Push(AethergramRoute.FollowRequests);
    }

    private void DrawFollowRequests(Rect area)
    {
        DrawScreenHeader(area, Loc.T(L.Social.FollowRequests));
        var scale = UiScale.Current;
        var listRect = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        var snapshot = store.FollowRequests;
        using (AppSurface.BeginEdgeToEdge(listRect))
        {
            if (snapshot.Length == 0)
            {
                var message = store.FollowRequestsLoading ? Loc.T(L.Common.Loading) : Loc.T(L.Social.ListEmpty);
                Typography.DrawCentered(new Vector2(listRect.Center.X, listRect.Min.Y + 60f * scale), message,
                    Ink.MutedInk);
                return;
            }

            ImGui.Dummy(new Vector2(0f, 4f * scale));
            for (var index = 0; index < snapshot.Length; index++)
            {
                DrawFollowRequestRow(snapshot[index]);
            }

            if (store.FollowRequestsLoadingMore)
            {
                InfiniteScroll.DrawLoadingRow(listRect.Center.X, Ink.MutedInk);
            }
            else if (store.HasMoreFollowRequests && InfiniteScroll.ReachedBottom())
            {
                store.LoadMoreFollowRequests();
            }

            ImGui.Dummy(new Vector2(0f, 12f * scale));
        }
    }

    private void DrawFollowRequestRow(UserDto user)
    {
        var scale = UiScale.Current;
        var row = DrawUserRow(user, RequestPillWidth * 2f + RequestPillGap);
        var pillWidth = RequestPillWidth * scale;
        var confirmRect = new Rect(row.Trailing.Min, new Vector2(row.Trailing.Min.X + pillWidth, row.Trailing.Max.Y));
        var deleteRect = new Rect(new Vector2(row.Trailing.Max.X - pillWidth, row.Trailing.Min.Y), row.Trailing.Max);
        if (DrawAccentPill(confirmRect, Loc.T(L.Social.Confirm)))
        {
            store.AcceptFollowRequest(user);
        }

        if (DrawGrayPill(deleteRect, Loc.T(L.Social.Delete)))
        {
            store.DeclineFollowRequest(user);
        }

        if (row.Tapped)
        {
            OpenProfile(user.Id);
        }
    }
}
