using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Aethergram;

// The post detail view: comments, comment composer, the profile view, and moderation actions
// (report / delete). Split from the main feed for readability.
internal sealed partial class AethergramApp
{
    private void DrawDetail(Rect area, string postId)
    {
        var post = store.DetailPost;
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Aethergram.PostTitle), back);
        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        if (post is null || post.Id != postId)
        {
            Typography.DrawCentered(new Vector2(area.Center.X, top + 60f * scale), Loc.T(L.Common.Loading),
                AppPalettes.Aethergram.MutedInk);
            return;
        }

        var composerHeight = 54f * scale;
        var body = new Rect(new Vector2(area.Min.X, top), new Vector2(area.Max.X, area.Max.Y - composerHeight));
        using (AppSurface.Begin(body))
        {
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var headerHeight = 52f * scale;
            var avatarRadius = 20f * scale;
            var avatarCenter = new Vector2(origin.X + avatarRadius + 6f * scale, origin.Y + headerHeight * 0.5f);
            if (stories.TryRing(post.AuthorId, out var authorRing))
            {
                AethergramArt.StoryRing(ImGui.GetWindowDrawList(), avatarCenter, avatarRadius + 3f * scale, scale,
                    authorRing.HasUnseen);
            }

            DrawAvatar(avatarCenter, avatarRadius - 1f * scale, post.AuthorName, post.AuthorWorld, post.AuthorAvatarUrl,
                0.85f, 32);
            var nameLeft = avatarCenter.X + avatarRadius + 12f * scale;
            var displayName = SocialIdentity.Name(post.AuthorDisplayName, post.AuthorHandle);
            var headerMeta = SocialIdentity.FeedMeta(post.AuthorHandle, TimeText.Short(post.CreatedAtUnix));
            var headerNameSize = Typography.Measure(displayName, 0.95f, FontWeight.SemiBold);
            var headerMetaSize = Typography.Measure(headerMeta, 0.78f);
            var headerTextGap = 3f * scale;
            var headerNameY = avatarCenter.Y - (headerNameSize.Y + headerTextGap + headerMetaSize.Y) * 0.5f;
            Typography.Draw(new Vector2(nameLeft, headerNameY), displayName, theme.TextStrong, 0.95f,
                FontWeight.SemiBold);
            Typography.Draw(new Vector2(nameLeft, headerNameY + headerNameSize.Y + headerTextGap), headerMeta,
                AppPalettes.Aethergram.MutedInk, 0.78f);
            if (UiInteract.HoverClick(new Vector2(origin.X, origin.Y),
                    new Vector2(origin.X + width * 0.7f, origin.Y + headerHeight)))
            {
                OpenProfile(post.AuthorId);
            }

            ImGui.SetCursorScreenPos(new Vector2(origin.X, origin.Y + headerHeight));
            var imageRect = new Rect(new Vector2(origin.X, origin.Y + headerHeight),
                new Vector2(origin.X + width, origin.Y + headerHeight + width));
            var photos = PostMedia.Photos(post.MediaUrls, post.MediaUrl);
            var page = DrawGramCarousel(imageRect, post, photos, 16f * scale);
            var liked = post.MyReaction >= 0;
            var actionsY = imageRect.Max.Y + 22f * scale;
            var heartCenter = new Vector2(origin.X + 13f * scale, actionsY);
            if (ui.IconButton(heartCenter, 15f * scale, FontAwesomeIcon.Heart.ToIconString(),
                    liked ? CommentHeart.LikeRed : AppPalettes.Aethergram.BodyInk, AppSkin.Transparent, 1.25f, Loc.T(L.Aethergram.Like)))
            {
                store.ToggleLike(post);
            }

            var actionCursorX = heartCenter.X + 20f * scale;
            if (post.TotalReactions > 0)
            {
                var likeText = post.TotalReactions.ToString(Loc.Culture);
                var likeSize = Typography.Measure(likeText, 0.9f, FontWeight.Medium);
                var likePos = new Vector2(actionCursorX, actionsY - 8f * scale);
                var likeHovered = UiInteract.Hover(likePos, likePos + likeSize);
                Typography.Draw(likePos, likeText, likeHovered ? theme.Accent : AppPalettes.Aethergram.BodyInk, 0.9f,
                    FontWeight.Medium);
                if (likeHovered)
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                    if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                    {
                        OpenUserList(post.Id, UserListKind.Likers);
                    }
                }

                actionCursorX += likeSize.X + 16f * scale;
            }
            else
            {
                actionCursorX += 6f * scale;
            }

            var commentCenter = new Vector2(actionCursorX + 13f * scale, actionsY);
            if (ui.IconButton(commentCenter, 15f * scale, FontAwesomeIcon.Comment.ToIconString(), AppPalettes.Aethergram.BodyInk,
                    AppSkin.Transparent, 1.2f, Loc.T(L.Aethergram.Comment)))
            {
                commentFocusPending = true;
            }

            var actionsRight = commentCenter.X + 20f * scale;
            if (post.CommentCount > 0)
            {
                var commentText = post.CommentCount.ToString(Loc.Culture);
                Typography.Draw(new Vector2(actionsRight, actionsY - 8f * scale), commentText,
                    AppPalettes.Aethergram.BodyInk, 0.9f, FontWeight.Medium);
                actionsRight += Typography.Measure(commentText, 0.9f, FontWeight.Medium).X;
            }

            var moreCenter = new Vector2(origin.X + width - 14f * scale, actionsY);
            if (photos.Length > 1)
            {
                var dotsCenter = new Vector2(origin.X + width * 0.5f, actionsY);
                var available = MathF.Min((moreCenter.X - 16f * scale - dotsCenter.X) * 2f,
                    (dotsCenter.X - actionsRight - 10f * scale) * 2f);
                PhotoCarousel.DrawDots(ImGui.GetWindowDrawList(), dotsCenter, photos.Length, page, available,
                    AppPalettes.Aethergram.BodyInk);
            }

            var moreRadius = 14f * scale;
            if (ui.IconButton(moreCenter, moreRadius, FontAwesomeIcon.EllipsisH.ToIconString(),
                    AppPalettes.Aethergram.BodyInk, AppSkin.Transparent, 1f, Loc.T(L.Aethergram.More)))
            {
                menuPost = post;
                postMenu.Toggle(post.Id, new Rect(moreCenter - new Vector2(moreRadius, moreRadius),
                    moreCenter + new Vector2(moreRadius, moreRadius)));
            }

            ImGui.SetCursorScreenPos(new Vector2(origin.X, actionsY + 20f * scale));
            if (post.Text.Length > 0)
            {
                var captionPos = ImGui.GetCursorScreenPos();
                Typography.Draw(captionPos, displayName, theme.TextStrong, 0.9f, FontWeight.SemiBold);
                var nameWidth = Typography.Measure(displayName, 0.9f, FontWeight.SemiBold).X;
                var captionLeft = captionPos.X + nameWidth + 6f * scale;
                ImGui.SetCursorScreenPos(new Vector2(captionLeft, captionPos.Y));
                RichTextLayout? captionLayout;
                using (Plugin.Fonts.Push(0.9f))
                {
                    captionLayout = detailBodyLayouts.LayoutFor(post.Id, post.Text, post.Mentions,
                        origin.X + width - captionLeft);
                }

                if (captionLayout is null)
                {
                    ImGui.PushTextWrapPos(origin.X + width - ImGui.GetWindowPos().X);
                    using (ImRaii.PushColor(ImGuiCol.Text, AppPalettes.Aethergram.BodyInk))
                    using (Plugin.Fonts.Push(0.9f))
                    {
                        Typography.Wrapped(post.Text);
                    }

                    ImGui.PopTextWrapPos();
                }
                else
                {
                    var captionOrigin = new Vector2(captionLeft, captionPos.Y);
                    using (Plugin.Fonts.Push(0.9f))
                    {
                        DrawRichBody(ImGui.GetWindowDrawList(), captionLayout, captionOrigin);
                    }

                    ImGui.SetCursorScreenPos(captionOrigin);
                    ImGui.Dummy(captionLayout.Size);
                }

                ImGui.Dummy(new Vector2(0f, 4f * scale));
            }

            ImGui.Dummy(new Vector2(0f, 12f * scale));
            var linePos = ImGui.GetCursorScreenPos();
            ImGui.GetWindowDrawList().AddLine(linePos, new Vector2(linePos.X + width, linePos.Y),
                ImGui.GetColorU32(theme.Separator), 1f);
            ImGui.Dummy(new Vector2(0f, 14f * scale));
            var comments = store.DetailComments;
            ui.SectionHeading(comments.Length > 0
                ? $"{Loc.T(L.Aethergram.CommentsTitle)} · {comments.Length}"
                : Loc.T(L.Aethergram.CommentsTitle));
            if (comments.Length == 0 && !store.DetailLoading)
            {
                Typography.Draw(ImGui.GetCursorScreenPos(), Loc.T(L.Aethergram.NoComments), AppPalettes.Aethergram.MutedInk,
                    0.85f);
            }
            else
            {
                for (var index = 0; index < comments.Length; index++)
                {
                    DrawComment(comments[index]);
                }
            }

            ImGui.Dummy(new Vector2(0f, 16f * scale));
        }

        DrawCommentComposer(new Rect(new Vector2(area.Min.X, area.Max.Y - composerHeight), area.Max), area, postId);
        DrawPostMenu(area, false);
    }

    private void DrawComment(CommentDto comment)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var avatarRadius = 20f * scale;
        var avatarCenterX = origin.X + avatarRadius + 5f * scale;
        var mine = store.Me is { } me && me.Id == comment.AuthorId;

        var bubbleLeft = avatarCenterX + avatarRadius + 11f * scale;
        var bubbleRight = origin.X + width;
        var padX = 13f * scale;
        var padTop = 10f * scale;
        var padBottom = 11f * scale;
        var textLeft = bubbleLeft + padX;
        var textRight = bubbleRight - padX - 22f * scale;
        var displayName = SocialIdentity.Name(comment.AuthorDisplayName, comment.AuthorHandle);
        var nameHeight = Typography.Measure(displayName, 0.9f, FontWeight.SemiBold).Y;
        RichTextLayout? commentLayout;
        using (Plugin.Fonts.Push(0.9f))
        {
            commentLayout = commentLayouts.LayoutFor(comment.Id, comment.Text, comment.Mentions, textRight - textLeft);
        }

        var textHeight = commentLayout?.Size.Y ?? Typography.MeasureWrapped(comment.Text, textRight - textLeft, 0.9f);
        var bubbleHeight = padTop + nameHeight + 4f * scale + textHeight + padBottom;
        var bubbleTop = origin.Y;
        var bubbleBottom = bubbleTop + bubbleHeight;
        var bubbleMin = new Vector2(bubbleLeft, bubbleTop);
        var bubbleMax = new Vector2(bubbleRight, bubbleBottom);
        var bubbleRadius = 15f * scale;

        drawList.AddRectFilled(bubbleMin + new Vector2(0f, 2f * scale), bubbleMax + new Vector2(0f, 2f * scale),
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.18f)), bubbleRadius);
        Squircle.Fill(drawList, bubbleMin, bubbleMax, bubbleRadius, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.055f)));
        Squircle.Stroke(drawList, bubbleMin, bubbleMax, bubbleRadius, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.09f)),
            1f);

        var avatarCenter = new Vector2(avatarCenterX, bubbleTop + avatarRadius + 2f * scale);
        DrawAvatar(avatarCenter, avatarRadius, comment.AuthorName, string.Empty, comment.AuthorAvatarUrl,
            0.8f, 28);

        var nameTop = bubbleTop + padTop;
        Typography.Draw(new Vector2(textLeft, nameTop), displayName, theme.TextStrong, 0.9f, FontWeight.SemiBold);
        var nameWidth = Typography.Measure(displayName, 0.9f, FontWeight.SemiBold).X;
        var meta = TimeText.Short(comment.CreatedAtUnix);
        var metaSize = Typography.Measure(meta, 0.8f);
        var metaLeft = textLeft + nameWidth + 8f * scale;
        var metaRightBound = mine ? textRight - 14f * scale : textRight;
        if (metaLeft + metaSize.X <= metaRightBound)
        {
            Typography.Draw(new Vector2(metaLeft, nameTop + (nameHeight - metaSize.Y) * 0.5f), meta,
                AppPalettes.Aethergram.MutedInk, 0.8f);
        }

        var textTop = nameTop + nameHeight + 4f * scale;
        ImGui.SetCursorScreenPos(new Vector2(textLeft, textTop));
        if (commentLayout is null)
        {
            ImGui.PushTextWrapPos(textRight - ImGui.GetWindowPos().X);
            using (ImRaii.PushColor(ImGuiCol.Text, AppPalettes.Aethergram.BodyInk))
            using (Plugin.Fonts.Push(0.9f))
            {
                Typography.Wrapped(comment.Text);
            }

            ImGui.PopTextWrapPos();
        }
        else
        {
            using (Plugin.Fonts.Push(0.9f))
            {
                DrawRichBody(drawList, commentLayout, new Vector2(textLeft, textTop));
            }
        }
        if (UiInteract.HoverClick(new Vector2(origin.X, bubbleTop), new Vector2(textLeft + nameWidth, textTop)))
        {
            OpenProfile(comment.AuthorId);
        }

        if (mine)
        {
            var trashCenter = new Vector2(bubbleRight - 13f * scale, bubbleTop + 13f * scale);
            if (ui.IconButton(trashCenter, 11f * scale, FontAwesomeIcon.Times.ToIconString(), AppPalettes.Aethergram.MutedInk,
                    AppSkin.Transparent, 0.85f, Loc.T(L.Aethergram.DeleteComment)) && store.DetailPost is { } post)
            {
                profile.AskDeleteComment(post.Id, comment.Id);
            }
        }

        var heartCenter = new Vector2(bubbleRight - 16f * scale, (bubbleTop + bubbleBottom) * 0.5f);
        if (mine)
        {
            heartCenter.Y = MathF.Max(heartCenter.Y, bubbleTop + 36f * scale);
        }

        if (CommentHeart.Draw(ui, heartCenter, comment.Liked, comment.LikeCount, AppPalettes.Aethergram.MutedInk,
                AppPalettes.Aethergram.MutedInk, Loc.T(L.Aethergram.Like), out _))
        {
            store.ToggleCommentLike(comment);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, bubbleHeight + 11f * scale));
    }

    private void DrawCommentComposer(Rect bar, Rect screen, string postId)
    {
        var style = new CommentComposerStyle(new Vector4(1f, 1f, 1f, 0.10f), AppPalettes.Aethergram.FieldSurface,
            AppPalettes.Aethergram.TitleInk, Accent, theme.SurfaceMuted, new Vector4(1f, 1f, 1f, 1f), true, 9f, 54f,
            0.8f);
        if (CommentComposerBar.Draw(bar, screen, ui, theme, style, "##gramComment", Loc.T(L.Aethergram.AddComment),
                ref commentDraft, MaxCommentLength, commentMentions, mentionPopup, images, lodestone, store.Commenting,
                ref commentFocusPending, commentEmoji))
        {
            var text = commentDraft;
            commentDraft = string.Empty;
            store.AddComment(postId, text, _ => { });
        }
    }

    private void DrawProfile(Rect area, string userId)
    {
        if (store.ProfileUserId != userId)
        {
            store.OpenProfile(userId);
        }

        var user = store.ProfileUser;
        var title = user is null
            ? DisplayName
            : SocialIdentity.Name(user.DisplayName, user.Handle);
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, title, back);
        var scale = ImGuiHelpers.GlobalScale;
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
            Typography.DrawCentered(body.Center, Loc.T(L.Aethergram.ProfileError), AppPalettes.Aethergram.MutedInk);
            return;
        }

        var user = store.ProfileUser;
        if (user is null)
        {
            Typography.DrawCentered(body.Center, Loc.T(L.Common.Loading), AppPalettes.Aethergram.MutedInk);
            return;
        }

        using (AppSurface.Begin(body))
        {
            profile.DrawProfileHeader(user, theme);
            var scale = ImGuiHelpers.GlobalScale;
            var tabRow = new Rect(
                new Vector2(ImGui.GetCursorScreenPos().X + 14f * scale, ImGui.GetCursorScreenPos().Y + 4f * scale),
                new Vector2(ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X - 14f * scale,
                    ImGui.GetCursorScreenPos().Y + 32f * scale));
            for (var index = 0; index < ProfileTabs.Length; index++)
            {
                profileTabLabels[index] = Loc.T(ProfileTabs[index]);
            }

            profileTab = SegmentStrip.Draw("aethergram.profileTabs", tabRow, profileTabLabels, profileTab,
                AppPalettes.Aethergram);
            ImGui.SetCursorScreenPos(new Vector2(ImGui.GetCursorScreenPos().X, tabRow.Max.Y + 10f * scale));
            if (profileTab == 0)
            {
                DrawProfileGrid();
            }
            else
            {
                store.EnsureTaggedPosts(userId);
                DrawProfileGrid(store.TaggedPosts, L.PhotoTag.NoTagged);
            }
        }
    }

}
