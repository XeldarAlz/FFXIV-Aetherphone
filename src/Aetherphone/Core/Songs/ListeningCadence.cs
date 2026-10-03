using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Songs;

internal enum ListeningAction : byte
{
    None,
    Publish,
    Clear,
}

internal enum ListeningStatus : byte
{
    Now,
    Paused,
    Earlier,
}

internal readonly record struct ListeningState(bool Sharing, string VideoId, bool Paused, string JamCode,
    long IdleSinceMilliseconds);

internal struct ListeningMark
{
    public bool Published;
    public string VideoId;
    public bool Paused;
    public string JamCode;
    public long SentAtMilliseconds;
    public long RetryAtMilliseconds;
    public int Failures;

    public static ListeningMark Fresh => new()
    {
        VideoId = string.Empty,
        JamCode = string.Empty,
        SentAtMilliseconds = long.MinValue / 2,
    };
}

internal static class ListeningCadence
{
    public const long MinimumIntervalMilliseconds = 10_000;
    public const long HeartbeatIntervalMilliseconds = 60_000;
    public const long StopGraceMilliseconds = 5_000;
    public const long MaximumBackoffMilliseconds = 5 * 60_000;
    public const long FriendsIntervalMilliseconds = 60_000;
    public const long RecentMilliseconds = 2 * 60_000;
    private const long MillisecondsPerMinute = 60_000;
    private const int MaximumBackoffDoublings = 5;

    public static ListeningAction Decide(in ListeningMark mark, in ListeningState state, long nowMilliseconds)
    {
        if (nowMilliseconds < mark.RetryAtMilliseconds)
        {
            return ListeningAction.None;
        }

        if (!state.Sharing)
        {
            return mark.Published ? ListeningAction.Clear : ListeningAction.None;
        }

        if (state.VideoId.Length == 0)
        {
            var settled = nowMilliseconds - state.IdleSinceMilliseconds >= StopGraceMilliseconds;
            return mark.Published && settled ? ListeningAction.Clear : ListeningAction.None;
        }

        var elapsed = nowMilliseconds - mark.SentAtMilliseconds;
        if (elapsed < MinimumIntervalMilliseconds)
        {
            return ListeningAction.None;
        }

        var changed = !mark.Published
            || mark.Paused != state.Paused
            || !string.Equals(mark.VideoId, state.VideoId, StringComparison.Ordinal)
            || !string.Equals(mark.JamCode, state.JamCode, StringComparison.Ordinal);
        if (changed)
        {
            return ListeningAction.Publish;
        }

        return !state.Paused && elapsed >= HeartbeatIntervalMilliseconds
            ? ListeningAction.Publish
            : ListeningAction.None;
    }

    public static void Sent(ref ListeningMark mark, in ListeningState state, long nowMilliseconds)
    {
        mark.Published = true;
        mark.VideoId = state.VideoId;
        mark.Paused = state.Paused;
        mark.JamCode = state.JamCode;
        mark.SentAtMilliseconds = nowMilliseconds;
    }

    public static void Completed(ref ListeningMark mark, ListeningAction action, bool succeeded,
        long nowMilliseconds)
    {
        if (succeeded)
        {
            mark.Failures = 0;
            mark.RetryAtMilliseconds = 0;
            if (action == ListeningAction.Clear)
            {
                mark.Published = false;
            }

            return;
        }

        mark.Failures++;
        mark.RetryAtMilliseconds = nowMilliseconds + Backoff(MinimumIntervalMilliseconds, mark.Failures);
        if (action == ListeningAction.Publish)
        {
            mark.VideoId = string.Empty;
        }
    }

    public static long Backoff(long baseMilliseconds, int failures)
    {
        var doublings = Math.Clamp(failures - 1, 0, MaximumBackoffDoublings);
        return Math.Min(MaximumBackoffMilliseconds, baseMilliseconds << doublings);
    }

    public static ListeningStatus Status(bool paused, long updatedAtUnixMilliseconds, long nowUnixMilliseconds)
    {
        if (paused)
        {
            return ListeningStatus.Paused;
        }

        return nowUnixMilliseconds - updatedAtUnixMilliseconds < RecentMilliseconds
            ? ListeningStatus.Now
            : ListeningStatus.Earlier;
    }

    public static int MinutesAgo(long updatedAtUnixMilliseconds, long nowUnixMilliseconds)
    {
        var minutes = (nowUnixMilliseconds - updatedAtUnixMilliseconds) / MillisecondsPerMinute;
        return (int)Math.Clamp(minutes, 1, int.MaxValue);
    }

    public static ListeningFriendDto[] Usable(ListeningFriendDto[]? friends)
    {
        if (friends is null || friends.Length == 0)
        {
            return Array.Empty<ListeningFriendDto>();
        }

        var count = 0;
        for (var index = 0; index < friends.Length; index++)
        {
            if (IsUsable(friends[index]))
            {
                count++;
            }
        }

        if (count == friends.Length)
        {
            return friends;
        }

        var usable = new ListeningFriendDto[count];
        var next = 0;
        for (var index = 0; index < friends.Length; index++)
        {
            if (IsUsable(friends[index]))
            {
                usable[next++] = friends[index];
            }
        }

        return usable;
    }

    private static bool IsUsable(ListeningFriendDto? friend) =>
        friend is not null && !string.IsNullOrEmpty(friend.UserId) && friend.Track is not null
        && !string.IsNullOrEmpty(friend.Track.VideoId);
}
