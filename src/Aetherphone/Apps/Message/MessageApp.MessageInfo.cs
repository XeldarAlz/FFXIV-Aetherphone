using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Maps;
using Aetherphone.Core.Muster;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Message;

internal sealed partial class MessageApp
{
    private static readonly Vector4 ReadTickColor = new(0.45f, 0.83f, 1f, 1f);

    private float sinceInfoPoll;

    private void DrawMessageInfo(Rect area, string messageId)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Message.InfoTitle), back);
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
            ImGui.Dummy(new Vector2(0f, 10f * scale));
            DrawInfoBubble(message, scale);
            if (conversation.IsGroup)
            {
                DrawGroupReceipts(message, scale);
            }
            else
            {
                DrawDirectReceipts(message, scale);
            }

            ImGui.Dummy(new Vector2(0f, 30f * scale));
        }
    }

    private void TickMessageInfo()
    {
        sinceInfoPoll += ImGui.GetIO().DeltaTime;
        if (sinceInfoPoll < ThreadPollSeconds)
        {
            return;
        }

        sinceInfoPoll = 0f;
        store.RefreshDetail();
        store.RefreshThread();
    }

    private void DrawInfoBubble(ChatMessageDto message, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
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
        else
        {
            text = UiText.Truncate(message.Body ?? string.Empty, 220);
        }
        var textSize = ImGui.CalcTextSize(text, false, wrap);
        var time = TimeText.Clock(message.CreatedAtUnix);
        var timeSize = Typography.Measure(time, 0.70f);
        var contentWidth = MathF.Max(textSize.X, timeSize.X);
        var bubbleWidth = contentWidth + paddingX * 2f;
        var bubbleHeight = textSize.Y + 2f * scale + timeSize.Y + paddingY * 2f;
        var bubbleMin = new Vector2(origin.X + width - bubbleWidth - 16f * scale, origin.Y);
        var bubbleMax = bubbleMin + new Vector2(bubbleWidth, bubbleHeight);
        Squircle.Fill(drawList, bubbleMin, bubbleMax, 14f * scale, ImGui.GetColorU32(ui.Accent));
        drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize(),
            new Vector2(bubbleMin.X + paddingX, bubbleMin.Y + paddingY), ImGui.GetColorU32(White), text, wrap);
        Typography.Draw(drawList, new Vector2(bubbleMax.X - paddingX - timeSize.X,
            bubbleMax.Y - paddingY - timeSize.Y), time, new Vector4(1f, 1f, 1f, 0.72f), 0.70f);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, bubbleHeight + 18f * scale));
    }

    private void DrawDirectReceipts(ChatMessageDto message, float scale)
    {
        long? readAt = message.ReadAtUnix;
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

        DrawReceiptStatusRow(FontAwesomeIcon.CheckDouble, readAt is not null ? ReadTickColor : ui.MutedInk,
            Loc.T(L.Message.ReadSection), readAt is { } readUnix ? FormatStamp(readUnix)
                : Loc.T(L.Message.NotReadYet), scale);
        DrawReceiptStatusRow(FontAwesomeIcon.Check, ui.MutedInk, Loc.T(L.Message.SentSection),
            FormatStamp(message.CreatedAtUnix), scale);
    }

    private void DrawGroupReceipts(ChatMessageDto message, float scale)
    {
        var members = store.Members;
        DrawSectionLabel(Loc.T(L.Message.ReadBy), scale);
        var readCount = 0;
        for (var index = 0; index < members.Length; index++)
        {
            var member = members[index];
            if (!IsReceiptMember(member))
            {
                continue;
            }

            if (member.LastReadAtUnix is { } readAt && readAt >= message.CreatedAtUnix)
            {
                DrawReceiptMemberRow(member, FormatStamp(readAt), FontAwesomeIcon.CheckDouble, ReadTickColor, scale);
                readCount++;
            }
        }

        if (readCount == 0)
        {
            DrawReceiptEmptyRow(Loc.T(L.Message.NotReadYet), scale);
        }

        var pendingCount = 0;
        for (var index = 0; index < members.Length; index++)
        {
            var member = members[index];
            if (!IsReceiptMember(member))
            {
                continue;
            }

            if (member.LastReadAtUnix is null || member.LastReadAtUnix.Value < message.CreatedAtUnix)
            {
                if (pendingCount == 0)
                {
                    DrawSectionLabel(Loc.T(L.Message.SentTo), scale);
                }

                DrawReceiptMemberRow(member, string.Empty, FontAwesomeIcon.Check, ui.MutedInk, scale);
                pendingCount++;
            }
        }
    }

    private bool IsReceiptMember(ConversationMemberDto member)
    {
        return member.IsActive && member.UserId != store.MyUserId;
    }

    private void DrawReceiptStatusRow(FontAwesomeIcon icon, Vector4 iconColor, string label, string value, float scale)
    {
        var rowHeight = 52f * scale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, origin, new Vector2(origin.X + width, origin.Y + rowHeight), 14f * scale);
        var pad = 14f * scale;
        AppSkin.Icon(new Vector2(origin.X + pad + 8f * scale, origin.Y + rowHeight * 0.5f), icon.ToIconString(),
            iconColor, 0.95f);
        Typography.Draw(new Vector2(origin.X + pad + 28f * scale, origin.Y + rowHeight * 0.5f - 9f * scale), label,
            theme.TextStrong, 1f, FontWeight.SemiBold);
        var valueSize = Typography.Measure(value, 0.85f);
        Typography.Draw(new Vector2(origin.X + width - pad - valueSize.X, origin.Y + rowHeight * 0.5f
            - valueSize.Y * 0.5f), value, ui.MutedInk, 0.85f);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight + 8f * scale));
    }

    private void DrawReceiptMemberRow(ConversationMemberDto member, string stamp, FontAwesomeIcon icon,
        Vector4 iconColor, float scale)
    {
        var rowHeight = 52f * scale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, origin, new Vector2(origin.X + width, origin.Y + rowHeight), 14f * scale);
        var pad = 12f * scale;
        var radius = 17f * scale;
        var avatarCenter = new Vector2(origin.X + pad + radius, origin.Y + rowHeight * 0.5f);
        var label = member.DisplayName.Length > 0 ? member.DisplayName : member.Handle;
        AvatarView.DrawRemote(drawList, avatarCenter, radius, theme, label, string.Empty, member.AvatarUrl, images,
            lodestone, 0.85f, 32);
        var textLeft = avatarCenter.X + radius + 12f * scale;
        Typography.Draw(new Vector2(textLeft, origin.Y + rowHeight * 0.5f - 9f * scale), label, theme.TextStrong, 1f,
            FontWeight.SemiBold);
        var right = origin.X + width - pad;
        if (stamp.Length > 0)
        {
            var stampSize = Typography.Measure(stamp, 0.80f);
            Typography.Draw(new Vector2(right - stampSize.X, origin.Y + rowHeight * 0.5f - stampSize.Y * 0.5f),
                stamp, ui.MutedInk, 0.80f);
            right -= stampSize.X + 10f * scale;
        }

        AppSkin.Icon(new Vector2(right - 6f * scale, origin.Y + rowHeight * 0.5f), icon.ToIconString(), iconColor,
            0.80f);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight + 8f * scale));
    }

    private void DrawReceiptEmptyRow(string label, float scale)
    {
        var rowHeight = 44f * scale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, origin, new Vector2(origin.X + width, origin.Y + rowHeight), 14f * scale);
        Typography.DrawCentered(new Vector2(origin.X + width * 0.5f, origin.Y + rowHeight * 0.5f), label,
            ui.MutedInk, 0.9f);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight + 8f * scale));
    }

    private static string FormatStamp(long unixSeconds)
    {
        var clock = TimeText.Clock(unixSeconds);
        if (TimeText.SameLocalDay(unixSeconds, DateTimeOffset.UtcNow.ToUnixTimeSeconds()))
        {
            return clock;
        }

        return string.Concat(TimeText.DayLabel(unixSeconds), ", ", clock);
    }
}
