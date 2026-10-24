using System.Collections.Concurrent;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Net;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Runtime;
using Aetherphone.Windows.Components;

namespace Aetherphone.Core.Message;

internal sealed class SocialDmStore : ChatThreadStoreBase<DmMessageDto, DmThreadDto>
{
    private readonly SocialDmSurface surface;
    private readonly DmClient client;
    private readonly SocialClient social;
    private readonly ConcurrentDictionary<string, PostDto?> sharedPosts = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> sharedPostFetches = new(StringComparer.Ordinal);
    private volatile bool keysHydrated;

    public SocialDmStore(SocialDmSurface surface, AethernetSession session, DmClient client, SocialClient social,
        SafetyClient safety, MediaClient media, NotificationService notifications, KeyVault vault,
        ConversationKeyStore keys, DecryptedHistoryStore chatHistory, PhoneVisibility visibility,
        RealtimeSignalBus signals, AppInstaller installer)
        : base(surface.LogTag, session, safety, media, notifications, vault, keys, chatHistory, visibility, signals,
            installer.Gate(surface.AppId))
    {
        this.surface = surface;
        this.client = client;
        this.social = social;
        signals.DmPinged += OnDmPinged;
    }

    private void OnDmPinged(string pinged)
    {
        if (pinged != surface.Name)
        {
            return;
        }

        InboxCadence.RequestImmediate();
        RequestThreadRefresh();
    }

    public DmThreadDto[] Threads => ThreadListItems;
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

    protected override bool IsThreadMuted(DmThreadDto thread) => thread.Pending;

    protected override bool TickActive => base.TickActive && surface.Live;

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
                var updated = (DmThreadDto[])snapshot.Clone();
                updated[index] = snapshot[index] with { Pending = false };
                ThreadListItems = updated;
            }

            break;
        }

        work.Run("thread accept", async token =>
            await client.AcceptThreadAsync(otherId, token).ConfigureAwait(false), RefreshThreads);
    }

    private void AcceptThreadIfPending(string otherId)
    {
        if (IsThreadPending(otherId))
        {
            AcceptThread(otherId);
        }
    }

    protected override string ImageUploadScope => surface.ImageUploadScope;
    protected override string VoiceUploadScope => surface.VoiceUploadScope;
    protected override string ReportTargetType => surface.ReportTargetType;
    protected override string TypingSignalType => surface.TypingSignalType;

    protected override string ScopeFor(string threadId) =>
        ConversationKeyStore.PairScope(surface.Name, ConversationKeyStore.Pair(MyUserId, threadId));

    protected override Task HydrateKeysAsync(CancellationToken token) => EnsureKeysHydratedAsync(token);

    protected override Task<ChatKeyStatus> EnsureThreadKeysAsync(string threadId, CancellationToken token) =>
        keys.EnsurePairKeysAsync(surface.Name, threadId, MyUserId, token);

    protected override void OnCipherCleared()
    {
        keysHydrated = false;
    }

    protected override void OnAccountSwitched()
    {
        sharedPosts.Clear();
        sharedPostFetches.Clear();
    }

    private async Task EnsureKeysHydratedAsync(CancellationToken token)
    {
        if (keysHydrated || vault.State != KeyVaultState.Unlocked)
        {
            return;
        }

        keysHydrated = true;
        await keys.HydratePairsAsync(surface.Name, token).ConfigureAwait(false);
    }

    protected override async Task<ThreadListPage?> FetchThreadListAsync(string? cursor, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        await EnsureKeysHydratedAsync(token).ConfigureAwait(false);
        var page = await client.ThreadsAsync(cursor, token, onFailure).ConfigureAwait(false);
        return page is null ? null : new ThreadListPage(page.Items, page.NextCursor);
    }

    protected override async Task<MessagePage?> FetchMessagesPageAsync(string threadId, string? cursor,
        CancellationToken token)
    {
        var page = await client.MessagesAsync(threadId, cursor, token).ConfigureAwait(false);
        return page is null ? null : new MessagePage(page.Items, page.NextCursor);
    }

    protected override async Task<DmMessageDto?> SendMessageRequestAsync(string threadId, string body, int kind,
        CancellationToken token, string? mediaKey, int mediaWidth, int mediaHeight, int encVersion,
        string? commitmentTag, string? replyToId, int durationSecs, Action<AepFailure>? onFailure = null)
    {
        var sent = await client.SendMessageAsync(threadId, body, kind, token, mediaKey, mediaWidth, mediaHeight,
            encVersion, commitmentTag, replyToId, durationSecs, onFailure: onFailure).ConfigureAwait(false);
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
            DmMessageDto? sent;
            var encoded = default(EncryptedOutbound);
            var encrypted = cipher.IsUnlocked && status.CanEncrypt
                && cipher.TryEncrypt(scope, generation, postId, MyUserId, out encoded);
            if (DowngradeBlocked(otherId, "post share", encrypted, status))
            {
                return;
            }

            if (encrypted)
            {
                sent = await SendMessageRequestAsync(otherId, encoded.Envelope, PostShareKind, token, null, 0, 0,
                    EnvelopeCodec.VersionEnvelope, encoded.CommitmentTag, null, 0).ConfigureAwait(false);
                if (sent is not null)
                {
                    cipher.RecordDecrypted(sent.Id, encoded.Envelope, postId, encoded.FrankingKeyBase64);
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

    public void SendStoryReply(string otherId, string storyId, string text)
    {
        if (otherId.Length == 0 || storyId.Length == 0 || text.Length == 0)
        {
            return;
        }

        work.Run("send story reply", async token =>
        {
            var status = await EnsureThreadKeysAsync(otherId, token).ConfigureAwait(false);
            var scope = ScopeFor(otherId);
            var generation = keys.CurrentGeneration(scope);
            DmMessageDto? sent;
            var encoded = default(EncryptedOutbound);
            var encrypted = cipher.IsUnlocked && status.CanEncrypt
                && cipher.TryEncrypt(scope, generation, text, MyUserId, out encoded);
            if (DowngradeBlocked(otherId, "story reply", encrypted, status))
            {
                return;
            }

            if (encrypted)
            {
                sent = await client.SendMessageAsync(otherId, encoded.Envelope, StoryReplyKind, token, null, 0, 0,
                    EnvelopeCodec.VersionEnvelope, encoded.CommitmentTag, null, 0, storyId).ConfigureAwait(false);
                if (sent is not null)
                {
                    cipher.RecordDecrypted(sent.Id, encoded.Envelope, text, encoded.FrankingKeyBase64);
                    sent = sent with { Body = text };
                }
            }
            else
            {
                sent = await client.SendMessageAsync(otherId, text, StoryReplyKind, token, null, 0, 0, 0, null,
                    null, 0, storyId).ConfigureAwait(false);
            }

            if (sent is null)
            {
                return;
            }

            AcceptThreadIfPending(otherId);
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

    protected override Task<DmMessageDto?> EditMessageRequestAsync(string messageId, string body,
        CancellationToken token, int encVersion, string? commitmentTag)
    {
        return client.EditMessageAsync(messageId, body, token, encVersion, commitmentTag);
    }

    protected override Task<bool> DeleteMessageRequestAsync(string messageId, CancellationToken token) =>
        client.DeleteMessageAsync(messageId, token);

    protected override Task<bool> DeleteThreadRequestAsync(string threadId, CancellationToken token) =>
        client.DeleteThreadAsync(threadId, token);

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

    protected override async Task<ReadMarkOutcome> MarkThreadReadRequestAsync(string threadKey, CancellationToken token)
    {
        var failure = AepFailure.None;
        var succeeded = await client.MarkThreadReadAsync(threadKey, token, reported => failure = reported)
            .ConfigureAwait(false);
        return ReadConfirmations.Classify(succeeded, failure, missingRouteIsUnsupported: true);
    }

    protected override long MessageTimeOf(DmMessageDto message) => message.CreatedAtUnix;

    protected override int MessageEncVersionOf(DmMessageDto message) => message.EncVersion;

    protected override string MessageBodyOf(DmMessageDto message) => message.Body;

    protected override int MessageKindOf(DmMessageDto message) => message.Kind;

    protected override string MessageSenderIdOf(DmMessageDto message) => message.SenderId;

    protected override ReactionSummaryDto[]? ReactionsOf(DmMessageDto message) => message.Reactions;

    protected override DmMessageDto WithReactions(DmMessageDto message, ReactionSummaryDto[]? reactions) =>
        message with { Reactions = reactions };

    protected override DmMessageDto WithBody(DmMessageDto message, string body) =>
        message with { Body = body };

    protected override DmMessageDto PreserveLocalFields(DmMessageDto updated, DmMessageDto existing) =>
        updated with { Reactions = existing.Reactions, ReadAtUnix = existing.ReadAtUnix };

    protected override DmMessageDto Tombstone(DmMessageDto message)
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

    protected override DmMessageDto ResolveOutgoingReply(string scope, DmMessageDto message)
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

    protected override string NotificationAppId => surface.AppId;

    protected override string ThreadKeyOf(DmThreadDto thread) => thread.OtherUserId;

    protected override long ThreadLastMessageAtOf(DmThreadDto thread) => thread.LastMessageAtUnix;

    protected override int ThreadUnreadCountOf(DmThreadDto thread) => thread.UnreadCount;

    protected override string? ThreadLastMessageIdOf(DmThreadDto thread) => thread.LastMessageId;

    protected override DmThreadDto WithUnreadCleared(DmThreadDto thread) => thread with { UnreadCount = 0 };

    protected override PhoneNotification BuildInboxNotification(DmThreadDto thread)
    {
        var name = string.IsNullOrEmpty(thread.OtherDisplayName) ? thread.OtherHandle : thread.OtherDisplayName;
        var preview = thread.LastMessageKind == PostShareKind
            ? Loc.T(surface.SharedPost)
            : ChatText.ListPreview(thread.LastMessagePreview);
        return new PhoneNotification(NotificationAppId, name, preview, DateTime.Now,
            surface.Accent, thread.OtherUserId);
    }

    protected override bool IsInboxPreviewReady(DmThreadDto thread)
    {
        return thread.LastMessageKind == PostShareKind
            || thread.LastMessageEncVersion != EnvelopeCodec.VersionEnvelope
            || cipher.IsPreviewResolved(thread.OtherUserId, thread.LastMessagePreview);
    }

    protected override DmMessageDto[] DecorateMessages(string threadId, DmMessageDto[] items)
    {
        var scope = ScopeFor(threadId);
        DmMessageDto[]? decorated = null;
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

            decorated ??= (DmMessageDto[])items.Clone();
            decorated[index] = updated;
        }

        return decorated ?? items;
    }

    protected override DmThreadDto[] DecorateThreadList(DmThreadDto[] items)
    {
        DmThreadDto[]? decorated = null;
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            var isPostShare = item.LastMessageKind == PostShareKind;
            if (!isPostShare && item.LastMessageEncVersion != EnvelopeCodec.VersionEnvelope)
            {
                continue;
            }

            decorated ??= (DmThreadDto[])items.Clone();
            if (isPostShare)
            {
                decorated[index] = item with { LastMessagePreview = Loc.T(surface.SharedPost) };
                continue;
            }

            var scope = ScopeFor(item.OtherUserId);
            decorated[index] = item with
            {
                LastMessagePreview = cipher.ResolvePreview(item.OtherUserId, scope, item.LastMessagePreview,
                    item.LastMessageSenderId),
            };
        }

        return decorated ?? items;
    }

    public byte[]? DecryptMedia(DmMessageDto message, byte[] sealedBytes, string threadPartnerId)
    {
        if (message.EncVersion != EnvelopeCodec.VersionEnvelope)
        {
            return null;
        }

        return cipher.TryDecryptMedia(message.Id, ScopeFor(threadPartnerId), sealedBytes, message.SenderId,
            message.Kind);
    }

    protected override void DisposeCore()
    {
        signals.DmPinged -= OnDmPinged;
    }
}
