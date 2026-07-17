using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private readonly FeedVirtualizer feedVirtualizer = new(400f);
    private float sinceFeedRefresh;

    private void DrawFeed(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        if (!store.FeedLoaded && !store.LoadingFeed)
        {
            store.RefreshFeed();
        }

        sinceFeedRefresh += ImGui.GetIO().DeltaTime;
        if (store.FeedLoaded && !store.LoadingFeed && sinceFeedRefresh >= SocialProfilePages.FeedRefreshSeconds)
        {
            sinceFeedRefresh = 0f;
            store.RefreshFeed();
        }

        using (AppSurface.Begin(area))
        {
            stories.DrawTray(theme);
            var width = ImGui.GetContentRegionAvail().X;
            var feed = store.Feed;
            if (feed.Length == 0)
            {
                var emptyY = ImGui.GetCursorScreenPos().Y + 60f * scale;
                var message = store.LoadingFeed ? Loc.T(L.Common.Loading) : Loc.T(L.Velvet.FeedNone);
                Typography.DrawCentered(new Vector2(area.Center.X, emptyY), message, VelvetTheme.TitleInk,
                    TextStyles.Headline);
                if (!store.LoadingFeed)
                {
                    Typography.DrawCentered(new Vector2(area.Center.X, emptyY + 26f * scale),
                        Loc.T(L.Velvet.FeedNoneHint), VelvetTheme.MutedInk, TextStyles.Subheadline);
                }
            }
            else
            {
                Gap(10f);
                feedVirtualizer.BeginFrame();
                for (var index = 0; index < feed.Length; index++)
                {
                    if (feedVirtualizer.Skip(feed[index].Id))
                    {
                        continue;
                    }

                    DrawPostCard(feed[index], width);
                    Gap(30f);
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

    private void StartStoryCompose()
    {
        post.Open(true);
        router.Push(VelvetView.Compose);
    }

    private void DrawPostCard(VelvetPostDto entry, float width)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var innerPad = Metrics.Space.Lg * scale;
        var headerHeight = 52f * scale;
        var imageSize = width - innerPad * 2f;
        var actionsHeight = 44f * scale;
        var captionHeight = entry.Caption.Length > 0 ? 22f * scale : 0f;
        var tagsHeight = entry.Tags.Length > 0 ? 20f * scale : 0f;
        var totalScaled = headerHeight + 8f * scale + imageSize + actionsHeight + captionHeight + tagsHeight +
            12f * scale;

        var card = Reserve(totalScaled / scale);
        var drawList = ImGui.GetWindowDrawList();
        VCard.Draw(drawList, card.Min, card.Max, Metrics.Radius.Card * scale, VCardStyle.Plain);

        var headerCenterY = card.Min.Y + 6f * scale + 20f * scale;
        var avatarRadius = 16f * scale;
        var avatarCenter = new Vector2(card.Min.X + innerPad + avatarRadius, headerCenterY);
        var authorName = DisplayNameOf(entry.OwnerDisplayName, entry.OwnerHandle);
        var ringRadius = avatarRadius + 3f * scale;
        var hasStory = stories.TryRing(entry.OwnerId, out var authorRing);
        if (hasStory)
        {
            VelvetArt.StoryRing(drawList, avatarCenter, ringRadius, scale, authorRing.HasUnseen);
        }

        VAvatar.Draw(drawList, avatarCenter, hasStory ? avatarRadius - 1f * scale : avatarRadius, theme, authorName,
            string.Empty, entry.OwnerAvatarUrl, images, lodestone, -1);
        var textLeft = avatarCenter.X + avatarRadius + 10f * scale;
        Typography.Draw(new Vector2(textLeft, headerCenterY - 14f * scale), authorName, VelvetTheme.TitleInk,
            TextStyles.Headline);
        Typography.Draw(new Vector2(textLeft, headerCenterY + 2f * scale),
            "@" + entry.OwnerHandle + " · " + TimeText.Short(entry.CreatedAtUnix), VelvetTheme.MutedInk,
            TextStyles.Subheadline);

        var headerHitMin = new Vector2(card.Min.X, card.Min.Y);
        var headerHitMax = new Vector2(card.Max.X - 44f * scale, card.Min.Y + headerHeight);
        if (hasStory && UiInteract.HoverClickCircle(avatarCenter, ringRadius))
        {
            stories.OpenRing(authorRing);
        }
        else if (UiInteract.Hover(headerHitMin, headerHitMax) && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            OpenProfile(entry.OwnerId);
        }

        var imageMin = new Vector2(card.Min.X + innerPad, card.Min.Y + headerHeight + 8f * scale);
        var imageMax = new Vector2(imageMin.X + imageSize, imageMin.Y + imageSize);
        var photos = PostMedia.Photos(entry.MediaUrls, entry.MediaUrl);
        var result = DrawPostCarousel(drawList, new Rect(imageMin, imageMax), entry, photos,
            Metrics.Radius.Md * scale);
        if (result.Tapped)
        {
            store.EnsurePost(entry.Id);
            router.Push(VelvetView.PostDetail(entry.Id));
        }

        var actionY = imageMax.Y + 22f * scale;
        var liked = entry.MyReaction >= 0;
        var heartCenter = new Vector2(card.Min.X + innerPad + 10f * scale, actionY);
        if (ui.IconButton(heartCenter, 13f * scale, FontAwesomeIcon.Heart.ToIconString(),
                liked ? VelvetTheme.Rose : VelvetTheme.MutedInk, AppSkin.Transparent, 0.95f))
        {
            store.ToggleReaction(entry, 0);
        }

        Typography.Draw(new Vector2(heartCenter.X + 16f * scale, actionY - 8f * scale), entry.TotalReactions.ToString(),
            VelvetTheme.BodyInk, TextStyles.Subheadline);
        var commentCenter = new Vector2(heartCenter.X + 62f * scale, actionY);
        if (ui.IconButton(commentCenter, 13f * scale, FontAwesomeIcon.Comment.ToIconString(), VelvetTheme.MutedInk,
                AppSkin.Transparent, 0.9f))
        {
            store.EnsurePost(entry.Id);
            router.Push(VelvetView.PostDetail(entry.Id));
        }

        var commentText = entry.CommentCount.ToString(Loc.Culture);
        Typography.Draw(new Vector2(commentCenter.X + 16f * scale, actionY - 8f * scale), commentText,
            VelvetTheme.BodyInk, TextStyles.Subheadline);
        if (photos.Length > 1)
        {
            var actionsRight = commentCenter.X + 16f * scale + Typography.Measure(commentText, TextStyles.Subheadline).X;
            var dotsCenter = new Vector2(card.Center.X, actionY);
            var available = MathF.Min((card.Max.X - innerPad - dotsCenter.X) * 2f,
                (dotsCenter.X - actionsRight - 10f * scale) * 2f);
            PhotoCarousel.DrawDots(drawList, dotsCenter, photos.Length, result.Index, available, VelvetTheme.BodyInk);
        }

        var lineY = actionY + 16f * scale;
        if (entry.Caption.Length > 0)
        {
            var caption = Typography.FitText(entry.Caption, imageSize, TextStyles.Callout);
            Typography.Draw(new Vector2(card.Min.X + innerPad, lineY), caption, VelvetTheme.BodyInk, TextStyles.Callout);
            lineY += 22f * scale;
        }

        if (entry.Tags.Length > 0)
        {
            var tags = "#" + string.Join("  #", entry.Tags);
            var fitted = Typography.FitText(tags, imageSize, TextStyles.Footnote);
            Typography.Draw(new Vector2(card.Min.X + innerPad, lineY), fitted, VelvetTheme.RoseInk, TextStyles.Footnote);
        }
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
            var (uv0, uv1) = ImageFit.CoverSquare(texture.Size);
            drawList.AddImageRounded(texture.Handle, min, max, uv0, uv1, 0xFFFFFFFFu, rounding,
                ImDrawFlags.RoundCornersAll);
        }

        ModerationOverlay.Draw(drawList, min, max, rounding, scanStatus);
    }

    private void DrawCompose(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        if (post.Draw(area, ui, context))
        {
            router.Pop();
        }
    }
}
