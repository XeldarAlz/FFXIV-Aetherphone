namespace Aetherphone.Core.Games;

internal static class GameRoomWire
{
    public const string UnoKind = "games.uno";

    public const string ChessKind = "games.chess";

    public const string PoolKind = "games.pool";

    public const string ConnectFourKind = "games.connectfour";

    public const string BroadsideKind = "games.broadside";

    public const int BroadsideSize = 10;

    public const int BroadsideCellCount = BroadsideSize * BroadsideSize;

    public const int BroadsideShipCount = 5;

    public const int BroadsideMarkMiss = 1;

    public const int BroadsideMarkHit = 2;

    public const string BroadsideFleetEvent = "broadside.fleet";

    public const string BroadsideResultMiss = "miss";

    public const string BroadsideResultHit = "hit";

    public const string BroadsideResultSunk = "sunk";

    public const string BroadsideEndFleet = "fleet";

    public const string BroadsideEndResign = "resign";

    public const string BroadsideEndDesertion = "desertion";

    public const string BroadsideEndTimeout = "timeout";

    public const string ActionFire = "fire";

    public const string LuckyDrawKind = "games.luckydraw";

    public const string CraterKind = "games.crater";

    public const string CraterTimeoutEvent = "crater.timeout";

    public const string CraterEndKnockout = "knockout";

    public const string CraterEndDraw = "draw";

    public const string CraterEndResign = "resign";

    public const string CraterEndDesertion = "desertion";

    public const string CraterEndTimeout = "timeout";

    public const int CraterMaxPlayers = 4;

    public const int CraterBeatStride = 6;

    public const int CraterBeatLaunched = 1;

    public const int CraterBeatBounced = 2;

    public const int CraterBeatExploded = 3;

    public const int CraterBeatDamaged = 4;

    public const int CraterBeatShielded = 5;

    public const int CraterBeatDied = 6;

    public const int CraterBeatDrowned = 7;

    public const int CraterBeatSplashed = 8;

    public const int CraterBeatClusterSplit = 9;

    public const int CraterBeatDrillStarted = 10;

    public const int CraterBeatLanded = 12;

    public const int CraterBeatFallHurt = 13;

    public const int CraterBeatTeleported = 14;

    public const int CraterBeatShieldRaised = 15;

    public const int CraterBeatSuddenDeath = 17;

    public const int CraterBeatWaterRising = 18;

    public const int CraterBeatTunnel = 20;

    public const int CraterMaxWalkTicks = 320;

    public const int CraterMaxSteps = 64;

    public const float CraterCentimetres = 100f;

    public const int CraterTicksPerSecond = 120;

    public const string MiniGolfKind = "games.minigolf";

    public const string MiniGolfEndCourse = "course";

    public const string MiniGolfEndDesertion = "desertion";

    public const string MiniGolfResultRest = "rest";

    public const string MiniGolfResultHoled = "holed";

    public const string MiniGolfResultWater = "water";

    public const string MiniGolfResultOut = "out";

    public const string MiniGolfResultPicked = "picked";

    public const int MiniGolfMarkWall = 1;

    public const int MiniGolfMarkPost = 2;

    public const int MiniGolfMarkMill = 3;

    public const int MiniGolfMarkSand = 4;

    public const int MiniGolfMarkTunnel = 5;

    public const int MiniGolfMarkSplash = 6;

    public const int MiniGolfMarkOut = 7;

    public const int MiniGolfMarkLipOut = 8;

    public const int MiniGolfMarkDrop = 9;

    public const int MiniGolfPathScale = 1000;

    public const int MiniGolfSamplesPerSecond = 30;

    public const int MiniGolfMaxStrokes = 10;

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

    public const string ActionHit = "hit";

    public const string ActionStay = "stay";

    public const string ActionTarget = "target";

    public const string LuckyDrawEndTarget = "target";

    public const string LuckyDrawEndDesertion = "desertion";

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
