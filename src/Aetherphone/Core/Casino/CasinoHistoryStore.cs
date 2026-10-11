using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Casino;

internal sealed record VerifiedCasinoRound(CasinoRoundVerifyDto Round, CasinoRoundVerdict Verdict);

internal readonly record struct CasinoTableHand(
    string Key,
    string RoomId,
    long HandIndex,
    string TableName,
    int Currency,
    long Net,
    long SettledAtUnixMs);

internal sealed class CasinoHistoryStore : IDisposable
{
    public const int TableHandLimit = 20;

    private const long RetryAfterAttemptMilliseconds = 30_000;

    private readonly AethernetSession session;
    private readonly CasinoClient casino;
    private readonly StoreWork work = new("CasinoHistory");
    private readonly object verifiedSwapLock = new();

    private volatile CasinoRoundHistoryDto[] rounds = Array.Empty<CasinoRoundHistoryDto>();
    private volatile string? nextCursor;
    private volatile bool loading;
    private volatile bool loaded;
    private volatile bool verifying;
    private volatile Dictionary<string, VerifiedCasinoRound> verified = new(StringComparer.Ordinal);
    private volatile CasinoTableHand[] tableHands = Array.Empty<CasinoTableHand>();
    private int loadFailed;
    private int verifyFailed;
    private long attemptedAtTick;
    private string? lastAccountId;

    public CasinoHistoryStore(AethernetSession session, CasinoClient casino)
    {
        this.session = session;
        this.casino = casino;
    }

    public CasinoRoundHistoryDto[] Rounds => rounds;

    public bool Loading => loading;

    public bool Loaded => loaded;

    public bool HasMore => nextCursor is not null;

    public bool Verifying => verifying;

    public void Invalidate()
    {
        Interlocked.Exchange(ref attemptedAtTick, 0);
        loaded = false;
    }

    public bool TakeLoadFailure()
    {
        return Interlocked.Exchange(ref loadFailed, 0) != 0;
    }

    public bool TakeVerifyFailure()
    {
        return Interlocked.Exchange(ref verifyFailed, 0) != 0;
    }

    public bool TryGetVerified(string roundId, out VerifiedCasinoRound result)
    {
        return verified.TryGetValue(roundId, out result!);
    }

    public void EnsureFresh()
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        var accountId = session.CurrentUser?.Id;
        if (!string.Equals(accountId, lastAccountId, StringComparison.Ordinal))
        {
            lastAccountId = accountId;
            rounds = Array.Empty<CasinoRoundHistoryDto>();
            nextCursor = null;
            loaded = false;
            Interlocked.Exchange(ref attemptedAtTick, 0);
            tableHands = Array.Empty<CasinoTableHand>();
            lock (verifiedSwapLock)
            {
                verified = new Dictionary<string, VerifiedCasinoRound>(StringComparer.Ordinal);
            }
        }

        if (!loaded && !loading && !CoolingDown(Interlocked.Read(ref attemptedAtTick), Environment.TickCount64))
        {
            Fetch(null);
        }
    }

    public void LoadMore()
    {
        if (loading || nextCursor is null
            || CoolingDown(Interlocked.Read(ref attemptedAtTick), Environment.TickCount64))
        {
            return;
        }

        Fetch(nextCursor);
    }

    internal static bool CoolingDown(long attemptedAtTick, long nowTick)
    {
        return attemptedAtTick != 0 && nowTick - attemptedAtTick < RetryAfterAttemptMilliseconds;
    }

    public void RequestVerify(string roundId)
    {
        if (verifying || !session.IsSignedIn || roundId.Length == 0)
        {
            return;
        }

        if (verified.TryGetValue(roundId, out var known) && known.Verdict != CasinoRoundVerdict.Unrevealed)
        {
            return;
        }

        verifying = true;
        work.Run("round verify", async token =>
        {
            var round = await casino.VerifyRoundAsync(roundId, token).ConfigureAwait(false);
            if (round is null)
            {
                Interlocked.Exchange(ref verifyFailed, 1);
                return;
            }

            var result = new VerifiedCasinoRound(round, CasinoVerifier.Verify(round));
            lock (verifiedSwapLock)
            {
                var next = new Dictionary<string, VerifiedCasinoRound>(verified, StringComparer.Ordinal)
                {
                    [roundId] = result,
                };
                verified = next;
            }
        }, () => verifying = false);
    }

    public CasinoTableHand[] TableHands => tableHands;

    public static string TableHandKey(string roomId, long handIndex)
    {
        return string.Concat(roomId, "#", handIndex.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    public void RecordTableHand(string roomId, long handIndex, string tableName, int currency, long net,
        long settledAtUnixMs)
    {
        if (roomId.Length == 0 || handIndex < 0)
        {
            return;
        }

        var held = tableHands;
        var key = TableHandKey(roomId, handIndex);
        for (var index = 0; index < held.Length; index++)
        {
            if (string.Equals(held[index].Key, key, StringComparison.Ordinal))
            {
                return;
            }
        }

        tableHands = Prepend(held, new CasinoTableHand(key, roomId, handIndex, tableName, currency, net,
            settledAtUnixMs));
    }

    internal static CasinoTableHand[] Prepend(CasinoTableHand[] held, in CasinoTableHand hand)
    {
        var count = Math.Min(held.Length + 1, TableHandLimit);
        var next = new CasinoTableHand[count];
        next[0] = hand;
        Array.Copy(held, 0, next, 1, count - 1);
        return next;
    }

    public void RequestTableVerify(in CasinoTableHand hand)
    {
        if (verifying || !session.IsSignedIn)
        {
            return;
        }

        var key = hand.Key;
        if (verified.TryGetValue(key, out var known) && known.Verdict != CasinoRoundVerdict.Unrevealed)
        {
            return;
        }

        var roomId = hand.RoomId;
        var handIndex = hand.HandIndex;
        verifying = true;
        work.Run("table hand verify", async token =>
        {
            var round = await casino.VerifyRoomIndexAsync(roomId, handIndex, token).ConfigureAwait(false);
            if (round is null)
            {
                Interlocked.Exchange(ref verifyFailed, 1);
                return;
            }

            var result = new VerifiedCasinoRound(round, CasinoVerifier.Verify(round));
            lock (verifiedSwapLock)
            {
                verified = new Dictionary<string, VerifiedCasinoRound>(verified, StringComparer.Ordinal)
                {
                    [key] = result,
                };
            }
        }, () => verifying = false);
    }

    internal static CasinoRoundHistoryDto[] MergePage(CasinoRoundHistoryDto[] current,
        CasinoRoundHistoryDto[] incoming)
    {
        if (current.Length == 0)
        {
            return incoming;
        }

        var appendCount = 0;
        var keep = incoming.Length <= 64 ? stackalloc bool[incoming.Length] : new bool[incoming.Length];
        for (var incomingIndex = 0; incomingIndex < incoming.Length; incomingIndex++)
        {
            var duplicate = false;
            for (var currentIndex = 0; currentIndex < current.Length; currentIndex++)
            {
                if (string.Equals(current[currentIndex].RoundId, incoming[incomingIndex].RoundId,
                        StringComparison.Ordinal))
                {
                    duplicate = true;
                    break;
                }
            }

            keep[incomingIndex] = !duplicate;
            if (!duplicate)
            {
                appendCount++;
            }
        }

        if (appendCount == 0)
        {
            return current;
        }

        var merged = new CasinoRoundHistoryDto[current.Length + appendCount];
        current.CopyTo(merged, 0);
        var writeIndex = current.Length;
        for (var incomingIndex = 0; incomingIndex < incoming.Length; incomingIndex++)
        {
            if (keep[incomingIndex])
            {
                merged[writeIndex] = incoming[incomingIndex];
                writeIndex++;
            }
        }

        return merged;
    }

    private void Fetch(string? cursor)
    {
        loading = true;
        Interlocked.Exchange(ref attemptedAtTick, Environment.TickCount64);
        work.Run("history fetch", async token =>
        {
            var page = await casino.RoundsPageAsync(cursor, token).ConfigureAwait(false);
            if (page is null)
            {
                Interlocked.Exchange(ref loadFailed, 1);
                return;
            }

            Interlocked.Exchange(ref attemptedAtTick, 0);
            var items = page.Items ?? Array.Empty<CasinoRoundHistoryDto>();
            rounds = cursor is null ? items : MergePage(rounds, items);
            nextCursor = page.NextCursor;
            loaded = true;
        }, () => loading = false);
    }

    public void Dispose()
    {
        work.Dispose();
    }
}
