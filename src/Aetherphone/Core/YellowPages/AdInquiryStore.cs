using System.Collections.Concurrent;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Message;
using Aetherphone.Core.Net;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Runtime;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;

namespace Aetherphone.Core.YellowPages;

internal sealed class AdInquiryStore : ChatThreadStoreBase<AdInquiryMessageDto, AdInquiryDto>
{
    private const int MaxThreadLookupPages = 5;

    private readonly YellowPagesClient client;
    private readonly RealtimeSignalBus signals;
    private readonly ConcurrentDictionary<string, string> otherByInquiry = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> scopeByUser = new(StringComparer.Ordinal);
    private volatile bool adKeysHydrated;

    public AdInquiryStore(AethernetSession session, YellowPagesClient client, SafetyClient safety, MediaClient media,
        NotificationService notifications, KeyVault vault, ConversationKeyStore keys,
        DecryptedHistoryStore chatHistory, PhoneVisibility visibility, RealtimeSignalBus signals, AppGate gate)
        : base("YellowPagesInquiries", session, safety, media, notifications, vault, keys, chatHistory, visibility,
            gate)
    {
        this.client = client;
        this.signals = signals;
        signals.AdsPinged += OnAdsPinged;
        signals.ConnectedChanged += OnRealtimeConnected;
    }

    public override bool RealtimePushActive => signals.RealtimeActive;

    public override bool SendWouldDowngrade => !EncryptingCurrent;

    public AdInquiryDto[] Threads => ThreadListItems;

    public bool LoadingThreads => LoadingThreadList;

    public bool ThreadsLoaded => ThreadListLoaded;

    public int UnreadCount => ComputeUnread();

    public void RefreshThreads() => RefreshThreadListCore();

    public AdInquiryDto? Thread(string inquiryId)
    {
        var snapshot = ThreadListItems;
        for (var index = 0; index < snapshot.Length; index++)
        {
            if (snapshot[index].Id == inquiryId)
            {
                return snapshot[index];
            }
        }

        return null;
    }

    public AdInquiryDto? ThreadForAd(string adId)
    {
        var snapshot = ThreadListItems;
        for (var index = 0; index < snapshot.Length; index++)
        {
            if (snapshot[index].AdId == adId && !snapshot[index].Mine)
            {
                return snapshot[index];
            }
        }

        return null;
    }

    public int CountForAd(string adId)
    {
        var snapshot = ThreadListItems;
        var total = 0;
        for (var index = 0; index < snapshot.Length; index++)
        {
            if (snapshot[index].AdId == adId)
            {
                total++;
            }
        }

        return total;
    }

    public int UnreadForAd(string adId)
    {
        var snapshot = ThreadListItems;
        var total = 0;
        for (var index = 0; index < snapshot.Length; index++)
        {
            if (snapshot[index].AdId == adId)
            {
                total += snapshot[index].UnreadCount;
            }
        }

        return total;
    }

    public string? OtherUserOf(string inquiryId) =>
        otherByInquiry.TryGetValue(inquiryId, out var otherId) ? otherId : null;

    public void OpenForAd(string adId, string ownerId, string body, Action<AdInquiryDto?> done)
    {
        if (!session.IsSignedIn)
        {
            done(null);
            return;
        }

        AdInquiryDto? opened = null;
        work.Run("inquiry open", async token =>
        {
            if (!cipher.IsUnlocked)
            {
                return false;
            }

            var status = await keys.EnsureAdKeysAsync(ownerId, MyUserId, token).ConfigureAwait(false);
            if (!status.CanEncrypt)
            {
                return false;
            }

            var scope = ScopeForUser(ownerId);
            if (!cipher.TryEncrypt(scope, keys.CurrentGeneration(scope), body, MyUserId, out var encoded))
            {
                return false;
            }

            var request = new SendAdInquiryRequest(encoded.Envelope, EnvelopeCodec.VersionEnvelope,
                encoded.CommitmentTag);
            opened = await client.OpenInquiryAsync(adId, request, token).ConfigureAwait(false);
            if (opened is null)
            {
                return false;
            }

            if (opened.LastMessageId is { } messageId)
            {
                cipher.RecordDecrypted(messageId, encoded.Envelope, body, encoded.FrankingKeyBase64);
            }

            Remember(opened);
            return true;
        }, ok =>
        {
            if (ok)
            {
                InvalidateThreadList();
                RefreshThreadListCore();
            }

            done(opened);
        });
    }

    public byte[]? DecryptMedia(AdInquiryMessageDto message, byte[] sealedBytes, string threadId)
    {
        if (message.EncVersion != EnvelopeCodec.VersionEnvelope)
        {
            return null;
        }

        return cipher.TryDecryptMedia(message.Id, ScopeFor(threadId), sealedBytes, message.SenderId, message.Kind);
    }

    public static string PreviewFor(AdInquiryDto thread, string revealedBody)
    {
        return thread.LastKind switch
        {
            ImageMediaKind => Loc.T(L.DirectMessages.PhotoPreview),
            VoiceMediaKind => Loc.T(L.DirectMessages.VoicePreview),
            _ => ChatText.ListPreview(revealedBody),
        };
    }

    protected override string ImageUploadScope => "ad-dm";

    protected override string VoiceUploadScope => "ad-voice";

    protected override string ReportTargetType => "ad_message";

    protected override string ScopeFor(string threadId)
    {
        return otherByInquiry.TryGetValue(threadId, out var otherId)
            ? ScopeForUser(otherId)
            : ConversationKeyStore.AdScope(threadId);
    }

    private string ScopeForUser(string otherId)
    {
        if (scopeByUser.TryGetValue(otherId, out var cached))
        {
            return cached;
        }

        var scope = ConversationKeyStore.AdScope(ConversationKeyStore.Pair(MyUserId, otherId));
        scopeByUser[otherId] = scope;
        return scope;
    }

    protected override Task HydrateKeysAsync(CancellationToken token) => EnsureAdsHydratedAsync(token);

    protected override async Task<ChatKeyStatus> EnsureThreadKeysAsync(string threadId, CancellationToken token)
    {
        var otherId = await EnsureOtherAsync(threadId, token).ConfigureAwait(false);
        if (otherId is null)
        {
            return ChatKeyStatus.None;
        }

        return await keys.EnsureAdKeysAsync(otherId, MyUserId, token).ConfigureAwait(false);
    }

    protected override void OnCipherCleared()
    {
        adKeysHydrated = false;
    }

    protected override void OnAccountSwitched()
    {
        otherByInquiry.Clear();
        scopeByUser.Clear();
        adKeysHydrated = false;
    }

    private async Task EnsureAdsHydratedAsync(CancellationToken token)
    {
        if (adKeysHydrated || vault.State != KeyVaultState.Unlocked)
        {
            return;
        }

        adKeysHydrated = true;
        await keys.HydrateAdsAsync(token).ConfigureAwait(false);
    }

    private async Task<string?> EnsureOtherAsync(string threadId, CancellationToken token)
    {
        if (otherByInquiry.TryGetValue(threadId, out var known))
        {
            return known;
        }

        string? cursor = null;
        for (var page = 0; page < MaxThreadLookupPages; page++)
        {
            var result = await client.InquiriesAsync(cursor, token).ConfigureAwait(false);
            if (result is null)
            {
                return null;
            }

            for (var index = 0; index < result.Items.Length; index++)
            {
                Remember(result.Items[index]);
            }

            if (otherByInquiry.TryGetValue(threadId, out var found))
            {
                return found;
            }

            cursor = result.NextCursor;
            if (cursor is null)
            {
                return null;
            }
        }

        return null;
    }

    private void Remember(AdInquiryDto thread)
    {
        otherByInquiry[thread.Id] = thread.OtherUserId;
    }

    protected override async Task<ThreadListPage?> FetchThreadListAsync(string? cursor, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        await EnsureAdsHydratedAsync(token).ConfigureAwait(false);
        var page = await client.InquiriesAsync(cursor, token, onFailure).ConfigureAwait(false);
        if (page is null)
        {
            return null;
        }

        for (var index = 0; index < page.Items.Length; index++)
        {
            Remember(page.Items[index]);
        }

        return new ThreadListPage(page.Items, page.NextCursor);
    }

    protected override async Task<MessagePage?> FetchMessagesPageAsync(string threadId, string? cursor,
        CancellationToken token)
    {
        await EnsureOtherAsync(threadId, token).ConfigureAwait(false);
        var page = await client.InquiryMessagesAsync(threadId, cursor, token).ConfigureAwait(false);
        return page is null ? null : new MessagePage(page.Items, page.NextCursor);
    }

    protected override Task<AdInquiryMessageDto?> SendMessageRequestAsync(string threadId, string body, int kind,
        CancellationToken token, string? mediaKey, int mediaWidth, int mediaHeight, int encVersion,
        string? commitmentTag, string? replyToId, int durationSecs, Action<AepFailure>? onFailure = null)
    {
        if (encVersion != EnvelopeCodec.VersionEnvelope)
        {
            return Task.FromResult<AdInquiryMessageDto?>(null);
        }

        var request = new SendAdInquiryRequest(body, encVersion, commitmentTag, kind, mediaKey, mediaWidth,
            mediaHeight, replyToId, durationSecs);
        return client.SendInquiryAsync(threadId, request, token, onFailure);
    }

    protected override Task<AdInquiryMessageDto?> EditMessageRequestAsync(string messageId, string body,
        CancellationToken token, int encVersion, string? commitmentTag)
    {
        if (encVersion != EnvelopeCodec.VersionEnvelope)
        {
            return Task.FromResult<AdInquiryMessageDto?>(null);
        }

        return client.EditInquiryMessageAsync(messageId, body, encVersion, commitmentTag, token);
    }

    protected override Task<bool> DeleteMessageRequestAsync(string messageId, CancellationToken token) =>
        client.DeleteInquiryMessageAsync(messageId, token);

    protected override Task<bool> DeleteThreadRequestAsync(string threadId, CancellationToken token) =>
        client.ClearInquiryAsync(threadId, token);

    protected override Task SetReactionRequestAsync(string messageId, string reactionToken, CancellationToken token) =>
        client.SetInquiryReactionAsync(messageId, reactionToken, token);

    protected override Task<ReactionListDto?> FetchReactionsAsync(string messageId, CancellationToken token) =>
        client.InquiryReactionsAsync(messageId, token);

    protected override Task SendTypingRequestAsync(string threadId, CancellationToken token) =>
        client.SendInquiryTypingAsync(threadId, token);

    protected override async Task<bool?> FetchOtherTypingAsync(string threadId, CancellationToken token)
    {
        var result = await client.InquiryTypingAsync(threadId, token).ConfigureAwait(false);
        return result?.OtherTyping;
    }

    protected override async Task<string?> FetchMediaUrlRequestAsync(string messageId, CancellationToken token)
    {
        var result = await client.InquiryMediaUrlAsync(messageId, token).ConfigureAwait(false);
        return result?.Url;
    }

    protected override long MessageTimeOf(AdInquiryMessageDto message) => message.CreatedAtUnix;

    protected override int MessageEncVersionOf(AdInquiryMessageDto message) => message.EncVersion;

    protected override string MessageBodyOf(AdInquiryMessageDto message) => message.Body;

    protected override int MessageKindOf(AdInquiryMessageDto message) => message.Kind;

    protected override string MessageSenderIdOf(AdInquiryMessageDto message) => message.SenderId;

    protected override ReactionSummaryDto[]? ReactionsOf(AdInquiryMessageDto message) => message.Reactions;

    protected override AdInquiryMessageDto WithReactions(AdInquiryMessageDto message,
        ReactionSummaryDto[]? reactions) => message with { Reactions = reactions };

    protected override AdInquiryMessageDto WithBody(AdInquiryMessageDto message, string body) =>
        message with { Body = body };

    protected override AdInquiryMessageDto PreserveLocalFields(AdInquiryMessageDto updated,
        AdInquiryMessageDto existing) =>
        updated with { Reactions = existing.Reactions, ReadAtUnix = existing.ReadAtUnix };

    protected override AdInquiryMessageDto Tombstone(AdInquiryMessageDto message) => message with
    {
        Deleted = true,
        Body = string.Empty,
        EncVersion = 0,
        CommitmentTag = null,
        DurationSecs = 0,
        Reactions = null,
    };

    protected override AdInquiryMessageDto ResolveOutgoingReply(string scope, AdInquiryMessageDto message)
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

    protected override AdInquiryMessageDto[] DecorateMessages(string threadId, AdInquiryMessageDto[] items)
    {
        var scope = ScopeFor(threadId);
        AdInquiryMessageDto[]? decorated = null;
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

            decorated ??= (AdInquiryMessageDto[])items.Clone();
            decorated[index] = updated;
        }

        return decorated ?? items;
    }

    protected override AdInquiryDto[] DecorateThreadList(AdInquiryDto[] items)
    {
        AdInquiryDto[]? decorated = null;
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            if (item.LastEncVersion != EnvelopeCodec.VersionEnvelope || item.LastBody.Length == 0)
            {
                continue;
            }

            decorated ??= (AdInquiryDto[])items.Clone();
            decorated[index] = item with
            {
                LastBody = cipher.ResolvePreview(item.Id, ScopeForUser(item.OtherUserId), item.LastBody,
                    item.LastSenderId),
            };
        }

        return decorated ?? items;
    }

    protected override string ThreadKeyOf(AdInquiryDto thread) => thread.Id;

    protected override long ThreadLastMessageAtOf(AdInquiryDto thread) => thread.LastMessageAtUnix;

    protected override int ThreadUnreadCountOf(AdInquiryDto thread) => thread.UnreadCount;

    protected override AdInquiryDto WithUnreadCleared(AdInquiryDto thread) => thread with { UnreadCount = 0 };

    protected override PhoneNotification BuildInboxNotification(AdInquiryDto thread)
    {
        var name = SocialIdentity.Name(thread.OtherName, thread.OtherHandle);
        var preview = PreviewFor(thread, thread.LastBody);
        var body = thread.AdTitle.Length > 0 ? $"{thread.AdTitle} · {preview}" : preview;
        return new PhoneNotification(YellowPagesStore.AppId, name, body, DateTime.Now,
            AppPalettes.YellowPages.Accent, thread.Id)
        {
            ActorId = thread.OtherUserId,
            SocialType = SocialActivity.TypeAdInquiry,
        };
    }

    protected override bool IsInboxPreviewReady(AdInquiryDto thread)
    {
        return thread.LastKind != 0
            || thread.LastEncVersion != EnvelopeCodec.VersionEnvelope
            || cipher.IsPreviewResolved(thread.Id, thread.LastBody);
    }

    private void OnAdsPinged()
    {
        InboxCadence.RequestImmediate();
        RequestThreadRefresh();
    }

    private void OnRealtimeConnected(bool connected)
    {
        if (connected)
        {
            InboxCadence.RequestAfterReconnect();
        }
    }

    protected override void DisposeCore()
    {
        signals.AdsPinged -= OnAdsPinged;
        signals.ConnectedChanged -= OnRealtimeConnected;
    }
}
