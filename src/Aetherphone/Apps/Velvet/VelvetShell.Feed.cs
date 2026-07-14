using System.Numerics;
using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private void DrawFeed(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        if (!store.FeedLoaded && !store.LoadingFeed)
        {
            store.RefreshFeed();
        }

        using (AppSurface.Begin(area))
        {
            var width = ImGui.GetContentRegionAvail().X;
            var feed = store.Feed;
            if (feed.Length == 0)
            {
                var message = store.LoadingFeed ? Loc.T(L.Common.Loading) : Loc.T(L.Velvet.FeedNone);
                Typography.DrawCentered(new Vector2(area.Center.X, area.Min.Y + 90f * scale), message,
                    VelvetTheme.TitleInk, TextStyles.Headline);
                if (!store.LoadingFeed)
                {
                    Typography.DrawCentered(new Vector2(area.Center.X, area.Min.Y + 116f * scale),
                        Loc.T(L.Velvet.FeedNoneHint), VelvetTheme.MutedInk, TextStyles.Subheadline);
                }
            }
            else
            {
                Gap(10f);
                for (var index = 0; index < feed.Length; index++)
                {
                    DrawPostCard(feed[index], width);
                    Gap(30f);
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
        var avatarCenter = new Vector2(card.Min.X + innerPad + 16f * scale, headerCenterY);
        var authorName = DisplayNameOf(entry.OwnerDisplayName, entry.OwnerHandle);
        VAvatar.Draw(drawList, avatarCenter, 16f * scale, theme, authorName, string.Empty, entry.OwnerAvatarUrl, images,
            lodestone, -1);
        var textLeft = avatarCenter.X + 16f * scale + 10f * scale;
        Typography.Draw(new Vector2(textLeft, headerCenterY - 14f * scale), authorName, VelvetTheme.TitleInk,
            TextStyles.Headline);
        Typography.Draw(new Vector2(textLeft, headerCenterY + 2f * scale),
            "@" + entry.OwnerHandle + " · " + TimeText.Short(entry.CreatedAtUnix), VelvetTheme.MutedInk,
            TextStyles.Subheadline);

        var headerHitMin = new Vector2(card.Min.X, card.Min.Y);
        var headerHitMax = new Vector2(card.Max.X - 44f * scale, card.Min.Y + headerHeight);
        if (UiInteract.Hover(headerHitMin, headerHitMax) && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            OpenProfile(entry.OwnerId);
        }

        var imageMin = new Vector2(card.Min.X + innerPad, card.Min.Y + headerHeight + 8f * scale);
        var imageMax = new Vector2(imageMin.X + imageSize, imageMin.Y + imageSize);
        DrawMedia(drawList, imageMin, imageMax, entry.MediaUrl, Metrics.Radius.Md * scale);
        if (UiInteract.Hover(imageMin, imageMax) && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
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

        Typography.Draw(new Vector2(commentCenter.X + 16f * scale, actionY - 8f * scale), entry.CommentCount.ToString(),
            VelvetTheme.BodyInk, TextStyles.Subheadline);

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

    private void DrawMedia(ImDrawListPtr drawList, Vector2 min, Vector2 max, string url, float rounding)
    {
        var texture = images.Get(url);
        if (texture is null)
        {
            VMediaTile.Placeholder(drawList, min, max, rounding);
            Typography.DrawCentered(new Vector2((min.X + max.X) * 0.5f, (min.Y + max.Y) * 0.5f),
                images.Failed(url) ? Loc.T(L.Velvet.ImageUnavailable) : Loc.T(L.Common.Loading), VelvetTheme.MutedInk,
                TextStyles.Footnote);
            return;
        }

        var (uv0, uv1) = ImageFit.CoverSquare(texture.Size);
        drawList.AddImageRounded(texture.Handle, min, max, uv0, uv1, 0xFFFFFFFFu, rounding, ImDrawFlags.RoundCornersAll);
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
