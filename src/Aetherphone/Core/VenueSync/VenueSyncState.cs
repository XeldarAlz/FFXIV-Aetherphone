namespace Aetherphone.Core.VenueSync;

internal enum VenueSyncLoadState
{
    Idle,
    Loading,
    Ready,
    Failed,
}

internal sealed class VenueSyncState : IDisposable
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(30);
    private readonly VenueSyncApiClient client;
    private readonly Configuration configuration;
    private readonly CancellationTokenSource cancellation = new();
    private int refreshing;
    private DateTime lastRefreshUtc;
    private volatile VenueSyncLoadState shiftsState = VenueSyncLoadState.Idle;

    private int sessionSalesCount;
    private decimal sessionSalesTotal;

    public VenueSyncState(VenueSyncApiClient client, Configuration configuration)
    {
        this.client = client;
        this.configuration = configuration;
    }

    public VenueSyncShiftsResponse? Shifts { get; private set; }
    public DateTime LastRefreshUtc => lastRefreshUtc;
    public int SessionSalesCount => sessionSalesCount;
    public decimal SessionSalesTotal => sessionSalesTotal;

    public void EnsureShiftsFresh(bool force)
    {
        var venueId = configuration.VenueSyncSelectedVenueId;
        if (string.IsNullOrEmpty(venueId))
        {
            return;
        }

        if (Volatile.Read(ref refreshing) == 1)
        {
            return;
        }

        var stale = shiftsState == VenueSyncLoadState.Idle ||
                    DateTime.UtcNow - lastRefreshUtc >= RefreshInterval;
        if (!force && !stale)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref refreshing, 1, 0) != 0)
        {
            return;
        }

        if (shiftsState != VenueSyncLoadState.Ready)
        {
            shiftsState = VenueSyncLoadState.Loading;
        }

        _ = RefreshShiftsAsync(venueId);
    }

    private async Task RefreshShiftsAsync(string venueId)
    {
        try
        {
            var token = cancellation.Token;
            var response = await client.GetShiftsAsync(venueId, token).ConfigureAwait(false);
            if (response is not null)
            {
                Shifts = response;
                lastRefreshUtc = DateTime.UtcNow;
                shiftsState = VenueSyncLoadState.Ready;
            }
            else if (shiftsState != VenueSyncLoadState.Ready)
            {
                shiftsState = VenueSyncLoadState.Failed;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            if (shiftsState != VenueSyncLoadState.Ready)
            {
                shiftsState = VenueSyncLoadState.Failed;
            }

            AepLog.Warning($"VenueSync shifts refresh failed: {exception.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref refreshing, 0);
        }
    }

    public void RecordSale(decimal amount)
    {
        sessionSalesCount++;
        sessionSalesTotal += amount;
    }

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
