using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Casino;

internal static class CasinoRules
{
    private static readonly LocString[] NoSteps = Array.Empty<LocString>();

    private static readonly LocString[] BirdSteps =
    {
        L.Machines.RulesBirdStep1,
        L.Machines.RulesBirdStep2,
        L.Machines.RulesBirdStep3,
        L.Machines.RulesBirdStep4,
    };

    private static readonly LocString[] CascadeSteps =
    {
        L.Machines.RulesCascadeStep1,
        L.Machines.RulesCascadeStep2,
        L.Machines.RulesCascadeStep3,
        L.Machines.RulesCascadeStep4,
    };

    private static readonly LocString[] MoogleSteps =
    {
        L.Machines.RulesMoogleStep1,
        L.Machines.RulesMoogleStep2,
        L.Machines.RulesMoogleStep3,
        L.Machines.RulesMoogleStep4,
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
        L.Blackjack.RulesSideBets,
        L.Blackjack.RulesInsurance,
        L.Blackjack.RulesSurrender,
    };

    private static readonly LocString[] HoldemSteps =
    {
        L.Holdem.RulesStep1,
        L.Holdem.RulesStep2,
        L.Holdem.RulesStep3,
        L.Holdem.RulesStep4,
        L.Holdem.RulesStep5,
    };

    private static readonly LocString[] DealerHoldemSteps =
    {
        L.DealerHoldem.RulesStep1,
        L.DealerHoldem.RulesStep2,
        L.DealerHoldem.RulesStep3,
        L.DealerHoldem.RulesStep4,
        L.DealerHoldem.RulesStep5,
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

    private static readonly LocString[] PlinkoSteps =
    {
        L.Plinko.RulesStep1,
        L.Plinko.RulesStep2,
        L.Plinko.RulesStep3,
        L.Plinko.RulesStep4,
    };

    private const int RangeFact = 0;
    private const int TimesBetFact = 1;
    private const int OneInFact = 2;

    private static readonly Dictionary<FactKey, string> FactTexts = new();

    private static string plinkoReturn = string.Empty;
    private static LanguageInfo? plinkoReturnLanguage;
    private static LanguageInfo? factLanguage;

    private readonly record struct FactKey(int Kind, long First, long Second);

    public static LocString PitchOf(string gameId) => gameId switch
    {
        CasinoGames.Mines => L.Originals.PitchMines,
        CasinoGames.Dice => L.Originals.PitchDice,
        CasinoGames.Limbo => L.Originals.PitchLimbo,
        CasinoGames.Keno => L.Originals.PitchKeno,
        CasinoGames.HiLo => L.Originals.PitchHiLo,
        CasinoGames.Race => L.Race.Pitch,
        CasinoGames.Plinko => L.Plinko.Pitch,
        CasinoGames.Slots or CasinoGames.SlotsBird => L.Machines.PitchBird,
        CasinoGames.SlotsCascade => L.Machines.PitchCascade,
        CasinoGames.SlotsMoogle => L.Machines.PitchMoogle,
        CasinoGames.Scratch => L.Casino.PitchScratch,
        CasinoGames.Wheel => L.Casino.PitchWheel,
        CasinoGames.Bingo => L.Casino.PitchBingo,
        CasinoGames.Blackjack => L.Casino.PitchBlackjack,
        CasinoGames.Holdem => L.Holdem.Pitch,
        CasinoGames.DealerHoldem => L.DealerHoldem.Pitch,
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
        CasinoGames.Plinko => L.Plinko.Game,
        CasinoGames.Slots or CasinoGames.SlotsBird => L.Machines.GameBird,
        CasinoGames.SlotsCascade => L.Machines.GameCascade,
        CasinoGames.SlotsMoogle => L.Machines.GameMoogle,
        CasinoGames.Scratch => L.Casino.GameScratch,
        CasinoGames.Wheel => L.Casino.GameWheel,
        CasinoGames.Bingo => L.Casino.GameBingo,
        CasinoGames.Blackjack => L.Casino.GameBlackjack,
        CasinoGames.Holdem => L.Casino.GameHoldem,
        CasinoGames.DealerHoldem => L.DealerHoldem.Game,
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
        CasinoGames.Plinko => PlinkoSteps,
        CasinoGames.Slots or CasinoGames.SlotsBird => BirdSteps,
        CasinoGames.SlotsCascade => CascadeSteps,
        CasinoGames.SlotsMoogle => MoogleSteps,
        CasinoGames.Scratch => ScratchSteps,
        CasinoGames.Wheel => WheelSteps,
        CasinoGames.Bingo => BingoSteps,
        CasinoGames.Blackjack => BlackjackSteps,
        CasinoGames.Holdem => HoldemSteps,
        CasinoGames.DealerHoldem => DealerHoldemSteps,
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
            case CasinoGames.SlotsBird:
                return MachineFact(Core.Casino.SlotsMachines.Bird, index, ref label, ref value);
            case CasinoGames.SlotsCascade:
                return MachineFact(Core.Casino.SlotsMachines.Cascade, index, ref label, ref value);
            case CasinoGames.SlotsMoogle:
                return MachineFact(Core.Casino.SlotsMachines.Moogle, index, ref label, ref value);
            case CasinoGames.Scratch:
                return ScratchFact(index, ref label, ref value);
            case CasinoGames.Wheel:
                return WheelFact(index, ref label, ref value);
            case CasinoGames.Bingo:
                return BingoFact(index, ref label, ref value);
            case CasinoGames.Blackjack:
                return BlackjackFact(index, ref label, ref value);
            case CasinoGames.Holdem:
                return HoldemFact(index, ref label, ref value);
            case CasinoGames.DealerHoldem:
                return DealerHoldemFact(index, ref label, ref value);
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
            case CasinoGames.Race:
                return RaceFact(index, ref label, ref value);
            case CasinoGames.Plinko:
                return PlinkoFact(index, ref label, ref value);
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
            default:
                return false;
        }
    }

    private static bool PlinkoFact(int index, ref LocString label, ref string value)
    {
        switch (index)
        {
            case 0:
                label = L.Originals.FactMinBet;
                value = Number(Core.Casino.PlinkoRules.MinBet);
                return true;
            case 1:
                label = L.Plinko.FactRows;
                value = Loc.T(L.Plinko.FactRowsValue);
                return true;
            case 2:
                label = L.Originals.FactTopMultiplier;
                value = Stage.CasinoMultiples.Label(Core.Casino.PlinkoRules.TopTenths
                    * Core.Casino.PlinkoRules.TenthsPerMultiple);
                return true;
            case 3:
                label = L.Strip.Return;
                value = PlinkoReturnRange();
                return true;
            default:
                return false;
        }
    }

    private static string PlinkoReturnRange()
    {
        if (ReferenceEquals(plinkoReturnLanguage, Loc.Current))
        {
            return plinkoReturn;
        }

        var lowest = int.MaxValue;
        var highest = 0;
        for (var rowsIndex = 0; rowsIndex < Core.Casino.PlinkoRules.RowCounts.Length; rowsIndex++)
        {
            for (var risk = 0; risk < Core.Casino.PlinkoRules.RiskCount; risk++)
            {
                var basisPoints = Core.Casino.PlinkoRules.ReturnBasisPoints(Core.Casino.PlinkoRules.RowCounts[rowsIndex],
                    risk);
                lowest = Math.Min(lowest, basisPoints);
                highest = Math.Max(highest, basisPoints);
            }
        }

        plinkoReturnLanguage = Loc.Current;
        plinkoReturn = Loc.T(L.Plinko.ReturnRange, Originals.OriginalsText.Percent(lowest),
            Originals.OriginalsText.Percent(highest));
        return plinkoReturn;
    }

    private static bool MachineFact(Core.Casino.SlotsMachineInfo info, int index, ref LocString label,
        ref string value)
    {
        switch (index)
        {
            case 0:
                label = L.Originals.FactMinBet;
                value = Number(Core.Casino.SlotsRules.MinStake);
                return true;
            case 1:
                label = L.Strip.Return;
                value = Originals.OriginalsText.Percent(info.ReturnBasisPoints);
                return true;
            case 2:
                label = L.Machines.MaxWin;
                value = Fact(TimesBetFact, info.MaxWinMultiple, 0);
                return true;
            case 3:
                label = info.Layout == Core.Casino.SlotsLayout.Hold ? L.Machines.HoldFrequency
                    : L.Machines.BonusFrequency;
                value = Fact(OneInFact, info.BonusOneIn, 0);
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
                value = "1x  3x  5x  11x  23x";
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

    private static bool HoldemFact(int index, ref LocString label, ref string value)
    {
        switch (index)
        {
            case 0:
                label = L.Holdem.FactBlinds;
                value = Range(Core.Casino.HoldemRules.BigBlindFor(Core.Casino.HoldemRules.LowTier),
                    Core.Casino.HoldemRules.BigBlindFor(Core.Casino.HoldemRules.RoyalTier));
                return true;
            case 1:
                label = L.Holdem.FactBuyIn;
                value = Loc.T(L.Holdem.FactBuyInValue);
                return true;
            case 2:
                label = L.Holdem.FactRake;
                value = Loc.T(L.Holdem.FactRakeValue);
                return true;
            case 3:
                label = L.Holdem.FactClock;
                value = Loc.T(L.Holdem.FactClockValue);
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
                label = L.Blackjack.FactSideBets;
                value = Loc.T(L.Blackjack.FactSideBetsValue);
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

    private static bool DealerHoldemFact(int index, ref LocString label, ref string value)
    {
        switch (index)
        {
            case 0:
                label = L.DealerHoldem.FactMinAnte;
                value = Number(Core.Casino.DealerHoldemRules.MinAnte);
                return true;
            case 1:
                label = L.DealerHoldem.FactQualify;
                value = Loc.T(L.DealerHoldem.FactQualifyValue);
                return true;
            case 2:
                label = L.DealerHoldem.FactTopPay;
                value = DealerHoldem.DealerHoldemTexts.TopPay();
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

    private static string Range(long low, long high) => Fact(RangeFact, low, high);

    private static string Fact(int kind, long first, long second)
    {
        if (!ReferenceEquals(factLanguage, Loc.Current))
        {
            factLanguage = Loc.Current;
            FactTexts.Clear();
        }

        var key = new FactKey(kind, first, second);
        if (FactTexts.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var text = kind switch
        {
            TimesBetFact => Loc.T(L.Machines.TimesBet, Number(first)),
            OneInFact => Loc.T(L.Machines.OneIn, Number(first)),
            _ => string.Concat(Number(first), " - ", Number(second)),
        };
        FactTexts[key] = text;
        return text;
    }
}
