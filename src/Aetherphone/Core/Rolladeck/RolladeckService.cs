using Aetherphone.Core.Net;

namespace Aetherphone.Core.Rolladeck;

internal sealed class RolladeckService(HttpService http)
{
    private const string LiveApiUrl = "https://us-central1-xiv-rolladeck.cloudfunctions.net/apiV1Live";
    private const string VenuesApiUrl = "https://us-central1-xiv-rolladeck.cloudfunctions.net/apiV1Venues";
    private static readonly TimeSpan LiveCacheAge = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan VenuesCacheAge = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ApiRateLimit = TimeSpan.FromSeconds(30);

    private volatile LiveResponse? data;
    private volatile DirectoryResponse? directory;
    private DateTime lastFetch = DateTime.MinValue;
    private DateTime lastDirectoryFetch = DateTime.MinValue;
    private DateTime lastAttempt = DateTime.MinValue;
    private DateTime lastDirectoryAttempt = DateTime.MinValue;
    private int fetching;
    private int fetchingDirectory;
    private volatile bool fetchFailed;
    private int liveCountWithAddress;
    private int version;

    public IReadOnlyList<LiveDjEntry> LiveDJs => data?.LiveDJs ?? (IReadOnlyList<LiveDjEntry>)[];
    public IReadOnlyList<OpenVenueEntry> OpenVenues => data?.OpenVenues ?? (IReadOnlyList<OpenVenueEntry>)[];
    public IReadOnlyList<DirectoryVenueEntry> Directory => directory?.Venues ?? (IReadOnlyList<DirectoryVenueEntry>)[];

    public int LiveCount => data?.LiveDJs.Count ?? 0;
    public int LiveCountWithAddress => liveCountWithAddress;
    public bool Loading => Volatile.Read(ref fetching) == 1;
    public bool HasData => data != null;
    public bool Failed => fetchFailed;
    public int Version => Volatile.Read(ref version);
    public DateTime LiveFetchedUtc => lastFetch;

    public void EnsureFresh(bool force = false)
    {
        var nowUtc = DateTime.UtcNow;
        if (nowUtc - lastAttempt < ApiRateLimit || (!force && data != null && nowUtc - lastFetch < LiveCacheAge))
        {
            return;
        }

        if (Interlocked.CompareExchange(ref fetching, 1, 0) != 0)
        {
            return;
        }

        lastAttempt = nowUtc;
        _ = Task.Run(FetchLiveAsync);
    }

    public void EnsureDirectoryFresh(bool force = false)
    {
        var nowUtc = DateTime.UtcNow;
        if (nowUtc - lastDirectoryAttempt < ApiRateLimit ||
            (!force && directory != null && nowUtc - lastDirectoryFetch < VenuesCacheAge))
        {
            return;
        }

        if (Interlocked.CompareExchange(ref fetchingDirectory, 1, 0) != 0)
        {
            return;
        }

        lastDirectoryAttempt = nowUtc;
        _ = Task.Run(FetchDirectoryAsync);
    }

    private async Task FetchLiveAsync()
    {
        try
        {
            var result = await http.GetJsonAsync(LiveApiUrl, RolladeckJsonContext.Default.LiveResponse, bearer: null,
                token: default).ConfigureAwait(false);
            if (result == null)
            {
                fetchFailed = true;
                return;
            }

            var count = 0;
            for (var index = 0; index < result.LiveDJs.Count; index++)
            {
                result.LiveDJs[index].InitNormalized();
                if (result.LiveDJs[index].HasLocation)
                {
                    count++;
                }
            }

            liveCountWithAddress = count;
            fetchFailed = false;
            lastFetch = DateTime.UtcNow;
            data = result;
            Interlocked.Increment(ref version);
        }
        catch (Exception exception)
        {
            fetchFailed = true;
            AepLog.Warning(exception, "Rolladeck live refresh failed");
        }
        finally
        {
            Interlocked.Exchange(ref fetching, 0);
        }
    }

    private async Task FetchDirectoryAsync()
    {
        try
        {
            var result = await http.GetJsonAsync(VenuesApiUrl, RolladeckJsonContext.Default.DirectoryResponse,
                bearer: null, token: default).ConfigureAwait(false);
            if (result == null)
            {
                return;
            }

            lastDirectoryFetch = DateTime.UtcNow;
            directory = result;
            Interlocked.Increment(ref version);
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "Rolladeck venue directory refresh failed");
        }
        finally
        {
            Interlocked.Exchange(ref fetchingDirectory, 0);
        }
    }
}
