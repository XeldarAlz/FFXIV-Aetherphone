using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private string commentsPostId = string.Empty;
    private string commentDraft = string.Empty;

    private void DrawPostDetail(Rect area, string postId)
    {
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Velvet.Post), back);
        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        var feed = store.Feed;
        VelvetPostDto? found = null;
        for (var index = 0; index < feed.Length; index++)
        {
            if (feed[index].Id == postId)
            {
                found = feed[index];
                break;
            }
        }

        if (found is null && store.FetchedPost is { } fetched && fetched.Id == postId)
        {
            found = fetched;
        }

        if (found is not { } post)
        {
            store.EnsurePost(postId);
            Typography.DrawCentered(body.Center, Loc.T(L.Common.Loading), VelvetTheme.MutedInk);
            return;
        }

        if (commentsPostId != postId)
        {
            commentsPostId = postId;
            commentDraft = string.Empty;
            store.OpenComments(postId);
        }

        var composerHeight = 52f * scale;
        body = new Rect(body.Min, new Vector2(body.Max.X, body.Max.Y - composerHeight));
        using (AppSurface.Begin(body))
        {
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var drawList = ImGui.GetWindowDrawList();
            var headerHeight = 48f * scale;
            var avatarRadius = 18f * scale;
            var avatarCenter = new Vector2(origin.X + avatarRadius, origin.Y + headerHeight * 0.5f);
            var authorName = DisplayNameOf(post.OwnerDisplayName, post.OwnerHandle);
            VAvatar.Draw(drawList, avatarCenter, avatarRadius, theme, authorName, string.Empty, post.OwnerAvatarUrl,
                images, lodestone, -1);
            var nameLeft = avatarCenter.X + avatarRadius + 10f * scale;
            var ownerSub = post.OwnerHandle.Length > 0 ? "@" + post.OwnerHandle : string.Empty;
            var ownerTime = TimeText.Short(post.CreatedAtUnix);
            if (ownerTime.Length > 0)
            {
                ownerSub = ownerSub.Length > 0 ? ownerSub + " · " + ownerTime : ownerTime;
            }

            Typography.Draw(new Vector2(nameLeft, avatarCenter.Y - 13f * scale), authorName, VelvetTheme.TitleInk,
                TextStyles.Headline);
            Typography.Draw(new Vector2(nameLeft, avatarCenter.Y + 3f * scale), ownerSub, VelvetTheme.MutedInk,
                TextStyles.Subheadline);
            if (UiInteract.HoverClick(origin, new Vector2(origin.X + width * 0.7f, origin.Y + headerHeight)))
            {
                OpenProfile(post.OwnerId);
            }

            var imageRect = new Rect(new Vector2(origin.X, origin.Y + headerHeight),
                new Vector2(origin.X + width, origin.Y + headerHeight + width));
            var photos = PostMedia.Photos(post.MediaUrls, post.MediaUrl);
            var result = DrawPostCarousel(drawList, imageRect, post, photos, Metrics.Radius.Md * scale);
            if (result.Tapped && result.Index < photos.Length)
            {
                var mediaUrl = photos[result.Index];
                photoViewer.Open(() => images.Get(mediaUrl));
            }

            var actionsY = imageRect.Max.Y + 22f * scale;
            var liked = post.MyReaction >= 0;
            var heartCenter = new Vector2(origin.X + 13f * scale, actionsY);
            if (ui.IconButton(heartCenter, 15f * scale, FontAwesomeIcon.Heart.ToIconString(),
                    liked ? VelvetTheme.Rose : VelvetTheme.BodyInk, AppSkin.Transparent, 1.2f, Loc.T(L.Velvet.Like)))
            {
                store.ToggleReaction(post, 0);
            }

            var actionCursorX = heartCenter.X + 20f * scale;
            if (post.TotalReactions > 0)
            {
                var likeText = post.TotalReactions.ToString(Loc.Culture);
                var likeSize = Typography.Measure(likeText, TextStyles.Callout);
                var likePos = new Vector2(actionCursorX, actionsY - 8f * scale);
                var likeHovered = UiInteract.Hover(likePos, likePos + likeSize);
                Typography.Draw(likePos, likeText, likeHovered ? VelvetTheme.RoseInk : VelvetTheme.BodyInk,
                    TextStyles.Callout);
                if (likeHovered)
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                    if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                    {
                        OpenLikers(post.Id);
                    }
                }

                actionCursorX += likeSize.X + 16f * scale;
            }
            else
            {
                actionCursorX += 6f * scale;
            }

            var commentCenter = new Vector2(actionCursorX + 13f * scale, actionsY);
            AppSkin.Icon(commentCenter, FontAwesomeIcon.Comment.ToIconString(), VelvetTheme.BodyInk, 1.1f);
            var actionsRight = commentCenter.X + 20f * scale;
            if (post.CommentCount > 0)
            {
                var commentText = post.CommentCount.ToString(Loc.Culture);
                Typography.Draw(new Vector2(actionsRight, actionsY - 8f * scale), commentText, VelvetTheme.BodyInk,
                    TextStyles.Callout);
                actionsRight += Typography.Measure(commentText, TextStyles.Callout).X;
            }

            var mine = store.Me is { } me && me.UserId == post.OwnerId;
            var trailingCenter = new Vector2(origin.X + width - 14f * scale, actionsY);
            if (photos.Length > 1)
            {
                var dotsCenter = new Vector2(origin.X + width * 0.5f, actionsY);
                var available = MathF.Min((trailingCenter.X - 16f * scale - dotsCenter.X) * 2f,
                    (dotsCenter.X - actionsRight - 10f * scale) * 2f);
                PhotoCarousel.DrawDots(drawList, dotsCenter, photos.Length, result.Index, available,
                    VelvetTheme.BodyInk);
            }

            if (mine)
            {
                if (ui.IconButton(trailingCenter, 14f * scale, FontAwesomeIcon.Trash.ToIconString(), VelvetTheme.Danger,
                        VelvetTheme.Alpha(VelvetTheme.Danger, 0.16f), 0.9f, Loc.T(L.Velvet.DeleteConfirm)))
                {
                    AskDeletePost(post.Id);
                }
            }
            else if (ui.IconButton(trailingCenter, 14f * scale, FontAwesomeIcon.Flag.ToIconString(), VelvetTheme.Danger,
                         VelvetTheme.Alpha(VelvetTheme.Danger, 0.16f), 0.9f, Loc.T(L.Velvet.Report)))
            {
                OpenReport("velvet_media", post.Id, Loc.T(L.Velvet.ReportPost));
            }

            ImGui.SetCursorScreenPos(new Vector2(origin.X, actionsY + 20f * scale));
            if (!string.IsNullOrWhiteSpace(post.Caption))
            {
                var captionOrigin = ImGui.GetCursorScreenPos();
                RichTextLayout? captionLayout;
                using (Plugin.Fonts.Push(TextStyles.Callout.Scale, TextStyles.Callout.Weight))
                {
                    captionLayout = detailBodyLayouts.LayoutFor(post.Id, post.Caption, post.Mentions,
                        ImGui.GetContentRegionAvail().X);
                }

                if (captionLayout is null)
                {
                    WrapText(post.Caption, VelvetTheme.BodyInk, TextStyles.Callout);
                }
                else
                {
                    using (Plugin.Fonts.Push(TextStyles.Callout.Scale, TextStyles.Callout.Weight))
                    {
                        DrawRichBody(ImGui.GetWindowDrawList(), captionLayout, captionOrigin);
                    }

                    ImGui.SetCursorScreenPos(captionOrigin);
                    ImGui.Dummy(captionLayout.Size);
                }

                Gap(12f);
            }

            if (post.Tags.Length > 0)
            {
                DrawDisplayTokens(post.Tags, VChipStyle.Tint, VelvetTheme.Rose);
            }

            DrawComments(width, scale);
            Gap(20f);
        }

        DrawCommentComposer(new Rect(new Vector2(area.Min.X, area.Max.Y - composerHeight), area.Max), area, postId);
    }

    private void DrawComments(float width, float scale)
    {
        Gap(10f);
        var linePos = ImGui.GetCursorScreenPos();
        ImGui.GetWindowDrawList().AddLine(linePos, new Vector2(linePos.X + width, linePos.Y),
            VelvetTheme.Divider.Packed(), 1f);
        Gap(14f);
        var count = store.DetailComments.Length;
        VSectionHeader.Bar(count > 0 ? Loc.T(L.Velvet.CommentsCount, count) : Loc.T(L.Velvet.Comments));
        if (store.LoadingComments)
        {
            Typography.Draw(ImGui.GetCursorScreenPos() + new Vector2(0f, 2f * scale), Loc.T(L.Common.Loading),
                VelvetTheme.MutedInk, TextStyles.Footnote);
            Gap(18f);
            return;
        }

        var comments = store.DetailComments;
        if (comments.Length == 0)
        {
            Typography.Draw(ImGui.GetCursorScreenPos() + new Vector2(0f, 2f * scale), Loc.T(L.Velvet.NoComments),
                VelvetTheme.MutedInk, TextStyles.Footnote);
            Gap(18f);
            return;
        }

        for (var index = 0; index < comments.Length; index++)
        {
            DrawCommentRow(comments[index], scale);
        }
    }

    private void DrawCommentRow(VelvetCommentDto comment, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var drawList = ImGui.GetWindowDrawList();
        var avatarRadius = 15f * scale;
        var avatarCenter = new Vector2(origin.X + avatarRadius, origin.Y + avatarRadius);
        var authorName = DisplayNameOf(comment.AuthorDisplayName, comment.AuthorHandle);
        VAvatar.Draw(drawList, avatarCenter, avatarRadius, theme, authorName, string.Empty, comment.AuthorAvatarUrl,
            images, lodestone, -1);
        var textLeft = avatarCenter.X + avatarRadius + 10f * scale;
        var wrapWidth = origin.X + width - 28f * scale - textLeft;
        Typography.Draw(new Vector2(textLeft, origin.Y), authorName, VelvetTheme.TitleInk, TextStyles.SubheadlineEmphasized);
        var nameWidth = Typography.Measure(authorName, TextStyles.SubheadlineEmphasized).X;
        var time = TimeText.Short(comment.CreatedAtUnix);
        if (time.Length > 0)
        {
            Typography.Draw(new Vector2(textLeft + nameWidth + 8f * scale, origin.Y + 1f * scale), time,
                VelvetTheme.MutedInk, TextStyles.Footnote);
            var timeWidth = Typography.Measure(time, TextStyles.Footnote).X;
            CommentReviewTag.Draw(
                new Vector2(textLeft + nameWidth + 8f * scale + timeWidth + 8f * scale, origin.Y + 1f * scale),
                textLeft + wrapWidth, comment.ScanStatus, 0.8f);
        }

        var textTop = origin.Y + 18f * scale;
        ImGui.SetCursorScreenPos(new Vector2(textLeft, textTop));
        RichTextLayout? commentLayout;
        using (Plugin.Fonts.Push(0.9f))
        {
            commentLayout = commentLayouts.LayoutFor(comment.Id, comment.Text, comment.Mentions, wrapWidth);
        }

        if (commentLayout is null)
        {
            ImGui.PushTextWrapPos(textLeft + wrapWidth);
            using (ImRaii.PushColor(ImGuiCol.Text, VelvetTheme.BodyInk))
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

        var textHeight = commentLayout?.Size.Y ?? Typography.MeasureWrapped(comment.Text, wrapWidth, 0.9f);
        var rowHeight = MathF.Max(avatarRadius * 2f, 18f * scale + textHeight);
        if (UiInteract.HoverClick(origin, new Vector2(textLeft + nameWidth, textTop)))
        {
            OpenProfile(comment.AuthorId);
        }

        var mine = store.Me is { } me && me.UserId == comment.AuthorId;
        if (mine)
        {
            var trashCenter = new Vector2(origin.X + width - 8f * scale, origin.Y + 8f * scale);
            if (ui.IconButton(trashCenter, 10f * scale, FontAwesomeIcon.Times.ToIconString(), VelvetTheme.MutedInk,
                    AppSkin.Transparent, 0.7f))
            {
                AskDeleteComment(commentsPostId, comment.Id);
            }
        }

        var heartCenter = new Vector2(origin.X + width - 10f * scale, origin.Y + 28f * scale);
        if (CommentHeart.Draw(ui, heartCenter, comment.Liked, comment.LikeCount, VelvetTheme.MutedInk,
                VelvetTheme.MutedInk, Loc.T(L.Velvet.Like), out var heartBottom))
        {
            store.ToggleCommentLike(comment);
        }

        rowHeight = MathF.Max(rowHeight, heartBottom - origin.Y);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight + 14f * scale));
    }

    private void DrawCommentComposer(Rect bar, Rect screen, string postId)
    {
        var style = new CommentComposerStyle(VelvetTheme.Hairline, VelvetTheme.PlumWell, VelvetTheme.TitleInk,
            VelvetTheme.Rose, VelvetTheme.PlumWell, VelvetTheme.OnAccent, true, 9f, 54f, 0.85f);
        var focusPending = false;
        if (CommentComposerBar.Draw(bar, screen, ui, theme, style, "##velvetComment", Loc.T(L.Velvet.AddComment),
                ref commentDraft, 500, commentMentions, mentionPopup, images, lodestone, store.Commenting,
                ref focusPending, commentEmoji))
        {
            store.AddComment(postId, commentDraft, _ => { });
            commentDraft = string.Empty;
        }
    }

    private void OpenLikers(string postId)
    {
        store.OpenLikers(postId);
        router.Push(VelvetView.Likers(postId));
    }

    private void DrawLikers(Rect area, string postId)
    {
        var scale = ImGuiHelpers.GlobalScale;
        if (VHeader.Push(area, Loc.T(L.Velvet.LikesTitle), theme))
        {
            router.Pop();
            return;
        }

        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + VHeader.Height * scale), area.Max);
        using (AppSurface.Begin(body))
        {
            if (store.LikersLoading)
            {
                Typography.DrawCentered(new Vector2(body.Center.X, body.Min.Y + 70f * scale), Loc.T(L.Common.Loading),
                    VelvetTheme.MutedInk, TextStyles.Callout);
                return;
            }

            var likers = store.Likers;
            if (likers.Length == 0)
            {
                Typography.DrawCentered(new Vector2(body.Center.X, body.Min.Y + 70f * scale), Loc.T(L.Velvet.NoLikes),
                    VelvetTheme.MutedInk, TextStyles.Callout);
                return;
            }

            Gap(8f);
            for (var index = 0; index < likers.Length; index++)
            {
                var user = likers[index];
                var model = new VRowModel
                {
                    Title = DisplayNameOf(user.DisplayName, user.Handle),
                    Subtitle = SocialIdentity.ProfileMeta(user.Handle, RegionOf(user.World)),
                    Height = 60f,
                    Leading = VRowLeading.Avatar,
                    AvatarRadius = 20f,
                    Name = DisplayNameOf(user.DisplayName, user.Handle),
                    World = user.World,
                    AvatarUrl = user.AvatarUrl,
                };
                if (VRow.Draw(in model, ui, theme, images, lodestone) == VRowHit.Body)
                {
                    OpenProfile(user.Id);
                }
            }

            Gap(40f);
        }
    }

    private void AskDeletePost(string postId)
    {
        confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Velvet.DeleteConfirmMessage),
            ConfirmLabel = Loc.T(L.Velvet.DeleteConfirm),
            CancelLabel = Loc.T(L.Velvet.DeleteCancel),
            BusyLabel = Loc.T(L.Velvet.Saving),
            FailedMessage = Loc.T(L.Velvet.DeleteFailed),
            ConfirmAsync = done => store.DeletePost(postId, ok =>
            {
                if (ok)
                {
                    router.Pop();
                }

                done(ok);
            }),
        });
    }

    private void AskDeleteComment(string postId, string commentId)
    {
        confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Velvet.DeleteCommentConfirmMessage),
            ConfirmLabel = Loc.T(L.Velvet.DeleteConfirm),
            CancelLabel = Loc.T(L.Velvet.DeleteCancel),
            BusyLabel = Loc.T(L.Velvet.Saving),
            FailedMessage = Loc.T(L.Velvet.DeleteCommentFailed),
            ConfirmAsync = done => store.DeleteComment(postId, commentId, done),
        });
    }
}
