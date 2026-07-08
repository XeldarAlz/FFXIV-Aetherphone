using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.DirectMessages;

internal sealed partial class DirectMessagesApp
{
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    private void DrawThread(Rect area, string conversationId)
    {
        if (store.ConversationId != conversationId)
        {
            store.OpenConversation(conversationId);
            sinceThreadPoll = ThreadPollSeconds;
            lastTypingDraft = string.Empty;
        }

        store.NoteConversationViewed(conversationId);
        TickThread(conversationId);
        var conversation = store.Conversation;
        var isGroup = conversation?.IsGroup ?? false;
        DrawThreadHeader(area, conversation, isGroup);
        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var composerHeight = 52f * scale;
        var listRect = new Rect(new Vector2(area.Min.X, top), new Vector2(area.Max.X, area.Max.Y - composerHeight));
        var transcriptMessages = BuildTranscript(store.Messages, isGroup);
        threadMediaUrl ??= store.DmMediaUrl;
        onThreadImageClick ??= id => router.Push(DmRoute.ImageView(id));
        var model = new ChatTranscriptModel(conversationId, transcriptMessages, store.MyUserId, ui.Accent, theme,
            AppPalettes.Messenger.MutedInk, AppPalettes.Messenger.BodyInk, store.OtherTyping, store.LoadingThread,
            isGroup, images, threadMediaUrl, onThreadImageClick, Loc.T(L.Velvet.ThreadEmpty), Loc.T(L.Common.Loading));
        transcript.Draw(listRect, model);
        DrawMessageComposer(new Rect(new Vector2(area.Min.X, area.Max.Y - composerHeight), area.Max), conversationId);
    }

    private void DrawThreadHeader(Rect area, ConversationDto? conversation, bool isGroup)
    {
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, string.Empty, back);
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        var name = conversation is null ? DisplayName : DirectMessagesStore.DisplayTitle(conversation);
        var avatarRadius = 18f * scale;
        var nameSize = Typography.Measure(name, 1f, FontWeight.SemiBold);
        var gap = 9f * scale;
        var groupWidth = avatarRadius * 2f + gap + nameSize.X;
        var startX = MathF.Max(area.Center.X - groupWidth * 0.5f, area.Min.X + 48f * scale);
        var avatarCenter = new Vector2(startX + avatarRadius, rowCenterY);
        if (isGroup)
        {
            drawList.AddCircleFilled(avatarCenter, avatarRadius, ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.85f)),
                32);
            AppSkin.Icon(avatarCenter, FontAwesomeIcon.Users.ToIconString(), White, 0.8f);
        }
        else
        {
            AvatarView.DrawRemote(drawList, avatarCenter, avatarRadius, theme, name, string.Empty,
                conversation?.OtherAvatarUrl, images, lodestone, 0.9f, 32);
        }

        var nameLeft = avatarCenter.X + avatarRadius + gap;
        if (isGroup && conversation is not null)
        {
            var sub = Loc.T(L.DirectMessages.MembersCount, conversation.MemberCount);
            var subSize = Typography.Measure(sub, 0.72f, FontWeight.Regular);
            var gapY = 1f * scale;
            var stackTop = rowCenterY - (nameSize.Y + gapY + subSize.Y) * 0.5f;
            Typography.Draw(new Vector2(nameLeft, stackTop), name, theme.TextStrong, 1f, FontWeight.SemiBold);
            Typography.Draw(new Vector2(nameLeft, stackTop + nameSize.Y + gapY), sub, AppPalettes.Messenger.MutedInk,
                0.72f);
            var hitMin = new Vector2(avatarCenter.X - avatarRadius, area.Min.Y);
            var hitMax = new Vector2(nameLeft + MathF.Max(nameSize.X, subSize.X), area.Min.Y + AppHeader.Height * scale);
            if (UiInteract.HoverClick(hitMin, hitMax))
            {
                router.Push(DmRoute.GroupInfo(conversation.Id));
            }
        }
        else
        {
            Typography.Draw(new Vector2(nameLeft, rowCenterY - nameSize.Y * 0.5f), name, theme.TextStrong, 1f,
                FontWeight.SemiBold);
        }
    }

    private ReadOnlySpan<TranscriptMessage> BuildTranscript(ChatMessageDto[] source, bool isGroup)
    {
        if (ReferenceEquals(source, transcriptSource))
        {
            return transcriptCache;
        }

        transcriptSource = source;
        var mapped = new TranscriptMessage[source.Length];
        for (var index = 0; index < source.Length; index++)
        {
            var message = source[index];
            if (message.Kind == 2)
            {
                mapped[index] = new TranscriptMessage(message.Id, message.SenderId, SystemText(message), 2,
                    message.CreatedAtUnix, 0, 0, null, string.Empty, default);
                continue;
            }

            var senderName = isGroup ? message.SenderDisplayName : string.Empty;
            var tint = isGroup ? SenderTint.Of(message.SenderDisplayName) : default;
            mapped[index] = new TranscriptMessage(message.Id, message.SenderId, message.Body, message.Kind,
                message.CreatedAtUnix, message.MediaWidth, message.MediaHeight, message.ReadAtUnix, senderName, tint);
        }

        transcriptCache = mapped;
        return transcriptCache;
    }

    private static string SystemText(ChatMessageDto message)
    {
        var actor = message.SenderDisplayName;
        var body = message.Body ?? string.Empty;
        var separator = (char)0x1F;
        var separatorIndex = body.IndexOf(separator);
        var token = separatorIndex >= 0 ? body.Substring(0, separatorIndex) : body;
        var argument = separatorIndex >= 0 ? body.Substring(separatorIndex + 1) : string.Empty;
        return token switch
        {
            "created" => Loc.T(L.DirectMessages.SysCreated, actor),
            "added" => Loc.T(L.DirectMessages.SysAdded, actor, argument),
            "removed" => Loc.T(L.DirectMessages.SysRemoved, actor, argument),
            "left" => Loc.T(L.DirectMessages.SysLeft, actor),
            "renamed" => Loc.T(L.DirectMessages.SysRenamed, actor, argument),
            _ => body,
        };
    }

    private void TickThread(string conversationId)
    {
        var delta = ImGui.GetIO().DeltaTime;
        sinceThreadPoll += delta;
        if (sinceThreadPoll >= ThreadPollSeconds)
        {
            sinceThreadPoll = 0f;
            store.RefreshThread();
            store.RefreshTyping(conversationId);
        }

        sinceTypingSend += delta;
        if (messageDraft != lastTypingDraft)
        {
            lastTypingDraft = messageDraft;
            if (messageDraft.Trim().Length > 0 && sinceTypingSend >= TypingSendSeconds)
            {
                sinceTypingSend = 0f;
                store.SendTyping(conversationId);
            }
        }
    }

    private void DrawMessageComposer(Rect area, string conversationId)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddLine(area.Min, new Vector2(area.Max.X, area.Min.Y), ImGui.GetColorU32(theme.Separator), 1f);
        var buttonRadius = 16f * scale;
        var pictureCenter = new Vector2(area.Min.X + 12f * scale + buttonRadius, area.Center.Y);
        var pictureMin = pictureCenter - new Vector2(buttonRadius, buttonRadius);
        var pictureMax = pictureCenter + new Vector2(buttonRadius, buttonRadius);
        var pictureHovered = ImGui.IsMouseHoveringRect(pictureMin, pictureMax);
        drawList.AddCircleFilled(pictureCenter, buttonRadius,
            ImGui.GetColorU32(pictureHovered ? Palette.Mix(ui.Accent, theme.TextStrong, 0.12f) : ui.Accent), 24);
        AppSkin.Icon(pictureCenter, FontAwesomeIcon.Image.ToIconString(), White, 0.85f);
        HoverTooltip.Show(new Rect(pictureMin, pictureMax), Loc.T(L.Velvet.SendPicture), HoverLabelSide.Above);
        if (pictureHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                router.Push(DmRoute.ChatImage(conversationId));
            }
        }

        var sendWidth = 40f * scale;
        var pillMin = new Vector2(pictureMax.X + 10f * scale, area.Min.Y + 8f * scale);
        var pillMax = new Vector2(area.Max.X - sendWidth - 12f * scale, area.Max.Y - 8f * scale);
        Squircle.Fill(drawList, pillMin, pillMax, (pillMax.Y - pillMin.Y) * 0.5f,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)));
        ImGui.SetCursorScreenPos(new Vector2(pillMin.X + 14f * scale,
            (pillMin.Y + pillMax.Y) * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(pillMax.X - pillMin.X - 24f * scale);
        if (threadFocus)
        {
            ImGui.SetKeyboardFocusHere();
            threadFocus = false;
        }

        var submitted = false;
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            if (ImGui.InputTextWithHint("##dmMessage", Loc.T(L.Velvet.MessageHint), ref messageDraft, MessageMax,
                    ImGuiInputTextFlags.EnterReturnsTrue))
            {
                submitted = true;
            }
        }

        var canSend = messageDraft.Trim().Length > 0 && !store.Sending;
        var sendCenter = new Vector2(area.Max.X - sendWidth * 0.5f - 8f * scale, area.Center.Y);
        drawList.AddCircleFilled(sendCenter, 16f * scale, ImGui.GetColorU32(canSend ? ui.Accent : theme.SurfaceMuted),
            24);
        AppSkin.Icon(sendCenter, FontAwesomeIcon.PaperPlane.ToIconString(), White, 0.9f);
        var sendHitRadius = 16f * scale;
        var sendRect = new Rect(sendCenter - new Vector2(sendHitRadius, sendHitRadius),
            sendCenter + new Vector2(sendHitRadius, sendHitRadius));
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
            store.SendMessage(conversationId, messageDraft, _ => { });
            messageDraft = string.Empty;
            lastTypingDraft = string.Empty;
            transcript.RequestSnapToBottom();
            threadFocus = true;
        }
    }
}
