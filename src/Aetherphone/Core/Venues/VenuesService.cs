using Aetherphone.Core.Game;
using Aetherphone.Core.Net;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Rolladeck;

namespace Aetherphone.Core.Venues;

internal sealed class VenuesService : IDisposable
{
    private const string FfxivApi = "https://api.ffxivvenues.com/v1.0/venue";
    private const string PartakeApi = "https://api.partake.gg/";
    private const int PartakePageSize = 100;
    private const int PartakeMaxPages = 5;
    private const int MaxNotificationsPerRefresh = 4;
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(5);
    private static readonly Vector4 NotificationAccent = new(0.93f, 0.28f, 0.55f, 1f);
    private readonly HttpService http;
    private readonly NotificationService notifications;
    private readonly Configuration configuration;
    private readonly GameData gameData;
    private readonly RolladeckService rolladeck;
    private readonly CancellationTokenSource cancellation = new();
    private readonly HashSet<string> knownIds = new(StringComparer.Ordinal);
    private bool seeded;
    private int refreshing;
    private int merging;
    private volatile VenueEvent[] listings = Array.Empty<VenueEvent>();
    private volatile VenueEvent[] events = Array.Empty<VenueEvent>();
    private volatile VenueDj[] djs = Array.Empty<VenueDj>();
    private int listingsVersion;
    private int mergedListingsVersion = -1;
    private int mergedRolladeckVersion = -1;
    private volatile int version;
    private DateTime lastRefreshUtc;
    private volatile VenueState state = VenueState.Idle;

    public VenuesService(HttpService http, NotificationService notifications, Configuration configuration,
        GameData gameData, RolladeckService rolladeck)
    {
        this.http = http;
        this.notifications = notifications;
        this.configuration = configuration;
        this.gameData = gameData;
        this.rolladeck = rolladeck;
    }

    public VenueState State => state;
    public int Version => version;
    public IReadOnlyList<VenueEvent> Events => events;
    public IReadOnlyList<VenueDj> Djs => djs;
    public DateTime LastRefreshUtc => lastRefreshUtc;
    public bool Busy => Volatile.Read(ref refreshing) == 1 || rolladeck.Loading;

    public void EnsureFresh(bool force)
    {
        rolladeck.EnsureFresh(force);
        rolladeck.EnsureDirectoryFresh(force);
        RefreshListings(force);
        RemergeIfStale();
    }

    private void RefreshListings(bool force)
    {
        if (Volatile.Read(ref refreshing) == 1)
        {
            return;
        }

        var stale = state == VenueState.Idle || DateTime.UtcNow - lastRefreshUtc >= RefreshInterval;
        if (!force && !stale)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref refreshing, 1, 0) != 0)
        {
            return;
        }

        if (state != VenueState.Ready)
        {
            state = VenueState.Loading;
        }

        var homeDataCenter = gameData.DataCenterName(gameData.LocalCurrentWorldId);
        _ = RefreshAsync(homeDataCenter);
    }

    private void RemergeIfStale()
    {
        var wantedListings = Volatile.Read(ref listingsVersion);
        var wantedRolladeck = rolladeck.Version;
        if (wantedListings == mergedListingsVersion && wantedRolladeck == mergedRolladeckVersion)
        {
            return;
        }

        if (wantedListings == 0 && !rolladeck.HasData)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref merging, 1, 0) != 0)
        {
            return;
        }

        mergedListingsVersion = wantedListings;
        mergedRolladeckVersion = wantedRolladeck;
        _ = Task.Run(Merge);
    }

    private void Merge()
    {
        try
        {
            var nowUtc = DateTime.UtcNow;
            var snapshot = VenueMerger.Merge(listings, rolladeck.Directory, rolladeck.OpenVenues, rolladeck.LiveDJs,
                rolladeck.LiveFetchedUtc, nowUtc);
            djs = snapshot.Djs;
            events = snapshot.Events;
            version++;
            if (state != VenueState.Ready && events.Length > 0)
            {
                state = VenueState.Ready;
            }
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "Venues merge failed");
        }
        finally
        {
            Interlocked.Exchange(ref merging, 0);
        }
    }

    private async Task RefreshAsync(string homeDataCenter)
    {
        try
        {
            var token = cancellation.Token;
            var collected = new List<VenueEvent>(1600);
            var ffxivOk = await FetchFfxivAsync(collected, token).ConfigureAwait(false);
            var partakeOk = await FetchPartakeAsync(collected, token).ConfigureAwait(false);
            var snapshot = collected.ToArray();
            NotifyNew(snapshot, homeDataCenter);
            listings = snapshot;
            lastRefreshUtc = DateTime.UtcNow;
            Interlocked.Increment(ref listingsVersion);
            if (!ffxivOk && !partakeOk && events.Length == 0)
            {
                state = VenueState.Failed;
            }

            RemergeIfStale();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            if (state != VenueState.Ready)
            {
                state = VenueState.Failed;
            }

            AepLog.Warning(exception, "Venues refresh failed");
        }
        finally
        {
            Interlocked.Exchange(ref refreshing, 0);
        }
    }

    private async Task<bool> FetchFfxivAsync(List<VenueEvent> into, CancellationToken token)
    {
        var dtos = await http.GetJsonAsync(FfxivApi, VenueJsonContext.Default.FfxivVenueDtoArray, null, token)
            .ConfigureAwait(false);
        if (dtos is null)
        {
            return false;
        }

        var nowUtc = DateTime.UtcNow;
        for (var index = 0; index < dtos.Length; index++)
        {
            var mapped = VenueMapper.FromFfxiv(dtos[index], nowUtc);
            if (mapped is not null)
            {
                into.Add(mapped);
            }
        }

        return true;
    }

    private async Task<bool> FetchPartakeAsync(List<VenueEvent> into, CancellationToken token)
    {
        var any = false;
        for (var page = 0; page < PartakeMaxPages; page++)
        {
            var request = new GraphQlRequest { Query = BuildPartakeQuery(page * PartakePageSize) };
            var envelope = await http.PostJsonAsync(PartakeApi, request, VenueJsonContext.Default.GraphQlRequest,
                VenueJsonContext.Default.PartakeEnvelope, null, token).ConfigureAwait(false);
            var batch = envelope?.Data?.Events;
            if (batch is null || batch.Length == 0)
            {
                break;
            }

            any = true;
            for (var index = 0; index < batch.Length; index++)
            {
                var mapped = VenueMapper.FromPartake(batch[index]);
                if (mapped is not null)
                {
                    into.Add(mapped);
                }
            }

            if (batch.Length < PartakePageSize)
            {
                break;
            }
        }

        return any;
    }

    private static string BuildPartakeQuery(int offset)
    {
        return "{ events(game: \"final-fantasy-xiv\", sortBy: STARTS_AT, limit: " + PartakePageSize + ", offset: " +
               offset +
               ") { id title location tags ageRating startsAt endsAt attendeeCount description: description(type: MARKDOWN) " +
               "locationData { server { name dataCenterId } dataCenter { id name } } " +
               "team { name iconUrl websiteUrl discordUrl } } }";
    }

    private void NotifyNew(VenueEvent[] snapshot, string homeDataCenter)
    {
        if (!seeded || !configuration.VenueNotifyNewEvents)
        {
            for (var index = 0; index < snapshot.Length; index++)
            {
                knownIds.Add(snapshot[index].Id);
            }

            seeded = true;
            return;
        }

        var nowUtc = DateTime.UtcNow;
        var presented = 0;
        for (var index = 0; index < snapshot.Length; index++)
        {
            var venue = snapshot[index];
            if (!knownIds.Add(venue.Id) || presented >= MaxNotificationsPerRefresh)
            {
                continue;
            }

            if (homeDataCenter.Length > 0 &&
                !string.Equals(venue.DataCenter, homeDataCenter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (venue.StartUtc is not { } start || start > nowUtc.AddHours(24))
            {
                continue;
            }

            notifications.Notify(new PhoneNotification("venues", venue.Title, venue.PlaceLine, DateTime.Now,
                NotificationAccent));
            presented++;
        }
    }

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
