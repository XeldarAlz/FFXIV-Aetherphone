using System.Collections.Concurrent;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Home;
using Aetherphone.Core.Media;
using Aetherphone.Core.Net;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Report;
using Aetherphone.Core.Runtime;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Message;

internal abstract class ChatThreadStoreBase<TMessage, TThread> : IDisposable
    where TMessage : class, IIdentified
    where TThread : class, IIdentified
{
    protected const int DmImageMaxDimension = 1280;
    protected const int ImageMediaKind = 1;
    protected const int VoiceMediaKind = 3;
    protected const int PostShareKind = 4;
    protected const int StoryReplyKind = 5;
    private const string ReportEvidenceUploadScope = "report-evidence";
    private static readonly TimeSpan ForegroundInboxPollInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan BackgroundInboxPollInterval = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan ViewingGrace = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan VaultRetryInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan KeyStatusRetryInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan ThreadReopenCooldown = TimeSpan.FromSeconds(3);

    protected readonly AethernetSession session;
    protected readonly SafetyClient safety;
    protected readonly MediaClient media;
    protected readonly KeyVault vault;
    protected readonly ConversationKeyStore keys;
    protected readonly StoreWork work;
    protected readonly MessageCipher cipher;
    private readonly string logTag;
    private readonly NotificationService notifications;
    private readonly AppGate gate;
    private readonly PollCadence inboxCadence;
    private readonly object messagesLock = new();
    private readonly ConcurrentDictionary<string, InboxMark> inboxMarks = new();
    private static readonly TimeSpan MediaUrlFailureRetryFor = TimeSpan.FromMinutes(2);
    private readonly ConcurrentDictionary<string, string> dmMediaUrls = new();
    private readonly ConcurrentDictionary<string, byte> dmMediaLoading = new();
    private readonly ConcurrentDictionary<string, DateTime> dmMediaFailed = new();
    private readonly Comparison<TMessage> messageOrder;

    private volatile TThread[] threadList = Array.Empty<TThread>();
    private volatile string? threadListCursor;
    private volatile bool loadingMoreThreads;
    private volatile bool loadingThreadList;
    private volatile bool threadListLoaded;
    private volatile AepFailureBox? threadListFailureBox;
    private volatile string? currentThreadId;
    private volatile TMessage[] messages = Array.Empty<TMessage>();
    private volatile string? olderCursor;
    private volatile bool loadingOlder;
    private volatile bool hasMoreOlder;
    private volatile bool loadingThread;
    private volatile bool refreshingThread;
    private volatile bool refreshingTyping;
    private volatile string? pendingOpenThreadId;
    private volatile string? lastOpenedThreadId;
    private DateTime lastThreadOpenUtc = DateTime.MinValue;
    private int pollFailureStreak;
    private DateTime pollBackoffUntilUtc = DateTime.MinValue;
    private volatile bool sending;
    private volatile bool otherTyping;

    private volatile bool inboxPolling;
    private volatile bool threadRefreshPending;
    private bool inboxPrimed;
    private volatile string? viewingThreadKey;
    private DateTime lastViewingUtc = DateTime.MinValue;
    private volatile bool vaultRefreshRequested;
    private volatile bool vaultRefreshInFlight;
    private DateTime nextVaultRetryUtc = DateTime.MinValue;
    private volatile bool keyStatusRefreshing;
    private volatile bool keyStatusRefreshForced;
    private DateTime lastKeyStatusUtc = DateTime.MinValue;
    private volatile ChatKeyStatus currentKeyStatus = ChatKeyStatus.None;
    private string? lastAccountId;

    protected ChatThreadStoreBase(string logTag, AethernetSession session, SafetyClient safety, MediaClient media,
        NotificationService notifications, KeyVault vault, ConversationKeyStore keys, DecryptedHistoryStore chatHistory,
        PhoneVisibility visibility,
        AppGate gate)
    {
        this.session = session;
        this.safety = safety;
        this.media = media;
        this.notifications = notifications;
        this.vault = vault;
        this.keys = keys;
        this.logTag = logTag;
        this.gate = gate;
        work = new StoreWork(logTag);
        cipher = new MessageCipher(vault, keys, chatHistory);
        messageOrder = CompareByCreatedAt;
        inboxCadence = new PollCadence(visibility, ForegroundInboxPollInterval, BackgroundInboxPollInterval);
        vault.Changed += OnVaultChanged;
        session.Changed += OnSessionAccountChanged;
        Plugin.Framework.Update += OnFrameworkTick;
    }

    private void OnSessionAccountChanged()
    {
        var accountId = session.CurrentUser?.Id;
        if (accountId is null || string.Equals(accountId, lastAccountId, StringComparison.Ordinal))
        {
            return;
        }

        lastAccountId = accountId;
        inboxMarks.Clear();
        threadList = Array.Empty<TThread>();
        threadListCursor = null;
        threadListLoaded = false;
        currentThreadId = null;
        pendingOpenThreadId = null;
        messages = Array.Empty<TMessage>();
        olderCursor = null;
        hasMoreOlder = false;
        otherTyping = false;
        inboxPrimed = false;
        threadRefreshPending = false;
        currentKeyStatus = ChatKeyStatus.None;
        dmMediaUrls.Clear();
        dmMediaFailed.Clear();
        cipher.Clear();
        OnCipherCleared();
        vaultRefreshRequested = false;
        OnAccountSwitched();
    }

    protected readonly record struct MessagePage(TMessage[] Items, string? NextCursor);

    protected readonly record struct ThreadListPage(TThread[] Items, string? NextCursor);

    protected abstract string ImageUploadScope { get; }

    protected abstract string VoiceUploadScope { get; }

    protected abstract string ReportTargetType { get; }

    protected abstract string ScopeFor(string threadId);

    protected abstract Task HydrateKeysAsync(CancellationToken token);

    protected abstract Task<ChatKeyStatus> EnsureThreadKeysAsync(string threadId, CancellationToken token);

    protected abstract Task<ThreadListPage?> FetchThreadListAsync(string? cursor, CancellationToken token,
        Action<AepFailure>? onFailure = null);

    protected abstract Task<MessagePage?> FetchMessagesPageAsync(string threadId, string? cursor,
        CancellationToken token);

    protected abstract Task<TMessage?> SendMessageRequestAsync(string threadId, string body, int kind,
        CancellationToken token, string? mediaKey = null, int mediaWidth = 0, int mediaHeight = 0,
        int encVersion = 0, string? commitmentTag = null, string? replyToId = null, int durationSecs = 0);

    protected abstract Task<TMessage?> EditMessageRequestAsync(string messageId, string body, CancellationToken token,
        int encVersion = 0, string? commitmentTag = null);

    protected abstract Task<bool> DeleteMessageRequestAsync(string messageId, CancellationToken token);

    protected abstract Task<bool> DeleteThreadRequestAsync(string threadId, CancellationToken token);

    protected abstract Task SetReactionRequestAsync(string messageId, string reactionToken, CancellationToken token);

    protected abstract Task<ReactionListDto?> FetchReactionsAsync(string messageId, CancellationToken token);

    protected abstract Task SendTypingRequestAsync(string threadId, CancellationToken token);

    protected abstract Task<bool?> FetchOtherTypingAsync(string threadId, CancellationToken token);

    protected abstract Task<string?> FetchMediaUrlRequestAsync(string messageId, CancellationToken token);

    protected abstract long MessageTimeOf(TMessage message);

    protected abstract int MessageEncVersionOf(TMessage message);

    protected abstract string MessageBodyOf(TMessage message);

    protected abstract int MessageKindOf(TMessage message);

    protected abstract string MessageSenderIdOf(TMessage message);

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

    protected virtual bool TickActive => session.IsSignedIn && gate.Open;

    public virtual bool RealtimePushActive => false;

    protected virtual bool IsThreadMuted(TThread thread) => false;

    protected virtual bool ShouldRevealForReport(TMessage message) => true;

    protected virtual void OnCipherCleared()
    {
    }

    protected virtual void OnAccountSwitched()
    {
    }

    protected virtual void OnThreadOpening(string threadId)
    {
    }

    protected virtual Task PrefetchThreadAsync(string threadId, CancellationToken token) => Task.CompletedTask;

    protected virtual void DisposeCore()
    {
    }

    public bool ThreadOpenPending => pendingOpenThreadId is not null;

    public TimeSpan SyncRetryIn
    {
        get
        {
            var remaining = pollBackoffUntilUtc - DateTime.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }

    public void RetrySyncNow()
    {
        pollFailureStreak = 0;
        pollBackoffUntilUtc = DateTime.MinValue;
        threadRefreshPending = true;
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
    public KeyVault Vault => vault;
    public ChatKeyStatus CurrentKeyStatus => currentKeyStatus;
    public bool EncryptingCurrent => cipher.IsUnlocked && currentKeyStatus.CanEncrypt;

    public bool SendWouldDowngrade => !EncryptingCurrent && IsEncryptedThread(currentKeyStatus);

    private bool RefuseDowngrade(string threadId, string what)
    {
        var status = currentThreadId == threadId ? currentKeyStatus : ChatKeyStatus.None;
        return DowngradeBlocked(threadId, what, EncryptingCurrent && currentThreadId == threadId, status);
    }

    protected bool DowngradeBlocked(string threadId, string what, bool encrypted, ChatKeyStatus status)
    {
        if (encrypted || !IsEncryptedThread(status))
        {
            return false;
        }

        AepLog.Warning(
            $"[{logTag}] {what} held back in {threadId}: the thread is encrypted at generation {status.CurrentGeneration} but this device cannot encrypt for it, and sending in the clear would be a silent downgrade.");
        return true;
    }

    private static bool IsEncryptedThread(ChatKeyStatus status)
    {
        return status.CurrentGeneration > 0 && status.MembersWithoutKeys.Length == 0;
    }

    public DmDecryptedBody DecryptionState(string messageId) => cipher.DecryptionState(messageId);

    public bool HasOlderKeyMessages(string scope) => cipher.HasOlderKeyMessages(scope);

    public string? CurrentThreadId => currentThreadId;

    protected PollCadence InboxCadence => inboxCadence;

    protected TThread[] ThreadListItems
    {
        get => threadList;
        set => threadList = value;
    }

    protected bool LoadingThreadList => loadingThreadList;
    protected bool ThreadListLoaded => threadListLoaded;
    public bool ThreadListFailed => threadListFailureBox is not null;
    public AepFailure ThreadListFailure => threadListFailureBox?.Failure ?? AepFailure.None;
    public bool LoadingMoreThreads => loadingMoreThreads;
    public bool HasMoreThreads => threadListCursor is not null;

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

    public void DeleteThread(string threadId, Action? onDone = null)
    {
        var snapshot = threadList;
        for (var index = 0; index < snapshot.Length; index++)
        {
            if (ThreadKeyOf(snapshot[index]) != threadId)
            {
                continue;
            }

            var updated = new TThread[snapshot.Length - 1];
            Array.Copy(snapshot, 0, updated, 0, index);
            Array.Copy(snapshot, index + 1, updated, index, snapshot.Length - index - 1);
            threadList = updated;
            break;
        }

        CloseThreadIfCurrent(threadId);
        work.Run("thread delete", async token =>
            await DeleteThreadRequestAsync(threadId, token).ConfigureAwait(false), succeeded =>
        {
            RefreshThreadListCore();
            if (succeeded)
            {
                onDone?.Invoke();
            }
        });
    }

    protected int ComputeUnread()
    {
        var snapshot = threadList;
        var total = 0;
        for (var index = 0; index < snapshot.Length; index++)
        {
            if (IsThreadMuted(snapshot[index]) || IsBeingViewed(ThreadKeyOf(snapshot[index])))
            {
                continue;
            }

            total += ThreadUnreadCountOf(snapshot[index]);
        }

        return total;
    }

    protected bool IsBeingViewed(string threadKey) =>
        string.Equals(viewingThreadKey, threadKey, StringComparison.Ordinal)
        && DateTime.UtcNow - lastViewingUtc < ViewingGrace;

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
            vaultRefreshRequested = false;
            pendingOpenThreadId = null;
            if (threadList.Length > 0 || messages.Length > 0 || currentThreadId is not null)
            {
                threadList = Array.Empty<TThread>();
                threadListCursor = null;
                messages = Array.Empty<TMessage>();
                currentThreadId = null;
                threadListLoaded = false;
            }

            return;
        }

        EnsureVaultRefreshed();
        var now = DateTime.UtcNow;
        EnsureCurrentThreadKeysFresh(now);
        ResumePendingThreadOpen(now);
        ConsumePendingThreadRefresh(now);

        if (inboxPolling || !inboxCadence.Due(now))
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
            || (currentKeyStatus.CanEncrypt && !keyStatusRefreshForced)
            || now - lastKeyStatusUtc < KeyStatusRetryInterval)
        {
            return;
        }

        keyStatusRefreshing = true;
        keyStatusRefreshForced = false;
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
            var page = await FetchThreadListAsync(null, token).ConfigureAwait(false);
            if (page is not null)
            {
                var decorated = DecorateThreadList(page.Value.Items);
                AcceptThreadListHead(decorated, page.Value.NextCursor);
                RaiseInboxNotifications(decorated);
            }
        }, () => inboxPolling = false);
    }

    private void AcceptThreadListHead(TThread[] decorated, string? nextCursor)
    {
        var current = threadList;
        if (current.Length == 0)
        {
            threadList = decorated;
            threadListCursor = nextCursor;
            return;
        }

        threadList = IdentifiedMerge.MergeById(current, decorated, CompareThreadsByRecency);
    }

    private int CompareThreadsByRecency(TThread left, TThread right)
    {
        var byTime = ThreadLastMessageAtOf(right).CompareTo(ThreadLastMessageAtOf(left));
        return byTime != 0 ? byTime : string.CompareOrdinal(right.Id, left.Id);
    }

    public void LoadMoreThreads()
    {
        var cursor = threadListCursor;
        if (!session.IsSignedIn || cursor is null || loadingMoreThreads || loadingThreadList)
        {
            return;
        }

        loadingMoreThreads = true;
        work.Run("threads more", async token =>
        {
            var page = await FetchThreadListAsync(cursor, token).ConfigureAwait(false);
            if (page is null)
            {
                return;
            }

            var decorated = DecorateThreadList(page.Value.Items);
            threadList = IdentifiedMerge.MergeById(threadList, decorated, CompareThreadsByRecency);
            threadListCursor = page.Value.NextCursor;
        }, () => loadingMoreThreads = false);
    }

    private readonly record struct InboxMark(long LastMessageAt, int Unread);

    private static readonly TimeSpan InboxNotifyDeferralLimit = TimeSpan.FromSeconds(30);
    private readonly ConcurrentDictionary<string, DateTime> inboxNotifyDeferrals = new(StringComparer.Ordinal);

    protected virtual bool IsInboxPreviewReady(TThread item)
    {
        return true;
    }

    private void RaiseInboxNotifications(TThread[] items)
    {
        var primed = inboxPrimed;
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            var key = ThreadKeyOf(item);
            var lastMessageAt = ThreadLastMessageAtOf(item);
            var unread = ThreadUnreadCountOf(item);
            var previous = inboxMarks.GetValueOrDefault(key);

            if (!primed || IsThreadMuted(item))
            {
                inboxMarks[key] = new InboxMark(lastMessageAt, unread);
                continue;
            }

            var isNew = lastMessageAt > previous.LastMessageAt
                || (lastMessageAt == previous.LastMessageAt && unread > previous.Unread);
            if (!isNew || unread <= 0)
            {
                inboxNotifyDeferrals.TryRemove(key, out _);
                continue;
            }

            if (!IsInboxPreviewReady(item))
            {
                if (!inboxNotifyDeferrals.TryGetValue(key, out var deferredSince))
                {
                    deferredSince = DateTime.UtcNow;
                    inboxNotifyDeferrals[key] = deferredSince;
                }

                if (DateTime.UtcNow - deferredSince < InboxNotifyDeferralLimit)
                {
                    continue;
                }
            }

            inboxNotifyDeferrals.TryRemove(key, out _);
            inboxMarks[key] = new InboxMark(lastMessageAt, unread);
            if (IsBeingViewed(key))
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
            var reported = AepFailure.None;
            var page = await FetchThreadListAsync(null, token, failure => reported = failure).ConfigureAwait(false);
            if (page is null)
            {
                threadListFailureBox = new AepFailureBox(reported.Failed
                    ? reported
                    : AepFailure.Transport(AepFailureKind.Offline));
                AepLog.Warning($"Conversation list failed to load: {threadListFailureBox.Failure.Describe()}");
                return;
            }

            threadListFailureBox = null;
            AcceptThreadListHead(DecorateThreadList(page.Value.Items), page.Value.NextCursor);
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

        if (IsRedundantReopen(id))
        {
            currentThreadId = id;
            return;
        }

        currentThreadId = id;
        OnThreadOpening(id);
        threadRefreshPending = false;
        messages = Array.Empty<TMessage>();
        olderCursor = null;
        hasMoreOlder = false;
        loadingOlder = false;
        otherTyping = false;
        currentKeyStatus = ChatKeyStatus.None;
        lastKeyStatusUtc = DateTime.UtcNow;
        BeginThreadOpen(id);
    }

    private bool IsRedundantReopen(string id)
    {
        return currentThreadId is null
            && id == lastOpenedThreadId
            && DateTime.UtcNow - lastThreadOpenUtc < ThreadReopenCooldown;
    }

    private void BeginThreadOpen(string id)
    {
        if (DateTime.UtcNow < pollBackoffUntilUtc)
        {
            pendingOpenThreadId = id;
            return;
        }

        pendingOpenThreadId = null;
        lastOpenedThreadId = id;
        lastThreadOpenUtc = DateTime.UtcNow;
        loadingThread = true;
        work.Run("thread open", async token =>
        {
            var detail = PrefetchThreadAsync(id, token);
            var threadKeys = EnsureThreadKeysAsync(id, token);
            await Task.WhenAll(detail, threadKeys).ConfigureAwait(false);
            var status = await threadKeys.ConfigureAwait(false);
            if (currentThreadId == id)
            {
                currentKeyStatus = status;
            }

            var page = await FetchMessagesPageAsync(id, null, token).ConfigureAwait(false);
            NotePollResult(page is not null);
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

    private void ResumePendingThreadOpen(DateTime now)
    {
        if (pendingOpenThreadId is not { } pending || loadingThread || now < pollBackoffUntilUtc)
        {
            return;
        }

        if (currentThreadId != pending)
        {
            pendingOpenThreadId = null;
            return;
        }

        BeginThreadOpen(pending);
    }

    public void RequestThreadKeyRefresh()
    {
        keyStatusRefreshForced = true;
    }

    public void RequestThreadRefresh(string? threadId = null)
    {
        if (threadId is not null && currentThreadId is { } open && threadId != open)
        {
            return;
        }

        threadRefreshPending = true;
        ConsumePendingThreadRefresh(DateTime.UtcNow);
    }

    private void ConsumePendingThreadRefresh(DateTime now)
    {
        if (!threadRefreshPending)
        {
            return;
        }

        var current = currentThreadId;
        if (current is null || !IsBeingViewed(current) || loadingThread || refreshingThread
            || now < pollBackoffUntilUtc)
        {
            return;
        }

        threadRefreshPending = false;
        RefreshThread();
    }

    protected void MergePushedMessage(string threadId, TMessage message)
    {
        if (currentThreadId != threadId)
        {
            return;
        }

        if (loadingThread)
        {
            threadRefreshPending = true;
            return;
        }

        var decorated = DecorateMessages(threadId, new[] { message });
        lock (messagesLock)
        {
            messages = IdentifiedMerge.MergeById(messages, decorated, messageOrder);
        }
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
        if (trimmed.Length == 0 || sending || RefuseDowngrade(id, "send"))
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
            return true;
        }, onComplete, () => sending = false);
    }

    public void SendImageMessage(string id, string sourcePath, string caption, Action<bool> onComplete)
    {
        if (sending || RefuseDowngrade(id, "send image"))
        {
            return;
        }

        sending = true;
        work.Run("send image", async token =>
        {
            byte[] plainBytes;
            int width;
            int height;
            string contentType;
            if (GifMedia.IsGif(sourcePath))
            {
                plainBytes = await File.ReadAllBytesAsync(sourcePath, token).ConfigureAwait(false);
                if (plainBytes.Length == 0 || plainBytes.Length > GifMedia.MaxBytes)
                {
                    AepLog.Warning($"[{logTag}] send image aborted: GIF of {plainBytes.Length} bytes exceeds the {GifMedia.MaxBytes} cap");
                    return false;
                }

                (width, height) = ImageProcessor.IdentifyDimensions(plainBytes);
                contentType = "image/gif";
            }
            else
            {
                var baked = ImageProcessor.BakeJpeg(sourcePath, DmImageMaxDimension);
                plainBytes = baked.Bytes;
                width = baked.Width;
                height = baked.Height;
                contentType = "image/jpeg";
            }

            var outbound = PrepareMedia(id, plainBytes, caption.Trim(), ImageMediaKind);
            var upload = await media.UploadUrlAsync(contentType, ImageUploadScope, token).ConfigureAwait(false);
            if (upload is null)
            {
                AepLog.Warning($"[{logTag}] send image aborted: upload-url denied (scope={ImageUploadScope}, enc={outbound.EncVersion})");
                return false;
            }

            var uploaded = await media.UploadImageAsync(upload.UploadUrl, outbound.UploadBytes, contentType, token)
                .ConfigureAwait(false);
            if (!uploaded)
            {
                AepLog.Warning($"[{logTag}] send image aborted: R2 upload failed ({outbound.UploadBytes.Length} bytes, enc={outbound.EncVersion})");
                return false;
            }

            var sent = await SendMessageRequestAsync(id, outbound.Body, ImageMediaKind, token, upload.Key,
                width, height, encVersion: outbound.EncVersion, commitmentTag: outbound.CommitmentTag)
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
            return true;
        }, onComplete, () => sending = false);
    }

    public void SendVoiceMessage(string id, byte[] wavBytes, int durationSecs, Action<bool> onComplete)
    {
        if (sending || RefuseDowngrade(id, "send voice"))
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
        if (trimmed.Length == 0 || RefuseDowngrade(id, "edit"))
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

        if (dmMediaFailed.TryGetValue(messageId, out var failedAtUtc))
        {
            if (DateTime.UtcNow - failedAtUtc < MediaUrlFailureRetryFor)
            {
                return null;
            }

            dmMediaFailed.TryRemove(messageId, out _);
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
            else
            {
                dmMediaFailed[messageId] = DateTime.UtcNow;
            }
        }, () => dmMediaLoading.TryRemove(messageId, out _));
        return null;
    }

    public void ReportMessage(string messageId, string? reason, Action<bool> onComplete)
    {
        var snapshot = messages;
        var threadId = currentThreadId;
        if (!ReportReveals.TryCollect(snapshot, messageId, RevealForReport, out var revealed))
        {
            onComplete(false);
            return;
        }

        work.Run("report message", async token =>
        {
            var evidence = await AttachMediaEvidenceAsync(threadId, snapshot, revealed, token).ConfigureAwait(false);
            return await safety.ReportAsync(ReportTargetType, messageId, reason, token, evidence).ConfigureAwait(false);
        }, onComplete);
    }

    private async Task<RevealedMessageDto[]?> AttachMediaEvidenceAsync(string? threadId, TMessage[] snapshot,
        RevealedMessageDto[]? revealed, CancellationToken token)
    {
        if (revealed is null || threadId is null)
        {
            return revealed;
        }

        var updated = revealed;
        for (var index = 0; index < revealed.Length; index++)
        {
            var uploaded = await UploadMediaEvidenceAsync(threadId, snapshot, revealed[index].MessageId, token)
                .ConfigureAwait(false);
            if (uploaded is not { } evidence)
            {
                continue;
            }

            if (ReferenceEquals(updated, revealed))
            {
                updated = (RevealedMessageDto[])revealed.Clone();
            }

            updated[index] = revealed[index] with
            {
                MediaKey = evidence.Key,
                MediaContentType = evidence.ContentType,
            };
        }

        return updated;
    }

    private async Task<(string Key, string ContentType)?> UploadMediaEvidenceAsync(string threadId,
        TMessage[] snapshot, string messageId, CancellationToken token)
    {
        TMessage? message = null;
        for (var index = 0; index < snapshot.Length; index++)
        {
            if (snapshot[index].Id == messageId)
            {
                message = snapshot[index];
                break;
            }
        }

        if (message is null || MessageEncVersionOf(message) != EnvelopeCodec.VersionEnvelope)
        {
            return null;
        }

        var kind = MessageKindOf(message);
        if (kind != ImageMediaKind && kind != VoiceMediaKind)
        {
            return null;
        }

        var url = await FetchMediaUrlRequestAsync(messageId, token).ConfigureAwait(false);
        if (url is null || !cipher.TryGetGeneration(messageId, out var generation))
        {
            return null;
        }

        var sealedBytes = await media.DownloadAsync(new Uri(url), token).ConfigureAwait(false);
        if (sealedBytes is null)
        {
            return null;
        }

        var plain = cipher.TryDecryptMedia(ScopeFor(threadId), generation, sealedBytes, MessageSenderIdOf(message),
            kind);
        if (plain is null)
        {
            AepLog.Warning($"[{logTag}] report evidence skipped: media decrypt failed ({messageId})");
            return null;
        }

        var contentType = kind == VoiceMediaKind ? "audio/wav" : "image/jpeg";
        var upload = await media.UploadUrlAsync(contentType, ReportEvidenceUploadScope, token).ConfigureAwait(false);
        if (upload is null
            || !await media.UploadImageAsync(upload.UploadUrl, plain, contentType, token).ConfigureAwait(false))
        {
            AepLog.Warning($"[{logTag}] report evidence skipped: upload failed ({messageId})");
            return null;
        }

        return (upload.Key, contentType);
    }

    private RevealedMessageDto? RevealForReport(TMessage message)
    {
        if (!ShouldRevealForReport(message))
        {
            return null;
        }

        if (MessageEncVersionOf(message) == EnvelopeCodec.VersionPlaintext)
        {
            return new RevealedMessageDto(message.Id, MessageBodyOf(message), null);
        }

        var state = DecryptionState(message.Id);
        return state.State == DmBodyState.Decrypted
            ? new RevealedMessageDto(message.Id, state.Text, state.FrankingKey)
            : null;
    }

    public void Dispose()
    {
        DisposeCore();
        vault.Changed -= OnVaultChanged;
        session.Changed -= OnSessionAccountChanged;
        Plugin.Framework.Update -= OnFrameworkTick;
        work.Dispose();
    }
}
