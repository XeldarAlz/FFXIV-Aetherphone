using System.Collections.Concurrent;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Message;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Aethergram;

internal sealed class GramDmStore : ChatThreadStoreBase<GramMessageDto, GramThreadDto>
{
    private readonly GramDmClient client;
    private readonly SocialClient social;
    private readonly RealtimeSignalBus signals;
    private readonly ConcurrentDictionary<string, PostDto?> sharedPosts = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> sharedPostFetches = new(StringComparer.Ordinal);
    private volatile bool gramKeysHydrated;

    public GramDmStore(AethernetSession session, GramDmClient client, SocialClient social, SafetyClient safety,
        MediaClient media, NotificationService notifications, KeyVault vault, ConversationKeyStore keys,
        PhoneVisibility visibility, RealtimeSignalBus signals)
        : base("AethergramDm", session, safety, media, notifications, vault, keys, visibility)
    {
        this.client = client;
        this.social = social;
        this.signals = signals;
        signals.GramPinged += OnGramPinged;
    }

    public override bool RealtimePushActive => signals.RealtimeActive;

    private void OnGramPinged()
    {
        InboxCadence.RequestImmediate();
        RefreshThread();
    }

    public GramThreadDto[] Threads => ThreadListItems;
    public bool LoadingThreads => LoadingThreadList;
    public bool ThreadsLoaded => ThreadListLoaded;
    public int UnreadCount => ComputeUnread();

    public int RequestCount
    {
        get
        {
            var snapshot = ThreadListItems;
            var count = 0;
            for (var index = 0; index < snapshot.Length; index++)
            {
                if (snapshot[index].Pending)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public void RefreshThreads() => RefreshThreadListCore();

    protected override bool IsThreadMuted(GramThreadDto thread) => thread.Pending;

    public bool IsThreadPending(string otherId)
    {
        var snapshot = ThreadListItems;
        for (var index = 0; index < snapshot.Length; index++)
        {
            if (snapshot[index].OtherUserId == otherId)
            {
                return snapshot[index].Pending;
            }
        }

        return false;
    }

    public void AcceptThread(string otherId)
    {
        var snapshot = ThreadListItems;
        for (var index = 0; index < snapshot.Length; index++)
        {
            if (snapshot[index].OtherUserId != otherId)
            {
                continue;
            }

            if (snapshot[index].Pending)
            {
                var updated = (GramThreadDto[])snapshot.Clone();
                updated[index] = snapshot[index] with { Pending = false };
                ThreadListItems = updated;
            }

            break;
        }

        work.Run("thread accept", async token =>
            await client.AcceptThreadAsync(otherId, token).ConfigureAwait(false), RefreshThreads);
    }

    public void DeleteThread(string otherId, Action? onDone = null)
    {
        var snapshot = ThreadListItems;
        for (var index = 0; index < snapshot.Length; index++)
        {
            if (snapshot[index].OtherUserId != otherId)
            {
                continue;
            }

            var updated = new GramThreadDto[snapshot.Length - 1];
            Array.Copy(snapshot, 0, updated, 0, index);
            Array.Copy(snapshot, index + 1, updated, index, snapshot.Length - index - 1);
            ThreadListItems = updated;
            break;
        }

        CloseThreadIfCurrent(otherId);
        work.Run("thread delete", async token =>
            await client.DeleteThreadAsync(otherId, token).ConfigureAwait(false), succeeded =>
        {
            RefreshThreads();
            if (succeeded)
            {
                onDone?.Invoke();
            }
        });
    }

    private void AcceptThreadIfPending(string otherId)
    {
        if (IsThreadPending(otherId))
        {
            AcceptThread(otherId);
        }
    }

    protected override string ImageUploadScope => "gram-dm";
    protected override string VoiceUploadScope => "gram-voice";
    protected override string ReportTargetType => "gram_message";

    protected override string ScopeFor(string threadId) =>
        ConversationKeyStore.GramScope(ConversationKeyStore.Pair(MyUserId, threadId));

    protected override Task HydrateKeysAsync(CancellationToken token) => EnsureGramHydratedAsync(token);

    protected override Task<ChatKeyStatus> EnsureThreadKeysAsync(string threadId, CancellationToken token) =>
        keys.EnsureGramKeysAsync(threadId, MyUserId, token);

    protected override void OnCipherCleared()
    {
        gramKeysHydrated = false;
    }

    private async Task EnsureGramHydratedAsync(CancellationToken token)
    {
        if (gramKeysHydrated || vault.State != KeyVaultState.Unlocked)
        {
            return;
        }

        gramKeysHydrated = true;
        await keys.HydrateGramAsync(token).ConfigureAwait(false);
    }

    protected override async Task<GramThreadDto[]?> FetchThreadListAsync(CancellationToken token)
    {
        await EnsureGramHydratedAsync(token).ConfigureAwait(false);
        var page = await client.ThreadsAsync(null, token).ConfigureAwait(false);
        return page?.Items;
    }

    protected override async Task<MessagePage?> FetchMessagesPageAsync(string threadId, string? cursor,
        CancellationToken token)
    {
        var page = await client.MessagesAsync(threadId, cursor, token).ConfigureAwait(false);
        return page is null ? null : new MessagePage(page.Items, page.NextCursor);
    }

    protected override async Task<GramMessageDto?> SendMessageRequestAsync(string threadId, string body, int kind,
        CancellationToken token, string? mediaKey, int mediaWidth, int mediaHeight, int encVersion,
        string? commitmentTag, string? replyToId, int durationSecs)
    {
        var sent = await client.SendMessageAsync(threadId, body, kind, token, mediaKey, mediaWidth, mediaHeight,
            encVersion, commitmentTag, replyToId, durationSecs).ConfigureAwait(false);
        if (sent is not null)
        {
            AcceptThreadIfPending(threadId);
        }

        return sent;
    }

    public void SendPostShare(string otherId, string postId)
    {
        if (otherId.Length == 0 || postId.Length == 0)
        {
            return;
        }

        work.Run("send post share", async token =>
        {
            var status = await EnsureThreadKeysAsync(otherId, token).ConfigureAwait(false);
            var scope = ScopeFor(otherId);
            var generation = keys.CurrentGeneration(scope);
            GramMessageDto? sent;
            if (cipher.IsUnlocked && status.CanEncrypt
                && cipher.TryEncrypt(scope, generation, postId, MyUserId, out var encoded))
            {
                sent = await SendMessageRequestAsync(otherId, encoded.Envelope, PostShareKind, token, null, 0, 0,
                    EnvelopeCodec.VersionEnvelope, encoded.CommitmentTag, null, 0).ConfigureAwait(false);
                if (sent is not null)
                {
                    cipher.RecordDecrypted(sent.Id, postId, encoded.FrankingKeyBase64);
                    sent = sent with { Body = postId };
                }
            }
            else
            {
                sent = await SendMessageRequestAsync(otherId, postId, PostShareKind, token, null, 0, 0, 0, null,
                    null, 0).ConfigureAwait(false);
            }

            if (sent is null)
            {
                return;
            }

            if (CurrentThreadId == otherId)
            {
                MessageItems = CopyOnWrite.Append(MessageItems, sent);
            }

            InvalidateThreadList();
        });
    }

    public bool TryResolvePost(string postId, out PostDto? post)
    {
        if (sharedPosts.TryGetValue(postId, out post))
        {
            return true;
        }

        post = null;
        if (!sharedPostFetches.TryAdd(postId, 0))
        {
            return false;
        }

        work.Run("shared post fetch", async token =>
        {
            var fetched = await social.PostAsync(postId, token).ConfigureAwait(false);
            sharedPosts[postId] = fetched;
        }, () => sharedPostFetches.TryRemove(postId, out _));
        return false;
    }

    protected override Task<GramMessageDto?> EditMessageRequestAsync(string messageId, string body,
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
        return result?.OtherTyping;
    }

    protected override async Task<string?> FetchMediaUrlRequestAsync(string messageId, CancellationToken token)
    {
        var result = await client.DmMediaUrlAsync(messageId, token).ConfigureAwait(false);
        return result?.Url;
    }

    protected override long MessageTimeOf(GramMessageDto message) => message.CreatedAtUnix;

    protected override int MessageEncVersionOf(GramMessageDto message) => message.EncVersion;

    protected override string MessageBodyOf(GramMessageDto message) => message.Body;

    protected override ReactionSummaryDto[]? ReactionsOf(GramMessageDto message) => message.Reactions;

    protected override GramMessageDto WithReactions(GramMessageDto message, ReactionSummaryDto[]? reactions) =>
        message with { Reactions = reactions };

    protected override GramMessageDto WithBody(GramMessageDto message, string body) =>
        message with { Body = body };

    protected override GramMessageDto PreserveLocalFields(GramMessageDto updated, GramMessageDto existing) =>
        updated with { Reactions = existing.Reactions, ReadAtUnix = existing.ReadAtUnix };

    protected override GramMessageDto Tombstone(GramMessageDto message)
    {
        return message with
        {
            Deleted = true,
            Body = string.Empty,
            EncVersion = 0,
            CommitmentTag = null,
            DurationSecs = 0,
            Reactions = null,
        };
    }

    protected override GramMessageDto ResolveOutgoingReply(string scope, GramMessageDto message)
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

    protected override string ThreadKeyOf(GramThreadDto thread) => thread.OtherUserId;

    protected override long ThreadLastMessageAtOf(GramThreadDto thread) => thread.LastMessageAtUnix;

    protected override int ThreadUnreadCountOf(GramThreadDto thread) => thread.UnreadCount;

    protected override PhoneNotification BuildInboxNotification(GramThreadDto thread)
    {
        var name = string.IsNullOrEmpty(thread.OtherDisplayName) ? thread.OtherHandle : thread.OtherDisplayName;
        var preview = thread.LastMessageKind == PostShareKind
            ? Loc.T(L.Aethergram.SharedPost)
            : thread.LastMessagePreview;
        return new PhoneNotification("aethergram", name, preview, DateTime.Now,
            AppPalettes.Aethergram.Accent, thread.OtherUserId);
    }

    protected override GramMessageDto[] DecorateMessages(string threadId, GramMessageDto[] items)
    {
        var scope = ScopeFor(threadId);
        GramMessageDto[]? decorated = null;
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            var needsBody = item.EncVersion == EnvelopeCodec.VersionEnvelope;
            var needsReply = item.ReplyEncVersion == EnvelopeCodec.VersionEnvelope;
            if (!needsBody && !needsReply)
            {
                continue;
            }

            var updated = item;
            if (needsBody)
            {
                updated = updated with
                {
                    Body = cipher.ResolveBody(scope, item.Id, item.Body, item.SenderId, item.CommitmentTag).Text,
                };
            }

            if (needsReply)
            {
                updated = updated with
                {
                    ReplyBody = cipher.ResolveQuotedBody(scope, item.ReplyToId, item.ReplyBody, item.ReplySenderId),
                };
            }

            decorated ??= (GramMessageDto[])items.Clone();
            decorated[index] = updated;
        }

        return decorated ?? items;
    }

    protected override GramThreadDto[] DecorateThreadList(GramThreadDto[] items)
    {
        GramThreadDto[]? decorated = null;
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            var isPostShare = item.LastMessageKind == PostShareKind;
            if (!isPostShare && item.LastMessageEncVersion != EnvelopeCodec.VersionEnvelope)
            {
                continue;
            }

            decorated ??= (GramThreadDto[])items.Clone();
            if (isPostShare)
            {
                decorated[index] = item with { LastMessagePreview = Loc.T(L.Aethergram.SharedPost) };
                continue;
            }

            var scope = ScopeFor(item.OtherUserId);
            decorated[index] = item with
            {
                LastMessagePreview = cipher.ResolvePreview(item.OtherUserId, scope, item.LastMessageAtUnix,
                    item.LastMessagePreview, item.LastMessageSenderId),
            };
        }

        return decorated ?? items;
    }

    public byte[]? DecryptMedia(GramMessageDto message, byte[] sealedBytes, string threadPartnerId)
    {
        if (message.EncVersion != EnvelopeCodec.VersionEnvelope
            || !cipher.TryGetGeneration(message.Id, out var generation))
        {
            return null;
        }

        var scope = ScopeFor(threadPartnerId);
        return cipher.TryDecryptMedia(scope, generation, sealedBytes, message.SenderId, message.Kind);
    }

    protected override void DisposeCore()
    {
        signals.GramPinged -= OnGramPinged;
    }
}
