using Aetherphone.Core.Casino;

namespace Aetherphone.Apps.Casino;

internal static class CasinoGameGate
{
    public static string FlagFor(string gameId) => gameId switch
    {
        CasinoGames.Holdem => CasinoFeatures.Holdem,
        CasinoGames.SlotsBird or CasinoGames.SlotsCascade or CasinoGames.SlotsMoogle or CasinoGames.SlotsGamble
            or CasinoGames.Slots => CasinoFeatures.Machines,
        CasinoGames.Plinko => CasinoFeatures.Plinko,
        CasinoGames.Mines or CasinoGames.Dice or CasinoGames.Limbo or CasinoGames.Keno or CasinoGames.HiLo =>
            CasinoFeatures.Originals,
        CasinoGames.Race => CasinoFeatures.Race,
        CasinoGames.DiceTable or CasinoGames.Deathroll or CasinoGames.Raffle => CasinoFeatures.Venue,
        _ => string.Empty,
    };

    public static bool IsOpen(CasinoFeatureSet features, string gameId)
    {
        var flag = FlagFor(gameId);
        return flag.Length == 0 || features.Has(flag);
    }

    public static bool RoomOpen(CasinoFeatureSet features, string gameKind)
    {
        if (string.Equals(gameKind, HoldemRules.Kind, StringComparison.Ordinal))
        {
            return features.Has(CasinoFeatures.Holdem);
        }

        if (string.Equals(gameKind, CasinoWire.RaceKind, StringComparison.Ordinal))
        {
            return features.Has(CasinoFeatures.Race);
        }

        return !VenueKinds.IsVenue(gameKind) || features.Has(CasinoFeatures.Venue);
    }
}
