using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Translation;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Aethergram;

internal sealed partial class AethergramApp
{
    private const float DetailComposerHeight = 56f;
    private const float DetailLoadingOffset = 60f;
    private const float CommentAvatarRadius = 16f;
    private const float CommentAvatarGap = 12f;
    private const float CommentPadY = 10f;
    private const float CommentTextGap = 3f;
    private const float CommentMetaGap = 8f;
    private const float CommentMediaGap = 6f;
    private const float CommentHeartColumn = 40f;
    private const float CommentHeartSize = 16f;
    private const float CommentHeartHitRadius = 14f;
    private const float CommentCountGap = 3f;
    private const float EarlierRowHeight = 36f;
    private const float EmptyCommentsHeight = 180f;
    private const float ComposerAvatarRadius = 16f;
    private const float ComposerFailureLift = 22f;
    private const float CommentSendRevealSmoothTime = 0.07f;
    private const int CommentSheetItemCount = 1;

    private static readonly TextStyle CommentNameStyle = new(0.95f, FontWeight.SemiBold);
    private static readonly TextStyle CommentBodyStyle = TextStyles.Callout;
    private static readonly TextStyle CommentMetaStyle = TextStyles.Footnote;
    private static readonly TextStyle CommentCountStyle = TextStyles.Caption1;
    private static readonly TextStyle EarlierCommentsStyle = TextStyles.SubheadlineEmphasized;

    private readonly ActionSheet commentSheet = new();
    private readonly ActionSheet.Item[] commentSheetItems = new ActionSheet.Item[CommentSheetItemCount];
    private string? commentSheetCommentId;
    private bool commentSheetMine;
    private Spring commentSendReveal;

    private void DrawDetail(Rect area, string postId)
    {
        var scale = UiScale.Current;
        DrawScreenHeader(area, Loc.T(L.Aethergram.PostTitle));
        var post = store.DetailPost;
        var top = area.Min.Y + AppHeader.Height * scale;
        if (post is null || post.Id != postId)
        {
            Typography.DrawCentered(new Vector2(area.Center.X, top + DetailLoadingOffset * scale),
                Loc.T(L.Common.Loading), Ink.MutedInk);
            return;
        }

        var composerHeight = DetailComposerHeight * scale;
        var body = new Rect(new Vector2(area.Min.X, top), new Vector2(area.Max.X, area.Max.Y - composerHeight));
        using (AppSurface.BeginEdgeToEdge(body))
        {
            DrawGramCard(post, true);
            DrawComments();
            ImGui.Dummy(new Vector2(0f, 16f * scale));
        }

        DrawCommentComposer(new Rect(new Vector2(area.Min.X, area.Max.Y - composerHeight), area.Max), area, post);
    }

    private void DrawComments()
    {
        var scale = UiScale.Current;
        var comments = store.DetailComments;
        if (comments.Length == 0)
        {
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            if (store.DetailLoading)
            {
                InfiniteScroll.DrawLoadingRow(origin.X + width * 0.5f, Ink.MutedInk);
                return;
            }

            var height = EmptyCommentsHeight * scale;
            DrawEmptyState(new Rect(origin, new Vector2(origin.X + width, origin.Y + height)),
                Loc.T(L.Aethergram.NoComments), Loc.T(L.Aethergram.StartConversation));
            ImGui.Dummy(new Vector2(width, height));
            return;
        }

        DrawEarlierCommentsRow();
        for (var index = 0; index < comments.Length; index++)
        {
            if (HiddenByMediaPreference(comments[index]))
            {
                continue;
            }

            DrawComment(comments[index]);
        }
    }

    private void DrawEarlierCommentsRow()
    {
        var scale = UiScale.Current;
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        if (store.CommentsLoadingMore)
        {
            InfiniteScroll.DrawLoadingRow(origin.X + width * 0.5f, Ink.MutedInk);
            return;
        }

        if (!store.HasMoreComments)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var height = EarlierRowHeight * scale;
        var rowMax = new Vector2(origin.X + width, origin.Y + height);
        var hovered = UiInteract.Hover(origin, rowMax);
        var label = Typography.FitText(Loc.T(L.Aethergram.EarlierComments), MathF.Max(1f, width - CellPadX * 2f * scale),
            EarlierCommentsStyle);
        var size = Typography.Measure(label, EarlierCommentsStyle);
        Typography.Draw(drawList, new Vector2(origin.X + CellPadX * scale, origin.Y + (height - size.Y) * 0.5f), label,
            hovered ? Ink.TitleInk : Ink.MutedInk, EarlierCommentsStyle);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(origin, rowMax, hovered))
        {
            store.LoadMoreComments();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawComment(CommentDto comment)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var padX = CellPadX * scale;
        var padY = CommentPadY * scale;
        var radius = CommentAvatarRadius * scale;
        var mine = store.Me is { } me && me.Id == comment.AuthorId;
        var ownsPost = store.Me is { } viewer && store.DetailPost is { } detailPost && viewer.Id == detailPost.AuthorId;
        var canDelete = mine || ownsPost;
        var avatarCenter = new Vector2(origin.X + padX + radius, origin.Y + padY + radius);
        var textLeft = avatarCenter.X + radius + CommentAvatarGap * scale;
        var textRight = origin.X + width - padX - CommentHeartColumn * scale;
        var textWidth = MathF.Max(1f, textRight - textLeft);
        var displayName = SocialIdentity.Name(comment.AuthorDisplayName, comment.AuthorHandle);
        var nameHeight = Typography.LineHeight(CommentNameStyle);
        var commentKey = new TranslationKey(TranslationSurface.Comment, comment.Id);
        var commentView = translation.View(commentKey, comment.Text, comment.Lang);
        var commentText = commentView.Text;
        RichTextLayout? commentLayout;
        using (Plugin.Fonts.Push(CommentBodyStyle.Scale))
        {
            commentLayout = commentLayouts.LayoutFor(commentView.LayoutKey, commentText, comment.Mentions, textWidth);
        }

        var textHeight = commentText.Length == 0
            ? 0f
            : commentLayout?.Size.Y ?? Typography.MeasureWrapped(commentText, textWidth, CommentBodyStyle.Scale);
        var textGap = textHeight > 0f ? CommentTextGap * scale : 0f;
        var linkHeight = TranslateLink.Height(translation, commentKey, comment.Lang, scale);
        var mediaHidden = CommentMediaHidden(comment.MediaUrl);
        var mediaHeight = mediaHidden ? 0f : CommentMedia.MeasureHeight(comment, textWidth, scale);
        var mediaGap = mediaHeight > 0f && (textHeight > 0f || linkHeight > 0f) ? CommentMediaGap * scale : 0f;
        var contentHeight = nameHeight + textGap + textHeight + linkHeight + mediaGap + mediaHeight;
        var rowHeight = padY * 2f + MathF.Max(contentHeight, radius * 2f);
        var rowMax = new Vector2(origin.X + width, origin.Y + rowHeight);

        DrawAvatar(avatarCenter, radius, displayName, string.Empty, comment.AuthorAvatarUrl, 0.8f, 28,
            Frames.Of(comment.AuthorFrameId));
        var nameTop = origin.Y + padY;
        var nameWidth = UserName.DrawAuto(drawList, "aethergram.comment." + comment.Id, displayName,
            comment.AuthorBadges, comment.AuthorBadgeIds, textLeft, nameTop, textWidth, CommentNameStyle,
            Ink.TitleInk, theme);
        var metaLeft = textLeft + nameWidth + CommentMetaGap * scale;
        var meta = TimeText.Short(comment.CreatedAtUnix);
        var metaSize = Typography.Measure(meta, CommentMetaStyle);
        if (metaLeft + metaSize.X <= textRight)
        {
            var metaTop = nameTop + (nameHeight - metaSize.Y) * 0.5f;
            Typography.Draw(drawList, new Vector2(metaLeft, metaTop), meta, Ink.MutedInk, CommentMetaStyle);
            CommentReviewTag.Draw(new Vector2(metaLeft + metaSize.X + CommentMetaGap * scale, metaTop), textRight,
                comment.ScanStatus, CommentMetaStyle.Scale);
        }

        var textTop = nameTop + nameHeight + textGap;
        if (textHeight > 0f)
        {
            if (commentLayout is null)
            {
                ImGui.SetCursorScreenPos(new Vector2(textLeft, textTop));
                using (Typography.WrapAt(textRight))
                using (ImRaii.PushColor(ImGuiCol.Text, Ink.BodyInk))
                using (Plugin.Fonts.Push(CommentBodyStyle.Scale))
                {
                    Typography.Wrapped(commentText);
                }
            }
            else
            {
                using (Plugin.Fonts.Push(CommentBodyStyle.Scale))
                {
                    DrawRichBody(drawList, commentLayout, new Vector2(textLeft, textTop));
                }
            }
        }

        if (linkHeight > 0f)
        {
            TranslateLink.Draw(translation, confirm, commentKey, comment.Lang, comment.Text,
                new Vector2(textLeft, textTop + textHeight), textWidth, Ink.MutedInk, Ink.AccentLink, scale);
        }

        if (comment.MediaUrl is { } commentMediaUrl && !mediaHidden)
        {
            var mediaRect = CommentMedia.Draw(drawList, images, comment,
                new Vector2(textLeft, textTop + textHeight + linkHeight + mediaGap), textWidth, scale, Ink.ThumbFill,
                Ink.MutedInk);
            if (UiInteract.HoverClick(mediaRect.Min, mediaRect.Max))
            {
                photoViewer.Open(this, () => GifMedia.Texture(images, commentMediaUrl, ImGui.GetTime()));
            }
        }

        var avatarExtent = new Vector2(radius, radius);
        if (UiInteract.HoverClick(avatarCenter - avatarExtent, avatarCenter + avatarExtent)
            || UiInteract.HoverClick(new Vector2(textLeft, nameTop), new Vector2(textLeft + nameWidth, nameTop + nameHeight)))
        {
            OpenProfile(comment.AuthorId);
        }

        DrawCommentHeart(drawList, comment, new Vector2(origin.X + width - padX - CommentHeartSize * 0.5f * scale,
            nameTop + nameHeight * 0.5f));
        if (canDelete && UiInteract.Hover(origin, rowMax) && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            OpenCommentSheet(comment, mine);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight));
    }

    private void DrawCommentHeart(ImDrawListPtr drawList, CommentDto comment, Vector2 center)
    {
        var scale = UiScale.Current;
        var hit = new Vector2(CommentHeartHitRadius * scale, CommentHeartHitRadius * scale);
        var hovered = UiInteract.Hover(center - hit, center + hit);
        var ink = comment.Liked ? Ink.LikeRed : hovered ? Ink.TitleInk : Ink.MutedInk;
        PhoneIcon.Draw(drawList, center, comment.Liked ? PhoneIcons.HeartFilled : PhoneIcons.Heart, ink,
            CommentHeartSize * scale);
        if (comment.LikeCount > 0)
        {
            var count = CountText.Compact(comment.LikeCount);
            var size = Typography.Measure(count, CommentCountStyle);
            Typography.Draw(drawList,
                new Vector2(center.X - size.X * 0.5f, center.Y + CommentHeartSize * 0.5f * scale + CommentCountGap * scale),
                count, Ink.MutedInk, CommentCountStyle);
        }

        HoverTooltip.Show(new Rect(center - hit, center + hit), Loc.T(L.Aethergram.Like), HoverLabelSide.Above);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(center - hit, center + hit, hovered))
        {
            store.ToggleCommentLike(comment);
        }
    }

    private void OpenCommentSheet(CommentDto comment, bool mine)
    {
        commentSheetCommentId = comment.Id;
        commentSheetMine = mine;
        commentSheetItems[0] = new ActionSheet.Item(
            Loc.T(mine ? L.Aethergram.DeleteComment : L.Aethergram.RemoveComment), string.Empty, true);
        commentSheet.Open();
    }

    private void DrawCommentSheet(Rect screen)
    {
        if (!commentSheet.CapturesPointer)
        {
            return;
        }

        if (commentSheet.IsOpen && router.Current.Screen != AethergramScreen.Detail)
        {
            commentSheet.Close();
        }

        var picked = commentSheet.Draw(screen, ActionSheetStyle.From(ui), commentSheetItems, Loc.T(L.Common.Cancel),
            false);
        if (picked != 0 || commentSheetCommentId is not { } commentId || store.DetailPost is not { } post)
        {
            return;
        }

        if (commentSheetMine)
        {
            profile.AskDeleteComment(post.Id, commentId);
            return;
        }

        profile.AskRemoveComment(post.Id, commentId);
    }

    private void DrawCommentComposer(Rect bar, Rect screen, PostDto post)
    {
        var returned = Interlocked.Exchange(ref commentRestore, null);
        if (returned is not null)
        {
            commentDraft = returned;
        }

        var returnedAttachment = Interlocked.Exchange(ref commentAttachmentRestore, null);
        if (returnedAttachment is not null)
        {
            commentAttachment.Restore(returnedAttachment);
        }

        var scale = UiScale.Current;
        if (commentFailure.Failed)
        {
            Typography.DrawWrappedCentered(new Vector2(bar.Center.X,
                    bar.Min.Y - ComposerFailureLift * scale - commentAttachment.StripHeight(scale)),
                commentFailure.Text(), Ink.Danger, TextStyles.Footnote, bar.Width - CellPadX * 2f * scale);
        }

        var drawList = ImGui.GetWindowDrawList();
        PaintBarBackdrop(drawList, bar);
        DrawHairline(drawList, bar.Min.X, bar.Max.X, bar.Min.Y + 1f);
        var fieldLeft = bar.Min.X;
        if (store.Me is { } me)
        {
            var radius = ComposerAvatarRadius * scale;
            var avatarCenter = new Vector2(bar.Min.X + CellPadX * scale + radius, bar.Center.Y);
            DrawAvatar(avatarCenter, radius, me.Name, me.World, me.AvatarUrl, 0.9f, 32, Frames.Of(me.FrameId));
            fieldLeft = avatarCenter.X + radius - 2f * scale;
        }

        var style = new CommentComposerStyle(AppSkin.Transparent, Ink.FieldFill, Ink.TitleInk, Ink.Accent,
            AppSkin.Transparent, Ink.White, true, 11f, 56f, 1f, 19f);
        var canSend = !string.IsNullOrWhiteSpace(commentDraft) || commentAttachment.Path is not null;
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        commentSendReveal.Step(canSend ? 1f : 0f, CommentSendRevealSmoothTime, delta);
        var hint = post.AuthorDisplayName.Length > 0 || post.AuthorHandle.Length > 0
            ? Loc.T(L.Aethergram.AddCommentFor, SocialIdentity.Name(post.AuthorDisplayName, post.AuthorHandle))
            : Loc.T(L.Aethergram.AddComment);
        var fieldBar = new Rect(new Vector2(fieldLeft, bar.Min.Y), bar.Max);
        if (CommentComposerBar.Draw(fieldBar, screen, ui, theme, style, "##gramComment", hint, ref commentDraft,
                MaxCommentLength, commentMentions, mentionPopup, images, lodestone, store.Commenting,
                ref commentFocusPending, commentEmoji, commentAttachment, library, wallpaperImages,
                commentSendReveal.Value))
        {
            var text = commentDraft;
            var attachmentPath = commentAttachment.Path;
            var postId = post.Id;
            commentDraft = string.Empty;
            commentAttachment.Clear();
            commentFailure.Clear();
            store.AddComment(postId, text, attachmentPath, accepted =>
            {
                if (accepted)
                {
                    return;
                }

                commentRestore = text;
                commentAttachmentRestore = attachmentPath;
            }, commentFailure.Set);
        }
    }
}
