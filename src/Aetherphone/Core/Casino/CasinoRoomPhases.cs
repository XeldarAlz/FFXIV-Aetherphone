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

    public const int BingoOpenSeconds = 60;

    public const int BingoLockedSeconds = 155;

    public const int BingoResultSeconds = 15;

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
}
