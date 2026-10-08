using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Casino.Strip;

internal enum StripShelf : byte
{
    Tables,
    Machines,
    Originals,
    LiveFloor,
    Instant,
    Skill,
    Venue,
}

internal enum StripAction : byte
{
    Game,
    HostTable,
    HostVenue,
}

internal readonly record struct StripEntry(
    string GameId,
    LocString Title,
    CasinoSign Sign,
    StripAction Action = StripAction.Game,
    VenueRoomKind Venue = VenueRoomKind.None)
{
    public bool LiveIdle => Action == StripAction.Game && Machines.MachineCabinet.Owns(GameId);
}

internal static class StripCatalog
{
    public static readonly StripShelf[] Shelves =
    {
        StripShelf.Tables,
        StripShelf.Machines,
        StripShelf.Originals,
        StripShelf.LiveFloor,
        StripShelf.Instant,
        StripShelf.Skill,
        StripShelf.Venue,
    };

    private static readonly StripEntry[] Tables =
    {
        new(CasinoGames.Blackjack, L.Casino.GameBlackjack, CasinoSign.TwentyOne),
        new(CasinoGames.Holdem, L.Casino.GameHoldem, CasinoSign.Holdem),
        new(CasinoGames.Blackjack, L.Tables.HostTitle, CasinoSign.Gamba, StripAction.HostTable),
    };

    private static readonly StripEntry[] MachineEntries =
    {
        new(CasinoGames.SlotsBird, L.Machines.GameBird, CasinoSign.GoldenBird),
        new(CasinoGames.SlotsCascade, L.Machines.GameCascade, CasinoSign.Cascade),
        new(CasinoGames.SlotsMoogle, L.Machines.GameMoogle, CasinoSign.Moogle),
    };

    private static readonly StripEntry[] Originals =
    {
        new(CasinoGames.Plinko, L.Plinko.Game, CasinoSign.Plinko),
        new(CasinoGames.Mines, L.Originals.GameMines, CasinoSign.Mines),
        new(CasinoGames.Dice, L.Originals.GameDice, CasinoSign.Dice),
        new(CasinoGames.Limbo, L.Originals.GameLimbo, CasinoSign.Limbo),
        new(CasinoGames.Keno, L.Originals.GameKeno, CasinoSign.Keno),
        new(CasinoGames.HiLo, L.Originals.GameHiLo, CasinoSign.HiLo),
    };

    private static readonly StripEntry[] LiveFloor =
    {
        new(CasinoGames.Race, L.Race.Title, CasinoSign.Race),
        new(CasinoGames.Wheel, L.Casino.GameWheel, CasinoSign.Wheel),
        new(CasinoGames.Bingo, L.Casino.GameBingo, CasinoSign.Bingo),
    };

    private static readonly StripEntry[] Instant =
    {
        new(CasinoGames.Scratch, L.Casino.GameScratch, CasinoSign.Scratch),
        new(CasinoGames.DailySpin, L.Casino.GameDailySpin, CasinoSign.FreeSpin),
    };

    private static readonly StripEntry[] Skill =
    {
        new(CasinoGames.Barkeep, L.Casino.GameBarkeep, CasinoSign.Bar),
    };

    private static readonly StripEntry[] Venue =
    {
        new(CasinoGames.DiceTable, L.Venue.GameDiceTable, CasinoSign.Dice, StripAction.HostVenue, VenueRoomKind.Dice),
        new(CasinoGames.Deathroll, L.Venue.GameDeathroll, CasinoSign.Deathroll, StripAction.HostVenue,
            VenueRoomKind.Deathroll),
        new(CasinoGames.Raffle, L.Venue.GameRaffle, CasinoSign.Raffle, StripAction.HostVenue, VenueRoomKind.Raffle),
    };

    public static ReadOnlySpan<StripEntry> EntriesOf(StripShelf shelf) => shelf switch
    {
        StripShelf.Tables => Tables,
        StripShelf.Machines => MachineEntries,
        StripShelf.Originals => Originals,
        StripShelf.LiveFloor => LiveFloor,
        StripShelf.Instant => Instant,
        StripShelf.Skill => Skill,
        _ => Venue,
    };

    public static LocString TitleOf(StripShelf shelf) => shelf switch
    {
        StripShelf.Tables => L.Strip.ShelfTables,
        StripShelf.Machines => L.Strip.ShelfMachines,
        StripShelf.Originals => L.Strip.ShelfOriginals,
        StripShelf.LiveFloor => L.Strip.ShelfLive,
        StripShelf.Instant => L.Strip.ShelfInstant,
        StripShelf.Skill => L.Strip.ShelfSkill,
        _ => L.Strip.ShelfVenue,
    };

    public static int EntryCount
    {
        get
        {
            var total = 0;
            for (var index = 0; index < Shelves.Length; index++)
            {
                total += EntriesOf(Shelves[index]).Length;
            }

            return total;
        }
    }

    public static bool Reaches(string gameId)
    {
        for (var shelfIndex = 0; shelfIndex < Shelves.Length; shelfIndex++)
        {
            var entries = EntriesOf(Shelves[shelfIndex]);
            for (var index = 0; index < entries.Length; index++)
            {
                if (entries[index].Action == StripAction.Game
                    && string.Equals(entries[index].GameId, gameId, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
