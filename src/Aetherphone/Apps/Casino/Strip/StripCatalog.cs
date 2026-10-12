using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Casino.Strip;

internal enum StripShelf : byte
{
    Cards,
    Machines,
    QuickBets,
}

internal readonly record struct StripEntry(string GameId, LocString Title, CasinoSign Sign)
{
    public bool LiveIdle => Machines.MachineCabinet.Owns(GameId);
}

internal static class StripCatalog
{
    public static readonly StripShelf[] Shelves =
    {
        StripShelf.Cards,
        StripShelf.Machines,
        StripShelf.QuickBets,
    };

    private static readonly StripEntry[] Cards =
    {
        new(CasinoGames.Blackjack, L.Casino.GameBlackjack, CasinoSign.TwentyOne),
        new(CasinoGames.Holdem, L.Casino.GameHoldem, CasinoSign.Holdem),
        new(CasinoGames.DealerHoldem, L.DealerHoldem.Game, CasinoSign.DealerHoldem),
    };

    private static readonly StripEntry[] MachineEntries =
    {
        new(CasinoGames.SlotsBird, L.Machines.GameBird, CasinoSign.GoldenBird),
        new(CasinoGames.SlotsCascade, L.Machines.GameCascade, CasinoSign.Cascade),
        new(CasinoGames.SlotsMoogle, L.Machines.GameMoogle, CasinoSign.Moogle),
    };

    private static readonly StripEntry[] QuickBets =
    {
        new(CasinoGames.Plinko, L.Plinko.Game, CasinoSign.Plinko),
        new(CasinoGames.Mines, L.Originals.GameMines, CasinoSign.Mines),
        new(CasinoGames.Dice, L.Originals.GameDice, CasinoSign.Dice),
        new(CasinoGames.Limbo, L.Originals.GameLimbo, CasinoSign.Limbo),
        new(CasinoGames.Keno, L.Originals.GameKeno, CasinoSign.Keno),
        new(CasinoGames.HiLo, L.Originals.GameHiLo, CasinoSign.HiLo),
        new(CasinoGames.Scratch, L.Casino.GameScratch, CasinoSign.Scratch),
        new(CasinoGames.Barkeep, L.Casino.GameBarkeep, CasinoSign.Bar),
    };

    public static readonly StripEntry[] Playable = CollectPlayable();

    public static readonly string[] PlayableIds = IdsOf(Playable);

    public static ReadOnlySpan<StripEntry> EntriesOf(StripShelf shelf) => shelf switch
    {
        StripShelf.Cards => Cards,
        StripShelf.Machines => MachineEntries,
        _ => QuickBets,
    };

    public static LocString TitleOf(StripShelf shelf) => shelf switch
    {
        StripShelf.Cards => L.Floor.ShelfCards,
        StripShelf.Machines => L.Strip.ShelfMachines,
        _ => L.Floor.ShelfQuickBets,
    };

    public static bool WearsSkillBadge(string gameId) =>
        string.Equals(gameId, CasinoGames.Barkeep, StringComparison.Ordinal);

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
        for (var index = 0; index < PlayableIds.Length; index++)
        {
            if (string.Equals(PlayableIds[index], gameId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static StripEntry[] CollectPlayable()
    {
        var playable = new StripEntry[EntryCount];
        var next = 0;
        for (var shelfIndex = 0; shelfIndex < Shelves.Length; shelfIndex++)
        {
            var entries = EntriesOf(Shelves[shelfIndex]);
            for (var index = 0; index < entries.Length; index++)
            {
                playable[next++] = entries[index];
            }
        }

        return playable;
    }

    private static string[] IdsOf(StripEntry[] entries)
    {
        var ids = new string[entries.Length];
        for (var index = 0; index < entries.Length; index++)
        {
            ids[index] = entries[index].GameId;
        }

        return ids;
    }
}
