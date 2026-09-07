using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Maps;
using Aetherphone.Core.Muster;
using Aetherphone.Core.YellowPages;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Message;

internal sealed partial class MessageApp
{
    private const float ReceiptRowHeight = 52f;
    private const float ReceiptGlyph = 16f;
    private const float ReceiptAvatarRadius = 17f;
    private const int InfoBubbleMaxChars = 220;

    private static readonly Vector4 ReadTickColor = new(0.45f, 0.83f, 1f, 1f);
    private static readonly TextStyle InfoStampStyle = new(0.70f, FontWeight.Regular);

    private float sinceInfoPoll;

    private void DrawMessageInfo(Rect area, string messageId)
    {
        var scale = UiScale.Current;
        DrawScreenHeader(area, Loc.T(L.Message.InfoTitle));
        var message = store.FindMessage(messageId);
        var conversation = store.Conversation;
        if (message is null || conversation is null)
        {
            return;
        }

        TickMessageInfo();
        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        using (AppSurface.Begin(body))
        {
            var drawList = ImGui.GetWindowDrawList();
            ImGui.Dummy(new Vector2(0f, 10f * scale));
            DrawInfoBubble(drawList, message, scale);
            if (conversation.IsGroup)
            {
                DrawGroupReceipts(drawList, message);
            }
            else
            {
                DrawDirectReceipts(drawList, message);
            }

            ImGui.Dummy(new Vector2(0f, 30f * scale));
        }
    }

    private void TickMessageInfo()
    {
        sinceInfoPoll += ImGui.GetIO().DeltaTime;
        if (sinceInfoPoll < MessageThreadViewBase.ThreadPollSeconds)
        {
            return;
        }

        sinceInfoPoll = 0f;
        store.RefreshThreadDetail();
        store.RefreshThread();
    }

    private void DrawInfoBubble(ImDrawListPtr drawList, ChatMessageDto message, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var paddingX = 11f * scale;
        var paddingY = 7f * scale;
        var wrap = width * 0.74f - paddingX * 2f;
        string text;
        if (message.Kind == 1 && string.IsNullOrEmpty(message.Body))
        {
            text = Loc.T(L.DirectMessages.PhotoPreview);
        }
        else if (LocationShare.TryParse(message.Body, out var location))
        {
            text = LocationShare.Summary(location);
        }
        else if (MusterShare.IsToken(message.Body))
        {
            text = Loc.T(L.Muster.InvitePreview);
        }
        else if (AdShare.IsToken(message.Body))
        {
            text = Loc.T(L.YellowPages.AdPreview);
        }
        else
        {
            text = UiText.Truncate(message.Body ?? string.Empty, InfoBubbleMaxChars);
        }

        var textSize = Typography.MeasureWrappedBlock(text, TextStyles.Body, wrap);
        var time = TimeText.Clock(message.CreatedAtUnix);
        var timeSize = Typography.Measure(time, InfoStampStyle);
        var contentWidth = MathF.Max(textSize.X, timeSize.X);
        var bubbleWidth = contentWidth + paddingX * 2f;
        var bubbleHeight = textSize.Y + 2f * scale + timeSize.Y + paddingY * 2f;
        var bubbleMin = new Vector2(origin.X + width - bubbleWidth, origin.Y);
        var bubbleMax = bubbleMin + new Vector2(bubbleWidth, bubbleHeight);
        Squircle.Fill(drawList, bubbleMin, bubbleMax, MessageThreadViewBase.BubbleRounding * scale,
            ImGui.GetColorU32(activeTheme.OutgoingBubble));
        Typography.DrawWrappedLeft(new Vector2(bubbleMin.X + paddingX, bubbleMin.Y + paddingY), text,
            MessageThemes.OutgoingInk, TextStyles.Body, wrap);
        Typography.Draw(drawList, new Vector2(bubbleMax.X - paddingX - timeSize.X, bubbleMax.Y - paddingY - timeSize.Y),
            time, Core.Theme.Palette.WithAlpha(MessageThemes.OutgoingInk, 0.72f), InfoStampStyle);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, bubbleHeight + 18f * scale));
    }

    private void DrawDirectReceipts(ImDrawListPtr drawList, ChatMessageDto message)
    {
        var readAt = message.ReadAtUnix;
        if (readAt is null)
        {
            var members = store.Members;
            for (var index = 0; index < members.Length; index++)
            {
                var member = members[index];
                if (member.UserId == store.MyUserId || !member.IsActive)
                {
                    continue;
                }

                if (member.LastReadAtUnix is { } lastRead && lastRead >= message.CreatedAtUnix)
                {
                    readAt = lastRead;
                }

                break;
            }
        }

        var card = GroupCard.Begin(ui, 2, ReceiptRowHeight);
        DrawReceiptStatusRow(drawList, card.NextRow(), PhoneIcons.Checks,
            readAt is not null ? ReadTickColor : ink.MutedInk, Loc.T(L.Message.ReadSection),
            readAt is { } readUnix ? TimeText.Stamp(readUnix) : Loc.T(L.Message.NotReadYet));
        DrawReceiptStatusRow(drawList, card.NextRow(), PhoneIcons.Check, ink.MutedInk, Loc.T(L.Message.SentSection),
            TimeText.Stamp(message.CreatedAtUnix));
        card.End();
        DrawCardGap();
    }

    private void DrawGroupReceipts(ImDrawListPtr drawList, ChatMessageDto message)
    {
        var members = store.Members;
        DrawInsetSectionLabel(Loc.T(L.Message.ReadBy));
        var readCount = 0;
        var pendingCount = 0;
        for (var index = 0; index < members.Length; index++)
        {
            var member = members[index];
            if (!IsReceiptMember(member))
            {
                continue;
            }

            if (member.LastReadAtUnix is { } readAt && readAt >= message.CreatedAtUnix)
            {
                readCount++;
            }
            else
            {
                pendingCount++;
            }
        }

        if (readCount == 0)
        {
            DrawReceiptEmptyRow(drawList, Loc.T(L.Message.NotReadYet));
        }
        else
        {
            var readCard = GroupCard.Begin(ui, readCount, ReceiptRowHeight);
            for (var index = 0; index < members.Length; index++)
            {
                var member = members[index];
                if (!IsReceiptMember(member) || member.LastReadAtUnix is not { } readAt
                    || readAt < message.CreatedAtUnix)
                {
                    continue;
                }

                DrawReceiptMemberRow(drawList, readCard.NextRow(), member, TimeText.Stamp(readAt), PhoneIcons.Checks,
                    ReadTickColor);
            }

            readCard.End();
            DrawCardGap();
        }

        if (pendingCount == 0)
        {
            return;
        }

        DrawInsetSectionLabel(Loc.T(L.Message.SentTo));
        var pendingCard = GroupCard.Begin(ui, pendingCount, ReceiptRowHeight);
        for (var index = 0; index < members.Length; index++)
        {
            var member = members[index];
            if (!IsReceiptMember(member))
            {
                continue;
            }

            if (member.LastReadAtUnix is null || member.LastReadAtUnix.Value < message.CreatedAtUnix)
            {
                DrawReceiptMemberRow(drawList, pendingCard.NextRow(), member, string.Empty, PhoneIcons.Check,
                    ink.MutedInk);
            }
        }

        pendingCard.End();
        DrawCardGap();
    }

    private bool IsReceiptMember(ConversationMemberDto member)
    {
        return member.IsActive && member.UserId != store.MyUserId;
    }

    private void DrawReceiptStatusRow(ImDrawListPtr drawList, Rect row, string glyph, Vector4 glyphInk, string label,
        string value)
    {
        var scale = UiScale.Current;
        PhoneIcon.Draw(drawList, new Vector2(row.Min.X + ReceiptGlyph * 0.5f * scale, row.Center.Y), glyph, glyphInk,
            ReceiptGlyph * scale);
        var valueSize = Typography.Measure(value, RowSubStyle);
        var labelLeft = row.Min.X + ReceiptGlyph * scale + RowTextGap * scale;
        var labelMaxWidth = MathF.Max(1f, row.Max.X - valueSize.X - RowTrailingGap * scale - labelLeft);
        var labelHeight = Typography.LineHeight(RowTitleStyle);
        Typography.Draw(drawList, new Vector2(labelLeft, row.Center.Y - labelHeight * 0.5f),
            Typography.FitText(label, labelMaxWidth, RowTitleStyle), ink.TitleInk, RowTitleStyle);
        Typography.Draw(drawList, new Vector2(row.Max.X - valueSize.X, row.Center.Y - valueSize.Y * 0.5f), value,
            ink.MutedInk, RowSubStyle);
    }

    private void DrawReceiptMemberRow(ImDrawListPtr drawList, Rect row, ConversationMemberDto member, string stamp,
        string glyph, Vector4 glyphInk)
    {
        var scale = UiScale.Current;
        var radius = ReceiptAvatarRadius * scale;
        var avatarCenter = new Vector2(row.Min.X + radius, row.Center.Y);
        DrawMemberAvatar(drawList, member, avatarCenter, radius);
        var textLeft = avatarCenter.X + radius + RowTextGap * scale;
        var right = row.Max.X;
        PhoneIcon.Draw(drawList, new Vector2(right - ReceiptGlyph * 0.5f * scale, row.Center.Y), glyph, glyphInk,
            ReceiptGlyph * scale);
        right -= ReceiptGlyph * scale + RowTrailingGap * scale;
        if (stamp.Length > 0)
        {
            var stampSize = Typography.Measure(stamp, RowMetaStyle);
            Typography.Draw(drawList, new Vector2(right - stampSize.X, row.Center.Y - stampSize.Y * 0.5f), stamp,
                ink.MutedInk, RowMetaStyle);
            right -= stampSize.X + RowTrailingGap * scale;
        }

        var band = RowBand(row, scale);
        var rowHovering = UiInteract.Hover(band.Min, band.Max);
        var labelHeight = Typography.LineHeight(RowTitleStyle);
        Marquee.DrawLeft(drawList, new MarqueeId("messageapp.messageinfo.member.", member.UserId), DirectMessagesStore.MemberLabel(member),
            textLeft, row.Center.Y - labelHeight * 0.5f, MathF.Max(1f, right - textLeft), RowTitleStyle, ink.TitleInk,
            rowHovering);
    }

    private void DrawReceiptEmptyRow(ImDrawListPtr drawList, string label)
    {
        var card = GroupCard.Begin(ui, 1, 44f);
        var row = card.NextRow();
        Typography.DrawCentered(drawList, row.Center, label, ink.MutedInk, RowSubStyle);
        card.End();
        DrawCardGap();
    }
}
