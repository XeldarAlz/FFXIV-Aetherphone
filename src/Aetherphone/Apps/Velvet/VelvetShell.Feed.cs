using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Social;
using Aetherphone.Core.Translation;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private const float FabRadius = 27f;
    private const float FeedConnectWidth = 76f;
    private const float FeedConnectHeight = 28f;
    private const float FeedConnectGap = 8f;

    private readonly FeedVirtualizer feedVirtualizer = new(400f);
    private readonly Dictionary<string, string[]> feedTagLabels = new(StringComparer.Ordinal);
    private readonly HashSet<string> feedConnectedIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> feedRequestedIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> feedIncomingIds = new(StringComparer.Ordinal);
    private VelvetConnectionDto[] feedConnectionsSource = Array.Empty<VelvetConnectionDto>();
    private VelvetConnectionDto[] feedSentSource = Array.Empty<VelvetConnectionDto>();
    private VelvetConnectionDto[] feedRequestsSource = Array.Empty<VelvetConnectionDto>();
    private bool feedScrollTopPending;

    private void DrawFeed(Rect area)
    {
        var scale = UiScale.Current;
        if (!store.FeedLoaded && !store.LoadingFeed)
        {
            store.RefreshFeed();
        }

        SyncFeedRelations();
        using (var surface = AppSurface.BeginEdgeToEdge(area))
        {
            if (feedScrollTopPending)
            {
                surface.JumpToTop();
                feedScrollTopPending = false;
            }

            pullToRefresh.Draw(area, surface.Pull, surface.Dragging,
                store.LoadingFeed, VelvetTheme.MutedInk, RefreshFeedContent);

            stories.DrawTray(theme);
            var width = ScrollLayout.StableContentWidth();
            var feed = AllowedRegions(feedInclude) == 0 ? Array.Empty<VelvetPostDto>() : store.Feed;
            if (feed.Length == 0)
            {
                var emptyRect = new Rect(new Vector2(area.Min.X, ImGui.GetCursorScreenPos().Y), area.Max);
                var filtered = feedInclude.Any || mutes.Any;
                DrawEmpty(emptyRect, store.LoadingFeed ? Loc.T(L.Common.Loading) : Loc.T(L.Velvet.FeedNone),
                    store.LoadingFeed
                        ? string.Empty
                        : Loc.T(filtered ? L.Velvet.FeedNoneFiltered : L.Velvet.FeedNoneHint));
            }
            else
            {
                Gap(6f);
                feedVirtualizer.BeginFrame(store.FeedSource);
                for (var index = 0; index < feed.Length; index++)
                {
                    if (feedVirtualizer.Skip(feed[index].Id))
                    {
                        continue;
                    }

                    DrawPostCard(feed[index], width);
                    feedVirtualizer.Record(feed[index].Id);
                }

                if (store.HasMoreFeed && !store.LoadingMoreFeed &&
                    ImGui.GetScrollY() >= ImGui.GetScrollMaxY() - 400f * scale)
                {
                    store.LoadMoreFeed();
                }

                Gap(40f);
            }
        }

        if (ComposeFab.Draw(area, "velvetCompose", VelvetTheme.Rose, PhoneIcons.Plus,
                Loc.T(L.Velvet.Share), "velvet.compose", VelvetTheme.RoseDeep, FabRadius, true))
        {
            post.Open();
            router.Push(VelvetView.Compose);
        }
    }

    private void SyncFeedRelations()
    {
        if (!store.ConnectionsLoaded && !store.LoadingConnections)
        {
            store.RefreshConnections();
        }

        if (!store.SentRequestsLoaded && !store.LoadingSentRequests)
        {
            store.RefreshSentRequests();
        }

        SyncIdSet(ref feedConnectionsSource, store.Connections, feedConnectedIds);
        SyncIdSet(ref feedSentSource, store.SentRequests, feedRequestedIds);
        SyncIdSet(ref feedRequestsSource, store.Requests, feedIncomingIds);
    }

    private static void SyncIdSet(ref VelvetConnectionDto[] tracked, VelvetConnectionDto[] source,
        HashSet<string> ids)
    {
        if (ReferenceEquals(tracked, source))
        {
            return;
        }

        tracked = source;
        ids.Clear();
        for (var index = 0; index < source.Length; index++)
        {
            ids.Add(source[index].UserId);
        }
    }

    private bool CanConnectFromFeed(string ownerId) =>
        store.ConnectionsLoaded && store.Me is { } me && me.UserId != ownerId
        && !feedConnectedIds.Contains(ownerId) && !feedRequestedIds.Contains(ownerId)
        && !feedIncomingIds.Contains(ownerId);

    private void RefreshFeed()
    {
        if (!store.IsSignedIn || store.LoadingFeed)
        {
            return;
        }

        feedScrollTopPending = true;
        RefreshFeedContent();
    }

    private void RefreshFeedContent()
    {
        feedTagLabels.Clear();
        store.RefreshFeed();
        stories.RefreshTray();
    }

    private static string PostTimestamp(VelvetPostDto post)
    {
        var time = TimeText.Short(post.CreatedAtUnix);
        return post.EditedAtUnix is null ? time : Loc.T(L.Velvet.EditedStamp, time);
    }

    private string[] TagLabelsFor(VelvetPostDto entry)
    {
        if (entry.Tags.Length == 0)
        {
            return Array.Empty<string>();
        }

        if (feedTagLabels.TryGetValue(entry.Id, out var cached) && cached.Length == entry.Tags.Length)
        {
            return cached;
        }

        var labels = new string[entry.Tags.Length];
        for (var index = 0; index < entry.Tags.Length; index++)
        {
            labels[index] = "#" + VelvetTokenLabels.Of(entry.Tags[index]);
        }

        feedTagLabels[entry.Id] = labels;
        return labels;
    }

    private void StartStoryCompose()
    {
        post.Open(true);
        router.Push(VelvetView.Compose);
    }

    private const float CardActionInset = 12f;
    private const float CardActionGap = 18f;
    private const float CardCountGap = 6f;

    private static readonly TextStyle CardCountStyle = TextStyles.SubheadlineEmphasized;

    private enum CardActionTap
    {
        None,
        Icon,
        Count,
    }

    private static CardActionTap DrawCardAction(ImDrawListPtr drawList, ref float x, float centerY, string glyph,
        Vector4 ink, int count, string tooltip, string? countTooltip = null)
    {
        var scale = UiScale.Current;
        var iconSize = VIcon.CardAction * scale;
        var halfHeight = PostCardMetrics.ActionsHeight * scale * 0.5f;
        var label = count > 0 ? CountText.Compact(count) : string.Empty;
        var labelWidth = label.Length > 0 ? Typography.Measure(label, CardCountStyle).X : 0f;
        var contentWidth = iconSize + (label.Length > 0 ? CardCountGap * scale + labelWidth : 0f);
        var min = new Vector2(x - 6f * scale, centerY - halfHeight);
        var max = new Vector2(x + contentWidth + 6f * scale, centerY + halfHeight);
        var splitCount = countTooltip is not null && label.Length > 0;
        var iconMax = splitCount ? new Vector2(x + iconSize + CardCountGap * scale * 0.5f, max.Y) : max;
        var countMin = new Vector2(iconMax.X, min.Y);
        var iconHovered = UiInteract.Hover(min, iconMax);
        var countHovered = splitCount && UiInteract.Hover(countMin, max);
        if (iconHovered || countHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        PhoneIcon.Draw(drawList, new Vector2(x + iconSize * 0.5f, centerY), glyph, ink, iconSize);
        if (label.Length > 0)
        {
            var labelSize = Typography.Measure(label, CardCountStyle);
            Typography.Draw(drawList, new Vector2(x + iconSize + CardCountGap * scale, centerY - labelSize.Y * 0.5f),
                label, VelvetTheme.TitleInk, CardCountStyle);
        }

        HoverTooltip.Show(new Rect(min, iconMax), tooltip, HoverLabelSide.Above);
        if (splitCount)
        {
            HoverTooltip.Show(new Rect(countMin, max), countTooltip!, HoverLabelSide.Above);
        }

        x += contentWidth + CardActionGap * scale;
        if (UiInteract.Click(min, iconMax, iconHovered))
        {
            return CardActionTap.Icon;
        }

        if (splitCount && UiInteract.Click(countMin, max, countHovered))
        {
            return CardActionTap.Count;
        }

        return CardActionTap.None;
    }

    private void DrawPostCard(VelvetPostDto entry, float width)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var padY = PostCardMetrics.PadY * scale;
        var inset = FeedCell.PadX * scale;
        var innerWidth = width - inset * 2f;
        var headerBlock = PostCardMetrics.HeaderBlock * scale;
        var avatarRadius = PostCardMetrics.AvatarRadius * scale;
        var mediaHeight = PostAspects.TallDisplayHeight(width, entry.MediaWidth, entry.MediaHeight);
        var actionsHeight = PostCardMetrics.ActionsHeight * scale;
        RichTextLayout? captionLayout = null;
        var translateKey = new TranslationKey(TranslationSurface.Post, entry.Id);
        var captionView = translation.View(translateKey, entry.Caption, entry.Lang);
        var captionText = captionView.Text;
        if (captionText.Length > 0)
        {
            using (Plugin.Fonts.Push(TextStyles.Callout.Scale, TextStyles.Callout.Weight))
            {
                captionLayout = feedCaptionLayouts.LayoutFor(captionView.LayoutKey, captionText, entry.Mentions,
                    innerWidth);
            }
        }

        var captionTextHeight = captionText.Length == 0
            ? 0f
            : captionLayout?.Size.Y ?? Typography.MeasureWrappedBlock(captionText, TextStyles.Callout, innerWidth).Y;
        var translateHeight = TranslateLink.Height(translation, translateKey, entry.Lang, scale);
        var captionHeight = captionText.Length == 0
            ? 0f
            : captionTextHeight + translateHeight + PostCardMetrics.CaptionGap * scale;
        var tagLabels = TagLabelsFor(entry);
        var tagsHeight = VTagRun.Height(tagLabels, innerWidth, scale);
        var cellHeight = padY + headerBlock + PostCardMetrics.MediaGap * scale + mediaHeight
            + PostCardMetrics.ActionsGap * scale + actionsHeight + PostCardMetrics.TextGap * scale
            + captionHeight + tagsHeight + padY;
        var cell = FeedCell.Begin(drawList, cellHeight, VelvetTheme.HoverWash, interactive: false);
        var origin = cell.Bounds.Min;
        var innerX = origin.X + inset;
        var imageTop = origin.Y + padY + headerBlock + PostCardMetrics.MediaGap * scale;
        var imageBottom = imageTop + mediaHeight;
        var actionsTop = imageBottom + PostCardMetrics.ActionsGap * scale;
        var textTop = actionsTop + actionsHeight + PostCardMetrics.TextGap * scale;

        var authorName = DisplayNameOf(entry.OwnerDisplayName, entry.OwnerHandle);
        var avatarCenter = new Vector2(innerX + avatarRadius, origin.Y + padY + avatarRadius);
        var ringRadius = avatarRadius + 3f * scale;
        var hasStory = stories.TryRing(entry.OwnerId, out var authorRing);
        if (hasStory)
        {
            VelvetArt.StoryRing(drawList, avatarCenter, ringRadius, scale, authorRing.HasUnseen);
        }

        VAvatar.Draw(drawList, avatarCenter, hasStory ? avatarRadius - 1f * scale : avatarRadius, theme, authorName,
            string.Empty, entry.OwnerAvatarUrl, images, lodestone, -1, null, Frames.Of(entry.OwnerFrameId));
        var nameLeft = avatarCenter.X + avatarRadius + PostCardMetrics.NameGap * scale;
        var headerTextRight = origin.X + width - inset - 34f * scale;
        var connectable = CanConnectFromFeed(entry.OwnerId);
        var connectRect = default(Rect);
        if (connectable)
        {
            var connectHalf = FeedConnectHeight * scale * 0.5f;
            connectRect = new Rect(
                new Vector2(headerTextRight - FeedConnectWidth * scale, avatarCenter.Y - connectHalf),
                new Vector2(headerTextRight, avatarCenter.Y + connectHalf));
            headerTextRight = connectRect.Min.X - FeedConnectGap * scale;
        }

        var headerTextMaxWidth = MathF.Max(1f, headerTextRight - nameLeft);
        var nameTop = origin.Y + padY;
        var nameSize = Typography.Measure(authorName, TextStyles.Headline);
        var nameHovering = UiInteract.Hover(new Vector2(nameLeft, nameTop),
            new Vector2(nameLeft + headerTextMaxWidth, nameTop + nameSize.Y));
        UserName.Draw("velvet.feed.author." + entry.Id, authorName, entry.OwnerBadges, entry.OwnerBadgeIds, nameLeft, nameTop,
            headerTextMaxWidth, TextStyles.Headline, VelvetTheme.TitleInk, nameHovering, false);
        var ownerSub = SocialIdentity.FeedMeta(entry.OwnerHandle, PostTimestamp(entry));
        var ownerSubY = nameTop + PostCardMetrics.SublineTop * scale;
        var ownerSubSize = Typography.Measure(ownerSub, TextStyles.Subheadline);
        var ownerSubHovering = UiInteract.Hover(new Vector2(nameLeft, ownerSubY),
            new Vector2(nameLeft + headerTextMaxWidth, ownerSubY + ownerSubSize.Y));
        Marquee.DrawLeft(new MarqueeId("velvet.feed.ownersub.", entry.Id), ownerSub, nameLeft, ownerSubY,
            headerTextMaxWidth, TextStyles.Subheadline, VelvetTheme.MutedInk, ownerSubHovering);
        var overRing = hasStory &&
            (ImGui.GetMousePos() - avatarCenter).LengthSquared() <= ringRadius * ringRadius;
        if (hasStory && UiInteract.HoverClickCircle(avatarCenter, ringRadius))
        {
            stories.OpenRing(authorRing);
        }
        else if (!overRing && UiInteract.Click(new Vector2(innerX, nameTop),
                     new Vector2(headerTextRight, nameTop + headerBlock)))
        {
            OpenProfile(entry.OwnerId);
        }

        var moreCenter = new Vector2(origin.X + width - inset - 6f * scale, avatarCenter.Y);
        var moreRadius = 14f * scale;
        if (VIcon.Button(moreCenter, moreRadius, PhoneIcons.Dots, VIcon.Overflow, VelvetTheme.BodyInk,
                Loc.T(L.Velvet.More)))
        {
            OpenPostSheet(entry, true);
        }

        if (connectable && SocialPill.Outline(drawList, connectRect, Loc.T(L.Velvet.Connect), VelvetInk.Shared,
                TextStyles.FootnoteEmphasized, connectRect.Height * 0.5f, VelvetInk.Shared.ButtonFill))
        {
            RequestIntro(entry.OwnerId, entry.OwnerDisplayName, entry.OwnerHandle, entry.OwnerAvatarUrl);
        }

        var photos = PostMedia.Photos(entry.MediaUrls, entry.MediaUrl);
        var result = DrawPostCarousel(drawList,
            new Rect(new Vector2(origin.X, imageTop), new Vector2(origin.X + width, imageBottom)), entry, photos,
            0f);
        if (result.Tapped && !UiInteract.InputBlocked)
        {
            OpenPostDetail(entry.Id);
        }

        var actionCenterY = actionsTop + actionsHeight * 0.5f;
        var liked = entry.MyReaction >= 0;
        var actionX = innerX + CardActionInset * scale - VIcon.CardAction * scale * 0.5f;
        var likeTap = DrawCardAction(drawList, ref actionX, actionCenterY,
            liked ? PhoneIcons.HeartFilled : PhoneIcons.Heart, liked ? VelvetInk.Shared.LikeRed : VelvetTheme.TitleInk,
            entry.TotalReactions, Loc.T(L.Velvet.Like), Loc.T(L.Velvet.LikesTitle));
        if (likeTap == CardActionTap.Icon)
        {
            store.ToggleReaction(entry, 0);
        }
        else if (likeTap == CardActionTap.Count)
        {
            OpenLikers(entry.Id);
        }

        if (DrawCardAction(drawList, ref actionX, actionCenterY, PhoneIcons.MessageCircle, VelvetTheme.TitleInk,
                entry.CommentCount, Loc.T(L.Velvet.Comments)) != CardActionTap.None)
        {
            OpenPostDetail(entry.Id);
        }

        var actionsRight = actionX;

        if (photos.Length > 1)
        {
            var dotsLeft = actionsRight + 10f * scale;
            var dotsRight = origin.X + width - inset;
            var dotsCenter = new Vector2((dotsLeft + dotsRight) * 0.5f, actionCenterY);
            PhotoCarousel.DrawDots(drawList, dotsCenter, photos.Length, result.Index,
                MathF.Max(0f, dotsRight - dotsLeft), VelvetTheme.BodyInk);
        }

        var lineY = textTop;
        if (captionText.Length > 0)
        {
            var captionOrigin = new Vector2(innerX, lineY);
            if (captionLayout is null)
            {
                Typography.DrawWrappedLeft(captionOrigin, captionText, VelvetTheme.BodyInk, TextStyles.Callout,
                    innerWidth);
            }
            else
            {
                using (Plugin.Fonts.Push(TextStyles.Callout.Scale, TextStyles.Callout.Weight))
                {
                    DrawRichBody(drawList, captionLayout, captionOrigin);
                }
            }

            if (translateHeight > 0f)
            {
                TranslateLink.Draw(translation, confirm, translateKey, entry.Lang, entry.Caption,
                    new Vector2(innerX, lineY + captionTextHeight), innerWidth, VelvetTheme.MutedInk,
                    VelvetTheme.RoseGlow, scale);
            }

            lineY += captionHeight;
        }

        if (tagLabels.Length > 0)
        {
            var tappedTag = VTagRun.Draw(drawList, new Vector2(innerX, lineY), innerWidth, tagLabels,
                VelvetTheme.RoseInk, VelvetTheme.RoseGlow, scale);
            if (tappedTag >= 0)
            {
                OpenTagPosts(entry.Tags[tappedTag]);
            }
        }

        FeedCell.End(drawList, cell, VelvetTheme.Hairline);
    }

    private void OpenPostDetail(string postId)
    {
        store.EnsurePost(postId);
        router.Push(VelvetView.PostDetail(postId));
    }

    private CarouselResult DrawPostCarousel(ImDrawListPtr drawList, Rect rect, VelvetPostDto entry, string[] photos,
        float rounding)
    {
        var scanStatus = entry.ScanStatus;
        var veiled = SensitiveReveals.ShouldVeil(entry.Sensitive, entry.Id, configuration.ShowSensitiveContent);
        var result = carousel.Draw(drawList, rect, entry.Id, photos, rounding,
            (list, min, max, radius, url) => DrawMedia(list, min, max, url ?? string.Empty, radius, scanStatus,
                veiled));
        if (!veiled || !result.Tapped)
        {
            return result;
        }

        SensitiveReveals.Reveal(entry.Id);
        return result with { Tapped = false };
    }

    private void DrawMedia(ImDrawListPtr drawList, Vector2 min, Vector2 max, string url, float rounding,
        string? scanStatus = null, bool veiled = false)
    {
        if (veiled)
        {
            SensitiveVeil.Draw(drawList, min, max, rounding);
            return;
        }

        var texture = images.Get(url);
        if (texture is null)
        {
            VMediaTile.Placeholder(drawList, min, max, rounding);
            Typography.DrawCentered(new Vector2((min.X + max.X) * 0.5f, (min.Y + max.Y) * 0.5f),
                images.Failed(url) ? Loc.T(L.Velvet.ImageUnavailable) : Loc.T(L.Common.Loading), VelvetTheme.MutedInk,
                TextStyles.Footnote);
        }
        else
        {
            var (uv0, uv1) = ImageFit.Cover(texture.Size.X, texture.Size.Y, max.X - min.X, max.Y - min.Y);
            drawList.AddImageRounded(texture.Handle, min, max, uv0, uv1, 0xFFFFFFFFu, rounding,
                ImDrawFlags.RoundCornersAll);
        }

        ModerationOverlay.Draw(drawList, min, max, rounding, scanStatus);
    }

    private void DrawCompose(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        var result = post.Draw(area, ui, context);
        if (result == VelvetComposeResult.Posted)
        {
            RefreshFeed();
        }

        if (result != VelvetComposeResult.Open)
        {
            router.Pop();
        }
    }
}
