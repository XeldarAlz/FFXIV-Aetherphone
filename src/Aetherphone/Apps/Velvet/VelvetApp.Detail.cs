using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Messaging;
using Aetherphone.Core.Net;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Velvet;

// The post detail view: post header, actions, comments list, comment composer and the delete
// confirmations. Split from the main hub/timeline for readability.
internal sealed partial class VelvetApp
{
    private void DrawPostDetail(Rect area, string postId)
    {
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Apps.Velvet), back);
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
            Typography.DrawCentered(body.Center, Loc.T(L.Common.Loading), AppPalettes.Velvet.MutedInk);
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
            var width = ImGui.GetContentRegionAvail().X;
            var headerHeight = 48f * scale;
            var avatarRadius = 18f * scale;
            var avatarCenter = new Vector2(origin.X + avatarRadius, origin.Y + headerHeight * 0.5f);
            AvatarView.Draw(ImGui.GetWindowDrawList(), avatarCenter, avatarRadius, Accent,
                Monogram(post.OwnerDisplayName, post.OwnerHandle), 1f,
                lodestone.Remote(post.OwnerId, ToUri(post.OwnerAvatarUrl)), 32);
            var nameLeft = avatarCenter.X + avatarRadius + 10f * scale;
            var displayName = string.IsNullOrEmpty(post.OwnerDisplayName) ? post.OwnerHandle : post.OwnerDisplayName;
            var ownerSub = post.OwnerHandle.Length > 0 ? $"@{post.OwnerHandle}" : string.Empty;
            var ownerTime = TimeText.Short(post.CreatedAtUnix);
            if (ownerTime.Length > 0)
            {
                ownerSub = ownerSub.Length > 0 ? $"{ownerSub} · {ownerTime}" : ownerTime;
            }

            if (ownerSub.Length > 0)
            {
                Typography.Draw(new Vector2(nameLeft, avatarCenter.Y - 13f * scale), displayName, theme.TextStrong,
                    0.95f, FontWeight.SemiBold);
                Typography.Draw(new Vector2(nameLeft, avatarCenter.Y + 3f * scale), ownerSub, AppPalettes.Velvet.MutedInk, 0.78f);
            }
            else
            {
                Typography.Draw(new Vector2(nameLeft, avatarCenter.Y - 8f * scale), displayName, theme.TextStrong,
                    0.95f, FontWeight.SemiBold);
            }

            if (UiInteract.HoverClick(new Vector2(origin.X, origin.Y),
                    new Vector2(origin.X + width * 0.7f, origin.Y + headerHeight)))
            {
                OpenProfile(post.OwnerId);
            }

            ImGui.SetCursorScreenPos(new Vector2(origin.X, origin.Y + headerHeight));
            var imageRect = new Rect(new Vector2(origin.X, origin.Y + headerHeight),
                new Vector2(origin.X + width, origin.Y + headerHeight + width));
            DrawPostThumbnail(post, imageRect.Min, imageRect.Max, scale);
            if (!string.IsNullOrEmpty(post.MediaUrl) && UiInteract.HoverClick(imageRect.Min, imageRect.Max))
            {
                var mediaUrl = post.MediaUrl;
                photoViewer.Open(() => images.Get(mediaUrl));
            }

            var actionsY = imageRect.Max.Y + 22f * scale;
            var liked = post.MyReaction >= 0;
            var heartCenter = new Vector2(origin.X + 13f * scale, actionsY);
            if (ui.IconButton(heartCenter, 15f * scale, FontAwesomeIcon.Heart.ToIconString(),
                    liked ? theme.Danger : AppPalettes.Velvet.BodyInk, new Vector4(0f, 0f, 0f, 0f), 1.25f, Loc.T(L.Velvet.Like)))
            {
                store.ToggleReaction(post, 0);
            }

            var actionCursorX = heartCenter.X + 20f * scale;
            if (post.TotalReactions > 0)
            {
                var likeText = post.TotalReactions.ToString(Loc.Culture);
                var likeSize = Typography.Measure(likeText, 0.9f, FontWeight.Medium);
                var likePos = new Vector2(actionCursorX, actionsY - 8f * scale);
                var likeHovered = UiInteract.Hover(likePos, likePos + likeSize);
                Typography.Draw(likePos, likeText, likeHovered ? theme.Accent : AppPalettes.Velvet.BodyInk, 0.9f,
                    FontWeight.Medium);
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
            AppSkin.Icon(commentCenter, FontAwesomeIcon.Comment.ToIconString(), AppPalettes.Velvet.BodyInk, 1.2f);
            HoverTooltip.Show(new Rect(commentCenter - new Vector2(14f * scale, 14f * scale),
                commentCenter + new Vector2(14f * scale, 14f * scale)), Loc.T(L.Velvet.Comment), HoverLabelSide.Above);
            if (post.CommentCount > 0)
            {
                Typography.Draw(new Vector2(commentCenter.X + 20f * scale, actionsY - 8f * scale),
                    post.CommentCount.ToString(Loc.Culture), AppPalettes.Velvet.BodyInk, 0.9f, FontWeight.Medium);
            }

            var mine = store.Me is { } me && me.UserId == post.OwnerId;
            var reportShown = false;
            if (mine)
            {
                var deleteCenter = new Vector2(origin.X + width - 14f * scale, actionsY);
                var deleteBackground = Palette.WithAlpha(ui.Theme.Danger, 0.16f);
                if (ui.IconButton(deleteCenter, 14f * scale, FontAwesomeIcon.Trash.ToIconString(), ui.Theme.Danger,
                        deleteBackground, 0.9f, Loc.T(L.Velvet.DeleteConfirm)))
                {
                    AskDeletePost(post.Id);
                }
            }
            else
            {
                var reportCenter = new Vector2(origin.X + width - 14f * scale, actionsY);
                reportShown = report.Toggle(ui, reportCenter, 14f * scale, "post", post.Id,
                    Loc.T(L.Velvet.ReportSubmit));
            }

            ImGui.SetCursorScreenPos(new Vector2(origin.X, actionsY + 20f * scale));
            if (reportShown)
            {
                report.Composer(ui, origin.X, width);
                ImGui.Dummy(new Vector2(0f, 6f * scale));
            }

            if (!string.IsNullOrWhiteSpace(post.Caption))
            {
                ImGui.PushTextWrapPos(origin.X + width);
                using (ImRaii.PushColor(ImGuiCol.Text, AppPalettes.Velvet.BodyInk))
                using (Plugin.Fonts.Push(0.92f))
                {
                    ImGui.TextWrapped(post.Caption);
                }

                ImGui.PopTextWrapPos();
                ImGui.Dummy(new Vector2(0f, 12f * scale));
            }

            if (post.Tags.Length > 0)
            {
                DrawTagChips(post.Tags);
            }

            DrawComments(width, scale);
            ImGui.Dummy(new Vector2(0f, 20f * scale));
        }

        DrawCommentComposer(new Rect(new Vector2(area.Min.X, area.Max.Y - composerHeight), area.Max), postId);
    }

    private void DrawComments(float width, float scale)
    {
        ImGui.Dummy(new Vector2(0f, 10f * scale));
        var linePos = ImGui.GetCursorScreenPos();
        ImGui.GetWindowDrawList().AddLine(linePos, new Vector2(linePos.X + width, linePos.Y),
            ImGui.GetColorU32(theme.Separator), 1f);
        ImGui.Dummy(new Vector2(0f, 14f * scale));
        var count = store.DetailComments.Length;
        DrawSectionHeading(count > 0 ? $"{Loc.T(L.Velvet.Comments)} · {count}" : Loc.T(L.Velvet.Comments));
        if (store.LoadingComments)
        {
            Typography.Draw(ImGui.GetCursorScreenPos() + new Vector2(0f, 2f * scale), Loc.T(L.Common.Loading),
                AppPalettes.Velvet.MutedInk, 0.85f);
            ImGui.Dummy(new Vector2(0f, 18f * scale));
        }
        else
        {
            var comments = store.DetailComments;
            if (comments.Length == 0)
            {
                Typography.Draw(ImGui.GetCursorScreenPos() + new Vector2(0f, 2f * scale), Loc.T(L.Velvet.NoComments),
                    AppPalettes.Velvet.MutedInk, 0.85f);
                ImGui.Dummy(new Vector2(0f, 18f * scale));
            }
            else
            {
                for (var index = 0; index < comments.Length; index++)
                {
                    DrawCommentRow(comments[index], scale);
                }
            }
        }
    }

    private void DrawCommentRow(VelvetCommentDto comment, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var drawList = ImGui.GetWindowDrawList();
        var avatarRadius = 15f * scale;
        var avatarCenter = new Vector2(origin.X + avatarRadius, origin.Y + avatarRadius);
        AvatarView.Draw(drawList, avatarCenter, avatarRadius, Accent,
            Monogram(comment.AuthorDisplayName, comment.AuthorHandle), 0.9f,
            lodestone.Remote(comment.AuthorId, ToUri(comment.AuthorAvatarUrl)), 28);
        var textLeft = avatarCenter.X + avatarRadius + 10f * scale;
        var wrapWidth = origin.X + width - textLeft;
        var name = string.IsNullOrEmpty(comment.AuthorDisplayName) ? comment.AuthorHandle : comment.AuthorDisplayName;
        Typography.Draw(new Vector2(textLeft, origin.Y), name, theme.TextStrong, 0.9f, FontWeight.SemiBold);
        var nameWidth = Typography.Measure(name, 0.9f, FontWeight.SemiBold).X;
        var time = TimeText.Short(comment.CreatedAtUnix);
        if (time.Length > 0)
        {
            Typography.Draw(new Vector2(textLeft + nameWidth + 8f * scale, origin.Y + 1f * scale), time,
                AppPalettes.Velvet.MutedInk, 0.8f);
        }

        var textTop = origin.Y + 18f * scale;
        ImGui.SetCursorScreenPos(new Vector2(textLeft, textTop));
        ImGui.PushTextWrapPos(textLeft + wrapWidth);
        using (ImRaii.PushColor(ImGuiCol.Text, AppPalettes.Velvet.BodyInk))
        using (Plugin.Fonts.Push(0.9f))
        {
            ImGui.TextWrapped(comment.Text);
        }

        ImGui.PopTextWrapPos();
        var textHeight = Typography.MeasureWrapped(comment.Text, wrapWidth, 0.9f);
        var rowHeight = MathF.Max(avatarRadius * 2f, 18f * scale + textHeight);
        if (UiInteract.HoverClick(new Vector2(origin.X, origin.Y), new Vector2(textLeft + nameWidth, textTop)))
        {
            OpenProfile(comment.AuthorId);
        }

        var mine = store.Me is { } me && me.UserId == comment.AuthorId;
        if (mine)
        {
            var trashCenter = new Vector2(origin.X + width - 8f * scale, origin.Y + 8f * scale);
            var trashHitRadius = 10f * scale;
            if (ui.IconButton(trashCenter, trashHitRadius, FontAwesomeIcon.Times.ToIconString(), AppPalettes.Velvet.MutedInk,
                    AppSkin.Transparent, 0.7f))
            {
                AskDeleteComment(commentsPostId, comment.Id);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight + 14f * scale));
    }

    private void DrawCommentComposer(Rect bar, string postId)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddLine(bar.Min, new Vector2(bar.Max.X, bar.Min.Y), ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)),
            1f);
        var sendRadius = 15f * scale;
        var pillMin = new Vector2(bar.Min.X + 12f * scale, bar.Min.Y + 9f * scale);
        var pillMax = new Vector2(bar.Max.X - 54f * scale, bar.Max.Y - 9f * scale);
        Squircle.Fill(drawList, pillMin, pillMax, (pillMax.Y - pillMin.Y) * 0.5f,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)));
        ImGui.SetCursorScreenPos(new Vector2(pillMin.X + 14f * scale,
            (pillMin.Y + pillMax.Y) * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(pillMax.X - pillMin.X - 24f * scale);
        var submitted = false;
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            if (ImGui.InputTextWithHint("##velvetComment", Loc.T(L.Velvet.AddComment), ref commentDraft, 500,
                    ImGuiInputTextFlags.EnterReturnsTrue))
            {
                submitted = true;
            }
        }

        var canSend = commentDraft.Trim().Length > 0 && !store.Commenting;
        var sendCenter = new Vector2(pillMax.X + 6f * scale + sendRadius, bar.Center.Y);
        drawList.AddCircleFilled(sendCenter, sendRadius, ImGui.GetColorU32(canSend ? Accent : theme.SurfaceMuted), 24);
        AppSkin.Icon(sendCenter, FontAwesomeIcon.PaperPlane.ToIconString(), new Vector4(1f, 1f, 1f, 1f), 0.85f);
        var sendRect = new Rect(sendCenter - new Vector2(sendRadius, sendRadius),
            sendCenter + new Vector2(sendRadius, sendRadius));
        HoverTooltip.Show(sendRect, Loc.T(L.Velvet.Send), HoverLabelSide.Above);
        if (UiInteract.Hover(sendRect.Min, sendRect.Max))
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && canSend)
            {
                submitted = true;
            }
        }

        if (submitted && canSend)
        {
            store.AddComment(postId, commentDraft, _ => { });
            commentDraft = string.Empty;
        }
    }

    private void AskDeletePost(string postId)
    {
        Plugin.Confirm.Ask(new ConfirmRequest
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
        Plugin.Confirm.Ask(new ConfirmRequest
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
