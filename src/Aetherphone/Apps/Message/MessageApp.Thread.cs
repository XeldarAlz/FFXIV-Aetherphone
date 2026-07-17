using System.Collections.Concurrent;
using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Message;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Report;
using Aetherphone.Core.Telephony.Audio;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Message;

internal sealed partial class MessageApp
{
    private readonly ChatMenuController messageMenuController = new();
    private readonly ChatComposer composer = new();
    private Action<string>? onMessageContext;
    private Action<string>? onQuoteClick;
    private Action<string, string>? onReactionClick;
    private Func<string, VoiceNoteState>? voiceStateFor;
    private Action<string>? onVoiceToggle;
    private Action<string>? composerPickImage;
    private Action<string, string, string?>? composerSendText;
    private Action<string, string, string>? composerEditText;
    private Action<string, byte[], int>? composerSendVoice;
    private Func<int>? composerResolveVoice;
    private float sinceThreadPoll;
    private float sinceTypingSend = TypingSendSeconds;
    private string lastTypingDraft = string.Empty;
    private ChatMessageDto[] transcriptSource = Array.Empty<ChatMessageDto>();
    private TranscriptMessage[] transcriptCache = Array.Empty<TranscriptMessage>();
    private Func<string, string?>? threadMediaUrl;
    private Func<string, IDalamudTextureWrap?>? resolveThreadImage;
    private Action<string>? onThreadImageClick;
    private Action? onThreadLoadOlder;
    private readonly VoiceNotePlayer voicePlayer = new();
    private readonly ConcurrentDictionary<string, byte[]> voiceBytes = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> voiceFetching = new(StringComparer.Ordinal);
    private volatile string? pendingVoicePlay;
    private readonly ChatSearchController searchController = new();

    private void DrawThread(Rect area, string conversationId)
    {
        if (store.ConversationId != conversationId)
        {
            if (!composer.IsEditing && store.ConversationId is { } previousId)
            {
                SaveDraft(previousId);
            }

            store.OpenConversation(conversationId);
            sinceThreadPoll = ThreadPollSeconds;
            lastTypingDraft = string.Empty;
            composer.ClearTargets();
            searchController.Close();
            composer.CancelVoice();
            voicePlayer.Stop();
            composer.Draft = configuration.MessageDrafts.GetValueOrDefault(conversationId, string.Empty);
        }

        store.NoteConversationViewed(conversationId);
        TickThread(conversationId);
        var conversation = store.Conversation;
        var isGroup = conversation?.IsGroup ?? false;
        DrawThreadHeader(area, conversation, isGroup);
        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var composerHeight = 52f * scale;
        var accessoryHeight = composer.AccessoryHeight;
        var transcriptMessages = BuildTranscript(store.Messages, isGroup);
        if (searchController.Open)
        {
            var searchHeight = 44f * scale;
            searchController.Draw(new Rect(new Vector2(area.Min.X, top), new Vector2(area.Max.X, top + searchHeight)),
                new ChatSearchModel(ui, transcriptMessages, transcript.RequestScrollTo));
            top += searchHeight;
        }

        var listRect = new Rect(new Vector2(area.Min.X, top),
            new Vector2(area.Max.X, area.Max.Y - composerHeight - accessoryHeight));
        DrawEncryptionBanner(ref listRect, conversation, isGroup);
        threadMediaUrl ??= store.DmMediaUrl;
        resolveThreadImage ??= ResolveThreadImage;
        onThreadImageClick ??= id => router.Push(MessageRoute.ImageView(id));
        onMessageContext ??= OpenMessageMenu;
        onQuoteClick ??= transcript.RequestScrollTo;
        onReactionClick ??= (messageId, _) => router.Push(MessageRoute.Reactions(messageId));
        voiceStateFor ??= voicePlayer.StateFor;
        onVoiceToggle ??= ToggleVoice;
        onThreadLoadOlder ??= store.LoadOlder;
        var model = new ChatTranscriptModel(conversationId, transcriptMessages, store.MyUserId, ui.Accent, theme,
            AppPalettes.Message.MutedInk, AppPalettes.Message.BodyInk, store.OtherTyping, store.LoadingThread,
            isGroup, images, threadMediaUrl, onThreadImageClick, Loc.T(L.Message.ThreadEmpty), Loc.T(L.Common.Loading),
            onMessageContext, onQuoteClick, onReactionClick, voiceStateFor, onVoiceToggle,
            store.HasMoreOlder, store.LoadingOlder, onThreadLoadOlder, resolveThreadImage);
        transcript.Draw(listRect, model);
        composerPickImage ??= id => router.Push(MessageRoute.ChatImage(id));
        composerSendText ??= ComposerSendText;
        composerEditText ??= ComposerEditText;
        composerSendVoice ??= ComposerSendVoice;
        composerResolveVoice ??= ResolveVoiceInput;
        composer.Draw(new Rect(new Vector2(area.Min.X, area.Max.Y - composerHeight), area.Max), new ChatComposerModel
        {
            Ui = ui,
            ConversationId = conversationId,
            MaxLength = MessageMax,
            Sending = store.Sending,
            CanImage = true,
            CanVoice = true,
            CanHandleEscape = !searchController.Open,
            ResolveVoiceInput = composerResolveVoice,
            OnPickImage = composerPickImage,
            OnSendText = composerSendText,
            OnEditText = composerEditText,
            OnSendVoice = composerSendVoice,
        });
        DrawMessageMenu(area);
    }

    private void OpenMessageMenu(string messageId)
    {
        var message = FindMessage(messageId);
        if (message is null || message.Deleted)
        {
            return;
        }

        messageMenuController.Open(messageId, message.SenderId == store.MyUserId, message.Kind);
    }

    private void DrawMessageMenu(Rect area)
    {
        if (!messageMenuController.Active)
        {
            return;
        }

        var model = new ChatMenuModel
        {
            Ui = ui,
            ShowReactions = true,
            CanReply = true,
            CanForward = true,
            CanCopy = true,
            CanStar = true,
            CanEdit = true,
            CanInfo = true,
            CanDelete = true,
            CanReport = true,
            IsStarred = IsStarred,
            MyReactionTo = store.MyReactionTo,
            OnReply = BeginReply,
            OnForward = id => router.Push(MessageRoute.Forward(id)),
            OnCopy = id => ChatActions.CopyMessageText(transcriptCache, id, CanRevealBody),
            OnStar = ToggleStar,
            OnEdit = BeginEdit,
            OnInfo = id =>
            {
                store.RefreshDetail();
                router.Push(MessageRoute.MessageInfo(id));
            },
            OnDelete = AskDeleteMessage,
            OnReport = OpenReportMessage,
            OnReact = store.SetReaction,
        };
        messageMenuController.Draw(area, model);
    }

    private bool IsStarred(string messageId)
    {
        var starred = configuration.MessageStarredMessages;
        for (var index = 0; index < starred.Count; index++)
        {
            if (starred[index].MessageId == messageId)
            {
                return true;
            }
        }

        return false;
    }

    private void ToggleStar(string messageId)
    {
        var starred = configuration.MessageStarredMessages;
        for (var index = 0; index < starred.Count; index++)
        {
            if (starred[index].MessageId == messageId)
            {
                starred.RemoveAt(index);
                configuration.Save();
                return;
            }
        }

        var message = FindMessage(messageId);
        var conversation = store.Conversation;
        if (message is null || message.Deleted || conversation is null)
        {
            return;
        }

        starred.Add(new StarredMessage
        {
            ConversationId = conversation.Id,
            MessageId = messageId,
            ConversationTitle = DirectMessagesStore.DisplayTitle(conversation),
            SenderName = message.SenderId == store.MyUserId ? Loc.T(L.Message.You) : message.SenderDisplayName,
            Preview = ChatText.QuotePreview(message.Body, message.Kind),
            Kind = message.Kind,
            CreatedAtUnix = message.CreatedAtUnix,
            StarredAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        });
        configuration.Save();
    }

    private void BeginEdit(string messageId)
    {
        var message = FindMessage(messageId);
        if (message is null || message.Kind != 0 || message.Deleted)
        {
            return;
        }

        if (message.EncVersion != 0 && store.DecryptionState(messageId).State != DmBodyState.Decrypted)
        {
            return;
        }

        composer.BeginEdit(messageId, message.Body ?? string.Empty);
    }

    private void AskDeleteMessage(string messageId)
    {
        Plugin.Confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Message.DeleteConfirm),
            ConfirmLabel = Loc.T(L.Message.DeleteAction),
            CancelLabel = Loc.T(L.Common.Cancel),
            Danger = true,
            ConfirmAsync = done => store.DeleteMessage(messageId, done),
        });
    }

    private IDalamudTextureWrap? ResolveThreadImage(string messageId)
    {
        var message = store.FindMessage(messageId);
        if (message is null)
        {
            return null;
        }

        if (message.EncVersion != EnvelopeCodec.VersionEnvelope)
        {
            return images.Get(store.DmMediaUrl(messageId));
        }

        var url = store.DmMediaUrl(messageId);
        if (url is null)
        {
            return null;
        }

        return images.GetKeyed(messageId, async token =>
        {
            var data = await http.GetBytesAsync(new Uri(url), token).ConfigureAwait(false);
            return data is null ? null : store.DecryptMedia(message, data);
        });
    }

    private void ToggleVoice(string messageId)
    {
        if (voiceBytes.TryGetValue(messageId, out var bytes))
        {
            pendingVoicePlay = null;
            voicePlayer.Toggle(messageId, bytes);
            return;
        }

        pendingVoicePlay = messageId;
        FetchVoice(messageId);
    }

    private void FetchVoice(string messageId)
    {
        if (voiceBytes.ContainsKey(messageId))
        {
            return;
        }

        var url = store.DmMediaUrl(messageId);
        if (url is null || !voiceFetching.TryAdd(messageId, 0))
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var data = await http.GetBytesAsync(new Uri(url), CancellationToken.None).ConfigureAwait(false);
                if (data is not null)
                {
                    var message = store.FindMessage(messageId);
                    var plain = message is { EncVersion: EnvelopeCodec.VersionEnvelope }
                        ? store.DecryptMedia(message, data)
                        : data;
                    if (plain is not null)
                    {
                        voiceBytes[messageId] = plain;
                    }
                }
            }
            catch (Exception exception)
            {
                AepLog.Warning($"Voice note download failed: {exception.Message}");
            }
            finally
            {
                voiceFetching.TryRemove(messageId, out _);
            }
        });
    }

    private void PumpPendingVoice()
    {
        if (pendingVoicePlay is not { } id)
        {
            return;
        }

        if (voiceBytes.TryGetValue(id, out var bytes))
        {
            pendingVoicePlay = null;
            voicePlayer.Toggle(id, bytes);
            return;
        }

        FetchVoice(id);
    }

    private ChatMessageDto? FindMessage(string messageId)
    {
        var snapshot = store.Messages;
        for (var index = 0; index < snapshot.Length; index++)
        {
            if (snapshot[index].Id == messageId)
            {
                return snapshot[index];
            }
        }

        return null;
    }

    private void BeginReply(string messageId)
    {
        var message = FindMessage(messageId);
        if (message is null || message.Kind == 2)
        {
            return;
        }

        var senderName = message.SenderId == store.MyUserId
            ? Loc.T(L.Message.You)
            : message.SenderDisplayName;
        composer.BeginReply(messageId, senderName, ChatText.QuotePreview(message.Body, message.Kind));
    }

    private void SaveDraft(string conversationId)
    {
        var trimmed = composer.Draft.Trim();
        var drafts = configuration.MessageDrafts;
        if (trimmed.Length == 0)
        {
            if (drafts.Remove(conversationId))
            {
                configuration.Save();
            }

            return;
        }

        if (drafts.GetValueOrDefault(conversationId) == trimmed)
        {
            return;
        }

        drafts[conversationId] = trimmed;
        configuration.Save();
    }

    private void ClearDraft(string conversationId)
    {
        if (configuration.MessageDrafts.Remove(conversationId))
        {
            configuration.Save();
        }
    }

    private void OpenReportMessage(string messageId)
    {
        Plugin.Report.Open(new ReportPrompt
        {
            Title = Loc.T(L.Encryption.ReportMessageAction),
            Disclosure = Loc.T(L.Encryption.ReportDisclosure),
            Submit = (reason, done) => store.ReportMessage(messageId, reason, done),
        });
    }

    private bool CanRevealBody(string id)
    {
        var message = FindMessage(id);
        if (message is null)
        {
            return false;
        }

        return message.EncVersion != 1 || store.DecryptionState(id).State == DmBodyState.Decrypted;
    }

    private void DrawEncryptionBanner(ref Rect listRect, ConversationDto? conversation, bool isGroup)
    {
        if (isGroup || conversation is null || !store.HasRotationNotice(conversation.OtherUserId))
        {
            return;
        }

        var dismissUserId = conversation.OtherUserId;
        var text = Loc.T(L.Encryption.SafetyChanged, DirectMessagesStore.DisplayTitle(conversation));
        ChatHeaderControls.DrawBanner(ui, ref listRect, text, AppPalettes.Message.MutedInk,
            () => store.ClearRotationNotice(dismissUserId));
    }

    private void DrawThreadHeader(Rect area, ConversationDto? conversation, bool isGroup)
    {
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, string.Empty, back);
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        ChatHeaderControls.DrawLock(ui, area, rowCenterY, store.EncryptingCurrent, store.VaultState,
            () =>
            {
                if (conversation is not null)
                {
                    router.Push(MessageRoute.Encryption(conversation.Id));
                }
            });
        ChatHeaderControls.DrawSearchToggle(ui, area, rowCenterY, searchController.Open, searchController.Toggle);
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
            Typography.Draw(new Vector2(nameLeft, stackTop + nameSize.Y + gapY), sub, AppPalettes.Message.MutedInk,
                0.72f);
            var hitMin = new Vector2(avatarCenter.X - avatarRadius, area.Min.Y);
            var hitMax = new Vector2(nameLeft + MathF.Max(nameSize.X, subSize.X), area.Min.Y + AppHeader.Height * scale);
            if (UiInteract.HoverClick(hitMin, hitMax))
            {
                router.Push(MessageRoute.GroupInfo(conversation.Id));
            }
        }
        else
        {
            var presence = PresenceText(conversation);
            if (presence.Length > 0)
            {
                var subSize = Typography.Measure(presence, 0.72f, FontWeight.Regular);
                var gapY = 1f * scale;
                var stackTop = rowCenterY - (nameSize.Y + gapY + subSize.Y) * 0.5f;
                Typography.Draw(new Vector2(nameLeft, stackTop), name, theme.TextStrong, 1f, FontWeight.SemiBold);
                Typography.Draw(new Vector2(nameLeft, stackTop + nameSize.Y + gapY), presence,
                    conversation!.Presence == 1 ? ui.Accent : AppPalettes.Message.MutedInk, 0.72f);
            }
            else
            {
                Typography.Draw(new Vector2(nameLeft, rowCenterY - nameSize.Y * 0.5f), name, theme.TextStrong, 1f,
                    FontWeight.SemiBold);
            }

            if (conversation is not null && contacts.Find(conversation.OtherUserId) is not null)
            {
                var hitMin = new Vector2(avatarCenter.X - avatarRadius, area.Min.Y);
                var hitMax = new Vector2(nameLeft + nameSize.X, area.Min.Y + AppHeader.Height * scale);
                if (UiInteract.HoverClick(hitMin, hitMax))
                {
                    router.Push(MessageRoute.Contact(conversation.OtherUserId));
                }
            }
        }
    }

    private string PresenceText(ConversationDto? conversation)
    {
        if (conversation is null)
        {
            return string.Empty;
        }

        if (conversation.Presence == 1)
        {
            return Loc.T(L.Message.PresenceOnline);
        }

        if (conversation.LastSeenAtUnix is { } lastSeen)
        {
            return Loc.T(L.Message.PresenceLastSeen, FormatStamp(lastSeen));
        }

        return string.Empty;
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
            if (message.Deleted)
            {
                mapped[index] = new TranscriptMessage(message.Id, message.SenderId,
                    Loc.T(L.Message.DeletedBody), 0, message.CreatedAtUnix, 0, 0, null, senderName, tint,
                    TranscriptFlags.Deleted);
                continue;
            }

            var replySender = string.Empty;
            var replyBody = string.Empty;
            if (message.ReplyToId is not null)
            {
                replySender = message.ReplySenderId == store.MyUserId
                    ? Loc.T(L.Message.You)
                    : message.ReplySenderName ?? Loc.T(L.Message.OriginalUnavailable);
                replyBody = ChatText.QuotePreview(message.ReplyBody, message.ReplyKind);
            }

            TranscriptReaction[]? reactions = null;
            var summaries = message.Reactions;
            if (summaries is { Length: > 0 })
            {
                reactions = new TranscriptReaction[summaries.Length];
                for (var summaryIndex = 0; summaryIndex < summaries.Length; summaryIndex++)
                {
                    reactions[summaryIndex] = new TranscriptReaction(summaries[summaryIndex].Token,
                        summaries[summaryIndex].Count, summaries[summaryIndex].Mine);
                }
            }

            mapped[index] = new TranscriptMessage(message.Id, message.SenderId, message.Body, message.Kind,
                message.CreatedAtUnix, message.MediaWidth, message.MediaHeight, message.ReadAtUnix, senderName, tint,
                MessageFlags(message), message.ReplyToId, replySender, replyBody, message.ReplyKind,
                message.DurationSecs, reactions);
        }

        transcriptCache = mapped;
        return transcriptCache;
    }

    private byte MessageFlags(ChatMessageDto message)
    {
        byte flags = 0;
        if (message.Forwarded)
        {
            flags |= TranscriptFlags.Forwarded;
        }

        if (message.EditedAtUnix is not null)
        {
            flags |= TranscriptFlags.Edited;
        }

        if (message.EncVersion == 0)
        {
            return flags;
        }

        var state = store.DecryptionState(message.Id);
        flags |= TranscriptFlags.Encrypted;
        if (state.IsPlaceholder)
        {
            flags |= TranscriptFlags.Placeholder;
        }
        else if (state.State == DmBodyState.Decrypted && !state.Verified)
        {
            flags |= TranscriptFlags.Unverified;
        }

        return flags;
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
        PumpPendingVoice();
        var delta = ImGui.GetIO().DeltaTime;
        sinceThreadPoll += delta;
        if (sinceThreadPoll >= ThreadPollSeconds)
        {
            sinceThreadPoll = 0f;
            store.RefreshThread();
            store.RefreshTyping(conversationId);
        }

        sinceTypingSend += delta;
        var draft = composer.Draft;
        if (draft != lastTypingDraft)
        {
            lastTypingDraft = draft;
            if (draft.Trim().Length > 0 && sinceTypingSend >= TypingSendSeconds)
            {
                sinceTypingSend = 0f;
                store.SendTyping(conversationId);
            }
        }
    }

    private void ComposerSendText(string conversationId, string text, string? replyToId)
    {
        store.SendMessage(conversationId, text, _ => { }, replyToId);
        transcript.RequestSnapToBottom();
        lastTypingDraft = string.Empty;
        ClearDraft(conversationId);
    }

    private void ComposerEditText(string conversationId, string editId, string text)
    {
        store.EditMessage(conversationId, editId, text, _ => { });
        lastTypingDraft = string.Empty;
        ClearDraft(conversationId);
    }

    private void ComposerSendVoice(string conversationId, byte[] wavBytes, int durationSecs)
    {
        store.SendVoiceMessage(conversationId, wavBytes, durationSecs, _ => { });
        transcript.RequestSnapToBottom();
    }

    private int ResolveVoiceInput()
    {
        return AudioDevices.ResolveInput(configuration.CallInputDevice);
    }
}
