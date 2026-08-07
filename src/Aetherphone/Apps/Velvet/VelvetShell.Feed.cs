using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private readonly FeedVirtualizer feedVirtualizer = new(400f);
    private bool feedScrollTopPending;

    private void DrawFeed(Rect area)
    {
        var scale = UiScale.Current;
        if (!store.FeedLoaded && !store.LoadingFeed)
        {
            store.RefreshFeed();
        }

        using (var surface = AppSurface.Begin(area))
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
            Gap(4f);
            var scopeRow = Reserve(34f);
            var filterSize = 34f * scale;
            var filterGap = 8f * scale;
            var scopeRect = new Rect(scopeRow.Min,
                new Vector2(scopeRow.Max.X - filterSize - filterGap, scopeRow.Max.Y));
            var filterRect = new Rect(new Vector2(scopeRow.Max.X - filterSize, scopeRow.Min.Y), scopeRow.Max);
            var activeScope = (int)store.FeedScope;
            var pickedScope = VSegmented.Draw("velvetFeedScope", scopeRect,
                new[] { Loc.T(L.Velvet.FeedScopeAll), Loc.T(L.Velvet.FeedScopeConnections) }, activeScope, scale);
            if (pickedScope >= 0 && pickedScope != activeScope)
            {
                store.SetFeedScope((VelvetFeedScope)pickedScope);
                feedScrollTopPending = true;
            }

            DrawFilterButton(filterRect, VelvetPage.Feed);
            Gap(6f);
            DrawActiveFilters(width, VelvetPage.Feed);

            var feed = store.Feed;
            if (feed.Length == 0)
            {
                var emptyY = ImGui.GetCursorScreenPos().Y + 60f * scale;
                var message = store.LoadingFeed ? Loc.T(L.Common.Loading) : Loc.T(L.Velvet.FeedNone);
                Typography.DrawCentered(new Vector2(area.Center.X, emptyY), message, VelvetTheme.TitleInk,
                    TextStyles.Headline);
                if (!store.LoadingFeed)
                {
                    var filtered = feedInclude.Any || mutes.Any;
                    Typography.DrawCentered(new Vector2(area.Center.X, emptyY + 26f * scale),
                        Loc.T(filtered ? L.Velvet.FeedNoneFiltered : L.Velvet.FeedNoneHint), VelvetTheme.MutedInk,
                        TextStyles.Subheadline);
                }
            }
            else
            {
                Gap(10f);
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

        if (ComposeFab.Draw(area, "velvetCompose", VelvetTheme.Rose, FontAwesomeIcon.Plus.ToIconString(),
                Loc.T(L.Velvet.Share), "velvet.compose"))
        {
            post.Open();
            router.Push(VelvetView.Compose);
        }
    }

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
        store.RefreshFeed();
        stories.RefreshTray();
    }

    private void StartStoryCompose()
    {
        post.Open(true);
        router.Push(VelvetView.Compose);
    }

    private void DrawPostCard(VelvetPostDto entry, float width)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var pad = PostCardMetrics.Pad * scale;
        var innerX = origin.X + pad;
        var innerWidth = width - pad * 2f;
        var headerBlock = PostCardMetrics.HeaderBlock * scale;
        var avatarRadius = PostCardMetrics.AvatarRadius * scale;
        var imageTop = origin.Y + pad + headerBlock + PostCardMetrics.MediaGap * scale;
        var imageBottom = imageTop + PostAspects.DisplayHeight(innerWidth, entry.MediaWidth, entry.MediaHeight);
        var actionsTop = imageBottom + PostCardMetrics.ActionsGap * scale;
        var actionsHeight = PostCardMetrics.ActionsHeight * scale;
        var textTop = actionsTop + actionsHeight + PostCardMetrics.TextGap * scale;
        RichTextLayout? captionLayout = null;
        if (entry.Caption.Length > 0)
        {
            using (Plugin.Fonts.Push(TextStyles.Callout.Scale, TextStyles.Callout.Weight))
            {
                captionLayout = feedCaptionLayouts.LayoutFor(entry.Id, entry.Caption, entry.Mentions, innerWidth);
            }
        }

        var captionHeight = entry.Caption.Length == 0
            ? 0f
            : (captionLayout?.Size.Y ??
                Typography.MeasureWrappedBlock(entry.Caption, TextStyles.Callout, innerWidth).Y) +
              PostCardMetrics.CaptionGap * scale;
        var tagsLine = entry.Tags.Length > 0 ? "#" + string.Join("  #", entry.Tags) : string.Empty;
        var tagsHeight = tagsLine.Length == 0
            ? 0f
            : Typography.MeasureWrappedBlock(tagsLine, TextStyles.Footnote, innerWidth).Y;
        var cardBottom = textTop + captionHeight + tagsHeight + pad;
        VCard.Draw(drawList, origin, new Vector2(origin.X + width, cardBottom), PostCardMetrics.Rounding * scale,
            VCardStyle.Plain);

        var authorName = DisplayNameOf(entry.OwnerDisplayName, entry.OwnerHandle);
        var avatarCenter = new Vector2(innerX + avatarRadius, origin.Y + pad + avatarRadius);
        var ringRadius = avatarRadius + 3f * scale;
        var hasStory = stories.TryRing(entry.OwnerId, out var authorRing);
        if (hasStory)
        {
            VelvetArt.StoryRing(drawList, avatarCenter, ringRadius, scale, authorRing.HasUnseen);
        }

        VAvatar.Draw(drawList, avatarCenter, hasStory ? avatarRadius - 1f * scale : avatarRadius, theme, authorName,
            string.Empty, entry.OwnerAvatarUrl, images, lodestone, -1);
        var nameLeft = avatarCenter.X + avatarRadius + PostCardMetrics.NameGap * scale;
        var headerTextRight = origin.X + width - pad - 34f * scale;
        var headerTextMaxWidth = MathF.Max(1f, headerTextRight - nameLeft);
        var nameTop = origin.Y + pad;
        var nameSize = Typography.Measure(authorName, TextStyles.Headline);
        var nameHovering = UiInteract.Hover(new Vector2(nameLeft, nameTop),
            new Vector2(nameLeft + headerTextMaxWidth, nameTop + nameSize.Y));
        UserName.Draw("velvet.feed.author." + entry.Id, authorName, entry.OwnerBadges, entry.OwnerBadgeIds, nameLeft, nameTop,
            headerTextMaxWidth, TextStyles.Headline, VelvetTheme.TitleInk, nameHovering, false);
        var ownerSub = SocialIdentity.FeedMeta(entry.OwnerHandle, TimeText.Short(entry.CreatedAtUnix));
        var ownerSubY = nameTop + PostCardMetrics.SublineTop * scale;
        var ownerSubSize = Typography.Measure(ownerSub, TextStyles.Subheadline);
        var ownerSubHovering = UiInteract.Hover(new Vector2(nameLeft, ownerSubY),
            new Vector2(nameLeft + headerTextMaxWidth, ownerSubY + ownerSubSize.Y));
        Marquee.DrawLeft("velvet.feed.ownersub." + entry.Id, ownerSub, nameLeft, ownerSubY,
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

        var moreCenter = new Vector2(origin.X + width - pad - 6f * scale, avatarCenter.Y);
        var moreRadius = 14f * scale;
        if (ui.IconButton(moreCenter, moreRadius, FontAwesomeIcon.EllipsisH.ToIconString(), VelvetTheme.BodyInk,
                AppSkin.Transparent, 1f, Loc.T(L.Velvet.More)))
        {
            menuPost = entry;
            postMenu.Toggle(entry.Id, new Rect(moreCenter - new Vector2(moreRadius, moreRadius),
                moreCenter + new Vector2(moreRadius, moreRadius)));
        }

        var photos = PostMedia.Photos(entry.MediaUrls, entry.MediaUrl);
        var result = DrawPostCarousel(drawList,
            new Rect(new Vector2(innerX, imageTop), new Vector2(innerX + innerWidth, imageBottom)), entry, photos,
            PostCardMetrics.MediaRounding * scale);
        if (result.Tapped && !UiInteract.InputBlocked)
        {
            OpenPostDetail(entry.Id);
        }

        var actionCenterY = actionsTop + actionsHeight * 0.5f;
        var iconRadius = PostCardMetrics.ActionIconRadius * scale;
        var countTop = actionCenterY - 8f * scale;
        var liked = entry.MyReaction >= 0;
        var heartCenter = new Vector2(innerX + PostCardMetrics.ActionIconInset * scale, actionCenterY);
        if (ui.IconButton(heartCenter, iconRadius, FontAwesomeIcon.Heart.ToIconString(),
                liked ? VelvetTheme.Rose : VelvetTheme.BodyInk, AppSkin.Transparent, 1.25f))
        {
            store.ToggleReaction(entry, 0);
        }

        var cursorX = heartCenter.X + PostCardMetrics.ActionCountGap * scale;
        if (entry.TotalReactions > 0)
        {
            var likeText = entry.TotalReactions.ToString(Loc.Culture);
            Typography.Draw(new Vector2(cursorX, countTop), likeText, VelvetTheme.BodyInk, TextStyles.SubheadlineEmphasized);
            cursorX += Typography.Measure(likeText, TextStyles.SubheadlineEmphasized).X + 14f * scale;
        }
        else
        {
            cursorX += 6f * scale;
        }

        var commentCenter = new Vector2(cursorX + 6f * scale, actionCenterY);
        if (ui.IconButton(commentCenter, iconRadius, FontAwesomeIcon.Comment.ToIconString(), VelvetTheme.BodyInk,
                AppSkin.Transparent, 1.2f))
        {
            OpenPostDetail(entry.Id);
        }

        var actionsRight = commentCenter.X + PostCardMetrics.ActionCountGap * scale;
        if (entry.CommentCount > 0)
        {
            var commentText = entry.CommentCount.ToString(Loc.Culture);
            Typography.Draw(new Vector2(actionsRight, countTop), commentText, VelvetTheme.BodyInk,
                TextStyles.SubheadlineEmphasized);
            actionsRight += Typography.Measure(commentText, TextStyles.SubheadlineEmphasized).X;
        }

        if (photos.Length > 1)
        {
            var dotsLeft = actionsRight + 10f * scale;
            var dotsRight = origin.X + width - pad;
            var dotsCenter = new Vector2((dotsLeft + dotsRight) * 0.5f, actionCenterY);
            PhotoCarousel.DrawDots(drawList, dotsCenter, photos.Length, result.Index,
                MathF.Max(0f, dotsRight - dotsLeft), VelvetTheme.BodyInk);
        }

        var lineY = textTop;
        if (entry.Caption.Length > 0)
        {
            var captionOrigin = new Vector2(innerX, lineY);
            if (captionLayout is null)
            {
                Typography.DrawWrappedLeft(captionOrigin, entry.Caption, VelvetTheme.BodyInk, TextStyles.Callout,
                    innerWidth);
            }
            else
            {
                using (Plugin.Fonts.Push(TextStyles.Callout.Scale, TextStyles.Callout.Weight))
                {
                    DrawRichBody(drawList, captionLayout, captionOrigin);
                }
            }

            lineY += captionHeight;
        }

        if (tagsLine.Length > 0)
        {
            Typography.DrawWrappedLeft(new Vector2(innerX, lineY), tagsLine, VelvetTheme.RoseInk, TextStyles.Footnote,
                innerWidth);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cardBottom - origin.Y + PostCardMetrics.CardGap * scale));
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
        return carousel.Draw(drawList, rect, entry.Id, photos, rounding,
            (list, min, max, radius, url) => DrawMedia(list, min, max, url ?? string.Empty, radius, scanStatus));
    }

    private void DrawMedia(ImDrawListPtr drawList, Vector2 min, Vector2 max, string url, float rounding,
        string? scanStatus = null)
    {
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
