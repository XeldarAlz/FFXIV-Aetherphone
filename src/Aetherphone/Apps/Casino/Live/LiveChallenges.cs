using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Casino.Strip;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Casino.Live;

internal enum ChallengePhase : byte
{
    Upcoming,
    Running,
    Over,
}

internal readonly record struct LiveChallengeView(
    string GameId,
    string Title,
    string Goal,
    string Stake,
    string Leader,
    string Yours,
    string Prize,
    long StartsAtUnix,
    long EndsAtUnix);

internal sealed class LiveChallenges
{
    private LiveChallengeView[] views = Array.Empty<LiveChallengeView>();
    private CasinoChallengesDto? source;
    private LanguageInfo? language;
    private bool built;

    public LiveChallengeView[] Refresh(CasinoChallengesDto? challenges)
    {
        if (built && ReferenceEquals(challenges, source) && ReferenceEquals(language, Loc.Current))
        {
            return views;
        }

        built = true;
        source = challenges;
        language = Loc.Current;
        var held = challenges?.Challenges ?? Array.Empty<CasinoChallengeDto>();
        var count = 0;
        for (var index = 0; index < held.Length; index++)
        {
            if (Shows(held[index]))
            {
                count++;
            }
        }

        views = count == 0 ? Array.Empty<LiveChallengeView>() : new LiveChallengeView[count];
        var next = 0;
        for (var index = 0; index < held.Length; index++)
        {
            if (Shows(held[index]))
            {
                views[next++] = ViewOf(held[index]);
            }
        }

        return views;
    }

    internal static bool Shows(CasinoChallengeDto challenge) =>
        challenge.State is CasinoChallengeStates.Scheduled or CasinoChallengeStates.Live
        && challenge.GameKind.Length > 0;

    internal static ChallengePhase PhaseOf(in LiveChallengeView view, long nowUnix)
    {
        if (nowUnix >= view.EndsAtUnix)
        {
            return ChallengePhase.Over;
        }

        return nowUnix >= view.StartsAtUnix ? ChallengePhase.Running : ChallengePhase.Upcoming;
    }

    internal static int SecondsUntil(long edgeUnix, long nowUnix) =>
        (int)Math.Clamp(edgeUnix - nowUnix, 0L, int.MaxValue);

    internal static LiveChallengeView ViewOf(CasinoChallengeDto challenge)
    {
        var gameId = CasinoRecentGames.ClientGameId(challenge.GameKind);
        var first = string.Equals(challenge.Kind, CasinoChallengeKinds.First, StringComparison.Ordinal);
        var multiple = CasinoMultiples.Label(Hundredths(challenge.ThresholdTenths));
        var goal = challenge.GameKind.Length == 0
            ? Loc.T(first ? L.LivePage.ChallengeFirstGoalAny : L.LivePage.ChallengeMostGoalAny, multiple)
            : Loc.T(first ? L.LivePage.ChallengeFirstGoal : L.LivePage.ChallengeMostGoal, multiple,
                Loc.T(CasinoGameNames.OfEveryMachine(gameId)));
        var stake = challenge.MinStake > 0
            ? Loc.T(L.LivePage.ChallengeMinStake, NumberText.Compact(challenge.MinStake))
            : string.Empty;
        var yours = !first && challenge.MyValue > 0
            ? Loc.T(L.LivePage.ChallengeYours, Wins(challenge.MyValue))
            : string.Empty;
        return new LiveChallengeView(gameId, challenge.Title, goal, stake, LeaderOf(challenge, first), yours,
            PrizeOf(challenge.Reward, challenge.BadgeId.Length > 0), challenge.StartsAtUnix, challenge.EndsAtUnix);
    }

    internal static string LeaderOf(CasinoChallengeDto challenge, bool first)
    {
        if (challenge.Leader is null && challenge.LeaderValue <= 0)
        {
            return Loc.T(L.LivePage.ChallengeNoLeader);
        }

        var score = first ? CasinoMultiples.Label(Hundredths(challenge.LeaderValue)) : Wins(challenge.LeaderValue);
        return Loc.T(L.LivePage.ChallengeLeader, WinsTicker.NameOf(challenge.Leader), score);
    }

    internal static string PrizeOf(long reward, bool badge)
    {
        if (reward <= 0)
        {
            return badge ? Loc.T(L.LivePage.ChallengeBadge) : string.Empty;
        }

        return Loc.T(badge ? L.LivePage.ChallengePrizeBadge : L.LivePage.ChallengePrize, NumberText.Compact(reward));
    }

    private static string Wins(long count) => Loc.Plural(L.LivePage.ChallengeWins, (int)Math.Min(count, int.MaxValue));

    private static int Hundredths(long tenths) => (int)Math.Clamp(tenths * 10, 0L, int.MaxValue);
}
