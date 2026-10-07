using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Games.Framework;

internal struct RankText
{
    private const string Separator = " · ";

    private GameRank rank;
    private LanguageInfo? language;
    private bool built;

    public string IntroLine { get; private set; }

    public string ResultLine { get; private set; }

    public string FriendsLine { get; private set; }

    public RankText()
    {
        IntroLine = string.Empty;
        ResultLine = string.Empty;
        FriendsLine = string.Empty;
    }

    public void Refresh(in GameRank wanted)
    {
        if (built && ReferenceEquals(language, Loc.Current) && Same(rank, wanted))
        {
            return;
        }

        built = true;
        language = Loc.Current;
        rank = wanted;
        switch (wanted.State)
        {
            case RankState.Ranked when wanted.Rank > 0:
                IntroLine = Loc.T(L.Stage.RankGlobal, GameNumber.Label(wanted.Rank));
                ResultLine = string.Concat(Loc.T(L.Stage.RankOf, GameNumber.Label(wanted.Rank),
                    CountText.Exact(wanted.Total)), Separator, Loc.T(L.Stage.Global));
                FriendsLine = wanted.FriendsRank > 0
                    ? Loc.T(L.Stage.RankFriends, GameNumber.Label(wanted.FriendsRank))
                    : string.Empty;
                return;
            case RankState.Uploading:
                IntroLine = Loc.T(L.Stage.Uploading);
                ResultLine = IntroLine;
                FriendsLine = string.Empty;
                return;
            case RankState.SignedOut:
                IntroLine = Loc.T(L.Stage.SignInToRank);
                ResultLine = Loc.T(L.Stage.KeptOnPhone);
                FriendsLine = string.Empty;
                return;
            case RankState.Failed:
                IntroLine = Loc.T(L.Stage.NotRanked);
                ResultLine = Loc.T(L.Stage.KeptOnPhone);
                FriendsLine = string.Empty;
                return;
            case RankState.Hidden:
                IntroLine = Loc.T(L.Stage.LeaderboardsOff);
                ResultLine = IntroLine;
                FriendsLine = string.Empty;
                return;
            default:
                IntroLine = Loc.T(L.Stage.NotRanked);
                ResultLine = string.Empty;
                FriendsLine = string.Empty;
                return;
        }
    }

    private static bool Same(in GameRank first, in GameRank second) =>
        first.State == second.State && first.Rank == second.Rank && first.Total == second.Total &&
        first.FriendsRank == second.FriendsRank && first.WeekRank == second.WeekRank;
}
