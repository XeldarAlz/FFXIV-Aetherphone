using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Jam;
using Aetherphone.Core.Playback;
using Aetherphone.Core.Runtime;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Songs;

internal sealed class ListeningPresence : IDisposable
{
    private const long TickMilliseconds = 1000;
    private const long VisibleWindowMilliseconds = 5_000;
    private const int OutcomePending = 0;
    private const int OutcomeSucceeded = 1;
    private const int OutcomeFailed = 2;

    private readonly MusicListeningClient client;
    private readonly AethernetSession session;
    private readonly PlaybackHub playback;
    private readonly JamSession jam;
    private readonly Configuration configuration;
    private readonly FrameworkTicker ticker;
    private readonly CancellationTokenSource cancellation = new();
    private ListeningMark mark = ListeningMark.Fresh;
    private ListeningAction inFlightAction;
    private int publishing;
    private int publishOutcome;
    private string? accountToken;
    private long idleSinceMilliseconds;
    private volatile ListeningFriendDto[] friends = Array.Empty<ListeningFriendDto>();
    private int version;
    private int fetchingFriends;
    private int friendsOutcome;
    private int friendsFailures;
    private long nextFriendsFetchMilliseconds;
    private long lastTouchMilliseconds = long.MinValue / 2;

    public ListeningPresence(MusicListeningClient client, AethernetSession session, PlaybackHub playback,
        JamSession jam, Configuration configuration, IFramework framework)
    {
        this.client = client;
        this.session = session;
        this.playback = playback;
        this.jam = jam;
        this.configuration = configuration;
        ticker = new FrameworkTicker(framework, TickMilliseconds, Tick);
    }

    public ListeningFriendDto[] Friends => friends;

    public int Version => Volatile.Read(ref version);

    public void Touch() => lastTouchMilliseconds = Environment.TickCount64;

    private void Tick()
    {
        var now = Environment.TickCount64;
        TrackAccount();
        ConsumePublishOutcome(now);
        Publish(now);
        ConsumeFriendsOutcome(now);
        PollFriends(now);
    }

    private void TrackAccount()
    {
        var token = session.IsSignedIn ? session.Token : null;
        if (string.Equals(token, accountToken, StringComparison.Ordinal))
        {
            return;
        }

        accountToken = token;
        mark = ListeningMark.Fresh;
        nextFriendsFetchMilliseconds = 0;
        friendsFailures = 0;
        if (friends.Length == 0)
        {
            return;
        }

        friends = Array.Empty<ListeningFriendDto>();
        Interlocked.Increment(ref version);
    }

    private void ConsumePublishOutcome(long now)
    {
        var outcome = Interlocked.Exchange(ref publishOutcome, OutcomePending);
        if (outcome == OutcomePending)
        {
            return;
        }

        ListeningCadence.Completed(ref mark, inFlightAction, outcome == OutcomeSucceeded, now);
    }

    private void Publish(long now)
    {
        if (accountToken is null || Volatile.Read(ref publishing) == 1
            || Volatile.Read(ref publishOutcome) != OutcomePending)
        {
            return;
        }

        var state = CurrentState(now);
        var action = ListeningCadence.Decide(mark, state, now);
        if (action == ListeningAction.None)
        {
            return;
        }

        inFlightAction = action;
        Volatile.Write(ref publishing, 1);
        if (action == ListeningAction.Clear)
        {
            _ = Task.Run(() => ClearAsync(cancellation.Token));
            return;
        }

        var request = BuildRequest(state.JamCode);
        ListeningCadence.Sent(ref mark, state, now);
        _ = Task.Run(() => PublishAsync(request, cancellation.Token));
    }

    private ListeningState CurrentState(long now)
    {
        var sharing = configuration.ShareListeningActivity;
        var song = playback.SongActive ? playback.CurrentSong : default;
        if (song.IsEmpty)
        {
            if (idleSinceMilliseconds == 0)
            {
                idleSinceMilliseconds = now;
            }

            return new ListeningState(sharing, string.Empty, false, string.Empty, idleSinceMilliseconds);
        }

        idleSinceMilliseconds = 0;
        return new ListeningState(sharing, song.VideoId, playback.IsPaused, ShareableJamCode(), 0);
    }

    private string ShareableJamCode() => jam.InJam && (jam.IsHost || jam.Discoverable) ? jam.Code : string.Empty;

    private ListeningUpdateRequest BuildRequest(string jamCode)
    {
        var song = playback.CurrentSong;
        var duration = song.DurationSeconds > 0 ? song.DurationSeconds : playback.Duration;
        return new ListeningUpdateRequest(song.VideoId, song.Title, song.Author, song.ThumbnailUrl,
            Math.Max(0d, duration), Math.Max(0d, playback.Position),
            playback.IsPaused, jamCode.Length > 0 ? jamCode : null);
    }

    private async Task PublishAsync(ListeningUpdateRequest request, CancellationToken token)
    {
        var succeeded = false;
        try
        {
            succeeded = await client.PublishAsync(request, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            AepLog.Debug(exception, "[Music] listening publish failed");
        }

        FinishPublish(succeeded);
    }

    private async Task ClearAsync(CancellationToken token)
    {
        var succeeded = false;
        try
        {
            succeeded = await client.ClearAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            AepLog.Debug(exception, "[Music] listening clear failed");
        }

        FinishPublish(succeeded);
    }

    private void FinishPublish(bool succeeded)
    {
        Volatile.Write(ref publishOutcome, succeeded ? OutcomeSucceeded : OutcomeFailed);
        Volatile.Write(ref publishing, 0);
    }

    private void ConsumeFriendsOutcome(long now)
    {
        var outcome = Interlocked.Exchange(ref friendsOutcome, OutcomePending);
        if (outcome == OutcomePending)
        {
            return;
        }

        if (outcome == OutcomeSucceeded)
        {
            friendsFailures = 0;
            nextFriendsFetchMilliseconds = now + ListeningCadence.FriendsIntervalMilliseconds;
            return;
        }

        friendsFailures++;
        nextFriendsFetchMilliseconds = now
            + ListeningCadence.Backoff(ListeningCadence.FriendsIntervalMilliseconds, friendsFailures);
    }

    private void PollFriends(long now)
    {
        if (accountToken is null || now - lastTouchMilliseconds > VisibleWindowMilliseconds
            || now < nextFriendsFetchMilliseconds || Volatile.Read(ref fetchingFriends) == 1
            || Volatile.Read(ref friendsOutcome) != OutcomePending)
        {
            return;
        }

        Volatile.Write(ref fetchingFriends, 1);
        var requestedFor = accountToken;
        _ = Task.Run(() => FetchFriendsAsync(requestedFor, cancellation.Token));
    }

    private async Task FetchFriendsAsync(string requestedFor, CancellationToken token)
    {
        var succeeded = false;
        try
        {
            var page = await client.FriendsAsync(token).ConfigureAwait(false);
            if (page is not null && string.Equals(requestedFor, session.Token, StringComparison.Ordinal))
            {
                friends = ListeningCadence.Usable(page.Friends);
                Interlocked.Increment(ref version);
                succeeded = true;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            AepLog.Debug(exception, "[Music] friends listening fetch failed");
        }

        Volatile.Write(ref friendsOutcome, succeeded ? OutcomeSucceeded : OutcomeFailed);
        Volatile.Write(ref fetchingFriends, 0);
    }

    public void Dispose()
    {
        ticker.Dispose();
        if (mark.Published && session.IsSignedIn)
        {
            _ = ClearOnShutdownAsync(client);
        }

        cancellation.Cancel();
        cancellation.Dispose();
    }

    private static async Task ClearOnShutdownAsync(MusicListeningClient client)
    {
        try
        {
            await client.ClearAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            AepLog.Debug(exception, "[Music] listening clear on shutdown failed");
        }
    }
}
