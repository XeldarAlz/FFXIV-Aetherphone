namespace Aetherphone.Core.Casino;

internal static class CasinoRoomStates
{
    public const int Live = 0;

    public const int Draining = 1;

    public const int Closed = 2;
}

internal static class CasinoRoomPhases
{
    public const int Open = 0;

    public const int Locked = 1;

    public const int Result = 2;
}

internal static class CasinoRoomIds
{
    public const string WheelFloor = "wheel-floor";

    public const string BingoHall = "bingo-hall";

    public const string RaceTrack = "race-track";

    public const string BlackjackPit = "blackjack-pit";

    public const string BlackjackParlour = "blackjack-parlour";

    public const string BlackjackSalon = "blackjack-salon";

    public const string BlackjackVault = "blackjack-vault";

    public static readonly string[] BlackjackHouse =
    {
        BlackjackPit, BlackjackParlour, BlackjackSalon, BlackjackVault,
    };

    public static bool IsBlackjackHouse(string roomId)
    {
        for (var index = 0; index < BlackjackHouse.Length; index++)
        {
            if (string.Equals(BlackjackHouse[index], roomId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}

internal static class CasinoRoomCadence
{
    public const int WheelOpenSeconds = 25;

    public const int WheelLockedSeconds = 5;

    public const int WheelResultSeconds = 10;

    public const int BingoOpenSeconds = 45;

    public const int BingoLockedSeconds = 110;

    public const int BingoResultSeconds = 10;

    public const int RaceOpenSeconds = 60;

    public const int RaceLockedSeconds = 35;

    public const int RaceResultSeconds = 15;

    public static int WheelWindow(int phase) => phase switch
    {
        CasinoRoomPhases.Locked => WheelLockedSeconds,
        CasinoRoomPhases.Result => WheelResultSeconds,
        _ => WheelOpenSeconds,
    };

    public static int BingoWindow(int phase) => phase switch
    {
        CasinoRoomPhases.Locked => BingoLockedSeconds,
        CasinoRoomPhases.Result => BingoResultSeconds,
        _ => BingoOpenSeconds,
    };

    public static int RaceWindow(int phase) => phase switch
    {
        CasinoRoomPhases.Locked => RaceLockedSeconds,
        CasinoRoomPhases.Result => RaceResultSeconds,
        _ => RaceOpenSeconds,
    };
}
