namespace Aetherphone.Core.Games;

internal static class GameRoomWire
{
    public const string UnoKind = "games.uno";

    public const string ChessKind = "games.chess";

    public const string PoolKind = "games.pool";

    public const string ConnectFourKind = "games.connectfour";

    public const int ConnectFourColumns = 7;

    public const int ConnectFourRows = 6;

    public const int ConnectFourCellCount = ConnectFourColumns * ConnectFourRows;

    public const string UnoHandEvent = "uno.hand";

    public const string UnoPlayEvent = "uno.play";

    public const string UnoDrawEvent = "uno.draw";

    public const string UnoPassEvent = "uno.pass";

    public const string UnoTimeoutEvent = "uno.timeout";

    public const string ActionShoot = "shoot";

    public const string ActionPlace = "place";

    public const string PoolEndEight = "eight";

    public const string PoolEndEightEarly = "eight_early";

    public const string PoolEndEightScratch = "eight_scratch";

    public const string PoolEndResign = "resign";

    public const string PoolEndDesertion = "desertion";

    public const string PoolEndTimeout = "timeout";

    public const string PoolFoulScratch = "scratch";

    public const string PoolFoulWrongBall = "wrong_ball";

    public const string PoolFoulNoContact = "no_contact";

    public const string PoolFoulNoRail = "no_rail";

    public const int PoolGroupSolids = 1;

    public const int PoolGroupStripes = 2;

    public const float PoolTableWidth = 2f;

    public const float PoolTableHeight = 1f;

    public const float PoolBallRadius = 0.028f;

    public const float PoolPocketRadius = 0.055f;

    public const string ActionStart = "start";

    public const string ActionPlay = "play";

    public const string ActionDraw = "draw";

    public const string ActionPass = "pass";

    public const string ActionMove = "move";

    public const string ActionResign = "resign";

    public const string ActionDrop = "drop";

    public const string ConnectFourEndConnect = "connect";

    public const string ConnectFourEndDraw = "draw";

    public const string ConnectFourEndResign = "resign";

    public const string ConnectFourEndDesertion = "desertion";

    public const string ConnectFourEndTimeout = "timeout";

    public const string ChessEndCheckmate = "checkmate";

    public const string ChessEndStalemate = "stalemate";

    public const string ChessEndFiftyMove = "fifty";

    public const string ChessEndMaterial = "material";

    public const string ChessEndTimeout = "timeout";

    public const string ChessEndResign = "resign";

    public const string ChessEndDesertion = "desertion";

    public const int PhaseLobby = 0;

    public const int PhasePlaying = 1;

    public const int PhaseFinished = 2;

    public const string ReasonEnded = "ended";

    public const string ReasonKicked = "kicked";

    public const string ReasonRestarting = "restarting";

    public const string ReasonStaleAction = "stale_action";

    public const int RuleSetDefault = 0;

    public const int RuleSetHouse = 1;

    public const int WildCard = 52;

    public const int WildDrawFourCard = 53;

    public const int RankZero = 0;

    public const int RankSeven = 7;

    public const int RankSkip = 10;

    public const int RankReverse = 11;

    public const int RankDrawTwo = 12;

    public static int ColorOf(int card)
    {
        return card is >= 0 and < WildCard ? card / 13 : -1;
    }

    public static int RankOf(int card)
    {
        return card is >= 0 and < WildCard ? card % 13 : -1;
    }

    public static bool IsWild(int card)
    {
        return card is WildCard or WildDrawFourCard;
    }

    public static bool IsZero(int card)
    {
        return RankOf(card) == RankZero;
    }

    public static bool IsSeven(int card)
    {
        return RankOf(card) == RankSeven;
    }

    public static bool IsPlayable(int card, int activeColor, int topCard, int ruleSet = RuleSetDefault,
        int pendingDrawCount = 0)
    {
        if (pendingDrawCount > 0)
        {
            if (ruleSet != RuleSetHouse)
            {
                return false;
            }

            if (card == WildDrawFourCard)
            {
                return true;
            }

            return RankOf(topCard) == RankDrawTwo && RankOf(card) == RankDrawTwo;
        }

        if (IsWild(card))
        {
            return true;
        }

        if (card is < 0 or >= WildCard)
        {
            return false;
        }

        if (ColorOf(card) == activeColor)
        {
            return true;
        }

        return topCard < WildCard && RankOf(card) == RankOf(topCard);
    }
}
