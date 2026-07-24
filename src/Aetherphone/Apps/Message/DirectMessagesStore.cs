using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Message;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Message;

internal sealed class DirectMessagesStore : ChatThreadStoreBase<ChatMessageDto, ConversationDto>
{
    private readonly ChatClient client;
    private readonly PeerKeyDirectory peers;
    private readonly RealtimeSignalBus signals;

    private volatile ConversationDto? conversation;
    private volatile ConversationMemberDto[] members = Array.Empty<ConversationMemberDto>();

    public DirectMessagesStore(AethernetSession session, ChatClient client, SafetyClient safety, MediaClient media,
        NotificationService notifications, KeyVault vault, ConversationKeyStore keys, PeerKeyDirectory peers,
        PhoneVisibility visibility, RealtimeSignalBus signals)
        : base("Messages", session, safety, media, notifications, vault, keys, visibility)
    {
        this.client = client;
        this.peers = peers;
        this.signals = signals;
        signals.ChatPinged += OnChatPinged;
    }

    public override bool RealtimePushActive => signals.RealtimeActive;

    private void OnChatPinged()
    {
        InboxCadence.RequestImmediate();
        RefreshThread();
    }

    public ConversationDto[] Conversations => ThreadListItems;
    public bool LoadingConversations => LoadingThreadList;
    public bool ConversationsLoaded => ThreadListLoaded;
    public string? ConversationId => CurrentThreadId;
    public ConversationDto? Conversation => conversation;
    public ConversationMemberDto[] Members => members;
    public string? MyPublicKey => vault.PublicKey;
    public int MyKeyVersion => vault.KeyVersion;
    public int UnreadTotal => ComputeUnread();

    public UserPublicKeyDto? PeerKey(string userId) => peers.Cached(userId);

    public void RequestPeerKeys(string[] userIds)
    {
        work.Run("peer keys", async token => await peers.ResolveAsync(userIds, token).ConfigureAwait(false));
    }

    public bool HasRotationNotice(string userId) => peers.HasRotationNotice(userId);

    public void ClearRotationNotice(string userId) => peers.ClearRotationNotice(userId);

    public void NoteConversationViewed(string id) => NoteThreadViewed(id);

    public void RefreshConversations() => RefreshThreadListCore();

    protected override void OnAccountSwitched()
    {
        conversation = null;
        members = Array.Empty<ConversationMemberDto>();
    }

    public void OpenConversation(string id) => OpenThread(id);

    protected override string ImageUploadScope => "chat-dm";
    protected override string VoiceUploadScope => "chat-voice";
    protected override string ReportTargetType => "chat_message";

    protected override string ScopeFor(string threadId) => ConversationKeyStore.ChatScope(threadId);

    protected override Task HydrateKeysAsync(CancellationToken token) => keys.HydrateAsync(token);

    protected override Task<ChatKeyStatus> EnsureThreadKeysAsync(string threadId, CancellationToken token) =>
        keys.EnsureChatKeysAsync(threadId, token);

    protected override async Task<ConversationDto[]?> FetchThreadListAsync(CancellationToken token)
    {
        var page = await client.ConversationsAsync(token).ConfigureAwait(false);
        return page?.Items;
    }

    protected override async Task<MessagePage?> FetchMessagesPageAsync(string threadId, string? cursor,
        CancellationToken token)
    {
        var page = await client.MessagesAsync(threadId, cursor, token).ConfigureAwait(false);
        return page is null ? null : new MessagePage(page.Items, page.NextCursor);
    }

    protected override Task<ChatMessageDto?> SendMessageRequestAsync(string threadId, string body, int kind,
        CancellationToken token, string? mediaKey, int mediaWidth, int mediaHeight, int encVersion,
        string? commitmentTag, string? replyToId, int durationSecs)
    {
        return client.SendMessageAsync(threadId, body, kind, token, mediaKey, mediaWidth, mediaHeight, encVersion,
            commitmentTag, replyToId, durationSecs: durationSecs);
    }

    protected override Task<ChatMessageDto?> EditMessageRequestAsync(string messageId, string body,
        CancellationToken token, int encVersion, string? commitmentTag)
    {
        return client.EditMessageAsync(messageId, body, token, encVersion, commitmentTag);
    }

    protected override Task<bool> DeleteMessageRequestAsync(string messageId, CancellationToken token) =>
        client.DeleteMessageAsync(messageId, token);

    protected override Task SetReactionRequestAsync(string messageId, string reactionToken, CancellationToken token) =>
        client.SetReactionAsync(messageId, reactionToken, token);

    protected override Task<ReactionListDto?> FetchReactionsAsync(string messageId, CancellationToken token) =>
        client.ReactionsAsync(messageId, token);

    protected override Task SendTypingRequestAsync(string threadId, CancellationToken token) =>
        client.SendTypingAsync(threadId, token);

    protected override async Task<bool?> FetchOtherTypingAsync(string threadId, CancellationToken token)
    {
        var result = await client.TypingAsync(threadId, token).ConfigureAwait(false);
        return result is null ? null : result.TypingUserIds.Length > 0;
    }

    protected override async Task<string?> FetchMediaUrlRequestAsync(string messageId, CancellationToken token)
    {
        var result = await client.DmMediaUrlAsync(messageId, token).ConfigureAwait(false);
        return result?.Url;
    }

    protected override long MessageTimeOf(ChatMessageDto message) => message.CreatedAtUnix;

    protected override int MessageEncVersionOf(ChatMessageDto message) => message.EncVersion;

    protected override string MessageBodyOf(ChatMessageDto message) => message.Body;

    protected override ReactionSummaryDto[]? ReactionsOf(ChatMessageDto message) => message.Reactions;

    protected override ChatMessageDto WithReactions(ChatMessageDto message, ReactionSummaryDto[]? reactions) =>
        message with { Reactions = reactions };

    protected override ChatMessageDto WithBody(ChatMessageDto message, string body) => message with { Body = body };

    protected override ChatMessageDto PreserveLocalFields(ChatMessageDto updated, ChatMessageDto existing) =>
        updated with { Reactions = existing.Reactions, ReadAtUnix = existing.ReadAtUnix };

    protected override ChatMessageDto Tombstone(ChatMessageDto message)
    {
        return message with
        {
            Deleted = true,
            Body = string.Empty,
            EncVersion = 0,
            CommitmentTag = null,
            Forwarded = false,
            DurationSecs = 0,
            Reactions = null,
        };
    }

    protected override ChatMessageDto ResolveOutgoingReply(string scope, ChatMessageDto message)
    {
        if (message.ReplyEncVersion != EnvelopeCodec.VersionEnvelope)
        {
            return message;
        }

        return message with
        {
            ReplyBody = cipher.ResolveQuotedBody(scope, message.ReplyToId, message.ReplyBody, message.ReplySenderId),
        };
    }

    protected override bool ShouldRevealForReport(ChatMessageDto message) => message.Kind != 2;

    protected override string ThreadKeyOf(ConversationDto thread) => thread.Id;

    protected override long ThreadLastMessageAtOf(ConversationDto thread) => thread.LastMessageAtUnix;

    protected override int ThreadUnreadCountOf(ConversationDto thread) => thread.UnreadCount;

    protected override bool IsThreadMuted(ConversationDto thread) => thread.Muted;

    protected override PhoneNotification BuildInboxNotification(ConversationDto thread)
    {
        return new PhoneNotification("message", DisplayTitle(thread), PreviewText(thread), DateTime.Now,
            AppPalettes.Message.Accent, thread.Id);
    }

    public static string DisplayTitle(ConversationDto item)
    {
        if (item.IsGroup)
        {
            return item.Title.Length > 0 ? item.Title : Loc.T(L.DirectMessages.GroupFallback);
        }

        return item.OtherDisplayName.Length > 0 ? item.OtherDisplayName : item.OtherHandle;
    }

    private static string PreviewText(ConversationDto item)
    {
        if (item.LastMessagePreview.Length > 0)
        {
            return item.LastMessagePreview;
        }

        return item.LastMessageKind switch
        {
            1 => Loc.T(L.DirectMessages.PhotoPreview),
            3 => Loc.T(L.DirectMessages.VoicePreview),
            _ => string.Empty,
        };
    }

    protected override void OnThreadOpening(string threadId)
    {
        conversation = FindConversation(threadId);
    }

    protected override async Task PrefetchThreadAsync(string threadId, CancellationToken token)
    {
        var detail = await client.ConversationAsync(threadId, token).ConfigureAwait(false);
        if (ConversationId == threadId && detail is not null)
        {
            conversation = detail.Conversation;
            members = detail.Members;
        }
    }

    public void RefreshDetail()
    {
        var current = ConversationId;
        if (current is null)
        {
            return;
        }

        work.Run("thread detail", async token =>
        {
            var detail = await client.ConversationAsync(current, token).ConfigureAwait(false);
            if (ConversationId == current && detail is not null)
            {
                conversation = detail.Conversation;
                members = detail.Members;
            }
        });
    }

    public byte[]? DecryptMedia(ChatMessageDto message, byte[] sealedBytes)
    {
        if (message.EncVersion != EnvelopeCodec.VersionEnvelope
            || !cipher.TryGetGeneration(message.Id, out var generation))
        {
            return null;
        }

        var scope = ConversationKeyStore.ChatScope(message.ConversationId);
        return cipher.TryDecryptMedia(scope, generation, sealedBytes, message.SenderId, message.Kind);
    }

    public void SetMuted(string id, bool muted, Action<bool> onComplete)
    {
        work.Run("mute", async token =>
        {
            var ok = await client.MuteConversationAsync(id, muted, token).ConfigureAwait(false);
            if (!ok)
            {
                return false;
            }

            ThreadListItems = ApplyLocalMute(ThreadListItems, id, muted);
            return true;
        }, onComplete);
    }

    private static ConversationDto[] ApplyLocalMute(ConversationDto[] items, string id, bool muted)
    {
        for (var index = 0; index < items.Length; index++)
        {
            if (items[index].Id != id)
            {
                continue;
            }

            var updated = (ConversationDto[])items.Clone();
            updated[index] = items[index] with { Muted = muted };
            return updated;
        }

        return items;
    }

    public void ForwardMessage(ChatMessageDto source, string targetId, Action<bool> onComplete)
    {
        work.Run("forward", async token =>
        {
            ChatMessageDto? sent;
            if (source.Kind != 0)
            {
                if (source.EncVersion == EnvelopeCodec.VersionEnvelope)
                {
                    return false;
                }

                sent = await client.SendMessageAsync(targetId, source.Body ?? string.Empty, source.Kind, token,
                    forwardOfId: source.Id).ConfigureAwait(false);
            }
            else
            {
                var plaintext = source.Body ?? string.Empty;
                if (source.EncVersion == EnvelopeCodec.VersionEnvelope)
                {
                    var state = DecryptionState(source.Id);
                    if (state.State != DmBodyState.Decrypted)
                    {
                        return false;
                    }

                    plaintext = state.Text;
                }

                if (plaintext.Trim().Length == 0)
                {
                    return false;
                }

                var scope = ConversationKeyStore.ChatScope(targetId);
                var status = await keys.EnsureChatKeysAsync(targetId, token).ConfigureAwait(false);
                if (cipher.IsUnlocked && status.CanEncrypt
                    && cipher.TryEncrypt(scope, status.CurrentGeneration, plaintext, MyUserId, out var encoded))
                {
                    sent = await client.SendMessageAsync(targetId, encoded.Envelope, 0, token,
                        encVersion: EnvelopeCodec.VersionEnvelope, commitmentTag: encoded.CommitmentTag,
                        forwarded: true).ConfigureAwait(false);
                    if (sent is not null)
                    {
                        cipher.RecordDecrypted(sent.Id, plaintext, encoded.FrankingKeyBase64);
                        sent = sent with { Body = plaintext };
                    }
                }
                else
                {
                    sent = await client.SendMessageAsync(targetId, plaintext, 0, token, forwarded: true)
                        .ConfigureAwait(false);
                }
            }

            if (sent is null)
            {
                return false;
            }

            if (ConversationId == targetId)
            {
                MessageItems = CopyOnWrite.Append(MessageItems, sent);
            }

            InvalidateThreadList();
            return true;
        }, onComplete);
    }

    public void CreateDirect(string userId, Action<string?> onResult)
    {
        work.Run("create direct", async token =>
        {
            var detail = await client.CreateConversationAsync(new CreateConversationRequest(userId, null, null), token)
                .ConfigureAwait(false);
            if (detail is not null)
            {
                await keys.EnsureChatKeysAsync(detail.Conversation.Id, token).ConfigureAwait(false);
                InvalidateThreadList();
            }

            onResult(detail?.Conversation.Id);
        });
    }

    public void CreateGroup(string title, string[] memberIds, Action<string?> onResult)
    {
        work.Run("create group", async token =>
        {
            var detail = await client
                .CreateConversationAsync(new CreateConversationRequest(null, title, memberIds), token)
                .ConfigureAwait(false);
            if (detail is not null)
            {
                await keys.EnsureChatKeysAsync(detail.Conversation.Id, token).ConfigureAwait(false);
                InvalidateThreadList();
            }

            onResult(detail?.Conversation.Id);
        });
    }

    public void AddMembers(string id, string[] memberIds, Action<bool> onComplete)
    {
        work.Run("add members", async token =>
        {
            var detail = await client.AddMembersAsync(id, memberIds, token).ConfigureAwait(false);
            if (detail is null)
            {
                return false;
            }

            if (ConversationId == id)
            {
                conversation = detail.Conversation;
                members = detail.Members;
            }

            var status = await keys.EnsureChatKeysAsync(id, token).ConfigureAwait(false);
            SetKeyStatusIfCurrent(id, status);
            InvalidateThreadList();
            return true;
        }, onComplete);
    }

    public void RemoveMember(string id, string userId, Action<bool> onComplete)
    {
        work.Run("remove member", async token =>
        {
            var ok = await client.RemoveMemberAsync(id, userId, token).ConfigureAwait(false);
            if (!ok)
            {
                return false;
            }

            if (userId != MyUserId)
            {
                var status = await keys.EnsureChatKeysAsync(id, token).ConfigureAwait(false);
                SetKeyStatusIfCurrent(id, status);
            }

            InvalidateThreadList();
            return true;
        }, onComplete);
    }

    public void Rename(string id, string title, Action<bool> onComplete)
    {
        work.Run("rename", async token =>
        {
            var detail = await client.RenameConversationAsync(id, title, token).ConfigureAwait(false);
            if (detail is null)
            {
                return false;
            }

            if (ConversationId == id)
            {
                conversation = detail.Conversation;
                members = detail.Members;
            }

            InvalidateThreadList();
            return true;
        }, onComplete);
    }

    private ConversationDto? FindConversation(string id)
    {
        var snapshot = ThreadListItems;
        for (var index = 0; index < snapshot.Length; index++)
        {
            if (snapshot[index].Id == id)
            {
                return snapshot[index];
            }
        }

        return null;
    }

    protected override ChatMessageDto[] DecorateMessages(string threadId, ChatMessageDto[] items)
    {
        var scope = ConversationKeyStore.ChatScope(threadId);
        ChatMessageDto[]? decorated = null;
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            var encryptedBody = item.EncVersion == EnvelopeCodec.VersionEnvelope;
            var encryptedReply = item.ReplyEncVersion == EnvelopeCodec.VersionEnvelope;
            if (!encryptedBody && !encryptedReply)
            {
                continue;
            }

            decorated ??= (ChatMessageDto[])items.Clone();
            var updated = item;
            if (encryptedBody)
            {
                var body = cipher.ResolveBody(scope, item.Id, item.Body, item.SenderId, item.CommitmentTag);
                updated = updated with { Body = body.Text };
            }

            if (encryptedReply)
            {
                updated = updated with
                {
                    ReplyBody = cipher.ResolveQuotedBody(scope, item.ReplyToId, item.ReplyBody, item.ReplySenderId),
                };
            }

            decorated[index] = updated;
        }

        return decorated ?? items;
    }

    protected override ConversationDto[] DecorateThreadList(ConversationDto[] items)
    {
        ConversationDto[]? decorated = null;
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            if (item.LastMessageEncVersion != EnvelopeCodec.VersionEnvelope)
            {
                continue;
            }

            decorated ??= (ConversationDto[])items.Clone();
            var scope = ConversationKeyStore.ChatScope(item.Id);
            decorated[index] = item with
            {
                LastMessagePreview = cipher.ResolvePreview(item.Id, scope, item.LastMessageAtUnix,
                    item.LastMessagePreview, item.LastMessageSenderId),
            };
        }

        return decorated ?? items;
    }

    protected override void DisposeCore()
    {
        signals.ChatPinged -= OnChatPinged;
    }
}
