using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Emoji;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Translation;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Chirper;

internal sealed partial class ChirperApp
{
    private const float HeadAvatarRadius = 24f;
    private const float HeadMediaMaxHeight = 360f;
    private const float ThreadLineWidth = 2f;
    private const float ThreadActionHeight = 46f;
    private const float ReplyActionHeight = 32f;
    private const float ReplyComposerHeight = 60f;
    private const string SourceLabel = "Aetherphone";

    private static readonly TextStyle HeadNameStyle = new(1f, FontWeight.SemiBold);
    private static readonly TextStyle HeadHandleStyle = new(0.93f, FontWeight.Regular);
    private static readonly TextStyle StatNumberStyle = new(0.97f, FontWeight.Bold);
    private static readonly TextStyle StatWordStyle = new(0.93f, FontWeight.Regular);
    private static readonly Vector4 ThreadLine = new(1f, 1f, 1f, 0.14f);

    private CommentDto? sheetComment;

    private void DrawThread(Rect area, string postId)
    {
        var post = store.DetailPost;
        DrawCenteredHeader(area, Loc.T(L.Chirper.ThreadTitle));
        var scale = UiScale.Current;
        var top = area.Min.Y + AppHeader.Height * scale;
        if (post is null || post.Id != postId)
        {
            if (post is null && !store.DetailLoading)
            {
                back();
                return;
            }

            Typography.DrawCentered(new Vector2(area.Center.X, top + 60f * scale), Loc.T(L.Common.Loading),
                ChirperInk.MutedInk);
            return;
        }

        var composerHeight = ReplyComposerHeight * scale;
        var body = new Rect(new Vector2(area.Min.X, top), new Vector2(area.Max.X, area.Max.Y - composerHeight));
        using (AppSurface.BeginEdgeToEdge(body))
        {
            ImGui.Dummy(new Vector2(0f, 6f * scale));
            DrawThreadHead(post);
            var comments = store.DetailComments;
            if (comments.Length == 0)
            {
                if (!store.DetailLoading)
                {
                    Typography.DrawCentered(new Vector2(body.Center.X, ImGui.GetCursorScreenPos().Y + 36f * scale),
                        Loc.T(L.Chirper.NoComments), ChirperInk.MutedInk, MetaStyle);
                }
            }
            else
            {
                DrawEarlierRepliesRow();
                var visibleCount = 0;
                for (var index = 0; index < comments.Length; index++)
                {
                    if (!HiddenByMediaPreference(comments[index]))
                    {
                        visibleCount++;
                    }
                }

                var drawn = 0;
                for (var index = 0; index < comments.Length; index++)
                {
                    if (HiddenByMediaPreference(comments[index]))
                    {
                        continue;
                    }

                    DrawReply(comments[index], post, drawn == 0, drawn == visibleCount - 1);
                    drawn++;
                }
            }

            ImGui.Dummy(new Vector2(0f, 24f * scale));
        }

        DrawCommentComposer(new Rect(new Vector2(area.Min.X, area.Max.Y - composerHeight), area.Max), area, postId);
    }

    private void DrawCenteredHeader(Rect area, string title)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        var backCenter = new Vector2(area.Min.X + CellPadX * scale + 9f * scale, rowCenterY);
        var hitHalf = 22f * scale;
        var hitMin = backCenter - new Vector2(hitHalf, hitHalf);
        var hitMax = backCenter + new Vector2(hitHalf, hitHalf);
        var hovered = UiInteract.Hover(hitMin, hitMax);
        PhoneIcon.Draw(drawList, backCenter, PhoneIcons.ChevronLeft,
            hovered ? ChirperInk.White : ChirperInk.TitleInk, 20f * scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(hitMin, hitMax, hovered))
        {
            back();
        }

        var fitted = Typography.FitText(title, MathF.Max(1f, area.Width - 120f * scale), ScreenTitleStyle);
        Typography.DrawCentered(drawList, new Vector2(area.Center.X, rowCenterY), fitted, ChirperInk.TitleInk,
            ScreenTitleStyle);
    }

    private void DrawThreadHead(PostDto post)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var padX = CellPadX * scale;
        var left = origin.X + padX;
        var right = origin.X + width - padX;
        var contentWidth = MathF.Max(1f, right - left);
        var avatarRadius = HeadAvatarRadius * scale;
        var avatarCenter = new Vector2(left + avatarRadius, origin.Y + avatarRadius);
        var rawDisplayName = SocialIdentity.Name(post.AuthorDisplayName, post.AuthorHandle);
        DrawAvatar(drawList, avatarCenter, avatarRadius, rawDisplayName, string.Empty, post.AuthorAvatarUrl, 1f, 48,
            Frames.Of(post.AuthorFrameId));
        if (UiInteract.HoverClick(avatarCenter - new Vector2(avatarRadius, avatarRadius),
                avatarCenter + new Vector2(avatarRadius, avatarRadius)))
        {
            OpenProfile(post.AuthorId);
        }

        var moreRadius = MoreButtonRadius * scale;
        var moreCenter = new Vector2(right - moreRadius + 4f * scale, origin.Y + moreRadius - 2f * scale);
        var moreExtent = new Vector2(moreRadius, moreRadius);
        var moreHovered = UiInteract.Hover(moreCenter - moreExtent, moreCenter + moreExtent);
        if (moreHovered)
        {
            drawList.AddCircleFilled(moreCenter, moreRadius, ImGui.GetColorU32(ChirperInk.AccentWash), 24);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        PhoneIcon.Draw(drawList, moreCenter, PhoneIcons.Dots,
            moreHovered ? ChirperInk.Accent : ChirperInk.MutedInk, 16f * scale);
        HoverTooltip.Show(new Rect(moreCenter - moreExtent, moreCenter + moreExtent), Loc.T(L.Chirper.More),
            HoverLabelSide.Above);
        if (UiInteract.Click(moreCenter - moreExtent, moreCenter + moreExtent, moreHovered))
        {
            OpenPostSheet(post);
        }

        var nameLeft = avatarCenter.X + avatarRadius + 12f * scale;
        var headerRight = moreCenter.X - moreRadius - 6f * scale;
        var nameHeight = Typography.LineHeight(HeadNameStyle);
        var nameTop = origin.Y + 3f * scale;
        var drawnNameWidth = UserName.DrawAuto(drawList, "chirper.head.author." + post.Id, rawDisplayName,
            post.AuthorBadges, post.AuthorBadgeIds, nameLeft, nameTop, MathF.Max(1f, headerRight - nameLeft),
            HeadNameStyle, ChirperInk.TitleInk, theme);
        if (UiInteract.HoverClick(new Vector2(nameLeft, nameTop), new Vector2(nameLeft + drawnNameWidth, nameTop + nameHeight)))
        {
            OpenProfile(post.AuthorId);
        }

        var handleText = post.AuthorHandle.Length > 0 ? "@" + post.AuthorHandle : TimeText.Short(post.CreatedAtUnix);
        if (ContentModeration.IsInReview(post.ScanStatus))
        {
            handleText = $"{handleText} · {Loc.T(L.Moderation.InReview)}";
        }

        Typography.Draw(drawList, new Vector2(nameLeft, nameTop + nameHeight + 1f * scale),
            Typography.FitText(handleText, MathF.Max(1f, headerRight - nameLeft), HeadHandleStyle), ChirperInk.MutedInk,
            HeadHandleStyle);

        var cursorY = origin.Y + avatarRadius * 2f + 12f * scale;
        var translateKey = new TranslationKey(TranslationSurface.Post, post.Id);
        var bodyView = translation.View(translateKey, post.Text, post.Lang);
        var bodyText = bodyView.Text;
        if (bodyText.Length > 0)
        {
            RichTextLayout? bodyLayout;
            using (Plugin.Fonts.Push(HeadBodyStyle.Scale))
            {
                bodyLayout = bodyLayouts.LayoutFor(bodyView.LayoutKey, bodyText, post.Mentions, contentWidth);
            }

            if (bodyLayout is null)
            {
                ImGui.SetCursorScreenPos(new Vector2(left, cursorY));
                using (Typography.WrapAt(right))
                using (Plugin.Fonts.Push(HeadBodyStyle.Scale))
                using (ImRaii.PushColor(ImGuiCol.Text, ChirperInk.BodyInk))
                {
                    Typography.Wrapped(bodyText);
                }

                cursorY += Typography.MeasureWrapped(bodyText, contentWidth, HeadBodyStyle.Scale);
            }
            else
            {
                using (Plugin.Fonts.Push(HeadBodyStyle.Scale))
                {
                    DrawRichBody(drawList, bodyLayout, new Vector2(left, cursorY));
                }

                cursorY += bodyLayout.Size.Y;
            }

            cursorY += TranslateLink.Draw(translation, confirm, translateKey, post.Lang, post.Text,
                new Vector2(left, cursorY), contentWidth, ChirperInk.MutedInk, ChirperInk.AccentLink, scale);
        }

        var photos = PostMedia.Photos(post.MediaUrls, post.MediaUrl);
        if (photos.Length > 0)
        {
            cursorY += 12f * scale;
            var mediaHeight = photos.Length == 1
                ? MathF.Min(HeadMediaMaxHeight * scale,
                    PostAspects.DisplayHeight(contentWidth, post.MediaWidth, post.MediaHeight))
                : MediaBlockHeight(photos.Length, true);
            DrawPostMedia(post, photos, new Rect(new Vector2(left, cursorY), new Vector2(right, cursorY + mediaHeight)));
            cursorY += mediaHeight;
        }

        if (post.QuotedPostId is not null)
        {
            cursorY += 12f * scale;
            var quoteHeight = QuotedCardHeight(post.ReferencedPost, contentWidth);
            DrawQuotedCard(drawList, new Vector2(left, cursorY), contentWidth, quoteHeight, post.ReferencedPost, true,
                post.Id);
            cursorY += quoteHeight;
        }

        if (post.CreatedAtUnix > 0)
        {
            cursorY += 12f * scale;
            var stamp = Typography.FitText($"{FullTimestamp(post.CreatedAtUnix)} · {SourceLabel}", contentWidth,
                HeadHandleStyle);
            Typography.Draw(drawList, new Vector2(left, cursorY), stamp, ChirperInk.MutedInk, HeadHandleStyle);
            cursorY += Typography.LineHeight(HeadHandleStyle);
        }

        cursorY += 12f * scale;
        DrawHairline(drawList, left, right, cursorY);
        cursorY = DrawThreadStats(drawList, post, left, right, cursorY);
        cursorY = DrawThreadActions(post, origin.X, width, cursorY);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cursorY - origin.Y));
    }

    private float DrawThreadStats(ImDrawListPtr drawList, PostDto post, float left, float right, float top)
    {
        if (post.TotalReactions <= 0 && post.RepostCount <= 0 && post.CommentCount <= 0)
        {
            return top;
        }

        var scale = UiScale.Current;
        var height = RowHeight * scale;
        var centerY = top + height * 0.5f;
        var cursorX = left;
        if (post.TotalReactions > 0)
        {
            cursorX = DrawThreadStat(drawList, cursorX, centerY, right, post.TotalReactions, L.Chirper.Likes, true,
                out var likesClicked);
            if (likesClicked)
            {
                OpenUserList(post.Id, UserListKind.Likers);
            }
        }

        if (post.RepostCount > 0)
        {
            cursorX = DrawThreadStat(drawList, cursorX, centerY, right, post.RepostCount, L.Chirper.Reposts, false,
                out _);
        }

        if (post.CommentCount > 0)
        {
            DrawThreadStat(drawList, cursorX, centerY, right, post.CommentCount, L.Chirper.RepliesCount, false, out _);
        }

        DrawHairline(drawList, left, right, top + height);
        return top + height;
    }

    private static float DrawThreadStat(ImDrawListPtr drawList, float left, float centerY, float limit, int count,
        LocPlural entry, bool tappable, out bool clicked)
    {
        var scale = UiScale.Current;
        var number = CountText.Compact(count);
        var formatted = Loc.Plural(entry, count);
        var word = formatted.StartsWith(number, StringComparison.Ordinal)
            ? formatted[number.Length..].Trim()
            : formatted;
        var numberSize = Typography.Measure(number, StatNumberStyle);
        var wordFitted = Typography.FitText(word, MathF.Max(1f, limit - left - numberSize.X - 5f * scale), StatWordStyle);
        var wordSize = Typography.Measure(wordFitted, StatWordStyle);
        var min = new Vector2(left, centerY - numberSize.Y * 0.5f);
        var max = new Vector2(left + numberSize.X + 5f * scale + wordSize.X, centerY + numberSize.Y * 0.5f);
        var hovered = tappable && UiInteract.Hover(min, max);
        Typography.Draw(drawList, min, number, hovered ? ChirperInk.AccentLink : ChirperInk.TitleInk, StatNumberStyle);
        Typography.Draw(drawList, new Vector2(min.X + numberSize.X + 5f * scale, centerY - wordSize.Y * 0.5f), wordFitted,
            hovered ? ChirperInk.AccentLink : ChirperInk.MutedInk, StatWordStyle);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        clicked = tappable && UiInteract.Click(min, max, hovered);
        return max.X + 16f * scale;
    }

    private float DrawThreadActions(PostDto post, float rowLeft, float rowWidth, float top)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var height = ThreadActionHeight * scale;
        var centerY = top + height * 0.5f;
        var slot = rowWidth / 4f;
        var replyCount = string.Empty;
        if (DrawSpreadTarget(drawList, rowLeft, slot, centerY, height, ActionGlyph.Reply, replyCount, ChirperInk.MutedInk,
                ChirperInk.AccentLink, Loc.T(L.Chirper.Reply)))
        {
            replyFocusPending = true;
        }

        var repostInk = post.MyReposted ? ChirperInk.RechirpGreen : ChirperInk.MutedInk;
        if (DrawSpreadTarget(drawList, rowLeft + slot, slot, centerY, height, ActionGlyph.Rechirp, string.Empty, repostInk,
                ChirperInk.RechirpGreen, Loc.T(post.MyReposted ? L.Chirper.Unrepost : L.Chirper.Repost)))
        {
            actions.Open(post.Id, ChirperPanel.Repost);
        }

        var reactMin = new Vector2(rowLeft + slot * 2f, top);
        var reactMax = new Vector2(rowLeft + slot * 3f, top + height);
        var reactHovered = UiInteract.Hover(reactMin, reactMax);
        var reactCenter = new Vector2(rowLeft + slot * 2.5f, centerY);
        if (post.MyReaction >= 0)
        {
            var emojiHalf = 10f * scale * (reactHovered ? 1.12f : 1f);
            EmojiImages.TryDraw(drawList, ChirperReactions.EmojiFile(post.MyReaction), reactCenter - new Vector2(emojiHalf, emojiHalf),
                reactCenter + new Vector2(emojiHalf, emojiHalf), 0xFFFFFFFFu);
        }
        else
        {
            PhoneIcon.Draw(drawList, reactCenter, PhoneIcons.MoodSmile,
                reactHovered ? ChirperInk.Warning : ChirperInk.MutedInk, 21f * scale);
        }

        if (reactHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        HoverTooltip.Show(new Rect(reactMin, reactMax), Loc.T(L.Chirper.React), HoverLabelSide.Above);
        if (UiInteract.Click(reactMin, reactMax, reactHovered))
        {
            actions.Open(post.Id, ChirperPanel.Picker);
        }

        if (DrawSpreadTarget(drawList, rowLeft + slot * 3f, slot, centerY, height, ActionGlyph.Share, string.Empty,
                ChirperInk.MutedInk, ChirperInk.AccentLink, Loc.T(L.Chirper.CopyChirp)))
        {
            CopyChirp(post);
        }

        DrawHairline(drawList, rowLeft, rowLeft + rowWidth, top + height);
        var popoverBottom = top - 4f * scale;
        if (actions.IsShowing(post.Id, ChirperPanel.Picker))
        {
            DrawReactionPicker(post, rowLeft + CellPadX * scale, rowLeft + rowWidth - CellPadX * scale, popoverBottom);
        }
        else if (actions.IsShowing(post.Id, ChirperPanel.Repost))
        {
            DrawRepostMenu(post, rowLeft + slot, popoverBottom);
        }

        return top + height;
    }

    private static bool DrawSpreadTarget(ImDrawListPtr drawList, float slotLeft, float slotWidth, float centerY,
        float height, ActionGlyph glyph, string count, Vector4 ink, Vector4 hoverInk, string tooltip)
    {
        var scale = UiScale.Current;
        var min = new Vector2(slotLeft, centerY - height * 0.5f);
        var max = new Vector2(slotLeft + slotWidth, centerY + height * 0.5f);
        var hovered = UiInteract.Hover(min, max);
        var color = hovered ? hoverInk : ink;
        var packed = ImGui.GetColorU32(color);
        var iconSize = 20f * scale;
        var countSize = count.Length > 0 ? Typography.Measure(count, CountStyle) : Vector2.Zero;
        var groupWidth = iconSize + (count.Length > 0 ? 5f * scale + countSize.X : 0f);
        var iconCenter = new Vector2(slotLeft + (slotWidth - groupWidth) * 0.5f + iconSize * 0.5f, centerY);
        switch (glyph)
        {
            case ActionGlyph.Reply:
                PhoneIcon.Draw(drawList, iconCenter, PhoneIcons.MessageCircle, packed, iconSize);
                break;
            case ActionGlyph.Rechirp:
                PhoneIcon.Draw(drawList, iconCenter, PhoneIcons.Repeat, packed, iconSize);
                break;
            default:
                PhoneIcon.Draw(drawList, iconCenter, PhoneIcons.Share, packed, iconSize);
                break;
        }

        if (count.Length > 0)
        {
            Typography.Draw(drawList, new Vector2(iconCenter.X + iconSize * 0.5f + 5f * scale, centerY - countSize.Y * 0.5f),
                count, color, CountStyle);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        HoverTooltip.Show(new Rect(min, max), tooltip, HoverLabelSide.Above);
        return UiInteract.Click(min, max, hovered);
    }

    private void DrawEarlierRepliesRow()
    {
        var scale = UiScale.Current;
        if (store.CommentsLoadingMore)
        {
            InfiniteScroll.DrawLoadingRow(
                ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X * 0.5f, ChirperInk.MutedInk);
            return;
        }

        if (!store.HasMoreComments)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var label = Loc.T(L.Chirper.EarlierComments);
        var size = Typography.Measure(label, CapsuleStyle);
        var height = 30f * scale;
        var padX = 14f * scale;
        var min = new Vector2(origin.X + CellPadX * scale, origin.Y + 10f * scale);
        var max = new Vector2(min.X + size.X + padX * 2f, min.Y + height);
        var hovered = UiInteract.Hover(min, max);
        Squircle.Fill(drawList, min, max, height * 0.5f,
            ImGui.GetColorU32(hovered ? Palette.WithAlpha(ChirperInk.Accent, 0.24f) : ChirperInk.MineFill));
        Typography.DrawCentered(drawList, (min + max) * 0.5f, label, ChirperInk.AccentLink, CapsuleStyle);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(min, max, hovered))
        {
            store.LoadMoreComments();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + 14f * scale));
    }

    private void DrawReply(CommentDto comment, PostDto post, bool first, bool last)
    {
        var scale = UiScale.Current;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var drawList = ImGui.GetWindowDrawList();
        var padX = CellPadX * scale;
        var padY = ReplyPadY * scale;
        var radius = ReplyAvatarRadius * scale;
        var contentRight = origin.X + width - padX;
        var avatarCenter = new Vector2(origin.X + padX + radius, origin.Y + padY + radius);
        var textLeft = avatarCenter.X + radius + 10f * scale;
        var bodyWidth = MathF.Max(1f, contentRight - textLeft);
        var rawDisplayName = SocialIdentity.Name(comment.AuthorDisplayName, comment.AuthorHandle);
        var mine = store.Me is { } me && me.Id == comment.AuthorId;
        var threadOwner = store.Me is { } owner && owner.Id == post.AuthorId;
        var canModerate = mine || threadOwner;
        var nameHeight = Typography.LineHeight(ReplyNameStyle);
        var commentKey = new TranslationKey(TranslationSurface.Comment, comment.Id);
        var commentView = translation.View(commentKey, comment.Text, comment.Lang);
        var commentText = commentView.Text;
        RichTextLayout? commentLayout = null;
        if (commentText.Length > 0)
        {
            using (Plugin.Fonts.Push(ReplyBodyStyle.Scale))
            {
                commentLayout = commentLayouts.LayoutFor(commentView.LayoutKey, commentText, comment.Mentions, bodyWidth);
            }
        }

        var textHeight = commentText.Length == 0 ? 0f
            : commentLayout?.Size.Y ?? Typography.MeasureWrapped(commentText, bodyWidth, ReplyBodyStyle.Scale);
        var translateHeight = TranslateLink.Height(translation, commentKey, comment.Lang, scale);
        var mediaHeight = comment.MediaUrl is not null && !CommentMediaHidden(comment.MediaUrl)
            ? CommentMedia.MeasureHeight(comment, bodyWidth, scale) + (commentText.Length > 0 ? 6f * scale : 0f)
            : 0f;
        var bodyTop = origin.Y + padY + nameHeight + 3f * scale;
        var actionsTop = bodyTop + textHeight + translateHeight + mediaHeight + 4f * scale;
        var bottom = MathF.Max(actionsTop + ReplyActionHeight * scale, avatarCenter.Y + radius) + padY;

        if (!first)
        {
            drawList.AddLine(new Vector2(avatarCenter.X, origin.Y), new Vector2(avatarCenter.X, avatarCenter.Y - radius - 4f * scale),
                ImGui.GetColorU32(ThreadLine), ThreadLineWidth * scale);
        }

        if (!last)
        {
            drawList.AddLine(new Vector2(avatarCenter.X, avatarCenter.Y + radius + 4f * scale), new Vector2(avatarCenter.X, bottom),
                ImGui.GetColorU32(ThreadLine), ThreadLineWidth * scale);
        }

        DrawAvatar(drawList, avatarCenter, radius, rawDisplayName, string.Empty, comment.AuthorAvatarUrl, 0.85f, 32,
            Frames.Of(comment.AuthorFrameId));
        if (UiInteract.HoverClick(avatarCenter - new Vector2(radius, radius), avatarCenter + new Vector2(radius, radius)))
        {
            OpenProfile(comment.AuthorId);
        }

        var headerTop = origin.Y + padY;
        var headerCenterY = headerTop + nameHeight * 0.5f;
        var headerRight = contentRight;
        if (canModerate)
        {
            var moreCenter = new Vector2(contentRight - 8f * scale, headerCenterY);
            var moreExtent = new Vector2(MoreButtonRadius * scale, MoreButtonRadius * scale);
            var moreHovered = UiInteract.Hover(moreCenter - moreExtent, moreCenter + moreExtent);
            if (moreHovered)
            {
                drawList.AddCircleFilled(moreCenter, MoreButtonRadius * scale, ImGui.GetColorU32(ChirperInk.AccentWash), 24);
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            PhoneIcon.Draw(drawList, moreCenter, PhoneIcons.Dots,
                moreHovered ? ChirperInk.Accent : ChirperInk.MutedInk, 15f * scale);
            HoverTooltip.Show(new Rect(moreCenter - moreExtent, moreCenter + moreExtent), Loc.T(L.Chirper.More),
                HoverLabelSide.Above);
            if (UiInteract.Click(moreCenter - moreExtent, moreCenter + moreExtent, moreHovered))
            {
                OpenReplySheet(comment, mine);
            }

            headerRight = moreCenter.X - moreExtent.X - 6f * scale;
        }

        var headerWidth = MathF.Max(1f, headerRight - textLeft);
        var drawnNameWidth = UserName.DrawAuto(drawList, "chirper.reply.author." + comment.Id, rawDisplayName,
            comment.AuthorBadges, comment.AuthorBadgeIds, textLeft, headerTop, headerWidth * 0.55f, ReplyNameStyle,
            ChirperInk.TitleInk, theme);
        if (UiInteract.HoverClick(new Vector2(textLeft, headerTop), new Vector2(textLeft + drawnNameWidth, headerTop + nameHeight)))
        {
            OpenProfile(comment.AuthorId);
        }

        var metaLeft = textLeft + drawnNameWidth + 5f * scale;
        var meta = SocialIdentity.FeedMeta(comment.AuthorHandle, TimeText.Short(comment.CreatedAtUnix));
        var metaFitted = Typography.FitText(meta, MathF.Max(1f, headerRight - metaLeft), ReplyMetaStyle);
        var metaSize = Typography.Measure(metaFitted, ReplyMetaStyle);
        var metaY = headerCenterY - metaSize.Y * 0.5f;
        Typography.Draw(drawList, new Vector2(metaLeft, metaY), metaFitted, ChirperInk.MutedInk, ReplyMetaStyle);
        CommentReviewTag.Draw(new Vector2(metaLeft + metaSize.X + 6f * scale, metaY), headerRight, comment.ScanStatus,
            ReplyMetaStyle.Scale);

        if (commentText.Length > 0)
        {
            if (commentLayout is null)
            {
                ImGui.SetCursorScreenPos(new Vector2(textLeft, bodyTop));
                using (Typography.WrapAt(contentRight))
                using (Plugin.Fonts.Push(ReplyBodyStyle.Scale))
                using (ImRaii.PushColor(ImGuiCol.Text, ChirperInk.BodyInk))
                {
                    Typography.Wrapped(commentText);
                }
            }
            else
            {
                using (Plugin.Fonts.Push(ReplyBodyStyle.Scale))
                {
                    DrawRichBody(drawList, commentLayout, new Vector2(textLeft, bodyTop));
                }
            }
        }

        var cursorY = bodyTop + textHeight;
        if (translateHeight > 0f)
        {
            TranslateLink.Draw(translation, confirm, commentKey, comment.Lang, comment.Text, new Vector2(textLeft, cursorY),
                bodyWidth, ChirperInk.MutedInk, ChirperInk.AccentLink, scale);
            cursorY += translateHeight;
        }

        if (comment.MediaUrl is { } commentMediaUrl && !CommentMediaHidden(commentMediaUrl))
        {
            var mediaTop = cursorY + (commentText.Length > 0 ? 6f * scale : 0f);
            var mediaRect = CommentMedia.Draw(drawList, images, comment, new Vector2(textLeft, mediaTop), bodyWidth, scale,
                ChirperInk.ChipFill, ChirperInk.MutedInk);
            if (UiInteract.HoverClick(mediaRect.Min, mediaRect.Max))
            {
                photoViewer.Open(this, () => MediaTexture(commentMediaUrl));
            }
        }

        DrawReplyActions(drawList, comment, textLeft, contentRight, actionsTop);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, bottom - origin.Y));
    }

    private void DrawReplyActions(ImDrawListPtr drawList, CommentDto comment, float left, float right, float top)
    {
        var scale = UiScale.Current;
        var height = ReplyActionHeight * scale;
        var centerY = top + height * 0.5f;
        var span = right - left;
        var slot = span / 3f;
        var iconSize = 16f * scale;
        var replyMin = new Vector2(left, top);
        var replyMax = new Vector2(left + slot, top + height);
        var replyHovered = UiInteract.Hover(replyMin, replyMax);
        PhoneIcon.Draw(drawList, new Vector2(left + iconSize * 0.5f, centerY), PhoneIcons.MessageCircle,
            replyHovered ? ChirperInk.AccentLink : ChirperInk.MutedInk, iconSize);
        HoverTooltip.Show(new Rect(replyMin, replyMax), Loc.T(L.Chirper.Reply), HoverLabelSide.Above);
        if (replyHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(replyMin, replyMax, replyHovered))
        {
            if (commentDraft.Length == 0 && comment.AuthorHandle.Length > 0)
            {
                commentDraft = "@" + comment.AuthorHandle + " ";
            }

            replyFocusPending = true;
        }

        var likeCount = comment.LikeCount > 0 ? comment.LikeCount.ToString(Loc.Culture) : string.Empty;
        var likeCountSize = likeCount.Length > 0 ? Typography.Measure(likeCount, LikeCountStyle) : Vector2.Zero;
        var heartLeft = left + slot;
        var heartMin = new Vector2(heartLeft, top);
        var heartMax = new Vector2(heartLeft + slot, top + height);
        var heartHovered = UiInteract.Hover(heartMin, heartMax);
        var heartCenter = new Vector2(heartLeft + iconSize * 0.5f, centerY);
        var heartInk = comment.Liked || heartHovered ? ChirperInk.LikeRed : ChirperInk.MutedInk;
        if (comment.Liked)
        {
            PhoneIcon.Draw(drawList, heartCenter, PhoneIcons.HeartFilled, heartInk, iconSize);
        }
        else
        {
            PhoneIcon.Draw(drawList, heartCenter, PhoneIcons.Heart, heartInk, iconSize);
        }

        if (likeCount.Length > 0)
        {
            Typography.Draw(drawList, new Vector2(heartCenter.X + iconSize * 0.5f + 5f * scale, centerY - likeCountSize.Y * 0.5f),
                likeCount, comment.Liked ? ChirperInk.LikeRed : ChirperInk.MutedInk, LikeCountStyle);
        }

        HoverTooltip.Show(new Rect(heartMin, heartMax), Loc.T(L.Chirper.ReactLike), HoverLabelSide.Above);
        if (heartHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(heartMin, heartMax, heartHovered))
        {
            store.ToggleCommentLike(comment);
        }

        var shareMin = new Vector2(right - slot, top);
        var shareMax = new Vector2(right, top + height);
        var shareHovered = UiInteract.Hover(shareMin, shareMax);
        PhoneIcon.Draw(drawList, new Vector2(right - iconSize * 0.5f - 8f * scale, centerY), PhoneIcons.Share,
            shareHovered ? ChirperInk.AccentLink : ChirperInk.MutedInk, iconSize);
        HoverTooltip.Show(new Rect(shareMin, shareMax), Loc.T(L.Chirper.CopyChirp), HoverLabelSide.Above);
        if (shareHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(shareMin, shareMax, shareHovered) && comment.Text.Length > 0)
        {
            ImGui.SetClipboardText(comment.Text);
            toast.Show(Loc.T(L.Common.Copied));
        }
    }

    private void OpenReplySheet(CommentDto comment, bool mine)
    {
        actions.Reset();
        sheetComment = comment;
        sheetKind = SheetKind.Reply;
        sheetCount = 0;
        if (mine)
        {
            AddSheetItem(PostSheetAction.DeleteReply, Loc.T(L.Chirper.DeleteComment), true);
        }
        else
        {
            AddSheetItem(PostSheetAction.RemoveReply, Loc.T(L.Chirper.RemoveComment), true);
        }

        sheet.Open();
    }

    private void DrawReplySheet(Rect screen)
    {
        var picked = sheet.Draw(screen, SheetStyle, sheetItems.AsSpan(0, sheetCount), Loc.T(L.Common.Cancel), false);
        if (picked < 0 || sheetComment is not { } comment || store.DetailPost is not { } post)
        {
            return;
        }

        switch (sheetActions[picked])
        {
            case PostSheetAction.DeleteReply:
                profile.AskDeleteComment(post.Id, comment.Id);
                break;
            case PostSheetAction.RemoveReply:
                profile.AskRemoveComment(post.Id, comment.Id);
                break;
        }
    }

    private void DrawCommentComposer(Rect bar, Rect screen, string postId)
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
                    bar.Min.Y - 22f * scale - commentAttachment.StripHeight(scale)),
                commentFailure.Text(), ChirperInk.MutedInk, TextStyles.Footnote, bar.Width - 28f * scale);
        }

        var drawList = ImGui.GetWindowDrawList();
        PaintBarBackdrop(drawList, bar);
        drawList.AddLine(bar.Min, new Vector2(bar.Max.X, bar.Min.Y), ImGui.GetColorU32(ChirperInk.Hairline), 1f);
        var fieldLeft = bar.Min.X;
        if (store.Me is { } me)
        {
            var radius = ReplyAvatarRadius * scale;
            var avatarCenter = new Vector2(bar.Min.X + CellPadX * scale + radius, bar.Center.Y);
            DrawAvatar(drawList, avatarCenter, radius, me.Name, me.World, me.AvatarUrl, 0.9f, 32, Frames.Of(me.FrameId));
            fieldLeft = avatarCenter.X + radius - 2f * scale;
        }

        var style = new CommentComposerStyle(AppSkin.Transparent, ChirperInk.FieldFill, ChirperInk.TitleInk,
            ChirperInk.Accent, AppSkin.Transparent, ChirperInk.White, true, 11f, 56f, 1f, 19f);
        var focusPending = replyFocusPending;
        replyFocusPending = false;
        var canSend = !string.IsNullOrWhiteSpace(commentDraft) || commentAttachment.Path is not null;
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        replySendReveal.Step(canSend ? 1f : 0f, Motion.Appear, delta);
        var fieldBar = new Rect(new Vector2(fieldLeft, bar.Min.Y), bar.Max);
        if (CommentComposerBar.Draw(fieldBar, screen, ui, theme, style, "##chirperComment", Loc.T(L.Chirper.AddComment),
                ref commentDraft, MaxCommentLength, commentMentions, mentionPopup, images, lodestone, store.Commenting,
                ref focusPending, commentEmoji, commentAttachment, library, wallpaperImages, replySendReveal.Value))
        {
            var text = commentDraft;
            var attachmentPath = commentAttachment.Path;
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
