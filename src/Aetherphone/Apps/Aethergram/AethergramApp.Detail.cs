using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Net;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Platform;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Wallpapers;
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
            var width = ImGui.GetContentRegionAvail().X;
            var headerHeight = 52f * scale;
            var avatarRadius = 20f * scale;
            var avatarCenter = new Vector2(origin.X + avatarRadius + 6f * scale, origin.Y + headerHeight * 0.5f);
            AethergramArt.StoryRing(ImGui.GetWindowDrawList(), avatarCenter, avatarRadius + 3f * scale, scale);
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
            DrawGramImage(imageRect, post.MediaUrl, 16f * scale);
            HandleLikeGesture(imageRect, post);
            DrawLikeBurst(imageRect, post.Id);
            var liked = post.MyReaction >= 0;
            var actionsY = imageRect.Max.Y + 22f * scale;
            var heartCenter = new Vector2(origin.X + 13f * scale, actionsY);
            if (ui.IconButton(heartCenter, 14f * scale, FontAwesomeIcon.Heart.ToIconString(),
                    liked ? LikeRed : AppPalettes.Aethergram.BodyInk, AppSkin.Transparent, 1.1f, Loc.T(L.Aethergram.Like)))
            {
                store.ToggleLike(post);
            }

            var actionCursorX = heartCenter.X + 18f * scale;
            if (post.TotalReactions > 0)
            {
                var likeText = post.TotalReactions.ToString(Loc.Culture);
                var likeSize = Typography.Measure(likeText, 0.85f, FontWeight.Medium);
                var likePos = new Vector2(actionCursorX, actionsY - 7f * scale);
                var likeHovered = ImGui.IsMouseHoveringRect(likePos, likePos + likeSize);
                Typography.Draw(likePos, likeText, likeHovered ? theme.Accent : AppPalettes.Aethergram.BodyInk, 0.85f,
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

            var commentCenter = new Vector2(actionCursorX + 12f * scale, actionsY);
            if (ui.IconButton(commentCenter, 14f * scale, FontAwesomeIcon.Comment.ToIconString(), AppPalettes.Aethergram.BodyInk,
                    AppSkin.Transparent, 1.05f, Loc.T(L.Aethergram.Comment)))
            {
                commentFocusPending = true;
            }

            if (post.CommentCount > 0)
            {
                Typography.Draw(new Vector2(commentCenter.X + 18f * scale, actionsY - 7f * scale),
                    post.CommentCount.ToString(Loc.Culture), AppPalettes.Aethergram.BodyInk, 0.85f, FontWeight.Medium);
            }

            var mine = store.Me is { } me && me.Id == post.AuthorId;
            var reportShown = false;
            if (mine)
            {
                var deleteCenter = new Vector2(origin.X + width - 14f * scale, actionsY);
                DrawDeleteToggle(deleteCenter, 14f * scale, post.Id);
            }
            else
            {
                var reportCenter = new Vector2(origin.X + width - 14f * scale, actionsY);
                reportShown = DrawReportToggle(reportCenter, 14f * scale, "post", post.Id);
            }

            ImGui.SetCursorScreenPos(new Vector2(origin.X, actionsY + 20f * scale));
            if (reportShown)
            {
                DrawReportComposer(origin.X, width);
                ImGui.Dummy(new Vector2(0f, 6f * scale));
            }

            if (post.Text.Length > 0)
            {
                var captionPos = ImGui.GetCursorScreenPos();
                Typography.Draw(captionPos, displayName, theme.TextStrong, 0.9f, FontWeight.SemiBold);
                var nameWidth = Typography.Measure(displayName, 0.9f, FontWeight.SemiBold).X;
                ImGui.SetCursorScreenPos(new Vector2(captionPos.X + nameWidth + 6f * scale, captionPos.Y));
                ImGui.PushTextWrapPos(origin.X + width - ImGui.GetWindowPos().X);
                using (ImRaii.PushColor(ImGuiCol.Text, AppPalettes.Aethergram.BodyInk))
                using (Plugin.Fonts.Push(0.9f))
                {
                    ImGui.TextWrapped(post.Text);
                }

                ImGui.PopTextWrapPos();
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

        DrawCommentComposer(new Rect(new Vector2(area.Min.X, area.Max.Y - composerHeight), area.Max), postId);
    }

    private void DrawComment(CommentDto comment)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var avatarRadius = 20f * scale;
        var avatarCenterX = origin.X + avatarRadius + 5f * scale;
        var mine = store.Me is { } me && me.Id == comment.AuthorId;

        var bubbleLeft = avatarCenterX + avatarRadius + 11f * scale;
        var bubbleRight = origin.X + width;
        var padX = 13f * scale;
        var padTop = 10f * scale;
        var padBottom = 11f * scale;
        var textLeft = bubbleLeft + padX;
        var textRight = bubbleRight - padX;
        var displayName = SocialIdentity.Name(comment.AuthorDisplayName, comment.AuthorHandle);
        var nameHeight = Typography.Measure(displayName, 0.85f, FontWeight.SemiBold).Y;
        var textHeight = Typography.MeasureWrapped(comment.Text, textRight - textLeft, 0.88f);
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
        AethergramArt.StoryRing(drawList, avatarCenter, avatarRadius + 2f * scale, scale);
        DrawAvatar(avatarCenter, avatarRadius - 1f * scale, comment.AuthorName, string.Empty, comment.AuthorAvatarUrl,
            0.8f, 28);

        var nameTop = bubbleTop + padTop;
        Typography.Draw(new Vector2(textLeft, nameTop), displayName, theme.TextStrong, 0.85f, FontWeight.SemiBold);
        var nameWidth = Typography.Measure(displayName, 0.85f, FontWeight.SemiBold).X;
        var meta = TimeText.Short(comment.CreatedAtUnix);
        var metaSize = Typography.Measure(meta, 0.76f);
        var metaLeft = textLeft + nameWidth + 8f * scale;
        var metaRightBound = mine ? textRight - 14f * scale : textRight;
        if (metaLeft + metaSize.X <= metaRightBound)
        {
            Typography.Draw(new Vector2(metaLeft, nameTop + (nameHeight - metaSize.Y) * 0.5f), meta,
                AppPalettes.Aethergram.MutedInk, 0.76f);
        }

        var textTop = nameTop + nameHeight + 4f * scale;
        ImGui.SetCursorScreenPos(new Vector2(textLeft, textTop));
        ImGui.PushTextWrapPos(textRight - ImGui.GetWindowPos().X);
        using (ImRaii.PushColor(ImGuiCol.Text, AppPalettes.Aethergram.BodyInk))
        using (Plugin.Fonts.Push(0.88f))
        {
            ImGui.TextWrapped(comment.Text);
        }

        ImGui.PopTextWrapPos();
        if (UiInteract.HoverClick(new Vector2(origin.X, bubbleTop), new Vector2(textLeft + nameWidth, textTop)))
        {
            OpenProfile(comment.AuthorId);
        }

        if (mine)
        {
            var trashCenter = new Vector2(bubbleRight - 13f * scale, bubbleTop + 13f * scale);
            var trashHitRadius = 10f * scale;
            if (ui.IconButton(trashCenter, trashHitRadius, FontAwesomeIcon.Times.ToIconString(), AppPalettes.Aethergram.MutedInk,
                    AppSkin.Transparent, 0.7f) && store.DetailPost is { } post)
            {
                AskDeleteComment(post.Id, comment.Id);
            }

            var trashHovered = ImGui.IsMouseHoveringRect(trashCenter - new Vector2(trashHitRadius, trashHitRadius),
                trashCenter + new Vector2(trashHitRadius, trashHitRadius));
            if (trashHovered)
            {
                DrawDeleteCommentTooltip(trashCenter, trashHitRadius, scale);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, bubbleHeight + 11f * scale));
    }

    private void DrawCommentComposer(Rect bar, string postId)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddLine(bar.Min, new Vector2(bar.Max.X, bar.Min.Y), ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)),
            1f);
        var pillMin = new Vector2(bar.Min.X + 12f * scale, bar.Min.Y + 9f * scale);
        var pillMax = new Vector2(bar.Max.X - 54f * scale, bar.Max.Y - 9f * scale);
        Squircle.Fill(drawList, pillMin, pillMax, (pillMax.Y - pillMin.Y) * 0.5f,
            ImGui.GetColorU32(AppPalettes.Aethergram.FieldSurface));
        ImGui.SetCursorScreenPos(new Vector2(pillMin.X + 14f * scale,
            (pillMin.Y + pillMax.Y) * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(pillMax.X - pillMin.X - 24f * scale);
        if (commentFocusPending)
        {
            ImGui.SetKeyboardFocusHere();
            commentFocusPending = false;
        }

        var submitted = false;
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, AppPalettes.Aethergram.TitleInk))
        {
            submitted = ImGui.InputTextWithHint("##gramComment", Loc.T(L.Aethergram.AddComment), ref commentDraft,
                MaxCommentLength, ImGuiInputTextFlags.EnterReturnsTrue);
        }

        var canSend = commentDraft.Trim().Length > 0 && !store.Commenting;
        var sendRadius = 15f * scale;
        var sendCenter = new Vector2(pillMax.X + 6f * scale + sendRadius, bar.Center.Y);
        drawList.AddCircleFilled(sendCenter, sendRadius, ImGui.GetColorU32(canSend ? Accent : theme.SurfaceMuted), 24);
        AppSkin.Icon(sendCenter, FontAwesomeIcon.PaperPlane.ToIconString(), new Vector4(1f, 1f, 1f, 1f), 0.8f);
        if ((UiInteract.HoverClick(sendCenter - new Vector2(sendRadius, sendRadius),
                sendCenter + new Vector2(sendRadius, sendRadius)) || submitted) && canSend)
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
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        if (store.ProfileFailed)
        {
            Typography.DrawCentered(body.Center, Loc.T(L.Aethergram.ProfileError), AppPalettes.Aethergram.MutedInk);
            return;
        }

        if (user is null)
        {
            Typography.DrawCentered(body.Center, Loc.T(L.Common.Loading), AppPalettes.Aethergram.MutedInk);
            return;
        }

        using (AppSurface.Begin(body))
        {
            DrawProfileHeader(user);
            ui.SectionHeading(Loc.T(L.Aethergram.GramsTitle));
            DrawProfileGrid();
        }
    }

    private bool DrawReportToggle(Vector2 center, float radius, string targetType, string targetId)
    {
        var active = reportTargetType == targetType && reportTargetId == targetId;
        var background = Palette.WithAlpha(theme.Danger, active ? 0.32f : 0.16f);
        if (ui.IconButton(center, radius, FontAwesomeIcon.Flag.ToIconString(), theme.Danger, background, 0.9f,
                Loc.T(L.Aethergram.ReportSubmit)))
        {
            if (active)
            {
                reportTargetType = null;
                reportTargetId = null;
                active = false;
            }
            else
            {
                reportTargetType = targetType;
                reportTargetId = targetId;
                reportReasonDraft = string.Empty;
                reportStatus = string.Empty;
                active = true;
            }
        }

        return active;
    }

    private void DrawReportComposer(float left, float width)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var buttonWidth = 84f * scale;
        var buttonHeight = 28f * scale;
        ImGui.SetCursorScreenPos(new Vector2(left, origin.Y));
        ImGui.SetNextItemWidth(width - buttonWidth - 8f * scale);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppPalettes.Aethergram.FieldSurface))
        using (ImRaii.PushColor(ImGuiCol.Text, AppPalettes.Aethergram.TitleInk))
        {
            ImGui.InputTextWithHint("##reportReason", Loc.T(L.Aethergram.ReportReasonHint), ref reportReasonDraft,
                MaxReportReasonLength);
        }

        var buttonRect = new Rect(new Vector2(left + width - buttonWidth, origin.Y - 2f * scale),
            new Vector2(left + width, origin.Y - 2f * scale + buttonHeight));
        var canSubmit = !reportSubmitting;
        if (ui.PillButton(buttonRect, reportSubmitting ? Loc.T(L.Aethergram.Saving) : Loc.T(L.Aethergram.ReportSubmit),
                canSubmit) && canSubmit)
        {
            SubmitReport();
        }

        ImGui.SetCursorScreenPos(new Vector2(left, origin.Y + buttonHeight + 2f * scale));
        if (reportStatus.Length > 0)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, AppPalettes.Aethergram.MutedInk))
            {
                ImGui.TextUnformatted(reportStatus);
            }

            ImGui.Dummy(new Vector2(0f, 4f * scale));
        }
    }

    private void SubmitReport()
    {
        if (reportSubmitting || reportTargetType is not { } targetType || reportTargetId is not { } targetId)
        {
            return;
        }

        reportSubmitting = true;
        var reason = reportReasonDraft.Trim();
        store.Report(targetType, targetId, reason.Length > 0 ? reason : null, ok =>
        {
            reportSubmitting = false;
            reportStatus = Loc.T(ok ? L.Aethergram.ReportSent : L.Aethergram.ReportFailed);
            if (ok)
            {
                reportTargetType = null;
                reportTargetId = null;
            }
        });
    }

    private void DrawDeleteToggle(Vector2 center, float radius, string postId)
    {
        var background = Palette.WithAlpha(theme.Danger, 0.16f);
        if (ui.IconButton(center, radius, FontAwesomeIcon.Trash.ToIconString(), theme.Danger, background, 0.9f,
                Loc.T(L.Aethergram.DeleteConfirm)))
        {
            AskDeletePost(postId);
        }
    }

    private void AskDeletePost(string postId)
    {
        Plugin.Confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Aethergram.DeleteConfirmMessage),
            ConfirmLabel = Loc.T(L.Aethergram.DeleteConfirm),
            CancelLabel = Loc.T(L.Aethergram.DeleteCancel),
            BusyLabel = Loc.T(L.Aethergram.Saving),
            FailedMessage = Loc.T(L.Aethergram.DeleteFailed),
            ConfirmAsync = done => store.DeletePost(postId, ok =>
            {
                if (ok)
                {
                    back();
                }

                done(ok);
            }),
        });
    }

    private void AskDeleteComment(string postId, string commentId)
    {
        Plugin.Confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Aethergram.DeleteCommentConfirmMessage),
            ConfirmLabel = Loc.T(L.Aethergram.DeleteConfirm),
            CancelLabel = Loc.T(L.Aethergram.DeleteCancel),
            BusyLabel = Loc.T(L.Aethergram.Saving),
            FailedMessage = Loc.T(L.Aethergram.DeleteCommentFailed),
            ConfirmAsync = done => store.DeleteComment(postId, commentId, done),
        });
    }

    private void DeferTooltip(Vector2 center, float hitRadius, string text)
    {
        deferredTooltipActive = true;
        deferredTooltipCenter = center;
        deferredTooltipRadius = hitRadius;
        deferredTooltipText = text;
    }

    private void FlushDeferredTooltip()
    {
        if (deferredTooltipActive && deferredTooltipText.Length > 0)
        {
            ui.DrawActionTooltip(deferredTooltipCenter, deferredTooltipRadius, deferredTooltipText);
        }
    }

    private void DrawDeleteCommentTooltip(Vector2 iconCenter, float hitRadius, float scale)
    {
        ui.DrawActionTooltip(iconCenter, hitRadius, Loc.T(L.Aethergram.DeleteComment));
    }
}
