using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Aetherphone.Core.Game;
using Aetherphone.Core.Net;
using Aetherphone.Core.Notifications;

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
    private readonly CancellationTokenSource cancellation = new();

    private readonly HashSet<string> knownIds = new(StringComparer.Ordinal);
    private bool seeded;
    private int refreshing;

    private volatile VenueEvent[] events = Array.Empty<VenueEvent>();
    private volatile int version;
    private DateTime lastRefreshUtc;
    private volatile VenueState state = VenueState.Idle;

    public VenuesService(HttpService http, NotificationService notifications, Configuration configuration, GameData gameData)
    {
        this.http = http;
        this.notifications = notifications;
        this.configuration = configuration;
        this.gameData = gameData;
    }

    public VenueState State => state;

    public int Version => version;

    public IReadOnlyList<VenueEvent> Events => events;

    public DateTime LastRefreshUtc => lastRefreshUtc;

    public void EnsureFresh(bool force)
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

    private async Task RefreshAsync(string homeDataCenter)
    {
        try
        {
            var token = cancellation.Token;
            var collected = new List<VenueEvent>(256);
            var ffxivOk = await FetchFfxivAsync(collected, token).ConfigureAwait(false);
            var partakeOk = await FetchPartakeAsync(collected, token).ConfigureAwait(false);

            collected.Sort(static (left, right) => left.StartUtc.CompareTo(right.StartUtc));
            var snapshot = collected.ToArray();

            NotifyNew(snapshot, homeDataCenter);

            events = snapshot;
            lastRefreshUtc = DateTime.UtcNow;
            version++;
            state = ffxivOk || partakeOk ? VenueState.Ready : VenueState.Failed;
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

            AepLog.Warning($"Venues refresh failed: {exception.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref refreshing, 0);
        }
    }

    private async Task<bool> FetchFfxivAsync(List<VenueEvent> into, CancellationToken token)
    {
        var dtos = await http.GetJsonAsync(FfxivApi, VenueJsonContext.Default.FfxivVenueDtoArray, null, token).ConfigureAwait(false);
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
            var envelope = await http.PostJsonAsync(PartakeApi, request, VenueJsonContext.Default.GraphQlRequest, VenueJsonContext.Default.PartakeEnvelope, null, token).ConfigureAwait(false);
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
        return "{ events(game: \"final-fantasy-xiv\", sortBy: STARTS_AT, limit: " + PartakePageSize +
            ", offset: " + offset +
            ") { id title location tags ageRating startsAt endsAt attendeeCount description: description(type: MARKDOWN) " +
            "locationData { server { name dataCenterId } dataCenter { id name } } " +
            "team { name iconUrl websiteUrl discordUrl } } }";
    }

    private void NotifyNew(VenueEvent[] snapshot, string homeDataCenter)
    {
        if (!seeded)
        {
            for (var index = 0; index < snapshot.Length; index++)
            {
                knownIds.Add(snapshot[index].Id);
            }

            seeded = true;
            return;
        }

        if (!configuration.VenueNotifyNewEvents)
        {
            for (var index = 0; index < snapshot.Length; index++)
            {
                knownIds.Add(snapshot[index].Id);
            }

            return;
        }

        var nowUtc = DateTime.UtcNow;
        var presented = 0;
        for (var index = 0; index < snapshot.Length; index++)
        {
            var venue = snapshot[index];
            if (!knownIds.Add(venue.Id))
            {
                continue;
            }

            if (presented >= MaxNotificationsPerRefresh)
            {
                continue;
            }

            if (homeDataCenter.Length > 0 && !string.Equals(venue.DataCenter, homeDataCenter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (venue.StartUtc > nowUtc.AddHours(24))
            {
                continue;
            }

            notifications.Notify(new PhoneNotification("venues", venue.Title, venue.LocationLine, DateTime.Now, NotificationAccent));
            presented++;
        }
    }

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
