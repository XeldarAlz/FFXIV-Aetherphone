using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Games.Framework;

internal static class GameSeats
{
    public const int Max = 6;

    private static readonly Vector4[] Colors =
    {
        new(0.32f, 0.62f, 1.00f, 1f), new(0.98f, 0.42f, 0.38f, 1f), new(0.36f, 0.82f, 0.48f, 1f),
        new(1.00f, 0.76f, 0.28f, 1f), new(0.70f, 0.52f, 0.98f, 1f), new(0.98f, 0.48f, 0.78f, 1f),
    };

    private static readonly string[] Names = new string[Max];
    private static readonly string[] PassLines = new string[Max];
    private static readonly string[] WinLines = new string[Max];
    private static LanguageInfo? language;

    public static Vector4 Color(int seat) => Colors[Slot(seat)];

    public static string Name(int seat)
    {
        Sync();
        return Names[Slot(seat)];
    }

    public static string PassLine(int seat)
    {
        Sync();
        return PassLines[Slot(seat)];
    }

    public static string WinLine(int seat)
    {
        Sync();
        return WinLines[Slot(seat)];
    }

    private static int Slot(int seat) => Math.Clamp(seat, 0, Max - 1);

    private static void Sync()
    {
        if (ReferenceEquals(language, Loc.Current) && Names[0] is not null)
        {
            return;
        }

        language = Loc.Current;
        for (var seat = 0; seat < Max; seat++)
        {
            Names[seat] = Loc.T(L.Stage.PlayerName, GameNumber.Label(seat + 1));
            PassLines[seat] = Loc.T(L.Stage.PassTo, Names[seat]);
            WinLines[seat] = Loc.T(L.Stage.SeatWins, Names[seat]);
        }
    }
}
