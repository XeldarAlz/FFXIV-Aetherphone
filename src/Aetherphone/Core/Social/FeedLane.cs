using System.Globalization;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Social;

internal sealed class FeedLane<TPost> : ITrimmable where TPost : class, IIdentified
{
    private readonly object gate = new();
    private readonly Comparison<TPost>? order;
    private readonly Func<TPost, long>? createdAtUnix;
    private volatile TPost[] items = Array.Empty<TPost>();
    private volatile string? cursor;
    private volatile bool loading;
    private volatile bool loadingMore;
    private volatile AepFailureBox? failureBox;

    public FeedLane(Comparison<TPost> order, Func<TPost, long>? createdAtUnix = null)
    {
        this.order = order;
        this.createdAtUnix = createdAtUnix;
    }

    private FeedLane()
    {
    }

    public static FeedLane<TPost> ServerOrdered() => new();

    public bool KeepsServerOrder => order is null;

    public TPost[] Items
    {
        get => items;
        set => items = value;
    }

    public string? Cursor => cursor;
    public bool HasMore => cursor is not null;

    public bool Loading
    {
        get => loading;
        set => loading = value;
    }

    public bool LoadingMore
    {
        get => loadingMore;
        set => loadingMore = value;
    }

    public bool Failed => failureBox is not null;

    public AepFailure Failure => failureBox?.Failure ?? AepFailure.None;

    public bool NeverLoaded => items.Length == 0;

    public void RecordFailure(AepFailure failure)
    {
        failureBox = new AepFailureBox(failure);
    }

    public void ClearFailure()
    {
        failureBox = null;
    }

    public void ApplyRefresh(TPost[] incoming, string? nextCursor)
    {
        failureBox = null;
        lock (gate)
        {
            if (order is null)
            {
                items = incoming;
                cursor = nextCursor;
                return;
            }

            var wasEmpty = items.Length == 0;
            items = IdentifiedMerge.ReconcileNewestPage(items, incoming, order);
            if (wasEmpty)
            {
                cursor = nextCursor;
            }
        }
    }

    public void ApplyMore(TPost[] incoming, string? nextCursor)
    {
        failureBox = null;
        lock (gate)
        {
            items = order is null
                ? IdentifiedMerge.AppendNew(items, incoming)
                : IdentifiedMerge.MergeById(items, incoming, order);
            cursor = nextCursor;
        }
    }

    public void Trim(int max)
    {
        if (max <= 0 || order is null)
        {
            return;
        }

        lock (gate)
        {
            var snapshot = items;
            if (snapshot.Length <= max)
            {
                return;
            }

            var trimmed = new TPost[max];
            Array.Copy(snapshot, trimmed, max);
            items = trimmed;
            if (createdAtUnix is not null)
            {
                cursor = ContinuationCursor(createdAtUnix(trimmed[max - 1]));
            }
        }
    }

    private static string ContinuationCursor(long unixSeconds) =>
        DateTimeOffset.FromUnixTimeSeconds(unixSeconds + 1).UtcDateTime
            .ToString("o", CultureInfo.InvariantCulture);

    public void Clear()
    {
        lock (gate)
        {
            items = Array.Empty<TPost>();
            cursor = null;
            loading = false;
            loadingMore = false;
        }
    }
}
