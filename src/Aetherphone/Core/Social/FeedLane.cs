using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Social;

internal sealed class FeedLane<TPost> where TPost : class, IIdentified
{
    private readonly object gate = new();
    private readonly Comparison<TPost> order;
    private volatile TPost[] items = Array.Empty<TPost>();
    private volatile string? cursor;
    private volatile bool loading;
    private volatile bool loadingMore;

    public FeedLane(Comparison<TPost> order)
    {
        this.order = order;
    }

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

    public void ApplyRefresh(TPost[] incoming, string? nextCursor)
    {
        lock (gate)
        {
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
        lock (gate)
        {
            items = IdentifiedMerge.MergeById(items, incoming, order);
            cursor = nextCursor;
        }
    }

    public void Trim(int max)
    {
        if (max <= 0)
        {
            return;
        }

        lock (gate)
        {
            if (items.Length <= max)
            {
                return;
            }

            var trimmed = new TPost[max];
            Array.Copy(items, trimmed, max);
            items = trimmed;
        }
    }

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
