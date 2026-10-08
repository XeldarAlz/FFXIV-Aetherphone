using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Casino;

internal static class CasinoGameNames
{
    public static LocString Of(string gameId) => gameId switch
    {
        CasinoGames.Blackjack => L.Casino.GameBlackjack,
        CasinoGames.Holdem => L.Casino.GameHoldem,
        CasinoGames.Slots or CasinoGames.SlotsBird => L.Machines.GameBird,
        CasinoGames.SlotsCascade => L.Machines.GameCascade,
        CasinoGames.SlotsMoogle => L.Machines.GameMoogle,
        CasinoGames.SlotsGamble => L.Machines.GambleTitle,
        CasinoGames.Scratch => L.Casino.GameScratch,
        CasinoGames.Bingo => L.Casino.GameBingo,
        CasinoGames.Wheel => L.Casino.GameWheel,
        CasinoGames.Barkeep => L.Casino.GameBarkeep,
        CasinoGames.DailySpin => L.Casino.GameDailySpin,
        CasinoGames.Mines => L.Originals.GameMines,
        CasinoGames.Dice => L.Originals.GameDice,
        CasinoGames.Limbo => L.Originals.GameLimbo,
        CasinoGames.Keno => L.Originals.GameKeno,
        CasinoGames.HiLo => L.Originals.GameHiLo,
        CasinoGames.Race => L.Race.Title,
        CasinoGames.Plinko => L.Plinko.Game,
        CasinoGames.DiceTable => L.Venue.GameDiceTable,
        CasinoGames.Deathroll => L.Venue.GameDeathroll,
        CasinoGames.Raffle => L.Venue.GameRaffle,
        _ => L.Apps.Casino,
    };
}
