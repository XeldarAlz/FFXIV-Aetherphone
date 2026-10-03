using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Apps.Polls;

internal static class PollRules
{
    public const long EndingSoonSeconds = 24L * 60L * 60L;

    private static readonly Comparison<PollDto> OpenOrderComparison = CompareOpen;

    public static bool IsClosed(PollDto poll, long nowUnix) =>
        poll.Closed || (poll.ClosesAtUnix > 0 && nowUnix >= poll.ClosesAtUnix);

    public static bool NeedsVote(PollDto poll, long nowUnix) => poll.MyVote < 0 && !IsClosed(poll, nowUnix);

    public static bool ResultsVisible(PollDto poll, long nowUnix) => poll.MyVote >= 0 || IsClosed(poll, nowUnix);

    public static bool EndingSoon(PollDto poll, long nowUnix) =>
        !IsClosed(poll, nowUnix) && poll.ClosesAtUnix > 0 && poll.ClosesAtUnix - nowUnix < EndingSoonSeconds;

    public static long ClosedMoment(PollDto poll)
    {
        if (poll.ClosedAtUnix > 0)
        {
            return poll.ClosedAtUnix;
        }

        return poll.ClosesAtUnix;
    }

    public static PollDto MarkClosed(PollDto poll, long nowUnix)
    {
        if (poll.Closed && poll.ClosedAtUnix > 0)
        {
            return poll;
        }

        var closedAt = poll.ClosedAtUnix > 0 ? poll.ClosedAtUnix
            : poll.ClosesAtUnix > 0 && poll.ClosesAtUnix <= nowUnix ? poll.ClosesAtUnix
            : nowUnix;
        return poll with { Closed = true, ClosedAtUnix = closedAt };
    }

    public static void Order(PollDto[] source, List<PollDto> target)
    {
        target.Clear();
        target.AddRange(source);
        target.Sort(OpenOrderComparison);
    }

    public static int CompareOpen(PollDto left, PollDto right)
    {
        var leftNeeds = left.MyVote < 0;
        var rightNeeds = right.MyVote < 0;
        if (leftNeeds != rightNeeds)
        {
            return leftNeeds ? -1 : 1;
        }

        var leftDeadline = left.ClosesAtUnix > 0;
        var rightDeadline = right.ClosesAtUnix > 0;
        if (leftDeadline != rightDeadline)
        {
            return leftDeadline ? -1 : 1;
        }

        if (leftDeadline && left.ClosesAtUnix != right.ClosesAtUnix)
        {
            return left.ClosesAtUnix.CompareTo(right.ClosesAtUnix);
        }

        return NewestFirst(left, right);
    }

    public static int NewestFirst(PollDto left, PollDto right)
    {
        var byTime = right.CreatedAtUnix.CompareTo(left.CreatedAtUnix);
        return byTime != 0 ? byTime : string.CompareOrdinal(right.Id, left.Id);
    }

    public static PollDto ApplyVote(PollDto poll, int newVote)
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

    public static int CountAt(PollDto poll, int optionIndex) =>
        optionIndex >= 0 && optionIndex < poll.VoteCounts.Length ? poll.VoteCounts[optionIndex] : 0;

    public static int LeaderCount(PollDto poll, bool[] leaders)
    {
        var best = 0;
        for (var index = 0; index < poll.Options.Length; index++)
        {
            best = Math.Max(best, CountAt(poll, index));
        }

        var count = 0;
        for (var index = 0; index < leaders.Length; index++)
        {
            leaders[index] = best > 0 && index < poll.Options.Length && CountAt(poll, index) == best;
            if (leaders[index])
            {
                count++;
            }
        }

        return count;
    }

    public static void Percentages(PollDto poll, int[] percents)
    {
        Array.Clear(percents);
        var total = 0;
        var optionCount = Math.Min(poll.Options.Length, percents.Length);
        for (var index = 0; index < optionCount; index++)
        {
            total += CountAt(poll, index);
        }

        if (total <= 0)
        {
            return;
        }

        var assigned = 0;
        for (var index = 0; index < optionCount; index++)
        {
            percents[index] = CountAt(poll, index) * 100 / total;
            assigned += percents[index];
        }

        Span<bool> rounded = stackalloc bool[optionCount];
        while (assigned < 100)
        {
            var bestIndex = -1;
            var bestRemainder = 0;
            for (var index = 0; index < optionCount; index++)
            {
                var remainder = CountAt(poll, index) * 100 % total;
                if (!rounded[index] && remainder > bestRemainder)
                {
                    bestRemainder = remainder;
                    bestIndex = index;
                }
            }

            if (bestIndex < 0)
            {
                return;
            }

            rounded[bestIndex] = true;
            percents[bestIndex]++;
            assigned++;
        }
    }
}
