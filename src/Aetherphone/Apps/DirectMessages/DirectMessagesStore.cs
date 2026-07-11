using System.Collections.Concurrent;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Analytics;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.DirectMessages;

internal sealed class DirectMessagesStore : IDisposable
{
    private const int DmImageMaxDimension = 1280;
    private static readonly TimeSpan InboxPollInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan ViewingGrace = TimeSpan.FromSeconds(4);

    private readonly AethernetSession session;
    private readonly AethernetClient client;
    private readonly NotificationService notifications;
    private readonly KeyVault vault;
    private readonly ConversationKeyStore keys;
    private readonly PeerKeyDirectory peers;
    private readonly StoreWork work = new("Messages");
    private readonly ConcurrentDictionary<string, string> dmMediaUrls = new();
    private readonly ConcurrentDictionary<string, byte> dmMediaLoading = new();
    private readonly ConcurrentDictionary<string, DmDecryptedBody> decryptedBodies = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, (long AtUnix, string Text)> previewCache = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> inboxLastAt = new();

    private volatile ConversationDto[] conversations = Array.Empty<ConversationDto>();
    private volatile bool loadingConversations;
    private volatile bool conversationsLoaded;
    private volatile string? conversationId;
    private volatile ConversationDto? conversation;
    private volatile ConversationMemberDto[] members = Array.Empty<ConversationMemberDto>();
    private volatile ChatMessageDto[] messages = Array.Empty<ChatMessageDto>();
    private volatile bool loadingThread;
    private volatile bool sending;
    private volatile bool otherTyping;

    private volatile bool inboxPolling;
    private bool inboxPrimed;
    private DateTime lastInboxPollUtc = DateTime.MinValue;
    private volatile string? viewingConversationId;
    private DateTime lastViewingUtc = DateTime.MinValue;
    private volatile bool vaultRefreshRequested;
    private volatile ChatKeyStatus currentKeyStatus = ChatKeyStatus.None;

    public DirectMessagesStore(AethernetSession session, AethernetClient client, NotificationService notifications,
        KeyVault vault, ConversationKeyStore keys, PeerKeyDirectory peers)
    {
        this.session = session;
        this.client = client;
        this.notifications = notifications;
        this.vault = vault;
        this.keys = keys;
        this.peers = peers;
        vault.Changed += OnVaultChanged;
        Plugin.Framework.Update += OnFrameworkTick;
    }

    public bool IsSignedIn => session.IsSignedIn;
    public string MyUserId => session.CurrentUser?.Id ?? string.Empty;
    public ConversationDto[] Conversations => conversations;
    public bool LoadingConversations => loadingConversations;
    public bool ConversationsLoaded => conversationsLoaded;
    public string? ConversationId => conversationId;
    public ConversationDto? Conversation => conversation;
    public ConversationMemberDto[] Members => members;
    public ChatMessageDto[] Messages => messages;
    public bool LoadingThread => loadingThread;
    public bool Sending => sending;
    public bool OtherTyping => otherTyping;
    public KeyVaultState VaultState => vault.State;
    public ChatKeyStatus CurrentKeyStatus => currentKeyStatus;
    public bool EncryptingCurrent => vault.State == KeyVaultState.Unlocked && currentKeyStatus.CanEncrypt;
    public string? MyPublicKey => vault.PublicKey;
    public int MyKeyVersion => vault.KeyVersion;

    public UserPublicKeyDto? PeerKey(string userId) => peers.Cached(userId);

    public void RequestPeerKeys(string[] userIds)
    {
        work.Run("peer keys", async token => await peers.ResolveAsync(userIds, token).ConfigureAwait(false));
    }

    public DmDecryptedBody DecryptionState(string messageId)
    {
        return decryptedBodies.TryGetValue(messageId, out var state)
            ? state
            : new DmDecryptedBody(DmBodyState.Plain, string.Empty, null, false);
    }

    public bool HasRotationNotice(string userId) => peers.HasRotationNotice(userId);

    public void ClearRotationNotice(string userId) => peers.ClearRotationNotice(userId);

    public int UnreadTotal
    {
        get
        {
            var snapshot = conversations;
            var total = 0;
            for (var index = 0; index < snapshot.Length; index++)
            {
                if (!snapshot[index].Muted)
                {
                    total += snapshot[index].UnreadCount;
                }
            }

            return total;
        }
    }

    public void NoteConversationViewed(string id)
    {
        viewingConversationId = id;
        lastViewingUtc = DateTime.UtcNow;
        notifications.RemoveGroup(id);
    }

    private void OnFrameworkTick(IFramework framework)
    {
        if (!session.IsSignedIn)
        {
            inboxPrimed = false;
            vaultRefreshRequested = false;
            return;
        }

        EnsureVaultRefreshed();
        var now = DateTime.UtcNow;
        if (now - lastInboxPollUtc < InboxPollInterval)
        {
            return;
        }

        lastInboxPollUtc = now;
        PollInbox();
    }

    private void EnsureVaultRefreshed()
    {
        if (vaultRefreshRequested || session.CurrentUser is null)
        {
            return;
        }

        vaultRefreshRequested = true;
        work.Run("vault refresh", async token =>
        {
            await vault.RefreshAsync(token).ConfigureAwait(false);
            if (vault.State == KeyVaultState.Unlocked)
            {
                await keys.HydrateAsync(token).ConfigureAwait(false);
            }
        });
    }

    private void OnVaultChanged()
    {
        decryptedBodies.Clear();
        previewCache.Clear();
        if (vault.State != KeyVaultState.Unlocked)
        {
            currentKeyStatus = ChatKeyStatus.None;
            return;
        }

        work.Run("vault unlocked", async token =>
        {
            await keys.HydrateAsync(token).ConfigureAwait(false);
            var current = conversationId;
            if (current is not null)
            {
                var status = await keys.EnsureChatKeysAsync(current, token).ConfigureAwait(false);
                if (conversationId == current)
                {
                    currentKeyStatus = status;
                }
            }

            conversationsLoaded = false;
        });
    }

    private void PollInbox()
    {
        if (inboxPolling)
        {
            return;
        }

        inboxPolling = true;
        work.Run("inbox poll", async token =>
        {
            var page = await client.ConversationsAsync(token).ConfigureAwait(false);
            if (page is not null)
            {
                var decorated = DecorateConversations(page.Items);
                conversations = decorated;
                RaiseInboxNotifications(decorated);
            }
        }, () => inboxPolling = false);
    }

    private void RaiseInboxNotifications(ConversationDto[] items)
    {
        var primed = inboxPrimed;
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            var previous = inboxLastAt.GetValueOrDefault(item.Id, 0L);
            inboxLastAt[item.Id] = item.LastMessageAtUnix;
            if (!primed || item.LastMessageAtUnix <= previous || item.UnreadCount <= 0 || item.Muted)
            {
                continue;
            }

            if (viewingConversationId == item.Id && DateTime.UtcNow - lastViewingUtc < ViewingGrace)
            {
                continue;
            }

            notifications.Notify(new PhoneNotification("message", DisplayTitle(item), PreviewText(item), DateTime.Now,
                AppPalettes.Message.Accent, item.Id));
        }

        inboxPrimed = true;
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

    public void RefreshConversations()
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        loadingConversations = true;
        work.Run("conversations", async token =>
        {
            var page = await client.ConversationsAsync(token).ConfigureAwait(false);
            if (page is not null)
            {
                conversations = DecorateConversations(page.Items);
            }
        }, () =>
        {
            loadingConversations = false;
            conversationsLoaded = true;
        });
    }

    public void OpenConversation(string id)
    {
        if (conversationId == id && (messages.Length > 0 || loadingThread))
        {
            return;
        }

        conversationId = id;
        conversation = FindConversation(id);
        messages = Array.Empty<ChatMessageDto>();
        otherTyping = false;
        loadingThread = true;
        currentKeyStatus = ChatKeyStatus.None;
        work.Run("thread open", async token =>
        {
            var detail = await client.ConversationAsync(id, token).ConfigureAwait(false);
            if (conversationId == id && detail is not null)
            {
                conversation = detail.Conversation;
                members = detail.Members;
            }

            var status = await keys.EnsureChatKeysAsync(id, token).ConfigureAwait(false);
            if (conversationId == id)
            {
                currentKeyStatus = status;
            }

            var page = await client.ChatMessagesAsync(id, token).ConfigureAwait(false);
            if (conversationId == id && page is not null)
            {
                messages = DecorateMessages(id, page.Items);
            }
        }, () =>
        {
            if (conversationId == id)
            {
                loadingThread = false;
            }
        });
    }

    public void RefreshThread()
    {
        var current = conversationId;
        if (current is null || loadingThread)
        {
            return;
        }

        work.Run("thread refresh", async token =>
        {
            var page = await client.ChatMessagesAsync(current, token).ConfigureAwait(false);
            if (conversationId == current && page is not null)
            {
                messages = DecorateMessages(current, page.Items);
            }
        });
    }

    public void RefreshDetail()
    {
        var current = conversationId;
        if (current is null)
        {
            return;
        }

        work.Run("thread detail", async token =>
        {
            var detail = await client.ConversationAsync(current, token).ConfigureAwait(false);
            if (conversationId == current && detail is not null)
            {
                conversation = detail.Conversation;
                members = detail.Members;
            }
        });
    }

    public void SendTyping(string id)
    {
        work.Run("typing", async token => await client.SendChatTypingAsync(id, token).ConfigureAwait(false));
    }

    public void RefreshTyping(string id)
    {
        work.Run("typing state", async token =>
        {
            var result = await client.ChatTypingAsync(id, token).ConfigureAwait(false);
            if (conversationId == id && result is not null)
            {
                otherTyping = result.TypingUserIds.Length > 0;
            }
        });
    }

    public void SendMessage(string id, string body, Action<bool> onComplete, string? replyToId = null)
    {
        var trimmed = body.Trim();
        if (trimmed.Length == 0 || sending)
        {
            return;
        }

        sending = true;
        work.Run("send", async token =>
        {
            ChatMessageDto? sent;
            var scope = ConversationKeyStore.ChatScope(id);
            var generation = keys.CurrentGeneration(scope);
            if (EncryptingCurrent && conversationId == id && generation > 0
                && keys.TryGetCek(scope, generation, out var cek))
            {
                var encoded = EnvelopeCodec.Encode(trimmed, cek, generation, scope, MyUserId);
                sent = await client.SendChatMessageAsync(id, encoded.Envelope, 0, token,
                    encVersion: EnvelopeCodec.VersionEnvelope, commitmentTag: encoded.CommitmentTag,
                    replyToId: replyToId)
                    .ConfigureAwait(false);
                if (sent is not null)
                {
                    decryptedBodies[sent.Id] = new DmDecryptedBody(DmBodyState.Decrypted, trimmed,
                        encoded.FrankingKeyBase64, true);
                    sent = sent with { Body = trimmed };
                }
            }
            else
            {
                sent = await client.SendChatMessageAsync(id, trimmed, 0, token, replyToId: replyToId)
                    .ConfigureAwait(false);
            }

            if (sent is null)
            {
                return false;
            }

            if (sent.ReplyEncVersion == EnvelopeCodec.VersionEnvelope)
            {
                sent = sent with { ReplyBody = ResolveQuotedBody(scope, sent) };
            }

            if (conversationId == id)
            {
                messages = CopyOnWrite.Append(messages, sent);
            }

            conversationsLoaded = false;
            Plugin.Analytics.Track(AnalyticsEvents.DmSent("dm"));
            return true;
        }, onComplete, () => sending = false);
    }

    public void SendImageMessage(string id, string sourcePath, string caption, Action<bool> onComplete)
    {
        if (sending)
        {
            return;
        }

        sending = true;
        work.Run("send image", async token =>
        {
            var baked = ImageProcessor.BakeJpeg(sourcePath, DmImageMaxDimension);
            var upload = await client.UploadUrlAsync("image/jpeg", "chat-dm", token).ConfigureAwait(false);
            if (upload is null)
            {
                return false;
            }

            var uploaded = await client.UploadImageAsync(upload.UploadUrl, baked.Bytes, "image/jpeg", token)
                .ConfigureAwait(false);
            if (!uploaded)
            {
                return false;
            }

            var sent = await client
                .SendChatMessageAsync(id, caption.Trim(), 1, token, upload.Key, baked.Width, baked.Height)
                .ConfigureAwait(false);
            if (sent is null)
            {
                return false;
            }

            if (conversationId == id)
            {
                messages = CopyOnWrite.Append(messages, sent);
            }

            conversationsLoaded = false;
            Plugin.Analytics.Track(AnalyticsEvents.DmSent("dm"));
            return true;
        }, onComplete, () => sending = false);
    }

    public void SendVoiceMessage(string id, byte[] wavBytes, int durationSecs, Action<bool> onComplete)
    {
        if (sending)
        {
            return;
        }

        sending = true;
        work.Run("send voice", async token =>
        {
            var upload = await client.UploadUrlAsync("audio/wav", "chat-voice", token).ConfigureAwait(false);
            if (upload is null)
            {
                return false;
            }

            var uploaded = await client.UploadImageAsync(upload.UploadUrl, wavBytes, "audio/wav", token)
                .ConfigureAwait(false);
            if (!uploaded)
            {
                return false;
            }

            var sent = await client.SendChatMessageAsync(id, string.Empty, 3, token, upload.Key,
                durationSecs: durationSecs).ConfigureAwait(false);
            if (sent is null)
            {
                return false;
            }

            if (conversationId == id)
            {
                messages = CopyOnWrite.Append(messages, sent);
            }

            conversationsLoaded = false;
            Plugin.Analytics.Track(AnalyticsEvents.DmSent("dm"));
            return true;
        }, onComplete, () => sending = false);
    }

    public void SetReaction(string messageId, string reactionToken)
    {
        messages = ApplyLocalReaction(messages, messageId, reactionToken);
        work.Run("react", async token =>
            await client.SetChatReactionAsync(messageId, reactionToken, token).ConfigureAwait(false));
    }

    public string MyReactionTo(string messageId)
    {
        var snapshot = messages;
        for (var index = 0; index < snapshot.Length; index++)
        {
            if (snapshot[index].Id != messageId)
            {
                continue;
            }

            var reactions = snapshot[index].Reactions;
            if (reactions is null)
            {
                return string.Empty;
            }

            for (var reactionIndex = 0; reactionIndex < reactions.Length; reactionIndex++)
            {
                if (reactions[reactionIndex].Mine)
                {
                    return reactions[reactionIndex].Token;
                }
            }

            return string.Empty;
        }

        return string.Empty;
    }

    private static ChatMessageDto[] ApplyLocalReaction(ChatMessageDto[] items, string messageId, string reactionToken)
    {
        for (var index = 0; index < items.Length; index++)
        {
            if (items[index].Id != messageId)
            {
                continue;
            }

            var current = items[index].Reactions ?? Array.Empty<ReactionSummaryDto>();
            var next = new List<ReactionSummaryDto>(current.Length + 1);
            var added = false;
            for (var summaryIndex = 0; summaryIndex < current.Length; summaryIndex++)
            {
                var summary = current[summaryIndex];
                if (summary.Mine)
                {
                    summary = summary with { Count = summary.Count - 1, Mine = false };
                }

                if (summary.Token == reactionToken)
                {
                    summary = summary with { Count = summary.Count + 1, Mine = true };
                    added = true;
                }

                if (summary.Count > 0)
                {
                    next.Add(summary);
                }
            }

            if (!added && reactionToken.Length > 0)
            {
                next.Add(new ReactionSummaryDto(reactionToken, 1, true));
            }

            var updated = (ChatMessageDto[])items.Clone();
            updated[index] = items[index] with { Reactions = next.Count > 0 ? next.ToArray() : null };
            return updated;
        }

        return items;
    }

    public void DeleteMessage(string messageId, Action<bool> onComplete)
    {
        work.Run("delete message", async token =>
        {
            var ok = await client.DeleteChatMessageAsync(messageId, token).ConfigureAwait(false);
            if (!ok)
            {
                return false;
            }

            messages = TombstoneLocal(messages, messageId);
            conversationsLoaded = false;
            return true;
        }, onComplete);
    }

    private static ChatMessageDto[] TombstoneLocal(ChatMessageDto[] items, string messageId)
    {
        for (var index = 0; index < items.Length; index++)
        {
            if (items[index].Id != messageId)
            {
                continue;
            }

            var updated = (ChatMessageDto[])items.Clone();
            updated[index] = items[index] with
            {
                Deleted = true,
                Body = string.Empty,
                EncVersion = 0,
                CommitmentTag = null,
                Forwarded = false,
                DurationSecs = 0,
                Reactions = null,
            };
            return updated;
        }

        return items;
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

            conversations = ApplyLocalMute(conversations, id, muted);
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
                sent = await client.SendChatMessageAsync(targetId, source.Body ?? string.Empty, source.Kind, token,
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
                if (vault.State == KeyVaultState.Unlocked && status.CanEncrypt
                    && keys.TryGetCek(scope, status.CurrentGeneration, out var cek))
                {
                    var encoded = EnvelopeCodec.Encode(plaintext, cek, status.CurrentGeneration, scope, MyUserId);
                    sent = await client.SendChatMessageAsync(targetId, encoded.Envelope, 0, token,
                        encVersion: EnvelopeCodec.VersionEnvelope, commitmentTag: encoded.CommitmentTag,
                        forwarded: true).ConfigureAwait(false);
                    if (sent is not null)
                    {
                        decryptedBodies[sent.Id] = new DmDecryptedBody(DmBodyState.Decrypted, plaintext,
                            encoded.FrankingKeyBase64, true);
                        sent = sent with { Body = plaintext };
                    }
                }
                else
                {
                    sent = await client.SendChatMessageAsync(targetId, plaintext, 0, token, forwarded: true)
                        .ConfigureAwait(false);
                }
            }

            if (sent is null)
            {
                return false;
            }

            if (conversationId == targetId)
            {
                messages = CopyOnWrite.Append(messages, sent);
            }

            conversationsLoaded = false;
            Plugin.Analytics.Track(AnalyticsEvents.DmSent("dm"));
            return true;
        }, onComplete);
    }

    public string? DmMediaUrl(string messageId)
    {
        if (dmMediaUrls.TryGetValue(messageId, out var url))
        {
            return url;
        }

        if (!dmMediaLoading.TryAdd(messageId, 0))
        {
            return null;
        }

        work.Run("dm media url", async token =>
        {
            var result = await client.ChatDmMediaUrlAsync(messageId, token).ConfigureAwait(false);
            if (result is not null)
            {
                dmMediaUrls[messageId] = result.Url;
            }
        }, () => dmMediaLoading.TryRemove(messageId, out _));
        return null;
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
                conversationsLoaded = false;
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
                conversationsLoaded = false;
            }

            onResult(detail?.Conversation.Id);
        });
    }

    public void AddMembers(string id, string[] memberIds, Action<bool> onComplete)
    {
        work.Run("add members", async token =>
        {
            var detail = await client.AddChatMembersAsync(id, memberIds, token).ConfigureAwait(false);
            if (detail is null)
            {
                return false;
            }

            if (conversationId == id)
            {
                conversation = detail.Conversation;
                members = detail.Members;
            }

            var status = await keys.EnsureChatKeysAsync(id, token).ConfigureAwait(false);
            if (conversationId == id)
            {
                currentKeyStatus = status;
            }

            conversationsLoaded = false;
            return true;
        }, onComplete);
    }

    public void RemoveMember(string id, string userId, Action<bool> onComplete)
    {
        work.Run("remove member", async token =>
        {
            var ok = await client.RemoveChatMemberAsync(id, userId, token).ConfigureAwait(false);
            if (!ok)
            {
                return false;
            }

            if (userId != MyUserId)
            {
                var status = await keys.EnsureChatKeysAsync(id, token).ConfigureAwait(false);
                if (conversationId == id)
                {
                    currentKeyStatus = status;
                }
            }

            conversationsLoaded = false;
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

            if (conversationId == id)
            {
                conversation = detail.Conversation;
                members = detail.Members;
            }

            conversationsLoaded = false;
            return true;
        }, onComplete);
    }

    private ConversationDto? FindConversation(string id)
    {
        var snapshot = conversations;
        for (var index = 0; index < snapshot.Length; index++)
        {
            if (snapshot[index].Id == id)
            {
                return snapshot[index];
            }
        }

        return null;
    }

    public void ReportMessage(string messageId, string? reason, Action<bool> onComplete)
    {
        var snapshot = messages;
        var targetIndex = -1;
        for (var index = 0; index < snapshot.Length; index++)
        {
            if (snapshot[index].Id == messageId)
            {
                targetIndex = index;
                break;
            }
        }

        if (targetIndex < 0)
        {
            onComplete(false);
            return;
        }

        var reveals = new List<RevealedMessageDto>(6);
        AppendReveal(reveals, snapshot[targetIndex]);
        for (var index = targetIndex - 1; index >= 0 && reveals.Count < 6; index--)
        {
            AppendReveal(reveals, snapshot[index]);
        }

        var revealed = reveals.Count > 0 && reveals[0].MessageId == messageId ? reveals.ToArray() : null;
        work.Run("report message", async token =>
            await client.ReportAsync("chat_message", messageId, reason, token, revealed).ConfigureAwait(false),
            onComplete);
    }

    private void AppendReveal(List<RevealedMessageDto> reveals, ChatMessageDto message)
    {
        if (message.Kind == 2)
        {
            return;
        }

        if (message.EncVersion == 0)
        {
            reveals.Add(new RevealedMessageDto(message.Id, message.Body, null));
            return;
        }

        var state = DecryptionState(message.Id);
        if (state.State == DmBodyState.Decrypted)
        {
            reveals.Add(new RevealedMessageDto(message.Id, state.Text, state.FrankingKey));
        }
    }

    private ChatMessageDto[] DecorateMessages(string id, ChatMessageDto[] items)
    {
        var scope = ConversationKeyStore.ChatScope(id);
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
                var body = ResolveBody(scope, item);
                updated = updated with { Body = body.Text };
            }

            if (encryptedReply)
            {
                updated = updated with { ReplyBody = ResolveQuotedBody(scope, item) };
            }

            decorated[index] = updated;
        }

        return decorated ?? items;
    }

    private string ResolveQuotedBody(string scope, ChatMessageDto item)
    {
        if (item.ReplyToId is null || string.IsNullOrEmpty(item.ReplyBody))
        {
            return Loc.T(L.Encryption.NoKeyPlaceholder);
        }

        if (decryptedBodies.TryGetValue(item.ReplyToId, out var cached) && cached.State == DmBodyState.Decrypted)
        {
            return cached.Text;
        }

        if (!EnvelopeCodec.TryParseGeneration(item.ReplyBody, out var generation))
        {
            return Loc.T(L.Encryption.NoKeyPlaceholder);
        }

        if (!keys.TryGetCek(scope, generation, out var cek))
        {
            return vault.State == KeyVaultState.Unlocked
                ? Loc.T(L.Encryption.NoKeyPlaceholder)
                : Loc.T(L.Encryption.EncryptedPlaceholder);
        }

        var decoded = EnvelopeCodec.Decode(item.ReplyBody, cek, scope, item.ReplySenderId ?? string.Empty, null);
        return decoded.Status == EnvelopeDecodeStatus.Success ? decoded.Body : Loc.T(L.Encryption.NoKeyPlaceholder);
    }

    private DmDecryptedBody ResolveBody(string scope, ChatMessageDto item)
    {
        if (decryptedBodies.TryGetValue(item.Id, out var cached)
            && cached.State is DmBodyState.Decrypted or DmBodyState.Malformed)
        {
            return cached;
        }

        DmDecryptedBody resolved;
        if (!EnvelopeCodec.TryParseGeneration(item.Body, out var generation))
        {
            resolved = new DmDecryptedBody(DmBodyState.Malformed, Loc.T(L.Encryption.NoKeyPlaceholder), null, false);
        }
        else if (!keys.TryGetCek(scope, generation, out var cek))
        {
            resolved = vault.State == KeyVaultState.Unlocked
                ? new DmDecryptedBody(DmBodyState.NoKey, Loc.T(L.Encryption.NoKeyPlaceholder), null, false)
                : new DmDecryptedBody(DmBodyState.Pending, Loc.T(L.Encryption.EncryptedPlaceholder), null, false);
        }
        else
        {
            var decoded = EnvelopeCodec.Decode(item.Body, cek, scope, item.SenderId, item.CommitmentTag);
            resolved = decoded.Status switch
            {
                EnvelopeDecodeStatus.Success => new DmDecryptedBody(DmBodyState.Decrypted, decoded.Body,
                    decoded.FrankingKeyBase64, decoded.CommitmentVerified),
                EnvelopeDecodeStatus.WrongKey => new DmDecryptedBody(DmBodyState.NoKey,
                    Loc.T(L.Encryption.NoKeyPlaceholder), null, false),
                _ => new DmDecryptedBody(DmBodyState.Malformed, Loc.T(L.Encryption.NoKeyPlaceholder), null, false),
            };
        }

        decryptedBodies[item.Id] = resolved;
        return resolved;
    }

    private ConversationDto[] DecorateConversations(ConversationDto[] items)
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
            decorated[index] = item with { LastMessagePreview = ResolvePreview(item) };
        }

        return decorated ?? items;
    }

    private string ResolvePreview(ConversationDto item)
    {
        if (previewCache.TryGetValue(item.Id, out var cached) && cached.AtUnix == item.LastMessageAtUnix)
        {
            return cached.Text;
        }

        var scope = ConversationKeyStore.ChatScope(item.Id);
        var text = Loc.T(L.Encryption.EncryptedPlaceholder);
        if (EnvelopeCodec.TryParseGeneration(item.LastMessagePreview, out var generation)
            && keys.TryGetCek(scope, generation, out var cek))
        {
            var decoded = EnvelopeCodec.Decode(item.LastMessagePreview, cek, scope, item.LastMessageSenderId, null);
            if (decoded.Status == EnvelopeDecodeStatus.Success)
            {
                text = decoded.Body;
            }

            previewCache[item.Id] = (item.LastMessageAtUnix, text);
        }

        return text;
    }

    public void Dispose()
    {
        vault.Changed -= OnVaultChanged;
        Plugin.Framework.Update -= OnFrameworkTick;
        work.Dispose();
    }
}
