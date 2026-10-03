using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Net;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Polls;

internal sealed class PollsStore : IDisposable
{
    // poll.ping announces every create/close/reopen, so the timer is only the fallback
    // for a dropped socket; anything shorter just re-downloads an unchanged page.
    private static readonly TimeSpan BackgroundRefreshInterval = TimeSpan.FromMinutes(45);

    private readonly AethernetSession session;
    private readonly PollsClient client;
    private readonly AppGate gate;
    private readonly RealtimeSignalBus signals;
    private readonly StoreWork work = new StoreWork("Polls");
    private readonly Lock voteGate = new();
    private readonly Dictionary<string, VoteTicket> tickets = new(StringComparer.Ordinal);
    private readonly FailureSlot listFailure = new();

    private volatile PollDto[] polls = Array.Empty<PollDto>();
    private volatile string? pollsCursor;
    private volatile bool loadingMore;
    private volatile bool pagedDeeper;
    private volatile bool loading;
    private volatile bool loadedOnce;
    private volatile bool listFailed;
    private volatile bool pingRefreshRequested;
    private volatile PollVoteFailure? voteFailure;
    private int voteSequence;
    private DateTime lastBackgroundRefreshUtc = DateTime.MinValue;

    public PollsStore(AethernetSession session, PollsClient client, AppGate gate, RealtimeSignalBus signals)
    {
        this.session = session;
        this.client = client;
        this.gate = gate;
        this.signals = signals;
        signals.PollsPinged += OnPollsPinged;
        Plugin.Framework.Update += OnFrameworkUpdate;
    }

    private void OnPollsPinged()
    {
        pingRefreshRequested = true;
    }

    public bool IsSignedIn => session.IsSignedIn;

    public PollDto[] Polls => polls;

    public bool Loading => loading;

    public bool LoadingMore => loadingMore;

    public bool HasMore => pollsCursor is not null;

    public bool LoadedOnce => loadedOnce;

    public bool ListFailed => listFailed;

    public string ListFailureText => listFailure.Text();

    public PollVoteFailure? VoteFailure => voteFailure;

    public int UnvotedCount
    {
        get
        {
            if (!session.IsSignedIn)
            {
                return 0;
            }

            var snapshot = polls;
            var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var count = 0;
            for (var index = 0; index < snapshot.Length; index++)
            {
                if (PollRules.NeedsVote(snapshot[index], nowUnix))
                {
                    count++;
                }
            }

            return count;
        }
    }

    public void Refresh()
    {
        if (!session.IsSignedIn || loading)
        {
            return;
        }

        loading = true;
        var lang = Loc.Current.Code;
        work.Run("polls refresh", async token =>
        {
            var failure = AepFailure.None;
            var page = await client.ListAsync(null, lang, token, received => failure = received)
                .ConfigureAwait(false);
            if (page is null)
            {
                listFailure.Set(failure);
                listFailed = true;
                return;
            }

            listFailure.Clear();
            listFailed = false;
            lock (voteGate)
            {
                var items = KeepPendingVotes(page.Items);
                if (pagedDeeper)
                {
                    polls = IdentifiedMerge.MergeById(DropDeparted(polls, items), items, PollRules.NewestFirst);
                }
                else
                {
                    polls = items;
                    pollsCursor = page.NextCursor;
                }
            }

            loadedOnce = true;
        }, () => loading = false);
    }

    public void LoadMore()
    {
        var cursor = pollsCursor;
        if (!session.IsSignedIn || cursor is null || loadingMore || loading)
        {
            return;
        }

        loadingMore = true;
        pagedDeeper = true;
        var lang = Loc.Current.Code;
        work.Run("polls more", async token =>
        {
            var page = await client.ListAsync(cursor, lang, token).ConfigureAwait(false);
            if (page is null)
            {
                return;
            }

            lock (voteGate)
            {
                polls = IdentifiedMerge.MergeById(polls, KeepPendingVotes(page.Items), PollRules.NewestFirst);
                pollsCursor = page.NextCursor;
            }
        }, () => loadingMore = false);
    }

    public bool Vote(PollDto poll, int optionIndex)
    {
        if (optionIndex < 0 || optionIndex >= poll.Options.Length || poll.MyVote == optionIndex)
        {
            return false;
        }

        return Submit(poll, optionIndex);
    }

    public bool ClearVote(PollDto poll)
    {
        if (poll.MyVote < 0)
        {
            return false;
        }

        return Submit(poll, -1);
    }

    private bool Submit(PollDto poll, int target)
    {
        if (PollRules.IsClosed(poll, DateTimeOffset.UtcNow.ToUnixTimeSeconds()))
        {
            voteFailure = new PollVoteFailure(poll.Id, Loc.T(L.Failure.PollClosed), DateTime.UtcNow);
            return false;
        }

        int sequence;
        lock (voteGate)
        {
            var current = Find(poll.Id) ?? poll;
            var baseline = tickets.TryGetValue(poll.Id, out var pending) ? pending.Baseline : current;
            sequence = ++voteSequence;
            tickets[poll.Id] = new VoteTicket(sequence, baseline);
            polls = CopyOnWrite.Replace(polls, PollRules.ApplyVote(current, target));
        }

        ClearFailureFor(poll.Id);
        var lang = Loc.Current.Code;
        work.Run("vote", async token =>
        {
            var failure = AepFailure.None;
            var result = target < 0
                ? await client.ClearVoteAsync(poll.Id, lang, token, received => failure = received)
                    .ConfigureAwait(false)
                : await client.VoteAsync(poll.Id, target, lang, token, received => failure = received)
                    .ConfigureAwait(false);
            Settle(poll.Id, sequence, result, failure);
        });
        return true;
    }

    private void Settle(string pollId, int sequence, PollDto? result, AepFailure failure)
    {
        var closedByServer = failure.Code == FailureCodes.PollClosed;
        lock (voteGate)
        {
            if (!tickets.TryGetValue(pollId, out var ticket) || ticket.Sequence != sequence)
            {
                return;
            }

            tickets.Remove(pollId);
            if (result is not null)
            {
                polls = CopyOnWrite.Replace(polls, result);
                return;
            }

            var restored = closedByServer
                ? PollRules.MarkClosed(ticket.Baseline, DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                : ticket.Baseline;
            polls = CopyOnWrite.Replace(polls, restored);
        }

        var reason = FailureText.Resolve(failure);
        voteFailure = new PollVoteFailure(pollId, reason.Length > 0 ? reason : Loc.T(L.Polls.VoteFailed),
            DateTime.UtcNow);
        pingRefreshRequested = true;
    }

    private void ClearFailureFor(string pollId)
    {
        var current = voteFailure;
        if (current is not null && current.PollId == pollId)
        {
            voteFailure = null;
        }
    }

    private PollDto? Find(string pollId)
    {
        var snapshot = polls;
        for (var index = 0; index < snapshot.Length; index++)
        {
            if (snapshot[index].Id == pollId)
            {
                return snapshot[index];
            }
        }

        return null;
    }

    private PollDto[] KeepPendingVotes(PollDto[] incoming)
    {
        if (tickets.Count == 0)
        {
            return incoming;
        }

        var merged = incoming;
        for (var index = 0; index < incoming.Length; index++)
        {
            if (!tickets.ContainsKey(incoming[index].Id) || Find(incoming[index].Id) is not { } local)
            {
                continue;
            }

            if (ReferenceEquals(merged, incoming))
            {
                merged = (PollDto[])incoming.Clone();
            }

            merged[index] = local;
        }

        return merged;
    }

    private static PollDto[] DropDeparted(PollDto[] existing, PollDto[] incoming)
    {
        var oldestIncoming = OldestCreated(incoming);
        var kept = existing;
        for (var index = 0; index < existing.Length; index++)
        {
            if (Departed(existing[index], incoming, oldestIncoming))
            {
                kept = CopyOnWrite.RemoveById(kept, existing[index].Id);
            }
        }

        return kept;
    }

    private static bool Departed(PollDto poll, PollDto[] incoming, long oldestIncoming)
    {
        for (var index = 0; index < incoming.Length; index++)
        {
            if (incoming[index].Id == poll.Id)
            {
                return false;
            }
        }

        return poll.CreatedAtUnix >= oldestIncoming;
    }

    private static long OldestCreated(PollDto[] polls)
    {
        if (polls.Length == 0)
        {
            return long.MaxValue;
        }

        var oldest = long.MaxValue;
        for (var index = 0; index < polls.Length; index++)
        {
            oldest = Math.Min(oldest, polls[index].CreatedAtUnix);
        }

        return oldest;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (!session.IsSignedIn || !gate.Open)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (!pingRefreshRequested && now - lastBackgroundRefreshUtc < BackgroundRefreshInterval)
        {
            return;
        }

        pingRefreshRequested = false;
        lastBackgroundRefreshUtc = now;
        Refresh();
    }

    public void Dispose()
    {
        signals.PollsPinged -= OnPollsPinged;
        Plugin.Framework.Update -= OnFrameworkUpdate;
        work.Dispose();
    }

    private readonly record struct VoteTicket(int Sequence, PollDto Baseline);
}

internal sealed record PollVoteFailure(string PollId, string Message, DateTime AtUtc);
