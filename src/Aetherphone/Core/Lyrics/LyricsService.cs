using System.Runtime.InteropServices;
using Aetherphone.Core.Net;
using Aetherphone.Core.Songs;

namespace Aetherphone.Core.Lyrics;

internal sealed class LyricsService : IDisposable
{
    public const int MemoryCapacity = 64;
    private const int MaxPending = 8;
    private const string DiskKeyPrefix = "lyrics:";
    private static readonly TimeSpan RequestSpacing = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan BaseBackoff = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan FailedRetryDelay = TimeSpan.FromMinutes(1);
    private readonly LrcLibClient client;
    private readonly DiskCache disk;
    private readonly CancellationTokenSource cancellation = new();
    private readonly object sync = new();
    private readonly Dictionary<string, LinkedListNode<Entry>> entries = new(StringComparer.Ordinal);
    private readonly LinkedList<Entry> recency = new();
    private readonly List<Song> pending = new(MaxPending + 1);
    private bool working;
    private int consecutiveFailures;
    private long backoffUntilMilliseconds;
    private long nextRequestAtMilliseconds;

    public LyricsService(LrcLibClient client, DiskCache disk)
    {
        this.client = client;
        this.disk = disk;
    }

    public LyricsState Get(in Song song)
    {
        if (song.IsEmpty)
        {
            return LyricsState.NotFound;
        }

        lock (sync)
        {
            if (entries.TryGetValue(song.VideoId, out var node))
            {
                if (node != recency.First)
                {
                    recency.Remove(node);
                    recency.AddFirst(node);
                }

                var entry = node.Value;
                if (entry.State.Status != LyricsStatus.Failed || Environment.TickCount64 < entry.RetryAtMilliseconds)
                {
                    return entry.State;
                }

                entry.State = LyricsState.Loading;
                Enqueue(song);
                return entry.State;
            }

            Insert(song.VideoId, LyricsState.Loading, 0);
            Enqueue(song);
            return LyricsState.Loading;
        }
    }

    public void Retry(in Song song)
    {
        if (song.IsEmpty)
        {
            return;
        }

        lock (sync)
        {
            if (entries.TryGetValue(song.VideoId, out var node) && node.Value.State.Status == LyricsStatus.Failed)
            {
                node.Value.RetryAtMilliseconds = 0;
            }
        }
    }

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
    }

    private void Insert(string videoId, LyricsState state, long retryAtMilliseconds)
    {
        if (entries.TryGetValue(videoId, out var existing))
        {
            existing.Value.State = state;
            existing.Value.RetryAtMilliseconds = retryAtMilliseconds;
            return;
        }

        var node = recency.AddFirst(new Entry(videoId, state, retryAtMilliseconds));
        entries[videoId] = node;
        while (recency.Count > MemoryCapacity)
        {
            var oldest = recency.Last!;
            recency.RemoveLast();
            entries.Remove(oldest.Value.VideoId);
        }
    }

    private void Enqueue(in Song song)
    {
        pending.Add(song);
        if (pending.Count > MaxPending)
        {
            var dropped = pending[0].VideoId;
            pending.RemoveAt(0);
            if (entries.TryGetValue(dropped, out var node) && node.Value.State.Status == LyricsStatus.Loading)
            {
                recency.Remove(node);
                entries.Remove(dropped);
            }
        }

        if (working)
        {
            return;
        }

        working = true;
        _ = Task.Run(DrainAsync);
    }

    private async Task DrainAsync()
    {
        CancellationToken token;
        try
        {
            token = cancellation.Token;
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        while (true)
        {
            Song song;
            lock (sync)
            {
                if (pending.Count == 0 || token.IsCancellationRequested)
                {
                    working = false;
                    return;
                }

                var last = pending.Count - 1;
                song = pending[last];
                pending.RemoveAt(last);
            }

            var state = LyricsState.Failed;
            try
            {
                state = await ResolveAsync(song, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                lock (sync)
                {
                    working = false;
                }

                return;
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, $"Lyrics lookup failed for {song.VideoId}");
            }

            lock (sync)
            {
                var retryAt = state.Status == LyricsStatus.Failed
                    ? Environment.TickCount64 + (long)Math.Max(FailedRetryDelay.TotalMilliseconds,
                        backoffUntilMilliseconds - Environment.TickCount64)
                    : 0;
                Insert(song.VideoId, state, retryAt);
            }
        }
    }

    private async Task<LyricsState> ResolveAsync(Song song, CancellationToken token)
    {
        var diskKey = string.Concat(DiskKeyPrefix, song.VideoId);
        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var cached = disk.Get(diskKey, LyricsRecord.FoundLifetime);
        if (cached is not null && LyricsRecord.TryDecode(cached, out var stored) && stored.IsFresh(nowUnix))
        {
            return stored.ToState();
        }

        var query = LyricsQuery.FromYoutube(song.Title, song.Author, song.DurationSeconds);
        if (query.IsEmpty)
        {
            return LyricsState.NotFound;
        }

        var outcome = await FetchAsync(query, token).ConfigureAwait(false);
        if (outcome.Failed)
        {
            return LyricsState.Failed;
        }

        var record = outcome.Found
            ? LyricsRecord.FromTrack(outcome.Track, nowUnix)
            : new LyricsRecord(LyricsRecordKind.Missing, string.Empty, nowUnix);
        disk.Set(diskKey, record.Encode());
        return record.ToState();
    }

    private async Task<FetchOutcome> FetchAsync(LyricsQuery query, CancellationToken token)
    {
        var primary = query.Candidates[0];
        var collected = new List<LrcLibTrack>(24);
        if (primary.HasArtist)
        {
            await WaitTurnAsync(token).ConfigureAwait(false);
            var exact = await client.GetAsync(primary.Artist, primary.Track, query.DurationSeconds, token)
                .ConfigureAwait(false);
            if (Settle(exact))
            {
                return FetchOutcome.Failure;
            }

            if (TryPick(exact.Tracks, collected, query, out var exactMatch) && exactMatch.HasSynced)
            {
                return FetchOutcome.Match(exactMatch);
            }
        }

        var searchText = primary.HasArtist ? string.Concat(primary.Artist, " ", primary.Track) : primary.Track;
        await WaitTurnAsync(token).ConfigureAwait(false);
        var search = await client.SearchAsync(searchText, token).ConfigureAwait(false);
        if (Settle(search))
        {
            return BestSoFar(collected, query);
        }

        if (TryPick(search.Tracks, collected, query, out var searchMatch))
        {
            return FetchOutcome.Match(searchMatch);
        }

        if (!primary.HasArtist)
        {
            return FetchOutcome.Miss;
        }

        await WaitTurnAsync(token).ConfigureAwait(false);
        var trackOnly = await client.SearchAsync(primary.Track, token).ConfigureAwait(false);
        if (Settle(trackOnly))
        {
            return BestSoFar(collected, query);
        }

        return TryPick(trackOnly.Tracks, collected, query, out var trackOnlyMatch)
            ? FetchOutcome.Match(trackOnlyMatch)
            : FetchOutcome.Miss;
    }

    private static FetchOutcome BestSoFar(List<LrcLibTrack> collected, in LyricsQuery query)
    {
        var best = LyricsMatcher.PickBest(CollectionsMarshal.AsSpan(collected), query);
        return best >= 0 ? FetchOutcome.Match(collected[best]) : FetchOutcome.Failure;
    }

    private static bool TryPick(LrcLibTrack[] fresh, List<LrcLibTrack> collected, in LyricsQuery query,
        out LrcLibTrack match)
    {
        for (var index = 0; index < fresh.Length; index++)
        {
            collected.Add(fresh[index]);
        }

        var best = LyricsMatcher.PickBest(CollectionsMarshal.AsSpan(collected), query);
        match = best >= 0 ? collected[best] : default;
        return best >= 0;
    }

    private bool Settle(in LrcLibResponse response)
    {
        lock (sync)
        {
            if (!response.ShouldBackOff)
            {
                consecutiveFailures = 0;
                return false;
            }

            consecutiveFailures++;
            var exponent = Math.Min(consecutiveFailures - 1, 6);
            var delay = Math.Min(BaseBackoff.TotalMilliseconds * (1 << exponent), MaxBackoff.TotalMilliseconds);
            backoffUntilMilliseconds = Environment.TickCount64 + (long)delay;
            return true;
        }
    }

    private async Task WaitTurnAsync(CancellationToken token)
    {
        long waitUntil;
        lock (sync)
        {
            waitUntil = Math.Max(backoffUntilMilliseconds, nextRequestAtMilliseconds);
        }

        var wait = waitUntil - Environment.TickCount64;
        if (wait > 0)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(wait), token).ConfigureAwait(false);
        }

        lock (sync)
        {
            nextRequestAtMilliseconds = Environment.TickCount64 + (long)RequestSpacing.TotalMilliseconds;
        }
    }

    private sealed class Entry
    {
        public readonly string VideoId;
        public LyricsState State;
        public long RetryAtMilliseconds;

        public Entry(string videoId, LyricsState state, long retryAtMilliseconds)
        {
            VideoId = videoId;
            State = state;
            RetryAtMilliseconds = retryAtMilliseconds;
        }
    }

    private readonly struct FetchOutcome
    {
        public static readonly FetchOutcome Failure = new(true, false, default);
        public static readonly FetchOutcome Miss = new(false, false, default);

        public readonly bool Failed;
        public readonly bool Found;
        public readonly LrcLibTrack Track;

        private FetchOutcome(bool failed, bool found, LrcLibTrack track)
        {
            Failed = failed;
            Found = found;
            Track = track;
        }

        public static FetchOutcome Match(in LrcLibTrack track)
        {
            return new FetchOutcome(false, true, track);
        }
    }
}
