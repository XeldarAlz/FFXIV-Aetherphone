using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Polls;

internal enum PollStatus : byte
{
    Open,
    Counting,
    EndingSoon,
    Ended,
}

internal sealed class PollText
{
    private const long SecondsPerMinute = 60L;
    private const long SecondsPerHour = 3600L;
    private const long SecondsPerDay = 86400L;

    public PollDto? Source;
    public string LangCode = string.Empty;
    public string Question = string.Empty;
    public string[] Options = Array.Empty<string>();
    public string[] Percents = Array.Empty<string>();
    public string[] CountLabels = Array.Empty<string>();
    public string[] TooltipIds = Array.Empty<string>();
    public string[] PressIds = Array.Empty<string>();
    public string[] LeaderIds = Array.Empty<string>();
    public float[] Fractions = Array.Empty<float>();
    public bool[] Leaders = Array.Empty<bool>();
    public int LeaderCount;
    public string VotesLabel = string.Empty;
    public string StatusLabel = string.Empty;
    public string FooterLabel = string.Empty;
    public PollStatus Status;
    public bool Closed;

    private int[] percentValues = Array.Empty<int>();
    private long timeKey = long.MinValue;

    public void Sync(PollDto poll, long nowUnix)
    {
        var code = Loc.Current.Code;
        var contentChanged = !ReferenceEquals(Source, poll) || LangCode != code;
        if (contentChanged)
        {
            Source = poll;
            LangCode = code;
            RebuildContent(poll, code);
        }

        var key = TimeKey(poll, nowUnix);
        if (!contentChanged && key == timeKey)
        {
            return;
        }

        timeKey = key;
        RebuildTime(poll, nowUnix);
    }

    private static long TimeKey(PollDto poll, long nowUnix)
    {
        var closed = PollRules.IsClosed(poll, nowUnix);
        var remaining = poll.ClosesAtUnix - nowUnix;
        var perSecond = !closed && poll.ClosesAtUnix > 0 && remaining < SecondsPerMinute;
        var bucket = perSecond ? nowUnix : nowUnix / SecondsPerMinute * SecondsPerMinute;
        return bucket * 2 + (closed ? 1 : 0);
    }

    private void RebuildContent(PollDto poll, string code)
    {
        var optionCount = poll.Options.Length;
        if (Percents.Length != optionCount)
        {
            Percents = new string[optionCount];
            CountLabels = new string[optionCount];
            TooltipIds = new string[optionCount];
            PressIds = new string[optionCount];
            LeaderIds = new string[optionCount];
            Fractions = new float[optionCount];
            Leaders = new bool[optionCount];
            percentValues = new int[optionCount];
        }

        Question = poll.Question;
        Options = poll.Options;
        ApplyTranslation(poll, code);

        PollRules.Percentages(poll, percentValues);
        LeaderCount = PollRules.LeaderCount(poll, Leaders);
        for (var index = 0; index < optionCount; index++)
        {
            var count = PollRules.CountAt(poll, index);
            Percents[index] = Loc.T(L.Polls.Percent, percentValues[index]);
            CountLabels[index] = Loc.Plural(L.Polls.Votes, count);
            TooltipIds[index] = string.Concat("polls.count.", poll.Id, ".", index.ToString(Loc.Culture));
            PressIds[index] = string.Concat("polls.option.", poll.Id, ".", index.ToString(Loc.Culture));
            LeaderIds[index] = string.Concat("polls.leader.", poll.Id, ".", index.ToString(Loc.Culture));
            Fractions[index] = poll.TotalVotes > 0 ? (float)count / poll.TotalVotes : 0f;
        }

        VotesLabel = Loc.Plural(L.Polls.Votes, poll.TotalVotes);
    }

    private void ApplyTranslation(PollDto poll, string code)
    {
        var translations = poll.Translations ?? Array.Empty<PollTranslationDto>();
        for (var index = 0; index < translations.Length; index++)
        {
            var translation = translations[index];
            if (translation.Lang != code)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(translation.Question))
            {
                Question = translation.Question;
            }

            var count = Math.Min(poll.Options.Length, translation.Options.Length);
            string[]? merged = null;
            for (var optionIndex = 0; optionIndex < count; optionIndex++)
            {
                if (string.IsNullOrEmpty(translation.Options[optionIndex]))
                {
                    continue;
                }

                merged ??= (string[])poll.Options.Clone();
                merged[optionIndex] = translation.Options[optionIndex];
            }

            if (merged is not null)
            {
                Options = merged;
            }

            return;
        }
    }

    private void RebuildTime(PollDto poll, long nowUnix)
    {
        Closed = PollRules.IsClosed(poll, nowUnix);
        if (Closed)
        {
            Status = PollStatus.Ended;
            var closedAt = PollRules.ClosedMoment(poll);
            StatusLabel = closedAt > 0
                ? Loc.T(L.Polls.EndedOn, TimeText.MonthDay(closedAt))
                : Loc.T(L.Polls.Ended);
            FooterLabel = LeaderCount > 1
                ? string.Concat(Loc.T(L.Polls.FinalResults), " · ", Loc.T(L.Polls.Tie))
                : Loc.T(L.Polls.FinalResults);
            return;
        }

        var ago = TimeText.Ago(poll.CreatedAtUnix);
        FooterLabel = poll.MyVote >= 0 ? ago : string.Concat(Loc.T(L.Polls.HiddenResults), " · ", ago);
        if (poll.ClosesAtUnix <= 0)
        {
            Status = PollStatus.Open;
            StatusLabel = string.Empty;
            return;
        }

        var remaining = poll.ClosesAtUnix - nowUnix;
        Status = remaining < PollRules.EndingSoonSeconds ? PollStatus.EndingSoon : PollStatus.Counting;
        StatusLabel = Countdown(remaining);
    }

    private static string Countdown(long remaining)
    {
        if (remaining >= SecondsPerDay)
        {
            return Loc.T(L.Polls.EndsInDaysHours, remaining / SecondsPerDay, remaining % SecondsPerDay / SecondsPerHour);
        }

        if (remaining >= SecondsPerHour)
        {
            return Loc.T(L.Polls.EndsInHoursMinutes, remaining / SecondsPerHour,
                remaining % SecondsPerHour / SecondsPerMinute);
        }

        if (remaining >= SecondsPerMinute)
        {
            return Loc.T(L.Polls.EndsInMinutes, remaining / SecondsPerMinute);
        }

        return Loc.T(L.Polls.EndsInSeconds, Math.Max(1L, remaining));
    }
}
