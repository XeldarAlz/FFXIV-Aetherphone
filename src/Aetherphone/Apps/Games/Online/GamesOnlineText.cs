using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Games.Online;

internal static class GamesOnlineText
{
    public static LocString GameName(string? gameKind)
    {
        if (string.Equals(gameKind, Core.Games.GameRoomWire.ChessKind, StringComparison.Ordinal))
        {
            return L.Games.OnlineChess;
        }

        if (string.Equals(gameKind, Core.Games.GameRoomWire.PoolKind, StringComparison.Ordinal))
        {
            return L.Games.OnlinePool;
        }

        if (string.Equals(gameKind, Core.Games.GameRoomWire.ConnectFourKind, StringComparison.Ordinal))
        {
            return L.Games.OnlineConnectFour;
        }

        return L.Games.OnlineUno;
    }

    public static LocString FoulMessage(string foul)
    {
        return foul switch
        {
            Core.Games.GameRoomWire.PoolFoulScratch => L.Games.OnlineFoulScratch,
            Core.Games.GameRoomWire.PoolFoulWrongBall => L.Games.OnlineFoulWrongBall,
            Core.Games.GameRoomWire.PoolFoulNoContact => L.Games.OnlineFoulNoContact,
            _ => L.Games.OnlineFoulNoRail,
        };
    }

    public static LocString ReasonMessage(string reason)
    {
        return reason switch
        {
            "full" => L.Games.OnlineRoomFull,
            "unavailable" => L.Games.OnlineWrongCode,
            "banned_from_room" => L.Games.OnlineBanned,
            "blocked" => L.Games.OnlineBlocked,
            "already_hosting" => L.Games.OnlineAlreadyHosting,
            "cooldown" => L.Games.OnlineCooldown,
            "not_your_turn" => L.Games.OnlineNotYourTurn,
            "stale_action" => L.Games.OnlineStale,
            "ended" => L.Games.OnlineRoomEnded,
            "kicked" => L.Games.OnlineKicked,
            "restarting" => L.Games.OnlineRestarting,
            _ => L.Games.OnlineUnavailable,
        };
    }
}
