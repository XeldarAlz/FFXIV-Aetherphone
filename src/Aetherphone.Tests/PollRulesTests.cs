using System.Text.Json;
using Aetherphone.Apps.Polls;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Xunit;

namespace Aetherphone.Tests;

public sealed class PollRulesTests
{
    private const long Now = 1_800_000_000L;
    private const long Hour = 3600L;

    private const string OldServerPoll =
        "{\"id\":\"p1\",\"question\":\"Q\",\"options\":[\"A\",\"B\"],\"translations\":[],\"voteCounts\":[3,1],"
        + "\"totalVotes\":4,\"myVote\":-1,\"createdAtUnix\":100,\"closed\":false}";

    private const string NewServerPoll =
        "{\"id\":\"p1\",\"question\":\"Q\",\"options\":[\"A\",\"B\"],\"translations\":[],\"voteCounts\":[3,1],"
        + "\"totalVotes\":4,\"myVote\":-1,\"createdAtUnix\":100,\"closed\":true,\"closesAtUnix\":500,"
        + "\"closedAtUnix\":501}";

    private static PollDto Poll(string id, int myVote = -1, long created = 0, bool closed = false, long closesAt = 0,
        long closedAt = 0, params int[] counts)
    {
        var tally = counts.Length == 0 ? new[] { 0, 0 } : counts;
        var options = new string[tally.Length];
        var total = 0;
        for (var index = 0; index < tally.Length; index++)
        {
            options[index] = "option" + index;
            total += tally[index];
        }

        return new PollDto(id, "question", options, Array.Empty<PollTranslationDto>(), tally, total, myVote, created,
            closed, closesAt, closedAt);
    }

    [Fact]
    public void AnOldServerPollDeserializesWithoutADeadline()
    {
        var poll = JsonSerializer.Deserialize(OldServerPoll, AethernetJsonContext.Default.PollDto);

        Assert.NotNull(poll);
        Assert.Equal(0, poll.ClosesAtUnix);
        Assert.Equal(0, poll.ClosedAtUnix);
        Assert.False(PollRules.IsClosed(poll, Now));
    }

    [Fact]
    public void ANewServerPollCarriesItsDeadlineFields()
    {
        var poll = JsonSerializer.Deserialize(NewServerPoll, AethernetJsonContext.Default.PollDto);

        Assert.NotNull(poll);
        Assert.Equal(500, poll.ClosesAtUnix);
        Assert.Equal(501, poll.ClosedAtUnix);
        Assert.Equal(501, PollRules.ClosedMoment(poll));
    }

    [Fact]
    public void APollClosesLocallyTheMomentItsDeadlinePasses()
    {
        var poll = Poll("p", closesAt: Now);

        Assert.False(PollRules.IsClosed(poll, Now - 1));
        Assert.True(PollRules.IsClosed(poll, Now));
        Assert.False(PollRules.NeedsVote(poll, Now));
        Assert.True(PollRules.ResultsVisible(poll, Now));
    }

    [Fact]
    public void EndingSoonStartsInsideTheLastDay()
    {
        var poll = Poll("p", closesAt: Now + 24 * Hour);

        Assert.False(PollRules.EndingSoon(poll, Now));
        Assert.True(PollRules.EndingSoon(poll, Now + 1));
    }

    [Fact]
    public void OpenPollsPutUnvotedFirstThenSoonestDeadlineThenNewest()
    {
        var voted = Poll("voted", myVote: 0, created: 50, closesAt: Now + Hour);
        var openEnded = Poll("open-ended", created: 90);
        var later = Poll("later", created: 10, closesAt: Now + 5 * Hour);
        var sooner = Poll("sooner", created: 20, closesAt: Now + 2 * Hour);
        var newer = Poll("newer", created: 95);
        var open = new List<PollDto>();

        PollRules.Order(new[] { voted, openEnded, later, sooner, newer }, open);

        Assert.Equal(new[] { "sooner", "later", "newer", "open-ended", "voted" }, Ids(open));
    }

    [Fact]
    public void MovingAVoteShiftsOneCountAcross()
    {
        var poll = Poll("p", myVote: 0, counts: new[] { 3, 1 });

        var moved = PollRules.ApplyVote(poll, 1);
        var undone = PollRules.ApplyVote(moved, -1);

        Assert.Equal(new[] { 2, 2 }, moved.VoteCounts);
        Assert.Equal(1, moved.MyVote);
        Assert.Equal(4, moved.TotalVotes);
        Assert.Equal(new[] { 2, 1 }, undone.VoteCounts);
        Assert.Equal(3, undone.TotalVotes);
        Assert.Equal(new[] { 3, 1 }, poll.VoteCounts);
    }

    [Fact]
    public void PercentagesAlwaysAddUpToOneHundred()
    {
        var poll = Poll("p", counts: new[] { 1, 1, 1 });
        var percents = new int[3];

        PollRules.Percentages(poll, percents);

        Assert.Equal(100, percents[0] + percents[1] + percents[2]);
        Assert.Equal(new[] { 34, 33, 33 }, percents);
    }

    [Fact]
    public void PercentagesStayZeroWithoutVotes()
    {
        var poll = Poll("p", counts: new[] { 0, 0 });
        var percents = new[] { 7, 9 };

        PollRules.Percentages(poll, percents);

        Assert.Equal(new[] { 0, 0 }, percents);
    }

    [Fact]
    public void EveryTiedOptionLeads()
    {
        var poll = Poll("p", counts: new[] { 4, 1, 4 });
        var leaders = new bool[3];

        var count = PollRules.LeaderCount(poll, leaders);

        Assert.Equal(2, count);
        Assert.Equal(new[] { true, false, true }, leaders);
    }

    [Fact]
    public void NobodyLeadsAPollWithoutVotes()
    {
        var poll = Poll("p", counts: new[] { 0, 0 });
        var leaders = new bool[2];

        Assert.Equal(0, PollRules.LeaderCount(poll, leaders));
    }

    [Fact]
    public void MarkingClosedPrefersTheDeadlineOverNow()
    {
        var expired = PollRules.MarkClosed(Poll("p", closesAt: Now - Hour), Now);
        var refused = PollRules.MarkClosed(Poll("q"), Now);

        Assert.True(expired.Closed);
        Assert.Equal(Now - Hour, expired.ClosedAtUnix);
        Assert.Equal(Now, refused.ClosedAtUnix);
    }

    private static string[] Ids(List<PollDto> polls)
    {
        var ids = new string[polls.Count];
        for (var index = 0; index < polls.Count; index++)
        {
            ids[index] = polls[index].Id;
        }

        return ids;
    }
}
