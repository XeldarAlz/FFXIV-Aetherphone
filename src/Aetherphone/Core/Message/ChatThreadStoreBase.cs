using System.Collections.Concurrent;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Analytics;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Media;
using Aetherphone.Core.Notifications;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Message;

internal abstract class ChatThreadStoreBase<TMessage, TThread> : IDisposable
    where TMessage : class, IIdentified
    where TThread : class, IIdentified
{
    protected const int DmImageMaxDimension = 1280;
    protected const int ImageMediaKind = 1;
    protected const int VoiceMediaKind = 3;
    private static readonly TimeSpan ForegroundInboxPollInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan BackgroundInboxPollInterval = TimeSpan.FromSeconds(600);
    private static readonly TimeSpan ViewingGrace = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan VaultRetryInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan KeyStatusRetryInterval = TimeSpan.FromSeconds(15);

    protected readonly AethernetSession session;
    protected readonly SafetyClient safety;
    protected readonly MediaClient media;
    protected readonly KeyVault vault;
    protected readonly ConversationKeyStore keys;
    protected readonly StoreWork work;
    protected readonly MessageCipher cipher;
    protected readonly IAnalyticsService analytics;
    private readonly string logTag;
    private readonly NotificationService notifications;
    private readonly PollCadence inboxCadence;
    private readonly object messagesLock = new();
    private readonly Dictionary<string, long> inboxLastAt = new();
    private readonly ConcurrentDictionary<string, string> dmMediaUrls = new();
    private readonly ConcurrentDictionary<string, byte> dmMediaLoading = new();
    private readonly Comparison<TMessage> messageOrder;

    private volatile TThread[] threadList = Array.Empty<TThread>();
    private volatile bool loadingThreadList;
    private volatile bool threadListLoaded;
    private volatile string? currentThreadId;
    private volatile TMessage[] messages = Array.Empty<TMessage>();
    private volatile string? olderCursor;
    private volatile bool loadingOlder;
    private volatile bool hasMoreOlder;
    private volatile bool loadingThread;
    private volatile bool refreshingThread;
    private volatile bool refreshingTyping;
    private int pollFailureStreak;
    private DateTime pollBackoffUntilUtc = DateTime.MinValue;
    private volatile bool sending;
    private volatile bool otherTyping;

    private volatile bool inboxPolling;
    private bool inboxPrimed;
    private volatile string? viewingThreadKey;
    private DateTime lastViewingUtc = DateTime.MinValue;
    private volatile bool vaultRefreshRequested;
    private volatile bool vaultRefreshInFlight;
    private DateTime nextVaultRetryUtc = DateTime.MinValue;
    private volatile bool keyStatusRefreshing;
    private DateTime lastKeyStatusUtc = DateTime.MinValue;
    private volatile ChatKeyStatus currentKeyStatus = ChatKeyStatus.None;

    protected ChatThreadStoreBase(string logTag, AethernetSession session, SafetyClient safety, MediaClient media,
        NotificationService notifications, KeyVault vault, ConversationKeyStore keys, PhoneVisibility visibility,
        IAnalyticsService analytics)
    {
        this.session = session;
        this.safety = safety;
        this.media = media;
        this.notifications = notifications;
        this.vault = vault;
        this.keys = keys;
        this.analytics = analytics;
        this.logTag = logTag;
        work = new StoreWork(logTag);
        cipher = new MessageCipher(vault, keys);
        messageOrder = CompareByCreatedAt;
        inboxCadence = new PollCadence(visibility, ForegroundInboxPollInterval, BackgroundInboxPollInterval);
        vault.Changed += OnVaultChanged;
        Plugin.Framework.Update += OnFrameworkTick;
    }

    protected readonly record struct MessagePage(TMessage[] Items, string? NextCursor);

    protected abstract string AnalyticsSource { get; }

    protected abstract string ImageUploadScope { get; }

    protected abstract string VoiceUploadScope { get; }

    protected abstract string ReportTargetType { get; }

    protected abstract string ScopeFor(string threadId);

    protected abstract Task HydrateKeysAsync(CancellationToken token);

    protected abstract Task<ChatKeyStatus> EnsureThreadKeysAsync(string threadId, CancellationToken token);

    protected abstract Task<TThread[]?> FetchThreadListAsync(CancellationToken token);

    protected abstract Task<MessagePage?> FetchMessagesPageAsync(string threadId, string? cursor,
        CancellationToken token);

    protected abstract Task<TMessage?> SendMessageRequestAsync(string threadId, string body, int kind,
        CancellationToken token, string? mediaKey = null, int mediaWidth = 0, int mediaHeight = 0,
        int encVersion = 0, string? commitmentTag = null, string? replyToId = null, int durationSecs = 0);

    protected abstract Task<TMessage?> EditMessageRequestAsync(string messageId, string body, CancellationToken token,
        int encVersion = 0, string? commitmentTag = null);

    protected abstract Task<bool> DeleteMessageRequestAsync(string messageId, CancellationToken token);

    protected abstract Task SetReactionRequestAsync(string messageId, string reactionToken, CancellationToken token);

    protected abstract Task<ReactionListDto?> FetchReactionsAsync(string messageId, CancellationToken token);

    protected abstract Task SendTypingRequestAsync(string threadId, CancellationToken token);

    protected abstract Task<bool?> FetchOtherTypingAsync(string threadId, CancellationToken token);

    protected abstract Task<string?> FetchMediaUrlRequestAsync(string messageId, CancellationToken token);

    protected abstract long MessageTimeOf(TMessage message);

    protected abstract int MessageEncVersionOf(TMessage message);

    protected abstract string MessageBodyOf(TMessage message);

    protected abstract ReactionSummaryDto[]? ReactionsOf(TMessage message);

    protected abstract TMessage WithReactions(TMessage message, ReactionSummaryDto[]? reactions);

    protected abstract TMessage WithBody(TMessage message, string body);

    protected abstract TMessage PreserveLocalFields(TMessage updated, TMessage existing);

    protected abstract TMessage Tombstone(TMessage message);

    protected abstract TMessage ResolveOutgoingReply(string scope, TMessage message);

    protected abstract TMessage[] DecorateMessages(string threadId, TMessage[] items);

    protected abstract TThread[] DecorateThreadList(TThread[] items);

    protected abstract string ThreadKeyOf(TThread thread);

    protected abstract long ThreadLastMessageAtOf(TThread thread);

    protected abstract int ThreadUnreadCountOf(TThread thread);

    protected abstract PhoneNotification BuildInboxNotification(TThread thread);

    protected virtual bool TickActive => session.IsSignedIn;

    protected virtual bool IsThreadMuted(TThread thread) => false;

    protected virtual bool ShouldRevealForReport(TMessage message) => true;

    protected virtual void OnCipherCleared()
    {
    }

    protected virtual void OnThreadOpening(string threadId)
    {
    }

    protected virtual Task PrefetchThreadAsync(string threadId, CancellationToken token) => Task.CompletedTask;

    protected virtual void DisposeCore()
    {
    }

    public bool IsSignedIn => session.IsSignedIn;
    public string MyUserId => session.CurrentUser?.Id ?? string.Empty;
    public TMessage[] Messages => messages;
    public bool LoadingOlder => loadingOlder;
    public bool HasMoreOlder => hasMoreOlder;
    public bool LoadingThread => loadingThread;
    public bool Sending => sending;
    public bool OtherTyping => otherTyping;
    public KeyVaultState VaultState => vault.State;
    public ChatKeyStatus CurrentKeyStatus => currentKeyStatus;
    public bool EncryptingCurrent => cipher.IsUnlocked && currentKeyStatus.CanEncrypt;

    public DmDecryptedBody DecryptionState(string messageId) => cipher.DecryptionState(messageId);

    public string? CurrentThreadId => currentThreadId;

    protected PollCadence InboxCadence => inboxCadence;

    protected TThread[] ThreadListItems
    {
        get => threadList;
        set => threadList = value;
    }

    protected bool LoadingThreadList => loadingThreadList;
    protected bool ThreadListLoaded => threadListLoaded;

    protected TMessage[] MessageItems
    {
        get => messages;
        set => messages = value;
    }

    protected void InvalidateThreadList() => threadListLoaded = false;

    protected void SetKeyStatusIfCurrent(string threadId, ChatKeyStatus status)
    {
        if (currentThreadId == threadId)
        {
            currentKeyStatus = status;
        }
    }

    protected void CloseThreadIfCurrent(string threadId)
    {
        if (currentThreadId == threadId)
        {
            currentThreadId = null;
            messages = Array.Empty<TMessage>();
        }
    }

    protected void TrackMessageSent() => analytics.Track(AnalyticsEvents.DmSent(AnalyticsSource));

    protected int ComputeUnread()
    {
        var snapshot = threadList;
        var total = 0;
        for (var index = 0; index < snapshot.Length; index++)
        {
            if (!IsThreadMuted(snapshot[index]))
            {
                total += ThreadUnreadCountOf(snapshot[index]);
            }
        }

        return total;
    }

    public void NoteThreadViewed(string threadKey)
    {
        viewingThreadKey = threadKey;
        lastViewingUtc = DateTime.UtcNow;
        notifications.RemoveGroup(threadKey);
    }

    private void OnFrameworkTick(IFramework framework)
    {
        if (!TickActive)
        {
            inboxPrimed = false;
            vaultRefreshRequested = false;
            return;
        }

        EnsureVaultRefreshed();
        var now = DateTime.UtcNow;
        EnsureCurrentThreadKeysFresh(now);
        if (!inboxCadence.Due(now))
        {
            return;
        }

        PollInbox();
    }

    private void EnsureVaultRefreshed()
    {
        if (session.CurrentUser is null || vaultRefreshInFlight || vault.State == KeyVaultState.Unsupported)
        {
            return;
        }

        if (vaultRefreshRequested
            && (vault.State == KeyVaultState.Unlocked || DateTime.UtcNow < nextVaultRetryUtc))
        {
            return;
        }

        vaultRefreshRequested = true;
        vaultRefreshInFlight = true;
        work.Run("vault refresh", async token =>
        {
            await vault.RefreshAsync(token).ConfigureAwait(false);
            if (vault.State == KeyVaultState.Unlocked)
            {
                await HydrateKeysAsync(token).ConfigureAwait(false);
            }
        }, () =>
        {
            nextVaultRetryUtc = DateTime.UtcNow + VaultRetryInterval;
            vaultRefreshInFlight = false;
        });
    }

    private void EnsureCurrentThreadKeysFresh(DateTime now)
    {
        var id = currentThreadId;
        if (id is null || keyStatusRefreshing || vault.State != KeyVaultState.Unlocked
            || currentKeyStatus.CanEncrypt || now - lastKeyStatusUtc < KeyStatusRetryInterval)
        {
            return;
        }

        keyStatusRefreshing = true;
        lastKeyStatusUtc = now;
        work.Run("key status refresh", async token =>
        {
            var status = await EnsureThreadKeysAsync(id, token).ConfigureAwait(false);
            if (currentThreadId == id)
            {
                currentKeyStatus = status;
            }
        }, () => keyStatusRefreshing = false);
    }

    private void OnVaultChanged()
    {
        cipher.Clear();
        OnCipherCleared();
        if (vault.State != KeyVaultState.Unlocked)
        {
            currentKeyStatus = ChatKeyStatus.None;
            return;
        }

        work.Run("vault unlocked", async token =>
        {
            await HydrateKeysAsync(token).ConfigureAwait(false);
            var current = currentThreadId;
            if (current is not null)
            {
                var status = await EnsureThreadKeysAsync(current, token).ConfigureAwait(false);
                if (currentThreadId == current)
                {
                    currentKeyStatus = status;
                }
            }

            threadListLoaded = false;
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
            var items = await FetchThreadListAsync(token).ConfigureAwait(false);
            if (items is not null)
            {
                var decorated = DecorateThreadList(items);
                threadList = decorated;
                RaiseInboxNotifications(decorated);
            }
        }, () => inboxPolling = false);
    }

    private void RaiseInboxNotifications(TThread[] items)
    {
        var primed = inboxPrimed;
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            var key = ThreadKeyOf(item);
            var lastMessageAt = ThreadLastMessageAtOf(item);
            var previous = inboxLastAt.GetValueOrDefault(key, 0L);
            inboxLastAt[key] = lastMessageAt;
            if (!primed || lastMessageAt <= previous || ThreadUnreadCountOf(item) <= 0 || IsThreadMuted(item))
            {
                continue;
            }

            if (viewingThreadKey == key && DateTime.UtcNow - lastViewingUtc < ViewingGrace)
            {
                continue;
            }

            notifications.Notify(BuildInboxNotification(item));
        }

        inboxPrimed = true;
    }

    protected void RefreshThreadListCore()
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        loadingThreadList = true;
        work.Run("threads", async token =>
        {
            var items = await FetchThreadListAsync(token).ConfigureAwait(false);
            if (items is not null)
            {
                threadList = DecorateThreadList(items);
            }
        }, () =>
        {
            loadingThreadList = false;
            threadListLoaded = true;
        });
    }

    public void OpenThread(string id)
    {
        if (currentThreadId == id && (messages.Length > 0 || loadingThread))
        {
            return;
        }

        currentThreadId = id;
        OnThreadOpening(id);
        messages = Array.Empty<TMessage>();
        olderCursor = null;
        hasMoreOlder = false;
        loadingOlder = false;
        otherTyping = false;
        loadingThread = true;
        currentKeyStatus = ChatKeyStatus.None;
        lastKeyStatusUtc = DateTime.UtcNow;
        work.Run("thread open", async token =>
        {
            await PrefetchThreadAsync(id, token).ConfigureAwait(false);
            var status = await EnsureThreadKeysAsync(id, token).ConfigureAwait(false);
            if (currentThreadId == id)
            {
                currentKeyStatus = status;
            }

            var page = await FetchMessagesPageAsync(id, null, token).ConfigureAwait(false);
            if (currentThreadId == id && page is not null)
            {
                messages = DecorateMessages(id, page.Value.Items);
                olderCursor = page.Value.NextCursor;
                hasMoreOlder = page.Value.NextCursor is not null;
            }
        }, () =>
        {
            if (currentThreadId == id)
            {
                loadingThread = false;
            }
        });
    }

    public void RefreshThread()
    {
        var current = currentThreadId;
        if (current is null || loadingThread || refreshingThread || DateTime.UtcNow < pollBackoffUntilUtc)
        {
            return;
        }

        refreshingThread = true;
        work.Run("thread refresh", async token =>
        {
            var page = await FetchMessagesPageAsync(current, null, token).ConfigureAwait(false);
            NotePollResult(page is not null);
            if (currentThreadId == current && page is not null)
            {
                var decorated = DecorateMessages(current, page.Value.Items);
                lock (messagesLock)
                {
                    messages = IdentifiedMerge.MergeById(messages, decorated, messageOrder);
                }
            }
        }, () => refreshingThread = false);
    }

    private void NotePollResult(bool succeeded)
    {
        if (succeeded)
        {
            pollFailureStreak = 0;
            pollBackoffUntilUtc = DateTime.MinValue;
            return;
        }

        var streak = Math.Min(pollFailureStreak + 1, 4);
        pollFailureStreak = streak;
        pollBackoffUntilUtc = DateTime.UtcNow.AddSeconds(Math.Pow(2, streak) * 2.5);
    }

    public void LoadOlder()
    {
        var current = currentThreadId;
        if (current is null || loadingThread || loadingOlder || !hasMoreOlder)
        {
            return;
        }

        var cursor = olderCursor;
        if (cursor is null)
        {
            hasMoreOlder = false;
            return;
        }

        loadingOlder = true;
        work.Run("thread older", async token =>
        {
            var page = await FetchMessagesPageAsync(current, cursor, token).ConfigureAwait(false);
            if (currentThreadId == current && page is not null)
            {
                var decorated = DecorateMessages(current, page.Value.Items);
                lock (messagesLock)
                {
                    messages = IdentifiedMerge.MergeById(messages, decorated, messageOrder);
                }

                olderCursor = page.Value.NextCursor;
                hasMoreOlder = page.Value.NextCursor is not null;
            }
        }, () => loadingOlder = false);
    }

    private int CompareByCreatedAt(TMessage left, TMessage right)
    {
        var byTime = MessageTimeOf(left).CompareTo(MessageTimeOf(right));
        return byTime != 0 ? byTime : string.CompareOrdinal(left.Id, right.Id);
    }

    public void SendTyping(string id)
    {
        work.Run("typing", async token => await SendTypingRequestAsync(id, token).ConfigureAwait(false));
    }

    public void RefreshTyping(string id)
    {
        if (refreshingTyping || DateTime.UtcNow < pollBackoffUntilUtc)
        {
            return;
        }

        refreshingTyping = true;
        work.Run("typing state", async token =>
        {
            var result = await FetchOtherTypingAsync(id, token).ConfigureAwait(false);
            NotePollResult(result is not null);
            if (currentThreadId == id && result is not null)
            {
                otherTyping = result.Value;
            }
        }, () => refreshingTyping = false);
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
            TMessage? sent;
            var scope = ScopeFor(id);
            var generation = keys.CurrentGeneration(scope);
            if (EncryptingCurrent && currentThreadId == id
                && cipher.TryEncrypt(scope, generation, trimmed, MyUserId, out var encoded))
            {
                sent = await SendMessageRequestAsync(id, encoded.Envelope, 0, token,
                    encVersion: EnvelopeCodec.VersionEnvelope, commitmentTag: encoded.CommitmentTag,
                    replyToId: replyToId)
                    .ConfigureAwait(false);
                if (sent is not null)
                {
                    cipher.RecordDecrypted(sent.Id, trimmed, encoded.FrankingKeyBase64);
                    sent = WithBody(sent, trimmed);
                }
            }
            else
            {
                sent = await SendMessageRequestAsync(id, trimmed, 0, token, replyToId: replyToId)
                    .ConfigureAwait(false);
            }

            if (sent is null)
            {
                return false;
            }

            sent = ResolveOutgoingReply(scope, sent);
            if (currentThreadId == id)
            {
                messages = CopyOnWrite.Append(messages, sent);
            }

            threadListLoaded = false;
            TrackMessageSent();
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
            var outbound = PrepareMedia(id, baked.Bytes, caption.Trim(), ImageMediaKind);
            var upload = await media.UploadUrlAsync("image/jpeg", ImageUploadScope, token).ConfigureAwait(false);
            if (upload is null)
            {
                AepLog.Warning($"[{logTag}] send image aborted: upload-url denied (scope={ImageUploadScope}, enc={outbound.EncVersion})");
                return false;
            }

            var uploaded = await media.UploadImageAsync(upload.UploadUrl, outbound.UploadBytes, "image/jpeg", token)
                .ConfigureAwait(false);
            if (!uploaded)
            {
                AepLog.Warning($"[{logTag}] send image aborted: R2 upload failed ({outbound.UploadBytes.Length} bytes, enc={outbound.EncVersion})");
                return false;
            }

            var sent = await SendMessageRequestAsync(id, outbound.Body, ImageMediaKind, token, upload.Key,
                baked.Width, baked.Height, encVersion: outbound.EncVersion, commitmentTag: outbound.CommitmentTag)
                .ConfigureAwait(false);
            if (sent is null)
            {
                AepLog.Warning($"[{logTag}] send image aborted: message create rejected (enc={outbound.EncVersion}, hasTag={outbound.CommitmentTag is not null})");
                return false;
            }

            sent = RecordMediaCaption(sent, outbound, caption.Trim());
            if (currentThreadId == id)
            {
                messages = CopyOnWrite.Append(messages, sent);
            }

            threadListLoaded = false;
            TrackMessageSent();
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
            var outbound = PrepareMedia(id, wavBytes, string.Empty, VoiceMediaKind);
            var upload = await media.UploadUrlAsync("audio/wav", VoiceUploadScope, token).ConfigureAwait(false);
            if (upload is null)
            {
                AepLog.Warning($"[{logTag}] send voice aborted: upload-url denied (scope={VoiceUploadScope}, enc={outbound.EncVersion})");
                return false;
            }

            var uploaded = await media.UploadImageAsync(upload.UploadUrl, outbound.UploadBytes, "audio/wav", token)
                .ConfigureAwait(false);
            if (!uploaded)
            {
                AepLog.Warning($"[{logTag}] send voice aborted: R2 upload failed ({outbound.UploadBytes.Length} bytes, enc={outbound.EncVersion})");
                return false;
            }

            var sent = await SendMessageRequestAsync(id, outbound.Body, VoiceMediaKind, token, upload.Key,
                encVersion: outbound.EncVersion, commitmentTag: outbound.CommitmentTag, durationSecs: durationSecs)
                .ConfigureAwait(false);
            if (sent is null)
            {
                AepLog.Warning($"[{logTag}] send voice aborted: message create rejected (enc={outbound.EncVersion}, hasTag={outbound.CommitmentTag is not null})");
                return false;
            }

            sent = RecordMediaCaption(sent, outbound, string.Empty);
            if (currentThreadId == id)
            {
                messages = CopyOnWrite.Append(messages, sent);
            }

            threadListLoaded = false;
            TrackMessageSent();
            return true;
        }, onComplete, () => sending = false);
    }

    private OutboundMedia PrepareMedia(string id, byte[] plaintextBytes, string caption, int mediaKind)
    {
        var scope = ScopeFor(id);
        return cipher.PrepareOutboundMedia(scope, keys.CurrentGeneration(scope), MyUserId, plaintextBytes, caption,
            mediaKind, EncryptingCurrent && currentThreadId == id);
    }

    private TMessage RecordMediaCaption(TMessage sent, OutboundMedia outbound, string caption)
    {
        if (outbound.EncVersion != EnvelopeCodec.VersionEnvelope || outbound.FrankingKey is null)
        {
            return sent;
        }

        cipher.RecordDecrypted(sent.Id, caption, outbound.FrankingKey);
        cipher.RecordGeneration(sent.Id, outbound.Generation);
        return WithBody(sent, caption);
    }

    public TMessage? FindMessage(string messageId)
    {
        var snapshot = messages;
        for (var index = 0; index < snapshot.Length; index++)
        {
            if (snapshot[index].Id == messageId)
            {
                return snapshot[index];
            }
        }

        return null;
    }

    public void SetReaction(string messageId, string reactionToken)
    {
        messages = ApplyLocalReaction(messages, messageId, reactionToken);
        work.Run("react", async token =>
            await SetReactionRequestAsync(messageId, reactionToken, token).ConfigureAwait(false));
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

            var reactions = ReactionsOf(snapshot[index]);
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

    private TMessage[] ApplyLocalReaction(TMessage[] items, string messageId, string reactionToken)
    {
        for (var index = 0; index < items.Length; index++)
        {
            if (items[index].Id != messageId)
            {
                continue;
            }

            var current = ReactionsOf(items[index]) ?? Array.Empty<ReactionSummaryDto>();
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

            var updated = (TMessage[])items.Clone();
            updated[index] = WithReactions(items[index], next.Count > 0 ? next.ToArray() : null);
            return updated;
        }

        return items;
    }

    public void LoadReactions(string messageId, Action<ReactorDto[]?> onResult)
    {
        work.Run("reaction list", async token =>
        {
            var result = await FetchReactionsAsync(messageId, token).ConfigureAwait(false);
            onResult(result?.Items);
        });
    }

    public void EditMessage(string id, string messageId, string body, Action<bool> onComplete)
    {
        var trimmed = body.Trim();
        if (trimmed.Length == 0)
        {
            return;
        }

        work.Run("edit message", async token =>
        {
            TMessage? edited;
            var scope = ScopeFor(id);
            var generation = keys.CurrentGeneration(scope);
            if (EncryptingCurrent && currentThreadId == id
                && cipher.TryEncrypt(scope, generation, trimmed, MyUserId, out var encoded))
            {
                edited = await EditMessageRequestAsync(messageId, encoded.Envelope, token,
                    EnvelopeCodec.VersionEnvelope, encoded.CommitmentTag).ConfigureAwait(false);
                if (edited is not null)
                {
                    cipher.RecordDecrypted(edited.Id, trimmed, encoded.FrankingKeyBase64);
                    edited = WithBody(edited, trimmed);
                }
            }
            else
            {
                edited = await EditMessageRequestAsync(messageId, trimmed, token).ConfigureAwait(false);
                if (edited is not null)
                {
                    cipher.Forget(messageId);
                }
            }

            if (edited is null)
            {
                return false;
            }

            edited = ResolveOutgoingReply(scope, edited);
            if (currentThreadId == id)
            {
                messages = ReplaceMessage(messages, edited);
            }

            threadListLoaded = false;
            return true;
        }, onComplete);
    }

    private TMessage[] ReplaceMessage(TMessage[] items, TMessage updated)
    {
        for (var index = 0; index < items.Length; index++)
        {
            if (items[index].Id != updated.Id)
            {
                continue;
            }

            var next = (TMessage[])items.Clone();
            next[index] = PreserveLocalFields(updated, items[index]);
            return next;
        }

        return items;
    }

    public void DeleteMessage(string messageId, Action<bool> onComplete)
    {
        work.Run("delete message", async token =>
        {
            var ok = await DeleteMessageRequestAsync(messageId, token).ConfigureAwait(false);
            if (!ok)
            {
                return false;
            }

            messages = TombstoneLocal(messages, messageId);
            threadListLoaded = false;
            return true;
        }, onComplete);
    }

    private TMessage[] TombstoneLocal(TMessage[] items, string messageId)
    {
        for (var index = 0; index < items.Length; index++)
        {
            if (items[index].Id != messageId)
            {
                continue;
            }

            var updated = (TMessage[])items.Clone();
            updated[index] = Tombstone(items[index]);
            return updated;
        }

        return items;
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
            var result = await FetchMediaUrlRequestAsync(messageId, token).ConfigureAwait(false);
            if (result is not null)
            {
                dmMediaUrls[messageId] = result;
            }
        }, () => dmMediaLoading.TryRemove(messageId, out _));
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
            await safety.ReportAsync(ReportTargetType, messageId, reason, token, revealed).ConfigureAwait(false),
            onComplete);
    }

    private void AppendReveal(List<RevealedMessageDto> reveals, TMessage message)
    {
        if (!ShouldRevealForReport(message))
        {
            return;
        }

        if (MessageEncVersionOf(message) == 0)
        {
            reveals.Add(new RevealedMessageDto(message.Id, MessageBodyOf(message), null));
            return;
        }

        var state = DecryptionState(message.Id);
        if (state.State == DmBodyState.Decrypted)
        {
            reveals.Add(new RevealedMessageDto(message.Id, state.Text, state.FrankingKey));
        }
    }

    public void Dispose()
    {
        DisposeCore();
        vault.Changed -= OnVaultChanged;
        Plugin.Framework.Update -= OnFrameworkTick;
        work.Dispose();
    }
}
