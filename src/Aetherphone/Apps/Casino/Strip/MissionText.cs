using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Casino.Strip;

internal static class MissionText
{
    public static string Compose(CasinoMissionDto mission)
    {
        var target = Math.Max(1, mission.Target);
        var count = Games.Framework.GameNumber.Label((int)Math.Min(target, int.MaxValue));
        var gameId = CasinoRecentGames.ClientGameId(mission.GameKind);
        var anyGame = mission.GameKind.Length == 0;
        return mission.Metric switch
        {
            CasinoMissionMetrics.Rounds => anyGame ? Loc.T(L.Club.MissionRounds, count) : Rounds(gameId, count),
            CasinoMissionMetrics.Games => Loc.T(L.Club.MissionGames, count),
            CasinoMissionMetrics.Wins => anyGame
                ? Loc.T(L.Club.MissionWins, count)
                : Loc.T(L.Club.MissionWinsOf, count, Loc.T(CasinoGameNames.Of(gameId))),
            CasinoMissionMetrics.Hits => Hits(mission, target, count, gameId, anyGame),
            CasinoMissionMetrics.MaxBet => target == 1 ? Loc.T(L.Club.MissionMaxBet) : Loc.T(L.Club.MissionMaxBets, count),
            CasinoMissionMetrics.Hosted => target == 1 ? Loc.T(L.Club.MissionHosted) : Loc.T(L.Club.MissionHostedMany, count),
            _ => Loc.T(L.Club.MissionGeneric),
        };
    }

    private static string Rounds(string gameId, string count) => gameId switch
    {
        CasinoGames.Slots => Loc.T(L.Club.MissionSpin, count),
        CasinoGames.Blackjack => Loc.T(L.Club.MissionHands, count),
        CasinoGames.Wheel => Loc.T(L.Club.MissionWheel, count),
        CasinoGames.Scratch => Loc.T(L.Club.MissionScratch, count),
        CasinoGames.Plinko => Loc.T(L.Club.MissionDrop, count),
        CasinoGames.Race => Loc.T(L.Club.MissionRaces, count),
        _ => Loc.T(L.Club.MissionRoundsOf, count, Loc.T(CasinoGameNames.Of(gameId))),
    };

    private static string Hits(CasinoMissionDto mission, long target, string count, string gameId, bool anyGame)
    {
        var multiple = CasinoMultiples.Label((int)Math.Min(mission.Threshold * 10, int.MaxValue));
        if (target == 1)
        {
            return anyGame
                ? Loc.T(L.Club.MissionHitOnce, multiple)
                : Loc.T(L.Club.MissionHitOnceOf, multiple, Loc.T(CasinoGameNames.Of(gameId)));
        }

        return Loc.T(L.Club.MissionHits, multiple, count);
    }
}
