using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Radio;

internal enum CommunityStationLoad : byte
{
    Idle,
    Loading,
    Ready,
    NotFound,
    SignedOut,
    Unavailable,
}

internal sealed class CommunityRadioService : IDisposable
{
    private const long ActiveIntervalMilliseconds = 15 * 1000;
    private const long IdleIntervalMilliseconds = 5 * 60 * 1000;
    private const long RetryIntervalMilliseconds = 60 * 1000;

    private readonly AethernetApi api;
    private readonly AethernetSession session;
    private readonly CancellationTokenSource cancellation = new();
    private volatile CommunityStationDto[] stations = Array.Empty<CommunityStationDto>();
    private volatile MyCommunityStationDto? mine;
    private volatile bool loading;
    private volatile bool loaded;
    private volatile bool mineChecked;
    private volatile RadioTrackDto[] tracks = Array.Empty<RadioTrackDto>();
    private volatile bool tracksLoading;
    private volatile string tracksStationId = string.Empty;
    private volatile bool tracksFailed;
    private volatile int liveCount;
    private volatile int followedLiveCount;
    private volatile CommunityStationDto? viewed;
    private volatile string viewedId = string.Empty;
    private volatile CommunityStationLoad viewedState;
    private long lastFetchTick = long.MinValue / 2;
    private long retryAfterTick;
    private int fetching;
    private int fetchingMine;

    public CommunityRadioService(AethernetApi api, AethernetSession session)
    {
        this.api = api;
        this.session = session;
    }

    public CommunityStationDto[] Stations => stations;
    public MyCommunityStationDto? Mine => mine;
    public bool Loading => loading;
    public bool Loaded => loaded;
    public bool OwnsStation => mine is not null;
    public bool IsSignedIn => session.IsSignedIn;

    public int LiveCount => liveCount;

    public int FollowedLiveCount => followedLiveCount;

    private static int CountLive(CommunityStationDto[] snapshot)
    {
        var count = 0;
        for (var index = 0; index < snapshot.Length; index++)
        {
            if (snapshot[index].IsLive)
            {
                count++;
            }
        }

        return count;
    }

    private static int CountFollowedLive(CommunityStationDto[] snapshot)
    {
        var count = 0;
        for (var index = 0; index < snapshot.Length; index++)
        {
            if (snapshot[index].IsLive && snapshot[index].IsFollowing)
            {
                count++;
            }
        }

        return count;
    }

    // The interval belongs to the caller, not to whoever fetched last. Storing a single "next fetch"
    // stamp let a background refresh from the home screen schedule five minutes out and then hold the
    // radio screens to that, so a station could go live and the list would keep saying off air.
    public void EnsureFresh(bool active)
    {
        var now = Environment.TickCount64;
        if (now < Volatile.Read(ref retryAfterTick))
        {
            return;
        }

        var interval = active ? ActiveIntervalMilliseconds : IdleIntervalMilliseconds;
        if (now - Volatile.Read(ref lastFetchTick) < interval)
        {
            return;
        }

        Refresh();
    }

    public void Refresh()
    {
        if (Interlocked.Exchange(ref fetching, 1) == 1)
        {
            return;
        }

        Volatile.Write(ref retryAfterTick, 0);
        loading = !loaded;
        _ = FetchAsync(cancellation.Token);
    }

    public void EnsureMine()
    {
        if (mineChecked || Interlocked.Exchange(ref fetchingMine, 1) == 1)
        {
            return;
        }

        _ = FetchMineAsync(cancellation.Token);
    }

    public async Task<bool> SaveMineAsync(UpdateCommunityStationRequest request)
    {
        try
        {
            var updated = await api.Radio.UpdateMineAsync(request, cancellation.Token).ConfigureAwait(false);
            if (updated is null)
            {
                return false;
            }

            mine = updated;
            Volatile.Write(ref lastFetchTick, long.MinValue / 2);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[Radio] station update failed");
            return false;
        }
    }

    public void ToggleFollow(CommunityStationDto station)
    {
        var wanted = !station.IsFollowing;
        var followers = Math.Max(0, station.Followers + (wanted ? 1 : -1));
        Replace(station.Id, station with { IsFollowing = wanted, Followers = followers });
        _ = FollowAsync(station.Id, wanted, station);
    }

    private async Task FollowAsync(string stationId, bool follow, CommunityStationDto previous)
    {
        try
        {
            var result = await api.Radio.FollowAsync(stationId, follow, cancellation.Token).ConfigureAwait(false);
            if (result is null)
            {
                Replace(stationId, previous);
                return;
            }

            if (TryResolve(stationId, out var current))
            {
                Replace(stationId, current with { IsFollowing = result.Following, Followers = result.Followers });
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[Radio] follow failed");
            Replace(stationId, previous);
        }
    }

    private void Replace(string stationId, CommunityStationDto updated)
    {
        var cached = viewed;
        if (cached is not null && string.Equals(cached.Id, stationId, StringComparison.Ordinal))
        {
            viewed = updated;
        }

        var snapshot = stations;
        for (var index = 0; index < snapshot.Length; index++)
        {
            if (!string.Equals(snapshot[index].Id, stationId, StringComparison.Ordinal))
            {
                continue;
            }

            var copy = new CommunityStationDto[snapshot.Length];
            Array.Copy(snapshot, copy, snapshot.Length);
            copy[index] = updated;
            stations = copy;
            liveCount = CountLive(copy);
            followedLiveCount = CountFollowedLive(copy);
            return;
        }
    }

    public RadioTrackDto[] Tracks => tracks;

    public bool TracksFailed => tracksFailed;
    public bool TracksLoading => tracksLoading;

    public void EnsureTracks(string stationId)
    {
        if (string.Equals(tracksStationId, stationId, StringComparison.Ordinal))
        {
            return;
        }

        tracksStationId = stationId;
        tracks = Array.Empty<RadioTrackDto>();
        tracksFailed = false;
        tracksLoading = true;
        _ = FetchTracksAsync(stationId, cancellation.Token);
    }

    private async Task FetchTracksAsync(string stationId, CancellationToken token)
    {
        try
        {
            var page = await api.Radio.TracksAsync(stationId, token).ConfigureAwait(false);
            if (!string.Equals(tracksStationId, stationId, StringComparison.Ordinal))
            {
                return;
            }

            if (page is null)
            {
                tracksFailed = true;
                AepLog.Warning($"[Radio] tracklist for station {stationId} could not be read; "
                    + "leaving the station without a track list rather than showing it as empty");
                return;
            }

            tracks = page.Items;
            tracksFailed = false;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[Radio] tracklist fetch failed");
        }
        finally
        {
            if (string.Equals(tracksStationId, stationId, StringComparison.Ordinal))
            {
                tracksLoading = false;
            }
        }
    }

    public bool TryFind(string stationId, out CommunityStationDto station)
    {
        var snapshot = stations;
        for (var index = 0; index < snapshot.Length; index++)
        {
            if (string.Equals(snapshot[index].Id, stationId, StringComparison.Ordinal))
            {
                station = snapshot[index];
                return true;
            }
        }

        station = null!;
        return false;
    }

    public CommunityStationLoad StationState => viewedState;

    public bool TryResolve(string stationId, out CommunityStationDto station)
    {
        if (TryFind(stationId, out station))
        {
            return true;
        }

        var cached = viewed;
        if (cached is not null && string.Equals(cached.Id, stationId, StringComparison.Ordinal))
        {
            station = cached;
            return true;
        }

        station = null!;
        return false;
    }

    public void OpenStation(string stationId, CommunityStationDto? known)
    {
        viewedId = stationId;
        if (known is not null)
        {
            viewed = known;
            viewedState = CommunityStationLoad.Ready;
            return;
        }

        if (TryFind(stationId, out var found))
        {
            viewed = found;
            viewedState = CommunityStationLoad.Ready;
            return;
        }

        viewed = null;
        viewedState = CommunityStationLoad.Loading;
        _ = FetchStationAsync(stationId, cancellation.Token);
    }

    public void RetryStation()
    {
        var stationId = viewedId;
        if (stationId.Length == 0)
        {
            return;
        }

        viewedState = CommunityStationLoad.Loading;
        _ = FetchStationAsync(stationId, cancellation.Token);
        RetryDirectory();
    }

    public void RetryDirectory()
    {
        Volatile.Write(ref retryAfterTick, 0);
        Volatile.Write(ref lastFetchTick, long.MinValue / 2);
        Refresh();
    }

    private async Task FetchStationAsync(string stationId, CancellationToken token)
    {
        var status = 0;
        try
        {
            var station = await api.Radio.StationAsync(stationId, token, code => status = code)
                .ConfigureAwait(false);
            if (!string.Equals(viewedId, stationId, StringComparison.Ordinal))
            {
                return;
            }

            if (station is not null)
            {
                viewed = station;
                viewedState = CommunityStationLoad.Ready;
                return;
            }

            viewedState = status == 404
                ? CommunityStationLoad.NotFound
                : session.IsSignedIn
                    ? CommunityStationLoad.Unavailable
                    : CommunityStationLoad.SignedOut;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[Radio] station fetch failed");
            if (string.Equals(viewedId, stationId, StringComparison.Ordinal))
            {
                viewedState = CommunityStationLoad.Unavailable;
            }
        }
    }

    public static RadioStation ToStation(CommunityStationDto station)
    {
        return new RadioStation(station.Name, station.StreamUrl, "MP3", 0, string.Empty, string.Empty, station.Id,
            station.ArtworkUrl);
    }

    public static RadioStation[] ToQueue(CommunityStationDto[] source)
    {
        var queue = new RadioStation[source.Length];
        for (var index = 0; index < source.Length; index++)
        {
            queue[index] = ToStation(source[index]);
        }

        return queue;
    }

    private async Task FetchAsync(CancellationToken token)
    {
        try
        {
            var page = await api.Radio.StationsAsync(token).ConfigureAwait(false);
            if (page?.Items is { } items)
            {
                stations = items;
                liveCount = CountLive(items);
                followedLiveCount = CountFollowedLive(items);
                loaded = true;
                Volatile.Write(ref lastFetchTick, Environment.TickCount64);
                return;
            }

            Volatile.Write(ref retryAfterTick, Environment.TickCount64 + RetryIntervalMilliseconds);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[Radio] community fetch failed");
            Volatile.Write(ref retryAfterTick, Environment.TickCount64 + RetryIntervalMilliseconds);
        }
        finally
        {
            loading = false;
            Volatile.Write(ref fetching, 0);
        }
    }

    private async Task FetchMineAsync(CancellationToken token)
    {
        try
        {
            mine = await api.Radio.MineAsync(token).ConfigureAwait(false);
            mineChecked = true;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[Radio] station lookup failed");
        }
        finally
        {
            Volatile.Write(ref fetchingMine, 0);
        }
    }

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
