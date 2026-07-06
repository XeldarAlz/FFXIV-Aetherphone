using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Polls;

internal sealed class PollsStore : IDisposable
{
    private static readonly TimeSpan BackgroundRefreshInterval = TimeSpan.FromMinutes(5);

    private readonly AethernetSession session;
    private readonly AethernetClient client;
    private readonly CancellationTokenSource cancellation = new();

    private volatile PollDto[] polls = Array.Empty<PollDto>();
    private volatile bool loading;
    private volatile bool loadedOnce;
    private DateTime lastBackgroundRefreshUtc = DateTime.MinValue;

    public PollsStore(AethernetSession session, AethernetClient client)
    {
        this.session = session;
        this.client = client;
        Plugin.Framework.Update += OnFrameworkUpdate;
    }

    public bool IsSignedIn => session.IsSignedIn;

    public PollDto[] Polls => polls;

    public bool Loading => loading;

    public bool LoadedOnce => loadedOnce;

    public int UnvotedCount
    {
        get
        {
            if (!session.IsSignedIn)
            {
                return 0;
            }

            var snapshot = polls;
            var count = 0;
            for (var index = 0; index < snapshot.Length; index++)
            {
                if (!snapshot[index].Closed && snapshot[index].MyVote < 0)
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
        RunGuarded("polls refresh", async token =>
        {
            var page = await client.PollsAsync(token).ConfigureAwait(false);
            if (page is not null)
            {
                polls = page.Items;
                loadedOnce = true;
            }
        }, () => loading = false);
    }

    public void Vote(PollDto poll, int optionIndex)
    {
        if (poll.Closed || optionIndex < 0 || optionIndex >= poll.Options.Length)
        {
            return;
        }

        var target = poll.MyVote == optionIndex ? -1 : optionIndex;
        ReplacePoll(ApplyVote(poll, target));
        RunGuarded("vote", async token =>
        {
            var result = target < 0
                ? await client.ClearVoteAsync(poll.Id, token).ConfigureAwait(false)
                : await client.VoteAsync(poll.Id, target, token).ConfigureAwait(false);
            if (result is not null)
            {
                ReplacePoll(result);
            }
        });
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (now - lastBackgroundRefreshUtc < BackgroundRefreshInterval)
        {
            return;
        }

        lastBackgroundRefreshUtc = now;
        Refresh();
    }

    private static PollDto ApplyVote(PollDto poll, int newVote)
    {
        var counts = (int[])poll.VoteCounts.Clone();
        if (poll.MyVote >= 0 && poll.MyVote < counts.Length && counts[poll.MyVote] > 0)
        {
            counts[poll.MyVote]--;
        }

        if (newVote >= 0 && newVote < counts.Length)
        {
            counts[newVote]++;
        }

        var total = 0;
        for (var index = 0; index < counts.Length; index++)
        {
            total += counts[index];
        }

        return poll with { VoteCounts = counts, TotalVotes = total, MyVote = newVote };
    }

    private void ReplacePoll(PollDto updated)
    {
        var snapshot = polls;
        var next = new PollDto[snapshot.Length];
        var changed = false;
        for (var index = 0; index < snapshot.Length; index++)
        {
            if (snapshot[index].Id == updated.Id)
            {
                next[index] = updated;
                changed = true;
            }
            else
            {
                next[index] = snapshot[index];
            }
        }

        if (changed)
        {
            polls = next;
        }
    }

    private void RunGuarded(string operation, Func<CancellationToken, Task> action, Action? cleanup = null)
    {
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await action(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"[Polls] {operation} failed: {exception.Message}");
            }
            finally
            {
                cleanup?.Invoke();
            }
        });
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= OnFrameworkUpdate;
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
