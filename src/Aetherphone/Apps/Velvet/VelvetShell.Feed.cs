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
            var inset = FeedCell.PadX * scale;
            Gap(4f);
            var scopeRow = Inset(Reserve(34f), inset);
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
            DrawActiveFilters(width - inset * 2f, VelvetPage.Feed, inset);

            var feed = store.Feed;
            if (feed.Length == 0)
            {
                var emptyY = ImGui.GetCursorScreenPos().Y + 60f * scale;
                var message = store.LoadingFeed ? Loc.T(L.Common.Loading) : Loc.T(L.Velvet.FeedNone);
                Typography.DrawCentered(new Vector2(area.Center.X, emptyY), message, VelvetTheme.TitleInk,
                    TextStyles.Headline);
                if (!store.LoadingFeed)
                {
                    var filtered = feedInclude.Any || feedExclude.Any || mutes.Any;
                    Typography.DrawCentered(new Vector2(area.Center.X, emptyY + 26f * scale),
                        Loc.T(filtered ? L.Velvet.FeedNoneFiltered : L.Velvet.FeedNoneHint), VelvetTheme.MutedInk,
                        TextStyles.Subheadline);
                }
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

        if (ComposeFab.Draw(area, "velvetCompose", VelvetTheme.Rose, IconGlyph.Of(FontAwesomeIcon.Plus),
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
        var padY = PostCardMetrics.PadY * scale;
        var inset = FeedCell.PadX * scale;
        var innerWidth = width - inset * 2f;
        var headerBlock = PostCardMetrics.HeaderBlock * scale;
        var avatarRadius = PostCardMetrics.AvatarRadius * scale;
        var mediaHeight = PostAspects.DisplayHeight(width, entry.MediaWidth, entry.MediaHeight);
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
        var tagsLine = entry.Tags.Length > 0 ? "#" + string.Join("  #", entry.Tags) : string.Empty;
        var tagsHeight = tagsLine.Length == 0
            ? 0f
            : Typography.MeasureWrappedBlock(tagsLine, TextStyles.Footnote, innerWidth).Y;
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
        var headerTextMaxWidth = MathF.Max(1f, headerTextRight - nameLeft);
        var nameTop = origin.Y + padY;
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
        if (ui.IconButton(moreCenter, moreRadius, IconGlyph.Of(FontAwesomeIcon.EllipsisH), VelvetTheme.BodyInk,
                AppSkin.Transparent, 1f, Loc.T(L.Velvet.More)))
        {
            OpenPostSheet(entry, true);
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
        var iconRadius = PostCardMetrics.ActionIconRadius * scale;
        var countTop = actionCenterY - 8f * scale;
        var liked = entry.MyReaction >= 0;
        var heartCenter = new Vector2(innerX + PostCardMetrics.ActionIconInset * scale, actionCenterY);
        if (ui.IconButton(heartCenter, iconRadius, IconGlyph.Of(FontAwesomeIcon.Heart),
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
        if (ui.IconButton(commentCenter, iconRadius, IconGlyph.Of(FontAwesomeIcon.Comment), VelvetTheme.BodyInk,
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

        if (tagsLine.Length > 0)
        {
            Typography.DrawWrappedLeft(new Vector2(innerX, lineY), tagsLine, VelvetTheme.RoseInk, TextStyles.Footnote,
                innerWidth);
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
                contain: true, veiled));
        if (!veiled || !result.Tapped)
        {
            return result;
        }

        SensitiveReveals.Reveal(entry.Id);
        return result with { Tapped = false };
    }

    // The profile grid leaves contain false: it wants its forced square cover crop, like Instagram's.
    private void DrawMedia(ImDrawListPtr drawList, Vector2 min, Vector2 max, string url, float rounding,
        string? scanStatus = null, bool contain = false, bool veiled = false)
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
        else if (contain)
        {
            ImageFit.DrawLetterboxed(drawList, texture, new Rect(min, max), Vector2.Zero, Vector2.One, rounding);
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
