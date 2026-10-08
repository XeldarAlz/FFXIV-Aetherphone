namespace Aetherphone.Core.Casino;

internal static class CasinoWire
{
    public const string SlotsKind = "casino.slots";

    public const string ScratchKind = "casino.scratch";

    public const string BartenderKind = "casino.bartender";

    public const string WheelKind = "casino.wheel";

    public const string BingoKind = "casino.bingo";

    public const string BlackjackKind = "casino.blackjack";

    public const string RaceKind = "casino.race";

    public const string BlackjackHandEvent = "you.cards";

    public const string DailySpinKind = "casino.dailyspin";

    private const string KindPrefix = "casino.";

    public static string Kind(string gameId)
    {
        return string.Concat(KindPrefix, gameId);
    }

    public static Aethernet.Contracts.CasinoSittingDto? SittingFor(
        Aethernet.Contracts.CasinoStateDto? state, string wireKind)
    {
        if (state is null)
        {
            return null;
        }

        return string.Equals(wireKind, BlackjackKind, StringComparison.Ordinal)
            ? state.TableSitting
            : state.Sitting;
    }
}
