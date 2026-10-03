using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Feedback;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Net;
using Aetherphone.Core.Notifications;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Feedback;

internal enum FeedbackHistoryState : byte
{
    Unknown,
    Loading,
    Ready,
    Unsupported,
}

internal readonly record struct FeedbackSendResult(bool Succeeded, AepFailure Failure);

internal sealed record FeedbackFetch(string UserId, MyFeedbackDto[] Items, bool Complete);

internal sealed class FeedbackStore : IDisposable
{
    public const string AppId = "feedback";

    private const int MaxImageDimension = 1600;
    private const long ViewingLeaseMilliseconds = 1500;

    private static readonly TimeSpan SessionCheckDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ReconnectSpread = TimeSpan.FromSeconds(20);

    private readonly AethernetSession session;
    private readonly FeedbackClient client;
    private readonly MediaClient media;
    private readonly NotificationService notifications;
    private readonly Configuration configuration;
    private readonly RealtimeSignalBus signals;
    private readonly StoreWork work = new StoreWork("Feedback");
    private readonly List<FeedbackUpdate> raised = new();

    private volatile bool posting;
    private volatile int uploadedImages;
    private volatile int totalImages;
    private volatile MyFeedbackDto[] history = Array.Empty<MyFeedbackDto>();
    private volatile string? historyCursor;
    private volatile bool loadingMore;
    private volatile bool refreshing;
    private volatile bool everLoaded;
    private volatile FeedbackHistoryState historyState;
    private int historyRevision;
    private string? historyOwnerId;
    private volatile FeedbackFetch? pendingFetch;
    private volatile bool checkRequested;
    private long checkDueTicks = long.MaxValue;
    private string? sessionCheckedUserId;
    private string? viewingId;
    private long viewingUntilMilliseconds;

    public FeedbackStore(AethernetSession session, FeedbackClient client, MediaClient media,
        NotificationService notifications, Configuration configuration, RealtimeSignalBus signals)
    {
        this.session = session;
        this.client = client;
        this.media = media;
        this.notifications = notifications;
        this.configuration = configuration;
        this.signals = signals;
        signals.FeedbackPinged += OnPinged;
        signals.ConnectedChanged += OnRealtimeConnected;
        Plugin.Framework.Update += OnFrameworkUpdate;
    }

    public bool IsSignedIn => session.IsSignedIn;

    public bool Posting => posting;

    public int UploadedImages => uploadedImages;

    public int TotalImages => totalImages;

    public MyFeedbackDto[] History => history;

    public int HistoryRevision => Volatile.Read(ref historyRevision);

    public FeedbackHistoryState HistoryState => historyState;

    public bool HistoryVisible =>
        historyState == FeedbackHistoryState.Ready || (historyState == FeedbackHistoryState.Loading && everLoaded);

    public bool HasMoreHistory => historyCursor is not null;

    public bool LoadingMoreHistory => loadingMore;

    public bool RefreshingHistory => refreshing;

    public int UnseenCount => CurrentMarks()?.Unseen.Count ?? 0;

    public bool IsUnseen(string feedbackId) => CurrentMarks()?.Unseen.ContainsKey(feedbackId) ?? false;

    public void NoteViewing(string feedbackId)
    {
        viewingId = feedbackId;
        viewingUntilMilliseconds = Environment.TickCount64 + ViewingLeaseMilliseconds;
    }

    public void MarkViewed(string feedbackId)
    {
        var marks = CurrentMarks();
        if (marks is null || !FeedbackUpdates.MarkViewed(marks, feedbackId))
        {
            return;
        }

        configuration.Save();
        notifications.RemoveGroup(FeedbackLauncher.GroupKey(feedbackId));
    }

    public void NoteSent()
    {
        var userId = session.CurrentUser?.Id;
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        var marks = MarksFor(userId);
        if (marks.HasHistory)
        {
            return;
        }

        marks.HasHistory = true;
        configuration.Save();
    }

    public void ResetHistorySupport()
    {
        if (historyState == FeedbackHistoryState.Unsupported)
        {
            historyState = FeedbackHistoryState.Unknown;
        }
    }

    public void RefreshHistory()
    {
        if (!session.IsSignedIn || refreshing)
        {
            return;
        }

        var userId = session.CurrentUser?.Id;
        if (!string.Equals(userId, historyOwnerId, StringComparison.Ordinal))
        {
            historyOwnerId = userId;
            everLoaded = false;
            history = Array.Empty<MyFeedbackDto>();
            historyCursor = null;
            historyState = FeedbackHistoryState.Unknown;
            Interlocked.Increment(ref historyRevision);
        }

        var hadItems = historyState == FeedbackHistoryState.Ready;
        refreshing = true;
        checkRequested = false;
        if (!hadItems)
        {
            historyState = FeedbackHistoryState.Loading;
        }

        work.Run("history", async token =>
        {
            var reported = AepFailure.None;
            var page = await client.MineAsync(null, token, failure => reported = failure).ConfigureAwait(false);
            if (page is null)
            {
                ApplyHistoryFailure(reported, hadItems);
                return;
            }

            var items = page.Items ?? Array.Empty<MyFeedbackDto>();
            history = items;
            historyCursor = page.NextCursor;
            everLoaded = true;
            historyState = FeedbackHistoryState.Ready;
            Interlocked.Increment(ref historyRevision);
            if (!string.IsNullOrEmpty(userId))
            {
                pendingFetch = new FeedbackFetch(userId, items, page.NextCursor is null);
            }
        }, () => refreshing = false);
    }

    public void LoadMoreHistory()
    {
        var cursor = historyCursor;
        if (cursor is null || loadingMore || refreshing || historyState != FeedbackHistoryState.Ready)
        {
            return;
        }

        loadingMore = true;
        work.Run("history more", async token =>
        {
            var page = await client.MineAsync(cursor, token).ConfigureAwait(false);
            if (page?.Items is null)
            {
                return;
            }

            history = IdentifiedMerge.MergeById(history, page.Items, ByNewestFirst);
            historyCursor = page.NextCursor;
            Interlocked.Increment(ref historyRevision);
        }, () => loadingMore = false);
    }

    private void ApplyHistoryFailure(AepFailure reported, bool hadItems)
    {
        if (hadItems && reported.Kind != AepFailureKind.Server)
        {
            historyState = FeedbackHistoryState.Ready;
            return;
        }

        history = Array.Empty<MyFeedbackDto>();
        historyCursor = null;
        historyState = FeedbackHistoryState.Unsupported;
        Interlocked.Increment(ref historyRevision);
    }

    private void OnPinged()
    {
        checkRequested = true;
    }

    private void OnRealtimeConnected(bool active)
    {
        if (active)
        {
            ScheduleCheck(DateTime.UtcNow + ReconnectSpread * Random.Shared.NextDouble());
        }
    }

    private void ScheduleCheck(DateTime dueUtc)
    {
        var due = dueUtc.Ticks;
        var current = Interlocked.Read(ref checkDueTicks);
        while (due < current)
        {
            var observed = Interlocked.CompareExchange(ref checkDueTicks, due, current);
            if (observed == current)
            {
                return;
            }

            current = observed;
        }
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        var fetch = Interlocked.Exchange(ref pendingFetch, null);
        if (fetch is not null)
        {
            ApplyFetch(fetch);
        }

        if (!session.IsSignedIn || session.CurrentUser?.Id is not { Length: > 0 } userId)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (!string.Equals(userId, sessionCheckedUserId, StringComparison.Ordinal))
        {
            sessionCheckedUserId = userId;
            ScheduleCheck(now + SessionCheckDelay);
        }

        if (Interlocked.Read(ref checkDueTicks) <= now.Ticks)
        {
            Interlocked.Exchange(ref checkDueTicks, long.MaxValue);
            if (CurrentMarks() is { HasHistory: true })
            {
                checkRequested = true;
            }
        }

        if (checkRequested && !refreshing)
        {
            RefreshHistory();
        }
    }

    private void ApplyFetch(FeedbackFetch fetch)
    {
        if (!string.Equals(fetch.UserId, session.CurrentUser?.Id, StringComparison.Ordinal))
        {
            return;
        }

        var viewing = Environment.TickCount64 < viewingUntilMilliseconds ? viewingId : null;
        raised.Clear();
        if (!FeedbackUpdates.Apply(MarksFor(fetch.UserId), fetch.Items, fetch.Complete, viewing, raised))
        {
            return;
        }

        configuration.Save();
        var accent = AppAccents.For(AppId);
        for (var index = 0; index < raised.Count; index++)
        {
            var update = raised[index];
            var item = update.Item;
            var isReply = update.Change == FeedbackChange.Reply;
            var title = isReply
                ? Loc.T(L.Feedback.UpdateReply)
                : Loc.T(FeedbackStatuses.UpdateTitle(FeedbackKinds.Parse(item.Category),
                    FeedbackStatuses.Parse(item.Status)));
            var body = FeedbackUpdates.Excerpt(isReply ? item.Reply : item.Text);
            notifications.Notify(new PhoneNotification(AppId, title, body, DateTime.Now, accent,
                FeedbackLauncher.GroupKey(item.Id)));
        }

        raised.Clear();
    }

    private FeedbackUpdateMarks? CurrentMarks()
    {
        if (!session.IsSignedIn || session.CurrentUser?.Id is not { Length: > 0 } userId)
        {
            return null;
        }

        return configuration.FeedbackMarks.TryGetValue(userId, out var marks) ? marks : null;
    }

    private FeedbackUpdateMarks MarksFor(string userId)
    {
        if (!configuration.FeedbackMarks.TryGetValue(userId, out var marks))
        {
            marks = new FeedbackUpdateMarks();
            configuration.FeedbackMarks[userId] = marks;
        }

        return marks;
    }

    private static int ByNewestFirst(MyFeedbackDto first, MyFeedbackDto second) =>
        second.CreatedAtUnix.CompareTo(first.CreatedAtUnix);

    public void Compose(string text, FeedbackCategory category, string context, string[] imagePaths,
        Action<FeedbackSendResult> onComplete)
    {
        var trimmed = (text ?? string.Empty).Trim();
        if (trimmed.Length == 0 || posting)
        {
            return;
        }

        posting = true;
        uploadedImages = 0;
        totalImages = imagePaths.Length;
        var reported = AepFailure.None;
        var wireCategory = FeedbackKinds.Of(category).WireName;
        work.Run("compose", async token =>
        {
            var keys = await UploadImagesAsync(imagePaths, token, failure => reported = failure).ConfigureAwait(false);
            if (keys is null)
            {
                return false;
            }

            var created = await client.CreateAsync(trimmed, keys, wireCategory, context, token,
                failure => reported = failure).ConfigureAwait(false);
            return created is not null;
        }, succeeded =>
        {
            var failure = succeeded || reported.Failed ? reported : AepFailure.Transport(AepFailureKind.Offline);
            if (!succeeded)
            {
                AepLog.Warning($"Feedback failed to send: {failure.Describe()}");
            }

            onComplete(new FeedbackSendResult(succeeded, succeeded ? AepFailure.None : failure));
        }, () => posting = false);
    }

    private async Task<string[]?> UploadImagesAsync(string[] imagePaths, CancellationToken token,
        Action<AepFailure> onFailure)
    {
        if (imagePaths.Length == 0)
        {
            return Array.Empty<string>();
        }

        var keys = new string[imagePaths.Length];
        for (var index = 0; index < imagePaths.Length; index++)
        {
            var baked = ImageProcessor.BakeJpeg(imagePaths[index], MaxImageDimension);
            var upload = await media.UploadUrlAsync("image/jpeg", "feedback", token, onFailure).ConfigureAwait(false);
            if (upload is null)
            {
                return null;
            }

            var uploaded = await media.UploadImageAsync(upload.UploadUrl, baked.Bytes, "image/jpeg", token, onFailure)
                .ConfigureAwait(false);
            if (!uploaded)
            {
                return null;
            }

            keys[index] = upload.Key;
            uploadedImages = index + 1;
        }

        return keys;
    }

    public void Dispose()
    {
        signals.FeedbackPinged -= OnPinged;
        signals.ConnectedChanged -= OnRealtimeConnected;
        Plugin.Framework.Update -= OnFrameworkUpdate;
        work.Dispose();
    }
}
