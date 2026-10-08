using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Casino;

internal static class CasinoRules
{
    private static readonly LocString[] NoSteps = Array.Empty<LocString>();

    private static readonly LocString[] SlotsSteps =
    {
        L.Casino.RulesSlotsStep1,
        L.Casino.RulesSlotsStep2,
        L.Casino.RulesSlotsStep3,
        L.Casino.RulesSlotsStep4,
        L.Casino.RulesSlotsStep5,
    };

    private static readonly LocString[] ScratchSteps =
    {
        L.Casino.RulesScratchStep1,
        L.Casino.RulesScratchStep2,
        L.Casino.RulesScratchStep3,
    };

    private static readonly LocString[] WheelSteps =
    {
        L.Casino.RulesWheelStep1,
        L.Casino.RulesWheelStep2,
        L.Casino.RulesWheelStep3,
        L.Casino.RulesWheelStep4,
    };

    private static readonly LocString[] BingoSteps =
    {
        L.Casino.RulesBingoStep1,
        L.Casino.RulesBingoStep2,
        L.Casino.RulesBingoStep3,
        L.Casino.RulesBingoStep4,
    };

    private static readonly LocString[] BlackjackSteps =
    {
        L.Casino.RulesBlackjackStep1,
        L.Casino.RulesBlackjackStep2,
        L.Casino.RulesBlackjackStep3,
        L.Casino.RulesBlackjackStep4,
    };

    private static readonly LocString[] BarkeepSteps =
    {
        L.Casino.RulesBarkeepStep1,
        L.Casino.RulesBarkeepStep2,
        L.Casino.RulesBarkeepStep3,
    };

    private static readonly LocString[] MinesSteps =
    {
        L.Originals.RulesMinesStep1,
        L.Originals.RulesMinesStep2,
        L.Originals.RulesMinesStep3,
        L.Originals.RulesMinesStep4,
    };

    private static readonly LocString[] DiceSteps =
    {
        L.Originals.RulesDiceStep1,
        L.Originals.RulesDiceStep2,
        L.Originals.RulesDiceStep3,
    };

    private static readonly LocString[] LimboSteps =
    {
        L.Originals.RulesLimboStep1,
        L.Originals.RulesLimboStep2,
        L.Originals.RulesLimboStep3,
    };

    private static readonly LocString[] KenoSteps =
    {
        L.Originals.RulesKenoStep1,
        L.Originals.RulesKenoStep2,
        L.Originals.RulesKenoStep3,
        L.Originals.RulesKenoStep4,
    };

    private static readonly LocString[] HiLoSteps =
    {
        L.Originals.RulesHiLoStep1,
        L.Originals.RulesHiLoStep2,
        L.Originals.RulesHiLoStep3,
        L.Originals.RulesHiLoStep4,
    };

    private static readonly LocString[] RaceSteps =
    {
        L.Race.Rules1,
        L.Race.Rules2,
        L.Race.Rules3,
        L.Race.Rules4,
    };

    public static LocString PitchOf(string gameId) => gameId switch
    {
        CasinoGames.Mines => L.Originals.PitchMines,
        CasinoGames.Dice => L.Originals.PitchDice,
        CasinoGames.Limbo => L.Originals.PitchLimbo,
        CasinoGames.Keno => L.Originals.PitchKeno,
        CasinoGames.HiLo => L.Originals.PitchHiLo,
        CasinoGames.Race => L.Race.Pitch,
        CasinoGames.Slots => L.Casino.PitchSlots,
        CasinoGames.Scratch => L.Casino.PitchScratch,
        CasinoGames.Wheel => L.Casino.PitchWheel,
        CasinoGames.Bingo => L.Casino.PitchBingo,
        CasinoGames.Blackjack => L.Casino.PitchBlackjack,
        CasinoGames.Barkeep => L.Casino.PitchBarkeep,
        _ => L.Casino.PitchGeneric,
    };

    public static LocString TitleOf(string gameId) => gameId switch
    {
        CasinoGames.Mines => L.Originals.GameMines,
        CasinoGames.Dice => L.Originals.GameDice,
        CasinoGames.Limbo => L.Originals.GameLimbo,
        CasinoGames.Keno => L.Originals.GameKeno,
        CasinoGames.HiLo => L.Originals.GameHiLo,
        CasinoGames.Race => L.Race.Title,
        CasinoGames.Slots => L.Casino.GameSlots,
        CasinoGames.Scratch => L.Casino.GameScratch,
        CasinoGames.Wheel => L.Casino.GameWheel,
        CasinoGames.Bingo => L.Casino.GameBingo,
        CasinoGames.Blackjack => L.Casino.GameBlackjack,
        CasinoGames.Barkeep => L.Casino.GameBarkeep,
        _ => L.Apps.Casino,
    };

    public static LocString[] StepsOf(string gameId) => gameId switch
    {
        CasinoGames.Mines => MinesSteps,
        CasinoGames.Dice => DiceSteps,
        CasinoGames.Limbo => LimboSteps,
        CasinoGames.Keno => KenoSteps,
        CasinoGames.HiLo => HiLoSteps,
        CasinoGames.Race => RaceSteps,
        CasinoGames.Slots => SlotsSteps,
        CasinoGames.Scratch => ScratchSteps,
        CasinoGames.Wheel => WheelSteps,
        CasinoGames.Bingo => BingoSteps,
        CasinoGames.Blackjack => BlackjackSteps,
        CasinoGames.Barkeep => BarkeepSteps,
        _ => NoSteps,
    };

    public static bool FactsOf(string gameId, int index, out LocString label, out string value)
    {
        label = default;
        value = string.Empty;
        switch (gameId)
        {
            case CasinoGames.Slots:
                return SlotsFact(index, ref label, ref value);
            case CasinoGames.Scratch:
                return ScratchFact(index, ref label, ref value);
            case CasinoGames.Wheel:
                return WheelFact(index, ref label, ref value);
            case CasinoGames.Bingo:
                return BingoFact(index, ref label, ref value);
            case CasinoGames.Blackjack:
                return BlackjackFact(index, ref label, ref value);
            case CasinoGames.Barkeep:
                return BarkeepFact(index, ref label, ref value);
            case CasinoGames.Mines:
                return OriginalsFact(index, Core.Casino.CasinoWire.MinesKind, true, ref label, ref value);
            case CasinoGames.Dice:
                return OriginalsFact(index, Core.Casino.CasinoWire.DiceKind, false, ref label, ref value);
            case CasinoGames.Limbo:
                return OriginalsFact(index, Core.Casino.CasinoWire.LimboKind, false, ref label, ref value);
            case CasinoGames.Keno:
                return OriginalsFact(index, Core.Casino.CasinoWire.KenoKind, false, ref label, ref value);
            case CasinoGames.HiLo:
                return OriginalsFact(index, Core.Casino.CasinoWire.HiLoKind, false, ref label, ref value);
            default:
                return false;
        }
    }

    private static bool OriginalsFact(int index, string wireKind, bool mines, ref LocString label, ref string value)
    {
        switch (index)
        {
            case 0:
                label = L.Originals.FactMinBet;
                value = Number(Core.Casino.OriginalsRules.MinBet);
                return true;
            case 1:
                label = L.Originals.FactTopMultiplier;
                value = Originals.OriginalsText.MultiplierHundredths(
                    Core.Casino.OriginalsRules.TopMultiple(wireKind) * 100);
                return true;
            case 2:
                label = L.Strip.Return;
                value = Originals.OriginalsText.Percent(Core.Casino.OriginalsRules.ReturnTenths * 10);
                return true;
            case 3 when mines:
                label = L.Originals.FactMines;
                value = Range(Core.Casino.OriginalsRules.MinMines, Core.Casino.OriginalsRules.MaxMines);
                return true;
            case CasinoGames.Race:
                return RaceFact(index, ref label, ref value);
            default:
                return false;
        }
    }

    private static bool SlotsFact(int index, ref LocString label, ref string value)
    {
        switch (index)
        {
            case 0:
                label = L.Casino.FactStakeRange;
                value = Range(Core.Casino.SlotsRules.MinStake, Core.Casino.SlotsRules.MaxStake);
                return true;
            case 1:
                label = L.Casino.FactPaylines;
                value = Number(Core.Casino.SlotsRules.PaylineCount);
                return true;
            case 2:
                label = L.Casino.FactWinCap;
                value = Loc.T(L.Casino.FactWinCapValue, Number(Core.Casino.SlotsRules.PayoutCapMultiple));
                return true;
            default:
                return false;
        }
    }

    private static bool ScratchFact(int index, ref LocString label, ref string value)
    {
        var prices = Core.Casino.ScratchRules.Prices;
        switch (index)
        {
            case 0:
                label = L.Casino.FactCardPrice;
                value = Range(prices[0], prices[prices.Length - 1]);
                return true;
            case 1:
                label = L.Casino.FactMatchesNeeded;
                value = Number(3);
                return true;
            default:
                return false;
        }
    }

    private static bool WheelFact(int index, ref LocString label, ref string value)
    {
        switch (index)
        {
            case 0:
                label = L.Casino.FactBetRange;
                value = Range(Core.Casino.WheelRules.MinStakePerSpot, Core.Casino.WheelRules.MaxStakePerSpot);
                return true;
            case 1:
                label = L.Casino.FactSpots;
                value = "1x  3x  5x  11x  22x";
                return true;
            case 2:
                label = L.Casino.FactRoundCap;
                value = Number(Core.Casino.WheelRules.MaxStakePerRound);
                return true;
            default:
                return false;
        }
    }

    private static bool BingoFact(int index, ref LocString label, ref string value)
    {
        switch (index)
        {
            case 0:
                label = L.Casino.FactCardPrice;
                value = Number(Core.Casino.BingoRules.CardPrice);
                return true;
            case 1:
                label = L.Casino.FactMaxCards;
                value = Number(Core.Casino.BingoRules.MaxCards);
                return true;
            case 2:
                label = L.Casino.FactPrizeStages;
                value = Loc.T(L.Casino.FactPrizeStagesValue);
                return true;
            default:
                return false;
        }
    }

    private static bool BlackjackFact(int index, ref LocString label, ref string value)
    {
        switch (index)
        {
            case 0:
                label = L.Casino.FactBetRange;
                value = Range(Core.Casino.BlackjackRules.HouseFloor, Core.Casino.BlackjackRules.HouseTop);
                return true;
            case 1:
                label = L.Casino.FactDecks;
                value = Number(Core.Casino.BlackjackRules.Decks);
                return true;
            case 2:
                label = L.Casino.FactHouseRules;
                value = Loc.T(L.Casino.FactHouseRulesValue);
                return true;
            case 3:
                label = L.Casino.FactNotOffered;
                value = Loc.T(L.Casino.FactNotOfferedValue);
                return true;
            default:
                return false;
        }
    }

    private static bool BarkeepFact(int index, ref LocString label, ref string value)
    {
        switch (index)
        {
            case 0:
                label = L.Casino.FactEntry;
                value = Number(Core.Casino.BarkeepRules.EntryChips);
                return true;
            case 1:
                label = L.Casino.FactSkill;
                value = Loc.T(L.Casino.FactSkillValue);
                return true;
            default:
                return false;
        }
    }

    private static bool RaceFact(int index, ref LocString label, ref string value)
    {
        switch (index)
        {
            case 0:
                label = L.Race.FactMinBet;
                value = Number(Core.Casino.RaceRules.MinBet);
                return true;
            case 1:
                label = L.Race.FactTickets;
                value = Number(Core.Casino.RaceRules.MaxTickets);
                return true;
            default:
                return false;
        }
    }

    private static string Number(long value)
    {
        return NumberText.Group(value);
    }

    private static string Range(long low, long high)
    {
        return string.Concat(Number(low), " - ", Number(high));
    }
}
