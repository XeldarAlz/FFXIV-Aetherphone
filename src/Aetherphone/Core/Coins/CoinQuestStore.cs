using System.Collections.Concurrent;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Coins;

internal sealed record CoinQuestClaim(string QuestId, CoinAwardDto Award);

internal readonly record struct CoinQuestDenial(string QuestId, string Reason, long RetryAtTick);

internal sealed class CoinQuestStore : IDisposable
{
    private const int NotFoundStatus = 404;
    private const long FailureBackoffMilliseconds = 60_000;
    private const long MissingBackoffMilliseconds = 15 * 60_000;
    private const long RolloverGraceSeconds = 5;
    private const long MinimumIntervalMilliseconds = 15_000;
    private const long DeniedRetryMilliseconds = 5 * 60_000;
    private const long StaleRetryMilliseconds = 60_000;

    private readonly AethernetSession session;
    private readonly CoinsClient coins;
    private readonly CoinStore wallet;
    private readonly StoreWork work = new("CoinQuests");
    private readonly ConcurrentQueue<CoinQuestClaim> claimResults = new();

    private volatile CoinQuestBoardDto? board;
    private volatile string? claimingId;
    private volatile bool watching;
    private volatile CoinQuestDenial[] denials = Array.Empty<CoinQuestDenial>();
    private long blockedUntilTick;
    private long loadedAtTick;
    private long rolloverRequestedFor;
    private int fetching;
    private int generation;
    private string? lastAccountId;

    public CoinQuestStore(AethernetSession session, CoinsClient coins, CoinStore wallet)
    {
        this.session = session;
        this.coins = coins;
        this.wallet = wallet;
        lastAccountId = session.CurrentUser?.Id;
        session.Changed += OnSessionChanged;
        wallet.WalletRefreshed += OnWalletRefreshed;
    }

    public CoinQuestBoardDto? Board => board;

    public string ReasonFor(string questId)
    {
        var current = denials;
        for (var index = 0; index < current.Length; index++)
        {
            if (string.Equals(current[index].QuestId, questId, StringComparison.Ordinal))
            {
                return current[index].Reason;
            }
        }

        return string.Empty;
    }

    public void Watch()
    {
        watching = true;
        Refresh(false);
    }

    public void Unwatch()
    {
        watching = false;
    }

    public void EnsureCurrent()
    {
        var current = board;
        if (current is null || current.ResetsAtUnix <= 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (now < current.ResetsAtUnix + RolloverGraceSeconds
            || Interlocked.Read(ref rolloverRequestedFor) == current.ResetsAtUnix)
        {
            return;
        }

        Interlocked.Exchange(ref rolloverRequestedFor, current.ResetsAtUnix);
        denials = Array.Empty<CoinQuestDenial>();
        Refresh(true);
    }

    public CoinQuestClaim? TakeClaimResult()
    {
        return claimResults.TryDequeue(out var claim) ? claim : null;
    }

    private void ClaimNext()
    {
        if (claimingId is not null || !session.IsSignedIn)
        {
            return;
        }

        var current = board;
        if (current is null)
        {
            return;
        }

        var now = Environment.TickCount64;
        var quests = current.Quests;
        for (var index = 0; index < quests.Length; index++)
        {
            var quest = quests[index];
            if (!CoinQuests.IsClaimable(quest) || RetryBlocked(quest.Id, now))
            {
                continue;
            }

            Claim(quest.Id);
            return;
        }
    }

    private bool RetryBlocked(string questId, long now)
    {
        var current = denials;
        for (var index = 0; index < current.Length; index++)
        {
            if (string.Equals(current[index].QuestId, questId, StringComparison.Ordinal))
            {
                return now < current[index].RetryAtTick;
            }
        }

        return false;
    }

    private void Claim(string questId)
    {
        claimingId = questId;
        var ticket = Volatile.Read(ref generation);
        work.Run("claim", async token =>
        {
            var award = await coins.ClaimQuestAsync(questId, token).ConfigureAwait(false);
            if (award is null || Volatile.Read(ref generation) != ticket)
            {
                return;
            }

            Settle(questId, award);
        }, () =>
        {
            claimingId = null;
            ClaimNext();
        });
    }

    private void Settle(string questId, CoinAwardDto award)
    {
        if (award.Granted)
        {
            MarkClaimed(questId);
            ClearDenial(questId);
            claimResults.Enqueue(new CoinQuestClaim(questId, award));
            wallet.AbsorbLocalAward(award.Balance);
            return;
        }

        if (string.Equals(award.Reason, CoinQuests.AlreadyClaimedReason, StringComparison.Ordinal))
        {
            MarkClaimed(questId);
            ClearDenial(questId);
            return;
        }

        if (CoinQuests.IsStale(award.Reason))
        {
            Deny(questId, award.Reason, StaleRetryMilliseconds);
            Refresh(true);
            return;
        }

        var repeated = string.Equals(ReasonFor(questId), award.Reason, StringComparison.Ordinal);
        Deny(questId, award.Reason, DeniedRetryMilliseconds);
        if (!repeated)
        {
            claimResults.Enqueue(new CoinQuestClaim(questId, award));
        }
    }

    private void MarkClaimed(string questId)
    {
        var current = board;
        if (current is not null)
        {
            board = CoinQuests.WithClaimed(current, questId);
        }
    }

    private void Deny(string questId, string reason, long retryAfterMilliseconds)
    {
        var retryAt = Environment.TickCount64 + retryAfterMilliseconds;
        var current = denials;
        for (var index = 0; index < current.Length; index++)
        {
            if (!string.Equals(current[index].QuestId, questId, StringComparison.Ordinal))
            {
                continue;
            }

            var replaced = (CoinQuestDenial[])current.Clone();
            replaced[index] = new CoinQuestDenial(questId, reason, retryAt);
            denials = replaced;
            return;
        }

        var grown = new CoinQuestDenial[current.Length + 1];
        Array.Copy(current, grown, current.Length);
        grown[current.Length] = new CoinQuestDenial(questId, reason, retryAt);
        denials = grown;
    }

    private void ClearDenial(string questId)
    {
        var current = denials;
        var keep = 0;
        for (var index = 0; index < current.Length; index++)
        {
            if (!string.Equals(current[index].QuestId, questId, StringComparison.Ordinal))
            {
                keep++;
            }
        }

        if (keep == current.Length)
        {
            return;
        }

        var trimmed = new CoinQuestDenial[keep];
        var write = 0;
        for (var index = 0; index < current.Length; index++)
        {
            if (string.Equals(current[index].QuestId, questId, StringComparison.Ordinal))
            {
                continue;
            }

            trimmed[write] = current[index];
            write++;
        }

        denials = trimmed;
    }

    private void OnWalletRefreshed()
    {
        if (!watching)
        {
            return;
        }

        Refresh(false);
    }

    private void Refresh(bool force)
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        var now = Environment.TickCount64;
        if (now < Interlocked.Read(ref blockedUntilTick))
        {
            return;
        }

        var loadedAt = Interlocked.Read(ref loadedAtTick);
        if (!force && loadedAt != 0 && now - loadedAt < MinimumIntervalMilliseconds)
        {
            return;
        }

        if (Interlocked.Exchange(ref fetching, 1) != 0)
        {
            return;
        }

        var ticket = Volatile.Read(ref generation);
        work.Run("board", async token =>
        {
            var status = 0;
            var fresh = await coins.QuestsAsync(token, failure => status = failure.StatusCode)
                .ConfigureAwait(false);
            if (Volatile.Read(ref generation) != ticket)
            {
                return;
            }

            if (fresh is null)
            {
                Backoff(status);
                return;
            }

            Interlocked.Exchange(ref blockedUntilTick, 0);
            Interlocked.Exchange(ref loadedAtTick, Environment.TickCount64);
            board = fresh.Quests is null ? null : fresh;
        }, () =>
        {
            Interlocked.Exchange(ref fetching, 0);
            ClaimNext();
        });
    }

    private void Backoff(int status)
    {
        var missing = status == NotFoundStatus;
        if (missing)
        {
            board = null;
        }

        var delay = missing ? MissingBackoffMilliseconds : FailureBackoffMilliseconds;
        Interlocked.Exchange(ref blockedUntilTick, Environment.TickCount64 + delay);
        Interlocked.Exchange(ref rolloverRequestedFor, 0);
    }

    private void OnSessionChanged()
    {
        var accountId = session.CurrentUser?.Id;
        if (string.Equals(accountId, lastAccountId, StringComparison.Ordinal))
        {
            return;
        }

        lastAccountId = accountId;
        Interlocked.Increment(ref generation);
        board = null;
        claimingId = null;
        denials = Array.Empty<CoinQuestDenial>();
        while (claimResults.TryDequeue(out _))
        {
        }

        Interlocked.Exchange(ref blockedUntilTick, 0);
        Interlocked.Exchange(ref rolloverRequestedFor, 0);
        Interlocked.Exchange(ref loadedAtTick, 0);
        if (watching)
        {
            Refresh(true);
        }
    }

    public void Dispose()
    {
        session.Changed -= OnSessionChanged;
        wallet.WalletRefreshed -= OnWalletRefreshed;
        work.Dispose();
    }
}
